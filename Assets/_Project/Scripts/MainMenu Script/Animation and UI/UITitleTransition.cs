using UnityEngine;
using DG.Tweening;
using TMPro;
using UnityEngine.UI;

public class UITitleTransition : MonoBehaviour
{
    [Header("Fade & Scale Intro")]
    [SerializeField] private float introDuration = 0.8f;
    [SerializeField] private float introDelay = 0.3f;

    [Header("Float Animation")]
    [SerializeField] private float floatAmount = 8f;
    [SerializeField] private float floatDuration = 2f;

    private RectTransform rectTransform;
    private Image tmpImg;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        tmpImg = GetComponent<Image>();
    }

    private void Start()
    {
        // Kondisi awal — tidak terlihat
        tmpImg.color = new Color(
            tmpImg.color.r,
            tmpImg.color.g,
            tmpImg.color.b,
            0f);
        rectTransform.localScale = Vector3.one * 0.8f;

        // Animasi intro: fade in + scale up
        tmpImg.DOFade(1f, introDuration).SetDelay(introDelay).SetEase(Ease.OutCubic);

        rectTransform.DOScale(1f, introDuration).SetDelay(introDelay).SetEase(Ease.OutBack).OnComplete(StartFloatLoop);
    }

    private void StartFloatLoop()
    {
        // Float naik-turun terus menerus
        rectTransform.DOAnchorPosY(rectTransform.anchoredPosition.y + floatAmount, floatDuration).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo);
    }
}
