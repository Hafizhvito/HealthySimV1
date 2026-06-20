#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class TrafficSceneSetup
{
    private const string TrafficRootName = "CityTraffic";
    private const string RouteName = "MainCityRoute";
    private const string VehiclesRootName = "Vehicles";
    private const int VehicleCount = 7;
    private const float MaxSegmentLinkDistance = 24f;
    private const float MinWaypointSpacing = 3f;
    private const float CornerAxisThreshold = 4f;
    private const float VehicleSpeed = 6f;

    private static readonly string[] VehiclePrefabPaths =
    {
        "Assets/SimplePoly City - Low Poly Assets/Prefab/Vehicles/Vehicle with Static Wheels/Vehicle_Car_color01.prefab",
        "Assets/SimplePoly City - Low Poly Assets/Prefab/Vehicles/Vehicle with Static Wheels/Vehicle_Car_color02.prefab",
        "Assets/SimplePoly City - Low Poly Assets/Prefab/Vehicles/Vehicle with Static Wheels/Vehicle_Car_color03.prefab",
        "Assets/SimplePoly City - Low Poly Assets/Prefab/Vehicles/Vehicle with Static Wheels/Vehicle_Taxi.prefab",
        "Assets/SimplePoly City - Low Poly Assets/Prefab/Vehicles/Vehicle with Static Wheels/Vehicle_SUV_color01.prefab",
        "Assets/SimplePoly City - Low Poly Assets/Prefab/Vehicles/Vehicle with Static Wheels/Vehicle_Pick up Truck_color01.prefab",
        "Assets/SimplePoly City - Low Poly Assets/Prefab/Vehicles/Vehicle with Static Wheels/Vehicle_Bus_color01.prefab"
    };

    [MenuItem("HealthySim/Traffic/1 - Generate Traffic Route From Roads")]
    public static void GenerateTrafficRoute()
    {
        if (!IsSampleSceneActive())
        {
            Debug.LogWarning("[Traffic] Buka SampleScene dulu.");
            return;
        }

        Transform roadLaneRoot = FindRoadLaneRoot();
        if (roadLaneRoot == null)
        {
            Debug.LogError("[Traffic] RoadObject/RoadLane tidak ditemukan.");
            return;
        }

        List<Vector3> routePoints = BuildRoutePoints(roadLaneRoot);
        if (routePoints.Count < 4)
        {
            Debug.LogError("[Traffic] Terlalu sedikit waypoint dari jalan. Periksa RoadObject/RoadLane.");
            return;
        }

        Transform trafficRoot = FindOrCreateRoot(TrafficRootName);
        Transform routeRoot = FindOrCreateChild(trafficRoot, RouteName);

        ClearChildren(routeRoot);

        TrafficRoute route = routeRoot.GetComponent<TrafficRoute>();
        if (route == null)
            route = routeRoot.gameObject.AddComponent<TrafficRoute>();

        List<Transform> waypointTransforms = CreateWaypointTransforms(routeRoot, routePoints);
        route.SetWaypoints(waypointTransforms, true);

        EditorUtility.SetDirty(routeRoot.gameObject);
        EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());

        int badSegments = LogDiagonalRouteSegments(routeRoot, MaxSegmentLinkDistance, CornerAxisThreshold);
        Debug.Log(
            $"[Traffic] Route generated: {routePoints.Count} waypoints, length ~{route.TotalLength:0.#}m, " +
            $"diagonalShortcuts={badSegments}.");
    }

    [MenuItem("HealthySim/Traffic/Validate Traffic Route Segments")]
    public static void ValidateTrafficRouteSegments()
    {
        if (!IsSampleSceneActive())
        {
            Debug.LogWarning("[Traffic] Buka SampleScene dulu.");
            return;
        }

        Transform routeRoot = FindTrafficRouteRoot();
        TrafficRoute route = routeRoot != null ? routeRoot.GetComponent<TrafficRoute>() : null;
        if (route == null || route.WaypointCount < 2)
        {
            Debug.LogWarning("[Traffic] MainCityRoute belum ada. Jalankan Generate Traffic Route From Roads.");
            return;
        }

        int badSegments = LogDiagonalRouteSegments(routeRoot, MaxSegmentLinkDistance, CornerAxisThreshold);
        if (badSegments == 0)
            Debug.Log("[Traffic] Tidak ada segmen diagonal shortcut. Route mengikuti sumbu jalan.");
        else
            Debug.LogWarning(
                $"[Traffic] {badSegments} segmen diagonal shortcut (> {MaxSegmentLinkDistance}m). " +
                "Mobil bisa memotong blok di segmen ini.");
    }

    [MenuItem("HealthySim/Traffic/2 - Setup City Traffic (7 Vehicles)")]
    public static void SetupCityTraffic()
    {
        if (!IsSampleSceneActive())
        {
            Debug.LogWarning("[Traffic] Buka SampleScene dulu.");
            return;
        }

        Transform trafficRoot = FindOrCreateRoot(TrafficRootName);
        Transform routeRoot = trafficRoot.Find(RouteName);
        TrafficRoute route = routeRoot != null ? routeRoot.GetComponent<TrafficRoute>() : null;

        if (route == null || route.WaypointCount < 4 || route.TotalLength <= 1f)
        {
            Debug.LogWarning("[Traffic] Route belum ada. Menjalankan Generate Traffic Route From Roads...");
            GenerateTrafficRoute();
            routeRoot = trafficRoot.Find(RouteName);
            route = routeRoot != null ? routeRoot.GetComponent<TrafficRoute>() : null;
        }

        if (route == null || route.WaypointCount < 4)
        {
            Debug.LogError("[Traffic] Gagal menyiapkan route.");
            return;
        }

        Transform vehiclesRoot = FindOrCreateChild(trafficRoot, VehiclesRootName);
        ClearChildren(vehiclesRoot);

        CityTrafficManager manager = trafficRoot.GetComponent<CityTrafficManager>();
        if (manager == null)
            manager = trafficRoot.gameObject.AddComponent<CityTrafficManager>();

        List<TrafficVehicleController> spawned = new List<TrafficVehicleController>();
        float spacing = route.TotalLength / VehicleCount;

        for (int i = 0; i < VehicleCount; i++)
        {
            GameObject prefab = LoadVehiclePrefab(i);
            if (prefab == null)
                continue;

            GameObject vehicle = PrefabUtility.InstantiatePrefab(prefab, vehiclesRoot) as GameObject;
            if (vehicle == null)
                continue;

            vehicle.name = $"TrafficVehicle_{i + 1}";
            Undo.RegisterCreatedObjectUndo(vehicle, "Setup city traffic");

            TrafficVehicleController controller = vehicle.GetComponent<TrafficVehicleController>();
            if (controller == null)
                controller = vehicle.AddComponent<TrafficVehicleController>();

            VehicleColliderProfile profile = GetColliderProfile(prefab.name);
            controller.EnsureSolidCollider(profile.size, profile.center);
            controller.Configure(route, spacing * i, VehicleSpeed);

            EnsureRigidbody(vehicle);

            route.Sample(spacing * i, out Vector3 editorPos, out Vector3 editorFwd);
            editorPos.y += 0.15f;
            editorFwd.y = 0f;
            if (editorFwd.sqrMagnitude < 0.0001f)
                editorFwd = Vector3.forward;
            vehicle.transform.SetPositionAndRotation(editorPos, Quaternion.LookRotation(editorFwd.normalized, Vector3.up));

            spawned.Add(controller);
        }

        manager.SetMainRoute(route);
        manager.SetVehicles(spawned);

        EditorUtility.SetDirty(trafficRoot.gameObject);
        EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());

        Debug.Log($"[Traffic] {spawned.Count} solid vehicles spawned on MainCityRoute (visible in editor).");
    }

    private static List<Vector3> BuildRoutePoints(Transform roadLaneRoot)
    {
        List<Transform> laneGroups = CollectLaneGroups(roadLaneRoot);
        List<List<Vector3>> lanePaths = new List<List<Vector3>>();

        for (int i = 0; i < laneGroups.Count; i++)
        {
            List<Vector3> lanePoints = DedupePoints(CollectRoadSurfacePoints(laneGroups[i]), 1.5f);
            lanePoints = SortPointsAlongLane(lanePoints);
            if (lanePoints.Count >= 2)
                lanePaths.Add(lanePoints);
        }

        List<Vector3> path = StitchLanePathsIntoLoop(lanePaths);
        return DedupePoints(path, MinWaypointSpacing);
    }

    private static List<Vector3> SortPointsAlongLane(List<Vector3> points)
    {
        if (points.Count <= 1)
            return points;

        float minX = points[0].x;
        float maxX = points[0].x;
        float minZ = points[0].z;
        float maxZ = points[0].z;

        for (int i = 1; i < points.Count; i++)
        {
            minX = Mathf.Min(minX, points[i].x);
            maxX = Mathf.Max(maxX, points[i].x);
            minZ = Mathf.Min(minZ, points[i].z);
            maxZ = Mathf.Max(maxZ, points[i].z);
        }

        bool sortByZ = (maxZ - minZ) >= (maxX - minX);
        points.Sort((a, b) =>
        {
            int axisCompare = sortByZ
                ? a.z.CompareTo(b.z)
                : a.x.CompareTo(b.x);
            if (axisCompare != 0)
                return axisCompare;

            return sortByZ
                ? a.x.CompareTo(b.x)
                : a.z.CompareTo(b.z);
        });

        return points;
    }

    private static List<Vector3> StitchLanePathsIntoLoop(List<List<Vector3>> lanePaths)
    {
        List<Vector3> path = new List<Vector3>();
        if (lanePaths.Count == 0)
            return path;

        path.AddRange(lanePaths[0]);

        for (int i = 1; i < lanePaths.Count; i++)
        {
            List<Vector3> segment = ChooseBestOrientation(path[path.Count - 1], lanePaths[i]);
            AppendSegmentWithCorners(path, segment);
        }

        AppendClosingCorners(path);
        return path;
    }

    private static List<Vector3> ChooseBestOrientation(Vector3 tail, List<Vector3> lane)
    {
        if (lane.Count <= 1)
            return new List<Vector3>(lane);

        float forwardCost = EstimateConnectionCost(tail, lane[0]);
        float reverseCost = EstimateConnectionCost(tail, lane[lane.Count - 1]);
        return reverseCost < forwardCost ? ReverseCopy(lane) : new List<Vector3>(lane);
    }

    private static float EstimateConnectionCost(Vector3 from, Vector3 to)
    {
        float dx = Mathf.Abs(to.x - from.x);
        float dz = Mathf.Abs(to.z - from.z);
        if (dx <= CornerAxisThreshold || dz <= CornerAxisThreshold)
            return Vector3.Distance(from, to);

        return dx + dz;
    }

    private static List<Vector3> ReverseCopy(List<Vector3> points)
    {
        List<Vector3> reversed = new List<Vector3>(points.Count);
        for (int i = points.Count - 1; i >= 0; i--)
            reversed.Add(points[i]);

        return reversed;
    }

    private static void AppendSegmentWithCorners(List<Vector3> path, List<Vector3> segment)
    {
        if (segment.Count == 0)
            return;

        AddCornerWaypoints(path, path[path.Count - 1], segment[0]);
        path.AddRange(segment);
    }

    private static void AppendClosingCorners(List<Vector3> path)
    {
        if (path.Count < 2)
            return;

        AddCornerWaypoints(path, path[path.Count - 1], path[0]);
    }

    private static void AddCornerWaypoints(List<Vector3> path, Vector3 from, Vector3 to)
    {
        float dx = Mathf.Abs(to.x - from.x);
        float dz = Mathf.Abs(to.z - from.z);
        if (dx <= CornerAxisThreshold || dz <= CornerAxisThreshold)
            return;

        Vector3 cornerViaX = new Vector3(to.x, from.y, from.z);
        Vector3 cornerViaZ = new Vector3(from.x, from.y, to.z);

        float viaXLength = Vector3.Distance(from, cornerViaX) + Vector3.Distance(cornerViaX, to);
        float viaZLength = Vector3.Distance(from, cornerViaZ) + Vector3.Distance(cornerViaZ, to);
        Vector3 corner = viaXLength <= viaZLength ? cornerViaX : cornerViaZ;

        if (Vector3.Distance(from, corner) >= 0.5f)
            path.Add(corner);
    }

    private static Transform FindTrafficRouteRoot()
    {
        Transform trafficRoot = FindByName(TrafficRootName)?.transform;
        return trafficRoot != null ? trafficRoot.Find(RouteName) : null;
    }

    private static int LogDiagonalRouteSegments(
        Transform routeRoot,
        float maxAllowedDistance,
        float axisThreshold)
    {
        if (routeRoot == null)
            return 0;

        List<Vector3> points = new List<Vector3>();
        for (int i = 0; i < routeRoot.childCount; i++)
            points.Add(routeRoot.GetChild(i).position);

        if (points.Count < 2)
            return 0;

        int badSegments = 0;
        for (int i = 0; i < points.Count; i++)
        {
            int next = (i + 1) % points.Count;
            Vector3 a = points[i];
            Vector3 b = points[next];
            float dx = Mathf.Abs(b.x - a.x);
            float dz = Mathf.Abs(b.z - a.z);
            float distance = Vector3.Distance(a, b);
            if (dx <= axisThreshold || dz <= axisThreshold || distance <= maxAllowedDistance)
                continue;

            badSegments++;
            Debug.LogWarning(
                $"[Traffic] Shortcut diagonal {distance:0.#}m: Waypoint_{i + 1:000} -> Waypoint_{next + 1:000} " +
                $"({a.x:0.#}, {a.z:0.#}) -> ({b.x:0.#}, {b.z:0.#})",
                routeRoot.GetChild(i).gameObject);
        }

        return badSegments;
    }

    private static List<Transform> CollectLaneGroups(Transform roadLaneRoot)
    {
        List<Transform> groups = new List<Transform>();
        for (int i = 0; i < roadLaneRoot.childCount; i++)
        {
            Transform child = roadLaneRoot.GetChild(i);
            if (child == null || !child.name.StartsWith("RoadLane", StringComparison.OrdinalIgnoreCase))
                continue;

            groups.Add(child);
        }

        groups.Sort((a, b) => CompareRoadLaneNames(a.name, b.name));
        return groups;
    }

    private static int CompareRoadLaneNames(string a, string b)
    {
        int numberA = ExtractTrailingNumber(a);
        int numberB = ExtractTrailingNumber(b);
        return numberA.CompareTo(numberB);
    }

    private static int ExtractTrailingNumber(string value)
    {
        Match match = Regex.Match(value ?? string.Empty, @"(\d+)\s*$");
        return match.Success && int.TryParse(match.Groups[1].Value, out int number) ? number : int.MaxValue;
    }

    private static List<Vector3> CollectRoadSurfacePoints(Transform root)
    {
        List<Vector3> points = new List<Vector3>();
        MeshRenderer[] renderers = root.GetComponentsInChildren<MeshRenderer>(true);

        for (int i = 0; i < renderers.Length; i++)
        {
            MeshRenderer renderer = renderers[i];
            if (renderer == null)
                continue;

            string lowerName = renderer.gameObject.name.ToLowerInvariant();
            if (lowerName.Contains("sidewalk"))
                continue;

            Bounds bounds = renderer.bounds;
            Vector3 center = bounds.center;
            center.y = bounds.min.y + 0.05f;

            bool isLongZ = bounds.size.z > bounds.size.x;
            float laneOffset = (isLongZ ? bounds.size.x : bounds.size.z) * 0.18f;
            Vector3 offsetDir = isLongZ ? Vector3.right : Vector3.forward;
            center += offsetDir * laneOffset;

            points.Add(center);
        }

        return points;
    }

    private static List<Vector3> DedupePoints(List<Vector3> points, float minSpacing)
    {
        List<Vector3> result = new List<Vector3>();
        for (int i = 0; i < points.Count; i++)
        {
            if (result.Count == 0 || Vector3.Distance(result[result.Count - 1], points[i]) >= minSpacing)
                result.Add(points[i]);
        }

        return result;
    }

    private static List<Transform> CreateWaypointTransforms(Transform parent, List<Vector3> points)
    {
        List<Transform> transforms = new List<Transform>(points.Count);
        for (int i = 0; i < points.Count; i++)
        {
            GameObject waypoint = new GameObject($"Waypoint_{i + 1:000}");
            Undo.RegisterCreatedObjectUndo(waypoint, "Create traffic waypoint");
            waypoint.transform.SetParent(parent, false);
            waypoint.transform.position = points[i];
            transforms.Add(waypoint.transform);
        }

        return transforms;
    }

    private static GameObject LoadVehiclePrefab(int index)
    {
        string path = VehiclePrefabPaths[Mathf.Clamp(index, 0, VehiclePrefabPaths.Length - 1)];
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null)
            Debug.LogWarning($"[Traffic] Prefab tidak ditemukan: {path}");

        return prefab;
    }

    private struct VehicleColliderProfile
    {
        public Vector3 size;
        public Vector3 center;
    }

    private static VehicleColliderProfile GetColliderProfile(string prefabName)
    {
        string lower = prefabName.ToLowerInvariant();
        if (lower.Contains("bus"))
            return new VehicleColliderProfile { size = new Vector3(2.4f, 2f, 8f), center = new Vector3(0f, 1f, 0f) };

        if (lower.Contains("truck") || lower.Contains("container"))
            return new VehicleColliderProfile { size = new Vector3(2.2f, 1.6f, 5.5f), center = new Vector3(0f, 0.8f, 0f) };

        return new VehicleColliderProfile { size = new Vector3(2f, 1.2f, 4.2f), center = new Vector3(0f, 0.6f, 0f) };
    }

    private static void EnsureRigidbody(GameObject vehicle)
    {
        Rigidbody body = vehicle.GetComponent<Rigidbody>();
        if (body == null)
            body = vehicle.AddComponent<Rigidbody>();

        body.isKinematic = true;
        body.useGravity = false;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
    }

    private static Transform FindRoadLaneRoot()
    {
        GameObject roadObject = FindByName("RoadObject");
        if (roadObject == null)
            return null;

        Transform roadLane = roadObject.transform.Find("RoadLane");
        return roadLane;
    }

    private static Transform FindOrCreateRoot(string name)
    {
        GameObject existing = FindByName(name);
        if (existing != null)
            return existing.transform;

        GameObject root = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(root, "Create traffic root");
        return root.transform;
    }

    private static Transform FindOrCreateChild(Transform parent, string childName)
    {
        Transform existing = parent.Find(childName);
        if (existing != null)
            return existing;

        GameObject child = new GameObject(childName);
        Undo.RegisterCreatedObjectUndo(child, "Create traffic child");
        child.transform.SetParent(parent, false);
        return child.transform;
    }

    private static void ClearChildren(Transform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            GameObject child = parent.GetChild(i).gameObject;
            Undo.DestroyObjectImmediate(child);
        }
    }

    private static bool IsSampleSceneActive()
    {
        return string.Equals(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().name,
            "SampleScene",
            StringComparison.OrdinalIgnoreCase);
    }

    private static GameObject FindByName(string objectName)
    {
        Transform[] all = UnityEngine.Object.FindObjectsByType<Transform>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null && all[i].name == objectName)
                return all[i].gameObject;
        }

        return null;
    }
}
#endif
