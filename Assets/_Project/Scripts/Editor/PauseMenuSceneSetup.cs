using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public static class PauseMenuSceneSetup
{
    private const string HudCanvasName = "HUD_Canvas";
    private const string PauseRootName = "Pause";
    private const string PausePanelName = "Pause_Panel";
    private const string SettingPanelName = "Setting_Panel";
    private const float PauseButtonInset = 24f;
    private const float PauseButtonMinSize = 120f;
    private const float PauseCardWidth = 720f;
    private const float PauseCardHeight = 580f;
    private const float SettingsCardWidth = 720f;
    private const float SettingsCardHeight = 520f;
    private const float MenuButtonHeight = 88f;
    private const float ButtonRaycastExpand = 20f;
    private const int ContentPadding = 32;
    private const int ContentTopInset = 128;
    private const int HudCanvasSortOrder = 100;

    [MenuItem("HealthySim/Fix Pause Menu Layout")]
    public static void FixPauseMenuLayout()
    {
        Transform hudCanvasTransform = FindHudCanvas();
        if (hudCanvasTransform == null)
        {
            Debug.LogWarning("[HealthySim] HUD_Canvas not found in the active scene.");
            return;
        }

        Transform pauseRoot = hudCanvasTransform.Find(PauseRootName);
        if (pauseRoot == null)
        {
            Debug.LogWarning("[HealthySim] HUD_Canvas/Pause not found.");
            return;
        }

        Undo.RegisterFullObjectHierarchyUndo(pauseRoot.gameObject, "Fix Pause Menu Layout");

        NarrativePanelEditorLayout.ApplyFullscreenRoot(pauseRoot as RectTransform);

        Transform pausePanel = pauseRoot.Find(PausePanelName);
        Transform settingPanel = pauseRoot.Find(SettingPanelName);

        if (pausePanel != null)
            UpgradePauseModalPanel(pausePanel, "Jeda", PauseCardWidth, PauseCardHeight, true);

        if (settingPanel != null)
            UpgradePauseModalPanel(settingPanel, "Pengaturan", SettingsCardWidth, SettingsCardHeight, false);

        Transform pauseButton = pauseRoot.Find("PauseButton");
        if (pauseButton != null)
        {
            ApplyTopRightPauseButtonLayout(pauseButton as RectTransform);
            pauseButton.SetAsLastSibling();
        }

        Canvas hudCanvas = hudCanvasTransform.GetComponent<Canvas>();
        if (hudCanvas != null && hudCanvas.sortingOrder < HudCanvasSortOrder)
            hudCanvas.sortingOrder = HudCanvasSortOrder;

        PauseMenuManager pauseManager = Object.FindFirstObjectByType<PauseMenuManager>(FindObjectsInactive.Include);
        if (pauseManager != null)
        {
            SerializedObject so = new SerializedObject(pauseManager);
            so.FindProperty("autoFixPauseLayoutAtRuntime").boolValue = false;
            so.FindProperty("autoFixPauseButtonAtRuntime").boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        EditorSceneManager.MarkSceneDirty(pauseRoot.gameObject.scene);
        Debug.Log("[HealthySim] Pause menu updated (card style like backstory, hierarchy-editable). Save scene (Ctrl+S).");
    }

    private static void UpgradePauseModalPanel(Transform panel, string title, float cardWidth, float cardHeight, bool layoutButtons)
    {
        NarrativePanelEditorLayout.ApplyFullscreenRoot(panel as RectTransform);

        Image panelImage = panel.GetComponent<Image>();
        if (panelImage != null)
        {
            panelImage.enabled = false;
            panelImage.raycastTarget = false;
        }

        VerticalLayoutGroup rootLayout = panel.GetComponent<VerticalLayoutGroup>();
        if (rootLayout != null)
            Object.DestroyImmediate(rootLayout, true);

        NarrativePanelEditorLayout.EnsureOverlay(panel);
        NarrativePanelEditorLayout.EnsureShadow(panel);
        Transform card = NarrativePanelEditorLayout.EnsureCard(panel).transform;

        RectTransform cardRect = card as RectTransform;
        cardRect.sizeDelta = new Vector2(cardWidth, cardHeight);

        Transform shadow = panel.Find("Shadow");
        if (shadow is RectTransform shadowRect)
            shadowRect.sizeDelta = new Vector2(cardWidth + 8f, cardHeight + 8f);

        NarrativePanelEditorLayout.EnsureAccentBar(card);
        NarrativePanelEditorLayout.EnsureDivider(card);
        TextMeshProUGUI titleText = NarrativePanelEditorLayout.EnsureTitle(card, "TitleText");
        titleText.text = title;

        Transform contentRoot = EnsureContentRoot(card);
        ReparentExistingContent(panel, contentRoot);

        DisableDecorativeRaycasts(card);
        card.SetAsLastSibling();

        if (layoutButtons)
            ApplyMenuButtonLayout(contentRoot);
        else
            ApplySettingsContentLayout(contentRoot);

        PrepareButtonsForTouch(contentRoot);
    }

    private static Transform EnsureContentRoot(Transform card)
    {
        Transform existing = card.Find("Content");
        GameObject content = existing != null
            ? existing.gameObject
            : new GameObject("Content", typeof(RectTransform));

        if (existing == null)
            content.transform.SetParent(card, false);

        ContentSizeFitter fitter = content.GetComponent<ContentSizeFitter>();
        if (fitter != null)
            Object.DestroyImmediate(fitter, true);

        RectTransform rect = content.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.offsetMin = new Vector2(ContentPadding, ContentPadding);
        rect.offsetMax = new Vector2(-ContentPadding, -ContentTopInset);
        return content.transform;
    }

    private static void ReparentExistingContent(Transform panel, Transform contentRoot)
    {
        string[] reserved = { "Overlay", "Shadow", "Card", "AccentBar", "TitleText", "Divider" };
        List<Transform> toMove = new List<Transform>();

        for (int i = 0; i < panel.childCount; i++)
        {
            Transform child = panel.GetChild(i);
            if (child == null || child.name == "Card")
                continue;

            bool reservedName = false;
            for (int r = 0; r < reserved.Length; r++)
            {
                if (child.name == reserved[r])
                {
                    reservedName = true;
                    break;
                }
            }

            if (!reservedName)
                toMove.Add(child);
        }

        Transform card = panel.Find("Card");
        if (card != null)
        {
            for (int i = card.childCount - 1; i >= 0; i--)
            {
                Transform child = card.GetChild(i);
                if (child == null)
                    continue;

                if (child.name == "AccentBar" || child.name == "TitleText" || child.name == "Divider" || child.name == "Content")
                    continue;

                toMove.Add(child);
            }
        }

        for (int i = 0; i < toMove.Count; i++)
            toMove[i].SetParent(contentRoot, false);
    }

    private static void ApplyMenuButtonLayout(Transform contentRoot)
    {
        VerticalLayoutGroup layout = contentRoot.GetComponent<VerticalLayoutGroup>();
        if (layout == null)
            layout = contentRoot.gameObject.AddComponent<VerticalLayoutGroup>();

        layout.childAlignment = TextAnchor.UpperCenter;
        layout.spacing = 16f;
        layout.padding = new RectOffset(8, 8, 12, 12);
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        for (int i = 0; i < contentRoot.childCount; i++)
            NormalizeButtonForVerticalLayout(contentRoot.GetChild(i) as RectTransform);
    }

    private static void ApplySettingsContentLayout(Transform contentRoot)
    {
        VerticalLayoutGroup layout = contentRoot.GetComponent<VerticalLayoutGroup>();
        if (layout == null)
            layout = contentRoot.gameObject.AddComponent<VerticalLayoutGroup>();

        layout.childAlignment = TextAnchor.UpperCenter;
        layout.spacing = 18f;
        layout.padding = new RectOffset(0, 0, 12, 12);
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        for (int i = 0; i < contentRoot.childCount; i++)
        {
            Transform child = contentRoot.GetChild(i);
            if (child.GetComponent<Button>() != null)
                NormalizeButtonForVerticalLayout(child as RectTransform);
        }
    }

    private static void NormalizeButtonForVerticalLayout(RectTransform rect)
    {
        if (rect == null)
            return;

        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(0f, MenuButtonHeight);

        LayoutElement layout = rect.GetComponent<LayoutElement>();
        if (layout == null)
            layout = rect.gameObject.AddComponent<LayoutElement>();

        layout.minHeight = MenuButtonHeight;
        layout.preferredHeight = MenuButtonHeight;
        layout.flexibleWidth = 1f;

        Image image = rect.GetComponent<Image>();
        if (image != null)
        {
            image.raycastTarget = true;
            image.color = NarrativePanelEditorLayout.AccentColor;
        }

        Button button = rect.GetComponent<Button>();
        if (button != null && image != null)
            button.targetGraphic = image;
    }

    private static void DisableDecorativeRaycasts(Transform card)
    {
        if (card == null)
            return;

        string[] decorativeNames = { "AccentBar", "Divider", "TitleText", "Shadow" };
        for (int i = 0; i < decorativeNames.Length; i++)
        {
            Transform child = card.Find(decorativeNames[i]);
            if (child == null)
                continue;

            Image image = child.GetComponent<Image>();
            if (image != null)
                image.raycastTarget = false;

            TextMeshProUGUI text = child.GetComponent<TextMeshProUGUI>();
            if (text != null)
                text.raycastTarget = false;
        }

        Transform overlay = card.parent != null ? card.parent.Find("Overlay") : null;
        if (overlay != null)
        {
            Image overlayImage = overlay.GetComponent<Image>();
            if (overlayImage != null)
                overlayImage.raycastTarget = true;
        }
    }

    private static void PrepareButtonsForTouch(Transform root)
    {
        if (root == null)
            return;

        Button[] buttons = root.GetComponentsInChildren<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            Button button = buttons[i];
            if (button == null)
                continue;

            button.interactable = true;
            button.navigation = new Navigation { mode = Navigation.Mode.None };

            UIButtonHover hover = button.GetComponent<UIButtonHover>();
            if (hover != null)
                hover.enabled = false;

            Image buttonImage = button.GetComponent<Image>();
            if (buttonImage != null)
            {
                buttonImage.raycastTarget = true;
                buttonImage.raycastPadding = new Vector4(
                    -ButtonRaycastExpand,
                    -ButtonRaycastExpand,
                    -ButtonRaycastExpand,
                    -ButtonRaycastExpand);
                button.targetGraphic = buttonImage;
            }

            TextMeshProUGUI[] labels = button.GetComponentsInChildren<TextMeshProUGUI>(true);
            for (int j = 0; j < labels.Length; j++)
            {
                if (labels[j] != null)
                {
                    labels[j].raycastTarget = false;
                    labels[j].fontSize = Mathf.Max(labels[j].fontSize, 22f);
                    labels[j].fontStyle = FontStyles.Bold;
                    labels[j].color = Color.white;
                    labels[j].alignment = TextAlignmentOptions.Center;
                }
            }
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(root as RectTransform);
    }

    private static void ApplyTopRightPauseButtonLayout(RectTransform buttonRect)
    {
        if (buttonRect == null)
            return;

        buttonRect.anchorMin = new Vector2(1f, 1f);
        buttonRect.anchorMax = new Vector2(1f, 1f);
        buttonRect.pivot = new Vector2(1f, 1f);
        buttonRect.anchoredPosition = new Vector2(-PauseButtonInset, -PauseButtonInset);

        Vector2 size = buttonRect.sizeDelta;
        size.x = Mathf.Max(size.x, PauseButtonMinSize);
        size.y = Mathf.Max(size.y, PauseButtonMinSize);
        buttonRect.sizeDelta = size;
    }

    private static Transform FindHudCanvas()
    {
        Canvas[] canvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < canvases.Length; i++)
        {
            if (canvases[i] != null && canvases[i].name == HudCanvasName)
                return canvases[i].transform;
        }

        return null;
    }
}
