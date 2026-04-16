using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

public class TutorialSequentialUI : MonoBehaviour
{
    private const string SampleSceneName = "SampleScene";

    private static TutorialSequentialUI instance;
    private static bool _hasShown;

    public static bool IsSequentialVisible { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        instance = null;
        _hasShown = false;
        IsSequentialVisible = false;
    }

    [System.Serializable]
    private struct HintData
    {
        public string icon;
        public string title;
        public string body;

        public HintData(string iconValue, string titleValue, string bodyValue)
        {
            icon = iconValue;
            title = titleValue;
            body = bodyValue;
        }
    }

    [Header("Timing")]
    [SerializeField] private float delayAfterIntroComplete = 1f;
    [SerializeField] private float fallbackDelayWhenNoIntroEvent = 2f;
    [SerializeField] private float startupVisualLockSeconds = 0.2f;

    [Header("Events")]
    public UnityEvent OnSequentialComplete;

    private readonly HintData[] hints =
    {
        new HintData("\u2726", "Cara Bergerak", "Gunakan W A S D untuk berjalan.\nTahan Shift untuk berlari."),
        new HintData("\u2B21", "Cara Berinteraksi", "Dekati objek atau NPC,\nlalu tekan E untuk berinteraksi.")
    };

    private Canvas hudCanvas;
    private RectTransform panelRoot;
    private CanvasGroup panelGroup;
    private TextMeshProUGUI hintIconText;
    private TextMeshProUGUI hintTitleText;
    private TextMeshProUGUI hintBodyText;
    private Button dismissButton;

    private IntroCutsceneController introCutsceneController;
    private StoryIntroManager storyIntroManager;
    private PlayerController playerController;

    private Coroutine flowRoutine;
    private int currentHintIndex = -1;
    private bool introHookFound;
    private bool tutorialInputLockApplied;
    private bool modalOpened;
    private bool cursorStateCaptured;
    private CursorLockMode previousCursorLockMode;
    private bool previousCursorVisible;
    private bool pendingShowRequest;
    private bool showScheduled;
    private bool startupReady;

    private void Awake()
    {
        startupReady = false;
        ForceHideExistingPanelEarly();
    }

    private void OnEnable()
    {
        if (instance != null && instance != this)
        {
            Destroy(this);
            return;
        }

        instance = this;
        startupReady = false;
        pendingShowRequest = false;
        showScheduled = false;
        IsSequentialVisible = false;
        ForceHideExistingPanelEarly();
    }

    private void Start()
    {
        EnsureEventSystemReady();
        EnsureUi();
        HideImmediate();

        pendingShowRequest = false;
        showScheduled = false;

        playerController = FindFirstObjectByType<PlayerController>();

        if (!IsInSampleScene())
            return;

        HookIntroCompletionEvents();

        StartCoroutine(MarkStartupReadyNextFrame());

        if (!introHookFound)
            flowRoutine = StartCoroutine(FallbackShowRoutine());
    }

    private void OnDisable()
    {
        UnhookIntroCompletionEvents();
        ReleaseGameplayLockAndCursor();

        if (flowRoutine != null)
        {
            StopCoroutine(flowRoutine);
            flowRoutine = null;
        }

        IsSequentialVisible = false;
        pendingShowRequest = false;
        showScheduled = false;
        startupReady = false;

        if (instance == this)
            instance = null;
    }

    private void Update()
    {
        if (!IsInSampleScene())
            return;

        if (!startupReady)
        {
            ForceHideExistingPanelEarly();
            return;
        }

        if (IntroCutsceneController.IsAnyCutscenePlaying)
        {
            SuppressWhileCutsceneActive();
            return;
        }

        if (pendingShowRequest && !_hasShown && !IsSequentialVisible && flowRoutine == null)
            Show();
    }

    public void Show()
    {
        if (!IsInSampleScene())
            return;

        if (_hasShown || IsSequentialVisible)
            return;

        if (!startupReady)
        {
            pendingShowRequest = true;
            return;
        }

        if (IntroCutsceneController.IsAnyCutscenePlaying)
        {
            pendingShowRequest = true;
            return;
        }

        pendingShowRequest = false;
        showScheduled = false;

        EnsureEventSystemReady();
        AcquireGameplayLockAndCursor();

        if (flowRoutine != null)
            StopCoroutine(flowRoutine);

        flowRoutine = StartCoroutine(ShowFirstHintRoutine());
    }

