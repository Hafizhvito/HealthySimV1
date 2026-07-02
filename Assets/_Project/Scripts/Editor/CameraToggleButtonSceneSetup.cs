using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public static class CameraToggleButtonSceneSetup
{
    private const string HudCanvasName = "HUD_Canvas";
    private const string PanelName     = "CameraToggleButton";

    // LiberationSans SDF — the font used by MainMenu buttons
    private const string FontGuid = "8f586378b4e144a9851e7b34d9b748ee";

    [MenuItem("HealthySim/Setup Camera Toggle Button")]
    public static void SetupCameraToggleButton()
    {
        Transform hudCanvas = FindHudCanvas();
        if (hudCanvas == null)
        {
            Debug.LogWarning("[HealthySim] HUD_Canvas not found. Open SampleScene first.");
            return;
        }

        // ── Find or create panel ──────────────────────────────────────────
        Transform panel = hudCanvas.Find(PanelName);
        if (panel == null)
        {
            GameObject root = new GameObject(PanelName,
                typeof(RectTransform), typeof(Image), typeof(Button), typeof(CameraToggleButton));
            root.transform.SetParent(hudCanvas, false);
            panel = root.transform;
            Undo.RegisterCreatedObjectUndo(root, "Create Camera Toggle Button");
        }
        else
        {
            Undo.RegisterFullObjectHierarchyUndo(panel.gameObject, "Setup Camera Toggle Button");
            if (panel.GetComponent<CameraToggleButton>() == null)
                panel.gameObject.AddComponent<CameraToggleButton>();
        }

        // ── Build UI elements ─────────────────────────────────────────────
        CameraToggleButtonUi.BuildOrRefresh(panel,
            out Button button, out Image background,
            out TextMeshProUGUI modeLabel, out TextMeshProUGUI arrowLabel);

        // ── Apply UISprite (same sprite as MainMenu buttons) ──────────────
        Sprite uiSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        if (uiSprite != null)
        {
            background.sprite = uiSprite;
            background.type   = Image.Type.Sliced;
        }

        // ── Apply font (LiberationSans SDF from MainMenu) ─────────────────
        string fontPath = AssetDatabase.GUIDToAssetPath(FontGuid);
        TMP_FontAsset font = !string.IsNullOrEmpty(fontPath)
            ? AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(fontPath)
            : null;

        if (font != null)
        {
            modeLabel.font = font;
            arrowLabel.font = font;
        }

        // ── Store borrowed references on controller ────────────────────────
        CameraToggleButton controller = panel.GetComponent<CameraToggleButton>();
        SerializedObject so = new SerializedObject(controller);
        so.FindProperty("button").objectReferenceValue     = button;
        so.FindProperty("background").objectReferenceValue = background;
        so.FindProperty("modeLabel").objectReferenceValue  = modeLabel;
        so.FindProperty("arrowLabel").objectReferenceValue = arrowLabel;
        so.FindProperty("menuGreenColor").colorValue       = CameraToggleButtonUi.MenuGreen;
        so.FindProperty("menuBorrowedSprite").objectReferenceValue = background.sprite;
        so.ApplyModifiedPropertiesWithoutUndo();

        controller.EditorRefreshBindings();
        panel.gameObject.SetActive(true);

        // Keep pause panel on top
        Transform pauseRoot = hudCanvas.Find("Pause");
        if (pauseRoot != null) pauseRoot.SetAsLastSibling();

        EditorSceneManager.MarkSceneDirty(hudCanvas.gameObject.scene);
        Selection.activeGameObject = panel.gameObject;
        Debug.Log($"[HealthySim] CameraToggleButton setup: sprite={background.sprite?.name}, font={font?.name}. Save scene (Ctrl+S).");
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
