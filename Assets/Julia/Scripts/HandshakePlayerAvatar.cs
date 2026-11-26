using System.Collections.Generic;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Visuals;

[DefaultExecutionOrder(100)]
public sealed class HandshakePlayerAvatar : MonoBehaviour
{
    [SerializeField] Transform headset;
    [SerializeField] Transform leftController;
    [SerializeField] Transform rightController;
    [SerializeField] Transform rightHandModel;
    [SerializeField] GameObject bodyModel;
    [SerializeField] float bodyScale = 0.43f;
    [SerializeField] float torsoBackOffset = 0.12f;
    [SerializeField] float eyeAboveHeadBone = 0.10f;
    // Camera Y Offset is an absolute height in device mode and an adjustment
    // relative to this reference height when the headset supplies floor height.
    const float FloorTrackingReferenceHeight = 1.65f;

    GameObject body;
    GameObject fallbackLeft;
    Renderer[] rightHandRenderers;
    readonly List<Mesh> avatarMeshes = new List<Mesh>();
    XROrigin xrOrigin;
    Arm leftArm;
    Arm rightArm;
    Vector3 headOffset;
    Vector3 bodyForward = Vector3.forward;
    float soleOffset;
    bool showAvatar;
    Material rightHandGlow;
    static readonly int GlowStrength = Shader.PropertyToID("_Strength");

    internal bool SetRightHandGlow(float amount)
    {
        if (!rightHandGlow) return false;
        rightHandGlow.SetFloat(GlowStrength, amount);
        return true;
    }
    internal Transform HandshakePalm { get; set; }
    internal Vector3 HandshakePalmOffset { get; set; }
    internal float HandshakePalmDepth { get; set; }
    const float AutoHoldClearance = 0.02f;

    internal bool TryGetRightPalmContact(out Vector3 center, out Vector3 inward, out Vector3 forward)
    {
        center = default;
        inward = default;
        forward = default;
        if (!showAvatar) return false;
        rightArm.GetPalmContact(out center, out inward, out forward);
        return true;
    }

    internal bool TryGetRightHandSpan(out Vector3 wrist, out Vector3 tip)
    {
        wrist = default;
        tip = default;
        if (!showAvatar) return false;
        rightArm.GetHandSpan(out wrist, out tip);
        return true;
    }

    internal static Vector3 GetPalmNormal(Transform wrist, Transform knuckles, Transform thumb,
        Transform fingers, Transform fingertip)
    {
        Vector3 forward = (knuckles.position - wrist.position).normalized;
        Vector3 thumbSide = Vector3.ProjectOnPlane(thumb.position - wrist.position, forward).normalized;
        Vector3 normal = Vector3.Cross(forward, thumbSide).normalized;
        // The authored finger bend identifies which side is the palm.
        Vector3 fingerBend = (fingertip ? fingertip.position : fingers.position) - knuckles.position;
        return Vector3.Dot(normal, fingerBend) < 0f ? -normal : normal;
    }

    void Start()
    {
        xrOrigin = GetComponentInChildren<XROrigin>();
        if (xrOrigin) HideLocomotionVisuals();
        if (!headset || !leftController || !rightController || !rightHandModel)
        {
            Debug.LogWarning("Assign the headset, controllers, and right-hand model on the player avatar.", this);
            return;
        }

        CreateLeftHand();
        if (!bodyModel) return;

        body = Instantiate(bodyModel, transform);
        body.name = "Player Avatar";
        body.transform.localScale = Vector3.one * bodyScale;
        var bones = new Dictionary<string, Transform>();
        foreach (var bone in body.GetComponentsInChildren<Transform>(true))
            bones.TryAdd(bone.name, bone);

        leftArm = new Arm(bones, ".L");
        rightArm = new Arm(bones, ".R");
        if (!bones.TryGetValue("Head", out var head) || !leftArm.IsValid || !rightArm.IsValid)
        {
            Debug.LogWarning("The player body is missing a head or arm bone.", this);
            body.SetActive(false);
            return;
        }

        foreach (var animator in body.GetComponentsInChildren<Animator>())
            animator.enabled = false;

        headOffset = body.transform.InverseTransformPoint(head.position) * bodyScale;
        bodyForward = Vector3.ProjectOnPlane(headset.forward, Vector3.up).normalized;
        if (bodyForward.sqrMagnitude < 0.1f) bodyForward = Vector3.forward;
        showAvatar = HideHead(out soleOffset);
        body.SetActive(showAvatar);
        if (!showAvatar) return;

        rightHandRenderers = rightHandModel.GetComponentsInChildren<Renderer>(true);
        foreach (var renderer in rightHandRenderers)
            renderer.forceRenderingOff = true;
        fallbackLeft.SetActive(false);
    }

