using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public static class NarrativePanelsSceneSetup
{
    private const string HudCanvasName = "HUD_Canvas";
    private const string BackstoryPanelName = "BackstoryPanel";
    private const string AgingPanelName = "AgingNotificationPanel";

    [MenuItem("HealthySim/Setup Narrative Panels (Backstory + Aging)")]
    public static void SetupNarrativePanels()
    {
        Transform hudCanvas = FindHudCanvas();
        if (hudCanvas == null)
        {
            Debug.LogWarning("[HealthySim] HUD_Canvas not found. Open SampleScene first.");
            return;
        }

        bool backstoryOk = SetupBackstoryPanel(hudCanvas);
        bool agingOk = SetupAgingPanel(hudCanvas);

        EditorSceneManager.MarkSceneDirty(hudCanvas.gameObject.scene);

        if (backstoryOk && agingOk)
            Debug.Log("[HealthySim] Backstory + Aging panels updated under HUD_Canvas. Save scene (Ctrl+S).");
    }

    private static bool SetupBackstoryPanel(Transform hudCanvas)
    {
        Transform panel = hudCanvas.Find(BackstoryPanelName);
        if (panel == null)
        {
            Debug.LogWarning("[HealthySim] BackstoryPanel not found under HUD_Canvas.");
            return false;
        }

        Undo.RegisterFullObjectHierarchyUndo(panel.gameObject, "Setup Backstory Panel");

        RectTransform rootRect = panel.GetComponent<RectTransform>();
        if (rootRect == null)
            rootRect = panel.gameObject.AddComponent<RectTransform>();

        NarrativePanelEditorLayout.ApplyFullscreenRoot(rootRect);

        Image rootImage = panel.GetComponent<Image>();
        if (rootImage != null)
            rootImage.enabled = false;

        NarrativePanelEditorLayout.EnsureOverlay(panel);
        NarrativePanelEditorLayout.EnsureShadow(panel);
        Transform card = NarrativePanelEditorLayout.EnsureCard(panel).transform;
        NarrativePanelEditorLayout.EnsureAccentBar(card);
        NarrativePanelEditorLayout.EnsureDivider(card);

        TextMeshProUGUI title = NarrativePanelEditorLayout.EnsureTitle(card, "CharacterNameText");
        TextMeshProUGUI body = NarrativePanelEditorLayout.EnsureBody(card, "BackstoryBodyText", centerAlign: false);
        Button continueButton = NarrativePanelEditorLayout.EnsurePrimaryButton(card, "ContinueButton", "Lanjut →");

        BackstoryDialogueController controller = panel.GetComponent<BackstoryDialogueController>();
        if (controller != null)
        {
            SerializedObject so = new SerializedObject(controller);
            so.FindProperty("panelRoot").objectReferenceValue = panel.gameObject;
            so.FindProperty("nameText").objectReferenceValue = title;
            so.FindProperty("bodyText").objectReferenceValue = body;
            so.FindProperty("continueButton").objectReferenceValue = continueButton;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        NarrativePanelEditorLayout.EnsureHiddenPanelState(panel.gameObject);
        return true;
    }

    private static bool SetupAgingPanel(Transform hudCanvas)
    {
        Transform panel = hudCanvas.Find(AgingPanelName);
        if (panel == null)
        {
            Debug.LogWarning("[HealthySim] AgingNotificationPanel not found under HUD_Canvas.");
            return false;
        }

        Undo.RegisterFullObjectHierarchyUndo(panel.gameObject, "Setup Aging Panel");

        RectTransform rootRect = panel.GetComponent<RectTransform>();
        if (rootRect == null)
            rootRect = panel.gameObject.AddComponent<RectTransform>();

        NarrativePanelEditorLayout.ApplyFullscreenRoot(rootRect);

        Image rootImage = panel.GetComponent<Image>();
        if (rootImage != null)
            rootImage.enabled = false;

        NarrativePanelEditorLayout.EnsureOverlay(panel);
        NarrativePanelEditorLayout.EnsureShadow(panel);
        Transform card = NarrativePanelEditorLayout.EnsureCard(panel).transform;
        NarrativePanelEditorLayout.EnsureAccentBar(card);
        NarrativePanelEditorLayout.EnsureDivider(card);

        TextMeshProUGUI title = NarrativePanelEditorLayout.EnsureTitle(card, "TitleText");
        TextMeshProUGUI body = NarrativePanelEditorLayout.EnsureBody(card, "BodyText", centerAlign: false);
        Button continueButton = NarrativePanelEditorLayout.EnsurePrimaryButton(card, "LanjutButton", "Lanjut →");

        AgingNotificationPanelBinder binder = panel.GetComponent<AgingNotificationPanelBinder>();
        if (binder == null)
            binder = panel.gameObject.AddComponent<AgingNotificationPanelBinder>();

        binder.panelRoot = rootRect;
        binder.panelGroup = panel.GetComponent<CanvasGroup>() ?? panel.gameObject.AddComponent<CanvasGroup>();
        binder.titleText = title;
        binder.bodyText = body;
        binder.continueButton = continueButton;
        EditorUtility.SetDirty(binder);

        NarrativePanelEditorLayout.EnsureHiddenPanelState(panel.gameObject);
        return true;
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
