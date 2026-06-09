using UnityEngine;

public enum GymTier
{
    Beginner,
    Regular,
    Advanced
}

public enum GymSessionResult
{
    Excellent,
    Solid,
    Strained,
    Failed
}

[System.Serializable]
public class GymSessionData
{
    public TimeManager.TimePeriod period;
    public int startHour;
    public int endHour;
    public float energyAtStart;
    public GymTier tierAtStart;
    public bool forceFaint;

    public float adaptationBefore;
    public float fatigueBefore;

    public GymSessionResult result;
    public float qualityScore;
    public float adaptationGain;
    public float fatigueGain;
    public float energyConsumed;
}

public class GymProgressionSystem : MonoBehaviour
{
    private static GymProgressionSystem _instance;
    public static GymProgressionSystem Instance => _instance;

    [Header("Training Rules")]
    [SerializeField] [Range(0f, 1f)] private float minEnergyToTrain = 0.15f;
    [SerializeField] [Range(0f, 1f)] private float baseEnergyCost = 0.18f;

    [Header("Progression Gain")]
    [SerializeField] private float baseAdaptationGain = 5f;
    [SerializeField] private float baseFatigueGain = 4f;

    [Header("Tier Thresholds")]
    [SerializeField] private float regularTierMinAdaptation = 25f;
    [SerializeField] private float advancedTierMinAdaptation = 55f;
    [SerializeField] private float regularTierMaxFatigue = 55f;
    [SerializeField] private float advancedTierMaxFatigue = 40f;

    [Header("Quality Thresholds")]
    [SerializeField] [Range(0f, 1f)] private float excellentThreshold = 0.72f;
    [SerializeField] [Range(0f, 1f)] private float strainedThreshold = 0.45f;

    [Header("Sleep Reset")]
    [SerializeField] private float sleepFatigueRecovery = 7.5f;
    [SerializeField] private float sleepAdaptationDecayWhenOverFatigued = 1.5f;
    [SerializeField] private float overFatigueDecayThreshold = 35f;

    public GymSessionData PendingSession { get; private set; }
    public GymSessionData LastSession { get; private set; }
    public bool HasTrainedToday { get; private set; }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public bool CanTrain(float energyNormalized, TimeManager.TimePeriod period)
    {
        if (HasTrainedToday)
            return false;

        if (period == TimeManager.TimePeriod.Night)
            return false;

        return Mathf.Clamp01(energyNormalized) >= minEnergyToTrain;
    }

    public GymSessionData BuildSession(TimeManager.TimePeriod period, float energyNormalized)
    {
        if (!CanTrain(energyNormalized, period))
            return null;

        PlayerStats stats = ResolvePlayerStats();
        float adaptation = stats != null ? stats.TrainingAdaptation : 0f;
        float fatigue = stats != null ? stats.FatigueDebt : 0f;

        GymSessionData data = new GymSessionData
        {
            period = period,
            startHour = ResolveStartHour(period),
            endHour = ResolveEndHour(period),
            energyAtStart = Mathf.Clamp01(energyNormalized),
            tierAtStart = EvaluateTier(adaptation, fatigue),
            adaptationBefore = adaptation,
            fatigueBefore = fatigue,
            result = GymSessionResult.Failed,
            forceFaint = false
        };

        PendingSession = data;
        return data;
    }

    public GymSessionData BuildFaintSession(TimeManager.TimePeriod period, float energyNormalized)
    {
        if (HasTrainedToday)
            return null;

        if (period == TimeManager.TimePeriod.Night)
            return null;

        PlayerStats stats = ResolvePlayerStats();
        float adaptation = stats != null ? stats.TrainingAdaptation : 0f;
        float fatigue = stats != null ? stats.FatigueDebt : 0f;

        GymSessionData data = new GymSessionData
        {
            period = period,
            startHour = ResolveStartHour(period),
            endHour = ResolveEndHour(period),
            energyAtStart = Mathf.Clamp01(energyNormalized),
            tierAtStart = EvaluateTier(adaptation, fatigue),
            adaptationBefore = adaptation,
            fatigueBefore = fatigue,
            result = GymSessionResult.Failed,
            forceFaint = true
        };

        PendingSession = data;
        return data;
    }

