using System.Collections;
using System.Collections.Generic;
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
    [SerializeField] private string[] hideInScenes = { "OfficeScene", "GymScene" };

    public Vector2 MoveInput { get; private set; }
    public Vector2 LookDelta { get; private set; }

    private const float JoystickRadius = 85f;
    private const float JoystickDeadZone = 0.05f;

    private static readonly Vector2 MoveJoystickAnchor = new Vector2(0f, 0f);
    private static readonly Vector2 MoveJoystickPivot = new Vector2(0f, 0f);
    private static readonly Vector2 MoveJoystickPosition = new Vector2(80f, 60f);
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

    private GameObject uiRoot;
    private Canvas uiCanvas;
    private MobileJoystick moveJoystick;
    private MobileSwipeLookZone lookSwipeZone;
    private TextMeshProUGUI perspectiveToggleLabel;
    [SerializeField] private RectTransform joystickOuter;
    [SerializeField] private RectTransform joystickKnob;
    private bool lastPerspectiveFirstPerson;
    private bool hasPerspectiveState;

    private bool isTouchUiEnabled;
    private bool fallbackPitchInitialized;
    private float fallbackPitch;
    private int leftTouchId = int.MinValue;
    private int rightTouchId = int.MinValue;

    internal struct TouchSample
    {
        public int id;
        public Vector2 position;
        public Vector2 delta;
        public TouchPhase phase;
    }

    internal static readonly List<TouchSample> TouchSamples = new List<TouchSample>(10);

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
        InitializePrefabJoystick();

        if (isTouchUiEnabled)
            BuildUi();
        else
            ResetInputs();
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
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

        GetTouchSamples(); // populate once per frame before children read it

#if !UNITY_EDITOR
        if (moveJoystick != null)
            moveJoystick.ProcessTouches();
        if (lookSwipeZone != null)
            lookSwipeZone.ProcessTouches();
