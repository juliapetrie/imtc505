using System;
using UnityEngine;
using UnityEngine.AI;

// Reusable controller for every capsule NPC.
[RequireComponent(typeof(NavMeshAgent))]
public class NPCController : MonoBehaviour
{
    public enum NPCState { Wandering, Approaching, WaitingForInteraction, Completed }

    [Header("Wandering")]
    [Min(1f)] public float wanderRadius = 5f;
    [Min(0f)] public float wanderWait = 2f;

    [Header("Player detection")]
    [Min(0.5f)] public float detectionDistance = 5f;
    [Min(0.5f)] public float cancelDistance = 7f;
    [Min(0.5f)] public float waitingCancelDistance = 3f;

    [Header("Approaching")]
    [Min(0.5f)] public float stopDistance = 1.3f;
    [Min(0.1f)] public float repathInterval = 0.4f;
    [Min(0.1f)] public float faceTurnSpeed = 8f;

    public NPCState State { get; private set; } = NPCState.Wandering;
    public bool IsCurrentTarget { get; private set; }
    public bool IsCompleted { get; private set; }

    // The future handshake script can subscribe to this event.
    public event Action<NPCController> OnInteractionReady;

    private NavMeshAgent agent;
    private Transform player;
    private Vector3 home;
    private float nextWanderAt;
    private float nextRepathAt;
    private bool travelling;
    private bool detectionArmed = true;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        home = transform.position;
    }

    private void Start()
    {
        if (!agent.isOnNavMesh)
        {
            Debug.LogError($"[{name}] NavMeshAgent is not on a baked NavMesh. Check NPC position / Bake.", this);
            enabled = false;
            return;
        }
        BeginWandering();
    }

    // Called by NPCManager at scene start.
    public void Configure(Transform playerTransform, bool isCurrentTarget)
    {
        player = playerTransform;
        IsCurrentTarget = isCurrentTarget;
    }

    public void SetCurrentTarget(bool value)
    {
        IsCurrentTarget = value && !IsCompleted;
        if (!IsCurrentTarget && (State == NPCState.Approaching || State == NPCState.WaitingForInteraction))
            BeginWandering();
    }

    private void Update()
    {
        if (!agent.isOnNavMesh || player == null) return;

        float distance = GroundDistance(player.position);
        switch (State)
        {
            case NPCState.Wandering:
            case NPCState.Completed:
                TickWandering();
                if (State == NPCState.Wandering && IsCurrentTarget)
                {
                    if (!detectionArmed && distance >= cancelDistance)
                        detectionArmed = true;
                    if (detectionArmed && distance <= detectionDistance)
                        BeginApproaching();
                }
                break;

            case NPCState.Approaching:
                if (!IsCurrentTarget || distance >= cancelDistance)
                {
                    BeginWandering();
                    break;
                }
                TickApproaching(distance);
                break;

            case NPCState.WaitingForInteraction:
                if (!IsCurrentTarget || distance > waitingCancelDistance)
                {
                    // Do not immediately re-trigger until player leaves detection area.
                    detectionArmed = false;
                    BeginWandering();
                    break;
                }
                FacePlayer();
                break;
        }
    }

    private float GroundDistance(Vector3 target)
    {
        Vector3 difference = target - transform.position;
        difference.y = 0f;
        return difference.magnitude;
    }

    private void BeginWandering()
    {
        State = IsCompleted ? NPCState.Completed : NPCState.Wandering;
        if (!agent.isOnNavMesh) return;
        agent.isStopped = false;
        agent.stoppingDistance = 0.15f;
        agent.ResetPath();
        travelling = false;
        nextWanderAt = Time.time + wanderWait;
    }

    private void TickWandering()
    {
        if (travelling)
        {
            if (agent.pathPending) return;
            if (!agent.hasPath || agent.remainingDistance <= agent.stoppingDistance + 0.2f)
            {
                travelling = false;
                nextWanderAt = Time.time + wanderWait;
            }
            return;
        }

        if (Time.time < nextWanderAt) return;
        if (TryChooseWanderDestination())
            travelling = true;
        else
            nextWanderAt = Time.time + 1f;
    }

    private bool TryChooseWanderDestination()
    {
        for (int attempt = 0; attempt < 12; attempt++)
        {
            Vector2 offset = UnityEngine.Random.insideUnitCircle * wanderRadius;
            Vector3 candidate = home + new Vector3(offset.x, 0f, offset.y);
            if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, 1.5f, agent.areaMask))
                continue;

            // Filter unreachable NavMesh islands.
            NavMeshPath path = new NavMeshPath();
            if (!agent.CalculatePath(hit.position, path) || path.status != NavMeshPathStatus.PathComplete)
                continue;

            if (agent.SetDestination(hit.position)) return true;
        }
        return false;
    }

    private void BeginApproaching()
    {
        State = NPCState.Approaching;
        agent.ResetPath();
        agent.isStopped = false;
        agent.stoppingDistance = stopDistance;
        nextRepathAt = 0f;
    }

    private void TickApproaching(float distance)
    {
        if (Time.time >= nextRepathAt && !agent.pathPending)
        {
            nextRepathAt = Time.time + repathInterval;
            if (NavMesh.SamplePosition(player.position, out NavMeshHit hit, 2f, agent.areaMask))
                agent.SetDestination(hit.position);
        }

        // Check both path distance and spatial distance. This prevents handshake through walls.
        if (!agent.pathPending && agent.hasPath &&
            agent.pathStatus == NavMeshPathStatus.PathComplete &&
            agent.remainingDistance <= agent.stoppingDistance + 0.15f &&
            distance <= stopDistance + 0.3f)
        {
            BeginWaiting();
        }
    }

    private void BeginWaiting()
    {
        State = NPCState.WaitingForInteraction;
        agent.isStopped = true;
        agent.ResetPath();
        OnInteractionReady?.Invoke(this);
        Debug.Log($"[{name}] Ready for interaction.", this);
    }

    private void FacePlayer()
    {
        Vector3 direction = player.position - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f) return;
        Quaternion desired = Quaternion.LookRotation(direction);
        transform.rotation = Quaternion.Slerp(transform.rotation, desired,
            1f - Mathf.Exp(-faceTurnSpeed * Time.deltaTime));
    }

    // Public integration point for teammate's handshake / IK module.
    // Returns false if NPC is not currently ready for interaction.
    public bool CompleteInteraction()
    {
        if (State != NPCState.WaitingForInteraction || IsCompleted) return false;
        IsCompleted = true;
        IsCurrentTarget = false;
        detectionArmed = false;
        BeginWandering(); // Completed NPC still wanders, but cannot trigger again.
        Debug.Log($"[{name}] Interaction completed.", this);
        return true;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(Application.isPlaying ? home : transform.position, wanderRadius);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionDistance);
    }
}
