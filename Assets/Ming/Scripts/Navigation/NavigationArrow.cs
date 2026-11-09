using UnityEngine;

// A world-space arrow that stays in front of the player and points to the current NPC.
// Mesh / children of this object should point along local +Z.
public class NavigationArrow : MonoBehaviour
{
    [Header("Assign in Inspector")]
    public Transform player;
    public NPCManager manager;

    [Header("Appearance")]
    [Min(0.1f)] public float forwardOffset = 1.8f;
    public float heightOffset = 1.25f;
    [Min(0.1f)] public float rotationSmooth = 9f;

    private void LateUpdate()
    {
        if (player == null || manager == null) return;

        NPCController target = manager.CurrentTarget;
        if (target == null)
        {
            // Finished: hide the arrow.
            foreach (Renderer renderer in GetComponentsInChildren<Renderer>())
                renderer.enabled = false;
            return;
        }

        Vector3 forward = Vector3.ProjectOnPlane(player.forward, Vector3.up).normalized;
        if (forward.sqrMagnitude < 0.001f) forward = Vector3.forward;
        transform.position = player.position + forward * forwardOffset + Vector3.up * heightOffset;

        Vector3 direction = target.transform.position - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f) return;

        Quaternion desiredRotation = Quaternion.LookRotation(direction);
        transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation,
            1f - Mathf.Exp(-rotationSmooth * Time.deltaTime));
    }
}
