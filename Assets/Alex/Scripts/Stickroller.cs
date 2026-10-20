using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// Put this on the stick root (next to XRGrabInteractable).
/// Your hand already gives free rotation while holding the stick; this adds
/// unlimited "roll" around the stick's own length with the thumbstick, so you can
/// spin it 360+ degrees without twisting your wrist.
/// </summary>
[RequireComponent(typeof(XRGrabInteractable))]
public class StickRoller : MonoBehaviour
{
    [Header("Input (use XRI Default Input Actions)")]
    public InputActionProperty leftThumbstick;   // XRI LeftHand Locomotion/Move
    public InputActionProperty rightThumbstick;  // XRI RightHand Locomotion/Move

    [Header("Rolling")]
    [Tooltip("Child object that holds the stick mesh + marshmallow. It is the thing that spins.")]
    public Transform rollPivot;
    [Tooltip("Stick's long axis in rollPivot's local space (Z if the stick points forward).")]
    public Vector3 rollAxis = Vector3.forward;
    public float degreesPerSecond = 360f;
    [Range(0f, 0.5f)] public float deadzone = 0.15f;
    [Tooltip("Use the thumbstick Y as well as X (either direction rolls).")]
    public bool invert;

    XRGrabInteractable grab;
    InputAction active;

    void Awake()
    {
        grab = GetComponent<XRGrabInteractable>();
        if (rollPivot == null) rollPivot = transform;
    }

    void OnEnable()
    {
        grab.selectEntered.AddListener(OnGrab);
        grab.selectExited.AddListener(OnRelease);
        Enable(leftThumbstick); Enable(rightThumbstick);
    }

    void OnDisable()
    {
        grab.selectEntered.RemoveListener(OnGrab);
        grab.selectExited.RemoveListener(OnRelease);
    }

    static void Enable(InputActionProperty p)
    {
        if (p.reference == null && p.action != null) p.action.Enable();
        else if (p.reference != null) p.reference.action.Enable();
    }

    void OnGrab(SelectEnterEventArgs args)
    {
        // Use the thumbstick of whichever hand is holding the stick.
        bool left = args.interactorObject.handedness == InteractorHandedness.Left;
        active = (left ? leftThumbstick : rightThumbstick).action;
    }

    void OnRelease(SelectExitEventArgs args) => active = null;

    void Update()
    {
        if (active == null) return;
        float x = active.ReadValue<Vector2>().x;
        if (Mathf.Abs(x) < deadzone) return;
        float dir = invert ? -1f : 1f;
        rollPivot.Rotate(rollAxis, x * dir * degreesPerSecond * Time.deltaTime, Space.Self);
    }
}