    void HideLocomotionVisuals()
    {
        foreach (var visual in xrOrigin.GetComponentsInChildren<XRInteractorLineVisual>(true))
        {
            // The teleport line visual also updates the destination arrow.
            if (!visual.reticle || !visual.reticle.name.StartsWith("Directional Teleport Reticle"))
            {
                visual.enabled = false;
                continue;
            }

            if (!visual.reticle.scene.IsValid())
                visual.reticle = Instantiate(visual.reticle, visual.transform);

            var centerMarker = visual.reticle.transform.Find("Sphere");
            if (centerMarker) centerMarker.gameObject.SetActive(false);
        }
        foreach (var visual in xrOrigin.GetComponentsInChildren<CurveVisualController>(true))
            visual.enabled = false;
        foreach (var line in xrOrigin.GetComponentsInChildren<LineRenderer>(true))
            line.forceRenderingOff = true;
    }

    void CreateLeftHand()
    {
        // Reflect the existing hand in controller space, including its alignment offset.
        var mirror = new GameObject("PlayerHand.L");
        fallbackLeft = mirror;
        mirror.transform.SetParent(leftController, false);
        mirror.transform.localScale = new Vector3(-1f, 1f, 1f);

        var model = Instantiate(rightHandModel.gameObject, mirror.transform, false);
        foreach (var grip in model.GetComponentsInChildren<VRHandGripPose>(true))
            grip.enabled = false;

        var leftGrip = model.AddComponent<VRHandGripPose>();
        leftGrip.controller = XRNode.LeftHand;
        leftGrip.curlAxis = Vector3.back;
        leftGrip.fingerCurlDegrees = 25f;
        leftGrip.thumbCurlDegrees = 10f;
    }

