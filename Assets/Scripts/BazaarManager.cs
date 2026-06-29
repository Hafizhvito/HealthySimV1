using UnityEngine;
using UnityEngine.SceneManagement;

public class BazaarManager : MonoBehaviour
{
    public static BazaarManager Instance { get; private set; }

    [SerializeField] private float spawnChance = 0.8f;
    [SerializeField] private int bazaarIntervalDays = 5;
    [SerializeField] private Vector3 bazaarSpawnPosition = new Vector3(-53f, 1f, 37f);
    [SerializeField] private float bazaarDiscountMultiplier = EconomyConstants.BazaarDiscountMultiplier;
    [SerializeField] private FoodData[] bazaarFoodPool;
    [SerializeField] private string bazaarObjectName = "Interactable_Bazaar";

    private GameObject bazaarCubeObject;
    private BazaarInteractable bazaarInteractable;
    private bool bazaarActiveToday = false;
    private int lastSpawnDay = -1;
    private TimeManager timeManager;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (Application.isPlaying)
        {
            ResolveBazaarObject();
            HideBazaarObject();
        }
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
        TryBindTimeManager();
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        UnbindTimeManager();
    }

    public void TrySpawnBazaar(int dayNumber)
    {
        HideBazaarObject();
        bazaarActiveToday = false;

        if (bazaarIntervalDays <= 0)
            return;

        if (dayNumber < bazaarIntervalDays)
            return;

        if (dayNumber % bazaarIntervalDays != 0)
            return;

        if (dayNumber == lastSpawnDay)
            return;

        if (Random.value > spawnChance)
            return;

        ResolveBazaarObject();
        if (bazaarCubeObject == null)
            bazaarCubeObject = CreateRuntimeBazaarObject();

        if (bazaarCubeObject == null)
            return;

        bazaarCubeObject.transform.position = bazaarSpawnPosition;
        bazaarCubeObject.transform.localScale = new Vector3(1.5f, 1.5f, 1.5f);

        Renderer renderer = bazaarCubeObject.GetComponent<Renderer>();
        if (renderer != null)
            renderer.material.color = new Color(0.95f, 0.82f, 0.12f);

        if (bazaarInteractable == null)
            bazaarInteractable = bazaarCubeObject.GetComponent<BazaarInteractable>();

        if (bazaarInteractable == null)
            bazaarInteractable = bazaarCubeObject.AddComponent<BazaarInteractable>();

        bazaarInteractable.Initialize(bazaarFoodPool, bazaarDiscountMultiplier);
        bazaarCubeObject.SetActive(true);

        bazaarActiveToday = true;
        lastSpawnDay = dayNumber;
        Debug.Log($"[BazaarManager] Bazaar spawned on day {dayNumber}.");
    }

    private void HideBazaarObject()
    {
        GameObject target = bazaarCubeObject != null ? bazaarCubeObject : FindBazaarObjectInScene();
        if (target != null)
            target.SetActive(false);
    }

    private void ResolveBazaarObject()
    {
        if (bazaarCubeObject != null)
            return;

        bazaarCubeObject = FindBazaarObjectInScene();

        if (bazaarCubeObject != null)
            bazaarInteractable = bazaarCubeObject.GetComponent<BazaarInteractable>();
    }

    private GameObject FindBazaarObjectInScene()
    {
        if (string.IsNullOrWhiteSpace(bazaarObjectName))
            return null;

        Transform[] all = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null && all[i].name == bazaarObjectName)
                return all[i].gameObject;
        }

        return null;
    }

    private GameObject CreateRuntimeBazaarObject()
    {
        GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        obj.name = string.IsNullOrWhiteSpace(bazaarObjectName) ? "Interactable_Bazaar" : bazaarObjectName;
        return obj;
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ResolveBazaarObject();
        HideBazaarObject();
        TryBindTimeManager();
    }

    private void TryBindTimeManager()
    {
        if (timeManager == null)
            timeManager = FindFirstObjectByType<TimeManager>();

        if (timeManager == null)
            return;

        timeManager.OnDayChanged -= HandleDayChanged;
        timeManager.OnDayChanged += HandleDayChanged;
    }

    private void UnbindTimeManager()
    {
        if (timeManager != null)
            timeManager.OnDayChanged -= HandleDayChanged;

        timeManager = null;
    }

    private void HandleDayChanged(int dayNumber, string dayName)
    {
        if (!IsEligibleDay(dayNumber))
        {
            bazaarActiveToday = false;
            HideBazaarObject();
        }
    }

    private bool IsEligibleDay(int dayNumber)
    {
        if (bazaarIntervalDays <= 0)
            return false;

        if (dayNumber < bazaarIntervalDays)
            return false;

        return dayNumber % bazaarIntervalDays == 0;
    }

    public bool IsBazaarActiveToday => bazaarActiveToday;
}
