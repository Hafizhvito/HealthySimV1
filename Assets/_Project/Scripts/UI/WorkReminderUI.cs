using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class WorkReminderUI : MonoBehaviour
{
    [SerializeField] private float showDelaySeconds = 1.5f;
    [SerializeField] private float fadeDuration = 0.3f;
    [SerializeField] private string dailyHealthReminderText = "Jangan lupa gym dan makan sehat hari ini.";

    private const float ReminderTitleFontSize = 26f;
    private const float ReminderBodyFontSize = 20f;
    private const float ReminderWarningFontSize = 18f;
    private const float ReminderButtonFontSize = 20f;
    private const float ReminderPanelWidth = 520f;
    private const float StatusPanelWidth = 420f;
    private const float StatusPanelHeight = 132f;
    private const float StatusPanelTopPadding = 14f;
    private const float StatusLineHeight = 34f;
    private const float StatusPanelGapBelowCalories = 14f;

    private Canvas hudCanvas;
    private CanvasGroup reminderGroup;
    private RectTransform reminderPanel;
    private RectTransform hudStatusPanel;
    private TextMeshProUGUI timeText;
    private TextMeshProUGUI healthReminderText;
    private TextMeshProUGUI energyWarningText;
    private Button dismissButton;
    private TextMeshProUGUI hudWorkIndicator;
    private TextMeshProUGUI hudMoneyIndicator;
    private TextMeshProUGUI hudGymIndicator;
    private bool reminderHasShownOnce;
    private Coroutine resumeRoutine;
    private bool reminderSuppressedByTutorial;
    private bool reminderDismissed;

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    private void Start()
    {
        EnsureUi();
        StartCoroutine(ShowReminderDelayed());
    }

    private void Update()
    {
        EnsureUiAlive();
        HandleTutorialSuppression();
        UpdateHudWorkIndicator();
        UpdateHudMoneyIndicator();
        UpdateHudGymIndicator();
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        StartCoroutine(RebindUiNextFrame());
    }

    private IEnumerator RebindUiNextFrame()
    {
        yield return null;
        EnsureUi();
    }

    private void EnsureUiAlive()
    {
        bool missingUi = hudCanvas == null || hudWorkIndicator == null || hudMoneyIndicator == null || hudGymIndicator == null;
        if (missingUi)
            EnsureUi();
    }

    private IEnumerator ShowReminderDelayed()
    {
        yield return new WaitForSecondsRealtime(showDelaySeconds);

        while (IntroCutsceneController.IsAnyCutscenePlaying || TutorialSequentialUI.IsSequentialVisible)
            yield return null;

        if (WorkSessionManager.Instance != null && WorkSessionManager.Instance.HasWorkedToday)
            yield break;

        if (reminderDismissed)
            yield break;

        // Reset flag ini supaya HandleTutorialSuppression tidak re-show lagi
        reminderSuppressedByTutorial = false;
        reminderHasShownOnce = true;

        RefreshReminderContent();
        yield return StartCoroutine(FadePanel(1f, fadeDuration));
        SetReminderPanelInteractive(true);
    }

    private void SetReminderPanelInteractive(bool interactive)
    {
        if (reminderGroup == null)
            return;

        reminderGroup.interactable = interactive;
        reminderGroup.blocksRaycasts = interactive;
    }

    private void HandleTutorialSuppression()
    {
        if (reminderGroup == null)
            return;

        bool blockedByOverlay = IntroCutsceneController.IsAnyCutscenePlaying || TutorialSequentialUI.IsSequentialVisible;

        if (blockedByOverlay)
        {
            if (reminderGroup.alpha > 0.001f)
            {
                reminderGroup.alpha = 0f;
                reminderGroup.interactable = false;
                reminderGroup.blocksRaycasts = false;
                reminderSuppressedByTutorial = true;
                SetReminderPanelInteractive(false);
            }

            return;
        }

        if (!reminderSuppressedByTutorial)
            return;

        reminderSuppressedByTutorial = false;

        if (reminderDismissed)
            return;

        if (reminderHasShownOnce)
            return;

        if (WorkSessionManager.Instance != null && WorkSessionManager.Instance.HasWorkedToday)
            return;

        if (resumeRoutine != null)
            StopCoroutine(resumeRoutine);

        resumeRoutine = StartCoroutine(ResumeReminderAfterTutorial());
    }

    private IEnumerator ResumeReminderAfterTutorial()
    {
        while (IntroCutsceneController.IsAnyCutscenePlaying || TutorialSequentialUI.IsSequentialVisible)
            yield return null;

        if (reminderDismissed)
            yield break;

        if (WorkSessionManager.Instance != null && WorkSessionManager.Instance.HasWorkedToday)
            yield break;

        RefreshReminderContent();
        yield return StartCoroutine(FadePanel(1f, fadeDuration));
        SetReminderPanelInteractive(true);
        resumeRoutine = null;
    }

    private void EnsureUi()
    {
        if (hudCanvas == null)
            hudCanvas = FindHudCanvas();

        if (hudCanvas == null)
        {
            GameObject canvasObj = new GameObject("HUD_Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            hudCanvas = canvasObj.GetComponent<Canvas>();
            hudCanvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = canvasObj.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
        }

        if (reminderPanel == null)
        {
            Transform existingPanel = hudCanvas.transform.Find("WorkReminderPanel");
            if (existingPanel != null)
            {
                reminderPanel = existingPanel as RectTransform;
                reminderGroup = existingPanel.GetComponent<CanvasGroup>();
                if (timeText == null)
                    timeText = existingPanel.Find("TimeText")?.GetComponent<TextMeshProUGUI>();
                if (healthReminderText == null)
                    healthReminderText = existingPanel.Find("HealthReminderText")?.GetComponent<TextMeshProUGUI>();
                if (energyWarningText == null)
                    energyWarningText = existingPanel.Find("EnergyWarning")?.GetComponent<TextMeshProUGUI>();
                if (dismissButton == null)
                    dismissButton = existingPanel.Find("DismissButton")?.GetComponent<Button>();
                if (dismissButton != null)
                {
                    dismissButton.onClick.RemoveAllListeners();
                    dismissButton.onClick.AddListener(OnDismissClicked);
                }
            }
            else
            {
                CreateReminderPanel();
            }
        }

        if (hudStatusPanel == null)
        {
            Transform existingStatus = hudCanvas.transform.Find("WorkStatusPanel");
            if (existingStatus != null)
                hudStatusPanel = existingStatus as RectTransform;
            else
                CreateStatusPanel();
        }

        ConfigureStatusPanelLayout();

        if (hudWorkIndicator == null)
        {
            Transform existingWork = hudStatusPanel != null
                ? hudStatusPanel.Find("WorkHUDIndicator")
                : null;

            if (existingWork == null)
                existingWork = hudCanvas.transform.Find("WorkHUDIndicator");

            if (existingWork != null)
            {
                hudWorkIndicator = existingWork.GetComponent<TextMeshProUGUI>();
                if (hudStatusPanel != null && hudWorkIndicator != null)
                    hudWorkIndicator.rectTransform.SetParent(hudStatusPanel, false);
            }

            if (hudWorkIndicator == null)
                CreateWorkHudIndicator();
        }
        else
            ApplyReadableHudText(hudWorkIndicator);

        ConfigureWorkIndicatorLayout();

        if (hudMoneyIndicator == null)
        {
            Transform existingMoney = hudStatusPanel != null
                ? hudStatusPanel.Find("MoneyHUDIndicator")
                : null;

            if (existingMoney == null)
                existingMoney = hudCanvas.transform.Find("MoneyHUDIndicator");

            if (existingMoney != null)
            {
                hudMoneyIndicator = existingMoney.GetComponent<TextMeshProUGUI>();
                if (hudStatusPanel != null && hudMoneyIndicator != null)
                    hudMoneyIndicator.rectTransform.SetParent(hudStatusPanel, false);
            }

            if (hudMoneyIndicator == null)
                CreateMoneyHudIndicator();
        }
        else
            ApplyReadableHudText(hudMoneyIndicator);

        ConfigureMoneyIndicatorLayout();

        if (hudGymIndicator == null)
        {
            Transform existingGym = hudStatusPanel != null
                ? hudStatusPanel.Find("GymHUDIndicator")
                : null;

            if (existingGym == null)
                existingGym = hudCanvas.transform.Find("GymHUDIndicator");

            if (existingGym != null)
            {
                hudGymIndicator = existingGym.GetComponent<TextMeshProUGUI>();
                if (hudStatusPanel != null && hudGymIndicator != null)
                    hudGymIndicator.rectTransform.SetParent(hudStatusPanel, false);
            }

            if (hudGymIndicator == null)
                CreateGymHudIndicator();
        }
        else
            ApplyReadableHudText(hudGymIndicator);

        ConfigureGymIndicatorLayout();
        EnsureHealthReminderLine();
        ApplyReminderPanelTypography();
    }

    private void EnsureHealthReminderLine()
    {
        if (reminderPanel == null || healthReminderText != null)
            return;

        healthReminderText = CreateTmp(
            "HealthReminderText",
            reminderPanel,
            ReminderBodyFontSize,
            FontStyles.Bold,
            new Color32(200, 238, 210, 255));
        ApplyReadableHudText(healthReminderText);
        healthReminderText.outlineWidth = 0.18f;
        healthReminderText.alignment = TextAlignmentOptions.Center;
        healthReminderText.text = dailyHealthReminderText;
    }

    private void ApplyReminderPanelTypography()
    {
        TextMeshProUGUI title = reminderPanel != null
            ? reminderPanel.Find("Title")?.GetComponent<TextMeshProUGUI>()
            : null;
        if (title != null)
        {
            title.fontSize = ReminderTitleFontSize;
            title.fontStyle = FontStyles.Bold;
            ApplyReadableHudText(title);
        }

        if (timeText != null)
        {
            timeText.fontSize = ReminderBodyFontSize;
            timeText.fontStyle = FontStyles.Bold;
            ApplyReadableHudText(timeText);
        }

        if (healthReminderText != null)
        {
            healthReminderText.fontSize = ReminderBodyFontSize;
            healthReminderText.fontStyle = FontStyles.Bold;
            healthReminderText.color = new Color32(200, 238, 210, 255);
            ApplyReadableHudText(healthReminderText);
            healthReminderText.outlineWidth = 0.18f;
        }

        if (energyWarningText != null)
        {
            energyWarningText.fontSize = ReminderWarningFontSize;
            energyWarningText.fontStyle = FontStyles.Bold;
            ApplyReadableHudText(energyWarningText);
        }

        if (dismissButton != null)
        {
            TextMeshProUGUI label = dismissButton.transform.Find("Label")?.GetComponent<TextMeshProUGUI>();
            if (label != null)
            {
                label.fontSize = ReminderButtonFontSize;
                label.fontStyle = FontStyles.Bold;
            }
        }

        ConfigureReminderPanelLayout();
    }

    private void ConfigureReminderPanelLayout()
    {
        if (reminderPanel == null)
            return;

        bool showEnergyWarning = energyWarningText != null && energyWarningText.gameObject.activeSelf;
        float y = 14f;

        TextMeshProUGUI title = reminderPanel.Find("Title")?.GetComponent<TextMeshProUGUI>();
        if (title != null)
            SetReminderLineLayout(title.rectTransform, ref y, 34f);

        if (timeText != null)
        {
            y += 6f;
            SetReminderLineLayout(timeText.rectTransform, ref y, 32f);
        }

        if (healthReminderText != null && healthReminderText.gameObject.activeSelf)
        {
            y += 6f;
            SetReminderLineLayout(healthReminderText.rectTransform, ref y, 30f);
        }

        if (showEnergyWarning && energyWarningText != null)
        {
            y += 8f;
            SetReminderLineLayout(energyWarningText.rectTransform, ref y, 44f);
        }

        const float buttonHeight = 46f;
        const float bottomPad = 14f;
        reminderPanel.sizeDelta = new Vector2(ReminderPanelWidth, y + 10f + buttonHeight + bottomPad);

        if (dismissButton != null)
        {
            RectTransform buttonRect = dismissButton.GetComponent<RectTransform>();
            buttonRect.anchorMin = new Vector2(0.5f, 0f);
            buttonRect.anchorMax = new Vector2(0.5f, 0f);
            buttonRect.pivot = new Vector2(0.5f, 0f);
            buttonRect.anchoredPosition = new Vector2(0f, bottomPad);
            buttonRect.sizeDelta = new Vector2(220f, buttonHeight);
        }
    }

    private static void SetReminderLineLayout(RectTransform rect, ref float y, float height)
    {
        if (rect == null)
            return;

        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -y);
        rect.sizeDelta = new Vector2(480f, height);
        y += height;
    }

    private void CreateStatusPanel()
    {
        if (hudCanvas == null)
            return;

        GameObject panelObj = new GameObject("WorkStatusPanel", typeof(RectTransform), typeof(Image), typeof(Outline));
        hudStatusPanel = panelObj.GetComponent<RectTransform>();
        hudStatusPanel.SetParent(hudCanvas.transform, false);
        ConfigureStatusPanelLayout();
    }

    private void ConfigureStatusPanelLayout()
    {
        if (hudStatusPanel == null)
            return;

        // Align with energy/calorie bars: calories panel ends at -140 (pos -92, height 48).
        float panelTopY = -(92f + 48f + StatusPanelGapBelowCalories);

        hudStatusPanel.anchorMin = new Vector2(0f, 1f);
        hudStatusPanel.anchorMax = new Vector2(0f, 1f);
        hudStatusPanel.pivot = new Vector2(0f, 1f);
        hudStatusPanel.anchoredPosition = new Vector2(34f, panelTopY);
        hudStatusPanel.sizeDelta = new Vector2(StatusPanelWidth, StatusPanelHeight);

        Image bg = hudStatusPanel.GetComponent<Image>();
        if (bg == null)
            bg = hudStatusPanel.gameObject.AddComponent<Image>();

        bg.color = new Color32(10, 14, 20, 230);

        Outline outline = hudStatusPanel.GetComponent<Outline>();
        if (outline == null)
            outline = hudStatusPanel.gameObject.AddComponent<Outline>();

        outline.effectColor = new Color(1f, 1f, 1f, 0.16f);
        outline.effectDistance = new Vector2(1f, -1f);
    }

    private void CreateReminderPanel()
    {
        GameObject panelObj = new GameObject("WorkReminderPanel", typeof(RectTransform), typeof(Image), typeof(Outline), typeof(CanvasGroup));
        reminderPanel = panelObj.GetComponent<RectTransform>();
        reminderPanel.SetParent(hudCanvas.transform, false);
        reminderPanel.anchorMin = new Vector2(0.5f, 0.5f);
        reminderPanel.anchorMax = new Vector2(0.5f, 0.5f);
        reminderPanel.pivot = new Vector2(0.5f, 0.5f);
        reminderPanel.sizeDelta = new Vector2(520f, 260f);

        Image panelImage = panelObj.GetComponent<Image>();
        panelImage.color = new Color(20f / 255f, 20f / 255f, 20f / 255f, 180f / 255f);

        Outline outline = panelObj.GetComponent<Outline>();
        outline.effectColor = new Color(1f, 1f, 1f, 0.18f);
        outline.effectDistance = new Vector2(2f, -2f);

        reminderGroup = panelObj.GetComponent<CanvasGroup>();
        reminderGroup.alpha = 0f;
        reminderGroup.interactable = false;
        reminderGroup.blocksRaycasts = false;

        CreateReminderTexts(panelObj.transform);
        CreateDismissButton(panelObj.transform);
        ConfigureReminderPanelLayout();
    }

    private void CreateReminderTexts(Transform parent)
    {
        TextMeshProUGUI title = CreateTmp("Title", parent, ReminderTitleFontSize, FontStyles.Bold, new Color(1f, 232f / 255f, 160f / 255f, 1f));
        ApplyReadableHudText(title);
        title.text = "Hari Kerja";

        timeText = CreateTmp("TimeText", parent, ReminderBodyFontSize, FontStyles.Bold, Color.white);
        ApplyReadableHudText(timeText);

        healthReminderText = CreateTmp("HealthReminderText", parent, ReminderBodyFontSize, FontStyles.Bold, new Color32(200, 238, 210, 255));
        ApplyReadableHudText(healthReminderText);
        healthReminderText.outlineWidth = 0.18f;
        healthReminderText.alignment = TextAlignmentOptions.Center;
        healthReminderText.text = dailyHealthReminderText;

        energyWarningText = CreateTmp("EnergyWarning", parent, ReminderWarningFontSize, FontStyles.Bold, new Color(1f, 153f / 255f, 102f / 255f, 1f));
        ApplyReadableHudText(energyWarningText);
        energyWarningText.gameObject.SetActive(false);
    }

    private void CreateDismissButton(Transform parent)
    {
        GameObject btnObj = new GameObject("DismissButton", typeof(RectTransform), typeof(Image), typeof(Button));
        RectTransform rect = btnObj.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = new Vector2(0f, 14f);
        rect.sizeDelta = new Vector2(220f, 46f);

        Image img = btnObj.GetComponent<Image>();
        img.color = new Color(0.18f, 0.18f, 0.22f, 0.95f);

        dismissButton = btnObj.GetComponent<Button>();
        dismissButton.onClick.RemoveAllListeners();
        dismissButton.onClick.AddListener(OnDismissClicked);

        TextMeshProUGUI label = CreateTmp("Label", btnObj.transform, ReminderButtonFontSize, FontStyles.Bold, new Color(1f, 0.95f, 0.8f, 1f));
        label.text = "Oke, mengerti";
        RectTransform labelRect = label.rectTransform;
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;
    }

    private void CreateWorkHudIndicator()
    {
        Transform parent = hudStatusPanel != null ? hudStatusPanel : hudCanvas.transform;
        hudWorkIndicator = CreateTmp("WorkHUDIndicator", parent, 18f, FontStyles.Bold, new Color32(245, 248, 252, 255));
        ApplyReadableHudText(hudWorkIndicator);
        ConfigureWorkIndicatorLayout();
        hudWorkIndicator.text = "Kerja · " + GetWorkHoursText();
    }

    private void ConfigureWorkIndicatorLayout()
    {
        if (hudWorkIndicator == null)
            return;

        RectTransform rect = hudWorkIndicator.rectTransform;
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(14f, -StatusPanelTopPadding);
        rect.sizeDelta = new Vector2(-28f, StatusLineHeight);
        hudWorkIndicator.alignment = TextAlignmentOptions.TopLeft;
    }

    private void CreateMoneyHudIndicator()
    {
        Transform parent = hudStatusPanel != null ? hudStatusPanel : hudCanvas.transform;
        hudMoneyIndicator = CreateTmp("MoneyHUDIndicator", parent, 19f, FontStyles.Bold, new Color32(255, 232, 150, 255));
        ApplyReadableHudText(hudMoneyIndicator);
        ConfigureMoneyIndicatorLayout();
        hudMoneyIndicator.text = "Saldo · Rp0";
    }

    private void ConfigureMoneyIndicatorLayout()
    {
        if (hudMoneyIndicator == null)
            return;

        RectTransform rect = hudMoneyIndicator.rectTransform;
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(14f, -(StatusPanelTopPadding + StatusLineHeight * 2f));
        rect.sizeDelta = new Vector2(-28f, StatusLineHeight);
        hudMoneyIndicator.alignment = TextAlignmentOptions.TopLeft;
    }

    private void CreateGymHudIndicator()
    {
        Transform parent = hudStatusPanel != null ? hudStatusPanel : hudCanvas.transform;
        hudGymIndicator = CreateTmp("GymHUDIndicator", parent, 18f, FontStyles.Bold, new Color32(245, 248, 252, 255));
        ApplyReadableHudText(hudGymIndicator);
        ConfigureGymIndicatorLayout();
        hudGymIndicator.text = "Gym · " + GetGymHoursText();
    }

    private void ConfigureGymIndicatorLayout()
    {
        if (hudGymIndicator == null)
            return;

        RectTransform rect = hudGymIndicator.rectTransform;
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(14f, -(StatusPanelTopPadding + StatusLineHeight));
        rect.sizeDelta = new Vector2(-28f, StatusLineHeight);
        hudGymIndicator.alignment = TextAlignmentOptions.TopLeft;
    }

    private void UpdateHudWorkIndicator()
    {
        if (hudWorkIndicator == null)
            return;

        WorkSessionManager work = WorkSessionManager.Instance;
        if (work == null || !work.HasWorkedToday || work.LastSession == null)
        {
            hudWorkIndicator.color = new Color32(245, 248, 252, 255);
            hudWorkIndicator.text = "Kerja · " + GetWorkHoursText();
            return;
        }

        WorkResult result = work.LastSession.result;
        if (result == WorkResult.Full)
        {
            hudWorkIndicator.text = "Kerja · selesai";
            hudWorkIndicator.color = new Color(0.62f, 0.96f, 0.68f, 1f);
        }
        else if (result == WorkResult.Partial)
        {
            hudWorkIndicator.text = "Kerja · sebagian";
            hudWorkIndicator.color = new Color(1f, 0.78f, 0.42f, 1f);
        }
        else
        {
            hudWorkIndicator.text = "Kerja · gagal";
            hudWorkIndicator.color = new Color(1f, 0.58f, 0.58f, 1f);
        }
    }

    private void UpdateHudMoneyIndicator()
    {
        if (hudMoneyIndicator == null)
            return;

        int money = PlayerStats.Instance != null ? Mathf.Max(0, PlayerStats.Instance.Money) : 0;
        hudMoneyIndicator.text = string.Format("Saldo · Rp{0:N0}", money);
        hudMoneyIndicator.color = new Color32(255, 232, 150, 255);
    }

    private void UpdateHudGymIndicator()
    {
        if (hudGymIndicator == null)
            return;

        GymProgressionSystem gym = GymProgressionSystem.Instance;
        PlayerStats stats = PlayerStats.Instance;
        bool trainedToday = GymProgressionSystem.DidTrainToday(gym, stats);
        if (!trainedToday || gym == null || gym.LastSession == null)
        {
            if (trainedToday)
            {
                hudGymIndicator.text = "Gym · selesai";
                hudGymIndicator.color = new Color(0.62f, 0.96f, 0.68f, 1f);
                return;
            }

            hudGymIndicator.color = new Color32(245, 248, 252, 255);
            hudGymIndicator.text = "Gym · " + GetGymHoursText();
            return;
        }

        switch (gym.LastSession.result)
        {
            case GymSessionResult.Excellent:
            case GymSessionResult.Solid:
                hudGymIndicator.text = "Gym · selesai";
                hudGymIndicator.color = new Color(0.62f, 0.96f, 0.68f, 1f);
                break;
            case GymSessionResult.Strained:
                hudGymIndicator.text = "Gym · terforsir";
                hudGymIndicator.color = new Color(1f, 0.78f, 0.42f, 1f);
                break;
            default:
                hudGymIndicator.text = "Gym · gagal";
                hudGymIndicator.color = new Color(1f, 0.58f, 0.58f, 1f);
                break;
        }
    }

    private static void ApplyReadableHudText(TextMeshProUGUI text)
    {
        if (text == null)
            return;

        text.outlineColor = new Color(0f, 0f, 0f, 0.9f);
        text.outlineWidth = 0.16f;
    }

    private void RefreshReminderContent()
    {
        if (timeText != null)
            timeText.text = "Jam kerja hari ini: " + GetWorkHoursText();

        if (healthReminderText != null)
            healthReminderText.text = dailyHealthReminderText;

        float energy = PlayerStats.Instance != null ? PlayerStats.Instance.EnergyPercent : 1f;
        bool showWarning = energy < 0.60f;

        if (energyWarningText != null)
        {
            energyWarningText.gameObject.SetActive(showWarning);
            energyWarningText.text = "Energimu belum penuh — makan dulu sebelum kerja!";
        }

        ConfigureReminderPanelLayout();
    }

    private string GetWorkHoursText()
    {
        return FacilityHours.WorkHoursLabel;
    }

    private static string GetGymHoursText()
    {
        TimeManager.TimePeriod period = TimeManager.Instance != null
            ? TimeManager.Instance.CurrentPeriod
            : TimeManager.TimePeriod.Morning;

        if (period == TimeManager.TimePeriod.Night)
            return "Tutup (Malam)";

        return FacilityHours.GymHoursLabel;
    }

    private void OnDismissClicked()
    {
        reminderDismissed = true;

        if (resumeRoutine != null)
        {
            StopCoroutine(resumeRoutine);
            resumeRoutine = null;
        }

        SetReminderPanelInteractive(false);
        StartCoroutine(FadePanel(0f, fadeDuration));
    }

    private IEnumerator FadePanel(float target, float duration)
    {
        if (reminderGroup == null)
            yield break;

        float from = reminderGroup.alpha;
        float t = 0f;
        float d = Mathf.Max(0.01f, duration);

        while (t < d)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / d);
            reminderGroup.alpha = Mathf.Lerp(from, target, k);
            yield return null;
        }

        reminderGroup.alpha = target;

        if (target <= 0.001f)
            SetReminderPanelInteractive(false);
    }

    private static TextMeshProUGUI CreateTmp(string name, Transform parent, float fontSize, FontStyles style, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);

        TextMeshProUGUI tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.fontSize = fontSize;
        tmp.fontStyle = style;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.textWrappingMode = TextWrappingModes.Normal;
        tmp.outlineColor = new Color(0f, 0f, 0f, 200f / 255f);
        tmp.outlineWidth = 0.1f;
        return tmp;
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
