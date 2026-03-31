using System;

public enum WorkPeriod
{
    Pagi,
    Siang,
    Sore
}

public enum WorkResult
{
    Full,
    Partial,
    Failed
}

[Serializable]
public class WorkSessionData
{
    public WorkPeriod period;
    public int startHour;
    public int endHour;
    public float energyAtStart;
    public WorkResult result;
    public int moneyEarned;
    public float energyConsumed;
    public float completionRatio = 1f;
    public bool performanceDropped;
    public string performanceNote = string.Empty;
}
