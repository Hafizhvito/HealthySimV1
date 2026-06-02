using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

[DefaultExecutionOrder(500)]
public class MobileInputController : MonoBehaviour
{
    public static MobileInputController Instance { get; private set; }

    [SerializeField] private bool forceMobileUI = true;
    [SerializeField] [Range(0.01f, 0.3f)] private float lookSensitivity = 0.06f;
    [SerializeField] [Range(1f, 12f)] private float dualTouchLookGain = 7.5f;
    [SerializeField] private string[] hideInScenes = { "OfficeScene", "GymScene" };
    [SerializeField] [Range(0f, 0.5f)] private float axisDominanceThreshold = 0.35f;
    [SerializeField] private bool debugDualTouchLogging;
    [SerializeField] private bool debugDualTouchOverlay;

    public Vector2 MoveInput { get; private set; }
    public Vector2 LookDelta { get; private set; }

    private const float JoystickRadius = 85f;
    private const float JoystickDeadZone = 0.05f;

    private static readonly Vector2 MoveJoystickAnchor = new Vector2(0f, 0f);
    private static readonly Vector2 MoveJoystickPivot = new Vector2(0f, 0f);
    private static readonly Vector2 MoveJoystickPosition = new Vector2(80f, 100f);
    private static readonly Vector2 MoveJoystickSize = new Vector2(220f, 220f);
    private static readonly Vector2 MoveKnobSize = new Vector2(90f, 90f);

    private static readonly Vector2 SwipeZoneAnchorMin = new Vector2(0.45f, 0f);
    private static readonly Vector2 SwipeZoneAnchorMax = new Vector2(1f, 1f);

    private static readonly Vector2 PerspectiveButtonAnchor = new Vector2(1f, 0f);
    private static readonly Vector2 PerspectiveButtonPivot = new Vector2(1f, 0f);
    private static readonly Vector2 PerspectiveButtonPosition = new Vector2(-80f, 270f);
    private static readonly Vector2 PerspectiveButtonSize = new Vector2(110f, 40f);

    private PlayerController playerController;
    private CameraSystem cameraSystem;
    private Transform cameraSystemTransform;
    private Camera cachedCamera;

    private readonly MobileTouchRouter touchRouter = new MobileTouchRouter();
    private MobileSwipeLookZone lookSwipeZone;

    private GameObject uiRoot;
    private Canvas uiCanvas;
    private MobileMoveStickPresenter moveStickPresenter;
    private TextMeshProUGUI perspectiveToggleLabel;
    [SerializeField] private RectTransform joystickOuter;
    [SerializeField] private RectTransform joystickKnob;
    private bool lastPerspectiveFirstPerson;
    private bool hasPerspectiveState;

    private bool isTouchUiEnabled;
    private bool fallbackPitchInitialized;
    private float fallbackPitch;
    private float dualTouchLogTimer;
    private const float DualTouchLogInterval = 0.25f;

    private GameObject dualTouchOverlayRoot;
    private TextMeshProUGUI dualTouchOverlayText;

    private bool testLatchMergedTwo;
    private bool testLatchJoyActive;
    private bool testLatchSwipeClaimed;
    private bool testLatchJoyAndSwipeTogether;
    private bool testLatchMoveWhileJoy;
    private bool testLatchLookWhileSwipe;
    private bool testLatchReswipeAfterRelease;
    private bool testJoyHeldBaseline;
    private bool testSwipeEndedWhileJoyHeld;
    private bool testPrevSwipeActive;
    private float debugLastTppYaw;
    private bool debugHadTppYaw;
    private bool lastFrameCameraMoved;

    private static Sprite runtimeCircleSprite;
    private static Sprite runtimeRoundedRectSprite;

    public bool IsTouchUiEnabled => isTouchUiEnabled;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
        Input.multiTouchEnabled = true;
    #if ENABLE_INPUT_SYSTEM
        UnityEngine.InputSystem.EnhancedTouch.EnhancedTouchSupport.Enable();
    #endif
        EnsureEventSystemSetup();
        isTouchUiEnabled = ShouldEnableTouchUi();
        cachedCamera = Camera.main;

        ResolveSceneReferences();
        TryBindExistingUi();
        InitializePrefabPresenter();

        if (isTouchUiEnabled)
            BuildUi();
        else
            ResetInputs();