    private IEnumerator ShowFirstHintRoutine()
    {
        currentHintIndex = 0;
        ApplyHint(hints[currentHintIndex]);
        yield return FadePanel(0f, 1f, 0.3f);
        flowRoutine = null;
    }

    private IEnumerator FallbackShowRoutine()
    {
        yield return new WaitForSecondsRealtime(fallbackDelayWhenNoIntroEvent);

        if (_hasShown)
            yield break;

        if (showScheduled)
            yield break;

        showScheduled = true;
        pendingShowRequest = true;
        Show();
    }

    private void SuppressWhileCutsceneActive()
    {
        bool wasVisible = IsSequentialVisible || (panelRoot != null && panelRoot.gameObject.activeSelf);
        if (wasVisible)
        {
            HideImmediate();
            ReleaseGameplayLockAndCursor();
        }

        if (flowRoutine != null)
        {
            StopCoroutine(flowRoutine);
            flowRoutine = null;
        }

        if (!_hasShown)
            pendingShowRequest = true;
    }

    private void HookIntroCompletionEvents()
    {
        introHookFound = false;

        introCutsceneController = FindFirstObjectByType<IntroCutsceneController>();
        if (introCutsceneController != null)
        {
            introCutsceneController.OnCutsceneCompleted += HandleIntroOrCutsceneCompleted;
            introHookFound = true;
        }

        storyIntroManager = StoryIntroManager.Instance;
        if (storyIntroManager == null)
            storyIntroManager = FindFirstObjectByType<StoryIntroManager>();

        if (storyIntroManager != null)
        {
            storyIntroManager.OnIntroFlowCompleted += HandleIntroOrCutsceneCompleted;
            introHookFound = true;
        }
    }

    private void UnhookIntroCompletionEvents()
    {
        if (introCutsceneController != null)
            introCutsceneController.OnCutsceneCompleted -= HandleIntroOrCutsceneCompleted;

        if (storyIntroManager != null)
            storyIntroManager.OnIntroFlowCompleted -= HandleIntroOrCutsceneCompleted;

        introCutsceneController = null;
        storyIntroManager = null;
        introHookFound = false;
    }

    private void HandleIntroOrCutsceneCompleted()
    {
        if (_hasShown || IsSequentialVisible || !IsInSampleScene())
            return;

        if (showScheduled)
            return;

        showScheduled = true;

        if (flowRoutine != null)
            StopCoroutine(flowRoutine);

        flowRoutine = StartCoroutine(ShowAfterIntroCompleteDelayRoutine());
    }

    private IEnumerator ShowAfterIntroCompleteDelayRoutine()
    {
        yield return new WaitForSecondsRealtime(delayAfterIntroComplete);
        flowRoutine = null;
        pendingShowRequest = true;
        Show();
    }

    private IEnumerator MarkStartupReadyNextFrame()
    {
        if (startupVisualLockSeconds > 0f)
            yield return new WaitForSecondsRealtime(startupVisualLockSeconds);

        yield return null;
        startupReady = true;
    }

    private void HandleDismissPressed()
    {
        if (!IsSequentialVisible)
            return;

        if (flowRoutine != null)
            StopCoroutine(flowRoutine);

        flowRoutine = StartCoroutine(AdvanceHintRoutine());
    }

    private IEnumerator AdvanceHintRoutine()
    {
        if (currentHintIndex < hints.Length - 1)
        {
            yield return FadePanel(1f, 0f, 0.2f);
            currentHintIndex++;
            ApplyHint(hints[currentHintIndex]);
            yield return FadePanel(0f, 1f, 0.2f);
            flowRoutine = null;
            yield break;
        }

        yield return FadePanel(1f, 0f, 0.3f);
        HideImmediate();

        _hasShown = true;
        pendingShowRequest = false;
        showScheduled = false;
        currentHintIndex = -1;

        OnSequentialComplete?.Invoke();
        flowRoutine = null;
    }

    private IEnumerator FadePanel(float from, float to, float duration)
    {
        if (panelRoot == null || panelGroup == null)
            yield break;

        panelRoot.SetAsLastSibling();
        panelRoot.gameObject.SetActive(true);
        panelGroup.blocksRaycasts = true;
        panelGroup.interactable = true;
        IsSequentialVisible = true;

        if (dismissButton != null)
            dismissButton.interactable = true;

        float elapsed = 0f;
        float safeDuration = Mathf.Max(0.01f, duration);

        while (elapsed < safeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / safeDuration);
            panelGroup.alpha = Mathf.Lerp(from, to, t);
            yield return null;
        }