    bool HideHead(out float lowestVertex)
    {
        bool hasBody = false;
        Shader glowShader = Resources.Load<Shader>("HandshakeSkinGlow");
        bool canRenderSkinGlow = glowShader && glowShader.isSupported;
        lowestVertex = float.PositiveInfinity;
        foreach (var renderer in body.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            var source = renderer.sharedMesh;
            if (!source || !source.isReadable)
            {
                renderer.enabled = false;
                continue;
            }

            var excludedBones = new HashSet<int>();
            var handBones = new HashSet<int>();
            var skinBones = renderer.bones;
            for (int index = 0; index < skinBones.Length; index++)
            {
                var bone = skinBones[index];
                if (bone && bone.name.StartsWith("Head"))
                    excludedBones.Add(index);
                if (bone && (bone.name == "Palm.R" || bone.name == "MiddleHand.R" ||
                    bone.name == "Fingers.R" || bone.name == "Thumb1.R" || bone.name == "Thumb2.R"))
                    handBones.Add(index);
            }

            var weights = source.boneWeights;
            if (weights.Length != source.vertexCount)
            {
                renderer.enabled = false;
                continue;
            }

            var vertices = source.vertices;
            var meshToBody = body.transform.worldToLocalMatrix * renderer.transform.localToWorldMatrix;
            var excludedVertices = new bool[weights.Length];
            var handVertices = new bool[weights.Length];
            for (int index = 0; index < weights.Length; index++)
            {
                var weight = weights[index];
                float excluded = WeightIfExcluded(weight.boneIndex0, weight.weight0) +
                    WeightIfExcluded(weight.boneIndex1, weight.weight1) +
                    WeightIfExcluded(weight.boneIndex2, weight.weight2) +
                    WeightIfExcluded(weight.boneIndex3, weight.weight3);
                excludedVertices[index] = excluded >= 0.5f;
                handVertices[index] = WeightIfHand(weight.boneIndex0, weight.weight0) +
                    WeightIfHand(weight.boneIndex1, weight.weight1) +
                    WeightIfHand(weight.boneIndex2, weight.weight2) +
                    WeightIfHand(weight.boneIndex3, weight.weight3) >= 0.65f;
                if (!excludedVertices[index])
                    lowestVertex = Mathf.Min(lowestVertex, meshToBody.MultiplyPoint3x4(vertices[index]).y * bodyScale);

                float WeightIfExcluded(int boneIndex, float value) =>
                    excludedBones.Contains(boneIndex) ? value : 0f;
                float WeightIfHand(int boneIndex, float value) =>
                    handBones.Contains(boneIndex) ? value : 0f;
            }

            var mesh = Instantiate(source);
            avatarMeshes.Add(mesh);
            bool hasTriangles = false;
            var glowTriangles = new List<int>();
            for (int submesh = 0; submesh < mesh.subMeshCount; submesh++)
            {
                var triangles = source.GetTriangles(submesh);
                var kept = new List<int>(triangles.Length);
                for (int index = 0; index < triangles.Length; index += 3)
                {
                    int a = triangles[index];
                    int b = triangles[index + 1];
                    int c = triangles[index + 2];
                    if (excludedVertices[a] || excludedVertices[b] || excludedVertices[c]) continue;
                    kept.Add(a);
                    kept.Add(b);
                    kept.Add(c);
                    if (canRenderSkinGlow && handVertices[a] && handVertices[b] && handVertices[c])
                    {
                        glowTriangles.Add(a);
                        glowTriangles.Add(b);
                        glowTriangles.Add(c);
                    }
                }

                mesh.SetTriangles(kept, submesh, false);
                hasTriangles |= kept.Count > 0;
            }

            renderer.sharedMesh = mesh;
            if (glowTriangles.Count > 0)
            {
                if (!rightHandGlow) rightHandGlow = new Material(glowShader);
                int glowSubmesh = mesh.subMeshCount++;
                mesh.SetTriangles(glowTriangles, glowSubmesh, false);
                Material[] materials = renderer.sharedMaterials;
                System.Array.Resize(ref materials, mesh.subMeshCount);
                materials[glowSubmesh] = rightHandGlow;
                renderer.sharedMaterials = materials;
            }
            renderer.enabled = hasTriangles;
            renderer.updateWhenOffscreen = hasTriangles;
            hasBody |= hasTriangles;
        }

        return hasBody;
    }

    void LateUpdate()
    {
        if (xrOrigin && xrOrigin.CameraFloorOffsetObject)
        {
            var trackingSpace = xrOrigin.CameraFloorOffsetObject.transform;
            Vector3 offset = trackingSpace.localPosition;
            float desiredY = xrOrigin.CurrentTrackingOriginMode == TrackingOriginModeFlags.Floor
                ? xrOrigin.CameraYOffset - FloorTrackingReferenceHeight
                : xrOrigin.CameraYOffset;
            if (!Mathf.Approximately(offset.y, desiredY))
            {
                offset.y = desiredY;
                trackingSpace.localPosition = offset;
            }
        }
        if (!showAvatar) return;

        Vector3 headsetForward = Vector3.ProjectOnPlane(headset.forward, Vector3.up);
        if (headsetForward.sqrMagnitude > 0.04f)
        {
            float yaw = Vector3.SignedAngle(bodyForward, headsetForward, Vector3.up);
            bodyForward = Quaternion.AngleAxis(yaw * (1f - Mathf.Exp(-10f * Time.deltaTime)),
                Vector3.up) * bodyForward;
        }

        Quaternion rotation = Quaternion.LookRotation(bodyForward, Vector3.up);
        Vector3 headPosition = headset.position - bodyForward * torsoBackOffset - Vector3.up * eyeAboveHeadBone;
        Vector3 bodyPosition = headPosition - rotation * headOffset;
        // Teleport moves the XR origin. Keep the model's standing pose on that floor.
        if (xrOrigin && xrOrigin.Origin) bodyPosition.y = xrOrigin.Origin.transform.position.y - soleOffset;
        body.transform.SetPositionAndRotation(bodyPosition, rotation);
        leftArm.UpdatePose(leftController, body.transform, XRNode.LeftHand, -1f);
        rightArm.UpdatePose(rightController, body.transform, XRNode.RightHand, 1f, cuppedHold: HandshakePalm != null);
        if (HandshakePalm)
        {
            rightArm.GetPalmContact(out Vector3 center, out Vector3 inward, out _);
            Vector3 correction = HandshakePalm.position + HandshakePalm.rotation * HandshakePalmOffset
                - (center + inward * (HandshakePalmDepth + AutoHoldClearance));
            rightArm.ApplyContactCorrection(Vector3.ClampMagnitude(correction, 0.08f));
        }
    }

