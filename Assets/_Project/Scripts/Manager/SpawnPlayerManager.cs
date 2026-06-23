using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SpawnPlayerManager : MonoBehaviour
{
    public static SpawnPlayerManager _Instance { get; private set; }

    public static event Action OnSpawnComplete;

    public const string DefaultSpawnId = "spawnpoint";

    // ID spawn yang akan dipakai saat scene berikutnya di-load
    public static string TargetSpawnID { get; set; } = "";

    public static void PrepareDefaultSpawnOnNextLoad()
    {
        TargetSpawnID = DefaultSpawnId;
    }

    public static void ClearSpawnTarget()
    {
        TargetSpawnID = string.Empty;
    }

    /// <summary>
    /// True when SampleScene reload should use a non-default spawn (office/gym return, etc.).
    /// </summary>
    public static bool IsReturningFromSubSceneLoad()
    {
        if (string.IsNullOrEmpty(TargetSpawnID))
            return false;

        return !string.Equals(TargetSpawnID, DefaultSpawnId, StringComparison.OrdinalIgnoreCase);
    }

    private bool spawnCompletedThisSceneLoad;

    private void Awake()
    {
        if (_Instance != null && _Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _Instance = this;
        DontDestroyOnLoad(transform.root.gameObject);
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
        if (scene.name != "SampleScene")
            return;

        spawnCompletedThisSceneLoad = false;

        if (string.IsNullOrEmpty(TargetSpawnID))
            TargetSpawnID = DefaultSpawnId;

        if (IsReturningFromSubSceneLoad())
            StartCoroutine(SpawnForSubSceneReturn());
    }

    /// <summary>
    /// Called by SampleSceneBootstrap before intro so cutscene cameras frame the final spawn pose.
    /// </summary>
    public IEnumerator SpawnWhenPlayerReady()
    {
        if (spawnCompletedThisSceneLoad)
            yield break;

        const float timeoutSeconds = 8f;
        float elapsed = 0f;
        GameObject player = null;

        while (player == null && elapsed < timeoutSeconds)
        {
            player = GameObject.FindWithTag("Player");
            if (player == null)
            {
                PlayerController pc = FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);
                if (pc != null)
                    player = pc.gameObject;
            }

            if (player != null)
                break;

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        if (player == null)
        {
            Debug.LogWarning("[Spawn] Player tidak siap — spawn dibatalkan.");
            yield break;
        }

        SpawnPlayer();
    }

    private IEnumerator SpawnForSubSceneReturn()
    {
        // Let scene objects activate before resolving spawn points.
        yield return null;
        yield return null;
        yield return SpawnWhenPlayerReady();
    }

    private void SpawnPlayer()
    {
        if (spawnCompletedThisSceneLoad)
            return;

        if (string.IsNullOrEmpty(TargetSpawnID)) return;

        // Cari SpawnPoint
        SpawnPointID[] spawnPoints = FindObjectsByType<SpawnPointID>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        SpawnPointID targetSpawn = ResolveSpawnPointId(spawnPoints, TargetSpawnID);

        Transform fallbackSpawn = null;
        if (targetSpawn == null)
            fallbackSpawn = ResolveFallbackSpawnTransform();

        if (targetSpawn == null && fallbackSpawn == null)
        {
            Debug.LogWarning($"[Spawn] SpawnPoint '{TargetSpawnID}' tidak ditemukan dan fallback Respawn/SpawnPoint kosong!");
            return;
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
        player.transform.position = spawnTransform.position;
        player.transform.rotation = spawnTransform.rotation;
        if (spawnTransform.position.y < 1f)
            SnapToGround(player.transform, spawnTransform.position);

        if (cc != null) cc.enabled = true;

        spawnCompletedThisSceneLoad = true;
        TargetSpawnID = "";
        OnSpawnComplete?.Invoke();
    }

    // ── Dev Tool ──────────────────────────────────────────────
    [ContextMenu("Test Spawn")]
    public void TestSpawn()
    {
        SpawnPlayer();
    }

    private static SpawnPointID ResolveSpawnPointId(SpawnPointID[] spawnPoints, string targetId)
    {
        if (spawnPoints == null || spawnPoints.Length == 0 || string.IsNullOrWhiteSpace(targetId))
            return null;

        SpawnPointID taggedRespawn = null;
        SpawnPointID activeMatch = null;
        SpawnPointID anyMatch = null;

        for (int i = 0; i < spawnPoints.Length; i++)
        {
            SpawnPointID sp = spawnPoints[i];
            if (sp == null || sp.ID != targetId)
                continue;

            anyMatch ??= sp;

            if (sp.gameObject.activeInHierarchy)
                activeMatch ??= sp;

            if (sp.CompareTag("Respawn"))
                taggedRespawn = sp;
        }

        return taggedRespawn ?? activeMatch ?? anyMatch;
    }

    private static Transform ResolveFallbackSpawnTransform()
    {
        GameObject respawnTagged = null;
        try
        {
            respawnTagged = GameObject.FindGameObjectWithTag("Respawn");
        }
        catch (UnityException)
        {
            // Tag may not exist in project settings yet.
        }

        if (respawnTagged != null)
            return respawnTagged.transform;

        GameObject namedSpawn = GameObject.Find("SpawnPoint (Main)");
        if (namedSpawn != null)
            return namedSpawn.transform;

        GameObject spawnPoint = GameObject.Find("SpawnPoint");
        if (spawnPoint != null)
            return spawnPoint.transform;

        GameObject spawnPointTagged = GameObject.FindWithTag("SpawnPoint");
        return spawnPointTagged != null ? spawnPointTagged.transform : null;
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