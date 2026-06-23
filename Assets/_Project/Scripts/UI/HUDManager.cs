using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

public class HUDManager : MonoBehaviour
{
    public static HUDManager Instance { get; private set; } // biar kagak perlu buat canvas lagi

    [Header("Settings HUD")]
    [SerializeField] private string[] _hideInScenes = { "MainMenu", "InputMenu", "LoadingScreen" };

    [Header("Energy Bar")]
    [SerializeField] private Image energyBarFill;
    [SerializeField] private Image energyBarWarningFill;
    [SerializeField] private Image energyBarChipFill;
    [SerializeField] private TextMeshProUGUI energyValueText;
    [SerializeField] private GameObject warningPanel;

    [Header("Mood Bar")]
    [SerializeField] private Image moodBarFill;
    [SerializeField] private Image moodBarChipFill;

    [Header("Drop FX")]
    [SerializeField] private float chipCatchupSpeed = 2.5f;
    [SerializeField] private float pulseDuration = 0.2f;
    [SerializeField] private float pulseScale = 1.05f;
    [SerializeField] private float pulseDropThreshold = 0.05f;

    [Header("Energy Bar Animation")]
    [SerializeField] private float energyFrontDrainSpeed = 1.8f;
    [SerializeField] private float energyFrontRecoverSpeed = 3.2f;
    [SerializeField] private float energyChipDelay = 0.1f;
    [SerializeField] private float energyChipDrainSpeed = 1.2f;

    [Header("Calories")]
    [SerializeField] private Image caloriesBarFill;
    [SerializeField] private TextMeshProUGUI caloriesText;

    [Header("Calories Bar Animation")]
    [SerializeField] private float caloriesLerpSpeed = 5f;
    [SerializeField] private float caloriesPulseHalfDuration = 0.1f;
    [SerializeField] private float caloriesPulseScale = 1.05f;
    [SerializeField] private float caloriesVisibleThreshold = 0.5f;
    [SerializeField] private Color caloriesLowColor = new Color(0.36f, 0.86f, 0.47f);
    [SerializeField] private Color caloriesMidColor = new Color(0.98f, 0.78f, 0.28f);
    [SerializeField] private Color caloriesHighColor = new Color(0.98f, 0.45f, 0.32f);

    [Header("Time")]
    [SerializeField] private TextMeshProUGUI periodText;
    [SerializeField] private TextMeshProUGUI timeRemainingText;

    [Header("Colors")]
    [SerializeField] private Color colorNormal = new Color(0.31f, 0.78f, 0.31f);
    [SerializeField] private Color colorWarning = new Color(1f, 0.65f, 0f);
    [SerializeField] private Color colorCritical = new Color(0.86f, 0.08f, 0.24f);

    private PlayerStats playerStats;
    private TimeManager timeManager;
    private bool warningVisible = false;
    private float warningBlinkTimer = 0f;
    private float energyVisualPercent = 1f;
    private float energyChipPercent = 1f;
    private float energyChipDelayTimer;
    private float moodChipPercent = 1f;
    private float energyPulseTimer;
    private float moodPulseTimer;
    private float previousEnergyPercent = 1f;
    private float previousMoodPercent = 1f;
    private float caloriesVisualPercent;
    private float previousCaloriesValue;
    private Vector3 energyBaseScale = Vector3.one;
    private Vector3 energyChipBaseScale = Vector3.one;
    private Vector3 moodBaseScale = Vector3.one;
    private Vector3 caloriesFillBaseScale = Vector3.one;
    private RectTransform caloriesPulseTarget;
    private Vector3 caloriesPulseBaseScale = Vector3.one;
    private Coroutine caloriesPulseRoutine;
    private bool energyFillPivotInitialized;
    private bool energyChipPivotInitialized;
    private bool caloriesFillPivotInitialized;

    // canvas
    private CanvasGroup canvasGroup;

