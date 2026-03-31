using System.Collections.Generic;
using UnityEngine;

public class SessionFoodStash : MonoBehaviour
{
    public static SessionFoodStash Instance { get; private set; }

    [SerializeField] private List<FoodData> savedFoods = new List<FoodData>();

    public event System.Action OnStashChanged;

    public int Count => savedFoods.Count;
    public int StashCount => savedFoods.Count;
    public IReadOnlyList<FoodData> StashedFoods => savedFoods;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    public IReadOnlyList<FoodData> GetItems()
    {
        return savedFoods;
    }

    public bool Add(FoodData food)
    {
        if (food == null)
            return false;

        savedFoods.Add(food);
        OnStashChanged?.Invoke();

        if (StoryManager.Instance != null)
            StoryManager.Instance.OnFoodSaved(food);

        if (PlayerActionTracker.Instance != null)
            PlayerActionTracker.Instance.Track(PlayerActionTracker.ActionType.GenericInteraction, $"StashAdd:{food.foodName}");

        Debug.Log($"[Stash] Disimpan: {food.foodName}. Total={savedFoods.Count}");
        return true;
    }

    public bool RemoveAt(int index)
    {
        if (index < 0 || index >= savedFoods.Count)
            return false;

        FoodData removed = savedFoods[index];
        savedFoods.RemoveAt(index);
        OnStashChanged?.Invoke();

        if (PlayerActionTracker.Instance != null)
            PlayerActionTracker.Instance.Track(PlayerActionTracker.ActionType.GenericInteraction, $"StashRemove:{removed.foodName}");

        Debug.Log($"[Stash] Dihapus: {removed.foodName}. Total={savedFoods.Count}");
        return true;
    }

    public bool ConsumeAt(int index)
    {
        if (index < 0 || index >= savedFoods.Count)
            return false;

        if (PlayerStats.Instance == null)
            return false;

        FoodData food = savedFoods[index];
        savedFoods.RemoveAt(index);
        OnStashChanged?.Invoke();

        PlayerStats.Instance.AddFood(food.energyRestored, food.calories, food.moodEffect);

        if (StoryManager.Instance != null)
            StoryManager.Instance.OnFoodEaten(food);

        if (PlayerActionTracker.Instance != null)
        {
            PlayerActionTracker.Instance.Track(
                food.isHealthy ? PlayerActionTracker.ActionType.HealthyFoodTaken : PlayerActionTracker.ActionType.UnhealthyFoodTaken,
                $"StashConsume:{food.foodName}");
        }

        Debug.Log($"[Stash] Dikonsumsi: {food.foodName}. Total={savedFoods.Count}");
        return true;
    }

    public void ClearAll()
    {
        if (savedFoods.Count == 0)
            return;

        savedFoods.Clear();
        OnStashChanged?.Invoke();
        if (PlayerActionTracker.Instance != null)
            PlayerActionTracker.Instance.Track(PlayerActionTracker.ActionType.GenericInteraction, "StashClearAll");

        Debug.Log("[Stash] Semua item dibersihkan.");
    }

    public void AddToStash(FoodData food)
    {
        Add(food);
    }

    public void ConsumeFromStash(int index)
    {
        ConsumeAt(index);
    }

    public void RemoveFromStash(int index)
    {
        RemoveAt(index);
    }

    public void ClearStash()
    {
        ClearAll();
    }
}
