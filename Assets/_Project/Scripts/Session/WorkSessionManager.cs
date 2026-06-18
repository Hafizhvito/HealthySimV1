using UnityEngine;

public class WorkSessionManager : MonoBehaviour
{
    private static WorkSessionManager _instance;
    public static WorkSessionManager Instance => _instance;

    [Header("Base Pay")]
    [SerializeField] private int _basePayPagi = 200;
    [SerializeField] private int _basePaySiang = 150;
    [SerializeField] private int _basePaySore = 150;

    [Header("Energy Rules")]
    [SerializeField] private float _energyDrainFull = 0.35f;
    [SerializeField] private float _energyDrainPartial = 0.20f;
    [SerializeField] private float _energyThresholdFail = 0.30f;
    [SerializeField] private float _energyThresholdBonus = 0.60f;
    [SerializeField] private float _bonusMultiplier = 1.25f;

    private const float MinEnergyToWork = 0.10f;

    public event System.Action<WorkSessionData> OnWorkStartRequested;

    public WorkSessionData PendingSession { get; private set; }
    public WorkSessionData LastSession { get; private set; }
    public bool LastSessionHadBonus { get; private set; }
    public bool HasWorkedToday { get; private set; }

    private float _lastObservedGameTime = -1f;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);
        EnsureTimeSkipPresenter();
    }

    private static void EnsureTimeSkipPresenter()
    {
        if (SessionTimeSkipPresenter.Instance != null)
            return;

        GameObject presenterObj = new GameObject("SessionTimeSkipPresenter");
        presenterObj.AddComponent<SessionTimeSkipPresenter>();
    }

    private void Update()
    {
        // Reset daily marker when session timer restarts from zero/new run.
        if (TimeManager.Instance == null)
            return;

        float currentTime = TimeManager.Instance.CurrentTime;
        if (_lastObservedGameTime >= 0f && currentTime < _lastObservedGameTime)
            HasWorkedToday = false;

        _lastObservedGameTime = currentTime;
    }

    public WorkSessionData BuildSession(TimeManager.TimePeriod currentPeriod, float currentEnergyNormalized)
    {
        if (currentPeriod == TimeManager.TimePeriod.Night)
        {
            PendingSession = null;
            return null;
        }

        WorkSessionData data = new WorkSessionData
        {
            period = MapWorkPeriod(currentPeriod),
            energyAtStart = Mathf.Clamp01(currentEnergyNormalized)
        };

        switch (data.period)
        {
            case WorkPeriod.Pagi:
                data.startHour = 7;
                data.endHour = 15;
                break;
            case WorkPeriod.Siang:
                data.startHour = 12;
                data.endHour = 17;
                break;
            default:
                data.startHour = 15;
                data.endHour = 20;
                break;
        }

            PendingSession = data;
        return data;
    }

    public void ApplyResult(WorkSessionData data)
    {
        if (data == null)
            return;

        float energy = Mathf.Clamp01(data.energyAtStart);
        float completionRatio = Mathf.Clamp01(data.completionRatio <= 0f ? 1f : data.completionRatio);
        int basePay = GetBasePay(data.period);

        if (energy < MinEnergyToWork)
        {
            data.result = WorkResult.Failed;
            data.moneyEarned = 0;
            data.energyConsumed = 0f;
            data.completionRatio = 0f;
            LastSessionHadBonus = false;
        }
        else if (data.performanceDropped || completionRatio < 0.98f || energy < _energyThresholdFail)
        {
            data.result = WorkResult.Partial;

            float payFactor = Mathf.Clamp(completionRatio * 0.90f, 0.25f, 0.85f);
            data.moneyEarned = Mathf.RoundToInt(basePay * payFactor);
            data.energyConsumed = Mathf.Lerp(_energyDrainPartial, _energyDrainFull, Mathf.Clamp01(completionRatio));
            LastSessionHadBonus = false;
        }
        else
        {
            data.result = WorkResult.Full;
            bool hasBonus = energy > _energyThresholdBonus;
            LastSessionHadBonus = hasBonus;
            data.moneyEarned = hasBonus
                ? Mathf.RoundToInt(basePay * _bonusMultiplier)
                : basePay;
            data.energyConsumed = Mathf.Clamp01(_energyDrainFull);
        }

        PlayerStats stats = PlayerStats.Instance;
        if (stats == null)
            stats = FindFirstObjectByType<PlayerStats>();

        if (stats != null)
        {
            stats.AddMoney(data.moneyEarned);
            stats.DrainEnergy(data.energyConsumed);
        }
        else
        {
            Debug.LogWarning("[WorkSession] PlayerStats tidak ditemukan saat payout. Uang belum bisa diterapkan.");
        }

        PendingSession = null;
        LastSession = data;
        HasWorkedToday = true;
    }

    public static void SyncGameClockAfterWork(WorkSessionData data)
    {
        if (data == null || data.result == WorkResult.Failed || TimeManager.Instance == null)
            return;

        float targetHour = ResolveWorkEndHour(data);
        TimeManager.Instance.SetTimeByHour(targetHour);
        Debug.Log($"[WorkSession] Jam maju ke {targetHour:0.##} setelah kerja ({data.result}).");
    }

    public void PrepareWorkSessionOutcome(WorkSessionData data)
    {
        if (data == null)
            return;

        const float partialThreshold = 0.30f;
        const float severeDropThreshold = 0.20f;

        float energy = Mathf.Clamp01(data.energyAtStart);
        float completionRatio = 1f;
        bool performanceDrop = false;

        if (energy < partialThreshold)
        {
            float normalizedLowEnergy = Mathf.Clamp01(energy / Mathf.Max(0.0001f, partialThreshold));
            float minimumFinishRatio = energy < severeDropThreshold ? 0.25f : 0.40f;
            completionRatio = Mathf.Lerp(minimumFinishRatio, 0.75f, normalizedLowEnergy);
            performanceDrop = true;
        }

        data.completionRatio = completionRatio;
        data.performanceDropped = performanceDrop;
    }

    public void RequestWorkStartFromBossDialogue()
    {
        if (PendingSession == null)
            return;

        OnWorkStartRequested?.Invoke(PendingSession);
    }

    public bool CanWork(float energyNormalized)
    {
        if (HasWorkedToday) return false;
        
        float energy = Mathf.Clamp01(energyNormalized);
        if (energy < MinEnergyToWork)
            return false;

        if (TimeManager.Instance == null)
            return true;

        return TimeManager.Instance.CurrentPeriod != TimeManager.TimePeriod.Night;
    }

    public void NotifyDayResetFromSleep()
    {
        HasWorkedToday = false;
        PendingSession = null;
        _lastObservedGameTime = TimeManager.Instance != null ? TimeManager.Instance.CurrentTime : -1f;
    }

    public void ResetForNewSession()
    {
        HasWorkedToday = false;
        PendingSession = null;
        LastSession = null;
        LastSessionHadBonus = false;
        _lastObservedGameTime = -1f;
    }

    private static WorkPeriod MapWorkPeriod(TimeManager.TimePeriod source)
    {
        switch (source)
        {
            case TimeManager.TimePeriod.Morning:
                return WorkPeriod.Pagi;
            case TimeManager.TimePeriod.Afternoon:
                return WorkPeriod.Siang;
            default:
                return WorkPeriod.Sore;
        }
    }

    private int GetBasePay(WorkPeriod period)
    {
        switch (period)
        {
            case WorkPeriod.Pagi:
                return _basePayPagi;
            case WorkPeriod.Siang:
                return _basePaySiang;
            default:
                return _basePaySore;
        }
    }

    private static float ResolveWorkEndHour(WorkSessionData data)
    {
        if (data.result == WorkResult.Failed)
            return TimeManager.Instance != null ? TimeManager.Instance.CurrentHour : data.startHour;

        float shiftHours = Mathf.Max(0f, data.endHour - data.startHour);
        float ratio = data.result == WorkResult.Full && !data.performanceDropped
            ? 1f
            : Mathf.Clamp01(data.completionRatio);

        return data.startHour + (shiftHours * ratio);
    }
}
