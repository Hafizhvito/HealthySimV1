using System.Collections;
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

    [Header("Optional References")]
    [SerializeField] private Transform bedSpawnPoint;
    [SerializeField] private TimeManager timeManagerOverride;
    [SerializeField] private PlayerStats playerStatsOverride;
    [SerializeField] private FadeManager fadeManagerOverride;
    [SerializeField] private WorkSessionManager workSessionManagerOverride;
    [SerializeField] private GymProgressionSystem gymProgressionSystemOverride;
    [SerializeField] private ClockAnimationUI clockAnimationUiOverride;

    private Collider cachedCollider;
    private Coroutine sleepRoutine;
    private Canvas wakeCanvas;
    private CanvasGroup wakeCanvasGroup;
    private TextMeshProUGUI wakeText;
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
        InteractableRegistry.Unregister(this);
    }

    private void Update()
    {
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
        return sleepRoutine == null;
    }

    public void Interact(GameObject interactor)
    {
        if (sleepRoutine != null)
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
        sleepRoutine = StartCoroutine(SleepRoutine(interactor, false));
    }

    private IEnumerator SleepRoutine(GameObject interactor, bool forcedSleep)
    {
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
            sleepRoutine = null;
            yield break;
        }

        TimeManager.TimePeriod periodBeforeSleep = timeManager.CurrentPeriod;
        if (!forcedSleep && requireNightToSleep && periodBeforeSleep != TimeManager.TimePeriod.Night)
        {
            ShowWakeMessage(blockedBeforeNightText, 1.8f);
            sleepRoutine = null;
            yield break;
        }

        // ── Snapshot data sebelum tidur ──────────────────────
        bool workedYesterday   = workSessionManager != null && workSessionManager.HasWorkedToday;
        bool trainedYesterday  = gymProgressionSystem != null && gymProgressionSystem.HasTrainedToday;
        WorkSessionData  lastWorkSession = workSessionManager?.LastSession;
        bool             lastWorkHadBonus = workSessionManager?.LastSessionHadBonus ?? false;
        GymSessionData   lastGymSession  = gymProgressionSystem?.LastSession;
        float energyBeforeSleep     = playerStats.EnergyPercent;
        // bool  trainedYesterday      = gymProgressionSystem != null && gymProgressionSystem.HasTrainedToday;
        float adaptationBeforeSleep = playerStats.TrainingAdaptation;
        float fatigueBeforeSleep    = playerStats.FatigueDebt;
        float scoreBeforeTransition = playerStats.HealthScoreThisPhase;

        bool  overworkedYesterday   = workedYesterday && DidOverworkYesterday(workSessionManager);
        bool  poorDietYesterday     = HasPoorDietPattern();
        bool  disturbedSleep        = RollSleepDisturbance(workedYesterday, overworkedYesterday, poorDietYesterday);
        float lateWakeChance        = CalculateLateWakeChance(workSessionManager, gymProgressionSystem, playerStats, energyBeforeSleep);
        bool  lateWakePenaltyTriggered = enableLateWakePenalty && UnityEngine.Random.value < lateWakeChance;

        if (forcedSleep)
            wasForced = true;

        if (playerController != null)
            playerController.LockInput(sleepLockSource);

        bool alreadyFaded = false;
        if (forcedSleep && fadeManager != null)
        {
            ShowWakeMessage(ForcedSleepMessage, Mathf.Max(1f, fadeDuration + 0.4f));
            bool fadeToBlackDone = false;
            fadeManager.FadeToBlack(fadeDuration, () => fadeToBlackDone = true);
            yield return new WaitUntil(() => fadeToBlackDone);
            alreadyFaded = true;
        }

        if (!forcedSleep && enablePreSleepCinematic)
            yield return StartCoroutine(PlayPreSleepCinematic());

        if (fadeManager != null && !alreadyFaded)
        {
            bool fadeToBlackDone = false;
            fadeManager.FadeToBlack(fadeDuration, () => fadeToBlackDone = true);
            yield return new WaitUntil(() => fadeToBlackDone);
        }

        HidePreSleepCinematic();

        int wakeHour = ResolveWakeHour(disturbedSleep, lateWakePenaltyTriggered);

        if (clockUi != null)
        {
            bool clockDone = false;
            clockUi.PlayTimeSkipAnimation("Istirahat Malam", 21, wakeHour, clockSkipDuration, () => clockDone = true);
            yield return new WaitUntil(() => clockDone);
        }

        // ── Apply recovery + evaluate health score ───────────
        DailyHealthResult dailyEvalResult = null;
        ApplyRecovery(playerStats, disturbedSleep, energyBeforeSleep,
                    workedYesterday, lastWorkSession, lastWorkHadBonus,
                    trainedYesterday, lastGymSession, overworkedYesterday, out dailyEvalResult);

        // ── Advance day ──────────────────────────────────────
        timeManager.AdvanceToNextDayFromSleep();
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

        float baseDrainModifier = ApplyNextDayMovementDrainModifier(playerStats, trainedYesterday, adaptationBeforeSleep, fatigueBeforeSleep);

        if (wasForced)
            ApplyBegadangPenalty(playerStats, baseDrainModifier);

        if (lateWakePenaltyTriggered)
            ApplyLateWakePenalty(playerStats);

        MoveInteractorToBedSpawn(interactor);
        Debug.Log($"[SleepBedInteractable] {sleepingLogText} Hari {wakeDayName}.");

        if (enableWakeEyeOpenCinematic)
            PrepareWakeEyeOpenCinematic(wakeDayName);

        if (fadeManager != null)
        {
            bool fadeFromBlackDone = false;
            fadeManager.FadeFromBlack(fadeDuration, () => fadeFromBlackDone = true);
            yield return new WaitUntil(() => fadeFromBlackDone);
        }

        if (enableWakeEyeOpenCinematic)
            yield return StartCoroutine(PlayWakeEyeOpenCinematic(wakeDayName));

        if (playerController != null)
            playerController.UnlockInput(sleepLockSource);

        if (ageStageChanged)
        {
            yield return StartCoroutine(ShowAgingNotificationRoutine(newAgeStage, scoreBeforeTransition));

            if (newAgeStage == PlayerStats.AgeStage.Senior)
            {
                int triggerDay = EndingManager.Instance != null
                    ? EndingManager.Instance.EndingTriggerDay
                    : 10;

                if (timeManager.CurrentDayNumber >= triggerDay)
                {
                    if (EndingManager.Instance != null)
                        EndingManager.Instance.TriggerEnding();
                    else
                        Debug.LogWarning("[SleepBedInteractable] EndingManager.Instance null — ending not triggered.");
                }
            }
        }

        // ── Wake message dengan hasil evaluator ──────────────
        ShowWakeMessage(
            BuildWakeMessage(wakeDayName, workedYesterday, energyBeforeSleep,
                             disturbedSleep, lateWakePenaltyTriggered, dailyEvalResult),
            wakeMessageDuration);

        if (EndingManager.Instance != null)
            EndingManager.Instance.NotifySleepCompleted(timeManager.CurrentDayNumber);

        forceSleepTriggered = false;
        sleepRoutine = null;
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
        Debug.Log($"[SleepBed] diet='{evalResult.dietNote}' gym='{evalResult.gymNote}' work='{evalResult.workNote}'");

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
        if (forceSleepTriggered || sleepRoutine != null)
            return;

        TimeManager timeManager = ResolveTimeManager();
        if (timeManager == null)
            return;

        if (timeManager.CurrentHour < 24f)
            return;

        PlayerController playerController = FindFirstObjectByType<PlayerController>();
        GameObject interactor = playerController != null ? playerController.gameObject : null;
        forceSleepTriggered = true;
        sleepRoutine = StartCoroutine(SleepRoutine(interactor, true));
    }

    private void MoveInteractorToBedSpawn(GameObject interactor)
    {
        if (interactor == null)
            return;

        Transform spawn = ResolveSpawnPoint();
        interactor.transform.position = spawn.position;
        interactor.transform.rotation = spawn.rotation;

        Rigidbody rb = interactor.GetComponent<Rigidbody>();
        if (rb != null)
            rb.linearVelocity = Vector3.zero;
    }

    private Transform ResolveSpawnPoint()
    {
        if (bedSpawnPoint != null)
            return bedSpawnPoint;

        Transform child = transform.Find("BedSpawnPoint");
        if (child != null)
            return child;

        return transform;
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
    private string BuildWakeMessage(
        string dayName,
        bool workedYesterday,
        float energyBeforeSleep,
        bool disturbedSleep,
        bool lateWakePenaltyTriggered,
        DailyHealthResult evalResult = null)
    {
        string intro         = string.Format(wakeIntroTemplate, dayName);
        bool   lowEnergySleep = energyBeforeSleep <= lowEnergyWarningThreshold;

        var warnings = new System.Collections.Generic.List<string>();

        if (!workedYesterday)        warnings.Add(wakeWarningNoWork);
        if (lowEnergySleep)          warnings.Add(wakeWarningLowEnergy);
        if (disturbedSleep)          warnings.Add(wakeWarningDisturbedSleep);
        if (lateWakePenaltyTriggered) warnings.Add(wakeWarningLateWakePenalty);

        // Overall note dari evaluator (misal "Hari yang baik. Terus konsisten.")
        if (evalResult != null && !string.IsNullOrEmpty(evalResult.overallNote))
            warnings.Add(evalResult.overallNote);

        if (warnings.Count > 0)
            return intro + "\n" + string.Join("\n", warnings);

        return intro + "\nIstirahatmu cukup. Lanjutkan harimu dengan pilihan sehat.";
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

    private IEnumerator ShowAgingNotificationRoutine(PlayerStats.AgeStage newStage, float phaseScore)
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

            string narrative          = BuildAgingPanelNarrative(phaseScore);
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

    private string BuildAgingPanelNarrative(float phaseScore)
    {
        bool worked    = WorkSessionManager.Instance   != null && WorkSessionManager.Instance.HasWorkedToday;
        bool exercised = GymProgressionSystem.Instance != null && GymProgressionSystem.Instance.HasTrainedToday;

        if (phaseScore > 70f && worked && exercised)
            return "Hidupmu di fase ini seimbang — kerja, gerak, dan makan terjaga. Tubuhmu memasuki fase berikutnya dalam kondisi terbaik.";

        if (phaseScore > 70f && worked && !exercised)
            return "Pekerja keras, tapi kurang bergerak. Nutrisimu baik, namun tubuhmu butuh aktivitas fisik lebih konsisten.";

        if (phaseScore > 70f && !worked && exercised)
            return "Tubuhmu aktif dan terawat, tapi stabilitas finansialmu perlu perhatian. Keseimbangan hidup bukan hanya soal fisik.";

        PlayerStats stats = PlayerStats.Instance;
        if (stats != null && stats.CurrentAgeStage == PlayerStats.AgeStage.Senior && stats.PlayerGender == PlayerStats.Gender.Female)
        {
            if (phaseScore > 70f)
                return "Kamu memasuki fase lansia sebagai perempuan — tubuhmu mulai menyesuaikan perubahan hormonal. Kebiasaan sehatmu akan sangat membantu stabilitas energi dan suasana hatimu.";
            if (phaseScore < 40f)
                return "Perubahan hormonal di fase lansia bisa terasa berat, apalagi jika pola hidupmu belum mendukung. Mulai perhatikan asupan dan istirahatmu lebih serius.";
            return "Memasuki fase menopause adalah perubahan besar. Tubuhmu butuh lebih banyak perhatian — terutama pola makan dan manajemen stres.";
        }

        if (phaseScore < 40f && !worked && !exercised)
            return "Fase ini banyak dilewatkan tanpa aktivitas berarti. Dampaknya mulai terasa — tubuhmu meminta perhatian lebih serius.";

        if (phaseScore < 40f && worked)
            return "Kamu bekerja keras, tapi pola makanmu belum mendukung. Energimu terkuras lebih dari yang seharusnya.";

        return "Ada kemajuan, tapi masih banyak ruang untuk berkembang. Fase berikutnya adalah kesempatan untuk lebih konsisten.";
    }

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

        agingPanelRoot  = panelTransform as RectTransform ?? panelTransform.GetComponent<RectTransform>();
        agingPanelGroup = panelTransform.GetComponent<CanvasGroup>();
        agingPanelTitleText      = FindChildByName(panelTransform, "TitleText")?.GetComponent<TextMeshProUGUI>();
        agingPanelBodyText       = FindChildByName(panelTransform, "BodyText")?.GetComponent<TextMeshProUGUI>();
        agingPanelContinueButton = FindChildByName(panelTransform, "LanjutButton")?.GetComponent<Button>();

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
        EnsureWakeUi();
        if (wakeText == null || wakeCanvasGroup == null) return;
        if (wakeMessageRoutine != null) StopCoroutine(wakeMessageRoutine);
        wakeMessageRoutine = StartCoroutine(ShowWakeMessageRoutine(message, duration));
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
        sleepCinematicText.fontSize          = 30f;
        sleepCinematicText.color             = new Color(0.98f, 0.95f, 0.86f, 1f);
        sleepCinematicText.textWrappingMode  = TextWrappingModes.Normal;
        sleepCinematicText.text              = string.Empty;

        sleepCinematicCanvas.gameObject.SetActive(false);
    }

    private IEnumerator ShowWakeMessageRoutine(string message, float duration)
    {
        wakeText.text              = message;
        wakeCanvasGroup.alpha      = 1f;
        wakeCanvasGroup.blocksRaycasts = false;
        yield return new WaitForSecondsRealtime(Mathf.Max(0.5f, duration));
        wakeCanvasGroup.alpha      = 0f;
        wakeMessageRoutine         = null;
    }

    private void EnsureWakeUi()
    {
        if (wakeCanvas != null && wakeCanvasGroup != null && wakeText != null)
            return;

        if (TryBindExistingWakeUi())
            return;

        GameObject canvasObj = new GameObject("SleepWakeCanvas",
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));

        wakeCanvas              = canvasObj.GetComponent<Canvas>();
        wakeCanvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        wakeCanvas.sortingOrder = 120;

        CanvasScaler scaler = canvasObj.GetComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        canvasObj.GetComponent<GraphicRaycaster>().enabled = false;
        wakeCanvasGroup       = canvasObj.GetComponent<CanvasGroup>();
        wakeCanvasGroup.alpha = 0f;

        GameObject panelObj  = new GameObject("WakePanel", typeof(RectTransform), typeof(Image));
        RectTransform panelRect = panelObj.GetComponent<RectTransform>();
        panelRect.SetParent(canvasObj.transform, false);
        panelRect.anchorMin  = new Vector2(0.5f, 0.87f);
        panelRect.anchorMax  = new Vector2(0.5f, 0.87f);
        panelRect.pivot      = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta  = new Vector2(980f, 140f);
        panelObj.GetComponent<Image>().color = new Color(0.06f, 0.08f, 0.12f, 0.78f);

        GameObject textObj  = new GameObject("WakeText", typeof(RectTransform), typeof(TextMeshProUGUI));
        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.SetParent(panelObj.transform, false);
        textRect.anchorMin = new Vector2(0.05f, 0.12f);
        textRect.anchorMax = new Vector2(0.95f, 0.88f);
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        wakeText                  = textObj.GetComponent<TextMeshProUGUI>();
        wakeText.alignment        = TextAlignmentOptions.Center;
        wakeText.fontSize         = 26f;
        wakeText.color            = new Color(0.98f, 0.94f, 0.84f, 1f);
        wakeText.textWrappingMode = TextWrappingModes.Normal;
        wakeText.text             = string.Empty;
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
            wakeText = binder.wakeText;
            return wakeCanvas != null && wakeCanvasGroup != null && wakeText != null;
        }

        wakeCanvas = canvasObj.GetComponent<Canvas>();
        wakeCanvasGroup = canvasObj.GetComponent<CanvasGroup>();
        Transform textTransform = canvasObj.transform.Find("WakePanel/WakeText");
        wakeText = textTransform != null ? textTransform.GetComponent<TextMeshProUGUI>() : null;

        return wakeCanvas != null && wakeCanvasGroup != null && wakeText != null;
    }
}