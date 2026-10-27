using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations.Rigging;
using UnityEngine.InputSystem;
using UnityEngine.XR;

// Attach to the same GameObject as the NPC Animator. Requires Animation Rigging
// and Input System. Right-hand bones match Male_LongSleeve.fbx.
[DisallowMultipleComponent]
[RequireComponent(typeof(Animator))]
public class VRHandshakeAuto : MonoBehaviour
{
    [Header("Player: assign these")]
    [Tooltip("Controller-tracked visual hand or controller transform.")]
    public Transform playerHand;
    [Tooltip("Optional child of playerHand, positioned where the NPC wrist should meet the hand.")]
    public Transform handshakePoint;
    [Tooltip("Optional Input System grip action. Otherwise reads the XR right controller grip.")]
    public InputActionProperty gripAction;
    public XRNode controller = XRNode.RightHand;

    [Header("Interaction distances (metres)")]
    [Min(0.01f)] public float startDistance = 0.16f;
    [Min(0.01f)] public float releaseDistance = 0.28f;
    [Range(0f, 1f)] public float gripThreshold = 0.5f;
    [Min(0.01f)] public float blendSpeed = 4f;
    [Min(0.01f)] public float followSpeed = 14f;
    [Range(0.5f, 0.99f)] public float maxReachFraction = 0.95f;

    [Header("Pose tuning")]
    [Tooltip("Offset from the handshake point in its local axes, in metres.")]
    public Vector3 handPositionOffset;
    [Tooltip("Rotation adjustment relative to the hand's rotation at first grip.")]
    public Vector3 handRotationOffset;
    [Range(0f, 1f)] public float followHandRotation = 0.35f;
    [Tooltip("NPC local-space direction used to keep the elbow pointing naturally.")]
    public Vector3 elbowDirection = new Vector3(0.5f, -0.4f, -0.7f);
    [Min(0.01f)] public float elbowHintDistance = 0.35f;

    [Header("Haptics")]
    public bool haptics = true;
    [Range(0f, 1f)] public float gripHapticAmplitude = 0.35f;
    [Range(0.01f, 1f)] public float gripHapticDuration = 0.08f;

    Transform upper, lower, wrist, target, hint;
    Animator animator;
    RigBuilder builder;
    Rig rig;
    TwoBoneIKConstraint constraint;
    bool holding, ready, enabledActionHere;
    Quaternion initialHandRotation, initialWristRotation;
    float armLength;

    void Start()
    {
        animator = GetComponent<Animator>();
        upper = FindBone("UpperArm.R", HumanBodyBones.RightUpperArm);
        lower = FindBone("LowerArm.R", HumanBodyBones.RightLowerArm);
        wrist = FindBone("MiddleHand.R", HumanBodyBones.RightHand);
        if (!upper || !lower || !wrist || !lower.IsChildOf(upper) || !wrist.IsChildOf(lower))
        {
            Debug.LogError("VRHandshakeAuto: Could not find a valid right arm chain. Check the FBX hierarchy and Animator.", this);
            enabled = false; return;
        }
        if (!playerHand)
        {
            Debug.LogError("VRHandshakeAuto: Assign the tracked Player Hand in the Inspector.", this);
            enabled = false; return;
        }
        armLength = Vector3.Distance(upper.position, lower.position) + Vector3.Distance(lower.position, wrist.position);
        if (armLength < 0.01f)
        {
            Debug.LogError("VRHandshakeAuto: Arm bones are too close together; check the model scale.", this);
            enabled = false; return;
        }

        builder = GetComponent<RigBuilder>();
        if (!builder) builder = gameObject.AddComponent<RigBuilder>();
        var rigObject = new GameObject("Handshake Rig (runtime)");
        rigObject.transform.SetParent(transform, false);
        rig = rigObject.AddComponent<Rig>();
        rig.weight = 0f;
        target = new GameObject("Handshake Target (runtime)").transform;
        target.SetParent(rigObject.transform, false);
        target.SetPositionAndRotation(wrist.position, wrist.rotation);
        hint = new GameObject("Elbow Hint (runtime)").transform;
        hint.SetParent(rigObject.transform, false);
        UpdateHint();
        var ikObject = new GameObject("Right Arm IK (runtime)");
        ikObject.transform.SetParent(rigObject.transform, false);
        constraint = ikObject.AddComponent<TwoBoneIKConstraint>();
        var data = constraint.data;
        data.root = upper; data.mid = lower; data.tip = wrist;
        data.target = target; data.hint = hint;
        data.targetPositionWeight = 1f;
        data.targetRotationWeight = followHandRotation;
        data.hintWeight = 1f;
        constraint.data = data;
        builder.layers.Add(new RigLayer(rig));
        // Rebuild after adding the runtime rig, including any existing rig layers.
        builder.Build();
        if (gripAction.action != null && !gripAction.action.enabled)
        {
            gripAction.action.Enable(); enabledActionHere = true;
        }
        ready = true;
        Debug.Log("VRHandshakeAuto: Ready. Move near the NPC's right hand and squeeze grip.", this);
    }

