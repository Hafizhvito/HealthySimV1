using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Credit scene scroll dari bawah ke atas, seperti film/game.
/// Dipanggil dari EndingManager via CreditsController.Instance.Play().
/// Tekan space / tap layar untuk skip.
/// </summary>
public class CreditsController : MonoBehaviour
{
    public static CreditsController Instance { get; private set; }

    [Header("Scroll Settings")]
    [SerializeField] private float scrollSpeed        = 60f;   // pixel per detik
    [SerializeField] private float fadeInDuration     = 1.0f;
    [SerializeField] private float endHoldDuration    = 2.5f;  // hold setelah teks habis
    [SerializeField] private float bgmFadeOutDuration = 5.0f;

    [Header("Optional BGM Source")]
    [SerializeField] private AudioSource bgmSource;

    [SerializeField] private float minCreditsDuration = 8f;

    private const int CreditsCanvasSortingOrder = 1100;

    [SerializeField] private string mainMenuSceneName = "MainMenu";

    // ── Runtime UI ───────────────────────────────────────────────────
    private Canvas          creditsCanvas;
    private CanvasGroup     creditsGroup;
    private RectTransform   scrollRect;   // container yang digerakkan
    private bool            isRunning;
    private bool            skipRequested;
    private float           creditsStartedAt;

    // ── Konten credits ───────────────────────────────────────────────
    // Setiap entry: (teks, fontSize, style, spaceAfter)
    private static readonly (string text, float size, FontStyles style, float spacer)[] Lines =
    {
        // Pesan pembuka
        ( "Kesehatan bukan tujuan akhir.",              36f, FontStyles.Bold,   0f  ),
        ( "Ia adalah cara kamu menjalani setiap harinya.", 24f, FontStyles.Normal, 80f ),

        // Validator
        ( "Validator Medis",                            22f, FontStyles.Normal, 12f ),
        ( "Dr. dr. Sri Wuryanti, MS, Sp.GK",           30f, FontStyles.Bold,   6f  ),
        ( "Spesialis Gizi Klinik",                      22f, FontStyles.Normal, 4f  ),
        ( "RS YARSI Jakarta",                           22f, FontStyles.Normal, 80f ),

        // Dosen Pembimbing
        ( "Dosen Pembimbing",                           22f, FontStyles.Normal, 12f ),
        ( "Paramaresthi Windriyani, S.Kom., M.Eng.",     28f, FontStyles.Bold,   80f ),

        // Tim
        ( "Dikembangkan oleh",                          22f, FontStyles.Normal, 12f ),
        ( "HealthVerse",                                40f, FontStyles.Bold,   20f ),
        ( "Hafizh Vito Pratomo",                        26f, FontStyles.Normal, 6f  ),
        ( "Alvin Dimas Lunardi",                        26f, FontStyles.Normal, 6f  ),
        ( "Aditiya Budi Listianto",                     26f, FontStyles.Normal, 80f ),

        // Institusi
        ( "Universitas YARSI",                          26f, FontStyles.Bold,   6f  ),
        ( "Teknik Informatika  2025",                   22f, FontStyles.Normal, 80f ),

        // Penutup
        ( "Terima kasih sudah bermain.",                34f, FontStyles.Bold,   0f  ),
    };

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public static CreditsController EnsureInstance()
    {
        if (Instance != null)
            return Instance;

        CreditsController existing = FindFirstObjectByType<CreditsController>(FindObjectsInactive.Include);
        if (existing != null)
        {
            Instance = existing;
            DontDestroyOnLoad(existing.gameObject);
            return existing;
        }

        GameObject go = new GameObject("CreditsController");
        return go.AddComponent<CreditsController>();
    }

    void Update()
    {
        // Skip: space, enter, atau tap layar
        if (!isRunning) return;
        if (Input.GetKeyDown(KeyCode.Space)  ||
            Input.GetKeyDown(KeyCode.Return) ||
            Input.GetMouseButtonDown(0)      ||
            (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began))
        {
            skipRequested = true;
        }
    }

    // ── Entry point ──────────────────────────────────────────────────
    public void Play(AudioSource endingBgm = null)
    {
        if (isRunning) return;
        bgmSource     = endingBgm;
        skipRequested = false;
        StartCoroutine(RunCredits());
    }

