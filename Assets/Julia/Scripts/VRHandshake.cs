
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
        [SerializeField, Min(0f)] float palmSeparation = 0.05f;
        [SerializeField, Range(0f, 1f)] float npcGripTowardFingers = 0.75f;
        [SerializeField, Min(0f)] float connectedFollowSpeed = 55f;
        [SerializeField, Min(0f)] float gripCloseDistance = 0.15f;
        [SerializeField, Range(0f, 25f)] float maxBodyTurn = 12f;

        [Header("Haptics")]
        public bool enableHaptics = true;

        [Range(0f, 1f)]
        public float contactHapticStrength = 0.45f;
        public float contactHapticDuration = 0.12f;

        [Range(0f, 1f)]
        public float releaseHapticStrength = 0.15f;
        public float releaseHapticDuration = 0.06f;

        [SerializeField, Range(0f, 1f)] float gripHapticStrength = 0.85f;
        [SerializeField, Range(0f, 1f)] float movementHapticStrength = 1f;

        [Header("Debugging")]
        public bool showDebugLogs = true;

        private bool _shaking;
        private bool _wasGripping;
        private bool _contactTriggered;
        private bool _hasConnected;
        private bool _closingGrip;
        private bool _palmHoldBroken;
        private float _palmSeparationTime;
        private Vector3 _currentTargetPosition;
        private HandshakePlayerAvatar _playerAvatar;
        private Transform _npcKnuckles;
        private Transform _npcFingers;
        private Transform _npcFingertip;
        private Quaternion _knuckleRest;
        private Quaternion _fingerRest;
        private Vector3 _knuckleCurlAxis;
        private Vector3 _fingerCurlAxis;
        private Vector3 _npcPalmCenterOffset;
        private Quaternion _npcPalmFrameOffset;
        private Quaternion _restTargetRotation;
        private bool _hasNpcPalmFrame;
        private float _gripBlend;
        private SpriteRenderer _palmGlow;
        private Light _palmLight;
        private Texture2D _glowTexture;
        private float _glowAmount;
        private MeshRenderer _handGlowSphere;
        private Material _handGlowSphereMaterial;
        private Quaternion _restBodyRotation;
        private float _bodyYaw;
        private Vector3 _lastGripPosition;
        private Vector3 _movementAnchor;
        private Vector3 _lastPulseDirection;
        private float _nextMovementPulse;
        private float _lastControllerMovement;
        private bool _movementPulseSent;

        private void Start()
        {
            _restBodyRotation = transform.localRotation;
            Debug.Log("VRHandshake script is running!");

            if (armIK != null)
                armIK.weight = 0f;

            if (ikTarget != null)
            {
                _currentTargetPosition = ikTarget.position;
                _restTargetRotation = ikTarget.localRotation;
            }

            Transform gripPoint = playerGripPoint != null ? playerGripPoint : playerHand;
            if (gripPoint) _playerAvatar = gripPoint.GetComponentInParent<HandshakePlayerAvatar>();
            if (npcPalm) CacheNpcGrip();

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
            if (!gripping) _palmHoldBroken = false;

            Vector3 npcContactPoint = npcPalm.position + npcPalm.rotation * _npcPalmCenterOffset;
            Vector3 palmCenter = default;
            Vector3 inward = default;
            Vector3 forward = default;
            bool hasPalmAlignment = _hasNpcPalmFrame && _playerAvatar &&
                _playerAvatar.TryGetRightPalmContact(out palmCenter, out inward, out forward);
            Vector3 playerContactPoint = hasPalmAlignment
                ? palmCenter + inward * (palmSeparation * 0.5f)
                : gripPoint.position;
            float distance = Vector3.Distance(playerContactPoint, npcContactPoint);

            if (gripping && !_wasGripping && showDebugLogs)
            {
                Debug.Log(
                    $"VRHandshake: Grip detected ({grip:F2}), " +
                    $"distance = {distance:F2}m",
                    this
                );
            }

            if (!_shaking &&
                gripping && !_palmHoldBroken &&
                distance <= startDistance)
            {
                _shaking = true;
                _contactTriggered = false;
                _hasConnected = false;
                _closingGrip = false;
                _palmSeparationTime = 0f;
                _lastGripPosition = gripPoint.position;
                _movementAnchor = gripPoint.position;
                _nextMovementPulse = Time.time + 0.12f;
                _lastControllerMovement = float.NegativeInfinity;
                _movementPulseSent = false;
                if (enableHaptics) SendHaptic(gripHapticStrength, 0.11f);

                if (showDebugLogs)
                    Debug.Log("Handshake started", this);
            }

            if (_shaking &&
                (!gripping || _palmHoldBroken || distance > releaseDistance))
            {
                _shaking = false;
                ClearPalmHold();

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

            TurnBodyTowardGrip(gripPoint.position);

            if (_shaking)
            {
                // Aim the NPC IK target at the
                // player's calibrated grip location.
                Vector3 desiredPosition = gripPoint.position + contactOffset;
                bool palmsTouching = false;
                float speed = _hasConnected ? Mathf.Max(snapSpeed, connectedFollowSpeed) : snapSpeed;
                float t = 1f - Mathf.Exp(-speed * Time.deltaTime);
                if (hasPalmAlignment)
                {
                    Quaternion opposingPalm = Quaternion.LookRotation(-forward, -inward) *
                        Quaternion.AngleAxis(10f, Vector3.forward) * _npcPalmFrameOffset;
                    ikTarget.rotation = Quaternion.Slerp(ikTarget.rotation, opposingPalm, t);
                    desiredPosition = playerContactPoint
                        - ikTarget.rotation * _npcPalmCenterOffset + contactOffset;
                    Vector3 npcPalmSurface = npcPalm.position + npcPalm.rotation * _npcPalmCenterOffset;
                    float surfaceGap = Vector3.Distance(playerContactPoint, npcPalmSurface);
                    palmsTouching = surfaceGap <= 0.02f;
                    if (_playerAvatar.HandshakePalm == npcPalm)
                    {
                        _palmSeparationTime = surfaceGap > 0.12f ? _palmSeparationTime + Time.deltaTime : 0f;
                        if (surfaceGap > 0.22f || _palmSeparationTime >= 0.12f)
                        {
                            ClearPalmHold();
                            _palmHoldBroken = true;
                        }
                    }
                }

                _currentTargetPosition = Vector3.Lerp(
                    ikTarget.position,
                    desiredPosition,
                    t
                );

                ikTarget.position = _currentTargetPosition;

                // Send a haptic pulse only when
                // the palms are visually close.
                float contactGap = Vector3.Distance(
                    desiredPosition,
                    npcPalm.position
                );

                // Start shaping the hand during the reach so the fingers are already
                // closing by the time the palm arrives at its contact target.
                _closingGrip |= palmsTouching ||
                    Mathf.Min(distance, contactGap) <= gripCloseDistance;

                if (!_contactTriggered &&
                    (hasPalmAlignment ? palmsTouching : contactGap <= contactDistance && armIK.weight >= 0.8f))
                {
                    _contactTriggered = true;
                    _hasConnected = true;
                    if (hasPalmAlignment)
                    {
                        _playerAvatar.HandshakePalm = npcPalm;
                        _playerAvatar.HandshakePalmOffset = _npcPalmCenterOffset;
                        _playerAvatar.HandshakePalmDepth = palmSeparation * 0.5f;
                    }

                    if (enableHaptics)
                    {
                        SendHaptic(
                            contactHapticStrength,
                            contactHapticDuration
                        );
                        _nextMovementPulse = Time.time + contactHapticDuration;
                    }

                    if (showDebugLogs)
                        Debug.Log("Handshake contact!", this);
                }

                PulseForMovement(gripPoint.position);
            }
            else if (armIK.weight <= 0f)
            {
                ikTarget.localRotation = _restTargetRotation;
            }

            float desiredWeight = _shaking ? 1f : 0f;

            armIK.weight = Mathf.MoveTowards(
                armIK.weight,
                desiredWeight,
                blendSpeed * Time.deltaTime
            );

            _wasGripping = gripping;
        }

        private void PulseForMovement(Vector3 gripPosition)
        {
            float displacement = Vector3.Distance(_lastGripPosition, gripPosition);
            _lastGripPosition = gripPosition;
            if (!enableHaptics || !_closingGrip) return;

            if (Time.time - _lastControllerMovement >= 0.2f)
            {
                _movementPulseSent = false;
                _movementAnchor = gripPosition;
            }

            if (displacement > 0.2f)
            {
                _movementAnchor = gripPosition;
                return;
            }
            if (displacement < 0.08f * Time.deltaTime)
                return;

            _lastControllerMovement = Time.time;
            Vector3 travel = gripPosition - _movementAnchor;
            if (_movementPulseSent && Vector3.Dot(travel, _lastPulseDirection) > 0f)
            {
                _movementAnchor = gripPosition;
                return;
            }
            if (travel.sqrMagnitude < 0.025f * 0.025f || Time.time < _nextMovementPulse) return;

            SendHaptic(movementHapticStrength, 0.10f);
            _movementPulseSent = true;
            _lastPulseDirection = travel.normalized;
            _movementAnchor = gripPosition;
            _nextMovementPulse = Time.time + 0.14f;
        }

        private void TurnBodyTowardGrip(Vector3 gripPosition)
        {
            if (!_shaking && Mathf.Approximately(_bodyYaw, 0f)) return;

            Quaternion restWorld = transform.parent
                ? transform.parent.rotation * _restBodyRotation
                : _restBodyRotation;
            Vector3 direction = Vector3.ProjectOnPlane(gripPosition - transform.position, Vector3.up);
            float targetYaw = 0f;
            if (_shaking && direction.sqrMagnitude > 0.01f)
            {
                Vector3 restForward = Vector3.ProjectOnPlane(restWorld * Vector3.forward, Vector3.up);
                float yawToGrip = Vector3.SignedAngle(restForward, direction, Vector3.up);
                // Let the arm handle most of the reach, with a small assist from the stance.
                targetYaw = Mathf.Clamp(Mathf.DeltaAngle(0f, yawToGrip - 10f) * 0.3f,
                    -maxBodyTurn, maxBodyTurn);
            }

            _bodyYaw = Mathf.MoveTowards(_bodyYaw, targetYaw, 45f * Time.deltaTime);
            transform.rotation = Quaternion.AngleAxis(_bodyYaw, Vector3.up) * restWorld;
        }

        private void CacheNpcGrip()
        {
            Transform thumb = null;
            foreach (Transform bone in npcPalm.GetComponentsInChildren<Transform>(true))
            {
                if (bone.name == "MiddleHand.R") _npcKnuckles = bone;
                if (bone.name == "Fingers.R") _npcFingers = bone;
                if (bone.name == "Thumb1.R") thumb = bone;
                if (bone.name == "Fingers.R_end") _npcFingertip = bone;
            }
            if (!_npcKnuckles || !_npcFingers || !thumb) return;

            Vector3 palm = _npcKnuckles.position - npcPalm.position;
            Vector3 across = Vector3.ProjectOnPlane(npcPalm.position - thumb.position, palm).normalized;
            Vector3 inward = HandshakePlayerAvatar.GetPalmNormal(npcPalm, _npcKnuckles, thumb, _npcFingers, _npcFingertip);
            if (inward.sqrMagnitude < 0.001f) return;
            if (Vector3.Dot(Vector3.Cross(across, palm), inward) < 0f) across = -across;
            _npcPalmFrameOffset = Quaternion.Inverse(Quaternion.LookRotation(palm, inward)) * npcPalm.rotation;
            _hasNpcPalmFrame = true;
            Vector3 gripPoint = Vector3.Lerp(_npcKnuckles.position, _npcFingers.position, npcGripTowardFingers);
            _npcPalmCenterOffset = npcPalm.InverseTransformDirection(
                gripPoint - npcPalm.position + inward * (palmSeparation * 0.5f));
            _knuckleRest = _npcKnuckles.localRotation;
            _fingerRest = _npcFingers.localRotation;
            _knuckleCurlAxis = _npcKnuckles.InverseTransformDirection(across);
            _fingerCurlAxis = _npcFingers.InverseTransformDirection(across);
        }

        private void LateUpdate()
        {
            UpdatePalmGlow();
            if (!_npcFingers || _knuckleCurlAxis.sqrMagnitude < 0.001f) return;
            bool closing = _shaking && _closingGrip;
            if (!closing && _gripBlend == 0f) return;

            float speed = closing ? 18f : 8f;
            _gripBlend = Mathf.MoveTowards(_gripBlend, closing ? 1f : 0f, speed * Time.deltaTime);
            _npcKnuckles.localRotation = _knuckleRest * Quaternion.AngleAxis(18f * _gripBlend, _knuckleCurlAxis);
            _npcFingers.localRotation = _fingerRest * Quaternion.AngleAxis(36f * _gripBlend, _fingerCurlAxis);
        }

        private void UpdatePalmGlow()
        {
            bool glowing = _shaking && _closingGrip && npcPalm;
            if (!glowing && _glowAmount <= 0f) return;
            _glowAmount = Mathf.MoveTowards(_glowAmount, glowing ? 1f : 0f, (glowing ? 14f : 8f) * Time.deltaTime);
            if (_playerAvatar && _playerAvatar.SetRightHandGlow(_glowAmount))
            {
                if (_palmGlow) _palmGlow.gameObject.SetActive(false);
                UpdateHandGlowSphere();
                return;
            }
            if (_handGlowSphere) _handGlowSphere.enabled = false;
            if (!_palmGlow) CreatePalmGlow();
            _palmGlow.gameObject.SetActive(_glowAmount > 0f);
            if (!_palmGlow.gameObject.activeSelf || !npcPalm) return;

            Vector3 center = npcPalm.position + npcPalm.rotation * _npcPalmCenterOffset;
            _palmGlow.transform.position = center;
            if (Camera.main is Camera camera)
            {
                FrameGlowAroundHands(camera.transform);
            }
            _palmGlow.color = new Color(1f, 0.63f, 0.22f, _glowAmount);
            _palmLight.intensity = 0.12f * _glowAmount;
        }

        private void UpdateHandGlowSphere()
        {
            if (!_handGlowSphere && _glowAmount > 0f)
            {
                Shader shader = Resources.Load<Shader>("HandshakeGlowSphere");
                if (!shader || !shader.isSupported) return;
                var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                sphere.name = "Handshake Glow Sphere";
                var collider = sphere.GetComponent<Collider>();
                collider.enabled = false;
                Destroy(collider);
                sphere.transform.localScale = Vector3.one * 0.23f;
                _handGlowSphere = sphere.GetComponent<MeshRenderer>();
                _handGlowSphere.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                _handGlowSphereMaterial = new Material(shader);
                _handGlowSphere.sharedMaterial = _handGlowSphereMaterial;
            }

            if (!_handGlowSphere) return;
            bool visible = _playerAvatar.TryGetRightHandSpan(out Vector3 wrist, out Vector3 tip) && _glowAmount > 0f;
            _handGlowSphere.enabled = visible;
            if (!visible) return;
            _handGlowSphere.transform.position = Vector3.Lerp(wrist, tip, 0.55f);
            _handGlowSphereMaterial.SetFloat("_Strength", _glowAmount);
        }

        const float GlowDiameter = 0.27f;

        private void FrameGlowAroundHands(Transform camera)
        {
            Vector3 npcWrist = npcPalm.position;
            Vector3 npcTip = _npcFingertip ? _npcFingertip.position : _npcFingers ? _npcFingers.position : npcWrist;
            Vector3 handAxis = Vector3.ProjectOnPlane(npcTip - npcWrist, camera.forward);
            float angle = handAxis.sqrMagnitude > 0.0001f
                ? Vector3.SignedAngle(camera.right, handAxis, camera.forward) : 0f;
            Quaternion rotation = Quaternion.AngleAxis(angle, camera.forward) * camera.rotation;
            Quaternion inverse = Quaternion.Inverse(rotation);
            var bounds = new Bounds(Vector3.zero, Vector3.zero);
            bounds.Encapsulate(inverse * (npcTip - npcWrist));
            if (_playerAvatar && _playerAvatar.TryGetRightHandSpan(out Vector3 playerWrist, out Vector3 playerTip))
            {
                bounds.Encapsulate(inverse * (playerWrist - npcWrist));
                bounds.Encapsulate(inverse * (playerTip - npcWrist));
            }

            Vector3 center = bounds.center;
            center.z = bounds.max.z + 0.075f;
            var glowTransform = _palmGlow.transform;
            glowTransform.SetPositionAndRotation(npcWrist + rotation * center, rotation);
            glowTransform.localScale = new Vector3(
                Mathf.Max(1f, (bounds.size.x + 0.09f) / (GlowDiameter * 0.64f)),
                Mathf.Max(1f, (bounds.size.y + 0.09f) / (GlowDiameter * 0.64f)), 1f);
        }

        private void CreatePalmGlow()
        {
            const int size = 64;
            _glowTexture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            _glowTexture.wrapMode = TextureWrapMode.Clamp;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                Vector2 point = new Vector2(x + 0.5f, y + 0.5f) * (2f / size) - Vector2.one;
                float radius = point.magnitude;
                float innerFade = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.31f, 0.58f, radius));
                float outerFade = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.64f, 1f, radius));
                byte alpha = (byte)(255f * innerFade * outerFade);
                pixels[y * size + x] = new Color32(255, 255, 255, alpha);
            }
            _glowTexture.SetPixels32(pixels);
            _glowTexture.Apply(false, true);

            var glow = new GameObject("Handshake Palm Glow");
            _palmGlow = glow.AddComponent<SpriteRenderer>();
            _palmGlow.sprite = Sprite.Create(_glowTexture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size / GlowDiameter);
            var lightObject = new GameObject("Palm Light");
            lightObject.transform.SetParent(glow.transform, false);
            _palmLight = lightObject.AddComponent<Light>();
            _palmLight.type = LightType.Point;
            _palmLight.color = new Color(1f, 0.78f, 0.4f);
            _palmLight.range = 0.35f;
            _palmLight.shadows = LightShadows.None;
        }

        private void OnDisable()
        {
            if (_handGlowSphere) _handGlowSphere.enabled = false;
            ClearPalmHold();
            if (_glowAmount > 0f && _playerAvatar) _playerAvatar.SetRightHandGlow(0f);
            if (_palmGlow) _palmGlow.gameObject.SetActive(false);
            _glowAmount = 0f;
        }

        private void ClearPalmHold()
        {
            if (_playerAvatar && _playerAvatar.HandshakePalm == npcPalm)
                _playerAvatar.HandshakePalm = null;
        }

        private void OnDestroy()
        {
            if (_handGlowSphere) Destroy(_handGlowSphere.gameObject);
            if (_handGlowSphereMaterial) Destroy(_handGlowSphereMaterial);
            if (_palmGlow)
            {
                Destroy(_palmGlow.sprite);
                Destroy(_palmGlow.gameObject);
            }
            if (_glowTexture) Destroy(_glowTexture);
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
