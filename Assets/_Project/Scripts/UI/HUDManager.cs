using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class HUDManager : MonoBehaviour
{
    [Header("Energy Bar")]
    [SerializeField] private Image energyBarFill;
    [SerializeField] private Image energyBarWarningFill;
    [SerializeField] private Image energyBarChipFill;
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
    [SerializeField] private TextMeshProUGUI caloriesText;

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
    private Vector3 energyBaseScale = Vector3.one;
    private Vector3 energyChipBaseScale = Vector3.one;
    private Vector3 moodBaseScale = Vector3.one;
    private bool energyFillPivotInitialized;
    private bool energyChipPivotInitialized;

    void Start()
    {
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
            }
        }

        if (timeManager == null)
        {
            timeManager = TimeManager.Instance;
            if (timeManager != null)
            {
                timeManager.OnPeriodChanged += HandlePeriodChanged;
                timeManager.OnGameTimeUp += HandleGameTimeUp;
            }
        }
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
        float targetPercent = energyNormalized;
        if (targetPercent < previousEnergyPercent - pulseDropThreshold)
            energyPulseTimer = pulseDuration;

        bool isDraining = targetPercent < energyVisualPercent;
        float frontSpeed = isDraining ? energyFrontDrainSpeed : energyFrontRecoverSpeed;
        energyVisualPercent = Mathf.MoveTowards(
            energyVisualPercent,
            targetPercent,
            Mathf.Max(0.01f, frontSpeed) * Time.deltaTime);

        energyBarFill.type = Image.Type.Filled;
        energyBarFill.fillMethod = Image.FillMethod.Horizontal;
        energyBarFill.fillOrigin = 0;
        energyBarFill.fillAmount = energyVisualPercent;

        // Fallback visual: shrink like a sliding door from left to right,
        // so bar depletion remains visible even if Filled rendering is inconsistent.
        EnsureEnergyFillPivot();

        if (energyNormalized > 0.60f)
            energyBarFill.color = new Color(0.4f, 0.9f, 0.4f);
        else if (energyNormalized > 0.30f)
            energyBarFill.color = new Color(1.0f, 0.85f, 0.1f);
        else
            energyBarFill.color = new Color(0.95f, 0.3f, 0.2f);

        if (isDraining)
            energyChipDelayTimer = energyChipDelay;

        previousEnergyPercent = targetPercent;

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
        if (playerStats == null || caloriesText == null) return;
        caloriesText.text = string.Format(
            "Kalori: {0:0} / {1:0} kcal",
            playerStats.TotalCalories,
            playerStats.DailyCalorieTarget
        );
    }

    void UpdateTimeDisplay()
    {
        if (timeManager == null) return;

        if (timeRemainingText != null)
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
        }
        if (timeManager != null)
        {
            timeManager.OnPeriodChanged -= HandlePeriodChanged;
            timeManager.OnGameTimeUp -= HandleGameTimeUp;
        }
    }
}
