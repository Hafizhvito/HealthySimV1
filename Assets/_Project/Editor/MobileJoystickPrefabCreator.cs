#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;

public static class MobileJoystickPrefabCreator
{
    private const string MenuPath = "HealthSim/Create Mobile Joystick Prefab";
    private const string PrefabPath = "Assets/_Project/Prefabs/UI/MobileJoystickUI.prefab";

    [MenuItem(MenuPath)]
    private static void CreateMobileJoystickPrefab()
    {
        EnsureFolders();

        MobileInputController controller = Object.FindFirstObjectByType<MobileInputController>();
        GameObject root;

        if (controller != null)
        {
            root = controller.gameObject;
        }
        else
        {
            root = new GameObject("MobileInputController");
            controller = root.AddComponent<MobileInputController>();
            Undo.RegisterCreatedObjectUndo(root, "Create MobileInputController");
        }

        root.name = "MobileInputController";
        controller.EditorBuildUiForPrefab();
        EnsureEventSystem();

        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null)
            AssetDatabase.DeleteAsset(PrefabPath);

        GameObject prefab = PrefabUtility.SaveAsPrefabAssetAndConnect(root, PrefabPath, InteractionMode.UserAction);
        if (prefab == null)
        {
            Debug.LogError("Failed to create Mobile Joystick prefab.");
            return;
        }

        Selection.activeObject = root;
        EditorGUIUtility.PingObject(root);
        Debug.Log("Mobile Joystick prefab created at: " + PrefabPath);
    }

    private static void EnsureFolders()
    {
        if (!AssetDatabase.IsValidFolder("Assets/_Project"))
            AssetDatabase.CreateFolder("Assets", "_Project");

        if (!AssetDatabase.IsValidFolder("Assets/_Project/Prefabs"))
            AssetDatabase.CreateFolder("Assets/_Project", "Prefabs");

        if (!AssetDatabase.IsValidFolder("Assets/_Project/Prefabs/UI"))
            AssetDatabase.CreateFolder("Assets/_Project/Prefabs", "UI");
    }

    private static void EnsureEventSystem()
    {
        EventSystem eventSystem = Object.FindFirstObjectByType<EventSystem>();
        if (eventSystem != null)
            return;

        GameObject eventObj = new GameObject("EventSystem");
        eventSystem = eventObj.AddComponent<EventSystem>();
        eventObj.AddComponent<StandaloneInputModule>();
        Undo.RegisterCreatedObjectUndo(eventObj, "Create EventSystem");
    }
}
#endif
