using UnityEngine;
using UnityEngine.XR;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

internal sealed class LivingRoomEntrance
{
    private const float DoorCenter = -1.4255f;
    private const float DoorRight = DoorCenter + 0.36f;
    private const float Floor = -0.475f;
    private const float WallZ = -1.375f;
    private readonly Transform room;
    private readonly Unity.XR.CoreUtils.XROrigin playerOrigin;
    private const float StandingEyeHeight = 1.65f;
    private float? floorTrackingOffset;
    private readonly GameObject rig;
    private readonly Transform door;
    private readonly Transform hinge;
    private readonly Transform originalDoorParent;
    private readonly Vector3 originalDoorPosition;
    private readonly Quaternion originalDoorRotation;
    private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
    private readonly MeshRenderer[] windowPanes;
    private readonly Light[] windowSpills;
    private readonly MaterialPropertyBlock windowProperties = new();
    private readonly Collider knockTarget;
    private readonly Transform[] knockHands;
    private readonly bool[] triggerHeld = new bool[2];
    private readonly Collider doorLeaf;
    private readonly AudioSource knockAudio;
    private readonly AudioClip knockClip;
    private readonly Material hazeMaterial;
    private readonly Texture2D hazeTexture;
    private AudioLowPassFilter exteriorFilter;
    private float exteriorMix = 1f;
    private readonly Transform entrancePopup;
    private bool doorPopupDismissed;
    private readonly Text prompt;
    private readonly Text popupTitle;
    private readonly Transform host;
    private readonly bool ownsHost;
    private readonly Animator animator;
    private readonly PartyMotion hostMotion;
    private readonly PartyFooting hostFooting;
    private readonly LivingRoomMixamo cast;
    private readonly Vector3[] route;
    private int waypoint;
    private float opening;
    private float elapsedSinceKnock;
    private bool knocked;
    private bool doorRequested;
    private bool approachedFromInside;

    public LivingRoomEntrance(Transform room, Transform booth, GameObject model,
        RuntimeAnimatorController controller, LivingRoomMixamo cast, MeshRenderer[] windowPanes)
    {
        this.room = room;
        playerOrigin = Camera.main != null
            ? Camera.main.GetComponentInParent<Unity.XR.CoreUtils.XROrigin>() : null;
        if (playerOrigin != null && playerOrigin.CameraFloorOffsetObject != null)
        {
            playerOrigin.CameraFloorOffsetObject.transform.localRotation = Quaternion.identity;
            playerOrigin.CameraYOffset = StandingEyeHeight;
        }
        var avatar = playerOrigin != null ? playerOrigin.GetComponentInParent<HandshakePlayerAvatar>() : null;
        knockHands = new[] { avatar?.LeftController, avatar?.RightController };
        this.cast = cast;
        this.windowPanes = windowPanes;
        windowSpills = System.Array.FindAll(room.GetComponentsInChildren<Light>(),
            light => light.name == "Window Light Spill");
        rig = new GameObject("Living Room Entrance");
        rig.transform.SetParent(room, false);
        foreach (MeshFilter part in room.GetComponentsInChildren<MeshFilter>())
        {
            if (part.name != "mesh1702814703")
                continue;
            door = part.transform;
            break;
        }

        if (door != null)
        {
            originalDoorParent = door.parent;
            originalDoorPosition = door.localPosition;
            originalDoorRotation = door.localRotation;
            hinge = Child("Door Hinge", new Vector3(DoorRight, Floor, WallZ));
            door.SetParent(hinge, true);
            var leaf = door.gameObject.AddComponent<BoxCollider>();
            leaf.center = door.GetComponent<MeshFilter>().sharedMesh.bounds.center;
            leaf.size = door.GetComponent<MeshFilter>().sharedMesh.bounds.size;
            doorLeaf = leaf;
            var target = Child("Front Door Knob Interaction", new Vector3(DoorCenter - 0.22f, 0.38f, WallZ));
            var area = target.gameObject.AddComponent<BoxCollider>();
            area.size = new Vector3(0.34f, 0.45f, 0.32f);
            knockTarget = area;
            var knockControl = target.gameObject.AddComponent<XRSimpleInteractable>();
            knockControl.selectEntered.AddListener(args =>
            {
                if (IsHandGrab(args.interactorObject, area))
                    Knock(args.interactorObject as XRBaseInputInteractor);
            });
            knockAudio = Child("Door Knock Audio", new Vector3(DoorCenter, 0.60f, WallZ)).gameObject.AddComponent<AudioSource>();
            knockAudio.playOnAwake = false;
            knockAudio.spatialBlend = 0.85f;
            knockAudio.maxDistance = 7f;
            knockAudio.bypassListenerEffects = true;
            knockClip = MakeKnock();
        }

        (hazeMaterial, hazeTexture) = AddHaze();
        var pink = new Color(1f, 0.22f, 0.66f);
        var billboard = new LivingRoomBillboard(rig.transform, "Entrance Instructions",
            new Vector2(660, 240), 0.00125f, pink);
        entrancePopup = billboard.Transform;
        popupTitle = billboard.AddText("Heading", new Rect(26, 18, 608, 65), 42, pink, FontStyle.Bold);
        prompt = billboard.AddText("Action", new Rect(32, 88, 596, 128), 37, Color.white);
        entrancePopup.gameObject.SetActive(false);

        host = room.Find("DJ Host");
        ownsHost = host == null;
        if (ownsHost)
            host = CreateHost(room, model, false);

        if (host.childCount > 0)
        {
            GameObject figure = host.GetChild(0).gameObject;
            animator = figure.GetComponentInChildren<Animator>();
            if (animator != null)
            {
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
            }

            hostFooting = new PartyFooting(figure.transform, animator);
            if (cast.Models != null && animator != null && animator.isHuman)
                hostMotion = new PartyMotion(animator);
        }
        Vector3 deck = room.InverseTransformPoint(booth.position);
        route = new[]
        {
            new Vector3(-1.03f, Floor, -0.72f),
            new Vector3(-1.83f, Floor, -0.74f),
            new Vector3(deck.x - 0.58f, Floor, -0.72f),
            new Vector3(deck.x - 0.58f, Floor, deck.z - 0.28f)
        };
    }

