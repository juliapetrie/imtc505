using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// Put on the stick root. Remembers where the stick started; ResetNow() makes any hand let go
/// of it and puts it back there (with its roll straightened).
/// </summary>
[RequireComponent(typeof(Rigidbody), typeof(XRGrabInteractable))]
public class ResetToStart : MonoBehaviour
{
    [Tooltip("The 'Visual' child that StickRoller spins. Its rotation is put back too. Optional.")]
    public Transform rollPivot;

    [Tooltip("Also turn the marshmallow on the stick back to raw white.")]
    public bool resetMarshmallowColour = true;

    Rigidbody rb;
    XRGrabInteractable grab;
    Vector3 startPos;
    Quaternion startRot;
    Vector3 pivotPos;
    Quaternion pivotRot;
    bool resetting;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        grab = GetComponent<XRGrabInteractable>();
        startPos = transform.position;
        startRot = transform.rotation;
        if (rollPivot != null) { pivotPos = rollPivot.localPosition; pivotRot = rollPivot.localRotation; }
    }

    public void ResetNow()
    {
        if (resetting) return;
        StartCoroutine(ResetRoutine());
    }

    IEnumerator ResetRoutine()
    {
        resetting = true;

        // 1. Make every hand let go.
        var manager = grab.interactionManager;
        if (manager != null)
        {
            var holders = new List<IXRSelectInteractor>(grab.interactorsSelecting);
            foreach (var h in holders) manager.SelectExit(h, grab);
        }

        // 2. Wait two frames so XRI and RestWhenDropped finish handling the release.
        yield return null;
        yield return null;

        // 3. Put everything back.
        if (rb.isKinematic == false)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
        rb.position = startPos;
        rb.rotation = startRot;
        transform.SetPositionAndRotation(startPos, startRot);
        if (rollPivot != null) { rollPivot.localPosition = pivotPos; rollPivot.localRotation = pivotRot; }

        if (resetMarshmallowColour) ResetMarshmallows();

        // Stay put at the start (same behaviour as RestWhenDropped at the beginning of the scene).
        if (GetComponent<RestWhenDropped>() != null) rb.isKinematic = true;

        resetting = false;
    }

    // Resets cooking on a marshmallow that is a child of the stick, and on one held in a socket on the stick.
    void ResetMarshmallows()
    {
        foreach (var m in GetComponentsInChildren<RoastableMarshmallow>(true))
            m.ResetCooking();

        foreach (var socket in GetComponentsInChildren<XRSocketInteractor>(true))
        {
            foreach (var held in socket.interactablesSelected)
            {
                if (held is Component c)
                {
                    var m = c.GetComponentInChildren<RoastableMarshmallow>();
                    if (m != null) m.ResetCooking();
                }
            }
        }
    }
}