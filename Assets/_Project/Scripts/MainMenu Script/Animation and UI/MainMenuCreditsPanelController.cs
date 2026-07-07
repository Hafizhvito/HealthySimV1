using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class MainMenuCreditsPanelController : MonoBehaviour
{
    private const string BodyObjectName = "CreditsBodyText";

    [Header("Optional References")]
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI bodyText;
    [SerializeField] private ScrollRect scrollRect;
    [SerializeField] private RectTransform contentRoot;
    [SerializeField] private RectTransform panelRect;

    [Header("Layout")]
    [SerializeField] private Vector2 panelSize = new Vector2(900f, 640f);
    [SerializeField] private int bodyFontSize = 20;
    [SerializeField] private float bodyLineSpacing = 2f;
    [SerializeField] private float contentBottomPadding = 24f;

    private void Awake()
    {
        AutoBind();
        ApplyPanelLayout();
        PopulateCredits();
    }

    private bool scrollPositionInitialized;

    private void OnEnable()
    {
        AutoBind();
        scrollPositionInitialized = false;
        RefreshScrollMetrics(resetScrollPosition: true);
        StartCoroutine(RefreshScrollMetricsNextFrame());
    }

    public void ApplyPanelLayout()
    {
        AutoBind();

        if (panelRect != null)
        {
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.anchoredPosition = Vector2.zero;
            panelRect.sizeDelta = panelSize;
        }

        if (scrollRect != null)
        {
            RectTransform scrollRectTransform = scrollRect.GetComponent<RectTransform>();
            if (scrollRectTransform != null)
            {
                scrollRectTransform.anchorMin = new Vector2(0f, 1f);
                scrollRectTransform.anchorMax = new Vector2(1f, 1f);
                scrollRectTransform.pivot = new Vector2(0.5f, 1f);
                scrollRectTransform.anchoredPosition = Vector2.zero;
                scrollRectTransform.sizeDelta = new Vector2(0f, 460f);
            }

            LayoutElement scrollLayout = scrollRect.GetComponent<LayoutElement>();
            if (scrollLayout == null)
                scrollLayout = scrollRect.gameObject.AddComponent<LayoutElement>();
            scrollLayout.minHeight = 420f;
            scrollLayout.preferredHeight = 460f;
            scrollLayout.flexibleHeight = 1f;
            scrollLayout.flexibleWidth = 1f;

            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.scrollSensitivity = 30f;

            if (scrollRect.horizontalScrollbar != null)
                scrollRect.horizontalScrollbar.gameObject.SetActive(false);
            if (scrollRect.verticalScrollbar != null)
                scrollRect.verticalScrollbar.gameObject.SetActive(true);
        }

        ConfigureTitleLayout();
        ConfigureContentLayout();
        ConfigureViewportMask();
        ConfigureBodyText();
        RefreshScrollMetrics(resetScrollPosition: true);
    }

    private void ConfigureTitleLayout()
    {
        if (titleText == null)
            return;

        RectTransform titleRect = titleText.rectTransform;
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.anchoredPosition = Vector2.zero;
        titleRect.sizeDelta = new Vector2(0f, 52f);

        LayoutElement titleLayout = titleText.GetComponent<LayoutElement>();
        if (titleLayout == null)
            titleLayout = titleText.gameObject.AddComponent<LayoutElement>();
        titleLayout.minHeight = 52f;
        titleLayout.preferredHeight = 52f;
    }

    private void ConfigureContentLayout()
    {
        if (contentRoot == null)
            return;

        contentRoot.anchorMin = new Vector2(0f, 1f);
        contentRoot.anchorMax = new Vector2(1f, 1f);
        contentRoot.pivot = new Vector2(0.5f, 1f);
        contentRoot.anchoredPosition = Vector2.zero;
        contentRoot.sizeDelta = new Vector2(0f, 0f);

        VerticalLayoutGroup contentLayout = contentRoot.GetComponent<VerticalLayoutGroup>();
        if (contentLayout != null)
        {
            if (Application.isPlaying)
                Destroy(contentLayout);
            else
                DestroyImmediate(contentLayout);
        }

        ContentSizeFitter contentFitter = contentRoot.GetComponent<ContentSizeFitter>();
        if (contentFitter != null)
        {
            if (Application.isPlaying)
                Destroy(contentFitter);
            else
                DestroyImmediate(contentFitter);
        }
    }

    public void PopulateCredits()
    {
        AutoBind();

        if (titleText != null)
            titleText.text = "TIM";

        if (bodyText != null)
            bodyText.text = BuildCreditsText();

        RefreshScrollMetrics(resetScrollPosition: true);
    }

    private IEnumerator RefreshScrollMetricsNextFrame()
    {
        yield return null;
        RefreshScrollMetrics(resetScrollPosition: !scrollPositionInitialized);
        yield return null;
        RefreshScrollMetrics(resetScrollPosition: !scrollPositionInitialized);
    }

    private void RefreshScrollMetrics(bool resetScrollPosition = false)
    {
        if (contentRoot == null || bodyText == null)
            return;

        bodyText.ForceMeshUpdate(true);
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(bodyText.rectTransform);

        float bodyHeight = Mathf.Max(bodyText.preferredHeight, bodyText.renderedHeight);
        RectTransform bodyRect = bodyText.rectTransform;
        bodyRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, bodyHeight);

        float contentHeight = bodyHeight + contentBottomPadding;
        contentRoot.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, contentHeight);

        if (scrollRect != null)
        {
            scrollRect.enabled = false;
            scrollRect.enabled = true;

            if (resetScrollPosition && !scrollPositionInitialized)
            {
                scrollRect.verticalNormalizedPosition = 1f;
                scrollPositionInitialized = true;
            }
        }
    }

    private void AutoBind()
    {
        if (panelRect == null)
            panelRect = GetComponent<RectTransform>();

        if (titleText == null)
        {
            Transform title = transform.Find("Title");
            if (title != null)
                titleText = title.GetComponent<TextMeshProUGUI>();
        }

        if (scrollRect == null)
        {
            Transform scroll = transform.Find("Scroll View");
            if (scroll != null)
                scrollRect = scroll.GetComponent<ScrollRect>();
        }

        if (contentRoot == null && scrollRect != null && scrollRect.content != null)
            contentRoot = scrollRect.content;

        if (bodyText == null && contentRoot != null)
        {
            Transform existing = contentRoot.Find(BodyObjectName);
            if (existing != null)
                bodyText = existing.GetComponent<TextMeshProUGUI>();
        }
    }

    private void ConfigureBodyText()
    {
        if (contentRoot == null)
            return;

        RemoveLegacyContentChildren();

        if (bodyText == null)
        {
            GameObject bodyGo = new GameObject(BodyObjectName, typeof(RectTransform), typeof(TextMeshProUGUI));
            bodyGo.transform.SetParent(contentRoot, false);
            bodyText = bodyGo.GetComponent<TextMeshProUGUI>();
        }

        bodyText.gameObject.layer = gameObject.layer;

        RectTransform bodyRect = bodyText.rectTransform;
        bodyRect.anchorMin = new Vector2(0f, 1f);
        bodyRect.anchorMax = new Vector2(1f, 1f);
        bodyRect.pivot = new Vector2(0.5f, 1f);
        bodyRect.anchoredPosition = Vector2.zero;
        bodyRect.sizeDelta = new Vector2(-32f, 0f);

        if (bodyText.font == null && TMP_Settings.defaultFontAsset != null)
            bodyText.font = TMP_Settings.defaultFontAsset;

        bodyText.fontSize = bodyFontSize;
        bodyText.lineSpacing = bodyLineSpacing;
        bodyText.alignment = TextAlignmentOptions.TopLeft;
        bodyText.textWrappingMode = TextWrappingModes.Normal;
        bodyText.overflowMode = TextOverflowModes.Overflow;
        bodyText.color = new Color(0.1f, 0.1f, 0.1f, 1f);
        bodyText.raycastTarget = false;

        if (string.IsNullOrWhiteSpace(bodyText.text))
            bodyText.text = BuildCreditsText();

        LayoutElement bodyLayout = bodyText.GetComponent<LayoutElement>();
        if (bodyLayout != null)
        {
            if (Application.isPlaying)
                Destroy(bodyLayout);
            else
                DestroyImmediate(bodyLayout);
        }

        ContentSizeFitter bodyFitter = bodyText.GetComponent<ContentSizeFitter>();
        if (bodyFitter == null)
            bodyFitter = bodyText.gameObject.AddComponent<ContentSizeFitter>();
        bodyFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        bodyFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        Image scrollBackground = scrollRect != null ? scrollRect.GetComponent<Image>() : null;
        if (scrollBackground != null)
        {
            scrollBackground.color = new Color(1f, 1f, 1f, 0f);
            scrollBackground.raycastTarget = true;
        }

        ConfigureViewportMask();

        if (titleText != null)
        {
            titleText.fontSize = 36f;
            titleText.fontStyle = FontStyles.Bold;
            titleText.alignment = TextAlignmentOptions.Center;
            titleText.color = Color.black;
        }
    }

    private void ConfigureViewportMask()
    {
        if (scrollRect == null || scrollRect.viewport == null)
            return;

        RectTransform viewport = scrollRect.viewport;

        Mask legacyMask = viewport.GetComponent<Mask>();
        if (legacyMask != null)
        {
            if (Application.isPlaying)
                Destroy(legacyMask);
            else
                DestroyImmediate(legacyMask);
        }

        if (viewport.GetComponent<RectMask2D>() == null)
            viewport.gameObject.AddComponent<RectMask2D>();

        Image viewportImage = viewport.GetComponent<Image>();
        if (viewportImage != null)
        {
            viewportImage.color = new Color(1f, 1f, 1f, 0f);
            viewportImage.raycastTarget = true;
        }
    }

    private void RemoveLegacyContentChildren()
    {
        if (contentRoot == null)
            return;

        for (int i = contentRoot.childCount - 1; i >= 0; i--)
        {
            Transform child = contentRoot.GetChild(i);
            if (child == null || child.name == BodyObjectName)
                continue;

            if (Application.isPlaying)
                Destroy(child.gameObject);
            else
                DestroyImmediate(child.gameObject);
        }
    }

    private static string BuildCreditsText()
    {
        return
            "<size=125%><b>HealthSim</b></size>\n" +
            "Simulasi Gizi Berbasis Mobile untuk Edukasi Perilaku Hidup Sehat\n\n" +
            "HealthSim adalah serious game di bidang kesehatan yang membantu kamu memahami dampak pola makan, aktivitas fisik, dan istirahat terhadap kesehatan jangka panjang. Setiap keputusan dalam game mencerminkan realita kehidupan nyata.\n\n" +
            "<i>Dikembangkan sebagai Proyek Akhir Teknik Informatika, Universitas YARSI, 2026</i>\n\n" +
            "────────────────────────\n\n" +
            "<b>Tim HealthVerse</b>\n" +
            "Hafizh Vito Pratomo\n" +
            "Alvin Dimas Lunardi\n" +
            "Aditya Budi Listianto\n\n" +
            "────────────────────────\n\n" +
            "<b>Pembimbing Ilmu</b>\n" +
            "Paramaresthi Windriyani, S.Kom., M.Eng.\n\n" +
            "<b>Validator Medis</b>\n" +
            "dr. Sri Wuryanti, MS, Sp.GK\n" +
            "Spesialis Gizi Klinik, RS YARSI\n\n" +
            "<b>Kepala Program Studi</b>\n" +
            "Elah Suherlan, M.Si.\n\n" +
            "────────────────────────\n\n" +
            "<b>Credits</b>\n" +
            "3D Characters dibuat dengan Tripo3D\n" +
            "Environment &amp; Props dari Unity Asset Store\n" +
            "Tipografi menggunakan <b>Poppins</b> dan <b>Nunito</b>\n\n" +
            "────────────────────────\n\n" +
            "<b>Special Thanks</b>\n" +
            "Abdillah Mohamad Ismail, Muhammad Ari Alfaridzi, Rainer Ariel, dan seluruh teman-teman yang selalu support dari awal sampai selesai.";
    }
}
