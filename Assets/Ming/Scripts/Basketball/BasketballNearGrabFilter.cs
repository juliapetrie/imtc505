using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Filtering;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class BasketballNearGrabFilter : MonoBehaviour,
    IXRHoverFilter, IXRSelectFilter
{
    [Min(0.01f)]
    public float maxGrabDistance = 0.15f;

    public Collider basketballCollider;

    private XRGrabInteractable grab;

    public bool canProcess => isActiveAndEnabled;

    private void Awake()
    {
        grab = GetComponent<XRGrabInteractable>();
    }

    private bool IsWithinReach(Transform hand)
    {
        // Do not interrupt an existing grab.
        if (grab != null && grab.isSelected)
            return true;

        if (basketballCollider == null)
            return false;

        Vector3 closestPoint =
            basketballCollider.ClosestPoint(hand.position);

        return Vector3.Distance(
            hand.position, closestPoint
        ) <= maxGrabDistance;
    }

    public bool Process(
        IXRHoverInteractor interactor,
        IXRHoverInteractable interactable)
    {
        return IsWithinReach(interactor.transform);
    }

    public bool Process(
        IXRSelectInteractor interactor,
        IXRSelectInteractable interactable)
    {
        return IsWithinReach(interactor.transform);
    }
}