    internal static Transform CreateHost(Transform room, GameObject model, bool authoring)
    {
        var anchor = new GameObject("DJ Host").transform;
        anchor.SetParent(room, false);
        anchor.localPosition = new Vector3(-0.62f, Floor, -0.86f);
        if (model != null)
        {
            GameObject figure = LivingRoomGuests.InstantiateGuestModel(model, anchor, authoring);
            figure.transform.localPosition = Vector3.zero;
            figure.transform.localRotation = Quaternion.identity;
            LivingRoomGuests.FitModel(figure.transform, 1.78f / room.lossyScale.y);
        }
        return anchor;
    }

    private Transform Child(string name, Vector3 position)
    {
        Transform child = new GameObject(name).transform;
        child.SetParent(rig.transform, false);
        child.localPosition = position;
        return child;
    }

    private bool Knock(XRBaseInputInteractor interactor = null)
    {
        Camera player = Camera.main;
        if (doorRequested || player == null)
            return false;

        Vector3 local = room.InverseTransformPoint(player.transform.position);
        if (!NearDoor(local))
            return false;

        approachedFromInside = local.z > WallZ;
        doorRequested = true;
        elapsedSinceKnock = knocked ? 0.42f : 0f;
        knocked = true;
        knockAudio.PlayOneShot(knockClip, 0.8f);
        interactor?.SendHapticImpulse(0.42f, 0.09f);
        return true;
    }

    private bool CheckKnockTrigger(bool nearby)
    {
        bool inReach = false;
        for (int i = 0; i < knockHands.Length; i++)
        {
            if (knockHands[i] == null)
                continue;

            XRNode node = i == 0 ? XRNode.LeftHand : XRNode.RightHand;
            InputDevice controller = InputDevices.GetDeviceAtXRNode(node);
            bool tracked = controller.isValid &&
                (!controller.TryGetFeatureValue(CommonUsages.isTracked, out bool isTracked) || isTracked);
            float trigger = 0f;
            if (tracked)
                controller.TryGetFeatureValue(CommonUsages.trigger, out trigger);
            bool pressed = trigger >= (triggerHeld[i] ? 0.36f : 0.60f);
            bool started = pressed && !triggerHeld[i];
            triggerHeld[i] = pressed;
            if (!nearby || !tracked || !knockTarget.enabled)
                continue;

            Vector3 hand = knockHands[i].position;
            bool touchesHandle = (knockTarget.ClosestPoint(hand) - hand).sqrMagnitude <= 0.12f * 0.12f;
            inReach |= touchesHandle;
            if (touchesHandle && started && Knock())
                controller.SendHapticImpulse(0u, 0.42f, 0.09f);
        }
        return inReach;
    }

