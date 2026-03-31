using System;
using System.Collections.Generic;
using UnityEngine;

public class PlayerActionTracker : MonoBehaviour
{
    public enum ActionType
    {
        HealthyFoodTaken,
        UnhealthyFoodTaken,
        PositiveNpcTalk,
        NegativeNpcTalk,
        GenericInteraction
    }

    public enum BranchOutcome
    {
        HealthyPath,
        MixedPath,
        RiskyPath
    }

    public static PlayerActionTracker Instance { get; private set; }

    private readonly Dictionary<ActionType, int> counts = new Dictionary<ActionType, int>();
    private readonly Dictionary<TimeManager.TimePeriod, int> positiveByPeriod = new Dictionary<TimeManager.TimePeriod, int>();
    private readonly Dictionary<TimeManager.TimePeriod, int> negativeByPeriod = new Dictionary<TimeManager.TimePeriod, int>();

    private bool statsSubscribed;
    private bool timeSubscribed;

    private int warningEvents;
    private int criticalEvents;
    private int faintEvents;

    public event Action<ActionType, string> OnActionTracked;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        foreach (ActionType actionType in Enum.GetValues(typeof(ActionType)))
            counts[actionType] = 0;

        foreach (TimeManager.TimePeriod period in Enum.GetValues(typeof(TimeManager.TimePeriod)))
        {
            positiveByPeriod[period] = 0;
            negativeByPeriod[period] = 0;
        }
    }

    void Update()
    {
        TrySubscribe();
    }

    void OnDisable()
    {
        if (statsSubscribed && PlayerStats.Instance != null)
        {
            PlayerStats.Instance.OnEnergyStateChanged -= HandleEnergyStateChanged;
            PlayerStats.Instance.OnPlayerFainted -= HandlePlayerFainted;
        }

        if (timeSubscribed && TimeManager.Instance != null)
            TimeManager.Instance.OnPeriodChanged -= HandlePeriodChanged;
    }

    private void TrySubscribe()
    {
        if (!statsSubscribed && PlayerStats.Instance != null)
        {
            PlayerStats.Instance.OnEnergyStateChanged += HandleEnergyStateChanged;
            PlayerStats.Instance.OnPlayerFainted += HandlePlayerFainted;
            statsSubscribed = true;
        }

        if (!timeSubscribed && TimeManager.Instance != null)
        {
            TimeManager.Instance.OnPeriodChanged += HandlePeriodChanged;
            timeSubscribed = true;
        }
    }

    private void HandleEnergyStateChanged(PlayerStats.EnergyState state)
    {
        if (state == PlayerStats.EnergyState.Warning)
            warningEvents++;
        else if (state == PlayerStats.EnergyState.Critical)
            criticalEvents++;
    }

    private void HandlePlayerFainted()
    {
        faintEvents++;
    }

    private void HandlePeriodChanged(TimeManager.TimePeriod period)
    {
        Debug.Log($"[Tracker] Period changed to {period}");
    }

    public void Track(ActionType actionType, string sourceId)
    {
        counts[actionType]++;

        TimeManager.TimePeriod period = TimeManager.Instance != null
            ? TimeManager.Instance.CurrentPeriod
            : TimeManager.TimePeriod.Morning;

        if (IsPositive(actionType))
            positiveByPeriod[period]++;
        else if (IsNegative(actionType))
            negativeByPeriod[period]++;

        OnActionTracked?.Invoke(actionType, sourceId);
    }

    private bool IsPositive(ActionType actionType)
    {
        return actionType == ActionType.HealthyFoodTaken || actionType == ActionType.PositiveNpcTalk;
    }

    private bool IsNegative(ActionType actionType)
    {
        return actionType == ActionType.UnhealthyFoodTaken || actionType == ActionType.NegativeNpcTalk;
    }

    public int GetCount(ActionType actionType)
    {
        return counts.TryGetValue(actionType, out int value) ? value : 0;
    }

    public BranchOutcome EvaluateBranchOutcome()
    {
        int positive = GetCount(ActionType.HealthyFoodTaken) + GetCount(ActionType.PositiveNpcTalk);
        int negative = GetCount(ActionType.UnhealthyFoodTaken) + GetCount(ActionType.NegativeNpcTalk);
        int generic = GetCount(ActionType.GenericInteraction);

        int score = 0;
        score += positive * 3;
        score -= negative * 3;
        score += Mathf.Clamp(generic, 0, 5);
        score -= warningEvents;
        score -= criticalEvents * 2;
        score -= faintEvents * 4;

        // Period-window behavior markers.
        score += positiveByPeriod[TimeManager.TimePeriod.Morning] > 0 ? 1 : 0;
        score += positiveByPeriod[TimeManager.TimePeriod.Afternoon] > 0 ? 1 : 0;
        score -= negativeByPeriod[TimeManager.TimePeriod.Night] > 0 ? 1 : 0;

        Debug.Log($"[Tracker] score={score} pos={positive} neg={negative} generic={generic} warning={warningEvents} critical={criticalEvents} faint={faintEvents}");

        if (score >= 4)
            return BranchOutcome.HealthyPath;

        if (score <= -4)
            return BranchOutcome.RiskyPath;

        return BranchOutcome.MixedPath;
    }

    public string GetDebugSummary()
    {
        return $"pos={GetCount(ActionType.HealthyFoodTaken) + GetCount(ActionType.PositiveNpcTalk)}, neg={GetCount(ActionType.UnhealthyFoodTaken) + GetCount(ActionType.NegativeNpcTalk)}, warn={warningEvents}, critical={criticalEvents}, faint={faintEvents}";
    }
}
