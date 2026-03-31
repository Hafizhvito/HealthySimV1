using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

public class StashItemRowView : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI foodNameText;
    [SerializeField] private TextMeshProUGUI statsText;
    [SerializeField] private Button consumeButton;
    [SerializeField] private Button removeButton;

    private int itemIndex;
    private Action<int> onConsume;
    private Action<int> onRemove;

    void Awake()
    {
        if (foodNameText == null)
            foodNameText = transform.Find("FoodName_Text")?.GetComponent<TextMeshProUGUI>();
        if (statsText == null)
            statsText = transform.Find("Stats_Text")?.GetComponent<TextMeshProUGUI>();
        if (consumeButton == null)
            consumeButton = transform.Find("ConsumeButton")?.GetComponent<Button>();
        if (removeButton == null)
            removeButton = transform.Find("RemoveButton")?.GetComponent<Button>();
    }

    public void Setup(FoodData food, int index,
        Action<int> consumeCallback, Action<int> removeCallback)
    {
        itemIndex = index;
        onConsume = consumeCallback;
        onRemove = removeCallback;

        if (foodNameText != null)
            foodNameText.text = food.foodName;

        if (statsText != null)
            statsText.text =
                $"{food.calories:0} kcal | +{food.energyRestored:0} ⚡";

        if (consumeButton != null)
        {
            consumeButton.onClick.RemoveAllListeners();
            consumeButton.onClick.AddListener(
                () => onConsume?.Invoke(itemIndex));
        }

        if (removeButton != null)
        {
            removeButton.onClick.RemoveAllListeners();
            removeButton.onClick.AddListener(
                () => onRemove?.Invoke(itemIndex));
        }
    }
}