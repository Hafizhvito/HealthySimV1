using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
#endif

/// <summary>
/// Single authority for gameplay touch routing (Genshin/Roblox-style).
/// </summary>
public class MobileTouchRouter
{
    public enum FingerRole
    {
        None = 0,
        Move = 1,
        Look = 2,
        UiBlocked = 3
    }

    private struct FingerBinding
    {
        public int touchId;
        public FingerRole role;
        public Vector2 startPosition;
        public Vector2 lastPosition;
        public Vector2 currentPosition;
        public Vector2 frameDelta;
    }

    private const float MicroJitterThreshold = 0.0125f;
    private const float ResponseExponent = 1.35f;
    private const int EditorMovePointerId = 10000;
    private const int EditorLookPointerId = 10001;

    private readonly Dictionary<int, FingerBinding> bindings = new Dictionary<int, FingerBinding>(8);
    private readonly List<int> removeBuffer = new List<int>(8);
    private readonly HashSet<int> activeThisFrame = new HashSet<int>();

    private float moveZoneSplit;
    private float joystickRadius;
    private bool enableEditorMouse;

    private int moveFingerId = -1;
    private int lookFingerId = -1;

    public bool HasMoveFinger => moveFingerId >= 0;
    public bool HasLookFinger => lookFingerId >= 0;
    public int MoveFingerId => moveFingerId;
    public int LookFingerId => lookFingerId;
    public Vector2 MoveVector { get; private set; }
    public Vector2 LookDelta { get; private set; }
    public Vector2 MoveOriginScreen { get; private set; }
    public Vector2 MoveKnobOffset { get; private set; }
    public int ActiveTouchCount { get; private set; }
    public float LookFrameDeltaPixels { get; private set; }

    public void Configure(float zoneSplit, float radius, bool editorMouseFallback)
    {
        moveZoneSplit = Mathf.Clamp01(zoneSplit);
        joystickRadius = Mathf.Max(1f, radius);
        enableEditorMouse = editorMouseFallback;
    }

    public void Reset()
    {
        bindings.Clear();
        moveFingerId = -1;
        lookFingerId = -1;
        MoveVector = Vector2.zero;
        LookDelta = Vector2.zero;
        LookFrameDeltaPixels = 0f;
        MoveKnobOffset = Vector2.zero;
        ActiveTouchCount = 0;
    }

    public void Tick()
    {
        MoveVector = Vector2.zero;
        LookDelta = Vector2.zero;
        LookFrameDeltaPixels = 0f;
        MoveKnobOffset = Vector2.zero;
        ActiveTouchCount = 0;
        activeThisFrame.Clear();

#if ENABLE_INPUT_SYSTEM
        PollTouchscreen();
        if (enableEditorMouse)
            PollEditorMouse();
#else
        PollLegacyTouches();
#endif

        ActiveTouchCount = activeThisFrame.Count;

        removeBuffer.Clear();
        foreach (KeyValuePair<int, FingerBinding> pair in bindings)
        {
            if (!activeThisFrame.Contains(pair.Key))
                removeBuffer.Add(pair.Key);
        }

        for (int i = 0; i < removeBuffer.Count; i++)
            RemoveBinding(removeBuffer[i]);

        MergeEnhancedLookDeltasWhenMultitouch();
        ApplyMoveOutput();
        ApplyLookOutput();
    }

#if ENABLE_INPUT_SYSTEM
    private void PollTouchscreen()
    {
        Touchscreen touchscreen = Touchscreen.current;
        if (touchscreen == null)
            return;

        var touches = touchscreen.touches;
        for (int i = 0; i < touches.Count; i++)
        {
            TouchControl touch = touches[i];
            UnityEngine.InputSystem.TouchPhase phase = touch.phase.ReadValue();
            if (phase == UnityEngine.InputSystem.TouchPhase.None)
                continue;

            int touchId = touch.touchId.ReadValue();
            Vector2 position = touch.position.ReadValue();
            Vector2 delta = touch.delta.ReadValue();
            bool ended = phase == UnityEngine.InputSystem.TouchPhase.Ended
                || phase == UnityEngine.InputSystem.TouchPhase.Canceled;

            if (ended)
            {
                if (bindings.ContainsKey(touchId))
                    RemoveBinding(touchId);
                continue;
            }

            ProcessContact(touchId, position, delta, phase == UnityEngine.InputSystem.TouchPhase.Began);
        }
    }

