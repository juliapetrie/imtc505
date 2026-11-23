using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

internal sealed class LivingRoomGuests
{
    private static readonly Vector3[] RiggedSpots =
    {
        new Vector3(-2.95f, 0f, -0.59f),
        new Vector3(-2.58f, 0f, -0.76f),
        new Vector3(-2.88f, 0f, 1.45f),
        new Vector3(-2.51f, 0f, 1.72f),
        new Vector3(-0.47f, 0f, -0.31f),
        new Vector3(-3.13f, 0f, 0.13f),
        new Vector3(-3.13f, 0f, 0.93f),
        new Vector3(-0.65f, 0f, 1.98f)
    };

    private static readonly Vector3[] CrowdSpots =
    {
        new Vector3(-1.48f, 0f, 1.94f),
        new Vector3(-0.43f, 0f, 1.45f),
        new Vector3(-1.66f, 0f, -0.63f)
    };

    private static readonly string[][] CrowdPieces =
    {
        new[] { "mesh611322402", "mesh1317379966", "mesh196049092" },
        new[] { "mesh2088876365", "mesh1208982426", "mesh1738346738", "mesh203651746" },
        new[] { "mesh938549224", "mesh1213976047", "mesh2058805562", "mesh421297011" }
    };

    private readonly struct Guest
    {
        public readonly Transform body;
        public readonly Transform head;
        public readonly Transform upperArm;
        public readonly Transform lowerArm;
        public readonly Transform hand;
        public readonly Quaternion facing;
        public readonly float phase;
        public readonly bool dances;
        public readonly bool greets;

        public readonly Vector3 spot;
        public readonly Animator animator;
        public readonly Transform leftArm;
        public readonly Transform leftForearm;
        public readonly Transform leftHand;
        public readonly Transform leftThigh;
        public readonly Transform rightThigh;
        public readonly Transform leftShin;
        public readonly Transform rightShin;
        public readonly Transform leftAnkle;
        public readonly Transform rightAnkle;
        public readonly int danceStyle;

        public Guest(Transform body, int index, bool dances, bool greets)
        {
            this.body = body;
            facing = body.rotation;
            phase = index * 1.73f;
            danceStyle = index % 3;
            this.dances = dances;
            this.greets = greets;

            spot = body.localPosition;
            animator = body.GetComponentInChildren<Animator>();
            leftArm = leftForearm = leftHand = leftThigh = rightThigh = leftShin = rightShin = null;
            leftAnkle = rightAnkle = null;
            head = null;
            upperArm = null;
            lowerArm = null;
            hand = null;
            if (!greets && !dances)
                return;

            if (animator != null && animator.isHuman)
            {
                head = animator.GetBoneTransform(HumanBodyBones.Head);
                upperArm = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
                lowerArm = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
                hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
                return;
            }
            foreach (Transform bone in body.GetComponentsInChildren<Transform>())
            {
                switch (bone.name)
                {
                    case "Head": head = bone; break;
                    case "UpperArm.R": upperArm = bone; break;
                    case "LowerArm.R": lowerArm = bone; break;
                    case "MiddleHand.R": hand = bone; break;
                    case "UpperArm.L": leftArm = bone; break;
                    case "LowerArm.L": leftForearm = bone; break;
                    case "MiddleHand.L": leftHand = bone; break;
                    case "UpperLeg.L": leftThigh = bone; break;
                    case "UpperLeg.R": rightThigh = bone; break;
                    case "LowerLeg.L": leftShin = bone; break;
                    case "LowerLeg.R": rightShin = bone; break;
                    case "LowerLeg.L_end": leftAnkle = bone; break;
                    case "LowerLeg.R_end": rightAnkle = bone; break;
                }
            }
        }
    }

    private readonly LivingRoomMixamo cast;
    private readonly Dictionary<Transform, PartyMotion> performances = new();
    private readonly Dictionary<Transform, PartyFooting> footing = new();
    private float partyTime;
    private readonly List<Guest> guests = new List<Guest>();
    private readonly Dictionary<Transform, Wanderer> walkers = new();
    private readonly NavMeshData navigation;
    private NavMeshDataInstance navigationInstance;
    private readonly List<(Transform bone, Quaternion rotation)> animatedBones = new();
    private readonly Transform group;
    private readonly bool ownsGroup;
    private readonly Transform room;
    private readonly Transform booth;
    private float greetingTime = 10f;
    private float danceTime;
    private float danceEnergy;
    private float entryDelay = 0.65f;
    private bool greeted;

