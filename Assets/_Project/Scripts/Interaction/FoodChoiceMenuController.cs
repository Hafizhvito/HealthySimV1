using System.Collections.Generic;
using UnityEngine;

public class FoodChoiceMenuController : MonoBehaviour
{
    public static FoodChoiceMenuController Instance { get; private set; }

    private readonly List<FoodData> currentFoods = new List<FoodData>();
    private string currentLocationName = "Paket Makanan";
    private bool isOpen;
    private Vector2 scrollPosition;
    private Rect windowRect = new Rect(0f, 0f, 840f, 560f);
    private string panelStatusMessage = string.Empty;
    private float panelStatusUntil;
    private bool panelStatusIsWarning;
    private GUIStyle headerTitleStyle;
    private GUIStyle headerMetaStyle;
    private GUIStyle cardStyle;
    private GUIStyle foodNameStyle;
    private GUIStyle foodDetailStyle;
    private GUIStyle nutrientStyle;
    private GUIStyle priceStyle;
    private GUIStyle sectionButtonStyle;
    private GUIStyle closeButtonStyle;
    private GUIStyle statusOkStyle;
    private GUIStyle statusWarnStyle;

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
        panelStatusMessage = string.Empty;
        panelStatusUntil = 0f;
        panelStatusIsWarning = false;

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
        EnsureStyles();

        GUILayout.Space(8f);
        int money = PlayerStats.Instance != null ? PlayerStats.Instance.Money : 0;
        GUILayout.Label(currentLocationName, headerTitleStyle);
        GUILayout.Label($"{currentFoods.Count} item tersedia  •  Saldo Rp{money}", headerMetaStyle);
        DrawPanelStatus();
        GUILayout.Space(8f);

        scrollPosition = GUILayout.BeginScrollView(scrollPosition, GUILayout.Height(370f));

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
        GUILayout.Space(10f);

        GUILayout.BeginHorizontal();

        if (GUILayout.Button("Lihat Stash", sectionButtonStyle, GUILayout.Height(36f)))
        {
            CloseMenu();
            if (FoodStashMenuController.Instance != null)
                FoodStashMenuController.Instance.OpenStash();
        }

        if (GUILayout.Button("Tutup", closeButtonStyle, GUILayout.Height(36f)))
            CloseMenu();

        GUILayout.EndHorizontal();

