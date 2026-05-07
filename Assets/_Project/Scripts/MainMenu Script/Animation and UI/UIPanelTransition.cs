using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

[RequireComponent(typeof(CanvasGroup))]
public class UIPanelTransition : MonoBehaviour
{
    [Header("Transition Settings")]
    [SerializeField] private float duration = 0.3f;
    [SerializeField] private float slideOffset = 50f;
    [SerializeField] private Ease easeIn = Ease.OutCubic;
    [SerializeField] private Ease easeOut = Ease.InCubic;

    private CanvasGroup canvasGroup;
    private RectTransform rectTransform;
    private Vector2 originalPosition;

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();

        rectTransform = GetComponent<RectTransform>();
        if (rectTransform != null)
            originalPosition = rectTransform.anchoredPosition;
    }

    public void Show(int direction = 1)
    {
        gameObject.SetActive(true);

        if (rectTransform == null)
        {
            if (canvasGroup != null)
                canvasGroup.alpha = 1f;
            return;
        }

        // Reset posisi & alpha sebelum animasi
        rectTransform.anchoredPosition = originalPosition + Vector2.right * slideOffset * direction;
        canvasGroup.alpha = 0f;

        // Animasi fade + slide up
        rectTransform.DOKill();
        canvasGroup.DOKill();
        rectTransform.DOAnchorPos(originalPosition, duration).SetEase(easeIn);
        canvasGroup.DOFade(1f, duration).SetEase(easeIn);
    }

    public void Hide(int direction = 1, System.Action onComplete = null)
    {
        if (rectTransform == null)
        {
            gameObject.SetActive(false);
            onComplete?.Invoke();
            return;
        }

        // Animasi fade + slide down
        rectTransform.DOKill();
        canvasGroup.DOKill();
        rectTransform.DOAnchorPos(originalPosition - Vector2.right * slideOffset * direction, duration).SetEase(easeOut);

        canvasGroup.DOFade(0f, duration).SetEase(easeOut).OnComplete(() =>
            {
                gameObject.SetActive(false);
                onComplete?.Invoke();
            });
    }

    private void OnDisable()
    {
        rectTransform?.DOKill();
        canvasGroup?.DOKill();
    }
}
