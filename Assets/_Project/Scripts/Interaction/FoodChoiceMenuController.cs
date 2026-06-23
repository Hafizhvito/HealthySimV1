using System.Collections.Generic;
using UnityEngine;

public class FoodChoiceMenuController : MonoBehaviour
{
    public static FoodChoiceMenuController Instance { get; private set; }

    private static class MenuDesign
    {
        public const float ReferenceWidth = 900f;
        public const float ReferenceHeight = 620f;
        public const float ScreenMargin = 16f;
        public const float MinTouchHeight = 48f;
        public const float FooterButtonHeight = 48f;
        public const float RowActionButtonHeight = 48f;
        public const float CloseButtonSize = 44f;
        public const float ScrollBottomPadding = 16f;
        public const float ThumbnailSize = 80f;
        public const float ThumbnailColumnWidth = 96f;
        public const float ActionColumnWidth = 172f;
    }

    private static class MenuPalette
    {
        public static readonly Color TitleText = new Color(0.98f, 0.97f, 0.94f, 1f);
        public static readonly Color MetaText = new Color(0.88f, 0.92f, 0.96f, 1f);
        public static readonly Color BodyText = new Color(0.84f, 0.88f, 0.92f, 1f);
        public static readonly Color NutrientText = new Color(0.86f, 0.96f, 0.80f, 1f);
        public static readonly Color PriceText = new Color(1f, 0.95f, 0.58f, 1f);
        public static readonly Color StatusOkText = new Color(0.70f, 1f, 0.70f, 1f);
        public static readonly Color StatusWarnText = new Color(1f, 0.77f, 0.50f, 1f);
        public static readonly Color PlaceholderText = new Color(0.62f, 0.65f, 0.68f, 1f);
        public static readonly Color ButtonText = Color.white;
        public static readonly Color TextOutline = new Color(0f, 0f, 0f, 0.88f);
        public static readonly Color ThumbnailBackground = new Color(0.14f, 0.15f, 0.18f, 0.85f);
        public static readonly Color ScreenDim = new Color(0f, 0f, 0f, 0.72f);
    }

    private struct MenuLayout
    {
        public float WindowWidth;
        public float WindowHeight;
        public float TouchButtonHeight;
        public float FooterButtonHeight;
        public float CloseButtonSize;
        public float ThumbnailSize;
        public float ThumbnailColumnWidth;
        public float ActionColumnWidth;
        public float ScrollBottomPadding;
        public float FontScale;
    }

    private readonly List<FoodData> currentFoods = new List<FoodData>();
    private string currentLocationName = "Paket Makanan";
    private bool isOpen;
    private Vector2 scrollPosition;
    private MenuLayout menuLayout;
    private float cachedStyleScale = -1f;
    private string panelStatusMessage = string.Empty;
    private float panelStatusUntil;
    private bool panelStatusIsWarning;
    private GUIStyle headerTitleStyle;
    private GUIStyle headerMetaStyle;
    private GUIStyle foodNameStyle;
    private GUIStyle foodDetailStyle;
    private GUIStyle nutrientStyle;
    private GUIStyle priceStyle;
    private GUIStyle actionButtonStyle;
    private GUIStyle closeButtonStyle;
    private GUIStyle statusOkStyle;
    private GUIStyle statusWarnStyle;
    private GUIStyle thumbnailPlaceholderStyle;

    private bool isHomeFoodMode;
    private float homePriceMultiplier = 1f;
    private bool homeQuickDrinkEnabled;
    private string homeQuickDrinkName = "Air Dingin";
    private float homeQuickDrinkEnergy = 6f;
    private float homeQuickDrinkCalories;
    private float homeQuickDrinkMood = 1.5f;
    private int homeQuickDrinkPrice = 4;

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

        if (isOpen)
        {
            scrollPosition = Vector2.zero;
            panelStatusMessage = string.Empty;
            panelStatusUntil = 0f;
            panelStatusIsWarning = false;
            Debug.Log($"[FoodMenu] Diperbarui: {currentLocationName} ({currentFoods.Count} item)");
            return;
        }

        ResetHomeMode();
        EnsureStashSystemsAvailable();

        isOpen = true;
        scrollPosition = Vector2.zero;
        cachedStyleScale = -1f;
        panelStatusMessage = string.Empty;
        panelStatusUntil = 0f;
        panelStatusIsWarning = false;

        if (TimeManager.Instance != null)
            TimeManager.Instance.PauseTime();