    public void CompleteFaintSession(GymSessionData data)
    {
        if (data == null)
            return;

        PendingSession = null;
        LastSession = data;
        HasTrainedToday = true;
    }

    public void ApplyResult(GymSessionData data)
    {
        if (data == null)
            return;

        PlayerStats stats = ResolvePlayerStats();
        if (stats == null)
        {
            PendingSession = null;
            LastSession = data;
            HasTrainedToday = true;
            return;
        }

        float adaptationNorm = Mathf.Clamp01(stats.TrainingAdaptation / 100f);
        float fatigueNorm = Mathf.Clamp01(stats.FatigueDebt / 100f);

        float quality = 0.55f * data.energyAtStart
            + 0.35f * adaptationNorm
            + 0.10f * (1f - fatigueNorm);
        quality = Mathf.Clamp01(quality);

        data.qualityScore = quality;

        float adaptationGain = baseAdaptationGain;
        float fatigueGain = baseFatigueGain;
        float energyCost = baseEnergyCost;

        switch (data.tierAtStart)
        {
            case GymTier.Beginner:
                adaptationGain *= 1.10f;
                fatigueGain *= 0.90f;
                break;
            case GymTier.Advanced:
                adaptationGain *= 0.90f;
                fatigueGain *= 1.15f;
                break;
        }

        if (quality >= excellentThreshold)
        {
            data.result = GymSessionResult.Excellent;
            adaptationGain *= 1.20f;
            fatigueGain *= 0.78f;
            energyCost *= 0.88f;
        }
        else if (quality < strainedThreshold)
        {
            data.result = GymSessionResult.Strained;
            adaptationGain *= 0.55f;
            fatigueGain *= 1.35f;
            energyCost *= 1.20f;
        }
        else
        {
            data.result = GymSessionResult.Solid;
            adaptationGain *= 0.95f;
            fatigueGain *= 1.00f;
        }

        data.adaptationGain = Mathf.Max(0f, adaptationGain);
        data.fatigueGain = Mathf.Max(0f, fatigueGain);
        data.energyConsumed = Mathf.Clamp01(energyCost);

        stats.ApplyGymProgression(data.adaptationGain, data.fatigueGain);
        stats.DrainEnergy(data.energyConsumed);

        PendingSession = null;
        LastSession = data;
        HasTrainedToday = true;
    }

    public void NotifyDayResetFromSleep()
    {
        HasTrainedToday = false;
        PendingSession = null;

        PlayerStats stats = ResolvePlayerStats();
        if (stats == null)
            return;

        float adaptationDecay = stats.FatigueDebt > overFatigueDecayThreshold
            ? sleepAdaptationDecayWhenOverFatigued
            : 0f;

        stats.ApplyGymProgression(-adaptationDecay, -sleepFatigueRecovery);
    }

    public void ResetForNewSession()
    {
        HasTrainedToday = false;
        PendingSession = null;
        LastSession = null;
    }

    public GymTier GetCurrentTier()
    {
        PlayerStats stats = ResolvePlayerStats();
        if (stats == null)
            return GymTier.Beginner;

        return EvaluateTier(stats.TrainingAdaptation, stats.FatigueDebt);
    }

    private GymTier EvaluateTier(float adaptation, float fatigue)
    {
        if (adaptation >= advancedTierMinAdaptation && fatigue <= advancedTierMaxFatigue)
            return GymTier.Advanced;

        if (adaptation >= regularTierMinAdaptation && fatigue <= regularTierMaxFatigue)
            return GymTier.Regular;

        return GymTier.Beginner;
    }

    private static PlayerStats ResolvePlayerStats()
    {
        if (PlayerStats.Instance != null)
            return PlayerStats.Instance;

        return FindFirstObjectByType<PlayerStats>();
    }

    private static int ResolveStartHour(TimeManager.TimePeriod period)
    {
        switch (period)
        {
            case TimeManager.TimePeriod.Morning:
                return 6;
            case TimeManager.TimePeriod.Afternoon:
                return 13;
            default:
                return 17;
        }
    }

    private static int ResolveEndHour(TimeManager.TimePeriod period)
    {
        switch (period)
        {
            case TimeManager.TimePeriod.Morning:
                return 8;
            case TimeManager.TimePeriod.Afternoon:
                return 15;
            default:
                return 19;
        }
    }
}
