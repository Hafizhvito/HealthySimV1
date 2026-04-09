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
    [SerializeField] [Range(0.75f, 1.25f)] private float movementDrainModifier = 1f;
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

    [Header("Energy State Thresholds")]
    [SerializeField] private float warningThreshold = 40f;
    [SerializeField] private float criticalThreshold = 15f;

    public enum EnergyState { Normal, Warning, Critical, Fainted }
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
    public int Money => _money;
    public float TrainingAdaptation => trainingAdaptation;
    public float FatigueDebt => fatigueDebt;
    public float MovementDrainModifier => movementDrainModifier;

    // Events
    public System.Action<EnergyState> OnEnergyStateChanged;
    public System.Action OnPlayerFainted;
    public System.Action<float> OnEnergyChanged;
    public System.Action<float> OnMoodChanged;
    public System.Action<float> OnCaloriesChanged;

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

    public void SetMovementDrainModifier(float value)
    {
        movementDrainModifier = Mathf.Clamp(value, 0.75f, 1.25f);
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
