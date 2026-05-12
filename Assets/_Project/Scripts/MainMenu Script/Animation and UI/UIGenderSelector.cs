using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

public class UIGenderSelector : MonoBehaviour
{
    [Header("Buttons")]
    [SerializeField] private Button btnLakiLaki;
    [SerializeField] private Button btnPerempuan;

    // [Header("Colors")]
    // [SerializeField] private Color selectedColor = new Color(0.29f, 0.56f, 0.85f);
    // [SerializeField] private Color unselectedColor = new Color(0.16f, 0.16f, 0.29f);
    // [SerializeField] private Color selectedTextColor = Color.white;
    // [SerializeField] private Color unselectedTextColor = new Color(0.7f, 0.7f, 0.7f);

    [Header("Animation")]
    [SerializeField] private float animDuration = 0.2f;
    [SerializeField] private float targetSelected = 1.1f;

    private Image _imgLakiLaki;
    private Image _imgPerempuan;
    private TMP_Text _txtLakiLaki;
    private TMP_Text _txtPerempuan;

    private string _selectedGender = "";

    private void Awake()
    {
        _imgLakiLaki = btnLakiLaki.GetComponent<Image>();
        _imgPerempuan = btnPerempuan.GetComponent<Image>();
        _txtLakiLaki = btnLakiLaki.GetComponentInChildren<TMP_Text>();
        _txtPerempuan = btnPerempuan.GetComponentInChildren<TMP_Text>();
    }

    private void Start()
    {
        // Set tampilan awal unselected
        SetButtonVisual(_imgLakiLaki, _txtLakiLaki, false, false);
        SetButtonVisual(_imgPerempuan, _txtPerempuan, false, false);

        // Load gender sebelumnya jika ada
        // if (!string.IsNullOrEmpty(PlayerData.Gender))
        // {
        //     SelectGender(PlayerData.Gender, false);
        // }
        // else
        // {
        //     // Set tampilan awal unselected
        //     SetButtonVisual(imgLakiLaki, txtLakiLaki, false, false);
        //     SetButtonVisual(imgPerempuan, txtPerempuan, false, false);
        // }

        // Setup button listeners
        btnLakiLaki.onClick.AddListener(() => SelectGender("Laki-laki", true));
        btnPerempuan.onClick.AddListener(() => SelectGender("Perempuan", true));
    }

    // ── Public ────────────────────────────────────────────────
    public string GetGender() => _selectedGender;

    public bool IsSelected() => !string.IsNullOrEmpty(_selectedGender);

    // ── Private ───────────────────────────────────────────────
    private void SelectGender(string gender, bool animate)
    {
        _selectedGender = gender;

        bool lakiSelected = gender == "Laki-laki";
        bool perempuanSelected = gender == "Perempuan";

        SetButtonVisual(_imgLakiLaki, _txtLakiLaki, lakiSelected, animate);
        SetButtonVisual(_imgPerempuan, _txtPerempuan, perempuanSelected, animate);

        // SFX
        AudioManager._Instance?.PlaySFX(null);
    }

    private void SetButtonVisual(
        Image img, TMP_Text txt, bool isSelected, bool animate)
    {
        // Color targetImgColor = isSelected ? selectedColor : unselectedColor;
        // Color targetTxtColor = isSelected ? selectedTextColor : unselectedTextColor;
        float targetScale = isSelected ? targetSelected : 1f;

        if (animate)
        {
            // img.DOColor(targetImgColor, animDuration);
            // txt.DOColor(targetTxtColor, animDuration);
            img.rectTransform.DOScale(targetScale, animDuration)
                .SetEase(Ease.OutBack);
        }
        else
        {
            // img.color = targetImgColor;
            // txt.color = targetTxtColor;
            img.rectTransform.localScale = Vector3.one * targetScale;
        }
    }
}
