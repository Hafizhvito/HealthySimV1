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

    [Header("Fonts")]
    [SerializeField] private TMP_FontAsset titleFont;
    [SerializeField] private TMP_FontAsset bodyFont;
    [SerializeField] private TMP_FontAsset buttonFont;

    private const string ModalKey = "ending_panel";
    private RectTransform panelRoot;
    private CanvasGroup panelGroup;
    private TextMeshProUGUI titleText;
    private TextMeshProUGUI bodyText;
    private Button closeButton;
    private RectTransform recapPanelRoot;
    private CanvasGroup recapPanelGroup;
    private TextMeshProUGUI recapTitleText;
    private TextMeshProUGUI recapBodyText;
    private Button recapCloseButton;
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
        graph.npcDisplayName = "dr. Sri Wuryanti, MS, Sp.GK";
        graph.startNodeId    = "start";
        graph.backgroundResourcePath = "Backgrounds/bg_hospital";

        var nodes = new System.Collections.Generic.List<DialogueNodeData>();

        // Node 1 — pembuka hangat
        nodes.Add(new DialogueNodeData
        {
            nodeId = "start",
            fallbackLine = BuildDoctorOpening(type, gender),
            choices = new System.Collections.Generic.List<DialogueChoiceData>
            {
                new DialogueChoiceData { choiceText = "Bagaimana hasilnya, Dok?", nextNodeId = "phase_review" }
            }
        });

        // Node 2 — review per fase
        nodes.Add(new DialogueNodeData
        {
            nodeId = "phase_review",
            fallbackLine = BuildPhaseReview(type, youthScore, adultScore, seniorScore),
            choices = new System.Collections.Generic.List<DialogueChoiceData>
            {
                new DialogueChoiceData { choiceText = "Apa kesimpulannya, Dok?", nextNodeId = "conclusion" }
            }
        });

        // Node 3 — kesimpulan klinis
        nodes.Add(new DialogueNodeData
        {
            nodeId = "conclusion",
            fallbackLine = BuildDoctorConclusion(type, gender),
            choices = new System.Collections.Generic.List<DialogueChoiceData>
            {
                new DialogueChoiceData { choiceText = "Ada pesan terakhir untuk saya?", nextNodeId = "farewell" }
            }
        });

        // Node 4 — pesan penutup (story-driven)
        nodes.Add(new DialogueNodeData
        {
            nodeId = "farewell",
            fallbackLine = BuildDoctorFarewell(type, gender),
            choices = new System.Collections.Generic.List<DialogueChoiceData>
            {
                new DialogueChoiceData { choiceText = "Terima kasih atas segalanya, Dok.", nextNodeId = "" }
            },
            isConversationEnd = true,
            isTerminal = true
        });

        graph.nodes = nodes;
        return graph;
    }

    private static string BuildDoctorFarewell(EndingType type, PlayerStats.Gender gender)
    {
        bool isFemale = gender == PlayerStats.Gender.Female;

        return type switch
        {
            EndingType.Good =>
                "Kamu sudah membuktikan sesuatu yang penting: bahwa pilihan kecil setiap hari bisa mengubah hidup. " +
                "Banyak orang tahu teorinya, tapi kamu menjalaninya. " +
                (isFemale
                    ? "Sebagai perempuan, tubuhmu merespons dengan indah terhadap konsistensi. Pertahankan."
                    : "Tubuhmu sekarang ada di titik terbaiknya. Jangan berhenti.") +
                " Ini bukan akhir, ini awal dari kebiasaan yang akan menemanimu seumur hidup.",

            EndingType.Neutral =>
                "Perjalananmu belum sempurna, dan itu tidak apa-apa. Yang penting, kamu punya kesadaran. " +
                "Mulai dari sini, kamu bisa memilih: terus seperti ini, atau naik satu level. " +
                "Satu perubahan kecil per minggu, itu sudah cukup. " +
                "Pintu klinik ini selalu terbuka untukmu.",

            EndingType.Bad =>
                "Aku tahu ini berat. Tapi fakta bahwa kamu duduk di sini, mendengarkan, itu sudah langkah pertama. " +
                "Tubuh manusia punya kemampuan luar biasa untuk pulih, asal diberi kesempatan. " +
                "Mulai dari besok, satu hal saja: makan teratur. Kemudian tidur cukup. Kemudian bergerak. " +
                "Pelan-pelan. Tidak ada yang memintamu sempurna dalam sehari. " +
                "Tapi tolong, jangan abaikan tubuhmu lebih lama lagi.",

            _ => "Jaga dirimu. Sampai jumpa."
        };
    }

    private static string BuildDoctorOpening(EndingType type, PlayerStats.Gender gender)
    {
        bool isFemale = gender == PlayerStats.Gender.Female;

        return type switch
        {
            EndingType.Good => isFemale
                ? "Selamat datang kembali. Aku sudah selesai menganalisa semua data kesehatanmu dari awal hingga sekarang. " +
                  "Dan... aku punya kabar yang sangat baik. Kebiasaan kecil yang kamu jaga tiap hari benar-benar bekerja. " +
                  "Tubuhmu merespons dengan indah."
                : "Selamat datang kembali. Aku sudah menganalisa seluruh riwayat kesehatanmu. " +
                  "Hasil pemeriksaanmu bagus dan konsisten. Ini bukan kebetulan. " +
                  "Ini hasil dari pilihan-pilihan yang kamu buat setiap hari.",

            EndingType.Neutral => isFemale
                ? "Silakan duduk. Aku sudah meninjau semua datamu dari fase pertama hingga sekarang. " +
                  "Ada yang sudah baik, tapi ritme harianmu belum sepenuhnya stabil. " +
                  "Ini belum darurat, tapi kita perlu bicara tentang apa yang bisa diperbaiki."
                : "Silakan duduk. Aku sudah melihat seluruh perjalanan kesehatanmu. " +
                  "Hasilnya campuran: ada kemajuan di beberapa area, tapi ada kebiasaan yang masih bocor di sana-sini. " +
                  "Kita bisa rapikan ini bersama.",

            EndingType.Bad =>
                "Terima kasih sudah datang. Aku akan bicara jujur denganmu hari ini, karena menurutku kamu berhak tahu. " +
                "Aku sudah menganalisa semua data dari awal perjalananmu hingga sekarang, dan... " +
                "tubuhmu sedang kewalahan. Ada tanda-tanda yang perlu ditangani serius mulai hari ini.",

            _ => "Silakan duduk. Mari kita bahas perjalanan kesehatanmu bersama."
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
        SetAccentColor(type, panelRoot);

        string narrative = GetNarrative(type, gender);
        string diseaseInfo = BuildDiseaseInfoSection(PlayerStats.Instance, type);
        if (!string.IsNullOrEmpty(diseaseInfo))
            narrative = narrative + "\n\n" + diseaseInfo;
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
        ForceRebuildTextLayout(bodyText);
        closeButton.interactable = true;

        bool closed = false;
        closeButton.onClick.RemoveAllListeners();
        closeButton.onClick.AddListener(() => closed = true);
        while (!closed) yield return null;

        panelGroup.alpha          = 0f;
        panelGroup.blocksRaycasts = false;
        panelGroup.interactable   = false;
        panelRoot.gameObject.SetActive(false);

        if (recapPanelRoot != null)
        {
            recapPanelRoot.gameObject.SetActive(true);
            recapPanelGroup.alpha          = 1f;
            recapPanelGroup.blocksRaycasts = true;
            recapPanelGroup.interactable   = true;
            recapPanelRoot.SetAsLastSibling();
            SetAccentColor(type, recapPanelRoot);
            recapTitleText.text = "Rekap Perjalanan Hidupmu";
            recapBodyText.text  = BuildRecapContent(PlayerStats.Instance);
            ForceRebuildTextLayout(recapBodyText);

            bool recapClosed = false;
            recapCloseButton.onClick.RemoveAllListeners();
            recapCloseButton.onClick.AddListener(() => recapClosed = true);
            while (!recapClosed) yield return null;

            recapPanelGroup.alpha          = 0f;
            recapPanelGroup.blocksRaycasts = false;
            recapPanelGroup.interactable   = false;
            recapPanelRoot.gameObject.SetActive(false);
        }

        if (ModalStateManager.Instance != null)
            ModalStateManager.Instance.CloseModal(ModalKey);

        // Ending selesai, credits diputar setelah pemain tidur lagi.
        isShowing = false;
        creditsPending = true;
        creditsAfterDay = TimeManager.Instance != null ? TimeManager.Instance.CurrentDayNumber : -1;
    }

    private void SetAccentColor(EndingType type, RectTransform targetRoot = null)
    {
        // Cari AccentBar di dalam card
        RectTransform root = targetRoot != null ? targetRoot : panelRoot;
        if (root == null) return;
        Transform card = root.Find("Card");
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

    private static void ForceRebuildTextLayout(TextMeshProUGUI text)
    {
        if (text == null) return;
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(text.rectTransform);
        RectTransform parent = text.rectTransform.parent as RectTransform;
        if (parent != null)
            text.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, parent.rect.width);
        text.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, text.preferredHeight);
    }

    private static void ApplyFont(TextMeshProUGUI text, TMP_FontAsset font)
    {
        if (text == null || font == null) return;
        text.font = font;
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

    private static string BuildDiseaseInfoSection(PlayerStats stats, EndingType endingType)
    {
        if (stats == null || stats.TotalDaysEvaluated <= 0)
            return string.Empty;

        var risks = BuildDiseaseRisks(stats);
        risks.Sort((a, b) => b.Score.CompareTo(a.Score));

        const float riskThreshold = 0.35f;
        const float warningThreshold = 0.20f;

        if (endingType == EndingType.Good)
        {
            bool anyMild = risks.Count > 0 && risks[0].Score >= warningThreshold;
            if (!anyMild)
                return "Tubuhmu dalam kondisi baik. Tidak ada indikasi risiko penyakit yang berarti. Pertahankan.";

            return "Meskipun kondisimu secara umum baik, tetap jaga pola hidupmu agar risiko kesehatan tetap rendah di masa depan.";
        }

        int count = 0;
        for (int i = 0; i < risks.Count && count < 3; i++)
        {
            if (risks[i].Score >= warningThreshold) count++;
        }
        if (count == 0)
            return string.Empty;

        string header = endingType == EndingType.Bad
            ? "Risiko penyakit yang muncul dari pola hidupmu:"
            : "Beberapa area kesehatan yang perlu diperhatikan:";

        var sb = new System.Text.StringBuilder();
        sb.Append(header);

        int shown = 0;
        for (int i = 0; i < risks.Count && shown < count; i++)
        {
            DiseaseRisk risk = risks[i];
            if (risk.Score < warningThreshold) continue;

            sb.Append("\n\n• ");
            sb.Append(risk.Name);
            if (risk.Score >= riskThreshold)
                sb.Append(" [risiko tinggi]");
            sb.Append("\n  ");
            sb.Append(risk.Description);
            if (!string.IsNullOrEmpty(risk.Reason))
            {
                sb.Append("\n  ");
                sb.Append(risk.Reason);
            }
            shown++;
        }

        if (endingType == EndingType.Bad)
            sb.Append("\n\nDi dunia nyata, pola hidup seperti ini bisa memicu penyakit kronis. Tapi selalu ada kesempatan untuk berubah.");

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

    private string BuildRecapContent(PlayerStats stats)
    {
        string doctorMessage = pendingEndingType switch
        {
            EndingType.Good    => "Selamat! Kamu telah menjalani gaya hidup yang sehat dan seimbang.",
            EndingType.Neutral => "Ada kemajuan, namun masih banyak ruang untuk perbaikan ke depannya.",
            EndingType.Bad     => "Pola hidup yang buruk memberikan dampak serius. Mulailah berubah sekarang.",
            _                  => "Tetap jaga kesehatanmu ke depan."
        };

        if (stats == null)
            return "Data statistik belum tersedia.\n\nPesan dr. Sri: " + doctorMessage;

        float[] scores = stats.CommittedPhaseScores;
        float total = 0f;
        int count = 0;
        for (int i = 0; i < 3; i++)
        {
            if (scores != null && i < scores.Length)
            {
                total += scores[i];
                count++;
            }
        }

        float avg = count > 0 ? total / count : 50f;
        string avgLabel = avg >= 65f ? "Baik" : avg >= 40f ? "Cukup" : "Buruk";

        int warningCount = 0;
        var sections = new System.Collections.Generic.List<string>();

        var sectionA = new System.Text.StringBuilder();
        sectionA.Append("Gizi Harian:");
        sectionA.Append($"\n- Rata-rata kesehatan fase: {avgLabel}");
        if (stats.HighCalorieDays > 3)
        {
            sectionA.Append($"\n! {stats.HighCalorieDays} hari asupan kalori berlebihan");
            warningCount++;
        }
        if (stats.LowCalorieDays > 3)
        {
            sectionA.Append($"\n! {stats.LowCalorieDays} hari asupan kalori kurang");
            warningCount++;
        }
        if (stats.PoorDietDays > 3)
        {
            sectionA.Append($"\n! {stats.PoorDietDays} hari pola makan buruk");
            warningCount++;
        }
        if (stats.NoFoodDays > 0)
        {
            sectionA.Append($"\n! {stats.NoFoodDays} hari tidak makan sama sekali");
            warningCount++;
        }
        sections.Add(sectionA.ToString());

        var sectionB = new System.Text.StringBuilder();
        if (stats.SkippedGymDays > 5)
        {
            sectionB.Append($"! Sering melewatkan gym ({stats.SkippedGymDays} hari)");
            warningCount++;
        }
        if (stats.SkippedWorkDays > 3)
        {
            if (sectionB.Length > 0) sectionB.Append("\n");
            sectionB.Append($"! Sering absen kerja ({stats.SkippedWorkDays} hari)");
            warningCount++;
        }
        if (stats.OverworkedDays > 3)
        {
            if (sectionB.Length > 0) sectionB.Append("\n");
            sectionB.Append("! Terlalu sering memaksakan diri bekerja");
            warningCount++;
        }
        if (sectionB.Length > 0)
            sections.Add("Aktivitas:\n" + sectionB.ToString());

        var sectionC = new System.Text.StringBuilder();
        if (stats.DisturbedSleepDays > 3)
        {
            sectionC.Append($"! {stats.DisturbedSleepDays} hari tidur terganggu");
            warningCount++;
        }
        if (stats.LowEnergySleepDays > 3)
        {
            if (sectionC.Length > 0) sectionC.Append("\n");
            sectionC.Append($"! {stats.LowEnergySleepDays} hari tidur dengan energi sangat rendah");
            warningCount++;
        }
        if (sectionC.Length > 0)
            sections.Add("Tidur:\n" + sectionC.ToString());

        if (warningCount == 0)
            return "Luar biasa! Tidak ada catatan negatif selama perjalananmu. Pertahankan!\n\nPesan dr. Sri: " + doctorMessage;

        sections.Add("Pesan dr. Sri: " + doctorMessage);
        return string.Join("\n\n", sections);
    }

    // ── Panel builder (tidak berubah dari versi asli) ────────────────

    private void EnsurePanel()
    {
        if (panelRoot != null && recapPanelRoot != null) return;

        Transform parent = FindHudCanvas();
        if (TryBindExistingPanel(parent))
        {
            if (recapPanelRoot != null)
                return;
        }

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
        cardR.sizeDelta          = new Vector2(820f, 520f);
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
        ApplyFont(titleText, titleFont);

        // Divider
        GameObject dv = new GameObject("Divider", typeof(RectTransform), typeof(Image));
        dv.transform.SetParent(card.transform, false);
        RectTransform dvR   = dv.GetComponent<RectTransform>();
        dvR.anchorMin       = new Vector2(0.5f, 1f); dvR.anchorMax = new Vector2(0.5f, 1f);
        dvR.pivot           = new Vector2(0.5f, 1f);
        dvR.anchoredPosition = new Vector2(0f, -108f);
        dvR.sizeDelta       = new Vector2(700f, 2f);
        dv.GetComponent<Image>().color = new Color32(0xE0, 0xE0, 0xE0, 0xFF);

        // Body (ScrollRect)
        GameObject scrollGo = new GameObject("BodyScroll", typeof(RectTransform), typeof(ScrollRect));
        scrollGo.transform.SetParent(card.transform, false);
        RectTransform scrollR = scrollGo.GetComponent<RectTransform>();
        scrollR.anchorMin        = new Vector2(0.5f, 1f);
        scrollR.anchorMax        = new Vector2(0.5f, 1f);
        scrollR.pivot            = new Vector2(0.5f, 1f);
        scrollR.anchoredPosition = new Vector2(0f, -126f);
        scrollR.sizeDelta        = new Vector2(740f, 320f);

        GameObject viewportGo = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
        viewportGo.transform.SetParent(scrollGo.transform, false);
        RectTransform viewportR = viewportGo.GetComponent<RectTransform>();
        viewportR.anchorMin = Vector2.zero; viewportR.anchorMax = Vector2.one;
        viewportR.offsetMin = Vector2.zero; viewportR.offsetMax = Vector2.zero;

        GameObject bGo = new GameObject("BodyText", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(ContentSizeFitter));
        bGo.transform.SetParent(viewportGo.transform, false);
        RectTransform bR    = bGo.GetComponent<RectTransform>();
        bR.anchorMin        = new Vector2(0f, 1f); bR.anchorMax = new Vector2(1f, 1f);
        bR.pivot            = new Vector2(0.5f, 1f);
        bR.anchoredPosition = Vector2.zero;
        bR.sizeDelta        = Vector2.zero;
        ContentSizeFitter bFit = bGo.GetComponent<ContentSizeFitter>();
        bFit.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        bFit.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;
        bodyText                   = bGo.GetComponent<TextMeshProUGUI>();
        bodyText.fontSize          = 21f;
        bodyText.color             = new Color32(0x33, 0x33, 0x33, 0xFF);
        bodyText.alignment         = TextAlignmentOptions.TopLeft;
        bodyText.textWrappingMode  = TextWrappingModes.Normal;
        bodyText.lineSpacing       = 5f;
        bodyText.margin            = new Vector4(6f, 0f, 6f, 0f);
        ApplyFont(bodyText, bodyFont);

        ScrollRect bodyScroll = scrollGo.GetComponent<ScrollRect>();
        bodyScroll.content    = bR;
        bodyScroll.viewport   = viewportR;
        bodyScroll.horizontal = false;
        bodyScroll.vertical   = true;
        bodyScroll.movementType = ScrollRect.MovementType.Clamped;

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
        btnTmp.text             = "Lanjut →";
        btnTmp.fontSize         = 22f;
        btnTmp.fontStyle        = FontStyles.Bold;
        btnTmp.color            = Color.white;
        btnTmp.alignment        = TextAlignmentOptions.Center;
        ApplyFont(btnTmp, buttonFont);

        // Recap panel
        GameObject recapRoot = new GameObject("EndingRecapPanel", typeof(RectTransform), typeof(CanvasGroup), typeof(Canvas), typeof(GraphicRaycaster));
        recapRoot.transform.SetParent(parent, false);
        recapPanelRoot            = recapRoot.GetComponent<RectTransform>();
        recapPanelRoot.anchorMin  = Vector2.zero;
        recapPanelRoot.anchorMax  = Vector2.one;
        recapPanelRoot.offsetMin  = Vector2.zero;
        recapPanelRoot.offsetMax  = Vector2.zero;
        recapPanelGroup           = recapRoot.GetComponent<CanvasGroup>();
        recapPanelGroup.alpha          = 0f;
        recapPanelGroup.blocksRaycasts = false;
        recapPanelGroup.interactable   = false;

        Canvas recapCanvas = recapRoot.GetComponent<Canvas>();
        recapCanvas.overrideSorting = true;
        Canvas parentCanvas = parent.GetComponent<Canvas>() ?? parent.GetComponentInParent<Canvas>();
        recapCanvas.sortingOrder = parentCanvas != null ? parentCanvas.sortingOrder + 1 : 301;

        // Overlay
        GameObject rov = new GameObject("Overlay", typeof(RectTransform), typeof(Image));
        rov.transform.SetParent(recapRoot.transform, false);
        RectTransform rovR = rov.GetComponent<RectTransform>();
        rovR.anchorMin = Vector2.zero; rovR.anchorMax = Vector2.one;
        rovR.offsetMin = Vector2.zero; rovR.offsetMax = Vector2.zero;
        rov.GetComponent<Image>().color = new Color(0.04f, 0.06f, 0.10f, 0.92f);

        // Card
        GameObject rcard = new GameObject("Card", typeof(RectTransform), typeof(Image));
        rcard.transform.SetParent(recapRoot.transform, false);
        RectTransform rcardR     = rcard.GetComponent<RectTransform>();
        rcardR.anchorMin         = new Vector2(0.5f, 0.5f);
        rcardR.anchorMax         = new Vector2(0.5f, 0.5f);
        rcardR.pivot             = new Vector2(0.5f, 0.5f);
        rcardR.anchoredPosition  = Vector2.zero;
        rcardR.sizeDelta         = new Vector2(820f, 520f);
        rcard.GetComponent<Image>().color = Color.white;

        // Accent bar
        GameObject racc = new GameObject("AccentBar", typeof(RectTransform), typeof(Image));
        racc.transform.SetParent(rcard.transform, false);
        RectTransform raccR = racc.GetComponent<RectTransform>();
        raccR.anchorMin     = new Vector2(0f, 0f); raccR.anchorMax = new Vector2(0f, 1f);
        raccR.pivot         = new Vector2(0f, 0.5f);
        raccR.offsetMin     = Vector2.zero; raccR.offsetMax = new Vector2(6f, 0f);
        raccR.sizeDelta     = new Vector2(6f, 0f);
        racc.GetComponent<Image>().color = new Color32(0x21, 0x96, 0xF3, 0xFF);

        // Title
        GameObject rtGo = new GameObject("TitleText", typeof(RectTransform), typeof(TextMeshProUGUI));
        rtGo.transform.SetParent(rcard.transform, false);
        RectTransform rtR   = rtGo.GetComponent<RectTransform>();
        rtR.anchorMin       = new Vector2(0.5f, 1f); rtR.anchorMax = new Vector2(0.5f, 1f);
        rtR.pivot           = new Vector2(0.5f, 1f);
        rtR.anchoredPosition = new Vector2(0f, -40f);
        rtR.sizeDelta       = new Vector2(740f, 60f);
        recapTitleText                   = rtGo.GetComponent<TextMeshProUGUI>();
        recapTitleText.fontSize          = 36f;
        recapTitleText.fontStyle         = FontStyles.Bold;
        recapTitleText.color             = new Color32(0x1A, 0x1A, 0x2E, 0xFF);
        recapTitleText.alignment         = TextAlignmentOptions.Center;
        recapTitleText.textWrappingMode  = TextWrappingModes.Normal;
        ApplyFont(recapTitleText, titleFont);

        // Divider
        GameObject rdv = new GameObject("Divider", typeof(RectTransform), typeof(Image));
        rdv.transform.SetParent(rcard.transform, false);
        RectTransform rdvR  = rdv.GetComponent<RectTransform>();
        rdvR.anchorMin      = new Vector2(0.5f, 1f); rdvR.anchorMax = new Vector2(0.5f, 1f);
        rdvR.pivot          = new Vector2(0.5f, 1f);
        rdvR.anchoredPosition = new Vector2(0f, -108f);
        rdvR.sizeDelta      = new Vector2(700f, 2f);
        rdv.GetComponent<Image>().color = new Color32(0xE0, 0xE0, 0xE0, 0xFF);

        // Body (ScrollRect)
        GameObject rscrollGo = new GameObject("BodyScroll", typeof(RectTransform), typeof(ScrollRect));
        rscrollGo.transform.SetParent(rcard.transform, false);
        RectTransform rscrollR = rscrollGo.GetComponent<RectTransform>();
        rscrollR.anchorMin        = new Vector2(0.5f, 1f);
        rscrollR.anchorMax        = new Vector2(0.5f, 1f);
        rscrollR.pivot            = new Vector2(0.5f, 1f);
        rscrollR.anchoredPosition = new Vector2(0f, -126f);
        rscrollR.sizeDelta        = new Vector2(740f, 320f);

        GameObject rviewportGo = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
        rviewportGo.transform.SetParent(rscrollGo.transform, false);
        RectTransform rviewportR = rviewportGo.GetComponent<RectTransform>();
        rviewportR.anchorMin = Vector2.zero; rviewportR.anchorMax = Vector2.one;
        rviewportR.offsetMin = Vector2.zero; rviewportR.offsetMax = Vector2.zero;

        GameObject rbGo = new GameObject("BodyText", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(ContentSizeFitter));
        rbGo.transform.SetParent(rviewportGo.transform, false);
        RectTransform rbR   = rbGo.GetComponent<RectTransform>();
        rbR.anchorMin       = new Vector2(0f, 1f); rbR.anchorMax = new Vector2(1f, 1f);
        rbR.pivot           = new Vector2(0.5f, 1f);
        rbR.anchoredPosition = Vector2.zero;
        rbR.sizeDelta       = Vector2.zero;
        ContentSizeFitter rbFit = rbGo.GetComponent<ContentSizeFitter>();
        rbFit.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        rbFit.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;
        recapBodyText                   = rbGo.GetComponent<TextMeshProUGUI>();
        recapBodyText.fontSize          = 21f;
        recapBodyText.color             = new Color32(0x33, 0x33, 0x33, 0xFF);
        recapBodyText.alignment         = TextAlignmentOptions.TopLeft;
        recapBodyText.textWrappingMode  = TextWrappingModes.Normal;
        recapBodyText.lineSpacing       = 5f;
        recapBodyText.margin            = new Vector4(6f, 0f, 6f, 0f);
        ApplyFont(recapBodyText, bodyFont);

        ScrollRect recapScroll = rscrollGo.GetComponent<ScrollRect>();
        recapScroll.content    = rbR;
        recapScroll.viewport   = rviewportR;
        recapScroll.horizontal = false;
        recapScroll.vertical   = true;
        recapScroll.movementType = ScrollRect.MovementType.Clamped;

        // Button
        GameObject rbtnGo = new GameObject("CloseButton",
            typeof(RectTransform), typeof(Image), typeof(Button));
        rbtnGo.transform.SetParent(rcard.transform, false);
        RectTransform rbtnR  = rbtnGo.GetComponent<RectTransform>();
        rbtnR.anchorMin      = new Vector2(1f, 0f); rbtnR.anchorMax = new Vector2(1f, 0f);
        rbtnR.pivot          = new Vector2(1f, 0f);
        rbtnR.anchoredPosition = new Vector2(-32f, 32f);
        rbtnR.sizeDelta      = new Vector2(200f, 52f);
        rbtnGo.GetComponent<Image>().color = new Color32(0x21, 0x96, 0xF3, 0xFF);
        recapCloseButton               = rbtnGo.GetComponent<Button>();
        recapCloseButton.targetGraphic = rbtnGo.GetComponent<Image>();

        GameObject rbtnTGo = new GameObject("ButtonText", typeof(RectTransform), typeof(TextMeshProUGUI));
        rbtnTGo.transform.SetParent(rbtnGo.transform, false);
        RectTransform rbtnTR = rbtnTGo.GetComponent<RectTransform>();
        rbtnTR.anchorMin     = Vector2.zero; rbtnTR.anchorMax = Vector2.one;
        rbtnTR.offsetMin     = Vector2.zero; rbtnTR.offsetMax = Vector2.zero;
        TextMeshProUGUI rbtnTmp = rbtnTGo.GetComponent<TextMeshProUGUI>();
        rbtnTmp.text            = "Selesai";
        rbtnTmp.fontSize        = 22f;
        rbtnTmp.fontStyle       = FontStyles.Bold;
        rbtnTmp.color           = Color.white;
        rbtnTmp.alignment       = TextAlignmentOptions.Center;
        ApplyFont(rbtnTmp, buttonFont);

        panelRoot.gameObject.SetActive(false);
        recapPanelRoot.gameObject.SetActive(false);
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