    internal static bool IsHandGrab(IXRSelectInteractor hand, Collider target)
    {
        if (hand is XRDirectInteractor)
            return true;
        if (hand is not NearFarInteractor nearFar)
            return false;

        // Check the physical hand position before the toolkit updates its selection region.
        Vector3 position = nearFar.transform.position;
        return (target.ClosestPoint(position) - position).sqrMagnitude <= 0.12f * 0.12f;
    }

    private void UpdateEyeHeight(Camera player)
    {
        if (playerOrigin == null || playerOrigin.CameraFloorOffsetObject == null)
            return;

        Transform offset = playerOrigin.CameraFloorOffsetObject.transform;
        Vector3 position = offset.localPosition;
        if (playerOrigin.CurrentTrackingOriginMode == TrackingOriginModeFlags.Floor)
        {
            if (!floorTrackingOffset.HasValue &&
                InputDevices.GetDeviceAtXRNode(XRNode.Head)
                    .TryGetFeatureValue(CommonUsages.isTracked, out bool tracked) && tracked)
            {
                float trackedHeight = playerOrigin.Origin.transform.InverseTransformPoint(player.transform.position).y
                    - position.y;
                if (trackedHeight > 0.10f)
                    floorTrackingOffset = Mathf.Max(0f, StandingEyeHeight - trackedHeight);
            }
            position.y = floorTrackingOffset ?? 0f;
        }
        else
        {
            floorTrackingOffset = null;
            position.y = StandingEyeHeight;
        }

        // XROrigin applies zero offset in floor mode, so set the shared head/hand offset directly.
        if (offset.localPosition != position)
            offset.localPosition = position;
    }

    private static bool NearDoor(Vector3 player)
    {
        return player.z < -0.48f && player.z > -3.10f && Mathf.Abs(player.x - DoorCenter) < 0.95f;
    }