    Transform FindBone(string name, HumanBodyBones humanBone)
    {
        if (animator.isHuman)
        {
            Transform found = animator.GetBoneTransform(humanBone);
            if (found) return found;
        }
        foreach (Transform child in GetComponentsInChildren<Transform>(true))
            if (child.name == name) return child;
        return null;
    }

    void Update()
    {
        if (!ready || !playerHand) return;
        Transform point = handshakePoint ? handshakePoint : playerHand;
        bool pressed = ReadGrip() >= gripThreshold;
        float distance = Vector3.Distance(point.position, wrist.position);
        if (!holding && pressed && distance <= startDistance)
        {
            holding = true;
            initialHandRotation = point.rotation;
            initialWristRotation = wrist.rotation;
            target.SetPositionAndRotation(wrist.position, wrist.rotation);
            Pulse();
        }
        else if (holding && (!pressed || distance > Mathf.Max(startDistance, releaseDistance)))
        {
            holding = false;
        }

        rig.weight = Mathf.MoveTowards(rig.weight, holding ? 1f : 0f, blendSpeed * Time.deltaTime);
        if (holding)
        {
            Vector3 desired = point.TransformPoint(handPositionOffset);
            desired = upper.position + Vector3.ClampMagnitude(desired - upper.position, armLength * maxReachFraction);
            Quaternion delta = point.rotation * Quaternion.Inverse(initialHandRotation);
            Quaternion desiredRotation = delta * initialWristRotation * Quaternion.Euler(handRotationOffset);
            float t = 1f - Mathf.Exp(-followSpeed * Time.deltaTime);
            target.position = Vector3.Lerp(target.position, desired, t);
            target.rotation = Quaternion.Slerp(target.rotation, desiredRotation, t);
        }
        UpdateHint();
    }

    void UpdateHint()
    {
        if (!hint || !upper || !lower) return;
        Vector3 dir = transform.TransformDirection(elbowDirection.normalized);
        hint.position = lower.position + dir * elbowHintDistance;
    }

    float ReadGrip()
    {
        if (gripAction.action != null) return gripAction.action.ReadValue<float>();
        UnityEngine.XR.InputDevice device = InputDevices.GetDeviceAtXRNode(controller);
        return device.isValid && device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.grip, out float value) ? value : 0f;
    }

    void Pulse()
    {
        if (!haptics) return;
        UnityEngine.XR.InputDevice device = InputDevices.GetDeviceAtXRNode(controller);
        if (device.isValid && device.TryGetHapticCapabilities(out HapticCapabilities caps) && caps.supportsImpulse)
            device.SendHapticImpulse(0, gripHapticAmplitude, gripHapticDuration);
    }

    void OnDestroy()
    {
        if (enabledActionHere && gripAction.action != null) gripAction.action.Disable();
        if (builder && rig)
        {
            for (int i = builder.layers.Count - 1; i >= 0; i--)
                if (builder.layers[i].rig == rig) builder.layers.RemoveAt(i);
        }
    }
}
