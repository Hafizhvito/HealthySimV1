using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

public class FoodListItemView : MonoBehaviour
{
    [SerializeField] private Image healthIndicator;
    [SerializeField] private TextMeshProUGUI foodNameText;
    [SerializeField] private TextMeshProUGUI categoryText;
    [SerializeField] private TextMeshProUGUI caloriesText;
    [SerializeField] private TextMeshProUGUI energyText;
    [SerializeField] private TextMeshProUGUI moodText;
    [SerializeField] private Button selectButton;

    private FoodData data;
    private Action<FoodData> onSelected;

    private Image bgImage;
    private readonly Color defaultColor = new Color(0.12f, 0.12f, 0.18f, 1f);
    private readonly Color selectedColor = new Color(0.25f, 0.28f, 0.35f, 1f);

    private readonly Color colorHealthy = new Color(0.23f, 0.78f, 0.31f);
    private readonly Color colorUnhealthy = new Color(0.78f, 0.23f, 0.23f);

    void Awake()
    {
        bgImage = GetComponent<Image>();
        if (healthIndicator == null)
            healthIndicator = transform.Find("HealthIndicator")?.GetComponent<Image>();
        if (foodNameText == null)
            foodNameText = transform.Find("FoodName_Text")?.GetComponent<TextMeshProUGUI>();
        if (categoryText == null)
            categoryText = transform.Find("Category_Text")?.GetComponent<TextMeshProUGUI>();
        if (caloriesText == null)
            caloriesText = transform.Find("StatsRow/Calories_Text")?.GetComponent<TextMeshProUGUI>();
        if (energyText == null)
            energyText = transform.Find("StatsRow/Energy_Text")?.GetComponent<TextMeshProUGUI>();
        if (moodText == null)
            moodText = transform.Find("StatsRow/Mood_Text")?.GetComponent<TextMeshProUGUI>();
        if (selectButton == null)
            selectButton = transform.Find("SelectButton")?.GetComponent<Button>();
    }

    public void Setup(FoodData food, Action<FoodData> callback)
    {
        data = food;
        onSelected = callback;

        if (food == null)
            return;

        if (healthIndicator != null)
            healthIndicator.color = food.isHealthy ? colorHealthy : colorUnhealthy;

        if (foodNameText != null)
            foodNameText.text = food.foodName;

        if (categoryText != null)
            categoryText.text = food.category.ToString();

        if (caloriesText != null)
            caloriesText.text = $"{food.calories:0} kcal";

        if (energyText != null)
            energyText.text = $"+{food.energyRestored:0} ⚡";

        if (moodText != null)
            moodText.text = $"+{food.moodEffect:0} 😊";

        if (selectButton != null)
        {
            selectButton.onClick.RemoveAllListeners();
            selectButton.onClick.AddListener(() => onSelected?.Invoke(data));
        }

        SetSelected(false);
    }

    public void SetSelected(bool isSelected)
    {
        if (bgImage != null)
        {
            bgImage.color = isSelected ? selectedColor : defaultColor;
        }
    }
}
