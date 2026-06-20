using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Closed or open polyline path for ambient city traffic.
/// </summary>
[DisallowMultipleComponent]
public class TrafficRoute : MonoBehaviour
{
    [SerializeField] private List<Transform> waypoints = new List<Transform>();
    [SerializeField] private bool closedLoop = true;

    private readonly List<float> segmentLengths = new List<float>();
    private float totalLength;

    public bool ClosedLoop => closedLoop;
    public int WaypointCount => waypoints.Count;
    public float TotalLength => totalLength;

    void Awake()
    {
        Recalculate();
    }

    void OnValidate()
    {
        Recalculate();
    }

    public void SetWaypoints(IReadOnlyList<Transform> points, bool loop)
    {
        waypoints.Clear();
        if (points != null)
        {
            for (int i = 0; i < points.Count; i++)
            {
                if (points[i] != null)
                    waypoints.Add(points[i]);
            }
        }

        closedLoop = loop;
        Recalculate();
    }

    public void Recalculate()
    {
        segmentLengths.Clear();
        totalLength = 0f;

        if (waypoints.Count < 2)
            return;

        int segmentCount = closedLoop ? waypoints.Count : waypoints.Count - 1;
        for (int i = 0; i < segmentCount; i++)
        {
            Transform a = waypoints[i];
            Transform b = waypoints[(i + 1) % waypoints.Count];
            if (a == null || b == null)
            {
                segmentLengths.Add(0f);
                continue;
            }

            float length = Vector3.Distance(a.position, b.position);
            segmentLengths.Add(length);
            totalLength += length;
        }
    }

    public void Sample(float distance, out Vector3 position, out Vector3 forward)
    {
        position = transform.position;
        forward = transform.forward;

        if (waypoints.Count == 0 || totalLength <= 0.001f)
            return;

        distance = WrapDistance(distance);

        float traveled = 0f;
        for (int i = 0; i < segmentLengths.Count; i++)
        {
            float segment = segmentLengths[i];
            if (segment <= 0.001f)
                continue;

            if (traveled + segment >= distance)
            {
                Transform a = waypoints[i];
                Transform b = waypoints[(i + 1) % waypoints.Count];
                if (a == null || b == null)
                    return;

                float t = (distance - traveled) / segment;
                position = Vector3.Lerp(a.position, b.position, t);
                forward = (b.position - a.position).normalized;
                if (forward.sqrMagnitude < 0.0001f)
                    forward = transform.forward;
                return;
            }

            traveled += segment;
        }

        Transform last = waypoints[waypoints.Count - 1];
        if (last != null)
            position = last.position;
    }

    private float WrapDistance(float distance)
    {
        if (totalLength <= 0.001f)
            return 0f;

        if (!closedLoop)
            return Mathf.Clamp(distance, 0f, totalLength);

        distance %= totalLength;
        if (distance < 0f)
            distance += totalLength;

        return distance;
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (waypoints == null || waypoints.Count < 2)
            return;

        Gizmos.color = new Color(0.2f, 0.7f, 1f, 0.9f);
        for (int i = 0; i < waypoints.Count; i++)
        {
            Transform a = waypoints[i];
            Transform b = waypoints[(i + 1) % waypoints.Count];
            if (a == null || b == null)
                continue;

            Gizmos.DrawLine(a.position, b.position);
            Gizmos.DrawSphere(a.position, 0.35f);

            if (!closedLoop && i == waypoints.Count - 2)
                Gizmos.DrawSphere(b.position, 0.35f);
        }
    }
#endif
}
