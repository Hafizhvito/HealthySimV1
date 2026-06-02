using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

public class NpcDialogueMenuController : MonoBehaviour
{
    private sealed class ChoiceCardRef
    {
        public Button Button;
        public RectTransform Rect;
        public Image Background;
        public TextMeshProUGUI IconText;
        public TextMeshProUGUI LabelText;
        public LayoutElement Layout;
        public DialogueChoiceCardFx Fx;
    }

    public static NpcDialogueMenuController Instance { get; private set; }

    public event Action OnDialogueClosed;

    [Header("Panel Referensi")]
    [SerializeField] private RectTransform dialoguePanel;
    [SerializeField] private Image gradientOverlay;
    [SerializeField] private Image letterboxTop;
    [SerializeField] private Image letterboxBottom;
    [SerializeField] private RectTransform npcSpeechArea;
    [SerializeField] private RectTransform choiceContainer;
    [SerializeField] private TextMeshProUGUI npcNameText;
    [SerializeField] private TextMeshProUGUI npcSubtitleText;
    [SerializeField] private TextMeshProUGUI dialogueText;
    [SerializeField] private Button closeButton;

    [Header("Animasi")]
    [SerializeField] private float openDuration = 0.25f;
    [SerializeField] private float closeDuration = 0.2f;
    [SerializeField] private float openSlideOffset = 20f;
    [SerializeField] [Range(0f, 1f)] private float overlayTargetAlpha = 180f / 255f;
    [SerializeField] [Range(0f, 220f)] private float letterboxHeight = 96f;

    [Header("Text Reveal")]
    [SerializeField] private bool useTypewriterText = true;
    [SerializeField] [Range(20f, 120f)] private float typewriterCharsPerSecond = 54f;
    [SerializeField] [Range(0f, 0.12f)] private float typewriterPunctuationPause = 0.03f;

    [Header("Choice Cards")]
    [SerializeField] private float choiceCardHeight = 56f;
    //[SerializeField] private float choiceSpacing = 8f;
    [SerializeField] private int maxDialogueChars = 260;
    [SerializeField] private int maxChoiceChars = 96;

    private readonly List<ChoiceCardRef> pooledChoices = new List<ChoiceCardRef>();

    private IDialogueActor activeNpc;
    private DialogueGraphData activeGraph;
    private DialogueNodeData activeNode;

    private PlayerController playerController;
    private UniversalInteractionController interactionController;
    private bool dialogueModalOpened;
    private bool gameplayPausedByDialogue;
    private CursorLockMode previousCursorLockMode;
    private bool previousCursorVisible;
    private bool cursorStateCaptured;

    private bool wasInteractionEnabled;
    private bool isAwaitingFollowUpContinue;
    private string pendingNextNodeId;
    private bool pendingCloseAfterFollowUp;

    private Coroutine panelAnimationRoutine;
    private Coroutine dialogueTextRoutine;
    private Vector2 speechBasePos;
    private Vector2 choicesBasePos;

    public bool IsOpen => activeNpc != null && activeGraph != null && dialoguePanel != null && dialoguePanel.gameObject.activeSelf;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        EnsureEventSystem();
        BuildOrBindCinematicUi();
        WireUiCallbacks();
        CacheControllers();
        SetMenuVisible(false);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        if (panelAnimationRoutine != null)
            StopCoroutine(panelAnimationRoutine);

        StopDialogueTextRoutine();

        if (closeButton != null)
            closeButton.onClick.RemoveAllListeners();

        if (gameplayPausedByDialogue)
            ResumeGameplay();
        else
        {
            if (dialogueModalOpened && ModalStateManager.Instance != null)
            {
                ModalStateManager.Instance.CloseModal("NpcDialogue");
                dialogueModalOpened = false;
            }

            RestoreCursorState();
        }

