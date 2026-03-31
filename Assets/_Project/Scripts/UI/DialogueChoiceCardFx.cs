using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class DialogueChoiceCardFx : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    [SerializeField] private Image targetImage;
    [SerializeField] private Color normalColor = new Color(20f / 255f, 28f / 255f, 40f / 255f, 0.72f);
    [SerializeField] private Color hoverColor = new Color(40f / 255f, 55f / 255f, 75f / 255f, 0.85f);
    [SerializeField] private float pressScale = 0.97f;
    [SerializeField] private float pressDuration = 0.08f;

    private Coroutine pressRoutine;
    private Vector3 baseScale = Vector3.one;

    void Awake()
    {
        baseScale = transform.localScale;
    }

    public void Configure(Image imageRef, Color normal, Color hover, float pressScaleValue, float pressDurationValue)
    {
        targetImage = imageRef;
        normalColor = normal;
        hoverColor = hover;
        pressScale = Mathf.Clamp(pressScaleValue, 0.7f, 1f);
        pressDuration = Mathf.Max(0.02f, pressDurationValue);

        if (targetImage != null)
            targetImage.color = normalColor;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (targetImage != null)
            targetImage.color = hoverColor;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (targetImage != null)
            targetImage.color = normalColor;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (pressRoutine != null)
            StopCoroutine(pressRoutine);

        pressRoutine = StartCoroutine(PressPulseRoutine());
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (targetImage != null)
            targetImage.color = hoverColor;
    }

    private IEnumerator PressPulseRoutine()
    {
        float elapsed = 0f;
        Vector3 downScale = baseScale * pressScale;

        while (elapsed < pressDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / pressDuration);
            transform.localScale = Vector3.Lerp(baseScale, downScale, t);
            yield return null;
        }

        elapsed = 0f;
        while (elapsed < pressDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / pressDuration);
            transform.localScale = Vector3.Lerp(downScale, baseScale, t);
            yield return null;
        }

        transform.localScale = baseScale;
        pressRoutine = null;
    }
}
