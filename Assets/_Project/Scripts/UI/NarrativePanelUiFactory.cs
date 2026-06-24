using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Runtime narrative panel layout (matches Editor NarrativePanelEditorLayout / BackstoryPanel).
/// </summary>
public static class NarrativePanelUiFactory
{
    public struct BuiltPanel
    {
        public RectTransform Root;
        public CanvasGroup Group;
        public TextMeshProUGUI Title;
        public TextMeshProUGUI Body;
        public Button ContinueButton;
    }

    private static readonly Color32 AccentColor = new Color32(0x21, 0x96, 0xF3, 0xFF);
    private static readonly Color32 TitleColor = new Color32(0x1A, 0x1A, 0x2E, 0xFF);
    private static readonly Color32 BodyColor = new Color32(0x44, 0x44, 0x44, 0xFF);
    private static readonly Color32 DividerColor = new Color32(0xE8, 0xE8, 0xE8, 0xFF);

    private const float CardWidth = 820f;
    private const float CardHeight = 480f;

    public static BuiltPanel Create(Canvas hudCanvas, int sortingOrder = 0)
    {
        BuiltPanel built = default;
        if (hudCanvas == null)
            return built;

        GameObject rootObj = new GameObject("NarrativePanelRoot", typeof(RectTransform), typeof(CanvasGroup));
        RectTransform root = rootObj.GetComponent<RectTransform>();
        root.SetParent(hudCanvas.transform, false);
        ApplyFullscreenRoot(root);
        root.SetAsLastSibling();

        if (sortingOrder > 0)
        {
            Canvas overlayCanvas = rootObj.AddComponent<Canvas>();
            overlayCanvas.overrideSorting = true;
            overlayCanvas.sortingOrder = sortingOrder;
            rootObj.AddComponent<GraphicRaycaster>();
        }

        CanvasGroup group = rootObj.GetComponent<CanvasGroup>();
        group.alpha = 1f;
        group.interactable = true;
        group.blocksRaycasts = true;

        EnsureOverlay(root);
        EnsureShadow(root);
        Transform card = EnsureCard(root).transform;
        EnsureAccentBar(card);
        EnsureDivider(card);

        built.Root = root;
        built.Group = group;
        built.Title = EnsureTitle(card, "TitleText");
        built.Body = EnsureBody(card, "BodyText");
        built.ContinueButton = EnsurePrimaryButton(card, "ContinueButton", "Mengerti →");
        ApplyDefaultFont(built.Title);
        ApplyDefaultFont(built.Body);
        ApplyDefaultFont(built.ContinueButton.GetComponentInChildren<TextMeshProUGUI>());

        return built;
    }

    private static void ApplyFullscreenRoot(RectTransform root)
    {
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.pivot = new Vector2(0.5f, 0.5f);
        root.anchoredPosition = Vector2.zero;
        root.sizeDelta = Vector2.zero;
        root.offsetMin = Vector2.zero;
        root.offsetMax = Vector2.zero;
        root.localScale = Vector3.one;
    }

    private static void EnsureOverlay(Transform parent)
    {
        GameObject overlay = CreateChild(parent, "Overlay");
        ApplyStretch(overlay.GetComponent<RectTransform>());
        Image image = overlay.GetComponent<Image>() ?? overlay.AddComponent<Image>();
        image.color = new Color(0f, 0f, 0f, 0.72f);
        image.raycastTarget = true;
        overlay.transform.SetSiblingIndex(0);
    }

    private static void EnsureShadow(Transform parent)
    {
        GameObject shadow = CreateChild(parent, "Shadow");
        RectTransform rect = shadow.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(8f, -8f);
        rect.sizeDelta = new Vector2(CardWidth + 8f, CardHeight + 8f);

        Image image = shadow.GetComponent<Image>() ?? shadow.AddComponent<Image>();
        image.color = new Color(0f, 0f, 0f, 0.28f);
        image.raycastTarget = false;
        shadow.transform.SetSiblingIndex(1);
    }

    private static Image EnsureCard(Transform parent)
    {
        GameObject card = CreateChild(parent, "Card");
        RectTransform rect = card.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(CardWidth, CardHeight);

        Image image = card.GetComponent<Image>() ?? card.AddComponent<Image>();
        image.color = Color.white;
        image.raycastTarget = true;
        card.transform.SetAsLastSibling();
        return image;
    }