        GUI.DragWindow(new Rect(0f, 0f, 10000f, 24f));
    }

    private void DrawFoodRow(FoodData food)
    {
        int price = food.GetEffectivePrice();
        GUILayout.BeginVertical(cardStyle);
        GUILayout.BeginHorizontal();

        GUILayout.BeginVertical(GUILayout.ExpandWidth(true));
        GUILayout.Label(food.foodName, foodNameStyle);
        GUILayout.Label($"{food.category}  •  Kalori {food.calories:0}  •  Energi +{food.energyRestored:0}  •  Mood +{food.moodEffect:0}", foodDetailStyle);
        GUILayout.Label($"Protein {food.Protein:0.#}g   Lemak {food.Fat:0.#}g   Gula {food.Sugar:0.#}g", nutrientStyle);
        GUILayout.EndVertical();

        GUILayout.BeginVertical(GUILayout.Width(170f));
        GUILayout.Label($"Rp{price}", priceStyle, GUILayout.Height(30f));

        if (GUILayout.Button("Makan", sectionButtonStyle, GUILayout.Height(30f)))
        {
            EatFood(food);
        }

        if (GUILayout.Button("Simpan", closeButtonStyle, GUILayout.Height(30f)))
        {
            if (!TryPurchaseFood(food))
                return;

            if (SessionFoodStash.Instance != null)
            {
                SessionFoodStash.Instance.AddToStash(food);
                Debug.Log($"[FoodMenu] Dibeli dan disimpan: {food.foodName} (Rp{food.GetEffectivePrice()})");
                ShowPanelStatus($"{food.foodName} disimpan ke stash.", false);
            }
            else
            {
                Debug.LogWarning("[FoodMenu] SessionFoodStash tidak ditemukan.");
                ShowPanelStatus("Stash tidak tersedia.", true);
            }
        }

        GUILayout.EndVertical();
        GUILayout.EndHorizontal();
        GUILayout.EndVertical();
        GUILayout.Space(6f);
    }

    private void EatFood(FoodData food)
    {
        if (food == null)
            return;

        if (!TryPurchaseFood(food))
            return;

        if (PlayerStats.Instance != null)
            PlayerStats.Instance.AddFood(food.energyRestored, food.calories, food.moodEffect);

        if (StoryManager.Instance != null)
            StoryManager.Instance.OnFoodEaten(food);

        Debug.Log($"[FoodMenu] Dibeli dan dimakan: {food.foodName} (Rp{food.GetEffectivePrice()})");
        ShowPanelStatus($"Kamu makan {food.foodName}.", false, 1.2f);
        CloseMenu();
    }

    private bool TryPurchaseFood(FoodData food)
    {
        if (food == null)
            return false;

        if (PlayerStats.Instance == null)
        {
            Debug.LogWarning("[FoodMenu] PlayerStats tidak ditemukan. Pembelian dibatalkan.");
            ShowPanelStatus("Data player belum siap.", true);
            return false;
        }

        int price = food.GetEffectivePrice();
        if (price <= 0)
            return true;

        if (PlayerStats.Instance.Money < price)
        {
            Debug.LogWarning($"[FoodMenu] Uang tidak cukup untuk membeli {food.foodName}. Butuh Rp{price}, uang sekarang Rp{PlayerStats.Instance.Money}.");
            ShowPanelStatus($"Uang tidak cukup. Butuh Rp{price}.", true);
            return false;
        }

        PlayerStats.Instance.SpendMoney(price);
        return true;
    }

    private void DrawPanelStatus()
    {
        if (string.IsNullOrWhiteSpace(panelStatusMessage) || Time.unscaledTime > panelStatusUntil)
            return;

        GUILayout.Label(panelStatusMessage, panelStatusIsWarning ? statusWarnStyle : statusOkStyle);
    }

    private void ShowPanelStatus(string message, bool isWarning, float duration = 2f)
    {
        panelStatusMessage = message;
        panelStatusIsWarning = isWarning;
        panelStatusUntil = Time.unscaledTime + Mathf.Max(0.5f, duration);
    }

    private void EnsureStyles()
    {
        if (headerTitleStyle != null)
            return;

        headerTitleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 22,
            fontStyle = FontStyle.Bold,
            normal = { textColor = new Color(0.96f, 0.95f, 0.9f, 1f) },
            margin = new RectOffset(10, 10, 2, 2)
        };

        headerMetaStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 13,
            normal = { textColor = new Color(0.78f, 0.86f, 0.92f, 1f) },
            margin = new RectOffset(10, 10, 2, 8)
        };

        cardStyle = new GUIStyle(GUI.skin.box)
        {
            padding = new RectOffset(12, 12, 10, 10),
            margin = new RectOffset(8, 8, 4, 6)
        };

        foodNameStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 16,
            fontStyle = FontStyle.Bold,
            normal = { textColor = new Color(0.96f, 0.93f, 0.86f, 1f) },
            wordWrap = true
        };

        foodDetailStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 12,
            normal = { textColor = new Color(0.78f, 0.85f, 0.91f, 1f) },
            wordWrap = true
        };

        nutrientStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 13,
            fontStyle = FontStyle.Bold,
            normal = { textColor = new Color(0.86f, 0.96f, 0.8f, 1f) },
            wordWrap = true,
            margin = new RectOffset(0, 0, 4, 0)
        };

        priceStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 15,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = new Color(1f, 0.95f, 0.58f, 1f) }
        };

        sectionButtonStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 13,
            fontStyle = FontStyle.Bold,
            normal = { textColor = new Color(0.12f, 0.17f, 0.2f, 1f) }
        };

        closeButtonStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 13,
            fontStyle = FontStyle.Bold,
            normal = { textColor = new Color(0.18f, 0.12f, 0.12f, 1f) }
        };

        statusOkStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 12,
            fontStyle = FontStyle.Bold,
            normal = { textColor = new Color(0.7f, 1f, 0.7f, 1f) },
            margin = new RectOffset(10, 10, 2, 4)
        };

        statusWarnStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 12,
            fontStyle = FontStyle.Bold,
            normal = { textColor = new Color(1f, 0.77f, 0.5f, 1f) },
            margin = new RectOffset(10, 10, 2, 4)
        };
    }
}
