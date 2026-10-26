
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Animations.Rigging;
using UnityEngine.XR;

namespace Julia.Scripts
{
    public class VRHandshake : MonoBehaviour
    {
        [Header("VR Hand")]
        public Transform playerHand;
        public InputActionProperty gripAction;

        [Header("NPC Hand")]
        public Transform npcPalm;
        public Transform ikTarget;
        public TwoBoneIKConstraint armIK;

        [Header("Interaction")]
        public float startDistance = 1.5f;
        public float releaseDistance = 2.0f;
        public float blendSpeed = 4f;
        public float followSpeed = 12f;

        [Header("Hand Contact")]
        public Transform playerGripPoint;
        public Vector3 contactOffset = Vector3.zero;
        public float snapSpeed = 16f;
        public float contactDistance = 0.08f;

        [Header("Haptics")]
        public bool enableHaptics = true;

        [Range(0f, 1f)]
        public float contactHapticStrength = 0.45f;
        public float contactHapticDuration = 0.12f;

        [Range(0f, 1f)]
        public float releaseHapticStrength = 0.15f;
        public float releaseHapticDuration = 0.06f;

        [Header("Debugging")]
        public bool showDebugLogs = true;

        private bool _shaking;
        private bool _wasGripping;
        private bool _contactTriggered;
        private bool _hasConnected;
        private Vector3 _currentTargetPosition;

        private void Start()
        {
            Debug.Log("VRHandshake script is running!");

            if (armIK != null)
                armIK.weight = 0f;

            if (ikTarget != null)
                _currentTargetPosition = ikTarget.position;

            if (playerHand == null ||
                npcPalm == null ||
                ikTarget == null ||
                armIK == null)
            {
                Debug.LogError(
                    "VRHandshake: Missing Inspector reference!",
                    this
                );
            }

            if (gripAction.action == null)
            {
                Debug.LogError(
                    "VRHandshake: Grip Action is missing!",
                    this
                );
            }
        }

        private void Update()
        {
            if (playerHand == null ||
                npcPalm == null ||
                ikTarget == null ||
                armIK == null)
                return;

            Transform gripPoint =
                playerGripPoint != null
                ? playerGripPoint
                : playerHand;

            float grip =
                gripAction.action?.ReadValue<float>() ?? 0f;

            bool gripping = grip > 0.5f;

            float distance = Vector3.Distance(
                gripPoint.position,
                npcPalm.position
            );

            if (gripping && !_wasGripping && showDebugLogs)
            {
                Debug.Log(
                    $"VRHandshake: Grip detected ({grip:F2}), " +
                    $"distance = {distance:F2}m",
                    this
                );
            }

            if (!_shaking &&
                gripping &&
                distance <= startDistance)
            {
                _shaking = true;
                _contactTriggered = false;
                _hasConnected = false;

                if (showDebugLogs)
                    Debug.Log("Handshake started", this);
            }

            if (_shaking &&
                (!gripping || distance > releaseDistance))
            {
                _shaking = false;

                if (_hasConnected && enableHaptics)
                {
                    SendHaptic(
                        releaseHapticStrength,
                        releaseHapticDuration
                    );
                }

                if (showDebugLogs)
                    Debug.Log("Handshake ended", this);
            }

            if (_shaking)
            {
                // Aim the NPC IK target at the
                // player's calibrated grip location.
                Vector3 desiredPosition =
                    gripPoint.position + contactOffset;

                // Faster movement as the hands approach.
                float speed = snapSpeed;

                float t = 1f - Mathf.Exp(
                    -speed * Time.deltaTime
                );

                _currentTargetPosition = Vector3.Lerp(
                    ikTarget.position,
                    desiredPosition,
                    t
                );

                ikTarget.position = _currentTargetPosition;

                // IMPORTANT:
                // Do not copy player rotation.
                // Preserve the working NPC wrist orientation.

                // Send a haptic pulse only when
                // the palms are visually close.
                float contactGap = Vector3.Distance(
                    gripPoint.position,
                    npcPalm.position
                );

                if (!_contactTriggered &&
                    contactGap <= contactDistance &&
                    armIK.weight >= 0.8f)
                {
                    _contactTriggered = true;
                    _hasConnected = true;

                    if (enableHaptics)
                    {
                        SendHaptic(
                            contactHapticStrength,
                            contactHapticDuration
                        );
                    }

                    if (showDebugLogs)
                        Debug.Log("Handshake contact!", this);
                }
            }

            float desiredWeight = _shaking ? 1f : 0f;

            armIK.weight = Mathf.MoveTowards(
                armIK.weight,
                desiredWeight,
                blendSpeed * Time.deltaTime
            );

            _wasGripping = gripping;
        }

        private void SendHaptic(
            float strength,
            float duration)
        {
            var device = InputDevices.GetDeviceAtXRNode(
                XRNode.RightHand
            );

            if (!device.isValid)
                return;

            if (device.TryGetHapticCapabilities(
                out HapticCapabilities capabilities))
            {
                if (capabilities.supportsImpulse)
                {
                    device.SendHapticImpulse(
                        0,
                        Mathf.Clamp01(strength),
                        Mathf.Max(0f, duration)
                    );
                }
            }
        }
    }
}
