using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Reliable tap/click handler for pause menu buttons (desktop + Android).
/// Works with timeScale = 0 and avoids duplicate Button.onClick wiring issues.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Button))]
public class PauseMenuButtonRelay : MonoBehaviour, IPointerClickHandler
{
    private UnityEngine.Events.UnityAction handler;
    private Button button;
    private float lastInvokeUnscaled;

    public void Configure(UnityEngine.Events.UnityAction action)
    {
        handler = action;
        button = GetComponent<Button>();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!isActiveAndEnabled || handler == null)
            return;

        if (button == null)
            button = GetComponent<Button>();

        if (button != null && !button.interactable)
            return;

        float now = Time.unscaledTime;
        if (now - lastInvokeUnscaled < 0.05f)
            return;

        lastInvokeUnscaled = now;
        handler.Invoke();
    }
}
