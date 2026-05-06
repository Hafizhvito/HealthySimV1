using UnityEngine;
using System.Collections;

[RequireComponent(typeof(RectTransform))]
public class UITransitionPop : MonoBehaviour
{
    private RectTransform rectTransform;
    [SerializeField] private float duration = 0.25f;
    [SerializeField] private bool doScale = true;
    [SerializeField] private Vector3 startScale = new Vector3(0.85f, 0.85f, 1f);
    [SerializeField] private bool doFade = false;

    private Coroutine popCoroutine;
    private CanvasGroup canvasGroup;

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        canvasGroup = GetComponent<CanvasGroup>();
        
        // Add a CanvasGroup if we want to fade as well, but gracefully handle if none exists
        if (doFade && canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();

        if (!doFade && canvasGroup != null)
            canvasGroup.alpha = 1f;
    }

    void OnEnable()
    {
        if (rectTransform == null) rectTransform = GetComponent<RectTransform>();
        if (!doFade && canvasGroup != null) canvasGroup.alpha = 1f;
        if (popCoroutine != null) StopCoroutine(popCoroutine);
        popCoroutine = StartCoroutine(DoPop());
    }

    private IEnumerator DoPop()
    {
        float elapsed = 0f;
        
        // Initial state
        if (doScale) rectTransform.localScale = startScale;
        if (doFade && canvasGroup != null) canvasGroup.alpha = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime; // Unscaled to bypass TimeManager pause
            float t = Mathf.Clamp01(elapsed / duration);
            
            // Custom snappy easeOutQuart curve
            float easeOut = 1f - Mathf.Pow(1f - t, 3f); 
            
            if (doScale)
                rectTransform.localScale = Vector3.LerpUnclamped(startScale, Vector3.one, easeOut);
            
            if (doFade && canvasGroup != null)
                canvasGroup.alpha = Mathf.Lerp(0f, 1f, easeOut * 1.5f); // Fade in faster than scale

            yield return null;
        }

        if (doScale) rectTransform.localScale = Vector3.one;
        if (doFade && canvasGroup != null) canvasGroup.alpha = 1f;
    }
}
