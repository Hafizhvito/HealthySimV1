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

        float goodThreshold    = gender == PlayerStats.Gender.Female ? 60f : 65f;
        float neutralThreshold = gender == PlayerStats.Gender.Female ? 35f : 40f;

        pendingEndingType = avg >= goodThreshold  ? EndingType.Good
                          : avg >= neutralThreshold ? EndingType.Neutral
                          : EndingType.Bad;
        pendingGender = gender;

        Debug.Log($"[EndingManager] avg={avg:F1} gender={gender} → {pendingEndingType}");

        if (NpcDialogueMenuController.Instance == null)
        {
            Debug.LogWarning("[EndingManager] NpcDialogueMenuController tidak ditemukan, skip doctor dialogue.");
            StartCoroutine(ShowEndingRoutine(pendingEndingType, pendingGender));
            return;
        }

        isShowing = true;
        DialogueGraphData doctorDialogue = BuildDoctorDialogue(pendingEndingType, pendingGender, avg);
        NpcDialogueMenuController.Instance.OnDialogueClosed += HandleDoctorDialogueClosed;
        NpcDialogueMenuController.Instance.OpenDialogue(new EndingDoctorActor(), doctorDialogue);
    }

    private void HandleDoctorDialogueClosed()
    {
        if (NpcDialogueMenuController.Instance != null)
            NpcDialogueMenuController.Instance.OnDialogueClosed -= HandleDoctorDialogueClosed;
        StartCoroutine(ShowEndingRoutine(pendingEndingType, pendingGender));
    }

    private DialogueGraphData BuildDoctorDialogue(EndingType type, PlayerStats.Gender gender, float avg)
    {
        float[] scores = PlayerStats.Instance.CommittedPhaseScores;
        float youthScore  = scores.Length > 0 ? scores[0] : 50f;
        float adultScore  = scores.Length > 1 ? scores[1] : 50f;
        float seniorScore = scores.Length > 2 ? scores[2] : 50f;

        DialogueGraphData graph = ScriptableObject.CreateInstance<DialogueGraphData>();
        graph.npcId = "dr_ending";
        graph.npcDisplayName = "Dr. Hana";
        graph.startNodeId = "start";

        // Node 1 — pembuka
        DialogueNodeData node1 = new DialogueNodeData();
        node1.nodeId = "start";
        node1.fallbackLine = BuildDoctorOpening(type, youthScore, adultScore, seniorScore);
        DialogueChoiceData choice1 = new DialogueChoiceData();
        choice1.choiceText = "Lanjut >";
        choice1.nextNodeId = "phase_review";
        node1.choices = new System.Collections.Generic.List<DialogueChoiceData> { choice1 };

        // Node 2 — review per fase
        DialogueNodeData node2 = new DialogueNodeData();
        node2.nodeId = "phase_review";
        node2.fallbackLine = BuildPhaseReview(youthScore, adultScore, seniorScore);
        DialogueChoiceData choice2 = new DialogueChoiceData();
        choice2.choiceText = "Lanjut >";
        choice2.nextNodeId = "conclusion";
        node2.choices = new System.Collections.Generic.List<DialogueChoiceData> { choice2 };

        // Node 3 — kesimpulan
        DialogueNodeData node3 = new DialogueNodeData();
        node3.nodeId = "conclusion";
        node3.fallbackLine = BuildDoctorConclusion(type, gender);
        node3.isConversationEnd = true;
        DialogueChoiceData choice3 = new DialogueChoiceData();
        choice3.choiceText = "Terima kasih, Dok.";
        choice3.nextNodeId = string.Empty;
        node3.choices = new System.Collections.Generic.List<DialogueChoiceData> { choice3 };

        graph.nodes = new System.Collections.Generic.List<DialogueNodeData> { node1, node2, node3 };
        return graph;
    }

    private static string BuildDoctorOpening(EndingType type, float youth, float adult, float senior)
    {
        return type switch
        {
            EndingType.Good =>
                "Selamat datang kembali. Saya sudah melihat hasil pemeriksaan lengkap kamu. " +
                "Secara keseluruhan, kondisimu sangat baik untuk usiamu sekarang.",
            EndingType.Neutral =>
                "Silakan duduk. Saya sudah periksa semua datamu dari waktu ke waktu. " +
                "Ada beberapa hal yang ingin saya sampaikan dengan jujur.",
            EndingType.Bad =>
                "Terima kasih sudah datang. Saya perlu bicara serius tentang kondisimu. " +
                "Hasil pemeriksaan menunjukkan beberapa hal yang perlu segera diperhatikan.",
            _ => "Silakan duduk, kita bicara tentang kondisimu."
        };
    }

    private static string BuildPhaseReview(float youth, float adult, float senior)
    {
        string youthLabel  = youth  >= 65f ? "baik" : youth  >= 40f ? "cukup" : "kurang";
        string adultLabel  = adult  >= 65f ? "baik" : adult  >= 40f ? "cukup" : "kurang";
        string seniorLabel = senior >= 65f ? "baik" : senior >= 40f ? "cukup" : "kurang";

        string youthRisk  = youth  < 40f ? " Pola ini mulai membentuk risiko metabolik." : "";
        string adultRisk  = adult  < 40f ? " Di fase ini tekanan pada jantung mulai terasa." : "";
        string seniorRisk = senior < 40f ? " Di fase lansia, dampaknya sudah terasa nyata." : "";

        return $"Di masa muda, kesehatanmu tergolong {youthLabel}.{youthRisk} " +
               $"Memasuki usia dewasa, kondisimu {adultLabel}.{adultRisk} " +
               $"Dan di fase lansia ini, rekam jejakmu {seniorLabel}.{seniorRisk}";
    }

    private static string BuildDoctorConclusion(EndingType type, PlayerStats.Gender gender)
    {
        bool isFemale = gender == PlayerStats.Gender.Female;
        return type switch
        {
            EndingType.Good => isFemale
                ? "Tidak ada tanda diabetes, tekanan darah normal, dan hormonmu stabil untuk usiamu. " +
                  "Teruskan kebiasaan ini. Kamu adalah contoh bahwa konsistensi kecil berdampak besar."
                : "Tidak ada tanda diabetes atau hipertensi. Jantungmu sehat. " +
                  "Teruskan kebiasaan ini. Pilihan hidupmu selama ini terbayar.",
            EndingType.Neutral => isFemale
                ? "Gula darahmu sedikit di atas normal — belum diabetes, tapi perlu diawasi. " +
                  "Perubahan hormonal di usiamu membuat pola makan semakin penting."
                : "Ada indikasi pra-diabetes ringan dan tekanan darah sedikit tinggi. " +
                  "Belum parah, tapi ini sinyal yang tidak boleh diabaikan.",
            EndingType.Bad => isFemale
                ? "Saya harus jujur — ada tanda diabetes tipe 2 dan tekanan darah tinggi yang konsisten. " +
                  "Perubahan hormonal memperburuk kondisi ini. Perubahan gaya hidup harus dimulai sekarang."
                : "Hasil menunjukkan diabetes tipe 2 dan hipertensi yang sudah berkembang. " +
                  "Ini akibat akumulasi kebiasaan bertahun-tahun. Tapi masih bisa diperbaiki mulai hari ini.",
            _ => "Jaga kesehatanmu ke depan."
        };
    }

    private IEnumerator ShowEndingRoutine(EndingType type, PlayerStats.Gender gender)
    {
        EnsurePanel();
        if (panelRoot == null) yield break;

        if (ModalStateManager.Instance != null)
            ModalStateManager.Instance.OpenModal(ModalKey);

        panelRoot.gameObject.SetActive(true);
        panelGroup.alpha = 1f;
        panelGroup.blocksRaycasts = true;
        panelGroup.interactable = true;
        panelRoot.SetAsLastSibling();

        titleText.text = GetTitle(type);
        bodyText.text = string.Empty;
        closeButton.interactable = false;

        string narrative = GetNarrative(type, gender);
        float visible = 0f;
        const float cps = 38f;
        while (visible < narrative.Length)
        {
            visible += cps * Time.unscaledDeltaTime;
            int shown = Mathf.Clamp(Mathf.FloorToInt(visible), 0, narrative.Length);
            bodyText.text = narrative.Substring(0, shown);
            yield return null;
        }
        bodyText.text = narrative;
        closeButton.interactable = true;

        bool closed = false;
        closeButton.onClick.RemoveAllListeners();
        closeButton.onClick.AddListener(() => closed = true);
        while (!closed) yield return null;

        panelGroup.alpha = 0f;
        panelGroup.blocksRaycasts = false;
        panelGroup.interactable = false;
        panelRoot.gameObject.SetActive(false);

        if (ModalStateManager.Instance != null)
            ModalStateManager.Instance.CloseModal(ModalKey);

        isShowing = false;
        Time.timeScale = 0f;
        Debug.Log("[EndingManager] Game ended. timeScale=0");
    }

    private static string GetTitle(EndingType type) => type switch
    {
        EndingType.Good    => "Hidup Sehat, Hidup Bahagia",
        EndingType.Neutral => "Perjalanan yang Belum Selesai",
        EndingType.Bad     => "Tubuh Berbicara",
        _                  => "Akhir Perjalanan"
    };

    private static string GetNarrative(EndingType type, PlayerStats.Gender gender)
    {
        bool isFemale = gender == PlayerStats.Gender.Female;
        return type switch
        {
            EndingType.Good => isFemale
                ? "Tiga fase hidup telah kamu jalani dengan pilihan yang sadar dan konsisten.\n\n" +
                  "Di tengah tekanan hormon, jadwal padat, dan ekspektasi yang tidak selalu adil — " +
                  "kamu tetap memilih makan dengan benar, bergerak, dan beristirahat cukup.\n\n" +
                  "Tubuhmu merespons dengan cara terbaik yang ia bisa. Ini bukan keberuntungan.\n\n" +
                  "Ini hasil dari keputusan kecil yang kamu buat setiap hari."
                : "Tiga fase hidup telah kamu jalani dengan pilihan yang konsisten.\n\n" +
                  "Makanan yang kamu pilih, tidur yang kamu jaga, dan keringat di gym — " +
                  "semuanya terakumulasi menjadi tubuh yang kuat dan pikiran yang jernih.\n\n" +
                  "Tubuhmu bukan proyek — ia adalah teman yang kamu rawat dengan sabar.\n\n" +
                  "Fondasi yang kamu bangun sudah cukup kuat untuk babak berikutnya.",

            EndingType.Neutral => isFemale
                ? "Ada hari-hari yang baik, ada yang tidak — dan kamu merasakannya di tubuhmu.\n\n" +
                  "Fluktuasi energi, mood yang naik turun, tidur yang kadang tidak cukup.\n\n" +
                  "Dokter tidak menemukan sesuatu yang serius. Tapi ada catatan kecil " +
                  "yang perlu diperhatikan ke depan.\n\n" +
                  "Konsistensi kecil lebih berharga dari perubahan besar yang tidak bertahan."
                : "Ada hari-hari yang baik, ada yang tidak.\n\n" +
                  "Kamu tidak selalu membuat pilihan terbaik — tapi kamu juga tidak menyerah.\n\n" +
                  "Dokter bilang tidak ada yang darurat. Tapi ada beberapa hal " +
                  "yang perlu lebih diperhatikan.\n\n" +
                  "Konsistensi kecil lebih berharga dari perubahan besar yang tidak bertahan.",

            EndingType.Bad => isFemale
                ? "Tubuhmu sudah lama mencoba berbicara — lewat kelelahan yang tidak wajar, " +
                  "mood yang sulit dikendalikan, dan energi yang habis sebelum hari selesai.\n\n" +
                  "Kamu duduk di ruang tunggu rumah sakit, menunggu hasil pemeriksaan.\n\n" +
                  "Dokter berbicara dengan hati-hati: ada beberapa kondisi yang perlu " +
                  "segera ditangani.\n\n" +
                  "Tapi hari ini bukan tentang penyesalan. Hari ini adalah titik mulai yang baru."
                : "Tubuhmu sudah lama mencoba berbicara — melalui lelah yang tidak wajar, " +
                  "tidur yang tidak pernah cukup, dan energi yang hilang sebelum siang.\n\n" +
                  "Hari ini kamu duduk di ruang tunggu rumah sakit.\n\n" +
                  "Dokter berbicara pelan tapi jelas: ini bukan akhir, " +
                  "tapi ini adalah sinyal yang tidak boleh diabaikan lagi.\n\n" +
                  "Perubahan selalu bisa dimulai — bahkan dari titik ini.",

            _ => "Perjalananmu telah selesai."
        };
    }

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
            cv.renderMode = RenderMode.ScreenSpaceOverlay;
            cv.sortingOrder = 300;
            CanvasScaler cs = cGo.GetComponent<CanvasScaler>();
            cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            cs.referenceResolution = new Vector2(1920f, 1080f);
            parent = cGo.transform;
        }

        GameObject root = new GameObject("EndingPanel",
            typeof(RectTransform), typeof(CanvasGroup));
        root.transform.SetParent(parent, false);
        panelRoot = root.GetComponent<RectTransform>();
        panelRoot.anchorMin = Vector2.zero;
        panelRoot.anchorMax = Vector2.one;
        panelRoot.offsetMin = Vector2.zero;
        panelRoot.offsetMax = Vector2.zero;
        panelGroup = root.GetComponent<CanvasGroup>();
        panelGroup.alpha = 0f;
        panelGroup.blocksRaycasts = false;
        panelGroup.interactable = false;

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
        RectTransform cardR = card.GetComponent<RectTransform>();
        cardR.anchorMin = new Vector2(0.5f, 0.5f);
        cardR.anchorMax = new Vector2(0.5f, 0.5f);
        cardR.pivot = new Vector2(0.5f, 0.5f);
        cardR.anchoredPosition = Vector2.zero;
        cardR.sizeDelta = new Vector2(820f, 480f);
        card.GetComponent<Image>().color = Color.white;

        // Accent bar
        GameObject acc = new GameObject("AccentBar", typeof(RectTransform), typeof(Image));
        acc.transform.SetParent(card.transform, false);
        RectTransform acR = acc.GetComponent<RectTransform>();
        acR.anchorMin = new Vector2(0f,0f); acR.anchorMax = new Vector2(0f,1f);
        acR.pivot = new Vector2(0f,0.5f);
        acR.offsetMin = Vector2.zero; acR.offsetMax = new Vector2(6f,0f);
        acR.sizeDelta = new Vector2(6f,0f);
        acc.GetComponent<Image>().color = new Color32(0x21,0x96,0xF3,0xFF);

        // Title
        GameObject tGo = new GameObject("TitleText", typeof(RectTransform), typeof(TextMeshProUGUI));
        tGo.transform.SetParent(card.transform, false);
        RectTransform tR = tGo.GetComponent<RectTransform>();
        tR.anchorMin = new Vector2(0.5f,1f); tR.anchorMax = new Vector2(0.5f,1f);
        tR.pivot = new Vector2(0.5f,1f);
        tR.anchoredPosition = new Vector2(0f,-40f);
        tR.sizeDelta = new Vector2(740f,60f);
        titleText = tGo.GetComponent<TextMeshProUGUI>();
        titleText.fontSize = 36f;
        titleText.fontStyle = FontStyles.Bold;
        titleText.color = new Color32(0x1A,0x1A,0x2E,0xFF);
        titleText.alignment = TextAlignmentOptions.Center;
        titleText.textWrappingMode = TextWrappingModes.Normal;

        // Divider
        GameObject dv = new GameObject("Divider", typeof(RectTransform), typeof(Image));
        dv.transform.SetParent(card.transform, false);
        RectTransform dvR = dv.GetComponent<RectTransform>();
        dvR.anchorMin = new Vector2(0.5f,1f); dvR.anchorMax = new Vector2(0.5f,1f);
        dvR.pivot = new Vector2(0.5f,1f);
        dvR.anchoredPosition = new Vector2(0f,-108f);
        dvR.sizeDelta = new Vector2(700f,2f);
        dv.GetComponent<Image>().color = new Color32(0xE0,0xE0,0xE0,0xFF);

        // Body
        GameObject bGo = new GameObject("BodyText", typeof(RectTransform), typeof(TextMeshProUGUI));
        bGo.transform.SetParent(card.transform, false);
        RectTransform bR = bGo.GetComponent<RectTransform>();
        bR.anchorMin = new Vector2(0.5f,1f); bR.anchorMax = new Vector2(0.5f,1f);
        bR.pivot = new Vector2(0.5f,1f);
        bR.anchoredPosition = new Vector2(0f,-126f);
        bR.sizeDelta = new Vector2(740f,280f);
        bodyText = bGo.GetComponent<TextMeshProUGUI>();
        bodyText.fontSize = 21f;
        bodyText.color = new Color32(0x33,0x33,0x33,0xFF);
        bodyText.alignment = TextAlignmentOptions.Center;
        bodyText.textWrappingMode = TextWrappingModes.Normal;
        bodyText.lineSpacing = 5f;

        // Button
        GameObject btnGo = new GameObject("CloseButton",
            typeof(RectTransform), typeof(Image), typeof(Button));
        btnGo.transform.SetParent(card.transform, false);
        RectTransform btnR = btnGo.GetComponent<RectTransform>();
        btnR.anchorMin = new Vector2(1f,0f); btnR.anchorMax = new Vector2(1f,0f);
        btnR.pivot = new Vector2(1f,0f);
        btnR.anchoredPosition = new Vector2(-32f,32f);
        btnR.sizeDelta = new Vector2(200f,52f);
        btnGo.GetComponent<Image>().color = new Color32(0x21,0x96,0xF3,0xFF);
        closeButton = btnGo.GetComponent<Button>();
        closeButton.targetGraphic = btnGo.GetComponent<Image>();

        GameObject btnTGo = new GameObject("ButtonText",
            typeof(RectTransform), typeof(TextMeshProUGUI));
        btnTGo.transform.SetParent(btnGo.transform, false);
        RectTransform btnTR = btnTGo.GetComponent<RectTransform>();
        btnTR.anchorMin = Vector2.zero; btnTR.anchorMax = Vector2.one;
        btnTR.offsetMin = Vector2.zero; btnTR.offsetMax = Vector2.zero;
        TextMeshProUGUI btnTmp = btnTGo.GetComponent<TextMeshProUGUI>();
        btnTmp.text = "Selesai";
        btnTmp.fontSize = 22f;
        btnTmp.fontStyle = FontStyles.Bold;
        btnTmp.color = Color.white;
        btnTmp.alignment = TextAlignmentOptions.Center;

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