    private void PollEditorMouse()
    {
        if (Application.isMobilePlatform)
            return;

        Mouse mouse = Mouse.current;
        if (mouse == null)
            return;

        Vector2 position = mouse.position.ReadValue();
        float deltaScale = 1f / Mathf.Max(1f, Screen.width);

        if (mouse.leftButton.isPressed)
        {
            Vector2 delta = mouse.delta.ReadValue() * deltaScale;
            ProcessContact(EditorMovePointerId, position, delta, mouse.leftButton.wasPressedThisFrame);
        }
        else if (bindings.ContainsKey(EditorMovePointerId))
        {
            RemoveBinding(EditorMovePointerId);
        }

        // Editor look: use LookSwipeZone UI drag when testing mobile UI in editor.
    }
#else
    private void PollLegacyTouches()
    {
        Touch[] touches = Input.touches;
        for (int i = 0; i < touches.Length; i++)
        {
            Touch touch = touches[i];
            if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
            {
                if (bindings.ContainsKey(touch.fingerId))
                    RemoveBinding(touch.fingerId);
                continue;
            }

            ProcessContact(
                touch.fingerId,
                touch.position,
                touch.deltaPosition,
                touch.phase == TouchPhase.Began);
        }
    }
#endif

    private void ProcessContact(int touchId, Vector2 position, Vector2 delta, bool isBegan)
    {
        activeThisFrame.Add(touchId);

        if (bindings.TryGetValue(touchId, out FingerBinding existing))
        {
            Vector2 positionDelta = position - existing.currentPosition;
            existing.lastPosition = existing.currentPosition;
            existing.currentPosition = position;

            existing.frameDelta = ComputeFrameDelta(existing.role, positionDelta, delta);

            bindings[touchId] = existing;
            return;
        }

        FingerRole role = AssignRoleForNewFinger(touchId, position);
        if (role == FingerRole.None || role == FingerRole.UiBlocked)
            return;

        FingerBinding created = new FingerBinding
        {
            touchId = touchId,
            role = role,
            startPosition = position,
            lastPosition = position,
            currentPosition = position,
            frameDelta = Vector2.zero
        };
        bindings[touchId] = created;

        if (role == FingerRole.Move)
            moveFingerId = touchId;
        else if (role == FingerRole.Look)
            lookFingerId = touchId;
    }

    private FingerRole AssignRoleForNewFinger(int touchId, Vector2 position)
    {
        if (IsBlockedByUi(touchId))
            return FingerRole.UiBlocked;

        float splitX = Screen.width * moveZoneSplit;
        bool isLeftHalf = position.x < splitX;

        if (isLeftHalf)
        {
            if (moveFingerId >= 0)
                return FingerRole.UiBlocked;
            return FingerRole.Move;
        }

        // Right half: MobileSwipeLookZone owns look via EventSystem; router is move-only.
        return FingerRole.None;
    }

    private static bool IsBlockedByUi(int pointerId)
    {
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null)
            return false;

