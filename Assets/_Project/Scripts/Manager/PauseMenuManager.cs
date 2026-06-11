using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class PauseMenuManager : MonoBehaviour
{
    public static PauseMenuManager Instance { get; private set; }

    public const string PauseModalKey = "Pause";

    private const string HudCanvasName = "HUD_Canvas";
    private const string PauseRootName = "Pause";
    private const float MinButtonWidth = 320f;
    private const float MinButtonHeight = 100f;
    private const int HudCanvasSortOrder = 100;
    private const int PausePanelSortOrder = 250;
    private const float ButtonRaycastExpand = 20f;

    [Header("Panel References")]
    [SerializeField] private GameObject panelPause;
    [SerializeField] private GameObject panelOption;

    [Header("Scene Settings")]
    [SerializeField] private string menuSceneName = "MainMenu";

    [Header("UI")]
    [SerializeField] private Button pauseButton;

    [Header("Option References")]
    [SerializeField] private Slider volumeSFX;
    [SerializeField] private Slider volumeMusic;

    [Header("Audio")]
    [SerializeField] private AudioClip gameMusic;

    [Header("Layout")]
    [Tooltip("When off, Pause_Panel/Setting_Panel size & card layout follow the Hierarchy (recommended).")]
    [SerializeField] private bool autoFixPauseLayoutAtRuntime = false;

    [Tooltip("Keeps only the Pause button top-right and HUD canvas sort order at runtime.")]
    [SerializeField] private bool autoFixPauseButtonAtRuntime = true;

    private bool isPaused;
    private bool pauseModalRegistered;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        ResolvePauseUiReferences();
        WirePauseUiButtons();

        ApplyRuntimePauseUiFixes();
    }

    void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    void Start()
    {
        ResetPauseState();
        LoadOptions();
        TryPlayGameMusic();
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != "SampleScene")
            return;

        ResetPauseState();
        ResolvePauseUiReferences();
        WirePauseUiButtons();

        ApplyRuntimePauseUiFixes();
        TryPlayGameMusic();
    }

    private void TryPlayGameMusic()
    {
        if (gameMusic == null)
        {
            Debug.LogWarning("[PauseMenuManager] gameMusic belum di-assign — musik in-game tidak diputar.");
            return;
        }

        if (AudioManager._Instance == null)
        {
            Debug.LogWarning("[PauseMenuManager] AudioManager tidak ditemukan — musik in-game tidak diputar.");
            return;
        }

        AudioManager._Instance.PlayMusic(gameMusic);
    }

    private void ApplyRuntimePauseUiFixes()
    {
        if (autoFixPauseLayoutAtRuntime)
            ApplyPauseUILayout();
        else if (autoFixPauseButtonAtRuntime)
            ApplyPauseButtonLayout();
    }

    private void ResetPauseState()
    {
        ClosePauseModalIfRegistered();

        isPaused = false;
        Time.timeScale = 1f;

        if (panelPause != null)
            panelPause.SetActive(false);

        if (panelOption != null)
            panelOption.SetActive(false);

        if (pauseButton != null)
        {
            pauseButton.gameObject.SetActive(true);
            pauseButton.interactable = true;
        }
    }

    private void ResolvePauseUiReferences()
    {
        Transform pauseRoot = FindPauseRoot();
        if (pauseRoot == null)
            return;

        Transform pausePanel = pauseRoot.Find("Pause_Panel");
        Transform settingPanel = pauseRoot.Find("Setting_Panel");

        if (pausePanel != null)
            panelPause = pausePanel.gameObject;

        if (settingPanel != null)
            panelOption = settingPanel.gameObject;

        Transform pauseButtonTransform = pauseRoot.Find("PauseButton");
        if (pauseButtonTransform != null)
            pauseButton = pauseButtonTransform.GetComponent<Button>();

        if (settingPanel != null)
        {
            Transform musicSlider = FindChildRecursive(settingPanel, "MusicSlider");
            if (musicSlider != null)
                volumeMusic = musicSlider.GetComponent<Slider>();

            Transform sfxSlider = FindChildRecursive(settingPanel, "SFX_Slider");
            if (sfxSlider != null)
                volumeSFX = sfxSlider.GetComponent<Slider>();
        }
    }

    private static Transform FindChildRecursive(Transform root, string childName)
    {
        if (root == null || string.IsNullOrWhiteSpace(childName))
            return null;

        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i] != null && children[i].name == childName)
                return children[i];
        }

        return null;
    }

    private static Transform FindPauseRoot()
    {
        if (HUDManager.Instance != null)
        {
            Transform pause = HUDManager.Instance.transform.Find(PauseRootName);
            if (pause != null)
                return pause;
        }

        Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < canvases.Length; i++)
        {
            Canvas canvas = canvases[i];
            if (canvas == null || canvas.name != HudCanvasName)
                continue;

            Transform pause = canvas.transform.Find(PauseRootName);
            if (pause != null)
                return pause;
        }

        return null;
    }

    private void WirePauseUiButtons()
    {
        if (pauseButton != null)
            BindButton(pauseButton, OnPauseButton);

        if (panelPause != null)
        {
            BindNamedButton(panelPause.transform, "Resume", OnResumeButton);
            BindNamedButton(panelPause.transform, "Settings", OnSettingButton);
            BindNamedButton(panelPause.transform, "ToMenu", OnBackToMenuButton);
            BindNamedButton(panelPause.transform, "Exit", OnExitGameButton);
        }

        if (panelOption != null)
        {
            BindNamedButton(panelOption.transform, "BackButton", OnBackButton);

            if (volumeMusic != null)
            {
                volumeMusic.onValueChanged.RemoveAllListeners();
                volumeMusic.onValueChanged.AddListener(_ => OnSetMusic());
            }

            if (volumeSFX != null)
            {
                volumeSFX.onValueChanged.RemoveAllListeners();
                volumeSFX.onValueChanged.AddListener(_ => OnSetSFX());
            }
        }
    }

    private static void BindNamedButton(Transform parent, string childName, UnityAction handler)
    {
        if (parent == null)
            return;

        Transform child = FindChildRecursive(parent, childName);
        if (child == null)
            return;

        Button button = child.GetComponent<Button>();
        if (button != null)
            BindButton(button, handler);
    }

    private static void PreparePanelForTouch(Transform panelRoot)
    {
        if (panelRoot == null)
            return;

        EnsureModalCanvas(panelRoot.gameObject);

        CanvasGroup panelGroup = panelRoot.GetComponent<CanvasGroup>();
        if (panelGroup == null)
            panelGroup = panelRoot.gameObject.AddComponent<CanvasGroup>();

        panelGroup.alpha = 1f;
        panelGroup.interactable = true;
        panelGroup.blocksRaycasts = true;

        Transform card = FindChildRecursive(panelRoot, "Card");
        if (card != null)
            card.SetAsLastSibling();

        DisableDecorativeRaycasts(panelRoot);

        Button[] buttons = panelRoot.GetComponentsInChildren<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
            HardenButtonForTouch(buttons[i]);

        RectTransform content = FindChildRecursive(panelRoot, "Content") as RectTransform;
        if (content != null)
        {
            ContentSizeFitter fitter = content.GetComponent<ContentSizeFitter>();
            if (fitter != null)
                fitter.enabled = false;

            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
        }

        RectTransform panelRect = panelRoot as RectTransform;
        if (panelRect != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(panelRect);
    }

    private static void EnsureModalCanvas(GameObject panelRoot)
    {
        Canvas canvas = panelRoot.GetComponent<Canvas>();
        if (canvas == null)
            canvas = panelRoot.AddComponent<Canvas>();

        canvas.overrideSorting = true;
        canvas.sortingOrder = PausePanelSortOrder;

        if (panelRoot.GetComponent<GraphicRaycaster>() == null)
            panelRoot.AddComponent<GraphicRaycaster>();
    }

    private static void DisableDecorativeRaycasts(Transform panelRoot)
    {
        string[] decorativeNames = { "Overlay", "Shadow", "AccentBar", "Divider", "TitleText" };
        for (int i = 0; i < decorativeNames.Length; i++)
        {
            Transform child = FindChildRecursive(panelRoot, decorativeNames[i]);
            if (child == null)
                continue;

            Image image = child.GetComponent<Image>();
            if (image != null && child.name != "Overlay")
                image.raycastTarget = false;

            TextMeshProUGUI text = child.GetComponent<TextMeshProUGUI>();
            if (text != null)
                text.raycastTarget = false;
        }
    }

    private static void HardenButtonForTouch(Button button)
    {
        if (button == null)
            return;

        button.interactable = true;
        button.navigation = new Navigation { mode = Navigation.Mode.None };

        UIButtonHover hover = button.GetComponent<UIButtonHover>();
        if (hover != null)
            hover.enabled = false;

        Image image = button.GetComponent<Image>();
        if (image != null)
        {
            image.raycastTarget = true;
            image.raycastPadding = new Vector4(
                -ButtonRaycastExpand,
                -ButtonRaycastExpand,
                -ButtonRaycastExpand,
                -ButtonRaycastExpand);
            button.targetGraphic = image;
        }

        TextMeshProUGUI[] labels = button.GetComponentsInChildren<TextMeshProUGUI>(true);
        for (int j = 0; j < labels.Length; j++)
        {
            if (labels[j] != null)
                labels[j].raycastTarget = false;
        }
    }

    private static void BindButton(Button button, UnityAction handler)
    {
        if (button == null || handler == null)
            return;

        HardenButtonForTouch(button);

        PauseMenuButtonRelay relay = button.GetComponent<PauseMenuButtonRelay>();
        if (relay == null)
            relay = button.gameObject.AddComponent<PauseMenuButtonRelay>();

        relay.Configure(handler);
        button.onClick.RemoveAllListeners();
        button.interactable = true;
    }

    private void LoadOptions()
    {
        if (volumeMusic == null || volumeSFX == null)
            return;

        float savedMusic = PlayerPrefs.GetFloat("Music", 0.8f);
        float savedSFX = PlayerPrefs.GetFloat("SFX", 0.8f);

        volumeMusic.SetValueWithoutNotify(savedMusic);
        volumeSFX.SetValueWithoutNotify(savedSFX);
    }

    public void OnSetMusic()
    {
        if (AudioManager._Instance == null || volumeMusic == null)
            return;

        AudioManager._Instance.SetMusicVolume(volumeMusic.value);
    }

    public void OnSetSFX()
    {
        if (AudioManager._Instance == null || volumeSFX == null)
            return;

        AudioManager._Instance.SetSFXVolume(volumeSFX.value);
    }

    public void OnPauseButton()
    {
        if (isPaused)
            return;

        ResolvePauseUiReferences();
        WirePauseUiButtons();

        isPaused = true;
        Time.timeScale = 0f;

        RegisterPauseModal();

        if (panelPause != null)
        {
            PreparePanelForTouch(panelPause.transform);
            panelPause.SetActive(true);
            panelPause.transform.SetAsLastSibling();
        }

        Transform pauseRoot = panelPause != null ? panelPause.transform.parent : null;
        if (pauseRoot != null)
            pauseRoot.SetAsLastSibling();

        if (pauseButton != null)
            pauseButton.gameObject.SetActive(false);
    }

    public void OnResumeButton()
    {
        ClosePause();
    }

    public void OnSettingButton()
    {
        if (panelPause != null)
            panelPause.SetActive(false);

        if (panelOption != null)
        {
            PreparePanelForTouch(panelOption.transform);
            panelOption.SetActive(true);
            panelOption.transform.SetAsLastSibling();
        }
    }

    public void OnBackToMenuButton()
    {
        ClosePauseUiOnly();
        SessionResetService.ResetAllForMenuExit();

        if (AudioManager._Instance != null)
            AudioManager._Instance.StopMusic(() => SceneLoader.LoadScene(menuSceneName));
        else
            SceneLoader.LoadScene(menuSceneName);
    }

    public void OnExitGameButton()
    {
        ClosePause();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    public void OnBackButton()
    {
        if (panelPause != null)
            panelPause.SetActive(true);

        if (panelOption != null)
            panelOption.SetActive(false);
    }

    private void ClosePause()
    {
        ClosePauseUiOnly();
        ClosePauseModalIfRegistered();
    }

    private void RegisterPauseModal()
    {
        if (pauseModalRegistered || ModalStateManager.Instance == null)
            return;

        ModalStateManager.Instance.OpenModal(PauseModalKey);
        pauseModalRegistered = true;
    }

    private void ClosePauseModalIfRegistered()
    {
        if (!pauseModalRegistered || ModalStateManager.Instance == null)
            return;

        ModalStateManager.Instance.CloseModal(PauseModalKey);
        pauseModalRegistered = false;
    }

    private void ClosePauseUiOnly()
    {
        isPaused = false;
        Time.timeScale = 1f;

        if (panelPause != null)
            panelPause.SetActive(false);

        if (panelOption != null)
            panelOption.SetActive(false);

        if (pauseButton != null)
            pauseButton.gameObject.SetActive(true);
    }

    private void ApplyPauseUILayout()
    {
        Transform pauseRoot = panelPause != null ? panelPause.transform.parent : null;
        if (pauseRoot != null)
        {
            ApplyFullscreenCenteredRect(pauseRoot as RectTransform);
            pauseRoot.SetAsLastSibling();
        }

        if (panelPause != null)
            ApplyFullscreenCenteredRect(panelPause.GetComponent<RectTransform>());

        if (panelOption != null)
            ApplyFullscreenCenteredRect(panelOption.GetComponent<RectTransform>());

        EnlargeButtonsInPanel(panelPause != null ? panelPause.transform : null);
        if (panelOption != null)
            EnlargeButtonsInPanel(panelOption.transform);

        ApplyPauseButtonLayout();
    }

    private void ApplyPauseButtonLayout()
    {
        if (pauseButton == null)
            return;

        RectTransform buttonRect = pauseButton.GetComponent<RectTransform>();
        if (buttonRect == null)
            return;

        buttonRect.anchorMin = new Vector2(1f, 1f);
        buttonRect.anchorMax = new Vector2(1f, 1f);
        buttonRect.pivot = new Vector2(1f, 1f);
        buttonRect.anchoredPosition = new Vector2(-24f, -24f);
        EnsureMinTouchSize(buttonRect, 120f, 120f);
        pauseButton.transform.SetAsLastSibling();
        EnsureHudCanvasAboveMobileInput();
    }

    private void EnsureHudCanvasAboveMobileInput()
    {
        if (pauseButton == null)
            return;

        Canvas hudCanvas = pauseButton.GetComponentInParent<Canvas>();
        if (hudCanvas != null && hudCanvas.sortingOrder < HudCanvasSortOrder)
            hudCanvas.sortingOrder = HudCanvasSortOrder;
    }

    private static void ApplyFullscreenCenteredRect(RectTransform rect)
    {
        if (rect == null)
            return;

        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
    }

    private static void EnlargeButtonsInPanel(Transform panel)
    {
        if (panel == null)
            return;

        for (int i = 0; i < panel.childCount; i++)
        {
            Transform child = panel.GetChild(i);
            Button button = child.GetComponent<Button>();
            if (button == null)
                continue;

            EnsureMinTouchSize(child as RectTransform, MinButtonWidth, MinButtonHeight);
            EnsureLayoutElement(child.gameObject, MinButtonWidth, MinButtonHeight);
        }
    }

    private static void EnsureMinTouchSize(RectTransform rect, float minWidth, float minHeight)
    {
        if (rect == null)
            return;

        Vector2 size = rect.sizeDelta;
        size.x = Mathf.Max(size.x, minWidth);
        size.y = Mathf.Max(size.y, minHeight);
        rect.sizeDelta = size;
    }

    private static void EnsureLayoutElement(GameObject target, float minWidth, float minHeight)
    {
        if (target == null)
            return;

        LayoutElement layout = target.GetComponent<LayoutElement>();
        if (layout == null)
            layout = target.AddComponent<LayoutElement>();

        layout.minWidth = minWidth;
        layout.minHeight = minHeight;
        layout.preferredHeight = minHeight;
    }
}