#endif

        MoveInput = moveJoystick != null ? ApplyDeadZone(moveJoystick.Output) : Vector2.zero;
        LookDelta = lookSwipeZone != null ? lookSwipeZone.ConsumeOutput() : Vector2.zero;

        if (playerController != null)
            playerController.InjectMobileInput(MoveInput);

        ApplyLookInput(LookDelta);
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
        moveJoystick = uiRoot.GetComponentInChildren<MobileJoystick>(true);
        lookSwipeZone = uiRoot.GetComponentInChildren<MobileSwipeLookZone>(true);
        perspectiveToggleLabel = uiRoot.GetComponentInChildren<TextMeshProUGUI>(true);

        if (joystickOuter == null)
            joystickOuter = uiRoot.transform.Find("MoveJoystickOuter")?.GetComponent<RectTransform>();

        if (joystickKnob == null && joystickOuter != null)
            joystickKnob = joystickOuter.Find("MoveJoystickKnob")?.GetComponent<RectTransform>();
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

    private void InitializePrefabJoystick()
    {
        if (joystickOuter == null || joystickKnob == null)
            return;

        ConfigureJoystickCircleImages(joystickOuter);

        moveJoystick = joystickOuter.GetComponent<MobileJoystick>();
        if (moveJoystick == null)
            moveJoystick = joystickOuter.gameObject.AddComponent<MobileJoystick>();

        moveJoystick.Initialize(this, joystickOuter, joystickKnob, JoystickRadius, JoystickDeadZone);
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

        if (joystickOuter != null && joystickKnob != null)
        {
            ConfigureJoystickCircleImages(joystickOuter);
            moveJoystick = joystickOuter.GetComponent<MobileJoystick>();
            if (moveJoystick == null)
                moveJoystick = joystickOuter.gameObject.AddComponent<MobileJoystick>();

            moveJoystick.Initialize(this, joystickOuter, joystickKnob, JoystickRadius, JoystickDeadZone);
        }

        RectTransform swipeZoneRect = CreateTouchZone(
            "LookSwipeZone",
            uiRoot.transform,
            SwipeZoneAnchorMin,
            SwipeZoneAnchorMax);
        lookSwipeZone = swipeZoneRect.gameObject.AddComponent<MobileSwipeLookZone>();
        lookSwipeZone.Initialize(this);

        RectTransform perspectiveRect = CreateControlCircle(
            "PerspectiveToggleButton",
            uiRoot.transform,
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

    private static RectTransform CreateTouchZone(
        string name,
        Transform parent,
        Vector2 anchorMin,
        Vector2 anchorMax)
    {
        GameObject zoneObj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(InvisibleTouchGraphic));
        zoneObj.transform.SetParent(parent, false);

        RectTransform rect = zoneObj.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        InvisibleTouchGraphic captureGraphic = zoneObj.GetComponent<InvisibleTouchGraphic>();
        captureGraphic.color = Color.clear;
        captureGraphic.raycastTarget = true;

        return rect;
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

    private void ApplyLookInput(Vector2 lookInput)
    {
        if (lookInput.sqrMagnitude <= 0.000001f)
            return;

        if (cameraSystem == null)
            return;

        cameraSystem.AddLookInput(lookInput, lookSensitivity);
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

        if (moveJoystick != null)
            moveJoystick.ForceReset();

        if (lookSwipeZone != null)
            lookSwipeZone.ForceReset();

        if (playerController != null)
            playerController.InjectMobileInput(Vector2.zero);
    }

    private static Vector2 ApplyDeadZone(Vector2 input)
    {
        return input.magnitude < JoystickDeadZone ? Vector2.zero : Vector2.ClampMagnitude(input, 1f);
    }

    internal static float GetScreenHalfX()
    {
        return Screen.width * Mathf.Clamp01(SwipeZoneAnchorMin.x);
    }

    private bool IsLeftSideTouch(Vector2 screenPosition)
    {
        if (joystickOuter != null)
        {
            Camera eventCamera = null;
            if (uiCanvas != null && uiCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
                eventCamera = uiCanvas.worldCamera != null ? uiCanvas.worldCamera : cachedCamera;

            if (RectTransformUtility.RectangleContainsScreenPoint(joystickOuter, screenPosition, eventCamera))
                return true;
        }

        return screenPosition.x < GetScreenHalfX();
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

    private bool TryClaimLeftTouch(int touchId)
    {
        if (rightTouchId != int.MinValue && rightTouchId == touchId)
            return false;

        if (leftTouchId != int.MinValue && leftTouchId != touchId)
            return false;

        leftTouchId = touchId;
        return true;
    }

    private bool TryClaimRightTouch(int touchId)
    {
        if (leftTouchId != int.MinValue && leftTouchId == touchId)
            return false;

        if (rightTouchId != int.MinValue && rightTouchId != touchId)
            return false;

        rightTouchId = touchId;
        return true;
    }

    private void ReleaseLeftTouch(int pointerId)
    {
        if (leftTouchId == pointerId)
            leftTouchId = int.MinValue;
    }

    private void ReleaseRightTouch(int pointerId)
    {
        if (rightTouchId == pointerId)
            rightTouchId = int.MinValue;
    }

    private bool IsLeftTouchId(int touchId)
    {
        return leftTouchId == touchId;
    }

    private List<TouchSample> GetTouchSamples()
    {
        TouchSamples.Clear();

#if ENABLE_INPUT_SYSTEM
        if (UnityEngine.InputSystem.EnhancedTouch.EnhancedTouchSupport.enabled)
        {
            var touches = UnityEngine.InputSystem.EnhancedTouch.Touch.activeTouches;
            for (int i = 0; i < touches.Count; i++)
            {
                var touch = touches[i];
                TouchSamples.Add(new TouchSample
                {
                    id = touch.touchId,
                    position = touch.screenPosition,
                    delta = touch.delta,
                    phase = ConvertEnhancedPhase(touch.phase)
                });
            }

            return TouchSamples;
        }
#endif

        Touch[] legacyTouches = Input.touches;
        for (int i = 0; i < legacyTouches.Length; i++)
        {
            Touch touch = legacyTouches[i];
            TouchSamples.Add(new TouchSample
            {
                id = touch.fingerId,
                position = touch.position,
                delta = touch.deltaPosition,
                phase = touch.phase
            });
        }

        return TouchSamples;
    }

#if ENABLE_INPUT_SYSTEM
    private static TouchPhase ConvertEnhancedPhase(UnityEngine.InputSystem.TouchPhase phase)
    {
        switch (phase)
        {
            case UnityEngine.InputSystem.TouchPhase.Began:
                return TouchPhase.Began;
            case UnityEngine.InputSystem.TouchPhase.Moved:
                return TouchPhase.Moved;
            case UnityEngine.InputSystem.TouchPhase.Stationary:
                return TouchPhase.Stationary;
            case UnityEngine.InputSystem.TouchPhase.Ended:
                return TouchPhase.Ended;
            case UnityEngine.InputSystem.TouchPhase.Canceled:
                return TouchPhase.Canceled;
            default:
                return TouchPhase.Canceled;
        }
    }
#endif

    internal void SetMoveOutput(Vector2 value)
    {
        MoveInput = ApplyDeadZone(value);
    }

    internal void SetLookOutput(Vector2 value)
    {
        LookDelta = value;
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

    private sealed class MobileJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        private MobileInputController owner;
        private RectTransform baseRect;
        private RectTransform joystickOuterRect;
        private RectTransform knobRect;
        private float maxRadius;
        private int activePointerId = int.MinValue;
        private Vector2 pressLocalOrigin;
        private Vector2 joystickDefaultAnchoredPos;

        private const float ActiveSmoothingSpeed = 22f;
        private const float ReleaseSmoothingSpeed = 32f;
        private const float MicroJitterThreshold = 0.0125f;
        private const float ResponseExponent = 1.35f;

        private Vector2 rawOutput;
        private Vector2 smoothedOutput;

        public Vector2 Output => smoothedOutput;
        public bool IsActive => activePointerId != int.MinValue;

        public void Initialize(
            MobileInputController ownerController,
            RectTransform outerRect,
            RectTransform knob,
            float dragRadius,
            float deadZone)
        {
            owner = ownerController;
            baseRect = transform as RectTransform;
            joystickOuterRect = outerRect;
            knobRect = knob;
            maxRadius = Mathf.Max(1f, dragRadius);
            rawOutput = Vector2.zero;
            smoothedOutput = Vector2.zero;

            if (joystickOuterRect != null)
                joystickDefaultAnchoredPos = joystickOuterRect.anchoredPosition;

            if (knobRect != null)
                knobRect.anchoredPosition = Vector2.zero;

            if (deadZone > 0f)
            {
                // Dead zone handled by controller-level normalization.
            }
        }

        void Update()
        {
            float smoothingSpeed = IsActive ? ActiveSmoothingSpeed : ReleaseSmoothingSpeed;
            float t = 1f - Mathf.Exp(-smoothingSpeed * Time.unscaledDeltaTime);
            smoothedOutput = Vector2.Lerp(smoothedOutput, rawOutput, t);

            if (!IsActive && smoothedOutput.sqrMagnitude < 0.0001f)
                smoothedOutput = Vector2.zero;

            if (owner != null)
                owner.SetMoveOutput(smoothedOutput);
        }

    public void ProcessTouches()
        {
            bool foundActiveFinger = false;
            List<TouchSample> touches = MobileInputController.TouchSamples;

            for (int i = 0; i < touches.Count; i++)
            {
                TouchSample touch = touches[i];
                if (touch.id == activePointerId)
                {
                    foundActiveFinger = true;
                }
                else
                {
                    if (owner == null || !owner.IsLeftSideTouch(touch.position))
                        continue;
                }

                if (activePointerId == int.MinValue
                    && (touch.phase == TouchPhase.Began
                        || touch.phase == TouchPhase.Moved
                        || touch.phase == TouchPhase.Stationary))
                {
                    if (owner != null && owner.TryClaimLeftTouch(touch.id))
                    {
                        activePointerId = touch.id;
                        foundActiveFinger = true;
                        SetPressOrigin(touch.position);
                    }

                    continue;
                }

                if (touch.id != activePointerId)
                    continue;

                if (touch.phase == TouchPhase.Began)
                    SetPressOrigin(touch.position);
                else if (touch.phase == TouchPhase.Moved || touch.phase == TouchPhase.Stationary)
                    UpdateFromScreenPosition(touch.position);
                else if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
                    ForceReset();
            }

            if (activePointerId != int.MinValue && !foundActiveFinger)
                ForceReset();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
#if !UNITY_EDITOR
            return;
#endif
            if (owner == null || !owner.IsLeftSideTouch(eventData.position))
                return;

            if (owner != null && !owner.TryClaimLeftTouch(eventData.pointerId))
                return;

            if (activePointerId != int.MinValue && activePointerId != eventData.pointerId)
                return;

            activePointerId = eventData.pointerId;
            eventData.useDragThreshold = false;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                baseRect,
                eventData.position,
                GetPointerEventCamera(eventData),
                out pressLocalOrigin);

            if (knobRect != null)
                knobRect.anchoredPosition = Vector2.zero;

            rawOutput = Vector2.zero;
        }

        public void OnDrag(PointerEventData eventData)
        {
#if !UNITY_EDITOR
            return;
#endif
            if (eventData.pointerId != activePointerId)
                return;

            UpdatePointerState(eventData);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
#if !UNITY_EDITOR
            return;
#endif
            if (eventData.pointerId != activePointerId)
                return;

            ForceReset();
        }

        void OnDisable()
        {
            ForceReset();
        }

        public void ForceReset()
        {
            int previousPointerId = activePointerId;
            activePointerId = int.MinValue;
            rawOutput = Vector2.zero;
            smoothedOutput = Vector2.zero;

            if (knobRect != null)
                knobRect.anchoredPosition = Vector2.zero;

            if (joystickOuterRect != null)
                joystickOuterRect.anchoredPosition = joystickDefaultAnchoredPos;

            if (owner == null)
                return;

            if (previousPointerId != int.MinValue)
                owner.ReleaseLeftTouch(previousPointerId);

            owner.SetMoveOutput(Vector2.zero);
        }

        private void SetPressOrigin(Vector2 screenPosition)
        {
            if (baseRect == null)
                return;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                baseRect,
                screenPosition,
                GetEventCamera(),
                out pressLocalOrigin);

            if (joystickOuterRect != null && owner?.uiCanvas != null)
            {
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    owner.uiCanvas.transform as RectTransform,
                    screenPosition,
                    owner.uiCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : owner.cachedCamera,
                    out Vector2 canvasLocal);
                joystickOuterRect.anchoredPosition = canvasLocal;
            }

            if (knobRect != null)
                knobRect.anchoredPosition = Vector2.zero;

            rawOutput = Vector2.zero;
        }

        private void UpdateFromScreenPosition(Vector2 screenPosition)
        {
            if (baseRect == null)
                return;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                baseRect,
                screenPosition,
                GetEventCamera(),
                out Vector2 localDragPoint);

            ApplyDragDelta(localDragPoint - pressLocalOrigin);
        }

        private void UpdatePointerState(PointerEventData eventData)
        {
            if (baseRect == null)
                return;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                baseRect,
                eventData.position,
                GetPointerEventCamera(eventData),
                out Vector2 localDragPoint);

            ApplyDragDelta(localDragPoint - pressLocalOrigin);
        }

        private void ApplyDragDelta(Vector2 delta)
        {
            Vector2 clamped = Vector2.ClampMagnitude(delta, maxRadius);

            if (knobRect != null)
                knobRect.anchoredPosition = clamped;

            Vector2 normalized = clamped / maxRadius;
            float magnitude = Mathf.Clamp01(normalized.magnitude);

            if (magnitude <= MicroJitterThreshold)
            {
                rawOutput = Vector2.zero;
                return;
            }

            float curvedMagnitude = Mathf.Pow(magnitude, ResponseExponent);
            rawOutput = normalized.normalized * curvedMagnitude;
        }

        private bool IsWithinJoystickRadius(Vector2 screenPosition)
        {
            if (baseRect == null)
                return false;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                baseRect,
                screenPosition,
                GetEventCamera(),
                out Vector2 localPoint);

            return localPoint.sqrMagnitude <= (maxRadius * maxRadius);
        }

        private Camera GetEventCamera()
        {
            if (owner == null)
                return null;

            Canvas ownerCanvas = owner.uiCanvas;
            if (ownerCanvas != null && ownerCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
                return ownerCanvas.worldCamera != null ? ownerCanvas.worldCamera : owner.cachedCamera;

            return null;
        }

        private Camera GetPointerEventCamera(PointerEventData eventData)
        {
            if (eventData != null && eventData.pressEventCamera != null)
                return eventData.pressEventCamera;

            if (owner == null)
                return null;

            Canvas ownerCanvas = owner.uiCanvas;
            if (ownerCanvas != null && ownerCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
                return ownerCanvas.worldCamera != null ? ownerCanvas.worldCamera : owner.cachedCamera;

            return null;
        }
    }

    private sealed class MobileSwipeLookZone : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        private MobileInputController owner;
        private Vector2 swipePrevPosition;
        private bool isSwiping;
        private int swipePointerId = int.MinValue;
        private Vector2 pendingLookDelta;

        public void Initialize(MobileInputController ownerController)
        {
            owner = ownerController;
            pendingLookDelta = Vector2.zero;
            isSwiping = false;
            swipePointerId = int.MinValue;
        }

        public Vector2 ConsumeOutput()
        {
            Vector2 output = pendingLookDelta;
            pendingLookDelta = Vector2.zero;
            return output;
        }

        void Update()
        {
            if (owner == null)
                return;
        }

        public void ProcessTouches()
        {
            if (owner == null)
                return;

            float screenW = Mathf.Max(1f, Screen.width);
            float screenH = Mathf.Max(1f, Screen.height);
            List<TouchSample> touches = MobileInputController.TouchSamples;

            // Check if active swipe finger still exists
            if (swipePointerId != int.MinValue)
            {
                bool stillAlive = false;
                for (int i = 0; i < touches.Count; i++)
                {
                    if (touches[i].id != swipePointerId)
                        continue;
                    stillAlive = true;
                    TouchSample t = touches[i];
                    if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled)
                    {
                        ForceReset();
                        return;
                    }
                    if (t.phase == TouchPhase.Moved)
                    {
                        Vector2 lookDelta = new Vector2(t.delta.x / screenW, -(t.delta.y / screenH));
                        pendingLookDelta = lookDelta;
                        owner.SetLookOutput(lookDelta);
                    }
                    break;
                }
                if (!stillAlive)
                    ForceReset();
                return;
            }

            // Try claim a new right-side touch
            for (int i = 0; i < touches.Count; i++)
            {
                TouchSample touch = touches[i];
                if (touch.phase != TouchPhase.Began)
                    continue;
                if (owner.IsLeftTouchId(touch.id))
                    continue;
                if (touch.position.x < MobileInputController.GetScreenHalfX())
                    continue;
                if (!owner.TryClaimRightTouch(touch.id))
                    continue;
                swipePointerId = touch.id;
                pendingLookDelta = Vector2.zero;
                owner.SetLookOutput(Vector2.zero);
                break;
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
#if !UNITY_EDITOR
            return;
#endif
            if (owner != null && owner.IsLeftSideTouch(eventData.position))
                return;

            if (owner != null && !owner.TryClaimRightTouch(eventData.pointerId))
                return;

            eventData.useDragThreshold = false;
            swipePrevPosition = eventData.position;
            isSwiping = true;
            swipePointerId = eventData.pointerId;
            pendingLookDelta = Vector2.zero;

            if (owner != null)
                owner.SetLookOutput(Vector2.zero);
        }

        public void OnDrag(PointerEventData eventData)
        {
#if !UNITY_EDITOR
            return;
#endif
            if (!isSwiping || eventData.pointerId != swipePointerId || owner == null)
                return;

            Vector2 rawDelta = eventData.position - swipePrevPosition;
            float screenW = Mathf.Max(1f, Screen.width);
            float screenH = Mathf.Max(1f, Screen.height);
            Vector2 lookDelta = new Vector2(rawDelta.x / screenW, rawDelta.y / screenH);
            lookDelta.y = -lookDelta.y;

            pendingLookDelta = lookDelta;
            owner.SetLookOutput(lookDelta);
            swipePrevPosition = eventData.position;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
#if !UNITY_EDITOR
            return;
#endif
            if (eventData.pointerId != swipePointerId)
                return;

            ForceReset();
        }

        public void ForceReset()
        {
            int previousPointerId = swipePointerId;
            isSwiping = false;
            swipePointerId = int.MinValue;
            pendingLookDelta = Vector2.zero;

            if (owner != null)
            {
                if (previousPointerId != int.MinValue)
                    owner.ReleaseRightTouch(previousPointerId);

                owner.SetLookOutput(Vector2.zero);
            }
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
