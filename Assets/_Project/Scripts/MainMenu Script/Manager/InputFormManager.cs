using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

public class InputFormManager : MonoBehaviour
{
    [Header("Panel References")]
    [SerializeField] private GameObject panelNama;
    [SerializeField] private GameObject panelTinggi;
    [SerializeField] private GameObject panelBerat;
    [SerializeField] private GameObject panelRingkasan;

    [Header("Transitions")]
    [SerializeField] private UIPanelTransition transitionNama;
    [SerializeField] private UIPanelTransition transitionTinggi;
    [SerializeField] private UIPanelTransition transitionBerat;
    [SerializeField] private UIPanelTransition transitionRingkasan;

    [Header("Ringkasan Animation")]
    [SerializeField] private RectTransform[] dataCards;

    [Header("Progress Bar")]
    [SerializeField] private StepProgressBar stepProgressBar;

    [Header("Panel Fade")]
    [SerializeField] private CanvasGroup panelFade;

    [Header("Nama References")]
    [SerializeField] private TMP_InputField inputNama;

    [Header("Scene Settings")]
    [SerializeField] private string nextSceneName = "GameScene";

    [Header("Skip Form")]
    [SerializeField] private GameObject panelSkipConfirm;
    [SerializeField] private TMP_Text txtValidation; // tambah di Panel_Nama
    [SerializeField] private TMP_Text txtDataPreview;

    [Header("Input Stepper References")]
    [SerializeField] private InputStepper stepperTinggi;
    [SerializeField] private InputStepper stepperBerat;

    [Header("Ringkasan References")]
    [SerializeField] private TMP_Text txtNama;
    [SerializeField] private TMP_Text txtTinggi;
    [SerializeField] private TMP_Text txtBerat;
    [SerializeField] private TMP_Text txtBMI;
    [SerializeField] private Image cardBMIBackground;

    [Header("BMI Colors")]
    [SerializeField] private Color colorKurang = new Color(0.29f, 0.56f, 0.85f);
    [SerializeField] private Color colorNormal = new Color(0.15f, 0.68f, 0.38f);
    [SerializeField] private Color colorLebih = new Color(0.95f, 0.61f, 0.07f);
    [SerializeField] private Color colorObesitas = new Color(0.91f, 0.30f, 0.24f);

    private UIPanelTransition currentPanel;

    private void Start()
    {
        panelNama.SetActive(true);
        panelTinggi.SetActive(false);
        panelBerat.SetActive(false);
        panelRingkasan.SetActive(false);
        panelSkipConfirm.SetActive(false);

        currentPanel = transitionNama;

        panelFade.alpha = 1f;
        panelFade.DOFade(0f, 0.5f).SetEase(Ease.OutCubic);

        PlayerData.Load();

        // Cek apakah sudah ada data sebelumnya
        if (!string.IsNullOrEmpty(PlayerData.PlayerName))
        {
            ShowSkipConfirm();
        }
        else
        {
            inputNama.text = "";
        }
    }


    private void ShowSkipConfirm()
    {
        txtDataPreview.text =
        $"Nama: {PlayerData.PlayerName}\n" +
        $"Tinggi: {PlayerData.TinggiBadan} cm\n" +
        $"Berat: {PlayerData.BeratBadan} kg\n" +
        $"BMI: {PlayerData.BMI:F1} ({PlayerData.KategoriBMI})";
        panelSkipConfirm.SetActive(true);
    }

    // ── Navigasi Panel ────────────────────────────────────────
    private void ShowPanel(UIPanelTransition nextPanel, int direction = 1)
    {
        if (currentPanel == nextPanel) return;
        currentPanel.Hide(direction, () => nextPanel.Show(direction));
        currentPanel = nextPanel;
    }

    // ── Validasi ──────────────────────────────────────────────
    private bool IsNamaValid()
    {
        string nama = inputNama.text.Trim();

        if (string.IsNullOrWhiteSpace(nama))
        {
            ShakeElement(inputNama.GetComponent<RectTransform>());
            ShowValidationMessage("Nama tidak boleh kosong!");
            return false;
        }

        if (nama.Length < 2)
        {
            ShakeElement(inputNama.GetComponent<RectTransform>());
            ShowValidationMessage("Nama terlalu pendek!");
            return false;
        }

        return true;
    }

