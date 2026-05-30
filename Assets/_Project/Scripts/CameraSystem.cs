using UnityEngine;
using Unity.Cinemachine;
using System.Collections;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class CameraSystem : MonoBehaviour
{
    [Header("Virtual Cameras")]
    [SerializeField] private CinemachineCamera tppCamera;
    [SerializeField] private CinemachineCamera fppCamera;

    [Header("Player Renderers to Hide in FPP")]
    [SerializeField] private Renderer[] playerRenderers;

    [Header("Settings")]
    [SerializeField] private int activePriority = 10;
    [SerializeField] private int inactivePriority = 0;
    [SerializeField] private bool autoConfigureCinemachineBrain = true;
    [SerializeField] private CinemachineBrain.UpdateMethods brainUpdateMethod = CinemachineBrain.UpdateMethods.SmartUpdate;
    [SerializeField] private CinemachineBrain.BrainUpdateMethods blendUpdateMethod = CinemachineBrain.BrainUpdateMethods.LateUpdate;

    [Header("Look Input")]
    [SerializeField] private bool enableDesktopMouseLook = true;
    [SerializeField] [Range(0.1f, 6f)] private float mouseLookSensitivity = 1f;
    [SerializeField] private float lookSmoothing = 10f;
    [SerializeField] private float fppPitchMin = -60f;
    [SerializeField] private float fppPitchMax = 60f;
    [SerializeField] private float transitionDuration = 0.25f;
    [SerializeField] private Transform playerRoot;

    [Header("Startup Cinematic")]
    [SerializeField] private bool playStartupCinematic = true;
    [SerializeField] private float startupCinematicDuration = 1.35f;
    [SerializeField] [Range(1f, 2f)] private float startupDistanceMultiplier = 1.24f;
    [SerializeField] [Range(0f, 0.4f)] private float startupSideOffset = 0.22f;
    [SerializeField] [Range(0f, 18f)] private float startupFovBoost = 7.5f;

    [Header("Dialogue Zoom")]
    [SerializeField] private float dialogueDistanceMultiplier = 0.8f;
    [SerializeField] private float dialogueFovDelta = 10f;
    [SerializeField] private float dialogueZoomDuration = 0.3f;

    [Header("Dialogue Cinematic Drift")]
    [SerializeField] private bool enableDialogueCinematicDrift = false;
    [SerializeField] [Range(0f, 0.4f)] private float dialogueDriftSideOffset = 0.18f;
    [SerializeField] [Range(0f, 0.2f)] private float dialogueDriftDistanceOffset = 0.1f;
    [SerializeField] private float dialogueDriftDuration = 1.4f;
    [SerializeField] [Range(1, 4)] private int dialogueDriftLoops = 2;

    private bool isFirstPerson = false;
    private CinemachineBrain brain;
    private CinemachineThirdPersonFollow tppFollow;
    private CinemachineOrbitalFollow orbitalFollow;
    private CinemachinePanTilt fppPanTilt;
    private CinemachineInputAxisController tppInputController;
    private CinemachineInputAxisController fppInputController;
    private bool hasZoomState;
    private bool isDialogueZoomed;
    private float defaultTppDistance;
    private float defaultTppSide;
    private float defaultTppFov;
    private float defaultFppFov;
    private float lastTppYaw;
    private float lastTppPitch;
    private Vector2 smoothedLookDelta;
    private Coroutine dialogueZoomRoutine;
    private Coroutine dialogueDriftRoutine;
    private Coroutine startupCinematicRoutine;
    private Coroutine transitionRoutine;

    public bool IsFirstPerson => isFirstPerson;

    void Awake()
    {
        if (tppCamera != null)
        {
            tppFollow = tppCamera.GetComponent<CinemachineThirdPersonFollow>();
            orbitalFollow = tppCamera.GetComponent<CinemachineOrbitalFollow>();
            tppInputController = tppCamera.GetComponent<CinemachineInputAxisController>();
        }

        if (fppCamera != null)
        {
            fppPanTilt = fppCamera.GetComponent<CinemachinePanTilt>();
            fppInputController = fppCamera.GetComponent<CinemachineInputAxisController>();
        }

        if (Camera.main != null)
            brain = Camera.main.GetComponent<CinemachineBrain>();

        if (brain == null)
            brain = FindFirstObjectByType<CinemachineBrain>();

        CacheDialogueZoomDefaults();

        ApplyBrainUpdateMode();
    }

    void Start()
    {
        // Auto-find all renderers on Player if not assigned
        if (playerRenderers == null || playerRenderers.Length == 0)
        {
            GameObject player = GameObject.FindWithTag("Player");
            if (player != null)
                playerRenderers = player.GetComponentsInChildren<Renderer>();
        }

        if (playerRoot == null)
        {
            GameObject player = GameObject.FindWithTag("Player");
            if (player == null)
            {
                PlayerController pc = FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);
                if (pc != null) player = pc.gameObject;
            }
            if (player != null)
            {
                player.SetActive(true);
                playerRoot = player.transform;
            }
        }

        ConfigureCameraTargets();

        ApplyBrainUpdateMode();
        SetTPP();
        StartCoroutine(ResetInitialPitchNextFrame());

        if (playStartupCinematic)
            StartStartupCinematic();
    }

    private IEnumerator ResetInitialPitchNextFrame()
    {
        yield return null;
        yield return null;
        ResetInitialPitch();
    }

    private void ResetInitialPitch()
    {
        if (orbitalFollow != null)
        {
            InputAxis pitch = orbitalFollow.VerticalAxis;
            pitch.Value = -10f;
            orbitalFollow.VerticalAxis = pitch;
            lastTppPitch = pitch.Value;
        }
        else if (tppCamera != null)
        {
            Vector3 euler = tppCamera.transform.localEulerAngles;
            tppCamera.transform.localEulerAngles = new Vector3(0f, euler.y, euler.z);
        }

        if (fppPanTilt != null)
        {
            InputAxis tilt = fppPanTilt.TiltAxis;
            tilt.Value = 0f;
            fppPanTilt.TiltAxis = tilt;
        }
    }



    private void OnEnable()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        StopDialogueDrift();
        StopStartupCinematic();
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        if (scene.name != "SampleScene") return;
        StartCoroutine(ReassignPlayerAfterLoad());
    }

    private IEnumerator ReassignPlayerAfterLoad()
    {
        yield return new WaitForSeconds(0.5f);
        PlayerController pc = FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);
        if (pc != null)
        {
            pc.gameObject.SetActive(true);
            playerRoot = pc.transform;
            playerRenderers = pc.GetComponentsInChildren<Renderer>();
            ConfigureCameraTargets();
            SetTPP();
        }
    }

    private void ConfigureCameraTargets()
    {
        if (playerRoot == null)
            return;

        Transform anchor = playerRoot.Find("FPP_Anchor")
            ?? playerRoot.Find("CameraTarget")
            ?? playerRoot;

        if (tppCamera != null)
        {
            var target = tppCamera.Target;
            target.TrackingTarget = anchor;
            target.LookAtTarget = anchor;
            tppCamera.Target = target;
        }

        if (fppCamera != null)
        {
            var target = fppCamera.Target;
            target.TrackingTarget = anchor;
            target.LookAtTarget = anchor;
            fppCamera.Target = target;
        }

        if (orbitalFollow != null && anchor == playerRoot)
        {
            Vector3 offset = orbitalFollow.TargetOffset;
            offset.y = Mathf.Max(offset.y, 1.6f);
            orbitalFollow.TargetOffset = offset;
        }
    }

    void Update()
    {
#if ENABLE_INPUT_SYSTEM
        var keyboard = Keyboard.current;
        bool togglePressed = keyboard != null
            && (keyboard.fKey.wasPressedThisFrame || keyboard.vKey.wasPressedThisFrame);
        bool escapePressed = keyboard != null && keyboard.escapeKey.wasPressedThisFrame;
#else
        bool togglePressed = Input.GetKeyDown(KeyCode.F) || Input.GetKeyDown(KeyCode.V);
        bool escapePressed = Input.GetKeyDown(KeyCode.Escape);
#endif

        if (togglePressed)
            ToggleCamera();

        if (escapePressed && !isFirstPerson)
            UnlockCursor();

        if (enableDesktopMouseLook && !IsTouchInputActive())
            HandleDesktopMouseLook();
    }

    public void TogglePerspectiveFromMobile()
    {
        ToggleCamera();
    }

    public void SetPerspectiveFromMobile(bool firstPerson)
    {
        if (firstPerson)
        {
            if (!isFirstPerson)
                SetFPP();
            return;
        }

        if (isFirstPerson)
            SetTPP();
    }

    void ToggleCamera()
    {
        isFirstPerson = !isFirstPerson;
        if (isFirstPerson) SetFPP();
        else SetTPP();
    }

    void SetTPP()
    {
        tppCamera.Priority = activePriority;
        fppCamera.Priority = inactivePriority;
        isFirstPerson = false;
        SetPlayerRenderersVisible(true);
        UnlockCursor();

        smoothedLookDelta = Vector2.zero;
        SetInputControllersEnabled(tppEnabled: true, fppEnabled: false);
        StartTransition(TransitionDirection.ToTPP);
    }

    void SetFPP()
    {
        tppCamera.Priority = inactivePriority;
        fppCamera.Priority = activePriority;
        isFirstPerson = true;
        SetPlayerRenderersVisible(false);
        LockCursor();

        smoothedLookDelta = Vector2.zero;
        SetInputControllersEnabled(tppEnabled: false, fppEnabled: false);
        StartTransition(TransitionDirection.ToFPP);
    }

    public void AddLookInput(Vector2 lookDelta, float sensitivity)
    {
        if (isFirstPerson)
        {
            Vector2 scaled = lookDelta * Mathf.Max(0.001f, sensitivity);
            if (scaled.sqrMagnitude <= 0.000001f)
                return;

            ApplyFppLookInput(scaled.x, scaled.y);
            return;
        }

        float dt = Time.unscaledDeltaTime;
        float scale = Mathf.Max(0.01f, sensitivity) * dt;
        Vector2 scaledTpp = lookDelta * scale;
        float t = lookSmoothing <= 0f ? 1f : 1f - Mathf.Exp(-lookSmoothing * dt);
        smoothedLookDelta = Vector2.Lerp(smoothedLookDelta, scaledTpp, t);
        ApplyTppLookInput(smoothedLookDelta.x, smoothedLookDelta.y);
    }

    private void ApplyFppLookInput(float yawDelta, float pitchDelta)
    {
        if (playerRoot != null && Mathf.Abs(yawDelta) > 0.000001f)
            playerRoot.Rotate(Vector3.up, yawDelta, Space.World);

        if (fppPanTilt == null)
            return;

        InputAxis tilt = fppPanTilt.TiltAxis;
        tilt.Value = Mathf.Clamp(tilt.Value - pitchDelta, fppPitchMin, fppPitchMax);
        fppPanTilt.TiltAxis = tilt;

        InputAxis pan = fppPanTilt.PanAxis;
        pan.Value = 0f;
        fppPanTilt.PanAxis = pan;
    }

    private void ApplyTppLookInput(float yawDelta, float pitchDelta)
    {
        if (orbitalFollow == null)
            return;

        InputAxis yaw = orbitalFollow.HorizontalAxis;
        InputAxis pitch = orbitalFollow.VerticalAxis;

        yaw.Value += yawDelta;
        pitch.Value += -pitchDelta;

        orbitalFollow.HorizontalAxis = yaw;
        orbitalFollow.VerticalAxis = pitch;

        lastTppYaw = yaw.Value;
        lastTppPitch = pitch.Value;
    }

    private void HandleDesktopMouseLook()
    {
#if !UNITY_ANDROID || UNITY_EDITOR
#if ENABLE_INPUT_SYSTEM
        var mouse = Mouse.current;
        if (mouse == null)
            return;

        if (Cursor.lockState != CursorLockMode.Locked && !mouse.rightButton.isPressed)
            return;

        Vector2 delta = mouse.delta.ReadValue();
        float mouseX = delta.x;
        float mouseY = delta.y;
#else
        if (Cursor.lockState != CursorLockMode.Locked && !Input.GetMouseButton(1))
            return;

        float mouseX = Input.GetAxis("Mouse X");
        float mouseY = Input.GetAxis("Mouse Y");
#endif

        if (Mathf.Abs(mouseX) < 0.0001f && Mathf.Abs(mouseY) < 0.0001f)
            return;

        Vector2 lookDelta = new Vector2(mouseX, mouseY) * mouseLookSensitivity;
        AddLookInput(lookDelta, 1f);
#endif
    }

    private bool IsTouchInputActive()
    {
        if (Application.isMobilePlatform)
            return true;

        if (MobileInputController.Instance != null && MobileInputController.Instance.IsTouchUiEnabled)
#if ENABLE_INPUT_SYSTEM
            return Touchscreen.current != null && Touchscreen.current.touches.Count > 0;
#else
            return Input.touchCount > 0;
#endif

        return false;
    }

    private void SetInputControllersEnabled(bool tppEnabled, bool fppEnabled)
    {
        if (tppInputController != null)
            tppInputController.enabled = tppEnabled;

        if (fppInputController != null)
            fppInputController.enabled = fppEnabled;
    }

    private void StartTransition(TransitionDirection direction)
    {
        if (transitionRoutine != null)
            StopCoroutine(transitionRoutine);

        transitionRoutine = StartCoroutine(TransitionRoutine(direction));
    }

    private IEnumerator TransitionRoutine(TransitionDirection direction)
    {
        float duration = Mathf.Max(0.01f, transitionDuration);
        float elapsed = 0f;

        float startYaw = orbitalFollow != null ? orbitalFollow.HorizontalAxis.Value : 0f;
        float startPitch = orbitalFollow != null ? orbitalFollow.VerticalAxis.Value : 0f;
        float targetYaw = direction == TransitionDirection.ToFPP ? startYaw : lastTppYaw;
        float targetPitch = direction == TransitionDirection.ToFPP ? startPitch : lastTppPitch;

        float startTilt = fppPanTilt != null ? fppPanTilt.TiltAxis.Value : 0f;
        float targetTilt = Mathf.Clamp(startTilt, fppPitchMin, fppPitchMax);

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = Mathf.SmoothStep(0f, 1f, t);

            if (direction == TransitionDirection.ToTPP && orbitalFollow != null)
            {
                InputAxis yaw = orbitalFollow.HorizontalAxis;
                InputAxis pitch = orbitalFollow.VerticalAxis;
                yaw.Value = Mathf.Lerp(startYaw, targetYaw, eased);
                pitch.Value = Mathf.Lerp(startPitch, targetPitch, eased);
                orbitalFollow.HorizontalAxis = yaw;
                orbitalFollow.VerticalAxis = pitch;
            }

            if (direction == TransitionDirection.ToFPP && fppPanTilt != null)
            {
                InputAxis tilt = fppPanTilt.TiltAxis;
                tilt.Value = Mathf.Lerp(startTilt, targetTilt, eased);
                fppPanTilt.TiltAxis = tilt;
            }

            yield return null;
        }

        transitionRoutine = null;
    }

    private enum TransitionDirection
    {
        ToFPP,
        ToTPP
    }

    void SetPlayerRenderersVisible(bool visible)
    {
        if (playerRenderers == null) return;
        foreach (Renderer r in playerRenderers)
        {
            if (r != null) r.enabled = visible;
        }
    }

    void LockCursor()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void UnlockCursor()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void ApplyBrainUpdateMode()
    {
        if (!autoConfigureCinemachineBrain || brain == null)
            return;

        if (brain.UpdateMethod != brainUpdateMethod)
            brain.UpdateMethod = brainUpdateMethod;

        if (brain.BlendUpdateMethod != blendUpdateMethod)
            brain.BlendUpdateMethod = blendUpdateMethod;
    }

    public void DialogueZoomIn()
    {
        StopStartupCinematic();
        CacheDialogueZoomDefaults();
        isDialogueZoomed = true;
        StartDialogueZoomTween(zoomIn: true);
    }

    public void DialogueZoomOut()
    {
        CacheDialogueZoomDefaults();
        isDialogueZoomed = false;
        StopDialogueDrift();
        StartDialogueZoomTween(zoomIn: false);
    }

    private void CacheDialogueZoomDefaults()
    {
        if (hasZoomState)
            return;

        if (tppFollow != null)
        {
            defaultTppDistance = tppFollow.CameraDistance;
            defaultTppSide = tppFollow.CameraSide;
        }
        else if (orbitalFollow != null)
        {
            defaultTppDistance = orbitalFollow.Radius;
            defaultTppSide = orbitalFollow.HorizontalAxis.Value;
        }

        defaultTppDistance = Mathf.Max(0.2f, defaultTppDistance);

        if (tppCamera != null)
            defaultTppFov = tppCamera.Lens.FieldOfView;

        if (fppCamera != null)
            defaultFppFov = fppCamera.Lens.FieldOfView;

        hasZoomState = true;
    }

    private void StartDialogueZoomTween(bool zoomIn)
    {
        if (dialogueZoomRoutine != null)
            StopCoroutine(dialogueZoomRoutine);

        dialogueZoomRoutine = StartCoroutine(DialogueZoomRoutine(zoomIn));
    }

    private IEnumerator DialogueZoomRoutine(bool zoomIn)
    {
        float startDistance = GetCurrentFollowDistance();
        float targetDistance = defaultTppDistance;

        if (tppFollow != null || orbitalFollow != null)
            targetDistance = zoomIn ? defaultTppDistance * dialogueDistanceMultiplier : defaultTppDistance;

        float startTppFov = tppCamera != null ? tppCamera.Lens.FieldOfView : 0f;
        float startFppFov = fppCamera != null ? fppCamera.Lens.FieldOfView : 0f;

        float targetTppFov = defaultTppFov;
        float targetFppFov = defaultFppFov;

        if (zoomIn)
        {
            targetTppFov = Mathf.Max(20f, defaultTppFov - dialogueFovDelta);
            targetFppFov = Mathf.Max(20f, defaultFppFov - dialogueFovDelta);
        }

        float elapsed = 0f;
        float duration = Mathf.Max(0.01f, dialogueZoomDuration);

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = 1f - Mathf.Pow(1f - t, 3f);

            SetFollowDistance(Mathf.Lerp(startDistance, targetDistance, eased));

            if (tppCamera != null)
                SetCameraFov(tppCamera, Mathf.Lerp(startTppFov, targetTppFov, eased));

            if (fppCamera != null)
                SetCameraFov(fppCamera, Mathf.Lerp(startFppFov, targetFppFov, eased));

            yield return null;
        }

        SetFollowDistance(targetDistance);

        if (tppCamera != null)
            SetCameraFov(tppCamera, targetTppFov);

        if (fppCamera != null)
            SetCameraFov(fppCamera, targetFppFov);

        if (zoomIn)
            StartDialogueDrift(targetDistance);
        else
            StopDialogueDrift();

        dialogueZoomRoutine = null;
    }

    private void StartDialogueDrift(float baseDistance)
    {
        if (!enableDialogueCinematicDrift || (tppFollow == null && orbitalFollow == null))
            return;

        StopDialogueDrift();
        dialogueDriftRoutine = StartCoroutine(DialogueDriftRoutine(baseDistance));
    }

    private void StopDialogueDrift()
    {
        if (dialogueDriftRoutine == null)
            return;

        StopCoroutine(dialogueDriftRoutine);
        dialogueDriftRoutine = null;
    }

    private void StartStartupCinematic()
    {
        if (!playStartupCinematic)
            return;

        StopStartupCinematic();
        startupCinematicRoutine = StartCoroutine(StartupCinematicRoutine());
    }

    private void StopStartupCinematic()
    {
        if (startupCinematicRoutine == null)
            return;

        StopCoroutine(startupCinematicRoutine);
        startupCinematicRoutine = null;
    }

    private IEnumerator StartupCinematicRoutine()
    {
        CacheDialogueZoomDefaults();

        if ((tppFollow == null && orbitalFollow == null) || tppCamera == null)
        {
            startupCinematicRoutine = null;
            yield break;
        }

        float duration = Mathf.Max(0.2f, startupCinematicDuration);
        float startDistance = Mathf.Max(0.2f, defaultTppDistance * startupDistanceMultiplier);
        float sideOffset = GetRigSideOffset(startupSideOffset);
        float startSide = defaultTppSide + sideOffset;
        float startFov = defaultTppFov + startupFovBoost;

        SetFollowDistance(startDistance);
        SetFollowSide(startSide);
        SetCameraFov(tppCamera, startFov);

        if (orbitalFollow != null)
        {
            InputAxis pitch = orbitalFollow.VerticalAxis;
            pitch.Value = 0f;
            orbitalFollow.VerticalAxis = pitch;
            lastTppPitch = pitch.Value;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = 1f - Mathf.Pow(1f - t, 3f);

            float sideSway = Mathf.Sin(t * Mathf.PI * 1.8f) * (sideOffset * 0.2f) * (1f - t);
            SetFollowDistance(Mathf.Lerp(startDistance, defaultTppDistance, eased));
            SetFollowSide(Mathf.Lerp(startSide, defaultTppSide, eased) + sideSway);
            SetCameraFov(tppCamera, Mathf.Lerp(startFov, defaultTppFov, eased));

            yield return null;
        }

        SetFollowDistance(defaultTppDistance);
        SetFollowSide(defaultTppSide);
        SetCameraFov(tppCamera, defaultTppFov);

        startupCinematicRoutine = null;
    }

    private IEnumerator DialogueDriftRoutine(float baseDistance)
    {
        if (tppFollow == null && orbitalFollow == null)
        {
            dialogueDriftRoutine = null;
            yield break;
        }

        float cycleDuration = Mathf.Max(0.2f, dialogueDriftDuration);
        int loops = Mathf.Max(1, dialogueDriftLoops);
        float totalDuration = cycleDuration * loops;
        float elapsed = 0f;

        float baseSide = defaultTppSide;
        float sideOffsetAmplitude = GetRigSideOffset(dialogueDriftSideOffset);
        float safeDistance = Mathf.Max(0.15f, baseDistance);

        while (elapsed < totalDuration && isDialogueZoomed)
        {
            elapsed += Time.unscaledDeltaTime;

            float phase = (elapsed / cycleDuration) * Mathf.PI * 2f;
            float intro = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / (cycleDuration * 0.35f)));
            float outroStart = totalDuration * 0.72f;
            float outro = Mathf.SmoothStep(1f, 0f, Mathf.Clamp01((elapsed - outroStart) / Mathf.Max(0.01f, totalDuration - outroStart)));
            float envelope = intro * outro;

            float sideOffset = Mathf.Sin(phase) * sideOffsetAmplitude * envelope;
            float distanceOffset = Mathf.Cos(phase * 0.8f) * dialogueDriftDistanceOffset * envelope;

            SetFollowSide(baseSide + sideOffset);
            SetFollowDistance(safeDistance * (1f + distanceOffset));

            yield return null;
        }

        if (isDialogueZoomed)
        {
            SetFollowSide(baseSide);
            SetFollowDistance(safeDistance);
        }

        dialogueDriftRoutine = null;
    }

    private float GetCurrentFollowDistance()
    {
        if (tppFollow != null)
            return tppFollow.CameraDistance;

        if (orbitalFollow != null)
            return orbitalFollow.Radius;

        return defaultTppDistance;
    }

    private void SetFollowDistance(float value)
    {
        float clamped = Mathf.Max(0.15f, value);

        if (tppFollow != null)
            tppFollow.CameraDistance = clamped;

        if (orbitalFollow != null)
            orbitalFollow.Radius = clamped;
    }

    private void SetFollowSide(float value)
    {
        if (tppFollow != null)
            tppFollow.CameraSide = Mathf.Clamp01(value);

        if (orbitalFollow != null)
        {
            InputAxis axis = orbitalFollow.HorizontalAxis;
            axis.Value = value;
            orbitalFollow.HorizontalAxis = axis;
        }
    }

    private float GetRigSideOffset(float normalizedOffset)
    {
        if (tppFollow != null)
            return normalizedOffset;

        if (orbitalFollow != null)
            return normalizedOffset * 60f;

        return 0f;
    }

    private void SetCameraFov(CinemachineCamera cameraRef, float targetFov)
    {
        LensSettings lens = cameraRef.Lens;
        lens.FieldOfView = targetFov;
        cameraRef.Lens = lens;
    }
}
