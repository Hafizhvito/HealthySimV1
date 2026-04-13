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

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoBootstrapAfterSceneLoad()
    {
        var activeScene = SceneManager.GetActiveScene();
        if (activeScene.name != TargetSceneName)
            return;

        if (FindFirstObjectByType<SampleSceneBootstrap>() != null)
            return;

        var bootstrap = new GameObject("SampleSceneBootstrap");
        bootstrap.AddComponent<SampleSceneBootstrap>();
    }

    void Start()
    {
        if (SceneManager.GetActiveScene().name != TargetSceneName)
            return;

        EnsureCoreManagers();
        EnsureEventSystemSetup();
        EnsurePlayerInteraction();
        EnsurePlaceholderInteractables();
        WirePlaceholderDialogueAssignments();

        StartCoroutine(RunIntroSequence());
    }

    private void EnsureCoreManagers()
    {
        GameObject manager = GameObject.Find("GameManager");
        if (manager == null)
        {
            manager = new GameObject("GameManager");
            Debug.LogWarning($"{FallbackLogPrefix} Created GameManager at runtime. Prefer authored scene object to keep swap contract stable.");
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
    }

    private void EnsureEventSystemSetup()
    {
        EventSystem eventSystem = FindFirstObjectByType<EventSystem>();
        if (eventSystem == null)
        {
            GameObject eventObj = new GameObject("EventSystem");
            eventSystem = eventObj.AddComponent<EventSystem>();
            Debug.LogWarning($"{FallbackLogPrefix} Created EventSystem at runtime.");
        }

        if (eventSystem.GetComponent<StandaloneInputModule>() == null)
        {
            eventSystem.gameObject.AddComponent<StandaloneInputModule>();
            Debug.LogWarning($"{FallbackLogPrefix} Added StandaloneInputModule to EventSystem at runtime.");
        }

#if ENABLE_INPUT_SYSTEM
        if (eventSystem.GetComponent<InputSystemUIInputModule>() == null)
        {
            eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
            Debug.LogWarning($"{FallbackLogPrefix} Added InputSystemUIInputModule to EventSystem at runtime.");
        }
#endif
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

            GameObject spawnObj = new GameObject("BedSpawnPoint");
            spawnObj.transform.SetParent(bedObj.transform, false);
            spawnObj.transform.localPosition = new Vector3(0f, 1.05f, -0.35f);
            spawnObj.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

            EnsureComponent<SleepBedInteractable>(bedObj);
            Debug.LogWarning($"{FallbackLogPrefix} Created placeholder Interactable_Bed at runtime.");
        }
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

        if (StoryIntroManager.Instance != null)
            yield return StoryIntroManager.Instance.StartIntroFlow();
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