    private void Awake()
    {
        // ================= Ini code singleton ==================
        /* Jadi jangan dihapus demi kesejahteraan semua scene yang membutuhkan HUDnya */
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        canvasGroup = GetComponent<CanvasGroup>();
        // =======================================================
    }

    // ================ HUD Logic ================================
    /* OnEnable dan OnDisable boleh dipake */
    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        bool shouldHide = System.Array.Exists(_hideInScenes, s => s == scene.name);

        SetHUDVisible(!shouldHide);
    }

    public void SetHUDVisible(bool visible)
    {
        if (canvasGroup == null) return;

        canvasGroup.alpha = visible ? 1f : 0f;
        canvasGroup.interactable = visible;
        canvasGroup.blocksRaycasts = visible;
    }

    /// <summary>
    /// Stops lagging energy bar animation (e.g. when a modal opens after walking).
    /// </summary>
    public void SnapEnergyVisualToActual()
    {
        if (playerStats == null)
            playerStats = PlayerStats.Instance;

        if (playerStats == null)
            return;

        float energyNormalized = Mathf.Clamp01(playerStats.EnergyPercent);
        energyVisualPercent = energyNormalized;
        energyChipPercent = energyNormalized;
        energyChipDelayTimer = 0f;
        previousEnergyPercent = energyNormalized;
    }
    // ===========================================================

    void Start()
    {
        // ================= Ini code singleton ==================
        /* Jadi jangan dihapus demi kesejahteraan semua scene yang membutuhkan HUDnya */
        // if (Instance != null && Instance != this)
        // {
        //     Destroy(gameObject);
        //     return;
        // }

        // Instance = this;
        // DontDestroyOnLoad(gameObject);
        // canvasGroup = GetComponent<CanvasGroup>();
        // =======================================================

        TryInitialize();

        // Initialize UI
        if (warningPanel != null)
            warningPanel.SetActive(false);

        if (energyBarChipFill == null)
            energyBarChipFill = energyBarWarningFill;

        if (energyBarFill != null)
            energyBaseScale = energyBarFill.rectTransform.localScale;

        if (moodBarFill != null)
            moodBaseScale = moodBarFill.rectTransform.localScale;

        if (energyBarChipFill != null)
            energyChipBaseScale = energyBarChipFill.rectTransform.localScale;

        float initialEnergy = 1f;
        if (playerStats != null)
            initialEnergy = Mathf.Clamp01(playerStats.EnergyPercent);
        else if (energyBarFill != null)
            initialEnergy = Mathf.Clamp01(energyBarFill.fillAmount);

        energyVisualPercent = initialEnergy;
        previousEnergyPercent = initialEnergy;

        previousCaloriesValue = playerStats != null ? Mathf.Max(0f, playerStats.TotalCalories) : 0f;
        caloriesVisualPercent = 0f;

        if (energyBarFill != null)
        {
            energyBarFill.fillAmount = initialEnergy;
            energyBarFill.color = colorNormal;
        }

        if (energyBarChipFill != null)
        {
            energyChipPercent = initialEnergy;
            energyBarChipFill.fillAmount = energyChipPercent;
            energyBarChipFill.color = new Color(1f, 1f, 1f, 0.30f);
        }

        if (moodBarChipFill != null)
        {
            moodChipPercent = moodBarFill != null ? moodBarFill.fillAmount : 1f;
            moodBarChipFill.fillAmount = moodChipPercent;
            moodBarChipFill.color = new Color(1f, 1f, 1f, 0.35f);
        }

        if (caloriesBarFill != null)
        {
            caloriesBarFill.type = Image.Type.Filled;
            caloriesBarFill.fillMethod = Image.FillMethod.Horizontal;
            caloriesBarFill.fillOrigin = 0;
            caloriesBarFill.fillAmount = 1f;
            SetCaloriesFillVisible(false);
            caloriesFillBaseScale = caloriesBarFill.rectTransform.localScale;
            EnsureCaloriesFillPivot();

            caloriesPulseTarget = caloriesBarFill.rectTransform;
            caloriesPulseBaseScale = caloriesPulseTarget.localScale;
        }
    }

    private void HandleAgeStageChanged(PlayerStats.AgeStage previous, PlayerStats.AgeStage current, int dayNumber)
    {
        // Target kalori berubah tiap fase — reset visual supaya proporsi bar tetap akurat.
        previousCaloriesValue = playerStats != null ? Mathf.Max(0f, playerStats.TotalCalories) : 0f;
        if (playerStats != null && playerStats.DailyCalorieTarget > 0f)
            caloriesVisualPercent = Mathf.Clamp01(previousCaloriesValue / playerStats.DailyCalorieTarget);
        else
            caloriesVisualPercent = 0f;
    }

    void TryInitialize()
    {
        if (playerStats != null)
        {
            // Already initialized
        }

        if (playerStats == null)
        {
            playerStats = PlayerStats.Instance;
            if (playerStats != null)
            {
                playerStats.OnEnergyStateChanged += HandleEnergyStateChange;
                playerStats.OnPlayerFainted += HandlePlayerFainted;
                playerStats.OnAgeStageChanged += HandleAgeStageChanged;
            }
        }

        if (timeManager == null)
        {
            TimeManager.EnsureExists();
            timeManager = TimeManager.Instance;
            if (timeManager == null)
                timeManager = FindFirstObjectByType<TimeManager>();

            if (timeManager != null)
            {
                timeManager.OnPeriodChanged += HandlePeriodChanged;
                timeManager.OnGameTimeUp += HandleGameTimeUp;
            }
        }

        EnsureTimeUiReferences();
    }

    private void EnsureTimeUiReferences()
    {
        if (timeRemainingText == null)
            timeRemainingText = FindTimeHudText("TimeRemaining_Text");

        if (periodText == null)
            periodText = FindTimeHudText("Period_Text");
    }

    private static TextMeshProUGUI FindTimeHudText(string childName)
    {
        GameObject canvasObj = GameObject.Find("HUD_Canvas");
        if (canvasObj == null)
            return null;

        Transform panel = canvasObj.transform.Find("Time_Panel");
        if (panel == null)
            return null;

        Transform text = panel.Find(childName);
        return text != null ? text.GetComponent<TextMeshProUGUI>() : null;
    }

    void Update()
    {
        TryInitialize();
        UpdateEnergyBar();
        UpdateMoodBar();
        UpdateCalories();
        UpdateTimeDisplay();
        UpdateWarningBlink();
    }

    void UpdateEnergyBar()
    {
        if (playerStats == null || energyBarFill == null) return;

        float energyNormalized = Mathf.Clamp01(playerStats.EnergyPercent);
        bool modalOpen = ModalStateManager.Instance != null && ModalStateManager.Instance.IsAnyModalOpen;
        bool isDraining;

        if (modalOpen)
        {
            energyVisualPercent = energyNormalized;
            energyChipPercent = energyNormalized;
            energyChipDelayTimer = 0f;
            isDraining = false;
        }
        else
        {
            if (energyNormalized < previousEnergyPercent - pulseDropThreshold)
                energyPulseTimer = pulseDuration;

            isDraining = energyNormalized < energyVisualPercent;
            float frontSpeed = isDraining ? energyFrontDrainSpeed : energyFrontRecoverSpeed;
            energyVisualPercent = Mathf.MoveTowards(
                energyVisualPercent,
                energyNormalized,
                Mathf.Max(0.01f, frontSpeed) * Time.deltaTime);
        }

        energyBarFill.type = Image.Type.Filled;
        energyBarFill.fillMethod = Image.FillMethod.Horizontal;
        energyBarFill.fillOrigin = 0;
        energyBarFill.fillAmount = energyVisualPercent;

        // Fallback visual: shrink like a sliding door from left to right,
        // so bar depletion remains visible even if Filled rendering is inconsistent.
        EnsureEnergyFillPivot();

        if (energyNormalized > 0.60f)
            energyBarFill.color = colorNormal;
        else if (energyNormalized > 0.30f)
            energyBarFill.color = colorWarning;
        else
            energyBarFill.color = colorCritical;

        if (energyValueText != null)
            energyValueText.text = $"{Mathf.RoundToInt(energyNormalized * 100f)}%";

        if (isDraining)
            energyChipDelayTimer = energyChipDelay;

        previousEnergyPercent = energyNormalized;

        if (energyBarChipFill != null)
        {
            if (energyChipPercent < energyVisualPercent)
                energyChipPercent = energyVisualPercent;
            else if (energyChipDelayTimer > 0f)
                energyChipDelayTimer -= Time.deltaTime;
            else if (energyChipPercent > energyVisualPercent)
                energyChipPercent = Mathf.MoveTowards(
                    energyChipPercent,
                    energyVisualPercent,
                    Mathf.Max(0.01f, energyChipDrainSpeed) * Time.deltaTime);

            energyBarChipFill.fillAmount = energyChipPercent;
            energyBarChipFill.type = Image.Type.Filled;
            energyBarChipFill.fillMethod = Image.FillMethod.Horizontal;
            energyBarChipFill.fillOrigin = 0;

            EnsureEnergyChipPivot();
            Vector3 chipScale = energyChipBaseScale;
            chipScale.x = Mathf.Clamp01(energyChipPercent);
            energyBarChipFill.rectTransform.localScale = chipScale;
        }

        float pulseFactor = 1f;
        if (energyPulseTimer > 0f)
        {
            energyPulseTimer -= Time.deltaTime;
            float t = 1f - (energyPulseTimer / pulseDuration);
            pulseFactor = 1f + (Mathf.Sin(t * Mathf.PI) * (pulseScale - 1f));
        }

        Vector3 finalEnergyScale = energyBaseScale * pulseFactor;
        finalEnergyScale.x *= Mathf.Clamp01(energyVisualPercent);
        energyBarFill.rectTransform.localScale = finalEnergyScale;
    }

    private void EnsureEnergyFillPivot()
    {
        if (energyFillPivotInitialized || energyBarFill == null)
            return;

        RectTransform rt = energyBarFill.rectTransform;
        rt.pivot = new Vector2(0f, 0.5f);
        energyFillPivotInitialized = true;
    }

    private void EnsureEnergyChipPivot()
    {
        if (energyChipPivotInitialized || energyBarChipFill == null)
            return;

        RectTransform rt = energyBarChipFill.rectTransform;
        rt.pivot = new Vector2(0f, 0.5f);
        energyChipPivotInitialized = true;
    }

    void UpdateMoodBar()
    {
        if (playerStats == null || moodBarFill == null) return;
        float percent = playerStats.MoodPercent;
        moodBarFill.fillAmount = percent;

        if (percent < previousMoodPercent - pulseDropThreshold)
            moodPulseTimer = pulseDuration;

        previousMoodPercent = percent;

        if (moodBarChipFill != null)
        {
            if (moodChipPercent > percent)
                moodChipPercent = Mathf.MoveTowards(moodChipPercent, percent, chipCatchupSpeed * Time.deltaTime);
            else
                moodChipPercent = percent;

            moodBarChipFill.fillAmount = moodChipPercent;
        }

        if (moodPulseTimer > 0f)
        {
            moodPulseTimer -= Time.deltaTime;
            float t = 1f - (moodPulseTimer / pulseDuration);
            float k = 1f + (Mathf.Sin(t * Mathf.PI) * ((pulseScale - 1f) * 0.8f));
            moodBarFill.rectTransform.localScale = moodBaseScale * k;
        }
        else
        {
            moodBarFill.rectTransform.localScale = moodBaseScale;
        }
    }

    void UpdateCalories()
    {
        if (playerStats == null)
            return;

        float maxCalories = ResolveCaloriesMax();
        float currentCalories = Mathf.Max(0f, playerStats.TotalCalories);
        float targetFillAmount = Mathf.Clamp01(currentCalories / maxCalories);
        bool hasCalories = currentCalories >= caloriesVisibleThreshold;

        caloriesVisualPercent = Mathf.Lerp(
            caloriesVisualPercent,
            hasCalories ? targetFillAmount : 0f,
            Mathf.Clamp01(Time.deltaTime * Mathf.Max(0.01f, caloriesLerpSpeed)));

        if (!hasCalories)
            caloriesVisualPercent = 0f;
        else if (Mathf.Abs(targetFillAmount - caloriesVisualPercent) < 0.0005f)
            caloriesVisualPercent = targetFillAmount;

        if (caloriesBarFill != null)
        {
            caloriesBarFill.type = Image.Type.Filled;
            caloriesBarFill.fillMethod = Image.FillMethod.Horizontal;
            caloriesBarFill.fillOrigin = 0;
            caloriesBarFill.fillAmount = 1f;

            EnsureCaloriesFillPivot();
            Vector3 fillScale = caloriesFillBaseScale;
            fillScale.x *= Mathf.Clamp01(caloriesVisualPercent);
            caloriesBarFill.rectTransform.localScale = fillScale;

            if (hasCalories)
            {
                caloriesBarFill.color = GetCaloriesColor(caloriesVisualPercent);
                SetCaloriesFillVisible(true);
            }
            else
            {
                fillScale.x = 0f;
                caloriesBarFill.rectTransform.localScale = fillScale;
                SetCaloriesFillVisible(false);
            }
        }

        if (currentCalories > previousCaloriesValue + 0.01f)
            TriggerCaloriesPulse();

        previousCaloriesValue = currentCalories;

        if (caloriesText != null)
        {
            caloriesText.text = string.Format(
                "Kalori {0:0} / {1:0} kcal",
                playerStats.TotalCalories,
                playerStats.DailyCalorieTarget
            );
            ApplyCaloriesTextStyle(hasCalories, caloriesVisualPercent);
        }
    }

    private void EnsureCaloriesFillPivot()
    {
        if (caloriesFillPivotInitialized || caloriesBarFill == null)
            return;

        caloriesBarFill.rectTransform.pivot = new Vector2(0f, 0.5f);
        caloriesFillPivotInitialized = true;
    }
    private void SetCaloriesFillVisible(bool visible)
    {
        if (caloriesBarFill == null)
            return;

        Color color = visible ? GetCaloriesColor(caloriesVisualPercent) : caloriesBarFill.color;
        color.a = visible ? 0.92f : 0f;
        caloriesBarFill.color = color;
    }

    private void ApplyCaloriesTextStyle(bool hasCalories, float fillPercent)
    {
        if (caloriesText == null)
            return;

        caloriesText.outlineColor = new Color(0f, 0f, 0f, 0.88f);
        caloriesText.outlineWidth = 0.16f;

        if (!hasCalories)
        {
            caloriesText.color = new Color(0.93f, 0.95f, 0.98f, 1f);
            return;
        }

        caloriesText.color = fillPercent > 0.45f
            ? new Color(0.08f, 0.1f, 0.14f, 1f)
            : new Color(0.98f, 0.99f, 1f, 1f);
    }

    private float ResolveCaloriesMax()
    {
        if (playerStats == null)
            return 1f;

        return Mathf.Max(1f, playerStats.DailyCalorieTarget);
    }

    private void TriggerCaloriesPulse()
    {
        if (caloriesPulseTarget == null)
            return;

        if (caloriesPulseRoutine != null)
            StopCoroutine(caloriesPulseRoutine);

        caloriesPulseRoutine = StartCoroutine(CaloriesPulseRoutine());
    }

    private IEnumerator CaloriesPulseRoutine()
    {
        if (caloriesPulseTarget == null)
            yield break;

        Vector3 baseScale = caloriesPulseBaseScale;
        float boost = Mathf.Max(1f, caloriesPulseScale);
        Vector3 peakScale = new Vector3(baseScale.x * boost, baseScale.y * boost, baseScale.z);
        float halfDuration = Mathf.Max(0.01f, caloriesPulseHalfDuration);

        float t = 0f;
        while (t < halfDuration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / halfDuration);
            caloriesPulseTarget.localScale = Vector3.Lerp(baseScale, peakScale, k);
            yield return null;
        }

        t = 0f;
        while (t < halfDuration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / halfDuration);
            caloriesPulseTarget.localScale = Vector3.Lerp(peakScale, baseScale, k);
            yield return null;
        }

        caloriesPulseTarget.localScale = baseScale;
        caloriesPulseRoutine = null;
    }

    private Color GetCaloriesColor(float percent)
    {
        float p = Mathf.Clamp01(percent);
        Color c;

        if (p <= 0.33f)
            c = Color.Lerp(caloriesLowColor, caloriesMidColor, Mathf.InverseLerp(0f, 0.33f, p));
        else if (p <= 0.66f)
            c = Color.Lerp(caloriesMidColor, caloriesHighColor, Mathf.InverseLerp(0.33f, 0.66f, p));
        else
            c = caloriesHighColor;

        c.a = 0.92f;
        return c;
    }

    void UpdateTimeDisplay()
    {
        EnsureTimeUiReferences();

        if (timeManager == null)
        {
            TimeManager.EnsureExists();
            timeManager = TimeManager.Instance;
        }

        if (timeManager == null || timeRemainingText == null)
            return;

        timeRemainingText.text = timeManager.GetFormattedTimeRemaining();

        if (periodText != null)
            periodText.text = timeManager.GetDayPeriodLabelIndonesia();
    }

    void UpdateWarningBlink()
    {
        if (!warningVisible || warningPanel == null) return;

        warningBlinkTimer += Time.deltaTime;
        if (warningBlinkTimer >= 0.5f)
        {
            warningBlinkTimer = 0f;
            warningPanel.SetActive(!warningPanel.activeSelf);
        }
    }

    void HandleEnergyStateChange(PlayerStats.EnergyState state)
    {
        switch (state)
        {
            case PlayerStats.EnergyState.Normal:
                warningVisible = false;
                if (warningPanel != null)
                    warningPanel.SetActive(false);
                break;
            case PlayerStats.EnergyState.Warning:
                warningVisible = true;
                if (warningPanel != null)
                {
                    var txt = warningPanel.GetComponentInChildren<TextMeshProUGUI>();
                    if (txt != null)
                        txt.text = "ENERGI RENDAH! Segera makan!";
                }
                break;
            case PlayerStats.EnergyState.Critical:
                warningVisible = true;
                if (warningPanel != null)
                {
                    var txt = warningPanel.GetComponentInChildren<TextMeshProUGUI>();
                    if (txt != null)
                        txt.text = "KRITIS! Kamu akan pingsan!";
                }
                break;
        }
    }

    void HandlePeriodChanged(TimeManager.TimePeriod period)
    {
        Debug.Log("Periode berubah ke: " + timeManager.GetPeriodName());
    }

    void HandlePlayerFainted()
    {
        warningVisible = false;
        if (warningPanel != null)
        {
            warningPanel.SetActive(true);
            var txt = warningPanel.GetComponentInChildren<TextMeshProUGUI>();
            if (txt != null)
                txt.text = "PINGSAN! Memulihkan energi...";
        }
    }

    void HandleGameTimeUp()
    {
        Debug.Log("Waktu habis! Game selesai.");
    }

    void OnDestroy()
    {
        if (playerStats != null)
        {
            playerStats.OnEnergyStateChanged -= HandleEnergyStateChange;
            playerStats.OnPlayerFainted -= HandlePlayerFainted;
            playerStats.OnAgeStageChanged -= HandleAgeStageChanged;
        }
        if (timeManager != null)
        {
            timeManager.OnPeriodChanged -= HandlePeriodChanged;
            timeManager.OnGameTimeUp -= HandleGameTimeUp;
        }
    }
}
