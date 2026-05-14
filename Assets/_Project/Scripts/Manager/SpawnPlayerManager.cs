using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

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

        Debug.Log($"[Spawn] Scene loaded: {scene.name}, TargetSpawnID: {TargetSpawnID}");
        StartCoroutine(SpawnAfterEverything());
    }

    private IEnumerator SpawnAfterEverything()
    {
        // Tunggu Bootstrap selesai — Bootstrap jalan di Start()
        // Kita tunggu beberapa frame + waktu ekstra
        yield return null;          // frame 1
        yield return null;          // frame 2
        yield return null;          // frame 3
        yield return new WaitForSeconds(0.2f); // ekstra safety

        SpawnPlayer();
    }

    private void SpawnPlayer()
    {
        if (string.IsNullOrEmpty(TargetSpawnID)) return;

        // Cari SpawnPoint
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
            Debug.LogWarning($"[Spawn] SpawnPoint '{TargetSpawnID}' tidak ditemukan!");
            return;
        }

        // Cari player
        GameObject player = GameObject.FindWithTag("Player");
        if (player == null)
        {
            Debug.LogWarning("[Spawn] Player tidak ditemukan!");
            return;
        }

        // Disable CharacterController sementara
        CharacterController cc = player.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;

        // Teleport player
        player.transform.position = targetSpawn.transform.position;
        player.transform.rotation = targetSpawn.transform.rotation;

        if (cc != null) cc.enabled = true;

        Debug.Log($"[Spawn] ✅ Player di-spawn di '{TargetSpawnID}' → {targetSpawn.transform.position}");

        // Reset setelah dipakai
        TargetSpawnID = "";
    }

    // ── Dev Tool ──────────────────────────────────────────────
    [ContextMenu("Test Spawn")]
    public void TestSpawn()
    {
        SpawnPlayer();
    }
}