    private static void EnsureAccentBar(Transform card)
    {
        GameObject accent = CreateChild(card, "AccentBar");
        RectTransform rect = accent.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = new Vector2(8f, 0f);

        Image image = accent.GetComponent<Image>() ?? accent.AddComponent<Image>();
        image.color = AccentColor;
        image.raycastTarget = false;
        accent.transform.SetAsFirstSibling();
    }

    private static void EnsureDivider(Transform card)
    {
        GameObject divider = CreateChild(card, "Divider");
        RectTransform rect = divider.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -108f);
        rect.sizeDelta = new Vector2(700f, 2f);

        Image image = divider.GetComponent<Image>() ?? divider.AddComponent<Image>();
        image.color = DividerColor;
        image.raycastTarget = false;
    }

    private static TextMeshProUGUI EnsureTitle(Transform card, string childName)
    {
        GameObject titleGo = CreateChild(card, childName);
        RectTransform rect = titleGo.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -36f);
        rect.sizeDelta = new Vector2(700f, 56f);

        TextMeshProUGUI tmp = titleGo.GetComponent<TextMeshProUGUI>() ?? titleGo.AddComponent<TextMeshProUGUI>();
        tmp.fontSize = 34f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color = TitleColor;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.textWrappingMode = TextWrappingModes.Normal;
        tmp.overflowMode = TextOverflowModes.Ellipsis;
        return tmp;
    }

    private static TextMeshProUGUI EnsureBody(Transform card, string childName)
    {
        GameObject bodyGo = CreateChild(card, childName);
        RectTransform rect = bodyGo.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = new Vector2(48f, 96f);
        rect.offsetMax = new Vector2(-48f, -132f);

        TextMeshProUGUI tmp = bodyGo.GetComponent<TextMeshProUGUI>() ?? bodyGo.AddComponent<TextMeshProUGUI>();
        tmp.fontSize = 20f;
        tmp.fontStyle = FontStyles.Normal;
        tmp.color = BodyColor;
        tmp.alignment = TextAlignmentOptions.TopLeft;
        tmp.textWrappingMode = TextWrappingModes.Normal;
        tmp.lineSpacing = 6f;
        return tmp;
    }

    private static Button EnsurePrimaryButton(Transform card, string childName, string label)
    {
        GameObject btnGo = CreateChild(card, childName);
        RectTransform rect = btnGo.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 0f);
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(1f, 0f);
        rect.anchoredPosition = new Vector2(-36f, 32f);
        rect.sizeDelta = new Vector2(196f, 54f);

        Image image = btnGo.GetComponent<Image>() ?? btnGo.AddComponent<Image>();
        image.color = AccentColor;

        Button button = btnGo.GetComponent<Button>() ?? btnGo.AddComponent<Button>();
        button.targetGraphic = image;

        TextMeshProUGUI labelTmp = EnsureButtonLabel(btnGo.transform, label);
        labelTmp.fontSize = 20f;
        labelTmp.fontStyle = FontStyles.Bold;
        labelTmp.color = Color.white;
        return button;
    }

    private static TextMeshProUGUI EnsureButtonLabel(Transform button, string label)
    {
        GameObject textGo = CreateChild(button, "ButtonText");
        ApplyStretch(textGo.GetComponent<RectTransform>());

        TextMeshProUGUI tmp = textGo.GetComponent<TextMeshProUGUI>() ?? textGo.AddComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.textWrappingMode = TextWrappingModes.Normal;
        return tmp;
    }

    private static GameObject CreateChild(Transform parent, string childName)
    {
        GameObject child = new GameObject(childName, typeof(RectTransform));
        child.transform.SetParent(parent, false);
        return child;
    }

    private static void ApplyStretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.localScale = Vector3.one;
    }

    private static void ApplyDefaultFont(TextMeshProUGUI text)
    {
        if (text == null)
            return;

        if (TMP_Settings.defaultFontAsset != null)
            text.font = TMP_Settings.defaultFontAsset;
        else
        {
            TMP_FontAsset fallback = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
            if (fallback != null)
                text.font = fallback;
        }
    }
}