    public LivingRoomGuests(Transform room, Transform booth, GameObject[] models,
        RuntimeAnimatorController idleController, GameObject crowdModel, LivingRoomMixamo cast, bool authoring = false)
    {
        this.room = room;
        this.booth = booth;
        this.cast = cast;
        group = room.Find("Living Room Guests");
        ownsGroup = group == null;
        if (ownsGroup)
        {
            group = new GameObject("Living Room Guests").transform;
            group.SetParent(room, false);
        }
        if (!authoring)
            navigation = BuildRoomNavMesh(room.lossyScale.x);
        if (navigation != null)
            navigationInstance = NavMesh.AddNavMeshData(navigation, room.position, room.rotation);
        if (models != null && models.Length > 0)
        {
            Animator referenceAnimator = models[0] != null ? models[0].GetComponentInChildren<Animator>() : null;
            for (int i = 0; i < RiggedSpots.Length + (cast.Models != null ? CrowdSpots.Length : 0); i++)
            {
                GameObject source = models[i % models.Length];
                if (source == null)
                    continue;

                Transform guest = ownsGroup
                    ? CreateAnchor(i < RiggedSpots.Length ? RiggedSpots[i] : CrowdSpots[i - RiggedSpots.Length], i)
                    : group.Find("Party Guest " + (i + 1));
                if (guest == null || (!ownsGroup && guest.childCount == 0))
                    continue;

                GameObject model;
                if (ownsGroup)
                {
                    model = InstantiateGuestModel(source, guest, authoring);
                    model.transform.localPosition = Vector3.zero;
                    model.transform.localRotation = Quaternion.identity;
                    FitModel(model.transform, (1.74f + (i % 3) * 0.035f) / room.lossyScale.y);
                }
                else
                {
                    model = guest.GetChild(0).gameObject;
                }
                Animator animator = model.GetComponentInChildren<Animator>();
                if (animator == null)
                    animator = model.AddComponent<Animator>();
                if (animator.avatar == null && referenceAnimator != null)
                    animator.avatar = referenceAnimator.avatar;
                animator.runtimeAnimatorController = idleController;
                animator.applyRootMotion = false;
                if (authoring)
                    continue;

                var partyGuest = new Guest(guest, i, i == 1 || i == 2 || i == 3 || i == 4
                    || i == 5 || i == 6 || i == 7, i == 0 || i == 2);
                guests.Add(partyGuest);
                if (cast.Models != null)
                {
                    performances.Add(guest, new PartyMotion(animator));
                    footing.Add(guest, new PartyFooting(model.transform, animator));
                }
                if (navigationInstance.valid && partyGuest.animator != null &&
                    (i == 1 || i == 3 || i == 6 || i == 7))
                    AddWalker(partyGuest, i);
            }
        }

        if (crowdModel != null && cast.Models == null && ownsGroup && !authoring)
            AddCrowd(crowdModel);
    }

