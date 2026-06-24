using UnityEngine;

/// <summary>
/// Shared IMGUI panel + typography scale for food/stash menus.
/// Single source of truth for panel size, fonts, touch targets, and scroll layout.
/// </summary>
public static class ImGuiMenuLayout
{
    public struct Metrics
    {
        public float WindowWidth;
        public float WindowHeight;
        public float LayoutScale;
        public float FontScale;
        public float TouchButtonHeight;
        public float FooterButtonHeight;
        public float CloseButtonSize;
        public float ThumbnailSize;
        public float ThumbnailColumnWidth;
        public float ActionColumnWidth;
        public float ScrollBottomPadding;
        public float FoodRowHeight;
        public float FoodHeaderReservedHeight;
        public float StashRowHeight;
        public float StashHeaderReservedHeight;
        public bool UseMobileMinimums;

        public float EmptyListHeight => 52f * LayoutScale;
    }

    private const float ReferenceWidth = 840f;
    private const float ReferenceHeight = 600f;
    private const float ScreenMargin = 12f;
    private const float PanelWidthRatio = 0.94f;
    private const float PanelHeightRatio = 0.92f;
    private const float MaxPanelWidth = 980f;
    private const float MaxPanelHeight = 780f;
    private const float MinTouchHeight = 54f;
    private const float BaseFoodRowHeight = 142f;
    private const float BaseFoodHeaderReserved = 138f;
    private const float BaseStashRowHeight = 116f;
    private const float BaseStashHeaderReserved = 112f;

    public const float FooterTopSpacing = 10f;
    public const float WindowBottomPadding = 14f;
    private const float WindowChromePadding = 16f;
    private const float ScrollHintReservedHeight = 30f;
    private const float FoodRowSpacing = 8f;

    public static float ComputeScrollHeight(Metrics metrics, float topReserved, bool includesScrollHint = false)
    {
        float reserved = topReserved
            + metrics.FooterButtonHeight
            + FooterTopSpacing
            + WindowBottomPadding
            + WindowChromePadding;

        if (includesScrollHint)
            reserved += ScrollHintReservedHeight;

        return Mathf.Max(108f, metrics.WindowHeight - reserved);
    }

    public static float FoodRowSpacingFor(Metrics metrics) => FoodRowSpacing * metrics.LayoutScale;

    public static Metrics Compute()
    {
        Rect safeArea = Screen.safeArea;
        float availableWidth = Mathf.Max(320f, safeArea.width - ScreenMargin * 2f);
        float availableHeight = Mathf.Max(400f, safeArea.height - ScreenMargin * 2f);

        float width = Mathf.Min(availableWidth * PanelWidthRatio, MaxPanelWidth);
        float height = Mathf.Min(availableHeight * PanelHeightRatio, MaxPanelHeight);

        float aspectHeight = height;
        float aspectWidth = width;
        if (aspectWidth / aspectHeight > ReferenceWidth / ReferenceHeight)
            aspectWidth = aspectHeight * (ReferenceWidth / ReferenceHeight);
        else
            aspectHeight = aspectWidth * (ReferenceHeight / ReferenceWidth);

        width = Mathf.Clamp(aspectWidth, 320f, MaxPanelWidth);
        height = Mathf.Clamp(aspectHeight, 400f, MaxPanelHeight);

        bool mobileMinimums = Application.isMobilePlatform || width < 920f || availableWidth < 1040f;

        float scaleW = width / ReferenceWidth;
        float scaleH = height / ReferenceHeight;
        float layoutScale = Mathf.Clamp(Mathf.Min(scaleW, scaleH), 0.92f, 1.38f);
        float fontScale = mobileMinimums
            ? Mathf.Clamp(layoutScale * 1.04f, 1.14f, 1.54f)
            : Mathf.Clamp(layoutScale, 1.0f, 1.32f);

        float touchHeight = Mathf.Max(MinTouchHeight, 50f * layoutScale);

        return new Metrics
        {
            WindowWidth = width,
            WindowHeight = height,
            LayoutScale = layoutScale,
            FontScale = fontScale,
            TouchButtonHeight = touchHeight,
            FooterButtonHeight = touchHeight,
            CloseButtonSize = Mathf.Max(MinTouchHeight, 46f * layoutScale),
            ThumbnailSize = Mathf.Max(84f, 80f * layoutScale),
            ThumbnailColumnWidth = Mathf.Max(100f, 96f * layoutScale),
            ActionColumnWidth = Mathf.Max(176f, 170f * layoutScale),
            ScrollBottomPadding = 18f * layoutScale,
            FoodRowHeight = BaseFoodRowHeight * layoutScale,
            FoodHeaderReservedHeight = BaseFoodHeaderReserved * layoutScale,
            StashRowHeight = BaseStashRowHeight * layoutScale,
            StashHeaderReservedHeight = BaseStashHeaderReserved * layoutScale,
            UseMobileMinimums = mobileMinimums
        };
    }

    public static Rect CenteredWindowRect(Metrics metrics)
    {
        Rect safeArea = Screen.safeArea;
        return new Rect(
            safeArea.x + (safeArea.width - metrics.WindowWidth) * 0.5f,
            safeArea.y + (safeArea.height - metrics.WindowHeight) * 0.5f,
            metrics.WindowWidth,
            metrics.WindowHeight);
    }

    public static int ScaledFont(Metrics metrics, int baseSize, int mobileMinimum)
    {
        int scaled = Mathf.RoundToInt(baseSize * metrics.FontScale);
        if (metrics.UseMobileMinimums)
            return Mathf.Max(mobileMinimum, scaled);

        return Mathf.Max(12, scaled);
    }
}
