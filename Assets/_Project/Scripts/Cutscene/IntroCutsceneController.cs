using System.Collections;
using TMPro;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.UI;

public class IntroCutsceneController : MonoBehaviour
{
    public event System.Action OnCutsceneCompleted;

    public static bool IsAnyCutscenePlaying { get; private set; }
    public bool IsPlayingCutscene { get; private set; }

    [Header("Phase Timings")]
    [SerializeField] private float phase0LetterboxDuration = 0.8f;
    [SerializeField] private float phase0FadeDuration = 1.2f;
    [SerializeField] private float topDownShotDuration = 4.2f;
    [SerializeField] private float playerShotDuration = 4.0f;
    [SerializeField] private float hotspotShotDuration = 3.6f;
    [SerializeField] private float shotCrossfadeDuration = 1.6f;
    [SerializeField] private float narrationFadeInDuration = 0.6f;
    [SerializeField] private float narrationFadeOutDuration = 0.5f;
    [SerializeField] private float titleFadeToBlackDuration = 0.8f;
    [SerializeField] private float titleFadeInDuration = 0.6f;
    [SerializeField] private float subtitleFadeInDuration = 0.5f;
    [SerializeField] private float titleHoldDuration = 1.5f;
    [SerializeField] private float titleFadeOutDuration = 0.5f;
    [SerializeField] private float finalFadeToSceneDuration = 1.0f;
    [SerializeField] private float phase6LetterboxOutDuration = 0.8f;

    [Header("Camera Priorities")]
    [SerializeField] private int introPriority = 80;
    [SerializeField] private int gameplayPriority = 50;
    [SerializeField] private int inactivePriority = 1;

    [Header("Street-Level Tour")]
    [SerializeField] private float streetShotHeight = 1.7f;
    [SerializeField] private float streetShotDistance = 8f;
    [SerializeField] private float streetShotDollySpeed = 1f;
    [SerializeField] private float sweepRadius = 6f;
    [SerializeField] private float sweepHeight = 1.8f;
    [SerializeField] private float arrivalDistance = 3f;

    [Header("Environmental Cue")]
    [SerializeField] private bool pulseMainLight = true;
    [SerializeField] private float introLightIntensity = 2.6f;
    [SerializeField] private Color introLightColor = new Color(1f, 0.88f, 0.74f);

    [Header("Title Card")]
    [SerializeField] private string titleCardText = "HealthSim";

    [Header("UI")]
    [SerializeField] private int cinematicCanvasSortOrder = 99;

    private PlayerController playerController;
    private UniversalInteractionController interactionController;
    private CinemachineCamera tppCamera;
    private CinemachineCamera fppCamera;
    private CinemachineCamera introTopDownCamera;
    private CinemachineCamera introPlayerCamera;
    private CinemachineCamera introHotspotCamera;

    private Light directionalLight;
    private float originalLightIntensity;
    private Color originalLightColor;

    private Canvas cinematicCanvas;
    private CanvasGroup fullscreenFadeGroup;
    private RectTransform letterboxTopRect;
    private RectTransform letterboxBottomRect;
    private TextMeshProUGUI introText;
    private TextMeshProUGUI titleText;
    private TextMeshProUGUI subtitleText;
    private CanvasGroup introTextGroup;
    private CanvasGroup titleTextGroup;
    private CanvasGroup subtitleTextGroup;
    private CanvasGroup vignetteGroup;
    private float letterboxHeight;