    private void ShakeElement(RectTransform target) // Animasi
    {
        target.DOShakePosition(0.3f, 10f, 20).SetEase(Ease.OutCubic);
    }

    // Tampilkan pesan validasi sementara

    private Tweener validationTween;

    private void ShowValidationMessage(string message)
    {
        txtValidation.text = message;
        txtValidation.alpha = 1f;

        // Auto hide setelah 2 detik
        validationTween?.Kill();
        validationTween = txtValidation.DOFade(0f, 0.3f).SetDelay(2f);
    }

    // ── Ringkasan ─────────────────────────────────────────────

    private void UpdateRingkasan()
    {
        txtNama.text = PlayerData.PlayerName;
        txtTinggi.text = $"{PlayerData.TinggiBadan} cm";
        txtBerat.text = $"{PlayerData.BeratBadan} kg";
        txtBMI.text = $"{PlayerData.BMI:F1} — {PlayerData.KategoriBMI}";

        // Update warna card BMI
        cardBMIBackground.color = PlayerData.KategoriBMI switch
        {
            "Berat Badan Kurang" => colorKurang,
            "Normal" => colorNormal,
            "Berat Badan Lebih" => colorLebih,
            _ => colorObesitas
        };

        AnimateCards();
    }

    private void AnimateCards()
    {
        foreach (var card in dataCards)
        {
            // Reset posisi & alpha
            card.anchoredPosition += Vector2.right * 100f;
            CanvasGroup cg = card.GetComponent<CanvasGroup>();
            if (cg != null) cg.alpha = 0f;
        }

        // Animasi masuk satu per satu dengan delay
        for (int i = 0; i < dataCards.Length; i++)
        {
            int index = i;
            float delay = i * 0.1f;

            dataCards[i].DOAnchorPosX(
                dataCards[i].anchoredPosition.x - 100f, 0.3f).SetDelay(delay).SetEase(Ease.OutCubic);

            CanvasGroup cg = dataCards[i].GetComponent<CanvasGroup>();
            if (cg != null)
            {
                cg.DOFade(1f, 0.3f).SetDelay(delay).SetEase(Ease.OutCubic);
            }
        }
    }

    // ── Button Callbacks ──────────────────────────────────────
    public void OnLanjutDariNama()
    {
        if (!IsNamaValid())
        {
            // Animasi shake input field jika kosong
            inputNama.GetComponent<RectTransform>().DOShakePosition(0.3f, 10f, 20);
            return;
        }

        // Simpan nama
        PlayerData.PlayerName = inputNama.text.Trim();

        // Lanjut ke step berikutnya
        stepProgressBar.NextStep();
        ShowPanel(transitionTinggi, 1);
        // AudioManager._Instance?.PlaySFX(null);
    }

    public void OnLanjutDariTinggi()
    {
        // Simpan data tinggi badan
        PlayerData.TinggiBadan = stepperTinggi.GetValue();

        stepProgressBar.NextStep();
        ShowPanel(transitionBerat, 1);
    }

    public void OnKembaliDariTinggi()
    {
        stepProgressBar.PreviousStep();
        ShowPanel(transitionNama, -1);
    }

    public void OnLanjutDariBerat()
    {
        // Simpan data berat badan
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
        // Simpan semua data ke PlayerPrefs
        PlayerData.Save();

        // Masuk ke GameScene via LoadingScreen
        AudioManager._Instance?.StopMusic(() => SceneLoader.LoadScene(nextSceneName));
    }

    // Tombol "Gunakan Data Lama" di panelSkipConfirm
    public void OnGunakanDataLama()
    {
        panelSkipConfirm.SetActive(false);
        UpdateRingkasan();

        // Langsung ke ringkasan
        panelNama.SetActive(false);
        panelRingkasan.SetActive(true);
        currentPanel = transitionRingkasan;

        // Set progress bar ke penuh
        stepProgressBar.SetStep(3);
    }

    // Tombol "Input Ulang" di panelSkipConfirm
    public void OnInputUlang()
    {
        panelSkipConfirm.SetActive(false);
        inputNama.text = PlayerData.PlayerName;
    }
}
