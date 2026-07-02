using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// FPP/TPP toggle on HUD_Canvas. Place via HealthySim/Setup Camera Toggle Button.
/// </summary>
public class CameraToggleButton : MonoBehaviour
{
    private const string PanelName = "CameraToggleButton";

    [Header("Bindings")]
    [SerializeField] private Button button;
    [SerializeField] private Image background;
    [SerializeField] private TextMeshProUGUI modeLabel;
    [SerializeField] private TextMeshProUGUI arrowLabel;

    [Header("Style (borrowed from menu at setup time)")]
    [SerializeField] private Color menuGreenColor = new Color(0.337f, 0.725f, 0.125f, 1f);
    [SerializeField] private Sprite menuBorrowedSprite;

    private CameraSystem cameraSystem;
    private bool lastFpp;

    private void Awake()
    {
        EnsureBindings();
        ApplyBorrowedSprite();
        BindButton();
        CameraToggleButtonUi.ApplyVisuals(background, modeLabel, arrowLabel, false, menuGreenColor);
        lastFpp = false;
    }

    private void ApplyBorrowedSprite()
    {
        if (background == null) return;
        if (menuBorrowedSprite != null)
        {
            background.sprite = menuBorrowedSprite;
            background.type = Image.Type.Sliced;
        }
        background.color = menuGreenColor;
    }

    private void Start()
    {
        cameraSystem = FindFirstObjectByType<CameraSystem>();
    }

    private void Update()
    {
        if (cameraSystem == null)
            cameraSystem = FindFirstObjectByType<CameraSystem>();

        bool fpp = cameraSystem != null && cameraSystem.IsFirstPerson;
        if (fpp == lastFpp)
            return;

        lastFpp = fpp;
        CameraToggleButtonUi.ApplyVisuals(background, modeLabel, arrowLabel, fpp, menuGreenColor);
    }

    private void EnsureBindings()
    {
        if (button != null && background != null && modeLabel != null && arrowLabel != null)
            return;

        RectTransform root = GetComponent<RectTransform>() ?? gameObject.AddComponent<RectTransform>();
        CameraToggleButtonUi.BuildOrRefresh(root, out Button builtButton, out Image builtBg,
            out TextMeshProUGUI builtMode, out TextMeshProUGUI builtArrow);

        if (button == null) button = builtButton;
        if (background == null) background = builtBg;
        if (modeLabel == null) modeLabel = builtMode;
        if (arrowLabel == null) arrowLabel = builtArrow;
        ApplyBorrowedSprite();
    }

    private void BindButton()
    {
        if (button == null)
            return;

        button.onClick.RemoveListener(OnClick);
        button.onClick.AddListener(OnClick);
    }

    private void OnClick()
    {
        if (cameraSystem == null)
            cameraSystem = FindFirstObjectByType<CameraSystem>();

        // Clear pending swipe delta before switching mode to avoid a phantom look jump.
        if (MobileInputController.Instance != null)
            MobileInputController.Instance.ResetSwipeOnToggle();

        if (cameraSystem != null)
            cameraSystem.TogglePerspectiveFromMobile();
    }

#if UNITY_EDITOR
    public void EditorRefreshBindings()
    {
        EnsureBindings();
        BindButton();
        UnityEditor.EditorUtility.SetDirty(this);
    }
#endif
}
