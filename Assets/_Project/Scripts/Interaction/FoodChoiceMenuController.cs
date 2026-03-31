using System.Collections.Generic;
using UnityEngine;

public class FoodChoiceMenuController : MonoBehaviour
{
    public static FoodChoiceMenuController Instance { get; private set; }

    private readonly List<FoodData> currentFoods = new List<FoodData>();
    private string currentLocationName = "Paket Makanan";
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

    public void OpenMenu(string locationName, List<FoodData> foods)
    {
        currentLocationName = string.IsNullOrWhiteSpace(locationName)
            ? "Paket Makanan"
            : locationName;

        currentFoods.Clear();
        if (foods != null)
            currentFoods.AddRange(foods);

        isOpen = true;

        if (TimeManager.Instance != null)
            TimeManager.Instance.PauseTime();

        if (ModalStateManager.Instance != null)
            ModalStateManager.Instance.OpenModal("FoodMenu");

        Debug.Log($"[FoodMenu] Dibuka: {currentLocationName} ({currentFoods.Count} item)");
    }

    public void CloseMenu()
    {
        if (!isOpen)
            return;

        isOpen = false;

        if (TimeManager.Instance != null)
            TimeManager.Instance.ResumeTime();

        if (ModalStateManager.Instance != null)
            ModalStateManager.Instance.CloseModal("FoodMenu");

        Debug.Log("[FoodMenu] Ditutup.");
    }

    public bool IsOpen => isOpen;

    void OnGUI()
    {
        if (!isOpen)
            return;

        GUI.depth = -1000;
        windowRect.x = (Screen.width - windowRect.width) * 0.5f;
        windowRect.y = (Screen.height - windowRect.height) * 0.5f;

        Color previousColor = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.72f);
        GUI.Box(new Rect(0f, 0f, Screen.width, Screen.height), GUIContent.none);
        GUI.color = previousColor;

        windowRect = GUI.Window(GetInstanceID(), windowRect, DrawWindow, "Food Interaction");
    }

    private void DrawWindow(int id)
    {
        GUILayout.Space(8f);
        GUILayout.Label($"Lokasi: {currentLocationName}");
        GUILayout.Label($"Item tersedia: {currentFoods.Count}");
        GUILayout.Space(8f);

        scrollPosition = GUILayout.BeginScrollView(scrollPosition, GUILayout.Height(360f));

        if (currentFoods.Count == 0)
        {
            GUILayout.Label("Tidak ada item makanan.");
        }
        else
        {
            for (int i = 0; i < currentFoods.Count; i++)
            {
                FoodData food = currentFoods[i];
                if (food == null)
                    continue;

                DrawFoodRow(food);
            }
        }

        GUILayout.EndScrollView();
        GUILayout.Space(8f);

        GUILayout.BeginHorizontal();

        if (GUILayout.Button("Lihat Stash", GUILayout.Height(36f)))
        {
            CloseMenu();
            if (FoodStashMenuController.Instance != null)
                FoodStashMenuController.Instance.OpenStash();
        }

        if (GUILayout.Button("Tutup", GUILayout.Height(36f)))
            CloseMenu();

        GUILayout.EndHorizontal();

        GUI.DragWindow(new Rect(0f, 0f, 10000f, 24f));
    }

    private void DrawFoodRow(FoodData food)
    {
        GUILayout.BeginVertical("box");
        GUILayout.Label(food.foodName);
        GUILayout.Label($"Kategori: {food.category}");
        GUILayout.Label($"Kalori: {food.calories:0} | Energi: +{food.energyRestored:0} | Mood: +{food.moodEffect:0}");

        GUILayout.BeginHorizontal();

        if (GUILayout.Button("Makan", GUILayout.Height(30f)))
        {
            EatFood(food);
        }

        if (GUILayout.Button("Simpan", GUILayout.Height(30f)))
        {
            if (SessionFoodStash.Instance != null)
            {
                SessionFoodStash.Instance.AddToStash(food);
                Debug.Log($"[FoodMenu] Disimpan: {food.foodName}");
            }
            else
            {
                Debug.LogWarning("[FoodMenu] SessionFoodStash tidak ditemukan.");
            }
        }

        GUILayout.EndHorizontal();
        GUILayout.EndVertical();
        GUILayout.Space(6f);
    }

    private void EatFood(FoodData food)
    {
        if (food == null)
            return;

        if (PlayerStats.Instance != null)
            PlayerStats.Instance.AddFood(food.energyRestored, food.calories, food.moodEffect);

        if (StoryManager.Instance != null)
            StoryManager.Instance.OnFoodEaten(food);

        Debug.Log($"[FoodMenu] Dimakan: {food.foodName}");
        CloseMenu();
    }
}
