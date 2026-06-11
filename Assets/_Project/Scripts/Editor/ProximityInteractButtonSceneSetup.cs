using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public static class ProximityInteractButtonSceneSetup
{
    private const string HudCanvasName = "HUD_Canvas";
    private const string PanelName = "ProximityInteractButton";

    [MenuItem("HealthySim/Setup Proximity Interact Button")]
    public static void SetupProximityInteractButton()
    {
        Transform hudCanvas = FindHudCanvas();
        if (hudCanvas == null)
        {
            Debug.LogWarning("[HealthySim] HUD_Canvas not found. Open SampleScene first.");
            return;
        }

        Transform panel = hudCanvas.Find(PanelName);
        if (panel == null)
        {
            GameObject root = new GameObject(PanelName, typeof(RectTransform), typeof(CanvasGroup));
            root.transform.SetParent(hudCanvas, false);
            panel = root.transform;
            Undo.RegisterCreatedObjectUndo(root, "Create Proximity Interact Button");
        }
        else
        {
            Undo.RegisterFullObjectHierarchyUndo(panel.gameObject, "Setup Proximity Interact Button");
        }

        ProximityInteractButtonUi.BuildOrRefresh(panel, out CanvasGroup group, out Button button, out TextMeshProUGUI label);

        ProximityInteractButton controller = panel.GetComponent<ProximityInteractButton>();
        if (controller == null)
            controller = panel.gameObject.AddComponent<ProximityInteractButton>();

        SerializedObject so = new SerializedObject(controller);
        so.FindProperty("panelRoot").objectReferenceValue = panel.gameObject;
        so.FindProperty("panelGroup").objectReferenceValue = group;
        so.FindProperty("actionButton").objectReferenceValue = button;
        so.FindProperty("labelText").objectReferenceValue = label;
        so.ApplyModifiedPropertiesWithoutUndo();

        panel.gameObject.SetActive(true);
        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;

        Transform shadow = panel.Find("Shadow");
        if (shadow != null)
            shadow.gameObject.SetActive(false);

        if (button != null)
            button.gameObject.SetActive(false);

        panel.SetAsLastSibling();

        EditorSceneManager.MarkSceneDirty(hudCanvas.gameObject.scene);
        Debug.Log("[HealthySim] ProximityInteractButton refreshed under HUD_Canvas/ProximityInteractButton → Shadow, ActionButton, LabelText. Save scene (Ctrl+S).");
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
