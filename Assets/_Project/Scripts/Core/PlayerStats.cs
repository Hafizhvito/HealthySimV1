using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PlayerStats : MonoBehaviour
{
    public static PlayerStats Instance { get; private set; }

    [Header("Energy")]
    [SerializeField] private float maxEnergy = 100f;
    [SerializeField] private float currentEnergy = 100f;
    [SerializeField] private float energyDrainIdle = 0.2f;
    [SerializeField] private float energyDrainWalk = 0.45f;
    [SerializeField] private float energyDrainRun = 0.95f;
    [SerializeField] [Range(0.05f, 1f)] private float movementDrainScale = 0.30f;
    [SerializeField] [Range(0.5f, 1.5f)] private float movementDrainModifier = 1f;
    [SerializeField] private float runDrainRampSeconds = 1.2f;

    [Header("Post-Activity Travel Grace")]
    [SerializeField] [Range(0.1f, 1f)] private float postActivityTravelGraceMultiplier = 0.55f;

    private bool postActivityTravelGraceActive;
    private bool pendingTravelGraceToast;

    [Header("Calories")]
    [SerializeField] private float totalCaloriesConsumed = 0f;
    [SerializeField] private float dailyCalorieTarget = 2000f;
    [SerializeField] private float dailyProtein = 0f;
    [SerializeField] private float dailyFat = 0f;

    [Header("Mood")]
    [SerializeField] private float maxMood = 100f;
    [SerializeField] private float currentMood = 50f;
    [SerializeField] private float moodDrainRate = 0.2f;

    [Header("Player Info (from home screen)")]
    [SerializeField] private string playerName = "Player";
    [SerializeField] private float playerHeight = 170f;
    [SerializeField] private float playerWeight = 65f;
    [SerializeField] private float playerBMI = 0f;

    [Header("Economy")]
    [SerializeField] private int _money = 500;

    [Header("Gym Progression (Hidden)")]
    [SerializeField] [HideInInspector] private float trainingAdaptation = 0f;
    [SerializeField] [HideInInspector] private float fatigueDebt = 0f;
    [SerializeField] [HideInInspector] private float healthScoreThisPhase = 50f;
    [SerializeField] [HideInInspector] private float[] committedPhaseScores = new float[3] { 50f, 50f, 50f };
    [SerializeField] [HideInInspector] private float phaseCarryOverModifier = 1.0f;

    [Header("Health Tracking (Hidden)")]
    [SerializeField] [HideInInspector] private int totalDaysEvaluated = 0;
    [SerializeField] [HideInInspector] private int poorDietDays = 0;
    [SerializeField] [HideInInspector] private int noFoodDays = 0;
    [SerializeField] [HideInInspector] private int highCalorieDays = 0;
    [SerializeField] [HideInInspector] private int lowCalorieDays = 0;
    [SerializeField] [HideInInspector] private int disturbedSleepDays = 0;
    [SerializeField] [HideInInspector] private int lowEnergySleepDays = 0;
    [SerializeField] [HideInInspector] private int skippedGymDays = 0;
    [SerializeField] [HideInInspector] private int skippedWorkDays = 0;
    [SerializeField] [HideInInspector] private int overworkedDays = 0;
    [SerializeField] [HideInInspector] private bool _visitedHospitalToday = false;
    [SerializeField] [HideInInspector] private bool _healthGuidanceDismissed = false;
    [SerializeField] [HideInInspector] private bool _healthGuidanceIndicatorsUnlocked = false;

    [Header("Phase-Start Snapshot (Hidden)")]
    [SerializeField] [HideInInspector] private int phaseStartDaysEvaluated;
    [SerializeField] [HideInInspector] private int phaseStartPoorDietDays;
    [SerializeField] [HideInInspector] private int phaseStartHighCalorieDays;
    [SerializeField] [HideInInspector] private int phaseStartSkippedGymDays;
    [SerializeField] [HideInInspector] private int phaseStartSkippedWorkDays;
    [SerializeField] [HideInInspector] private int phaseStartOverworkedDays;
    [SerializeField] [HideInInspector] private int phaseStartDisturbedSleepDays;

    [Header("Streak Counters (Hidden)")]
    [SerializeField] [HideInInspector] private int gymSkipStreak  = 0;
    [SerializeField] [HideInInspector] private int workSkipStreak = 0;

    [Header("Age Progression (Hidden)")]
    [SerializeField] [HideInInspector] private int progressionDayCount = 1;
    [SerializeField] [HideInInspector] private AgeStage currentAgeStage = AgeStage.Youth;
    [SerializeField] private Gender playerGender = Gender.Male;

    [Header("Energy State Thresholds")]
    [SerializeField] private float warningThreshold = 40f;
    [SerializeField] private float criticalThreshold = 15f;

    [Header("Faint Presentation")]
    [SerializeField] private float faintFadeOutDuration = 0.35f;
    [SerializeField] private float faintFadeInDuration = 0.5f;
    [SerializeField] [Range(0f, 1f)] private float vehicleKnockdownEnergyDrain = 0.12f;

    public enum EnergyState { Normal, Warning, Critical, Fainted }
    public enum AgeStage { Youth, Adult, Senior }
    public enum Gender { Male, Female }

    private EnergyState currentEnergyState = EnergyState.Normal;
    private float faintTimer = 0f;
    private float faintDuration = 3f;
    private float runningDuration;
    private bool faintRespawnHandled;
    private Coroutine faintPresentationRoutine;
    private bool vehicleKnockdownInProgress;

    private const string VehicleKnockdownTitle = "Tertabrak Kendaraan";
    private const string VehicleKnockdownBody =
        "Kamu tertabrak kendaraan. Istirahat sejenak dan lebih berhati-hati saat menyeberang jalan.";
    private const float PresentationWaitTimeoutSeconds = 8f;

    // ── Public getters ───────────────────────────────────────
    public float CurrentEnergy       => currentEnergy;
    public float MaxEnergy           => maxEnergy;
    public float EnergyPercent       => currentEnergy / maxEnergy;
    public float CurrentMood         => currentMood;
    public float MaxMood             => maxMood;
    public float MoodPercent         => currentMood / maxMood;
    public float TotalCalories       => totalCaloriesConsumed;
    public float DailyCalorieTarget  => dailyCalorieTarget;
    public float DailyProtein        => dailyProtein;
    public float DailyFat            => dailyFat;
    public EnergyState CurrentEnergyState => currentEnergyState;
    public string PlayerName         => playerName;
    public float PlayerBMI           => playerBMI;
    public Gender PlayerGender       => playerGender;
    public int Money                 => _money;
    public float TrainingAdaptation  => trainingAdaptation;
    public float FatigueDebt        => fatigueDebt;
    public float HealthScoreThisPhase => healthScoreThisPhase;
    public float[] CommittedPhaseScores => committedPhaseScores;
    public float MovementDrainModifier  => movementDrainModifier;
    public bool PostActivityTravelGraceActive => postActivityTravelGraceActive;
    public int ProgressionDayCount   => progressionDayCount;
    public AgeStage CurrentAgeStage  => currentAgeStage;

    public int TotalDaysEvaluated   => totalDaysEvaluated;
    public int PoorDietDays         => poorDietDays;
    public int NoFoodDays           => noFoodDays;
    public int HighCalorieDays      => highCalorieDays;
    public int LowCalorieDays       => lowCalorieDays;
    public int DisturbedSleepDays   => disturbedSleepDays;
    public int LowEnergySleepDays   => lowEnergySleepDays;
    public int SkippedGymDays       => skippedGymDays;
    public int SkippedWorkDays      => skippedWorkDays;
    public int OverworkedDays       => overworkedDays;
    public bool VisitedHospitalToday => _visitedHospitalToday;

    public bool ShouldShowHealthGuidance(float threshold = 40f)
    {
        if (_healthGuidanceDismissed)
            return false;

        return HealthScoreThisPhase < threshold;
    }

    public bool ShouldShowHealthIndicators(float threshold = 40f)
    {
        return ShouldShowHealthGuidance(threshold) && _healthGuidanceIndicatorsUnlocked;
    }

    public void AcknowledgeHealthAlertNotice(float threshold = 40f)
    {
        if (!ShouldShowHealthGuidance(threshold))
            return;

        _healthGuidanceIndicatorsUnlocked = true;
        OnHealthGuidanceIndicatorsUnlocked?.Invoke();
    }

    public void DismissHealthGuidance()
    {
        _healthGuidanceDismissed = true;
        _healthGuidanceIndicatorsUnlocked = false;
    }

    private void TryRearmHealthGuidance(float threshold)
    {
        if (HealthScoreThisPhase >= threshold)
        {
            _healthGuidanceDismissed = false;
            _healthGuidanceIndicatorsUnlocked = false;
        }
    }

    public void NotifyHealthScoreEvaluated(float threshold = 40f)
    {
        TryRearmHealthGuidance(threshold);
    }

    public PhaseSnapshot GetCurrentPhaseSnapshot()
    {
        return new PhaseSnapshot
        {
            daysInPhase        = Mathf.Max(1, totalDaysEvaluated - phaseStartDaysEvaluated),
            poorDietDays       = Mathf.Max(0, poorDietDays - phaseStartPoorDietDays),
            highCalorieDays    = Mathf.Max(0, highCalorieDays - phaseStartHighCalorieDays),
            skippedGymDays     = Mathf.Max(0, skippedGymDays - phaseStartSkippedGymDays),
            skippedWorkDays    = Mathf.Max(0, skippedWorkDays - phaseStartSkippedWorkDays),
            overworkedDays     = Mathf.Max(0, overworkedDays - phaseStartOverworkedDays),
            disturbedSleepDays = Mathf.Max(0, disturbedSleepDays - phaseStartDisturbedSleepDays)
        };
    }

    public struct PhaseSnapshot
    {
        public int daysInPhase;
        public int poorDietDays;
        public int highCalorieDays;
        public int skippedGymDays;
        public int skippedWorkDays;
        public int overworkedDays;
        public int disturbedSleepDays;
    }

    // ── Streak getters ───────────────────────────────────────
    public int GymSkipStreak         => gymSkipStreak;
    public int WorkSkipStreak        => workSkipStreak;

    // ── Events ───────────────────────────────────────────────
    public System.Action<EnergyState> OnEnergyStateChanged;
    public System.Action OnPlayerFainted;
    public System.Action<float> OnEnergyChanged;
    public System.Action<float> OnMoodChanged;
    public System.Action<float> OnCaloriesChanged;
    public System.Action<AgeStage, AgeStage, int> OnAgeStageChanged;
    public System.Action OnHealthGuidanceIndicatorsUnlocked;

    [System.Serializable]
    private struct PhaseModifierData
    {
        public float dailyCalorieTarget;
        public float movementDrainMultiplier;
        public float moodDrainMultiplier;
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        movementDrainScale = 0.30f;
    }

    void Start()
    {
        CalculateBMI();
        SyncAgeProgressionFromDay(progressionDayCount);
        ApplyPhaseModifiers();
    }

    void Update()
    {
        if (currentEnergyState == EnergyState.Fainted) return;
        TryShowPendingTravelGraceToast();
        DrainMoodOverTime();
        CheckFaintCondition();
    }

    // ── Movement energy drain ────────────────────────────────
    public void DrainEnergy(bool isWalking, bool isRunning, float deltaTime)
    {
        if (currentEnergyState == EnergyState.Fainted) return;

        float drainRate = energyDrainIdle;
        if (isRunning)
        {
            runningDuration += Mathf.Max(0f, deltaTime);
            float runRamp = Mathf.Clamp01(runningDuration / Mathf.Max(0.1f, runDrainRampSeconds));
            drainRate = Mathf.Lerp(energyDrainWalk, energyDrainRun, runRamp);
        }
        else if (isWalking)
        {
            runningDuration = 0f;
            drainRate = energyDrainWalk;
        }
        else
        {
            runningDuration = 0f;
        }

        float drainModifier = Mathf.Clamp(movementDrainModifier, 0.75f, 1.25f);
        float graceMultiplier = postActivityTravelGraceActive
            ? Mathf.Clamp(postActivityTravelGraceMultiplier, 0.1f, 1f)
            : 1f;
        float scaledDrain   = drainRate * Mathf.Clamp(movementDrainScale, 0.05f, 1f) * drainModifier * graceMultiplier;
        ModifyEnergy(-(scaledDrain * deltaTime));
    }

    public void ActivatePostActivityTravelGrace()
    {
        postActivityTravelGraceActive = true;
        pendingTravelGraceToast = true;
    }

    public void ClearPostActivityTravelGrace()
    {
        postActivityTravelGraceActive = false;
        pendingTravelGraceToast = false;
    }

    private void TryShowPendingTravelGraceToast()
    {
        if (!pendingTravelGraceToast)
            return;

        if (!string.Equals(SceneManager.GetActiveScene().name, "SampleScene", System.StringComparison.Ordinal))
            return;

        TutorialContextualUI tutorial = FindFirstObjectByType<TutorialContextualUI>();
        if (tutorial == null)
            return;

        pendingTravelGraceToast = false;
        tutorial.ShowTransientToast(
            "\U0001F9D8",
            "Badan masih adaptasi — perjalanan lebih ringan sampai kamu makan.");
    }

    public void AddFood(
        float energyAmount,
        float calories,
        float moodEffect,
        float protein,
        float fat,
        bool countsAsMeal = false)
    {
        ModifyEnergy(energyAmount);
        totalCaloriesConsumed += calories;
        dailyProtein += protein;
        dailyFat += fat;
        ModifyMood(moodEffect);
        OnCaloriesChanged?.Invoke(totalCaloriesConsumed);
        if (countsAsMeal)
            ClearPostActivityTravelGrace();
        Debug.Log($"[Nutrition] Calories={totalCaloriesConsumed:0.#}, Protein={dailyProtein:0.#}, Fat={dailyFat:0.#}");
    }

    public void ResetDailyCalories()
    {
        totalCaloriesConsumed = 0f;
        dailyProtein = 0f;
        dailyFat = 0f;
        OnCaloriesChanged?.Invoke(totalCaloriesConsumed);
    }

    public void ResetDailyHospitalVisitForNewDay()
    {
        _visitedHospitalToday = false;

        if (HealthScoreThisPhase < 40f)
        {
            _healthGuidanceDismissed = false;
            _healthGuidanceIndicatorsUnlocked = false;
        }
    }

    public void SetVisitedHospital()
    {
        _visitedHospitalToday = true;
        DismissHealthGuidance();
    }

    /// <summary>Reduces current energy by normalised amount [0..1] of max energy.</summary>
    public void DrainEnergy(float normalizedAmount)
    {
        float clamped = Mathf.Clamp01(normalizedAmount);
        ModifyEnergy(-(clamped * maxEnergy));
    }

    public void AddMoney(int amount)
    {
        int safeAmount = Mathf.Max(0, amount);
        _money = Mathf.Max(0, _money + safeAmount);
    }

    public void SpendMoney(int amount)
    {
        int safeAmount = Mathf.Max(0, amount);
        _money = Mathf.Max(0, _money - safeAmount);
    }

    /// <summary>Gym progression values are hidden gameplay stats used by gym systems.</summary>
    public void ApplyGymProgression(float adaptationDelta, float fatigueDelta)
    {
        trainingAdaptation = Mathf.Clamp(trainingAdaptation + adaptationDelta, 0f, 100f);
        fatigueDebt        = Mathf.Clamp(fatigueDebt        + fatigueDelta,    0f, 100f);
    }

    public void ApplyFatigueDebt(float amount)
    {
        if (amount <= 0f)
            return;

        fatigueDebt = Mathf.Clamp(fatigueDebt + amount, 0f, 100f);
    }

    public void RegisterHealthScore(float delta)
    {
        healthScoreThisPhase = Mathf.Clamp(healthScoreThisPhase + delta, 0f, 100f);
        TryRearmHealthGuidance(40f);
    }

    public void RegisterDailyHealthSnapshot(
        DailyHealthResult evalResult,
        bool disturbedSleep,
        float energyBeforeSleep,
        bool workedYesterday,
        bool trainedYesterday,
        bool overworkedYesterday,
        float calorieRatio)
    {
        totalDaysEvaluated++;

        if (evalResult != null)
        {
            int totalFood = evalResult.healthyFoodCount + evalResult.unhealthyFoodCount;
            if (totalFood == 0)
                noFoodDays++;

            if (evalResult.dietScore < 0f)
                poorDietDays++;
        }

        if (calorieRatio > 1.30f)
            highCalorieDays++;
        else if (calorieRatio > 0f && calorieRatio < 0.50f)
            lowCalorieDays++;

        if (disturbedSleep)
            disturbedSleepDays++;

        if (energyBeforeSleep <= 0.20f)
            lowEnergySleepDays++;

        if (!trainedYesterday)
            skippedGymDays++;

        if (!workedYesterday)
            skippedWorkDays++;

        if (overworkedYesterday)
            overworkedDays++;
    }

    public float GetAveragePhaseScore()
    {
        int currentIndex = (int)currentAgeStage;
        if (currentIndex >= 0 && currentIndex < committedPhaseScores.Length)
            committedPhaseScores[currentIndex] = healthScoreThisPhase;

        float total = 0f;
        for (int i = 0; i < 3; i++)
            total += committedPhaseScores[i];

        return total / 3f;
    }

    // ── Streak methods ───────────────────────────────────────

    /// <summary>Called by DailyHealthEvaluator when player did NOT gym today.</summary>
    public void IncrementGymSkipStreak()
    {
        gymSkipStreak = Mathf.Max(0, gymSkipStreak + 1);
    }

    /// <summary>Called by DailyHealthEvaluator when player DID gym today.</summary>
    public void ResetGymSkipStreak()
    {
        gymSkipStreak = 0;
    }

    /// <summary>Called by DailyHealthEvaluator when player did NOT work today.</summary>
    public void IncrementWorkSkipStreak()
    {
        workSkipStreak = Mathf.Max(0, workSkipStreak + 1);
    }

    /// <summary>Called by DailyHealthEvaluator when player DID work today.</summary>
    public void ResetWorkSkipStreak()
    {
        workSkipStreak = 0;
    }

    // ── Modifiers / setup ────────────────────────────────────
    public void SetGender(Gender g)
    {
        playerGender = g;
        ApplyPhaseModifiers();
    }

    public void SetMovementDrainModifier(float value)
    {
        movementDrainModifier = Mathf.Clamp(value, 0.5f, 1.5f);
    }

    public void ApplyPhaseModifiers()
    {
        PhaseModifierData data = GetPhaseModifier(currentAgeStage, playerGender);
        dailyCalorieTarget    = data.dailyCalorieTarget;
        movementDrainModifier = Mathf.Clamp(data.movementDrainMultiplier, 0.5f, 1.5f);
        moodDrainRate         = data.moodDrainMultiplier * 0.2f;
    }

    private PhaseModifierData GetPhaseModifier(AgeStage stage, Gender gender)
    {
        // Target harian mengacu AKG Indonesia 2019 (disederhanakan per fase simulasi).
        // Remaja: kebutuhan lebih tinggi; lansia: lebih rendah; perempuan umumnya < laki-laki.
        return (stage, gender) switch
        {
            (AgeStage.Youth,  Gender.Male)   => new PhaseModifierData { dailyCalorieTarget = 2600f, movementDrainMultiplier = 1.00f, moodDrainMultiplier = 1.00f },
            (AgeStage.Youth,  Gender.Female) => new PhaseModifierData { dailyCalorieTarget = 2100f, movementDrainMultiplier = 1.00f, moodDrainMultiplier = 1.00f },
            (AgeStage.Adult,  Gender.Male)   => new PhaseModifierData { dailyCalorieTarget = 2400f, movementDrainMultiplier = 1.05f, moodDrainMultiplier = 1.05f },
            (AgeStage.Adult,  Gender.Female) => new PhaseModifierData { dailyCalorieTarget = 1950f, movementDrainMultiplier = 1.05f, moodDrainMultiplier = 1.10f },
            (AgeStage.Senior, Gender.Male)   => new PhaseModifierData { dailyCalorieTarget = 2050f, movementDrainMultiplier = 1.20f, moodDrainMultiplier = 1.15f },
            (AgeStage.Senior, Gender.Female) => new PhaseModifierData { dailyCalorieTarget = 1700f, movementDrainMultiplier = 1.20f, moodDrainMultiplier = 1.30f },
            _                                => new PhaseModifierData { dailyCalorieTarget = 2000f, movementDrainMultiplier = 1.00f, moodDrainMultiplier = 1.00f },
        };
    }

    public bool SyncDayAndTryAdvanceAgeStage(int dayNumber, out AgeStage previousStage, out AgeStage newStage)
    {
        progressionDayCount = Mathf.Max(1, dayNumber);
        previousStage = currentAgeStage;
        newStage      = ResolveAgeStageForDay(progressionDayCount);

        if (newStage == previousStage)
            return false;

        currentAgeStage = newStage;

        if (healthScoreThisPhase > 70f)
            phaseCarryOverModifier = 1.1f;
        else if (healthScoreThisPhase < 40f)
            phaseCarryOverModifier = 0.9f;
        else
            phaseCarryOverModifier = 1.0f;

        maxEnergy = Mathf.Clamp(maxEnergy * phaseCarryOverModifier, 60f, 150f);

        int prevIndex = (int)previousStage;
        if (prevIndex >= 0 && prevIndex < committedPhaseScores.Length)
            committedPhaseScores[prevIndex] = healthScoreThisPhase;

        Debug.Log($"[PlayerStats] Committed phase score: {previousStage}={healthScoreThisPhase}");

        healthScoreThisPhase = 50f;

        SnapshotPhaseStartCounters();

        gymSkipStreak  = 0;
        workSkipStreak = 0;
        Debug.Log("[PlayerStats] Phase transition: counters snapshot taken, streaks reset.");

        ApplyPhaseModifiers();

        Debug.Log($"[PlayerStats] Phase transition {previousStage}→{newStage}, " +
                  $"score={healthScoreThisPhase}, modifier={phaseCarryOverModifier}, newMaxEnergy={maxEnergy}");

        ApplyPhaseWeightShift(previousStage, newStage, healthScoreThisPhase);

        OnAgeStageChanged?.Invoke(previousStage, newStage, progressionDayCount);
        return true;
    }

    public static string GetAgeStageLabelIndonesia(AgeStage stage)
    {
        return stage switch
        {
            AgeStage.Youth  => "Muda",
            AgeStage.Adult  => "Dewasa",
            AgeStage.Senior => "Lansia",
            _               => "Muda"
        };
    }

    // ── Called from home screen ──────────────────────────────
    public void SetPlayerData(string name, float height, float weight)
    {
        playerName   = name;
        playerHeight = height;
        playerWeight = weight;
        CalculateBMI();
    }

    /// <summary>
    /// Full gameplay reset for a new run. Profile name/height/weight/gender are reapplied after clearing session stats.
    /// </summary>
    public void ResetForNewSession(string name, float height, float weight, Gender gender)
    {
        maxEnergy = 100f;
        currentEnergy = 100f;
        currentEnergyState = EnergyState.Normal;
        faintTimer = 0f;
        runningDuration = 0f;
        faintRespawnHandled = false;
        vehicleKnockdownInProgress = false;

        totalCaloriesConsumed = 0f;
        dailyProtein = 0f;
        dailyFat = 0f;

        maxMood = 100f;
        currentMood = 50f;

        _money = 500;

        trainingAdaptation = 0f;
        fatigueDebt = 0f;
        healthScoreThisPhase = 50f;
        committedPhaseScores = new float[3] { 50f, 50f, 50f };
        phaseCarryOverModifier = 1f;

        totalDaysEvaluated = 0;
        poorDietDays = 0;
        noFoodDays = 0;
        highCalorieDays = 0;
        lowCalorieDays = 0;
        disturbedSleepDays = 0;
        lowEnergySleepDays = 0;
        skippedGymDays = 0;
        skippedWorkDays = 0;
        overworkedDays = 0;
        _visitedHospitalToday = false;

        gymSkipStreak = 0;
        workSkipStreak = 0;

        ClearPostActivityTravelGrace();

        progressionDayCount = 1;
        currentAgeStage = AgeStage.Youth;

        SnapshotPhaseStartCounters();

        SetPlayerData(name, height, weight);
        SetGender(gender);

        OnEnergyChanged?.Invoke(currentEnergy);
        OnMoodChanged?.Invoke(currentMood);
        OnCaloriesChanged?.Invoke(totalCaloriesConsumed);
        OnEnergyStateChanged?.Invoke(currentEnergyState);

        Debug.Log("[PlayerStats] Session reset for new run.");
    }

    public void AdjustWeight(float deltaKg, string reason)
    {
        if (Mathf.Abs(deltaKg) <= 0.0001f)
            return;

        float before = playerWeight;
        playerWeight = Mathf.Clamp(playerWeight + deltaKg, 35f, 200f);
        CalculateBMI();

        Debug.Log($"[PlayerStats] Weight change {before:0.0} -> {playerWeight:0.0} kg ({deltaKg:+0.0;-0.0;0.0}). {reason}");
    }

    private void ApplyPhaseWeightShift(AgeStage previousStage, AgeStage newStage, float phaseScore)
    {
        float delta;
        string reason;

        if (phaseScore < 40f)
        {
            delta = 3.5f;
            reason = $"phase_{previousStage}_to_{newStage} unhealthy";
        }
        else if (phaseScore < 60f)
        {
            delta = 1.5f;
            reason = $"phase_{previousStage}_to_{newStage} mixed";
        }
        else if (phaseScore > 75f)
        {
            delta = -2.0f;
            reason = $"phase_{previousStage}_to_{newStage} healthy";
        }
        else
        {
            delta = 0.5f;
            reason = $"phase_{previousStage}_to_{newStage} stable";
        }

        AdjustWeight(delta, reason);
    }

    // ── Called by PlayerController ───────────────────────────
    public float GetSpeedMultiplier()
    {
        return currentEnergyState switch
        {
            EnergyState.Warning  => 0.7f,
            EnergyState.Critical => 0.4f,
            EnergyState.Fainted  => 0f,
            _                    => 1f
        };
    }

    // ── Private helpers ──────────────────────────────────────
    void ModifyEnergy(float amount)
    {
        currentEnergy = Mathf.Clamp(currentEnergy + amount, 0f, maxEnergy);
        OnEnergyChanged?.Invoke(currentEnergy);
        UpdateEnergyState();
    }

    void ModifyMood(float amount)
    {
        currentMood = Mathf.Clamp(currentMood + amount, 0f, maxMood);
        OnMoodChanged?.Invoke(currentMood);
    }

    void DrainMoodOverTime()
    {
        ModifyMood(-moodDrainRate * Time.deltaTime);
    }

    void UpdateEnergyState()
    {
        EnergyState newState;
        if (currentEnergy <= 0)                          newState = EnergyState.Critical;
        else if (currentEnergy <= criticalThreshold)     newState = EnergyState.Critical;
        else if (currentEnergy <= warningThreshold)      newState = EnergyState.Warning;
        else                                             newState = EnergyState.Normal;

        if (newState != currentEnergyState)
        {
            if (currentEnergyState == EnergyState.Fainted && newState != EnergyState.Fainted)
                ReleaseFaintMovementLock();

            currentEnergyState = newState;
            OnEnergyStateChanged?.Invoke(currentEnergyState);
        }
    }

    void CheckFaintCondition()
    {
        if (currentEnergy <= 0)
        {
            faintTimer += Time.deltaTime;
            if (faintTimer >= faintDuration && !faintRespawnHandled)
                FaintAndRespawn();
        }
        else
        {
            faintTimer = 0f;
        }
    }

    public void FaintAndRespawn()
    {
        if (faintRespawnHandled || vehicleKnockdownInProgress)
            return;

        faintRespawnHandled = true;
        faintTimer = 0f;

        if (faintPresentationRoutine != null)
            StopCoroutine(faintPresentationRoutine);

        faintPresentationRoutine = StartCoroutine(FaintAndRespawnRoutine());
    }

    public void HandleVehicleKnockdown()
    {
        if (vehicleKnockdownInProgress || faintRespawnHandled)
            return;

        if (currentEnergyState == EnergyState.Fainted)
            return;

        vehicleKnockdownInProgress = true;

        if (faintPresentationRoutine != null)
            StopCoroutine(faintPresentationRoutine);

        faintPresentationRoutine = StartCoroutine(VehicleKnockdownRoutine());
    }

    private IEnumerator VehicleKnockdownRoutine()
    {
        yield return FadeToBlackWithTimeout(faintFadeOutDuration);

        DrainEnergy(vehicleKnockdownEnergyDrain);
        TeleportPlayerToRespawn();

        FaintNotificationController notificationPanel =
            FindFirstObjectByType<FaintNotificationController>(FindObjectsInactive.Include);

        if (notificationPanel != null)
            notificationPanel.ShowPanel(VehicleKnockdownTitle, VehicleKnockdownBody);
        else
            Debug.LogWarning("[PlayerStats] FaintNotificationController tidak ditemukan untuk notifikasi tabrak kendaraan.");

        yield return FadeFromBlackWithTimeout(faintFadeInDuration);

        vehicleKnockdownInProgress = false;
        faintPresentationRoutine = null;
    }

    private IEnumerator FaintAndRespawnRoutine()
    {
        yield return FadeToBlackWithTimeout(faintFadeOutDuration);

        currentEnergy = maxEnergy;
        OnEnergyChanged?.Invoke(currentEnergy);

        currentEnergyState = EnergyState.Fainted;
        OnEnergyStateChanged?.Invoke(currentEnergyState);

        TeleportPlayerToRespawn();
        OnPlayerFainted?.Invoke();

        FaintNotificationController faintPanel =
            FindFirstObjectByType<FaintNotificationController>(FindObjectsInactive.Include);

        if (faintPanel != null)
            faintPanel.ShowPanel();
        else
        {
            Debug.LogWarning("[PlayerStats] FaintNotificationController tidak ditemukan di scene.");
            ResetFaintState();
            faintPresentationRoutine = null;
            yield break;
        }

        yield return FadeFromBlackWithTimeout(faintFadeInDuration);

        faintPresentationRoutine = null;
    }

    private IEnumerator FadeToBlackWithTimeout(float duration)
    {
        if (FadeManager.Instance == null)
            yield break;

        bool fadeOutDone = false;
        FadeManager.Instance.FadeToBlack(duration, () => fadeOutDone = true);
        yield return WaitUntilOrTimeout(() => fadeOutDone, PresentationWaitTimeoutSeconds);
    }

    private IEnumerator FadeFromBlackWithTimeout(float duration)
    {
        if (FadeManager.Instance == null)
            yield break;

        bool fadeInDone = false;
        FadeManager.Instance.FadeFromBlack(duration, () => fadeInDone = true);
        yield return WaitUntilOrTimeout(() => fadeInDone, PresentationWaitTimeoutSeconds);
    }

    private static IEnumerator WaitUntilOrTimeout(System.Func<bool> predicate, float timeoutSeconds)
    {
        float elapsed = 0f;
        while (!predicate() && elapsed < timeoutSeconds)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    public void ResetFaintState()
    {
        bool wasFainted = currentEnergyState == EnergyState.Fainted;

        if (faintPresentationRoutine != null)
        {
            StopCoroutine(faintPresentationRoutine);
            faintPresentationRoutine = null;
        }

        faintRespawnHandled = false;
        faintTimer = 0f;
        vehicleKnockdownInProgress = false;

        if (wasFainted)
        {
            currentEnergyState = EnergyState.Normal;
            OnEnergyStateChanged?.Invoke(currentEnergyState);
        }

        ReleaseFaintMovementLock();
    }

    private static void ReleaseFaintMovementLock()
    {
        EnergySystem energySystem = Object.FindFirstObjectByType<EnergySystem>(FindObjectsInactive.Include);
        if (energySystem != null)
        {
            energySystem.CompleteFaintRecovery();
            return;
        }

        PlayerController player = Object.FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);
        if (player != null)
            player.ForceUnlockInput(EnergySystem.FaintLockKey);
    }

    static void TeleportPlayerToRespawn()
    {
        Transform respawnTransform = null;

        try
        {
            GameObject tagged = GameObject.FindGameObjectWithTag("Respawn");
            if (tagged != null)
                respawnTransform = tagged.transform;
        }
        catch (UnityException)
        {
            // Tag "Respawn" may not exist in the project yet.
        }

        if (respawnTransform == null)
        {
            GameObject mainSpawn = GameObject.Find("SpawnPoint (Main)");
            if (mainSpawn != null)
                respawnTransform = mainSpawn.transform;
        }

        if (respawnTransform == null)
        {
            GameObject spawnPoint = GameObject.Find("SpawnPoint");
            if (spawnPoint != null)
                respawnTransform = spawnPoint.transform;
        }

        if (respawnTransform == null)
        {
            Debug.LogWarning("[PlayerStats] Respawn point tidak ditemukan (tag Respawn / SpawnPoint (Main) / SpawnPoint).");
            return;
        }

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
        {
            Debug.LogWarning("[PlayerStats] Player tidak ditemukan untuk teleport respawn.");
            return;
        }

        player.transform.SetPositionAndRotation(respawnTransform.position, respawnTransform.rotation);

        Rigidbody body = player.GetComponent<Rigidbody>();
        if (body != null)
            body.linearVelocity = Vector3.zero;
    }

    void CalculateBMI()
    {
        if (playerHeight > 0)
        {
            float heightInMeters = playerHeight / 100f;
            playerBMI = playerWeight / (heightInMeters * heightInMeters);
        }
    }

    private void SyncAgeProgressionFromDay(int dayNumber)
    {
        progressionDayCount = Mathf.Max(1, dayNumber);
        currentAgeStage     = ResolveAgeStageForDay(progressionDayCount);
    }

    private void SnapshotPhaseStartCounters()
    {
        phaseStartDaysEvaluated     = totalDaysEvaluated;
        phaseStartPoorDietDays      = poorDietDays;
        phaseStartHighCalorieDays   = highCalorieDays;
        phaseStartSkippedGymDays    = skippedGymDays;
        phaseStartSkippedWorkDays   = skippedWorkDays;
        phaseStartOverworkedDays    = overworkedDays;
        phaseStartDisturbedSleepDays = disturbedSleepDays;
    }

    private static AgeStage ResolveAgeStageForDay(int dayNumber)
    {
        int safeDay = Mathf.Max(1, dayNumber);
        if (safeDay >= 10) return AgeStage.Senior;
        if (safeDay >= 5)  return AgeStage.Adult;
        return AgeStage.Youth;
    }
}