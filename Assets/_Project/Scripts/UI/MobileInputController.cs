using System.Collections;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

[DefaultExecutionOrder(500)]
public class MobileInputController : MonoBehaviour
{
    public static MobileInputController Instance { get; private set; }

    [SerializeField] private bool forceMobileUI = true;
    [SerializeField] private float lookSensitivity = 0.08f;

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

    private GameObject uiRoot;
    private Canvas uiCanvas;
    private MobileJoystick moveJoystick;
    private MobileSwipeLookZone lookSwipeZone;
    private TextMeshProUGUI perspectiveToggleLabel;
    private bool lastPerspectiveFirstPerson;
    private bool hasPerspectiveState;

    private MethodInfo cameraLookVectorMethod;
    private MethodInfo cameraLookFloatMethod;
    private bool cameraMethodLookupDone;
    private bool isTouchUiEnabled;
    private bool fallbackPitchInitialized;
    private float fallbackPitch;

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
        EnsureEventSystemSetup();
        isTouchUiEnabled = ShouldEnableTouchUi();

        ResolveSceneReferences();

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
#endif
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

        if (cameraSystem != null && !cameraMethodLookupDone)
            CacheCameraLookMethods();
    }

    private void CacheCameraLookMethods()
    {
        cameraMethodLookupDone = true;

        string[] methodNames =
        {
            "AddLookInput",
            "InjectLookInput",
            "ApplyLookInput",
            "RotateCamera",
            "PanCamera"
        };

        for (int i = 0; i < methodNames.Length; i++)
        {
            MethodInfo vectorMethod = cameraSystem.GetType().GetMethod(
                methodNames[i],
                BindingFlags.Instance | BindingFlags.Public,
                null,
                new[] { typeof(Vector2) },
                null);

            if (vectorMethod != null)
            {
                cameraLookVectorMethod = vectorMethod;
                return;
            }

            MethodInfo floatMethod = cameraSystem.GetType().GetMethod(
                methodNames[i],
                BindingFlags.Instance | BindingFlags.Public,
                null,
                new[] { typeof(float), typeof(float) },
                null);

            if (floatMethod != null)
            {
                cameraLookFloatMethod = floatMethod;
                return;
            }
        }
    }

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

        RectTransform moveOuter = CreateControlCircle(
            "MoveJoystickOuter",
            uiRoot.transform,
            MoveJoystickAnchor,
            MoveJoystickAnchor,
            MoveJoystickPivot,
            MoveJoystickPosition,
            MoveJoystickSize,
            new Color(1f, 1f, 1f, 0.12f),
            true,
            true);

        RectTransform moveBorder = CreateControlCircle(
            "MoveJoystickOuterBorder",
            moveOuter,
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            MoveJoystickSize,
            new Color(1f, 1f, 1f, 0.22f),
            true,
            false);
        moveBorder.SetAsFirstSibling();
        moveBorder.localScale = new Vector3(1.04f, 1.04f, 1f);

        RectTransform moveKnob = CreateControlCircle(
            "MoveJoystickKnob",
            moveOuter,
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            MoveKnobSize,
            new Color(1f, 1f, 1f, 0.3f),
            true,
            false);

        moveJoystick = moveOuter.gameObject.AddComponent<MobileJoystick>();
        moveJoystick.Initialize(this, moveKnob, JoystickRadius, JoystickDeadZone);

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
        captureGraphic.raycastTarget = Application.isMobilePlatform;

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
        if (lookInput.sqrMagnitude <= 0.000001f || cameraSystemTransform == null)
            return;

        float yawDelta = lookInput.x;
        float pitchDelta = lookInput.y;

        if (TryInvokeCameraLookMethod(yawDelta, pitchDelta))
            return;

        fallbackPitch = Mathf.Clamp(fallbackPitch + pitchDelta, -60f, 70f);

        Vector3 euler = cameraSystemTransform.eulerAngles;
        euler.y += yawDelta;
        euler.x = fallbackPitch;
        cameraSystemTransform.rotation = Quaternion.Euler(euler);
    }

    private bool TryInvokeCameraLookMethod(float yawDelta, float pitchDelta)
    {
        if (cameraSystem == null)
            return false;

        if (cameraLookVectorMethod != null)
        {
            cameraLookVectorMethod.Invoke(cameraSystem, new object[] { new Vector2(yawDelta, pitchDelta) });
            return true;
        }

        if (cameraLookFloatMethod != null)
        {
            cameraLookFloatMethod.Invoke(cameraSystem, new object[] { yawDelta, pitchDelta });
            return true;
        }

        return false;
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
        private RectTransform knobRect;
        private float maxRadius;
        private int activePointerId = int.MinValue;

        private const float ActiveSmoothingSpeed = 22f;
        private const float ReleaseSmoothingSpeed = 32f;
        private const float MicroJitterThreshold = 0.0125f;
        private const float ResponseExponent = 1.35f;

        private Vector2 pressLocalOrigin;
        private Vector2 rawOutput;
        private Vector2 smoothedOutput;

        public Vector2 Output => smoothedOutput;
        public bool IsActive => activePointerId != int.MinValue;

        public void Initialize(
            MobileInputController ownerController,
            RectTransform knob,
            float dragRadius,
            float deadZone)
        {
            owner = ownerController;
            baseRect = transform as RectTransform;
            knobRect = knob;
            maxRadius = Mathf.Max(1f, dragRadius);
            rawOutput = Vector2.zero;
            smoothedOutput = Vector2.zero;

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

        public void OnPointerDown(PointerEventData eventData)
        {
            if (activePointerId != int.MinValue && activePointerId != eventData.pointerId)
                return;

            activePointerId = eventData.pointerId;
            eventData.useDragThreshold = false;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                baseRect,
                eventData.position,
                eventData.pressEventCamera,
                out pressLocalOrigin);

            if (knobRect != null)
                knobRect.anchoredPosition = Vector2.zero;

            rawOutput = Vector2.zero;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (eventData.pointerId != activePointerId)
                return;

            UpdatePointerState(eventData);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
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
            activePointerId = int.MinValue;
            rawOutput = Vector2.zero;
            smoothedOutput = Vector2.zero;

            if (knobRect != null)
                knobRect.anchoredPosition = Vector2.zero;

            if (owner == null)
                return;

            owner.SetMoveOutput(Vector2.zero);
        }

        private void UpdatePointerState(PointerEventData eventData)
        {
            if (baseRect == null)
                return;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                baseRect,
                eventData.position,
                eventData.pressEventCamera,
                out Vector2 localDragPoint);

            Vector2 delta = localDragPoint - pressLocalOrigin;
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
    }

    private sealed class MobileSwipeLookZone : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        private MobileInputController owner;
        private Vector2 swipePrevPosition;
        private bool isSwiping;
        private int swipePointerId = -1;
        private Vector2 pendingLookDelta;

        public void Initialize(MobileInputController ownerController)
        {
            owner = ownerController;
            pendingLookDelta = Vector2.zero;
            isSwiping = false;
            swipePointerId = -1;
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

            if (owner != null)
                owner.SetLookOutput(Vector2.zero);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!isSwiping || eventData.pointerId != swipePointerId || owner == null)
                return;

            Vector2 lookDelta = (eventData.position - swipePrevPosition) * owner.lookSensitivity;
            lookDelta.y = -lookDelta.y;

            pendingLookDelta = lookDelta;
            owner.SetLookOutput(lookDelta);
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

            if (owner != null)
                owner.SetLookOutput(Vector2.zero);
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
