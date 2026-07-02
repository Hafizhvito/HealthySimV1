using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

public class BazaarManager : MonoBehaviour
{
    public static BazaarManager Instance { get; private set; }

    [SerializeField] private float spawnChance = 1f;
    [SerializeField] private int bazaarIntervalDays = 5;
    [SerializeField] private Vector3 bazaarSpawnPosition = new Vector3(-53f, 1f, 37f);
    [SerializeField] private float bazaarDiscountMultiplier = EconomyConstants.BazaarDiscountMultiplier;
    [SerializeField] private FoodData[] bazaarFoodPool;
    [SerializeField] private string bazaarObjectName = "Interactable_Bazaar";

    private GameObject bazaarCubeObject;
    private BazaarInteractable bazaarInteractable;
    private bool bazaarActiveToday;
    private bool usingScenePlacedBazaar;
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
        spawnChance = 1f;
        bazaarDiscountMultiplier = EconomyConstants.BazaarDiscountMultiplier;
        EnsureBazaarFoodPool();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
        TryBindTimeManager();
        SyncBazaarForCurrentDay();
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        UnbindTimeManager();
    }

    public void TrySpawnBazaar(int dayNumber)
    {
        if (!IsEligibleDay(dayNumber))
        {
            bazaarActiveToday = false;
            HideBazaarObject();
            return;
        }

        if (spawnChance < 1f && Random.value > spawnChance)
        {
            bazaarActiveToday = false;
            HideBazaarObject();
            return;
        }

        if (bazaarActiveToday && IsBazaarObjectVisible())
            return;

        ResolveBazaarObject();
        bool createdRuntime = false;
        if (bazaarCubeObject == null)
        {
            bazaarCubeObject = CreateRuntimeBazaarObject();
            createdRuntime = bazaarCubeObject != null;
        }

        if (bazaarCubeObject == null)
            return;

        if (createdRuntime || !usingScenePlacedBazaar)
        {
            bazaarCubeObject.transform.position = bazaarSpawnPosition;
            bazaarCubeObject.transform.localScale = new Vector3(1.5f, 1.5f, 1.5f);

            Renderer renderer = bazaarCubeObject.GetComponent<Renderer>();
            if (renderer != null)
                renderer.material.color = new Color(0.95f, 0.82f, 0.12f);
        }

        if (bazaarInteractable == null)
            bazaarInteractable = bazaarCubeObject.GetComponent<BazaarInteractable>();

        if (bazaarInteractable == null)
            bazaarInteractable = bazaarCubeObject.AddComponent<BazaarInteractable>();

        EnsureBazaarFoodPool();
        bazaarInteractable.Initialize(bazaarFoodPool, bazaarDiscountMultiplier);
        bazaarCubeObject.SetActive(true);

        bazaarActiveToday = true;
        Debug.Log($"[BazaarManager] Bazaar aktif hari {dayNumber} di {bazaarCubeObject.transform.position} ({bazaarFoodPool?.Length ?? 0} item, diskon {bazaarDiscountMultiplier:P0}).");
    }

    private void EnsureBazaarFoodPool()
    {
        FoodCatalogProvider provider = GetComponent<FoodCatalogProvider>();
        if (provider == null)
            provider = FindFirstObjectByType<FoodCatalogProvider>();

        if (provider == null)
            return;

        List<FoodData> catalog = provider.GetFoods();
        if (catalog == null || catalog.Count == 0)
            return;

        List<FoodData> healthyCatalog = new List<FoodData>();
        for (int i = 0; i < catalog.Count; i++)
        {
            FoodData food = catalog[i];
            if (food != null && food.isHealthy)
                healthyCatalog.Add(food);
        }

        if (healthyCatalog.Count == 0)
            return;

        bazaarFoodPool = healthyCatalog.ToArray();
    }

    private void HideBazaarObject()
    {
        GameObject target = bazaarCubeObject != null ? bazaarCubeObject : FindBazaarObjectInScene();
        if (target != null)
            target.SetActive(false);
    }

    private bool IsBazaarObjectVisible()
    {
        ResolveBazaarObject();
        return bazaarCubeObject != null && bazaarCubeObject.activeInHierarchy;
    }

    private void ResolveBazaarObject()
    {
        if (bazaarCubeObject != null)
            return;

        bazaarCubeObject = FindBazaarObjectInScene();
        if (bazaarCubeObject == null)
            return;

        usingScenePlacedBazaar = true;
        bazaarInteractable = bazaarCubeObject.GetComponent<BazaarInteractable>();
    }

    private void ClearBazaarObjectReference()
    {
        bazaarCubeObject = null;
        bazaarInteractable = null;
        usingScenePlacedBazaar = false;
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
        usingScenePlacedBazaar = false;
        GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        obj.name = string.IsNullOrWhiteSpace(bazaarObjectName) ? "Interactable_Bazaar" : bazaarObjectName;
        return obj;
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ClearBazaarObjectReference();
        TryBindTimeManager();
        SyncBazaarForCurrentDay();
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
        SyncBazaarForCurrentDay();
    }

    private void SyncBazaarForCurrentDay()
    {
        if (timeManager == null)
            timeManager = FindFirstObjectByType<TimeManager>();

        if (timeManager == null)
            return;

        TrySpawnBazaar(timeManager.CurrentDayNumber);
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
