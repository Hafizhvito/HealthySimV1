using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class EndingManager : MonoBehaviour
{
    public static EndingManager Instance { get; private set; }
    public enum EndingType { Good, Neutral, Bad }

    [Header("Config")]
    [SerializeField] private int endingTriggerDay = 10;

    private const string ModalKey = "ending_panel";
    private RectTransform panelRoot;
    private CanvasGroup panelGroup;
    private TextMeshProUGUI titleText;
    private TextMeshProUGUI bodyText;
    private Button closeButton;
    private bool isShowing;
    private EndingType pendingEndingType;
    private PlayerStats.Gender pendingGender;
    private bool creditsPending;
    private bool endingTriggered;
    private int creditsAfterDay = -1;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public bool IsShowing => isShowing;
    public int EndingTriggerDay => endingTriggerDay;

    public void TriggerEnding()
    {
        if (isShowing || endingTriggered) return;
        if (PlayerStats.Instance == null) return;

        float avg = PlayerStats.Instance.GetAveragePhaseScore();
        PlayerStats.Gender gender = PlayerStats.Instance.PlayerGender;

        float goodThreshold    = gender == PlayerStats.Gender.Female ? 65f : 70f;
        float neutralThreshold = gender == PlayerStats.Gender.Female ? 40f : 45f;

        pendingEndingType = avg >= goodThreshold  ? EndingType.Good
                          : avg >= neutralThreshold ? EndingType.Neutral
                          : EndingType.Bad;
        pendingGender = gender;

        Debug.Log($"[EndingManager] avg={avg:F1} gender={gender} -> {pendingEndingType}");

        endingTriggered = true;

        if (NpcDialogueMenuController.Instance == null)
        {
            Debug.LogWarning("[EndingManager] NpcDialogueMenuController tidak ditemukan, skip doctor dialogue.");
            StartCoroutine(ShowEndingRoutine(pendingEndingType, pendingGender));
            return;
        }

        isShowing = true;
        float[] scores = PlayerStats.Instance.CommittedPhaseScores;
        float youthScore  = scores.Length > 0 ? scores[0] : 50f;
        float adultScore  = scores.Length > 1 ? scores[1] : 50f;
        float seniorScore = scores.Length > 2 ? scores[2] : 50f;

        DialogueGraphData doctorDialogue = BuildDoctorDialogue(
            pendingEndingType, pendingGender, avg,
            youthScore, adultScore, seniorScore);

        NpcDialogueMenuController.Instance.OnDialogueClosed += HandleDoctorDialogueClosed;
        NpcDialogueMenuController.Instance.OpenDialogue(new EndingDoctorActor(), doctorDialogue);
    }

    private void HandleDoctorDialogueClosed()
    {
        if (NpcDialogueMenuController.Instance != null)
            NpcDialogueMenuController.Instance.OnDialogueClosed -= HandleDoctorDialogueClosed;
        StartCoroutine(ShowEndingRoutine(pendingEndingType, pendingGender));
    }

    public void NotifySleepCompleted(int currentDayNumber)
    {
        if (!creditsPending)
            return;

        if (creditsAfterDay < 0 || currentDayNumber <= creditsAfterDay)
            return;

        creditsPending = false;

        if (CreditsController.Instance == null)
        {
            GameObject creditsGo = new GameObject("CreditsController");
            creditsGo.AddComponent<CreditsController>();
        }

        CreditsController.Instance.Play();
    }

    // ── Dialogue builder ─────────────────────────────────────────────

    private static DialogueGraphData BuildDoctorDialogue(
        EndingType type,
        PlayerStats.Gender gender,
        float avg,
        float youthScore,
        float adultScore,
        float seniorScore)
    {
        DialogueGraphData graph = ScriptableObject.CreateInstance<DialogueGraphData>();
        graph.npcId          = "dr_ending";
        graph.npcDisplayName = "Dr. Hana";
        graph.startNodeId    = "start";

        // Node 1 — pembuka
        DialogueNodeData node1 = new DialogueNodeData();
        node1.nodeId       = "start";
        node1.fallbackLine = BuildDoctorOpening(type, gender);
        DialogueChoiceData c1 = new DialogueChoiceData();
        c1.choiceText  = "Lanjut >";
        c1.nextNodeId  = "phase_review";
        node1.choices  = new System.Collections.Generic.List<DialogueChoiceData> { c1 };

        // Node 2 — review per fase
        DialogueNodeData node2 = new DialogueNodeData();
        node2.nodeId       = "phase_review";
        node2.fallbackLine = BuildPhaseReview(type, youthScore, adultScore, seniorScore);
        DialogueChoiceData c2 = new DialogueChoiceData();
        c2.choiceText  = "Lanjut >";
        c2.nextNodeId  = "conclusion";
        node2.choices  = new System.Collections.Generic.List<DialogueChoiceData> { c2 };

        // Node 3 — kesimpulan + saran
        DialogueNodeData node3 = new DialogueNodeData();
        node3.nodeId            = "conclusion";
        node3.fallbackLine      = BuildDoctorConclusion(type, gender);
        node3.isConversationEnd = true;
        DialogueChoiceData c3 = new DialogueChoiceData();
        c3.choiceText  = "Terima kasih, Dok.";
        c3.nextNodeId  = string.Empty;
        node3.choices  = new System.Collections.Generic.List<DialogueChoiceData> { c3 };

        graph.nodes = new System.Collections.Generic.List<DialogueNodeData> { node1, node2, node3 };
        return graph;
    }

    private static string BuildDoctorOpening(EndingType type, PlayerStats.Gender gender)
    {
        bool isFemale = gender == PlayerStats.Gender.Female;

        return type switch
        {
            EndingType.Good => isFemale
                                ? "Selamat datang kembali. Kabar baik: hasil pemeriksaanmu sangat stabil. " +
                                    "Kebiasaan kecil yang kamu jaga tiap hari benar-benar bekerja."
                                : "Selamat datang kembali. Hasil pemeriksaanmu bagus dan konsisten. " +
                                    "Tubuh merespons ketika pola hidup dijaga rutin.",

            EndingType.Neutral => isFemale
                                ? "Silakan duduk. Ada yang sudah baik, tapi ritme harianmu belum stabil. " +
                                    "Ini belum darurat, tapi perlu konsisten diperbaiki."
                                : "Silakan duduk. Hasilnya campuran: ada kemajuan, ada kebiasaan yang masih bocor. " +
                                    "Kita bisa rapikan pelan-pelan.",

            EndingType.Bad =>
                "Terima kasih sudah datang. Saya akan bicara jujur: tubuhmu sedang kewalahan. " +
                "Ada tanda yang perlu ditangani lebih serius mulai sekarang.",

            _ => "Silakan duduk. Mari kita bahas kondisimu."
        };
    }

    private static string BuildPhaseReview(
        EndingType type,
        float youth,
        float adult,
        float senior)
    {
        // Label per fase
        string youthLabel  = youth  >= 65f ? "baik"   : youth  >= 40f ? "cukup"  : "kurang baik";
        string adultLabel  = adult  >= 65f ? "baik"   : adult  >= 40f ? "cukup"  : "kurang baik";
        string seniorLabel = senior >= 65f ? "terjaga" : senior >= 40f ? "cukup"  : "cukup berat";

        // Komentar kontekstual per fase berdasarkan ending
        string youthComment  = BuildPhaseComment("muda",   youth,  type);
        string adultComment  = BuildPhaseComment("dewasa", adult,  type);
        string seniorComment = BuildPhaseComment("lansia", senior, type);

        // Deteksi tren (naik/turun/stabil)
        string trend = DetectTrend(youth, adult, senior);

         return $"Ringkasannya per fase:\n" +
             $"- Muda: {youthLabel}. {youthComment}\n" +
             $"- Dewasa: {adultLabel}. {adultComment}\n" +
             $"- Lansia: {seniorLabel}. {seniorComment}\n\n" +
             $"{trend}";
    }

    private static string BuildPhaseComment(string fase, float score, EndingType type)
    {
        if (type == EndingType.Good)
        {
            if (score >= 65f) return "Rutinitasmu rapi, efeknya terasa nyata.";
            return "Ada hari yang turun, tapi arahmu tetap sehat.";
        }

        if (type == EndingType.Neutral)
        {
            if (score >= 65f) return "Fase ini paling stabil di perjalananmu.";
            if (score >= 40f) return "Ada usaha, tapi masih sering putus di tengah.";
            return "Banyak kebiasaan belum mendukung tubuhmu.";
        }

        // Bad
        if (score >= 65f) return "Fase ini justru paling baik di datamu.";
        if (score >= 40f) return "Ada momen baik, tapi tidak bertahan lama.";
        return "Di fase ini tubuh paling sering memberi sinyal.";
    }

    private static string DetectTrend(float youth, float adult, float senior)
    {
        bool naik   = senior > adult && adult > youth;
        bool turun  = senior < adult && adult < youth;
        bool stabil = Mathf.Abs(senior - youth) < 10f;

        if (naik)
            return "Trennya naik. Artinya kebiasaanmu makin rapi.";
        if (turun)
            return "Trennya turun. Ini sinyal yang perlu ditangani.";
        if (stabil)
            return "Trennya stabil. Kalau stabilnya rendah, tetap perlu perhatian.";

        return "Trennya naik-turun. Konsistensi adalah kunci berikutnya.";
    }

    private static string BuildDoctorConclusion(EndingType type, PlayerStats.Gender gender)
    {
        bool isFemale = gender == PlayerStats.Gender.Female;

        return type switch
        {
            EndingType.Good => isFemale
                                ? "Secara klinis, kondisimu baik dan stabil. " +
                                    "Ini bukti bahwa tidur cukup, makan seimbang, dan gerak rutin saling menguatkan."
                                : "Secara klinis, kondisimu baik. " +
                                    "Tubuh merespons ketika makan, tidur, dan gerak dijaga konsisten.",

            EndingType.Neutral => isFemale
                                ? "Ada beberapa catatan yang perlu dirapikan. " +
                                    "Jika ritme makan dan tidur lebih teratur, kondisi ini bisa membaik."
                                : "Ada sinyal yang perlu kamu perhatikan. " +
                                    "Perbaiki pola makan dan aktivitas fisik sebelum kebiasaan buruk jadi menetap.",

            EndingType.Bad => isFemale
                                ? "Ada tanda kondisi serius yang perlu ditangani. " +
                                    "Mulai dari jadwal tidur, porsi makan, dan aktivitas fisik, semuanya harus dibenahi bertahap."
                                : "Ada tanda kondisi serius yang perlu ditangani segera. " +
                                    "Perubahan kecil yang konsisten masih bisa membalikkan arah.",

            _ => "Jaga kesehatanmu ke depan."
        };
    }

    // ── Ending panel ─────────────────────────────────────────────────

    private IEnumerator ShowEndingRoutine(EndingType type, PlayerStats.Gender gender)
    {
        EnsurePanel();
        if (panelRoot == null) yield break;

        if (ModalStateManager.Instance != null)
            ModalStateManager.Instance.OpenModal(ModalKey);

        panelRoot.gameObject.SetActive(true);
        panelGroup.alpha          = 1f;
        panelGroup.blocksRaycasts = true;
        panelGroup.interactable   = true;
        panelRoot.SetAsLastSibling();

        titleText.text           = GetTitle(type);
        bodyText.text            = string.Empty;
        closeButton.interactable = false;

        // Warna aksen berbeda per ending
        SetAccentColor(type);

        string narrative = GetNarrative(type, gender);
        // string diseaseInfo = BuildDiseaseInfoSection(PlayerStats.Instance);
        // if (!string.IsNullOrEmpty(diseaseInfo))
        //     narrative = narrative + "\n\n" + diseaseInfo;
        float  visible   = 0f;
        const float cps  = 50f;

        while (visible < narrative.Length)
        {
            visible += cps * Time.unscaledDeltaTime;
            int shown = Mathf.Clamp(Mathf.FloorToInt(visible), 0, narrative.Length);
            bodyText.text = narrative.Substring(0, shown);
            yield return null;
        }

        bodyText.text            = narrative;
        closeButton.interactable = true;

        bool closed = false;
        closeButton.onClick.RemoveAllListeners();
        closeButton.onClick.AddListener(() => closed = true);
        while (!closed) yield return null;

        panelGroup.alpha          = 0f;
        panelGroup.blocksRaycasts = false;
        panelGroup.interactable   = false;
        panelRoot.gameObject.SetActive(false);

        if (ModalStateManager.Instance != null)
            ModalStateManager.Instance.CloseModal(ModalKey);

        // Ending selesai, credits diputar setelah pemain tidur lagi.
        isShowing = false;
        creditsPending = true;
        creditsAfterDay = TimeManager.Instance != null ? TimeManager.Instance.CurrentDayNumber : -1;
    }

    private void SetAccentColor(EndingType type)
    {
        // Cari AccentBar di dalam card
        if (panelRoot == null) return;
        Transform card = panelRoot.Find("Card");
        if (card == null) return;
        Transform accent = card.Find("AccentBar");
        if (accent == null) return;

        Image accentImage = accent.GetComponent<Image>();
        if (accentImage == null) return;

        accentImage.color = type switch
        {
            EndingType.Good    => new Color32(0x4C, 0xAF, 0x50, 0xFF), // hijau
            EndingType.Neutral => new Color32(0xFF, 0x98, 0x00, 0xFF), // oranye
            EndingType.Bad     => new Color32(0xF4, 0x43, 0x36, 0xFF), // merah
            _                  => new Color32(0x21, 0x96, 0xF3, 0xFF)
        };
    }

    private static string GetTitle(EndingType type) => type switch
    {
        EndingType.Good    => "Hidup Sehat, Hidup Bahagia",
        EndingType.Neutral => "Perjalanan yang Belum Selesai",
        EndingType.Bad     => "Tubuh Sudah Berbicara",
        _                  => "Akhir Perjalanan"
    };

    private static string GetNarrative(EndingType type, PlayerStats.Gender gender)
    {
        bool isFemale = gender == PlayerStats.Gender.Female;

        return type switch
        {
            EndingType.Good => isFemale
                ? "Kamu sudah melewati setiap fase dengan pilihan yang kamu jaga.\n\n" +
                "Tidak selalu sempurna, tapi cukup konsisten untuk membuat perbedaan.\n\n" +
                "Dan tubuhmu mengingat itu."
                : "Perjalananmu tidak selalu mudah, tapi kamu menjalaninya dengan cukup konsisten.\n\n" +
                "Pilihan kecil yang kamu ulang setiap hari mulai membentuk sesuatu yang nyata.\n\n" +
                "Dan tubuhmu merespons.",

            EndingType.Neutral => isFemale
                ? "Ada hari yang berjalan baik, ada yang tidak.\n\n" +
                "Kamu sudah mencoba, tapi belum sepenuhnya konsisten.\n\n" +
                "Dan mungkin, ini saatnya mulai lebih serius menjaga dirimu."
                : "Tidak semua berjalan sesuai rencana.\n\n" +
                "Tapi kamu juga tidak berhenti mencoba.\n\n" +
                "Masih ada waktu untuk memperbaiki arah ini.",

            EndingType.Bad => isFemale
                ? "Tubuhmu sudah lama memberi sinyal.\n\n" +
                "Dan hari ini, kamu akhirnya benar-benar mendengarnya.\n\n" +
                "Mungkin ini bukan akhir. Tapi titik untuk mulai berubah."
                : "Tubuhmu sudah lama mencoba berbicara.\n\n" +
                "Dan sekarang, kamu tidak bisa lagi mengabaikannya.\n\n" +
                "Setiap orang punya titik baliknya. Ini mungkin milikmu.",

            _ => "Perjalananmu telah selesai."
        };
    }

    private struct DiseaseRisk
    {
        public string Name;
        public string Description;
        public string Reason;
        public float Score;
    }

    private static string BuildDiseaseInfoSection(PlayerStats stats)
    {
        if (stats == null || stats.TotalDaysEvaluated <= 0)
            return "Info kesehatan:\nData harian belum cukup untuk membuat kesimpulan yang spesifik.";

        var risks = BuildDiseaseRisks(stats);
        risks.Sort((a, b) => b.Score.CompareTo(a.Score));

        const float riskThreshold = 0.35f;
        bool hasRisk = risks.Count > 0 && risks[0].Score >= riskThreshold;
        int count = Mathf.Min(3, risks.Count);

        string header = hasRisk
            ? "Info penyakit yang perlu diperhatikan:"
            : "Info kesehatan: belum ada indikasi kuat, tapi ada area yang perlu dijaga:";

        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.Append(header);

        for (int i = 0; i < count; i++)
        {
            DiseaseRisk risk = risks[i];
            sb.Append("\n- ");
            sb.Append(risk.Name);
            sb.Append(": ");
            sb.Append(risk.Description);
            if (!string.IsNullOrEmpty(risk.Reason))
            {
                sb.Append("\n  ");
                sb.Append(risk.Reason);
            }
        }

        return sb.ToString();
    }

    private static System.Collections.Generic.List<DiseaseRisk> BuildDiseaseRisks(PlayerStats stats)
    {
        float days = Mathf.Max(1f, stats.TotalDaysEvaluated);
        float poorDietRatio = stats.PoorDietDays / days;
        float noFoodRatio = stats.NoFoodDays / days;
        float highCalRatio = stats.HighCalorieDays / days;
        float lowCalRatio = stats.LowCalorieDays / days;
        float disturbedSleepRatio = stats.DisturbedSleepDays / days;
        float lowEnergySleepRatio = stats.LowEnergySleepDays / days;
        float skippedGymRatio = stats.SkippedGymDays / days;
        float overworkedRatio = stats.OverworkedDays / days;

        var risks = new System.Collections.Generic.List<DiseaseRisk>
        {
            new DiseaseRisk
            {
                Name = "Diabetes tipe 2",
                Description = "Gula darah tinggi akibat resistensi insulin.",
                Reason = BuildReason(new []
                {
                    ReasonIf(poorDietRatio >= 0.35f, "pola makan sering tidak sehat"),
                    ReasonIf(highCalRatio >= 0.35f, "kalori sering berlebih"),
                    ReasonIf(skippedGymRatio >= 0.45f, "aktivitas fisik jarang")
                }),
                Score = (poorDietRatio * 0.55f) + (highCalRatio * 0.35f) + (skippedGymRatio * 0.10f)
            },
            new DiseaseRisk
            {
                Name = "Hipertensi",
                Description = "Tekanan darah tinggi yang membebani jantung.",
                Reason = BuildReason(new []
                {
                    ReasonIf(overworkedRatio >= 0.35f, "ritme kerja terlalu berat"),
                    ReasonIf(disturbedSleepRatio >= 0.35f, "tidur sering terganggu"),
                    ReasonIf(lowEnergySleepRatio >= 0.35f, "energi sangat rendah sebelum tidur")
                }),
                Score = (overworkedRatio * 0.45f) + (disturbedSleepRatio * 0.35f) + (lowEnergySleepRatio * 0.20f)
            },
            new DiseaseRisk
            {
                Name = "Kolesterol tinggi",
                Description = "Lemak darah tinggi yang meningkatkan risiko kardiovaskular.",
                Reason = BuildReason(new []
                {
                    ReasonIf(poorDietRatio >= 0.35f, "pola makan sering tidak sehat"),
                    ReasonIf(highCalRatio >= 0.35f, "kalori sering berlebih")
                }),
                Score = (poorDietRatio * 0.60f) + (highCalRatio * 0.40f)
            },
            new DiseaseRisk
            {
                Name = "Obesitas",
                Description = "Penumpukan lemak tubuh yang mengganggu metabolisme.",
                Reason = BuildReason(new []
                {
                    ReasonIf(highCalRatio >= 0.35f, "kalori sering berlebih"),
                    ReasonIf(skippedGymRatio >= 0.40f, "aktivitas fisik jarang"),
                    ReasonIf(poorDietRatio >= 0.35f, "pola makan tidak seimbang")
                }),
                Score = (highCalRatio * 0.50f) + (skippedGymRatio * 0.30f) + (poorDietRatio * 0.20f)
            },
            new DiseaseRisk
            {
                Name = "Kelelahan kronis",
                Description = "Kelelahan berkepanjangan yang mengganggu fungsi harian.",
                Reason = BuildReason(new []
                {
                    ReasonIf(disturbedSleepRatio >= 0.35f, "tidur sering terganggu"),
                    ReasonIf(lowEnergySleepRatio >= 0.35f, "energi terlalu rendah sebelum tidur"),
                    ReasonIf(overworkedRatio >= 0.35f, "ritme kerja terlalu berat")
                }),
                Score = (disturbedSleepRatio * 0.45f) + (lowEnergySleepRatio * 0.35f) + (overworkedRatio * 0.20f)
            },
            new DiseaseRisk
            {
                Name = "Gangguan tidur",
                Description = "Kualitas tidur rendah yang menurunkan pemulihan tubuh.",
                Reason = BuildReason(new []
                {
                    ReasonIf(disturbedSleepRatio >= 0.35f, "tidur sering terganggu"),
                    ReasonIf(lowEnergySleepRatio >= 0.35f, "energi terlalu rendah sebelum tidur"),
                    ReasonIf(noFoodRatio >= 0.35f, "sering melewatkan makan")
                }),
                Score = (disturbedSleepRatio * 0.70f) + (lowEnergySleepRatio * 0.30f)
            }
        };

        return risks;
    }

    private static string ReasonIf(bool condition, string reason)
    {
        return condition ? reason : string.Empty;
    }

    private static string BuildReason(string[] reasons)
    {
        var list = new System.Collections.Generic.List<string>();
        for (int i = 0; i < reasons.Length; i++)
        {
            if (!string.IsNullOrWhiteSpace(reasons[i]))
                list.Add(reasons[i]);
        }

        if (list.Count == 0)
            return "Alasan: pola hidup belum stabil secara konsisten.";

        if (list.Count > 2)
            list = list.GetRange(0, 2);

        return "Alasan: " + string.Join(", ", list) + ".";
    }

    // ── Panel builder (tidak berubah dari versi asli) ────────────────

    private void EnsurePanel()
    {
        if (panelRoot != null) return;

        Transform parent = FindHudCanvas();
        if (TryBindExistingPanel(parent))
            return;

        if (parent == null)
        {
            GameObject cGo = new GameObject("EndingCanvas",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas cv = cGo.GetComponent<Canvas>();
            cv.renderMode   = RenderMode.ScreenSpaceOverlay;
            cv.sortingOrder = 300;
            CanvasScaler cs = cGo.GetComponent<CanvasScaler>();
            cs.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            cs.referenceResolution = new Vector2(1920f, 1080f);
            parent = cGo.transform;
        }

        GameObject root = new GameObject("EndingPanel", typeof(RectTransform), typeof(CanvasGroup));
        root.transform.SetParent(parent, false);
        panelRoot            = root.GetComponent<RectTransform>();
        panelRoot.anchorMin  = Vector2.zero;
        panelRoot.anchorMax  = Vector2.one;
        panelRoot.offsetMin  = Vector2.zero;
        panelRoot.offsetMax  = Vector2.zero;
        panelGroup           = root.GetComponent<CanvasGroup>();
        panelGroup.alpha          = 0f;
        panelGroup.blocksRaycasts = false;
        panelGroup.interactable   = false;

        // Overlay
        GameObject ov = new GameObject("Overlay", typeof(RectTransform), typeof(Image));
        ov.transform.SetParent(root.transform, false);
        RectTransform ovR = ov.GetComponent<RectTransform>();
        ovR.anchorMin = Vector2.zero; ovR.anchorMax = Vector2.one;
        ovR.offsetMin = Vector2.zero; ovR.offsetMax = Vector2.zero;
        ov.GetComponent<Image>().color = new Color(0.04f, 0.06f, 0.10f, 0.92f);

        // Card
        GameObject card = new GameObject("Card", typeof(RectTransform), typeof(Image));
        card.transform.SetParent(root.transform, false);
        RectTransform cardR      = card.GetComponent<RectTransform>();
        cardR.anchorMin          = new Vector2(0.5f, 0.5f);
        cardR.anchorMax          = new Vector2(0.5f, 0.5f);
        cardR.pivot              = new Vector2(0.5f, 0.5f);
        cardR.anchoredPosition   = Vector2.zero;
        cardR.sizeDelta          = new Vector2(820f, 480f);
        card.GetComponent<Image>().color = Color.white;

        // Accent bar
        GameObject acc = new GameObject("AccentBar", typeof(RectTransform), typeof(Image));
        acc.transform.SetParent(card.transform, false);
        RectTransform acR  = acc.GetComponent<RectTransform>();
        acR.anchorMin      = new Vector2(0f, 0f); acR.anchorMax = new Vector2(0f, 1f);
        acR.pivot          = new Vector2(0f, 0.5f);
        acR.offsetMin      = Vector2.zero; acR.offsetMax = new Vector2(6f, 0f);
        acR.sizeDelta      = new Vector2(6f, 0f);
        acc.GetComponent<Image>().color = new Color32(0x21, 0x96, 0xF3, 0xFF);

        // Title
        GameObject tGo = new GameObject("TitleText", typeof(RectTransform), typeof(TextMeshProUGUI));
        tGo.transform.SetParent(card.transform, false);
        RectTransform tR    = tGo.GetComponent<RectTransform>();
        tR.anchorMin        = new Vector2(0.5f, 1f); tR.anchorMax = new Vector2(0.5f, 1f);
        tR.pivot            = new Vector2(0.5f, 1f);
        tR.anchoredPosition = new Vector2(0f, -40f);
        tR.sizeDelta        = new Vector2(740f, 60f);
        titleText                    = tGo.GetComponent<TextMeshProUGUI>();
        titleText.fontSize           = 36f;
        titleText.fontStyle          = FontStyles.Bold;
        titleText.color              = new Color32(0x1A, 0x1A, 0x2E, 0xFF);
        titleText.alignment          = TextAlignmentOptions.Center;
        titleText.textWrappingMode   = TextWrappingModes.Normal;

        // Divider
        GameObject dv = new GameObject("Divider", typeof(RectTransform), typeof(Image));
        dv.transform.SetParent(card.transform, false);
        RectTransform dvR   = dv.GetComponent<RectTransform>();
        dvR.anchorMin       = new Vector2(0.5f, 1f); dvR.anchorMax = new Vector2(0.5f, 1f);
        dvR.pivot           = new Vector2(0.5f, 1f);
        dvR.anchoredPosition = new Vector2(0f, -108f);
        dvR.sizeDelta       = new Vector2(700f, 2f);
        dv.GetComponent<Image>().color = new Color32(0xE0, 0xE0, 0xE0, 0xFF);

        // Body
        GameObject bGo = new GameObject("BodyText", typeof(RectTransform), typeof(TextMeshProUGUI));
        bGo.transform.SetParent(card.transform, false);
        RectTransform bR    = bGo.GetComponent<RectTransform>();
        bR.anchorMin        = new Vector2(0.5f, 1f); bR.anchorMax = new Vector2(0.5f, 1f);
        bR.pivot            = new Vector2(0.5f, 1f);
        bR.anchoredPosition = new Vector2(0f, -126f);
        bR.sizeDelta        = new Vector2(740f, 280f);
        bodyText                   = bGo.GetComponent<TextMeshProUGUI>();
        bodyText.fontSize          = 21f;
        bodyText.color             = new Color32(0x33, 0x33, 0x33, 0xFF);
        bodyText.alignment         = TextAlignmentOptions.Center;
        bodyText.textWrappingMode  = TextWrappingModes.Normal;
        bodyText.lineSpacing       = 5f;

        // Button
        GameObject btnGo = new GameObject("CloseButton",
            typeof(RectTransform), typeof(Image), typeof(Button));
        btnGo.transform.SetParent(card.transform, false);
        RectTransform btnR   = btnGo.GetComponent<RectTransform>();
        btnR.anchorMin       = new Vector2(1f, 0f); btnR.anchorMax = new Vector2(1f, 0f);
        btnR.pivot           = new Vector2(1f, 0f);
        btnR.anchoredPosition = new Vector2(-32f, 32f);
        btnR.sizeDelta       = new Vector2(200f, 52f);
        btnGo.GetComponent<Image>().color = new Color32(0x21, 0x96, 0xF3, 0xFF);
        closeButton               = btnGo.GetComponent<Button>();
        closeButton.targetGraphic = btnGo.GetComponent<Image>();

        GameObject btnTGo = new GameObject("ButtonText", typeof(RectTransform), typeof(TextMeshProUGUI));
        btnTGo.transform.SetParent(btnGo.transform, false);
        RectTransform btnTR  = btnTGo.GetComponent<RectTransform>();
        btnTR.anchorMin      = Vector2.zero; btnTR.anchorMax = Vector2.one;
        btnTR.offsetMin      = Vector2.zero; btnTR.offsetMax = Vector2.zero;
        TextMeshProUGUI btnTmp  = btnTGo.GetComponent<TextMeshProUGUI>();
        btnTmp.text             = "Selesai";
        btnTmp.fontSize         = 22f;
        btnTmp.fontStyle        = FontStyles.Bold;
        btnTmp.color            = Color.white;
        btnTmp.alignment        = TextAlignmentOptions.Center;

        root.SetActive(false);
    }

    private static Transform FindHudCanvas()
    {
        Canvas[] canvases = FindObjectsByType<Canvas>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        for (int i = 0; i < canvases.Length; i++)
            if (canvases[i].gameObject.name == "HUD_Canvas")
                return canvases[i].transform;

        return null;
    }

    private bool TryBindExistingPanel(Transform parent)
    {
        if (parent == null)
            return false;

        Transform panelTransform = FindChildByName(parent, "EndingPanel");
        if (panelTransform == null)
            return false;

        EndingPanelBinder binder = panelTransform.GetComponent<EndingPanelBinder>();
        if (binder != null)
        {
            panelRoot = binder.panelRoot != null ? binder.panelRoot : panelTransform.GetComponent<RectTransform>();
            panelGroup = binder.panelGroup != null ? binder.panelGroup : panelTransform.GetComponent<CanvasGroup>();
            titleText = binder.titleText;
            bodyText = binder.bodyText;
            closeButton = binder.closeButton;
        }
        else
        {
            panelRoot = panelTransform as RectTransform ?? panelTransform.GetComponent<RectTransform>();
            panelGroup = panelTransform.GetComponent<CanvasGroup>();

            Transform card = panelTransform.Find("Card");
            titleText = FindChildByName(card, "TitleText")?.GetComponent<TextMeshProUGUI>();
            bodyText = FindChildByName(card, "BodyText")?.GetComponent<TextMeshProUGUI>();
            closeButton = FindChildByName(card, "CloseButton")?.GetComponent<Button>();
        }

        if (panelRoot == null || panelGroup == null || titleText == null || bodyText == null || closeButton == null)
            return false;

        panelGroup.alpha = 0f;
        panelGroup.blocksRaycasts = false;
        panelGroup.interactable = false;
        panelRoot.gameObject.SetActive(false);
        return true;
    }

    private static Transform FindChildByName(Transform root, string targetName)
    {
        if (root == null || string.IsNullOrWhiteSpace(targetName)) return null;
        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
            if (children[i].name == targetName) return children[i];
        return null;
    }

    private class EndingDoctorActor : IDialogueActor
    {
        public System.Collections.Generic.List<DialogueChoiceData> GetAvailableChoices(DialogueNodeData node)
        {
            return node?.choices ?? new System.Collections.Generic.List<DialogueChoiceData>();
        }

        public void ApplyConsequence(DialogueConsequence consequence) { }
    }
}