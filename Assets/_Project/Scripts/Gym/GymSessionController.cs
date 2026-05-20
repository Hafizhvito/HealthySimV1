using System.Collections;
using UnityEngine;

public class GymSessionController : MonoBehaviour
{
    [SerializeField] private ClockAnimationUI clockUI;
    [SerializeField] private NpcDialogueMenuController dialogueUI;
    [SerializeField] private GymTrainerDialogueController trainerDialogueController;
    [SerializeField] private string mainSceneName = "SampleScene";
    [SerializeField] private string gymSessionLockKey = "GymSession";

    [Header("Optional Scene References")]
    [SerializeField] private NpcDialogueInteractable trainerInteractableOverride;
    [SerializeField] private Transform playerSpawnPointOverride;
    [SerializeField] private GameObject playerOverride;

    [Header("Flow Timings")]
    [SerializeField] private float postLoadDelay = 0.5f;
    [SerializeField] private float dialoguePollInterval = 0.05f;
    [SerializeField] private float trainingAnimationDuration = 3.2f;

    private GymSessionData activeSession;
    private NpcDialogueInteractable trainerInteractable;
    private bool gymModalOpened;
    private bool fallbackPlayerLockActive;
    private PlayerController fallbackPlayerController;
    private const string FallbackLogPrefix = "[SwapContract/Fallback]";

    private void Start()
    {
        StartCoroutine(RunFlow());
    }

    private void OnDisable()
    {
        SetGymSessionLock(false);
    }

    private void OnDestroy()
    {
        SetGymSessionLock(false);
    }

    private IEnumerator RunFlow()
    {
        GymProgressionSystem progression = GymProgressionSystem.Instance;
        if (progression == null || progression.PendingSession == null)
        {
            ExitToMainScene();
            yield break;
        }

        activeSession = progression.PendingSession;
        MovePlayerToSpawnPoint();

        yield return new WaitForSecondsRealtime(postLoadDelay);

        if (!ResolveSceneDependencies())
        {
            ExitToMainScene();
            yield break;
        }

        SetGymSessionLock(true);

        DialogueGraphData preDialogue = trainerDialogueController.BuildPreTrainingDialogue(activeSession);
        yield return StartCoroutine(PlayDialogueAndWait(preDialogue));

        bool animationDone = false;
        clockUI.PlayTimeSkipAnimation(
            "Sesi Latihan",
            activeSession.startHour,
            activeSession.endHour,
            trainingAnimationDuration,
            () => animationDone = true);

        while (!animationDone)
            yield return null;

        progression.ApplyResult(activeSession);
        PlayerStats.Instance?.RegisterHealthScore(10f);

        DialogueGraphData postDialogue = trainerDialogueController.BuildPostTrainingDialogue(activeSession);
        yield return StartCoroutine(PlayDialogueAndWait(postDialogue));

        ExitToMainScene();
    }

