using UnityEngine;

/// <summary>
/// Visual-only dynamic joystick driven by MobileTouchRouter (no touch handling).
/// </summary>
public class MobileMoveStickPresenter : MonoBehaviour
{
    private MobileTouchRouter router;
    private RectTransform joystickOuterRect;
    private RectTransform knobRect;
    private Canvas parentCanvas;
    private Camera eventCamera;
    private Vector2 joystickDefaultAnchoredPos;
    private float maxRadius;

    private const float ActiveSmoothingSpeed = 22f;
    private const float ReleaseSmoothingSpeed = 32f;

    private Vector2 smoothedOutput;

    public Vector2 SmoothedOutput => smoothedOutput;
    public bool IsActive => router != null && router.HasMoveFinger;

    public void Initialize(
        MobileTouchRouter touchRouter,
        RectTransform outerRect,
        RectTransform knob,
        Canvas canvas,
        Camera camera,
        float dragRadius,
        Vector2 defaultAnchoredPos)
    {
        router = touchRouter;
        joystickOuterRect = outerRect;
        knobRect = knob;
        parentCanvas = outerRect != null ? outerRect.GetComponentInParent<Canvas>() : canvas;
        if (parentCanvas == null)
            parentCanvas = canvas;
        eventCamera = camera;
        maxRadius = Mathf.Max(1f, dragRadius);
        joystickDefaultAnchoredPos = defaultAnchoredPos;
        smoothedOutput = Vector2.zero;

        ResetVisuals();
    }

    void Update()
    {
        if (router == null)
        {
            ResetVisuals();
            return;
        }

        Vector2 target = router.MoveVector;
        float smoothingSpeed = router.HasMoveFinger ? ActiveSmoothingSpeed : ReleaseSmoothingSpeed;
        float t = 1f - Mathf.Exp(-smoothingSpeed * Time.unscaledDeltaTime);
        smoothedOutput = Vector2.Lerp(smoothedOutput, target, t);

        if (!router.HasMoveFinger && smoothedOutput.sqrMagnitude < 0.0001f)
            smoothedOutput = Vector2.zero;

        if (router.HasMoveFinger)
        {
            if (knobRect != null)
                knobRect.anchoredPosition = router.MoveKnobOffset;
        }
        else
        {
            ResetVisuals();
        }
    }

    public void ForceReset()
    {
        smoothedOutput = Vector2.zero;
        ResetVisuals();
    }

    private void ResetVisuals()
    {
        if (knobRect != null)
            knobRect.anchoredPosition = Vector2.zero;

        if (joystickOuterRect != null)
            joystickOuterRect.anchoredPosition = joystickDefaultAnchoredPos;
    }
}