    private static float EaseInOutCubic(float t)
    {
        t = Mathf.Clamp01(t);
        return t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) / 2f;
    }

    void Start()
    {
        CacheSceneReferences();
    }

    public IEnumerator PlayIntro(StoryTemplate template)
    {
        CacheSceneReferences();

        IsPlayingCutscene = true;
        IsAnyCutscenePlaying = true;

        if (template == null)
        {
            CompleteCutscene();
            yield break;
        }

        if (playerController != null)
            playerController.LockInput("IntroCutscene");

        if (interactionController != null)
        {
            interactionController.enabled = false;
            interactionController.SetGlobalPromptSuppressed(true);
            interactionController.HideAllBubbles();
        }

        if (TimeManager.Instance != null)
            TimeManager.Instance.PauseTime();

        EnsureCinematicCanvas();
        EnsureIntroCameras();
        SetupCameraForIntro();
        ConfigureIntroCamerasForManualControl();
        ApplyEnvironmentalCue(true);

        Transform player = playerController != null ? playerController.transform : null;
        Transform hotspot = FindHotspotTransform();
        if (hotspot == null)
            hotspot = player;

        string[] lines = ResolveNarrationLines(template);

        // Phase 0 - Fade in + letterbox appear.
        PrepareUiForIntroStart();
        Coroutine letterboxIn = StartCoroutine(AnimateLetterbox(opening: true, Mathf.Max(0.01f, phase0LetterboxDuration)));
        yield return FadeCanvasGroup(fullscreenFadeGroup, 1f, 0f, Mathf.Max(0.01f, phase0FadeDuration));
        if (letterboxIn != null)
            yield return letterboxIn;

        SetVignetteActive(true);

        // Phase 1 - Aerial descent (city overview descending toward player).
        float shot1Duration = Mathf.Max(0.01f, topDownShotDuration + playerShotDuration);
        ActivateIntroCamera(introTopDownCamera);
        Coroutine shotOne = StartCoroutine(PlayAerialDescentShot(player, shot1Duration));
        string combinedNarration = lines[0] + "\n" + lines[1];
        yield return StartCoroutine(PlayNarrationLine(combinedNarration, shot1Duration));
        if (shotOne != null)
            yield return shotOne;

        yield return PlayShotCrossfade();

        // Phase 2 - Arrive at player.
        ActivateIntroCamera(introHotspotCamera);
        Coroutine shotTwo = StartCoroutine(PlayHotspotRevealShot(hotspot, Mathf.Max(0.01f, hotspotShotDuration)));
        yield return StartCoroutine(PlayNarrationLine(lines[2], Mathf.Max(0.01f, hotspotShotDuration)));
        if (shotTwo != null)
            yield return shotTwo;

        // Title card.
        yield return StartCoroutine(PlayTitleCard(template));

        // Phase 6 - Fade to scene + letterbox close + resume gameplay.
        Coroutine letterboxOut = StartCoroutine(AnimateLetterbox(opening: false, Mathf.Max(0.01f, phase6LetterboxOutDuration)));
        yield return FadeCanvasGroup(fullscreenFadeGroup, 1f, 0f, Mathf.Max(0.01f, finalFadeToSceneDuration));
        if (letterboxOut != null)
            yield return letterboxOut;

        SetVignetteActive(false);
        HideTextElements();
        ApplyEnvironmentalCue(false);
        RestoreGameplayState();
        CompleteCutscene();
    }

    private IEnumerator PlayShotCrossfade()
    {
        if (fullscreenFadeGroup == null)
            yield break;

        float half = Mathf.Max(0.01f, shotCrossfadeDuration * 0.5f);
        yield return FadeCanvasGroup(fullscreenFadeGroup, fullscreenFadeGroup.alpha, 0.3f, half);
        yield return FadeCanvasGroup(fullscreenFadeGroup, 0.3f, 0f, half);
    }

    private void SetVignetteActive(bool active)
    {
        if (vignetteGroup != null)
            vignetteGroup.alpha = active ? 0.42f : 0f;
    }

    private void OnDisable()
    {
        if (IsPlayingCutscene)
        {
            ApplyEnvironmentalCue(false);
            RestoreGameplayState();
            HideTextElements();

            if (fullscreenFadeGroup != null)
                fullscreenFadeGroup.alpha = 0f;

            SetLetterboxInstant(inside: false);
            CompleteCutscene();
        }
    }

    private void SetupCameraForIntro()
    {
        if (tppCamera != null)
            tppCamera.Priority = gameplayPriority;

        if (fppCamera != null)
            fppCamera.Priority = inactivePriority;

        if (introTopDownCamera != null)
            introTopDownCamera.Priority = inactivePriority;

        if (introPlayerCamera != null)
            introPlayerCamera.Priority = inactivePriority;

        if (introHotspotCamera != null)
            introHotspotCamera.Priority = inactivePriority;
    }

    private void CacheSceneReferences()
    {
        GameObject player = GameObject.FindWithTag("Player");
        if (player != null)
        {
            playerController = player.GetComponent<PlayerController>();
            interactionController = player.GetComponent<UniversalInteractionController>();
        }

        GameObject tppObject = GameObject.Find("CM_TPP");
        if (tppObject != null)
            tppCamera = tppObject.GetComponent<CinemachineCamera>();

        GameObject fppObject = GameObject.Find("CM_FPP");
        if (fppObject != null)
            fppCamera = fppObject.GetComponent<CinemachineCamera>();

        GameObject lightObj = GameObject.Find("Directional Light");
        if (lightObj != null)
            directionalLight = lightObj.GetComponent<Light>();

        if (directionalLight != null)
        {
            originalLightIntensity = directionalLight.intensity;
            originalLightColor = directionalLight.color;
        }
    }

    private void EnsureCinematicCanvas()
    {
        if (cinematicCanvas != null
            && fullscreenFadeGroup != null
            && letterboxTopRect != null
            && letterboxBottomRect != null
            && introText != null
            && titleText != null
            && subtitleText != null
            && vignetteGroup != null)
            return;

        GameObject canvasObj = GameObject.Find("CinematicCanvas");
        if (canvasObj == null)
            canvasObj = new GameObject("CinematicCanvas");

        var canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = cinematicCanvasSortOrder;
        cinematicCanvas = canvas;

        var scaler = canvasObj.AddComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasObj.AddComponent<UnityEngine.UI.GraphicRaycaster>();

        letterboxHeight = Mathf.Max(1f, Screen.height * 0.1f);

        GameObject fadeObj = new GameObject("fullscreenFade", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
        fadeObj.transform.SetParent(canvasObj.transform, false);
        RectTransform fadeRect = fadeObj.GetComponent<RectTransform>();
        StretchFull(fadeRect);
        Image fadeImage = fadeObj.GetComponent<Image>();
        fadeImage.color = Color.black;
        fadeImage.raycastTarget = false;
        fullscreenFadeGroup = fadeObj.GetComponent<CanvasGroup>();

        GameObject topObj = new GameObject("letterboxTop", typeof(RectTransform), typeof(Image));
        topObj.transform.SetParent(canvasObj.transform, false);
        letterboxTopRect = topObj.GetComponent<RectTransform>();
        letterboxTopRect.anchorMin = new Vector2(0f, 1f);
        letterboxTopRect.anchorMax = new Vector2(1f, 1f);
        letterboxTopRect.pivot = new Vector2(0.5f, 1f);
        letterboxTopRect.sizeDelta = new Vector2(0f, letterboxHeight);
        topObj.GetComponent<Image>().color = Color.black;

        GameObject bottomObj = new GameObject("letterboxBottom", typeof(RectTransform), typeof(Image));
        bottomObj.transform.SetParent(canvasObj.transform, false);
        letterboxBottomRect = bottomObj.GetComponent<RectTransform>();
        letterboxBottomRect.anchorMin = new Vector2(0f, 0f);
        letterboxBottomRect.anchorMax = new Vector2(1f, 0f);
        letterboxBottomRect.pivot = new Vector2(0.5f, 0f);
        letterboxBottomRect.sizeDelta = new Vector2(0f, letterboxHeight);
        bottomObj.GetComponent<Image>().color = Color.black;

        introText = CreateCinematicText(canvasObj.transform, "introText", new Vector2(0.1f, 0.08f), new Vector2(0.9f, 0.20f), 36f, FontStyles.Normal, new Color(1f, 1f, 1f, 1f), TextAlignmentOptions.Center);
        titleText = CreateCinematicText(canvasObj.transform, "titleText", new Vector2(0.1f, 0.48f), new Vector2(0.9f, 0.58f), 64f, FontStyles.Bold, new Color(1f, 1f, 1f, 1f), TextAlignmentOptions.Center);
        subtitleText = CreateCinematicText(canvasObj.transform, "subtitleText", new Vector2(0.15f, 0.40f), new Vector2(0.85f, 0.47f), 28f, FontStyles.Italic, new Color(1f, 1f, 1f, 0.8f), TextAlignmentOptions.Center);

        Outline introOutline = introText.gameObject.AddComponent<Outline>();
        introOutline.effectColor = new Color(0f, 0f, 0f, 0.6f);
        introOutline.effectDistance = new Vector2(2f, -2f);

        introTextGroup = introText.gameObject.AddComponent<CanvasGroup>();
        titleTextGroup = titleText.gameObject.AddComponent<CanvasGroup>();
        subtitleTextGroup = subtitleText.gameObject.AddComponent<CanvasGroup>();

        GameObject vignetteObj = new GameObject("vignette", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
        vignetteObj.transform.SetParent(canvasObj.transform, false);
        RectTransform vignetteRect = vignetteObj.GetComponent<RectTransform>();
        StretchFull(vignetteRect);
        Image vignetteImage = vignetteObj.GetComponent<Image>();
        vignetteImage.color = new Color(0f, 0f, 0f, 1f);
        vignetteImage.raycastTarget = false;
        vignetteGroup = vignetteObj.GetComponent<CanvasGroup>();
        vignetteGroup.alpha = 0f;
        vignetteGroup.blocksRaycasts = false;

        PrepareUiForIntroStart();
        SetLetterboxInstant(inside: false);
    }

    private TextMeshProUGUI CreateCinematicText(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, float size, FontStyles style, Color color, TextAlignmentOptions alignment)
    {
        GameObject textObj = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        textObj.transform.SetParent(parent, false);
        RectTransform rect = textObj.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        TextMeshProUGUI tmp = textObj.GetComponent<TextMeshProUGUI>();
        tmp.alignment = alignment;
        tmp.fontSize = size;
        tmp.fontStyle = style;
        tmp.color = color;
        tmp.textWrappingMode = TextWrappingModes.Normal;
        tmp.text = string.Empty;
        tmp.raycastTarget = false;
        return tmp;
    }

    private void PrepareUiForIntroStart()
    {
        if (fullscreenFadeGroup != null)
            fullscreenFadeGroup.alpha = 1f;

        if (introTextGroup != null)
            introTextGroup.alpha = 0f;

        if (titleTextGroup != null)
            titleTextGroup.alpha = 0f;

        if (subtitleTextGroup != null)
            subtitleTextGroup.alpha = 0f;

        if (introText != null)
            introText.text = string.Empty;

        if (titleText != null)
            titleText.text = string.Empty;

        if (subtitleText != null)
            subtitleText.text = string.Empty;

        SetLetterboxInstant(inside: false);
    }

    private void SetLetterboxInstant(bool inside)
    {
        if (letterboxTopRect == null || letterboxBottomRect == null)
            return;

        letterboxTopRect.anchoredPosition = inside ? Vector2.zero : new Vector2(0f, letterboxHeight);
        letterboxBottomRect.anchoredPosition = inside ? Vector2.zero : new Vector2(0f, -letterboxHeight);
    }

    private IEnumerator AnimateLetterbox(bool opening, float duration)
    {
        if (letterboxTopRect == null || letterboxBottomRect == null)
            yield break;

        float topFrom = opening ? letterboxHeight : 0f;
        float topTo = opening ? 0f : letterboxHeight;
        float bottomFrom = opening ? -letterboxHeight : 0f;
        float bottomTo = opening ? 0f : -letterboxHeight;

        float t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / duration;
            float eased = EaseInOutCubic(t);
            letterboxTopRect.anchoredPosition = new Vector2(0f, Mathf.Lerp(topFrom, topTo, eased));
            letterboxBottomRect.anchoredPosition = new Vector2(0f, Mathf.Lerp(bottomFrom, bottomTo, eased));
            yield return null;
        }

        letterboxTopRect.anchoredPosition = new Vector2(0f, topTo);
        letterboxBottomRect.anchoredPosition = new Vector2(0f, bottomTo);
    }

    private IEnumerator PlayNarrationLine(string line, float shotDuration)
    {
        if (introText == null || introTextGroup == null)
        {
            yield return new WaitForSecondsRealtime(shotDuration);
            yield break;
        }

        introText.text = line;
        introTextGroup.alpha = 0f;

        float fadeIn = Mathf.Max(0.01f, narrationFadeInDuration);
        float fadeOut = Mathf.Max(0.01f, narrationFadeOutDuration);
        float hold = Mathf.Max(0f, shotDuration - fadeIn - fadeOut);

        yield return FadeCanvasGroup(introTextGroup, 0f, 1f, fadeIn);

        if (hold > 0f)
            yield return new WaitForSecondsRealtime(hold);

        yield return FadeCanvasGroup(introTextGroup, 1f, 0f, fadeOut);
    }

    private IEnumerator PlayTopDownShot(Transform player, float duration)
    {
        if (introTopDownCamera == null || player == null)
        {
            yield return new WaitForSecondsRealtime(duration);
            yield break;
        }

        Vector3 streetDir = player.forward;
        if (streetDir.sqrMagnitude < 0.01f)
            streetDir = Vector3.forward;
        streetDir.y = 0f;
        streetDir.Normalize();

        Vector3 startPos = player.position - streetDir * streetShotDistance + Vector3.up * streetShotHeight;
        Vector3 endPos = player.position - streetDir * (streetShotDistance * 0.35f) + Vector3.up * streetShotHeight;
        Vector3 sideOffset = Vector3.Cross(Vector3.up, streetDir);

        float t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / duration * streetShotDollySpeed;
            float eased = EaseInOutCubic(Mathf.Clamp01(t));

            float sideDrift = Mathf.Sin(eased * Mathf.PI) * 1.8f;
            Vector3 pos = Vector3.Lerp(startPos, endPos, eased) + sideOffset * sideDrift;

            Vector3 lookTarget = player.position + Vector3.up * 1.4f + streetDir * 3f;
            lookTarget = Vector3.Lerp(pos + streetDir * 5f, lookTarget, eased);

            introTopDownCamera.transform.position = pos;
            introTopDownCamera.transform.rotation = Quaternion.LookRotation((lookTarget - pos).normalized, Vector3.up);
            yield return null;
        }
    }

    private IEnumerator PlayAerialDescentShot(Transform player, float duration)
    {
        if (introTopDownCamera == null || player == null)
        {
            yield return new WaitForSecondsRealtime(duration);
            yield break;
        }

        Vector3 playerFwd = player.forward;
        playerFwd.y = 0f;
        if (playerFwd.sqrMagnitude < 0.01f) playerFwd = Vector3.forward;
        playerFwd.Normalize();

        Vector3 sideDir = Vector3.Cross(Vector3.up, playerFwd);
        float startHeight = 25f;
        float endHeight = streetShotHeight + 0.5f;
        float startDist = streetShotDistance * 1.5f;
        float endDist = arrivalDistance * 1.2f;

        float t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / duration;
            float eased = EaseInOutCubic(Mathf.Clamp01(t));

            float height = Mathf.Lerp(startHeight, endHeight, eased);
            float dist = Mathf.Lerp(startDist, endDist, eased);
            float sideDrift = Mathf.Sin(eased * Mathf.PI * 0.7f) * 3f;
            float yawSweep = Mathf.Lerp(-15f, 10f, eased);

            Vector3 offset = Quaternion.Euler(0f, yawSweep, 0f) * (-playerFwd) * dist;
            offset.y = height;
            Vector3 camPos = player.position + offset + sideDir * sideDrift;

            Vector3 lookTarget = player.position + Vector3.up * Mathf.Lerp(5f, 1.4f, eased);
            introTopDownCamera.transform.position = camPos;
            introTopDownCamera.transform.rotation = Quaternion.LookRotation((lookTarget - camPos).normalized, Vector3.up);
            yield return null;
        }
    }

    private IEnumerator PlayPlayerTrackingShot(Transform player, float duration)
    {
        if (introPlayerCamera == null || player == null)
        {
            yield return new WaitForSecondsRealtime(duration);
            yield break;
        }

        float startYaw = player.eulerAngles.y + 90f;

        float t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / duration;
            float eased = EaseInOutCubic(t);

            float yaw = startYaw + eased * 120f;
            float radius = Mathf.Lerp(sweepRadius, sweepRadius * 0.7f, eased);
            float height = Mathf.Lerp(sweepHeight, sweepHeight * 0.85f, eased);

            Vector3 offset = Quaternion.Euler(0f, yaw, 0f) * new Vector3(0f, 0f, -radius);
            offset.y = height;
            Vector3 camPos = player.position + offset;

            Vector3 lookCenter = player.position + Vector3.up * 1.5f;
            Vector3 envBias = Quaternion.Euler(0f, yaw + 30f, 0f) * Vector3.forward * 4f;
            Vector3 lookTarget = Vector3.Lerp(lookCenter + envBias, lookCenter, eased);

            introPlayerCamera.transform.position = camPos;
            introPlayerCamera.transform.rotation = Quaternion.LookRotation((lookTarget - camPos).normalized, Vector3.up);
            yield return null;
        }
    }

    private IEnumerator PlayHotspotRevealShot(Transform hotspot, float duration)
    {
        if (introHotspotCamera == null || hotspot == null)
        {
            yield return new WaitForSecondsRealtime(duration);
            yield break;
        }

        Transform player = playerController != null ? playerController.transform : hotspot;
        Vector3 playerFwd = player.forward;
        playerFwd.y = 0f;
        if (playerFwd.sqrMagnitude < 0.01f)
            playerFwd = Vector3.forward;
        playerFwd.Normalize();

        Vector3 startPos = player.position
            + Quaternion.Euler(0f, -40f, 0f) * (-playerFwd) * arrivalDistance * 2f
            + Vector3.up * sweepHeight;

        Vector3 endPos = player.position - playerFwd * arrivalDistance + Vector3.up * 1.6f;

        float t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / duration;
            float eased = EaseInOutCubic(t);

            Vector3 camPos = Vector3.Lerp(startPos, endPos, eased);
            Vector3 lookTarget = player.position + Vector3.up * 1.35f;

            introHotspotCamera.transform.position = camPos;
            introHotspotCamera.transform.rotation = Quaternion.LookRotation((lookTarget - camPos).normalized, Vector3.up);
            yield return null;
        }
    }

    private IEnumerator PlayTitleCard(StoryTemplate template)
    {
        if (fullscreenFadeGroup == null)
            yield break;

        yield return FadeCanvasGroup(fullscreenFadeGroup, 0f, 1f, Mathf.Max(0.01f, titleFadeToBlackDuration));

        if (titleText != null)
            titleText.text = titleCardText;

        if (subtitleText != null)
            subtitleText.text = string.IsNullOrWhiteSpace(template.recommendedAction) ? string.Empty : template.recommendedAction;

        if (titleTextGroup != null)
            titleTextGroup.alpha = 0f;

        if (subtitleTextGroup != null)
            subtitleTextGroup.alpha = 0f;

        Coroutine titleFadeIn = titleTextGroup != null
            ? StartCoroutine(FadeCanvasGroup(titleTextGroup, 0f, 1f, Mathf.Max(0.01f, titleFadeInDuration)))
            : null;

        yield return new WaitForSecondsRealtime(0.4f);

        Coroutine subtitleFadeIn = subtitleTextGroup != null
            ? StartCoroutine(FadeCanvasGroup(subtitleTextGroup, 0f, 1f, Mathf.Max(0.01f, subtitleFadeInDuration)))
            : null;

        if (titleFadeIn != null)
            yield return titleFadeIn;

        if (subtitleFadeIn != null)
            yield return subtitleFadeIn;

        yield return new WaitForSecondsRealtime(Mathf.Max(0f, titleHoldDuration));

        Coroutine titleFadeOut = titleTextGroup != null
            ? StartCoroutine(FadeCanvasGroup(titleTextGroup, titleTextGroup.alpha, 0f, Mathf.Max(0.01f, titleFadeOutDuration)))
            : null;

        Coroutine subtitleFadeOut = subtitleTextGroup != null
            ? StartCoroutine(FadeCanvasGroup(subtitleTextGroup, subtitleTextGroup.alpha, 0f, Mathf.Max(0.01f, titleFadeOutDuration)))
            : null;

        if (titleFadeOut != null)
            yield return titleFadeOut;

        if (subtitleFadeOut != null)
            yield return subtitleFadeOut;
    }

    private string[] ResolveNarrationLines(StoryTemplate template)
    {
        string[] fallback = new[]
        {
            "Jakarta. Pagi yang belum sepenuhnya bangun.",
            "Di sini, setiap pilihan kecil meninggalkan jejak yang tak terlihat.",
            "Dan hari ini... dimulai dari meja makanmu."
        };

        string[] result = new string[3];
        for (int i = 0; i < result.Length; i++)
        {
            string value = template != null && template.lines != null && i < template.lines.Length
                ? template.lines[i]
                : fallback[i];

            result[i] = string.IsNullOrWhiteSpace(value) ? fallback[i] : value;
        }

        return result;
    }

    private void ConfigureIntroCamerasForManualControl()
    {
        ConfigureIntroCamera(introTopDownCamera);
        ConfigureIntroCamera(introPlayerCamera);
        ConfigureIntroCamera(introHotspotCamera);
    }

    private void ConfigureIntroCamera(CinemachineCamera cam)
    {
        if (cam == null)
            return;

        var target = cam.Target;
        target.TrackingTarget = null;
        target.LookAtTarget = null;
        target.CustomLookAtTarget = false;
        cam.Target = target;
    }

    private void HideTextElements()
    {
        if (introText != null)
            introText.text = string.Empty;

        if (titleText != null)
            titleText.text = string.Empty;

        if (subtitleText != null)
            subtitleText.text = string.Empty;

        if (introTextGroup != null)
            introTextGroup.alpha = 0f;

        if (titleTextGroup != null)
            titleTextGroup.alpha = 0f;

        if (subtitleTextGroup != null)
            subtitleTextGroup.alpha = 0f;
    }

    private void RestoreGameplayState()
    {
        RestoreGameplayCamera();

        if (TimeManager.Instance != null)
            TimeManager.Instance.ResumeTime();

        if (playerController != null)
            playerController.UnlockInput("IntroCutscene");

        if (interactionController != null)
        {
            interactionController.SetGlobalPromptSuppressed(false);
            interactionController.enabled = true;
        }
    }

    private void CompleteCutscene()
    {
        IsPlayingCutscene = false;
        IsAnyCutscenePlaying = false;
        OnCutsceneCompleted?.Invoke();
    }

    private static void StretchFull(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.pivot = new Vector2(0.5f, 0.5f);
    }

    private void EnsureIntroCameras()
    {
        Transform player = playerController != null ? playerController.transform : null;
        if (player == null)
            return;

        Transform hotspot = FindHotspotTransform();
        if (hotspot == null)
            hotspot = player;

        introTopDownCamera = EnsureShotCamera("CM_Intro_TopDown");
        introPlayerCamera = EnsureShotCamera("CM_Intro_Player");
        introHotspotCamera = EnsureShotCamera("CM_Intro_Hotspot");

        Vector3 playerFwd = player.forward;
        playerFwd.y = 0f;
        if (playerFwd.sqrMagnitude < 0.01f) playerFwd = Vector3.forward;
        playerFwd.Normalize();

        introTopDownCamera.transform.position = player.position - playerFwd * streetShotDistance + Vector3.up * streetShotHeight;
        introTopDownCamera.transform.LookAt(player.position + Vector3.up * streetShotHeight);

        float sweepStartYaw = player.eulerAngles.y + 90f;
        Vector3 sweepOffset = Quaternion.Euler(0f, sweepStartYaw, 0f) * new Vector3(0f, sweepHeight, -sweepRadius);
        introPlayerCamera.transform.position = player.position + sweepOffset;
        introPlayerCamera.transform.LookAt(player.position + Vector3.up * 1.5f);

        Vector3 arrivalStart = player.position
            + Quaternion.Euler(0f, -40f, 0f) * (-playerFwd) * arrivalDistance * 2f
            + Vector3.up * sweepHeight;
        introHotspotCamera.transform.position = arrivalStart;
        introHotspotCamera.transform.LookAt(player.position + Vector3.up * 1.35f);
    }

    private CinemachineCamera EnsureShotCamera(string name)
    {
        GameObject obj = GameObject.Find(name);
        CinemachineCamera cam;

        if (obj == null)
        {
            obj = new GameObject(name);
            cam = obj.AddComponent<CinemachineCamera>();
        }
        else
        {
            cam = obj.GetComponent<CinemachineCamera>();
            if (cam == null)
                cam = obj.AddComponent<CinemachineCamera>();
        }

        cam.Priority = inactivePriority;

        return cam;
    }

    private Transform FindHotspotTransform()
    {
        FoodPickupInteractable food = FindFirstObjectByType<FoodPickupInteractable>();
        if (food != null)
            return food.transform;

        NpcDialogueInteractable npc = FindFirstObjectByType<NpcDialogueInteractable>();
        if (npc != null)
            return npc.transform;

        return playerController != null ? playerController.transform : null;
    }

    private void ActivateIntroCamera(CinemachineCamera target)
    {
        if (introTopDownCamera != null)
            introTopDownCamera.Priority = ReferenceEquals(target, introTopDownCamera) ? introPriority : inactivePriority;

        if (introPlayerCamera != null)
            introPlayerCamera.Priority = ReferenceEquals(target, introPlayerCamera) ? introPriority : inactivePriority;

        if (introHotspotCamera != null)
            introHotspotCamera.Priority = ReferenceEquals(target, introHotspotCamera) ? introPriority : inactivePriority;
    }

    private void RestoreGameplayCamera()
    {
        if (tppCamera != null)
            tppCamera.Priority = gameplayPriority;

        if (fppCamera != null)
            fppCamera.Priority = inactivePriority;

        if (introTopDownCamera != null)
            introTopDownCamera.Priority = inactivePriority;

        if (introPlayerCamera != null)
            introPlayerCamera.Priority = inactivePriority;

        if (introHotspotCamera != null)
            introHotspotCamera.Priority = inactivePriority;
    }

    private void ApplyEnvironmentalCue(bool active)
    {
        if (!pulseMainLight || directionalLight == null)
            return;

        if (active)
        {
            directionalLight.intensity = introLightIntensity;
            directionalLight.color = introLightColor;
        }
        else
        {
            directionalLight.intensity = originalLightIntensity;
            directionalLight.color = originalLightColor;
        }
    }

    private IEnumerator FadeCanvasGroup(CanvasGroup cg, float from, float to, float duration)
    {
        float t = 0f;
        cg.alpha = from;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / duration;
            cg.alpha = Mathf.Lerp(from, to, EaseInOutCubic(t));
            yield return null;
        }
        cg.alpha = to;
    }
}
