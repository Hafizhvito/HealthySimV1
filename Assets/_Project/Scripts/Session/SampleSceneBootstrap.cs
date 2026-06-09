using System.Collections;
using System;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif
using UnityEngine.UI;

public class SampleSceneBootstrap : MonoBehaviour
{
    private const string TargetSceneName = "SampleScene";
    private const string FallbackLogPrefix = "[SwapContract/Fallback]";

    [Header("Intro Cutscene")]
    [SerializeField] private bool forceIntroEveryPlay = true;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoBootstrapAfterSceneLoad()
    {
        SceneManager.sceneLoaded += HandleAutoBootstrapSceneLoaded;
    }

    private static void HandleAutoBootstrapSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Debug.Log($"Bootstrap check — scene loaded: {scene.name}");

        if (scene.name != TargetSceneName) // skip scene kalo bukan yang dituju
        {
            Debug.Log($"Bootstrap skip — bukan {TargetSceneName}");
            return;
        }

        // hilangkan script di scene supaya tidak dipanggil lagi
        SceneManager.sceneLoaded -= HandleAutoBootstrapSceneLoaded;

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.GetComponentInChildren<SampleSceneBootstrap>(true) != null)
            {
                Debug.Log("Bootstrap sudah ada di scene, skip runtime spawn.");
                return;
            }
        }

        Debug.LogWarning($"{FallbackLogPrefix} Spawning SampleSceneBootstrap at runtime. Add it to the scene via HealthySim/Setup SampleScene Core.");
        var bootstrap = new GameObject("SampleSceneBootstrap");
        bootstrap.AddComponent<SampleSceneBootstrap>();
    }

    private Coroutine _sceneEntryRoutine;
    private bool _sceneEntryHandled;

    void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneEntryLoaded;
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneEntryLoaded;
    }

    void Start()
    {
        // Runtime-spawned bootstrap (scene already loaded) still needs one entry pass.
        if (!_sceneEntryHandled)
            BeginSceneEntryIfNeeded();
    }

    private void HandleSceneEntryLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != TargetSceneName)
            return;

        _sceneEntryHandled = false;
        BeginSceneEntryIfNeeded();
    }

    private void BeginSceneEntryIfNeeded()
    {
        if (SceneManager.GetActiveScene().name != TargetSceneName)
            return;

        if (_sceneEntryRoutine != null)
            StopCoroutine(_sceneEntryRoutine);

        _sceneEntryHandled = true;
        _sceneEntryRoutine = StartCoroutine(RunSceneEntryRoutine());
    }

    private IEnumerator RunSceneEntryRoutine()
    {
        AudioListenerEnforcer.EnforceSingleListener();

        EnsureCoreManagers();
        EnsureEventSystemSetup();
        EnsurePlayerInteraction();
        EnsurePlaceholderInteractables();
        WirePlaceholderDialogueAssignments();

        PlayerData.Load();
        if (PlayerStats.Instance != null)
        {
            string savedName   = !string.IsNullOrWhiteSpace(PlayerData.PlayerName) ? PlayerData.PlayerName : "Pemain";
            float savedHeight  = PlayerData.TinggiBadan > 0 ? PlayerData.TinggiBadan : 170f;
            float savedWeight  = PlayerData.BeratBadan  > 0 ? PlayerData.BeratBadan  : 65f;
            PlayerStats.Instance.SetPlayerData(savedName, savedHeight, savedWeight);
            PlayerStats.Instance.SetGender(ResolvePlayerGender(PlayerData.JenisKelamin));
            Debug.Log($"[Bootstrap] PlayerData wired → name={savedName} h={savedHeight} w={savedWeight} gender={PlayerStats.Instance.PlayerGender}");
        }
        else
        {
            Debug.LogWarning("[Bootstrap] PlayerStats.Instance null — SetPlayerData skipped.");
        }

        EnsurePlayerCharacterSwapper();

        yield return RunIntroSequence();
        _sceneEntryRoutine = null;
    }

    private void EnsureCoreManagers()
    {
        GameObject manager = FindManagerHost();

        if (manager == null)
        {
            manager = new GameObject("GameManager");
            DontDestroyOnLoad(manager);
            Debug.LogWarning($"{FallbackLogPrefix} Created GameManager at runtime. Run HealthySim/Setup SampleScene Core in the Editor.");
        }
        else if (manager.scene.name != "DontDestroyOnLoad")
        {
            GameObject root = manager.transform.root.gameObject;
            DontDestroyOnLoad(root);
        }

        EnsureComponent<SessionSeedManager>(manager);
        EnsureComponent<PlayerActionTracker>(manager);
        EnsureComponent<StoryIntroManager>(manager);
        EnsureComponent<StoryManager>(manager);
        EnsureComponent<IntroCutsceneController>(manager);
        EnsureComponent<SessionFlowController>(manager);
        EnsureComponent<FoodChoiceMenuController>(manager);
        EnsureComponent<FoodStashMenuController>(manager);
        EnsureComponent<NpcDialogueMenuController>(manager);
        EnsureComponent<FoodCatalogProvider>(manager);
        EnsureComponent<DialogueCatalogProvider>(manager);
        EnsureComponent<SessionFoodStash>(manager);
        EnsureComponent<ModalStateManager>(manager);
        EnsureComponent<WorkReminderUI>(manager);
        EnsureComponent<WorkSessionManager>(manager);
        EnsureComponent<FadeManager>(manager);
        EnsureComponent<MobileInputController>(manager);
        EnsureComponent<BazaarManager>(manager);
        EnsureComponent<EndingManager>(manager);
        EnsureComponent<GameplayAudioListenerKeeper>(manager);
    }

    private void EnsureEventSystemSetup()
    {
        EventSystem eventSystem = FindFirstObjectByType<EventSystem>();
        if (eventSystem == null)
            eventSystem = FindFirstObjectByType<EventSystem>(FindObjectsInactive.Include);

        if (eventSystem == null)
        {
            GameObject eventObj = new GameObject("EventSystem");
            eventSystem = eventObj.AddComponent<EventSystem>();
            Debug.LogWarning($"{FallbackLogPrefix} Created EventSystem at runtime.");
        }

        StandaloneInputModule standaloneModule = eventSystem.GetComponent<StandaloneInputModule>();
#if ENABLE_INPUT_SYSTEM
        InputSystemUIInputModule inputSystemModule = eventSystem.GetComponent<InputSystemUIInputModule>();
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        if (standaloneModule == null)
        {
            standaloneModule = eventSystem.gameObject.AddComponent<StandaloneInputModule>();
            Debug.LogWarning($"{FallbackLogPrefix} Added StandaloneInputModule to EventSystem at runtime.");
        }

        standaloneModule.enabled = true;

#if ENABLE_INPUT_SYSTEM
        if (inputSystemModule != null)
            inputSystemModule.enabled = false;
#endif
#else
        if (standaloneModule != null)
            standaloneModule.enabled = false;

#if ENABLE_INPUT_SYSTEM
        if (inputSystemModule == null)
        {
            inputSystemModule = eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
            Debug.LogWarning($"{FallbackLogPrefix} Added InputSystemUIInputModule to EventSystem at runtime.");
        }

        inputSystemModule.enabled = true;
#endif
#endif

        eventSystem.sendNavigationEvents = true;
        eventSystem.enabled = true;
        eventSystem.gameObject.SetActive(true);
    }

    private void EnsurePlayerInteraction()
    {
        GameObject player = GameObject.FindWithTag("Player");
        if (player == null)
        {
            Debug.LogWarning("[Bootstrap] Player tidak ditemukan. Interaction tidak diaktifkan.");
            return;
        }

        EnsureComponent<UniversalInteractionController>(player);
    }

    private void EnsurePlayerCharacterSwapper()
    {
        GameObject player = GameObject.FindWithTag("Player");
        if (player == null)
        {
            Debug.LogWarning("[Bootstrap] Player tidak ditemukan. CharacterModelSwapper tidak diaktifkan.");
            return;
        }

        CharacterModelSwapper swapper = EnsureComponent<CharacterModelSwapper>(player);
        if (PlayerStats.Instance != null)
            swapper.InitializeModel();
    }

    private static PlayerStats.Gender ResolvePlayerGender(string jenisKelamin)
    {
        if (string.Equals(jenisKelamin, "Perempuan", StringComparison.OrdinalIgnoreCase))
            return PlayerStats.Gender.Female;

        return PlayerStats.Gender.Male;
    }

    private void EnsurePlaceholderInteractables()
    {
        FoodPickupInteractable[] foodPoints = FindObjectsByType<FoodPickupInteractable>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        HomeFoodStationInteractable[] homeStations = FindObjectsByType<HomeFoodStationInteractable>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        if (foodPoints.Length > 1)
        {
            FoodPickupInteractable keep = null;
            for (int i = 0; i < foodPoints.Length; i++)
            {
                if (foodPoints[i] == null)
                    continue;

                if (keep == null)
                    keep = foodPoints[i];

                if (string.Equals(foodPoints[i].gameObject.name, "Interactable_Food_Restaurant", StringComparison.OrdinalIgnoreCase))
                {
                    keep = foodPoints[i];
                    break;
                }
            }

            for (int i = 0; i < foodPoints.Length; i++)
            {
                FoodPickupInteractable item = foodPoints[i];
                if (item == null || item == keep)
                    continue;

                Debug.LogWarning($"{FallbackLogPrefix} Removed extra FoodPickupInteractable '{item.gameObject.name}' at runtime.");
                Destroy(item.gameObject);
            }
        }

        bool hasFoodSetup = FindFirstObjectByType<FoodPickupInteractable>() != null
            && FindFirstObjectByType<HomeFoodStationInteractable>() != null;

        GameObject legacyFoodCube = GameObject.Find("Interactable_FoodCube");
        if (hasFoodSetup && legacyFoodCube != null)
        {
            Debug.LogWarning($"{FallbackLogPrefix} Removed legacy Interactable_FoodCube at runtime (scene already has restaurant + home station).");
            Destroy(legacyFoodCube);
        }

        if (!hasFoodSetup && legacyFoodCube == null)
        {
            var foodObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            foodObj.name = "Interactable_FoodCube";
            foodObj.transform.position = new Vector3(-55f, 1f, 31f);
            foodObj.transform.localScale = new Vector3(1.2f, 1.2f, 1.2f);
            foodObj.GetComponent<Renderer>().material.color = new Color(0.2f, 0.8f, 0.2f);
            EnsureComponent<FoodPickupInteractable>(foodObj);
            Debug.LogWarning($"{FallbackLogPrefix} Created placeholder Interactable_FoodCube at runtime.");
        }

        if (GameObject.Find("Interactable_NPC") == null)
        {
            var npcObj = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            npcObj.name = "Interactable_NPC";
            npcObj.transform.position = new Vector3(-52f, 1f, 31f);
            npcObj.GetComponent<Renderer>().material.color = new Color(0.2f, 0.5f, 0.95f);
            EnsureComponent<NpcDialogueInteractable>(npcObj);
            Debug.LogWarning($"{FallbackLogPrefix} Created placeholder Interactable_NPC at runtime.");
        }

        if (GameObject.Find("Interactable_NPC_Restoran") == null)
        {
            var npcObj = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            npcObj.name = "Interactable_NPC_Restoran";
            npcObj.transform.position = new Vector3(-49f, 1f, 34f);
            npcObj.GetComponent<Renderer>().material.color = new Color(0.95f, 0.55f, 0.2f);
            EnsureComponentByTypeName(npcObj, "NpcRestaurantInteractable");
            Debug.LogWarning($"{FallbackLogPrefix} Created placeholder Interactable_NPC_Restoran at runtime.");
        }

        if (GameObject.Find("Interactable_Bed") == null)
        {
            var bedObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bedObj.name = "Interactable_Bed";
            bedObj.transform.position = new Vector3(-60f, 0.4f, 27f);
            bedObj.transform.localScale = new Vector3(2.2f, 0.45f, 1.25f);
            bedObj.GetComponent<Renderer>().material.color = new Color(0.64f, 0.42f, 0.28f);
            EnsureComponent<SleepBedInteractable>(bedObj);
            Debug.LogWarning($"{FallbackLogPrefix} Created placeholder Interactable_Bed at runtime.");
        }

        // ── SpawnPoint: jangan pernah destroy SpawnPoint yang sudah ada di scene ──
        // Kalau sudah ada SpawnPointID di scene, pakai yang itu — jangan buat baru, jangan destroy.
        SpawnPointID[] existingSpawnPoints = FindObjectsByType<SpawnPointID>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        if (existingSpawnPoints.Length > 0)
        {
            // Pastikan semua SpawnPointID punya ID "spawnpoint" supaya SpawnPlayerManager bisa nemuin
            foreach (SpawnPointID sp in existingSpawnPoints)
            {
                if (sp == null) continue;
                FieldInfo spawnField = typeof(SpawnPointID).GetField("spawnID", BindingFlags.NonPublic | BindingFlags.Instance);
                if (spawnField != null)
                {
                    string currentId = spawnField.GetValue(sp) as string;
                    if (string.IsNullOrWhiteSpace(currentId))
                    {
                        spawnField.SetValue(sp, "spawnpoint");
                        Debug.Log($"[Bootstrap] SpawnPointID '{sp.gameObject.name}' — ID kosong, diset ke 'spawnpoint'.");
                    }
                    else
                    {
                        Debug.Log($"[Bootstrap] SpawnPointID '{sp.gameObject.name}' sudah ada dengan ID='{currentId}' di {sp.transform.position} — skip.");
                    }
                }
            }
            return;
        }

        // Tidak ada SpawnPointID sama sekali — buat fallback
        GameObject spawnPoint = GameObject.Find("SpawnPoint") ?? GameObject.FindGameObjectWithTag("SpawnPoint");
        if (spawnPoint == null)
        {
            spawnPoint = new GameObject("SpawnPoint");
            GameObject bedObj = GameObject.Find("Interactable_Bed");
            if (bedObj != null)
            {
                spawnPoint.transform.position = bedObj.transform.position + new Vector3(0f, 1.05f, -0.35f);
                spawnPoint.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            }
            else
            {
                spawnPoint.transform.position = new Vector3(0f, 1f, 0f);
            }
            Debug.LogWarning($"{FallbackLogPrefix} Created fallback SpawnPoint (tidak ada SpawnPointID di scene).");
        }

        SpawnPointID newSpawnId = spawnPoint.GetComponent<SpawnPointID>();
        if (newSpawnId == null)
            newSpawnId = spawnPoint.AddComponent<SpawnPointID>();

        FieldInfo field = typeof(SpawnPointID).GetField("spawnID", BindingFlags.NonPublic | BindingFlags.Instance);
        if (field != null)
            field.SetValue(newSpawnId, "spawnpoint");
    }

    private void WirePlaceholderDialogueAssignments()
    {
        GameObject manager = GameObject.Find("GameManager");
        if (manager == null)
            return;

        DialogueCatalogProvider provider = manager.GetComponent<DialogueCatalogProvider>();
        List<DialogueGraphData> dialogues = provider != null ? provider.GetDialogues() : new List<DialogueGraphData>();

        DialogueGraphData streetDialogue = FindDialogue(dialogues, "npc_utama", "NPCUtama_Dialogue", "NPCUtama_Dialogue_Alt");

        GameObject streetNpc = GameObject.Find("Interactable_NPC");
        if (streetNpc != null)
        {
            NpcDialogueInteractable streetInteractable = EnsureComponent<NpcDialogueInteractable>(streetNpc);
            if (streetDialogue != null)
                streetInteractable.SetDialogueOptions(new List<DialogueGraphData> { streetDialogue }, false);
        }

        GameObject restaurantNpc = GameObject.Find("Interactable_NPC_Restoran");
        if (restaurantNpc != null)
        {
            NpcRestaurantInteractable restaurantInteractable = restaurantNpc.GetComponent<NpcRestaurantInteractable>();
            if (restaurantInteractable != null)
                restaurantInteractable.SetDialogueIds("npc_restoran_first", "npc_restoran_return");
        }
    }

    private DialogueGraphData FindDialogue(List<DialogueGraphData> dialogues, string npcId, string preferredName, string fallbackName)
    {
        for (int i = 0; i < dialogues.Count; i++)
        {
            DialogueGraphData graph = dialogues[i];
            if (graph == null)
                continue;

            if (!string.IsNullOrWhiteSpace(graph.npcId) && string.Equals(graph.npcId, npcId, StringComparison.OrdinalIgnoreCase))
                return graph;
        }

        for (int i = 0; i < dialogues.Count; i++)
        {
            DialogueGraphData graph = dialogues[i];
            if (graph == null)
                continue;

            if (string.Equals(graph.name, preferredName, StringComparison.OrdinalIgnoreCase))
                return graph;
        }

        for (int i = 0; i < dialogues.Count; i++)
        {
            DialogueGraphData graph = dialogues[i];
            if (graph == null)
                continue;

            if (string.Equals(graph.name, fallbackName, StringComparison.OrdinalIgnoreCase))
                return graph;
        }

        return null;
    }

    private IEnumerator RunIntroSequence()
    {
        // Let singleton startup settle for one frame.
        yield return null;
        AudioListenerEnforcer.EnforceSingleListener();

        if (StoryIntroManager.Instance == null)
            yield break;

        if (forceIntroEveryPlay)
            StoryIntroManager.Instance.ResetIntroFlag();

        yield return StoryIntroManager.Instance.StartIntroFlow();
    }

    private static GameObject FindManagerHost()
    {
        GameObject ddolManager = null;
        GameObject sceneManager = null;
        GameObject ddolMobileHost = null;
        GameObject sceneMobileHost = null;
        Scene activeScene = SceneManager.GetActiveScene();

        GameObject[] allObjects = Resources.FindObjectsOfTypeAll<GameObject>();
        for (int i = 0; i < allObjects.Length; i++)
        {
            GameObject obj = allObjects[i];
            if (obj == null || !obj.scene.IsValid())
                continue;

            if (string.Equals(obj.name, "GameManager", StringComparison.Ordinal))
            {
                if (obj.scene.name == "DontDestroyOnLoad")
                    ddolManager = obj;
                else if (obj.scene == activeScene)
                    sceneManager = obj;
            }
        }

        MobileInputController[] mobileHosts = Resources.FindObjectsOfTypeAll<MobileInputController>();
        for (int i = 0; i < mobileHosts.Length; i++)
        {
            MobileInputController host = mobileHosts[i];
            if (host == null || !host.gameObject.scene.IsValid())
                continue;

            if (host.gameObject.scene.name == "DontDestroyOnLoad")
                ddolMobileHost = host.gameObject;
            else if (host.gameObject.scene == activeScene)
                sceneMobileHost = host.gameObject;
        }

        if (ddolManager != null)
            return ddolManager;
        if (sceneManager != null)
            return sceneManager;
        if (ddolMobileHost != null)
            return ddolMobileHost;
        return sceneMobileHost;
    }

    private static T EnsureComponent<T>(GameObject target) where T : Component
    {
        T component = target.GetComponent<T>();
        if (component == null)
        {
            component = target.AddComponent<T>();
            Debug.LogWarning($"{FallbackLogPrefix} Added {typeof(T).Name} to {target.name} at runtime.");
        }

        return component;
    }

    private static Component EnsureComponentByTypeName(GameObject target, string typeName)
    {
        if (target == null || string.IsNullOrWhiteSpace(typeName))
            return null;

        Component existing = target.GetComponent(typeName);
        if (existing != null)
            return existing;

        Type found = null;
        Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
        for (int i = 0; i < assemblies.Length; i++)
        {
            found = assemblies[i].GetType(typeName);
            if (found != null)
                break;
        }

        if (found == null)
        {
            Debug.LogWarning($"[Bootstrap] Tipe komponen tidak ditemukan: {typeName}");
            return null;
        }

        Debug.LogWarning($"{FallbackLogPrefix} Added {typeName} to {target.name} at runtime.");
        return target.AddComponent(found);
    }
}