        if (ModalStateManager.Instance != null)
            ModalStateManager.Instance.OpenModal("FoodMenu");

        Debug.Log($"[FoodMenu] Dibuka: {currentLocationName} ({currentFoods.Count} item)");
    }

    public void OpenHomeMenu(
        string locationName,
        List<FoodData> foods,
        float priceMultiplier = 0.65f,
        bool enableQuickDrink = true,
        string quickDrinkName = "Air Dingin",
        float quickDrinkEnergy = 6f,
        float quickDrinkCalories = 0f,
        float quickDrinkMood = 1.5f,
        int quickDrinkPrice = 4)
    {
        OpenMenu(locationName, foods);

        isHomeFoodMode = true;
        homePriceMultiplier = Mathf.Clamp(priceMultiplier, 0.25f, 1f);
        homeQuickDrinkEnabled = enableQuickDrink;
        homeQuickDrinkName = string.IsNullOrWhiteSpace(quickDrinkName) ? "Air Dingin" : quickDrinkName.Trim();
        homeQuickDrinkEnergy = quickDrinkEnergy;
        homeQuickDrinkCalories = quickDrinkCalories;
        homeQuickDrinkMood = quickDrinkMood;
        homeQuickDrinkPrice = Mathf.Max(0, quickDrinkPrice);
    }

    public void CloseMenu()
    {
        if (!isOpen)
            return;

        isOpen = false;
        scrollPosition = Vector2.zero;

        if (TimeManager.Instance != null)
            TimeManager.Instance.ResumeTime();

        ReleaseGameplayAfterClose();
        ResetHomeMode();

        Debug.Log("[FoodMenu] Ditutup.");
    }

    void OnDisable()
    {
        if (isOpen)
            CloseMenu();
    }

    private void ReleaseGameplayAfterClose()
    {
        if (ModalStateManager.Instance != null)
            ModalStateManager.Instance.ForceCloseModal("FoodMenu");

        PlayerController player = FindFirstObjectByType<PlayerController>();
        if (player != null)
            player.ForceUnlockInput("FoodMenu");

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public bool IsOpen => isOpen;

    void OnGUI()
    {
        if (!isOpen)
            return;

        menuLayout = ComputeLayout();
        EnsureStyles();

        GUI.depth = -1000;

        Rect safeArea = Screen.safeArea;
        Rect windowRect = new Rect(
            safeArea.x + (safeArea.width - menuLayout.WindowWidth) * 0.5f,
            safeArea.y + (safeArea.height - menuLayout.WindowHeight) * 0.5f,
            menuLayout.WindowWidth,
            menuLayout.WindowHeight);

        Color previousColor = GUI.color;
        GUI.color = MenuPalette.ScreenDim;
        GUI.Box(new Rect(0f, 0f, Screen.width, Screen.height), GUIContent.none);
        GUI.color = previousColor;

        GUI.Window(GetInstanceID(), windowRect, DrawWindow, "Food Interaction");
    }

    private MenuLayout ComputeLayout()
    {
        Rect safeArea = Screen.safeArea;
        float margin = MenuDesign.ScreenMargin;
        float availableWidth = Mathf.Max(320f, safeArea.width - margin * 2f);
        float availableHeight = Mathf.Max(380f, safeArea.height - margin * 2f);

        float width = Mathf.Min(MenuDesign.ReferenceWidth, availableWidth);
        float height = Mathf.Min(MenuDesign.ReferenceHeight, availableHeight);
        float scale = width / MenuDesign.ReferenceWidth;

        return new MenuLayout
        {
            WindowWidth = width,
            WindowHeight = height,
            TouchButtonHeight = Mathf.Max(MenuDesign.MinTouchHeight, MenuDesign.RowActionButtonHeight * scale),
            FooterButtonHeight = Mathf.Max(MenuDesign.MinTouchHeight, MenuDesign.FooterButtonHeight * scale),
            CloseButtonSize = Mathf.Max(MenuDesign.MinTouchHeight, MenuDesign.CloseButtonSize * scale),
            ThumbnailSize = MenuDesign.ThumbnailSize * scale,
            ThumbnailColumnWidth = MenuDesign.ThumbnailColumnWidth * scale,
            ActionColumnWidth = MenuDesign.ActionColumnWidth * scale,
            ScrollBottomPadding = MenuDesign.ScrollBottomPadding * scale,
            FontScale = Mathf.Clamp(scale, 0.72f, 1.12f)
        };
    }

    private void DrawWindow(int id)
    {
        DrawCloseButton();

        GUILayout.Space(8f);
        int money = PlayerStats.Instance != null ? PlayerStats.Instance.Money : 0;
        LabelOutlined(currentLocationName, headerTitleStyle);
        LabelOutlined($"{currentFoods.Count} item tersedia  •  Saldo Rp{money}", headerMetaStyle);
        DrawPanelStatus();

        if (isHomeFoodMode)
            DrawHomeQuickActions();

        GUILayout.Space(8f);

        scrollPosition = BeginMenuScrollView(scrollPosition);

        if (currentFoods.Count == 0)
        {
            LabelOutlined("Tidak ada item makanan.", foodDetailStyle);
        }
        else
        {
            for (int i = 0; i < currentFoods.Count; i++)
            {
                FoodData food = currentFoods[i];
                if (food != null)
                    DrawFoodRow(food);
            }
        }

        GUILayout.Space(menuLayout.ScrollBottomPadding);
        GUILayout.EndScrollView();

        GUILayout.Space(8f);
        DrawFooterButtons();

        GUI.DragWindow(new Rect(0f, 0f, 10000f, 24f));
    }

    private void DrawCloseButton()
    {
        float size = menuLayout.CloseButtonSize;
        float padding = 8f;
        Rect closeRect = new Rect(menuLayout.WindowWidth - size - padding, padding, size, size);

        if (OutlinedButton("X", closeButtonStyle, closeRect))
            CloseMenu();
    }

    private void DrawFooterButtons()
    {
        GUILayout.BeginHorizontal();

        if (OutlinedButton("Lihat Stash", actionButtonStyle, GUILayout.Height(menuLayout.FooterButtonHeight)))
        {
            EnsureStashSystemsAvailable();
            CloseMenu();
            if (FoodStashMenuController.Instance != null)
                FoodStashMenuController.Instance.OpenStash();
        }

        if (OutlinedButton("Tutup", actionButtonStyle, GUILayout.Height(menuLayout.FooterButtonHeight)))
            CloseMenu();

        GUILayout.EndHorizontal();
    }

    private void DrawFoodRow(FoodData food)
    {
        int price = GetDisplayPrice(food);
        string eatLabel = isHomeFoodMode ? "Makan Cepat" : "Makan";
        string stashLabel = isHomeFoodMode ? "Meal Prep" : "Simpan";
        float actionButtonHeight = menuLayout.TouchButtonHeight;
        string healthTag = food.isHealthy ? "Sehat" : "Kurang Sehat";

        GUILayout.BeginVertical("box");
        GUILayout.BeginHorizontal();

        DrawFoodThumbnail(food);

        GUILayout.BeginVertical(GUILayout.ExpandWidth(true));
        LabelOutlined($"{food.foodName}  •  {healthTag}", foodNameStyle);
        LabelOutlined(
            $"{food.category}  •  Kalori {food.calories:0}  •  Energi +{food.energyRestored:0}  •  Mood +{food.moodEffect:0}",
            foodDetailStyle);
        LabelOutlined(
            $"Protein {food.Protein:0.#}g   Lemak {food.Fat:0.#}g   Karbohidrat {food.Carbohydrate:0.#}g",
            nutrientStyle);
        GUILayout.EndVertical();

        GUILayout.BeginVertical(GUILayout.Width(menuLayout.ActionColumnWidth));
        LabelOutlined($"Rp{price}", priceStyle, GUILayout.Height(actionButtonHeight * 0.55f));

        if (OutlinedButton(eatLabel, actionButtonStyle, GUILayout.Height(actionButtonHeight)))
            EatFood(food);

        if (OutlinedButton(stashLabel, actionButtonStyle, GUILayout.Height(actionButtonHeight)))
            StashFood(food);

        GUILayout.EndVertical();
        GUILayout.EndHorizontal();
        GUILayout.EndVertical();
        GUILayout.Space(6f);
    }

    private void DrawFoodThumbnail(FoodData food)
    {
        float thumbnailSize = menuLayout.ThumbnailSize;
        float columnWidth = menuLayout.ThumbnailColumnWidth;
        float rowHeight = thumbnailSize + 8f;

        Rect thumbRect = GUILayoutUtility.GetRect(columnWidth, rowHeight, GUILayout.Width(columnWidth));
        float inset = (columnWidth - thumbnailSize) * 0.5f;
        Rect imageRect = new Rect(thumbRect.x + inset, thumbRect.y + 4f, thumbnailSize, thumbnailSize);

        Color previousColor = GUI.color;
        GUI.color = MenuPalette.ThumbnailBackground;
        GUI.DrawTexture(imageRect, Texture2D.whiteTexture, ScaleMode.StretchToFill);
        GUI.color = previousColor;

        Texture iconTexture = ResolveFoodIconTexture(food);
        if (iconTexture != null)
            GUI.DrawTexture(imageRect, iconTexture, ScaleMode.ScaleToFit);
        else
            DrawOutlinedText(imageRect, "?", thumbnailPlaceholderStyle);
    }

    private void StashFood(FoodData food)
    {
        if (!TryPurchaseFood(food))
            return;

        EnsureStashSystemsAvailable();

        if (SessionFoodStash.Instance != null)
        {
            SessionFoodStash.Instance.AddToStash(food);
            Debug.Log($"[FoodMenu] Dibeli dan disimpan: {food.foodName} (Rp{GetDisplayPrice(food)})");
            string stashMessage = isHomeFoodMode
                ? $"Meal prep tersimpan: {food.foodName}."
                : $"{food.foodName} disimpan ke stash.";
            ShowPanelStatus(stashMessage, false);
            return;
        }

        Debug.LogWarning("[FoodMenu] SessionFoodStash tidak ditemukan.");
        ShowPanelStatus("Stash tidak tersedia.", true);
    }

    private Texture ResolveFoodIconTexture(FoodData food)
    {
        if (food == null || food.icon == null)
            return null;

        return food.icon.texture;
    }

    private void EatFood(FoodData food)
    {
        if (food == null)
            return;

        if (!TryPurchaseFood(food))
            return;

        if (PlayerStats.Instance != null)
            PlayerStats.Instance.AddFood(food.energyRestored, food.calories, food.moodEffect, food.protein, food.fat, countsAsMeal: true);

        if (PlayerActionTracker.Instance != null)
        {
            PlayerActionTracker.Instance.Track(
                food.isHealthy
                    ? PlayerActionTracker.ActionType.HealthyFoodTaken
                    : PlayerActionTracker.ActionType.UnhealthyFoodTaken,
                $"FoodMenu:{food.foodName}");
        }

        if (StoryManager.Instance != null)
            StoryManager.Instance.OnFoodEaten(food);

        Debug.Log($"[FoodMenu] Dibeli dan dimakan: {food.foodName} (Rp{GetDisplayPrice(food)})");
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

        int price = GetDisplayPrice(food);
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

    private void DrawHomeQuickActions()
    {
        float buttonHeight = menuLayout.TouchButtonHeight;

        GUILayout.BeginVertical("box");
        LabelOutlined("Aksi Cepat Rumah", foodNameStyle);
        LabelOutlined("Rumah lebih hemat. Meal prep otomatis masuk stash dan bisa dimakan nanti tanpa bayar lagi.", foodDetailStyle);

        if (OutlinedButton("Buka Stash Rumah", actionButtonStyle, GUILayout.Height(buttonHeight)))
        {
            EnsureStashSystemsAvailable();
            CloseMenu();
            if (FoodStashMenuController.Instance != null)
                FoodStashMenuController.Instance.OpenStash();
            return;
        }

        if (homeQuickDrinkEnabled)
        {
            int drinkPrice = Mathf.Max(0, homeQuickDrinkPrice);
            LabelOutlined(
                $"Minuman cepat: {homeQuickDrinkName}  •  Energi +{homeQuickDrinkEnergy:0.#}  •  Mood +{homeQuickDrinkMood:0.#}  •  Rp{drinkPrice}",
                nutrientStyle);

            if (OutlinedButton($"Minum Cepat (Rp{drinkPrice})", actionButtonStyle, GUILayout.Height(buttonHeight)))
                ConsumeHomeQuickDrink();
        }

        GUILayout.EndVertical();
    }

    private void ConsumeHomeQuickDrink()
    {
        if (PlayerStats.Instance == null)
        {
            ShowPanelStatus("Data player belum siap.", true);
            return;
        }

        int price = Mathf.Max(0, homeQuickDrinkPrice);
        if (price > 0 && PlayerStats.Instance.Money < price)
        {
            ShowPanelStatus($"Uang tidak cukup. Butuh Rp{price}.", true);
            return;
        }

        if (price > 0)
            PlayerStats.Instance.SpendMoney(price);

        PlayerStats.Instance.AddFood(homeQuickDrinkEnergy, homeQuickDrinkCalories, homeQuickDrinkMood, 0f, 0f, countsAsMeal: true);

        if (PlayerActionTracker.Instance != null)
            PlayerActionTracker.Instance.Track(PlayerActionTracker.ActionType.HealthyFoodTaken, "HomeQuickDrink");

        ShowPanelStatus($"Kamu minum {homeQuickDrinkName}.", false, 1.2f);
    }

    private int GetDisplayPrice(FoodData food)
    {
        if (food == null)
            return 0;

        int basePrice = Mathf.Max(0, food.GetEffectivePrice());
        if (!isHomeFoodMode)
            return basePrice;

        return Mathf.Max(1, Mathf.RoundToInt(basePrice * homePriceMultiplier));
    }

    private void EnsureStashSystemsAvailable()
    {
        GameObject manager = GameObject.Find("GameManager");

        if (SessionFoodStash.Instance == null)
        {
            GameObject stashObj = new GameObject("SessionFoodStash");
            if (manager != null)
                stashObj.transform.SetParent(manager.transform, false);

            stashObj.AddComponent<SessionFoodStash>();
        }

        if (FoodStashMenuController.Instance == null)
        {
            GameObject stashMenuObj = new GameObject("FoodStashMenuController");
            if (manager != null)
                stashMenuObj.transform.SetParent(manager.transform, false);

            stashMenuObj.AddComponent<FoodStashMenuController>();
        }
    }

    private void ResetHomeMode()
    {
        isHomeFoodMode = false;
        homePriceMultiplier = 1f;
        homeQuickDrinkEnabled = false;
        homeQuickDrinkName = "Air Dingin";
        homeQuickDrinkEnergy = 6f;
        homeQuickDrinkCalories = 0f;
        homeQuickDrinkMood = 1.5f;
        homeQuickDrinkPrice = 4;
    }

    private void DrawPanelStatus()
    {
        if (string.IsNullOrWhiteSpace(panelStatusMessage) || Time.unscaledTime > panelStatusUntil)
            return;

        LabelOutlined(panelStatusMessage, panelStatusIsWarning ? statusWarnStyle : statusOkStyle);
    }

    private void ShowPanelStatus(string message, bool isWarning, float duration = 2f)
    {
        panelStatusMessage = message;
        panelStatusIsWarning = isWarning;
        panelStatusUntil = Time.unscaledTime + Mathf.Max(0.5f, duration);
    }

    private int ScaledFont(int baseSize)
    {
        return Mathf.Max(11, Mathf.RoundToInt(baseSize * menuLayout.FontScale));
    }

    private void EnsureStyles()
    {
        float scale = menuLayout.FontScale;
        if (headerTitleStyle != null && Mathf.Approximately(cachedStyleScale, scale))
            return;

        cachedStyleScale = scale;

        headerTitleStyle = CreateLabelStyle(ScaledFont(22), FontStyle.Bold, MenuPalette.TitleText);
        headerTitleStyle.margin = new RectOffset(10, 10, 2, 2);

        headerMetaStyle = CreateLabelStyle(ScaledFont(13), FontStyle.Normal, MenuPalette.MetaText);
        headerMetaStyle.margin = new RectOffset(10, 10, 2, 8);

        foodNameStyle = CreateLabelStyle(ScaledFont(16), FontStyle.Bold, MenuPalette.TitleText);
        foodNameStyle.wordWrap = true;

        foodDetailStyle = CreateLabelStyle(ScaledFont(13), FontStyle.Normal, MenuPalette.BodyText);
        foodDetailStyle.wordWrap = true;

        nutrientStyle = CreateLabelStyle(ScaledFont(13), FontStyle.Bold, MenuPalette.NutrientText);
        nutrientStyle.wordWrap = true;
        nutrientStyle.margin = new RectOffset(0, 0, 4, 0);

        priceStyle = CreateLabelStyle(ScaledFont(15), FontStyle.Bold, MenuPalette.PriceText);
        priceStyle.alignment = TextAnchor.MiddleCenter;

        actionButtonStyle = CreateActionButtonStyle(ScaledFont(13));
        closeButtonStyle = CreateActionButtonStyle(ScaledFont(12));

        statusOkStyle = CreateLabelStyle(ScaledFont(12), FontStyle.Bold, MenuPalette.StatusOkText);
        statusOkStyle.margin = new RectOffset(10, 10, 2, 4);

        statusWarnStyle = CreateLabelStyle(ScaledFont(12), FontStyle.Bold, MenuPalette.StatusWarnText);
        statusWarnStyle.margin = new RectOffset(10, 10, 2, 4);

        thumbnailPlaceholderStyle = CreateLabelStyle(ScaledFont(28), FontStyle.Bold, MenuPalette.PlaceholderText);
        thumbnailPlaceholderStyle.alignment = TextAnchor.MiddleCenter;
    }

    private static GUIStyle CreateActionButtonStyle(int fontSize)
    {
        return new GUIStyle(GUI.skin.button)
        {
            fontSize = fontSize,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            padding = new RectOffset(10, 10, 8, 8)
        };
    }

    private void LabelOutlined(string text, GUIStyle style, params GUILayoutOption[] options)
    {
        GUIContent content = new GUIContent(text);
        Rect rect = GUILayoutUtility.GetRect(content, style, options);
        DrawOutlinedText(rect, text, style);
    }

    private void DrawOutlinedButtonText(Rect rect, string text, int fontSize)
    {
        GUIStyle labelStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = fontSize,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            wordWrap = false
        };
        ApplyTextColorAllStates(labelStyle, MenuPalette.ButtonText);
        DrawOutlinedText(rect, text, labelStyle);
    }

    private bool OutlinedButton(string text, GUIStyle buttonStyle, params GUILayoutOption[] options)
    {
        bool clicked = GUILayout.Button(GUIContent.none, buttonStyle, options);
        Rect rect = GUILayoutUtility.GetLastRect();
        DrawOutlinedButtonText(rect, text, buttonStyle.fontSize);
        return clicked;
    }

    private bool OutlinedButton(string text, GUIStyle buttonStyle, Rect rect)
    {
        bool clicked = GUI.Button(rect, GUIContent.none, buttonStyle);
        DrawOutlinedButtonText(rect, text, buttonStyle.fontSize);
        return clicked;
    }

    private static void DrawOutlinedText(Rect rect, string text, GUIStyle style)
    {
        if (string.IsNullOrEmpty(text))
            return;

        GUIStyle outlineStyle = GetOutlineStyle(style);
        const float offset = 1f;
        GUI.Label(new Rect(rect.x - offset, rect.y, rect.width, rect.height), text, outlineStyle);
        GUI.Label(new Rect(rect.x + offset, rect.y, rect.width, rect.height), text, outlineStyle);
        GUI.Label(new Rect(rect.x, rect.y - offset, rect.width, rect.height), text, outlineStyle);
        GUI.Label(new Rect(rect.x, rect.y + offset, rect.width, rect.height), text, outlineStyle);
        GUI.Label(rect, text, style);
    }

    private static GUIStyle GetOutlineStyle(GUIStyle source)
    {
        GUIStyle outline = new GUIStyle(source)
        {
            alignment = source.alignment,
            fontSize = source.fontSize,
            fontStyle = source.fontStyle,
            wordWrap = source.wordWrap
        };
        ApplyTextColorAllStates(outline, MenuPalette.TextOutline);
        return outline;
    }

    private GUIStyle CreateLabelStyle(int fontSize, FontStyle fontStyle, Color textColor)
    {
        GUIStyle style = new GUIStyle(GUI.skin.label)
        {
            fontSize = fontSize,
            fontStyle = fontStyle,
            wordWrap = false
        };
        ApplyTextColorAllStates(style, textColor);
        return style;
    }

    private static Vector2 BeginMenuScrollView(Vector2 position)
    {
        // GameSkin lacks scrollview* styles; hide scrollbars to avoid console spam.
        // Touch drag and mouse wheel still scroll the list.
        return GUILayout.BeginScrollView(
            position,
            false,
            false,
            GUIStyle.none,
            GUIStyle.none,
            GUIStyle.none,
            GUILayout.ExpandHeight(true));
    }

    private static void ApplyTextColorAllStates(GUIStyle style, Color textColor)
    {
        style.normal.textColor = textColor;
        style.hover.textColor = textColor;
        style.active.textColor = textColor;
        style.focused.textColor = textColor;
        style.onNormal.textColor = textColor;
        style.onHover.textColor = textColor;
        style.onActive.textColor = textColor;
        style.onFocused.textColor = textColor;
    }
}
