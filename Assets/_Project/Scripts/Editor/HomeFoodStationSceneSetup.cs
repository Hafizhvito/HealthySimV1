#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class HomeFoodStationSceneSetup
{
    private const string SampleScenePath = "Assets/Scenes/SampleScene.unity";
    private const string PlaceholderName = "HomeFoodStation_Placeholder";

    [MenuItem("HealthSim/Setup/Ensure Home Food Station Placeholder")]
    public static void EnsureHomeFoodStationPlaceholder()
    {
        Scene scene = EditorSceneManager.OpenScene(SampleScenePath, OpenSceneMode.Single);
        if (!scene.IsValid())
        {
            Debug.LogError("[HomeFoodStationSceneSetup] SampleScene tidak bisa dibuka.");
            return;
        }

        HomeFoodStationInteractable station = Object.FindFirstObjectByType<HomeFoodStationInteractable>(FindObjectsInactive.Include);
        GameObject target = station != null ? station.gameObject : null;

        if (target == null)
        {
            target = GameObject.CreatePrimitive(PrimitiveType.Cube);
            target.name = PlaceholderName;
            target.transform.position = new Vector3(-54.5f, 0.6f, 30.25f);
            target.transform.localScale = new Vector3(1.2f, 1.2f, 0.9f);
        }

        if (target.GetComponent<Collider>() == null)
            target.AddComponent<BoxCollider>();

        if (target.GetComponent<HomeFoodStationInteractable>() == null)
            target.AddComponent<HomeFoodStationInteractable>();

        Renderer renderer = target.GetComponent<Renderer>();
        if (renderer != null)
        {
            Material mat = renderer.sharedMaterial;
            if (mat != null)
            {
                mat.color = new Color(0.23f, 0.66f, 0.86f, 1f);
            }
            else
            {
                renderer.material.color = new Color(0.23f, 0.66f, 0.86f, 1f);
            }
        }

        Selection.activeGameObject = target;

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[HomeFoodStationSceneSetup] Placeholder home food station siap dan visible di edit mode.");
    }
}
#endif
