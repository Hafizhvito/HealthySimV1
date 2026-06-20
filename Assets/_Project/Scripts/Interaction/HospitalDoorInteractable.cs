using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HospitalDoorInteractable : MonoBehaviour, IInteractable, IDialogueActor
{
    [SerializeField] private string proximityHintText = "Periksakan kondisi kesehatanmu ke dokter.";
    [SerializeField] private string npcName = "dr. Sri Wuryanti";
    [SerializeField] private DoctorSriDialogueController dialogueController;
    [SerializeField] private GameObject hospitalDoorModel;
    [SerializeField] private AudioClip doorOpenSound;
    [SerializeField] private GameObject hospitalOverlay;
    [SerializeField] private float consultationHours = 1f;

    private Collider cachedCollider;
    private Coroutine floatingTextRoutine;
    private Coroutine postConsultationRoutine;
    private bool isMenuCloseSubscribed;
    private bool awaitingConsultationClose;
    private bool wasAlertActiveOnEntry;

    private void Awake()
    {
        cachedCollider = GetComponent<Collider>();
        if (cachedCollider == null)
            cachedCollider = GetComponentInChildren<Collider>();

        if (string.IsNullOrWhiteSpace(npcName))
            npcName = "dr. Sri Wuryanti";
    }

    private void OnEnable()
    {
        InteractableRegistry.Register(this, cachedCollider, transform);
        TrySubscribeMenuClose();
    }

    private void Update()
    {
        if (!awaitingConsultationClose)
            return;

        if (NpcDialogueMenuController.Instance == null)
            return;

        if (!NpcDialogueMenuController.Instance.IsOpen)
            HandleDialogueClosed();
    }

    private void OnDisable()
    {
        TryUnsubscribeMenuClose();
        InteractableRegistry.Unregister(this);
    }

    public string GetInteractionText() => proximityHintText;
    public string GetInteractPrompt() => proximityHintText;
    public bool CanInteract(GameObject interactor) => true;

    public void Interact(GameObject interactor)
    {
        OnInteract(interactor);
    }

    public void OnInteract(GameObject interactor)
    {
        PlayerStats playerStats = PlayerStats.Instance;
        if (playerStats == null)
        {
            ShowFloatingText("Klinik belum siap.", 2f);
            return;
        }

        if (dialogueController == null)
        {
            ShowFloatingText("Dokter belum siap.", 2f);
            return;
        }

        if (NpcDialogueMenuController.Instance == null)
        {
            ShowFloatingText("Dialog belum siap.", 2f);
            return;
        }

        if (doorOpenSound != null)
            AudioSource.PlayClipAtPoint(doorOpenSound, transform.position);

        if (hospitalDoorModel != null && !hospitalDoorModel.activeSelf)
            hospitalDoorModel.SetActive(true);

        wasAlertActiveOnEntry = dialogueController.IsHealthAlertActive;

        float healthScore = playerStats.HealthScoreThisPhase;
        float dailyFat = playerStats.DailyFat;
        float dailyProtein = playerStats.DailyProtein;
        DialogueGraphData graph = dialogueController.BuildConsultationDialogue(healthScore, dailyFat, dailyProtein);

        if (graph == null)
        {
            ShowFloatingText("Dialog belum siap.", 2f);
            return;
        }

        bool opened = NpcDialogueMenuController.Instance.OpenDialogue(this, graph);
        if (opened)
        {
            TrySubscribeMenuClose();
            awaitingConsultationClose = true;
            if (hospitalOverlay != null) hospitalOverlay.SetActive(true);
        }
        else
        {
            ShowFloatingText("Dialog belum siap.", 2f);
        }
    }

    private void TrySubscribeMenuClose()
    {
        if (isMenuCloseSubscribed) return;
        if (NpcDialogueMenuController.Instance == null) return;

        NpcDialogueMenuController.Instance.OnDialogueClosed += HandleDialogueClosed;
        isMenuCloseSubscribed = true;
    }

    private void TryUnsubscribeMenuClose()
    {
        if (!isMenuCloseSubscribed) return;
        if (NpcDialogueMenuController.Instance != null)
            NpcDialogueMenuController.Instance.OnDialogueClosed -= HandleDialogueClosed;
        isMenuCloseSubscribed = false;
    }

    private void HandleDialogueClosed()
    {
        if (!awaitingConsultationClose) return;
        awaitingConsultationClose = false;

        if (NpcDialogueMenuController.Instance != null)
            NpcDialogueMenuController.Instance.ForceCleanupAfterSceneLoad();

        if (PlayerStats.Instance != null)
            PlayerStats.Instance.SetVisitedHospital();

        if (hospitalOverlay != null)
            hospitalOverlay.SetActive(false);

        if (postConsultationRoutine != null)
            StopCoroutine(postConsultationRoutine);
        postConsultationRoutine = StartCoroutine(PostConsultationSequence());
    }

    private IEnumerator PostConsultationSequence()
    {
        float startHour = TimeManager.Instance != null ? TimeManager.Instance.CurrentHour : 10f;
        float endHour = startHour + consultationHours;
        if (endHour > 23f) endHour = 23f;

        ClockAnimationUI clock = ClockAnimationUI.EnsureInstance();
        if (clock != null)
        {
            bool clockDone = false;
            clock.PlayTimeSkipAnimation("Konsultasi Dokter", startHour, endHour, 2.5f, () => clockDone = true);
            yield return new WaitUntil(() => clockDone);
        }

        if (TimeManager.Instance != null)
            TimeManager.Instance.SetTimeByHour(Mathf.RoundToInt(endHour));

        if (wasAlertActiveOnEntry)
            yield return StartCoroutine(ShowRecommendationPanel());

        postConsultationRoutine = null;
    }

    private IEnumerator ShowRecommendationPanel()
    {
        if (ModalStateManager.Instance != null)
            ModalStateManager.Instance.OpenModal("DoctorRecommendation");

        RectTransform panelRoot = CreateRecommendationPanel(out TextMeshProUGUI bodyText, out Button continueBtn);
        if (panelRoot == null || bodyText == null || continueBtn == null) yield break;

        CanvasGroup group = panelRoot.GetComponent<CanvasGroup>();
        if (group == null)
        {
            Destroy(panelRoot.gameObject);
            yield break;
        }

        group.alpha = 1f;
        group.blocksRaycasts = true;
        group.interactable = true;
        panelRoot.gameObject.SetActive(true);

        bodyText.text = BuildRecommendationText();

        bool closed = false;
        continueBtn.onClick.RemoveAllListeners();
        continueBtn.onClick.AddListener(() => closed = true);

        yield return new WaitUntil(() => closed);

        group.alpha = 0f;
        group.blocksRaycasts = false;
        group.interactable = false;
        Destroy(panelRoot.gameObject);

        if (ModalStateManager.Instance != null)
            ModalStateManager.Instance.CloseModal("DoctorRecommendation");
    }

    private string BuildRecommendationText()
    {
        PlayerStats stats = PlayerStats.Instance;
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("<b>Tips hidup sehat:</b>");
        sb.AppendLine();

        sb.AppendLine("\u2022 Konsumsi makanan bergizi seimbang: karbohidrat, protein, sayur, dan buah setiap hari.");

        if (stats != null && stats.DailyFat > 65f)
            sb.AppendLine("\u2022 Kurangi makanan tinggi lemak (gorengan, fast food). Batas lemak harian: 60-70g.");

        if (stats != null && stats.DailyProtein < 40f)
            sb.AppendLine("\u2022 Tingkatkan asupan protein: telur, tahu, tempe, ikan, atau ayam tanpa kulit.");

        sb.AppendLine("\u2022 Minum air putih yang cukup setiap hari.");
        sb.AppendLine("\u2022 Tidur cukup setiap malam (jangan begadang).");
        sb.AppendLine("\u2022 Rutin berolahraga di gym, minimal datang setiap hari kerja.");
        sb.AppendLine("\u2022 Hindari makanan tinggi gula dan kalori berlebih.");
        sb.AppendLine();
        sb.Append("<i>\"Perubahan kecil yang konsisten akan membawa hasil besar.\"</i>");

        return sb.ToString();
    }

    private RectTransform CreateRecommendationPanel(out TextMeshProUGUI bodyText, out Button continueBtn)
    {
        bodyText = null;
        continueBtn = null;

        Canvas canvas = FindHudCanvas();
        if (canvas == null) return null;

        GameObject panelObj = new GameObject("DoctorRecommendationPanel", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
        RectTransform panelRect = panelObj.GetComponent<RectTransform>();
        panelRect.SetParent(canvas.transform, false);
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(520f, 340f);
        panelRect.anchoredPosition = Vector2.zero;
        panelRect.SetAsLastSibling();

        Image bg = panelObj.GetComponent<Image>();
        bg.color = new Color(0.96f, 0.97f, 0.95f, 0.98f);

        GameObject titleObj = new GameObject("TitleText", typeof(RectTransform), typeof(TextMeshProUGUI));
        RectTransform titleRect = titleObj.GetComponent<RectTransform>();
        titleRect.SetParent(panelRect, false);
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.anchoredPosition = new Vector2(0f, -12f);
        titleRect.sizeDelta = new Vector2(-32f, 36f);

        TextMeshProUGUI titleTmp = titleObj.GetComponent<TextMeshProUGUI>();
        titleTmp.text = "Rekomendasi dr. Sri";
        titleTmp.fontSize = 20f;
        titleTmp.fontStyle = FontStyles.Bold;
        titleTmp.color = new Color(0.12f, 0.35f, 0.42f, 1f);
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
        bodyText.richText = true;
        bodyText.lineSpacing = 2f;

        GameObject btnObj = new GameObject("ContinueButton", typeof(RectTransform), typeof(Image), typeof(Button));
        RectTransform btnRect = btnObj.GetComponent<RectTransform>();
        btnRect.SetParent(panelRect, false);
        btnRect.anchorMin = new Vector2(0.5f, 0f);
        btnRect.anchorMax = new Vector2(0.5f, 0f);
        btnRect.pivot = new Vector2(0.5f, 0f);
        btnRect.anchoredPosition = new Vector2(0f, 14f);
        btnRect.sizeDelta = new Vector2(220f, 36f);

        Image btnBg = btnObj.GetComponent<Image>();
        btnBg.color = new Color(0.18f, 0.55f, 0.62f, 1f);

        continueBtn = btnObj.GetComponent<Button>();

        GameObject btnTextObj = new GameObject("BtnText", typeof(RectTransform), typeof(TextMeshProUGUI));
        RectTransform btnTextRect = btnTextObj.GetComponent<RectTransform>();
        btnTextRect.SetParent(btnRect, false);
        btnTextRect.anchorMin = Vector2.zero;
        btnTextRect.anchorMax = Vector2.one;
        btnTextRect.offsetMin = Vector2.zero;
        btnTextRect.offsetMax = Vector2.zero;

        TextMeshProUGUI btnTmp = btnTextObj.GetComponent<TextMeshProUGUI>();
        btnTmp.text = "Saya mengerti, Dok.";
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
            if (c != null) return c;
        }

        Canvas[] all = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i].renderMode == RenderMode.ScreenSpaceOverlay)
                return all[i];
        }
        return null;
    }

    public List<DialogueChoiceData> GetAvailableChoices(DialogueNodeData node)
    {
        if (node == null)
            return new List<DialogueChoiceData>();
        return node.choices ?? new List<DialogueChoiceData>();
    }

    public void ApplyConsequence(DialogueConsequence consequence) { }

    private void ShowFloatingText(string text, float duration)
    {
        if (floatingTextRoutine != null)
            StopCoroutine(floatingTextRoutine);
        floatingTextRoutine = StartCoroutine(FloatingTextRoutine(text, duration));
    }

    private IEnumerator FloatingTextRoutine(string text, float duration)
    {
        GameObject textObj = new GameObject("HospitalDoorFloatingText");
        TextMeshPro tmp = textObj.AddComponent<TextMeshPro>();
        tmp.text = text;
        tmp.fontSize = 3.8f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = new Color(1f, 0.92f, 0.68f, 1f);

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
                if (toCam.sqrMagnitude < 0.001f) toCam = -transform.forward;
                toCam.Normalize();
                displayPos = transform.position + toCam * 0.75f + Vector3.up * 1.25f;
                textObj.transform.rotation = Quaternion.LookRotation(textObj.transform.position - cam.transform.position);
            }
            textObj.transform.position = displayPos;
            yield return null;
        }

        Destroy(textObj);
        floatingTextRoutine = null;
    }
}
