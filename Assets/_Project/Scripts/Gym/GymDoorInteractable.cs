using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GymDoorInteractable : MonoBehaviour, IInteractable
{
    private const float FaintEnergyThreshold = 0.20f;

    [SerializeField] private string gymSceneName = "GymScene";
    [SerializeField] private string promptText = "Masuk Gym";
    [SerializeField] private string blockedEnergyText = "Energi terlalu rendah untuk latihan.";
    [SerializeField] private string blockedNightText = "Gym sudah tutup malam ini.";
    [SerializeField] private string alreadyTrainedText = "Kamu sudah latihan hari ini.";
    [SerializeField] private GymProgressionSystem gymProgressionOverride;
    [SerializeField] private FadeManager fadeManagerOverride;

    private Collider cachedCollider;
    private Coroutine floatingTextRoutine;
    private GameObject floatingTextObject;
    private Canvas faintDialogCanvas;
    private bool pendingFaintConfirm;
    private TimeManager.TimePeriod pendingPeriod;
    private float pendingEnergy;

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
        return promptText;
    }

    // Compatibility helper with requested naming.
    public string GetInteractPrompt()
    {
        return promptText;
    }

    public bool CanInteract(GameObject interactor)
    {
        return true;
    }

    public void Interact(GameObject interactor)
    {
        OnInteract(interactor);
    }

    // Compatibility helper with requested naming.
    public void OnInteract(GameObject interactor)
    {
        TimeManager timeManager = TimeManager.Instance;
        PlayerStats playerStats = PlayerStats.Instance;
        GymProgressionSystem progression = ResolveGymProgression();
        FadeManager fadeManager = ResolveFadeManager();

        if (timeManager == null || playerStats == null || progression == null)
        {
            ShowFloatingText("Sistem gym belum siap.", 2f);
            return;
        }

        float hour = timeManager.CurrentHour;
        float energy = playerStats.EnergyPercent;

        if (hour < 6f || hour >= 22f)
        {
            ShowFloatingText("Gym sudah tutup. Jam operasional 06.00 - 22.00.", 2f);
            return;
        }

        TimeManager.TimePeriod period = timeManager.CurrentPeriod;
        bool hasTrained = progression.HasTrainedToday;
        bool canTrain = progression.CanTrain(energy, period);

        Debug.Log($"[GymDoor] hour={hour:F2} energy={energy:F2} hasTrained={hasTrained} canTrain={canTrain}");

        if (hasTrained)
        {
            ShowFloatingText("Kamu sudah berlatih hari ini. Istirahat dulu!", 2f);
            return;
        }

        if (energy < FaintEnergyThreshold)
        {
            ShowFaintWarningDialog(period, energy);
            return;
        }

        if (!canTrain)
        {
            ShowFloatingText("Energimu terlalu rendah untuk berlatih.", 2f);
            return;
        }

        GymSessionData session = progression.BuildSession(period, energy);
        if (session == null)
        {
            ShowFloatingText(blockedEnergyText, 2f);
            return;
        }

        if (fadeManager == null)
        {
            ShowFloatingText("Transisi belum siap.", 2f);
            return;
        }

        SpawnPlayerManager.TargetSpawnID = "gymdoor";
        fadeManager.FadeToBlackAndLoad(gymSceneName, 0.5f);
    }

    private GymProgressionSystem ResolveGymProgression()
    {
        if (gymProgressionOverride != null)
            return gymProgressionOverride;

        if (GymProgressionSystem.Instance != null)
            return GymProgressionSystem.Instance;

        GameObject managerObj = GameObject.Find("GameManager");
        if (managerObj == null)
        {
            managerObj = new GameObject("GameManager");
            Debug.LogWarning("[GymDoorInteractable] GameManager tidak ditemukan. Membuat fallback runtime.");
        }

        GymProgressionSystem existing = managerObj.GetComponent<GymProgressionSystem>();
        return existing != null ? existing : managerObj.AddComponent<GymProgressionSystem>();
    }

    private FadeManager ResolveFadeManager()
    {
        if (fadeManagerOverride != null)
            return fadeManagerOverride;

        if (FadeManager.Instance != null)
            return FadeManager.Instance;

        GameObject managerObj = GameObject.Find("GameManager");
        if (managerObj == null)
            return null;

        FadeManager existing = managerObj.GetComponent<FadeManager>();
        return existing != null ? existing : managerObj.AddComponent<FadeManager>();
    }

    private void ShowFloatingText(string text, float duration)
    {
        if (floatingTextRoutine != null)
            StopCoroutine(floatingTextRoutine);

        if (floatingTextObject != null)
        {
            Destroy(floatingTextObject);
            floatingTextObject = null;
        }

        floatingTextRoutine = StartCoroutine(FloatingTextRoutine(text, duration));
    }

    private IEnumerator FloatingTextRoutine(string text, float duration)
    {
        GameObject textObj = new GameObject("GymDoorFloatingText");
        floatingTextObject = textObj;

        TextMeshPro tmp = textObj.AddComponent<TextMeshPro>();
        tmp.text = text;
        tmp.fontSize = 3f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = new Color(0.78f, 0.95f, 0.82f, 1f);

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            Camera cam = Camera.main;
            Vector3 displayPos = transform.position + Vector3.up * 1.25f;
            if (cam != null)
            {
                Vector3 toCam = cam.transform.position - transform.position;
                toCam.y = 0f;
                if (toCam.sqrMagnitude < 0.001f)
                    toCam = -transform.forward;

                toCam.Normalize();
                displayPos = transform.position + toCam * 0.75f + Vector3.up * 1.25f;
                textObj.transform.rotation = Quaternion.LookRotation(textObj.transform.position - cam.transform.position);
            }

            textObj.transform.position = displayPos;
            yield return null;
        }

        if (textObj != null)
            Destroy(textObj);

        floatingTextObject = null;
        floatingTextRoutine = null;
    }

    private void ShowFaintWarningDialog(TimeManager.TimePeriod period, float energy)
    {
        if (faintDialogCanvas != null)
            return;

        pendingFaintConfirm = true;
        pendingPeriod = period;
        pendingEnergy = energy;

        GameObject canvasObj = new GameObject("GymFaintWarningCanvas");
        faintDialogCanvas = canvasObj.AddComponent<Canvas>();
        faintDialogCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        faintDialogCanvas.sortingOrder = 1000;
        canvasObj.AddComponent<GraphicRaycaster>();

        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        GameObject backdropObj = new GameObject("Backdrop", typeof(RectTransform), typeof(Image));
        backdropObj.transform.SetParent(canvasObj.transform, false);
        RectTransform backdropRect = backdropObj.GetComponent<RectTransform>();
        backdropRect.anchorMin = Vector2.zero;
        backdropRect.anchorMax = Vector2.one;
        backdropRect.offsetMin = Vector2.zero;
        backdropRect.offsetMax = Vector2.zero;
        Image backdropImage = backdropObj.GetComponent<Image>();
        backdropImage.color = new Color(0f, 0f, 0f, 0.72f);

        GameObject panelObj = new GameObject("DialogPanel", typeof(RectTransform), typeof(Image));
        panelObj.transform.SetParent(backdropObj.transform, false);
        RectTransform panelRect = panelObj.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(980f, 420f);
        panelRect.anchoredPosition = Vector2.zero;
        Image panelImage = panelObj.GetComponent<Image>();
        panelImage.color = new Color(0.12f, 0.12f, 0.12f, 0.95f);

        GameObject messageObj = new GameObject("Message", typeof(RectTransform), typeof(TextMeshProUGUI));
        messageObj.transform.SetParent(panelObj.transform, false);
        RectTransform messageRect = messageObj.GetComponent<RectTransform>();
        messageRect.anchorMin = new Vector2(0.5f, 1f);
        messageRect.anchorMax = new Vector2(0.5f, 1f);
        messageRect.pivot = new Vector2(0.5f, 1f);
        messageRect.anchoredPosition = new Vector2(0f, -40f);
        messageRect.sizeDelta = new Vector2(900f, 200f);
        TextMeshProUGUI messageText = messageObj.GetComponent<TextMeshProUGUI>();
        messageText.text = "Tubuhmu sangat lemah.\nMemaksakan latihan bisa berbahaya.\nTetap masuk gym?";
        messageText.fontSize = 36f;
        messageText.alignment = TextAlignmentOptions.Center;
        messageText.color = Color.white;

        CreateDialogButton(panelObj.transform, "Tetap Masuk", new Vector2(-160f, -140f), new Vector2(240f, 90f),
            new Color(0.86f, 0.34f, 0.28f, 1f), OnConfirmFaint);
        CreateDialogButton(panelObj.transform, "Batalkan", new Vector2(160f, -140f), new Vector2(240f, 90f),
            new Color(0.25f, 0.25f, 0.25f, 1f), CloseFaintDialog);
    }

    private void CreateDialogButton(Transform parent, string label, Vector2 anchoredPos, Vector2 size, Color color, System.Action onClick)
    {
        GameObject buttonObj = new GameObject(label + "Button", typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObj.transform.SetParent(parent, false);
        RectTransform rect = buttonObj.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = anchoredPos;

        Image image = buttonObj.GetComponent<Image>();
        image.color = color;

        Button button = buttonObj.GetComponent<Button>();
        button.onClick.AddListener(() => onClick?.Invoke());

        GameObject textObj = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObj.transform.SetParent(buttonObj.transform, false);
        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        TextMeshProUGUI text = textObj.GetComponent<TextMeshProUGUI>();
        text.text = label;
        text.fontSize = 32f;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
    }

    private void OnConfirmFaint()
    {
        CloseFaintDialog();
        if (!pendingFaintConfirm)
            return;

        pendingFaintConfirm = false;
        StartGymFaintSession(pendingPeriod, pendingEnergy);
    }

    private void CloseFaintDialog()
    {
        if (faintDialogCanvas == null)
            return;

        Destroy(faintDialogCanvas.gameObject);
        faintDialogCanvas = null;
    }

    private void StartGymFaintSession(TimeManager.TimePeriod period, float energy)
    {
        GymProgressionSystem progression = ResolveGymProgression();
        FadeManager fadeManager = ResolveFadeManager();

        if (progression == null)
        {
            ShowFloatingText("Sistem gym belum siap.", 2f);
            return;
        }

        GymSessionData session = progression.BuildFaintSession(period, energy);
        if (session == null)
        {
            ShowFloatingText(blockedEnergyText, 2f);
            return;
        }

        if (fadeManager == null)
        {
            ShowFloatingText("Transisi belum siap.", 2f);
            return;
        }

        SpawnPlayerManager.TargetSpawnID = "gymdoor";
        fadeManager.FadeToBlackAndLoad(gymSceneName, 0.5f);
    }

    private IEnumerator FaintSequence()
    {
        FadeManager fadeManager = FadeManager.Instance;
        if (fadeManager == null)
            yield break;

        bool fadedOut = false;
        fadeManager.FadeToBlack(0.5f, () => fadedOut = true);
        while (!fadedOut)
            yield return null;

        GameObject messageCanvasObj = new GameObject("GymFaintMessageCanvas");
        Canvas messageCanvas = messageCanvasObj.AddComponent<Canvas>();
        messageCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        messageCanvas.sortingOrder = 1001;
        messageCanvasObj.AddComponent<GraphicRaycaster>();
        CanvasScaler scaler = messageCanvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        GameObject messageObj = new GameObject("FaintMessage", typeof(RectTransform), typeof(TextMeshProUGUI));
        messageObj.transform.SetParent(messageCanvasObj.transform, false);
        RectTransform messageRect = messageObj.GetComponent<RectTransform>();
        messageRect.anchorMin = new Vector2(0.5f, 0.5f);
        messageRect.anchorMax = new Vector2(0.5f, 0.5f);
        messageRect.sizeDelta = new Vector2(1200f, 240f);
        messageRect.anchoredPosition = Vector2.zero;
        TextMeshProUGUI messageText = messageObj.GetComponent<TextMeshProUGUI>();
        messageText.text = "Kamu pingsan karena kelelahan...\nIstirahatlah dulu.";
        messageText.fontSize = 42f;
        messageText.alignment = TextAlignmentOptions.Center;
        messageText.color = Color.white;

        yield return new WaitForSecondsRealtime(2f);

        bool fadedIn = false;
        fadeManager.FadeFromBlack(0.5f, () => fadedIn = true);
        while (!fadedIn)
            yield return null;

        Destroy(messageCanvasObj);
    }
}
