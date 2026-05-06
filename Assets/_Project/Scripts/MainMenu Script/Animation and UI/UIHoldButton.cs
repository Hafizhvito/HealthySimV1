using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.UI;
using DG.Tweening;

public class UIHoldButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    public UnityEvent onDown;
    public UnityEvent onUp;

    [Header("Touch Feedback")]
    [SerializeField] private float pressScale = 0.92f;
    [SerializeField] private float scaleDuration = 0.1f;
    [SerializeField] private Color pressColor = new Color(0.8f, 0.8f, 0.8f, 1f);
    [SerializeField] private Color normalColor = new Color(1f, 1f, 1f, 1f);

    private Image buttonImage;

    private void Awake()
    {
        buttonImage = GetComponent<Image>();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        onDown?.Invoke();
        transform.DOScale(pressScale, scaleDuration).SetEase(Ease.OutQuad);
        buttonImage?.DOColor(pressColor, scaleDuration);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        onUp?.Invoke();
        transform.DOScale(1f, scaleDuration).SetEase(Ease.OutBack);
        buttonImage?.DOColor(normalColor, scaleDuration);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        onUp?.Invoke();
        transform.DOScale(1f, scaleDuration).SetEase(Ease.OutBack);
        buttonImage?.DOColor(normalColor, scaleDuration);
    }
}