    void OnDestroy()
    {
        if (rightHandRenderers != null)
            foreach (var renderer in rightHandRenderers)
                if (renderer) renderer.forceRenderingOff = false;

        if (body) Destroy(body);
        if (fallbackLeft) Destroy(fallbackLeft);
        foreach (var mesh in avatarMeshes)
            Destroy(mesh);
        if (rightHandGlow) Destroy(rightHandGlow);
    }

    sealed class Arm
    {
        readonly Transform shoulder;
        readonly Transform elbow;
        readonly Transform wrist;
        readonly Transform knuckles;
        readonly Transform fingers;
        readonly Transform thumb;
        readonly Quaternion wristRest;
        readonly Vector3 elbowRestPosition;
        readonly Vector3 wristRestPosition;
        readonly Quaternion shoulderRest;
        readonly Quaternion elbowRest;
        readonly Quaternion knuckleRest;
        readonly Quaternion fingerRest;
        readonly Quaternion thumbRest;
        readonly Vector3 knuckleCurlAxis;
        readonly Vector3 fingerCurlAxis;
        readonly Vector3 thumbSpreadAxis;
        readonly Transform fingertip;
        readonly Vector3 palmNormalLocal;
        Vector3 freePalmCenter;
        Vector3 freePalmNormal;
        Vector3 freePalmForward;
        float gripBlend;

        public bool IsValid => shoulder && elbow && wrist && knuckles && fingers && thumb;

        public void GetPalmContact(out Vector3 center, out Vector3 inward, out Vector3 forward)
        {
            center = freePalmCenter;
            inward = freePalmNormal;
            forward = freePalmForward;
        }

        public void ApplyContactCorrection(Vector3 correction)
        {
            // Distribute the small visual correction across both joints.
            elbow.position += correction * 0.5f;
            wrist.position += correction * 0.5f;
        }

        void CacheFreePalm()
        {
            Vector3 palm = knuckles.position - wrist.position;
            freePalmForward = palm.normalized;
            freePalmCenter = wrist.position + 0.5f * palm;
            freePalmNormal = Vector3.ProjectOnPlane(wrist.TransformDirection(palmNormalLocal), freePalmForward).normalized;
        }

        public Arm(Dictionary<string, Transform> bones, string side)
        {
            bones.TryGetValue("UpperArm" + side, out shoulder);
            bones.TryGetValue("LowerArm" + side, out elbow);
            bones.TryGetValue("Palm" + side, out wrist);
            bones.TryGetValue("MiddleHand" + side, out knuckles);
            bones.TryGetValue("Fingers" + side, out fingers);
            bones.TryGetValue("Thumb1" + side, out thumb);
            if (!IsValid) return;

            bones.TryGetValue("Fingers" + side + "_end", out fingertip);
            palmNormalLocal = wrist.InverseTransformDirection(GetPalmNormal(wrist, knuckles, thumb, fingers, fingertip));

            shoulderRest = shoulder.localRotation;
            elbowRest = elbow.localRotation;
            wristRest = wrist.localRotation;
            elbowRestPosition = elbow.localPosition;
            wristRestPosition = wrist.localPosition;
            knuckleRest = knuckles.localRotation;
            fingerRest = fingers.localRotation;
            thumbRest = thumb.localRotation;
            Vector3 palm = knuckles.position - wrist.position;
            Vector3 forward = palm.normalized;
            Vector3 towardThumb = Vector3.ProjectOnPlane(thumb.position - wrist.position, forward).normalized;
            Vector3 across = side == ".L" ? towardThumb : -towardThumb;
            if (side == ".L" && Vector3.Dot(Vector3.Cross(across, forward),
                wrist.TransformDirection(palmNormalLocal)) < 0f)
                across = -across;
            knuckleCurlAxis = knuckles.InverseTransformDirection(across);
            fingerCurlAxis = fingers.InverseTransformDirection(across);
            if (bones.TryGetValue("Thumb2" + side, out var thumbJoint))
            {
                Vector3 thumbDirection = (thumbJoint.position - thumb.position).normalized;
                thumbSpreadAxis = thumb.InverseTransformDirection(Vector3.Cross(thumbDirection, towardThumb).normalized);
            }
        }

