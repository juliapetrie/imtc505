using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Put on the stick root (or any grabbable with a Rigidbody).
///  - Starts frozen in place, so it doesn't roll around the scene.
///  - Pick it up as normal.
///  - When dropped: all hand velocity is thrown away (no throwing), it falls under gravity,
///    and as soon as it has come to rest it freezes again.
/// </summary>
[RequireComponent(typeof(Rigidbody), typeof(XRGrabInteractable))]
public class RestWhenDropped : MonoBehaviour
{
    [Tooltip("Hold the object still at the start of the scene.")]
    public bool freezeAtStart = true;

    [Tooltip("Discard the hand's velocity on release so it can't be thrown.")]
    public bool cancelThrow = true;

    [Header("When is it 'at rest'?")]
    public float restLinearSpeed = 0.05f;    // m/s
    public float restAngularSpeed = 0.5f;    // rad/s
    public float restSeconds = 0.5f;         // must stay this still this long

    Rigidbody rb;
    XRGrabInteractable grab;
    bool falling;
    float stillTime;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        grab = GetComponent<XRGrabInteractable>();
        if (freezeAtStart) rb.isKinematic = true;
    }

    void OnEnable()
    {
        grab.selectEntered.AddListener(OnGrab);
        grab.selectExited.AddListener(OnRelease);
    }

    void OnDisable()
    {
        grab.selectEntered.RemoveListener(OnGrab);
        grab.selectExited.RemoveListener(OnRelease);
    }

    void OnGrab(SelectEnterEventArgs args) { falling = false; stillTime = 0f; }

    void OnRelease(SelectExitEventArgs args)
    {
        // Wait a frame so XRI finishes restoring the Rigidbody state first.
        if (isActiveAndEnabled) StartCoroutine(DropRoutine());
    }

    IEnumerator DropRoutine()
    {
        yield return null;
        if (grab.isSelected) yield break;      // picked up again (e.g. by the other hand)

        rb.isKinematic = false;
        rb.useGravity = true;
        if (cancelThrow)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
        falling = true;
        stillTime = 0f;
    }

    void FixedUpdate()
    {
        if (!falling || rb.isKinematic) return;

        bool still = rb.linearVelocity.sqrMagnitude < restLinearSpeed * restLinearSpeed
                  && rb.angularVelocity.sqrMagnitude < restAngularSpeed * restAngularSpeed;
        stillTime = still ? stillTime + Time.fixedDeltaTime : 0f;

        if (stillTime >= restSeconds)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;     // freeze where it landed
            falling = false;
        }
    }
}