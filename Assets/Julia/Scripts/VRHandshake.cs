
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Animations.Rigging;

namespace Julia.Scripts
{
    public class VRHandshake : MonoBehaviour
    {
        [Header("VR Hand")]
        public Transform playerHand;
        public InputActionProperty gripAction;

        [Header("Optional Hand Visual Alignment")]
        public Transform rightHandVisual;
        public Vector3 handPositionOffset;
        public Vector3 handRotationOffset;

        [Header("NPC Hand")]
        public Transform npcPalm;
        public Transform ikTarget;
        public TwoBoneIKConstraint armIK;

        [Header("Optional Handshake Anchor")]
        public Transform handshakeAnchor;

        [Header("Interaction Distances")]
        public float startDistance = 0.35f;
        public float releaseDistance = 0.65f;
        public float approachDistance = 1.0f;

        [Header("Movement")]
        public float blendSpeed = 3f;
        public float followSpeed = 8f;
        public float reachMultiplier = 0.85f;

        [Header("Handshake Motion")]
        public float shakeAmplitude = 0.025f;
        public float shakeFrequency = 2.5f;

        [Header("Debugging")]
        public bool showDebugLogs = true;

        private bool _shaking;
        private bool _wasGripping;
        private float _shakeTime;
        private float _maxReach;
        private Quaternion _handshakeRotation;

        private Transform _shoulder;
        private Transform _elbow;

        private void Start()
        {
            if (playerHand == null ||
                npcPalm == null ||
                ikTarget == null ||
                armIK == null)
            {
                Debug.LogError(
                    "VRHandshake: Missing Inspector references!",
                    this
                );
                enabled = false;
                return;
            }

            _shoulder = armIK.data.root;
            _elbow = armIK.data.mid;

            if (_shoulder == null || _elbow == null)
            {
                Debug.LogError(
                    "VRHandshake: IK arm bones missing!",
                    this
                );
                enabled = false;
                return;
            }

            // Calculate the real arm length.
            float upperArm = Vector3.Distance(
                _shoulder.position,
                _elbow.position
            );

            float lowerArm = Vector3.Distance(
                _elbow.position,
                npcPalm.position
            );

            _maxReach = Mathf.Max(
                0.01f,
                (upperArm + lowerArm) *
                Mathf.Clamp(reachMultiplier, 0.1f, 0.95f)
            );

            // Preserve the target's intended handshake rotation.
            _handshakeRotation = handshakeAnchor != null
                ? handshakeAnchor.rotation
                : ikTarget.rotation;

            // Prevent awkward wrist twisting.
            var data = armIK.data;
            data.targetPositionWeight = 1f;
            data.targetRotationWeight = 1f;
            data.hintWeight = 0f;
            armIK.data = data;

            armIK.weight = 0f;

            // Optional player hand alignment.
            if (rightHandVisual != null)
            {
                rightHandVisual.localPosition =
                    handPositionOffset;

                rightHandVisual.localRotation =
                    Quaternion.Euler(handRotationOffset);
            }

            if (showDebugLogs)
            {
                Debug.Log(
                    $"VRHandshake ready. Arm reach: " +
                    $"{_maxReach:F2}m",
                    this
                );
            }
        }

        private void Update()
        {
            if (playerHand == null ||
                ikTarget == null ||
                armIK == null)
                return;

            float grip =
                gripAction.action?.ReadValue<float>() ?? 0f;

            bool gripping = grip > 0.5f;

            Vector3 offerPosition = GetOfferPosition();

            float distance = Vector3.Distance(
                playerHand.position,
                offerPosition
            );

            bool nearby = distance <= approachDistance;

            // Begin when the player grips near the offered hand.
            if (!_shaking &&
                gripping &&
                distance <= startDistance)
            {
                _shaking = true;
                _shakeTime = 0f;

                if (showDebugLogs)
                    Debug.Log("Handshake started", this);
            }

            if (_shaking &&
                (!gripping || distance > releaseDistance))
            {
                _shaking = false;

                if (showDebugLogs)
                    Debug.Log("Handshake ended", this);
            }

            if (gripping && !_wasGripping &&
                showDebugLogs)
            {
                Debug.Log(
                    $"Grip: {grip:F2} | " +
                    $"Distance to handshake: {distance:F2}m",
                    this
                );
            }

            // NPC offers its hand when player approaches.
            float desiredWeight =
                (_shaking || nearby) ? 1f : 0f;

            armIK.weight = Mathf.MoveTowards(
                armIK.weight,
                desiredWeight,
                blendSpeed * Time.deltaTime
            );

            if (desiredWeight > 0f ||
                armIK.weight > 0f)
            {
                Vector3 destination = offerPosition;

                if (_shaking)
                {
                    // Follow player's hand, but only within
                    // the NPC's actual arm reach.
                    destination = playerHand.position;

                    _shakeTime += Time.deltaTime;

                    float shake = Mathf.Sin(
                        _shakeTime *
                        shakeFrequency *
                        Mathf.PI * 2f
                    ) * shakeAmplitude;

                    destination += Vector3.up * shake;
                }

                // Prevent the shoulder from overextending.
                Vector3 shoulderPosition =
                    _shoulder.position;

                Vector3 offset =
                    destination - shoulderPosition;

                destination = shoulderPosition +
                    Vector3.ClampMagnitude(
                        offset,
                        _maxReach
                    );

                float t = 1f - Mathf.Exp(
                    -followSpeed * Time.deltaTime
                );

                ikTarget.position = Vector3.Lerp(
                    ikTarget.position,
                    destination,
                    t
                );

                // Never copy the raw controller rotation.
                Quaternion targetRotation =
                    handshakeAnchor != null
                    ? handshakeAnchor.rotation
                    : _handshakeRotation;

                ikTarget.rotation = Quaternion.Slerp(
                    ikTarget.rotation,
                    targetRotation,
                    t
                );
            }

            _wasGripping = gripping;
        }

        private Vector3 GetOfferPosition()
        {
            if (handshakeAnchor != null)
                return handshakeAnchor.position;

            // Automatic forward-reaching position,
            // relative to the NPC's right shoulder.
            Vector3 direction =
                playerHand.position - _shoulder.position;

            direction.y = 0f;

            if (direction.sqrMagnitude < 0.001f)
                direction = transform.forward;

            direction.Normalize();

            return _shoulder.position +
                   direction * (_maxReach * 0.8f) +
                   Vector3.down * (_maxReach * 0.2f);
        }
    }
}
