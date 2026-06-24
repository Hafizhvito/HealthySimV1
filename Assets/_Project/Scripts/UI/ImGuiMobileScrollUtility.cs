using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
#endif

/// <summary>
/// Touch swipe scrolling for IMGUI menus on Android/iOS.
/// Poll from LateUpdate and optionally handle MouseDrag in OnGUI.
/// </summary>
public static class ImGuiMobileScrollUtility
{
    private const float DragThresholdPixels = 10f;
    private const float WideScrollbarWidth = 48f;

    private static bool isDragging;
    private static bool pendingDrag;
    private static bool suppressClicks;
    private static int activeFingerId = -1;
    private static float dragStartScreenY;
    private static float dragStartScreenX;
    private static float scrollStartY;

    private static Texture2D scrollbarTrackTexture;
    private static Texture2D scrollbarThumbTexture;
    private static GUIStyle wideVerticalScrollbar;
    private static GUIStyle wideVerticalScrollbarThumb;
    private static GUIStyle scrollViewBackground;

    public static bool ShouldSuppressClicks => isDragging || suppressClicks;

    public static void EndFrameCleanup()
    {
        if (!isDragging && !pendingDrag)
            suppressClicks = false;
    }

    public static void Reset()
    {
        isDragging = false;
        pendingDrag = false;
        suppressClicks = false;
        activeFingerId = -1;
    }

    public static void EnsureScrollbarStyles()
    {
        if (GUI.skin == null)
            return;

        if (wideVerticalScrollbar == null)
        {
            scrollbarTrackTexture = CreateSolidTexture(new Color(0.22f, 0.24f, 0.28f, 0.95f));
            scrollbarThumbTexture = CreateSolidTexture(new Color(0.72f, 0.76f, 0.82f, 1f));

            wideVerticalScrollbar = new GUIStyle(GUI.skin.verticalScrollbar)
            {
                fixedWidth = WideScrollbarWidth
            };
            wideVerticalScrollbar.normal.background = scrollbarTrackTexture;
        }

        if (wideVerticalScrollbarThumb == null)
        {
            wideVerticalScrollbarThumb = new GUIStyle(GUI.skin.verticalScrollbarThumb)
            {
                fixedWidth = WideScrollbarWidth - 6f,
                margin = new RectOffset(3, 3, 4, 4)
            };
            wideVerticalScrollbarThumb.normal.background = scrollbarThumbTexture;
        }

        if (scrollViewBackground == null)
        {
            scrollViewBackground = new GUIStyle(GUI.skin.scrollView);
        }
    }

    public static GUIStyle WideVerticalScrollbar
    {
        get
        {
            EnsureScrollbarStyles();
            return wideVerticalScrollbar ?? GUI.skin.verticalScrollbar;
        }
    }

    public static GUIStyle WideVerticalScrollbarThumb
    {
        get
        {
            EnsureScrollbarStyles();
            return wideVerticalScrollbarThumb ?? GUI.skin.verticalScrollbarThumb;
        }
    }

    public static GUIStyle ScrollViewBackground
    {
        get
        {
            EnsureScrollbarStyles();
            return scrollViewBackground ?? GUI.skin.scrollView;
        }
    }

    public static Vector2 PollTouchScroll(Rect screenRect, Vector2 scrollPosition, float contentHeight)
    {
        if (screenRect.width <= 1f || screenRect.height <= 1f)
            return scrollPosition;

        float maxScroll = Mathf.Max(0f, contentHeight - screenRect.height);
        if (maxScroll <= 0.5f)
            return scrollPosition;

#if ENABLE_INPUT_SYSTEM
        if (Touchscreen.current != null)
            return PollInputSystemTouchscreen(screenRect, scrollPosition, maxScroll);
#endif

        return PollLegacyTouches(screenRect, scrollPosition, maxScroll);
    }

    public static void TryHandleGuiScrollEvent(Rect screenRect, ref Vector2 scrollPosition, float contentHeight)
    {
        if (Event.current == null)
            return;

        float maxScroll = Mathf.Max(0f, contentHeight - screenRect.height);
        if (maxScroll <= 0.5f || screenRect.height <= 1f)
            return;

        Event e = Event.current;
        Vector2 guiPoint = new Vector2(e.mousePosition.x, Screen.height - e.mousePosition.y);

        if (e.type == EventType.MouseDrag && (isDragging || screenRect.Contains(guiPoint)))
        {
            if (Mathf.Abs(e.delta.y) > 0.01f)
            {
                isDragging = true;
                suppressClicks = true;
                scrollPosition.y = Mathf.Clamp(scrollPosition.y - e.delta.y, 0f, maxScroll);
                e.Use();
            }
        }
        else if (e.type == EventType.MouseUp)
        {
            if (isDragging || pendingDrag)
                e.Use();
            isDragging = false;
            pendingDrag = false;
            activeFingerId = -1;
        }
    }

