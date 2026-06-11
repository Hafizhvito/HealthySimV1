using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class MainSpawnPointSceneSetup
{
    private const string MainSpawnName = "SpawnPoint (Main)";
    private const string DefaultSpawnId = "spawnpoint";
    private const string LegacySpawnId = "legacy_spawnpoint";

    [MenuItem("HealthySim/Setup Main Spawn Point")]
    public static void SetupMainSpawnPoint()
    {
        GameObject mainSpawn = GameObject.Find(MainSpawnName);
        if (mainSpawn == null)
        {
            try
            {
                mainSpawn = GameObject.FindGameObjectWithTag("Respawn");
            }
            catch (UnityException)
            {
                // Tag may not exist yet.
            }
        }

        if (mainSpawn == null)
        {
            Debug.LogWarning($"[HealthySim] '{MainSpawnName}' atau tag Respawn tidak ditemukan di scene aktif.");
            return;
        }

        Undo.RegisterFullObjectHierarchyUndo(mainSpawn, "Setup Main Spawn Point");

        SpawnPointID mainId = mainSpawn.GetComponent<SpawnPointID>();
        if (mainId == null)
            mainId = Undo.AddComponent<SpawnPointID>(mainSpawn);

        SerializedObject mainSo = new SerializedObject(mainId);
        mainSo.FindProperty("spawnID").stringValue = DefaultSpawnId;
        mainSo.ApplyModifiedPropertiesWithoutUndo();

        int demoted = 0;
        SpawnPointID[] allSpawnPoints = Object.FindObjectsByType<SpawnPointID>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        for (int i = 0; i < allSpawnPoints.Length; i++)
        {
            SpawnPointID candidate = allSpawnPoints[i];
            if (candidate == null || candidate == mainId)
                continue;

            SerializedObject candidateSo = new SerializedObject(candidate);
            string currentId = candidateSo.FindProperty("spawnID").stringValue;
            if (currentId != DefaultSpawnId)
                continue;

            candidateSo.FindProperty("spawnID").stringValue = LegacySpawnId;
            candidateSo.ApplyModifiedPropertiesWithoutUndo();
            demoted++;
        }

        bool playerMoved = TryMovePlayerToSpawnInEditor(mainSpawn.transform);

        EditorSceneManager.MarkSceneDirty(mainSpawn.scene);
        Debug.Log($"[HealthySim] '{mainSpawn.name}' sekarang spawn utama (ID='{DefaultSpawnId}'). " +
                  $"{demoted} spawn lama diubah ke '{LegacySpawnId}'." +
                  (playerMoved ? " Player dipindah ke spawn di Scene view." : "") +
                  " Save scene (Ctrl+S).");
    }

    [MenuItem("HealthySim/Sync Player to Main Spawn (Editor View)")]
    public static void SyncPlayerToMainSpawnInEditor()
    {
        GameObject mainSpawn = GameObject.Find(MainSpawnName);
        if (mainSpawn == null)
        {
            try
            {
                mainSpawn = GameObject.FindGameObjectWithTag("Respawn");
            }
            catch (UnityException)
            {
                // Tag may not exist yet.
            }
        }

        if (mainSpawn == null)
        {
            Debug.LogWarning($"[HealthySim] '{MainSpawnName}' atau tag Respawn tidak ditemukan.");
            return;
        }

        if (!TryMovePlayerToSpawnInEditor(mainSpawn.transform))
        {
            Debug.LogWarning("[HealthySim] Player (tag Player) tidak ditemukan di scene.");
            return;
        }

        EditorSceneManager.MarkSceneDirty(mainSpawn.scene);
        Debug.Log("[HealthySim] Player dipindah ke spawn utama di Scene view. Save scene (Ctrl+S).");
    }

    private static bool TryMovePlayerToSpawnInEditor(Transform spawnTransform)
    {
        if (spawnTransform == null)
            return false;

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
            return false;

        Undo.RecordObject(player.transform, "Move Player to Main Spawn");
        player.transform.SetPositionAndRotation(spawnTransform.position, spawnTransform.rotation);
        return true;
    }
}
