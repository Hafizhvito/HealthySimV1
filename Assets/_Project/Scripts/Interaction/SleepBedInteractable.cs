using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SleepBedInteractable : MonoBehaviour, IInteractable
{
    [Header("Prompt")]
    [SerializeField] private string interactionText = "Tekan E untuk tidur";
    [SerializeField] private string blockedBeforeNightText = "Belum malam. Kamu belum bisa tidur sekarang.";
    [SerializeField] private string blockedRepeatedText = "Kamu tidak bisa tidur terus-menerus. Lanjutkan aktivitasmu dulu sampai malam.";
    [SerializeField] private string sleepingLogText = "Kamu tidur nyenyak. Hari berganti ke pagi.";

    [Header("Blocked Sleep Warning")]
    [SerializeField] private float blockedWarningCooldown = 0.8f;
    [SerializeField] private float blockedBurstWindow = 4f;
    [SerializeField] private int blockedBurstThreshold = 3;

    [Header("Sleep Confirm UX")]
    [SerializeField] private bool requireConfirmBeforeSleep = true;
    [SerializeField] private float confirmWindowDuration = 2.2f;
    [SerializeField] private float confirmPromptDuration = 1.8f;
    [SerializeField] private string confirmPromptText = "Tekan E sekali lagi untuk tidur.";
    [SerializeField] private string confirmPromptWithRiskTemplate = "Tekan E sekali lagi untuk tidur. Risiko tidur terganggu: {0}%";

    [Header("Sleep Recovery")]
    [SerializeField] [Range(0f, 1f)] private float fullRecoveryNormalized = 1f;
    [SerializeField] [Range(0f, 1f)] private float lowEnergyWarningThreshold = 0.35f;

    [Header("Next-Day Energy Sustainability")]
    [SerializeField] [Range(0.75f, 1.25f)] private float bestTrainingDrainModifier = 0.85f;
    [SerializeField] [Range(0.75f, 1.25f)] private float worstTrainingDrainModifier = 1.15f;

    [Header("Sleep Disturbance Chance")]
    [SerializeField] private bool enableSleepDisturbance = true;
    [SerializeField] [Range(0f, 1f)] private float baseDisturbChance = 0.05f;
    [SerializeField] [Range(0f, 1f)] private float chanceIfNoWork = 0.20f;
    [SerializeField] [Range(0f, 1f)] private float chanceIfOverwork = 0.25f;
    [SerializeField] [Range(0f, 1f)] private float chanceIfPoorDiet = 0.15f;
    [SerializeField] [Range(1f, 3f)] private float disturbanceMultiplier = 1.6f;
    [SerializeField] [Range(0f, 1f)] private float disturbedRecoveryNormalized = 0.65f;
    [SerializeField] private int poorDietThresholdDelta = 2;

    [Header("Late Wake Timing")]
    [SerializeField] [Range(5, 11)] private int normalWakeHour = 7;
    [SerializeField] [Range(6, 12)] private int disturbedWakeHour = 8;
    [SerializeField] [Range(7, 13)] private int lateWakeHour = 9;

    [Header("Transition")]
    [SerializeField] private bool requireNightToSleep = true;
    [SerializeField] private float clockSkipDuration = 2.8f;
    [SerializeField] private float fadeDuration = 0.35f;
    [SerializeField] private string sleepLockSource = "SleepTransition";

    [Header("Pre-Sleep Cinematic")]
    [SerializeField] private bool enablePreSleepCinematic = true;
    [SerializeField] private float eyeCloseDuration = 0.95f;
    [SerializeField] private float warmLineHoldDuration = 0.9f;
    [SerializeField] private string[] warmSleepLines = new string[]
    {
        "Hari ini mungkin belum sempurna, tapi tubuhmu tetap layak dipeluk oleh istirahat.",
        "Kamu sudah berjuang seharian. Sekarang biarkan malam menyelesaikan sisanya.",
        "Tarik napas pelan. Besok bukan untuk menebus, tapi untuk memulai lagi.",
        "Tidak apa-apa melambat. Tubuhmu bukan mesin, ia teman seperjalanan.",
        "Kadang yang paling sehat bukan berlari lebih jauh, tapi berhenti tepat waktu."
    };

    [Header("Wake Eye-Open Cinematic")]
    [SerializeField] private bool enableWakeEyeOpenCinematic = true;
    [SerializeField] private float eyeOpenDuration = 1f;
    [SerializeField] private float wakeLineHoldDuration = 0.5f;
    [SerializeField] private string[] warmWakeLines = new string[]
    {
        "Pelan-pelan buka mata. Hari ini masih memberi ruang untuk memulai dengan lembut.",
        "Pagi datang tanpa menuntutmu sempurna. Cukup hadir, lalu lanjut satu langkah.",
        "Napasmu sudah kembali tenang. Sekarang giliran ritmemu kembali seimbang.",
        "Tubuhmu sudah bekerja semalaman untuk pulih. Dengarkan dia hari ini.",
        "Cahaya pagi selalu baru. Kamu juga boleh mulai dari versi baru dirimu."
    };

    [Header("Wake Reminder")]
    [SerializeField] private float wakeMessageDuration = 3f;
    [SerializeField] private string wakeIntroTemplate = "Kamu bangun di Hari {0}.";
    [SerializeField] private string wakeWarningNoWork = "Peringatan: Kemarin kamu belum kerja. Atur ritme harimu lebih baik.";
    [SerializeField] private string wakeWarningNoGym = "Peringatan: Kemarin kamu belum ke gym. Tubuh butuh gerak rutin.";
    [SerializeField] private string wakeWarningLowEnergy = "Peringatan: Kemarin kamu tidur saat energi sangat rendah.";
    [SerializeField] private string wakeWarningDisturbedSleep = "Peringatan: Tidurmu kurang nyenyak, jadi energimu belum pulih penuh.";

    [Header("Aging Transition")]
    //[SerializeField] private float agingNotificationDuration = 3.2f;
    [SerializeField] private string agingNotificationTemplate = "Tubuhmu memasuki fase {0} pada Hari {1}.";
    [SerializeField] private string agingNotificationDetailTemplate = "Transisi dari fase {0} ke {1}.";

    [Header("Late Wake Penalty (Energy + Narrative Only)")]
    [SerializeField] private bool enableLateWakePenalty = true;
    [SerializeField] [Range(0f, 1f)] private float sugarSignalWeight = 0.15f;
    [SerializeField] [Range(0f, 1f)] private float overworkSignalWeight = 0.20f;
    [SerializeField] [Range(0f, 1f)] private float fatigueSignalWeight = 0.15f;
    [SerializeField] [Range(0f, 1f)] private float lowEnergySignalWeight = 0.25f;
    [SerializeField] [Range(0f, 1f)] private float lateWakeChanceCap = 0.75f;
    [SerializeField] [Range(0f, 100f)] private float lateWakePenaltyAmount = 15f;
    [SerializeField] [Range(0f, 1f)] private float lowEnergySleepPenaltyThreshold = 0.20f;
    [SerializeField] [Range(0f, 100f)] private float highFatigueDebtThreshold = 60f;
    [SerializeField] [Range(0f, 200f)] private float highSugarEstimateThreshold = 45f;
    [SerializeField] private string wakeWarningLateWakePenalty = "Kamu bangun dengan badan terasa berat. Pola hidupmu mulai berdampak...";

    [Header("Wake Spawn")]
    [Tooltip("Optional marker child. Wake position is computed beside the bed at runtime.")]
    [SerializeField] private Transform bedSpawnPoint;
    [SerializeField] [Range(0.2f, 1.5f)] private float besideBedStandDistance = 0.55f;

    [Header("Optional References")]
    [SerializeField] private TimeManager timeManagerOverride;
    [SerializeField] private PlayerStats playerStatsOverride;
    [SerializeField] private FadeManager fadeManagerOverride;
    [SerializeField] private WorkSessionManager workSessionManagerOverride;
    [SerializeField] private GymProgressionSystem gymProgressionSystemOverride;
    [SerializeField] private ClockAnimationUI clockAnimationUiOverride;

    private Collider cachedCollider;
    private Coroutine sleepRoutine;
    private bool isSleepInProgress;
    private const float AsyncWaitTimeoutSeconds = 10f;
    private Canvas wakeCanvas;
    private CanvasGroup wakeCanvasGroup;
    private RectTransform wakePanelRect;
    private TextMeshProUGUI wakeIntroText;
    private TextMeshProUGUI wakeWarningsText;
    private TextMeshProUGUI wakeSummaryText;
    private Coroutine wakeMessageRoutine;
    private Canvas sleepCinematicCanvas;
    private CanvasGroup sleepCinematicCanvasGroup;
    private TextMeshProUGUI sleepCinematicText;
    private RectTransform agingPanelRoot;
    private CanvasGroup agingPanelGroup;
    private TextMeshProUGUI agingPanelTitleText;
    private TextMeshProUGUI agingPanelBodyText;
    private Button agingPanelContinueButton;
    private float lastBlockedWarningTime = -999f;
    private float blockedWindowStartTime = -999f;
    private int blockedClickCount;
    private float confirmExpiresAt = -999f;
    private bool wasForced;
    private bool forceSleepTriggered;
    private const string AgingNotificationModalKey = "aging_panel";
    private const string ForcedSleepMessage = "Kamu terlalu lelah dan tertidur...";

    /// <summary>Only one bed may run a sleep session at a time (scene can have bedSingle + legacy Interactable_Bed).</summary>
    private static SleepBedInteractable sleepSessionOwner;

    private struct WakeMessageContent
    {
        public string intro;
        public List<string> warnings;
        public string summary;
    }

    private const float WakePanelWidth = 920f;
    private const float WakePanelMinHeight = 104f;
    private const float WakePanelMaxHeight = 320f;
    private const float WakeIntroFontSize = 26f;
    private const float WakeWarningsFontSize = 21f;
    private const float WakeSummaryFontSize = 19f;
    private const float SleepCinematicFontSize = 34f;

    private void Awake()
    {
        cachedCollider = GetComponent<Collider>();
        if (cachedCollider == null)
            cachedCollider = GetComponentInChildren<Collider>();

        ResolveAgingPanelFromScene();
    }

    private void OnEnable()
    {
        InteractableRegistry.Register(this, cachedCollider, transform);
    }

    private void OnDisable()
    {
        if (isSleepInProgress || sleepRoutine != null)
            ResetSleepState("on_disable");

        InteractableRegistry.Unregister(this);
    }

    private void OnApplicationPause(bool paused)
    {
#if UNITY_EDITOR
        // Alt-tab in Editor fires application_pause and used to abort sleep mid-fade (stuck black screen).
        return;
#else
        if (paused && (isSleepInProgress || sleepRoutine != null))
            ResetSleepState("application_pause");
#endif
    }

    private void Update()
    {
        if (IsAnotherBedSleeping(this))
            return;

        TryForceSleep();
    }

    public string GetInteractionText()
    {
        if (!CanSleepNow())
            return blockedBeforeNightText;

        if (HasActiveSleepConfirm())
            return confirmPromptText;

        return interactionText;
    }

    public bool CanInteract(GameObject interactor)
    {
        if (IsAnotherBedSleeping(this))
            return false;

        return !isSleepInProgress;
    }

    public void Interact(GameObject interactor)
    {
        if (isSleepInProgress)
            return;

        if (!CanSleepNow())
        {
            ResetSleepConfirm();
            HandleBlockedSleepAttempt();
            return;
        }

        if (ShouldRequestSleepConfirm())
        {
            RequestSleepConfirm();
            return;
        }

        ResetSleepConfirm();
        BeginSleepRoutine(interactor, false);
    }

    private void BeginSleepRoutine(GameObject interactor, bool forcedSleep)
    {
        if (isSleepInProgress || sleepSessionOwner != null)
            return;

        sleepSessionOwner = this;
        isSleepInProgress = true;
        sleepRoutine = StartCoroutine(SleepRoutine(interactor, forcedSleep));
    }

    private static bool IsAnotherBedSleeping(SleepBedInteractable self)
    {
        return sleepSessionOwner != null && sleepSessionOwner != self;
    }

    private void ReleaseSleepSessionOwnership()
    {
        if (sleepSessionOwner == this)
            sleepSessionOwner = null;
    }

    private void ResetSleepState(string reason)
    {
        if (sleepSessionOwner != null && sleepSessionOwner != this)
            return;

        bool wasActive = isSleepInProgress || sleepRoutine != null;

        if (sleepRoutine != null)
        {
            StopCoroutine(sleepRoutine);
            sleepRoutine = null;
        }

        if (wasActive)
        {
            PlayerController player = FindFirstObjectByType<PlayerController>();
            RestoreGameplayAfterSleep(player, ResolvePlayerStats());

            FadeManager fadeManager = FadeManager.Instance;
            if (fadeManager != null)
                fadeManager.ReleaseInputBlock();

            if (!string.IsNullOrEmpty(reason))
                Debug.LogWarning($"[SleepBed] Sleep state reset ({reason}).");
        }

        forceSleepTriggered = false;
        isSleepInProgress = false;
        ReleaseSleepSessionOwnership();
    }

    private IEnumerator WaitUntilOrTimeout(System.Func<bool> predicate, float timeoutSeconds, string label)
    {
        float elapsed = 0f;
        while (!predicate() && elapsed < timeoutSeconds)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        if (!predicate())
            Debug.LogWarning($"[SleepBed] Timeout waiting for {label} after {timeoutSeconds:0}s — continuing.");
    }

    private IEnumerator SleepRoutine(GameObject interactor, bool forcedSleep)
    {
        interactor = ResolveInteractorRoot(interactor);

        TimeManager         timeManager         = ResolveTimeManager();
        PlayerStats         playerStats         = ResolvePlayerStats();
        FadeManager         fadeManager         = ResolveFadeManager();
        WorkSessionManager  workSessionManager  = ResolveWorkSessionManager();
        GymProgressionSystem gymProgressionSystem = ResolveGymProgressionSystem();
        ClockAnimationUI    clockUi             = ResolveClockAnimationUi();
        PlayerController    playerController    = interactor != null ? interactor.GetComponent<PlayerController>() : null;

        if (timeManager == null || playerStats == null)
        {
            Debug.LogWarning("[SleepBedInteractable] Sleep dibatalkan karena TimeManager/PlayerStats tidak tersedia.");
            yield break;
        }

        TimeManager.TimePeriod periodBeforeSleep = timeManager.CurrentPeriod;
        if (!forcedSleep && requireNightToSleep && periodBeforeSleep != TimeManager.TimePeriod.Night)
        {
            ShowWakeMessage(blockedBeforeNightText, 1.8f);
            yield break;
        }

        // ── Snapshot data sebelum tidur ──────────────────────
        bool workedYesterday   = workSessionManager != null && workSessionManager.HasWorkedToday;
        bool trainedYesterday  = GymProgressionSystem.DidTrainToday(gymProgressionSystem, playerStats);
        GymSessionData lastGymAtSleepStart = gymProgressionSystem != null ? gymProgressionSystem.LastSession : null;
        WorkSessionData lastWorkAtSleepStart = workSessionManager != null ? workSessionManager.LastSession : null;
        bool lastWorkBonusAtSleepStart = workSessionManager != null && workSessionManager.LastSessionHadBonus;
        float energyBeforeSleep     = playerStats.EnergyPercent;
        float adaptationBeforeSleep = playerStats.TrainingAdaptation;
        float fatigueBeforeSleep    = playerStats.FatigueDebt;

        bool  overworkedYesterday   = workedYesterday && DidOverworkYesterday(workSessionManager);
        bool  poorDietYesterday     = HasPoorDietPattern();
        bool  disturbedSleep        = RollSleepDisturbance(workedYesterday, overworkedYesterday, poorDietYesterday);
        float lateWakeChance        = CalculateLateWakeChance(workSessionManager, gymProgressionSystem, playerStats, energyBeforeSleep);
        bool  lateWakePenaltyTriggered = enableLateWakePenalty && UnityEngine.Random.value < lateWakeChance;

        if (forcedSleep)
            wasForced = true;

        bool sleepInputLocked = false;
        bool prematureDeathOccurred = false;
        bool skipInputRestore = false;
        int triggerDay = EndingManager.Instance != null
            ? EndingManager.Instance.EndingTriggerDay
            : LifestyleMortalityEvaluator.DefaultEndingDay;
        int dayBeforeAdvance = timeManager.CurrentDayNumber;
        bool isFinalDaySleep = dayBeforeAdvance >= triggerDay;
        LifestyleMortalityEvaluator.MortalityAssessment mortalityAssessment = default;

        try
        {
            ResetGameplayStateBeforeSleep(playerController);

            if (playerController != null)
            {
                playerController.LockInput(sleepLockSource);
                sleepInputLocked = true;
            }

            bool alreadyFaded = false;
            if (forcedSleep && fadeManager != null)
            {
                ShowWakeMessage(ForcedSleepMessage, Mathf.Max(1f, fadeDuration + 0.4f));
                bool fadeToBlackDone = false;
                fadeManager.FadeToBlack(fadeDuration, () => fadeToBlackDone = true);
                yield return WaitUntilOrTimeout(() => fadeToBlackDone, AsyncWaitTimeoutSeconds, "forced_fade_to_black");
                alreadyFaded = true;
            }

            if (!forcedSleep && enablePreSleepCinematic)
                yield return StartCoroutine(PlayPreSleepCinematic());

            if (fadeManager != null && !alreadyFaded)
            {
                bool fadeToBlackDone = false;
                fadeManager.FadeToBlack(fadeDuration, () => fadeToBlackDone = true);
                yield return WaitUntilOrTimeout(() => fadeToBlackDone, AsyncWaitTimeoutSeconds, "fade_to_black");
            }

            HidePreSleepCinematic();

            int wakeHour = ResolveWakeHour(disturbedSleep, lateWakePenaltyTriggered);
            float clockStartHour = forcedSleep
                ? Mathf.Clamp(timeManager.CurrentHour, 21f, 24f)
                : 21f;

            if (clockUi != null)
            {
                bool clockDone = false;
                clockUi.PlayTimeSkipAnimation("Istirahat Malam", clockStartHour, wakeHour, clockSkipDuration, () => clockDone = true);
                yield return WaitUntilOrTimeout(() => clockDone, AsyncWaitTimeoutSeconds, "sleep_clock_animation");
            }

            // ── Apply recovery + evaluate health score ───────────
            DailyHealthResult dailyEvalResult = null;
            ApplyRecovery(playerStats, disturbedSleep, energyBeforeSleep,
                        workedYesterday, lastWorkAtSleepStart, lastWorkBonusAtSleepStart,
                        trainedYesterday, lastGymAtSleepStart, overworkedYesterday, out dailyEvalResult);

            float scoreForPhaseTransition = playerStats.HealthScoreThisPhase;
            PlayerStats.PhaseSnapshot snapshotForPhaseTransition = playerStats.GetCurrentPhaseSnapshot();

            mortalityAssessment = LifestyleMortalityEvaluator.Assess(
                dayBeforeAdvance, triggerDay, playerStats, PlayerActionTracker.Instance);

            if (!isFinalDaySleep
                && mortalityAssessment.RollChance > 0f
                && LifestyleMortalityEvaluator.RollMortality(mortalityAssessment.RollChance))
            {
                prematureDeathOccurred = true;
                yield return StartCoroutine(HandlePrematureDeathDuringSleep(
                    playerController, dayBeforeAdvance, mortalityAssessment));
                yield break;
            }

            // ── Advance day ──────────────────────────────────────
            timeManager.AdvanceToNextDayFromSleep();
            Debug.Log($"[SleepBed] Hari berganti: {dayBeforeAdvance} → {timeManager.CurrentDayNumber} ({timeManager.GetDayNameIndonesia()}), forced={forcedSleep}");
            if (BazaarManager.Instance != null)
                BazaarManager.Instance.TrySpawnBazaar(timeManager.CurrentDayNumber);
            timeManager.SetTimeByHour(wakeHour);
            string     wakeDayName    = timeManager.GetDayNameIndonesia();
            bool ageStageChanged = playerStats.SyncDayAndTryAdvanceAgeStage(
                timeManager.CurrentDayNumber,
                out PlayerStats.AgeStage previousAgeStage,
                out PlayerStats.AgeStage newAgeStage);

            if (workSessionManager != null)
                workSessionManager.NotifyDayResetFromSleep();

            if (gymProgressionSystem != null)
                gymProgressionSystem.NotifyDayResetFromSleep();

            playerStats.ResetDailyHospitalVisitForNewDay();
            HealthAlertPanelController.EnsureInstance();

            playerStats.ClearPostActivityTravelGrace();

            float baseDrainModifier = ApplyNextDayMovementDrainModifier(playerStats, trainedYesterday, adaptationBeforeSleep, fatigueBeforeSleep);

            if (wasForced)
                ApplyBegadangPenalty(playerStats, baseDrainModifier);

            if (lateWakePenaltyTriggered)
                ApplyLateWakePenalty(playerStats);

            if (isFinalDaySleep)
            {
                skipInputRestore = true;
                MoveInteractorToBedSpawn(interactor);
                if (fadeManager != null)
                    fadeManager.ReleaseInputBlock();
                if (EndingManager.Instance != null)
                    EndingManager.Instance.TriggerEnding();
                else
                    Debug.LogWarning("[SleepBedInteractable] EndingManager.Instance null — ending not triggered.");
                yield break;
            }

            MoveInteractorToBedSpawn(interactor);
            Debug.Log($"[SleepBedInteractable] {sleepingLogText} Hari {wakeDayName}.");

            if (enableWakeEyeOpenCinematic)
                PrepareWakeEyeOpenCinematic(wakeDayName);

            if (fadeManager != null)
            {
                bool fadeFromBlackDone = false;
                fadeManager.FadeFromBlack(fadeDuration, () => fadeFromBlackDone = true);
                yield return WaitUntilOrTimeout(() => fadeFromBlackDone, AsyncWaitTimeoutSeconds, "fade_from_black");
            }

            if (enableWakeEyeOpenCinematic)
                yield return StartCoroutine(PlayWakeEyeOpenCinematic(wakeDayName));

            RestoreGameplayAfterSleep(playerController, playerStats);
            sleepInputLocked = false;

            if (ageStageChanged)
            {
                yield return StartCoroutine(ShowAgingNotificationRoutine(
                    previousAgeStage, newAgeStage, scoreForPhaseTransition,
                    snapshotForPhaseTransition, playerStats.PlayerGender));
            }

            string mortalityWarning = mortalityAssessment.IsWarningZone && !string.IsNullOrEmpty(mortalityAssessment.WarningText)
                ? mortalityAssessment.WarningText
                : null;

            ShowWakeMessage(
                BuildWakeMessage(wakeDayName, workedYesterday, trainedYesterday, energyBeforeSleep,
                                 disturbedSleep, lateWakePenaltyTriggered, dailyEvalResult,
                                 mortalityWarning),
                wakeMessageDuration);

            EvaluateCharacterModelSwap(interactor);
        }
        finally
        {
            if (sleepInputLocked && !prematureDeathOccurred && !skipInputRestore)
                RestoreGameplayAfterSleep(playerController, playerStats);

            forceSleepTriggered = false;
            sleepRoutine = null;
            isSleepInProgress = false;
            ReleaseSleepSessionOwnership();
        }
    }

    private static void ResetGameplayStateBeforeSleep(PlayerController playerController)
    {
        if (ModalStateManager.Instance != null)
            ModalStateManager.Instance.ForceResetAllModals();
        else if (playerController != null)
            playerController.ForceResetLock();

        Time.timeScale = 1f;

        FaintNotificationController faintPanel =
            FindFirstObjectByType<FaintNotificationController>(FindObjectsInactive.Include);
        faintPanel?.DismissForSleepWake();

        if (FadeManager.Instance != null)
            FadeManager.Instance.ReleaseInputBlock();
    }

    private static void RestoreGameplayAfterSleep(PlayerController playerController, PlayerStats playerStats)
    {
        Time.timeScale = 1f;

        if (playerStats != null)
            playerStats.ResetFaintState();

        EnergySystem energySystem =
            FindFirstObjectByType<EnergySystem>(FindObjectsInactive.Include);
        energySystem?.CompleteFaintRecovery();

        FaintNotificationController faintPanel =
            FindFirstObjectByType<FaintNotificationController>(FindObjectsInactive.Include);
        faintPanel?.DismissForSleepWake();

        if (FadeManager.Instance != null)
            FadeManager.Instance.ReleaseInputBlock();

        if (playerController != null)
        {
            playerController.ForceUnlockInput("SleepTransition");
            playerController.ForceUnlockInput(EnergySystem.FaintLockKey);
        }
    }

    private void EvaluateCharacterModelSwap(GameObject interactor)
    {
        if (interactor == null)
            return;

        CharacterModelSwapper swapper = interactor.GetComponent<CharacterModelSwapper>();
        swapper?.EvaluateAndSwap();
    }

    // ── ApplyRecovery — INTI PERUBAHAN ───────────────────────
    private void ApplyRecovery(
        PlayerStats stats,
        bool disturbedSleep,
        float energyBeforeSleep,
        bool workedYesterday,
        WorkSessionData lastWorkSession,
        bool lastWorkHadBonus,
        bool trainedYesterday,
        GymSessionData lastGymSession,
        bool overworkedYesterday,
        out DailyHealthResult evalResult)
    {
        // 1. Energy recovery
        float targetNormalized = Mathf.Clamp01(fullRecoveryNormalized);
        if (disturbedSleep)
            targetNormalized = Mathf.Min(targetNormalized, Mathf.Clamp01(disturbedRecoveryNormalized));

        float targetEnergy = stats.MaxEnergy * targetNormalized;
        float recovery     = Mathf.Max(0f, targetEnergy - stats.CurrentEnergy);
        if (recovery > 0.01f)
            stats.AddFood(recovery, 0f, 0f, 0f, 0f);

        stats.ResetFaintState();

        // 2. Evaluasi harian dengan snapshot — bukan baca langsung dari manager
        evalResult = DailyHealthEvaluator.Evaluate(
            disturbedSleep,
            energyBeforeSleep,
            workedYesterday,
            lastWorkSession,
            lastWorkHadBonus,
            trainedYesterday,
            lastGymSession,
            PlayerActionTracker.Instance,
            stats
        );

        if (stats != null)
        {
            float calorieRatio = 0f;
            if (stats.DailyCalorieTarget > 0f)
                calorieRatio = stats.TotalCalories / stats.DailyCalorieTarget;

            stats.RegisterDailyHealthSnapshot(
                evalResult,
                disturbedSleep,
                energyBeforeSleep,
                workedYesterday,
                trainedYesterday,
                overworkedYesterday,
                calorieRatio);
        }

        stats.RegisterHealthScore(evalResult.totalDelta);

        ApplyDailyWeightShift(stats, evalResult, disturbedSleep, trainedYesterday, workedYesterday, overworkedYesterday);

        Debug.Log($"[SleepBed] {evalResult}");
        Debug.Log($"[SleepBed] diet='{evalResult.dietNote}' gym='{evalResult.gymNote}' work='{evalResult.workNote}' trained={trainedYesterday}");

        // 3. Reset food tracker dan kalori harian
        if (PlayerActionTracker.Instance != null)
            PlayerActionTracker.Instance.ResetDailyFoodCounts();

        stats.ResetDailyCalories();
    }

    private void ApplyDailyWeightShift(
        PlayerStats stats,
        DailyHealthResult evalResult,
        bool disturbedSleep,
        bool trainedYesterday,
        bool workedYesterday,
        bool overworkedYesterday)
    {
        if (stats == null)
            return;

        float calorieRatio = stats.DailyCalorieTarget > 0f
            ? stats.TotalCalories / stats.DailyCalorieTarget
            : 0f;

        int healthy = evalResult != null ? evalResult.healthyFoodCount : 0;
        int unhealthy = evalResult != null ? evalResult.unhealthyFoodCount : 0;
        int totalFood = healthy + unhealthy;

        float delta = 0f;
        string reason = string.Empty;

        if (totalFood == 0)
        {
            delta -= 0.2f;
            reason += "no_food ";
        }

        if (calorieRatio > 1.15f)
        {
            delta += 0.25f;
            reason += "high_calorie ";
        }
        else if (calorieRatio > 0f && calorieRatio < 0.75f)
        {
            delta -= 0.2f;
            reason += "low_calorie ";
        }

        if (unhealthy > healthy)
        {
            delta += 0.15f;
            reason += "unhealthy_bias ";
        }

        if (disturbedSleep)
        {
            delta += 0.1f;
            reason += "disturbed_sleep ";
        }

        if (!trainedYesterday)
        {
            delta += 0.05f;
            reason += "no_gym ";
        }

        if (!workedYesterday || overworkedYesterday)
        {
            delta += 0.05f;
            reason += "work_stress ";
        }

        delta = Mathf.Clamp(delta, -0.35f, 0.35f);

        if (Mathf.Abs(delta) > 0.001f)
            stats.AdjustWeight(delta, reason.Trim());
    }

    private float ApplyNextDayMovementDrainModifier(PlayerStats stats, bool trainedYesterday, float adaptationBeforeSleep, float fatigueBeforeSleep)
    {
        if (stats == null)
            return 1f;

        float nextModifier = 1f;
        if (trainedYesterday)
        {
            float adaptationNorm = Mathf.Clamp01(adaptationBeforeSleep / 100f);
            float fatigueNorm    = Mathf.Clamp01(fatigueBeforeSleep / 100f);
            float balance        = Mathf.Clamp(adaptationNorm - fatigueNorm, -1f, 1f);
            float blend          = Mathf.Clamp01((balance + 1f) * 0.5f);

            float best  = Mathf.Min(1f, bestTrainingDrainModifier);
            float worst = Mathf.Max(1f, worstTrainingDrainModifier);
            nextModifier = Mathf.Lerp(worst, best, blend);
        }

        stats.SetMovementDrainModifier(nextModifier);
        return nextModifier;
    }

    private void ApplyBegadangPenalty(PlayerStats stats, float baseDrainModifier)
    {
        if (stats == null)
            return;

        float targetEnergy = stats.MaxEnergy * 0.6f;
        float delta = targetEnergy - stats.CurrentEnergy;
        if (Mathf.Abs(delta) > 0.01f)
            stats.AddFood(delta, 0f, 0f, 0f, 0f);

        stats.SetMovementDrainModifier(baseDrainModifier * 1.3f);
        Debug.Log("[Sleep] Begadang penalty applied");
        wasForced = false;
    }

    private void TryForceSleep()
    {
        if (sleepSessionOwner != null || forceSleepTriggered || isSleepInProgress)
            return;

        TimeManager timeManager = ResolveTimeManager();
        if (timeManager == null)
            return;

        if (timeManager.CurrentHour < 24f)
            return;

        GameObject interactor = ResolveInteractorRoot();
        if (interactor == null)
            return;

        forceSleepTriggered = true;
        BeginSleepRoutine(interactor, true);
    }

    private IEnumerator HandlePrematureDeathDuringSleep(
        PlayerController playerController,
        int dayNumber,
        LifestyleMortalityEvaluator.MortalityAssessment mortality)
    {
        HidePreSleepCinematic();

        if (EndingManager.Instance != null)
        {
            EndingManager.Instance.TriggerPrematureDeath(mortality, dayNumber);
            yield return new WaitUntil(() => !EndingManager.Instance.IsShowing);
        }
        else
        {
            Debug.LogWarning("[SleepBedInteractable] EndingManager.Instance null — premature death not shown.");
        }
    }

    private static GameObject ResolveInteractorRoot(GameObject hint = null)
    {
        if (hint != null)
            return hint;

        PlayerController playerController =
            FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);
        return playerController != null ? playerController.gameObject : null;
    }

    private void MoveInteractorToBedSpawn(GameObject interactor)
    {
        interactor = ResolveInteractorRoot(interactor);
        if (interactor == null)
        {
            Debug.LogWarning("[SleepBedInteractable] Wake spawn skipped — player not found.");
            return;
        }

        ResolveBesideBedSpawnPose(out Vector3 position, out Quaternion rotation);

        Rigidbody rb = interactor.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.position = position;
            rb.rotation = rotation;
            Physics.SyncTransforms();
        }
        else
        {
            interactor.transform.SetPositionAndRotation(position, rotation);
        }

        Transform spawnMarker = bedSpawnPoint != null ? bedSpawnPoint : transform.Find("BedSpawnPoint");
        if (spawnMarker != null)
            spawnMarker.SetPositionAndRotation(position, rotation);
    }

    private void ResolveBesideBedSpawnPose(out Vector3 position, out Quaternion rotation)
    {
        Collider bedCollider = cachedCollider;
        if (bedCollider == null)
            bedCollider = GetComponentInChildren<Collider>();

        if (bedCollider == null)
        {
            position = transform.position;
            rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
            return;
        }

        Bounds bounds = bedCollider.bounds;
        Vector3 side = Vector3.ProjectOnPlane(transform.right, Vector3.up);
        if (side.sqrMagnitude < 0.0001f)
            side = Vector3.right;
        else
            side.Normalize();

        float lateralExtent = Mathf.Max(bounds.extents.x, bounds.extents.z);
        position = bounds.center + side * (lateralExtent + besideBedStandDistance);
        position.y = bounds.min.y + 0.05f;

        Vector3 towardBed = bounds.center - position;
        towardBed.y = 0f;
        rotation = towardBed.sqrMagnitude > 0.0001f
            ? Quaternion.LookRotation(towardBed.normalized, Vector3.up)
            : Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
    }

    // ── Resolvers ────────────────────────────────────────────
    private TimeManager ResolveTimeManager()
    {
        if (timeManagerOverride != null) return timeManagerOverride;
        return TimeManager.Instance;
    }

    private PlayerStats ResolvePlayerStats()
    {
        if (playerStatsOverride != null) return playerStatsOverride;
        return PlayerStats.Instance;
    }

    private FadeManager ResolveFadeManager()
    {
        if (fadeManagerOverride != null) return fadeManagerOverride;
        if (FadeManager.Instance != null) return FadeManager.Instance;
        GameObject managerObj = GameObject.Find("GameManager");
        return managerObj != null ? managerObj.GetComponent<FadeManager>() : null;
    }

    private ClockAnimationUI ResolveClockAnimationUi()
    {
        if (clockAnimationUiOverride != null) return clockAnimationUiOverride;
        ClockAnimationUI existing = FindFirstObjectByType<ClockAnimationUI>();
        if (existing != null) return existing;
        existing = FindFirstObjectByType<ClockAnimationUI>(FindObjectsInactive.Include);
        if (existing != null) return existing;
        GameObject clockObj = new GameObject("ClockAnimationUI");
        return clockObj.AddComponent<ClockAnimationUI>();
    }

    private WorkSessionManager ResolveWorkSessionManager()
    {
        if (workSessionManagerOverride != null) return workSessionManagerOverride;
        if (WorkSessionManager.Instance != null) return WorkSessionManager.Instance;
        GameObject managerObj = GameObject.Find("GameManager");
        return managerObj != null ? managerObj.GetComponent<WorkSessionManager>() : null;
    }

    private GymProgressionSystem ResolveGymProgressionSystem()
    {
        if (gymProgressionSystemOverride != null) return gymProgressionSystemOverride;
        if (GymProgressionSystem.Instance != null) return GymProgressionSystem.Instance;
        GameObject managerObj = GameObject.Find("GameManager");
        if (managerObj == null) return null;
        GymProgressionSystem existing = managerObj.GetComponent<GymProgressionSystem>();
        return existing != null ? existing : managerObj.AddComponent<GymProgressionSystem>();
    }

    // ── Sleep logic helpers ──────────────────────────────────
    private bool CanSleepNow()
    {
        TimeManager timeManager = ResolveTimeManager();
        if (timeManager == null) return false;
        if (!requireNightToSleep) return true;
        return timeManager.CurrentPeriod == TimeManager.TimePeriod.Night;
    }

    private void HandleBlockedSleepAttempt()
    {
        float now = Time.unscaledTime;
        if (now - blockedWindowStartTime > Mathf.Max(0.5f, blockedBurstWindow))
        {
            blockedWindowStartTime = now;
            blockedClickCount = 0;
        }

        blockedClickCount++;

        if (now - lastBlockedWarningTime < Mathf.Max(0.1f, blockedWarningCooldown))
            return;

        lastBlockedWarningTime = now;
        bool repeated = blockedClickCount >= Mathf.Max(2, blockedBurstThreshold);
        string message  = repeated ? blockedRepeatedText : blockedBeforeNightText;
        float  duration = repeated ? 2.4f : 1.8f;
        ShowWakeMessage(message, duration);
    }

    private bool DidOverworkYesterday(WorkSessionManager workSessionManager)
    {
        if (workSessionManager == null || workSessionManager.LastSession == null)
            return false;

        WorkSessionData session = workSessionManager.LastSession;
        bool lowStartEnergy          = session.energyAtStart <= 0.25f;
        bool heavyDrainAtLowReserve  = session.energyConsumed >= 0.32f && session.energyAtStart <= 0.50f;
        return session.result != WorkResult.Full || session.performanceDropped || lowStartEnergy || heavyDrainAtLowReserve;
    }

    private bool ShouldRequestSleepConfirm()
    {
        if (!requireConfirmBeforeSleep) return false;
        return !HasActiveSleepConfirm();
    }

    private bool HasActiveSleepConfirm() => Time.unscaledTime <= confirmExpiresAt;

    private void RequestSleepConfirm()
    {
        confirmExpiresAt = Time.unscaledTime + Mathf.Max(0.5f, confirmWindowDuration);

        string prompt  = confirmPromptText;
        float  chance  = BuildCurrentDisturbanceChancePreview();
        if (enableSleepDisturbance)
        {
            int percentage = Mathf.RoundToInt(chance * 100f);
            prompt = string.Format(confirmPromptWithRiskTemplate, percentage);
        }

        ShowWakeMessage(prompt, Mathf.Max(0.5f, confirmPromptDuration));
    }

    private void ResetSleepConfirm() => confirmExpiresAt = -999f;

    private bool HasPoorDietPattern()
    {
        if (PlayerActionTracker.Instance == null) return false;
        int healthy   = PlayerActionTracker.Instance.GetCount(PlayerActionTracker.ActionType.HealthyFoodTaken);
        int unhealthy = PlayerActionTracker.Instance.GetCount(PlayerActionTracker.ActionType.UnhealthyFoodTaken);
        return unhealthy - healthy >= Mathf.Max(1, poorDietThresholdDelta);
    }

    private bool RollSleepDisturbance(bool workedYesterday, bool overworkedYesterday, bool poorDietYesterday)
    {
        if (!enableSleepDisturbance) return false;
        float chance = CalculateSleepDisturbanceChance(workedYesterday, overworkedYesterday, poorDietYesterday);
        return chance > 0f && UnityEngine.Random.value < chance;
    }

    private float BuildCurrentDisturbanceChancePreview()
    {
        WorkSessionManager workSessionManager = ResolveWorkSessionManager();
        bool workedYesterday    = workSessionManager != null && workSessionManager.HasWorkedToday;
        bool overworkedYesterday = workedYesterday && DidOverworkYesterday(workSessionManager);
        bool poorDietYesterday  = HasPoorDietPattern();
        return CalculateSleepDisturbanceChance(workedYesterday, overworkedYesterday, poorDietYesterday);
    }

    private float CalculateSleepDisturbanceChance(bool workedYesterday, bool overworkedYesterday, bool poorDietYesterday)
    {
        float chance = Mathf.Clamp01(baseDisturbChance);
        if (!workedYesterday)    chance += chanceIfNoWork;
        if (overworkedYesterday) chance += chanceIfOverwork;
        if (poorDietYesterday)   chance += chanceIfPoorDiet;
        return Mathf.Clamp01(chance * Mathf.Max(1f, disturbanceMultiplier));
    }

    private int ResolveWakeHour(bool disturbedSleep, bool lateWakePenaltyTriggered)
    {
        if (lateWakePenaltyTriggered)
            return Mathf.Max(normalWakeHour, lateWakeHour);

        if (disturbedSleep)
            return Mathf.Max(normalWakeHour, disturbedWakeHour);

        return normalWakeHour;
    }

    private float CalculateLateWakeChance(WorkSessionManager workSessionManager, GymProgressionSystem gymProgressionSystem, PlayerStats playerStats, float energyBeforeSleep)
    {
        if (!enableLateWakePenalty) return 0f;

        bool highSugarPattern = HasHighRecentSugarIntake();
        bool overworkPattern  = workSessionManager != null && workSessionManager.HasWorkedToday && DidOverworkYesterday(workSessionManager);
        bool highFatigueDebt  = gymProgressionSystem != null && playerStats != null && playerStats.FatigueDebt >= highFatigueDebtThreshold;
        bool lowEnergyAtSleep = energyBeforeSleep <= lowEnergySleepPenaltyThreshold;

        float chance = 0f;
        if (highSugarPattern) chance += sugarSignalWeight;
        if (overworkPattern)  chance += overworkSignalWeight;
        if (highFatigueDebt)  chance += fatigueSignalWeight;
        if (lowEnergyAtSleep) chance += lowEnergySignalWeight;

        return Mathf.Clamp(chance, 0f, lateWakeChanceCap);
    }

    private bool HasHighRecentSugarIntake()
    {
        if (PlayerActionTracker.Instance == null) return false;
        int unhealthyCount = PlayerActionTracker.Instance.GetCount(PlayerActionTracker.ActionType.UnhealthyFoodTaken);
        if (unhealthyCount <= 0) return false;
        float estimatedSugar = unhealthyCount * GetAverageUnhealthySugarEstimate();
        return estimatedSugar >= highSugarEstimateThreshold;
    }

    private static float GetAverageUnhealthySugarEstimate()
    {
        FoodData[] foods = Resources.LoadAll<FoodData>("FoodData");
        if (foods == null || foods.Length == 0) return 12f;

        float totalSugar = 0f;
        int   count      = 0;
        for (int i = 0; i < foods.Length; i++)
        {
            FoodData food = foods[i];
            if (food == null || food.isHealthy) continue;
            float sugar = Mathf.Max(0f, food.Sugar);
            if (sugar <= 0f) sugar = Mathf.Max(0f, food.carbohydrate * 0.35f);
            totalSugar += sugar;
            count++;
        }

        return count == 0 ? 12f : totalSugar / count;
    }

    private void ApplyLateWakePenalty(PlayerStats stats)
    {
        if (stats == null) return;
        float penalty = Mathf.Max(0f, lateWakePenaltyAmount);
        if (penalty <= 0f) return;
        stats.AddFood(-penalty, 0f, 0f, 0f, 0f);
    }

    // ── Wake message ─────────────────────────────────────────
    private WakeMessageContent BuildWakeMessage(
        string dayName,
        bool workedYesterday,
        bool trainedYesterday,
        float energyBeforeSleep,
        bool disturbedSleep,
        bool lateWakePenaltyTriggered,
        DailyHealthResult evalResult = null,
        string mortalityWarning = null)
    {
        var content = new WakeMessageContent
        {
            intro = string.Format(wakeIntroTemplate, dayName),
            warnings = new List<string>(),
            summary = string.Empty
        };

        bool lowEnergySleep = energyBeforeSleep <= lowEnergyWarningThreshold;

        if (!workedYesterday)
            content.warnings.Add(wakeWarningNoWork);
        if (!trainedYesterday)
            content.warnings.Add(wakeWarningNoGym);
        if (lowEnergySleep)
            content.warnings.Add(wakeWarningLowEnergy);
        if (disturbedSleep)
            content.warnings.Add(wakeWarningDisturbedSleep);
        if (lateWakePenaltyTriggered)
            content.warnings.Add(wakeWarningLateWakePenalty);
        if (!string.IsNullOrEmpty(mortalityWarning))
            content.warnings.Add(mortalityWarning);

        var summaryParts = new List<string>();
        if (evalResult != null)
        {
            if (!string.IsNullOrEmpty(evalResult.overallNote))
                summaryParts.Add(evalResult.overallNote);

            if (trainedYesterday && !string.IsNullOrEmpty(evalResult.gymNote))
                summaryParts.Add(evalResult.gymNote);
            else if (!trainedYesterday && !string.IsNullOrEmpty(evalResult.gymNote)
                     && evalResult.gymScore < 0f)
                summaryParts.Add(evalResult.gymNote);

            if (workedYesterday && !string.IsNullOrEmpty(evalResult.workNote))
                summaryParts.Add(evalResult.workNote);
            else if (!workedYesterday && !string.IsNullOrEmpty(evalResult.workNote)
                     && evalResult.workScore < 0f)
                summaryParts.Add(evalResult.workNote);
        }

        if (summaryParts.Count > 0)
            content.summary = string.Join(" ", summaryParts);

        if (content.warnings.Count == 0 && string.IsNullOrEmpty(content.summary))
            content.summary = "Istirahatmu cukup. Lanjutkan harimu dengan pilihan sehat.";

        return content;
    }

    // ── Aging notification ───────────────────────────────────
    private string BuildAgingTransitionMessage(PlayerStats.AgeStage previousStage, PlayerStats.AgeStage newStage, int dayNumber, float phaseScore)
    {
        string fromLabel = PlayerStats.GetAgeStageLabelIndonesia(previousStage);
        string toLabel   = PlayerStats.GetAgeStageLabelIndonesia(newStage);
        string headline  = string.Format(agingNotificationTemplate, toLabel, Mathf.Max(1, dayNumber));
        string detail    = string.Format(agingNotificationDetailTemplate, fromLabel, toLabel);
        string narrative;
        if (phaseScore > 70f)
            narrative = "Kebiasaan sehatmu di fase ini terbayar. Tubuhmu memasuki fase baru dengan lebih kuat.";
        else if (phaseScore >= 40f)
            narrative = "Ada beberapa kebiasaan yang perlu diperbaiki. Fase berikutnya memberi kesempatan baru.";
        else
            narrative = "Pola hidupmu di fase ini berdampak. Fase berikutnya akan terasa lebih berat.";

        return headline + "\n" + detail + "\n\n" + narrative;
    }

    private IEnumerator ShowAgingNotificationRoutine(
        PlayerStats.AgeStage previousStage,
        PlayerStats.AgeStage newStage,
        float phaseScore,
        PlayerStats.PhaseSnapshot snapshot,
        PlayerStats.Gender gender)
    {
        EnsureAgingNotificationPanel();
        if (agingPanelRoot == null || agingPanelGroup == null ||
            agingPanelTitleText == null || agingPanelBodyText == null || agingPanelContinueButton == null)
            yield break;

        if (ModalStateManager.Instance != null)
            ModalStateManager.Instance.OpenModal(AgingNotificationModalKey);

        bool continueClicked = false;

        try
        {
            agingPanelRoot.SetAsLastSibling();
            agingPanelRoot.gameObject.SetActive(true);
            agingPanelGroup.alpha           = 1f;
            agingPanelGroup.blocksRaycasts  = true;
            agingPanelGroup.interactable    = true;

            agingPanelTitleText.text = $"Memasuki Usia {PlayerStats.GetAgeStageLabelIndonesia(newStage)}";
            agingPanelBodyText.text  = string.Empty;
            agingPanelContinueButton.onClick.RemoveAllListeners();
            agingPanelContinueButton.onClick.AddListener(() => continueClicked = true);
            agingPanelContinueButton.interactable = false;

            PlayerStats stats = PlayerStats.Instance;
            float carryOver = stats != null ? (stats.MaxEnergy / 100f) : 1f;

            var reviewInput = new PhaseReviewBuilder.PhaseReviewInput
            {
                previousStage = previousStage,
                newStage = newStage,
                phaseScore = phaseScore,
                snapshot = snapshot,
                phaseCarryOverModifier = carryOver,
                gender = gender
            };
            string narrative          = PhaseReviewBuilder.Build(reviewInput);
            float  visibleCharacters  = 0f;
            const float charsPerSecond = 40f;

            while (visibleCharacters < narrative.Length)
            {
                visibleCharacters += charsPerSecond * Time.unscaledDeltaTime;
                int shownCount = Mathf.Clamp(Mathf.FloorToInt(visibleCharacters), 0, narrative.Length);
                agingPanelBodyText.text = narrative.Substring(0, shownCount);
                yield return null;
            }

            agingPanelBodyText.text = narrative;
            agingPanelContinueButton.interactable = true;

            while (!continueClicked)
                yield return null;
        }
        finally
        {
            if (agingPanelContinueButton != null)
                agingPanelContinueButton.onClick.RemoveAllListeners();

            if (agingPanelGroup != null)
            {
                agingPanelGroup.alpha          = 0f;
                agingPanelGroup.blocksRaycasts = false;
                agingPanelGroup.interactable   = false;
            }

            if (agingPanelRoot != null)
                agingPanelRoot.gameObject.SetActive(false);

            if (ModalStateManager.Instance != null)
                ModalStateManager.Instance.CloseModal(AgingNotificationModalKey);
        }
    }

    // BuildAgingPanelNarrative replaced by PhaseReviewBuilder.Build()

    // ── UI helpers (tidak berubah dari versi asli) ───────────
    private static TMP_FontAsset LoadAgingPanelFont(string resourcePath)
    {
        TMP_FontAsset fontAsset = Resources.Load<TMP_FontAsset>(resourcePath);
        return fontAsset != null ? fontAsset : TMP_Settings.defaultFontAsset;
    }

    private void EnsureAgingNotificationPanel()
    {
        if (agingPanelRoot != null && agingPanelGroup != null &&
            agingPanelTitleText != null && agingPanelBodyText != null && agingPanelContinueButton != null)
            return;

        ResolveAgingPanelFromScene();
    }

    private void ResolveAgingPanelFromScene()
    {
        GameObject hudObject = GameObject.Find("HUD_Canvas");
        if (hudObject == null) return;

        Transform panelTransform = null;
        Transform[] children = hudObject.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i].name == "AgingNotificationPanel")
            {
                panelTransform = children[i];
                break;
            }
        }

        if (panelTransform == null) return;

        AgingNotificationPanelBinder binder = panelTransform.GetComponent<AgingNotificationPanelBinder>();
        if (binder != null && binder.titleText != null && binder.bodyText != null && binder.continueButton != null)
        {
            agingPanelRoot = binder.panelRoot != null
                ? binder.panelRoot
                : panelTransform as RectTransform ?? panelTransform.GetComponent<RectTransform>();
            agingPanelGroup = binder.panelGroup != null
                ? binder.panelGroup
                : panelTransform.GetComponent<CanvasGroup>();
            agingPanelTitleText = binder.titleText;
            agingPanelBodyText = binder.bodyText;
            agingPanelContinueButton = binder.continueButton;
        }
        else
        {
            agingPanelRoot = panelTransform as RectTransform ?? panelTransform.GetComponent<RectTransform>();
            agingPanelGroup = panelTransform.GetComponent<CanvasGroup>();
            agingPanelTitleText = FindChildByName(panelTransform, "TitleText")?.GetComponent<TextMeshProUGUI>();
            agingPanelBodyText = FindChildByName(panelTransform, "BodyText")?.GetComponent<TextMeshProUGUI>();
            agingPanelContinueButton = FindChildByName(panelTransform, "LanjutButton")?.GetComponent<Button>();
        }

        if (agingPanelGroup != null)
        {
            agingPanelGroup.alpha          = 0f;
            agingPanelGroup.interactable   = false;
            agingPanelGroup.blocksRaycasts = false;
        }

        if (agingPanelRoot != null)
            agingPanelRoot.gameObject.SetActive(false);
    }

    private static Transform FindChildByName(Transform root, string targetName)
    {
        if (root == null || string.IsNullOrWhiteSpace(targetName)) return null;
        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
            if (children[i].name == targetName) return children[i];
        return null;
    }

    private void ShowWakeMessage(string message, float duration)
    {
        ShowWakeMessage(new WakeMessageContent { intro = InteractionPromptCopy.FormatBannerMessage(message) }, duration);
    }

    private void ShowWakeMessage(WakeMessageContent content, float duration)
    {
        EnsureWakeUi();
        if (wakeCanvasGroup == null || wakeIntroText == null)
            return;

        if (wakeMessageRoutine != null)
            StopCoroutine(wakeMessageRoutine);

        wakeMessageRoutine = StartCoroutine(ShowWakeMessageRoutine(content, duration));
    }

    private IEnumerator PlayPreSleepCinematic()
    {
        EnsurePreSleepCinematicUi();
        if (sleepCinematicCanvas == null || sleepCinematicCanvasGroup == null || sleepCinematicText == null)
            yield break;

        sleepCinematicText.text = PickWarmSleepLine();
        sleepCinematicCanvas.gameObject.SetActive(true);
        sleepCinematicCanvasGroup.alpha = 0f;

        float duration = Mathf.Max(0.2f, eyeCloseDuration);
        float elapsed  = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t     = Mathf.Clamp01(elapsed / duration);
            float eased = t * t * (3f - 2f * t);
            sleepCinematicCanvasGroup.alpha = eased;
            yield return null;
        }

        sleepCinematicCanvasGroup.alpha = 1f;
        yield return new WaitForSecondsRealtime(Mathf.Max(0.2f, warmLineHoldDuration));
    }

    private void HidePreSleepCinematic()
    {
        if (sleepCinematicCanvasGroup != null) sleepCinematicCanvasGroup.alpha = 0f;
        if (sleepCinematicCanvas      != null) sleepCinematicCanvas.gameObject.SetActive(false);
    }

    private void PrepareWakeEyeOpenCinematic(string dayName)
    {
        EnsurePreSleepCinematicUi();
        if (sleepCinematicCanvas == null || sleepCinematicCanvasGroup == null || sleepCinematicText == null)
            return;

        sleepCinematicText.text = PickWarmWakeLine(dayName);
        Color warmColor = sleepCinematicText.color;
        warmColor.a = 1f;
        sleepCinematicText.color = warmColor;

        sleepCinematicCanvas.gameObject.SetActive(true);
        sleepCinematicCanvasGroup.alpha = 1f;
    }

    private IEnumerator PlayWakeEyeOpenCinematic(string dayName)
    {
        EnsurePreSleepCinematicUi();
        if (sleepCinematicCanvas == null || sleepCinematicCanvasGroup == null || sleepCinematicText == null)
            yield break;

        sleepCinematicText.text = PickWarmWakeLine(dayName);
        sleepCinematicCanvas.gameObject.SetActive(true);
        sleepCinematicCanvasGroup.alpha = 1f;

        float duration = Mathf.Max(0.25f, eyeOpenDuration);
        float hold     = Mathf.Max(0f, wakeLineHoldDuration);
        float elapsed  = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t     = Mathf.Clamp01(elapsed / duration);
            float eased = t * t;
            sleepCinematicCanvasGroup.alpha = 1f - eased;

            Color txtColor       = sleepCinematicText.color;
            float textFadeOutStart = Mathf.Clamp01((hold + duration * 0.45f) / duration);
            float textAlpha = t < textFadeOutStart
                ? 1f
                : 1f - Mathf.InverseLerp(textFadeOutStart, 1f, t);
            txtColor.a = Mathf.Clamp01(textAlpha);
            sleepCinematicText.color = txtColor;

            yield return null;
        }

        HidePreSleepCinematic();
    }

    private string PickWarmSleepLine()
    {
        if (warmSleepLines == null || warmSleepLines.Length == 0)
            return "Tarik napas pelan. Besok kamu bisa mulai lagi dari awal yang lebih baik.";

        int    index = Random.Range(0, warmSleepLines.Length);
        string line  = warmSleepLines[index];
        return string.IsNullOrWhiteSpace(line)
            ? "Tarik napas pelan. Besok kamu bisa mulai lagi dari awal yang lebih baik."
            : line.Trim();
    }

    private string PickWarmWakeLine(string dayName)
    {
        string fallback = string.IsNullOrWhiteSpace(dayName)
            ? "Pagi datang lagi. Buka mata perlahan, lalu mulai dengan ritme yang lebih baik."
            : $"{dayName} dimulai. Buka mata perlahan, lalu mulai dengan ritme yang lebih baik.";

        if (warmWakeLines == null || warmWakeLines.Length == 0)
            return fallback;

        int    index = Random.Range(0, warmWakeLines.Length);
        string line  = warmWakeLines[index];
        if (string.IsNullOrWhiteSpace(line)) return fallback;

        string trimmed = line.Trim();
        return trimmed.Contains("{0}") ? string.Format(trimmed, dayName) : trimmed;
    }

    private void EnsurePreSleepCinematicUi()
    {
        if (sleepCinematicCanvas != null && sleepCinematicCanvasGroup != null && sleepCinematicText != null)
            return;

        if (TryBindExistingPreSleepUi())
            return;

        GameObject canvasObj = new GameObject("SleepPreCinematicCanvas",
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));

        sleepCinematicCanvas              = canvasObj.GetComponent<Canvas>();
        sleepCinematicCanvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        sleepCinematicCanvas.sortingOrder = 998;

        CanvasScaler scaler = canvasObj.GetComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        canvasObj.GetComponent<GraphicRaycaster>().enabled = false;
        sleepCinematicCanvasGroup       = canvasObj.GetComponent<CanvasGroup>();
        sleepCinematicCanvasGroup.alpha = 0f;

        GameObject panelObj  = new GameObject("SleepEyeClosePanel", typeof(RectTransform), typeof(Image));
        RectTransform panelRect = panelObj.GetComponent<RectTransform>();
        panelRect.SetParent(canvasObj.transform, false);
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;
        panelObj.GetComponent<Image>().color = new Color(0f, 0f, 0f, 1f);

        GameObject textObj  = new GameObject("SleepWarmLine", typeof(RectTransform), typeof(TextMeshProUGUI));
        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.SetParent(panelObj.transform, false);
        textRect.anchorMin = new Vector2(0.12f, 0.18f);
        textRect.anchorMax = new Vector2(0.88f, 0.36f);
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        sleepCinematicText                   = textObj.GetComponent<TextMeshProUGUI>();
        sleepCinematicText.alignment         = TextAlignmentOptions.Center;
        sleepCinematicText.fontSize          = SleepCinematicFontSize;
        sleepCinematicText.color             = new Color(0.98f, 0.95f, 0.86f, 1f);
        sleepCinematicText.textWrappingMode  = TextWrappingModes.Normal;
        sleepCinematicText.text              = string.Empty;

        sleepCinematicCanvas.gameObject.SetActive(false);
    }

    private IEnumerator ShowWakeMessageRoutine(WakeMessageContent content, float duration)
    {
        ApplyWakeMessageContent(content);
        wakeCanvasGroup.alpha = 1f;
        wakeCanvasGroup.blocksRaycasts = false;
        yield return new WaitForSecondsRealtime(Mathf.Max(0.5f, duration));
        wakeCanvasGroup.alpha = 0f;
        wakeMessageRoutine = null;
    }

    private void ApplyWakeMessageContent(WakeMessageContent content)
    {
        if (wakeIntroText != null)
        {
            wakeIntroText.text = content.intro ?? string.Empty;
            wakeIntroText.gameObject.SetActive(!string.IsNullOrWhiteSpace(wakeIntroText.text));
        }

        if (wakeWarningsText != null)
        {
            var lines = new List<string>();
            if (content.warnings != null)
            {
                for (int i = 0; i < content.warnings.Count; i++)
                {
                    string line = FormatWakeWarningLine(content.warnings[i]);
                    if (!string.IsNullOrWhiteSpace(line))
                        lines.Add(line);
                }
            }

            wakeWarningsText.text = lines.Count > 0 ? string.Join("\n", lines) : string.Empty;
            wakeWarningsText.gameObject.SetActive(lines.Count > 0);
        }

        if (wakeSummaryText != null)
        {
            wakeSummaryText.text = content.summary ?? string.Empty;
            wakeSummaryText.gameObject.SetActive(!string.IsNullOrWhiteSpace(wakeSummaryText.text));
        }

        RefreshWakePanelLayout();
    }

    private static string FormatWakeWarningLine(string warning)
    {
        if (string.IsNullOrWhiteSpace(warning))
            return string.Empty;

        const string prefix = "Peringatan: ";
        if (warning.StartsWith(prefix, System.StringComparison.Ordinal))
            return "• " + warning.Substring(prefix.Length);

        return "• " + warning;
    }

    private void RefreshWakePanelLayout()
    {
        if (wakePanelRect == null)
            return;

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(wakePanelRect);
        float preferredHeight = LayoutUtility.GetPreferredHeight(wakePanelRect);
        wakePanelRect.sizeDelta = new Vector2(
            WakePanelWidth,
            Mathf.Clamp(preferredHeight, WakePanelMinHeight, WakePanelMaxHeight));
    }

    private void EnsureWakeUi()
    {
        if (wakeCanvas != null && wakeCanvasGroup != null && wakeIntroText != null)
        {
            ConfigureWakePanelChrome();
            ApplyWakeTextTypography();
            return;
        }

        if (TryBindExistingWakeUi())
        {
            EnsureWakePanelChildren();
            ConfigureWakePanelChrome();
            ApplyWakeTextTypography();
            return;
        }

        GameObject canvasObj = new GameObject("SleepWakeCanvas",
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));

        wakeCanvas = canvasObj.GetComponent<Canvas>();
        wakeCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        wakeCanvas.sortingOrder = 120;

        CanvasScaler scaler = canvasObj.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        canvasObj.GetComponent<GraphicRaycaster>().enabled = false;
        wakeCanvasGroup = canvasObj.GetComponent<CanvasGroup>();
        wakeCanvasGroup.alpha = 0f;

        GameObject panelObj = new GameObject("WakePanel", typeof(RectTransform), typeof(Image), typeof(Outline), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        wakePanelRect = panelObj.GetComponent<RectTransform>();
        wakePanelRect.SetParent(canvasObj.transform, false);
        wakePanelRect.anchorMin = new Vector2(0.5f, 0.72f);
        wakePanelRect.anchorMax = new Vector2(0.5f, 0.72f);
        wakePanelRect.pivot = new Vector2(0.5f, 0.5f);
        wakePanelRect.sizeDelta = new Vector2(WakePanelWidth, WakePanelMinHeight);

        Image panelImage = panelObj.GetComponent<Image>();
        panelImage.color = new Color32(10, 14, 20, 230);

        Outline outline = panelObj.GetComponent<Outline>();
        outline.effectColor = new Color(1f, 1f, 1f, 0.16f);
        outline.effectDistance = new Vector2(1f, -1f);

        VerticalLayoutGroup layout = panelObj.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(22, 22, 18, 18);
        layout.spacing = 8f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        ContentSizeFitter fitter = panelObj.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        wakeIntroText = CreateWakeLineText("WakeIntroText", panelObj.transform, WakeIntroFontSize, FontStyles.Bold, new Color32(245, 248, 252, 255), TextAlignmentOptions.Center);
        wakeWarningsText = CreateWakeLineText("WakeWarningsText", panelObj.transform, WakeWarningsFontSize, FontStyles.Normal, new Color32(255, 176, 112, 255), TextAlignmentOptions.Center);
        wakeSummaryText = CreateWakeLineText("WakeSummaryText", panelObj.transform, WakeSummaryFontSize, FontStyles.Italic, new Color32(196, 204, 214, 255), TextAlignmentOptions.Center);
        ApplyWakeTextTypography();
    }

    private void EnsureWakePanelChildren()
    {
        if (wakePanelRect == null)
            return;

        if (wakeIntroText == null)
        {
            Transform legacy = wakePanelRect.Find("WakeText");
            wakeIntroText = legacy != null ? legacy.GetComponent<TextMeshProUGUI>() : null;
        }

        if (wakeWarningsText == null)
            wakeWarningsText = wakePanelRect.Find("WakeWarningsText")?.GetComponent<TextMeshProUGUI>();

        if (wakeSummaryText == null)
            wakeSummaryText = wakePanelRect.Find("WakeSummaryText")?.GetComponent<TextMeshProUGUI>();

        if (wakeIntroText == null)
            wakeIntroText = CreateWakeLineText("WakeIntroText", wakePanelRect, WakeIntroFontSize, FontStyles.Bold, new Color32(245, 248, 252, 255), TextAlignmentOptions.Center);

        if (wakeWarningsText == null)
            wakeWarningsText = CreateWakeLineText("WakeWarningsText", wakePanelRect, WakeWarningsFontSize, FontStyles.Normal, new Color32(255, 176, 112, 255), TextAlignmentOptions.Center);

        if (wakeSummaryText == null)
            wakeSummaryText = CreateWakeLineText("WakeSummaryText", wakePanelRect, WakeSummaryFontSize, FontStyles.Italic, new Color32(196, 204, 214, 255), TextAlignmentOptions.Center);

        ApplyWakeTextTypography();

        if (wakePanelRect.GetComponent<VerticalLayoutGroup>() == null)
        {
            VerticalLayoutGroup layout = wakePanelRect.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(22, 22, 18, 18);
            layout.spacing = 8f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
        }

        if (wakePanelRect.GetComponent<ContentSizeFitter>() == null)
        {
            ContentSizeFitter fitter = wakePanelRect.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }
    }

    private void ConfigureWakePanelChrome()
    {
        if (wakePanelRect == null)
            return;

        wakePanelRect.anchorMin = new Vector2(0.5f, 0.72f);
        wakePanelRect.anchorMax = new Vector2(0.5f, 0.72f);
        wakePanelRect.pivot = new Vector2(0.5f, 0.5f);

        Image panelImage = wakePanelRect.GetComponent<Image>();
        if (panelImage == null)
            panelImage = wakePanelRect.gameObject.AddComponent<Image>();
        panelImage.color = new Color32(10, 14, 20, 230);

        Outline outline = wakePanelRect.GetComponent<Outline>();
        if (outline == null)
            outline = wakePanelRect.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(1f, 1f, 1f, 0.16f);
        outline.effectDistance = new Vector2(1f, -1f);
    }

    private void ApplyWakeTextTypography()
    {
        ApplyWakeLineTypography(wakeIntroText, WakeIntroFontSize, FontStyles.Bold);
        ApplyWakeLineTypography(wakeWarningsText, WakeWarningsFontSize, FontStyles.Normal);
        ApplyWakeLineTypography(wakeSummaryText, WakeSummaryFontSize, FontStyles.Italic);
    }

    private static void ApplyWakeLineTypography(TextMeshProUGUI text, float fontSize, FontStyles style)
    {
        if (text == null)
            return;

        text.fontSize = fontSize;
        text.fontStyle = style;

        LayoutElement layoutElement = text.GetComponent<LayoutElement>();
        if (layoutElement != null)
        {
            layoutElement.minHeight = fontSize + 10f;
            layoutElement.preferredWidth = WakePanelWidth - 44f;
        }
    }

    private static TextMeshProUGUI CreateWakeLineText(
        string name,
        Transform parent,
        float fontSize,
        FontStyles style,
        Color32 color,
        TextAlignmentOptions alignment)
    {
        GameObject textObj = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
        textObj.transform.SetParent(parent, false);

        TextMeshProUGUI text = textObj.GetComponent<TextMeshProUGUI>();
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.color = color;
        text.alignment = alignment;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Overflow;
        text.outlineColor = new Color(0f, 0f, 0f, 0.9f);
        text.outlineWidth = 0.14f;
        text.text = string.Empty;

        LayoutElement layoutElement = textObj.GetComponent<LayoutElement>();
        layoutElement.minHeight = fontSize + 10f;
        layoutElement.preferredWidth = WakePanelWidth - 44f;

        return text;
    }

    private bool TryBindExistingPreSleepUi()
    {
        GameObject canvasObj = GameObject.Find("SleepPreCinematicCanvas");
        if (canvasObj == null)
            return false;

        SleepPreCinematicCanvasBinder binder = canvasObj.GetComponent<SleepPreCinematicCanvasBinder>();
        if (binder != null)
        {
            sleepCinematicCanvas = binder.canvas != null ? binder.canvas : canvasObj.GetComponent<Canvas>();
            sleepCinematicCanvasGroup = binder.canvasGroup != null ? binder.canvasGroup : canvasObj.GetComponent<CanvasGroup>();
            sleepCinematicText = binder.warmText;
            return sleepCinematicCanvas != null && sleepCinematicCanvasGroup != null && sleepCinematicText != null;
        }

        sleepCinematicCanvas = canvasObj.GetComponent<Canvas>();
        sleepCinematicCanvasGroup = canvasObj.GetComponent<CanvasGroup>();
        Transform textTransform = canvasObj.transform.Find("SleepEyeClosePanel/SleepWarmLine");
        sleepCinematicText = textTransform != null ? textTransform.GetComponent<TextMeshProUGUI>() : null;

        return sleepCinematicCanvas != null && sleepCinematicCanvasGroup != null && sleepCinematicText != null;
    }

    private bool TryBindExistingWakeUi()
    {
        GameObject canvasObj = GameObject.Find("SleepWakeCanvas");
        if (canvasObj == null)
            return false;

        SleepWakeCanvasBinder binder = canvasObj.GetComponent<SleepWakeCanvasBinder>();
        if (binder != null)
        {
            wakeCanvas = binder.canvas != null ? binder.canvas : canvasObj.GetComponent<Canvas>();
            wakeCanvasGroup = binder.canvasGroup != null ? binder.canvasGroup : canvasObj.GetComponent<CanvasGroup>();
            wakeIntroText = binder.wakeIntroText != null ? binder.wakeIntroText : binder.wakeText;
            wakeWarningsText = binder.wakeWarningsText;
            wakeSummaryText = binder.wakeSummaryText;
            wakePanelRect = binder.wakePanel != null
                ? binder.wakePanel
                : wakeIntroText != null ? wakeIntroText.transform.parent as RectTransform : null;
            return wakeCanvas != null && wakeCanvasGroup != null && wakeIntroText != null;
        }

        wakeCanvas = canvasObj.GetComponent<Canvas>();
        wakeCanvasGroup = canvasObj.GetComponent<CanvasGroup>();
        Transform panelTransform = canvasObj.transform.Find("WakePanel");
        wakePanelRect = panelTransform as RectTransform;

        Transform introTransform = panelTransform != null ? panelTransform.Find("WakeIntroText") : null;
        if (introTransform == null && panelTransform != null)
            introTransform = panelTransform.Find("WakeText");

        wakeIntroText = introTransform != null ? introTransform.GetComponent<TextMeshProUGUI>() : null;
        wakeWarningsText = panelTransform != null ? panelTransform.Find("WakeWarningsText")?.GetComponent<TextMeshProUGUI>() : null;
        wakeSummaryText = panelTransform != null ? panelTransform.Find("WakeSummaryText")?.GetComponent<TextMeshProUGUI>() : null;

        return wakeCanvas != null && wakeCanvasGroup != null && wakeIntroText != null;
    }
}