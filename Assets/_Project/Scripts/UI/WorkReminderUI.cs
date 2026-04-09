using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class WorkReminderUI : MonoBehaviour
{
    [SerializeField] private float showDelaySeconds = 1.5f;
    [SerializeField] private float autoDismissSeconds = 6f;
    [SerializeField] private float fadeDuration = 0.3f;

    private Canvas hudCanvas;
    private CanvasGroup reminderGroup;
    private RectTransform reminderPanel;
    private RectTransform hudStatusPanel;
    private TextMeshProUGUI timeText;
    private TextMeshProUGUI energyWarningText;
    private Button dismissButton;
    private TextMeshProUGUI hudWorkIndicator;
    private TextMeshProUGUI hudMoneyIndicator;

    private Coroutine dismissRoutine;
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
        bool missingUi = hudCanvas == null || hudWorkIndicator == null || hudMoneyIndicator == null;
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

        RefreshReminderContent();
        yield return StartCoroutine(FadePanel(1f, fadeDuration));

        if (dismissRoutine != null)
            StopCoroutine(dismissRoutine);
        dismissRoutine = StartCoroutine(AutoDismissAfterDelay());
    }

    private IEnumerator AutoDismissAfterDelay()
    {
        yield return new WaitForSecondsRealtime(autoDismissSeconds);
        yield return StartCoroutine(FadePanel(0f, fadeDuration));
        reminderDismissed = true;
        dismissRoutine = null;
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

                if (dismissRoutine != null)
                {
                    StopCoroutine(dismissRoutine);
                    dismissRoutine = null;
                }
            }

            return;
        }

        if (!reminderSuppressedByTutorial)
            return;

        reminderSuppressedByTutorial = false;

        if (reminderDismissed)
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

        if (dismissRoutine != null)
            StopCoroutine(dismissRoutine);

        dismissRoutine = StartCoroutine(AutoDismissAfterDelay());
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

        ConfigureMoneyIndicatorLayout();
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

        hudStatusPanel.anchorMin = new Vector2(0f, 1f);
        hudStatusPanel.anchorMax = new Vector2(0f, 1f);
        hudStatusPanel.pivot = new Vector2(0f, 1f);
        hudStatusPanel.anchoredPosition = new Vector2(40f, -124f);
        hudStatusPanel.sizeDelta = new Vector2(320f, 66f);

        Image bg = hudStatusPanel.GetComponent<Image>();
        if (bg == null)
            bg = hudStatusPanel.gameObject.AddComponent<Image>();

        bg.color = new Color(14f / 255f, 20f / 255f, 28f / 255f, 185f / 255f);

        Outline outline = hudStatusPanel.GetComponent<Outline>();
        if (outline == null)
            outline = hudStatusPanel.gameObject.AddComponent<Outline>();

        outline.effectColor = new Color(1f, 1f, 1f, 0.14f);
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
        reminderPanel.sizeDelta = new Vector2(480f, 200f);

        Image panelImage = panelObj.GetComponent<Image>();
        panelImage.color = new Color(20f / 255f, 20f / 255f, 20f / 255f, 180f / 255f);

        Outline outline = panelObj.GetComponent<Outline>();
        outline.effectColor = new Color(1f, 1f, 1f, 0.18f);
        outline.effectDistance = new Vector2(2f, -2f);

        reminderGroup = panelObj.GetComponent<CanvasGroup>();
        reminderGroup.alpha = 0f;

        CreateReminderTexts(panelObj.transform);
        CreateDismissButton(panelObj.transform);
    }

    private void CreateReminderTexts(Transform parent)
    {
        TextMeshProUGUI title = CreateTmp("Title", parent, 22f, FontStyles.Bold, new Color(1f, 232f / 255f, 160f / 255f, 1f));
        title.text = "Hari Kerja";
        RectTransform titleRect = title.rectTransform;
        titleRect.anchorMin = new Vector2(0.5f, 1f);
        titleRect.anchorMax = new Vector2(0.5f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.anchoredPosition = new Vector2(0f, -20f);
        titleRect.sizeDelta = new Vector2(420f, 32f);

        timeText = CreateTmp("TimeText", parent, 16f, FontStyles.Normal, Color.white);
        RectTransform timeRect = timeText.rectTransform;
        timeRect.anchorMin = new Vector2(0.5f, 1f);
        timeRect.anchorMax = new Vector2(0.5f, 1f);
        timeRect.pivot = new Vector2(0.5f, 1f);
        timeRect.anchoredPosition = new Vector2(0f, -70f);
        timeRect.sizeDelta = new Vector2(440f, 30f);

        energyWarningText = CreateTmp("EnergyWarning", parent, 14f, FontStyles.Normal, new Color(1f, 153f / 255f, 102f / 255f, 1f));
        RectTransform warningRect = energyWarningText.rectTransform;
        warningRect.anchorMin = new Vector2(0.5f, 1f);
        warningRect.anchorMax = new Vector2(0.5f, 1f);
        warningRect.pivot = new Vector2(0.5f, 1f);
        warningRect.anchoredPosition = new Vector2(0f, -105f);
        warningRect.sizeDelta = new Vector2(440f, 40f);
    }

    private void CreateDismissButton(Transform parent)
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

        dismissButton = btnObj.GetComponent<Button>();
        dismissButton.onClick.RemoveAllListeners();
        dismissButton.onClick.AddListener(OnDismissClicked);

        TextMeshProUGUI label = CreateTmp("Label", btnObj.transform, 16f, FontStyles.Bold, new Color(1f, 0.95f, 0.8f, 1f));
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
        hudWorkIndicator = CreateTmp("WorkHUDIndicator", parent, 15f, FontStyles.Bold, new Color32(189, 244, 194, 240));
        ConfigureWorkIndicatorLayout();
        hudWorkIndicator.text = "Status kerja: " + GetWorkHoursText();
    }

    private void ConfigureWorkIndicatorLayout()
    {
        if (hudWorkIndicator == null)
            return;

        RectTransform rect = hudWorkIndicator.rectTransform;
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(12f, -10f);
        rect.sizeDelta = new Vector2(-18f, 26f);
        hudWorkIndicator.alignment = TextAlignmentOptions.TopLeft;
    }

    private void CreateMoneyHudIndicator()
    {
        Transform parent = hudStatusPanel != null ? hudStatusPanel : hudCanvas.transform;
        hudMoneyIndicator = CreateTmp("MoneyHUDIndicator", parent, 16f, FontStyles.Bold, new Color32(255, 233, 150, 240));
        ConfigureMoneyIndicatorLayout();
        hudMoneyIndicator.text = "Saldo: Rp0";
    }

    private void ConfigureMoneyIndicatorLayout()
    {
        if (hudMoneyIndicator == null)
            return;

        RectTransform rect = hudMoneyIndicator.rectTransform;
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(12f, -36f);
        rect.sizeDelta = new Vector2(-18f, 26f);
        hudMoneyIndicator.alignment = TextAlignmentOptions.TopLeft;
    }

    private void UpdateHudWorkIndicator()
    {
        if (hudWorkIndicator == null)
            return;

        WorkSessionManager work = WorkSessionManager.Instance;
        if (work == null || !work.HasWorkedToday || work.LastSession == null)
        {
            hudWorkIndicator.color = new Color32(255, 232, 160, 220);
            hudWorkIndicator.text = "Status kerja: " + GetWorkHoursText();
            return;
        }

        WorkResult result = work.LastSession.result;
        if (result == WorkResult.Full)
        {
            hudWorkIndicator.text = "Status kerja: selesai";
            hudWorkIndicator.color = new Color(0.46f, 0.88f, 0.5f, 0.95f);
        }
        else if (result == WorkResult.Partial)
        {
            hudWorkIndicator.text = "Status kerja: sebagian";
            hudWorkIndicator.color = new Color(1f, 0.68f, 0.3f, 0.95f);
        }
        else
        {
            hudWorkIndicator.text = "Status kerja: gagal";
            hudWorkIndicator.color = new Color(1f, 0.45f, 0.45f, 0.95f);
        }
    }

    private void UpdateHudMoneyIndicator()
    {
        if (hudMoneyIndicator == null)
            return;

        int money = PlayerStats.Instance != null ? Mathf.Max(0, PlayerStats.Instance.Money) : 0;
        hudMoneyIndicator.text = string.Format("Saldo: Rp{0:N0}", money);
    }

    private void RefreshReminderContent()
    {
        if (timeText != null)
            timeText.text = "Jam kerja hari ini: " + GetWorkHoursText();

        float energy = PlayerStats.Instance != null ? PlayerStats.Instance.EnergyPercent : 1f;
        bool showWarning = energy < 0.60f;

        if (energyWarningText != null)
        {
            energyWarningText.gameObject.SetActive(showWarning);
            energyWarningText.text = "Energimu belum penuh — makan dulu sebelum kerja!";
        }
    }

    private string GetWorkHoursText()
    {
        TimeManager.TimePeriod period = TimeManager.Instance != null
            ? TimeManager.Instance.CurrentPeriod
            : TimeManager.TimePeriod.Morning;

        switch (period)
        {
            case TimeManager.TimePeriod.Morning:
                return "07.00 - 15.00";
            case TimeManager.TimePeriod.Afternoon:
                return "12.00 - 17.00";
            case TimeManager.TimePeriod.Evening:
                return "15.00 - 20.00";
            default:
                return "Tutup (Malam)";
        }
    }

    private void OnDismissClicked()
    {
        reminderDismissed = true;

        if (resumeRoutine != null)
        {
            StopCoroutine(resumeRoutine);
            resumeRoutine = null;
        }

        if (dismissRoutine != null)
            StopCoroutine(dismissRoutine);

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