    private bool ResolveSceneDependencies()
    {
        if (dialogueUI == null)
            dialogueUI = NpcDialogueMenuController.Instance;

        if (dialogueUI == null)
            dialogueUI = FindFirstObjectByType<NpcDialogueMenuController>(FindObjectsInactive.Include);

        if (dialogueUI == null)
        {
            GameObject dialogueObj = new GameObject("NpcDialogueMenuController_Runtime");
            dialogueUI = dialogueObj.AddComponent<NpcDialogueMenuController>();
            Debug.LogWarning(FallbackLogPrefix + " Created runtime NpcDialogueMenuController in Gym scene.");
        }

        if (clockUI == null)
            clockUI = FindFirstObjectByType<ClockAnimationUI>();

        if (clockUI == null)
            clockUI = FindFirstObjectByType<ClockAnimationUI>(FindObjectsInactive.Include);

        if (clockUI == null)
        {
            GameObject clockObj = new GameObject("ClockAnimationUI");
            clockUI = clockObj.AddComponent<ClockAnimationUI>();
            Debug.LogWarning(FallbackLogPrefix + " Created runtime ClockAnimationUI in Gym scene.");
        }

        if (trainerDialogueController == null)
            trainerDialogueController = FindFirstObjectByType<GymTrainerDialogueController>();

        if (trainerDialogueController == null)
            trainerDialogueController = FindFirstObjectByType<GymTrainerDialogueController>(FindObjectsInactive.Include);

        if (trainerDialogueController == null)
        {
            GameObject trainerDialogueObj = new GameObject("GymTrainerDialogueController");
            trainerDialogueController = trainerDialogueObj.AddComponent<GymTrainerDialogueController>();
            Debug.LogWarning(FallbackLogPrefix + " Created runtime GymTrainerDialogueController in Gym scene.");
        }

        if (trainerInteractable == null && trainerInteractableOverride != null)
            trainerInteractable = trainerInteractableOverride;

        if (trainerInteractable == null)
        {
            GameObject trainerObj = GameObject.FindWithTag("NPCTrainer");
            if (trainerObj != null)
                trainerInteractable = trainerObj.GetComponent<NpcDialogueInteractable>();
        }

        if (trainerInteractable == null)
            trainerInteractable = FindFirstObjectByType<NpcDialogueInteractable>();

        if (trainerInteractable == null)
        {
            Debug.LogWarning(FallbackLogPrefix + " Trainer interactable missing in Gym scene.");
            return false;
        }

        return true;
    }

    private IEnumerator PlayDialogueAndWait(DialogueGraphData graph)
    {
        if (graph == null || dialogueUI == null || trainerInteractable == null)
            yield break;

        bool opened = dialogueUI.OpenDialogue(trainerInteractable, graph);
        if (!opened)
            yield break;

        while (dialogueUI != null && dialogueUI.IsOpen)
            yield return new WaitForSecondsRealtime(dialoguePollInterval);
    }

    private void ExitToMainScene()
    {
        SetGymSessionLock(false);

        SpawnPlayerManager.TargetSpawnID = "spawnpoint";

        if (FadeManager.Instance != null)
        {
            FadeManager.Instance.FadeToBlackAndLoad(mainSceneName, 0.5f);
        }
    }

    private void SetGymSessionLock(bool locked)
    {
        string key = string.IsNullOrWhiteSpace(gymSessionLockKey) ? "GymSession" : gymSessionLockKey;

        if (locked)
        {
            if (!gymModalOpened && ModalStateManager.Instance != null)
            {
                ModalStateManager.Instance.OpenModal(key);
                gymModalOpened = true;
                return;
            }

            if (!fallbackPlayerLockActive)
            {
                fallbackPlayerController = FindFirstObjectByType<PlayerController>();
                if (fallbackPlayerController != null)
                {
                    fallbackPlayerController.LockInput(key);
                    fallbackPlayerLockActive = true;
                }
            }

            return;
        }

        if (gymModalOpened && ModalStateManager.Instance != null)
            ModalStateManager.Instance.CloseModal(key);

        if (fallbackPlayerLockActive && fallbackPlayerController != null)
            fallbackPlayerController.UnlockInput(key);

        gymModalOpened = false;
        fallbackPlayerLockActive = false;
        fallbackPlayerController = null;
    }

    private void MovePlayerToSpawnPoint()
    {
        Transform spawnTransform = playerSpawnPointOverride;
        if (spawnTransform == null)
        {
            GameObject spawn = GameObject.Find("PlayerSpawnPoint");
            if (spawn != null)
                spawnTransform = spawn.transform;
        }

        if (spawnTransform == null)
        {
            Debug.LogWarning(FallbackLogPrefix + " Missing PlayerSpawnPoint in Gym scene.");
            return;
        }

        GameObject player = playerOverride;
        if (player == null)
            player = GameObject.FindWithTag("Player");

        if (player == null)
        {
            Debug.LogWarning(FallbackLogPrefix + " Missing Player in Gym scene when moving to spawn point.");
            return;
        }

        player.transform.position = spawnTransform.position;
        player.transform.rotation = spawnTransform.rotation;
    }
}
