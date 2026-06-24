using System.Collections.Generic;
using UnityEngine;

public class HomeFoodStationInteractable : MonoBehaviour, IInteractable
{
    [Header("Prompt")]
    [SerializeField] private string interactionText = "Tekan E untuk akses dapur rumah";
    [SerializeField] private string stationLabel = "Dapur Rumah";

    [Header("Home Food Options")]
    [SerializeField] private List<FoodData> homeFoods = new List<FoodData>();
    [SerializeField] private bool includeCatalogFallbackWhenEmpty = true;
    [SerializeField] private bool filterByTime = true;

    [Header("Home Pricing")]
    [SerializeField] [Range(0.25f, 1f)] private float homePriceMultiplier = 0.65f;

    [Header("Optional References")]
    [SerializeField] private FoodCatalogProvider foodCatalogProviderOverride;

    private Collider cachedCollider;
    private FoodCatalogProvider foodCatalogProvider;
    private FoodData fallbackFood;

    void Awake()
    {
        cachedCollider = GetComponent<Collider>();
        if (cachedCollider == null)
            cachedCollider = GetComponentInChildren<Collider>();

        foodCatalogProvider = foodCatalogProviderOverride;
        if (foodCatalogProvider == null)
        {
            GameObject manager = GameObject.Find("GameManager");
            if (manager != null)
                foodCatalogProvider = manager.GetComponent<FoodCatalogProvider>();

            if (foodCatalogProvider == null)
                foodCatalogProvider = FindFirstObjectByType<FoodCatalogProvider>();
        }

        homePriceMultiplier = Mathf.Clamp(homePriceMultiplier, 0.25f, 1f);
    }

    void OnEnable()
    {
        InteractableRegistry.Register(this, cachedCollider, transform);
    }

    void OnDisable()
    {
        InteractableRegistry.Unregister(this);
    }

    public string GetInteractionText()
    {
        return interactionText;
    }

    public bool CanInteract(GameObject interactor)
    {
        return PlayerStats.Instance != null;
    }

    public void Interact(GameObject interactor)
    {
        List<FoodData> choices = BuildHomeFoodChoices();

        if (FoodChoiceMenuController.Instance == null)
        {
            Debug.LogWarning("[HomeFood] FoodChoiceMenuController tidak ditemukan. Interaksi dibatalkan.");
            return;
        }

        FoodChoiceMenuController.Instance.OpenHomeMenu(stationLabel, choices, homePriceMultiplier);
    }

    private List<FoodData> BuildHomeFoodChoices()
    {
        List<FoodData> source = new List<FoodData>();
        for (int i = 0; i < homeFoods.Count; i++)
        {
            FoodData item = homeFoods[i];
            if (item != null && !source.Contains(item))
                source.Add(item);
        }

        if (source.Count == 0 && includeCatalogFallbackWhenEmpty)
        {
            if (foodCatalogProvider != null)
            {
                List<FoodData> catalogFoods = foodCatalogProvider.GetFoods();
                for (int i = 0; i < catalogFoods.Count; i++)
                {
                    FoodData item = catalogFoods[i];
                    if (item != null && !source.Contains(item))
                        source.Add(item);
                }
            }
            else
            {
                FoodData[] resourcesFoods = Resources.LoadAll<FoodData>("FoodData");
                for (int i = 0; i < resourcesFoods.Length; i++)
                {
                    FoodData item = resourcesFoods[i];
                    if (item != null && !source.Contains(item))
                        source.Add(item);
                }
            }
        }

        List<FoodData> filtered = new List<FoodData>();
        TimeManager.TimePeriod currentPeriod = TimeManager.Instance != null
            ? TimeManager.Instance.CurrentPeriod
            : TimeManager.TimePeriod.Morning;

        for (int i = 0; i < source.Count; i++)
        {
            FoodData food = source[i];
            if (food == null)
                continue;

            if (filterByTime && !food.IsAvailableAt(currentPeriod))
                continue;

            filtered.Add(food);
        }

        if (filtered.Count == 0)
            filtered.Add(GetFallbackFood());

        return filtered;
    }

    private FoodData GetFallbackFood()
    {
        if (fallbackFood != null)
            return fallbackFood;

        fallbackFood = ScriptableObject.CreateInstance<FoodData>();
        fallbackFood.foodName = "Menu Rumahan Sederhana";
        fallbackFood.description = "Fallback menu rumah (runtime only).";
        fallbackFood.calories = 220f;
        fallbackFood.energyRestored = 16f;
        fallbackFood.moodEffect = 5f;
        fallbackFood.fat = 7f;
        fallbackFood.protein = 10f;
        fallbackFood.carbohydrate = 28f;
        fallbackFood.category = FoodData.FoodCategory.MakananBerat;
        fallbackFood.isHealthy = true;
        fallbackFood.price = 18;
        fallbackFood.availableMorning = true;
        fallbackFood.availableAfternoon = true;
        fallbackFood.availableEvening = true;
        fallbackFood.availableNight = true;
        return fallbackFood;
    }
}
