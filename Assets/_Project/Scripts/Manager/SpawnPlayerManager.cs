using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SpawnPlayerManager : MonoBehaviour
{
    public static SpawnPlayerManager _Instance { get; private set; }

    public static event Action OnSpawnComplete;

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
        Debug.Log($"[Spawn DEBUG] TargetSpawnID saat SpawnPlayer() = '{TargetSpawnID}'");
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

        Transform fallbackSpawn = null;
        if (targetSpawn == null)
        {
            GameObject fallbackObj = GameObject.FindWithTag("SpawnPoint");
            fallbackSpawn = fallbackObj != null ? fallbackObj.transform : null;

            if (fallbackSpawn == null)
            {
                Debug.LogWarning($"[Spawn] SpawnPoint '{TargetSpawnID}' tidak ditemukan dan fallback tag 'SpawnPoint' kosong!");
                return;
            }
        }

        
        // Cari player (termasuk inactive karena bisa di-hide di scene sebelumnya)
        GameObject player = GameObject.FindWithTag("Player");
        if (player == null)
        {
            // Fallback: cari termasuk inactive
            PlayerController pc = FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);
            if (pc != null) player = pc.gameObject;
        }
        if (player == null)
        {
            Debug.LogWarning("[Spawn] Player tidak ditemukan!");
            return;
        }
        player.SetActive(true);

        // Disable CharacterController sementara
        CharacterController cc = player.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;

        // Teleport player
        Transform spawnTransform = targetSpawn != null ? targetSpawn.transform : fallbackSpawn;
        Debug.Log($"[Spawn] Target '{TargetSpawnID}' at {spawnTransform.position} (pre-teleport player at {player.transform.position}).");
        player.transform.position = spawnTransform.position;
        player.transform.rotation = spawnTransform.rotation;
        if (spawnTransform.position.y < 1f)
            SnapToGround(player.transform, spawnTransform.position);
        Debug.Log($"[Spawn] Player post-teleport at {player.transform.position}.");

        if (cc != null) cc.enabled = true;

        string spawnLabel = targetSpawn != null ? targetSpawn.ID : "SpawnPoint";
        Debug.Log($"[Spawn] ✅ Player di-spawn di '{spawnLabel}' → {spawnTransform.position}");

        // Reset setelah dipakai
        TargetSpawnID = "";
        OnSpawnComplete?.Invoke();
        StartCoroutine(WatchPlayerPosition());
    }

     private IEnumerator WatchPlayerPosition()
    {
        GameObject player = GameObject.FindWithTag("Player");
        if (player == null) yield break;
        
        for (int i = 0; i < 10; i++)
        {
            yield return new WaitForSeconds(0.1f);
            if (player == null) yield break;
            Debug.Log($"[SpawnWatch] t={i * 0.1f:F1}s player pos = {player.transform.position}");
        }
    }

    // ── Dev Tool ──────────────────────────────────────────────
    [ContextMenu("Test Spawn")]
    public void TestSpawn()
    {
        SpawnPlayer();
    }

    private static void SnapToGround(Transform player, Vector3 spawnPosition)
    {
        // Raycast dari atas untuk cari ground collider
        Vector3 rayOrigin = spawnPosition + Vector3.up * 20f;
        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 100f))
        {
            float groundY = hit.point.y + 0.05f;

            // Safety: hanya snap kalau ground ditemukan dalam jarak wajar dari SpawnPoint Y
            // Mencegah player di-snap ke collider void / trigger jauh di bawah
            if (Mathf.Abs(groundY - spawnPosition.y) < 10f)
            {
                player.position = new Vector3(spawnPosition.x, groundY, spawnPosition.z);
                Debug.Log($"[Spawn] SnapToGround berhasil → y={groundY:F2}");
                return;
            }

            Debug.LogWarning($"[Spawn] SnapToGround hit terlalu jauh dari SpawnPoint " +
                             $"(spawnY={spawnPosition.y:F2}, hitY={groundY:F2}) — pakai SpawnPoint Y.");
        }
        else
        {
            Debug.LogWarning($"[Spawn] SnapToGround raycast miss di {spawnPosition} — pakai SpawnPoint Y.");
        }

        // Fallback: pakai Y dari SpawnPoint dengan sedikit offset agar tidak nabrak collider bawah.
        player.position = new Vector3(spawnPosition.x, spawnPosition.y + 0.1f, spawnPosition.z);
    }
}