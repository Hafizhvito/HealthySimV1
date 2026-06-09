using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

public class InputFormManager : MonoBehaviour
{
    private const float MinStartButtonWidth = 320f;
    private const float MinStartButtonHeight = 100f;

    [Header("Panel References")]
    [SerializeField] private GameObject panelNama;
    [SerializeField] private GameObject panelTinggi;
    [SerializeField] private GameObject panelBerat;
    [SerializeField] private GameObject panelRingkasan;
    [SerializeField] private GameObject panelGender;

    [Header("Transitions")]
    [SerializeField] private UIPanelTransition transitionNama;
    [SerializeField] private UIPanelTransition transitionTinggi;
    [SerializeField] private UIPanelTransition transitionBerat;
    [SerializeField] private UIPanelTransition transitionRingkasan;
    [SerializeField] private UIPanelTransition transitionGender;

    [Header("Ringkasan Animation")]
    [SerializeField] private RectTransform[] dataCards;

    [Header("Progress Bar")]
    [SerializeField] private StepProgressBar stepProgressBar;

    [Header("Panel Fade")]
    [SerializeField] private CanvasGroup panelFade;

    [Header("Nama References")]
    [SerializeField] private TMP_InputField inputNama;

    [Header("Scene Settings")]
    [SerializeField] private string nextSceneName = "SampleScene";

    [Header("Gender References")]
    [SerializeField] private UIGenderSelector genderSelector;

    [Header("Input Stepper References")]
    [SerializeField] private InputStepper stepperTinggi;
    [SerializeField] private InputStepper stepperBerat;

    [Header("Ringkasan References")]
    [SerializeField] private TMP_Text txtNama;
    [SerializeField] private TMP_Text txtGender;
    [SerializeField] private TMP_Text txtTinggi;
    [SerializeField] private TMP_Text txtBerat;
    [SerializeField] private TMP_Text txtBMI;
    [SerializeField] private Image cardBMIBackground;

    [Header("Ringkasan Buttons")]
    [SerializeField] private Button btnMulai;

    [Header("Layout")]
    [Tooltip("When off, layout/touch fixes are skipped — use after running HealthySim/Fix Input Menu Ringkasan Layout in the Editor.")]
    [SerializeField] private bool autoFixRingkasanTouchAtRuntime = true;

    [Header("BMI Colors")]
    [SerializeField] private Color colorKurang = new Color(0.29f, 0.56f, 0.85f);
    [SerializeField] private Color colorNormal = new Color(0.15f, 0.68f, 0.38f);
    [SerializeField] private Color colorLebih = new Color(0.95f, 0.61f, 0.07f);
    [SerializeField] private Color colorObesitas = new Color(0.91f, 0.30f, 0.24f);

    private UIPanelTransition currentPanel;
    private Tweener validationTween;
    private Vector2[] cardBasePositions;
    private bool cardPositionsCached;

    private void Start()
    {
        panelNama.SetActive(true);
        panelGender.SetActive(false);
        panelTinggi.SetActive(false);
        panelBerat.SetActive(false);
        panelRingkasan.SetActive(false);

        currentPanel = transitionNama;

        if (panelFade != null)
        {
            panelFade.alpha = 1f;
            panelFade.DOFade(0f, 0.5f).SetEase(Ease.OutCubic);
        }

        PlayerData.Load();
        inputNama.text = "";

        CacheCardBasePositions();
        if (autoFixRingkasanTouchAtRuntime)
            PrepareRingkasanTouchLayer();
    }

    private void CacheCardBasePositions()
    {
        if (cardPositionsCached || dataCards == null || dataCards.Length == 0)
            return;

        cardBasePositions = new Vector2[dataCards.Length];
        for (int i = 0; i < dataCards.Length; i++)
        {
            if (dataCards[i] != null)
                cardBasePositions[i] = dataCards[i].anchoredPosition;
        }

        cardPositionsCached = true;
    }

