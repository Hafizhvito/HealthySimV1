using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class NpcWanderController : MonoBehaviour
{
    [Header("Wander Zone")]
    [SerializeField] private float wanderRadius = 15f;
    [SerializeField] private bool useCustomOrigin;
    [SerializeField] private Vector3 customOriginWorld;

    [Header("Timing")]
    [SerializeField] private float idleTimeMin = 2f;
    [SerializeField] private float idleTimeMax = 6f;

    [Header("Movement")]
    [SerializeField] private float walkSpeed = 1.2f;
    [SerializeField] private float rotationSpeed = 360f;
    [SerializeField] private float stoppingDistance = 0.4f;
    [SerializeField] private float facingThreshold = 25f;
    [Tooltip("Compensates NpcVisualModelSlot faceNegativeZ (180°) so model faces movement.")]
    [SerializeField] private float visualYawOffset = 180f;

    private NavMeshAgent agent;
    private Vector3 origin;
    private float idleTimer;
    private float retrySnapTimer;
    private bool paused;

    public bool IsWalking { get; private set; }
    public bool IsTurning { get; private set; }

    private enum State { Idle, Turning, Walking }
    private State state = State.Idle;

    private bool AgentReady => agent != null && agent.isActiveAndEnabled && agent.enabled && agent.isOnNavMesh;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        agent.speed = walkSpeed;
        agent.angularSpeed = 0f;
        agent.stoppingDistance = stoppingDistance;
        agent.autoBraking = true;
        agent.updateRotation = false;

        if (!agent.isOnNavMesh)
            agent.enabled = false;
    }

    void Start()
    {
        origin = useCustomOrigin ? customOriginWorld : transform.position;
        idleTimer = Random.Range(idleTimeMin, idleTimeMax);

        if (!TrySnapToNavMesh())
        {
            Debug.LogWarning($"[NpcWander] {name} not on NavMesh. " +
                "Bake via HealthySim > City NPCs > 3 - Bake City NavMesh.", this);
        }
    }

    void Update()
    {
        if (paused)
        {
            IsWalking = false;
            IsTurning = false;
            return;
        }

        if (!AgentReady)
        {
            IsWalking = false;
            IsTurning = false;
            state = State.Idle;
            DisableAgentIfOffMesh();

            retrySnapTimer -= Time.deltaTime;
            if (retrySnapTimer <= 0f)
            {
                TrySnapToNavMesh();
                retrySnapTimer = 1f;
            }
            return;
        }

        switch (state)
        {
            case State.Idle:
                IsWalking = false;
                IsTurning = false;
                SetAgentStopped(true);
                idleTimer -= Time.deltaTime;
                if (idleTimer <= 0f)
                {
                    if (TryPickDestination())
                    {
                        state = State.Turning;
                        SetAgentStopped(true);
                    }
                    else
                    {
                        idleTimer = 1f;
                    }
                }
                break;

            case State.Turning:
                IsWalking = false;
                IsTurning = true;
                if (RotateTowardDestination())
                {
                    state = State.Walking;
                    SetAgentStopped(false);
                }
                break;

            case State.Walking:
                IsWalking = true;
                IsTurning = false;
                RotateTowardVelocity();

                if (HasReachedDestination())
                {
                    state = State.Idle;
                    idleTimer = Random.Range(idleTimeMin, idleTimeMax);
                    IsWalking = false;
                    SetAgentStopped(true);
                }
                break;
        }
    }

    private void SetAgentStopped(bool stopped)
    {
        if (agent == null || !agent.isActiveAndEnabled)
            return;

        if (!agent.isOnNavMesh)
        {
            agent.enabled = false;
            return;
        }

        if (!agent.enabled)
            agent.enabled = true;

        agent.isStopped = stopped;
    }

    private bool HasReachedDestination()
    {
        if (!AgentReady)
            return false;

        if (agent.pathPending || !agent.hasPath)
            return false;

        float remaining = agent.remainingDistance;
        if (float.IsInfinity(remaining))
            return false;

        return remaining <= agent.stoppingDistance;
    }

    private bool RotateTowardDestination()
    {
        if (!AgentReady)
            return false;

        if (!agent.hasPath || agent.pathStatus == NavMeshPathStatus.PathInvalid)
            return true;

        Vector3 nextPoint = agent.steeringTarget;
        Vector3 dir = nextPoint - transform.position;
        dir.y = 0f;

        if (dir.sqrMagnitude < 0.01f)
            return true;

        Quaternion targetRot = FacingRotation(dir);
        transform.rotation = Quaternion.RotateTowards(
            transform.rotation, targetRot, rotationSpeed * Time.deltaTime);

        float angle = Quaternion.Angle(transform.rotation, targetRot);
        return angle <= facingThreshold;
    }

    private void RotateTowardVelocity()
    {
        if (!AgentReady)
            return;

        Vector3 velocity = agent.velocity;
        velocity.y = 0f;
        if (velocity.sqrMagnitude < 0.01f)
            return;

        Quaternion targetRot = FacingRotation(velocity);
        transform.rotation = Quaternion.RotateTowards(
            transform.rotation, targetRot, rotationSpeed * Time.deltaTime);
    }

    private bool TryPickDestination()
    {
        if (!AgentReady)
            return false;

        for (int attempt = 0; attempt < 8; attempt++)
        {
            Vector2 circle = Random.insideUnitCircle * wanderRadius;
            Vector3 candidate = origin + new Vector3(circle.x, 0f, circle.y);

            NavMeshHit hit;
            if (NavMesh.SamplePosition(candidate, out hit, 2f, NavMesh.AllAreas))
            {
                agent.SetDestination(hit.position);
                return true;
            }
        }
        return false;
    }

    private bool TrySnapToNavMesh()
    {
        if (agent == null)
            return false;

        NavMeshHit hit;
        if (!NavMesh.SamplePosition(transform.position, out hit, 20f, NavMesh.AllAreas))
        {
            agent.enabled = false;
            return false;
        }

        if (!agent.enabled)
            agent.enabled = true;

        agent.Warp(hit.position);
        transform.position = hit.position;
        origin = hit.position;

        if (!agent.isOnNavMesh)
        {
            agent.enabled = false;
            return false;
        }

        agent.isStopped = true;
        return true;
    }

    private void DisableAgentIfOffMesh()
    {
        if (agent == null || !agent.enabled)
            return;

        if (!agent.isOnNavMesh)
            agent.enabled = false;
    }

    public void PauseWander()
    {
        paused = true;
        IsWalking = false;
        IsTurning = false;

        if (agent == null || !agent.isActiveAndEnabled || !agent.enabled)
            return;

        if (!agent.isOnNavMesh)
        {
            agent.enabled = false;
            return;
        }

        agent.isStopped = true;
        agent.ResetPath();
    }

    public void ResumeWander()
    {
        paused = false;
        state = State.Idle;
        idleTimer = Random.Range(0.5f, 2f);
    }

    public void FaceTarget(Transform target)
    {
        if (target == null) return;
        Vector3 dir = target.position - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.01f)
            transform.rotation = FacingRotation(dir);
    }

    private Quaternion FacingRotation(Vector3 worldDirection)
    {
        worldDirection.y = 0f;
        if (worldDirection.sqrMagnitude < 0.0001f)
            return transform.rotation;

        return Quaternion.LookRotation(worldDirection.normalized)
               * Quaternion.Euler(0f, visualYawOffset, 0f);
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Vector3 center = Application.isPlaying ? origin :
                         useCustomOrigin ? customOriginWorld : transform.position;
        Gizmos.color = new Color(0.2f, 0.8f, 0.4f, 0.25f);
        Gizmos.DrawWireSphere(center, wanderRadius);
        Gizmos.color = new Color(0.2f, 0.8f, 0.4f, 0.08f);
        Gizmos.DrawSphere(center, wanderRadius);
    }
#endif
}
