using UnityEngine;

public class StoryManager : MonoBehaviour
{
    public static StoryManager Instance { get; private set; }

    [SerializeField] private NpcDialogueInteractable npcReference;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    void Start()
    {
        if (npcReference == null)
            npcReference = FindFirstObjectByType<NpcDialogueInteractable>();
    }

    public void OnFoodSaved(FoodData food)
    {
        if (food == null)
            return;

        Debug.Log($"[Story] Makanan disimpan: {food.foodName}");
    }

    public void OnFoodEaten(FoodData food)
    {
        if (food == null)
            return;

        UpdateNPCTrust(food.isHealthy);
    }

    public void UpdateNPCTrust(bool isHealthy)
    {
        if (npcReference == null)
            npcReference = FindFirstObjectByType<NpcDialogueInteractable>();

        if (npcReference != null)
            npcReference.ApplyTrustDelta(isHealthy ? 1f : -1f);
    }
}