    // ── Main coroutine ───────────────────────────────────────────────
    private IEnumerator RunCredits()
    {
        isRunning = true;
        creditsStartedAt = Time.unscaledTime;
        Debug.Log("[CreditsController] Memulai credit scene.");

        if (FadeManager.Instance != null)
            FadeManager.Instance.ReleaseInputBlock();

        EnsureUI();

        if (creditsCanvas == null || scrollRect == null)
        {
            Debug.LogError("[CreditsController] UI gagal dibuat — credit scene dibatalkan.");
            isRunning = false;
            yield break;
        }

        creditsCanvas.gameObject.SetActive(true);
        creditsGroup.alpha = 0f;

        yield return StartCoroutine(FadeCanvas(0f, 1f, fadeInDuration));

        if (bgmSource != null && bgmSource.isPlaying)
            StartCoroutine(FadeOutBGM(bgmSource, bgmFadeOutDuration));

        yield return new WaitForEndOfFrame();
        yield return new WaitForEndOfFrame();
        LayoutRebuilder.ForceRebuildLayoutImmediate(scrollRect);
        yield return new WaitForEndOfFrame();

        float viewportHeight = creditsCanvas.pixelRect.height > 1f
            ? creditsCanvas.pixelRect.height
            : Screen.height;
        float contentHeight = Mathf.Max(
            LayoutUtility.GetPreferredHeight(scrollRect),
            scrollRect.rect.height,
            900f);

        scrollRect.anchoredPosition = new Vector2(0f, -viewportHeight * 0.5f);
        float targetY = viewportHeight * 0.5f + contentHeight;

        while (!skipRequested)
        {
            float currentY = scrollRect.anchoredPosition.y;
            if (currentY < targetY)
            {
                float newY = currentY + scrollSpeed * Time.unscaledDeltaTime;
                scrollRect.anchoredPosition = new Vector2(0f, Mathf.Min(newY, targetY));
            }

            float elapsed = Time.unscaledTime - creditsStartedAt;
            if (currentY >= targetY && elapsed >= minCreditsDuration)
                break;

            yield return null;
        }

        if (!skipRequested)
            yield return new WaitForSecondsRealtime(endHoldDuration);

        yield return StartCoroutine(FadeCanvas(1f, 0f, fadeInDuration));

        creditsCanvas.gameObject.SetActive(false);
        Time.timeScale = 1f;
        isRunning = false;

        if (!string.IsNullOrWhiteSpace(mainMenuSceneName))
            SceneLoader.LoadScene(mainMenuSceneName);

        Debug.Log("[CreditsController] Credit scene selesai.");
    }

