using UnityEngine;

[CreateAssetMenu(fileName = "NewFood", menuName = "HealthSim/Food Data")]
public class FoodData : ScriptableObject
{
    [Header("Basic Info")]
    public string foodName = "Makanan";
    public string description = "Deskripsi makanan";
    public Sprite icon;

    [Header("Nutrition Values")]
    public float calories = 200f;
    public float energyRestored = 20f;
    public float moodEffect = 5f;
    public float fat = 8f;
    public float protein = 6f;
    public float carbohydrate = 24f;
    [SerializeField] private float sugar = 0f;

    public float Protein => protein;
    public float Fat => fat;
    public float Carbohydrate => carbohydrate;
    public float Sugar => sugar;
    public bool IsJunkFood => !isHealthy || category == FoodCategory.FastFood;

    [Header("Classification")]
    public FoodCategory category;
    public bool isHealthy = true;

    [Header("Economy")]
    [Min(0)] public int price = 0;

    [Header("Availability")]
    public bool availableMorning = true;
    public bool availableAfternoon = true;
    public bool availableEvening = true;
    public bool availableNight = false;

    public enum FoodCategory
    {
        MakananBerat,
        MakananRingan,
        Minuman,
        Buah,
        FastFood,
        Dessert
    }

    // Check if this food is available in given time period
    public bool IsAvailableAt(TimeManager.TimePeriod period)
    {
        return period switch
        {
            TimeManager.TimePeriod.Morning => availableMorning,
            TimeManager.TimePeriod.Afternoon => availableAfternoon,
            TimeManager.TimePeriod.Evening => availableEvening,
            TimeManager.TimePeriod.Night => availableNight,
            _ => false
        };
    }

    public int GetEffectivePrice()
    {
        int assetPrice = price > 0
            ? price
            : category switch
            {
                FoodCategory.Minuman => isHealthy ? 14 : 18,
                FoodCategory.Buah => isHealthy ? 12 : 16,
                FoodCategory.MakananRingan => isHealthy ? 16 : 21,
                FoodCategory.Dessert => isHealthy ? 18 : 24,
                FoodCategory.FastFood => isHealthy ? 24 : 33,
                _ => isHealthy ? 24 : 31
            };

        return EconomyConstants.ScalePrice(assetPrice);
    }
}
