using System.Collections.Generic;
using UnityEngine;

public class FoodStashMenuController : MonoBehaviour
{
    public static FoodStashMenuController Instance { get; private set; }

    private bool isOpen;
    private Vector2 scrollPosition;
    private Rect windowRect = new Rect(0f, 0f, 760f, 520f);

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

        if (TimeManager.Instance != null)
            TimeManager.Instance.ResumeTime();

        if (ModalStateManager.Instance != null)
            ModalStateManager.Instance.CloseModal("Stash");

        Debug.Log("[StashMenu] Ditutup.");
    }

    public void Close() => CloseStash();

    void OnGUI()
    {
        if (!isOpen)
            return;

        GUI.depth = -999;
        windowRect.x = (Screen.width - windowRect.width) * 0.5f;
        windowRect.y = (Screen.height - windowRect.height) * 0.5f;

        Color previousColor = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.72f);
        GUI.Box(new Rect(0f, 0f, Screen.width, Screen.height), GUIContent.none);
        GUI.color = previousColor;

        windowRect = GUI.Window(GetInstanceID() + 77, windowRect, DrawWindow, "Food Stash");
    }

    private void DrawWindow(int id)
    {
        IReadOnlyList<FoodData> foods = SessionFoodStash.Instance != null
            ? SessionFoodStash.Instance.StashedFoods
            : null;

        int count = foods != null ? foods.Count : 0;

        GUILayout.Space(8f);
        GUILayout.Label($"Item tersimpan: {count}");
        GUILayout.Space(8f);

        scrollPosition = GUILayout.BeginScrollView(
            scrollPosition,
            false,
            false,
            GUIStyle.none,
            GUIStyle.none,
            GUIStyle.none,
            GUILayout.Height(360f));

        if (foods == null || foods.Count == 0)
        {
            GUILayout.Label("Stash kosong.");
        }
        else
        {
            for (int i = 0; i < foods.Count; i++)
            {
                FoodData food = foods[i];
                if (food == null)
                    continue;

                DrawStashRow(food, i);
            }
        }

        GUILayout.EndScrollView();
        GUILayout.Space(8f);

        GUILayout.BeginHorizontal();

        if (GUILayout.Button("Clear All", GUILayout.Height(36f)) && SessionFoodStash.Instance != null)
            SessionFoodStash.Instance.ClearStash();

        if (GUILayout.Button("Tutup", GUILayout.Height(36f)))
            CloseStash();

        GUILayout.EndHorizontal();

        GUI.DragWindow(new Rect(0f, 0f, 10000f, 24f));
    }

    private void DrawStashRow(FoodData food, int index)
    {
        GUILayout.BeginVertical("box");
        GUILayout.Label(food.foodName);
        GUILayout.Label($"Kalori: {food.calories:0} | Energi: +{food.energyRestored:0}");

        GUILayout.BeginHorizontal();

        if (GUILayout.Button("Konsumsi", GUILayout.Height(30f)) && SessionFoodStash.Instance != null)
            SessionFoodStash.Instance.ConsumeFromStash(index);

        if (GUILayout.Button("Hapus", GUILayout.Height(30f)) && SessionFoodStash.Instance != null)
            SessionFoodStash.Instance.RemoveFromStash(index);

        GUILayout.EndHorizontal();
        GUILayout.EndVertical();
        GUILayout.Space(6f);
    }
}
