using System.Collections.Generic;
using UnityEngine;

public class FoodStashMenuController : MonoBehaviour
{
    public static FoodStashMenuController Instance { get; private set; }

    private bool isOpen;
    private Vector2 scrollPosition;
    private ImGuiMenuLayout.Metrics menuMetrics;
    private Rect stashWindowScreenRect;
    private Rect stashScrollScreenRect;
    private float stashScrollContentHeight;
    private bool hasStashScrollScreenRect;
    private float cachedStyleScale = -1f;

    private GUIStyle headerStyle;
    private GUIStyle bodyStyle;
    private GUIStyle buttonStyle;
    private GUIStyle scrollHintStyle;

    private float ComputeScrollHeight(bool includesScrollHint)
    {
        return ImGuiMenuLayout.ComputeScrollHeight(
            menuMetrics,
            menuMetrics.StashHeaderReservedHeight,
            includesScrollHint);
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    public void OpenStash()
    {
        isOpen = true;
        scrollPosition = Vector2.zero;
        cachedStyleScale = -1f;

        if (TimeManager.Instance != null)
            TimeManager.Instance.PauseTime();

        if (ModalStateManager.Instance != null)
            ModalStateManager.Instance.OpenModal("Stash");

        Debug.Log("[StashMenu] Dibuka.");
    }

    public void Open() => OpenStash();

    public void CloseStash()
    {
        if (!isOpen)
            return;

        isOpen = false;
        scrollPosition = Vector2.zero;
        stashScrollScreenRect = Rect.zero;
        stashScrollContentHeight = 0f;
        hasStashScrollScreenRect = false;
        ImGuiMobileScrollUtility.Reset();

        if (TimeManager.Instance != null)
            TimeManager.Instance.ResumeTime();

        if (ModalStateManager.Instance != null)
            ModalStateManager.Instance.CloseModal("Stash");

        Debug.Log("[StashMenu] Ditutup.");
    }

    public void Close() => CloseStash();

    void LateUpdate()
    {
        if (!isOpen || !hasStashScrollScreenRect)
            return;

        scrollPosition = ImGuiMobileScrollUtility.PollTouchScroll(
            stashScrollScreenRect,
            scrollPosition,
            stashScrollContentHeight);

        ImGuiMobileScrollUtility.EndFrameCleanup();
    }

    void OnGUI()
    {
        if (!isOpen)
            return;

        GUI.depth = -999;
        menuMetrics = ImGuiMenuLayout.Compute();
        ImGuiMobileScrollUtility.EnsureScrollbarStyles();
        EnsureStyles();

        Rect windowRect = ImGuiMenuLayout.CenteredWindowRect(menuMetrics);

        Color previousColor = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.72f);
        GUI.Box(new Rect(0f, 0f, Screen.width, Screen.height), GUIContent.none);
        GUI.color = previousColor;

        stashWindowScreenRect = windowRect;
        GUI.Window(GetInstanceID() + 77, windowRect, DrawWindow, GUIContent.none);
    }

    private void EnsureStyles()
    {
        float scale = menuMetrics.FontScale;
        if (headerStyle != null && Mathf.Approximately(cachedStyleScale, scale))
            return;

        cachedStyleScale = scale;

        headerStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = ImGuiMenuLayout.ScaledFont(menuMetrics, 28, 34),
            fontStyle = FontStyle.Bold,
            wordWrap = false
        };
        headerStyle.normal.textColor = Color.white;

        bodyStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = ImGuiMenuLayout.ScaledFont(menuMetrics, 18, 23),
            wordWrap = true
        };
        bodyStyle.normal.textColor = new Color(0.88f, 0.92f, 0.96f, 1f);

        buttonStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = ImGuiMenuLayout.ScaledFont(menuMetrics, 17, 21),
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            padding = new RectOffset(12, 12, 10, 10)
        };

        scrollHintStyle = new GUIStyle(bodyStyle)
        {
            fontSize = ImGuiMenuLayout.ScaledFont(menuMetrics, 16, 20),
            fontStyle = FontStyle.Italic,
            alignment = TextAnchor.MiddleCenter
        };
    }

    private void DrawWindow(int id)
    {
        IReadOnlyList<FoodData> foods = SessionFoodStash.Instance != null
            ? SessionFoodStash.Instance.StashedFoods
            : null;

        int count = foods != null ? foods.Count : 0;

        GUILayout.Space(8f);
        GUILayout.Label("Stash Makanan", headerStyle);
        GUILayout.Label($"Item tersimpan: {count}", bodyStyle);
        GUILayout.Space(8f);

        stashScrollContentHeight = count == 0
            ? menuMetrics.EmptyListHeight
            : count * menuMetrics.StashRowHeight
                + Mathf.Max(0, count - 1) * ImGuiMenuLayout.FoodRowSpacingFor(menuMetrics);
        bool needsScroll = count > 0 && stashScrollContentHeight > 120f;
        float scrollHeight = ComputeScrollHeight(needsScroll);

        if (needsScroll && stashScrollContentHeight > scrollHeight + 8f)
            GUILayout.Label("Geser daftar ke atas/bawah untuk scroll", scrollHintStyle);

        if (count == 0)
        {
            GUILayout.Label("Stash kosong.", bodyStyle);
            hasStashScrollScreenRect = false;
            stashScrollScreenRect = Rect.zero;
        }
        else
        {
            if (hasStashScrollScreenRect)
            {
                ImGuiMobileScrollUtility.TryHandleGuiScrollEvent(
                    stashScrollScreenRect,
                    ref scrollPosition,
                    stashScrollContentHeight);
            }

            scrollPosition = ImGuiMobileScrollUtility.BeginWideScrollView(
                scrollPosition,
                scrollHeight,
                out Rect scrollViewportLocal);

            for (int i = 0; i < foods.Count; i++)
            {
                FoodData food = foods[i];
                if (food == null)
                    continue;

                DrawStashRow(food, i);
            }

            ImGuiMobileScrollUtility.EndWideScrollView();

            if (Event.current.type == EventType.Repaint && scrollViewportLocal.height > 1f)
            {
                stashScrollScreenRect = ImGuiMobileScrollUtility.BuildContentSwipeRect(
                    stashWindowScreenRect,
                    scrollViewportLocal);
                hasStashScrollScreenRect = true;
            }
        }

        GUILayout.FlexibleSpace();
        GUILayout.Space(ImGuiMenuLayout.FooterTopSpacing);

        GUILayout.BeginHorizontal();

        if (GUILayout.Button("Clear All", buttonStyle, GUILayout.Height(menuMetrics.FooterButtonHeight))
            && SessionFoodStash.Instance != null
            && !ImGuiMobileScrollUtility.ShouldSuppressClicks)
        {
            SessionFoodStash.Instance.ClearStash();
            scrollPosition = Vector2.zero;
        }

        if (GUILayout.Button("Tutup", buttonStyle, GUILayout.Height(menuMetrics.FooterButtonHeight)))
            CloseStash();

        GUILayout.EndHorizontal();

        GUILayout.Space(ImGuiMenuLayout.WindowBottomPadding);
        GUI.DragWindow(new Rect(0f, 0f, menuMetrics.WindowWidth, menuMetrics.CloseButtonSize + 12f));
    }

    private void DrawStashRow(FoodData food, int index)
    {
        GUILayout.BeginVertical("box");
        GUILayout.Label(food.foodName, headerStyle);
        GUILayout.Label($"Kalori: {food.calories:0} | Energi: +{food.energyRestored:0}", bodyStyle);

        GUILayout.BeginHorizontal();

        if (GUILayout.Button("Konsumsi", buttonStyle, GUILayout.Height(menuMetrics.TouchButtonHeight))
            && SessionFoodStash.Instance != null
            && !ImGuiMobileScrollUtility.ShouldSuppressClicks)
            SessionFoodStash.Instance.ConsumeFromStash(index);

        if (GUILayout.Button("Hapus", buttonStyle, GUILayout.Height(menuMetrics.TouchButtonHeight))
            && SessionFoodStash.Instance != null
            && !ImGuiMobileScrollUtility.ShouldSuppressClicks)
            SessionFoodStash.Instance.RemoveFromStash(index);

        GUILayout.EndHorizontal();
        GUILayout.EndVertical();
        GUILayout.Space(ImGuiMenuLayout.FoodRowSpacingFor(menuMetrics));
    }
}
