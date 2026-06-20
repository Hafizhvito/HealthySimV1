using UnityEngine;
using System.Collections.Generic;

public class FoodPickupInteractable : MonoBehaviour, IInteractable
{
    [SerializeField] private string interactionLabel = "Paket Makanan";
    [SerializeField] private List<FoodData> availableFoods = new List<FoodData>();
    [SerializeField] private bool useGlobalFoodCatalogWhenEmpty = true;
    [SerializeField] private bool filterByTime = false;

    [Header("Economy")]
    [SerializeField] [Range(0.5f, 2f)] private float outletPriceMultiplier = 1f;

    [Header("Fallback (used only if no FoodData is assigned)")]
    [SerializeField] private string fallbackFoodName = "Paket Makanan Sehat";
    [SerializeField] private float fallbackEnergyGain = 18f;
    [SerializeField] private float fallbackCalorieGain = 220f;
    [SerializeField] private float fallbackMoodGain = 8f;
    [SerializeField] private bool fallbackHealthy = true;
    [SerializeField] private bool consumeOnInteract = true;

    [Header("Optional References")]
    [SerializeField] private FoodCatalogProvider foodCatalogProviderOverride;
    [SerializeField] private SessionFoodStash foodStashOverride;

    private Collider cachedCollider;
    private FoodData fallbackFood;
    private FoodCatalogProvider foodCatalogProvider;
    private SessionFoodStash foodStash;
    private const string FallbackLogPrefix = "[SwapContract/Fallback]";

    public bool FilterByTime => filterByTime;

    void Start()
    {
        if (foodCatalogProvider != null)
        {
            List<FoodData> allFoods = foodCatalogProvider.GetFoods();
            if (availableFoods.Count == 0)
            {
                availableFoods.AddRange(allFoods);
            }
            else if (availableFoods.Count < allFoods.Count)
            {
                for (int i = 0; i < allFoods.Count; i++)
                {
                    FoodData item = allFoods[i];
                    if (item != null && !availableFoods.Contains(item))
                        availableFoods.Add(item);
                }
            }
        }
    }

    void Awake()
    {
        cachedCollider = GetComponent<Collider>();
        foodCatalogProvider = foodCatalogProviderOverride;
        foodStash = foodStashOverride;
        outletPriceMultiplier = Mathf.Clamp(outletPriceMultiplier, 0.5f, 2f);

        if (foodCatalogProvider != null && foodStash != null)
            return;

        GameObject manager = GameObject.Find("GameManager");
        if (manager != null)
        {
            if (foodCatalogProvider == null)
                foodCatalogProvider = manager.GetComponent<FoodCatalogProvider>();

            if (foodStash == null)
                foodStash = manager.GetComponent<SessionFoodStash>();

            if (foodCatalogProvider != null || foodStash != null)
                Debug.LogWarning($"{FallbackLogPrefix} Resolved FoodCatalogProvider/SessionFoodStash via GameManager lookup on {gameObject.name}.");
        }
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
        return $"Tekan E untuk memilih makanan: {interactionLabel}";
    }

    public bool CanInteract(GameObject interactor)
    {
        return PlayerStats.Instance != null;
    }

    public void Interact(GameObject interactor)
    {
        List<FoodData> choices = GetAvailableFoodChoices();
        TutorialContextualUI.HasPickedUpFood = true;

        if (FoodChoiceMenuController.Instance != null)
        {
            FoodChoiceMenuController.Instance.OpenMenu(interactionLabel, choices);
            return;
        }

        // Fallback if menu controller is missing.
        FoodData selected = choices.Count > 0 ? choices[0] : GetFallbackFood();
        ConsumeNow(interactor, selected);
    }

    public string GetFoodName()
    {
        return interactionLabel;
    }

    public List<FoodData> GetAvailableFoodChoices()
    {
        if (availableFoods.Count == 0 && foodCatalogProvider != null)
            availableFoods.AddRange(foodCatalogProvider.GetFoods());

        if (availableFoods.Count == 0 && useGlobalFoodCatalogWhenEmpty)
            availableFoods.AddRange(Resources.LoadAll<FoodData>("FoodData"));

        List<FoodData> result = new List<FoodData>();
        TimeManager.TimePeriod currentPeriod = TimeManager.Instance != null
            ? TimeManager.Instance.CurrentPeriod
            : TimeManager.TimePeriod.Morning;

        for (int i = 0; i < availableFoods.Count; i++)
        {
            FoodData data = availableFoods[i];
            if (data == null)
                continue;

            if (filterByTime && !data.IsAvailableAt(currentPeriod))
                continue;

            result.Add(data);
        }

        if (result.Count == 0)
            result.Add(GetFallbackFood());

        return result;
    }

