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
                int missing = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject);
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

        GameObject[] roots = scene.GetRootGameObjects();
        foreach (GameObject root in roots)
        {
            foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
            {
                int removed = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(transform.gameObject);
                if (removed > 0)
                {
                    cleanedObjects++;
                    removedComponents += removed;
                    EditorUtility.SetDirty(transform.gameObject);
                }
            }
        }

        if (removedComponents > 0)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        Debug.Log($"[MissingScriptCleaner] Cleaned objects: {cleanedObjects}, removed missing components: {removedComponents}");
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
