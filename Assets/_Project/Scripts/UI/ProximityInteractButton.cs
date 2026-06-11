using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class ProximityInteractButton : MonoBehaviour
{
    private const string HudCanvasName = "HUD_Canvas";
    private const string PanelName = "ProximityInteractButton";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void RegisterSceneHook()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        TryEnsureOnScene(SceneManager.GetActiveScene());
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        TryEnsureOnScene(scene);
    }

    private static void TryEnsureOnScene(Scene scene)
    {
        if (!scene.IsValid() || scene.name != "SampleScene")
            return;

        ProximityInteractButton existing = FindFirstObjectByType<ProximityInteractButton>(FindObjectsInactive.Include);
        if (existing != null)
        {
            // Scene may save this inactive; script must stay enabled to poll proximity.
            if (!existing.gameObject.activeInHierarchy)
                existing.gameObject.SetActive(true);
            return;
        }

        Canvas hudCanvas = FindHudCanvas();
        if (hudCanvas == null)
            return;

        GameObject root = new GameObject(PanelName, typeof(RectTransform), typeof(CanvasGroup), typeof(ProximityInteractButton));
        root.transform.SetParent(hudCanvas.transform, false);
        root.SetActive(true);
    }

    public static void BuildDefaultUi(RectTransform root, out CanvasGroup group, out Button button, out TextMeshProUGUI label)
    {
        ProximityInteractButtonUi.BuildOrRefresh(root, out group, out button, out label);
    }

    private static Canvas FindHudCanvas()
    {
        Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < canvases.Length; i++)
        {
            if (canvases[i] != null && canvases[i].name == HudCanvasName)
                return canvases[i];
        }

        return null;
    }
    [Header("Bindings")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private CanvasGroup panelGroup;
    [SerializeField] private Button actionButton;
    [SerializeField] private TextMeshProUGUI labelText;

    [Header("Behaviour")]
    [SerializeField] private bool hideWhenUnavailable = true;

    private UniversalInteractionController interactionController;
    private bool isVisible;

    private void Awake()
    {
        if (!gameObject.activeInHierarchy)
            gameObject.SetActive(true);

        if (panelRoot == null)
            panelRoot = gameObject;

        TryAutoBind();

        if (actionButton == null)
        {
            RectTransform rootRect = panelRoot.GetComponent<RectTransform>() ?? panelRoot.AddComponent<RectTransform>();
            BuildDefaultUi(rootRect, out CanvasGroup builtGroup, out Button builtButton, out TextMeshProUGUI builtLabel);
            panelGroup = builtGroup;
            actionButton = builtButton;
            labelText = builtLabel;
        }

        TryResolveInteractionController();
        BindButton();
        HideImmediate();

        if (transform.parent != null)
            transform.SetAsLastSibling();
    }

    private void LateUpdate()
    {
        if (interactionController == null)
            TryResolveInteractionController();

        RefreshVisibility();
    }

    private void OnClick()
    {
        if (interactionController == null)
            return;

        interactionController.TriggerInteractFromMobile();
    }

    private void RefreshVisibility()
    {
        if (interactionController == null || !interactionController.ShouldUseProximityButton())
        {
            if (isVisible)
                HideImmediate();
            return;
        }

        if (interactionController.TryGetProximityActionLabel(out string label))
            Show(label);
        else if (hideWhenUnavailable)
            HideImmediate();
    }

    private void Show(string label)
    {
        if (labelText != null)
            labelText.text = label;

        isVisible = true;
        SetVisualVisible(true);
    }

    private void HideImmediate()
    {
        isVisible = false;
        SetVisualVisible(false);
    }

    private void SetVisualVisible(bool visible)
    {
        if (panelRoot != null)
        {
            Transform shadow = panelRoot.transform.Find("Shadow");
            if (shadow != null)
                shadow.gameObject.SetActive(visible);
        }

        if (actionButton != null)
            actionButton.gameObject.SetActive(visible);

        if (panelGroup != null)
        {
            panelGroup.alpha = visible ? 1f : 0f;
            panelGroup.interactable = visible;
            panelGroup.blocksRaycasts = visible;
        }
    }

    private void TryResolveInteractionController()
    {
        if (interactionController != null)
            return;

        interactionController = FindFirstObjectByType<UniversalInteractionController>(FindObjectsInactive.Include);
    }

    private void BindButton()
    {
        if (actionButton == null)
            return;

        actionButton.onClick.RemoveListener(OnClick);
        actionButton.onClick.AddListener(OnClick);
    }

    private void TryAutoBind()
    {
        if (panelRoot == null)
            panelRoot = gameObject;

        if (panelGroup == null && panelRoot != null)
            panelGroup = panelRoot.GetComponent<CanvasGroup>();

        Transform buttonTransform = panelRoot != null ? panelRoot.transform.Find("ActionButton") : null;
        if (buttonTransform != null)
        {
            if (actionButton == null)
                actionButton = buttonTransform.GetComponent<Button>();

            if (labelText == null)
            {
                Transform label = buttonTransform.Find("LabelText");
                if (label != null)
                    labelText = label.GetComponent<TextMeshProUGUI>();
            }
        }
    }
}
