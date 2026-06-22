using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public static class MainMenuCreditsPanelSceneSetup
{
    private const string PanelName = "CreditsPanel";
    private const string PanelSpritePath = "Assets/Picture/Background/BG_Panel.png";
    private const string BackButtonSpritePath = "Assets/Picture/Btn/Button Kembali.png";

    private static readonly Color PanelImageColor = new Color(1f, 1f, 1f, 0.5882353f);
    private static readonly Color TitleColor = Color.black;
    private static readonly Color BodyTextColor = new Color(0.1f, 0.1f, 0.1f, 1f);
    private static readonly Vector2 BackButtonSize = new Vector2(422f, 85f);

    [MenuItem("HealthySim/Setup Main Menu Credits Panel")]
    public static void SetupMainMenuCreditsPanel()
    {
        GameObject panel = FindCreditsPanel();
        if (panel == null)
        {
            Debug.LogWarning("[HealthySim] CreditsPanel not found. Open MainMenu scene first.");
            return;
        }

        Undo.RegisterFullObjectHierarchyUndo(panel, "Setup Main Menu Credits Panel");

        ApplyPanelBackground(panel);
        ApplyPanelLayout(panel);
        ApplyTitleStyle(panel.transform.Find("Title"));
        ApplyScrollAreaStyle(panel.transform.Find("Scroll View"));
        ApplyBackButtonStyle(panel.transform.Find("BackButton/Btn_Back"));

        MainMenuCreditsPanelController controller = panel.GetComponent<MainMenuCreditsPanelController>();
        if (controller == null)
            controller = panel.AddComponent<MainMenuCreditsPanelController>();

        SerializedObject controllerData = new SerializedObject(controller);
        controllerData.FindProperty("panelSize").vector2Value = new Vector2(900f, 640f);
        controllerData.FindProperty("bodyFontSize").intValue = 20;
        controllerData.FindProperty("bodyLineSpacing").floatValue = 2f;
        controllerData.ApplyModifiedPropertiesWithoutUndo();

        controller.ApplyPanelLayout();
        controller.PopulateCredits();

        panel.SetActive(false);

        EditorSceneManager.MarkSceneDirty(panel.scene);
        Debug.Log("[HealthySim] Main menu TIM panel styled like Pengaturan. Save scene (Ctrl+S).");
    }

    private static void ApplyPanelBackground(GameObject panel)
    {
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        if (panelRect != null)
        {
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.anchoredPosition = Vector2.zero;
            panelRect.sizeDelta = new Vector2(900f, 640f);
        }

        Image panelImage = panel.GetComponent<Image>();
        if (panelImage == null)
            return;

        Sprite panelSprite = AssetDatabase.LoadAssetAtPath<Sprite>(PanelSpritePath);
        if (panelSprite != null)
        {
            panelImage.sprite = panelSprite;
            panelImage.type = Image.Type.Sliced;
            panelImage.pixelsPerUnitMultiplier = 100f;
        }

        panelImage.color = PanelImageColor;
    }

    private static void ApplyPanelLayout(GameObject panel)
    {
        VerticalLayoutGroup layout = panel.GetComponent<VerticalLayoutGroup>();
        if (layout == null)
            return;

        layout.padding = new RectOffset(32, 32, 28, 24);
        layout.spacing = 20f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
    }

    private static void ApplyTitleStyle(Transform title)
    {
        if (title == null)
            return;

        RectTransform titleRect = title as RectTransform;
        if (titleRect != null)
        {
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.anchoredPosition = Vector2.zero;
            titleRect.sizeDelta = new Vector2(0f, 52f);
        }

        TextMeshProUGUI titleText = title.GetComponent<TextMeshProUGUI>();
        if (titleText != null)
        {
            titleText.text = "TIM";
            titleText.fontSize = 36f;
            titleText.fontStyle = FontStyles.Bold;
            titleText.alignment = TextAlignmentOptions.Center;
            titleText.color = TitleColor;
        }

        LayoutElement titleLayout = title.GetComponent<LayoutElement>() ?? title.gameObject.AddComponent<LayoutElement>();
        titleLayout.minHeight = 52f;
        titleLayout.preferredHeight = 52f;
    }

    private static void ApplyScrollAreaStyle(Transform scroll)
    {
        if (scroll == null)
            return;

        RectTransform scrollRectTransform = scroll as RectTransform;
        if (scrollRectTransform != null)
        {
            scrollRectTransform.anchorMin = new Vector2(0f, 1f);
            scrollRectTransform.anchorMax = new Vector2(1f, 1f);
            scrollRectTransform.pivot = new Vector2(0.5f, 1f);
            scrollRectTransform.anchoredPosition = Vector2.zero;
            scrollRectTransform.sizeDelta = new Vector2(0f, 460f);
        }

        LayoutElement scrollLayout = scroll.GetComponent<LayoutElement>() ?? scroll.gameObject.AddComponent<LayoutElement>();
        scrollLayout.minHeight = 420f;
        scrollLayout.preferredHeight = 460f;
        scrollLayout.flexibleHeight = 1f;
        scrollLayout.flexibleWidth = 1f;

        ScrollRect scrollRect = scroll.GetComponent<ScrollRect>();
        if (scrollRect != null)
        {
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            if (scrollRect.horizontalScrollbar != null)
                scrollRect.horizontalScrollbar.gameObject.SetActive(false);
            if (scrollRect.verticalScrollbar != null)
                scrollRect.verticalScrollbar.gameObject.SetActive(true);
        }

        Image scrollImage = scroll.GetComponent<Image>();
        if (scrollImage != null)
        {
            scrollImage.color = new Color(1f, 1f, 1f, 0f);
            scrollImage.raycastTarget = true;
        }

        Transform viewport = scroll.Find("Viewport");
        if (viewport != null)
        {
            Mask legacyMask = viewport.GetComponent<Mask>();
            if (legacyMask != null)
                Object.DestroyImmediate(legacyMask);

            if (viewport.GetComponent<RectMask2D>() == null)
                viewport.gameObject.AddComponent<RectMask2D>();

            Image viewportImage = viewport.GetComponent<Image>();
            if (viewportImage != null)
            {
                viewportImage.color = new Color(1f, 1f, 1f, 0f);
                viewportImage.raycastTarget = true;
            }
        }

        Transform content = scroll.Find("Viewport/Content");
        if (content != null)
        {
            VerticalLayoutGroup contentLayout = content.GetComponent<VerticalLayoutGroup>();
            if (contentLayout != null)
                Object.DestroyImmediate(contentLayout);

            Transform legacy = content.Find("Text (TMP)");
            if (legacy != null)
                Object.DestroyImmediate(legacy.gameObject);

            ContentSizeFitter contentFitter = content.GetComponent<ContentSizeFitter>();
            if (contentFitter != null)
                Object.DestroyImmediate(contentFitter);
        }

        Transform body = content != null ? content.Find("CreditsBodyText") : null;
        if (body != null)
        {
            body.gameObject.layer = scroll.gameObject.layer;

            TextMeshProUGUI bodyText = body.GetComponent<TextMeshProUGUI>();
            if (bodyText != null)
            {
                bodyText.fontSize = 20f;
                bodyText.lineSpacing = 2f;
                bodyText.color = BodyTextColor;
                bodyText.alignment = TextAlignmentOptions.TopLeft;
            }

            LayoutElement bodyLayout = body.GetComponent<LayoutElement>();
            if (bodyLayout != null)
                Object.DestroyImmediate(bodyLayout);
        }
    }

    private static void ApplyBackButtonStyle(Transform backButton)
    {
        if (backButton == null)
            return;

        RectTransform backRect = backButton as RectTransform;
        if (backRect != null)
        {
            backRect.anchorMin = new Vector2(0.5f, 0.5f);
            backRect.anchorMax = new Vector2(0.5f, 0.5f);
            backRect.pivot = new Vector2(0.5f, 0.5f);
            backRect.anchoredPosition = Vector2.zero;
            backRect.sizeDelta = BackButtonSize;
        }

        Image backImage = backButton.GetComponent<Image>();
        if (backImage != null)
        {
            Sprite backSprite = AssetDatabase.LoadAssetAtPath<Sprite>(BackButtonSpritePath);
            if (backSprite != null)
            {
                backImage.sprite = backSprite;
                backImage.type = Image.Type.Sliced;
                backImage.pixelsPerUnitMultiplier = 1000f;
            }

            backImage.color = Color.white;
        }

        Transform label = backButton.Find("Text (TMP)");
        if (label != null)
        {
            TextMeshProUGUI labelText = label.GetComponent<TextMeshProUGUI>();
            if (labelText != null)
                labelText.text = string.Empty;
        }

        Transform backButtonRoot = backButton.parent;
        if (backButtonRoot != null)
        {
            LayoutElement backLayout = backButtonRoot.GetComponent<LayoutElement>() ??
                                       backButtonRoot.gameObject.AddComponent<LayoutElement>();
            backLayout.minHeight = 88f;
            backLayout.preferredHeight = 88f;
        }
    }

    private static GameObject FindCreditsPanel()
    {
        MainMenuCreditsPanelController[] controllers = Object.FindObjectsByType<MainMenuCreditsPanelController>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        if (controllers.Length > 0 && controllers[0] != null)
            return controllers[0].gameObject;

        Transform[] transforms = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < transforms.Length; i++)
        {
            if (transforms[i] != null && transforms[i].name == PanelName)
                return transforms[i].gameObject;
        }

        return null;
    }
}