        ClearChoicePoolListeners();
        pooledChoices.Clear();
        ResetDialogueRuntimeState();
    }

    void Update()
    {
        if (IsOpen && PressedCancelThisFrame())
            Cancel();
    }

        private bool PressedCancelThisFrame()
        {
        bool legacy = false;
    #if ENABLE_LEGACY_INPUT_MANAGER
        legacy = Input.GetKeyDown(KeyCode.Escape);
    #endif

    #if ENABLE_INPUT_SYSTEM
        bool inputSystem = UnityEngine.InputSystem.Keyboard.current != null
            && UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame;
        return legacy || inputSystem;
    #else
        return legacy;
    #endif
        }

    public bool OpenDialogue(IDialogueActor npc, DialogueGraphData graph)
    {
        EnsureUiReady();

        if (npc == null || graph == null || IsOpen)
            return false;

        activeNpc = npc;
        activeGraph = graph;
        activeNode = activeGraph.GetNode(activeGraph.startNodeId);
        if (activeNode == null)
        {
            ResetDialogueRuntimeState();
            return false;
        }

        isAwaitingFollowUpContinue = false;
        pendingNextNodeId = string.Empty;
        pendingCloseAfterFollowUp = false;

        PauseGameplay();
        UpdateNpcHeader();
        RenderNode();
        PlayOpenAnimation();
        return true;
    }

    private void RenderNode()
    {
        if (activeNode == null)
            return;

        PruneDestroyedChoiceCards();

        int seed = SessionSeedManager.Instance != null
            ? SessionSeedManager.Instance.NextInt(0, int.MaxValue)
            : UnityEngine.Random.Range(0, int.MaxValue);

        SetDialogueLine(activeNode.PickLine(seed));

        List<DialogueChoiceData> choices = activeNpc.GetAvailableChoices(activeNode);
        if (choices == null || choices.Count == 0)
        {
            ShowImplicitContinueForChoiceLessNode();
            if (choiceContainer != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(choiceContainer);
            return;
        }

        EnsureChoicePool(choices.Count);

        for (int i = 0; i < pooledChoices.Count; i++)
        {
            bool isActive = i < choices.Count;
            ChoiceCardRef card = pooledChoices[i];
            if (card == null || card.Button == null || card.LabelText == null)
                continue;

            card.Button.gameObject.SetActive(isActive);
            if (!isActive)
                continue;

            int capture = i;
            card.LabelText.text = FormatChoiceLabel(choices[capture].GetDisplayLabel());
            ApplyChoiceCardSize(card);
            card.Button.onClick.RemoveAllListeners();
            card.Button.onClick.AddListener(() => SelectChoice(choices[capture]));
            card.Button.interactable = true;
        }

        if (choiceContainer != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(choiceContainer);
    }

    private void ShowImplicitContinueForChoiceLessNode()
    {
        EnsureChoicePool(1);
        bool hasFollowUp = activeNode != null && !string.IsNullOrWhiteSpace(activeNode.npcFollowUpText);

        for (int i = 0; i < pooledChoices.Count; i++)
        {
            ChoiceCardRef card = pooledChoices[i];
            if (card == null || card.Button == null || card.LabelText == null)
                continue;

            bool showContinue = i == 0;
            card.Button.gameObject.SetActive(showContinue);
            if (!showContinue)
                continue;

            bool shouldClose = activeNode == null || activeNode.isConversationEnd || activeNode.isTerminal;
            card.LabelText.text = hasFollowUp ? "Lanjut >" : (shouldClose ? "Selesai >" : "Lanjut >");
            ApplyChoiceCardSize(card);
            card.Button.onClick.RemoveAllListeners();

            if (hasFollowUp)
                card.Button.onClick.AddListener(() => ShowFollowUpStep(activeNode.npcFollowUpText, string.Empty, shouldClose));
            else if (shouldClose)
                card.Button.onClick.AddListener(CloseMenu);
            else
                card.Button.onClick.AddListener(() => AdvanceFromChoice(string.Empty, true));

            card.Button.interactable = true;
        }
    }

    private void SelectChoice(DialogueChoiceData choice)
    {
        if (!IsOpen || choice == null || isAwaitingFollowUpContinue)
            return;

        activeNpc.ApplyConsequence(choice.consequence);

        string nextNodeId = choice.nextNodeId;
        bool closeAfterFollowUp = activeNode != null && activeNode.isConversationEnd;
        bool hasFollowUp = activeNode != null && !string.IsNullOrWhiteSpace(activeNode.npcFollowUpText);

        if (hasFollowUp)
        {
            ShowFollowUpStep(activeNode.npcFollowUpText, nextNodeId, closeAfterFollowUp);
            return;
        }

        AdvanceFromChoice(nextNodeId, closeAfterFollowUp);
    }

    private void ShowFollowUpStep(string followUpText, string nextNodeId, bool closeAfterFollowUp)
    {
        isAwaitingFollowUpContinue = true;
        pendingNextNodeId = string.IsNullOrEmpty(nextNodeId) ? string.Empty : nextNodeId;
        pendingCloseAfterFollowUp = closeAfterFollowUp;

        PruneDestroyedChoiceCards();

        SetDialogueLine(followUpText);

        EnsureChoicePool(1);
        for (int i = 0; i < pooledChoices.Count; i++)
        {
            bool continueBtn = i == 0;
            ChoiceCardRef card = pooledChoices[i];
            if (card == null || card.Button == null || card.LabelText == null)
                continue;

            card.Button.gameObject.SetActive(continueBtn);
            if (!continueBtn)
                continue;

            card.LabelText.text = "Lanjut >";
            ApplyChoiceCardSize(card);
            card.Button.onClick.RemoveAllListeners();
            card.Button.onClick.AddListener(ContinueAfterFollowUp);
            card.Button.interactable = true;
        }

        if (choiceContainer != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(choiceContainer);
    }

    private void ContinueAfterFollowUp()
    {
        if (!IsOpen || !isAwaitingFollowUpContinue)
            return;

        string nextNodeId = pendingNextNodeId;
        bool closeAfterFollowUp = pendingCloseAfterFollowUp;

        isAwaitingFollowUpContinue = false;
        pendingNextNodeId = string.Empty;
        pendingCloseAfterFollowUp = false;

        if (closeAfterFollowUp)
        {
            CloseMenu();
            return;
        }

        AdvanceFromChoice(nextNodeId, false);
    }

    private void AdvanceFromChoice(string nextNodeId, bool closeNow)
    {
        if (closeNow)
        {
            CloseMenu();
            return;
        }

        activeNode = string.IsNullOrEmpty(nextNodeId) ? null : activeGraph.GetNode(nextNodeId);
        if (activeNode == null || activeNode.isTerminal)
            CloseMenu();
        else
            RenderNode();
    }

    private void Cancel()
    {
        CloseMenu();
    }

    public void ForceCleanupAfterSceneLoad()
    {
        if (panelAnimationRoutine != null)
        {
            StopCoroutine(panelAnimationRoutine);
            panelAnimationRoutine = null;
        }

        StopDialogueTextRoutine();
        ResetDialogueRuntimeState();
        SetMenuVisible(false);

        if (gameplayPausedByDialogue)
            ResumeGameplay();
        else if (dialogueModalOpened && ModalStateManager.Instance != null)
        {
            ModalStateManager.Instance.CloseModal("NpcDialogue");
            dialogueModalOpened = false;
            RestoreCursorState();
        }
    }

    private void CloseMenu()
    {
        if (!IsOpen)
        {
            if (gameplayPausedByDialogue || dialogueModalOpened)
                ForceCleanupAfterSceneLoad();
            return;
        }

        isAwaitingFollowUpContinue = false;
        pendingNextNodeId = string.Empty;
        pendingCloseAfterFollowUp = false;

        if (panelAnimationRoutine != null)
            StopCoroutine(panelAnimationRoutine);

        panelAnimationRoutine = StartCoroutine(CloseRoutine());
    }

    private IEnumerator CloseRoutine()
    {
        yield return RunPanelAnimation(open: false);

        ResetDialogueRuntimeState();

        SetMenuVisible(false);
        ResumeGameplay();
        OnDialogueClosed?.Invoke();
        panelAnimationRoutine = null;
    }

    private void WireUiCallbacks()
    {
        if (closeButton == null)
            return;

        closeButton.onClick.RemoveAllListeners();
        closeButton.onClick.AddListener(Cancel);
    }

    private void EnsureChoicePool(int count)
    {
        if (choiceContainer == null)
            return;

        PruneDestroyedChoiceCards();

        while (pooledChoices.Count < count)
        {
            ChoiceCardRef card = CreateChoiceCard(choiceContainer, pooledChoices.Count);
            if (card == null)
                break;

            pooledChoices.Add(card);
        }
    }

    private ChoiceCardRef CreateChoiceCard(RectTransform parent, int index)
    {
        GameObject root = new GameObject($"DialogueChoiceCard_{index}", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement), typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter));
        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.SetParent(parent, false);
        rootRect.anchorMin = new Vector2(1f, 0f);
        rootRect.anchorMax = new Vector2(1f, 0f);
        rootRect.pivot = new Vector2(1f, 0f);

        Image bg = root.GetComponent<Image>();
        bg.color = new Color(20f / 255f, 28f / 255f, 40f / 255f, 0.72f);

        Button button = root.GetComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.targetGraphic = bg;

        LayoutElement layout = root.GetComponent<LayoutElement>();
        layout.minWidth = 320f;
        layout.preferredWidth = 420f;
        layout.flexibleWidth = 0f;
        layout.minHeight = choiceCardHeight;
        layout.preferredHeight = choiceCardHeight;

        ContentSizeFitter rootFitter = root.GetComponent<ContentSizeFitter>();
        rootFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        rootFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        HorizontalLayoutGroup hLayout = root.GetComponent<HorizontalLayoutGroup>();
        hLayout.padding = new RectOffset(20, 20, 14, 14);
        hLayout.spacing = 8;
        hLayout.childAlignment = TextAnchor.MiddleLeft;
        hLayout.childForceExpandHeight = false;
        hLayout.childForceExpandWidth = false;
        hLayout.childControlHeight = true;
        hLayout.childControlWidth = true;

        GameObject iconObj = new GameObject("Icon", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
        RectTransform iconRect = iconObj.GetComponent<RectTransform>();
        iconRect.SetParent(rootRect, false);

        LayoutElement iconLayout = iconObj.GetComponent<LayoutElement>();
        iconLayout.preferredWidth = 22f;
        iconLayout.minWidth = 22f;

        TextMeshProUGUI iconText = iconObj.GetComponent<TextMeshProUGUI>();
        iconText.text = "?";
        iconText.fontSize = 20f;
        iconText.color = new Color(200f / 255f, 210f / 255f, 220f / 255f, 0.6f);
        iconText.alignment = TextAlignmentOptions.MidlineLeft;

        GameObject labelObj = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
        RectTransform labelRect = labelObj.GetComponent<RectTransform>();
        labelRect.SetParent(rootRect, false);

        LayoutElement labelLayout = labelObj.GetComponent<LayoutElement>();
        labelLayout.flexibleWidth = 1f;

        TextMeshProUGUI labelText = labelObj.GetComponent<TextMeshProUGUI>();
        labelText.text = "Pilihan";
        labelText.fontSize = 22f;
        labelText.lineSpacing = 6f;
        labelText.color = new Color(240f / 255f, 245f / 255f, 1f, 0.9f);
        labelText.textWrappingMode = TextWrappingModes.Normal;
        labelText.maxVisibleLines = 3;
        labelText.alignment = TextAlignmentOptions.MidlineLeft;
        labelText.overflowMode = TextOverflowModes.Overflow;
        labelText.margin = new Vector4(12f, 8f, 12f, 8f);

        DialogueChoiceCardFx fx = root.AddComponent<DialogueChoiceCardFx>();
        fx.Configure(bg,
            new Color(20f / 255f, 28f / 255f, 40f / 255f, 0.72f),
            new Color(40f / 255f, 55f / 255f, 75f / 255f, 0.85f),
            0.97f,
            0.08f);

        return new ChoiceCardRef
        {
            Button = button,
            Rect = rootRect,
            Background = bg,
            IconText = iconText,
            LabelText = labelText,
            Layout = layout,
            Fx = fx
        };
    }

    private void BuildOrBindCinematicUi()
    {
        Canvas hudCanvas = FindHudCanvas();
        if (hudCanvas == null)
        {
            Debug.LogError("[NPCMenu] HUD_Canvas tidak ditemukan.");
            return;
        }

        RectTransform hudRect = hudCanvas.GetComponent<RectTransform>();
        RectTransform panel = hudRect.Find("DialogueCinematicPanel") as RectTransform;
        if (panel == null)
            panel = CreateCinematicPanel(hudRect);

        dialoguePanel = panel;

        Transform gradient = panel.Find("GradientOverlay");
        if (gradient != null)
            gradientOverlay = gradient.GetComponent<Image>();

        Transform letterboxTopObj = panel.Find("LetterboxTop");
        if (letterboxTopObj != null)
            letterboxTop = letterboxTopObj.GetComponent<Image>();

        Transform letterboxBottomObj = panel.Find("LetterboxBottom");
        if (letterboxBottomObj != null)
            letterboxBottom = letterboxBottomObj.GetComponent<Image>();

        if (letterboxTop == null)
            letterboxTop = CreateLetterboxBar(panel, "LetterboxTop", true);

        if (letterboxBottom == null)
            letterboxBottom = CreateLetterboxBar(panel, "LetterboxBottom", false);

        Transform speech = panel.Find("NpcSpeechArea");
        if (speech != null)
            npcSpeechArea = speech.GetComponent<RectTransform>();

        Transform choices = panel.Find("PlayerChoicesArea");
        if (choices != null)
            choiceContainer = choices.GetComponent<RectTransform>();

        Transform nameObj = panel.Find("NpcSpeechArea/NpcNameText");
        if (nameObj != null)
            npcNameText = nameObj.GetComponent<TextMeshProUGUI>();

        Transform subtitleObj = panel.Find("NpcSpeechArea/NpcSubtitleText");
        if (subtitleObj != null)
            npcSubtitleText = subtitleObj.GetComponent<TextMeshProUGUI>();

        Transform dialogueObj = panel.Find("NpcSpeechArea/NpcDialogueText");
        if (dialogueObj != null)
            dialogueText = dialogueObj.GetComponent<TextMeshProUGUI>();

        Transform closeObj = panel.Find("CloseDialogue");
        if (closeObj != null)
            closeButton = closeObj.GetComponent<Button>();

        ConfigureTextStyles();
        ConfigureSpeechArea();
        ConfigureChoicesArea();
        ConfigureGradientOverlay();
        ConfigureLetterboxBars();

        if (npcSpeechArea != null)
            speechBasePos = npcSpeechArea.anchoredPosition;

        if (choiceContainer != null)
            choicesBasePos = choiceContainer.anchoredPosition;
    }

    private void EnsureUiReady()
    {
        EnsureEventSystem();

        bool missingUi = dialoguePanel == null || choiceContainer == null || npcSpeechArea == null
            || npcNameText == null || npcSubtitleText == null || dialogueText == null;

        if (missingUi)
            BuildOrBindCinematicUi();

        if (closeButton == null && dialoguePanel != null)
        {
            Transform closeObj = dialoguePanel.Find("CloseDialogue");
            if (closeObj != null)
                closeButton = closeObj.GetComponent<Button>();
        }

        WireUiCallbacks();
        CacheControllers();
        PruneDestroyedChoiceCards();
    }

    private void PruneDestroyedChoiceCards()
    {
        for (int i = pooledChoices.Count - 1; i >= 0; i--)
        {
            ChoiceCardRef card = pooledChoices[i];
            bool invalid = card == null || card.Button == null || card.LabelText == null || card.Rect == null;
            if (!invalid && choiceContainer != null && card.Rect.parent != choiceContainer)
                invalid = true;

            if (!invalid)
                continue;

            if (card != null && card.Button != null)
                card.Button.onClick.RemoveAllListeners();

            pooledChoices.RemoveAt(i);
        }
    }

    private void ClearChoicePoolListeners()
    {
        for (int i = 0; i < pooledChoices.Count; i++)
        {
            ChoiceCardRef card = pooledChoices[i];
            if (card != null && card.Button != null)
                card.Button.onClick.RemoveAllListeners();
        }
    }

    private void ResetDialogueRuntimeState()
    {
        StopDialogueTextRoutine();

        isAwaitingFollowUpContinue = false;
        pendingNextNodeId = string.Empty;
        pendingCloseAfterFollowUp = false;

        if (dialogueText != null)
            dialogueText.text = string.Empty;

        activeNpc = null;
        activeGraph = null;
        activeNode = null;
    }

    private RectTransform CreateCinematicPanel(RectTransform parent)
    {
        GameObject panelObj = new GameObject("DialogueCinematicPanel", typeof(RectTransform));
        RectTransform panelRect = panelObj.GetComponent<RectTransform>();
        panelRect.SetParent(parent, false);
        StretchFull(panelRect);

        GameObject gradientObj = new GameObject("GradientOverlay", typeof(RectTransform), typeof(Image));
        RectTransform gradientRect = gradientObj.GetComponent<RectTransform>();
        gradientRect.SetParent(panelRect, false);
        gradientRect.anchorMin = new Vector2(0f, 0f);
        gradientRect.anchorMax = new Vector2(1f, 0f);
        gradientRect.pivot = new Vector2(0.5f, 0f);
        gradientRect.anchoredPosition = Vector2.zero;
        gradientRect.sizeDelta = new Vector2(0f, 400f);

        Image gradientImage = gradientObj.GetComponent<Image>();
        gradientImage.color = new Color(0f, 0f, 0f, 180f / 255f);

        CreateLetterboxBar(panelRect, "LetterboxTop", true);
        CreateLetterboxBar(panelRect, "LetterboxBottom", false);

        GameObject speechObj = new GameObject("NpcSpeechArea", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        RectTransform speechRect = speechObj.GetComponent<RectTransform>();
        speechRect.SetParent(panelRect, false);
        speechRect.anchorMin = new Vector2(0f, 0f);
        speechRect.anchorMax = new Vector2(0.52f, 0f);
        speechRect.pivot = new Vector2(0f, 0f);
        speechRect.offsetMin = new Vector2(60f, 100f);
        speechRect.offsetMax = new Vector2(-20f, 300f);

        VerticalLayoutGroup speechLayout = speechObj.GetComponent<VerticalLayoutGroup>();
        speechLayout.spacing = 6f;
        speechLayout.childControlWidth = true;
        speechLayout.childControlHeight = true;
        speechLayout.childForceExpandWidth = true;
        speechLayout.childForceExpandHeight = false;

        ContentSizeFitter speechFitter = speechObj.GetComponent<ContentSizeFitter>();
        speechFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        speechFitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;

        CreateTextItem(speechRect, "NpcNameText", 28f, FontStyles.Bold, new Color(1f, 0.909f, 0.627f, 1f));
        CreateTextItem(speechRect, "NpcSubtitleText", 20f, FontStyles.Italic, new Color(1f, 0.862f, 0.47f, 0.7f));
        TextMeshProUGUI body = CreateTextItem(speechRect, "NpcDialogueText", 22f, FontStyles.Normal, new Color(1f, 1f, 1f, 0.92f));
        body.lineSpacing = 1.4f;
        body.textWrappingMode = TextWrappingModes.Normal;

        GameObject choicesObj = new GameObject("PlayerChoicesArea", typeof(RectTransform), typeof(VerticalLayoutGroup));
        RectTransform choicesRect = choicesObj.GetComponent<RectTransform>();
        choicesRect.SetParent(panelRect, false);
        choicesRect.anchorMin = new Vector2(0.54f, 0f);
        choicesRect.anchorMax = new Vector2(1f, 0f);
        choicesRect.pivot = new Vector2(1f, 0f);
        choicesRect.offsetMin = new Vector2(0f, 100f);
        choicesRect.offsetMax = new Vector2(-60f, 380f);

        VerticalLayoutGroup choicesLayout = choicesObj.GetComponent<VerticalLayoutGroup>();
        choicesLayout.padding = new RectOffset(0, 0, 0, 0);
        choicesLayout.spacing = 14f;
        choicesLayout.childAlignment = TextAnchor.LowerRight;
        choicesLayout.childControlWidth = true;
        choicesLayout.childControlHeight = true;
        choicesLayout.childForceExpandWidth = false;
        choicesLayout.childForceExpandHeight = false;

        GameObject closeObj = new GameObject("CloseDialogue", typeof(RectTransform), typeof(Image), typeof(Button));
        RectTransform closeRect = closeObj.GetComponent<RectTransform>();
        closeRect.SetParent(panelRect, false);
        closeRect.anchorMin = new Vector2(1f, 1f);
        closeRect.anchorMax = new Vector2(1f, 1f);
        closeRect.pivot = new Vector2(1f, 1f);
        closeRect.anchoredPosition = new Vector2(-24f, -24f);
        closeRect.sizeDelta = new Vector2(36f, 36f);

        Image closeBg = closeObj.GetComponent<Image>();
        closeBg.color = new Color(0.15f, 0.18f, 0.24f, 0.75f);

        Button closeBtn = closeObj.GetComponent<Button>();
        closeBtn.transition = Selectable.Transition.ColorTint;

        TextMeshProUGUI closeLabel = CreateTextItem(closeRect, "Label", 18f, FontStyles.Bold, new Color(1f, 1f, 1f, 0.9f));
        closeLabel.text = "x";
        closeLabel.alignment = TextAlignmentOptions.Center;

        panelObj.SetActive(false);
        return panelRect;
    }

    private TextMeshProUGUI CreateTextItem(Transform parent, string name, float size, FontStyles style, Color color)
    {
        GameObject textObj = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
        RectTransform rect = textObj.GetComponent<RectTransform>();
        rect.SetParent(parent, false);

        LayoutElement layout = textObj.GetComponent<LayoutElement>();
        layout.minHeight = 18f;

        TextMeshProUGUI text = textObj.GetComponent<TextMeshProUGUI>();
        text.text = string.Empty;
        text.fontSize = size;
        text.fontStyle = style;
        text.color = color;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Truncate;
        text.alignment = TextAlignmentOptions.TopLeft;
        return text;
    }

    private void ConfigureTextStyles()
    {
        if (npcNameText != null)
        {
            npcNameText.fontSize = 28f;
            npcNameText.fontStyle = FontStyles.Bold;
            npcNameText.color = new Color(1f, 0.909f, 0.627f, 1f);
            npcNameText.alignment = TextAlignmentOptions.TopLeft;
        }

        if (npcSubtitleText != null)
        {
            npcSubtitleText.fontSize = 20f;
            npcSubtitleText.fontStyle = FontStyles.Italic;
            npcSubtitleText.color = new Color(1f, 0.862f, 0.47f, 0.7f);
            npcSubtitleText.alignment = TextAlignmentOptions.TopLeft;
        }

        if (dialogueText != null)
        {
            dialogueText.fontSize = 22f;
            dialogueText.color = new Color(1f, 1f, 1f, 0.92f);
            dialogueText.alignment = TextAlignmentOptions.TopLeft;
            dialogueText.textWrappingMode = TextWrappingModes.Normal;
            dialogueText.lineSpacing = 1.4f;
            dialogueText.overflowMode = TextOverflowModes.Ellipsis;
            dialogueText.enableAutoSizing = false;
        }
    }

    private void ConfigureSpeechArea()
    {
        if (npcSpeechArea != null)
        {
            npcSpeechArea.anchorMin = new Vector2(0f, 0f);
            npcSpeechArea.anchorMax = new Vector2(0.52f, 0f);
            npcSpeechArea.pivot = new Vector2(0f, 0f);
            npcSpeechArea.offsetMin = new Vector2(60f, 100f);
            npcSpeechArea.offsetMax = new Vector2(-20f, 300f);
        }

        if (npcNameText != null)
            npcNameText.fontSize = 28f;

        if (npcSubtitleText != null)
            npcSubtitleText.fontSize = 20f;

        if (dialogueText != null)
        {
            dialogueText.fontSize = 22f;
            dialogueText.textWrappingMode = TextWrappingModes.Normal;
            dialogueText.lineSpacing = 1.4f;
        }
    }

    private void ConfigureGradientOverlay()
    {
        if (gradientOverlay == null)
            return;

        RectTransform rect = gradientOverlay.rectTransform;
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(0f, 400f);

        gradientOverlay.color = new Color(0f, 0f, 0f, 180f / 255f);
        gradientOverlay.raycastTarget = false;
    }

    private void ConfigureLetterboxBars()
    {
        ConfigureLetterboxBar(letterboxTop, true);
        ConfigureLetterboxBar(letterboxBottom, false);
        SetLetterboxHeight(0f);
    }

    private void ConfigureLetterboxBar(Image bar, bool top)
    {
        if (bar == null)
            return;

        RectTransform rect = bar.rectTransform;
        rect.anchorMin = top ? new Vector2(0f, 1f) : new Vector2(0f, 0f);
        rect.anchorMax = top ? new Vector2(1f, 1f) : new Vector2(1f, 0f);
        rect.pivot = top ? new Vector2(0.5f, 1f) : new Vector2(0.5f, 0f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(0f, 0f);

        bar.color = new Color(0f, 0f, 0f, 0.95f);
        bar.raycastTarget = false;
    }

    private static Image CreateLetterboxBar(RectTransform parent, string name, bool top)
    {
        GameObject barObj = new GameObject(name, typeof(RectTransform), typeof(Image));
        RectTransform rect = barObj.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = top ? new Vector2(0f, 1f) : new Vector2(0f, 0f);
        rect.anchorMax = top ? new Vector2(1f, 1f) : new Vector2(1f, 0f);
        rect.pivot = top ? new Vector2(0.5f, 1f) : new Vector2(0.5f, 0f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;

        Image image = barObj.GetComponent<Image>();
        image.color = new Color(0f, 0f, 0f, 0.95f);
        image.raycastTarget = false;
        return image;
    }

    private void SetLetterboxHeight(float height)
    {
        float h = Mathf.Max(0f, height);

        if (letterboxTop != null)
            letterboxTop.rectTransform.sizeDelta = new Vector2(0f, h);

        if (letterboxBottom != null)
            letterboxBottom.rectTransform.sizeDelta = new Vector2(0f, h);
    }

    private void ConfigureChoicesArea()
    {
        if (choiceContainer == null)
            return;

        choiceContainer.anchorMin = new Vector2(0.54f, 0f);
        choiceContainer.anchorMax = new Vector2(1f, 0f);
        choiceContainer.pivot = new Vector2(1f, 0f);
        choiceContainer.offsetMin = new Vector2(0f, 100f);
        choiceContainer.offsetMax = new Vector2(-60f, 380f);

        VerticalLayoutGroup layout = choiceContainer.GetComponent<VerticalLayoutGroup>();
        if (layout != null)
        {
            layout.padding = new RectOffset(0, 0, 0, 0);
            layout.spacing = 14f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            layout.childAlignment = TextAnchor.LowerRight;
        }
    }

    private void ApplyChoiceCardSize(ChoiceCardRef card)
    {
        if (card == null || card.LabelText == null || card.Rect == null || card.Layout == null)
            return;

        card.Layout.minWidth = 320f;
        card.Layout.preferredWidth = 420f;
        card.Layout.flexibleWidth = 0f;

        float labelHeight = card.LabelText.GetPreferredValues(card.LabelText.text, 380f, 0f).y;
        float cardHeight = Mathf.Max(choiceCardHeight, labelHeight + 18f);

        card.Layout.preferredHeight = cardHeight;
        card.Rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 420f);
        card.Rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, cardHeight);
    }

    private Canvas FindHudCanvas()
    {
        Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < canvases.Length; i++)
        {
            if (canvases[i] != null && string.Equals(canvases[i].name, "HUD_Canvas", StringComparison.OrdinalIgnoreCase))
            {
                EnsureHudCanvasComponents(canvases[i]);
                return canvases[i];
            }
        }

        GameObject hudObj = new GameObject("HUD_Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas hudCanvas = hudObj.GetComponent<Canvas>();
        hudCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        hudCanvas.sortingOrder = 50;

        CanvasScaler scaler = hudObj.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        EnsureHudCanvasComponents(hudCanvas);

        return hudCanvas;
    }

    private static void EnsureHudCanvasComponents(Canvas canvas)
    {
        if (canvas == null)
            return;

        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        if (canvas.sortingOrder < 50)
            canvas.sortingOrder = 50;

        CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
        if (scaler == null)
            scaler = canvas.gameObject.AddComponent<CanvasScaler>();

        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        GraphicRaycaster raycaster = canvas.GetComponent<GraphicRaycaster>();
        if (raycaster == null)
            raycaster = canvas.gameObject.AddComponent<GraphicRaycaster>();

        raycaster.enabled = true;
    }

    private void StretchFull(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.pivot = new Vector2(0.5f, 0.5f);
    }

    private void UpdateNpcHeader()
    {
        if (npcNameText != null)
            npcNameText.text = string.IsNullOrWhiteSpace(activeGraph.npcDisplayName) ? "NPC" : activeGraph.npcDisplayName;

        if (npcSubtitleText != null)
            npcSubtitleText.text = BuildSubtitle(activeGraph);
    }

    private string BuildSubtitle(DialogueGraphData graph)
    {
        if (graph == null)
            return string.Empty;

        if (!string.IsNullOrWhiteSpace(graph.npcId)
            && (string.Equals(graph.npcId, "npc_boss", StringComparison.OrdinalIgnoreCase)
                || string.Equals(graph.npcId, "npc_boss_post", StringComparison.OrdinalIgnoreCase)))
            return "Manajer";

        if (!string.IsNullOrWhiteSpace(graph.npcId) && graph.npcId.IndexOf("restoran", StringComparison.OrdinalIgnoreCase) >= 0)
            return "Penjaga Resto";

        return "Warga Kota";
    }

    private void PauseGameplay()
    {
        if (playerController != null)
            playerController.LockMovement("NpcDialogue");

        if (!dialogueModalOpened && ModalStateManager.Instance != null)
        {
            ModalStateManager.Instance.OpenModal("NpcDialogue");
            dialogueModalOpened = true;
        }

        CaptureCursorState();
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (interactionController != null)
        {
            wasInteractionEnabled = interactionController.enabled;
            interactionController.enabled = false;
            interactionController.SetGlobalPromptSuppressed(true);
            interactionController.HideAllBubbles();
        }

        gameplayPausedByDialogue = true;
    }

    private void ResumeGameplay()
    {
        if (playerController != null)
            playerController.UnlockMovement("NpcDialogue");

        if (dialogueModalOpened && ModalStateManager.Instance != null)
        {
            ModalStateManager.Instance.CloseModal("NpcDialogue");
            dialogueModalOpened = false;
        }

        RestoreCursorState();

        if (interactionController != null)
        {
            interactionController.SetGlobalPromptSuppressed(false);
            interactionController.enabled = wasInteractionEnabled;
        }

        gameplayPausedByDialogue = false;
    }

    private void PlayOpenAnimation()
    {
        if (panelAnimationRoutine != null)
            StopCoroutine(panelAnimationRoutine);

        panelAnimationRoutine = StartCoroutine(OpenRoutine());
    }

    private IEnumerator OpenRoutine()
    {
        SetMenuVisible(true);
        yield return RunPanelAnimation(open: true);
        panelAnimationRoutine = null;
    }

    private IEnumerator RunPanelAnimation(bool open)
    {
        if (dialoguePanel == null)
            yield break;

        float duration = open ? Mathf.Max(0.01f, openDuration) : Mathf.Max(0.01f, closeDuration);
        float elapsed = 0f;

        Color grad = gradientOverlay != null ? gradientOverlay.color : new Color(0f, 0f, 0f, 0f);
        float fromAlpha = open ? 0f : overlayTargetAlpha;
        float toAlpha = open ? overlayTargetAlpha : 0f;
        float fromLetterbox = open ? 0f : letterboxHeight;
        float toLetterbox = open ? letterboxHeight : 0f;

        Vector2 speechFrom = speechBasePos + new Vector2(0f, open ? -openSlideOffset : 0f);
        Vector2 speechTo = speechBasePos + new Vector2(0f, open ? 0f : -openSlideOffset);

        Vector2 choicesFrom = choicesBasePos + new Vector2(open ? openSlideOffset : 0f, 0f);
        Vector2 choicesTo = choicesBasePos + new Vector2(open ? 0f : openSlideOffset, 0f);

        if (gradientOverlay != null)
        {
            grad.a = fromAlpha;
            gradientOverlay.color = grad;
        }

        if (npcSpeechArea != null)
            npcSpeechArea.anchoredPosition = speechFrom;

        if (choiceContainer != null)
            choiceContainer.anchoredPosition = choicesFrom;

        SetLetterboxHeight(fromLetterbox);

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = 1f - Mathf.Pow(1f - t, 3f);

            if (gradientOverlay != null)
            {
                grad.a = Mathf.Lerp(fromAlpha, toAlpha, eased);
                gradientOverlay.color = grad;
            }

            if (npcSpeechArea != null)
                npcSpeechArea.anchoredPosition = Vector2.Lerp(speechFrom, speechTo, eased);

            if (choiceContainer != null)
                choiceContainer.anchoredPosition = Vector2.Lerp(choicesFrom, choicesTo, eased);

            SetLetterboxHeight(Mathf.Lerp(fromLetterbox, toLetterbox, eased));

            yield return null;
        }

        if (gradientOverlay != null)
        {
            grad.a = toAlpha;
            gradientOverlay.color = grad;
        }

        if (npcSpeechArea != null)
            npcSpeechArea.anchoredPosition = speechTo;

        if (choiceContainer != null)
            choiceContainer.anchoredPosition = choicesTo;

        SetLetterboxHeight(toLetterbox);
    }

    private void SetMenuVisible(bool visible)
    {
        if (dialoguePanel != null)
            dialoguePanel.gameObject.SetActive(visible);
    }

    private void CacheControllers()
    {
        if (playerController == null)
            playerController = FindFirstObjectByType<PlayerController>();

        if (interactionController == null)
            interactionController = FindFirstObjectByType<UniversalInteractionController>();
    }

    private void EnsureEventSystem()
    {
        EventSystem eventSystem = FindFirstObjectByType<EventSystem>();

        if (eventSystem == null)
            eventSystem = FindFirstObjectByType<EventSystem>(FindObjectsInactive.Include);

        if (eventSystem == null)
        {
            GameObject eventObj = new GameObject("EventSystem");
            eventSystem = eventObj.AddComponent<EventSystem>();
        }

        StandaloneInputModule inputModule = eventSystem.GetComponent<StandaloneInputModule>();

    #if ENABLE_LEGACY_INPUT_MANAGER
        if (inputModule == null)
            inputModule = eventSystem.gameObject.AddComponent<StandaloneInputModule>();

        inputModule.enabled = true;
    #else
        if (inputModule != null)
            inputModule.enabled = false;
    #endif

    #if ENABLE_INPUT_SYSTEM
        InputSystemUIInputModule inputSystemModule = eventSystem.GetComponent<InputSystemUIInputModule>();

    #if ENABLE_LEGACY_INPUT_MANAGER
        if (inputSystemModule != null)
            inputSystemModule.enabled = false;
    #else
        if (inputSystemModule == null)
            inputSystemModule = eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();

        inputSystemModule.enabled = true;
    #endif
    #endif

        eventSystem.sendNavigationEvents = true;
        eventSystem.enabled = true;
        eventSystem.gameObject.SetActive(true);
    }

    private void CaptureCursorState()
    {
        if (cursorStateCaptured)
            return;

        previousCursorLockMode = Cursor.lockState;
        previousCursorVisible = Cursor.visible;
        cursorStateCaptured = true;
    }

    private void RestoreCursorState()
    {
        if (!cursorStateCaptured)
            return;

        Cursor.lockState = previousCursorLockMode;
        Cursor.visible = previousCursorVisible;
        cursorStateCaptured = false;
    }

    private string FormatChoiceLabel(string source)
    {
        if (string.IsNullOrWhiteSpace(source))
            return "Pilihan";

        return CompactText(source.Trim(), maxChoiceChars);
    }

    private void SetDialogueLine(string source)
    {
        if (dialogueText == null)
            return;

        string line = CompactText(source, maxDialogueChars);
        StopDialogueTextRoutine();

        if (!useTypewriterText || string.IsNullOrEmpty(line))
        {
            dialogueText.text = line;
            dialogueText.maxVisibleCharacters = int.MaxValue;
            return;
        }

        dialogueTextRoutine = StartCoroutine(TypeDialogueRoutine(line));
    }

    private IEnumerator TypeDialogueRoutine(string line)
    {
        dialogueText.text = line;
        dialogueText.maxVisibleCharacters = 0;
        dialogueText.ForceMeshUpdate();

        int totalChars = dialogueText.textInfo.characterCount;
        if (totalChars <= 0)
        {
            dialogueText.maxVisibleCharacters = int.MaxValue;
            dialogueTextRoutine = null;
            yield break;
        }

        float charInterval = 1f / Mathf.Max(1f, typewriterCharsPerSecond);
        float timer = 0f;
        int visible = 0;

        while (visible < totalChars)
        {
            timer += Time.unscaledDeltaTime;
            if (timer < charInterval)
            {
                yield return null;
                continue;
            }

            timer -= charInterval;
            visible++;
            dialogueText.maxVisibleCharacters = visible;

            int sourceIndex = Mathf.Clamp(visible - 1, 0, line.Length - 1);
            if (sourceIndex < line.Length && IsPunctuationForPause(line[sourceIndex]))
                timer -= Mathf.Max(0f, typewriterPunctuationPause);

            yield return null;
        }

        dialogueText.maxVisibleCharacters = int.MaxValue;
        dialogueTextRoutine = null;
    }

    private void StopDialogueTextRoutine()
    {
        if (dialogueTextRoutine != null)
        {
            StopCoroutine(dialogueTextRoutine);
            dialogueTextRoutine = null;
        }

        if (dialogueText != null)
            dialogueText.maxVisibleCharacters = int.MaxValue;
    }

    private static bool IsPunctuationForPause(char c)
    {
        return c == ',' || c == '.' || c == '!' || c == '?' || c == ';' || c == ':';
    }

    private string CompactText(string source, int maxChars)
    {
        if (string.IsNullOrWhiteSpace(source))
            return string.Empty;

        string clean = source.Replace("\r\n", " ").Replace('\n', ' ').Trim();
        if (maxChars < 8 || clean.Length <= maxChars)
            return clean;

        return clean.Substring(0, maxChars - 3).TrimEnd() + "...";
    }
}