using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Owns the city traffic route and pre-placed vehicles configured by the editor setup menu.
/// </summary>
[DisallowMultipleComponent]
public class CityTrafficManager : MonoBehaviour
{
    public static CityTrafficManager Instance { get; private set; }

    [SerializeField] private TrafficRoute mainRoute;
    [SerializeField] private List<TrafficVehicleController> vehicles = new List<TrafficVehicleController>();

    public TrafficRoute MainRoute => mainRoute;
    public IReadOnlyList<TrafficVehicleController> Vehicles => vehicles;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[CityTraffic] Duplicate CityTrafficManager detected.", this);
            return;
        }

        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void Configure(TrafficRoute route, List<TrafficVehicleController> configuredVehicles)
    {
        mainRoute = route;
        vehicles = configuredVehicles ?? new List<TrafficVehicleController>();
    }

#if UNITY_EDITOR
    public void SetMainRoute(TrafficRoute route)
    {
        mainRoute = route;
    }

    public void SetVehicles(List<TrafficVehicleController> configuredVehicles)
    {
        vehicles = configuredVehicles ?? new List<TrafficVehicleController>();
    }
#endif
}
