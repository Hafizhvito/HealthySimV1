using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ClockAnimationUI : MonoBehaviour
{
    private static Sprite runtimeCircleSprite;

    [Header("Timings")]
    [SerializeField] private float fadeDuration = 0.5f;
    [SerializeField] private float animationDuration = 4f;
    [SerializeField] private float statusHoldDuration = 1.5f;

    [Header("Energy Thresholds")]
    [SerializeField] private float fullBonusThreshold = 0.60f;
    [SerializeField] private float partialThreshold = 0.30f;
    [SerializeField] private float severeDropThreshold = 0.20f;

    private Canvas clockCanvas;
    private CanvasGroup panelGroup;
    private RectTransform clockContainer;
    private RectTransform hourHand;
    private RectTransform minuteHand;
    private TextMeshProUGUI timeLabel;
    private TextMeshProUGUI statusLabel;

    private Coroutine playRoutine;
    private bool isAnimating;

    private void Start()
    {
        EnsureUiBuilt();
        if (panelGroup != null && !isAnimating)
            panelGroup.gameObject.SetActive(false);
    }

    public void PlayWorkAnimation(WorkSessionData data, Action<WorkResult> onComplete)
    {
        if (data == null)
        {
            isAnimating = false;
            onComplete?.Invoke(WorkResult.Failed);
            return;
        }

        EnsureUiBuilt();

        if (playRoutine != null)
            StopCoroutine(playRoutine);

        isAnimating = true;
        playRoutine = StartCoroutine(PlayRoutine(data, onComplete));
    }

    public void PlayTimeSkipAnimation(string title, int startHour, int endHour, float customDuration, Action onComplete)
    {
        EnsureUiBuilt();

        if (playRoutine != null)
            StopCoroutine(playRoutine);

        isAnimating = true;
        playRoutine = StartCoroutine(PlayTimeSkipRoutine(title, startHour, endHour, customDuration, onComplete));
    }

    private IEnumerator PlayTimeSkipRoutine(string title, int startHour, int endHour, float customDuration, Action onComplete)
    {
        panelGroup.alpha = 0f;
        panelGroup.gameObject.SetActive(true);
        statusLabel.text = string.IsNullOrWhiteSpace(title) ? "Melewati waktu..." : title;
        statusLabel.color = new Color(1f, 232f / 255f, 160f / 255f, 1f);

        yield return FadePanel(0f, 1f, fadeDuration);

        float duration = Mathf.Max(0.8f, customDuration);
        float fromHour = Mathf.Repeat(startHour, 24f);
        float toHour = Mathf.Repeat(endHour, 24f);
        float hoursForward = toHour - fromHour;
        if (hoursForward <= 0f)
            hoursForward += 24f;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = Mathf.SmoothStep(0f, 1f, t);
            float currentHour = Mathf.Repeat(fromHour + (hoursForward * eased), 24f);
            UpdateClockVisual(currentHour);
            yield return null;
        }

        UpdateClockVisual(toHour);
        statusLabel.text = "Waktu berlalu... pagi tiba.";

        yield return new WaitForSecondsRealtime(Mathf.Max(0.3f, statusHoldDuration * 0.6f));

        onComplete?.Invoke();

        yield return FadePanel(1f, 0f, fadeDuration);
        panelGroup.gameObject.SetActive(false);
        isAnimating = false;
        playRoutine = null;
    }

    private IEnumerator PlayRoutine(WorkSessionData data, Action<WorkResult> onComplete)
    {
        panelGroup.alpha = 0f;
        panelGroup.gameObject.SetActive(true);
        statusLabel.text = string.Empty;

        yield return FadePanel(0f, 1f, fadeDuration);

        float energy = Mathf.Clamp01(data.energyAtStart);
        float completionRatio = 1f;
        WorkResult result = WorkResult.Full;
        bool performanceDrop = false;

        if (energy < partialThreshold)
        {
            float normalizedLowEnergy = Mathf.Clamp01(energy / Mathf.Max(0.0001f, partialThreshold));
            float minimumFinishRatio = energy < severeDropThreshold ? 0.25f : 0.40f;
            completionRatio = Mathf.Lerp(minimumFinishRatio, 0.75f, normalizedLowEnergy);
            result = WorkResult.Partial;
            performanceDrop = true;
        }

        float workHours = Mathf.Max(0f, data.endHour - data.startHour);
        float simulatedHours = workHours * completionRatio;
        float animatedEndHour = data.startHour + simulatedHours;

        float elapsed = 0f;
        bool stopTriggered = false;
        while (elapsed < animationDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, animationDuration));
            float eased = Mathf.SmoothStep(0f, 1f, t);

            float currentHour = Mathf.Lerp(data.startHour, animatedEndHour, eased);
            UpdateClockVisual(currentHour);

            if (!stopTriggered && performanceDrop && eased >= 0.985f)
            {
                stopTriggered = true;
                statusLabel.text = "Performa drop, jam kerja dihentikan!";
                statusLabel.color = new Color(1f, 0.5f, 0.5f, 1f);
            }

            yield return null;
        }

        UpdateClockVisual(animatedEndHour);

        data.completionRatio = completionRatio;
        data.performanceDropped = performanceDrop;

        bool bonus = result == WorkResult.Full && energy > fullBonusThreshold;
        if (result == WorkResult.Partial)
        {
            data.performanceNote = "Energi rendah, ritme kerja turun dan shift selesai lebih cepat.";

            if (!stopTriggered)
            {
                statusLabel.text = "Badan mulai ga kuat...";
                statusLabel.color = new Color(1f, 0.66f, 0.28f, 1f);
            }
        }
        else if (bonus)
        {
            data.performanceNote = "Performa stabil sampai akhir shift.";
            statusLabel.text = "Kerja selesai! Performa terbaik.";
            statusLabel.color = new Color(0.45f, 0.9f, 0.55f, 1f);
        }
        else
        {
            data.performanceNote = "Kerja selesai normal tanpa bonus performa.";
            statusLabel.text = "Kerja selesai.";
            statusLabel.color = Color.white;
        }

        yield return new WaitForSecondsRealtime(statusHoldDuration);

        onComplete?.Invoke(result);

        yield return FadePanel(1f, 0f, fadeDuration);
        panelGroup.gameObject.SetActive(false);
        isAnimating = false;
        playRoutine = null;
    }

    private void UpdateClockVisual(float hourValue)
    {
        float normalizedHour = Mathf.Repeat(hourValue, 12f);
        float hourRot = -(normalizedHour / 12f) * 360f;

        float minuteProgress = hourValue - Mathf.Floor(hourValue);
        float minuteRot = -(minuteProgress * 360f);

        if (hourHand != null)
            hourHand.localEulerAngles = new Vector3(0f, 0f, hourRot);

        if (minuteHand != null)
            minuteHand.localEulerAngles = new Vector3(0f, 0f, minuteRot);

        int h = Mathf.FloorToInt(hourValue);
        int m = Mathf.FloorToInt((hourValue - h) * 60f);
        h = Mathf.Clamp(h, 0, 23);
        m = Mathf.Clamp(m, 0, 59);

        if (timeLabel != null)
            timeLabel.text = string.Format("{0:00}:{1:00}", h, m);
    }

    private IEnumerator FadePanel(float from, float to, float duration)
    {
        float t = 0f;
        float d = Mathf.Max(0.01f, duration);

        while (t < d)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / d);
            panelGroup.alpha = Mathf.Lerp(from, to, k);
            yield return null;
        }

        panelGroup.alpha = to;
    }

    private void EnsureUiBuilt()
    {
        if (clockCanvas != null && panelGroup != null && hourHand != null && minuteHand != null)
            return;

        GameObject canvasObj = new GameObject("ClockCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        clockCanvas = canvasObj.GetComponent<Canvas>();
        clockCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        clockCanvas.sortingOrder = 1200;

        CanvasScaler scaler = canvasObj.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        GameObject panelObj = new GameObject("ClockPanel", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
        RectTransform panelRect = panelObj.GetComponent<RectTransform>();
        panelRect.SetParent(canvasObj.transform, false);
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        Image bg = panelObj.GetComponent<Image>();
        bg.color = new Color(5f / 255f, 6f / 255f, 10f / 255f, 245f / 255f);

        panelGroup = panelObj.GetComponent<CanvasGroup>();
        panelGroup.alpha = 0f;

        GameObject containerObj = new GameObject("ClockContainer", typeof(RectTransform));
        clockContainer = containerObj.GetComponent<RectTransform>();
        clockContainer.SetParent(panelRect, false);
        clockContainer.anchorMin = new Vector2(0.5f, 0.5f);
        clockContainer.anchorMax = new Vector2(0.5f, 0.5f);
        clockContainer.pivot = new Vector2(0.5f, 0.5f);
        clockContainer.sizeDelta = new Vector2(280f, 280f);

        Image face = CreateCircleImage("ClockFace", clockContainer, new Vector2(280f, 280f), new Color(30f / 255f, 35f / 255f, 50f / 255f, 1f));
        _ = face;
        Image border = CreateCircleImage("ClockBorder", clockContainer, new Vector2(280f, 280f), new Color(1f, 232f / 255f, 160f / 255f, 200f / 255f));
        border.type = Image.Type.Simple;

        Image inner = CreateCircleImage("ClockInner", clockContainer, new Vector2(262f, 262f), new Color(22f / 255f, 27f / 255f, 40f / 255f, 1f));
        _ = inner;

        CreateHourMarkers(clockContainer);

        hourHand = CreateHand("HourHand", clockContainer, new Vector2(6f, 70f), Color.white, 2);
        minuteHand = CreateHand("MinuteHand", clockContainer, new Vector2(4f, 90f), Color.white, 3);

        timeLabel = CreateTmp("TimeLabel", panelRect, 24f, Color.white);
        timeLabel.rectTransform.anchoredPosition = new Vector2(0f, -160f);

        statusLabel = CreateTmp("StatusLabel", panelRect, 16f, new Color(1f, 232f / 255f, 160f / 255f, 1f));
        statusLabel.rectTransform.anchoredPosition = new Vector2(0f, -200f);
    }

    private void CreateHourMarkers(RectTransform parent)
    {
        for (int i = 0; i < 12; i++)
        {
            GameObject markerObj = new GameObject($"HourMarker_{i}", typeof(RectTransform), typeof(Image));
            RectTransform rect = markerObj.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.sizeDelta = new Vector2(4f, 16f);
            rect.pivot = new Vector2(0.5f, 0f);

            float angleDeg = i * 30f;
            float rad = angleDeg * Mathf.Deg2Rad;
            float radius = 118f;

            rect.anchoredPosition = new Vector2(Mathf.Sin(rad) * radius, Mathf.Cos(rad) * radius);
            rect.localEulerAngles = new Vector3(0f, 0f, -angleDeg);

            Image img = markerObj.GetComponent<Image>();
            img.color = new Color(1f, 1f, 1f, 0.92f);
        }
    }

    private static Image CreateCircleImage(string name, RectTransform parent, Vector2 size, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;

        Image img = go.GetComponent<Image>();
        img.sprite = GetOrCreateRuntimeCircleSprite();
        img.preserveAspect = true;
        img.color = color;
        img.type = Image.Type.Simple;
        return img;
    }

    private static Sprite GetOrCreateRuntimeCircleSprite()
    {
        if (runtimeCircleSprite != null)
            return runtimeCircleSprite;

        const int size = 256;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.name = "ClockCircleSprite_Runtime";
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;
        texture.hideFlags = HideFlags.HideAndDontSave;

        Color32[] pixels = new Color32[size * size];
        float radius = (size - 1) * 0.5f;
        Vector2 center = new Vector2(radius, radius);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                int index = (y * size) + x;
                float distance = Vector2.Distance(new Vector2(x, y), center);

                // Soft one-pixel edge to avoid hard aliasing on the circle boundary.
                float alpha = Mathf.Clamp01(radius - distance + 0.5f);
                byte a = (byte)Mathf.RoundToInt(alpha * 255f);
                pixels[index] = new Color32(255, 255, 255, a);
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, true);

        runtimeCircleSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, size, size),
            new Vector2(0.5f, 0.5f),
            100f,
            0,
            SpriteMeshType.FullRect);
        runtimeCircleSprite.name = "ClockCircleSprite_Runtime";

        return runtimeCircleSprite;
    }

    private static RectTransform CreateHand(string name, RectTransform parent, Vector2 size, Color color, int siblingIndex)
    {
        GameObject handObj = new GameObject(name, typeof(RectTransform), typeof(Image));
        RectTransform rect = handObj.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.sizeDelta = size;
        rect.anchoredPosition = Vector2.zero;
        rect.SetSiblingIndex(siblingIndex + 10);

        Image img = handObj.GetComponent<Image>();
        img.color = color;
        return rect;
    }

    private static TextMeshProUGUI CreateTmp(string name, RectTransform parent, float fontSize, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(620f, 40f);

        TextMeshProUGUI tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.fontSize = fontSize;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.text = string.Empty;
        return tmp;
    }
}
