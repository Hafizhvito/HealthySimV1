using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[DisallowMultipleComponent]
public class TrafficVehicleController : MonoBehaviour
{
    [SerializeField] private TrafficRoute route;
    [SerializeField] private float speed = 6f;
    [SerializeField] private float groundClearance = 0.15f;
    [SerializeField] private float lookAheadDistance = 5f;
    [SerializeField] private float maxTurnDegreesPerSecond = 110f;
    [SerializeField] private float cornerTurnBoost = 1.3f;
    [SerializeField] private float cornerAngleThreshold = 35f;
    [SerializeField] private float routeProgress;

    private Rigidbody body;

    public TrafficRoute Route
    {
        get => route;
        set => route = value;
    }

    public float RouteProgress
    {
        get => routeProgress;
        set => routeProgress = value;
    }

    void Awake()
    {
        body = GetComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
    }

    void Start()
    {
        SnapToRoutePosition();
    }

    void FixedUpdate()
    {
        if (route == null || route.WaypointCount < 2 || route.TotalLength <= 0.001f)
            return;

        routeProgress += speed * Time.fixedDeltaTime;
        ApplyRoutePosition();
    }

    public void Configure(TrafficRoute assignedRoute, float startProgress, float driveSpeed)
    {
        route = assignedRoute;
        routeProgress = startProgress;
        speed = driveSpeed;
    }

    public void SnapToRoutePosition()
    {
        if (route == null || route.WaypointCount < 2 || route.TotalLength <= 0.001f)
            return;

        route.Sample(routeProgress, out Vector3 pos, out Vector3 fwd);
        pos.y += groundClearance;

        fwd.y = 0f;
        if (fwd.sqrMagnitude < 0.0001f)
            fwd = Vector3.forward;

        transform.SetPositionAndRotation(pos, Quaternion.LookRotation(fwd.normalized, Vector3.up));
    }

    public void EnsureSolidCollider(Vector3 size, Vector3 center)
    {
        BoxCollider box = GetComponent<BoxCollider>();
        if (box == null)
            box = gameObject.AddComponent<BoxCollider>();

        box.isTrigger = false;
        box.size = size;
        box.center = center;
    }

    private void ApplyRoutePosition()
    {
        route.Sample(routeProgress, out Vector3 targetPosition, out Vector3 segmentForward);
        targetPosition.y += groundClearance;

        Vector3 flatForward = GetLookAheadForward(targetPosition, segmentForward);
        Quaternion targetRotation = Quaternion.LookRotation(flatForward, Vector3.up);

        float turnRate = maxTurnDegreesPerSecond;
        float cornerAngle = Vector3.Angle(body.rotation * Vector3.forward, flatForward);
        if (cornerAngle >= cornerAngleThreshold)
            turnRate *= cornerTurnBoost;

        Quaternion smoothedRotation = Quaternion.RotateTowards(
            body.rotation,
            targetRotation,
            turnRate * Time.fixedDeltaTime);

        body.MovePosition(targetPosition);
        body.MoveRotation(smoothedRotation);
    }

    private Vector3 GetLookAheadForward(Vector3 currentPosition, Vector3 segmentForward)
    {
        Vector3 flatSegmentForward = segmentForward;
        flatSegmentForward.y = 0f;

        if (flatSegmentForward.sqrMagnitude < 0.0001f)
            flatSegmentForward = transform.forward;

        flatSegmentForward.Normalize();

        float aheadDistance = Mathf.Clamp(lookAheadDistance, 1f, Mathf.Max(1f, route.TotalLength * 0.1f));
        route.Sample(routeProgress + aheadDistance, out Vector3 aheadPosition, out _);

        Vector3 lookDirection = aheadPosition - currentPosition;
        lookDirection.y = 0f;
        if (lookDirection.sqrMagnitude < 0.01f)
            return flatSegmentForward;

        return lookDirection.normalized;
    }
}
