
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

        [Header("NPC Hand")]
        public Transform npcPalm;
        public Transform ikTarget;
        public TwoBoneIKConstraint armIK;

        [Header("Interaction")]
        public float startDistance = 1.5f;
        public float releaseDistance = 2.0f;
        public float blendSpeed = 4f;
        public float followSpeed = 12f;

        [Header("Debugging")]
        public bool showDebugLogs = true;

        private bool _shaking;
        private bool _wasGripping;

        private void Start()
        {
            Debug.Log("VRHandshake script is running!");

            if (armIK != null)
                armIK.weight = 0f;

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

            float grip =
                gripAction.action?.ReadValue<float>() ?? 0f;

            bool gripping = grip > 0.5f;

            float distance = Vector3.Distance(
                playerHand.position,
                npcPalm.position
            );

            // Log grip input
            if (gripping && !_wasGripping && showDebugLogs)
            {
                Debug.Log(
                    $"VRHandshake: Grip detected ({grip:F2}), " +
                    $"distance = {distance:F2}m",
                    this
                );
            }

            // Start while gripping within range
            if (!_shaking &&
                gripping &&
                distance <= startDistance)
            {
                _shaking = true;

                if (showDebugLogs)
                    Debug.Log("Handshake started", this);
            }

            // Release grip or move out of range
            if (_shaking &&
                (!gripping || distance > releaseDistance))
            {
                _shaking = false;

                if (showDebugLogs)
                    Debug.Log("Handshake ended", this);
            }

            // NPC hand follows player controller
            if (_shaking)
            {
                float t = 1f - Mathf.Exp(
                    -followSpeed * Time.deltaTime
                );

                ikTarget.position = Vector3.Lerp(
                    ikTarget.position,
                    playerHand.position,
                    t
                );

                ikTarget.rotation = Quaternion.Slerp(
                    ikTarget.rotation,
                    playerHand.rotation,
                    t
                );
            }

            float desiredWeight = _shaking ? 1f : 0f;

            armIK.weight = Mathf.MoveTowards(
                armIK.weight,
                desiredWeight,
                blendSpeed * Time.deltaTime
            );

            _wasGripping = gripping;
        }
    }
}