    public void ConsumeNow(GameObject interactor, FoodData selectedFood)
    {
        if (PlayerStats.Instance == null)
            return;
    
        FoodData food = selectedFood != null ? selectedFood : GetFallbackFood();
    
        if (!TrySpendForFood(food, out int chargedPrice))
            return;
    
        PlayerStats.Instance.AddFood(food.energyRestored, food.calories, food.moodEffect, food.protein, food.fat);
        // DIHAPUS: PlayerStats.Instance?.RegisterHealthScore(foodData.isHealthy ? 5f : -5f);
    
        if (PlayerActionTracker.Instance != null)
        {
            PlayerActionTracker.Instance.Track(
                food.isHealthy
                    ? PlayerActionTracker.ActionType.HealthyFoodTaken
                    : PlayerActionTracker.ActionType.UnhealthyFoodTaken,
                gameObject.name
            );
        }
    
        Debug.Log($"[Interaction] {food.foodName} dibeli Rp{chargedPrice}. " +
                $"Energi +{food.energyRestored}, Kalori +{food.calories}, " +
                $"Mood {food.moodEffect:+0.##;-0.##;0}");
    
        if (consumeOnInteract)
            gameObject.SetActive(false);
    }

    public bool SaveForLater(GameObject interactor, FoodData selectedFood)
    {
        FoodData food = selectedFood != null ? selectedFood : GetFallbackFood();

        if (!TrySpendForFood(food, out int chargedPrice))
            return false;

        if (foodStash == null)
        {
            foodStash = foodStashOverride;

            GameObject manager = GameObject.Find("GameManager");
            if (manager != null)
            {
                foodStash = manager.GetComponent<SessionFoodStash>();
                if (foodStash != null)
                    Debug.LogWarning($"{FallbackLogPrefix} Resolved SessionFoodStash via GameManager lookup during SaveForLater on {gameObject.name}.");
            }
        }

        bool saved = foodStash != null && foodStash.Add(food);

        if (PlayerActionTracker.Instance != null)
            PlayerActionTracker.Instance.Track(PlayerActionTracker.ActionType.GenericInteraction, $"SaveForLater:{gameObject.name}:{food.foodName}");

        int stashCount = foodStash != null ? foodStash.Count : 0;
        Debug.Log(saved
            ? $"[Interaction] {food.foodName} dibeli Rp{chargedPrice} dan disimpan ke stash sesi. Total stash: {stashCount}."
            : $"[Interaction] Gagal simpan {food.foodName} (stash tidak tersedia).");

        return saved;
    }

    private FoodData GetFallbackFood()
    {
        if (fallbackFood != null)
            return fallbackFood;

        fallbackFood = ScriptableObject.CreateInstance<FoodData>();
        fallbackFood.foodName = fallbackFoodName;
        fallbackFood.description = "Fallback food data (runtime only).";
        fallbackFood.calories = fallbackCalorieGain;
        fallbackFood.energyRestored = fallbackEnergyGain;
        fallbackFood.moodEffect = fallbackMoodGain;
        fallbackFood.isHealthy = fallbackHealthy;
        fallbackFood.availableMorning = true;
        fallbackFood.availableAfternoon = true;
        fallbackFood.availableEvening = true;
        fallbackFood.availableNight = true;
        fallbackFood.price = 18;
        return fallbackFood;
    }

    private bool TrySpendForFood(FoodData food, out int chargedPrice)
    {
        chargedPrice = 0;

        if (food == null)
            return false;

        if (PlayerStats.Instance == null)
            return false;

        int price = GetOutletPrice(food);
        chargedPrice = price;
        if (price <= 0)
            return true;

        if (PlayerStats.Instance.Money < price)
        {
            Debug.LogWarning($"[Interaction] Uang tidak cukup untuk membeli {food.foodName}. Butuh Rp{price}, uang sekarang Rp{PlayerStats.Instance.Money}.");
            return false;
        }

        PlayerStats.Instance.SpendMoney(price);
        return true;
    }

    public int GetDisplayPrice(FoodData food)
    {
        return GetOutletPrice(food);
    }

    public bool TryPurchaseFood(FoodData food, out int chargedPrice)
    {
        chargedPrice = 0;
        if (food == null || PlayerStats.Instance == null)
            return false;

        if (!TrySpendForFood(food, out chargedPrice))
            return false;

        PlayerStats.Instance.AddFood(
            food.energyRestored, food.calories, food.moodEffect, food.protein, food.fat);

        if (PlayerActionTracker.Instance != null)
        {
            PlayerActionTracker.Instance.Track(
                food.isHealthy
                    ? PlayerActionTracker.ActionType.HealthyFoodTaken
                    : PlayerActionTracker.ActionType.UnhealthyFoodTaken,
                gameObject.name);
        }

        return true;
    }

    private int GetOutletPrice(FoodData food)
    {
        if (food == null)
            return 0;

        int basePrice = Mathf.Max(0, food.GetEffectivePrice());
        if (basePrice <= 0)
            return 0;

        float multiplier = Mathf.Clamp(outletPriceMultiplier, 0.5f, 2f);
        return Mathf.Max(1, Mathf.RoundToInt(basePrice * multiplier));
    }
}
    