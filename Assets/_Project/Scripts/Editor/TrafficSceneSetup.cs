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
    private const float MinWaypointSpacing = 2.5f;
    private const float AxisAlignEpsilon = 0.25f;
    private const float MaxLaneConnectDistance = 80f;
    private const float RoadSnapMaxDistance = 10f;
    private const float RoadGraphAttachMaxDistance = 20f;
    private const float MaxRoadGraphEdgeLength = 35f;
    private const float RoadIntersectionClusterDistance = 12f;
    private const float VehicleProbeRadius = 1.05f;
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

        int badSegments = LogNonAxisAlignedSegments(routeRoot);
        int blockedSegments = LogBuildingBlockedSegments(routeRoot);
        Debug.Log(
            $"[Traffic] Route generated: {routePoints.Count} waypoints, length ~{route.TotalLength:0.#}m, " +
            $"nonAxisSegments={badSegments}, buildingBlocked={blockedSegments}.");
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

        int badSegments = LogNonAxisAlignedSegments(routeRoot);
        int blockedSegments = LogBuildingBlockedSegments(routeRoot);
        if (badSegments == 0 && blockedSegments == 0)
            Debug.Log("[Traffic] Route axis-aligned dan tidak terdeteksi memotong gedung.");
        else
            Debug.LogWarning(
                $"[Traffic] nonAxisSegments={badSegments}, buildingBlocked={blockedSegments}. " +
                "Periksa RoadLane atau regenerate setelah layout jalan diperbaiki.");
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
        List<Vector3> allRoadPoints = new List<Vector3>();
        List<int> roadLaneIndex = new List<int>();
        List<int> roadOrderInLane = new List<int>();

        for (int i = 0; i < laneGroups.Count; i++)
        {
            List<Vector3> lanePoints = DedupePoints(CollectRoadSurfacePoints(laneGroups[i]), 1.25f);
            lanePoints = SortPointsAlongLane(lanePoints);
            lanePoints = SanitizePathAxisAlignment(lanePoints);
            if (lanePoints.Count < 2)
                continue;

            int lanePathIndex = lanePaths.Count;
            lanePaths.Add(lanePoints);

            for (int order = 0; order < lanePoints.Count; order++)
            {
                allRoadPoints.Add(lanePoints[order]);
                roadLaneIndex.Add(lanePathIndex);
                roadOrderInLane.Add(order);
            }
        }

        List<Vector3> path = ChainLanePathsByProximity(
            lanePaths, allRoadPoints, roadLaneIndex, roadOrderInLane);
        path = SanitizePathAxisAlignment(path);
        path = SnapWaypointsToNearestRoad(path, allRoadPoints);
        path = DedupePoints(path, MinWaypointSpacing);
        return SanitizePathAxisAlignment(path);
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

    private static List<Vector3> ChainLanePathsByProximity(
        List<List<Vector3>> lanePaths,
        List<Vector3> roadPoints,
        List<int> roadLaneIndex,
        List<int> roadOrderInLane)
    {
        List<Vector3> path = new List<Vector3>();
        if (lanePaths.Count == 0)
            return path;

        List<List<Vector3>> remaining = new List<List<Vector3>>(lanePaths);
        path.AddRange(remaining[0]);
        remaining.RemoveAt(0);

        while (remaining.Count > 0)
        {
            Vector3 tail = path[path.Count - 1];
            int bestLane = -1;
            bool bestReversed = false;
            float bestScore = float.MaxValue;

            for (int i = 0; i < remaining.Count; i++)
            {
                List<Vector3> lane = remaining[i];
                if (lane.Count == 0)
                    continue;

                EvaluateLaneConnection(
                    tail, lane, false, roadPoints, roadLaneIndex, roadOrderInLane,
                    out float scoreForward, out _);
                if (scoreForward < bestScore)
                {
                    bestScore = scoreForward;
                    bestLane = i;
                    bestReversed = false;
                }

                EvaluateLaneConnection(
                    tail, lane, true, roadPoints, roadLaneIndex, roadOrderInLane,
                    out float scoreReverse, out _);
                if (scoreReverse < bestScore)
                {
                    bestScore = scoreReverse;
                    bestLane = i;
                    bestReversed = true;
                }
            }

            if (bestLane < 0)
                break;

            float connectDist = Vector3.Distance(
                tail,
                bestReversed ? remaining[bestLane][remaining[bestLane].Count - 1] : remaining[bestLane][0]);

            List<Vector3> nextLane = bestReversed
                ? ReverseCopy(remaining[bestLane])
                : new List<Vector3>(remaining[bestLane]);
            remaining.RemoveAt(bestLane);

            Vector3 connectPoint = nextLane[0];
            List<Vector3> bridge = FindRoadGraphPath(
                tail, connectPoint, roadPoints, roadLaneIndex, roadOrderInLane, logFallback: true);

            if (bridge.Count <= 2 && connectDist > MaxLaneConnectDistance)
            {
                Debug.LogWarning(
                    $"[Traffic] Lane jump {connectDist:0.#}m memakai fallback Manhattan. " +
                    "Periksa konektivitas RoadLane.");
            }

            AppendBridgePath(path, bridge);
            for (int i = 0; i < nextLane.Count; i++)
            {
                if (path.Count == 0 || Vector3.Distance(path[path.Count - 1], nextLane[i]) > AxisAlignEpsilon)
                    path.Add(nextLane[i]);
            }
        }

        if (path.Count >= 2)
        {
            List<Vector3> closingBridge = FindRoadGraphPath(
                path[path.Count - 1], path[0], roadPoints, roadLaneIndex, roadOrderInLane, logFallback: true);
            AppendBridgePath(path, closingBridge);
        }

        return path;
    }

    private static void EvaluateLaneConnection(
        Vector3 tail,
        List<Vector3> lane,
        bool reversed,
        List<Vector3> roadPoints,
        List<int> roadLaneIndex,
        List<int> roadOrderInLane,
        out float score,
        out List<Vector3> bridge)
    {
        Vector3 connectPoint = reversed ? lane[lane.Count - 1] : lane[0];
        bridge = FindRoadGraphPath(
            tail, connectPoint, roadPoints, roadLaneIndex, roadOrderInLane, logFallback: false);
        score = MeasurePolylineLength(bridge);

        int startIdx = FindNearestRoadIndex(tail, roadPoints);
        int goalIdx = FindNearestRoadIndex(connectPoint, roadPoints);
        if (startIdx >= 0 && goalIdx >= 0)
        {
            int[] parent = BfsRoadGraph(startIdx, goalIdx, roadPoints, roadLaneIndex, roadOrderInLane);
            if (parent[goalIdx] < 0)
                score += 2000f;
        }
        else
        {
            score += 2000f;
        }

        if (!IsPolylineClearOfBuildings(bridge))
            score += 500f;

        float directDist = Vector3.Distance(tail, connectPoint);
        if (directDist > MaxLaneConnectDistance)
            score += 1000f;
    }

    private static void AppendBridgePath(List<Vector3> path, List<Vector3> bridge)
    {
        if (bridge == null || bridge.Count == 0)
            return;

        int startIndex = 1;
        if (bridge.Count == 1)
            startIndex = 0;

        for (int i = startIndex; i < bridge.Count; i++)
        {
            Vector3 point = bridge[i];
            if (path.Count == 0 || Vector3.Distance(path[path.Count - 1], point) > AxisAlignEpsilon)
                path.Add(point);
        }
    }

    private static List<Vector3> FindRoadGraphPath(
        Vector3 from,
        Vector3 to,
        List<Vector3> roadPoints,
        List<int> roadLaneIndex,
        List<int> roadOrderInLane,
        bool logFallback)
    {
        if (roadPoints == null || roadPoints.Count == 0)
            return BuildBestManhattanSegment(from, to);

        int startIdx = FindNearestRoadIndex(from, roadPoints);
        int goalIdx = FindNearestRoadIndex(to, roadPoints);
        if (startIdx < 0 || goalIdx < 0)
        {
            if (logFallback)
                Debug.LogWarning("[Traffic] Road graph attach gagal; fallback Manhattan singkat.");

            return BuildBestManhattanSegment(from, to);
        }

        if (startIdx == goalIdx)
            return new List<Vector3> { from, to };

        int[] parent = BfsRoadGraph(startIdx, goalIdx, roadPoints, roadLaneIndex, roadOrderInLane);
        if (parent[goalIdx] < 0)
        {
            if (logFallback)
            {
                Debug.LogWarning(
                    $"[Traffic] Road graph tidak terhubung ({Vector3.Distance(from, to):0.#}m); fallback Manhattan.");
            }

            return BuildBestManhattanSegment(from, to);
        }

        List<Vector3> graphPath = ReconstructRoadGraphPath(parent, startIdx, goalIdx, roadPoints);
        if (graphPath.Count == 0)
            return BuildBestManhattanSegment(from, to);

        graphPath[0] = from;
        graphPath[graphPath.Count - 1] = to;
        return SanitizePathAxisAlignment(graphPath);
    }

    private static int FindNearestRoadIndex(Vector3 point, List<Vector3> roadPoints)
    {
        int bestIndex = -1;
        float bestDist = RoadGraphAttachMaxDistance;

        for (int i = 0; i < roadPoints.Count; i++)
        {
            float dist = Vector3.Distance(
                new Vector3(point.x, 0f, point.z),
                new Vector3(roadPoints[i].x, 0f, roadPoints[i].z));

            if (dist < bestDist)
            {
                bestDist = dist;
                bestIndex = i;
            }
        }

        return bestIndex;
    }

    private static int[] BfsRoadGraph(
        int start,
        int goal,
        List<Vector3> nodes,
        List<int> laneIndex,
        List<int> orderInLane)
    {
        int count = nodes.Count;
        int[] parent = new int[count];
        bool[] visited = new bool[count];

        for (int i = 0; i < count; i++)
            parent[i] = -1;

        Queue<int> queue = new Queue<int>();
        queue.Enqueue(start);
        visited[start] = true;

        while (queue.Count > 0)
        {
            int current = queue.Dequeue();
            if (current == goal)
                break;

            for (int next = 0; next < count; next++)
            {
                if (visited[next] ||
                    !AreRoadGraphNeighbors(current, next, nodes, laneIndex, orderInLane))
                    continue;

                visited[next] = true;
                parent[next] = current;
                queue.Enqueue(next);
            }
        }

        return parent;
    }

    private static bool AreRoadGraphNeighbors(
        int indexA,
        int indexB,
        List<Vector3> nodes,
        List<int> laneIndex,
        List<int> orderInLane)
    {
        Vector3 a = nodes[indexA];
        Vector3 b = nodes[indexB];
        float distance = Vector3.Distance(a, b);
        if (distance <= RoadIntersectionClusterDistance)
            return true;

        if (laneIndex[indexA] == laneIndex[indexB] &&
            Mathf.Abs(orderInLane[indexA] - orderInLane[indexB]) == 1)
            return true;

        if (distance > MaxRoadGraphEdgeLength)
            return false;

        return IsAxisAlignedSegment(a, b);
    }

    private static List<Vector3> ReconstructRoadGraphPath(int[] parent, int start, int goal, List<Vector3> nodes)
    {
        List<Vector3> path = new List<Vector3>();
        int current = goal;
        while (current >= 0)
        {
            path.Add(nodes[current]);
            if (current == start)
                break;

            current = parent[current];
        }

        if (path.Count == 0 || Vector3.Distance(path[path.Count - 1], nodes[start]) > AxisAlignEpsilon)
            return new List<Vector3>();

        path.Reverse();
        return path;
    }

    private static List<Vector3> SanitizePathAxisAlignment(List<Vector3> path)
    {
        if (path.Count < 2)
            return path;

        List<Vector3> result = new List<Vector3> { path[0] };
        for (int i = 1; i < path.Count; i++)
            ManhattanConnectIntoPath(result, result[result.Count - 1], path[i]);

        return result;
    }

    private static void ManhattanConnectIntoPath(List<Vector3> path, Vector3 from, Vector3 to)
    {
        if (Vector3.Distance(from, to) <= AxisAlignEpsilon)
            return;

        List<Vector3> segment = BuildBestManhattanSegment(from, to);
        for (int i = 1; i < segment.Count; i++)
        {
            Vector3 point = segment[i];
            if (path.Count == 0 || Vector3.Distance(path[path.Count - 1], point) > AxisAlignEpsilon)
                path.Add(point);
        }
    }

    private static List<Vector3> BuildBestManhattanSegment(Vector3 from, Vector3 to)
    {
        List<Vector3> viaXFirst = BuildManhattanSegment(from, to, cornerViaXFirst: true);
        List<Vector3> viaZFirst = BuildManhattanSegment(from, to, cornerViaXFirst: false);

        bool viaXClear = IsPolylineClearOfBuildings(viaXFirst);
        bool viaZClear = IsPolylineClearOfBuildings(viaZFirst);

        if (viaXClear && !viaZClear)
            return viaXFirst;
        if (viaZClear && !viaXClear)
            return viaZFirst;

        float viaXLength = MeasurePolylineLength(viaXFirst);
        float viaZLength = MeasurePolylineLength(viaZFirst);
        return viaXLength <= viaZLength ? viaXFirst : viaZFirst;
    }

    private static List<Vector3> BuildManhattanSegment(Vector3 from, Vector3 to, bool cornerViaXFirst)
    {
        List<Vector3> segment = new List<Vector3> { from };

        float dx = Mathf.Abs(to.x - from.x);
        float dz = Mathf.Abs(to.z - from.z);
        if (dx <= AxisAlignEpsilon || dz <= AxisAlignEpsilon)
        {
            segment.Add(to);
            return segment;
        }

        Vector3 corner = cornerViaXFirst
            ? new Vector3(to.x, from.y, from.z)
            : new Vector3(from.x, from.y, to.z);

        if (Vector3.Distance(from, corner) > AxisAlignEpsilon)
            segment.Add(corner);

        segment.Add(to);
        return segment;
    }

    private static float MeasurePolylineLength(List<Vector3> points)
    {
        float length = 0f;
        for (int i = 1; i < points.Count; i++)
            length += Vector3.Distance(points[i - 1], points[i]);

        return length;
    }

    private static bool IsPolylineClearOfBuildings(List<Vector3> points)
    {
        for (int i = 1; i < points.Count; i++)
        {
            if (IsSegmentBlockedByBuildings(points[i - 1], points[i]))
                return false;
        }

        return true;
    }

    private static bool IsSegmentBlockedByBuildings(Vector3 from, Vector3 to)
    {
        Vector3 start = from + Vector3.up * 0.85f;
        Vector3 end = to + Vector3.up * 0.85f;
        Vector3 delta = end - start;
        float distance = delta.magnitude;
        if (distance <= AxisAlignEpsilon)
            return false;

        Vector3 direction = delta / distance;
        if (Physics.SphereCast(start, VehicleProbeRadius, direction, out RaycastHit hit, distance))
            return IsBuildingCollider(hit.collider);

        return false;
    }

    private static bool IsBuildingCollider(Collider collider)
    {
        if (collider == null)
            return false;

        if (IsRoadRelatedCollider(collider) || IsTrafficRelatedCollider(collider))
            return false;

        Transform node = collider.transform;
        while (node != null)
        {
            string lower = node.name.ToLowerInvariant();
            if (lower.Contains("road") || lower.Contains("lane") || lower.Contains("sidewalk") ||
                lower.Contains("ground") || lower.Contains("terrain") || lower.Contains("grass") ||
                lower.Contains("garden") || lower.Contains("player") || lower.Contains("traffic"))
                return false;

            node = node.parent;
        }

        return true;
    }

    private static bool IsRoadRelatedCollider(Collider collider)
    {
        Transform node = collider.transform;
        while (node != null)
        {
            string lower = node.name.ToLowerInvariant();
            if (lower.Contains("roadobject") || lower.Contains("roadlane") || lower.Contains("sidewalk"))
                return true;

            node = node.parent;
        }

        return false;
    }

    private static bool IsTrafficRelatedCollider(Collider collider)
    {
        Transform node = collider.transform;
        while (node != null)
        {
            if (string.Equals(node.name, TrafficRootName, StringComparison.OrdinalIgnoreCase))
                return true;

            node = node.parent;
        }

        return false;
    }

    private static List<Vector3> SnapWaypointsToNearestRoad(List<Vector3> path, List<Vector3> roadPoints)
    {
        if (path.Count == 0 || roadPoints.Count == 0)
            return path;

        List<Vector3> snapped = new List<Vector3>(path.Count);
        for (int i = 0; i < path.Count; i++)
            snapped.Add(SnapPointToNearestRoad(path[i], roadPoints));

        return snapped;
    }

    private static Vector3 SnapPointToNearestRoad(Vector3 point, List<Vector3> roadPoints)
    {
        Vector3 best = point;
        float bestDist = RoadSnapMaxDistance;

        for (int i = 0; i < roadPoints.Count; i++)
        {
            Vector3 candidate = roadPoints[i];
            float dist = Vector3.Distance(
                new Vector3(point.x, 0f, point.z),
                new Vector3(candidate.x, 0f, candidate.z));

            if (dist < bestDist)
            {
                bestDist = dist;
                best = new Vector3(candidate.x, candidate.y, candidate.z);
            }
        }

        if (Physics.Raycast(best + Vector3.up * 40f, Vector3.down, out RaycastHit hit, 80f))
        {
            if (IsRoadRelatedCollider(hit.collider))
                best.y = hit.point.y + 0.05f;
        }

        return best;
    }

    private static bool IsAxisAlignedSegment(Vector3 a, Vector3 b)
    {
        float dx = Mathf.Abs(b.x - a.x);
        float dz = Mathf.Abs(b.z - a.z);
        return dx <= AxisAlignEpsilon || dz <= AxisAlignEpsilon;
    }

    private static List<Vector3> ReverseCopy(List<Vector3> points)
    {
        List<Vector3> reversed = new List<Vector3>(points.Count);
        for (int i = points.Count - 1; i >= 0; i--)
            reversed.Add(points[i]);

        return reversed;
    }

    private static int LogNonAxisAlignedSegments(Transform routeRoot)
    {
        if (routeRoot == null)
            return 0;

        List<Vector3> points = CollectRoutePointPositions(routeRoot);
        if (points.Count < 2)
            return 0;

        int badSegments = 0;
        for (int i = 0; i < points.Count; i++)
        {
            int next = (i + 1) % points.Count;
            Vector3 a = points[i];
            Vector3 b = points[next];
            if (IsAxisAlignedSegment(a, b))
                continue;

            badSegments++;
            Debug.LogWarning(
                $"[Traffic] Non-axis segment {Vector3.Distance(a, b):0.#}m: Waypoint_{i + 1:000} -> Waypoint_{next + 1:000} " +
                $"({a.x:0.#}, {a.z:0.#}) -> ({b.x:0.#}, {b.z:0.#})",
                routeRoot.GetChild(i).gameObject);
        }

        return badSegments;
    }

    private static int LogBuildingBlockedSegments(Transform routeRoot)
    {
        if (routeRoot == null)
            return 0;

        List<Vector3> points = CollectRoutePointPositions(routeRoot);
        if (points.Count < 2)
            return 0;

        int blockedSegments = 0;
        for (int i = 0; i < points.Count; i++)
        {
            int next = (i + 1) % points.Count;
            if (!IsSegmentBlockedByBuildings(points[i], points[next]))
                continue;

            blockedSegments++;
            Debug.LogWarning(
                $"[Traffic] Building overlap: Waypoint_{i + 1:000} -> Waypoint_{next + 1:000}",
                routeRoot.GetChild(i).gameObject);
        }

        return blockedSegments;
    }

    private static List<Vector3> CollectRoutePointPositions(Transform routeRoot)
    {
        List<Vector3> points = new List<Vector3>();
        for (int i = 0; i < routeRoot.childCount; i++)
            points.Add(routeRoot.GetChild(i).position);

        return points;
    }

    private static Transform FindTrafficRouteRoot()
    {
        Transform trafficRoot = FindByName(TrafficRootName)?.transform;
        return trafficRoot != null ? trafficRoot.Find(RouteName) : null;
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
            bool isLongZ = bounds.size.z > bounds.size.x;
            float primaryLength = isLongZ ? bounds.size.z : bounds.size.x;
            int samples = Mathf.Max(1, Mathf.CeilToInt(primaryLength / 8f));

            for (int sample = 0; sample < samples; sample++)
            {
                float t = samples == 1 ? 0.5f : sample / (float)(samples - 1);
                Vector3 center = bounds.center;
                center.y = bounds.min.y + 0.05f;

                if (isLongZ)
                    center.z = bounds.min.z + bounds.size.z * t;
                else
                    center.x = bounds.min.x + bounds.size.x * t;

                points.Add(center);
            }
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
