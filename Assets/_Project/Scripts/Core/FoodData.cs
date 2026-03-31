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

    [Header("Classification")]
    public FoodCategory category;
    public bool isHealthy = true;

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
}
