using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Attachment;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// Put this on the stick root (next to XRGrabInteractable).
/// While the stick is held, the holding hand's thumbstick (left/right) rolls the stick
/// around its own length. It uses its OWN input actions, so it does not enable or change
/// any locomotion/turn actions from the XRI rig.
/// </summary>
[RequireComponent(typeof(XRGrabInteractable))]
public class StickRoller : MonoBehaviour
{
    [Header("Rolling")]
    [Tooltip("The object that spins: parent of the stick + marshmallow meshes. A CHILD of this root, not the root itself.")]
    public Transform rollPivot;
    [Tooltip("Empty child of the root (NOT inside Roll Pivot), on the stick's centre line, blue Z arrow along the stick.")]
    public Transform rollAxis;
    public float degreesPerSecond = 360f;
    [Range(0f, 0.5f)] public float deadzone = 0.15f;
    public bool invert;

    [Header("Block the built-in joystick rotation while holding")]
    [Tooltip("While held, sets the X-axis mode of the holding hand's Interaction Attach Controller to None " +
             "(so left/right on the stick no longer rotates the stick), and restores it on release. " +
             "Y-axis (push/pull) is left untouched. Found automatically on the interactor.")]
    public bool blockBuiltInRotation = true;

    [Tooltip("Components that let the thumbstick rotate/move a held object (e.g. Interaction Attach Controller on the LEFT hand's interactor). Disabled while the left hand holds the stick.")]
    public Behaviour[] disableWhileHeldLeft;
    [Tooltip("Same for the RIGHT hand's interactor.")]
    public Behaviour[] disableWhileHeldRight;

    [Header("Debug")]
    public bool debugLog;

    XRGrabInteractable grab;
    InputAction leftStick, rightStick, active;

    static InputAction MakeStick(string name, string hand)
    {
        var a = new InputAction(name, InputActionType.Value);
        a.AddBinding("<XRController>{" + hand + "}/thumbstick");
        a.AddBinding("<XRController>{" + hand + "}/primary2DAxis");
        return a;
    }

    void Awake()
    {
        grab = GetComponent<XRGrabInteractable>();
        leftStick = MakeStick("StickRollLeft", "LeftHand");
        rightStick = MakeStick("StickRollRight", "RightHand");
        if (rollPivot == null || rollPivot == transform)
            Debug.LogWarning("StickRoller: Roll Pivot must be a child object holding the stick meshes, not the root.", this);
        if (rollAxis == null)
            Debug.LogWarning("StickRoller: assign Roll Axis.", this);
    }

    void OnEnable()
    {
        grab.selectEntered.AddListener(OnGrab);
        grab.selectExited.AddListener(OnRelease);
        leftStick.Enable();
        rightStick.Enable();
    }

    void OnDisable()
    {
        grab.selectEntered.RemoveListener(OnGrab);
        grab.selectExited.RemoveListener(OnRelease);
        leftStick.Disable();
        rightStick.Disable();
        if (active != null) SetBlockers(heldLeft ? disableWhileHeldLeft : disableWhileHeldRight, true);
        RestoreController();
        active = null;
    }

    void OnDestroy()
    {
        leftStick?.Dispose();
        rightStick?.Dispose();
    }

    void OnGrab(SelectEnterEventArgs args)
    {
        bool left = args.interactorObject.handedness == InteractorHandedness.Left;
        active = left ? leftStick : rightStick;
        heldLeft = left;
        SetBlockers(left ? disableWhileHeldLeft : disableWhileHeldRight, false);

        RestoreController();
        if (blockBuiltInRotation && args.interactorObject is Component interactorComponent)
        {
            heldController = interactorComponent.GetComponent<InteractionAttachController>();
            if (heldController != null)
            {
                savedXMode = heldController.manipulationXAxisMode;
                heldController.manipulationXAxisMode = InteractionAttachController.ManipulationXAxisMode.None;
            }
            else if (debugLog)
                Debug.Log("StickRoller: no Interaction Attach Controller found on " + interactorComponent.name, this);
        }
        if (debugLog) Debug.Log("StickRoller: grabbed by " + args.interactorObject.handedness + " hand", this);
    }

    bool heldLeft;
    InteractionAttachController heldController;
    InteractionAttachController.ManipulationXAxisMode savedXMode;

    void RestoreController()
    {
        if (heldController != null) heldController.manipulationXAxisMode = savedXMode;
        heldController = null;
    }

    static void SetBlockers(Behaviour[] list, bool enabled)
    {
        if (list == null) return;
        foreach (var b in list) if (b != null) b.enabled = enabled;
    }

    void OnRelease(SelectExitEventArgs args)
    {
        RestoreController();
        SetBlockers(heldLeft ? disableWhileHeldLeft : disableWhileHeldRight, true);
        active = null;
    }

    void Update()
    {
        if (active == null || rollPivot == null || rollAxis == null) return;
        float x = active.ReadValue<Vector2>().x;
        if (debugLog && Mathf.Abs(x) > 0.01f) Debug.Log("StickRoller: thumbstick x = " + x.ToString("F2"), this);
        if (Mathf.Abs(x) < deadzone) return;
        float dir = invert ? -1f : 1f;
        rollPivot.RotateAround(rollAxis.position, rollAxis.forward, x * dir * degreesPerSecond * Time.deltaTime);
    }
}