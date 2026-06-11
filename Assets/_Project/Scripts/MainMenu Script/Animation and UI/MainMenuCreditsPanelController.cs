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
    [SerializeField] private Vector2 panelSize = new Vector2(980f, 700f);
    [SerializeField] private int bodyFontSize = 19;
    [SerializeField] private float bodyLineSpacing = 4f;

    private void Awake()
    {
        AutoBind();
        ApplyPanelLayout();
        PopulateCredits();
    }

    private void OnEnable()
    {
        AutoBind();
        RefreshScrollMetrics();
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
                scrollRectTransform.sizeDelta = Vector2.zero;
            }

            LayoutElement scrollLayout = scrollRect.GetComponent<LayoutElement>();
            if (scrollLayout == null)
                scrollLayout = scrollRect.gameObject.AddComponent<LayoutElement>();
            scrollLayout.minHeight = 520f;
            scrollLayout.flexibleHeight = 1f;
            scrollLayout.flexibleWidth = 1f;

            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Elastic;
            scrollRect.scrollSensitivity = 30f;

            if (scrollRect.horizontalScrollbar != null)
                scrollRect.horizontalScrollbar.gameObject.SetActive(false);
            if (scrollRect.verticalScrollbar != null)
                scrollRect.verticalScrollbar.gameObject.SetActive(true);
        }

        if (contentRoot != null)
        {
            contentRoot.anchorMin = new Vector2(0f, 1f);
            contentRoot.anchorMax = new Vector2(1f, 1f);
            contentRoot.pivot = new Vector2(0.5f, 1f);
            contentRoot.anchoredPosition = Vector2.zero;
            contentRoot.sizeDelta = new Vector2(0f, 0f);

            ContentSizeFitter fitter = contentRoot.GetComponent<ContentSizeFitter>();
            if (fitter == null)
                fitter = contentRoot.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            VerticalLayoutGroup contentLayout = contentRoot.GetComponent<VerticalLayoutGroup>();
            if (contentLayout == null)
                contentLayout = contentRoot.gameObject.AddComponent<VerticalLayoutGroup>();
            contentLayout.childAlignment = TextAnchor.UpperLeft;
            contentLayout.spacing = 0f;
            contentLayout.padding = new RectOffset(36, 36, 24, 24);
            contentLayout.childControlWidth = true;
            contentLayout.childControlHeight = true;
            contentLayout.childForceExpandWidth = true;
            contentLayout.childForceExpandHeight = false;
        }

        ConfigureBodyText();
        RefreshScrollMetrics();
    }

    public void PopulateCredits()
    {
        AutoBind();

        if (titleText != null)
            titleText.text = "TIM";

        if (bodyText != null)
            bodyText.text = BuildCreditsText();

        RefreshScrollMetrics();
    }

    private IEnumerator RefreshScrollMetricsNextFrame()
    {
        yield return null;
        RefreshScrollMetrics();
    }

    private void RefreshScrollMetrics()
    {
        if (contentRoot == null || bodyText == null)
            return;

        bodyText.ForceMeshUpdate(true);
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(bodyText.rectTransform);
        LayoutRebuilder.ForceRebuildLayoutImmediate(contentRoot);

        float contentHeight = LayoutUtility.GetPreferredHeight(contentRoot);
        if (contentHeight < 1f)
            contentHeight = bodyText.preferredHeight + 48f;

        contentRoot.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, contentHeight);

        if (scrollRect != null)
            scrollRect.verticalNormalizedPosition = 1f;
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

        RectTransform bodyRect = bodyText.rectTransform;
        bodyRect.anchorMin = new Vector2(0f, 1f);
        bodyRect.anchorMax = new Vector2(1f, 1f);
        bodyRect.pivot = new Vector2(0.5f, 1f);
        bodyRect.anchoredPosition = Vector2.zero;
        bodyRect.sizeDelta = new Vector2(-72f, 0f);

        bodyText.fontSize = bodyFontSize;
        bodyText.lineSpacing = bodyLineSpacing;
        bodyText.alignment = TextAlignmentOptions.TopLeft;
        bodyText.textWrappingMode = TextWrappingModes.Normal;
        bodyText.overflowMode = TextOverflowModes.Overflow;
        bodyText.color = new Color(0.93f, 0.95f, 0.98f, 1f);
        bodyText.raycastTarget = false;

        ContentSizeFitter bodyFitter = bodyText.GetComponent<ContentSizeFitter>();
        if (bodyFitter == null)
            bodyFitter = bodyText.gameObject.AddComponent<ContentSizeFitter>();
        bodyFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        bodyFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        LayoutElement layout = bodyText.GetComponent<LayoutElement>();
        if (layout == null)
            layout = bodyText.gameObject.AddComponent<LayoutElement>();
        layout.minHeight = 120f;
        layout.preferredWidth = -1f;
        layout.flexibleWidth = 1f;

        Image scrollBackground = scrollRect != null ? scrollRect.GetComponent<Image>() : null;
        if (scrollBackground != null)
            scrollBackground.color = new Color(0.08f, 0.1f, 0.14f, 0.82f);

        if (titleText != null)
            titleText.color = new Color(0.12f, 0.14f, 0.18f, 1f);
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
            "HealthSim adalah game simulasi edukasi yang membantu kamu memahami dampak pola makan, aktivitas fisik, dan istirahat terhadap kesehatan jangka panjang. Setiap keputusan dalam game mencerminkan realita kehidupan nyata.\n\n" +
            "<i>Dikembangkan sebagai Proyek Akhir Teknik Informatika, Universitas YARSI, 2026</i>\n\n" +
            "────────────────────────\n\n" +
            "<b>Tim HealthVerse</b>\n" +
            "Hafizh Vito Pratomo\n" +
            "Alvin Dimas Lunardi\n" +
            "Aditya Budi Listianto\n\n" +
            "────────────────────────\n\n" +
            "<b>Pembimbing</b>\n" +
            "Paramaresthi Windriyani, S.Kom., M.Eng.\n" +
            "Irwandi M. Zen, Lc., M.A.\n\n" +
            "<b>Validator Medis</b>\n" +
            "dr. Sri Wuryanti, MS, Sp.GK\n" +
            "Spesialis Gizi Klinik, RS YARSI\n\n" +
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
