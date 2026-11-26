using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Filtering;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

// Put this on the stick (same object as its XRGrabInteractable).
// The stick can only be hovered or grabbed when a hand is within maxGrabDistance of it,
// so the ray cannot pick it up from far away.
[RequireComponent(typeof(XRGrabInteractable))]
public class MarshmallowNearGrabFilter : MonoBehaviour, IXRHoverFilter, IXRSelectFilter
{
    [Min(0.01f)]
    [Tooltip("How close (metres) the hand must be to the stick's surface.")]
    public float maxGrabDistance = 0.2f;

    [Tooltip("Leave empty to use every collider on the stick (not trigger colliders).")]
    public Collider[] stickColliders;

    XRGrabInteractable grab;

    public bool canProcess => isActiveAndEnabled;

    void Awake()
    {
        grab = GetComponent<XRGrabInteractable>();
        if (stickColliders == null || stickColliders.Length == 0)
            stickColliders = GetComponentsInChildren<Collider>();
    }

    void OnEnable()
    {
        if (!grab) grab = GetComponent<XRGrabInteractable>();
        grab.hoverFilters.Add(this);
        grab.selectFilters.Add(this);
    }

    void OnDisable()
    {
        if (!grab) return;
        grab.hoverFilters.Remove(this);
        grab.selectFilters.Remove(this);
    }

    bool IsWithinReach(IXRInteractor interactor)
    {
        // Never interrupt a grab that is already happening.
        if (grab != null && grab.isSelected) return true;

        Transform attach = interactor.GetAttachTransform(grab);
        Vector3 hand = attach ? attach.position : interactor.transform.position;

        foreach (var c in stickColliders)
        {
            if (!c || !c.enabled || c.isTrigger) continue;
            if (Vector3.Distance(hand, c.ClosestPoint(hand)) <= maxGrabDistance) return true;
        }
        return false;
    }

    public bool Process(IXRHoverInteractor interactor, IXRHoverInteractable interactable)
        => IsWithinReach(interactor);

    public bool Process(IXRSelectInteractor interactor, IXRSelectInteractable interactable)
        => IsWithinReach(interactor);
}
