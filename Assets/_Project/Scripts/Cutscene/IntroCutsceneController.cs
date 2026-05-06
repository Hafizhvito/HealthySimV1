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
    [SerializeField] private float topDownShotDuration = 3.5f;
    [SerializeField] private float playerShotDuration = 3.0f;
    [SerializeField] private float hotspotShotDuration = 3.0f;
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

    [Header("Camera Motion")]
    [SerializeField] private float cutShakeAmplitude = 0.03f;

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
    private float letterboxHeight;

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

        // Phase 1 - Aerial top-down shot.
        ActivateIntroCamera(introTopDownCamera);
        Coroutine shotOne = StartCoroutine(PlayTopDownShot(player, Mathf.Max(0.01f, topDownShotDuration)));
        yield return StartCoroutine(PlayNarrationLine(lines[0], Mathf.Max(0.01f, topDownShotDuration)));
        if (shotOne != null)
            yield return shotOne;

        // Phase 2 - Hard cut with one-frame micro-shake.
        ActivateIntroCamera(introPlayerCamera);
        yield return StartCoroutine(ApplySingleFrameCutShake(introPlayerCamera, cutShakeAmplitude));

        // Phase 3 - Player tracking shot.
        Coroutine shotTwo = StartCoroutine(PlayPlayerTrackingShot(player, Mathf.Max(0.01f, playerShotDuration)));
        yield return StartCoroutine(PlayNarrationLine(lines[1], Mathf.Max(0.01f, playerShotDuration)));
        if (shotTwo != null)
            yield return shotTwo;

        // Phase 4 - Hotspot reveal shot.
        ActivateIntroCamera(introHotspotCamera);
        Coroutine shotThree = StartCoroutine(PlayHotspotRevealShot(hotspot, Mathf.Max(0.01f, hotspotShotDuration)));
        yield return StartCoroutine(PlayNarrationLine(lines[2], Mathf.Max(0.01f, hotspotShotDuration)));
        if (shotThree != null)
            yield return shotThree;

        // Phase 5 - Title card.
        yield return StartCoroutine(PlayTitleCard(template));

        // Phase 6 - Fade to scene + letterbox close + resume gameplay.
        Coroutine letterboxOut = StartCoroutine(AnimateLetterbox(opening: false, Mathf.Max(0.01f, phase6LetterboxOutDuration)));
        yield return FadeCanvasGroup(fullscreenFadeGroup, 1f, 0f, Mathf.Max(0.01f, finalFadeToSceneDuration));
        if (letterboxOut != null)
            yield return letterboxOut;

        HideTextElements();
        ApplyEnvironmentalCue(false);
        RestoreGameplayState();
        CompleteCutscene();
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
            && subtitleText != null)
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
            t += Time.deltaTime / duration;
            float eased = Mathf.SmoothStep(0f, 1f, t);
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
            yield return new WaitForSeconds(shotDuration);
            yield break;
        }

        introText.text = line;
        introTextGroup.alpha = 0f;

        float fadeIn = Mathf.Max(0.01f, narrationFadeInDuration);
        float fadeOut = Mathf.Max(0.01f, narrationFadeOutDuration);
        float hold = Mathf.Max(0f, shotDuration - fadeIn - fadeOut);

        yield return FadeCanvasGroup(introTextGroup, 0f, 1f, fadeIn);

        if (hold > 0f)
            yield return new WaitForSeconds(hold);

        yield return FadeCanvasGroup(introTextGroup, 1f, 0f, fadeOut);
    }

    private IEnumerator PlayTopDownShot(Transform player, float duration)
    {
        if (introTopDownCamera == null || player == null)
        {
            yield return new WaitForSeconds(duration);
            yield break;
        }

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / duration;
            float eased = Mathf.SmoothStep(0f, 1f, t);

            Vector3 basePos = player.position;
            Vector3 startPos = new Vector3(basePos.x, basePos.y + 28f, basePos.z);
            Vector3 endPos = new Vector3(basePos.x, basePos.y + 18f, basePos.z);
            Quaternion startRot = Quaternion.Euler(75f, 0f, 0f);
            Quaternion endRot = Quaternion.Euler(82f, 0f, 0f);

            introTopDownCamera.transform.position = Vector3.Lerp(startPos, endPos, eased);
            introTopDownCamera.transform.rotation = Quaternion.Slerp(startRot, endRot, eased);
            yield return null;
        }
    }

    private IEnumerator PlayPlayerTrackingShot(Transform player, float duration)
    {
        if (introPlayerCamera == null || player == null)
        {
            yield return new WaitForSeconds(duration);
            yield break;
        }

        Vector3 baseOffset = new Vector3(-0.8f, 1.8f, -3.5f);
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / duration;
            float eased = Mathf.SmoothStep(0f, 1f, t);
            float orbitYaw = Mathf.Lerp(0f, 25f, eased);

            Vector3 orbitOffset = Quaternion.Euler(0f, orbitYaw, 0f) * baseOffset;
            Vector3 targetPos = player.position + orbitOffset;
            Vector3 lookTarget = player.position + Vector3.up * 1.2f;

            introPlayerCamera.transform.position = targetPos;
            introPlayerCamera.transform.rotation = Quaternion.LookRotation((lookTarget - targetPos).normalized, Vector3.up);
            yield return null;
        }
    }

    private IEnumerator PlayHotspotRevealShot(Transform hotspot, float duration)
    {
        if (introHotspotCamera == null || hotspot == null)
        {
            yield return new WaitForSeconds(duration);
            yield break;
        }

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / duration;
            float eased = Mathf.SmoothStep(0f, 1f, t);

            float distance = Mathf.Lerp(4.5f, 2.8f, eased);
            float tiltX = Mathf.Lerp(8f, 2f, eased);
            float yaw = hotspot.eulerAngles.y;

            Vector3 offset = Quaternion.Euler(0f, yaw, 0f) * new Vector3(0f, 1.2f, -distance);
            introHotspotCamera.transform.position = hotspot.position + offset;
            introHotspotCamera.transform.rotation = Quaternion.Euler(tiltX, yaw, 0f);
            yield return null;
        }
    }

    private IEnumerator ApplySingleFrameCutShake(CinemachineCamera cam, float amplitude)
    {
        if (cam == null || amplitude <= 0f)
            yield break;

        Vector3 originalPosition = cam.transform.position;
        Vector3 offset = Random.insideUnitSphere * amplitude;
        cam.transform.position = originalPosition + offset;
        yield return null;
        cam.transform.position = originalPosition;
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

        yield return new WaitForSeconds(0.4f);

        Coroutine subtitleFadeIn = subtitleTextGroup != null
            ? StartCoroutine(FadeCanvasGroup(subtitleTextGroup, 0f, 1f, Mathf.Max(0.01f, subtitleFadeInDuration)))
            : null;

        if (titleFadeIn != null)
            yield return titleFadeIn;

        if (subtitleFadeIn != null)
            yield return subtitleFadeIn;

        yield return new WaitForSeconds(Mathf.Max(0f, titleHoldDuration));

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

        introTopDownCamera.transform.position = player.position + new Vector3(0f, 28f, 0f);
        introTopDownCamera.transform.rotation = Quaternion.Euler(75f, 0f, 0f);

        introPlayerCamera.transform.position = player.position + new Vector3(-0.8f, 1.8f, -3.5f);
        introPlayerCamera.transform.LookAt(player.position + Vector3.up * 1.2f);

        float hotspotYaw = hotspot.eulerAngles.y;
        Vector3 hotspotOffset = Quaternion.Euler(0f, hotspotYaw, 0f) * new Vector3(0f, 1.2f, -4.5f);
        introHotspotCamera.transform.position = hotspot.position + hotspotOffset;
        introHotspotCamera.transform.rotation = Quaternion.Euler(8f, hotspotYaw, 0f);
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
            t += Time.deltaTime / duration;
            cg.alpha = Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t));
            yield return null;
        }
        cg.alpha = to;
    }
}
