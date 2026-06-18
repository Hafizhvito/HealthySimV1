#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class SleepBedSceneSetup
{
    private const string SampleScenePath = "Assets/Scenes/SampleScene.unity";

    [MenuItem("HealthySim/Setup/Ensure Sleep Bed Placeholder")]
    public static void EnsureSleepBedPlaceholder()
    {
        Scene scene = EditorSceneManager.OpenScene(SampleScenePath, OpenSceneMode.Single);
        if (!scene.IsValid())
        {
            Debug.LogError("[SleepBedSceneSetup] SampleScene tidak bisa dibuka.");
            return;
        }

        GameObject bed = GameObject.Find("Interactable_Bed");
        if (bed == null)
        {
            bed = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bed.name = "Interactable_Bed";
            bed.transform.position = new Vector3(-60f, 0.4f, 27f);
            bed.transform.localScale = new Vector3(2.2f, 0.45f, 1.25f);

            Renderer renderer = bed.GetComponent<Renderer>();
            if (renderer != null)
                renderer.material.color = new Color(0.64f, 0.42f, 0.28f);
        }

        Transform spawn = bed.transform.Find("BedSpawnPoint");
        if (spawn == null)
        {
            GameObject spawnObj = new GameObject("BedSpawnPoint");
            spawnObj.transform.SetParent(bed.transform, false);
            spawnObj.transform.localPosition = new Vector3(0f, 1.05f, -0.35f);
            spawnObj.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
        }

        if (bed.GetComponent<SleepBedInteractable>() == null)
            bed.AddComponent<SleepBedInteractable>();

        Collider col = bed.GetComponent<Collider>();
        if (col == null)
            bed.AddComponent<BoxCollider>();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[SleepBedSceneSetup] Placeholder kasur siap di SampleScene (visible sebelum Play).");
    }

    [MenuItem("HealthySim/Setup/Wire Sleep Wake To bedSingle")]
    public static void WireSleepWakeToBedSingle()
    {
        Scene scene = EditorSceneManager.GetActiveScene();
        if (!scene.IsValid())
        {
            Debug.LogError("[SleepBedSceneSetup] Tidak ada scene aktif.");
            return;
        }

        GameObject bedSingle = GameObject.Find("bedSingle");
        if (bedSingle == null)
        {
            Debug.LogError("[SleepBedSceneSetup] Object 'bedSingle' tidak ditemukan di scene aktif.");
            return;
        }

        SleepBedInteractable sleepBed = bedSingle.GetComponent<SleepBedInteractable>();
        if (sleepBed == null)
            sleepBed = bedSingle.AddComponent<SleepBedInteractable>();

        Transform spawn = bedSingle.transform.Find("BedSpawnPoint");
        if (spawn == null)
        {
            GameObject spawnObj = new GameObject("BedSpawnPoint");
            spawnObj.transform.SetParent(bedSingle.transform, false);
            spawn = spawnObj.transform;
        }

        Collider bedCollider = bedSingle.GetComponent<Collider>();
        if (bedCollider == null)
            bedCollider = bedSingle.GetComponentInChildren<Collider>();

        Vector3 worldSpawn;
        if (bedCollider != null)
        {
            Bounds bounds = bedCollider.bounds;
            worldSpawn = new Vector3(bounds.center.x, bounds.max.y + 0.05f, bounds.center.z);
        }
        else
        {
            worldSpawn = bedSingle.transform.position + Vector3.up * 0.35f;
        }

        spawn.position = worldSpawn;
        spawn.rotation = Quaternion.Euler(0f, bedSingle.transform.eulerAngles.y, 0f);

        SerializedObject serializedSleep = new SerializedObject(sleepBed);
        serializedSleep.FindProperty("bedSpawnPoint").objectReferenceValue = spawn;
        serializedSleep.ApplyModifiedPropertiesWithoutUndo();

        SleepBedInteractable legacyBed = null;
        GameObject interactableBed = GameObject.Find("Interactable_Bed");
        if (interactableBed != null)
            legacyBed = interactableBed.GetComponent<SleepBedInteractable>();

        if (legacyBed != null && legacyBed != sleepBed)
            Object.DestroyImmediate(legacyBed);

        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log($"[SleepBedSceneSetup] bedSpawnPoint di '{bedSingle.name}' → {spawn.position}. Save scene (Ctrl+S).");
    }
}
#endif