        public void GetHandSpan(out Vector3 wristPosition, out Vector3 tipPosition)
        {
            wristPosition = wrist.position;
            tipPosition = fingertip ? fingertip.position : fingers.position;
        }

        public void UpdatePose(Transform controller, Transform body, XRNode node, float side, bool cuppedHold = false)
        {
            elbow.localPosition = elbowRestPosition;
            wrist.localPosition = wristRestPosition;
            shoulder.localRotation = shoulderRest;
            elbow.localRotation = elbowRest;
            Vector3 start = shoulder.position;
            Vector3 upper = elbow.position - start;
            Vector3 lower = wrist.position - elbow.position;
            float upperLength = upper.magnitude;
            float lowerLength = lower.magnitude;

            var device = InputDevices.GetDeviceAtXRNode(node);
            bool tracked = device.TryGetFeatureValue(CommonUsages.isTracked, out bool isTracked)
                ? isTracked
                : device.isValid;
            Vector3 target = tracked ? controller.position :
                start + body.TransformDirection(new Vector3(0.06f * side, -0.47f, 0.12f));
            Vector3 reach = target - start;
            Vector3 direction = reach.sqrMagnitude > 0.000001f ? reach.normalized : -body.up;

            // Solve the shoulder-elbow-wrist triangle at a reachable distance.
            float fullReach = upperLength + lowerLength;
            // Ease into the last part of the reach so small movements still bend the elbow.
            float bendReserve = tracked ? 0.02f * Mathf.SmoothStep(0f, 1f,
                Mathf.InverseLerp(fullReach * 0.85f, fullReach, reach.magnitude)) : 0f;
            float distance = Mathf.Clamp(reach.magnitude - bendReserve,
                Mathf.Abs(upperLength - lowerLength) + 0.001f, fullReach - bendReserve - 0.001f);
            float along = (upperLength * upperLength - lowerLength * lowerLength + distance * distance) /
                (2f * distance);
            float bend = Mathf.Sqrt(Mathf.Max(0f, upperLength * upperLength - along * along));
            Vector3 hint = body.TransformDirection(new Vector3(0.45f * side, -1f, -0.2f));
            Vector3 pole = Vector3.ProjectOnPlane(hint, direction);
            if (pole.sqrMagnitude < 0.01f)
                pole = Vector3.ProjectOnPlane(body.right * side, direction);

            Vector3 solvedElbow = start + direction * along + pole.normalized * bend;
            shoulder.rotation = Quaternion.FromToRotation(upper, solvedElbow - start) * shoulder.rotation;
            elbow.rotation = Quaternion.FromToRotation(wrist.position - elbow.position,
                start + direction * distance - elbow.position) * elbow.rotation;

            wrist.localRotation = wristRest;
            Vector3 handForward = knuckles.position - wrist.position;
            Vector3 forearmForward = wrist.position - elbow.position;
            wrist.rotation = Quaternion.FromToRotation(handForward, forearmForward) * wrist.rotation;
            CacheFreePalm();

            float grip = 0f;
            if (tracked) device.TryGetFeatureValue(CommonUsages.grip, out grip);
            gripBlend = Mathf.MoveTowards(gripBlend, Mathf.Clamp01(grip), 8f * Time.deltaTime);
            float knuckleBend = cuppedHold ? 16f : 22f;
            float fingerBend = cuppedHold ? 36f : 28f;
            knuckles.localRotation = knuckleRest * Quaternion.AngleAxis(knuckleBend * gripBlend, knuckleCurlAxis);
            fingers.localRotation = fingerRest * Quaternion.AngleAxis(fingerBend * gripBlend, fingerCurlAxis);
            if (thumbSpreadAxis.sqrMagnitude > 0.001f)
                thumb.localRotation = thumbRest * Quaternion.AngleAxis(Mathf.Lerp(22f, 17f, gripBlend), thumbSpreadAxis);
        }
    }
}