    internal static GameObject InstantiateGuestModel(GameObject source, Transform parent, bool authoring)
    {
#if UNITY_EDITOR
        if (authoring)
            return (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(source, parent);
#endif
        return Object.Instantiate(source, parent, false);
    }

    private static NavMeshData BuildRoomNavMesh(float roomScale)
    {
        NavMeshBuildSettings settings = NavMesh.GetSettingsByID(0);
        settings.agentRadius = 0.12f;
        settings.agentHeight = 1.30f;
        settings.agentClimb = 0.12f;
        settings.minRegionArea = 0.08f;
        settings.overrideVoxelSize = true;
        settings.voxelSize = 0.035f;
        var sources = new List<NavMeshBuildSource>
        {
            new NavMeshBuildSource
            {
                shape = NavMeshBuildSourceShape.Box,
                transform = Matrix4x4.Translate(new Vector3(-1.365f, -0.525f, 0.59f)),
                size = new Vector3(4.03f, 0.10f, 3.68f),
                area = 0
            }
        };
        // Bound the usable floor and mark the furniture footprints as non-walkable.
        AddFootprint(sources, -1.87f, 0.72f, 0.80f, 1.86f); // DJ table and speakers
        AddFootprint(sources, -1.06f, 0.60f, 0.54f, 1.30f); // couch
        AddFootprint(sources, -1.065f, 1.52f, 0.74f, 0.76f); // full lampshade plus arm clearance
        AddFootprint(sources, 0.33f, 0.65f, 0.54f, 1.0f); // TV console
        AddFootprint(sources, 0.21f, -0.29f, 0.86f, 0.73f); // speaker cluster
        AddFootprint(sources, 0.25f, 1.88f, 0.86f, 0.90f); // speaker cluster and cabinet

        settings.agentRadius *= roomScale;
        settings.agentHeight *= roomScale;
        settings.agentClimb *= roomScale;
        for (int i = 0; i < sources.Count; i++)
        {
            NavMeshBuildSource source = sources[i];
            source.transform = Matrix4x4.Translate((Vector3)source.transform.GetColumn(3) * roomScale);
            source.size *= roomScale;
            sources[i] = source;
        }

        return NavMeshBuilder.BuildNavMeshData(settings, sources,
            new Bounds(new Vector3(-1.365f, 0.10f, 0.59f) * roomScale,
                new Vector3(4.25f, 1.8f, 3.90f) * roomScale),
            Vector3.zero, Quaternion.identity);
    }

    private static void AddFootprint(List<NavMeshBuildSource> sources, float x, float z, float width, float depth)
    {
        sources.Add(new NavMeshBuildSource
        {
            shape = NavMeshBuildSourceShape.ModifierBox,
            transform = Matrix4x4.Translate(new Vector3(x, -0.45f, z)),
            size = new Vector3(width, 0.40f, depth),
            area = 1
        });
    }

    private void AddWalker(Guest guest, int index)
    {
        if (!NavMesh.SamplePosition(guest.body.position, out NavMeshHit home, 0.27f, NavMesh.AllAreas))
            return;

        Vector3[] localStops = index switch
        {
            1 => new[] { new Vector3(-2.53f, -0.475f, 0.47f), new Vector3(-2.96f, -0.475f, -0.17f) },
            3 => new[] { new Vector3(-2.60f, -0.475f, 0.91f), new Vector3(-2.90f, -0.475f, 1.84f) },
            6 => new[] { new Vector3(-3.04f, -0.475f, 1.50f), new Vector3(-2.77f, -0.475f, 0.18f) },
            _ => new[] { new Vector3(-0.55f, -0.475f, 1.30f), new Vector3(-0.44f, -0.475f, 0.38f) }
        };
        var stops = new List<Vector3> { home.position };
        var path = new NavMeshPath();
        foreach (Vector3 localStop in localStops)
        {
            if (NavMesh.SamplePosition(room.TransformPoint(localStop), out NavMeshHit stop, 0.23f, NavMesh.AllAreas)
                && NavMesh.CalculatePath(home.position, stop.position, NavMesh.AllAreas, path)
                && path.status == NavMeshPathStatus.PathComplete)
                stops.Add(stop.position);
        }

        if (stops.Count < 2)
            return;

        guest.body.position = home.position;
        var agent = guest.body.gameObject.AddComponent<NavMeshAgent>();
        agent.radius = 0.12f * room.lossyScale.x;
        agent.height = 1.30f * room.lossyScale.y;
        agent.speed = 0.52f;
        agent.acceleration = 2.5f;
        agent.stoppingDistance = 0.075f;
        agent.avoidancePriority = 35 + index * 4;
        agent.updatePosition = false;
        agent.updateRotation = false;
        if (!agent.isOnNavMesh)
        {
            Object.Destroy(agent);
            return;
        }
        walkers.Add(guest.body, new Wanderer(agent, guest.animator, stops, index));
    }

    internal const float PlayerBuffer = 0.62f;

    internal static bool EntersPlayerSpace(Vector3 current, Vector3 next, Vector3 player)
    {
        Vector3 fromPlayer = Vector3.ProjectOnPlane(current - player, Vector3.up);
        Vector3 step = Vector3.ProjectOnPlane(next - current, Vector3.up);
        if (step.sqrMagnitude < 0.000001f)
            return false;

        // If the player approaches a guest, allow the guest to move farther away.
        if (fromPlayer.sqrMagnitude < PlayerBuffer * PlayerBuffer)
            return Vector3.Dot(fromPlayer, step) < 0f;

        float along = Mathf.Clamp01(-Vector3.Dot(fromPlayer, step) / step.sqrMagnitude);
        return (fromPlayer + step * along).sqrMagnitude < PlayerBuffer * PlayerBuffer;
    }

    private sealed class Wanderer
    {
        public NavMeshAgent Agent { get; }
        public bool IsWalking { get; private set; }
        public Quaternion Facing { get; private set; }

        private readonly Animator animator;
        private readonly List<Vector3> stops;
        private float pause;
        private float walkingTime;
        private int stop;
        private readonly int index;

        public Wanderer(NavMeshAgent agent, Animator animator, List<Vector3> stops, int index)
        {
            Agent = agent;
            this.animator = animator;
            this.stops = stops;
            this.index = index;
            Facing = agent.transform.rotation;
            pause = 1.6f + index * 0.39f;
        }

        public void Update(float elapsed, Vector3? player)
        {
            if (!Agent.isOnNavMesh)
                return;

            if (!IsWalking)
            {
                pause -= elapsed;
                if (pause > 0f)
                    return;

                stop = (stop + 1) % stops.Count;
                Vector3 separation = stops[stop] - (player ?? stops[stop]);
                separation.y = 0f;
                IsWalking = (!player.HasValue || separation.sqrMagnitude >= PlayerBuffer * PlayerBuffer)
                    && Agent.SetDestination(stops[stop]);
                walkingTime = 0f;
                Facing = Agent.transform.rotation;
                if (!IsWalking)
                    pause = 2f;
                return;
            }

            if (player.HasValue && EntersPlayerSpace(Agent.transform.position, Agent.nextPosition, player.Value))
            {
                Agent.ResetPath();
                Agent.nextPosition = Agent.transform.position;
                IsWalking = false;
                if (animator.runtimeAnimatorController != null)
                    animator.SetFloat("Speed", 0f);
                pause = 1.25f;
                return;
            }

            walkingTime += elapsed;
            Vector3 heading = Vector3.ProjectOnPlane(Agent.desiredVelocity, Vector3.up);
            if (heading.sqrMagnitude > 0.0025f)
                Facing = Quaternion.RotateTowards(Facing, Quaternion.LookRotation(heading), elapsed * 245f);
            Agent.transform.SetPositionAndRotation(Agent.nextPosition, Facing);
            if (animator.runtimeAnimatorController != null) animator.SetFloat("Speed", Agent.velocity.sqrMagnitude > 0.005f ? 1f : 0f);

            if (Agent.pathPending || (walkingTime < 10f && Agent.pathStatus == NavMeshPathStatus.PathComplete
                && Agent.remainingDistance > Agent.stoppingDistance))
                return;

            Agent.ResetPath();
            IsWalking = false;
            if (animator.runtimeAnimatorController != null) animator.SetFloat("Speed", 0f);
            pause = 3.5f + (index % 3) * 0.85f + stop * 0.4f;
        }
    }

    private Transform CreateAnchor(Vector3 position, int index)
    {
        var guest = new GameObject("Party Guest " + (index + 1)).transform;
        guest.SetParent(group, false);
        position.y = -0.475f;
        guest.localPosition = position;
        Vector3 groupCenter = new Vector3(index >= 4 ? -1.15f : -2.74f, position.y,
            position.z < 0f ? -0.35f : 1.22f);
        guest.localRotation = Quaternion.LookRotation(groupCenter - position);
        return guest;
    }

    private void AddCrowd(GameObject crowdModel)
    {
        GameObject source = Object.Instantiate(crowdModel, group, false);
        var pieces = new Dictionary<string, Transform>();
        foreach (MeshFilter piece in source.GetComponentsInChildren<MeshFilter>())
            pieces[piece.name] = piece.transform;

        for (int i = 0; i < CrowdPieces.Length; i++)
        {
            Transform guest = CreateAnchor(CrowdSpots[i], RiggedSpots.Length + i);
            var model = new GameObject("Crowd Figure").transform;
            model.SetParent(group, false);
            foreach (string piece in CrowdPieces[i])
            {
                if (pieces.TryGetValue(piece, out Transform part))
                    part.SetParent(model, true);
            }
            if (model.childCount != CrowdPieces[i].Length)
            {
                Object.Destroy(guest.gameObject);
                Object.Destroy(model.gameObject);
                continue;
            }

            model.SetParent(guest, false);
            model.localRotation = Quaternion.Euler(0f, -90f, 0f);
            FitModel(model, 1.30f + i * 0.045f);
            guests.Add(new Guest(guest, i + RiggedSpots.Length, i != 1, false));
        }

        source.SetActive(false);
        Object.Destroy(source);
    }

    internal static void FitModel(Transform model, float height)
    {
        Renderer[] renderers = model.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
            return;

        Bounds bounds = LocalBounds(model.parent, renderers);
        model.localScale *= height / Mathf.Max(bounds.size.y, 0.01f);
        bounds = LocalBounds(model.parent, renderers);
        model.localPosition -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
    }

    private static Bounds LocalBounds(Transform relativeTo, Renderer[] renderers)
    {
        Bounds result = new Bounds(relativeTo.InverseTransformPoint(renderers[0].bounds.center), Vector3.zero);
        foreach (Renderer renderer in renderers)
        {
            Bounds bounds = renderer.localBounds;
            if (renderer is SkinnedMeshRenderer skin && skin.sharedMesh != null)
            {
                // Imported skinned bounds include animation padding, which lifts the soles off the floor.
                var posedMesh = new Mesh();
                skin.BakeMesh(posedMesh);
                if (posedMesh.vertexCount > 0)
                    bounds = posedMesh.bounds;
#if UNITY_EDITOR
                if (!Application.isPlaying)
                    Object.DestroyImmediate(posedMesh);
                else
#endif
                    Object.Destroy(posedMesh);
            }
            for (int corner = 0; corner < 8; corner++)
            {
                var sign = new Vector3((corner & 1) == 0 ? -1f : 1f,
                    (corner & 2) == 0 ? -1f : 1f, (corner & 4) == 0 ? -1f : 1f);
                Vector3 cornerPoint = bounds.center + Vector3.Scale(bounds.extents, sign);
                result.Encapsulate(relativeTo.InverseTransformPoint(renderer.transform.TransformPoint(cornerPoint)));
            }
        }
        return result;
    }

    public void Update(float pulse, float elapsed)
    {
        foreach (var pose in animatedBones)
        {
            if (pose.bone != null)
                pose.bone.localRotation = pose.rotation;
        }
        animatedBones.Clear();

        Camera player = Camera.main;
        Vector3 playerPosition = player != null ? player.transform.position : room.position;
        Vector3 local = room.InverseTransformPoint(playerPosition);
        bool inside = player != null && local.x > -3.35f && local.x < 0.65f
            && local.z > -1.25f && local.z < 2.45f;
        if (!inside)
        {
            greeted = false;
            entryDelay = 0.65f;
        }
        else if (!greeted)
        {
            entryDelay -= elapsed;
            if (entryDelay <= 0f)
            {
                greeted = true;
                greetingTime = 0f;
            }
        }

        greetingTime += elapsed;
        partyTime += elapsed;
        danceEnergy = Mathf.Lerp(danceEnergy, pulse, 1f - Mathf.Exp(-elapsed * 2f));
        danceTime += elapsed * (5.75f + danceEnergy * 1.45f);
        float attention = Mathf.SmoothStep(0f, 1f, greetingTime / 0.40f)
            * (1f - Mathf.SmoothStep(0f, 1f, (greetingTime - 3.15f) / 0.9f));
        foreach (Guest guest in guests)
        {
            Quaternion facing = SocialFacing(guest);
            if (guest.greets && attention > 0f)
            {
                Vector3 toward = Vector3.ProjectOnPlane(playerPosition - guest.body.position, Vector3.up);
                if (toward.sqrMagnitude > 0.015f)
                    facing = Quaternion.Slerp(facing, Quaternion.LookRotation(toward), attention);
            }

            Vector3 position = guest.spot;
            if (walkers.TryGetValue(guest.body, out Wanderer walker))
            {
                walker.Update(elapsed, inside ? playerPosition : null);
                position = group.InverseTransformPoint(walker.Agent.nextPosition);
                if (walker.IsWalking)
                {
                    if (performances.TryGetValue(guest.body, out PartyMotion walkMotion))
                    {
                        walkMotion.Set(cast.Walk);
                        walkMotion.Update(elapsed);
                    }
                    continue;
                }
            }

            if (performances.TryGetValue(guest.body, out PartyMotion partyMotion))
            {
                float cycle = (partyTime + guest.phase * 1.6f) / 9f;
                int turn = Mathf.FloorToInt(cycle);
                bool dancingNow = Mathf.Repeat(cycle, 1f) < (guest.dances ? 0.68f : 0.26f);
                partyMotion.Set(dancingNow ? cast.Dance(turn + guest.danceStyle)
                    : cast.Talk(turn + guest.danceStyle), guest.phase * 0.13f, dancingNow ? 0.65f : 1f);
                partyMotion.Update(elapsed);
                guest.body.rotation = Quaternion.RotateTowards(guest.body.rotation, facing, elapsed * 150f);
                guest.body.localPosition = position;
                continue;
            }

            float phase = danceTime + guest.phase;
            float beat = Mathf.Sin(phase);
            bool dancing = guest.dances && guest.animator != null;
            float danceWeight = guest.greets ? 1f - attention : 1f;
            if (dancing)
            {
                float bounce = 0.5f + 0.5f * Mathf.Cos(phase * 2f);
                position.y -= 0.012f * bounce * danceWeight;
                guest.body.rotation = Quaternion.RotateTowards(guest.body.rotation, facing * Quaternion.Euler(
                    1.5f * bounce * danceWeight,
                    0f,
                    1.3f * beat * danceWeight), elapsed * 150f);
            }
            else
            {
                float sway = beat * (guest.dances ? 3.5f : 1.1f);
                position.y += guest.dances ? 0.024f * beat * beat : 0f;
                guest.body.rotation = Quaternion.RotateTowards(guest.body.rotation,
                    facing * Quaternion.Euler(0f, sway * 0.6f, sway), elapsed * 150f);
            }
            guest.body.localPosition = position;
        }
    }

    private Quaternion SocialFacing(Guest guest)
    {
        Vector3 center = room.TransformPoint(new Vector3(-1.4f, 0f, 0.58f));
        Vector3 inward = Vector3.ProjectOnPlane(center - guest.body.position, Vector3.up);
        Vector3 target = inward.sqrMagnitude >= 0.01f ? inward : guest.facing * Vector3.forward;
        float closest = 1.2f * 1.2f;
        foreach (Guest other in guests)
        {
            if (other.body == guest.body || (walkers.TryGetValue(other.body, out Wanderer walker) && walker.IsWalking))
                continue;

            Vector3 direction = Vector3.ProjectOnPlane(other.body.position - guest.body.position, Vector3.up);
            float distance = direction.sqrMagnitude;
            if (distance <= 0.08f || distance >= closest)
                continue;

            Vector3 otherInward = Vector3.ProjectOnPlane(center - other.body.position, Vector3.up);
            Vector3 heading = direction.normalized;
            if ((inward.sqrMagnitude >= 0.01f && Vector3.Dot(inward.normalized, heading) < 0f)
                || (otherInward.sqrMagnitude >= 0.01f && Vector3.Dot(otherInward.normalized, -heading) < 0f))
                continue;

            closest = distance;
            target = direction;
        }
        return Quaternion.LookRotation(target);
    }

    public void LateUpdate()
    {
        foreach (Guest guest in guests)
        {
            if (footing.TryGetValue(guest.body, out PartyFooting feet))
                feet.Apply();
            bool walking = walkers.TryGetValue(guest.body, out Wanderer walker) && walker.IsWalking;
            if (!performances.ContainsKey(guest.body) && guest.dances && guest.animator != null && !walking && !(guest.greets && greetingTime < 4.05f))
                DancePose(guest);
        }

        if (greetingTime >= 4.05f)
            return;

        Camera player = Camera.main;
        if (player == null)
            return;

        float gesture = Mathf.SmoothStep(0f, 1f, (greetingTime - 0.65f) / 0.40f)
            * (1f - Mathf.SmoothStep(0f, 1f, (greetingTime - 2.55f) / 0.55f));
        foreach (Guest guest in guests)
        {
            if (!guest.greets)
                continue;

            if (guest.head != null)
            {
                Vector3 look = player.transform.position - guest.head.position;
                if (look.sqrMagnitude > 0.01f)
                {
                    Quaternion turn = Quaternion.FromToRotation(guest.body.forward, look.normalized);
                    Remember(guest.head);
                    guest.head.rotation = Quaternion.RotateTowards(Quaternion.identity, turn, 18f) * guest.head.rotation;
                }
            }

            if (gesture <= 0f || guest.upperArm == null || guest.lowerArm == null || guest.hand == null)
                continue;

            Vector3 target = booth.position + Vector3.up * 0.31f;
            Vector3 direction = (target - guest.upperArm.position).normalized;
            AimBone(guest.upperArm, guest.lowerArm, direction + Vector3.down * 0.20f, gesture);
            AimBone(guest.lowerArm, guest.hand, direction + Vector3.up * 0.10f, gesture);
        }
    }

    private void DancePose(Guest guest)
    {
        float phase = danceTime + guest.phase;
        float beat = Mathf.Sin(phase);
        float rightStep = Mathf.Max(0f, beat);
        float leftStep = Mathf.Max(0f, -beat);
        Vector3 right = guest.body.right;
        Vector3 front = guest.body.forward;

        float dip = 0.5f + 0.5f * Mathf.Cos(phase * 2f);
        DanceLeg(guest.rightThigh, guest.rightShin, guest.rightAnkle, front, dip);
        DanceLeg(guest.leftThigh, guest.leftShin, guest.leftAnkle, front, dip);

        // Give the group a mix of arm pumps, outward reaches, and higher accents.
        float rightAccent = Mathf.SmoothStep(0f, 1f, rightStep);
        float leftAccent = Mathf.SmoothStep(0f, 1f, leftStep);
        float spread = guest.danceStyle == 1 ? 0.82f : 0.48f;
        float reach = guest.danceStyle == 2 ? 0.82f : 0.50f;
        float lift = guest.danceStyle == 0 ? 0.85f : 0.44f;

        AimBone(guest.upperArm, guest.lowerArm, right * spread + front * reach
            + Vector3.up * (-0.48f + rightAccent * lift), 0.89f);
        AimBone(guest.lowerArm, guest.hand, front * (0.48f + rightAccent * 0.52f)
            + Vector3.up * (0.82f + rightAccent * 0.28f), 0.86f);
        AimBone(guest.leftArm, guest.leftForearm, -right * spread + front * reach
            + Vector3.up * (-0.48f + leftAccent * lift), 0.89f);
        AimBone(guest.leftForearm, guest.leftHand, front * (0.48f + leftAccent * 0.52f)
            + Vector3.up * (0.82f + leftAccent * 0.28f), 0.86f);

        if (guest.head != null)
        {
            Remember(guest.head);
            guest.head.rotation = Quaternion.AngleAxis(Mathf.Cos(phase * 2f) * 5f, right) * guest.head.rotation;
        }
    }

    private void DanceLeg(Transform thigh, Transform shin, Transform ankle, Vector3 front, float dip)
    {
        if (thigh == null || shin == null || ankle == null)
            return;

        Vector3 thighDirection = (shin.position - thigh.position).normalized;
        Vector3 shinDirection = (ankle.position - shin.position).normalized;
        AimBone(thigh, shin, thighDirection + front * (0.12f * dip), 0.72f);
        AimBone(shin, ankle, shinDirection - front * (0.10f * dip), 0.72f);
    }

    private void Remember(Transform bone)
    {
        animatedBones.Add((bone, bone.localRotation));
    }

    private void AimBone(Transform bone, Transform child, Vector3 direction, float weight)
    {
        if (bone == null || child == null)
            return;

        Remember(bone);
        Quaternion turn = Quaternion.FromToRotation(child.position - bone.position, direction);
        bone.rotation = Quaternion.Slerp(Quaternion.identity, turn, weight * 0.88f) * bone.rotation;
    }

    public void Dispose()
    {
        foreach (PartyMotion motion in performances.Values)
            motion.Dispose();
        navigationInstance.Remove();
        Object.Destroy(navigation);
        if (ownsGroup && group != null)
            Object.Destroy(group.gameObject);
    }
}

internal sealed class PartyFooting
{
    private readonly Transform model;
    private readonly Transform leftFoot;
    private readonly Transform rightFoot;
    private readonly float restModelY;
    private readonly float restFootY;

    public PartyFooting(Transform model, Animator animator)
    {
        this.model = model;
        if (animator == null || !animator.isHuman)
            return;

        leftFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
        rightFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot);
        if (leftFoot == null || rightFoot == null)
            return;

        restModelY = model.localPosition.y;
        restFootY = FootHeight();
    }

    public void Apply()
    {
        if (leftFoot == null || rightFoot == null)
            return;

        Vector3 position = model.localPosition;
        float poseHeight = FootHeight() - (position.y - restModelY);
        position.y = restModelY + Mathf.Clamp(restFootY - poseHeight, -0.23f, 0.23f);
        model.localPosition = position;
    }

    private float FootHeight()
    {
        return Mathf.Min(model.parent.InverseTransformPoint(leftFoot.position).y,
            model.parent.InverseTransformPoint(rightFoot.position).y);
    }
}
