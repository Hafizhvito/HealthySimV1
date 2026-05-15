using System.Collections;
using TMPro;
using UnityEngine;

public class GymDoorInteractable : MonoBehaviour, IInteractable
{
    [SerializeField] private string gymSceneName = "GymScene";
    [SerializeField] private string promptText = "Masuk Gym";
    [SerializeField] private string blockedEnergyText = "Energi terlalu rendah untuk latihan.";
    [SerializeField] private string blockedNightText = "Gym sudah tutup malam ini.";
    [SerializeField] private string alreadyTrainedText = "Kamu sudah latihan hari ini.";
    [SerializeField] private GymProgressionSystem gymProgressionOverride;
    [SerializeField] private FadeManager fadeManagerOverride;

    private Collider cachedCollider;
    private Coroutine floatingTextRoutine;

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

    public string GetInteractionText()
    {
        return promptText;
    }

    // Compatibility helper with requested naming.
    public string GetInteractPrompt()
    {
        return promptText;
    }

    public bool CanInteract(GameObject interactor)
    {
        return true;
    }

    public void Interact(GameObject interactor)
    {
        OnInteract(interactor);
    }

    // Compatibility helper with requested naming.
    public void OnInteract(GameObject interactor)
    {
        TimeManager timeManager = TimeManager.Instance;
        PlayerStats playerStats = PlayerStats.Instance;
        GymProgressionSystem progression = ResolveGymProgression();
        FadeManager fadeManager = ResolveFadeManager();

        if (timeManager == null || playerStats == null || progression == null)
        {
            ShowFloatingText("Sistem gym belum siap.", 2f);
            return;
        }

        float hour = timeManager.CurrentHour;
        float energy = playerStats.EnergyPercent;

        if (hour < 6f || hour >= 22f)
        {
            ShowFloatingText("Gym sudah tutup. Jam operasional 06.00 - 22.00.", 2f);
            return;
        }

        TimeManager.TimePeriod period = timeManager.CurrentPeriod;
        bool hasTrained = progression.HasTrainedToday;
        bool canTrain = progression.CanTrain(energy, period);

        Debug.Log($"[GymDoor] hour={hour:F2} energy={energy:F2} hasTrained={hasTrained} canTrain={canTrain}");

        if (hasTrained)
        {
            ShowFloatingText("Kamu sudah berlatih hari ini. Istirahat dulu!", 2f);
            return;
        }

        if (!canTrain)
        {
            ShowFloatingText("Energimu terlalu rendah untuk berlatih.", 2f);
            return;
        }

        GymSessionData session = progression.BuildSession(period, energy);
        if (session == null)
        {
            ShowFloatingText(blockedEnergyText, 2f);
            return;
        }

        if (fadeManager == null)
        {
            ShowFloatingText("Transisi belum siap.", 2f);
            return;
        }

        SpawnPlayerManager.TargetSpawnID = "default";
fadeManager.FadeToBlackAndLoad(gymSceneName, 0.5f);
    }

    private GymProgressionSystem ResolveGymProgression()
    {
        if (gymProgressionOverride != null)
            return gymProgressionOverride;

        if (GymProgressionSystem.Instance != null)
            return GymProgressionSystem.Instance;

        GameObject managerObj = GameObject.Find("GameManager");
        if (managerObj == null)
        {
            managerObj = new GameObject("GameManager");
            Debug.LogWarning("[GymDoorInteractable] GameManager tidak ditemukan. Membuat fallback runtime.");
        }

        GymProgressionSystem existing = managerObj.GetComponent<GymProgressionSystem>();
        return existing != null ? existing : managerObj.AddComponent<GymProgressionSystem>();
    }

    private FadeManager ResolveFadeManager()
    {
        if (fadeManagerOverride != null)
            return fadeManagerOverride;

        if (FadeManager.Instance != null)
            return FadeManager.Instance;

        GameObject managerObj = GameObject.Find("GameManager");
        if (managerObj == null)
            return null;

        FadeManager existing = managerObj.GetComponent<FadeManager>();
        return existing != null ? existing : managerObj.AddComponent<FadeManager>();
    }

    private void ShowFloatingText(string text, float duration)
    {
        if (floatingTextRoutine != null)
            StopCoroutine(floatingTextRoutine);

        floatingTextRoutine = StartCoroutine(FloatingTextRoutine(text, duration));
    }

    private IEnumerator FloatingTextRoutine(string text, float duration)
    {
        GameObject textObj = new GameObject("GymDoorFloatingText");

        TextMeshPro tmp = textObj.AddComponent<TextMeshPro>();
        tmp.text = text;
        tmp.fontSize = 3f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = new Color(0.78f, 0.95f, 0.82f, 1f);

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            Camera cam = Camera.main;
            Vector3 displayPos = transform.position + Vector3.up * 1.25f;
            if (cam != null)
            {
                Vector3 toCam = cam.transform.position - transform.position;
                toCam.y = 0f;
                if (toCam.sqrMagnitude < 0.001f)
                    toCam = -transform.forward;

                toCam.Normalize();
                displayPos = transform.position + toCam * 0.75f + Vector3.up * 1.25f;
                textObj.transform.rotation = Quaternion.LookRotation(textObj.transform.position - cam.transform.position);
            }

            textObj.transform.position = displayPos;
            yield return null;
        }

        Destroy(textObj);
        floatingTextRoutine = null;
    }
}
