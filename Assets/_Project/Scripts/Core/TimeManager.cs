using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DefaultExecutionOrder(-200)]
public class TimeManager : MonoBehaviour
{
    public static TimeManager Instance { get; private set; }

    private static readonly string[] NonGameplayScenes =
    {
        "MainMenu", "InputMenu", "LoadingScreen"
    };
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
    public float CurrentHour => Mathf.Clamp(6f + (TimePercent * 18f), 0f, 24f);
    public TimePeriod CurrentPeriod => currentPeriod;
    public bool IsRunning => isRunning;
    public int CurrentDayNumber => currentDayNumber;
    public int CurrentDayOfWeekIndex => currentDayOfWeekIndex;

    // Events
    public System.Action<TimePeriod> OnPeriodChanged;
    public System.Action OnGameTimeUp;
    public System.Action<int, string> OnDayChanged;

    [Header("Sleep Reminder")]
    [SerializeField] private float sleepReminderStartHour = 22f;
    [SerializeField] private float sleepReminderRepeatDelay = 60f;
    [SerializeField] private float sleepReminderFadeDuration = 0.3f;
    [SerializeField] private string sleepReminderMessage = "Sudah larut malam, waktunya istirahat!";

    private Canvas sleepReminderCanvas;
    private CanvasGroup sleepReminderGroup;
    private RectTransform sleepReminderPanel;
    private TextMeshProUGUI sleepReminderText;
    private Button sleepReminderDismissButton;
    private float sleepReminderNextShowTime;
    private bool sleepReminderShowing;
    private Coroutine sleepReminderFadeRoutine;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoEnsureForGameplayScene()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (string.IsNullOrEmpty(scene.name) || IsNonGameplayScene(scene.name))
            return;

