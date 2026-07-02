using TMPro;
using UnityEngine;
using UnityEngine.UI;

public static class CameraToggleButtonUi
{
    // Green matching game's menu button style (UIButtonHover normalColor)
    public static readonly Color MenuGreen = new Color(0.337f, 0.725f, 0.125f, 1f);

    private const float PauseBtnRight = -24f;
    private const float PauseBtnWidth = 120f;
    private const float Gap           = 12f;
    private const float TopOffset     = -24f;
    private const float TouchSize     = 108f;

    public static void ApplyRootLayout(RectTransform root)
    {
        root.anchorMin        = new Vector2(1f, 1f);
        root.anchorMax        = new Vector2(1f, 1f);
        root.pivot            = new Vector2(1f, 1f);
        root.anchoredPosition = new Vector2(PauseBtnRight - PauseBtnWidth - Gap, TopOffset);
        root.sizeDelta        = new Vector2(TouchSize, TouchSize);
    }

    public static void BuildOrRefresh(Transform panel, out Button button, out Image background,
        out TextMeshProUGUI modeLabel, out TextMeshProUGUI arrowLabel)
    {
        RectTransform rootRect = panel.GetComponent<RectTransform>() ?? panel.gameObject.AddComponent<RectTransform>();
        ApplyRootLayout(rootRect);

        background = panel.GetComponent<Image>() ?? panel.gameObject.AddComponent<Image>();
        background.color         = MenuGreen;
        background.raycastTarget = true;

        button = panel.GetComponent<Button>() ?? panel.gameObject.AddComponent<Button>();
        button.targetGraphic = background;
        button.transition    = Selectable.Transition.ColorTint;
        button.navigation    = new Navigation { mode = Navigation.Mode.None };

        ColorBlock cb = button.colors;
        cb.normalColor      = Color.white;
        cb.highlightedColor = new Color(0.9f, 0.9f, 0.9f, 1f);
        cb.pressedColor     = new Color(0.7f, 0.7f, 0.7f, 1f);
        cb.selectedColor    = Color.white;
        cb.fadeDuration     = 0.1f;
        button.colors       = cb;

        // Single centred label showing the TARGET mode
        modeLabel = EnsureLabel(panel, "ModeLabel", "FPP", 22f, FontStyles.Bold,
            Color.white, Vector2.zero);

        // arrowLabel kept for compatibility but hidden (zero size, empty text)
        arrowLabel = EnsureLabel(panel, "ArrowLabel", "", 1f, FontStyles.Normal,
            new Color(0f, 0f, 0f, 0f), new Vector2(0f, -200f));
    }

    /// <summary>Update visuals: shows the TARGET perspective the button will switch to.</summary>
    public static void ApplyVisuals(Image background, TextMeshProUGUI modeLabel,
        TextMeshProUGUI arrowLabel, bool currentlyFpp, Color? overrideColor = null)
    {
        // Button color stays green regardless — same as menu buttons
        if (background != null)
            background.color = overrideColor ?? MenuGreen;

        if (modeLabel != null)
        {
            // Show where we'll GO, not where we are
            modeLabel.text  = currentlyFpp ? "TPP" : "FPP";
            modeLabel.color = Color.white;
        }

        // arrowLabel intentionally invisible
        if (arrowLabel != null)
            arrowLabel.text = "";
    }

    private static TextMeshProUGUI EnsureLabel(Transform parent, string name, string text,
        float size, FontStyles style, Color color, Vector2 offset)
    {
        Transform existing = parent.Find(name);
        GameObject go = existing != null
            ? existing.gameObject
            : new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));

        if (existing == null)
            go.transform.SetParent(parent, false);

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin        = Vector2.zero;
        rt.anchorMax        = Vector2.one;
        rt.pivot            = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = offset;
        rt.sizeDelta        = Vector2.zero;

        TextMeshProUGUI tmp = go.GetComponent<TextMeshProUGUI>() ?? go.AddComponent<TextMeshProUGUI>();
        tmp.text          = text;
        tmp.fontSize      = size;
        tmp.fontStyle     = style;
        tmp.alignment     = TextAlignmentOptions.Center;
        tmp.color         = color;
        tmp.outlineColor  = new Color(0f, 0f, 0f, 0.5f);
        tmp.outlineWidth  = 0.15f;
        tmp.raycastTarget = false;
        tmp.overflowMode  = TextOverflowModes.Overflow;
        return tmp;
    }
}
