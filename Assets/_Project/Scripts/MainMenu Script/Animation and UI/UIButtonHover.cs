using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using DG.Tweening;

public class UIButtonHover : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    [Header("Scale Effect")]
    [SerializeField] private float pressScale = 0.92f;
    [SerializeField] private float scaleDuration = 0.1f;

    [Header("Color Effect")]
    [SerializeField] private Color normalColor = new Color(1f, 1f, 1f, 1f);
    [SerializeField] private Color pressColor = new Color(0.8f, 0.8f, 0.8f, 1f);
    [SerializeField] private float colorDuration = 0.1f;

    private RectTransform rectTransform;
    private Image buttonImage;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        buttonImage = GetComponent<Image>();
    }

    // Saat ditekan tombolnya
    public void OnPointerDown(PointerEventData eventData)
    {
        rectTransform?.DOKill();
        buttonImage?.DOKill();
        rectTransform?.DOScale(pressScale, scaleDuration).SetEase(Ease.OutQuad).SetUpdate(true);
        buttonImage?.DOColor(pressColor, colorDuration).SetUpdate(true);
    }

    // Saat dilepas tombolnya
    public void OnPointerUp(PointerEventData eventData)
    {
        rectTransform?.DOKill();
        buttonImage?.DOKill();
        rectTransform?.DOScale(1f, scaleDuration).SetEase(Ease.OutBack).SetUpdate(true);
        buttonImage?.DOColor(normalColor, colorDuration).SetUpdate(true);
    }

    private void OnDisable()
    {
        rectTransform?.DOKill();
        buttonImage?.DOKill();
    }
}
