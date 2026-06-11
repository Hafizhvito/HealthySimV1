using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public static class FaintNotificationPanelSceneSetup
{
    private const string HudCanvasName = "HUD_Canvas";
    private const string BackstoryPanelName = "BackstoryPanel";
    private const string FaintPanelName = "FaintNotificationPanel";

    private const string BodyText =
        "Kamu pingsan karena kehabisan energi. Pastikan kamu selalu makan dan istirahat yang cukup agar tetap bisa beraktivitas.";

    [MenuItem("HealthySim/Setup Faint Notification Panel")]
    public static void SetupFaintNotificationPanel()
    {
        Transform hudCanvas = FindHudCanvas();
        if (hudCanvas == null)
        {
            Debug.LogWarning("[HealthySim] HUD_Canvas not found. Open SampleScene first.");
            return;
        }

        Transform backstory = hudCanvas.Find(BackstoryPanelName);
        if (backstory == null)
        {
            Debug.LogWarning("[HealthySim] BackstoryPanel not found under HUD_Canvas.");
            return;
        }

        Transform panel = hudCanvas.Find(FaintPanelName);
        if (panel == null)
        {
            GameObject copy = Object.Instantiate(backstory.gameObject, hudCanvas);
            copy.name = FaintPanelName;
            panel = copy.transform;
            Undo.RegisterCreatedObjectUndo(copy, "Create Faint Notification Panel");
        }
        else
        {
            Undo.RegisterFullObjectHierarchyUndo(panel.gameObject, "Setup Faint Notification Panel");
        }

        BackstoryDialogueController backstoryController = panel.GetComponent<BackstoryDialogueController>();
        if (backstoryController != null)
            Object.DestroyImmediate(backstoryController, true);

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
        title.text = "Pingsan";

        TextMeshProUGUI body = NarrativePanelEditorLayout.EnsureBody(card, "BackstoryBodyText", centerAlign: false);
        body.text = BodyText;
        body.alignment = TextAlignmentOptions.Center;

        Button continueButton = NarrativePanelEditorLayout.EnsurePrimaryButton(card, "ContinueButton", "Lanjut →");

        FaintNotificationController controller = panel.GetComponent<FaintNotificationController>();
        if (controller == null)
            controller = panel.gameObject.AddComponent<FaintNotificationController>();

        SerializedObject so = new SerializedObject(controller);
        so.FindProperty("panelRoot").objectReferenceValue = panel.gameObject;
        so.FindProperty("panelGroup").objectReferenceValue =
            panel.GetComponent<CanvasGroup>() ?? panel.gameObject.AddComponent<CanvasGroup>();
        so.FindProperty("titleText").objectReferenceValue = title;
        so.FindProperty("bodyText").objectReferenceValue = body;
        so.FindProperty("continueButton").objectReferenceValue = continueButton;
        so.ApplyModifiedPropertiesWithoutUndo();

        NarrativePanelEditorLayout.EnsureHiddenPanelState(panel.gameObject);

        EditorSceneManager.MarkSceneDirty(hudCanvas.gameObject.scene);
        Debug.Log("[HealthySim] FaintNotificationPanel ready under HUD_Canvas. Save scene (Ctrl+S).");
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