    public void Update(float dt, LivingRoomPartyLighting partyLighting)
    {
        if (hinge == null)
            return;

        foreach (Light spill in windowSpills)
        {
            if (spill != null)
                spill.color = WindowColor(spill.transform, partyLighting);
        }
        if (windowPanes != null)
        {
            foreach (MeshRenderer pane in windowPanes)
            {
                if (pane == null)
                    continue;
                Color glow = WindowColor(pane.transform, partyLighting);
                glow.a = 0.09f;
                pane.GetPropertyBlock(windowProperties);
                windowProperties.SetColor(BaseColor, glow);
                pane.SetPropertyBlock(windowProperties);
            }
        }
        Camera player = Camera.main;
        if (player == null)
            return;

        UpdateEyeHeight(player);

        Vector3 local = room.InverseTransformPoint(player.transform.position);
        UpdateExteriorAudio(player, local, dt);
        bool nearby = NearDoor(local);
        bool handleInReach = CheckKnockTrigger(nearby);
        if (doorRequested)
            elapsedSinceKnock += dt;

        // Wait until the player has crossed and cleared the full inward swing.
        bool inSwing = local.z > WallZ - 0.26f && local.z < WallZ + 0.95f
            && Mathf.Abs(local.x - DoorCenter) < 0.62f;
        bool crossed = approachedFromInside ? local.z < WallZ - 0.12f : local.z > WallZ + 0.12f;
        if (doorRequested && crossed && !inSwing)
            doorRequested = false;
        float target = doorRequested && elapsedSinceKnock >= 0.42f ? 1f : 0f;
        bool waitingForClearance = target > opening && inSwing && local.z > WallZ;
        if (!waitingForClearance && (target > opening || !inSwing))
            opening = Mathf.MoveTowards(opening, target, dt * 1.45f);
        hinge.localRotation = Quaternion.Euler(0f, 96f * Mathf.SmoothStep(0f, 1f, opening), 0f);
        knockTarget.enabled = !doorRequested && opening < 0.05f;

        bool entered = local.z > -1.29f;
        bool approaching = !entered && local.z > -5f && Mathf.Abs(local.x - DoorCenter) < 2f;
        bool guiding = knocked && entered && waypoint < route.Length;
        bool atBooth = knocked && entered && waypoint == route.Length
            && Vector2.Distance(new Vector2(local.x, local.z),
                new Vector2(host.localPosition.x, host.localPosition.z)) < 1.15f;
        (string title, string message) = guiding
            ? ("FIND THE DJ BOOTH", "Teleport near the host.\nFollow them around the room.")
            : atBooth ? ("YOU'RE UP NEXT", "Stand in front of the decks.\nFollow the booth's popup.")
            : waitingForClearance ? ("OPENING THE DOOR", "Step back to clear the doorway.")
            : doorRequested && opening < 0.85f ? ("KNOCK HEARD", "Wait for the door to open.")
            : opening >= 0.85f ? ("COME ON IN", "Teleport through the open door.")
            : local.z < WallZ - 0.85f ? ("FRONT DOOR", "Teleport onto the porch landing.")
            : handleInReach ? ("HANDLE IN REACH", "Squeeze your index trigger to knock.")
            : ("KNOCK ON THE DOOR", "Reach toward the handle.\nSqueeze your index trigger.");
        Vector3 doorPosition = room.TransformPoint(new Vector3(DoorCenter, local.y, WallZ));
        Vector3 doorOffset = player.transform.position - doorPosition;
        doorOffset.y = 0f;
        float closeDistance = doorPopupDismissed ? 1.0f : 0.75f;
        doorPopupDismissed = handleInReach || doorOffset.sqrMagnitude < closeDistance * closeDistance;
        bool showDoor = !doorPopupDismissed && (approaching || (nearby && opening < 0.85f));
        bool show = guiding || atBooth || showDoor;
        entrancePopup.gameObject.SetActive(show);
        if (show)
        {
            popupTitle.text = title;
            prompt.text = message;
            entrancePopup.localPosition = guiding || atBooth
                ? host.localPosition + Vector3.up * 1.80f
                : new Vector3(DoorCenter, 0.81f, entered ? -1.10f : -1.56f);
            Vector3 view = entrancePopup.position - player.transform.position;
            view.y = 0f;
            if (view.sqrMagnitude > 0.01f)
                entrancePopup.rotation = Quaternion.LookRotation(view);
        }

        bool walking = false;
        Vector3 facing = local - host.localPosition;
        if (knocked && !entered && waypoint == 0)
        {
            Vector3 doorSide = new Vector3(-0.79f, Floor, -1.03f);
            walking = MoveHost(doorSide, local, dt * 0.50f);
        }
        if (knocked && entered && waypoint < route.Length
            && Vector2.Distance(new Vector2(local.x, local.z), new Vector2(host.localPosition.x, host.localPosition.z)) < 1.45f)
        {
            Vector3 next = route[waypoint];
            facing = next - host.localPosition;
            walking = MoveHost(next, local, dt * 0.62f);
            if ((next - host.localPosition).sqrMagnitude < 0.0025f)
                waypoint++;
        }
        facing.y = 0f;
        if (facing.sqrMagnitude > 0.0025f)
            host.localRotation = Quaternion.RotateTowards(host.localRotation, Quaternion.LookRotation(facing), dt * 240f);
        if (hostMotion != null)
        {
            hostMotion.Set(walking ? cast.Walk : cast.Talk(0));
            hostMotion.Update(dt);
            hostFooting.Apply();
        }
        else if (animator != null && animator.runtimeAnimatorController != null)
            animator.SetFloat("Speed", walking ? 1f : 0f);
    }

    private Color WindowColor(Transform window, LivingRoomPartyLighting partyLighting)
    {
        // The front wall occupies the first quarter of the LED loop.
        float x = room.InverseTransformPoint(window.position).x;
        return partyLighting.Palette(0.25f * Mathf.InverseLerp(-3.375f, 0.650f, x));
    }

    private bool MoveHost(Vector3 destination, Vector3 player, float distance)
    {
        Vector3 current = host.localPosition;
        Vector3 next = Vector3.MoveTowards(current, destination, distance);
        if (LivingRoomGuests.EntersPlayerSpace(current, next, player))
            return false;

        host.localPosition = next;
        return (next - current).sqrMagnitude > 0.000001f;
    }

