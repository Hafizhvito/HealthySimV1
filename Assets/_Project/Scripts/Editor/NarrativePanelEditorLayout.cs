using TMPro;
using UnityEngine;
using UnityEngine.UI;

internal static class NarrativePanelEditorLayout
{
    internal static readonly Color32 AccentColor = new Color32(0x21, 0x96, 0xF3, 0xFF);
    internal static readonly Color32 TitleColor = new Color32(0x1A, 0x1A, 0x2E, 0xFF);
    internal static readonly Color32 BodyColor = new Color32(0x44, 0x44, 0x44, 0xFF);
    internal static readonly Color32 DividerColor = new Color32(0xE8, 0xE8, 0xE8, 0xFF);

    internal const float CardWidth = 820f;
    internal const float CardHeight = 480f;

    internal static void ApplyFullscreenRoot(RectTransform root)
    {
        if (root == null)
            return;

        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.pivot = new Vector2(0.5f, 0.5f);
        root.anchoredPosition = Vector2.zero;
        root.sizeDelta = Vector2.zero;
        root.offsetMin = Vector2.zero;
        root.offsetMax = Vector2.zero;
        root.localScale = Vector3.one;
    }

    internal static Image EnsureOverlay(Transform parent)
    {
        GameObject overlay = EnsureChild(parent, "Overlay");
        RectTransform rect = overlay.GetComponent<RectTransform>();
        ApplyStretch(rect);

        Image image = overlay.GetComponent<Image>() ?? overlay.AddComponent<Image>();
        image.color = new Color(0f, 0f, 0f, 0.72f);
        image.raycastTarget = true;
        overlay.transform.SetSiblingIndex(0);
        return image;
    }

    internal static Image EnsureShadow(Transform parent)
    {
        GameObject shadow = EnsureChild(parent, "Shadow");
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
        return image;
    }

    internal static Image EnsureCard(Transform parent)
    {
        GameObject card = EnsureChild(parent, "Card");
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

    internal static Image EnsureAccentBar(Transform card)
    {
        GameObject accent = EnsureChild(card, "AccentBar");
        RectTransform rect = accent.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = new Vector2(8f, 0f);
        rect.sizeDelta = new Vector2(8f, 0f);

        Image image = accent.GetComponent<Image>() ?? accent.AddComponent<Image>();
        image.color = AccentColor;
        image.raycastTarget = false;
        accent.transform.SetAsFirstSibling();
        return image;
    }

    internal static Image EnsureDivider(Transform card, string childName = "Divider")
    {
        GameObject divider = EnsureChild(card, childName);
        RectTransform rect = divider.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -108f);
        rect.sizeDelta = new Vector2(700f, 2f);

        Image image = divider.GetComponent<Image>() ?? divider.AddComponent<Image>();
        image.color = DividerColor;
        image.raycastTarget = false;
        return image;
    }

    internal static TextMeshProUGUI EnsureTitle(Transform card, string childName)
    {
        GameObject titleGo = EnsureChild(card, childName);
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
        tmp.text = string.Empty;
        return tmp;
    }

    internal static TextMeshProUGUI EnsureBody(Transform card, string childName, bool centerAlign = false)
    {
        GameObject bodyGo = EnsureChild(card, childName);
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
        tmp.alignment = centerAlign ? TextAlignmentOptions.Center : TextAlignmentOptions.TopLeft;
        tmp.textWrappingMode = TextWrappingModes.Normal;
        tmp.overflowMode = TextOverflowModes.Overflow;
        tmp.lineSpacing = 6f;
        tmp.text = string.Empty;
        return tmp;
    }

    internal static Button EnsurePrimaryButton(Transform card, string childName, string label)
    {
        GameObject btnGo = EnsureChild(card, childName);
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

    internal static void EnsureHiddenPanelState(GameObject root)
    {
        if (root == null)
            return;

        CanvasGroup group = root.GetComponent<CanvasGroup>();
        if (group == null)
            group = root.AddComponent<CanvasGroup>();

        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;
        root.SetActive(false);
    }

    private static TextMeshProUGUI EnsureButtonLabel(Transform button, string label)
    {
        Transform existing = button.Find("ButtonText");
        GameObject textGo = existing != null ? existing.gameObject : new GameObject("ButtonText", typeof(RectTransform));
        if (existing == null)
            textGo.transform.SetParent(button, false);

        RectTransform rect = textGo.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        TextMeshProUGUI tmp = textGo.GetComponent<TextMeshProUGUI>() ?? textGo.AddComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.textWrappingMode = TextWrappingModes.Normal;
        return tmp;
    }

    private static GameObject EnsureChild(Transform parent, string childName)
    {
        Transform existing = parent.Find(childName);
        if (existing != null)
            return existing.gameObject;

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
}
