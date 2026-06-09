using UnityEngine;
using UnityEngine.EventSystems;

public class UIButtonSFX : MonoBehaviour, IPointerDownHandler
{
    [SerializeField] private AudioClip touchSFX;

    public void OnPointerDown(PointerEventData eventData)
    {
        if (AudioManager._Instance == null || touchSFX == null)
            return;

        AudioManager._Instance.PlaySFX(touchSFX);
    }
}
