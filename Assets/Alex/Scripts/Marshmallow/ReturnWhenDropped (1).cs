using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

// Put this on the stick (next to XRGrabInteractable and ResetToStart).
// A moment after the stick is let go, it goes back to where it started.
[RequireComponent(typeof(XRGrabInteractable))]
[RequireComponent(typeof(ResetToStart))]
public class ReturnWhenDropped : MonoBehaviour
{
    [Tooltip("Seconds after letting go before the stick returns. Gives the player time to re-grab it.")]
    public float returnDelay = 2f;
    [Tooltip("If the stick falls below this height (world Y), return it straight away.")]
    public float fallLimitY = -5f;
    public bool debugLog = true;

    XRGrabInteractable grab;
    ResetToStart reset;
    Coroutine pending;

    void Awake()
    {
        grab = GetComponent<XRGrabInteractable>();
        reset = GetComponent<ResetToStart>();
    }

    void OnEnable()
    {
        grab.selectEntered.AddListener(OnGrabbed);
        grab.selectExited.AddListener(OnDropped);
    }

    void OnDisable()
    {
        grab.selectEntered.RemoveListener(OnGrabbed);
        grab.selectExited.RemoveListener(OnDropped);
    }

    void Update()
    {
        if (!grab.isSelected && transform.position.y < fallLimitY) ReturnNow();
    }

    void OnGrabbed(SelectEnterEventArgs _)
    {
        // Picked up again (by either hand): cancel the countdown.
        if (pending != null) { StopCoroutine(pending); pending = null; }
    }

    void OnDropped(SelectExitEventArgs _)
    {
        // Still held by the other hand, or being reset right now: do nothing.
        if (grab.isSelected) return;
        if (pending != null) StopCoroutine(pending);
        pending = StartCoroutine(ReturnAfterDelay());
    }

    IEnumerator ReturnAfterDelay()
    {
        yield return new WaitForSeconds(returnDelay);
        pending = null;
        if (!grab.isSelected) ReturnNow();
    }

    void ReturnNow()
    {
        if (debugLog) Debug.Log("ReturnWhenDropped: sending the stick back to its start.", this);
        reset.ResetNow(false); // false = keep the marshmallow's colour
    }
}