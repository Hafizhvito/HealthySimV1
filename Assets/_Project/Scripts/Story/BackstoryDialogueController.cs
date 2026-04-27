using System.Collections;
using System;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class BackstoryDialogueController : MonoBehaviour
{
    private const string DefaultShownPrefsKey = "healthsim.backstory.shown";
    private const string ModalLockKey = "backstory";

    [Header("Data")]
    [SerializeField] private CharacterData characterData;
    [SerializeField] private string preferredCharacterDataAssetName = "PlayerCharacter";
    [SerializeField] private bool showOnlyOnce = true;
    [SerializeField] private string shownPrefsKey = DefaultShownPrefsKey;
    [SerializeField] private bool ignoreShownStateInEditor = true;
    [SerializeField] private bool verboseLogs = true;

    [Header("Bindings")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI bodyText;
    [SerializeField] private Button continueButton;

    [Header("Backstory Warning Suppression")]
    [SerializeField] private bool suppressWarningDuringBackstory = true;
    [SerializeField] private GameObject warningTextObject;

    [Header("Typewriter")]
    [SerializeField] private float nameCharsPerSecond = 48f;
    [SerializeField] private float bodyCharsPerSecond = 64f;

    private Coroutine typewriterRoutine;
    private string activeNameText = string.Empty;
    private string activeBodyText = string.Empty;
    private CanvasGroup panelCanvasGroup;
    private bool isShowing;
    private bool isTyping;
    private bool modalOpenedByController;
    private bool warningWasSuppressed;
    private bool warningPreviousActive;

    public event System.Action OnDialogueComplete;

    private void Awake()
    {
        if (panelRoot == null)
            panelRoot = gameObject;

        ResolveCharacterDataIfNeeded();
        TryAutoBindIfNeeded();
        BindContinueButton();
    }

    private void Start()
    {
        if (!isShowing)
            HideImmediate();
    }

    private void OnEnable()
    {
        TryAutoBindIfNeeded();
        BindContinueButton();
    }

    public void Show()
    {
        Show(null);
    }

    public void Show(StoryTemplate template)
    {
        ResolveCharacterDataIfNeeded();
        TryAutoBindIfNeeded();
        TryCreateRuntimeBindingsIfNeeded();
        TryAutoBindIfNeeded();
        BindContinueButton();

        if (isShowing)
            return;

        bool shouldIgnoreShownState = Application.isEditor && ignoreShownStateInEditor;
        bool alreadyShown = showOnlyOnce && HasShown();
        if (alreadyShown && !shouldIgnoreShownState)
        {
            if (verboseLogs)
                Debug.Log("[BackstoryDialogueController] Show di-skip karena sudah pernah ditampilkan.");

            return;
        }

        if (!HasRequiredBindings())
        {
            Debug.LogWarning("[BackstoryDialogueController] Show dibatalkan: binding UI belum lengkap.");
            return;
        }

        string resolvedName = ResolveTitle();
        string resolvedBody = BuildBodyText(template);
        if (string.IsNullOrWhiteSpace(resolvedBody))
            return;

        activeNameText = resolvedName;
        activeBodyText = resolvedBody;

        isShowing = true;
        panelRoot.SetActive(true);
        panelRoot.transform.SetAsLastSibling();

        if (panelCanvasGroup != null)
        {
            panelCanvasGroup.alpha = 1f;
            panelCanvasGroup.interactable = true;
            panelCanvasGroup.blocksRaycasts = true;
        }

        nameText.text = string.Empty;
        bodyText.text = string.Empty;

        if (continueButton != null)
            continueButton.interactable = false;

        OpenModalLock();
        SuppressWarningIfNeeded();

        if (typewriterRoutine != null)
            StopCoroutine(typewriterRoutine);

        typewriterRoutine = StartCoroutine(TypewriterRoutine(resolvedName, resolvedBody));

        if (verboseLogs)
            Debug.Log("[BackstoryDialogueController] Backstory panel ditampilkan.");
    }

    public IEnumerator ShowBackstoryOnceRoutine(StoryTemplate template)
    {
        Show(template);
        while (isShowing)
            yield return null;
    }

    private void OnDisable()
    {
        if (typewriterRoutine != null)
        {
            StopCoroutine(typewriterRoutine);
            typewriterRoutine = null;
        }

        isTyping = false;

        if (!isShowing)
            return;

        CloseModalLock();
        RestoreWarningIfNeeded();
        isShowing = false;
    }

    private bool HasShown()
    {
        return PlayerPrefs.GetInt(GetShownPrefsKey(), 0) == 1;
    }

    private void MarkShownIfNeeded()
    {
        if (!showOnlyOnce)
            return;

        PlayerPrefs.SetInt(GetShownPrefsKey(), 1);
        PlayerPrefs.Save();
    }

    private string GetShownPrefsKey()
    {
        if (string.IsNullOrWhiteSpace(shownPrefsKey))
            return DefaultShownPrefsKey;

        return shownPrefsKey.Trim();
    }

    private string ResolveTitle()
    {
        ResolveCharacterDataIfNeeded();

        if (characterData != null && !string.IsNullOrWhiteSpace(characterData.characterName))
            return characterData.characterName.Trim();

        if (PlayerStats.Instance != null && !string.IsNullOrWhiteSpace(PlayerStats.Instance.PlayerName))
            return PlayerStats.Instance.PlayerName.Trim();

        return "Latar Belakang Karakter";
    }

    private string BuildBodyText(StoryTemplate template)
    {
        ResolveCharacterDataIfNeeded();

        StringBuilder sb = new StringBuilder(256);

        if (characterData != null)
            characterData.AppendBackstory(sb);

        if (sb.Length == 0)
            sb.Append(BuildDefaultBackstoryText());

        return sb.ToString().Trim();
    }

    private string BuildDefaultBackstoryText()
    {
        string name = ResolveTitle();

        return $"{name} sedang memulai fase baru untuk hidup lebih sehat.\n\n" +
               "Selama ini pola makan, istirahat, dan aktivitas harian belum konsisten.\n\n" +
               "Sekarang kamu memilih membangun kebiasaan sehat sedikit demi sedikit, dimulai dari keputusan kecil setiap hari.";
    }

    private void ResolveCharacterDataIfNeeded()
    {
        if (characterData != null)
            return;

        CharacterData[] loaded = Resources.FindObjectsOfTypeAll<CharacterData>();
        if (loaded != null && loaded.Length > 0)
        {
            for (int i = 0; i < loaded.Length; i++)
            {
                if (loaded[i] == null)
                    continue;

                if (!string.IsNullOrWhiteSpace(preferredCharacterDataAssetName)
                    && string.Equals(loaded[i].name, preferredCharacterDataAssetName, StringComparison.OrdinalIgnoreCase))
                {
                    characterData = loaded[i];
                    break;
                }
            }

            if (characterData == null)
                characterData = loaded[0];
        }

#if UNITY_EDITOR
        if (characterData == null)
        {
            string query = string.IsNullOrWhiteSpace(preferredCharacterDataAssetName)
                ? "t:CharacterData"
                : $"t:CharacterData {preferredCharacterDataAssetName}";

            string[] guids = AssetDatabase.FindAssets(query);
            if (guids.Length == 0)
                guids = AssetDatabase.FindAssets("t:CharacterData");

            if (guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                characterData = AssetDatabase.LoadAssetAtPath<CharacterData>(path);
            }
        }
#endif

        if (characterData != null && verboseLogs)
            Debug.Log($"[BackstoryDialogueController] CharacterData aktif: {characterData.name}");
    }

    private static void AppendLines(StringBuilder sb, string[] lines)
    {
        if (sb == null || lines == null)
            return;

        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i];
            if (string.IsNullOrWhiteSpace(line))
                continue;

            if (sb.Length > 0)
                sb.AppendLine();

            sb.Append(line.Trim());
        }
    }

    private bool HasRequiredBindings()
    {
        return panelRoot != null
            && nameText != null
            && bodyText != null
            && continueButton != null;
    }

    private void TryAutoBindIfNeeded()
    {
        if (panelRoot == null)
            panelRoot = gameObject;

        if (panelRoot == null)
            return;

        if (panelCanvasGroup == null)
            panelCanvasGroup = panelRoot.GetComponent<CanvasGroup>();

        if (nameText == null)
        {
            Transform named = panelRoot.transform.Find("CharacterNameText");
            if (named != null)
                nameText = named.GetComponent<TextMeshProUGUI>();

            if (nameText == null)
            {
                TextMeshProUGUI[] tmps = panelRoot.GetComponentsInChildren<TextMeshProUGUI>(true);
                if (tmps.Length > 0)
                    nameText = tmps[0];
            }
        }

        if (bodyText == null)
        {
            Transform named = panelRoot.transform.Find("BackstoryBodyText");
            if (named != null)
                bodyText = named.GetComponent<TextMeshProUGUI>();

            if (bodyText == null)
            {
                TextMeshProUGUI[] tmps = panelRoot.GetComponentsInChildren<TextMeshProUGUI>(true);
                if (tmps.Length > 1)
                    bodyText = tmps[1];
            }
        }

        if (continueButton == null)
        {
            Transform named = panelRoot.transform.Find("ContinueButton");
            if (named != null)
                continueButton = named.GetComponent<Button>();

            if (continueButton == null)
                continueButton = panelRoot.GetComponentInChildren<Button>(true);
        }
    }

    private void TryCreateRuntimeBindingsIfNeeded()
    {
        EnsureCanvasParent();

        if (panelRoot == null)
            panelRoot = gameObject;

        EnsureModernPanelLayout();

        if (panelCanvasGroup == null)
            panelCanvasGroup = panelRoot.GetComponent<CanvasGroup>();

        if (panelCanvasGroup == null)
            panelCanvasGroup = panelRoot.AddComponent<CanvasGroup>();

        RectTransform panelRect = panelRoot.GetComponent<RectTransform>();
        if (panelRect == null)
            panelRect = panelRoot.AddComponent<RectTransform>();

        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        if (!isShowing)
            panelRoot.SetActive(false);

        TryAutoBindIfNeeded();

        EnsureEventSystemExists();
    }

    private void EnsureCanvasParent()
    {
        Canvas parentCanvas = GetComponentInParent<Canvas>(true);
        if (parentCanvas != null)
            return;

        Canvas canvas = FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);
        if (canvas == null)
        {
            GameObject canvasGo = new GameObject("BackstoryCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
        }

        transform.SetParent(canvas.transform, false);
    }

    private void EnsureModernPanelLayout()
    {
        if (panelRoot == null)
            return;

        Image rootImage = panelRoot.GetComponent<Image>();
        if (rootImage != null)
            rootImage.enabled = false;

        RectTransform rootRect = panelRoot.GetComponent<RectTransform>();
        if (rootRect == null)
            rootRect = panelRoot.AddComponent<RectTransform>();

        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;

        GameObject overlay = EnsureChild(panelRoot.transform, "Overlay", typeof(RectTransform), typeof(Image));
        RectTransform overlayRect = overlay.GetComponent<RectTransform>();
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = Vector2.zero;
        overlayRect.offsetMax = Vector2.zero;

        Image overlayImage = overlay.GetComponent<Image>();
        overlayImage.color = new Color(0f, 0f, 0f, 0.75f);
        overlayImage.raycastTarget = true;

        GameObject shadow = EnsureChild(panelRoot.transform, "Shadow", typeof(RectTransform), typeof(Image));
        RectTransform shadowRect = shadow.GetComponent<RectTransform>();
        shadowRect.anchorMin = new Vector2(0.5f, 0.5f);
        shadowRect.anchorMax = new Vector2(0.5f, 0.5f);
        shadowRect.pivot = new Vector2(0.5f, 0.5f);
        shadowRect.anchoredPosition = new Vector2(6f, -6f);
        shadowRect.sizeDelta = new Vector2(764f, 424f);

        Image shadowImage = shadow.GetComponent<Image>();
        shadowImage.color = new Color(0f, 0f, 0f, 0.25f);
        shadowImage.raycastTarget = false;

        GameObject card = EnsureChild(panelRoot.transform, "Card", typeof(RectTransform), typeof(Image));
        RectTransform cardRect = card.GetComponent<RectTransform>();
        cardRect.anchorMin = new Vector2(0.5f, 0.5f);
        cardRect.anchorMax = new Vector2(0.5f, 0.5f);
        cardRect.pivot = new Vector2(0.5f, 0.5f);
        cardRect.anchoredPosition = Vector2.zero;
        cardRect.sizeDelta = new Vector2(760f, 420f);

        Image cardImage = card.GetComponent<Image>();
        cardImage.color = Color.white;
        cardImage.raycastTarget = true;

        TextMeshProUGUI cardNameText = EnsureTmpText(card.transform, "CharacterNameText");
        RectTransform nameRect = cardNameText.rectTransform;
        nameRect.anchorMin = new Vector2(0.5f, 1f);
        nameRect.anchorMax = new Vector2(0.5f, 1f);
        nameRect.pivot = new Vector2(0.5f, 1f);
        nameRect.anchoredPosition = new Vector2(0f, -40f);
        nameRect.sizeDelta = new Vector2(680f, 60f);
        cardNameText.fontSize = 38f;
        cardNameText.fontStyle = FontStyles.Bold;
        cardNameText.color = new Color32(0x1A, 0x1A, 0x2E, 0xFF);
        cardNameText.alignment = TextAlignmentOptions.Center;
        cardNameText.text = string.Empty;
        cardNameText.textWrappingMode = TextWrappingModes.Normal;
        cardNameText.overflowMode = TextOverflowModes.Overflow;

        GameObject divider = EnsureChild(card.transform, "Divider", typeof(RectTransform), typeof(Image));
        RectTransform dividerRect = divider.GetComponent<RectTransform>();
        dividerRect.anchorMin = new Vector2(0.5f, 1f);
        dividerRect.anchorMax = new Vector2(0.5f, 1f);
        dividerRect.pivot = new Vector2(0.5f, 1f);
        dividerRect.anchoredPosition = new Vector2(0f, -90f);
        dividerRect.sizeDelta = new Vector2(600f, 2f);
        Image dividerImage = divider.GetComponent<Image>();
        dividerImage.color = new Color32(0xE0, 0xE0, 0xE0, 0xFF);
        dividerImage.raycastTarget = false;

        TextMeshProUGUI cardBodyText = EnsureTmpText(card.transform, "BackstoryBodyText");
        RectTransform bodyRect = cardBodyText.rectTransform;
        bodyRect.anchorMin = new Vector2(0.5f, 1f);
        bodyRect.anchorMax = new Vector2(0.5f, 1f);
        bodyRect.pivot = new Vector2(0.5f, 1f);
        bodyRect.anchoredPosition = new Vector2(0f, -120f);
        bodyRect.sizeDelta = new Vector2(680f, 220f);
        cardBodyText.fontSize = 22f;
        cardBodyText.fontStyle = FontStyles.Normal;
        cardBodyText.color = new Color32(0x33, 0x33, 0x33, 0xFF);
        cardBodyText.alignment = TextAlignmentOptions.Center;
        cardBodyText.text = string.Empty;
        cardBodyText.textWrappingMode = TextWrappingModes.Normal;
        cardBodyText.overflowMode = TextOverflowModes.Overflow;

        Button cardContinueButton = EnsureButton(card.transform, "ContinueButton");
        RectTransform buttonRect = cardContinueButton.GetComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(1f, 0f);
        buttonRect.anchorMax = new Vector2(1f, 0f);
        buttonRect.pivot = new Vector2(1f, 0f);
        buttonRect.anchoredPosition = new Vector2(-30f, 30f);
        buttonRect.sizeDelta = new Vector2(160f, 48f);

        Image buttonImage = cardContinueButton.GetComponent<Image>();
        buttonImage.color = new Color32(0x21, 0x96, 0xF3, 0xFF);

        TextMeshProUGUI continueText = EnsureTmpText(cardContinueButton.transform, "ButtonText");
        RectTransform continueRect = continueText.rectTransform;
        continueRect.anchorMin = Vector2.zero;
        continueRect.anchorMax = Vector2.one;
        continueRect.offsetMin = Vector2.zero;
        continueRect.offsetMax = Vector2.zero;
        continueText.text = "Lanjut";
        continueText.fontSize = 20f;
        continueText.fontStyle = FontStyles.Bold;
        continueText.color = Color.white;
        continueText.alignment = TextAlignmentOptions.Center;
        continueText.textWrappingMode = TextWrappingModes.Normal;
        continueText.overflowMode = TextOverflowModes.Overflow;

        nameText = cardNameText;
        bodyText = cardBodyText;
        continueButton = cardContinueButton;
    }

    private static GameObject EnsureChild(Transform parent, string childName, params Type[] requiredComponents)
    {
        Transform existing = parent.Find(childName);
        GameObject child = existing != null ? existing.gameObject : new GameObject(childName, requiredComponents);
        if (existing == null)
            child.transform.SetParent(parent, false);

        for (int i = 0; i < requiredComponents.Length; i++)
        {
            Type componentType = requiredComponents[i];
            if (child.GetComponent(componentType) == null)
                child.AddComponent(componentType);
        }

        return child;
    }

    private static TextMeshProUGUI EnsureTmpText(Transform parent, string childName)
    {
        GameObject go = EnsureChild(parent, childName, typeof(RectTransform), typeof(TextMeshProUGUI));
        return go.GetComponent<TextMeshProUGUI>();
    }

    private static Button EnsureButton(Transform parent, string childName)
    {
        GameObject go = EnsureChild(parent, childName, typeof(RectTransform), typeof(Image), typeof(Button));
        Button button = go.GetComponent<Button>();
        button.targetGraphic = go.GetComponent<Image>();
        return button;
    }

    private static void EnsureEventSystemExists()
    {
        if (FindFirstObjectByType<EventSystem>(FindObjectsInactive.Include) != null)
            return;

        GameObject es = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        DontDestroyOnLoad(es);
    }

    private void SuppressWarningIfNeeded()
    {
        if (!suppressWarningDuringBackstory)
            return;

        TryResolveWarningTextObject();
        if (warningTextObject == null)
            return;

        warningPreviousActive = warningTextObject.activeSelf;
        if (warningPreviousActive)
            warningTextObject.SetActive(false);

        warningWasSuppressed = true;
    }

    private void RestoreWarningIfNeeded()
    {
        if (!warningWasSuppressed)
            return;

        if (warningTextObject != null)
            warningTextObject.SetActive(warningPreviousActive);

        warningWasSuppressed = false;
    }

    private void TryResolveWarningTextObject()
    {
        if (warningTextObject != null)
            return;

        HUDManager hud = FindFirstObjectByType<HUDManager>(FindObjectsInactive.Include);
        if (hud != null)
        {
            var warningField = typeof(HUDManager).GetField("warningPanel", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (warningField != null)
            {
                warningTextObject = warningField.GetValue(hud) as GameObject;
                if (warningTextObject != null)
                    return;
            }
        }

        GameObject byName = GameObject.Find("Warning_Text");
        if (byName == null)
            byName = GameObject.Find("warningPanel");

        if (byName == null)
        {
            GameObject[] allObjects = Resources.FindObjectsOfTypeAll<GameObject>();
            for (int i = 0; i < allObjects.Length; i++)
            {
                GameObject go = allObjects[i];
                if (go == null)
                    continue;

                if (go.name == "Warning_Text" || go.name == "warningPanel")
                {
                    byName = go;
                    break;
                }
            }
        }

        warningTextObject = byName;
    }

    private void OpenModalLock()
    {
        if (modalOpenedByController)
            return;

        if (ModalStateManager.Instance == null)
            return;

        ModalStateManager.Instance.OpenModal(ModalLockKey);
        modalOpenedByController = true;
    }

    private void CloseModalLock()
    {
        if (!modalOpenedByController)
            return;

        if (ModalStateManager.Instance != null)
            ModalStateManager.Instance.CloseModal(ModalLockKey);

        modalOpenedByController = false;
    }

    private void BindContinueButton()
    {
        if (continueButton == null)
            return;

        continueButton.onClick.RemoveListener(HandleContinuePressed);
        continueButton.onClick.AddListener(HandleContinuePressed);
    }

    private void HandleContinuePressed()
    {
        if (!isShowing)
            return;

        if (isTyping)
        {
            CompleteTypingInstant();
            return;
        }

        CloseDialogue();
    }

    private IEnumerator TypewriterRoutine(string characterName, string backstory)
    {
        isTyping = true;
        yield return StartCoroutine(TypeText(nameText, characterName, nameCharsPerSecond));
        yield return StartCoroutine(TypeText(bodyText, backstory, bodyCharsPerSecond));

        isTyping = false;
        typewriterRoutine = null;

        if (continueButton != null)
            continueButton.interactable = true;
    }

    private static IEnumerator TypeText(TextMeshProUGUI target, string fullText, float charsPerSecond)
    {
        if (target == null)
            yield break;

        string safeText = string.IsNullOrWhiteSpace(fullText) ? string.Empty : fullText;
        target.text = string.Empty;

        if (safeText.Length == 0)
        {
            yield break;
        }

        float cps = Mathf.Max(1f, charsPerSecond);
        float interval = 1f / cps;
        float timer = 0f;
        int index = 0;

        while (index < safeText.Length)
        {
            timer += Time.unscaledDeltaTime;
            while (timer >= interval && index < safeText.Length)
            {
                timer -= interval;
                index++;
            }

            target.text = safeText.Substring(0, index);
            yield return null;
        }
    }

    private void CompleteTypingInstant()
    {
        if (typewriterRoutine != null)
        {
            StopCoroutine(typewriterRoutine);
            typewriterRoutine = null;
        }

        isTyping = false;
        nameText.text = activeNameText;
        bodyText.text = activeBodyText;

        if (continueButton != null)
            continueButton.interactable = true;
    }

    private void CloseDialogue()
    {
        if (!isShowing)
            return;

        MarkShownIfNeeded();
        HideImmediate();
        CloseModalLock();
        RestoreWarningIfNeeded();
        isShowing = false;
        OnDialogueComplete?.Invoke();

        if (verboseLogs)
            Debug.Log("[BackstoryDialogueController] Backstory panel ditutup.");
    }

    private void HideImmediate()
    {
        if (panelCanvasGroup != null)
        {
            panelCanvasGroup.alpha = 0f;
            panelCanvasGroup.interactable = false;
            panelCanvasGroup.blocksRaycasts = false;
        }

        if (panelRoot != null)
            panelRoot.SetActive(false);
    }

}
