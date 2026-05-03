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
        if (isShowing) return;
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
                ? "Selamat datang kembali. Saya senang bisa menyampaikan kabar baik hari ini. " +
                  "Hasil pemeriksaan kamu secara keseluruhan sangat memuaskan. " +
                  "Tubuhmu merespons dengan baik terhadap kebiasaan yang sudah kamu bangun selama ini."
                : "Selamat datang kembali. Langsung saja, hasil pemeriksaan kamu hari ini sangat bagus. " +
                  "Kondisi fisikmu berada di angka yang saya harapkan untuk usiamu sekarang. " +
                  "Apa yang sudah kamu lakukan selama ini jelas memberikan dampak nyata.",

            EndingType.Neutral => isFemale
                ? "Silakan duduk. Saya sudah periksa semua data kamu dari waktu ke waktu. " +
                  "Ada kabar baik dan ada hal yang perlu kita bicarakan bersama. " +
                  "Kondisimu tidak mengkhawatirkan, tapi ada beberapa catatan penting."
                : "Silakan duduk. Terima kasih sudah datang untuk pemeriksaan ini. " +
                  "Hasilnya cukup campuran. Ada yang sudah berjalan baik, " +
                  "tapi ada juga beberapa hal yang sebaiknya mulai kita perhatikan sekarang.",

            EndingType.Bad =>
                "Terima kasih sudah meluangkan waktu untuk datang ke sini. " +
                "Saya akan langsung bicara jujur karena saya rasa itu lebih baik dari pada berputar-putar. " +
                "Hasil pemeriksaan kamu menunjukkan beberapa kondisi yang perlu segera ditangani.",

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

        return $"Mari saya jelaskan rekam jejakmu per fase.\n\n" +
               $"Di masa muda, kondisimu tergolong {youthLabel}. {youthComment}\n\n" +
               $"Memasuki usia dewasa, kesehatanmu {adultLabel}. {adultComment}\n\n" +
               $"Di fase lansia ini, kondisimu {seniorLabel}. {seniorComment}\n\n" +
               $"{trend}";
    }

    private static string BuildPhaseComment(string fase, float score, EndingType type)
    {
        if (type == EndingType.Good)
        {
            if (score >= 65f) return "Pilihan harianmu di fase ini konsisten dan berdampak positif.";
            return "Ada beberapa hari yang kurang optimal, tapi secara keseluruhan kamu tetap di jalur yang baik.";
        }

        if (type == EndingType.Neutral)
        {
            if (score >= 65f) return "Fase ini adalah yang terbaik dalam perjalananmu.";
            if (score >= 40f) return "Ada usaha yang terlihat, meski belum konsisten sepenuhnya.";
            return "Di fase ini banyak kebiasaan yang belum mendukung kesehatanmu.";
        }

        // Bad
        if (score >= 65f) return "Ironisnya, fase ini adalah yang paling baik dalam rekam jejakmu.";
        if (score >= 40f) return "Ada momen baik, tapi tidak cukup untuk mengimbangi kebiasaan yang kurang sehat.";
        return "Di fase ini tubuhmu sudah memberi sinyal, tapi belum sempat ditangani dengan serius.";
    }

    private static string DetectTrend(float youth, float adult, float senior)
    {
        bool naik   = senior > adult && adult > youth;
        bool turun  = senior < adult && adult < youth;
        bool stabil = Mathf.Abs(senior - youth) < 10f;

        if (naik)
            return "Yang menarik, kondisimu justru membaik seiring waktu. Itu jarang terjadi dan patut diapresiasi.";
        if (turun)
            return "Dari data ini, kondisimu menunjukkan tren menurun dari waktu ke waktu. Ini yang perlu kita tangani.";
        if (stabil)
            return "Kondisimu relatif stabil dari fase ke fase. Tapi stabil di angka rendah tetap perlu perhatian.";

        return "Kondisimu berfluktuasi cukup signifikan antar fase. Konsistensi adalah kunci yang masih perlu dibangun.";
    }

    private static string BuildDoctorConclusion(EndingType type, PlayerStats.Gender gender)
    {
        bool isFemale = gender == PlayerStats.Gender.Female;

        return type switch
        {
            EndingType.Good => isFemale
                ? "Secara klinis, kondisimu sangat baik. Tidak ada tanda diabetes, " +
                  "tekanan darahmu normal, dan hormonmu stabil untuk usiamu. " +
                  "Saya jarang bisa bilang ini ke pasien, tapi kamu benar-benar merawat tubuhmu dengan baik. " +
                  "Teruskan apa yang sudah kamu lakukan. Konsistensi kecil setiap hari ternyata berdampak besar."
                : "Secara klinis, kondisimu sangat baik. Tidak ada tanda diabetes atau hipertensi. " +
                  "Jantungmu sehat, berat badanmu terjaga. " +
                  "Ini bukan sesuatu yang bisa dibeli atau didapat dalam semalam. " +
                  "Ini hasil dari pilihan yang kamu buat setiap hari selama bertahun-tahun. " +
                  "Pertahankan, dan tubuhmu akan terus mendukungmu.",

            EndingType.Neutral => isFemale
                ? "Ada beberapa catatan yang perlu kamu bawa pulang. " +
                  "Gula darahmu sedikit di atas normal, belum masuk kategori diabetes, tapi perlu diawasi. " +
                  "Tekanan darahmu juga sedikit tinggi di beberapa pengukuran terakhir. " +
                  "Perubahan hormonal di usiamu membuat pola makan dan istirahat semakin penting, bukan semakin bisa diabaikan. " +
                  "Saya sarankan mulai lebih perhatikan porsi makan dan jadwal tidurmu."
                : "Ada beberapa hal yang perlu kamu perhatikan ke depan. " +
                  "Ada indikasi pra-diabetes ringan dan tekanan darah yang kadang tinggi. " +
                  "Belum sampai ke tahap yang mengharuskan obat, tapi ini sinyal yang tidak boleh diabaikan. " +
                  "Pola makan dan aktivitas fisik yang lebih teratur bisa membalikkan kondisi ini sebelum berkembang lebih jauh. " +
                  "Masih ada waktu untuk memperbaikinya.",

            EndingType.Bad => isFemale
                ? "Saya harus menyampaikan ini dengan jelas. " +
                  "Ada tanda-tanda diabetes tipe 2 dan tekanan darah tinggi yang konsisten dalam data kamu. " +
                  "Perubahan hormonal di usiamu memperburuk kondisi ini lebih cepat dari yang seharusnya. " +
                  "Ini bukan sesuatu yang bisa kita tunda lagi. " +
                  "Saya akan berikan rujukan dan rencana penanganan, tapi perubahan gaya hidup harus dimulai sekarang, bukan besok."
                : "Saya akan bicara langsung karena ini penting. " +
                  "Hasil menunjukkan diabetes tipe 2 yang sudah berkembang dan hipertensi yang perlu segera ditangani. " +
                  "Ini bukan sesuatu yang muncul tiba-tiba. Ini akumulasi dari kebiasaan yang berlangsung bertahun-tahun. " +
                  "Yang perlu kamu pegang adalah ini masih bisa diperbaiki jika kamu mulai sekarang. " +
                  "Saya akan dampingi prosesnya, tapi keputusan ada di tanganmu.",

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
        float  visible   = 0f;
        const float cps  = 38f;

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

        // Ending selesai, langsung ke credit scene
        isShowing = false;

        if (CreditsController.Instance == null)
        {
            GameObject creditsGo = new GameObject("CreditsController");
            creditsGo.AddComponent<CreditsController>();
        }
        // 
        CreditsController.Instance.Play();
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
                ? "Tiga fase hidup telah kamu jalani dengan pilihan yang sadar dan cukup konsisten.\n\n" +
                  "Di tengah tekanan jadwal padat dan ekspektasi yang tidak selalu adil, " +
                  "kamu tetap memilih makan dengan lebih benar, tetap bergerak, dan menjaga istirahat.\n\n" +
                  "Tubuhmu merespons dengan cara terbaik yang ia bisa. Ini bukan keberuntungan.\n\n" +
                  "Ini adalah hasil dari keputusan kecil yang kamu buat setiap hari, " +
                  "bahkan di hari-hari yang terasa berat sekalipun."
                : "Tiga fase hidup telah kamu jalani dan kamu tidak menyia-nyiakannya.\n\n" +
                  "Makanan yang kamu pilih, tidur yang kamu jaga, dan keringat yang kamu keluarkan " +
                  "semuanya terakumulasi menjadi sesuatu yang nyata.\n\n" +
                  "Tubuhmu bukan sesuatu yang harus dipaksa. Ia adalah teman yang kamu rawat pelan-pelan.\n\n" +
                  "Fondasi yang kamu bangun sudah cukup kuat untuk membawamu ke babak berikutnya.",

            EndingType.Neutral => isFemale
                ? "Ada hari-hari yang baik dan ada yang tidak, dan kamu sudah merasakannya sendiri.\n\n" +
                  "Fluktuasi energi, mood yang naik turun, tidur yang kadang tidak cukup.\n\n" +
                  "Dokter tidak menemukan sesuatu yang darurat. Tapi catatan kecil yang ada " +
                  "sebaiknya tidak terus diabaikan.\n\n" +
                  "Perubahan tidak harus besar untuk terasa. Konsistensi kecil setiap hari " +
                  "jauh lebih berharga dari niat besar yang tidak pernah dimulai."
                : "Ada hari-hari yang berjalan baik dan ada yang tidak.\n\n" +
                  "Kamu tidak selalu membuat pilihan terbaik, tapi kamu juga tidak berhenti mencoba.\n\n" +
                  "Tidak ada yang darurat dari hasil pemeriksaan ini. " +
                  "Tapi ada beberapa hal yang kalau dibiarkan, bisa berkembang menjadi masalah lebih besar.\n\n" +
                  "Masih ada ruang untuk memperbaiki ritme hidupmu. Dan waktu terbaik untuk mulai adalah sekarang.",

            EndingType.Bad => isFemale
                ? "Tubuhmu sudah lama mencoba memberi tahu sesuatu, " +
                  "lewat kelelahan yang tidak wajar, mood yang sulit dikendalikan, " +
                  "dan energi yang habis sebelum hari selesai.\n\n" +
                  "Kamu duduk di ruang tunggu sambil menunggu hasil yang sudah lama tertunda.\n\n" +
                  "Dokter bicara dengan hati-hati tapi jelas. Ada beberapa kondisi yang perlu segera ditangani.\n\n" +
                  "Tapi ini bukan tentang menyalahkan diri sendiri. " +
                  "Ini tentang memilih untuk mulai dari titik ini, dengan cara yang berbeda."
                : "Tubuhmu sudah lama mencoba memberi sinyal, " +
                  "lewat rasa lelah yang tidak kunjung hilang, tidur yang tidak pernah terasa cukup, " +
                  "dan energi yang menghilang bahkan sebelum siang.\n\n" +
                  "Hari ini kamu akhirnya duduk di sana, mendengarkan hasil yang sudah lama tertunda.\n\n" +
                  "Dokter bicara pelan tapi tegas. Kondisi ini nyata dan perlu ditangani sekarang.\n\n" +
                  "Tapi setiap orang punya titik baliknya masing-masing. " +
                  "Dan mungkin, ini adalah milikmu.",

            _ => "Perjalananmu telah selesai."
        };
    }

    // ── Panel builder (tidak berubah dari versi asli) ────────────────

    private void EnsurePanel()
    {
        if (panelRoot != null) return;

        Canvas[] canvases = FindObjectsByType<Canvas>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        Transform parent = null;
        foreach (var c in canvases)
            if (c.gameObject.name == "HUD_Canvas") { parent = c.transform; break; }

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

    private class EndingDoctorActor : IDialogueActor
    {
        public System.Collections.Generic.List<DialogueChoiceData> GetAvailableChoices(DialogueNodeData node)
        {
            return node?.choices ?? new System.Collections.Generic.List<DialogueChoiceData>();
        }

        public void ApplyConsequence(DialogueConsequence consequence) { }
    }
}