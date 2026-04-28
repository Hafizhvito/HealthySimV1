using UnityEngine;

public class PlayerStats : MonoBehaviour
{
    public static PlayerStats Instance { get; private set; }

    [Header("Energy")]
    [SerializeField] private float maxEnergy = 100f;
    [SerializeField] private float currentEnergy = 100f;
    [SerializeField] private float energyDrainIdle = 0.2f;
    [SerializeField] private float energyDrainWalk = 0.45f;
    [SerializeField] private float energyDrainRun = 0.95f;
    [SerializeField] [Range(0.05f, 1f)] private float movementDrainScale = 0.40f;
    [SerializeField] [Range(0.5f, 1.5f)] private float movementDrainModifier = 1f;
    [SerializeField] private float runDrainRampSeconds = 1.2f;

    [Header("Calories")]
    [SerializeField] private float totalCaloriesConsumed = 0f;
    [SerializeField] private float dailyCalorieTarget = 2000f;

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
    [SerializeField] [HideInInspector] private float phaseCarryOverModifier = 1.0f;

    [Header("Age Progression (Hidden)")]
    [SerializeField] [HideInInspector] private int progressionDayCount = 1;
    [SerializeField] [HideInInspector] private AgeStage currentAgeStage = AgeStage.Youth;
    [SerializeField] private Gender playerGender = Gender.Male;

    [Header("Energy State Thresholds")]
    [SerializeField] private float warningThreshold = 40f;
    [SerializeField] private float criticalThreshold = 15f;

    public enum EnergyState { Normal, Warning, Critical, Fainted }
    public enum AgeStage { Youth, Adult, Senior }
    public enum Gender { Male, Female }
    private EnergyState currentEnergyState = EnergyState.Normal;
    private float faintTimer = 0f;
    private float faintDuration = 3f;
    private float runningDuration;

    // Public getters
    public float CurrentEnergy => currentEnergy;
    public float MaxEnergy => maxEnergy;
    public float EnergyPercent => currentEnergy / maxEnergy;
    public float CurrentMood => currentMood;
    public float MaxMood => maxMood;
    public float MoodPercent => currentMood / maxMood;
    public float TotalCalories => totalCaloriesConsumed;
    public float DailyCalorieTarget => dailyCalorieTarget;
    public EnergyState CurrentEnergyState => currentEnergyState;
    public string PlayerName => playerName;
    public float PlayerBMI => playerBMI;
    public Gender PlayerGender => playerGender;
    public int Money => _money;
    public float TrainingAdaptation => trainingAdaptation;
    public float FatigueDebt => fatigueDebt;
    public float HealthScoreThisPhase => healthScoreThisPhase;
    public float MovementDrainModifier => movementDrainModifier;
    public int ProgressionDayCount => progressionDayCount;
    public AgeStage CurrentAgeStage => currentAgeStage;