        EnsureDualTouchDebugOverlay();
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        if (dualTouchOverlayRoot != null && dualTouchOverlayRoot.transform.parent == transform)
            Destroy(dualTouchOverlayRoot);
    }

    private static void EnsureEventSystemSetup()
    {
        EventSystem eventSystem = FindFirstObjectByType<EventSystem>();
        if (eventSystem == null)
            eventSystem = FindFirstObjectByType<EventSystem>(FindObjectsInactive.Include);

        if (eventSystem == null)
        {
            GameObject eventObj = new GameObject("EventSystem");
            eventSystem = eventObj.AddComponent<EventSystem>();
        }

        StandaloneInputModule standaloneModule = eventSystem.GetComponent<StandaloneInputModule>();
#if ENABLE_INPUT_SYSTEM
        InputSystemUIInputModule inputSystemModule = eventSystem.GetComponent<InputSystemUIInputModule>();
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        Input.multiTouchEnabled = true;
        if (standaloneModule == null)
            standaloneModule = eventSystem.gameObject.AddComponent<StandaloneInputModule>();

        standaloneModule.enabled = true;

#if ENABLE_INPUT_SYSTEM
        if (inputSystemModule != null)
            inputSystemModule.enabled = false;
#endif
#else
        if (standaloneModule != null)
            standaloneModule.enabled = false;

#if ENABLE_INPUT_SYSTEM
        if (inputSystemModule == null)
            inputSystemModule = eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();

        inputSystemModule.enabled = true;
        TryEnableMultiPointer(inputSystemModule);
#endif
#endif

#if ENABLE_INPUT_SYSTEM
        if (Application.isMobilePlatform)
        {
            if (inputSystemModule == null)
                inputSystemModule = eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();

            if (standaloneModule != null)
                standaloneModule.enabled = false;

            inputSystemModule.enabled = true;
            TryEnableMultiPointer(inputSystemModule);
        }
#endif

        eventSystem.sendNavigationEvents = true;
        eventSystem.enabled = true;
        eventSystem.gameObject.SetActive(true);
    }

    void Update()
    {
        if (!isTouchUiEnabled)
            return;


        if (playerController == null || cameraSystemTransform == null)
            ResolveSceneReferences();

        RefreshPerspectiveToggleLabel();

        bool modalOpen = ModalStateManager.Instance != null && ModalStateManager.Instance.IsAnyModalOpen;
        bool cutscenePlaying = IntroCutsceneController.IsAnyCutscenePlaying;
        bool shouldHide = modalOpen || cutscenePlaying;

        SetUiVisible(!shouldHide);

        if (shouldHide)
        {
            ResetMotionInput();
            return;
        }

        touchRouter.Configure(
            SwipeZoneAnchorMin.x,
            JoystickRadius,
            isTouchUiEnabled && !Application.isMobilePlatform);
        touchRouter.Tick();

        Vector2 rawMove = moveStickPresenter != null
            ? moveStickPresenter.SmoothedOutput
            : touchRouter.MoveVector;
        MoveInput = ApplyDeadZone(rawMove);

        Vector2 swipeNormalized = lookSwipeZone != null ? lookSwipeZone.ConsumeOutput() : Vector2.zero;
        LookDelta = swipeNormalized;

        if (playerController != null)
            playerController.InjectMobileInput(MoveInput);

        // Always disable Cinemachine touch input on mobile to prevent Y-sign inconsistency
        // between Cinemachine's path and our manual path.
        if (cameraSystem != null && !cameraSystem.IsFirstPerson)
            cameraSystem.SetTppInputControllerEnabled(false);

        if (cameraSystem != null && !cameraSystem.IsFirstPerson
            && swipeNormalized.sqrMagnitude > 0.0000001f)
        {
            cameraSystem.NotifyManualLook();
            cameraSystem.AddMobileTppLookInput(swipeNormalized, lookSensitivity, dualTouchLookGain);
        }

        bool cameraMovedThisFrame = UpdateCameraMovedLatch();
        if (cameraSystem != null && cameraSystem.IsFirstPerson)
            ApplyLookInput(LookDelta, lookSensitivity);

        UpdateDualTouchTestLatches(cameraMovedThisFrame);
        ResetDualTouchTestLatchesIfIdle();
        UpdateDualTouchDiagnostics();
    }

    private bool UpdateCameraMovedLatch()
    {
        if (cameraSystem != null && cameraSystem.IsFirstPerson)
        {
            lastFrameCameraMoved = LookDelta.sqrMagnitude > 0.000001f;
            return lastFrameCameraMoved;
        }

        if (cameraSystem == null || !cameraSystem.TryGetTppOrbitYaw(out float yaw))
            return false;

        bool moved = false;
        if (debugHadTppYaw)
            moved = Mathf.Abs(Mathf.DeltaAngle(debugLastTppYaw, yaw)) > 0.02f;

        debugLastTppYaw = yaw;
        debugHadTppYaw = true;
        lastFrameCameraMoved = moved;
        return moved;
    }


    private bool ShouldEnableTouchUi()
    {
        return Application.platform == RuntimePlatform.Android
            || (Application.isEditor && forceMobileUI);
    }

    private void ResolveSceneReferences()
    {
        if (playerController == null)
            playerController = FindFirstObjectByType<PlayerController>();

        if (cameraSystem == null)
            cameraSystem = FindFirstObjectByType<CameraSystem>();

        if (cachedCamera == null)
            cachedCamera = Camera.main;

        if (cameraSystemTransform == null)
        {
            GameObject cameraSystemObject = GameObject.Find("CameraSystem");
            if (cameraSystemObject != null)
                cameraSystemTransform = cameraSystemObject.transform;
            else if (cameraSystem != null)
                cameraSystemTransform = cameraSystem.transform;

            if (cameraSystemTransform != null && !fallbackPitchInitialized)
            {
                fallbackPitch = NormalizePitch(cameraSystemTransform.eulerAngles.x);
                fallbackPitchInitialized = true;
            }
        }

        // Camera look input is handled via CameraSystem.
    }

    private void TryBindExistingUi()
    {
        if (uiRoot != null)
            return;

        Transform existingRoot = transform.Find("MobileInputCanvas");
        if (existingRoot == null)
        {
            GameObject hudCanvasObj = GameObject.Find("HUD_Canvas");
            if (hudCanvasObj != null)
                existingRoot = hudCanvasObj.transform.Find("MobileInputCanvas");
        }
        if (existingRoot == null)
            return;

        uiRoot = existingRoot.gameObject;
        EnsureUiRootIsPanel(uiRoot);
        uiCanvas = uiRoot.GetComponent<Canvas>();
        moveStickPresenter = uiRoot.GetComponentInChildren<MobileMoveStickPresenter>(true);
        perspectiveToggleLabel = uiRoot.GetComponentInChildren<TextMeshProUGUI>(true);

        if (joystickOuter == null)
            joystickOuter = uiRoot.transform.Find("MoveJoystickOuter")?.GetComponent<RectTransform>();

        if (joystickKnob == null && joystickOuter != null)
            joystickKnob = joystickOuter.Find("MoveJoystickKnob")?.GetComponent<RectTransform>();

        EnsureLookSwipeZone();
        EnsureDualTouchDebugOverlay();
    }

    private void EnsureUiRootIsPanel(GameObject root)
    {
        if (root == null)
            return;

        RectTransform rect = root.GetComponent<RectTransform>();
        if (rect == null)
            rect = root.AddComponent<RectTransform>();

        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Canvas rootCanvas = root.GetComponent<Canvas>();
        CanvasScaler rootScaler = root.GetComponent<CanvasScaler>();
        GraphicRaycaster rootRaycaster = root.GetComponent<GraphicRaycaster>();
        Canvas parentCanvas = root.transform.parent != null ? root.transform.parent.GetComponentInParent<Canvas>() : null;

        if (rootCanvas != null && parentCanvas != null && parentCanvas != rootCanvas)
        {
            Destroy(rootCanvas);
            if (rootScaler != null)
                Destroy(rootScaler);
            if (rootRaycaster != null)
                Destroy(rootRaycaster);
        }
    }

    private void InitializePrefabPresenter()
    {
        if (joystickOuter == null || joystickKnob == null)
            return;

        ConfigureJoystickCircleImages(joystickOuter);
        DisableJoystickRaycast(joystickOuter);

        moveStickPresenter = joystickOuter.GetComponent<MobileMoveStickPresenter>();
        if (moveStickPresenter == null)
            moveStickPresenter = joystickOuter.gameObject.AddComponent<MobileMoveStickPresenter>();

        moveStickPresenter.Initialize(
            touchRouter,
            joystickOuter,
            joystickKnob,
            uiCanvas,
            GetEventCamera(),
            JoystickRadius,
            MoveJoystickPosition);
    }

    private static void DisableJoystickRaycast(RectTransform outerRect)
    {
        if (outerRect == null)
            return;

        Image image = outerRect.GetComponent<Image>();
        if (image != null)
            image.raycastTarget = false;
    }

    private Camera GetEventCamera()
    {
        if (uiCanvas != null && uiCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
            return uiCanvas.worldCamera != null ? uiCanvas.worldCamera : cachedCamera;
        return null;
    }

#if UNITY_EDITOR
    public void EditorBuildUiForPrefab()
    {
        forceMobileUI = true;
        isTouchUiEnabled = true;

        if (uiRoot != null)
        {
            DestroyImmediate(uiRoot);
            uiRoot = null;
        }

        BuildUi();
    }
#endif

    private void BuildUi()
    {
        if (uiRoot != null)
            return;

        uiRoot = new GameObject("MobileInputCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        uiRoot.transform.SetParent(transform, false);

        uiCanvas = uiRoot.GetComponent<Canvas>();
        uiCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        uiCanvas.sortingOrder = 10;

        CanvasScaler scaler = uiRoot.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        GameObject safeAreaObj = new GameObject("SafeArea", typeof(RectTransform));
        safeAreaObj.transform.SetParent(uiRoot.transform, false);
        RectTransform safeAreaRect = safeAreaObj.GetComponent<RectTransform>();
        safeAreaRect.anchorMin = Vector2.zero;
        safeAreaRect.anchorMax = Vector2.one;
        safeAreaRect.offsetMin = Vector2.zero;
        safeAreaRect.offsetMax = Vector2.zero;
        safeAreaObj.AddComponent<SafeAreaHandler>();

        if (joystickOuter != null && joystickKnob != null)
        {
            ConfigureJoystickCircleImages(joystickOuter);
            DisableJoystickRaycast(joystickOuter);

            moveStickPresenter = joystickOuter.GetComponent<MobileMoveStickPresenter>();
            if (moveStickPresenter == null)
                moveStickPresenter = joystickOuter.gameObject.AddComponent<MobileMoveStickPresenter>();

            moveStickPresenter.Initialize(
                touchRouter,
                joystickOuter,
                joystickKnob,
                uiCanvas,
                GetEventCamera(),
                JoystickRadius,
                MoveJoystickPosition);
        }

        EnsureLookSwipeZone();

        RectTransform perspectiveRect = CreateControlCircle(
            "PerspectiveToggleButton",
            safeAreaObj.transform,
            PerspectiveButtonAnchor,
            PerspectiveButtonAnchor,
            PerspectiveButtonPivot,
            PerspectiveButtonPosition,
            PerspectiveButtonSize,
            new Color(1f, 1f, 1f, 0.13f),
            false,
            true);

        Image perspectiveImage = perspectiveRect.GetComponent<Image>();

        MobilePerspectiveButton perspectiveButton = perspectiveRect.gameObject.AddComponent<MobilePerspectiveButton>();
        perspectiveButton.Initialize(
            this,
            perspectiveRect,
            perspectiveImage,
            new Color(1f, 1f, 1f, 0.13f),
            new Color(1f, 1f, 1f, 0.28f),
            new Vector3(0.94f, 0.94f, 1f),
            0.08f);

        GameObject perspectiveLabelObj = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        perspectiveLabelObj.transform.SetParent(perspectiveRect, false);

        RectTransform perspectiveLabelRect = perspectiveLabelObj.GetComponent<RectTransform>();
        perspectiveLabelRect.anchorMin = new Vector2(0f, 0f);
        perspectiveLabelRect.anchorMax = new Vector2(1f, 1f);
        perspectiveLabelRect.offsetMin = Vector2.zero;
        perspectiveLabelRect.offsetMax = Vector2.zero;

        perspectiveToggleLabel = perspectiveLabelObj.GetComponent<TextMeshProUGUI>();
        perspectiveToggleLabel.text = "FPP";
        perspectiveToggleLabel.alignment = TextAlignmentOptions.Center;
        perspectiveToggleLabel.fontSize = 20f;
        perspectiveToggleLabel.fontStyle = FontStyles.Bold;
        perspectiveToggleLabel.color = new Color(1f, 1f, 1f, 0.75f);
        perspectiveToggleLabel.raycastTarget = false;

        RefreshPerspectiveToggleLabel(force: true);
        EnsureDualTouchDebugOverlay();
    }

    private static RectTransform CreateControlCircle(
        string name,
        Transform parent,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 pivot,
        Vector2 anchoredPos,
        Vector2 size,
        Color color,
        bool circular,
        bool raycastTarget)
    {
        GameObject controlObj = new GameObject(name, typeof(RectTransform), typeof(Image));
        controlObj.transform.SetParent(parent, false);

        RectTransform rect = controlObj.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPos;
        rect.sizeDelta = size;

        Image image = controlObj.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = raycastTarget;

        if (circular)
        {
            image.sprite = GetOrCreateRuntimeCircleSprite();
            image.type = Image.Type.Simple;
        }
        else
        {
            image.sprite = GetOrCreateRuntimeRoundedRectSprite();
            image.type = Image.Type.Sliced;
        }

        return rect;
    }

    private static void ConfigureJoystickCircleImages(RectTransform outerRect)
    {
        if (outerRect == null)
            return;

        Sprite circleSprite = GetOrCreateRuntimeCircleSprite();
        ApplyCircleSprite(outerRect.GetComponent<Image>(), circleSprite);

        RectTransform borderRect = outerRect.Find("MoveJoystickOuterBorder") as RectTransform;
        ApplyCircleSprite(borderRect != null ? borderRect.GetComponent<Image>() : null, circleSprite);

        RectTransform knobRect = outerRect.Find("MoveJoystickKnob") as RectTransform;
        ApplyCircleSprite(knobRect != null ? knobRect.GetComponent<Image>() : null, circleSprite);
    }

    private static void ApplyCircleSprite(Image image, Sprite circleSprite)
    {
        if (image == null || circleSprite == null)
            return;

        image.sprite = circleSprite;
        image.type = Image.Type.Simple;
    }

    private void EnsureLookSwipeZone()
    {
        if (uiRoot == null)
            return;

        Transform zoneTransform = uiRoot.transform.Find("LookSwipeZone");
        if (zoneTransform == null)
        {
            GameObject zoneObj = new GameObject(
                "LookSwipeZone",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(InvisibleTouchGraphic));
            zoneObj.transform.SetParent(uiRoot.transform, false);
            zoneTransform = zoneObj.transform;

            RectTransform rect = zoneObj.GetComponent<RectTransform>();
            rect.anchorMin = SwipeZoneAnchorMin;
            rect.anchorMax = SwipeZoneAnchorMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        InvisibleTouchGraphic graphic = zoneTransform.GetComponent<InvisibleTouchGraphic>();
        if (graphic == null)
            graphic = zoneTransform.gameObject.AddComponent<InvisibleTouchGraphic>();

        graphic.color = Color.clear;
        graphic.raycastTarget = true;

        lookSwipeZone = zoneTransform.GetComponent<MobileSwipeLookZone>();
        if (lookSwipeZone == null)
            lookSwipeZone = zoneTransform.gameObject.AddComponent<MobileSwipeLookZone>();

        lookSwipeZone.Initialize(this);
    }

    private static Sprite GetOrCreateRuntimeCircleSprite()
    {
        if (runtimeCircleSprite != null)
            return runtimeCircleSprite;

        const int size = 256;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.name = "MobileCircleSprite_Runtime";
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;
        texture.hideFlags = HideFlags.HideAndDontSave;

        Color32[] pixels = new Color32[size * size];
        float radius = (size - 1) * 0.5f;
        Vector2 center = new Vector2(radius, radius);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                int index = (y * size) + x;
                float distance = Vector2.Distance(new Vector2(x, y), center);
                float alpha = Mathf.Clamp01(radius - distance + 0.5f);
                byte a = (byte)Mathf.RoundToInt(alpha * 255f);
                pixels[index] = new Color32(255, 255, 255, a);
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, true);

        runtimeCircleSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, size, size),
            new Vector2(0.5f, 0.5f),
            100f,
            0,
            SpriteMeshType.FullRect);

        runtimeCircleSprite.name = "MobileCircleSprite_Runtime";
        return runtimeCircleSprite;
    }

    private static Sprite GetOrCreateRuntimeRoundedRectSprite()
    {
        if (runtimeRoundedRectSprite != null)
            return runtimeRoundedRectSprite;

        const int size = 256;
        const float cornerRadius = 42f;

        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.name = "MobileRoundedRectSprite_Runtime";
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;
        texture.hideFlags = HideFlags.HideAndDontSave;

        Color32[] pixels = new Color32[size * size];
        float half = (size - 1) * 0.5f;
        Vector2 extents = new Vector2(half, half);
        Vector2 innerExtents = extents - new Vector2(cornerRadius, cornerRadius);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                int index = (y * size) + x;

                float px = x - half;
                float py = y - half;

                float dx = Mathf.Max(Mathf.Abs(px) - innerExtents.x, 0f);
                float dy = Mathf.Max(Mathf.Abs(py) - innerExtents.y, 0f);
                float distance = Mathf.Sqrt((dx * dx) + (dy * dy));

                float alpha = Mathf.Clamp01(cornerRadius - distance + 0.75f);
                byte a = (byte)Mathf.RoundToInt(alpha * 255f);
                pixels[index] = new Color32(255, 255, 255, a);
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, true);

        runtimeRoundedRectSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, size, size),
            new Vector2(0.5f, 0.5f),
            100f,
            0,
            SpriteMeshType.FullRect,
            new Vector4(cornerRadius, cornerRadius, cornerRadius, cornerRadius));

        runtimeRoundedRectSprite.name = "MobileRoundedRectSprite_Runtime";
        return runtimeRoundedRectSprite;
    }

    private void ApplyLookInput(Vector2 lookInput, float sensitivity)
    {
        if (lookInput.sqrMagnitude <= 0.000001f)
            return;

        if (cameraSystem == null)
            return;

        cameraSystem.AddLookInput(lookInput, sensitivity);
    }

    private static float NormalizePitch(float xAngle)
    {
        float normalized = xAngle;
        if (normalized > 180f)
            normalized -= 360f;
        return normalized;
    }

    private void SetUiVisible(bool visible)
    {
        if (uiRoot == null)
            return;

        if (uiRoot.activeSelf != visible)
            uiRoot.SetActive(visible);
    }

    private void RefreshPerspectiveToggleLabel(bool force = false)
    {
        if (perspectiveToggleLabel == null || cameraSystem == null)
            return;

        bool firstPerson = cameraSystem.IsFirstPerson;
        if (!force && hasPerspectiveState && firstPerson == lastPerspectiveFirstPerson)
            return;

        hasPerspectiveState = true;
        lastPerspectiveFirstPerson = firstPerson;

        // Show next mode to switch to: when currently FPP, button says TPP, and vice versa.
        perspectiveToggleLabel.text = firstPerson ? "TPP" : "FPP";
    }

    private void ResetInputs()
    {
        MoveInput = Vector2.zero;
        LookDelta = Vector2.zero;
    }

    private void ResetMotionInput()
    {
        MoveInput = Vector2.zero;
        LookDelta = Vector2.zero;

        touchRouter.Reset();

        if (moveStickPresenter != null)
            moveStickPresenter.ForceReset();

        if (lookSwipeZone != null)
            lookSwipeZone.ForceReset();

        if (playerController != null)
            playerController.InjectMobileInput(Vector2.zero);
    }

    private static Vector2 ApplyDeadZone(Vector2 input)
    {
        return input.magnitude < JoystickDeadZone ? Vector2.zero : Vector2.ClampMagnitude(input, 1f);
    }

    private static void TryEnableMultiPointer(InputSystemUIInputModule inputSystemModule)
    {
        if (inputSystemModule == null)
            return;

        var prop = inputSystemModule.GetType().GetProperty("pointerBehavior");
        if (prop == null || !prop.CanWrite)
            return;

        try
        {
            object value = System.Enum.Parse(prop.PropertyType, "AllPointers");
            prop.SetValue(inputSystemModule, value);
        }
        catch
        {
        }
    }

    private void UpdateDualTouchDiagnostics()
    {
        if (!isTouchUiEnabled)
            return;

        if (!debugDualTouchOverlay && !debugDualTouchLogging)
            return;

        string report = BuildDualTouchDiagnosticReport();

        if (debugDualTouchOverlay)
            ApplyDualTouchOverlayText(report);

        if (debugDualTouchLogging)
            LogDualTouchDiagnosticsThrottled(report);
    }

    private void UpdateDualTouchTestLatches(bool cameraMovedThisFrame)
    {
        bool joyActive = touchRouter.HasMoveFinger;
        bool swipeActive = (lookSwipeZone != null && lookSwipeZone.IsDragging) || touchRouter.HasLookFinger;

        if (joyActive && swipeActive)
            testLatchMergedTwo = true;

        if (joyActive)
        {
            testLatchJoyActive = true;
            testJoyHeldBaseline = true;
        }

        if (swipeActive)
            testLatchSwipeClaimed = true;

        if (joyActive && swipeActive)
            testLatchJoyAndSwipeTogether = true;

        if (joyActive && MoveInput.sqrMagnitude > 0.0001f)
            testLatchMoveWhileJoy = true;

        if (cameraMovedThisFrame)
            testLatchLookWhileSwipe = true;

        if (testJoyHeldBaseline && testPrevSwipeActive && !swipeActive)
            testSwipeEndedWhileJoyHeld = true;

        if (testJoyHeldBaseline && testSwipeEndedWhileJoyHeld && swipeActive)
            testLatchReswipeAfterRelease = true;

        testPrevSwipeActive = swipeActive;
    }

    private void ResetDualTouchTestLatchesIfIdle()
    {
        bool swipeActive = lookSwipeZone != null && lookSwipeZone.IsDragging;
        if (touchRouter.ActiveTouchCount > 0 || touchRouter.HasMoveFinger || swipeActive)
            return;

        testLatchMergedTwo = false;
        testLatchJoyActive = false;
        testLatchSwipeClaimed = false;
        testLatchJoyAndSwipeTogether = false;
        testLatchMoveWhileJoy = false;
        testLatchLookWhileSwipe = false;
        testLatchReswipeAfterRelease = false;
        testJoyHeldBaseline = false;
        testSwipeEndedWhileJoyHeld = false;
        testPrevSwipeActive = false;
    }

    private static string TestMark(bool passed)
    {
        return passed ? "[OK]" : "[  ]";
    }

    private string BuildDualTouchTestChecklist()
    {
        bool allPassed = testLatchMergedTwo
            && testLatchJoyActive
            && testLatchSwipeClaimed
            && testLatchJoyAndSwipeTogether
            && testLatchMoveWhileJoy
            && testLatchLookWhileSwipe
            && testLatchReswipeAfterRelease;

        return string.Concat(
            "\n--- Tests (do in order) ---\n",
            TestMark(testLatchMergedTwo), " move + swipe zone drag together\n",
            TestMark(testLatchJoyActive), " Hold left joystick\n",
            TestMark(testLatchSwipeClaimed), " Swipe right side\n",
            TestMark(testLatchJoyAndSwipeTogether), " Joystick + swipe SAME time\n",
            TestMark(testLatchMoveWhileJoy), " Move while joystick held\n",
            TestMark(testLatchLookWhileSwipe), " Camera moves while swiping\n",
            TestMark(testLatchReswipeAfterRelease), " Release swipe, swipe again (joy held)\n",
            allPassed ? "\nALL PASSED" : "\nKeep trying...");
    }

    private string BuildDualTouchDiagnosticReport()
    {
        StringBuilder report = new StringBuilder(256);
        report.Append("Dual-touch router\n");
        touchRouter.AppendDebugReport(report);
        report.Append("\nmoveOut=");
        report.Append(MoveInput.ToString("F2"));
        report.Append(" lookOut=");
        report.Append(LookDelta.ToString("F3"));
        report.Append("\nswipeZone=");
        report.Append(lookSwipeZone != null && lookSwipeZone.IsDragging ? "drag" : "idle");
        bool joyHeld = touchRouter.HasMoveFinger;
        bool swipeDragging = lookSwipeZone != null && lookSwipeZone.IsDragging;
        report.Append(" cmInput=");
        if (joyHeld)
            report.Append(swipeDragging ? "off(dual)" : "off(joy)");
        else
            report.Append("on");
        report.Append(" dualGain=");
        report.Append(dualTouchLookGain.ToString("F1"));
        report.Append("\ndual=");
        bool dual = touchRouter.HasMoveFinger
            && ((lookSwipeZone != null && lookSwipeZone.IsDragging) || touchRouter.HasLookFinger);
        report.Append(dual ? "yes" : "no");
        report.Append("\n");
        report.Append(TestMark(lastFrameCameraMoved));
        report.Append(" LIVE camera moved this frame");

        if (debugDualTouchOverlay)
            report.Append(BuildDualTouchTestChecklist());

        return report.ToString();
    }

    private void LogDualTouchDiagnosticsThrottled(string report)
    {
        bool swipeActive = lookSwipeZone != null && lookSwipeZone.IsDragging;
        if (touchRouter.ActiveTouchCount <= 0 && !touchRouter.HasMoveFinger && !swipeActive)
            return;

        dualTouchLogTimer -= Time.unscaledDeltaTime;
        if (dualTouchLogTimer > 0f)
            return;

        dualTouchLogTimer = DualTouchLogInterval;
        Debug.Log("[DualTouch]\n" + report);
    }

    private void ApplyDualTouchOverlayText(string report)
    {
        EnsureDualTouchDebugOverlay();
        if (dualTouchOverlayText == null)
            return;

        dualTouchOverlayText.text = report;
    }

    private void EnsureDualTouchDebugOverlay()
    {
        if (!debugDualTouchOverlay)
        {
            if (dualTouchOverlayRoot != null)
                dualTouchOverlayRoot.SetActive(false);
            return;
        }

        if (dualTouchOverlayText != null)
        {
            dualTouchOverlayRoot.SetActive(true);
            return;
        }

        Transform existing = transform.Find("DualTouchDebugCanvas");
        if (existing != null)
        {
            dualTouchOverlayRoot = existing.gameObject;
            dualTouchOverlayText = dualTouchOverlayRoot.GetComponentInChildren<TextMeshProUGUI>(true);
            dualTouchOverlayRoot.SetActive(true);
            return;
        }

        dualTouchOverlayRoot = new GameObject(
            "DualTouchDebugCanvas",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler));

        dualTouchOverlayRoot.transform.SetParent(transform, false);

        Canvas overlayCanvas = dualTouchOverlayRoot.GetComponent<Canvas>();
        overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        overlayCanvas.sortingOrder = 100;

        CanvasScaler overlayScaler = dualTouchOverlayRoot.GetComponent<CanvasScaler>();
        overlayScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        overlayScaler.referenceResolution = new Vector2(1920f, 1080f);
        overlayScaler.matchWidthOrHeight = 0.5f;

        GameObject panelObj = new GameObject("Panel", typeof(RectTransform), typeof(Image));
        panelObj.transform.SetParent(dualTouchOverlayRoot.transform, false);

        RectTransform panelRect = panelObj.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0f, 1f);
        panelRect.anchorMax = new Vector2(0f, 1f);
        panelRect.pivot = new Vector2(0f, 1f);
        panelRect.anchoredPosition = new Vector2(12f, -12f);
        panelRect.sizeDelta = new Vector2(560f, 520f);

        Image panelImage = panelObj.GetComponent<Image>();
        panelImage.color = new Color(0f, 0f, 0f, 0.72f);
        panelImage.raycastTarget = false;

        GameObject labelObj = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelObj.transform.SetParent(panelObj.transform, false);

        RectTransform labelRect = labelObj.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(10f, 10f);
        labelRect.offsetMax = new Vector2(-10f, -10f);

        dualTouchOverlayText = labelObj.GetComponent<TextMeshProUGUI>();
        dualTouchOverlayText.fontSize = 18f;
        dualTouchOverlayText.alignment = TextAlignmentOptions.TopLeft;
        dualTouchOverlayText.color = new Color(0.35f, 1f, 0.45f, 1f);
        dualTouchOverlayText.raycastTarget = false;
        dualTouchOverlayText.textWrappingMode = TextWrappingModes.Normal;
        dualTouchOverlayText.text = "Dual-touch debug\n(waiting...)";

        dualTouchOverlayRoot.SetActive(true);
    }

    internal void TogglePerspectivePressed()
    {
        if (cameraSystem == null)
            ResolveSceneReferences();

        if (cameraSystem == null)
            return;

        cameraSystem.TogglePerspectiveFromMobile();
        RefreshPerspectiveToggleLabel(force: true);
    }

    private IEnumerator SpringBack(RectTransform rt, Vector3 pressedScale, Vector3 normalScale, float duration)
    {
        if (rt == null)
            yield break;

        rt.localScale = pressedScale;
        float t = 0f;

        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / Mathf.Max(0.0001f, duration);
            rt.localScale = Vector3.Lerp(pressedScale, normalScale, Mathf.SmoothStep(0f, 1f, t));
            yield return null;
        }

        rt.localScale = normalScale;
    }


    private static Vector2 ApplyAxisDominance(Vector2 delta, float threshold)
    {
        float absX = Mathf.Abs(delta.x);
        float absY = Mathf.Abs(delta.y);
        if (absX > absY && absY < absX * threshold)
            delta.y = 0f;
        else if (absY > absX && absX < absY * threshold)
            delta.x = 0f;
        return delta;
    }

    private sealed class MobileSwipeLookZone : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        private MobileInputController owner;
        private Vector2 swipePrevPosition;
        private bool isSwiping;
        private int swipePointerId = -1;
        private Vector2 pendingLookDelta;

        public bool IsDragging => isSwiping;

        public void Initialize(MobileInputController ownerController)
        {
            owner = ownerController;
            ForceReset();
        }

        public Vector2 ConsumeOutput()
        {
            Vector2 output = pendingLookDelta;
            pendingLookDelta = Vector2.zero;
            return output;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            swipePrevPosition = eventData.position;
            isSwiping = true;
            swipePointerId = eventData.pointerId;
            pendingLookDelta = Vector2.zero;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!isSwiping || eventData.pointerId != swipePointerId)
                return;

            Vector2 rawDelta = eventData.position - swipePrevPosition;
            float screenW = Mathf.Max(1f, Screen.width);
            float screenH = Mathf.Max(1f, Screen.height);
            pendingLookDelta = new Vector2(rawDelta.x / screenW, -rawDelta.y / screenH);
            float threshold = owner != null ? owner.axisDominanceThreshold : 0.35f;
            pendingLookDelta = ApplyAxisDominance(pendingLookDelta, threshold);
            swipePrevPosition = eventData.position;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.pointerId != swipePointerId)
                return;

            ForceReset();
        }

        public void ForceReset()
        {
            isSwiping = false;
            swipePointerId = -1;
            pendingLookDelta = Vector2.zero;
        }
    }

    [RequireComponent(typeof(CanvasRenderer))]
    private sealed class InvisibleTouchGraphic : Graphic
    {
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
        }
    }

    private sealed class MobilePerspectiveButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        private MobileInputController owner;
        private RectTransform buttonRect;
        private Image buttonImage;
        private Color releaseColor;
        private Color pressedColor;
        private Vector3 pressedScale;
        private Vector3 normalScale = Vector3.one;
        private float springDuration;
        private Coroutine releaseRoutine;

        public void Initialize(
            MobileInputController ownerController,
            RectTransform rect,
            Image image,
            Color normalColor,
            Color downColor,
            Vector3 buttonPressedScale,
            float buttonSpringDuration)
        {
            owner = ownerController;
            buttonRect = rect;
            buttonImage = image;
            releaseColor = normalColor;
            pressedColor = downColor;
            pressedScale = buttonPressedScale;
            springDuration = buttonSpringDuration;

            if (buttonRect != null)
                buttonRect.localScale = Vector3.one;

            if (buttonImage != null)
                buttonImage.color = releaseColor;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (releaseRoutine != null)
                StopCoroutine(releaseRoutine);

            if (buttonRect != null)
                buttonRect.localScale = pressedScale;

            if (buttonImage != null)
                buttonImage.color = pressedColor;

            if (owner != null)
                owner.TogglePerspectivePressed();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (releaseRoutine != null)
                StopCoroutine(releaseRoutine);

            releaseRoutine = StartCoroutine(ReleaseFeedbackRoutine());
        }

        private IEnumerator ReleaseFeedbackRoutine()
        {
            Coroutine springRoutine = null;
            if (owner != null && buttonRect != null)
                springRoutine = owner.StartCoroutine(owner.SpringBack(buttonRect, pressedScale, normalScale, springDuration));

            float elapsed = 0f;
            Color startColor = buttonImage != null ? buttonImage.color : releaseColor;

            while (elapsed < springDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / Mathf.Max(0.0001f, springDuration));

                if (buttonImage != null)
                    buttonImage.color = Color.Lerp(startColor, releaseColor, Mathf.SmoothStep(0f, 1f, t));

                yield return null;
            }

            if (buttonImage != null)
                buttonImage.color = releaseColor;

            if (springRoutine == null && buttonRect != null)
                buttonRect.localScale = normalScale;

            releaseRoutine = null;
        }
    }
}
