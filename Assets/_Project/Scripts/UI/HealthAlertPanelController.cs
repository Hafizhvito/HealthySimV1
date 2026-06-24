using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One-time health notice before the !!! warning indicator unlocks.
/// </summary>
public class HealthAlertPanelController : MonoBehaviour
{
    public static HealthAlertPanelController Instance { get; private set; }

    private const string ModalKey = "HealthAlertNotice";
    private const float AlertThreshold = 40f;
    private const int NoticePanelSortingOrder = 850;

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
        return stats != null && stats.ShouldShowHealthGuidance(AlertThreshold);
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

        Canvas hudCanvas = FindHudCanvas();
        NarrativePanelUiFactory.BuiltPanel panel = NarrativePanelUiFactory.Create(hudCanvas, NoticePanelSortingOrder);
        if (panel.Root == null || panel.Body == null || panel.ContinueButton == null)
        {
            CleanupFailedShow();
            yield break;
        }

        panel.Root.gameObject.name = "HealthAlertNoticePanel";
        panel.Title.text = "Perhatian Kesehatan";
        panel.Body.text = BuildNoticeBody(PlayerStats.Instance);

        bool closed = false;
        panel.ContinueButton.onClick.RemoveAllListeners();
        panel.ContinueButton.onClick.AddListener(() => closed = true);

        yield return new WaitUntil(() => closed);

        if (panel.Group != null)
        {
            panel.Group.alpha = 0f;
            panel.Group.blocksRaycasts = false;
            panel.Group.interactable = false;
        }

        if (panel.Root != null)
            Destroy(panel.Root.gameObject);

        FinishNoticeAndUnlockIndicators();
        noticeAcknowledgedToday = true;
        isShowing = false;
    }

    private static void FinishNoticeAndUnlockIndicators()
    {
        if (ModalStateManager.Instance != null)
            ModalStateManager.Instance.ForceCloseModal(ModalKey);

        PlayerController player = Object.FindFirstObjectByType<PlayerController>();
        if (player != null)
            player.ForceUnlockInput(ModalKey);

        if (PlayerStats.Instance != null)
            PlayerStats.Instance.AcknowledgeHealthAlertNotice(AlertThreshold);

        MobileInputController.RequestGameplayTouchRecovery();
    }

    private void CleanupFailedShow()
    {
        if (ModalStateManager.Instance != null)
            ModalStateManager.Instance.ForceCloseModal(ModalKey);

        PlayerController player = Object.FindFirstObjectByType<PlayerController>();
        if (player != null)
            player.ForceUnlockInput(ModalKey);

        MobileInputController.RequestGameplayTouchRecovery();
        isShowing = false;
    }

    private static string BuildNoticeBody(PlayerStats stats)
    {
        if (stats == null)
            return "Kondisi kesehatanmu perlu diperhatikan.\n\nSegera kunjungi klinik dr. Sri untuk pemeriksaan.";

        if (stats.HealthScoreThisPhase < 30f)
            return "Kondisi kesehatanmu sedang buruk.\n\nSegera kunjungi klinik dr. Sri untuk pemeriksaan.";

        return "Kondisi kesehatanmu mulai menurun.\n\nPertimbangkan untuk berkunjung ke klinik dr. Sri.";
    }

    private static Canvas FindHudCanvas()
    {
        GameObject hud = GameObject.Find("HUD_Canvas");
        if (hud != null)
        {
            Canvas canvas = hud.GetComponent<Canvas>();
            if (canvas != null)
                return canvas;
        }

        Canvas[] all = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i].renderMode == RenderMode.ScreenSpaceOverlay)
                return all[i];
        }

        return null;
    }
}
