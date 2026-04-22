using UnityEngine;

public class TimeManager : MonoBehaviour
{
    public static TimeManager Instance { get; private set; }
    private static readonly string[] DayNamesIndonesia =
    {
        "Senin", "Selasa", "Rabu", "Kamis", "Jumat", "Sabtu", "Minggu"
    };

    [Header("Time Settings")]
    [SerializeField] private float totalGameDuration = 230f; // 3 minutes 50 seconds
    private float currentGameTime = 0f;
    private bool isRunning = false;
    [SerializeField] private int currentDayNumber = 1;
    [SerializeField] [Range(0, 6)] private int currentDayOfWeekIndex = 0;

    public enum TimePeriod { Morning, Afternoon, Evening, Night }
    private TimePeriod currentPeriod = TimePeriod.Morning;

    // Time period thresholds (in seconds)
    private float morningEnd;
    private float afternoonEnd;
    private float eveningEnd;

    // Public getters
    public float CurrentTime => currentGameTime;
    public float TotalDuration => totalGameDuration;
    public float TimePercent => currentGameTime / totalGameDuration;
    public float TimeRemaining => totalGameDuration - currentGameTime;
    public TimePeriod CurrentPeriod => currentPeriod;
    public bool IsRunning => isRunning;
    public int CurrentDayNumber => currentDayNumber;
    public int CurrentDayOfWeekIndex => currentDayOfWeekIndex;

    // Events
    public System.Action<TimePeriod> OnPeriodChanged;
    public System.Action OnGameTimeUp;
    public System.Action<int, string> OnDayChanged;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        SyncPeriodThresholdsFromTotalDuration();
    }

    void OnValidate()
    {
        totalGameDuration = Mathf.Max(1f, totalGameDuration);
        SyncPeriodThresholdsFromTotalDuration();
    }

    void Start()
    {
        StartGame();
    }

    void Update()
    {
        if (!isRunning) return;

        currentGameTime += Time.deltaTime;
        CheckPeriodChange();

        if (currentGameTime >= totalGameDuration)
        {
            currentGameTime = totalGameDuration;
            isRunning = false;
            OnGameTimeUp?.Invoke();
        }
    }

    public void StartGame()
    {
        SyncPeriodThresholdsFromTotalDuration();
        currentGameTime = 0f;
        isRunning = true;
        currentPeriod = TimePeriod.Morning;
        currentDayNumber = 1;
        currentDayOfWeekIndex = 0;
        OnDayChanged?.Invoke(currentDayNumber, GetDayNameIndonesia());
    }

    public void AdvanceToNextDayFromSleep()
    {
        currentDayNumber = Mathf.Max(1, currentDayNumber + 1);
        currentDayOfWeekIndex = (currentDayOfWeekIndex + 1) % DayNamesIndonesia.Length;

        currentGameTime = 0f;
        isRunning = true;

        TimePeriod previousPeriod = currentPeriod;
        currentPeriod = TimePeriod.Morning;
        if (previousPeriod != currentPeriod)
            OnPeriodChanged?.Invoke(currentPeriod);

        OnDayChanged?.Invoke(currentDayNumber, GetDayNameIndonesia());
    }

    public void PauseTime() => isRunning = false;
    public void ResumeTime() => isRunning = true;

    void CheckPeriodChange()
    {
        TimePeriod newPeriod;
        if (currentGameTime < morningEnd) newPeriod = TimePeriod.Morning;
        else if (currentGameTime < afternoonEnd) newPeriod = TimePeriod.Afternoon;
        else if (currentGameTime < eveningEnd) newPeriod = TimePeriod.Evening;
        else newPeriod = TimePeriod.Night;

        if (newPeriod != currentPeriod)
        {
            currentPeriod = newPeriod;
            OnPeriodChanged?.Invoke(currentPeriod);
        }
    }

    private void SyncPeriodThresholdsFromTotalDuration()
    {
        // Keep 4 periods proportionally equal against total day duration.
        float quarter = totalGameDuration * 0.25f;
        morningEnd = quarter;
        afternoonEnd = quarter * 2f;
        eveningEnd = quarter * 3f;
    }

    // Returns time period name in Indonesian
    public string GetPeriodName()
    {
        return currentPeriod switch
        {
            TimePeriod.Morning => "Pagi",
            TimePeriod.Afternoon => "Siang",
            TimePeriod.Evening => "Sore",
            TimePeriod.Night => "Malam",
            _ => ""
        };
    }

    public string GetDayNameIndonesia()
    {
        int index = Mathf.Clamp(currentDayOfWeekIndex, 0, DayNamesIndonesia.Length - 1);
        return DayNamesIndonesia[index];
    }

    public string GetDayPeriodLabelIndonesia()
    {
        return $"Hari {GetDayNameIndonesia()} - {GetPeriodName()}";
    }

    // Returns formatted time remaining as MM:SS
    public string GetFormattedTimeRemaining()
    {
        int minutes = Mathf.FloorToInt(TimeRemaining / 60f);
        int seconds = Mathf.FloorToInt(TimeRemaining % 60f);
        return string.Format("{0:00}:{1:00}", minutes, seconds);
    }
}
