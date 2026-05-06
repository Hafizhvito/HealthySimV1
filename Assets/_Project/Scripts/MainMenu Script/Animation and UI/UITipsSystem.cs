using UnityEngine;
using TMPro;
using DG.Tweening;

public class UITipsSystem : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TMP_Text tipsText;

    [Header("Settings")]
    [SerializeField] private float displayDuration = 3f;
    [SerializeField] private float fadeDuration = 0.5f;

    [Header("Tips Content")]
    [TextArea(2, 4)]
    [SerializeField] private string[] tips;

    private int currentIndex;
    private float timer;
    private bool isFading;

    private void Start()
    {
        ShuffleTips();

        tipsText.text = tips[currentIndex];
        tipsText.alpha = 1f;
    }

    private void Update()
    {
        if (isFading) return;

        timer += Time.deltaTime;

        if (timer >= displayDuration)
        {
            timer = 0f;
            ShowNextTip();
        }
    }

    private void ShowNextTip() // Animasi
    {
        isFading = true;

        tipsText.DOFade(0f, fadeDuration).SetEase(Ease.OutCubic).OnComplete(() =>
            {
                currentIndex = (currentIndex + 1) % tips.Length;
                tipsText.text = tips[currentIndex];

                tipsText.DOFade(1f, fadeDuration).SetEase(Ease.InCubic).OnComplete(() => isFading = false);
            });
    }

    private void ShuffleTips() // Ngerandom Tips (opsional sih, kalo gak dipake hapus aja nanti)
    {
        for (int i = tips.Length - 1; i > 0; i--)
        {
            int randomIndex = Random.Range(0, i + 1);
            (tips[i], tips[randomIndex]) = (tips[randomIndex], tips[i]);
        }
    }
}