        EnsureExists();
    }

    public static void EnsureExists()
    {
        if (Instance != null)
            return;

        TimeManager existing = FindFirstObjectByType<TimeManager>();
        if (existing != null)
            return;

        GameObject host = GameObject.Find("GameManager");
        if (host == null)
        {
            host = new GameObject("GameManager");
            Object.DontDestroyOnLoad(host);
        }
        else if (host.scene.name != "DontDestroyOnLoad")
        {
            Object.DontDestroyOnLoad(host.transform.root.gameObject);
        }

        host.AddComponent<TimeManager>();
        Debug.Log("[TimeManager] Runtime bootstrap created GameManager + TimeManager.");
    }

    private static bool IsNonGameplayScene(string sceneName)
    {
        for (int i = 0; i < NonGameplayScenes.Length; i++)
        {
            if (string.Equals(sceneName, NonGameplayScenes[i], System.StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        GameObject root = transform.root.gameObject;
        if (root.scene.name != "DontDestroyOnLoad")
            DontDestroyOnLoad(root);

        SyncPeriodThresholdsFromTotalDuration();
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
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
        HandleSleepReminder();

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
        ResetSleepReminderState();

        if (SessionTimeSkipPresenter.Instance != null)
            SessionTimeSkipPresenter.Instance.ClearPending();

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

        ResetSleepReminderState();
        OnDayChanged?.Invoke(currentDayNumber, GetDayNameIndonesia());
    }

    public void PauseTime() => isRunning = false;
    public void ResumeTime() => isRunning = true;

    public void SetTimeByHour(float hour)
    {
        float clampedHour = Mathf.Clamp(hour, 6f, 24f);
        float normalized = (clampedHour - 6f) / 18f;
        currentGameTime = totalGameDuration * normalized;
        CheckPeriodChange();
    }

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
        float hour = Mathf.Clamp(CurrentHour, 0f, 24f);
        int hourInt = Mathf.FloorToInt(hour);
        int minuteInt = Mathf.FloorToInt((hour - hourInt) * 60f);
        return string.Format("{0:00}:{1:00}", hourInt, minuteInt);
    }

    private void HandleSleepReminder()
    {
        if (CurrentHour < sleepReminderStartHour)
            return;

        if (sleepReminderShowing)
            return;

        if (Time.unscaledTime < sleepReminderNextShowTime)
            return;

        ShowSleepReminder();
    }

    private void ShowSleepReminder()
    {
        EnsureSleepReminderUi();
        if (sleepReminderGroup == null || sleepReminderText == null)
            return;

        sleepReminderText.text = sleepReminderMessage;
        sleepReminderGroup.interactable = true;
        sleepReminderGroup.blocksRaycasts = true;

        if (sleepReminderFadeRoutine != null)
            StopCoroutine(sleepReminderFadeRoutine);

        sleepReminderFadeRoutine = StartCoroutine(FadeSleepReminder(1f, sleepReminderFadeDuration));
        sleepReminderShowing = true;
    }

    private void HideSleepReminder()
    {
        if (sleepReminderGroup == null)
            return;

        sleepReminderGroup.interactable = false;
        sleepReminderGroup.blocksRaycasts = false;

        if (sleepReminderFadeRoutine != null)
            StopCoroutine(sleepReminderFadeRoutine);

        sleepReminderFadeRoutine = StartCoroutine(FadeSleepReminder(0f, sleepReminderFadeDuration));
        sleepReminderShowing = false;
    }

    private IEnumerator FadeSleepReminder(float target, float duration)
    {
        if (sleepReminderGroup == null)
            yield break;

        float from = sleepReminderGroup.alpha;
        float t = 0f;
        float d = Mathf.Max(0.01f, duration);

        while (t < d)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / d);
            sleepReminderGroup.alpha = Mathf.Lerp(from, target, k);
            yield return null;
        }

        sleepReminderGroup.alpha = target;
        sleepReminderFadeRoutine = null;
    }

    private void OnSleepReminderDismissed()
    {
        sleepReminderNextShowTime = Time.unscaledTime + Mathf.Max(1f, sleepReminderRepeatDelay);
        HideSleepReminder();
    }

    private void ResetSleepReminderState()
    {
        sleepReminderNextShowTime = 0f;
        sleepReminderShowing = false;

        if (sleepReminderGroup != null)
        {
            sleepReminderGroup.alpha = 0f;
            sleepReminderGroup.interactable = false;
            sleepReminderGroup.blocksRaycasts = false;
        }
    }

    private void EnsureSleepReminderUi()
    {
        if (sleepReminderCanvas == null)
            sleepReminderCanvas = FindHudCanvas();

        if (sleepReminderCanvas == null)
        {
            GameObject canvasObj = new GameObject("HUD_Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            sleepReminderCanvas = canvasObj.GetComponent<Canvas>();
            sleepReminderCanvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = canvasObj.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
        }

        if (sleepReminderPanel == null)
        {
            Transform existingPanel = sleepReminderCanvas.transform.Find("SleepReminderPanel");
            if (existingPanel != null)
            {
                sleepReminderPanel = existingPanel as RectTransform;
                sleepReminderGroup = existingPanel.GetComponent<CanvasGroup>();
                sleepReminderText = existingPanel.Find("ReminderText")?.GetComponent<TextMeshProUGUI>();
                sleepReminderDismissButton = existingPanel.Find("DismissButton")?.GetComponent<Button>();
            }
            else
            {
                CreateSleepReminderPanel();
            }
        }

        if (sleepReminderDismissButton != null)
        {
            sleepReminderDismissButton.onClick.RemoveAllListeners();
            sleepReminderDismissButton.onClick.AddListener(OnSleepReminderDismissed);
        }
    }

    private void CreateSleepReminderPanel()
    {
        GameObject panelObj = new GameObject("SleepReminderPanel", typeof(RectTransform), typeof(Image), typeof(Outline), typeof(CanvasGroup));
        sleepReminderPanel = panelObj.GetComponent<RectTransform>();
        sleepReminderPanel.SetParent(sleepReminderCanvas.transform, false);
        sleepReminderPanel.anchorMin = new Vector2(0.5f, 0.5f);
        sleepReminderPanel.anchorMax = new Vector2(0.5f, 0.5f);
        sleepReminderPanel.pivot = new Vector2(0.5f, 0.5f);
        sleepReminderPanel.sizeDelta = new Vector2(520f, 180f);

        Image panelImage = panelObj.GetComponent<Image>();
        panelImage.color = new Color(20f / 255f, 20f / 255f, 20f / 255f, 180f / 255f);

        Outline outline = panelObj.GetComponent<Outline>();
        outline.effectColor = new Color(1f, 1f, 1f, 0.18f);
        outline.effectDistance = new Vector2(2f, -2f);

        sleepReminderGroup = panelObj.GetComponent<CanvasGroup>();
        sleepReminderGroup.alpha = 0f;
        sleepReminderGroup.interactable = false;
        sleepReminderGroup.blocksRaycasts = false;

        CreateSleepReminderText(panelObj.transform);
        CreateSleepReminderDismissButton(panelObj.transform);
    }

    private void CreateSleepReminderText(Transform parent)
    {
        GameObject textObj = new GameObject("ReminderText", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObj.transform.SetParent(parent, false);

        sleepReminderText = textObj.GetComponent<TextMeshProUGUI>();
        sleepReminderText.fontSize = 18f;
        sleepReminderText.fontStyle = FontStyles.Bold;
        sleepReminderText.color = new Color(1f, 232f / 255f, 160f / 255f, 1f);
        sleepReminderText.alignment = TextAlignmentOptions.Center;
        sleepReminderText.textWrappingMode = TextWrappingModes.Normal;

        RectTransform textRect = sleepReminderText.rectTransform;
        textRect.anchorMin = new Vector2(0.1f, 0.5f);
        textRect.anchorMax = new Vector2(0.9f, 0.9f);
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
    }

    private void CreateSleepReminderDismissButton(Transform parent)
    {
        GameObject btnObj = new GameObject("DismissButton", typeof(RectTransform), typeof(Image), typeof(Button));
        RectTransform rect = btnObj.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = new Vector2(0f, 18f);
        rect.sizeDelta = new Vector2(180f, 40f);

        Image img = btnObj.GetComponent<Image>();
        img.color = new Color(0.18f, 0.18f, 0.22f, 0.95f);

        sleepReminderDismissButton = btnObj.GetComponent<Button>();
        sleepReminderDismissButton.onClick.RemoveAllListeners();
        sleepReminderDismissButton.onClick.AddListener(OnSleepReminderDismissed);

        GameObject labelObj = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelObj.transform.SetParent(btnObj.transform, false);
        TextMeshProUGUI label = labelObj.GetComponent<TextMeshProUGUI>();
        label.fontSize = 16f;
        label.fontStyle = FontStyles.Bold;
        label.color = new Color(1f, 0.95f, 0.8f, 1f);
        label.alignment = TextAlignmentOptions.Center;
        label.text = "Oke";

        RectTransform labelRect = label.rectTransform;
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;
    }

    private static Canvas FindHudCanvas()
    {
        Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < canvases.Length; i++)
        {
            if (canvases[i] != null && string.Equals(canvases[i].name, "HUD_Canvas", System.StringComparison.OrdinalIgnoreCase))
                return canvases[i];
        }

        return null;
    }
}