    private void UpdateExteriorAudio(Camera player, Vector3 local, float dt)
    {
        if (exteriorFilter == null && player.TryGetComponent<AudioListener>(out _))
        {
            exteriorFilter = player.gameObject.AddComponent<AudioLowPassFilter>();
            exteriorFilter.lowpassResonanceQ = 1f;
        }
        if (exteriorFilter == null)
            return;

        bool inside = local.z > -1.29f && local.z < 2.85f && local.x > -3.49f && local.x < 0.70f;
        float target = inside ? 0f : 1f - opening * 0.42f;
        exteriorMix = Mathf.MoveTowards(exteriorMix, target, dt * 2.5f);
        exteriorFilter.cutoffFrequency = 22000f * Mathf.Pow(850f / 22000f, exteriorMix);
        exteriorFilter.enabled = exteriorMix > 0.001f;
    }

    private static AudioClip MakeKnock()
    {
        const int rate = 24000;
        var samples = new float[rate / 3];
        var noise = new System.Random(144);
        for (int i = 0; i < samples.Length; i++)
        {
            float t = (float)i / rate;
            float hit = t < 0.125f ? t : t - 0.125f;
            float envelope = Mathf.Min(hit * 950f, 1f);
            samples[i] = envelope * (0.62f * Mathf.Sin(hit * 2f * Mathf.PI * 142f) * Mathf.Exp(-hit * 56f)
                + (float)(noise.NextDouble() * 2 - 1) * 0.32f * Mathf.Exp(-hit * 215f));
        }
        AudioClip clip = AudioClip.Create("Two taps on wood", samples.Length, 1, rate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private (Material, Texture2D) AddHaze()
    {
        if (windowPanes == null || windowPanes.Length == 0 || windowPanes[0] == null
            || windowPanes[0].sharedMaterial == null)
            return (null, null);

        const int size = 64;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        var pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            Vector2 uv = new Vector2(x, y) / (size - 1);
            float edge = Mathf.Clamp01(1f - (uv - Vector2.one * 0.5f).magnitude * 2f);
            float noise = Mathf.PerlinNoise(uv.x * 5f + 3f, uv.y * 5f);
            pixels[y * size + x] = new Color(1f, 1f, 1f, edge * edge * noise);
        }
        texture.SetPixels(pixels);
        texture.Apply(false, true);
        var material = new Material(windowPanes[0].sharedMaterial);
        material.SetTexture("_BaseMap", texture);

        material.SetFloat("_Cull", 0f);
        material.SetColor(BaseColor, new Color(0.56f, 0.50f, 0.68f, 0.075f));
        Vector3[] pockets = { new Vector3(-2.60f, 1.22f, 0.6f), new Vector3(-0.65f, 1.40f, 1.7f) };
        foreach (Vector3 center in pockets)
        {
            for (int layer = 0; layer < 3; layer++)
            {
                GameObject sheet = GameObject.CreatePrimitive(PrimitiveType.Quad);
                sheet.name = "Soft Haze Layer";
                sheet.transform.SetParent(rig.transform, false);
                sheet.transform.localPosition = center + Vector3.up * (layer * 0.16f);
                sheet.transform.localRotation = Quaternion.Euler(90f, 37f * layer, 0f);
                sheet.transform.localScale = new Vector3(2.1f, 1.45f, 1f);
                var renderer = sheet.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                var collider = sheet.GetComponent<Collider>();
                collider.enabled = false;
                Object.Destroy(collider);
            }
        }
        return (material, texture);
    }

    public void Dispose()
    {
        hostMotion?.Dispose();
        Object.Destroy(exteriorFilter);
        Object.Destroy(knockClip);
        Object.Destroy(hazeMaterial);
        Object.Destroy(hazeTexture);
        if (door != null)
        {
            door.SetParent(originalDoorParent, false);
            door.localPosition = originalDoorPosition;
            door.localRotation = originalDoorRotation;
            Object.Destroy(doorLeaf);
        }
        Object.Destroy(rig);
        if (ownsHost) Object.Destroy(host.gameObject);
    }
}
