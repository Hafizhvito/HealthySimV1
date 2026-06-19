using TMPro;
using UnityEngine;
using UnityEngine.UI;

public static class ProximityInteractButtonUi
{
    public static readonly Color AccentColor = new Color32(0x21, 0x96, 0xF3, 0xFF);
    public static readonly Color AccentHighlightColor = new Color32(0x42, 0xA5, 0xF5, 0xFF);
    public static readonly Color AccentPressedColor = new Color32(0x19, 0x76, 0xD2, 0xFF);
    public static readonly Color ShadowColor = new Color(0f, 0f, 0f, 0.26f);

    private static Sprite roundedRectSprite;

    public static void ApplyRootLayout(RectTransform root)
    {
        root.anchorMin = new Vector2(1f, 0f);
        root.anchorMax = new Vector2(1f, 0f);
        root.pivot = new Vector2(1f, 0f);
        root.anchoredPosition = new Vector2(-80f, 172f);
        root.sizeDelta = new Vector2(268f, 64f);
    }

    public static void BuildOrRefresh(Transform panel, out CanvasGroup group, out Button button, out TextMeshProUGUI label)
    {
        RectTransform rootRect = panel.GetComponent<RectTransform>() ?? panel.gameObject.AddComponent<RectTransform>();
        ApplyRootLayout(rootRect);

        group = panel.GetComponent<CanvasGroup>() ?? panel.gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;

        Image shadowImage = EnsureShadow(panel);
        button = EnsureActionButton(panel, out label);

        shadowImage.transform.SetAsFirstSibling();
        button.transform.SetAsLastSibling();
    }

    public static void ApplyRoundedImage(Image image, Color color)
    {
        if (image == null)
            return;

        image.sprite = GetRoundedRectSprite();
        image.type = Image.Type.Sliced;
        image.color = color;
    }

    public static void ApplyButtonColors(Button button)
    {
        if (button == null)
            return;

        ColorBlock colors = button.colors;
        colors.normalColor = AccentColor;
        colors.highlightedColor = AccentHighlightColor;
        colors.pressedColor = AccentPressedColor;
        colors.selectedColor = AccentColor;
        colors.disabledColor = new Color(AccentColor.r, AccentColor.g, AccentColor.b, 0.45f);
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.08f;
        button.colors = colors;
        button.transition = Selectable.Transition.ColorTint;
    }

    private static Image EnsureShadow(Transform panel)
    {
        Transform existing = panel.Find("Shadow");
        GameObject shadowGo = existing != null
            ? existing.gameObject
            : new GameObject("Shadow", typeof(RectTransform), typeof(Image));

        if (existing == null)
            shadowGo.transform.SetParent(panel, false);

        RectTransform shadowRect = shadowGo.GetComponent<RectTransform>();
        shadowRect.anchorMin = Vector2.zero;
        shadowRect.anchorMax = Vector2.one;
        shadowRect.offsetMin = new Vector2(-2f, -6f);
        shadowRect.offsetMax = new Vector2(2f, -2f);

        Image shadowImage = shadowGo.GetComponent<Image>();
        ApplyRoundedImage(shadowImage, ShadowColor);
        shadowImage.raycastTarget = false;
        return shadowImage;
    }

    private static Button EnsureActionButton(Transform panel, out TextMeshProUGUI label)
    {
        Transform existing = panel.Find("ActionButton");
        GameObject buttonGo = existing != null
            ? existing.gameObject
            : new GameObject("ActionButton", typeof(RectTransform), typeof(Image), typeof(Button));

        if (existing == null)
            buttonGo.transform.SetParent(panel, false);

        RectTransform buttonRect = buttonGo.GetComponent<RectTransform>();
        buttonRect.anchorMin = Vector2.zero;
        buttonRect.anchorMax = Vector2.one;
        buttonRect.offsetMin = Vector2.zero;
        buttonRect.offsetMax = Vector2.zero;

        Image buttonImage = buttonGo.GetComponent<Image>();
        ApplyRoundedImage(buttonImage, AccentColor);
        buttonImage.raycastTarget = true;

        Button button = buttonGo.GetComponent<Button>();
        button.targetGraphic = buttonImage;
        ApplyButtonColors(button);

        label = EnsureLabel(buttonGo.transform);
        return button;
    }

    private static TextMeshProUGUI EnsureLabel(Transform button)
    {
        Transform existing = button.Find("LabelText");
        GameObject labelGo = existing != null
            ? existing.gameObject
            : new GameObject("LabelText", typeof(RectTransform), typeof(TextMeshProUGUI));

        if (existing == null)
            labelGo.transform.SetParent(button, false);

        RectTransform labelRect = labelGo.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(16f, 10f);
        labelRect.offsetMax = new Vector2(-16f, -10f);

        TextMeshProUGUI label = labelGo.GetComponent<TextMeshProUGUI>();
        label.text = "Interaksi";
        label.fontSize = 23f;
        label.fontStyle = FontStyles.Bold;
        label.color = Color.white;
        label.alignment = TextAlignmentOptions.Center;
        label.textWrappingMode = TextWrappingModes.Normal;
        label.overflowMode = TextOverflowModes.Ellipsis;
        label.raycastTarget = false;
        return label;
    }

    public static Sprite GetRoundedRectSprite()
    {
        if (roundedRectSprite != null)
            return roundedRectSprite;

        const int size = 256;
        const float cornerRadius = 36f;

        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.name = "ProximityInteractRoundedRect";
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;
        texture.hideFlags = HideFlags.HideAndDontSave;

        Color32[] pixels = new Color32[size * size];
        float half = (size - 1) * 0.5f;
        Vector2 innerExtents = new Vector2(half, half) - new Vector2(cornerRadius, cornerRadius);

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

        roundedRectSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, size, size),
            new Vector2(0.5f, 0.5f),
            100f,
            0,
            SpriteMeshType.FullRect,
            new Vector4(cornerRadius, cornerRadius, cornerRadius, cornerRadius));

        roundedRectSprite.name = "ProximityInteractRoundedRect";
        return roundedRectSprite;
    }
}
