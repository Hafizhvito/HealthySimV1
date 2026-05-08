using UnityEngine;
using Unity.Cinemachine;
using System.Collections;

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
    private bool hasZoomState;
    private bool isDialogueZoomed;
    private float defaultTppDistance;
    private float defaultTppSide;
    private float defaultTppFov;
    private float defaultFppFov;
    private Coroutine dialogueZoomRoutine;
    private Coroutine dialogueDriftRoutine;
    private Coroutine startupCinematicRoutine;

    public bool IsFirstPerson => isFirstPerson;

    void Awake()
    {
        if (tppCamera != null)
        {
            tppFollow = tppCamera.GetComponent<CinemachineThirdPersonFollow>();
            orbitalFollow = tppCamera.GetComponent<CinemachineOrbitalFollow>();
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

        ApplyBrainUpdateMode();
        SetTPP();

        if (playStartupCinematic)
            StartStartupCinematic();
    }

    private void OnDisable()
    {
        StopDialogueDrift();
        StopStartupCinematic();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.F) || Input.GetKeyDown(KeyCode.V))
            ToggleCamera();

        if (Input.GetKeyDown(KeyCode.Escape) && !isFirstPerson)
            UnlockCursor();
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
    }

    void SetFPP()
    {
        tppCamera.Priority = activePriority;
        fppCamera.Priority = activePriority + 1;
        isFirstPerson = true;
        SetPlayerRenderersVisible(false);
        LockCursor();
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
