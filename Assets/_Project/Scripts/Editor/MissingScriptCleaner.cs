#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class MissingScriptCleaner
{
    [MenuItem("Tools/HealthySim/Report Missing Scripts In Scene")]
    public static void ReportMissingScriptsInScene()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid())
        {
            Debug.LogWarning("[MissingScriptCleaner] No active scene to report.");
            return;
        }

        int objectsWithMissing = 0;
        int totalMissing = 0;

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
            {
                int missing = CountMissingScripts(transform.gameObject);
                if (missing > 0)
                {
                    objectsWithMissing++;
                    totalMissing += missing;
                    Debug.LogWarning($"[MissingScriptCleaner] Missing scripts on '{GetHierarchyPath(transform)}' count={missing}");
                }
            }
        }

        Debug.Log($"[MissingScriptCleaner] Report done. objects={objectsWithMissing}, missingComponents={totalMissing}");
    }

    [MenuItem("Tools/HealthySim/Fix Missing Scripts In Scene")]
    public static void FixMissingScriptsInScene()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid())
        {
            Debug.LogWarning("[MissingScriptCleaner] No active scene to clean.");
            return;
        }

        int cleanedObjects = 0;
        int removedComponents = 0;

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
            {
                int removed = RemoveMissingScriptsDeep(transform.gameObject);
                if (removed > 0)
                {
                    cleanedObjects++;
                    removedComponents += removed;
                    EditorUtility.SetDirty(transform.gameObject);
                }
            }
        }

        FixMoveJoystickPresentersInOpenScenes();

        if (removedComponents > 0 || EditorSceneManager.GetActiveScene().isDirty)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        Debug.Log($"[MissingScriptCleaner] Cleaned objects: {cleanedObjects}, removed missing components: {removedComponents}");
    }

    [MenuItem("Tools/HealthySim/Fix MoveJoystickOuter Missing Script")]
    public static void FixMoveJoystickOuterMissingScript()
    {
        if (RepairMoveJoystickOuterFromPrefab())
            return;

        int removed = 0;
        int fixedPresenters = 0;

        foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
            {
                if (!string.Equals(transform.name, "MoveJoystickOuter", System.StringComparison.Ordinal))
                    continue;

                removed += RemoveMissingScriptsDeep(transform.gameObject);
                if (transform.GetComponent<MobileMoveStickPresenter>() == null)
                {
                    transform.gameObject.AddComponent<MobileMoveStickPresenter>();
                    fixedPresenters++;
                }

                EditorUtility.SetDirty(transform.gameObject);
            }
        }

        Scene scene = SceneManager.GetActiveScene();
        if (removed > 0 || fixedPresenters > 0)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        Debug.Log($"[MissingScriptCleaner] MoveJoystickOuter fix done. removed={removed}, presentersAdded={fixedPresenters}");
    }

    [MenuItem("Tools/HealthySim/Repair MoveJoystickOuter From Prefab")]
    public static void RepairMoveJoystickOuterFromPrefabMenu()
    {
        RepairMoveJoystickOuterFromPrefab();
    }

    private static bool RepairMoveJoystickOuterFromPrefab()
    {
        const string prefabPath = "Assets/_Project/Prefabs/UI/MobileJoystickUI.prefab";

        Transform canvas = FindSceneTransform("HUD_Canvas/MobileInputCanvas");
        if (canvas == null)
        {
            Debug.LogWarning("[MissingScriptCleaner] MobileInputCanvas not found in active scene.");
            return false;
        }

        Transform brokenOuter = canvas.Find("MoveJoystickOuter");
        if (brokenOuter == null)
        {
            Debug.LogWarning("[MissingScriptCleaner] MoveJoystickOuter not found in scene.");
            return false;
        }

        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
        if (prefabRoot == null)
        {
            Debug.LogWarning($"[MissingScriptCleaner] Prefab not found: {prefabPath}");
            return false;
        }

        Transform sourceOuter = prefabRoot.transform.Find("MobileInputCanvas/MoveJoystickOuter");
        if (sourceOuter == null)
        {
            foreach (Transform child in prefabRoot.GetComponentsInChildren<Transform>(true))
            {
                if (string.Equals(child.name, "MoveJoystickOuter", System.StringComparison.Ordinal)
                    && child.parent != null
                    && string.Equals(child.parent.name, "MobileInputCanvas", System.StringComparison.Ordinal))
                {
                    sourceOuter = child;
                    break;
                }
            }
        }

        if (sourceOuter == null)
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
            Debug.LogWarning("[MissingScriptCleaner] MoveJoystickOuter not found in MobileJoystickUI prefab.");
            return false;
        }

        RectTransform brokenRect = brokenOuter as RectTransform;
        int siblingIndex = brokenOuter.GetSiblingIndex();
        Vector2 anchoredPosition = brokenRect != null ? brokenRect.anchoredPosition : Vector2.zero;
        Vector2 sizeDelta = brokenRect != null ? brokenRect.sizeDelta : new Vector2(220f, 220f);
        Vector2 anchorMin = brokenRect != null ? brokenRect.anchorMin : Vector2.zero;
        Vector2 anchorMax = brokenRect != null ? brokenRect.anchorMax : Vector2.zero;
        Vector2 pivot = brokenRect != null ? brokenRect.pivot : Vector2.zero;

        Object.DestroyImmediate(brokenOuter.gameObject);

        GameObject replacement = Object.Instantiate(sourceOuter.gameObject, canvas);
        replacement.name = "MoveJoystickOuter";
        replacement.transform.SetSiblingIndex(siblingIndex);

        RectTransform replacementRect = replacement.GetComponent<RectTransform>();
        if (replacementRect != null)
        {
            replacementRect.anchorMin = anchorMin;
            replacementRect.anchorMax = anchorMax;
            replacementRect.pivot = pivot;
            replacementRect.anchoredPosition = anchoredPosition;
            replacementRect.sizeDelta = sizeDelta;
        }

        if (replacement.GetComponent<MobileMoveStickPresenter>() == null)
            replacement.AddComponent<MobileMoveStickPresenter>();

        PrefabUtility.UnloadPrefabContents(prefabRoot);

        Scene scene = SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log("[MissingScriptCleaner] Replaced MoveJoystickOuter from MobileJoystickUI prefab.");
        return true;
    }

    private static Transform FindSceneTransform(string hierarchyPath)
    {
        if (string.IsNullOrWhiteSpace(hierarchyPath))
            return null;

        string[] parts = hierarchyPath.Split('/');
        if (parts.Length == 0)
            return null;

        foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            foreach (Transform candidate in root.GetComponentsInChildren<Transform>(true))
            {
                if (!string.Equals(candidate.name, parts[^1], System.StringComparison.Ordinal))
                    continue;

                if (GetHierarchyPath(candidate) == hierarchyPath)
                    return candidate;
            }
        }

        return null;
    }

    [MenuItem("Tools/HealthySim/Fix Missing Scripts In Mobile UI Prefab")]
    public static void FixMissingScriptsInMobileUiPrefab()
    {
        const string prefabPath = "Assets/_Project/Prefabs/UI/MobileJoystickUI.prefab";
        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
        if (prefabRoot == null)
        {
            Debug.LogWarning($"[MissingScriptCleaner] Prefab not found: {prefabPath}");
            return;
        }

        int cleanedObjects = 0;
        int removedComponents = 0;

        foreach (Transform transform in prefabRoot.GetComponentsInChildren<Transform>(true))
        {
            int removed = RemoveMissingScriptsDeep(transform.gameObject);
            if (removed <= 0)
                continue;

            cleanedObjects++;
            removedComponents += removed;
        }

        FixMoveJoystickPresentersInHierarchy(prefabRoot.transform);

        if (removedComponents > 0)
            PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);

        PrefabUtility.UnloadPrefabContents(prefabRoot);
        Debug.Log($"[MissingScriptCleaner] Prefab cleaned. objects={cleanedObjects}, removed={removedComponents}");
    }

    private static void FixMoveJoystickPresentersInOpenScenes()
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (!scene.IsValid() || !scene.isLoaded)
                continue;

            foreach (GameObject root in scene.GetRootGameObjects())
                FixMoveJoystickPresentersInHierarchy(root.transform);
        }
    }

    private static void FixMoveJoystickPresentersInHierarchy(Transform root)
    {
        foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
        {
            if (!string.Equals(transform.name, "MoveJoystickOuter", System.StringComparison.Ordinal))
                continue;

            if (transform.GetComponent<MobileMoveStickPresenter>() != null)
                continue;

            transform.gameObject.AddComponent<MobileMoveStickPresenter>();
            EditorUtility.SetDirty(transform.gameObject);
        }
    }

    private static int CountMissingScripts(GameObject gameObject)
    {
        int count = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(gameObject);

        Component[] components = gameObject.GetComponents<Component>();
        for (int i = 0; i < components.Length; i++)
        {
            if (components[i] == null)
                count++;
        }

        MonoBehaviour[] behaviours = gameObject.GetComponents<MonoBehaviour>();
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] != null && IsBrokenMonoBehaviour(behaviours[i]))
                count++;
        }

        return count;
    }

    private static int RemoveMissingScriptsDeep(GameObject gameObject)
    {
        int removed = RemoveBrokenMonoBehaviours(gameObject);

        int passRemoved;
        do
        {
            passRemoved = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(gameObject);
            removed += passRemoved;
        }
        while (passRemoved > 0);

        return removed;
    }

    private static int RemoveBrokenMonoBehaviours(GameObject gameObject)
    {
        int removed = 0;
        MonoBehaviour[] behaviours = gameObject.GetComponents<MonoBehaviour>();
        for (int i = behaviours.Length - 1; i >= 0; i--)
        {
            MonoBehaviour behaviour = behaviours[i];
            if (behaviour == null)
                continue;

            if (!IsBrokenMonoBehaviour(behaviour))
                continue;

            Object.DestroyImmediate(behaviour);
            removed++;
        }

        return removed;
    }

    private static bool IsBrokenMonoBehaviour(MonoBehaviour behaviour)
    {
        if (behaviour == null)
            return true;

        // Deleted scripts surface as the plain MonoBehaviour base type in the Inspector.
        if (behaviour.GetType() == typeof(MonoBehaviour))
            return true;

        try
        {
            SerializedObject serializedObject = new SerializedObject(behaviour);
            SerializedProperty classIdentifier = serializedObject.FindProperty("m_EditorClassIdentifier");
            string identifier = classIdentifier != null ? classIdentifier.stringValue : string.Empty;
            if (identifier.Contains("MobileInputController/MobileJoystick"))
                return true;

            SerializedProperty scriptProperty = serializedObject.FindProperty("m_Script");
            MonoScript monoScript = scriptProperty != null ? scriptProperty.objectReferenceValue as MonoScript : null;
            if (monoScript == null)
                return true;

            return monoScript.GetClass() == null;
        }
        catch
        {
            return true;
        }
    }

    private static string GetHierarchyPath(Transform transform)
    {
        string path = transform.name;
        Transform current = transform.parent;

        while (current != null)
        {
            path = current.name + "/" + path;
            current = current.parent;
        }

        return path;
    }
}
#endif