        return eventSystem.IsPointerOverGameObject(pointerId);
    }

    private void RemoveBinding(int touchId)
    {
        if (!bindings.Remove(touchId))
            return;

        if (moveFingerId == touchId)
            moveFingerId = -1;
        if (lookFingerId == touchId)
            lookFingerId = -1;
    }

    private Vector2 ComputeFrameDelta(FingerRole role, Vector2 positionDelta, Vector2 hardwareDelta)
    {
        bool multitouchActive = moveFingerId >= 0 && lookFingerId >= 0;

        if (role == FingerRole.Look)
        {
            if (!multitouchActive)
            {
                if (hardwareDelta.sqrMagnitude > 0.0001f)
                    return hardwareDelta;
                return positionDelta;
            }

            Vector2 best = positionDelta;
            if (hardwareDelta.sqrMagnitude > best.sqrMagnitude)
                best = hardwareDelta;
            return best;
        }

        if (multitouchActive && hardwareDelta.sqrMagnitude > 0.0001f)
            return hardwareDelta;

        if (positionDelta.sqrMagnitude > 0.0001f)
            return positionDelta;

        return hardwareDelta;
    }

    private void ApplyMoveOutput()
    {
        if (moveFingerId < 0 || !bindings.TryGetValue(moveFingerId, out FingerBinding move))
            return;

        MoveOriginScreen = move.startPosition;
        Vector2 offset = move.currentPosition - move.startPosition;
        MoveKnobOffset = Vector2.ClampMagnitude(offset, joystickRadius);

        Vector2 normalized = MoveKnobOffset / joystickRadius;
        float magnitude = Mathf.Clamp01(normalized.magnitude);
        if (magnitude <= MicroJitterThreshold)
        {
            MoveVector = Vector2.zero;
            return;
        }

        float curved = Mathf.Pow(magnitude, ResponseExponent);
        MoveVector = normalized.normalized * curved;
    }

    private void MergeEnhancedLookDeltasWhenMultitouch()
    {
        if (moveFingerId < 0 || lookFingerId < 0)
            return;

#if ENABLE_INPUT_SYSTEM
        if (!UnityEngine.InputSystem.EnhancedTouch.EnhancedTouchSupport.enabled)
            return;

        var enhTouches = UnityEngine.InputSystem.EnhancedTouch.Touch.activeTouches;
        for (int i = 0; i < enhTouches.Count; i++)
        {
            var enhTouch = enhTouches[i];
            if (enhTouch.touchId != lookFingerId)
                continue;

            if (!bindings.TryGetValue(lookFingerId, out FingerBinding look))
                return;

            Vector2 enhDelta = enhTouch.delta;
            if (enhDelta.sqrMagnitude > look.frameDelta.sqrMagnitude)
            {
                look.frameDelta = enhDelta;
                bindings[lookFingerId] = look;
            }

            return;
        }
#endif
    }

    private void ApplyLookOutput()
    {
        if (lookFingerId < 0 || !bindings.TryGetValue(lookFingerId, out FingerBinding look))
            return;

        Vector2 delta = look.frameDelta;
        if (delta.sqrMagnitude <= 0.0001f)
            delta = look.currentPosition - look.lastPosition;

        if (delta.sqrMagnitude <= 0.0001f)
            return;

        LookFrameDeltaPixels = delta.magnitude;

        float screenW = Mathf.Max(1f, Screen.width);
        float screenH = Mathf.Max(1f, Screen.height);
        bool multitouchActive = moveFingerId >= 0 && lookFingerId >= 0;
        float dualTouchBoost = multitouchActive ? 1.25f : 1f;
        LookDelta = new Vector2(delta.x / screenW, -(delta.y / screenH)) * dualTouchBoost;
    }

    public void AppendDebugReport(StringBuilder report)
    {
        report.Append("touchCount=");
        report.Append(ActiveTouchCount);
        report.Append(" moveId=");
        report.Append(moveFingerId);
        report.Append(" lookId=swipeZone");
        report.Append("\nmoveVec=");
        report.Append(MoveVector.ToString("F2"));
        report.Append(" look=");
        report.Append(LookDelta.ToString("F3"));
        report.Append(" lookPx=");
        report.Append(LookFrameDeltaPixels.ToString("F1"));

        if (bindings.Count == 0)
        {
            report.Append("\nfingers: (none)");
            return;
        }

        report.Append("\nfingers:");
        foreach (KeyValuePair<int, FingerBinding> pair in bindings)
        {
            report.Append("\n  #");
            report.Append(pair.Key);
            report.Append(' ');
            report.Append(pair.Value.role);
            report.Append(" x=");
            report.Append(Mathf.RoundToInt(pair.Value.currentPosition.x));
        }
    }
}