    public static Rect LocalRectToScreenRect(Rect windowOuterScreenRect, Rect localRect)
    {
        GUIStyle windowStyle = GUI.skin != null ? GUI.skin.window : null;
        float padLeft = windowStyle != null ? windowStyle.padding.left + windowStyle.border.left : 8f;
        float padTop = windowStyle != null ? windowStyle.padding.top + windowStyle.border.top : 8f;

        return new Rect(
            windowOuterScreenRect.x + padLeft + localRect.x,
            windowOuterScreenRect.y + padTop + localRect.y,
            localRect.width,
            localRect.height);
    }

    /// <summary>
    /// Swipe hit area for list content (viewport minus scrollbar strip).
    /// </summary>
    public static Rect BuildContentSwipeRect(Rect windowOuterScreenRect, Rect viewportLocalRect)
    {
        Rect screen = LocalRectToScreenRect(windowOuterScreenRect, viewportLocalRect);
        screen.width = Mathf.Max(64f, screen.width - WideScrollbarWidth);
        return screen;
    }

#if ENABLE_INPUT_SYSTEM
    private static Vector2 PollInputSystemTouchscreen(Rect screenRect, Vector2 scrollPosition, float maxScroll)
    {
        Touchscreen touchscreen = Touchscreen.current;
        if (touchscreen == null)
            return scrollPosition;

        var touches = touchscreen.touches;

        if (activeFingerId >= 0)
        {
            for (int i = 0; i < touches.Count; i++)
            {
                TouchControl touch = touches[i];
                if (touch.touchId.ReadValue() != activeFingerId)
                    continue;

                UnityEngine.InputSystem.TouchPhase phase = touch.phase.ReadValue();
                if (phase == UnityEngine.InputSystem.TouchPhase.None)
                    continue;

                Vector2 screenPoint = TouchToTopLeftScreen(touch.position.ReadValue());
                return ProcessTouchPhase(
                    (UnityEngine.TouchPhase)phase,
                    activeFingerId,
                    screenRect,
                    screenPoint,
                    scrollPosition,
                    maxScroll);
            }
        }

        for (int i = 0; i < touches.Count; i++)
        {
            TouchControl touch = touches[i];
            UnityEngine.InputSystem.TouchPhase phase = touch.phase.ReadValue();
            if (phase == UnityEngine.InputSystem.TouchPhase.None)
                continue;

            int fingerId = touch.touchId.ReadValue();
            Vector2 screenPoint = TouchToTopLeftScreen(touch.position.ReadValue());

            if (phase == UnityEngine.InputSystem.TouchPhase.Began && !screenRect.Contains(screenPoint))
                continue;

            return ProcessTouchPhase(
                (UnityEngine.TouchPhase)phase,
                fingerId,
                screenRect,
                screenPoint,
                scrollPosition,
                maxScroll);
        }

        ClearDragIfNoActiveTouch();
        return scrollPosition;
    }
#endif

    private static Vector2 PollLegacyTouches(Rect screenRect, Vector2 scrollPosition, float maxScroll)
    {
        if (Input.touchCount > 0)
        {
            if (activeFingerId >= 0)
            {
                for (int i = 0; i < Input.touchCount; i++)
                {
                    UnityEngine.Touch touch = Input.GetTouch(i);
                    if (touch.fingerId != activeFingerId)
                        continue;

                    Vector2 screenPoint = TouchToTopLeftScreen(touch.position);
                    return ProcessTouchPhase(
                        touch.phase,
                        touch.fingerId,
                        screenRect,
                        screenPoint,
                        scrollPosition,
                        maxScroll);
                }
            }

            for (int i = 0; i < Input.touchCount; i++)
            {
                UnityEngine.Touch touch = Input.GetTouch(i);
                Vector2 screenPoint = TouchToTopLeftScreen(touch.position);
                Vector2 result = ProcessTouchPhase(
                    touch.phase,
                    touch.fingerId,
                    screenRect,
                    screenPoint,
                    scrollPosition,
                    maxScroll);

                if (isDragging || pendingDrag)
                    return result;
            }

            ClearDragIfNoActiveTouch();
            return scrollPosition;
        }

        if (isDragging && Input.GetMouseButton(0))
        {
            Vector2 screenPoint = TouchToTopLeftScreen(Input.mousePosition);
            return ProcessDrag(screenRect, screenPoint, scrollPosition, maxScroll);
        }

        if (isDragging || pendingDrag)
            Reset();

        return scrollPosition;
    }