    // Events
    public System.Action<EnergyState> OnEnergyStateChanged;
    public System.Action OnPlayerFainted;
    public System.Action<float> OnEnergyChanged;
    public System.Action<float> OnMoodChanged;
    public System.Action<float> OnCaloriesChanged;
    public System.Action<AgeStage, AgeStage, int> OnAgeStageChanged;

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
        DrainMoodOverTime();
        CheckFaintCondition();
    }

    // Called by PlayerController every frame with current movement state
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
        float scaledDrain = drainRate * Mathf.Clamp(movementDrainScale, 0.05f, 1f) * drainModifier;

        ModifyEnergy(-(scaledDrain * deltaTime));
    }

    public void AddFood(float energyAmount, float calories, float moodEffect)
    {
        ModifyEnergy(energyAmount);
        totalCaloriesConsumed += calories;
        ModifyMood(moodEffect);
        OnCaloriesChanged?.Invoke(totalCaloriesConsumed);
    }

    public void ResetDailyCalories()
    {
        totalCaloriesConsumed = 0f;
        OnCaloriesChanged?.Invoke(totalCaloriesConsumed);
    }

    // Reduces current energy by normalized amount [0..1] of max energy.
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

    // Gym progression values are hidden gameplay stats used by gym systems.
    public void ApplyGymProgression(float adaptationDelta, float fatigueDelta)
    {
        trainingAdaptation = Mathf.Clamp(trainingAdaptation + adaptationDelta, 0f, 100f);
        fatigueDebt = Mathf.Clamp(fatigueDebt + fatigueDelta, 0f, 100f);
    }

    public void RegisterHealthScore(float delta)
    {
        healthScoreThisPhase = Mathf.Clamp(healthScoreThisPhase + delta, 0f, 100f);
    }

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
        dailyCalorieTarget = data.dailyCalorieTarget;
        movementDrainModifier = Mathf.Clamp(data.movementDrainMultiplier, 0.5f, 1.5f);
        moodDrainRate = data.moodDrainMultiplier * 0.2f;
        Debug.Log($"[PlayerStats] PhaseModifiers applied: stage={currentAgeStage} gender={playerGender} cal={dailyCalorieTarget} drain={movementDrainModifier} mood={moodDrainRate}");
    }

    private PhaseModifierData GetPhaseModifier(AgeStage stage, Gender gender)
    {
        return (stage, gender) switch
        {
            (AgeStage.Youth,  Gender.Male)   => new PhaseModifierData { dailyCalorieTarget = 2500f, movementDrainMultiplier = 1.00f, moodDrainMultiplier = 1.00f },
            (AgeStage.Youth,  Gender.Female) => new PhaseModifierData { dailyCalorieTarget = 2000f, movementDrainMultiplier = 1.00f, moodDrainMultiplier = 1.00f },
            (AgeStage.Adult,  Gender.Male)   => new PhaseModifierData { dailyCalorieTarget = 2300f, movementDrainMultiplier = 1.05f, moodDrainMultiplier = 1.05f },
            (AgeStage.Adult,  Gender.Female) => new PhaseModifierData { dailyCalorieTarget = 1900f, movementDrainMultiplier = 1.05f, moodDrainMultiplier = 1.10f },
            (AgeStage.Senior, Gender.Male)   => new PhaseModifierData { dailyCalorieTarget = 2000f, movementDrainMultiplier = 1.20f, moodDrainMultiplier = 1.15f },
            (AgeStage.Senior, Gender.Female) => new PhaseModifierData { dailyCalorieTarget = 1700f, movementDrainMultiplier = 1.20f, moodDrainMultiplier = 1.30f },
            _                                => new PhaseModifierData { dailyCalorieTarget = 2000f, movementDrainMultiplier = 1.00f, moodDrainMultiplier = 1.00f },
        };
    }

    public bool SyncDayAndTryAdvanceAgeStage(int dayNumber, out AgeStage previousStage, out AgeStage newStage)
    {
        progressionDayCount = Mathf.Max(1, dayNumber);
        previousStage = currentAgeStage;
        newStage = ResolveAgeStageForDay(progressionDayCount);

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
        healthScoreThisPhase = 50f;
        ApplyPhaseModifiers();

        Debug.Log($"[PlayerStats] Phase transition {previousStage}→{newStage}, score={healthScoreThisPhase}, modifier={phaseCarryOverModifier}, newMaxEnergy={maxEnergy}");
        OnAgeStageChanged?.Invoke(previousStage, newStage, progressionDayCount);
        return true;
    }

    public static string GetAgeStageLabelIndonesia(AgeStage stage)
    {
        return stage switch
        {
            AgeStage.Youth => "Muda",
            AgeStage.Adult => "Dewasa",
            AgeStage.Senior => "Lansia",
            _ => "Muda"
        };
    }

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
        if (currentEnergy <= 0) newState = EnergyState.Critical;
        else if (currentEnergy <= criticalThreshold) newState = EnergyState.Critical;
        else if (currentEnergy <= warningThreshold) newState = EnergyState.Warning;
        else newState = EnergyState.Normal;

        if (newState != currentEnergyState)
        {
            currentEnergyState = newState;
            OnEnergyStateChanged?.Invoke(currentEnergyState);
        }
    }

    void CheckFaintCondition()
    {
        if (currentEnergy <= 0)
        {
            faintTimer += Time.deltaTime;
            if (faintTimer >= faintDuration)
            {
                currentEnergyState = EnergyState.Fainted;
                OnPlayerFainted?.Invoke();
            }
        }
        else
        {
            faintTimer = 0f;
        }
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
        currentAgeStage = ResolveAgeStageForDay(progressionDayCount);
    }

    private static AgeStage ResolveAgeStageForDay(int dayNumber)
    {
        int safeDay = Mathf.Max(1, dayNumber);
        if (safeDay >= 10)
            return AgeStage.Senior;

        if (safeDay >= 5)
            return AgeStage.Adult;

        return AgeStage.Youth;
    }

    // Called from home screen to set player data before gameplay
    public void SetPlayerData(string name, float height, float weight)
    {
        playerName = name;
        playerHeight = height;
        playerWeight = weight;
        CalculateBMI();
    }

    // Called by PlayerController to adjust speed based on energy state
    public float GetSpeedMultiplier()
    {
        return currentEnergyState switch
        {
            EnergyState.Warning => 0.7f,
            EnergyState.Critical => 0.4f,
            EnergyState.Fainted => 0f,
            _ => 1f
        };
    }
}
