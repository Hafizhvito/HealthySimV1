using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public static class MainMenuCreditsPanelSceneSetup
{
    private const string PanelName = "CreditsPanel";

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

        RectTransform panelRect = panel.GetComponent<RectTransform>();
        if (panelRect != null)
        {
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.anchoredPosition = Vector2.zero;
            panelRect.sizeDelta = new Vector2(980f, 700f);
        }

        Image panelImage = panel.GetComponent<Image>();
        if (panelImage != null)
            panelImage.color = new Color(0.94f, 0.96f, 0.98f, 0.94f);

        VerticalLayoutGroup layout = panel.GetComponent<VerticalLayoutGroup>();
        if (layout != null)
        {
            layout.padding = new RectOffset(28, 28, 24, 20);
            layout.spacing = 16f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
        }

        Transform title = panel.transform.Find("Title");
        if (title != null)
        {
            TextMeshProUGUI titleText = title.GetComponent<TextMeshProUGUI>();
            if (titleText != null)
            {
                titleText.text = "TIM";
                titleText.fontSize = 34f;
                titleText.fontStyle = FontStyles.Bold;
                titleText.alignment = TextAlignmentOptions.Center;
                titleText.color = new Color(0.12f, 0.14f, 0.18f, 1f);
            }

            LayoutElement titleLayout = title.GetComponent<LayoutElement>() ?? title.gameObject.AddComponent<LayoutElement>();
            titleLayout.minHeight = 48f;
            titleLayout.preferredHeight = 48f;
        }

        Transform scroll = panel.transform.Find("Scroll View");
        if (scroll != null)
        {
            RectTransform scrollRectTransform = scroll.GetComponent<RectTransform>();
            if (scrollRectTransform != null)
            {
                scrollRectTransform.anchorMin = new Vector2(0f, 1f);
                scrollRectTransform.anchorMax = new Vector2(1f, 1f);
                scrollRectTransform.pivot = new Vector2(0.5f, 1f);
                scrollRectTransform.anchoredPosition = Vector2.zero;
                scrollRectTransform.sizeDelta = Vector2.zero;
            }

            LayoutElement scrollLayout = scroll.GetComponent<LayoutElement>() ?? scroll.gameObject.AddComponent<LayoutElement>();
            scrollLayout.minHeight = 520f;
            scrollLayout.flexibleHeight = 1f;
            scrollLayout.flexibleWidth = 1f;

            ScrollRect scrollRect = scroll.GetComponent<ScrollRect>();
            if (scrollRect != null)
            {
                scrollRect.horizontal = false;
                scrollRect.vertical = true;
                if (scrollRect.horizontalScrollbar != null)
                    scrollRect.horizontalScrollbar.gameObject.SetActive(false);
                if (scrollRect.verticalScrollbar != null)
                    scrollRect.verticalScrollbar.gameObject.SetActive(true);
            }

            Image scrollImage = scroll.GetComponent<Image>();
            if (scrollImage != null)
                scrollImage.color = new Color(0.08f, 0.1f, 0.14f, 0.82f);
        }

        Transform content = panel.transform.Find("Scroll View/Viewport/Content");
        if (content != null)
        {
            Transform legacy = content.Find("Text (TMP)");
            if (legacy != null)
                Object.DestroyImmediate(legacy.gameObject);
        }

        Transform backButton = panel.transform.Find("BackButton");
        if (backButton != null)
        {
            LayoutElement backLayout = backButton.GetComponent<LayoutElement>() ?? backButton.gameObject.AddComponent<LayoutElement>();
            backLayout.minHeight = 64f;
            backLayout.preferredHeight = 64f;
        }

        MainMenuCreditsPanelController controller = panel.GetComponent<MainMenuCreditsPanelController>();
        if (controller == null)
            controller = panel.AddComponent<MainMenuCreditsPanelController>();

        controller.ApplyPanelLayout();
        controller.PopulateCredits();

        EditorSceneManager.MarkSceneDirty(panel.scene);
        Debug.Log("[HealthySim] Main menu TIM/Credits panel updated. Save scene (Ctrl+S).");
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