        panelGroup.alpha = to;

        bool visible = to > 0.01f;
        panelGroup.blocksRaycasts = visible;
        panelGroup.interactable = visible;
        IsSequentialVisible = visible;

        if (!visible)
        {
            panelRoot.gameObject.SetActive(false);
            ReleaseGameplayLockAndCursor();
        }
    }

    private void ApplyHint(HintData hint)
    {
        if (hintIconText != null)
            hintIconText.text = hint.icon;

        if (hintTitleText != null)
            hintTitleText.text = hint.title;

        if (hintBodyText != null)
            hintBodyText.text = hint.body;
    }

    private void EnsureUi()
    {
        if (hudCanvas == null)
            hudCanvas = FindHudCanvas();

        if (hudCanvas == null)
        {
            GameObject canvasObj = new GameObject("HUD_Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            hudCanvas = canvasObj.GetComponent<Canvas>();
            hudCanvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = canvasObj.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
        }

        Transform existing = hudCanvas.transform.Find("TutorialSequentialPanel");
        if (existing == null)
        {
            GameObject panelObj = new GameObject("TutorialSequentialPanel", typeof(RectTransform), typeof(Image), typeof(CanvasGroup), typeof(Outline));
            panelRoot = panelObj.GetComponent<RectTransform>();
            panelRoot.SetParent(hudCanvas.transform, false);
        }
        else
        {
            panelRoot = existing as RectTransform;
            if (panelRoot.GetComponent<Image>() == null)
                panelRoot.gameObject.AddComponent<Image>();
            if (panelRoot.GetComponent<CanvasGroup>() == null)
                panelRoot.gameObject.AddComponent<CanvasGroup>();
            if (panelRoot.GetComponent<Outline>() == null)
                panelRoot.gameObject.AddComponent<Outline>();
        }

        panelGroup = panelRoot.GetComponent<CanvasGroup>();
        panelGroup.alpha = 0f;
        panelGroup.interactable = false;
        panelGroup.blocksRaycasts = false;
        panelRoot.gameObject.SetActive(false);

        ConfigureSequentialPanelVisual();
        RebuildSequentialPanelChildren();

        hintIconText = panelRoot.Find("ContentRow/HintIcon")?.GetComponent<TextMeshProUGUI>();
        hintTitleText = panelRoot.Find("ContentRow/RightColumn/HintTitle")?.GetComponent<TextMeshProUGUI>();
        hintBodyText = panelRoot.Find("ContentRow/RightColumn/HintBody")?.GetComponent<TextMeshProUGUI>();
        dismissButton = panelRoot.Find("ContentRow/RightColumn/ButtonRow/DismissButton")?.GetComponent<Button>();

        if (dismissButton != null)
        {
            dismissButton.onClick.RemoveAllListeners();
            dismissButton.onClick.AddListener(HandleDismissPressed);
        }

        panelRoot.SetAsLastSibling();
    }

    private void ConfigureSequentialPanelVisual()
    {
        if (panelRoot == null)
            return;

        panelRoot.anchorMin = new Vector2(0.5f, 0.5f);
        panelRoot.anchorMax = new Vector2(0.5f, 0.5f);
        panelRoot.pivot = new Vector2(0.5f, 0.5f);
        panelRoot.anchoredPosition = Vector2.zero;
        panelRoot.sizeDelta = new Vector2(560f, 200f);

        Image bg = panelRoot.GetComponent<Image>();
        bg.color = new Color32(15, 18, 28, 225);

        Outline border = panelRoot.GetComponent<Outline>();
        border.effectColor = new Color(1f, 1f, 1f, 0.12f);
        border.effectDistance = new Vector2(1f, -1f);
    }

    private void RebuildSequentialPanelChildren()
    {
        if (panelRoot == null)
            return;

        for (int i = panelRoot.childCount - 1; i >= 0; i--)
        {
            Transform child = panelRoot.GetChild(i);
            if (Application.isPlaying)
                Destroy(child.gameObject);
            else
                DestroyImmediate(child.gameObject);
        }

        GameObject accentObj = new GameObject("LeftAccent", typeof(RectTransform), typeof(Image));
        RectTransform accentRect = accentObj.GetComponent<RectTransform>();
        accentRect.SetParent(panelRoot, false);
        accentRect.anchorMin = new Vector2(0f, 0.5f);
        accentRect.anchorMax = new Vector2(0f, 0.5f);
        accentRect.pivot = new Vector2(0f, 0.5f);
        accentRect.anchoredPosition = new Vector2(24f, 0f);
        accentRect.sizeDelta = new Vector2(4f, 160f);
        accentObj.GetComponent<Image>().color = new Color32(255, 232, 160, 200);

        GameObject rowObj = new GameObject("ContentRow", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        RectTransform rowRect = rowObj.GetComponent<RectTransform>();
        rowRect.SetParent(panelRoot, false);
        rowRect.anchorMin = Vector2.zero;
        rowRect.anchorMax = Vector2.one;
        rowRect.offsetMin = Vector2.zero;
        rowRect.offsetMax = Vector2.zero;

        HorizontalLayoutGroup rowLayout = rowObj.GetComponent<HorizontalLayoutGroup>();
        rowLayout.padding = new RectOffset(48, 32, 24, 24);
        rowLayout.spacing = 20f;
        rowLayout.childAlignment = TextAnchor.MiddleLeft;
        rowLayout.childControlHeight = true;
        rowLayout.childControlWidth = true;
        rowLayout.childForceExpandHeight = false;
        rowLayout.childForceExpandWidth = false;

        TextMeshProUGUI icon = CreateTmpText(rowRect, "HintIcon", 36f, new Color32(255, 232, 160, 255), TextAlignmentOptions.Center);
        LayoutElement iconLayout = icon.gameObject.AddComponent<LayoutElement>();
        iconLayout.preferredWidth = 48f;
        iconLayout.minWidth = 48f;
        iconLayout.flexibleWidth = 0f;

        GameObject rightObj = new GameObject("RightColumn", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement));
        RectTransform rightRect = rightObj.GetComponent<RectTransform>();
        rightRect.SetParent(rowRect, false);

        LayoutElement rightLayout = rightObj.GetComponent<LayoutElement>();
        rightLayout.flexibleWidth = 1f;
        rightLayout.minWidth = 380f;

        VerticalLayoutGroup columnLayout = rightObj.GetComponent<VerticalLayoutGroup>();
        columnLayout.spacing = 8f;
        columnLayout.childAlignment = TextAnchor.UpperLeft;
        columnLayout.childControlHeight = true;
        columnLayout.childControlWidth = true;
        columnLayout.childForceExpandHeight = false;
        columnLayout.childForceExpandWidth = true;

        TextMeshProUGUI title = CreateTmpText(rightRect, "HintTitle", 20f, new Color32(255, 232, 160, 255), TextAlignmentOptions.TopLeft);
        title.fontStyle = FontStyles.Bold;
        title.textWrappingMode = TextWrappingModes.NoWrap;
        AddTextShadow(title);

        TextMeshProUGUI body = CreateTmpText(rightRect, "HintBody", 15f, new Color(240f / 255f, 240f / 255f, 240f / 255f, 0.88f), TextAlignmentOptions.TopLeft);
        body.textWrappingMode = TextWrappingModes.Normal;
        body.lineSpacing = 1.5f;

        LayoutElement bodyLayout = body.gameObject.AddComponent<LayoutElement>();
        bodyLayout.minHeight = 68f;

        GameObject buttonRowObj = new GameObject("ButtonRow", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        RectTransform buttonRowRect = buttonRowObj.GetComponent<RectTransform>();
        buttonRowRect.SetParent(rightRect, false);

        HorizontalLayoutGroup buttonRowLayout = buttonRowObj.GetComponent<HorizontalLayoutGroup>();
        buttonRowLayout.childAlignment = TextAnchor.MiddleRight;
        buttonRowLayout.childControlHeight = true;
        buttonRowLayout.childControlWidth = true;
        buttonRowLayout.childForceExpandHeight = false;
        buttonRowLayout.childForceExpandWidth = false;

        Button button = CreateButton(buttonRowRect, "DismissButton", "Oke, mengerti  \u2192");
        LayoutElement buttonLayout = button.gameObject.AddComponent<LayoutElement>();
        buttonLayout.preferredWidth = 160f;
        buttonLayout.preferredHeight = 36f;
    }

    private void AcquireGameplayLockAndCursor()
    {
        if (playerController == null)
            playerController = FindFirstObjectByType<PlayerController>();

        if (!tutorialInputLockApplied && playerController != null)
        {
            playerController.LockInput("TutorialSequential");
            tutorialInputLockApplied = true;
        }

        if (!modalOpened && ModalStateManager.Instance != null)
        {
            ModalStateManager.Instance.OpenModal("TutorialSequential");
            modalOpened = true;
        }

        if (!cursorStateCaptured)
        {
            previousCursorLockMode = Cursor.lockState;
            previousCursorVisible = Cursor.visible;
            cursorStateCaptured = true;
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void ReleaseGameplayLockAndCursor()
    {
        if (tutorialInputLockApplied && playerController != null)
        {
            playerController.UnlockInput("TutorialSequential");
            tutorialInputLockApplied = false;
        }

        if (modalOpened && ModalStateManager.Instance != null)
        {
            ModalStateManager.Instance.CloseModal("TutorialSequential");
            modalOpened = false;
        }

        if (cursorStateCaptured)
        {
            Cursor.lockState = previousCursorLockMode;
            Cursor.visible = previousCursorVisible;
            cursorStateCaptured = false;
        }
    }

    private static void EnsureEventSystemReady()
    {
        EventSystem eventSystem = Object.FindFirstObjectByType<EventSystem>();
        if (eventSystem == null)
            eventSystem = Object.FindFirstObjectByType<EventSystem>(FindObjectsInactive.Include);

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

    private static Canvas FindHudCanvas()
    {
        GameObject canvasObj = GameObject.Find("HUD_Canvas");
        return canvasObj != null ? canvasObj.GetComponent<Canvas>() : null;
    }

    private static TextMeshProUGUI CreateTmpText(RectTransform parent, string name, float fontSize, Color color, TextAlignmentOptions align)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);

        TextMeshProUGUI tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.fontSize = fontSize;
        tmp.color = color;
        tmp.alignment = align;
        tmp.textWrappingMode = TextWrappingModes.Normal;
        tmp.text = string.Empty;
        return tmp;
    }

    private static Button CreateButton(RectTransform parent, string name, string label)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(Outline));
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);

        Image image = go.GetComponent<Image>();
        image.color = new Color32(255, 232, 160, 30);

        Outline border = go.GetComponent<Outline>();
        border.effectColor = new Color32(255, 232, 160, 120);
        border.effectDistance = new Vector2(0.5f, -0.5f);

        Button button = go.GetComponent<Button>();

        TextMeshProUGUI text = CreateTmpText(rect, "Label", 14f, new Color32(255, 232, 160, 220), TextAlignmentOptions.Center);
        text.fontStyle = FontStyles.Normal;
        RectTransform textRect = text.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        text.text = label;

        return button;
    }

    private static void AddTextShadow(TextMeshProUGUI target)
    {
        if (target == null)
            return;

        Outline outline = target.GetComponent<Outline>();
        if (outline == null)
            outline = target.gameObject.AddComponent<Outline>();

        outline.effectColor = new Color(0f, 0f, 0f, 180f / 255f);
        outline.effectDistance = new Vector2(1f, 1f);
    }

    private void HideImmediate()
    {
        if (panelRoot == null || panelGroup == null)
            return;

        panelGroup.alpha = 0f;
        panelGroup.interactable = false;
        panelGroup.blocksRaycasts = false;
        panelRoot.gameObject.SetActive(false);
        IsSequentialVisible = false;
    }

    private static void ForceHideExistingPanelEarly()
    {
        GameObject canvasObj = GameObject.Find("HUD_Canvas");
        if (canvasObj == null)
            return;

        Transform panel = canvasObj.transform.Find("TutorialSequentialPanel");
        if (panel == null)
            return;

        CanvasGroup cg = panel.GetComponent<CanvasGroup>();
        if (cg != null)
        {
            cg.alpha = 0f;
            cg.interactable = false;
            cg.blocksRaycasts = false;
        }

        panel.gameObject.SetActive(false);
    }

    private static bool IsInSampleScene()
    {
        return SceneManager.GetActiveScene().name == SampleSceneName;
    }
}
