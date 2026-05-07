using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using DG.Tweening;

public class InputStepper : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TMP_Text txtValue;
    [SerializeField] private Button btnPlus;
    [SerializeField] private Button btnMinus;

    [Header("Settings")]
    [SerializeField] private float minValue = 100f;
    [SerializeField] private float maxValue = 250f;
    [SerializeField] private float defaultValue = 160f;
    [SerializeField] private float stepAmount = 1f;
    [SerializeField] private string format = "0";  // "0" = bulat, "0.0" = 1 desimal

    [Header("Hold Settings")]
    [SerializeField] private float holdDelay = 0.5f;  // delay sebelum hold aktif
    [SerializeField] private float holdInterval = 0.1f;  // kecepatan saat ditahan

    [Header("Audio")]
    [SerializeField] private AudioClip sfxTick;    // suara saat +/- ditekan
    [SerializeField] private AudioClip sfxLimit;   // suara saat sudah di batas min/max

    private float currentValue;
    private float holdTimer;
    private bool isHolding;
    private int holdDirection; // 1 = plus, -1 = minus

    // Event supaya script lain bisa subscribe
    public System.Action<float> OnValueChanged;

    private void Start()
    {
        currentValue = defaultValue;
        UpdateDisplay(false);
    }

    private void Update()
    {
        // Handle hold tombol
        if (isHolding)
        {
            holdTimer += Time.deltaTime;

            if (holdTimer >= holdDelay)
            {
                // Setelah delay, percepat perubahan nilai
                ChangeValue(holdDirection * stepAmount);
                holdTimer = holdDelay - holdInterval;
            }
        }
    }

    // ── Public Methods ────────────────────────────────────────
    public void OnPlusDown()
    {
        isHolding = true;
        holdDirection = 1;
        holdTimer = 0f;
        ChangeValue(stepAmount);
    }

    public void OnMinusDown()
    {
        isHolding = true;
        holdDirection = -1;
        holdTimer = 0f;
        ChangeValue(-stepAmount);
    }

    public void OnButtonUp()
    {
        isHolding = false;
        holdTimer = 0f;
    }

    public float GetValue() => currentValue;

    public void SetValue(float value)
    {
        currentValue = Mathf.Clamp(value, minValue, maxValue);
        UpdateDisplay(false);
    }

    // ── Private Methods ───────────────────────────────────────
    private void ChangeValue(float amount)
    {
        float newValue = Mathf.Clamp(currentValue + amount, minValue, maxValue);

        if (Mathf.Approximately(newValue, currentValue))
        {
            // Sudah di batas — play SFX limit
            AudioManager._Instance?.PlaySFX(sfxLimit);
            return;
        }

        currentValue = newValue;
        UpdateDisplay(true);
        OnValueChanged?.Invoke(currentValue);
        AudioManager._Instance?.PlaySFX(sfxTick);
    }

    private void UpdateDisplay(bool animate)
    {
        if (txtValue == null || btnMinus == null || btnPlus == null)
            return;

        txtValue.text = currentValue.ToString(format);

        if (animate)
        {
            // Animasi scale kecil saat nilai berubah
            txtValue.rectTransform.DOKill();
            txtValue.rectTransform.DOScale(1.2f, 0.08f).SetEase(Ease.OutQuad).OnComplete(() =>
                txtValue.rectTransform.DOScale(1f, 0.08f).SetEase(Ease.InQuad));
        }

        // Disable tombol kalau sudah di batas
        btnMinus.interactable = currentValue > minValue;
        btnPlus.interactable = currentValue < maxValue;
    }

    private void OnDisable()
    {
        txtValue?.rectTransform.DOKill();
    }
}
