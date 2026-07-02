using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BazaarInteractable : MonoBehaviour, IInteractable
{
    private const string ModalKey = "bazaar_menu";

    private FoodData[] foodPool;
    private float discountMultiplier = EconomyConstants.BazaarDiscountMultiplier;
    private Collider cachedCollider;

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
        discountMultiplier = Mathf.Clamp(discount, 0.05f, 1f);
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

        for (int i = 0; i < foodPool.Length; i++)
        {
            FoodData item = foodPool[i];
            if (item != null)
                choices.Add(item);
        }

        return BuildDiscountedClones(choices);
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

            int discountedAssetPrice = Mathf.Max(1, Mathf.RoundToInt(original.GetAssetPrice() * discountMultiplier));
            clone.price = discountedAssetPrice;

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
