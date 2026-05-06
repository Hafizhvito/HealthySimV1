using UnityEngine;

[CreateAssetMenu(fileName = "NpcDialogueData", menuName = "HealthSim/NPC Dialogue Data")]
public class NpcDialogueData : ScriptableObject
{
    [Header("Prompt")]
    public string npcName = "Warga Kota";
    [TextArea(2, 5)] public string prompt = "Bagaimana kamu hari ini?";

    [Header("Positive Choice")]
    public string positiveChoiceText = "Terima kasih, saya akan pilih yang sehat.";
    [TextArea(2, 5)] public string positiveResponse = "Bagus! Pilihan kecil yang konsisten itu kunci.";
    public float positiveMoodDelta = 4f;

    [Header("Negative Choice")]
    public string negativeChoiceText = "Biar saja, saya pilih yang cepat saja.";
    [TextArea(2, 5)] public string negativeResponse = "Hmm, kebiasaan itu bisa jadi masalah nanti.";
    public float negativeMoodDelta = -3f;

    [Header("Availability")]
    public bool availableMorning = true;
    public bool availableAfternoon = true;
    public bool availableEvening = true;
    public bool availableNight = true;

    public bool IsAvailableAt(TimeManager.TimePeriod period)
    {
        return period switch
        {
            TimeManager.TimePeriod.Morning => availableMorning,
            TimeManager.TimePeriod.Afternoon => availableAfternoon,
            TimeManager.TimePeriod.Evening => availableEvening,
            TimeManager.TimePeriod.Night => availableNight,
            _ => true
        };
    }
}
