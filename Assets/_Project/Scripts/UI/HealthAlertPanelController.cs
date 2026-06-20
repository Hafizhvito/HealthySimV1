using System.Collections;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Shows a one-time notice when the player's health drops into the "!!!" alert zone.
/// Instructs the player to follow the arrow above their head to the clinic.
/// </summary>
public class HealthAlertPanelController : MonoBehaviour
{
    public static HealthAlertPanelController Instance { get; private set; }

    private const string ModalKey = "HealthAlertNotice";
    private const float AlertThreshold = 40f;

    [SerializeField] private float showDelaySeconds = 0.6f;

    private bool noticeAcknowledgedToday;
    private bool isShowing;
    private int trackedDay = -1;
    private Coroutine pendingShowRoutine;

    public static HealthAlertPanelController EnsureInstance()
    {
        if (Instance != null)
            return Instance;

        Instance = FindFirstObjectByType<HealthAlertPanelController>();
        if (Instance != null)
            return Instance;

        Instance = FindFirstObjectByType<HealthAlertPanelController>(FindObjectsInactive.Include);
        if (Instance != null)
            return Instance;

        GameObject obj = new GameObject("HealthAlertPanelController");
        Instance = obj.AddComponent<HealthAlertPanelController>();
        return Instance;
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    void OnEnable()
    {
        if (TimeManager.Instance != null)
            TimeManager.Instance.OnDayChanged += HandleDayChanged;
    }

    void OnDisable()
    {
        if (TimeManager.Instance != null)
            TimeManager.Instance.OnDayChanged -= HandleDayChanged;
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    void Update()
    {
        SyncDayTracking();

        if (isShowing || noticeAcknowledgedToday)
            return;

        if (!IsHealthAlertActive())
            return;

        if (pendingShowRoutine != null)
            return;

        if (!CanShowNow())
            return;

        pendingShowRoutine = StartCoroutine(ShowNoticeWhenReady());
    }

    private void SyncDayTracking()
    {
        int day = TimeManager.Instance != null ? TimeManager.Instance.CurrentDayNumber : -1;
        if (day == trackedDay)
            return;

        trackedDay = day;
        noticeAcknowledgedToday = false;
    }

    private void HandleDayChanged(int day, string _)
    {
        trackedDay = day;
        noticeAcknowledgedToday = false;
    }

    private static bool IsHealthAlertActive()
    {
        PlayerStats stats = PlayerStats.Instance;
        if (stats == null)
            return false;

        return stats.HealthScoreThisPhase < AlertThreshold && !stats.VisitedHospitalToday;
    }

    private static bool CanShowNow()
    {
        if (ModalStateManager.Instance != null && ModalStateManager.Instance.IsAnyModalOpen)
            return false;

        if (EndingManager.Instance != null && EndingManager.Instance.IsShowing)
            return false;

        if (NpcDialogueMenuController.Instance != null && NpcDialogueMenuController.Instance.IsOpen)
            return false;

        return true;
    }

    private IEnumerator ShowNoticeWhenReady()
    {
        yield return new WaitForSeconds(showDelaySeconds);

        while (!CanShowNow())
            yield return null;

        if (!IsHealthAlertActive() || noticeAcknowledgedToday)
        {
            pendingShowRoutine = null;
            yield break;
        }

        yield return ShowNoticePanel();
        pendingShowRoutine = null;
    }

    private IEnumerator ShowNoticePanel()
    {
        isShowing = true;

        if (ModalStateManager.Instance != null)
            ModalStateManager.Instance.OpenModal(ModalKey);

        RectTransform panelRoot = CreateNoticePanel(out TextMeshProUGUI bodyText, out Button continueBtn);
        if (panelRoot == null || bodyText == null || continueBtn == null)
        {
            CleanupFailedShow();
            yield break;
        }

        CanvasGroup group = panelRoot.GetComponent<CanvasGroup>();
        if (group == null)
        {
            Destroy(panelRoot.gameObject);
            CleanupFailedShow();
            yield break;
        }

        group.alpha = 1f;
        group.blocksRaycasts = true;
        group.interactable = true;
        panelRoot.gameObject.SetActive(true);
        bodyText.text = BuildNoticeBody(PlayerStats.Instance);

        bool closed = false;
        continueBtn.onClick.RemoveAllListeners();
        continueBtn.onClick.AddListener(() => closed = true);

        yield return new WaitUntil(() => closed);

        group.alpha = 0f;
        group.blocksRaycasts = false;
        group.interactable = false;
        Destroy(panelRoot.gameObject);

        if (ModalStateManager.Instance != null)
            ModalStateManager.Instance.CloseModal(ModalKey);

        noticeAcknowledgedToday = true;
        isShowing = false;
    }

    private void CleanupFailedShow()
    {
        if (ModalStateManager.Instance != null)
            ModalStateManager.Instance.CloseModal(ModalKey);

        isShowing = false;
    }

    private static string BuildNoticeBody(PlayerStats stats)
    {
        var sb = new StringBuilder();

        if (stats == null)
        {
            sb.AppendLine("Kondisi kesehatanmu perlu diperhatikan.");
            sb.AppendLine();
            sb.AppendLine("Ikuti panah biru di atas kepalamu menuju klinik, lalu periksakan diri ke dr. Sri.");
            return sb.ToString();
        }

        float score = stats.HealthScoreThisPhase;

        if (score < 30f)
            sb.AppendLine("Kondisi kesehatanmu sedang buruk. Tubuhmu memberi sinyal yang tidak boleh diabaikan.");
        else
            sb.AppendLine("Kondisi kesehatanmu mulai menurun. Kalau dibiarkan, risiko penyakit bisa meningkat.");

        sb.AppendLine();
        sb.AppendLine($"Skor kesehatan fase ini: {score:0}/100.");
        sb.AppendLine();

        string issues = BuildIssueSummary(stats);
        if (!string.IsNullOrEmpty(issues))
        {
            sb.AppendLine("Kemungkinan penyebab:");
            sb.AppendLine(issues);
            sb.AppendLine();
        }

        sb.AppendLine("Ikuti panah biru di atas kepalamu menuju klinik.");
        sb.AppendLine("Periksakan kondisimu ke dr. Sri sebelum hari ini berakhir.");

        return sb.ToString();
    }

    private static string BuildIssueSummary(PlayerStats stats)
    {
        var snap = stats.GetCurrentPhaseSnapshot();
        var lines = new StringBuilder();

        if (snap.poorDietDays >= 2)
            lines.AppendLine("\u2022 Pola makan belum seimbang");

        if (snap.highCalorieDays >= 2)
            lines.AppendLine("\u2022 Asupan kalori sering berlebihan");

        if (snap.skippedGymDays >= 2)
            lines.AppendLine("\u2022 Jarang berolahraga");

        if (snap.disturbedSleepDays >= 2)
            lines.AppendLine("\u2022 Tidur sering terganggu");

        if (snap.overworkedDays >= 2)
            lines.AppendLine("\u2022 Terlalu sering memaksakan kerja");

        if (stats.DailyFat > 65f)
            lines.AppendLine("\u2022 Lemak harian terlalu tinggi");

        if (stats.DailyProtein < 40f)
            lines.AppendLine("\u2022 Protein harian kurang");

        return lines.Length > 0 ? lines.ToString().TrimEnd() : string.Empty;
    }

    private static RectTransform CreateNoticePanel(out TextMeshProUGUI bodyText, out Button continueBtn)
    {
        bodyText = null;
        continueBtn = null;

        Canvas canvas = FindHudCanvas();
        if (canvas == null)
            return null;

        GameObject panelObj = new GameObject("HealthAlertNoticePanel", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
        RectTransform panelRect = panelObj.GetComponent<RectTransform>();
        panelRect.SetParent(canvas.transform, false);
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(500f, 320f);
        panelRect.anchoredPosition = Vector2.zero;
        panelRect.SetAsLastSibling();

        Image bg = panelObj.GetComponent<Image>();
        bg.color = new Color(0.98f, 0.96f, 0.94f, 0.98f);

        GameObject titleObj = new GameObject("TitleText", typeof(RectTransform), typeof(TextMeshProUGUI));
        RectTransform titleRect = titleObj.GetComponent<RectTransform>();
        titleRect.SetParent(panelRect, false);
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.anchoredPosition = new Vector2(0f, -12f);
        titleRect.sizeDelta = new Vector2(-32f, 36f);

        TextMeshProUGUI titleTmp = titleObj.GetComponent<TextMeshProUGUI>();
        titleTmp.text = "!!! Perhatian Kesehatan";
        titleTmp.fontSize = 20f;
        titleTmp.fontStyle = FontStyles.Bold;
        titleTmp.color = new Color(0.55f, 0.18f, 0.65f, 1f);
        titleTmp.alignment = TextAlignmentOptions.MidlineLeft;

        GameObject textObj = new GameObject("BodyText", typeof(RectTransform), typeof(TextMeshProUGUI));
        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.SetParent(panelRect, false);
        textRect.anchorMin = new Vector2(0f, 0f);
        textRect.anchorMax = new Vector2(1f, 1f);
        textRect.offsetMin = new Vector2(20f, 58f);
        textRect.offsetMax = new Vector2(-20f, -52f);

        bodyText = textObj.GetComponent<TextMeshProUGUI>();
        bodyText.fontSize = 15f;
        bodyText.color = new Color(0.18f, 0.22f, 0.24f, 1f);
        bodyText.alignment = TextAlignmentOptions.TopLeft;
        bodyText.textWrappingMode = TextWrappingModes.Normal;
        bodyText.lineSpacing = 2f;

        GameObject btnObj = new GameObject("ContinueButton", typeof(RectTransform), typeof(Image), typeof(Button));
        RectTransform btnRect = btnObj.GetComponent<RectTransform>();
        btnRect.SetParent(panelRect, false);
        btnRect.anchorMin = new Vector2(0.5f, 0f);
        btnRect.anchorMax = new Vector2(0.5f, 0f);
        btnRect.pivot = new Vector2(0.5f, 0f);
        btnRect.anchoredPosition = new Vector2(0f, 14f);
        btnRect.sizeDelta = new Vector2(260f, 36f);

        Image btnBg = btnObj.GetComponent<Image>();
        btnBg.color = new Color(0.55f, 0.22f, 0.62f, 1f);

        continueBtn = btnObj.GetComponent<Button>();

        GameObject btnTextObj = new GameObject("BtnText", typeof(RectTransform), typeof(TextMeshProUGUI));
        RectTransform btnTextRect = btnTextObj.GetComponent<RectTransform>();
        btnTextRect.SetParent(btnRect, false);
        btnTextRect.anchorMin = Vector2.zero;
        btnTextRect.anchorMax = Vector2.one;
        btnTextRect.offsetMin = Vector2.zero;
        btnTextRect.offsetMax = Vector2.zero;

        TextMeshProUGUI btnTmp = btnTextObj.GetComponent<TextMeshProUGUI>();
        btnTmp.text = "Mengerti, saya ke klinik";
        btnTmp.fontSize = 15f;
        btnTmp.color = Color.white;
        btnTmp.alignment = TextAlignmentOptions.Center;

        return panelRect;
    }

    private static Canvas FindHudCanvas()
    {
        GameObject hud = GameObject.Find("HUD_Canvas");
        if (hud != null)
        {
            Canvas c = hud.GetComponent<Canvas>();
            if (c != null)
                return c;
        }

        Canvas[] all = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i].renderMode == RenderMode.ScreenSpaceOverlay)
                return all[i];
        }

        return null;
    }
}
