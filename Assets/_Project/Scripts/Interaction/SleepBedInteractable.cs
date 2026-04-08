using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SleepBedInteractable : MonoBehaviour, IInteractable
{
    [Header("Prompt")]
    [SerializeField] private string interactionText = "Tekan E untuk tidur";
    [SerializeField] private string blockedBeforeNightText = "Belum malam. Kamu belum bisa tidur sekarang.";
    [SerializeField] private string blockedRepeatedText = "Kamu tidak bisa tidur terus-menerus. Lanjutkan aktivitasmu dulu sampai malam.";
    [SerializeField] private string sleepingLogText = "Kamu tidur nyenyak. Hari berganti ke pagi.";

    [Header("Blocked Sleep Warning")]
    [SerializeField] private float blockedWarningCooldown = 0.8f;
    [SerializeField] private float blockedBurstWindow = 4f;
    [SerializeField] private int blockedBurstThreshold = 3;

    [Header("Sleep Confirm UX")]
    [SerializeField] private bool requireConfirmBeforeSleep = true;
    [SerializeField] private float confirmWindowDuration = 2.2f;
    [SerializeField] private float confirmPromptDuration = 1.8f;
    [SerializeField] private string confirmPromptText = "Tekan E sekali lagi untuk tidur.";
    [SerializeField] private string confirmPromptWithRiskTemplate = "Tekan E sekali lagi untuk tidur. Risiko tidur terganggu: {0}%";

    [Header("Sleep Recovery")]
    [SerializeField] [Range(0f, 1f)] private float fullRecoveryNormalized = 1f;
    [SerializeField] [Range(0f, 1f)] private float lowEnergyWarningThreshold = 0.35f;

    [Header("Sleep Disturbance Chance")]
    [SerializeField] private bool enableSleepDisturbance = true;
    [SerializeField] [Range(0f, 1f)] private float baseDisturbChance = 0.05f;
    [SerializeField] [Range(0f, 1f)] private float chanceIfNoWork = 0.20f;
    [SerializeField] [Range(0f, 1f)] private float chanceIfOverwork = 0.25f;
    [SerializeField] [Range(0f, 1f)] private float chanceIfPoorDiet = 0.15f;
    [SerializeField] [Range(0f, 1f)] private float disturbedRecoveryNormalized = 0.65f;
    [SerializeField] private int poorDietThresholdDelta = 2;

    [Header("Transition")]
    [SerializeField] private bool requireNightToSleep = true;
    [SerializeField] private float clockSkipDuration = 2.8f;
    [SerializeField] private float fadeDuration = 0.35f;
    [SerializeField] private string sleepLockSource = "SleepTransition";

    [Header("Wake Reminder")]
    [SerializeField] private float wakeMessageDuration = 3f;
    [SerializeField] private string wakeIntroTemplate = "Kamu bangun di Hari {0}.";
    [SerializeField] private string wakeWarningNoWork = "Peringatan: Kemarin kamu belum kerja. Atur ritme harimu lebih baik.";
    [SerializeField] private string wakeWarningLowEnergy = "Peringatan: Kemarin kamu tidur saat energi sangat rendah.";
    [SerializeField] private string wakeWarningDisturbedSleep = "Peringatan: Tidurmu kurang nyenyak, jadi energimu belum pulih penuh.";

    [Header("Optional References")]
    [SerializeField] private Transform bedSpawnPoint;
    [SerializeField] private TimeManager timeManagerOverride;
    [SerializeField] private PlayerStats playerStatsOverride;
    [SerializeField] private FadeManager fadeManagerOverride;
    [SerializeField] private WorkSessionManager workSessionManagerOverride;
    [SerializeField] private GymProgressionSystem gymProgressionSystemOverride;
    [SerializeField] private ClockAnimationUI clockAnimationUiOverride;

    private Collider cachedCollider;
    private Coroutine sleepRoutine;
    private Canvas wakeCanvas;
    private CanvasGroup wakeCanvasGroup;
    private TextMeshProUGUI wakeText;
    private Coroutine wakeMessageRoutine;
    private float lastBlockedWarningTime = -999f;
    private float blockedWindowStartTime = -999f;
    private int blockedClickCount;
    private float confirmExpiresAt = -999f;

    private void Awake()
    {
        cachedCollider = GetComponent<Collider>();
        if (cachedCollider == null)
            cachedCollider = GetComponentInChildren<Collider>();
    }

    private void OnEnable()
    {
        InteractableRegistry.Register(this, cachedCollider, transform);
    }

    private void OnDisable()
    {
        InteractableRegistry.Unregister(this);
    }

    public string GetInteractionText()
    {
        if (!CanSleepNow())
            return blockedBeforeNightText;

        if (HasActiveSleepConfirm())
            return confirmPromptText;

        return interactionText;
    }

    public bool CanInteract(GameObject interactor)
    {
        return sleepRoutine == null;
    }

    public void Interact(GameObject interactor)
    {
        if (sleepRoutine != null)
            return;

        if (!CanSleepNow())
        {
            ResetSleepConfirm();
            HandleBlockedSleepAttempt();
            return;
        }

        if (ShouldRequestSleepConfirm())
        {
            RequestSleepConfirm();
            return;
        }

        ResetSleepConfirm();

        sleepRoutine = StartCoroutine(SleepRoutine(interactor));
    }

    private IEnumerator SleepRoutine(GameObject interactor)
    {
        TimeManager timeManager = ResolveTimeManager();
        PlayerStats playerStats = ResolvePlayerStats();
        FadeManager fadeManager = ResolveFadeManager();
        WorkSessionManager workSessionManager = ResolveWorkSessionManager();
        GymProgressionSystem gymProgressionSystem = ResolveGymProgressionSystem();
        ClockAnimationUI clockUi = ResolveClockAnimationUi();
        PlayerController playerController = interactor != null ? interactor.GetComponent<PlayerController>() : null;

        if (timeManager == null || playerStats == null)
        {
            Debug.LogWarning("[SleepBedInteractable] Sleep dibatalkan karena TimeManager/PlayerStats tidak tersedia.");
            sleepRoutine = null;
            yield break;
        }

        TimeManager.TimePeriod periodBeforeSleep = timeManager.CurrentPeriod;
        if (requireNightToSleep && periodBeforeSleep != TimeManager.TimePeriod.Night)
        {
            ShowWakeMessage(blockedBeforeNightText, 1.8f);
            sleepRoutine = null;
            yield break;
        }

        bool workedYesterday = workSessionManager != null && workSessionManager.HasWorkedToday;
        float energyBeforeSleep = playerStats.EnergyPercent;
        bool overworkedYesterday = workedYesterday && DidOverworkYesterday(workSessionManager);
        bool poorDietYesterday = HasPoorDietPattern();
        bool disturbedSleep = RollSleepDisturbance(workedYesterday, overworkedYesterday, poorDietYesterday);

        if (playerController != null)
            playerController.LockInput(sleepLockSource);

        if (fadeManager != null)
        {
            bool fadeToBlackDone = false;
            fadeManager.FadeToBlack(fadeDuration, () => fadeToBlackDone = true);
            yield return new WaitUntil(() => fadeToBlackDone);
        }

        if (clockUi != null)
        {
            bool clockDone = false;
            clockUi.PlayTimeSkipAnimation("Istirahat Malam", 21, 7, clockSkipDuration, () => clockDone = true);
            yield return new WaitUntil(() => clockDone);
        }

        ApplyRecovery(playerStats, disturbedSleep);

        timeManager.AdvanceToNextDayFromSleep();

        if (workSessionManager != null)
            workSessionManager.NotifyDayResetFromSleep();

        if (gymProgressionSystem != null)
            gymProgressionSystem.NotifyDayResetFromSleep();

        MoveInteractorToBedSpawn(interactor);
        Debug.Log($"[SleepBedInteractable] {sleepingLogText} Hari {timeManager.GetDayNameIndonesia()}.");

        if (fadeManager != null)
        {
            bool fadeFromBlackDone = false;
            fadeManager.FadeFromBlack(fadeDuration, () => fadeFromBlackDone = true);
            yield return new WaitUntil(() => fadeFromBlackDone);
        }

        if (playerController != null)
            playerController.UnlockInput(sleepLockSource);

        ShowWakeMessage(BuildWakeMessage(timeManager.GetDayNameIndonesia(), workedYesterday, energyBeforeSleep, disturbedSleep), wakeMessageDuration);

        sleepRoutine = null;
    }

    private void ApplyRecovery(PlayerStats stats, bool disturbedSleep)
    {
        float targetNormalized = Mathf.Clamp01(fullRecoveryNormalized);
        if (disturbedSleep)
            targetNormalized = Mathf.Min(targetNormalized, Mathf.Clamp01(disturbedRecoveryNormalized));

        float targetEnergy = stats.MaxEnergy * targetNormalized;
        float recovery = Mathf.Max(0f, targetEnergy - stats.CurrentEnergy);

        if (recovery > 0.01f)
            stats.AddFood(recovery, 0f, 0f);
    }

    private void MoveInteractorToBedSpawn(GameObject interactor)
    {
        if (interactor == null)
            return;

        Transform spawn = ResolveSpawnPoint();
        interactor.transform.position = spawn.position;
        interactor.transform.rotation = spawn.rotation;

        Rigidbody rb = interactor.GetComponent<Rigidbody>();
        if (rb != null)
            rb.linearVelocity = Vector3.zero;
    }

    private Transform ResolveSpawnPoint()
    {
        if (bedSpawnPoint != null)
            return bedSpawnPoint;

        Transform child = transform.Find("BedSpawnPoint");
        if (child != null)
            return child;

        return transform;
    }

    private TimeManager ResolveTimeManager()
    {
        if (timeManagerOverride != null)
            return timeManagerOverride;

        return TimeManager.Instance;
    }

    private PlayerStats ResolvePlayerStats()
    {
        if (playerStatsOverride != null)
            return playerStatsOverride;

        return PlayerStats.Instance;
    }

    private FadeManager ResolveFadeManager()
    {
        if (fadeManagerOverride != null)
            return fadeManagerOverride;

        if (FadeManager.Instance != null)
            return FadeManager.Instance;

        GameObject managerObj = GameObject.Find("GameManager");
        return managerObj != null ? managerObj.GetComponent<FadeManager>() : null;
    }

    private ClockAnimationUI ResolveClockAnimationUi()
    {
        if (clockAnimationUiOverride != null)
            return clockAnimationUiOverride;

        ClockAnimationUI existing = FindFirstObjectByType<ClockAnimationUI>();
        if (existing != null)
            return existing;

        GameObject clockObj = new GameObject("ClockAnimationUI");
        return clockObj.AddComponent<ClockAnimationUI>();
    }

    private WorkSessionManager ResolveWorkSessionManager()
    {
        if (workSessionManagerOverride != null)
            return workSessionManagerOverride;

        if (WorkSessionManager.Instance != null)
            return WorkSessionManager.Instance;

        GameObject managerObj = GameObject.Find("GameManager");
        return managerObj != null ? managerObj.GetComponent<WorkSessionManager>() : null;
    }

    private GymProgressionSystem ResolveGymProgressionSystem()
    {
        if (gymProgressionSystemOverride != null)
            return gymProgressionSystemOverride;

        if (GymProgressionSystem.Instance != null)
            return GymProgressionSystem.Instance;

        GameObject managerObj = GameObject.Find("GameManager");
        if (managerObj == null)
            return null;

        GymProgressionSystem existing = managerObj.GetComponent<GymProgressionSystem>();
        return existing != null ? existing : managerObj.AddComponent<GymProgressionSystem>();
    }

    private bool CanSleepNow()
    {
        TimeManager timeManager = ResolveTimeManager();
        if (timeManager == null)
            return false;

        if (!requireNightToSleep)
            return true;

        return timeManager.CurrentPeriod == TimeManager.TimePeriod.Night;
    }

    private void HandleBlockedSleepAttempt()
    {
        float now = Time.unscaledTime;
        if (now - blockedWindowStartTime > Mathf.Max(0.5f, blockedBurstWindow))
        {
            blockedWindowStartTime = now;
            blockedClickCount = 0;
        }

        blockedClickCount++;

        if (now - lastBlockedWarningTime < Mathf.Max(0.1f, blockedWarningCooldown))
            return;

        lastBlockedWarningTime = now;

        bool repeated = blockedClickCount >= Mathf.Max(2, blockedBurstThreshold);
        string message = repeated ? blockedRepeatedText : blockedBeforeNightText;
        float duration = repeated ? 2.4f : 1.8f;
        ShowWakeMessage(message, duration);
    }

    private bool DidOverworkYesterday(WorkSessionManager workSessionManager)
    {
        if (workSessionManager == null || workSessionManager.LastSession == null)
            return false;

        WorkSessionData session = workSessionManager.LastSession;
        bool lowStartEnergy = session.energyAtStart <= 0.25f;
        bool heavyDrainAtLowReserve = session.energyConsumed >= 0.32f && session.energyAtStart <= 0.50f;
        return session.result != WorkResult.Full || session.performanceDropped || lowStartEnergy || heavyDrainAtLowReserve;
    }

    private bool ShouldRequestSleepConfirm()
    {
        if (!requireConfirmBeforeSleep)
            return false;

        return !HasActiveSleepConfirm();
    }

    private bool HasActiveSleepConfirm()
    {
        return Time.unscaledTime <= confirmExpiresAt;
    }

    private void RequestSleepConfirm()
    {
        confirmExpiresAt = Time.unscaledTime + Mathf.Max(0.5f, confirmWindowDuration);

        string prompt = confirmPromptText;
        float chance = BuildCurrentDisturbanceChancePreview();
        if (enableSleepDisturbance)
        {
            int percentage = Mathf.RoundToInt(chance * 100f);
            prompt = string.Format(confirmPromptWithRiskTemplate, percentage);
        }

        ShowWakeMessage(prompt, Mathf.Max(0.5f, confirmPromptDuration));
    }

    private void ResetSleepConfirm()
    {
        confirmExpiresAt = -999f;
    }

    private bool HasPoorDietPattern()
    {
        if (PlayerActionTracker.Instance == null)
            return false;

        int healthy = PlayerActionTracker.Instance.GetCount(PlayerActionTracker.ActionType.HealthyFoodTaken);
        int unhealthy = PlayerActionTracker.Instance.GetCount(PlayerActionTracker.ActionType.UnhealthyFoodTaken);
        return unhealthy - healthy >= Mathf.Max(1, poorDietThresholdDelta);
    }

    private bool RollSleepDisturbance(bool workedYesterday, bool overworkedYesterday, bool poorDietYesterday)
    {
        if (!enableSleepDisturbance)
            return false;

        float chance = CalculateSleepDisturbanceChance(workedYesterday, overworkedYesterday, poorDietYesterday);
        return chance > 0f && UnityEngine.Random.value < chance;
    }

    private float BuildCurrentDisturbanceChancePreview()
    {
        WorkSessionManager workSessionManager = ResolveWorkSessionManager();
        bool workedYesterday = workSessionManager != null && workSessionManager.HasWorkedToday;
        bool overworkedYesterday = workedYesterday && DidOverworkYesterday(workSessionManager);
        bool poorDietYesterday = HasPoorDietPattern();
        return CalculateSleepDisturbanceChance(workedYesterday, overworkedYesterday, poorDietYesterday);
    }

    private float CalculateSleepDisturbanceChance(bool workedYesterday, bool overworkedYesterday, bool poorDietYesterday)
    {
        float chance = Mathf.Clamp01(baseDisturbChance);
        if (!workedYesterday)
            chance += chanceIfNoWork;

        if (overworkedYesterday)
            chance += chanceIfOverwork;

        if (poorDietYesterday)
            chance += chanceIfPoorDiet;

        return Mathf.Clamp01(chance);
    }

    private string BuildWakeMessage(string dayName, bool workedYesterday, float energyBeforeSleep, bool disturbedSleep)
    {
        string intro = string.Format(wakeIntroTemplate, dayName);
        bool lowEnergySleep = energyBeforeSleep <= lowEnergyWarningThreshold;

        System.Collections.Generic.List<string> warnings = new System.Collections.Generic.List<string>();

        if (!workedYesterday)
            warnings.Add(wakeWarningNoWork);

        if (lowEnergySleep)
            warnings.Add(wakeWarningLowEnergy);

        if (disturbedSleep)
            warnings.Add(wakeWarningDisturbedSleep);

        if (warnings.Count > 0)
            return intro + "\n" + string.Join("\n", warnings);

        return intro + "\nIstirahatmu cukup. Lanjutkan harimu dengan pilihan sehat.";
    }

    private void ShowWakeMessage(string message, float duration)
    {
        EnsureWakeUi();
        if (wakeText == null || wakeCanvasGroup == null)
            return;

        if (wakeMessageRoutine != null)
            StopCoroutine(wakeMessageRoutine);

        wakeMessageRoutine = StartCoroutine(ShowWakeMessageRoutine(message, duration));
    }

    private IEnumerator ShowWakeMessageRoutine(string message, float duration)
    {
        wakeText.text = message;
        wakeCanvasGroup.alpha = 1f;
        wakeCanvasGroup.blocksRaycasts = false;
        yield return new WaitForSecondsRealtime(Mathf.Max(0.5f, duration));
        wakeCanvasGroup.alpha = 0f;
        wakeMessageRoutine = null;
    }

    private void EnsureWakeUi()
    {
        if (wakeCanvas != null && wakeCanvasGroup != null && wakeText != null)
            return;

        GameObject canvasObj = new GameObject("SleepWakeCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
        wakeCanvas = canvasObj.GetComponent<Canvas>();
        wakeCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        wakeCanvas.sortingOrder = 120;

        CanvasScaler scaler = canvasObj.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        GraphicRaycaster raycaster = canvasObj.GetComponent<GraphicRaycaster>();
        raycaster.enabled = false;

        wakeCanvasGroup = canvasObj.GetComponent<CanvasGroup>();
        wakeCanvasGroup.alpha = 0f;

        GameObject panelObj = new GameObject("WakePanel", typeof(RectTransform), typeof(Image));
        RectTransform panelRect = panelObj.GetComponent<RectTransform>();
        panelRect.SetParent(canvasObj.transform, false);
        panelRect.anchorMin = new Vector2(0.5f, 0.87f);
        panelRect.anchorMax = new Vector2(0.5f, 0.87f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(980f, 140f);

        Image panelImage = panelObj.GetComponent<Image>();
        panelImage.color = new Color(0.06f, 0.08f, 0.12f, 0.78f);

        GameObject textObj = new GameObject("WakeText", typeof(RectTransform), typeof(TextMeshProUGUI));
        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.SetParent(panelObj.transform, false);
        textRect.anchorMin = new Vector2(0.05f, 0.12f);
        textRect.anchorMax = new Vector2(0.95f, 0.88f);
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        wakeText = textObj.GetComponent<TextMeshProUGUI>();
        wakeText.alignment = TextAlignmentOptions.Center;
        wakeText.fontSize = 26f;
        wakeText.color = new Color(0.98f, 0.94f, 0.84f, 1f);
        wakeText.textWrappingMode = TextWrappingModes.Normal;
        wakeText.text = string.Empty;
    }
}
