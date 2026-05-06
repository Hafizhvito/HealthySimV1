using UnityEngine;
using UnityEngine.EventSystems;

public class UIButtonSFX : MonoBehaviour, IPointerDownHandler
{
    [SerializeField] private AudioClip touchSFX;

    public void OnPointerDown(PointerEventData eventData)
    {
        AudioManager._Instance.PlaySFX(touchSFX);
    }
}