    // ── Canvas fade ──────────────────────────────────────────────────
    private IEnumerator FadeCanvas(float from, float to, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed            += Time.unscaledDeltaTime;
            creditsGroup.alpha  = Mathf.Lerp(from, to, elapsed / duration);
            yield return null;
        }
        creditsGroup.alpha = to;
    }

    // ── BGM fade out ─────────────────────────────────────────────────
    private IEnumerator FadeOutBGM(AudioSource source, float duration)
    {
        float startVolume = source.volume;
        float elapsed     = 0f;
        while (elapsed < duration && source != null)
        {
            elapsed       += Time.unscaledDeltaTime;
            source.volume  = Mathf.Lerp(startVolume, 0f, elapsed / duration);
            yield return null;
        }
        if (source != null) { source.volume = 0f; source.Stop(); }
    }

    // ── UI builder ───────────────────────────────────────────────────
    private void EnsureUI()
    {
        if (creditsCanvas != null)
        {
            creditsCanvas.sortingOrder = CreditsCanvasSortingOrder;
            RebuildScrollContent();
            return;
        }

        // Canvas
        GameObject canvasGo = new GameObject("CreditsCanvas",
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));

        creditsCanvas              = canvasGo.GetComponent<Canvas>();
        creditsCanvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        creditsCanvas.sortingOrder = CreditsCanvasSortingOrder;

        CanvasScaler scaler        = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.matchWidthOrHeight  = 0.5f;

        canvasGo.GetComponent<GraphicRaycaster>().enabled = false;

        creditsGroup                = canvasGo.GetComponent<CanvasGroup>();
        creditsGroup.alpha          = 0f;
        creditsGroup.blocksRaycasts = false;
        creditsGroup.interactable   = false;

        // Background hitam
        GameObject bg   = new GameObject("Background", typeof(RectTransform), typeof(Image));
        bg.transform.SetParent(canvasGo.transform, false);
        RectTransform bgR = bg.GetComponent<RectTransform>();
        bgR.anchorMin = Vector2.zero;
        bgR.anchorMax = Vector2.one;
        bgR.offsetMin = Vector2.zero;
        bgR.offsetMax = Vector2.zero;
        bg.GetComponent<Image>().color = Color.black;

        // Scroll container — lebar penuh, tinggi dihitung dari konten
        GameObject scrollGo = new GameObject("ScrollContainer", typeof(RectTransform));
        scrollGo.transform.SetParent(canvasGo.transform, false);
        scrollRect            = scrollGo.GetComponent<RectTransform>();
        scrollRect.anchorMin  = new Vector2(0.5f, 0f);
        scrollRect.anchorMax  = new Vector2(0.5f, 0f);
        scrollRect.pivot      = new Vector2(0.5f, 0f);
        scrollRect.sizeDelta  = new Vector2(900f, 0f); // tinggi diisi BuildContent

        RebuildScrollContent();

        creditsCanvas.gameObject.SetActive(false);
        DontDestroyOnLoad(canvasGo);
    }

    private void RebuildScrollContent()
    {
        for (int i = scrollRect.childCount - 1; i >= 0; i--)
            DestroyImmediate(scrollRect.GetChild(i).gameObject);

        // Vertical layout supaya tinggi otomatis
        ContentSizeFitter csf = scrollRect.gameObject.GetComponent<ContentSizeFitter>();
        if (csf == null) csf  = scrollRect.gameObject.AddComponent<ContentSizeFitter>();
        csf.verticalFit       = ContentSizeFitter.FitMode.PreferredSize;

        VerticalLayoutGroup vlg = scrollRect.gameObject.GetComponent<VerticalLayoutGroup>();
        if (vlg == null) vlg    = scrollRect.gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.childAlignment      = TextAnchor.UpperCenter;
        vlg.spacing             = 0f;
        vlg.padding             = new RectOffset(0, 0, 40, 120);
        vlg.childControlWidth   = true;
        vlg.childControlHeight  = true;
        vlg.childForceExpandWidth  = true;
        vlg.childForceExpandHeight = false;

        // Bangun baris per baris
        foreach (var line in Lines)
        {
            // Teks
            GameObject textGo = new GameObject("Line", typeof(RectTransform), typeof(TextMeshProUGUI));
            textGo.transform.SetParent(scrollRect, false);

            TextMeshProUGUI tmp  = textGo.GetComponent<TextMeshProUGUI>();
            tmp.text             = line.text;
            tmp.fontSize         = line.size;
            tmp.fontStyle        = line.style;
            tmp.alignment        = TextAlignmentOptions.Center;
            tmp.textWrappingMode = TextWrappingModes.Normal;
            tmp.color            = GetLineColor(line.style, line.size);
            tmp.font             = ResolveCreditsFont();
            tmp.ForceMeshUpdate();

            LayoutElement le     = textGo.AddComponent<LayoutElement>();
            le.preferredHeight   = line.size * 1.4f;

            // Spacer setelah baris (kalau ada)
            if (line.spacer > 0f)
            {
                GameObject spacerGo = new GameObject("Spacer", typeof(RectTransform));
                spacerGo.transform.SetParent(scrollRect, false);
                LayoutElement spacerLe  = spacerGo.AddComponent<LayoutElement>();
                spacerLe.preferredHeight = line.spacer;
                spacerLe.minHeight       = line.spacer;
            }
        }
    }

    private static TMP_FontAsset ResolveCreditsFont()
    {
        if (TMP_Settings.defaultFontAsset != null)
            return TMP_Settings.defaultFontAsset;

        TMP_FontAsset liberation = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
        if (liberation != null)
            return liberation;

        return Resources.Load<TMP_FontAsset>("LiberationSans SDF");
    }

    private static Color GetLineColor(FontStyles style, float size)
    {
        // Bold besar = putih terang (judul/nama)
        if (style == FontStyles.Bold && size >= 30f)
            return new Color(0.97f, 0.95f, 0.90f, 1f);

        // Bold kecil = putih agak redup
        if (style == FontStyles.Bold)
            return new Color(0.88f, 0.88f, 0.85f, 1f);

        // Normal = abu-abu kebiruan (label/institusi)
        return new Color(0.70f, 0.78f, 0.88f, 1f);
    }
}