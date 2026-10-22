using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// Put on the marshmallow prefab. While the marshmallow sits in the stick's socket, its colliders
/// ignore the stick's colliders (so they don't push each other), but stay solid so a hand can still
/// grab the marshmallow off the stick. Collisions come back when it leaves the socket.
/// </summary>
[RequireComponent(typeof(XRGrabInteractable))]
public class MarshmallowSocketPhysics : MonoBehaviour
{
    XRGrabInteractable grab;
    Collider[] myColliders;
    Collider[] ignored;

    void Awake()
    {
        grab = GetComponent<XRGrabInteractable>();
        myColliders = GetComponentsInChildren<Collider>();
    }

    void OnEnable()
    {
        grab.selectEntered.AddListener(OnSelectEntered);
        grab.selectExited.AddListener(OnSelectExited);
    }

    void OnDisable()
    {
        grab.selectEntered.RemoveListener(OnSelectEntered);
        grab.selectExited.RemoveListener(OnSelectExited);
        SetIgnore(false);
    }

    void OnSelectEntered(SelectEnterEventArgs args)
    {
        SetIgnore(false);   // clear any previous socket
        if (args.interactorObject is XRSocketInteractor socket)
        {
            var body = socket.GetComponentInParent<Rigidbody>();
            if (body != null) ignored = body.GetComponentsInChildren<Collider>();
            SetIgnore(true);
        }
    }

    void OnSelectExited(SelectExitEventArgs args)
    {
        if (args.interactorObject is XRSocketInteractor) SetIgnore(false);
    }

    void SetIgnore(bool ignore)
    {
        if (ignored == null) return;
        foreach (var a in myColliders)
        foreach (var b in ignored)
            if (a != null && b != null) Physics.IgnoreCollision(a, b, ignore);
        if (!ignore) ignored = null;
    }
}