    private void PrepareRingkasanTouchLayer()
    {
        ResolveMulaiButtonReference();
        EnsureStartButtonTouchTarget();
        EnsureRingkasanRaycastOrder();
    }

    private void ResolveMulaiButtonReference()
    {
        if (btnMulai != null || panelRingkasan == null)
            return;

        Button[] buttons = panelRingkasan.GetComponentsInChildren<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            if (buttons[i] != null && buttons[i].gameObject.name.Contains("Mulai"))
            {
                btnMulai = buttons[i];
                break;
            }
        }
    }

    private void EnsureStartButtonTouchTarget()
    {
        if (btnMulai == null)
            return;

        RectTransform rect = btnMulai.GetComponent<RectTransform>();
        if (rect == null)
            return;

        Vector2 size = rect.sizeDelta;
        size.x = Mathf.Max(size.x, MinStartButtonWidth);
        size.y = Mathf.Max(size.y, MinStartButtonHeight);
        rect.sizeDelta = size;

        LayoutElement layout = btnMulai.GetComponent<LayoutElement>();
        if (layout == null)
            layout = btnMulai.gameObject.AddComponent<LayoutElement>();

        layout.minWidth = MinStartButtonWidth;
        layout.minHeight = MinStartButtonHeight;
        layout.preferredWidth = size.x;
        layout.preferredHeight = size.y;

        Image targetGraphic = btnMulai.targetGraphic as Image;
        if (targetGraphic != null)
            targetGraphic.raycastTarget = true;
    }

    private void EnsureRingkasanRaycastOrder()
    {
        if (panelRingkasan == null)
            return;

        Image panelBackground = panelRingkasan.GetComponent<Image>();
        if (panelBackground != null)
            panelBackground.raycastTarget = false;

        Transform buttonRow = panelRingkasan.transform.Find("Button");
        if (buttonRow != null)
            buttonRow.SetAsLastSibling();

        Transform cardGroup = panelRingkasan.transform.Find("Group_DataCard");
        if (cardGroup != null)
            SetDisplayOnlyRaycasts(cardGroup, false);

        if (dataCards != null)
        {
            for (int i = 0; i < dataCards.Length; i++)
            {
                if (dataCards[i] != null)
                    SetDisplayOnlyRaycasts(dataCards[i], false);
            }
        }
    }

    private static void SetDisplayOnlyRaycasts(Transform root, bool enabled)
    {
        if (root == null)
            return;

        Graphic[] graphics = root.GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < graphics.Length; i++)
        {
            Graphic graphic = graphics[i];
            if (graphic == null || graphic.GetComponentInParent<Button>(true) != null)
                continue;

            graphic.raycastTarget = enabled;
        }
    }

    private void ShowPanel(UIPanelTransition nextPanel, int direction = 1)
    {
        if (currentPanel == nextPanel) return;
        currentPanel.Hide(direction, () => nextPanel.Show(direction));
        currentPanel = nextPanel;
    }

    private bool IsNamaValid()
    {
        string nama = inputNama.text.Trim();

        if (string.IsNullOrWhiteSpace(nama))
        {
            ShakeElement(inputNama.GetComponent<RectTransform>());
            return false;
        }

        if (nama.Length < 2)
        {
            ShakeElement(inputNama.GetComponent<RectTransform>());
            return false;
        }

        return true;
    }

    private void ShakeElement(RectTransform target)
    {
        if (target == null)
            return;

        target.DOKill();
        target.DOShakePosition(0.3f, 10f, 20).SetEase(Ease.OutCubic);
    }

    private void UpdateRingkasan()
    {
        txtNama.text = PlayerData.PlayerName;
        txtGender.text = PlayerData.JenisKelamin;
        txtTinggi.text = $"{PlayerData.TinggiBadan} cm";
        txtBerat.text = $"{PlayerData.BeratBadan} kg";
        txtBMI.text = $"{PlayerData.BMI:F1} — {PlayerData.KategoriBMI}";

        cardBMIBackground.color = PlayerData.KategoriBMI switch
        {
            "Berat Badan Kurang" => colorKurang,
            "Normal" => colorNormal,
            "Berat Badan Lebih" => colorLebih,
            _ => colorObesitas
        };

        CacheCardBasePositions();
        if (autoFixRingkasanTouchAtRuntime)
            PrepareRingkasanTouchLayer();
        AnimateCards();
    }

    private void AnimateCards()
    {
        if (dataCards == null || dataCards.Length == 0)
            return;

        CacheCardBasePositions();

        for (int i = 0; i < dataCards.Length; i++)
        {
            RectTransform card = dataCards[i];
            if (card == null)
                continue;

            card.DOKill();

            Vector2 basePos = cardBasePositions != null && i < cardBasePositions.Length
                ? cardBasePositions[i]
                : card.anchoredPosition;

            card.anchoredPosition = basePos + Vector2.right * 100f;

            CanvasGroup canvasGroup = card.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = card.gameObject.AddComponent<CanvasGroup>();

            canvasGroup.alpha = 0f;

            float delay = i * 0.12f;
            card.DOAnchorPosX(basePos.x, 0.5f).SetDelay(delay).SetEase(Ease.OutCubic);
            canvasGroup.DOFade(1f, 0.5f).SetDelay(delay).SetEase(Ease.OutCubic);
        }
    }

    public void OnLanjutDariNama()
    {
        if (!IsNamaValid())
        {
            if (inputNama != null)
                ShakeElement(inputNama.GetComponent<RectTransform>());
            return;
        }

        PlayerData.PlayerName = inputNama.text.Trim();
        stepProgressBar.NextStep();
        ShowPanel(transitionGender, 1);
    }

    public void OnLanjutDariGender()
    {
        if (!genderSelector.IsSelected())
        {
            ShakeElement(genderSelector.GetComponent<RectTransform>());
            return;
        }

        PlayerData.JenisKelamin = genderSelector.GetGender();
        stepProgressBar.NextStep();
        ShowPanel(transitionTinggi, 1);
    }

    public void OnKembaliDariGender()
    {
        stepProgressBar.PreviousStep();
        ShowPanel(transitionNama, -1);
    }

    public void OnLanjutDariTinggi()
    {
        PlayerData.TinggiBadan = stepperTinggi.GetValue();
        stepProgressBar.NextStep();
        ShowPanel(transitionBerat, 1);
    }

    public void OnKembaliDariTinggi()
    {
        stepProgressBar.PreviousStep();
        ShowPanel(transitionGender, -1);
    }

    public void OnLanjutDariBerat()
    {
        PlayerData.BeratBadan = stepperBerat.GetValue();
        stepProgressBar.NextStep();
        UpdateRingkasan();
        ShowPanel(transitionRingkasan, 1);
    }

    public void OnKembaliDariBerat()
    {
        stepProgressBar.PreviousStep();
        ShowPanel(transitionTinggi, -1);
    }

    public void OnKembaliDariRingkasan()
    {
        stepProgressBar.PreviousStep();
        ShowPanel(transitionBerat, -1);
    }

    public void OnMulaiButton()
    {
        PlayerData.Save();
        SessionResetService.ResetAllForMenuExit();
        SpawnPlayerManager.PrepareDefaultSpawnOnNextLoad();

        if (AudioManager._Instance != null)
            AudioManager._Instance.StopMusic(() => SceneLoader.LoadScene(nextSceneName));
        else
            SceneLoader.LoadScene(nextSceneName);
    }

    private void OnDisable()
    {
        validationTween?.Kill();
        panelFade?.DOKill();

        if (inputNama != null)
            inputNama.GetComponent<RectTransform>()?.DOKill();

        if (dataCards != null)
        {
            for (int i = 0; i < dataCards.Length; i++)
                dataCards[i]?.DOKill();
        }
    }
}
