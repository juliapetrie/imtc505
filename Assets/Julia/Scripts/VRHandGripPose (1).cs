using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;

// Add to the imported HandDefault model. Finds its finger bones by name.
// A starting pose only: adjust local curl axis and angle for your model.
public class VRHandGripPose : MonoBehaviour
{
    public InputActionProperty gripAction;
    public XRNode controller = XRNode.RightHand;
    [Range(0f, 1f)] public float gripThreshold = 0.5f;
    [Min(0.01f)] public float poseSpeed = 8f;
    [Tooltip("Local bone axis to rotate around. Try X, Y, or Z; negate it if fingers bend backwards.")]
    public Vector3 curlAxis = Vector3.right;
    [Range(-120f, 120f)] public float fingerCurlDegrees = 35f;
    [Range(-120f, 120f)] public float thumbCurlDegrees = 20f;
    [Tooltip("Leave false if another component already animates the fingers.")]
    public bool animateFingers = true;

    class BonePose { public Transform bone; public Quaternion rest; public bool thumb; }
    readonly List<BonePose> bones = new List<BonePose>();
    float blend;
    bool enabledActionHere;

    void Start()
    {
        string[] names = {"ThumbRoot", "ThumbMiddle", "ThumbTop", "IndexF_lower", "IndexF_middle", "IndexF_tip", "MiddleF_lower", "MiddleF_middle", "MiddleF_tip", "RingF_lower", "RingF_middle", "RingF_tip", "PinkyF_lower", "PinkyF_middle", "PinkyF_tip"};
        foreach (Transform child in GetComponentsInChildren<Transform>(true))
            foreach (string name in names)
                if (child.name == name)
                {
                    bones.Add(new BonePose { bone = child, rest = child.localRotation, thumb = name.StartsWith("Thumb") });
                    break;
                }
        if (bones.Count == 0) Debug.LogWarning("VRHandGripPose: No matching finger bones found. Add this component to the HandDefault model or its parent.", this);
        if (gripAction.action != null && !gripAction.action.enabled) { gripAction.action.Enable(); enabledActionHere = true; }
        Debug.Log("VRHandGripPose: Found " + bones.Count + " finger bones. Check curl direction in Play mode.", this);
    }

    void LateUpdate()
    {
        if (!animateFingers) return;
        float grip = 0f;
        if (gripAction.action != null) grip = gripAction.action.ReadValue<float>();
        else
        {
            UnityEngine.XR.InputDevice device = InputDevices.GetDeviceAtXRNode(controller);
            if (device.isValid) device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.grip, out grip);
        }
        blend = Mathf.MoveTowards(blend, grip >= gripThreshold ? 1f : 0f, poseSpeed * Time.deltaTime);
        if (curlAxis.sqrMagnitude < 0.0001f) return;
        foreach (var pose in bones)
            if (pose.bone) pose.bone.localRotation = pose.rest * Quaternion.AngleAxis((pose.thumb ? thumbCurlDegrees : fingerCurlDegrees) * blend, curlAxis.normalized);
    }
    void OnDestroy()
    {
        if (enabledActionHere && gripAction.action != null) gripAction.action.Disable();
    }
}
