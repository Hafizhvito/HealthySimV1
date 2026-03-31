using System.Collections;
using TMPro;
using Unity.Cinemachine;
using UnityEngine;

public class IntroCutsceneController : MonoBehaviour
{
    public event System.Action OnCutsceneCompleted;

    [Header("Narration")]
    [SerializeField] private float lineDuration = 2.2f;
    [SerializeField] private float introDelay = 0.6f;

    [Header("Panorama Shots")]
    [SerializeField] private float topDownShotDuration = 2.4f;
    [SerializeField] private float playerShotDuration = 2.0f;
    [SerializeField] private float hotspotShotDuration = 2.2f;
    [SerializeField] private int introPriority = 80;

    [Header("Shot Anchors")]
    [SerializeField] private Vector3 topDownOffset = new Vector3(0f, 22f, -0.25f);
    [SerializeField] private Vector3 playerOffset = new Vector3(0.4f, 2.1f, -3.2f);
    [SerializeField] private Vector3 hotspotOffset = new Vector3(0f, 2.8f, -5.4f);

    [Header("Environmental Cue")]
    [SerializeField] private bool pulseMainLight = true;
    [SerializeField] private float introLightIntensity = 2.6f;
    [SerializeField] private Color introLightColor = new Color(1f, 0.88f, 0.74f);

    private TextMeshProUGUI introText;
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

    void Start()
    {
        CacheSceneReferences();
    }

    public IEnumerator PlayIntro(StoryTemplate template)
    {
        if (template == null)
        {
            OnCutsceneCompleted?.Invoke();
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

        EnsureIntroCameras();
        SetupCameraForIntro();
        ApplyEnvironmentalCue(true);
        EnsureIntroText();

        yield return new WaitForSeconds(introDelay);

        string[] lines = template.lines ?? new string[0];
        string lineTopDown = lines.Length > 0 ? lines[0] : "Pagi ini kamu memulai hari di kota yang sibuk.";
        string linePlayer = lines.Length > 1 ? lines[1] : "Setiap keputusan kecil akan memengaruhi energi dan mood.";
        string lineHotspot = lines.Length > 2 ? lines[2] : "Lihat sekitarmu dan tentukan aksi sehat pertamamu.";

        ActivateIntroCamera(introTopDownCamera);
        ShowIntroLine(lineTopDown);
        yield return new WaitForSeconds(Mathf.Max(lineDuration, topDownShotDuration));

        ActivateIntroCamera(introPlayerCamera);
        ShowIntroLine(linePlayer);
        yield return new WaitForSeconds(Mathf.Max(lineDuration, playerShotDuration));

        ActivateIntroCamera(introHotspotCamera);
        ShowIntroLine(lineHotspot);
        yield return new WaitForSeconds(Mathf.Max(lineDuration, hotspotShotDuration));

        if (introText != null)
            introText.gameObject.SetActive(false);

        ApplyEnvironmentalCue(false);
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

        OnCutsceneCompleted?.Invoke();
    }

    private void SetupCameraForIntro()
    {
        if (tppCamera != null)
            tppCamera.Priority = 20;

        if (fppCamera != null)
            fppCamera.Priority = 1;

        if (introTopDownCamera != null)
            introTopDownCamera.Priority = 1;

        if (introPlayerCamera != null)
            introPlayerCamera.Priority = 1;

        if (introHotspotCamera != null)
            introHotspotCamera.Priority = 1;
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

    private void EnsureIntroText()
    {
        if (introText != null)
        {
            introText.gameObject.SetActive(true);
            return;
        }

        var canvasObj = new GameObject("IntroTextCanvas");
        var canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        var scaler = canvasObj.AddComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        canvasObj.AddComponent<UnityEngine.UI.GraphicRaycaster>();

        var textObj = new GameObject("IntroText");
        textObj.transform.SetParent(canvasObj.transform, false);

        var rect = textObj.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.1f, 0.12f);
        rect.anchorMax = new Vector2(0.9f, 0.22f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        introText = textObj.AddComponent<TextMeshProUGUI>();
        introText.alignment = TextAlignmentOptions.Center;
        introText.fontSize = 36;
        introText.color = Color.white;
        introText.textWrappingMode = TextWrappingModes.Normal;
        introText.text = string.Empty;
    }

    private void EnsureIntroCameras()
    {
        if (playerController == null)
            return;

        Transform player = playerController.transform;
        Transform hotspot = FindHotspotTransform();
        if (hotspot == null)
            hotspot = player;

        introTopDownCamera = EnsureShotCamera("CM_Intro_TopDown", player.position + topDownOffset, player, true);
        introPlayerCamera = EnsureShotCamera("CM_Intro_Player", player.position + playerOffset, player);
        introHotspotCamera = EnsureShotCamera("CM_Intro_Hotspot", hotspot.position + hotspotOffset, hotspot);
    }

    private CinemachineCamera EnsureShotCamera(string name, Vector3 position, Transform lookAt, bool strictTopDown = false)
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

        obj.transform.position = position;
        if (strictTopDown)
            obj.transform.rotation = Quaternion.Euler(84f, 0f, 0f);
        else
            obj.transform.LookAt(lookAt.position + Vector3.up * 1.1f);

        cam.Priority = 1;
        cam.Target.TrackingTarget = lookAt;
        cam.Target.LookAtTarget = lookAt;
        cam.Target.CustomLookAtTarget = true;

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
            introTopDownCamera.Priority = ReferenceEquals(target, introTopDownCamera) ? introPriority : 1;

        if (introPlayerCamera != null)
            introPlayerCamera.Priority = ReferenceEquals(target, introPlayerCamera) ? introPriority : 1;

        if (introHotspotCamera != null)
            introHotspotCamera.Priority = ReferenceEquals(target, introHotspotCamera) ? introPriority : 1;
    }

    private void ShowIntroLine(string line)
    {
        if (introText != null)
            introText.text = line;

        Debug.Log($"[Intro] {line}");
    }

    private void RestoreGameplayCamera()
    {
        if (tppCamera != null)
            tppCamera.Priority = 50;

        if (fppCamera != null)
            fppCamera.Priority = 1;

        if (introTopDownCamera != null)
            introTopDownCamera.Priority = 1;

        if (introPlayerCamera != null)
            introPlayerCamera.Priority = 1;

        if (introHotspotCamera != null)
            introHotspotCamera.Priority = 1;
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
}
