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
    [SerializeField] private float faintTimeSkipDuration = 1.2f;
    [SerializeField] private int faintSkipHours = 1;

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

        if (activeSession.forceFaint)
        {
            yield return StartCoroutine(RunFaintFlow(progression));
            yield break;
        }

        SetGymSessionLock(true);

        DialogueGraphData preDialogue = trainerDialogueController.BuildPreTrainingDialogue(activeSession);
        yield return StartCoroutine(PlayDialogueAndWait(preDialogue));

        bool animationDone = false;
        float targetEndHour = activeSession.endHour;
        clockUI.PlayTimeSkipAnimation(
            "Sesi Latihan",
            activeSession.startHour,
            targetEndHour,
            trainingAnimationDuration,
            () => animationDone = true);

        while (!animationDone)
            yield return null;

        progression.ApplyResult(activeSession);
        GymProgressionSystem.SyncGameClockAfterGym(activeSession);
        PlayerStats.Instance?.RegisterHealthScore(10f);

        DialogueGraphData postDialogue = trainerDialogueController.BuildPostTrainingDialogue(activeSession);
        yield return StartCoroutine(PlayDialogueAndWait(postDialogue));

        ExitToMainScene();
    }

    private IEnumerator RunFaintFlow(GymProgressionSystem progression)
    {
        SetGymSessionLock(true);

        int startHour = activeSession.startHour;
        int endHour = startHour + Mathf.Max(1, faintSkipHours);

        bool animationDone = false;
        clockUI.PlayTimeSkipAnimation(
            "Pingsan saat latihan",
            startHour,
            endHour,
            faintTimeSkipDuration,
            () => animationDone = true);

        while (!animationDone)
            yield return null;

        if (TimeManager.Instance != null)
            TimeManager.Instance.SetTimeByHour(endHour);

        progression.CompleteFaintSession(activeSession);
        ExitToMainScene();
        yield break;
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
            clockUI = ClockAnimationUI.EnsureInstance();

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

        yield return SubSceneReturnHelper.WaitForDialogue(dialogueUI, dialoguePollInterval);
    }

    private void ExitToMainScene()
    {
        SetGymSessionLock(false);
        SubSceneReturnHelper.ReturnToSampleScene("gymdoor", mainSceneName);
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
        // Skip - player akan di-hide saja, posisi dihandle SpawnPlayerManager
        GameObject player = playerOverride;
        if (player == null)
            player = GameObject.FindWithTag("Player");

        if (player == null)
        {
            PlayerController pc = FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);
            if (pc != null) player = pc.gameObject;
        }

        if (player == null) return;

        player.SetActive(false);
    }
}
