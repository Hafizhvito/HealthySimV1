using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class StoryIntroManager : MonoBehaviour
{
    public static StoryIntroManager Instance { get; private set; }

    private const string INTRO_PLAYED_KEY = "StoryIntroPlayed";

    public event System.Action OnIntroFlowCompleted;

    [Header("Optional Intro Follow-up")]
    [SerializeField] private BackstoryDialogueController backstoryDialogue;

    [Header("Intro Debug")]
    [SerializeField] private bool forcePlayIntro = false;

    private StoryTemplate[] templates;

    public StoryTemplate ActiveTemplate { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        BuildTemplates();
    }

    public IEnumerator StartIntroFlow()
    {
        bool alreadyPlayed = PlayerPrefs.GetInt(INTRO_PLAYED_KEY, 0) == 1;

        if (alreadyPlayed && !forcePlayIntro)
        {
            Debug.Log("[StoryIntro] Intro sudah pernah dimainkan — skip.");
            OnIntroFlowCompleted?.Invoke();
            yield break;
        }

        if (templates == null || templates.Length == 0)
        {
            OnIntroFlowCompleted?.Invoke();
            yield break;
        }

        PlayerPrefs.SetInt(INTRO_PLAYED_KEY, 1);
        PlayerPrefs.Save();

        int index = SessionSeedManager.Instance != null
            ? SessionSeedManager.Instance.NextInt(0, templates.Length)
            : Random.Range(0, templates.Length);

        ActiveTemplate = templates[index];

        Debug.Log($"[StoryIntro] Template terpilih: {ActiveTemplate.title}");

        var cutscene = FindFirstObjectByType<IntroCutsceneController>();
        if (cutscene != null)
            yield return cutscene.PlayIntro(ActiveTemplate);

        if (backstoryDialogue == null)
            backstoryDialogue = ResolveBackstoryDialogueController();

        if (backstoryDialogue != null)
        {
            backstoryDialogue.Show(ActiveTemplate);
            Debug.Log("[StoryIntro] Backstory dialogue dipanggil setelah intro selesai.");
        }
        else
        {
            Debug.LogWarning("[StoryIntro] BackstoryDialogueController tidak ditemukan di scene.");
        }

        OnIntroFlowCompleted?.Invoke();
    }

    // ── reset untuk testing ───────────────────────────────────
    [ContextMenu("Reset Intro Flag")]
    public void ResetIntroFlag()
    {
        PlayerPrefs.DeleteKey(INTRO_PLAYED_KEY);
        Debug.Log("[StoryIntro] Intro flag di-reset — akan muncul lagi saat Play.");
    }

    private BackstoryDialogueController ResolveBackstoryDialogueController()
    {
        if (backstoryDialogue != null)
            return backstoryDialogue;

        backstoryDialogue = FindFirstObjectByType<BackstoryDialogueController>();
        if (backstoryDialogue != null)
            return backstoryDialogue;

        backstoryDialogue = FindFirstObjectByType<BackstoryDialogueController>(FindObjectsInactive.Include);
        if (backstoryDialogue != null)
            return backstoryDialogue;

        backstoryDialogue = CreateRuntimeBackstoryController();
        return backstoryDialogue;
    }

    private BackstoryDialogueController CreateRuntimeBackstoryController()
    {
        Canvas canvas = FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);
        if (canvas == null)
        {
            GameObject canvasGo = new GameObject("BackstoryCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
        }

        GameObject panelGo = new GameObject("BackstoryPanel", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
        panelGo.transform.SetParent(canvas.transform, false);

        RectTransform rect = panelGo.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Image bg = panelGo.GetComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.72f);

        BackstoryDialogueController controller = panelGo.AddComponent<BackstoryDialogueController>();
        Debug.Log("[StoryIntro] BackstoryDialogueController runtime fallback dibuat di Canvas.");
        return controller;
    }

    private void BuildTemplates()
    {
        templates = new[]
        {
            new StoryTemplate
            {
                title = "Pagi Sibuk di Kota",
                recommendedAction = "Tubuhmu ingat semua yang kamu abaikan.",
                lines = new[]
                {
                    "Jakarta. Pagi yang belum sepenuhnya bangun.",
                    "Kamu bergegas, tapi tubuhmu sudah tertinggal sejak semalam.",
                    "Hari ini bukan soal sempurna. Tapi soal memilih lebih baik dari kemarin."
                }
            },
            new StoryTemplate
            {
                title = "Target Gaya Hidup Sehat",
                recommendedAction = "Kamu sudah tahu apa yang harus dilakukan. Pertanyaannya.. mulai kapan?",
                lines = new[]
                {
                    "Ada hari-hari ketika kamu memutuskan untuk berubah.",
                    "Bukan besok. Bukan minggu depan. Hari ini.",
                    "Tubuhmu sudah lama menunggu keputusan itu."
                }
            },
            new StoryTemplate
            {
                title = "Hari Ujian Kebiasaan",
                recommendedAction = "Kebiasaan buruk tidak terasa salah — sampai tubuhmu yang berbicara.",
                lines = new[]
                {
                    "Tidak ada yang tiba-tiba sakit. Tidak ada yang tiba-tiba sehat.",
                    "Semuanya dibangun diam-diam dari pilihan yang kamu anggap kecil.",
                    "Hari ini, pilihan itu ada di tanganmu."
                }
            },
            new StoryTemplate
            {
                title = "Harga Sebuah Pilihan",
                recommendedAction = "Kesehatan bukan hadiah. Itu hasil dari keputusan yang kamu buat setiap hari.",
                lines = new[]
                {
                    "Kamu tidak bisa membeli waktu yang sudah terbuang.",
                    "Tapi kamu masih bisa memilih apa yang kamu lakukan dengan waktu yang tersisa.",
                    "Mulai dari sini. Mulai dari sekarang."
                }
            }
        };
    }
}
