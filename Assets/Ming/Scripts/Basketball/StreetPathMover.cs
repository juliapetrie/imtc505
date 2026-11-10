using UnityEngine;

/// <summary>
/// Lightweight waypoint movement for decorative cars and NPCs in Unity 6.
/// Add to an empty root object; put the visual model beneath that root.
/// No NavMesh or extra packages required.
/// </summary>
public class StreetPathMover : MonoBehaviour
{
    public enum PathMode
    {
        Loop,           // Last point connects directly to the first point.
        PingPong,       // Walk back along the same path at either end.
        RestartAtStart  // Teleport from the last point to the first point.
    }

    [Header("Route (drag points in travel order)")]
    public Transform[] waypoints;
    public PathMode pathMode = PathMode.PingPong;
    [Min(0)] public int startingWaypointIndex = 0;
    public bool snapToStartingPoint = true;

    [Header("Movement")]
    [Min(0f)] public float speed = 1.4f;
    [Min(0f)] public float turnSpeed = 240f; // Degrees per second
    [Min(0.01f)] public float arrivalDistance = 0.12f;
    [Min(0f)] public float pauseAtEnds = 0f; // Useful for pedestrians
    public bool keepStartingHeight = true;
    public bool turnOnlyAroundY = true;
    public bool turnInPlaceAtEnds = true; // Applied only to PingPong

    [Header("Optional NPC Body Bob (leave empty for cars)")]
    public Transform bodyToBob;
    [Min(0f)] public float bobHeight = 0f;
    [Min(0f)] public float bobFrequency = 2f;

    private int targetIndex;
    private int travelDirection = 1;
    private float waitRemaining;
    private float startingHeight;
    private Vector3 originalBodyLocalPosition;
    private bool routeReady;
    private bool turningAtEnd;

    private void Start()
    {
        if (waypoints == null || waypoints.Length < 2)
        {
            Debug.LogWarning($"{name}: StreetPathMover requires at least 2 waypoints.", this);
            enabled = false;
            return;
        }

        foreach (Transform point in waypoints)
        {
            if (point == null)
            {
                Debug.LogWarning($"{name}: A StreetPathMover waypoint is missing.", this);
                enabled = false;
                return;
            }
        }

        startingHeight = transform.position.y;
        if (bodyToBob != null)
            originalBodyLocalPosition = bodyToBob.localPosition;

        // Keep at least one following waypoint on the route.
        int start = Mathf.Clamp(startingWaypointIndex, 0, waypoints.Length - 2);

        if (snapToStartingPoint)
            transform.position = GetPoint(start);

        targetIndex = start + 1;
        FaceNextWaypointImmediately();
        routeReady = true;
    }

    private void Update()
    {
        if (!routeReady)
            return;

        if (waitRemaining > 0f)
        {
            waitRemaining -= Time.deltaTime;
            Bob(false);
            return;
        }

        Vector3 destination = GetPoint(targetIndex);
        Vector3 toDestination = destination - transform.position;

        // Pedestrians pause and turn on the spot at route endpoints.
        if (turningAtEnd)
        {
            Vector3 flatDirection = toDestination;
            if (turnOnlyAroundY)
                flatDirection.y = 0f;

            if (flatDirection.sqrMagnitude > 0.000001f)
            {
                Quaternion desired = Quaternion.LookRotation(flatDirection, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation, desired, turnSpeed * Time.deltaTime
                );
                if (Quaternion.Angle(transform.rotation, desired) > 2f)
                {
                    Bob(false);
                    return;
                }
            }
            turningAtEnd = false;
        }

        if (toDestination.sqrMagnitude > 0.000001f)
            TurnTowards(toDestination, false);

        transform.position = Vector3.MoveTowards(
            transform.position,
            destination,
            speed * Time.deltaTime
        );

        Bob(speed > 0.001f);

        if (Vector3.Distance(transform.position, destination) <= arrivalDistance)
        {
            transform.position = destination;
            AdvanceTarget();
        }
    }

    private Vector3 GetPoint(int index)
    {
        Vector3 position = waypoints[index].position;
        if (keepStartingHeight)
            position.y = startingHeight;
        return position;
    }

    private void AdvanceTarget()
    {
        int last = waypoints.Length - 1;

        if (pathMode == PathMode.RestartAtStart)
        {
            if (targetIndex == last)
            {
                // Use only when the end and restart are hidden from the player.
                transform.position = GetPoint(0);
                targetIndex = 1;
                FaceNextWaypointImmediately();
            }
            else
            {
                targetIndex++;
            }
            return;
        }

        if (pathMode == PathMode.Loop)
        {
            targetIndex = (targetIndex + 1) % waypoints.Length;
            return;
        }

        // PingPong reverses the route at both ends.
        bool atEnd = targetIndex == last || targetIndex == 0;
        if (targetIndex == last)
            travelDirection = -1;
        else if (targetIndex == 0)
            travelDirection = 1;

        targetIndex += travelDirection;
        if (atEnd)
        {
            waitRemaining = pauseAtEnds;
            turningAtEnd = turnInPlaceAtEnds;
        }
    }

    private void FaceNextWaypointImmediately()
    {
        Vector3 toTarget = GetPoint(targetIndex) - transform.position;
        TurnTowards(toTarget, true);
    }

    private void TurnTowards(Vector3 direction, bool immediately)
    {
        if (turnOnlyAroundY)
            direction.y = 0f;

        if (direction.sqrMagnitude < 0.000001f)
            return;

        Quaternion desiredRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        transform.rotation = immediately
            ? desiredRotation
            : Quaternion.RotateTowards(transform.rotation, desiredRotation, turnSpeed * Time.deltaTime);
    }

    private void Bob(bool moving)
    {
        if (bodyToBob == null)
            return;

        float offset = moving && bobHeight > 0f
            ? Mathf.Abs(Mathf.Sin(Time.time * bobFrequency * Mathf.PI * 2f)) * bobHeight
            : 0f;

        bodyToBob.localPosition = originalBodyLocalPosition + Vector3.up * offset;
    }

    private void OnDrawGizmosSelected()
    {
        if (waypoints == null)
            return;

        Gizmos.color = new Color(1f, 0.65f, 0.15f, 0.9f);
        for (int i = 0; i < waypoints.Length; i++)
        {
            if (waypoints[i] == null)
                continue;

            Gizmos.DrawSphere(waypoints[i].position, 0.16f);
            if (i > 0 && waypoints[i - 1] != null)
                Gizmos.DrawLine(waypoints[i - 1].position, waypoints[i].position);
        }

        if (pathMode == PathMode.Loop && waypoints.Length > 1 &&
            waypoints[0] != null && waypoints[waypoints.Length - 1] != null)
        {
            Gizmos.DrawLine(waypoints[waypoints.Length - 1].position, waypoints[0].position);
        }
    }
}