    private static Vector2 ProcessTouchPhase(
        UnityEngine.TouchPhase phase,
        int fingerId,
        Rect screenRect,
        Vector2 screenPoint,
        Vector2 scrollPosition,
        float maxScroll)
    {
        switch (phase)
        {
            case UnityEngine.TouchPhase.Began:
                if (screenRect.Contains(screenPoint))
                {
                    pendingDrag = true;
                    isDragging = false;
                    suppressClicks = false;
                    activeFingerId = fingerId;
                    dragStartScreenY = screenPoint.y;
                    dragStartScreenX = screenPoint.x;
                    scrollStartY = scrollPosition.y;
                }
                return scrollPosition;

            case UnityEngine.TouchPhase.Moved:
                if (activeFingerId != fingerId)
                    return scrollPosition;

                if (pendingDrag && !isDragging)
                {
                    float deltaY = Mathf.Abs(screenPoint.y - dragStartScreenY);
                    float deltaX = Mathf.Abs(screenPoint.x - dragStartScreenX);
                    if (deltaY >= DragThresholdPixels && deltaY > deltaX)
                    {
                        isDragging = true;
                        pendingDrag = false;
                        suppressClicks = true;
                    }
                }

                if (!isDragging)
                    return scrollPosition;

                return ProcessDrag(screenRect, screenPoint, scrollPosition, maxScroll);

            case UnityEngine.TouchPhase.Ended:
            case UnityEngine.TouchPhase.Canceled:
                if (activeFingerId != fingerId)
                    return scrollPosition;

                bool wasScroll = isDragging;
                isDragging = false;
                pendingDrag = false;
                activeFingerId = -1;
                suppressClicks = wasScroll;
                return scrollPosition;

            default:
                return scrollPosition;
        }
    }

    private static Vector2 ProcessDrag(Rect screenRect, Vector2 screenPoint, Vector2 scrollPosition, float maxScroll)
    {
        suppressClicks = true;
        float deltaY = dragStartScreenY - screenPoint.y;
        scrollPosition.y = Mathf.Clamp(scrollStartY + deltaY, 0f, maxScroll);
        return scrollPosition;
    }

    private static void ClearDragIfNoActiveTouch()
    {
        if (!isDragging && !pendingDrag)
            return;

        bool anyActive = false;
#if ENABLE_INPUT_SYSTEM
        if (Touchscreen.current != null)
        {
            var touches = Touchscreen.current.touches;
            for (int i = 0; i < touches.Count; i++)
            {
                UnityEngine.InputSystem.TouchPhase phase = touches[i].phase.ReadValue();
                if (phase == UnityEngine.InputSystem.TouchPhase.Began
                    || phase == UnityEngine.InputSystem.TouchPhase.Moved
                    || phase == UnityEngine.InputSystem.TouchPhase.Stationary)
                {
                    anyActive = true;
                    break;
                }
            }
        }
#endif
        if (!anyActive && Input.touchCount > 0)
            anyActive = true;

        if (!anyActive)
            Reset();
    }

    private static Vector2 TouchToTopLeftScreen(Vector2 touchPosition)
    {
        return new Vector2(touchPosition.x, Screen.height - touchPosition.y);
    }

    public static Vector2 BeginWideScrollView(Vector2 position, float height)
    {
        return BeginWideScrollView(position, height, out _);
    }

    public static Vector2 BeginWideScrollView(Vector2 position, float height, out Rect viewportLocalRect)
    {
        EnsureScrollbarStyles();

        GUIStyle previousThumb = GUI.skin.verticalScrollbarThumb;
        GUI.skin.verticalScrollbarThumb = WideVerticalScrollbarThumb;

        Vector2 result = GUILayout.BeginScrollView(
            position,
            false,
            true,
            GUIStyle.none,
            WideVerticalScrollbar,
            ScrollViewBackground,
            GUILayout.Height(height));

        viewportLocalRect = Event.current.type == EventType.Repaint
            ? GUILayoutUtility.GetLastRect()
            : default;

        if (Event.current.type == EventType.Repaint)
            GUI.skin.verticalScrollbarThumb = WideVerticalScrollbarThumb;

        return result;
    }

    public static void EndWideScrollView()
    {
        GUILayout.EndScrollView();
    }

    private static Texture2D CreateSolidTexture(Color color)
    {
        Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        texture.hideFlags = HideFlags.HideAndDontSave;
        texture.SetPixel(0, 0, color);
        texture.Apply();
        return texture;
    }
}
