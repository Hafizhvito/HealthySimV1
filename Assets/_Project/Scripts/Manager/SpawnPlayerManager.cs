using UnityEngine;
using UnityEngine.SceneManagement;
public class SpawnPlayerManager : MonoBehaviour
{
    public static SpawnPlayerManager _Instance { get; private set; }

    // ID spawn yang akan dipakai saat scene berikutnya di-load
    public static string TargetSpawnID { get; set; } = "";

    private void Awake()
    {
        if (_Instance != null && _Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (string.IsNullOrEmpty(TargetSpawnID)) return;

        // SpawnPoint dengan ID yang sesuai
        SpawnPointID[] spawnPoints = FindObjectsByType<SpawnPointID>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        SpawnPointID targetSpawn = null;
        foreach (var sp in spawnPoints)
        {
            if (sp.ID == TargetSpawnID)
            {
                targetSpawn = sp;
                break;
            }
        }

        if (targetSpawn == null)
        {
            Debug.LogWarning($"[PlayerSpawnManager] SpawnPoint '{TargetSpawnID}' tidak ditemukan di {scene.name}!");
            return;
        }

        // memindahkan player ke SpawnPoint
        GameObject player = GameObject.FindWithTag("Player");
        if (player == null)
        {
            Debug.LogWarning("[PlayerSpawnManager] Player tidak ditemukan!");
            return;
        }

        // disable controller sementara agar tidak error
        CharacterController cc = player.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;

        player.transform.position = targetSpawn.transform.position;
        player.transform.rotation = targetSpawn.transform.rotation;

        if (cc != null) cc.enabled = true;

        Debug.Log($"[PlayerSpawnManager] Player di-spawn di '{TargetSpawnID}' → {targetSpawn.transform.position}");

        TargetSpawnID = "";
    }
}
