using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BazaarInteractable : MonoBehaviour, IInteractable
{
    private const string ModalKey = "bazaar_menu";

    private FoodData[] foodPool;
    private float discountMultiplier = 0.5f;
    private Collider cachedCollider;
    [SerializeField] private int foodCount = 10;
    [SerializeField] private int drinkCount = 3;

    private void Awake()
    {
        cachedCollider = GetComponent<Collider>();
        if (cachedCollider == null)
            cachedCollider = GetComponentInChildren<Collider>();
    }

    private void OnEnable()
    {
        InteractableRegistry.Register(this, cachedCollider, transform);
    }

    private void OnDisable()
    {
        InteractableRegistry.Unregister(this);
    }

    public void Initialize(FoodData[] pool, float discount)
    {
        foodPool = pool;
        discountMultiplier = discount;
    }

    public string GetInteractionText() => "Tekan E untuk buka Bazaar Sehat";

    public bool CanInteract(GameObject interactor) => true;

    public void Interact(GameObject interactor)
    {
        List<FoodData> choices = BuildDiscountedFoods();

        if (ModalStateManager.Instance != null)
            ModalStateManager.Instance.OpenModal(ModalKey);

        if (FoodChoiceMenuController.Instance != null)
        {
            FoodChoiceMenuController.Instance.OpenMenu("Bazaar Sehat", choices);
            StartCoroutine(WaitForMenuCloseAndReleaseModal());
            Debug.Log("[BazaarInteractable] Bazaar dibuka.");
            return;
        }

        if (ModalStateManager.Instance != null)
            ModalStateManager.Instance.CloseModal(ModalKey);

        Debug.LogWarning("[BazaarInteractable] FoodChoiceMenuController tidak ditemukan.");
    }

    private List<FoodData> BuildDiscountedFoods()
    {
        List<FoodData> choices = new List<FoodData>();
        if (foodPool == null || foodPool.Length == 0)
            return choices;

        List<FoodData> foods = new List<FoodData>();
        List<FoodData> drinks = new List<FoodData>();

        for (int i = 0; i < foodPool.Length; i++)
        {
            FoodData item = foodPool[i];
            if (item == null)
                continue;

            if (item.category == FoodData.FoodCategory.Minuman)
                drinks.Add(item);
            else
                foods.Add(item);
        }

        int pickedFoods = AddRandomSelection(choices, foods, Mathf.Max(0, foodCount));
        int pickedDrinks = AddRandomSelection(choices, drinks, Mathf.Max(0, drinkCount));

        if (pickedFoods < foodCount || pickedDrinks < drinkCount)
        {
            Debug.LogWarning($"[BazaarInteractable] Pool kurang. foods={pickedFoods}/{foodCount} drinks={pickedDrinks}/{drinkCount}.");
        }

        return BuildDiscountedClones(choices);
    }

    private int AddRandomSelection(List<FoodData> output, List<FoodData> source, int count)
    {
        if (output == null || source == null || count <= 0)
            return 0;

        List<FoodData> shuffled = new List<FoodData>(source);
        for (int i = 0; i < shuffled.Count; i++)
        {
            int j = Random.Range(i, shuffled.Count);
            (shuffled[i], shuffled[j]) = (shuffled[j], shuffled[i]);
        }

        int take = Mathf.Min(count, shuffled.Count);
        for (int i = 0; i < take; i++)
            output.Add(shuffled[i]);

        return take;
    }

    private List<FoodData> BuildDiscountedClones(List<FoodData> selected)
    {
        List<FoodData> discounted = new List<FoodData>();
        if (selected == null)
            return discounted;

        for (int i = 0; i < selected.Count; i++)
        {
            FoodData original = selected[i];
            if (original == null)
                continue;

            FoodData clone = ScriptableObject.CreateInstance<FoodData>();
            clone.foodName = original.foodName;
            clone.description = original.description;
            clone.icon = original.icon;
            clone.calories = original.calories;
            clone.energyRestored = original.energyRestored;
            clone.moodEffect = original.moodEffect;
            clone.fat = original.fat;
            clone.protein = original.protein;
            clone.carbohydrate = original.carbohydrate;
            clone.category = original.category;
            clone.isHealthy = original.isHealthy;
            clone.availableMorning = original.availableMorning;
            clone.availableAfternoon = original.availableAfternoon;
            clone.availableEvening = original.availableEvening;
            clone.availableNight = original.availableNight;

            int discountedPrice = Mathf.RoundToInt(original.GetEffectivePrice() * discountMultiplier);
            clone.price = Mathf.Max(0, discountedPrice);

            discounted.Add(clone);
        }

        return discounted;
    }

    private IEnumerator WaitForMenuCloseAndReleaseModal()
    {
        while (FoodChoiceMenuController.Instance != null && FoodChoiceMenuController.Instance.IsOpen)
            yield return null;

        if (ModalStateManager.Instance != null)
            ModalStateManager.Instance.CloseModal(ModalKey);
    }
}
