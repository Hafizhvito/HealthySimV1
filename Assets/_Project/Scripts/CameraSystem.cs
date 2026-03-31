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
    [Header("Dialogue Zoom")]
    [SerializeField] private float dialogueDistanceMultiplier = 0.8f;
    [SerializeField] private float dialogueFovDelta = 10f;
    [SerializeField] private float dialogueZoomDuration = 0.3f;

    private bool isFirstPerson = false;
    private CinemachineBrain brain;
    private CinemachineThirdPersonFollow tppFollow;
    private bool hasZoomState;
    private bool isDialogueZoomed;
    private float defaultTppDistance;
    private float defaultTppFov;
    private float defaultFppFov;
    private Coroutine dialogueZoomRoutine;

    public bool IsFirstPerson => isFirstPerson;

    void Awake()
    {
        if (tppCamera != null)
            tppFollow = tppCamera.GetComponent<CinemachineThirdPersonFollow>();

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
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.F) || Input.GetKeyDown(KeyCode.V))
            ToggleCamera();

        if (Input.GetKeyDown(KeyCode.Escape) && !isFirstPerson)
            UnlockCursor();
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
        CacheDialogueZoomDefaults();
        isDialogueZoomed = true;
        StartDialogueZoomTween(zoomIn: true);
    }

    public void DialogueZoomOut()
    {
        CacheDialogueZoomDefaults();
        isDialogueZoomed = false;
        StartDialogueZoomTween(zoomIn: false);
    }

    private void CacheDialogueZoomDefaults()
    {
        if (hasZoomState)
            return;

        if (tppFollow != null)
            defaultTppDistance = tppFollow.CameraDistance;

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
        float startDistance = tppFollow != null ? tppFollow.CameraDistance : 0f;
        float targetDistance = defaultTppDistance;

        if (tppFollow != null)
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

            if (tppFollow != null)
                tppFollow.CameraDistance = Mathf.Lerp(startDistance, targetDistance, eased);

            if (tppCamera != null)
                SetCameraFov(tppCamera, Mathf.Lerp(startTppFov, targetTppFov, eased));

            if (fppCamera != null)
                SetCameraFov(fppCamera, Mathf.Lerp(startFppFov, targetFppFov, eased));

            yield return null;
        }

        if (tppFollow != null)
            tppFollow.CameraDistance = targetDistance;

        if (tppCamera != null)
            SetCameraFov(tppCamera, targetTppFov);

        if (fppCamera != null)
            SetCameraFov(fppCamera, targetFppFov);

        dialogueZoomRoutine = null;
    }

    private void SetCameraFov(CinemachineCamera cameraRef, float targetFov)
    {
        LensSettings lens = cameraRef.Lens;
        lens.FieldOfView = targetFov;
        cameraRef.Lens = lens;
    }
}
