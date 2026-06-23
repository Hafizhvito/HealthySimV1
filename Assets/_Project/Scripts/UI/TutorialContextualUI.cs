using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class TutorialContextualUI : MonoBehaviour
{
    private const string SampleSceneName = "SampleScene";
    private const int MaxQueueSize = 8;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        HasTalkedToNPC = false;
        HasPickedUpFood = false;
    }

    public static bool HasTalkedToNPC;
    public static bool HasPickedUpFood;

    [System.Serializable]
    private struct ToastData
    {
        public string key;
        public string icon;
        public string text;

        public ToastData(string keyValue, string iconValue, string textValue)
        {
            key = keyValue;
            icon = iconValue;
            text = textValue;
        }
    }

    [Header("Timing")]
    [SerializeField] private float conditionCheckInterval = 2f;
    [SerializeField] private float showDuration = 5f;
    [SerializeField] private float fadeInDuration = 0.3f;
    [SerializeField] private float fadeOutDuration = 0.4f;
    [SerializeField] private float slideUpDistance = 15f;

    private readonly Queue<ToastData> queue = new Queue<ToastData>();
    private readonly HashSet<string> shownHintKeys = new HashSet<string>();

    private Canvas hudCanvas;
    private RectTransform toastPanel;
    private CanvasGroup toastGroup;
    private TextMeshProUGUI toastIconText;
    private TextMeshProUGUI toastBodyText;

    private TimeManager timeManager;
    private PlayerStats playerStats;
    private WorkSessionManager workSessionManager;

    private Coroutine toastRoutine;
    private float checkTimer;
    private bool isShowingToast;
    private Vector2 shownPosition;
    private Vector2 hiddenPosition;

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
        ResetForScene(SceneManager.GetActiveScene());
    }

    private void Start()
    {
        CacheReferences();
        EnsureUi();
        HideImmediate();
    }

    private void Update()
    {
        if (!IsInSampleScene())
            return;

        if (IntroCutsceneController.IsAnyCutscenePlaying)
        {
            SuppressWhileCutsceneActive();
            return;
        }

        CacheReferencesFromSingletons();

        checkTimer += Time.deltaTime;
        if (checkTimer >= conditionCheckInterval)
        {
            checkTimer = 0f;
            EvaluateConditions();
        }

        if (!isShowingToast && queue.Count > 0 && !TutorialSequentialUI.IsSequentialVisible)
            StartNextToast();
    }

    private void SuppressWhileCutsceneActive()
    {
        if (toastRoutine != null)
        {
            StopCoroutine(toastRoutine);
            toastRoutine = null;
        }

        isShowingToast = false;
        HideImmediate();
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;

        if (toastRoutine != null)
        {
            StopCoroutine(toastRoutine);
            toastRoutine = null;
        }

        isShowingToast = false;
    }

    private void OnDestroy()
    {
        HasTalkedToNPC = false;
        HasPickedUpFood = false;
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ResetForScene(scene);
        HideImmediate();
    }

    private void ResetForScene(Scene scene)
    {
        HasTalkedToNPC = false;
        HasPickedUpFood = false;

        shownHintKeys.Clear();
        queue.Clear();

        if (toastRoutine != null)
        {
            StopCoroutine(toastRoutine);
            toastRoutine = null;
        }

        isShowingToast = false;
        checkTimer = 0f;
    }

    private void CacheReferences()
    {
        timeManager = TimeManager.Instance;
        playerStats = PlayerStats.Instance;
        workSessionManager = WorkSessionManager.Instance;
    }

    private void CacheReferencesFromSingletons()
    {
        if (timeManager == null)
            timeManager = TimeManager.Instance;

        if (playerStats == null)
            playerStats = PlayerStats.Instance;

        if (workSessionManager == null)
            workSessionManager = WorkSessionManager.Instance;
    }

    private void EvaluateConditions()
    {
        float timeElapsed = timeManager != null ? timeManager.CurrentTime : 0f;

        bool npcCondition = !HasTalkedToNPC && timeElapsed > 30f;
        TryQueueHint(
            "npc",
            "\U0001F4AC",
            TutorialInputHints.NpcToast,
            npcCondition);

        bool energyCondition = playerStats != null && playerStats.EnergyPercent < 0.70f;
        TryQueueHint(
            "energy",
            "\u26A1",
            "Energimu mulai turun - cari makanan untuk mengisinya!",
            energyCondition);

        bool foodCondition = !HasPickedUpFood && timeElapsed > 60f;
        TryQueueHint(
            "food",
            "\U0001F371",
            TutorialInputHints.FoodToast,
            foodCondition);

        bool workCondition = workSessionManager != null
            && !workSessionManager.HasWorkedToday
            && IsWorkReminderHour();

        TryQueueHint(
            "work",
            "\U0001F4BC",
            $"Kantor buka {FacilityHours.WorkHoursLabel}. Cari pintu bertulis Kantor.",
            workCondition);
    }

    private bool IsWorkReminderHour()
    {
        if (timeManager != null)
            return FacilityHours.IsWorkOpen(timeManager);

        return false;
    }

    private void TryQueueHint(string key, string icon, string text, bool condition)
    {
        if (!condition)
            return;

        if (shownHintKeys.Contains(key))
            return;

        if (queue.Count >= MaxQueueSize)
            return;

        shownHintKeys.Add(key);
        queue.Enqueue(new ToastData(key, icon, text));
    }

    private void StartNextToast()
    {
        if (queue.Count == 0 || isShowingToast)
            return;

        ToastData toast = queue.Dequeue();

        if (toastRoutine != null)
            StopCoroutine(toastRoutine);

        toastRoutine = StartCoroutine(ShowToastRoutine(toast));
    }

    private IEnumerator ShowToastRoutine(ToastData toast)
    {
        if (toastPanel == null || toastGroup == null)
            yield break;

        isShowingToast = true;

        toastPanel.gameObject.SetActive(true);
        toastGroup.alpha = 0f;
        toastGroup.interactable = false;
        toastGroup.blocksRaycasts = false;

        toastPanel.anchoredPosition = hiddenPosition;

        if (toastIconText != null)
            toastIconText.text = toast.icon;

        if (toastBodyText != null)
            toastBodyText.text = toast.text;

        float inElapsed = 0f;
        float inDuration = Mathf.Max(0.01f, fadeInDuration);

        while (inElapsed < inDuration)
        {
            inElapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(inElapsed / inDuration);
            float eased = 1f - Mathf.Pow(1f - t, 3f);

            toastGroup.alpha = Mathf.Lerp(0f, 1f, eased);
            toastPanel.anchoredPosition = Vector2.Lerp(hiddenPosition, shownPosition, eased);
            yield return null;
        }

        toastGroup.alpha = 1f;
        toastPanel.anchoredPosition = shownPosition;

        yield return new WaitForSecondsRealtime(showDuration);

        float outElapsed = 0f;
        float outDuration = Mathf.Max(0.01f, fadeOutDuration);

        while (outElapsed < outDuration)
        {
            outElapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(outElapsed / outDuration);
            toastGroup.alpha = Mathf.Lerp(1f, 0f, t);
            yield return null;
        }

        toastGroup.alpha = 0f;
        toastPanel.gameObject.SetActive(false);
        isShowingToast = false;
        toastRoutine = null;
    }

    private void EnsureUi()
    {
        if (hudCanvas == null)
            hudCanvas = FindHudCanvas();

        if (hudCanvas == null)
        {
            GameObject canvasObj = new GameObject("HUD_Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            hudCanvas = canvasObj.GetComponent<Canvas>();
            hudCanvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = canvasObj.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
        }

        Transform existingPanel = hudCanvas.transform.Find("TutorialToastPanel");
        if (existingPanel == null)
        {
            GameObject panelObj = new GameObject("TutorialToastPanel", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            toastPanel = panelObj.GetComponent<RectTransform>();
            toastPanel.SetParent(hudCanvas.transform, false);
        }
        else
        {
            toastPanel = existingPanel as RectTransform;
            if (toastPanel.GetComponent<Image>() == null)
                toastPanel.gameObject.AddComponent<Image>();
            if (toastPanel.GetComponent<CanvasGroup>() == null)
                toastPanel.gameObject.AddComponent<CanvasGroup>();
        }

        ConfigureToastPanelVisual();
        RebuildToastChildren();

        toastGroup = toastPanel.GetComponent<CanvasGroup>();
        toastIconText = toastPanel.Find("ToastContent/ToastIcon")?.GetComponent<TextMeshProUGUI>();
        toastBodyText = toastPanel.Find("ToastContent/ToastText")?.GetComponent<TextMeshProUGUI>();

        shownPosition = new Vector2(0f, 150f);
        hiddenPosition = new Vector2(0f, 150f - slideUpDistance);
        toastPanel.anchoredPosition = hiddenPosition;
    }

    private void ConfigureToastPanelVisual()
    {
        if (toastPanel == null)
            return;

        toastPanel.anchorMin = new Vector2(0.5f, 0f);
        toastPanel.anchorMax = new Vector2(0.5f, 0f);
        toastPanel.pivot = new Vector2(0.5f, 0f);
        toastPanel.offsetMin = new Vector2(-240f, 150f);
        toastPanel.offsetMax = new Vector2(240f, 202f);

        Image bg = toastPanel.GetComponent<Image>();
        bg.color = new Color32(12, 16, 28, 200);
    }

    private void RebuildToastChildren()
    {
        if (toastPanel == null)
            return;

        for (int i = toastPanel.childCount - 1; i >= 0; i--)
        {
            Transform child = toastPanel.GetChild(i);
            if (Application.isPlaying)
                Destroy(child.gameObject);
            else
                DestroyImmediate(child.gameObject);
        }

        GameObject accentObj = new GameObject("LeftAccent", typeof(RectTransform), typeof(Image));
        RectTransform accentRect = accentObj.GetComponent<RectTransform>();
        accentRect.SetParent(toastPanel, false);
        accentRect.anchorMin = new Vector2(0f, 0.5f);
        accentRect.anchorMax = new Vector2(0f, 0.5f);
        accentRect.pivot = new Vector2(0f, 0.5f);
        accentRect.anchoredPosition = new Vector2(8f, 0f);
        accentRect.sizeDelta = new Vector2(3f, 40f);
        accentObj.GetComponent<Image>().color = new Color32(255, 232, 160, 200);

        GameObject contentObj = new GameObject("ToastContent", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        RectTransform contentRect = contentObj.GetComponent<RectTransform>();
        contentRect.SetParent(toastPanel, false);
        contentRect.anchorMin = Vector2.zero;
        contentRect.anchorMax = Vector2.one;
        contentRect.offsetMin = Vector2.zero;
        contentRect.offsetMax = Vector2.zero;

        HorizontalLayoutGroup layout = contentObj.GetComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(16, 16, 0, 0);
        layout.spacing = 12f;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = false;

        TextMeshProUGUI icon = CreateTmpText(contentRect, "ToastIcon", 18f, new Color(1f, 1f, 1f, 0.7f), TextAlignmentOptions.MidlineLeft);
        LayoutElement iconLayout = icon.gameObject.AddComponent<LayoutElement>();
        iconLayout.preferredWidth = 24f;
        iconLayout.minWidth = 24f;
        iconLayout.flexibleWidth = 0f;

        TextMeshProUGUI body = CreateTmpText(contentRect, "ToastText", 14f, new Color(240f / 255f, 240f / 255f, 240f / 255f, 0.90f), TextAlignmentOptions.MidlineLeft);
        body.textWrappingMode = TextWrappingModes.NoWrap;
        body.overflowMode = TextOverflowModes.Ellipsis;

        LayoutElement bodyLayout = body.gameObject.AddComponent<LayoutElement>();
        bodyLayout.flexibleWidth = 1f;
        bodyLayout.minWidth = 360f;
    }

    private static Canvas FindHudCanvas()
    {
        GameObject canvasObj = GameObject.Find("HUD_Canvas");
        return canvasObj != null ? canvasObj.GetComponent<Canvas>() : null;
    }

    private static TextMeshProUGUI CreateTmpText(RectTransform parent, string name, float fontSize, Color color, TextAlignmentOptions align)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);

        TextMeshProUGUI tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.fontSize = fontSize;
        tmp.color = color;
        tmp.alignment = align;
        tmp.text = string.Empty;
        return tmp;
    }

    private void HideImmediate()
    {
        if (toastPanel == null || toastGroup == null)
            return;

        toastGroup.alpha = 0f;
        toastGroup.interactable = false;
        toastGroup.blocksRaycasts = false;
        toastPanel.anchoredPosition = hiddenPosition;
        toastPanel.gameObject.SetActive(false);
    }

    private static bool IsInSampleScene()
    {
        return SceneManager.GetActiveScene().name == SampleSceneName;
    }
}
