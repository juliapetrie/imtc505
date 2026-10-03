using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables; // XRI 3.x (delete this line for XRI 2.x)

[RequireComponent(typeof(Rigidbody))]
public class VRClickToBounce : MonoBehaviour
{
    public float bounceForce = 5f;

    XRSimpleInteractable interactable;
    Rigidbody rb;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        interactable = GetComponent<XRSimpleInteractable>();

        if (interactable == null)
            Debug.LogError("No XRSimpleInteractable on " + name + ". Add one to the sphere.", this);
    }

    void OnEnable()
    {
        if (interactable == null) return;
        interactable.hoverEntered.AddListener(OnHover);
        interactable.selectEntered.AddListener(OnSelect);
        interactable.activated.AddListener(OnActivate);
    }

    void OnDisable()
    {
        if (interactable == null) return;
        interactable.hoverEntered.RemoveListener(OnHover);
        interactable.selectEntered.RemoveListener(OnSelect);
        interactable.activated.RemoveListener(OnActivate);
    }

    void OnHover(HoverEnterEventArgs args)
    {
        Debug.Log("HOVER works");
    }

    void OnSelect(SelectEnterEventArgs args)
    {
        Debug.Log("SELECT works");
        Bounce();
    }

    // Fires on the trigger press while hovering/selecting (the "activate" action)
    void OnActivate(ActivateEventArgs args)
    {
        Debug.Log("ACTIVATE works");
        Bounce();
    }

    void Bounce()
    {
        if (rb.isKinematic)
        {
            Debug.LogWarning("Rigidbody is Kinematic, so forces do nothing. Untick Is Kinematic.", this);
            return;
        }

        rb.WakeUp();

#if UNITY_6000_0_OR_NEWER
        rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
#else
        rb.velocity = new Vector3(rb.velocity.x, 0f, rb.velocity.z);
#endif
        rb.AddForce(Vector3.up * bounceForce, ForceMode.Impulse);
        Debug.Log("Bounce applied, force = " + bounceForce);
    }
}