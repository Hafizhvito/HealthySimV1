using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

public class StepProgressBar : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Image barFill;
    [SerializeField] private TMP_Text txtStepLabel;

    [Header("Settings")]
    [SerializeField] private int totalSteps = 3;
    [SerializeField] private float animDuration = 0.4f;
    [SerializeField] private Ease animEase = Ease.OutCubic;

    [Header("Color Settings")]
    [SerializeField] private Color colorStart = new Color(0.29f, 0.56f, 0.85f); // biru
    [SerializeField] private Color colorEnd = new Color(0.29f, 0.85f, 0.56f); // hijau

    private int currentStep = 1;

    private void Start()
    {
        // set tampilan awal step 1
        UpdateBar(currentStep, false);
    }

    public void NextStep()
    {
        if (currentStep >= totalSteps) return;
        currentStep++;
        UpdateBar(currentStep, true);
    }

    public void PreviousStep()
    {
        if (currentStep <= 1) return;
        currentStep--;
        UpdateBar(currentStep, true);
    }

    public void SetStep(int step)
    {
        currentStep = Mathf.Clamp(step, 1, totalSteps);
        UpdateBar(currentStep, true);
    }

    // update bar lansung atau pakai animasi
    private void UpdateBar(int step, bool animate)
    {
        float targetFill = (float)step / totalSteps;
        Color targetColor = Color.Lerp(colorStart, colorEnd, targetFill);


        txtStepLabel.text = step <= totalSteps
            ? $"Langkah {step} dari {totalSteps}"
            : "Ringkasan";

        if (animate)
        {
            barFill.DOFillAmount(targetFill, animDuration).SetEase(animEase);
            barFill.DOColor(targetColor, animDuration).SetEase(animEase);
        }
        else
        {
            barFill.fillAmount = targetFill;
        }
    }

    // getter buat script lain
    public int CurrentStep => currentStep;
}
