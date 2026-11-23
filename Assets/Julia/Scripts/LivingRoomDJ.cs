using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Filtering;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Movement;

public sealed class LivingRoomDJ : MonoBehaviour
{
    [SerializeField, Min(0f)] private float platterRpm = 33.33f;
    [SerializeField, Min(1f)] private float demoBpm = 120f;
    [SerializeField, Range(0f, 0.4f)] private float speakerTravel = 0.22f;
    [Tooltip("Optional. Leave empty to react to the scene's audio mix.")]
    [SerializeField] private AudioSource musicSource;
    [Tooltip("Optional second track. Assign a separate AudioSource with a clip to unlock the crossfade step.")]
    [SerializeField] private AudioSource secondMusicSource;
    [SerializeField] private AudioClip[] songChoices;

    [SerializeField] private Transform roomModel;
    [SerializeField] private MeshRenderer[] exteriorWindowPanes;
    [SerializeField] private GameObject[] guestModels;
    [SerializeField] private RuntimeAnimatorController guestIdleController;
    [SerializeField] private GameObject crowdModel;
    [SerializeField] private AnimationClip guestSittingClip;
    private LivingRoomCouch couch;
    private LivingRoomGuests guests;
    private LivingRoomEntrance entrance;

    private readonly List<RoomSpeaker> roomSpeakers = new List<RoomSpeaker>();
    private readonly float[] spectrum = new float[512];
    private readonly float[] rightSpectrum = new float[512];
    private readonly Transform[] cones = new Transform[2];
    private LivingRoomDJChallenge challenge;
    private const int MeterBars = 16;
    private readonly float[] meterLevels = new float[MeterBars];
    private readonly Vector3[] meterVertices = new Vector3[MeterBars * 4];
    private GameObject soundDisplay;
    private Mesh meterMesh;
    private Material meterInk;
    private Material meterBackground;
    private float deckUnit;
    private MeshFilter deck;
    private Mesh originalMesh;
    private Mesh animatedMesh;
    private Vector3[] restVertices;
    private Vector3[] vertices;
    private Vector3[] restNormals;
    private Vector3[] normals;
    private int[] platterSides;
    private Vector3 platterCenter;
    private Material cabinetMaterial;
    private Material coneMaterial;
    private float angle;
    private float pulse;
    private LivingRoomPartyLighting partyLighting;
    private MeshFilter screenFilter;
    private MeshRenderer screenRenderer;
    private Mesh originalScreen;
    private Mesh animatedScreen;
    private Material[] screenMaterials;
    private Material dotMaterial;
    private Vector3[] screenRest;
    private Vector3[] screenVertices;
    private int[] dotVertices;
    private Vector3 dotCenter;
    private float dotRadius;
    private float dotPhase;
    private float peak;
    private float averageEnergy;
    [SerializeField, Range(0f, 2f)] private float pictureWobbleDegrees = 0.85f;
    private readonly List<WallPicture> wallPictures = new List<WallPicture>();
    private float bassAverage;
    private float bassPeak;
    private float pictureShake;
    private float pictureTime;
    private float audioHold;
    private float demoPhase;
    private float forward;
    private float coneRestX;

#if UNITY_EDITOR
    [UnityEditor.MenuItem("Tools/Living Room/Normalize NPC Sizes")]
    private static void NormalizeActiveSceneNpcs()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (Application.isPlaying || scene.path != "Assets/Julia/Scenes/LivingRoom.unity")
            return;

        foreach (var root in scene.GetRootGameObjects())
        {
            var dj = root.GetComponentInChildren<LivingRoomDJ>();
            if (dj != null && dj.roomModel != null)
            {
                dj.NormalizeNpcSizes();
                return;
            }
        }
    }

    private void NormalizeNpcSizes()
    {
        Transform standing = roomModel.Find("Living Room Guests");
        if (standing == null)
            return;

        var guests = new List<(Transform model, Transform pivot, float size)>();
        foreach (Transform guest in standing)
            MeasureNpc(guest, false, guests);
        if (guests.Count == 0)
            return;

        var sizes = new List<float>();
        foreach (var guest in guests)
            sizes.Add(guest.size);
        sizes.Sort();
        float typicalSize = sizes[sizes.Count / 2];

        MeasureNpc(roomModel.Find("DJ Host"), false, guests);
        Transform couchGuests = roomModel.Find("Couch Guests");
        if (couchGuests != null)
        {
            foreach (Transform guest in couchGuests)
                MeasureNpc(guest, true, guests);
        }

        foreach (var guest in guests)
        {
            float correction = typicalSize / guest.size;
            // Preserve small differences between adult characters.
            if (correction >= 0.94f && correction <= 1.06f)
                continue;

            Vector3 fixedPoint = guest.pivot.position;
            UnityEditor.Undo.RecordObject(guest.model, "Normalize NPC size");
            guest.model.localScale *= correction;
            guest.model.position += fixedPoint - guest.pivot.position;
            UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(guest.model);
            Debug.Log($"Resized {guest.model.parent.name} by {correction:P0} of its previous scale.");
        }
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
    }

    private static void MeasureNpc(Transform guest, bool seated,
        List<(Transform model, Transform pivot, float size)> guests)
    {
        if (guest == null || guest.childCount == 0)
            return;

        Transform model = guest.GetChild(0);
        Animator animator = model.GetComponentInChildren<Animator>();
        if (animator == null || !animator.isHuman)
            return;

        Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips);
        Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
        Transform left = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
        Transform right = animator.GetBoneTransform(HumanBodyBones.RightFoot);
        if (hips == null || head == null || left == null || right == null)
            return;

        // Bone lengths stay consistent when the same rig sits, dances, or stands.
        float size = BoneChainLength(head, hips)
            + (BoneChainLength(left, hips) + BoneChainLength(right, hips)) * 0.5f;
        Transform pivot = seated ? hips : (left.position.y < right.position.y ? left : right);
        if (size > 0.1f)
            guests.Add((model, pivot, size));
    }

    private static float BoneChainLength(Transform bone, Transform ancestor)
    {
        float length = 0f;
        while (bone != null && bone != ancestor && bone.parent != null)
        {
            length += Vector3.Distance(bone.position, bone.parent.position);
            bone = bone.parent;
        }
        return bone == ancestor ? length : 0f;
    }

    [UnityEditor.MenuItem("Tools/Living Room/Place NPCs in Scene")]
    private static void PlaceActiveSceneNpcs()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (scene.path != "Assets/Julia/Scenes/LivingRoom.unity")
            return;

        foreach (var root in scene.GetRootGameObjects())
        {
            var dj = root.GetComponentInChildren<LivingRoomDJ>();
            if (dj != null)
            {
                dj.PlaceNpcsInScene();
                return;
            }
        }
    }

    [ContextMenu("Place NPCs in Scene")]
    private void PlaceNpcsInScene()
    {
        if (Application.isPlaying || roomModel == null)
            return;

        var cast = new LivingRoomMixamo();
        GameObject[] models = cast.Models ?? guestModels;
        if (roomModel.Find("Living Room Guests") == null)
        {
            _ = new LivingRoomGuests(roomModel, transform, models, guestIdleController, crowdModel, cast, authoring: true);
            UnityEditor.Undo.RegisterCreatedObjectUndo(roomModel.Find("Living Room Guests").gameObject, "Place standing guests");
        }
        if (roomModel.Find("Couch Guests") == null)
        {
            _ = new LivingRoomCouch(roomModel, guestModels, guestSittingClip, cast, authoring: true);
            UnityEditor.Undo.RegisterCreatedObjectUndo(roomModel.Find("Couch Guests").gameObject, "Place couch guests");
        }
        if (roomModel.Find("DJ Host") == null)
        {
            GameObject model = models != null && models.Length > 0 ? models[0] : null;
            Transform host = LivingRoomEntrance.CreateHost(roomModel, model, true);
            UnityEditor.Undo.RegisterCreatedObjectUndo(host.gameObject, "Place DJ host");
        }
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
    }
#endif

    private void Start()
    {
        deck = GetComponentInChildren<MeshFilter>();
        if (deck == null || deck.sharedMesh == null || !deck.sharedMesh.isReadable)
        {
            Debug.LogWarning("Living Room DJ needs the readable DJGear mesh.", this);
            enabled = false;
            return;
        }

        originalMesh = deck.sharedMesh;
        animatedMesh = Instantiate(originalMesh);
        animatedMesh.MarkDynamic();
        deck.sharedMesh = animatedMesh;

        Bounds bounds = originalMesh.bounds;
        // The OBJ's authored dimensions are used to identify the two disconnected platter meshes.
        float unit = bounds.size.z / 16.506f;
        forward = bounds.center.x <= 0f ? 1f : -1f;
        platterCenter = new Vector3(-0.165638f * forward, 0f, 5.619958f) * unit;
        restVertices = originalMesh.vertices;
        vertices = animatedMesh.vertices;
        restNormals = originalMesh.normals;
        normals = animatedMesh.normals;
        platterSides = new int[vertices.Length];

        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 vertex = vertices[i] / unit;
            if (Mathf.Abs(Mathf.Abs(vertex.z) - 5.619958f) > 1.888f)
                continue;

            bool platterHeight = Mathf.Abs(vertex.y - 1.475653f) < 0.0001f
                || Mathf.Abs(vertex.y - 1.731197f) < 0.0001f
                || Mathf.Abs(vertex.y - 1.770845f) < 0.0001f
                || Mathf.Abs(vertex.y - 1.86774f) < 0.0001f;
            if (platterHeight)
                platterSides[i] = vertex.z < 0f ? -1 : 1;
        }

        BuildSpeakers(unit);
        deckUnit = unit;
        BuildSoundDisplay();
        challenge = new LivingRoomDJChallenge(deck.transform, unit, forward, platterCenter,
            musicSource, secondMusicSource, songChoices);
        if (roomModel != null)
        {
            var cast = new LivingRoomMixamo();
            GameObject[] partyModels = cast.Models ?? guestModels;
            couch = new LivingRoomCouch(roomModel, guestModels, guestSittingClip, cast);
            guests = new LivingRoomGuests(roomModel, deck.transform, partyModels, guestIdleController, crowdModel, cast);
            partyLighting = new LivingRoomPartyLighting(roomModel, deck.transform.position);
            entrance = new LivingRoomEntrance(roomModel, deck.transform,
                partyModels != null && partyModels.Length > 0 ? partyModels[0] : null, guestIdleController, cast, exteriorWindowPanes);
            foreach (Transform cone in cones)
            {
                Vector3 center = cone.localPosition;
                center.x += forward * cone.localScale.y * 1.1f;
                partyLighting.AddSpeakerRing(cone.parent, center, cone.localScale.x * 0.54f);
            }
            foreach (MeshFilter filter in roomModel.GetComponentsInChildren<MeshFilter>())
            {
                switch (filter.name)
                {
                    case "mesh1676509992":
                    case "mesh866233314":
                    case "mesh1384295385":
                    case "mesh709689000":
                    case "mesh864671905":
                    case "mesh2054087311":
                    case "mesh161297073":
                    case "mesh1956117766":
                        wallPictures.Add(new WallPicture(filter, wallPictures.Count));
                        break;
                    case "mesh1755818725":
                        SetupScreenDot(filter);
                        break;
                    case "mesh15764063":
                    case "mesh1785736494":
                    case "mesh908259242":
                    case "mesh783608269":
                        roomSpeakers.Add(new RoomSpeaker(filter, partyLighting));
                        break;
                }
            }

            if (roomSpeakers.Count != 4)
                Debug.LogWarning("Expected four speaker meshes in the Living Room model.", this);
        }
    }

    private void BuildSpeakers(float unit)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        cabinetMaterial = new Material(shader) { color = new Color(0.035f, 0.035f, 0.044f) };
        coneMaterial = new Material(shader) { color = new Color(0.28f, 0.31f, 0.39f) };
        coneRestX = 1.09f * unit;

        for (int i = 0; i < cones.Length; i++)
        {
            Transform speaker = new GameObject(i == 0 ? "Speaker Left" : "Speaker Right").transform;
            speaker.SetParent(deck.transform, false);
            speaker.localPosition = new Vector3(0f, 1.6f, i == 0 ? -9.7f : 9.7f) * unit;

            AddSpeakerPart(speaker, "Cabinet", PrimitiveType.Cube, Vector3.zero,
                new Vector3(2.05f, 3.2f, 2.55f) * unit, cabinetMaterial);

            cones[i] = AddSpeakerPart(speaker, "Woofer", PrimitiveType.Cylinder,
                new Vector3(forward * coneRestX, -0.35f * unit, 0f),
                new Vector3(1.95f, 0.09f, 1.95f) * unit, coneMaterial);
            cones[i].localRotation = Quaternion.Euler(0f, 0f, 90f);

            Transform tweeter = AddSpeakerPart(speaker, "Tweeter", PrimitiveType.Cylinder,
                new Vector3(forward * coneRestX, 0.98f * unit, 0f),
                new Vector3(0.59f, 0.08f, 0.59f) * unit, coneMaterial);
            tweeter.localRotation = cones[i].localRotation;
        }
    }

    private static Transform AddSpeakerPart(Transform parent, string partName, PrimitiveType shape,
        Vector3 position, Vector3 scale, Material material)
    {
        GameObject part = GameObject.CreatePrimitive(shape);
        part.name = partName;
        Destroy(part.GetComponent<Collider>());
        part.GetComponent<MeshRenderer>().sharedMaterial = material;
        part.transform.SetParent(parent, false);
        part.transform.localPosition = position;
        part.transform.localScale = scale;
        return part.transform;
    }

    private void Update()
    {
        float elapsed = Time.deltaTime;
        angle = Mathf.Repeat(angle + platterRpm * 6f * elapsed, 360f);
        challenge?.Update();
        Quaternion rotation = Quaternion.AngleAxis(angle, Vector3.up);
        Quaternion scratchRotation = Quaternion.AngleAxis(angle + (challenge?.PlatterOffset ?? 0f), Vector3.up);

        for (int i = 0; i < vertices.Length; i++)
        {
            int side = platterSides[i];
            if (side == 0)
                continue;

            Vector3 center = new Vector3(platterCenter.x, restVertices[i].y, platterCenter.z * side);
            Quaternion platterRotation = side < 0 ? scratchRotation : rotation;
            vertices[i] = center + platterRotation * (restVertices[i] - center);
            if (i < normals.Length)
                normals[i] = platterRotation * restNormals[i];
        }

        animatedMesh.vertices = vertices;
        if (normals.Length == vertices.Length)
            animatedMesh.normals = normals;

        float strength = ReadPulse(elapsed);
        pulse = Mathf.Lerp(pulse, strength, 1f - Mathf.Exp(-(strength > pulse ? 24f : 12f) * elapsed));
        couch?.Update(elapsed);
        guests?.Update(pulse, elapsed);
        AnimateSoundDisplay(elapsed);
        AnimateScreenDot(elapsed);
        AnimateWallPictures(elapsed);
        partyLighting?.Update(pulse, elapsed);
        entrance?.Update(elapsed, partyLighting);

        foreach (RoomSpeaker speaker in roomSpeakers)
            speaker.Apply(speakerTravel * pulse);

        foreach (Transform cone in cones)
        {
            Vector3 position = cone.localPosition;
            position.x = forward * coneRestX * (1f + speakerTravel * pulse);
            cone.localPosition = position;
        }
    }

    private void SetupScreenDot(MeshFilter filter)
    {
        Mesh source = filter.sharedMesh;
        MeshRenderer renderer = filter.GetComponent<MeshRenderer>();
        Material[] materials = renderer.sharedMaterials;
        Vector3[] points = source.vertices;
        for (int slot = 0; slot < source.subMeshCount; slot++)
        {
            if (materials[slot].name != "mat5")
                continue;

            // mat5 also colours a detail below the screen. Isolate just the dot.
            float screenCutoff = source.bounds.center.y;
            int[] triangles = source.GetTriangles(slot);
            var dotTriangles = new List<int>();
            var otherTriangles = new List<int>();
            var indices = new HashSet<int>();
            for (int i = 0; i < triangles.Length; i += 3)
            {
                int a = triangles[i];
                int b = triangles[i + 1];
                int c = triangles[i + 2];
                bool isDot = points[a].y > screenCutoff
                    && points[b].y > screenCutoff && points[c].y > screenCutoff;
                List<int> destination = isDot ? dotTriangles : otherTriangles;
                destination.Add(a);
                destination.Add(b);
                destination.Add(c);
                if (isDot)
                {
                    indices.Add(a);
                    indices.Add(b);
                    indices.Add(c);
                }
            }

            if (indices.Count == 0)
                continue;

            Bounds bounds = new Bounds(points[dotTriangles[0]], Vector3.zero);
            foreach (int index in indices)
                bounds.Encapsulate(points[index]);

            screenFilter = filter;
            screenRenderer = renderer;
            originalScreen = source;
            screenMaterials = materials;
            screenRest = points;
            dotVertices = new int[indices.Count];
            indices.CopyTo(dotVertices);
            dotCenter = bounds.center;
            dotRadius = Mathf.Min(bounds.extents.y, bounds.extents.z);
            animatedScreen = Instantiate(source);
            animatedScreen.MarkDynamic();
            animatedScreen.subMeshCount = source.subMeshCount + 1;
            animatedScreen.SetTriangles(otherTriangles, slot, false);
            animatedScreen.SetTriangles(dotTriangles, source.subMeshCount, false);
            screenVertices = animatedScreen.vertices;

            dotMaterial = new Material(materials[slot]);
            var animatedMaterials = new Material[materials.Length + 1];
            materials.CopyTo(animatedMaterials, 0);
            animatedMaterials[materials.Length] = dotMaterial;
            filter.sharedMesh = animatedScreen;
            renderer.sharedMaterials = animatedMaterials;
            return;
        }

        Debug.LogWarning("Could not identify the blue TV dot.", filter);
    }

    private void BuildSoundDisplay()
    {
        soundDisplay = new GameObject("Deck Sound Meter");
        soundDisplay.transform.SetParent(deck.transform, false);
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        meterBackground = new Material(shader) { color = new Color(0.018f, 0.012f, 0.038f) };
        meterInk = new Material(shader) { color = new Color(0.55f, 0.22f, 0.93f) };

        // These coordinates sit inside the sloped screen recess in the authored DJGear mesh.
        GameObject panel = GameObject.CreatePrimitive(PrimitiveType.Quad);
        panel.name = "Meter Screen";
        panel.transform.SetParent(soundDisplay.transform, false);
        panel.transform.localPosition = MeterPoint(-0.743f, 0f, 0.024f);
        panel.transform.localRotation = Quaternion.LookRotation(
            new Vector3(-0.189f * forward, -1f, 0f), Vector3.right * forward);
        panel.transform.localScale = new Vector3(1.90f, 0.68f, 1f) * deckUnit;
        Collider collider = panel.GetComponent<Collider>();
        collider.enabled = false;
        Destroy(collider);
        panel.GetComponent<MeshRenderer>().sharedMaterial = meterBackground;

        var bars = new GameObject("Spectrum Bars");
        bars.transform.SetParent(soundDisplay.transform, false);
        int[] triangles = new int[MeterBars * 6];
        for (int i = 0; i < MeterBars; i++)
        {
            int v = i * 4;
            int t = i * 6;
            triangles[t] = v;
            triangles[t + 1] = v + (forward > 0f ? 2 : 1);
            triangles[t + 2] = v + (forward > 0f ? 1 : 2);
            triangles[t + 3] = v + 2;
            triangles[t + 4] = v + (forward > 0f ? 3 : 1);
            triangles[t + 5] = v + (forward > 0f ? 1 : 3);
        }

        meterMesh = new Mesh { name = "DJ Spectrum Bars" };
        meterMesh.MarkDynamic();
        meterMesh.vertices = meterVertices;
        meterMesh.triangles = triangles;
        bars.AddComponent<MeshFilter>().sharedMesh = meterMesh;
        MeshRenderer renderer = bars.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = meterInk;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        AnimateSoundDisplay(0f);
    }

    private Vector3 MeterPoint(float x, float z, float clearance)
    {
        float y = 1.491412f - (x + 0.409442f) * 0.1891f + clearance;
        return new Vector3(x * forward, y, z) * deckUnit;
    }

    private void AnimateSoundDisplay(float elapsed)
    {
        if (meterMesh == null)
            return;

        for (int i = 0; i < MeterBars; i++)
        {
            float fraction = (float)i / MeterBars;
            int first = Mathf.RoundToInt(Mathf.Pow(2f, fraction * 6f));
            int end = Mathf.Max(first + 1, Mathf.RoundToInt(Mathf.Pow(2f, (i + 1f) / MeterBars * 6f)));
            float energy = 0f;
            for (int bin = first; bin < end; bin++)
                energy = Mathf.Max(energy, spectrum[bin]);

            float target = audioHold > 0f
                ? Mathf.Sqrt(Mathf.Clamp01(energy / Mathf.Max(peak, 0.002f)))
                : pulse * (0.28f + 0.62f * Mathf.Abs(Mathf.Sin(i * 1.8f + Time.time * 3f)));
            float response = target > meterLevels[i] ? 20f : 9f;
            meterLevels[i] = Mathf.Lerp(meterLevels[i], target, 1f - Mathf.Exp(-response * elapsed));

            float height = 0.035f + meterLevels[i] * 0.28f;
            float z = Mathf.Lerp(-0.85f, 0.85f, fraction) + 0.85f / MeterBars;
            int v = i * 4;
            meterVertices[v] = MeterPoint(-0.743f - height, z - 0.039f, 0.030f);
            meterVertices[v + 1] = MeterPoint(-0.743f + height, z - 0.039f, 0.030f);
            meterVertices[v + 2] = MeterPoint(-0.743f - height, z + 0.039f, 0.030f);
            meterVertices[v + 3] = MeterPoint(-0.743f + height, z + 0.039f, 0.030f);
        }

        meterMesh.vertices = meterVertices;
        meterMesh.RecalculateBounds();
    }

    private void AnimateScreenDot(float elapsed)
    {
        if (animatedScreen == null || dotVertices == null || screenRest == null
            || screenVertices == null || dotMaterial == null)
            return;

        dotPhase = Mathf.Repeat(dotPhase + elapsed * (0.10f + 0.8f * pulse), 1f);
        float angle = dotPhase * 2f * Mathf.PI;
        float scale = 0.83f + 0.23f * pulse;
        float orbit = dotRadius * (0.13f + 0.10f * pulse);
        Vector3 offset = new Vector3(0f, Mathf.Sin(angle), Mathf.Cos(angle)) * orbit;
        foreach (int index in dotVertices)
        {
            Vector3 local = screenRest[index] - dotCenter;
            screenVertices[index] = dotCenter + new Vector3(local.x, local.y * scale, local.z * scale) + offset;
        }

        animatedScreen.vertices = screenVertices;
        dotMaterial.color = Color.HSVToRGB(Mathf.Repeat(0.60f + dotPhase + pulse * 0.12f, 1f),
            0.85f, 0.78f + 0.22f * pulse);
    }

    private sealed class RoomSpeaker
    {
        private readonly MeshFilter filter;
        private readonly Mesh original;
        private readonly Mesh mesh;
        private readonly Vector3[] rest;
        private readonly Vector3[] vertices;
        private readonly List<int> moving = new List<int>();
        private readonly float travel;

        public RoomSpeaker(MeshFilter filter, LivingRoomPartyLighting lighting)
        {
            this.filter = filter;
            original = filter.sharedMesh;
            rest = original.vertices;

            // Unity splits vertices at UV and normal seams. Join equal positions before
            // identifying the disconnected cone and dust-cap geometry in this model.
            var positions = new Dictionary<Vector3, int>();
            int[] parents = new int[rest.Length];
            for (int i = 0; i < rest.Length; i++)
            {
                if (!positions.TryGetValue(rest[i], out int first))
                {
                    first = i;
                    positions.Add(rest[i], i);
                }
                parents[i] = first;
            }

            int[] triangles = original.triangles;
            for (int i = 0; i < triangles.Length; i += 3)
            {
                int root = FindRoot(parents, triangles[i]);
                parents[FindRoot(parents, triangles[i + 1])] = root;
                parents[FindRoot(parents, triangles[i + 2])] = root;
            }

            var groups = new Dictionary<int, HashSet<Vector3>>();
            foreach (int index in positions.Values)
            {
                int root = FindRoot(parents, index);
                if (!groups.TryGetValue(root, out HashSet<Vector3> points))
                {
                    points = new HashSet<Vector3>();
                    groups.Add(root, points);
                }
                points.Add(rest[index]);
            }

            var driverRoots = new HashSet<int>();
            foreach (var group in groups)
            {
                if (group.Value.Count == 144)
                    driverRoots.Add(group.Key);
            }

            if (driverRoots.Count != 2)
            {
                Debug.LogWarning("Could not identify both cones in " + filter.name, filter);
                return;
            }

            float direction = 0f;
            var movingRoots = new HashSet<int>(driverRoots);
            foreach (int root in driverRoots)
            {
                Vector3 center = CenterOf(groups[root]);
                direction += center.x - original.bounds.center.x;
                foreach (var group in groups)
                {
                    if (group.Value.Count != 12)
                        continue;

                    Vector3 offset = CenterOf(group.Value) - center;
                    // Dust caps are small disconnected pieces near the cone center.
                    if (new Vector2(offset.y, offset.z).magnitude < original.bounds.size.z * 0.12f)
                        movingRoots.Add(group.Key);
                }
            }

            for (int i = 0; i < rest.Length; i++)
            {
                if (movingRoots.Contains(FindRoot(parents, i)))
                    moving.Add(i);
            }

            travel = Mathf.Sign(direction) * original.bounds.size.z * 0.25f;
            foreach (int root in driverRoots)
            {
                HashSet<Vector3> points = groups[root];
                Vector3 center = CenterOf(points);
                float radius = 0f;
                foreach (Vector3 point in points)
                {
                    Vector3 offset = point - center;
                    radius = Mathf.Max(radius, new Vector2(offset.y, offset.z).magnitude);
                    if ((point.x - center.x) * travel > 0f)
                        center.x = point.x;
                }
                center.x += travel * 0.06f;
                lighting.AddSpeakerRing(filter.transform, center, radius * 1.07f);
            }
            mesh = Instantiate(original);
            mesh.MarkDynamic();
            vertices = mesh.vertices;
            filter.sharedMesh = mesh;
        }

        public void Apply(float amount)
        {
            if (mesh == null)
                return;

            Vector3 displacement = Vector3.right * (travel * amount);
            foreach (int index in moving)
                vertices[index] = rest[index] + displacement;

            mesh.vertices = vertices;
        }

        public void Dispose()
        {
            if (mesh == null)
                return;
            if (filter != null)
                filter.sharedMesh = original;
            Destroy(mesh);
        }

        private static int FindRoot(int[] parents, int index)
        {
            while (parents[index] != index)
            {
                parents[index] = parents[parents[index]];
                index = parents[index];
            }
            return index;
        }

        private static Vector3 CenterOf(HashSet<Vector3> points)
        {
            Vector3 center = Vector3.zero;
            foreach (Vector3 point in points)
                center += point;
            return center / points.Count;
        }
    }

    private void AnimateWallPictures(float elapsed)
    {
        float hit;
        if (audioHold > 0f)
        {
            float binWidth = AudioSettings.outputSampleRate / (2f * spectrum.Length);
            int bassEnd = Mathf.Clamp(Mathf.FloorToInt(160f / binWidth), 1, spectrum.Length - 1);
            float bass = 0f;
            for (int i = 1; i <= bassEnd; i++)
                bass = Mathf.Max(bass, spectrum[i]);

            bassAverage = Mathf.Lerp(bassAverage, bass, 1f - Mathf.Exp(-elapsed * 3f));
            bassPeak = Mathf.Max(bass, bassPeak * Mathf.Exp(-elapsed * 0.8f));
            hit = Mathf.Clamp01((bass - bassAverage * 1.35f) / Mathf.Max(0.002f, bassPeak) * 1.7f);
        }
        else
        {
            hit = Mathf.Exp(-12f * demoPhase) * 0.55f;
        }

        pictureShake = Mathf.Max(hit, pictureShake * Mathf.Exp(-elapsed * 7f));
        pictureTime += elapsed;
        foreach (WallPicture picture in wallPictures)
            picture.Apply(pictureTime, pictureShake * pictureWobbleDegrees);
    }

    private sealed class WallPicture
    {
        private readonly Transform frame;
        private readonly Vector3 originalPosition;
        private readonly Quaternion originalRotation;
        private readonly Vector3 hookFromOrigin;
        private readonly float variation;

        public WallPicture(MeshFilter filter, int index)
        {
            frame = filter.transform;
            originalPosition = frame.localPosition;
            originalRotation = frame.localRotation;
            Bounds bounds = filter.sharedMesh.bounds;
            Vector3 hook = new Vector3(bounds.center.x, bounds.max.y, bounds.center.z);
            hookFromOrigin = Vector3.Scale(frame.localScale, hook);
            variation = index * 0.69f;
        }

        public void Apply(float time, float strength)
        {
            float angle = Mathf.Sin(time * (21f + variation) + variation) * strength;
            frame.localRotation = originalRotation * Quaternion.AngleAxis(angle, Vector3.right);
            frame.localPosition = originalPosition + originalRotation * hookFromOrigin
                - frame.localRotation * hookFromOrigin;
        }

        public void Restore()
        {
            if (frame == null)
                return;
            frame.localPosition = originalPosition;
            frame.localRotation = originalRotation;
        }
    }

    private float ReadPulse(float elapsed)
    {
        AudioSource first = challenge?.FirstTrack ?? musicSource;
        AudioSource second = challenge?.SecondTrack ?? secondMusicSource;
        if (first != null)
        {
            first.GetSpectrumData(spectrum, 0, FFTWindow.BlackmanHarris);
            if (second != null && second != first && second.isPlaying)
            {
                second.GetSpectrumData(rightSpectrum, 0, FFTWindow.BlackmanHarris);
                for (int i = 0; i < spectrum.Length; i++)
                    spectrum[i] = Mathf.Max(spectrum[i] * first.volume, rightSpectrum[i] * second.volume);
            }
        }
        else
        {
            AudioListener.GetSpectrumData(spectrum, 0, FFTWindow.BlackmanHarris);
            AudioListener.GetSpectrumData(rightSpectrum, 1, FFTWindow.BlackmanHarris);
            for (int i = 0; i < spectrum.Length; i++)
                spectrum[i] = Mathf.Max(spectrum[i], rightSpectrum[i]);
        }

        float binWidth = AudioSettings.outputSampleRate / (2f * spectrum.Length);
        int bandEnd = Mathf.Clamp(Mathf.CeilToInt(1000f / binWidth), 2, spectrum.Length);
        float energy = 0f;
        for (int i = 1; i < bandEnd; i++)
            energy = Mathf.Max(energy, spectrum[i] / (1f + i * 0.15f));

        float audible = energy;
        for (int i = bandEnd; i < spectrum.Length; i++)
            audible = Mathf.Max(audible, spectrum[i]);

        audioHold = audible > 0.00005f ? 2f : Mathf.Max(0f, audioHold - elapsed);
        if (audioHold > 0f)
        {
            // Adapt to track volume and use higher frequencies when there's little bass.
            energy = Mathf.Max(energy, audible * 0.3f);
            peak = Mathf.Max(Mathf.Max(0.0001f, energy), peak * Mathf.Exp(-elapsed));
            averageEnergy = Mathf.Lerp(averageEnergy, energy, 1f - Mathf.Exp(-4f * elapsed));
            float transient = Mathf.Max(0f, energy - averageEnergy);
            return Mathf.Clamp01((energy * 0.35f + transient * 1.5f) / peak);
        }

        demoPhase = Mathf.Repeat(demoPhase + elapsed * demoBpm / 60f, 1f);
        return Mathf.Exp(-7f * demoPhase);
    }

    private void LateUpdate()
    {
        guests?.LateUpdate();
    }

    private void OnDestroy()
    {
        foreach (RoomSpeaker speaker in roomSpeakers)
            speaker.Dispose();
        entrance?.Dispose();
        couch?.Dispose();
        guests?.Dispose();
        challenge?.Dispose();
        Destroy(soundDisplay);
        Destroy(meterMesh);
        Destroy(meterInk);
        Destroy(meterBackground);
        partyLighting?.Dispose();
        foreach (WallPicture picture in wallPictures)
            picture.Restore();

        foreach (Transform cone in cones)
        {
            if (cone != null)
                Destroy(cone.parent.gameObject);
        }

        if (animatedMesh != null)
        {
            if (deck != null)
                deck.sharedMesh = originalMesh;
            Destroy(animatedMesh);
        }

        if (animatedScreen != null)
        {
            if (screenFilter != null)
                screenFilter.sharedMesh = originalScreen;
            if (screenRenderer != null)
                screenRenderer.sharedMaterials = screenMaterials;
            Destroy(animatedScreen);
            Destroy(dotMaterial);
        }

        Destroy(cabinetMaterial);
        Destroy(coneMaterial);
    }
}

internal sealed class LivingRoomDJChallenge
{
    private enum Step { PickSongs, Fader, Scratch, Crossfade, Complete }

    private static readonly Color Ready = new Color(1f, 0.22f, 0.66f);
    private static readonly Color Idle = new Color(0.17f, 0.21f, 0.35f);
    private readonly GameObject rig;
    private readonly Transform deck;
    private readonly float unit;
    private readonly float front;
    private readonly Camera camera;
    private readonly ContinuousMoveProvider boothMovement;
    private readonly XRInputValueReader<Vector2> originalRightMove;
    private readonly float originalMoveSpeed;
    private readonly bool originalMoveEnabled;
    private readonly AudioSource music;
    private readonly AudioSource secondTrack;
    private readonly float originalVolume;
    private readonly float originalSecondVolume;
    private readonly float originalPitch;
    private readonly AudioClip[] songs;
    private readonly AudioClip originalClip;
    private readonly AudioClip originalSecondClip;
    private readonly bool originalLoop;
    private readonly bool originalSecondLoop;
    private readonly Renderer[] songButtons = new Renderer[2];
    private readonly Text[] controlLabels = new Text[5];
    private readonly BoxCollider[] menuButtonTargets = new BoxCollider[5];
    private readonly int[] songSelection = { 0, 1 };
    private float turnPitch;
    private bool adjustingPitch;
    private readonly Text prompt;
    private readonly Text instructionTitle;
    private readonly Text instructionAction;
    private readonly Text navigationHint;
    private readonly RectTransform djPopup;
    private readonly RectTransform progressFill;
    private readonly Transform fader;
    private readonly Renderer faderRenderer;
    private readonly Renderer cueRenderer;
    private readonly Renderer scratchRenderer;
    private readonly Material activeMaterial;
    private readonly Material idleMaterial;
    private readonly BoxCollider[] targets = new BoxCollider[5];
    private const float ControllerReach = 0.115f;
    private const float ScratchGoalDegrees = 45f;
    private readonly Transform[] controllers = new Transform[2];
    private readonly bool[] triggerPressed = new bool[2];
    private readonly float[] triggerGrace = new float[2];
    private readonly Renderer[] controlRenderers = new Renderer[5];
    private readonly MaterialPropertyBlock controlGlow = new();
    private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
    private int triggerOwner = -1;
    private int nearControl = -1;
    private string actionHint;
    private readonly Vector3 faderStart;
    private readonly Transform faderGuide;
    private readonly Vector3 platterCenter;
    private Step step;
    private int held = -1;
    private IXRSelectInteractor interactor;
    private XRSimpleInteractable interactable;
    private Vector3 lastPoint;
    private float faderTravel;
    private float scratchTravel;
    private float platterOffset;

    public float PlatterOffset => platterOffset;
    public AudioSource FirstTrack => music;
    public AudioSource SecondTrack => secondTrack;

    public LivingRoomDJChallenge(Transform deck, float unit, float front, Vector3 platterCenter,
        AudioSource music, AudioSource secondTrack, AudioClip[] songChoices)
    {
        this.deck = deck;
        this.unit = unit;
        this.front = front;
        camera = Camera.main;
        var origin = camera != null ? camera.GetComponentInParent<Unity.XR.CoreUtils.XROrigin>() : null;
        if (origin != null)
        {
            var avatar = origin.GetComponentInParent<HandshakePlayerAvatar>();
            if (avatar != null)
            {
                controllers[0] = avatar.LeftController;
                controllers[1] = avatar.RightController;
            }
            boothMovement = origin.GetComponentInChildren<ContinuousMoveProvider>();
            if (boothMovement != null)
            {
                originalMoveEnabled = boothMovement.enabled;
                originalMoveSpeed = boothMovement.moveSpeed;
                originalRightMove = boothMovement.rightHandMoveInput;
                boothMovement.enabled = false;
                boothMovement.moveSpeed = 0.65f;
                boothMovement.rightHandMoveInput = new XRInputValueReader<Vector2>(
                    "Booth right stick", XRInputValueReader.InputSourceMode.Unused);
            }
        }
        rig = new GameObject("DJ Booth Challenge");
        rig.transform.SetParent(deck, false);
        this.music = music != null ? music : CreateDeckAudio("Track A");
        this.secondTrack = secondTrack != null && secondTrack != music ? secondTrack : CreateDeckAudio("Track B");
        originalVolume = this.music.volume;
        originalPitch = this.music.pitch;
        originalSecondVolume = this.secondTrack.volume;
        originalClip = this.music.clip;
        originalSecondClip = this.secondTrack.clip;
        originalLoop = this.music.loop;
        originalSecondLoop = this.secondTrack.loop;
        var catalog = new List<AudioClip>();
        foreach (AudioClip clip in songChoices ?? System.Array.Empty<AudioClip>())
        {
            if (clip != null && !catalog.Contains(clip))
                catalog.Add(clip);
        }
        foreach (AudioClip clip in new[] { originalClip, originalSecondClip })
        {
            if (clip != null && !catalog.Contains(clip))
                catalog.Add(clip);
        }
        if (catalog.Count < 2)
            catalog.AddRange(Resources.LoadAll<AudioClip>("LivingRoomDJTracks"));
        songs = catalog.ToArray();
        if (originalClip == null && songs.Length > 0)
        {
            this.music.clip = songs[0];
            this.music.loop = true;
            this.music.volume = originalVolume * 0.45f;
            this.music.Play();
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        activeMaterial = new Material(shader) { color = Ready };
        activeMaterial.EnableKeyword("_EMISSION");
        activeMaterial.SetColor("_EmissionColor", Ready * 0.38f);
        idleMaterial = new Material(shader) { color = Idle };

        faderStart = new Vector3(1.25f * front, 1.62f, -1.44f) * unit;
        fader = CreatePart("Fader Cap", faderStart, new Vector3(0.36f, 0.20f, 0.38f) * unit);
        faderRenderer = fader.GetComponent<Renderer>();
        controlRenderers[0] = faderRenderer;
        AddTarget(0, "Fader Drag Area", faderStart - Vector3.right * (0.48f * unit * front),
            new Vector3(1.85f, 0.44f, 0.62f) * unit);

        Vector3 pad = new Vector3(2.75f * front, 1.83f, 0f) * unit;
        cueRenderer = CreatePart("Cue Pad", pad, new Vector3(0.54f, 0.16f, 0.60f) * unit)
            .GetComponent<Renderer>();
        controlRenderers[1] = cueRenderer;
        AddTarget(1, "Cue Pad Button", pad, new Vector3(0.78f, 0.40f, 0.82f) * unit);
        controlLabels[0] = AddControlLabel("Fader Callout", faderStart / unit + Vector3.up * 0.75f);
        controlLabels[1] = AddControlLabel("Cue Callout", pad / unit + Vector3.up * 0.70f);
        AddMenuButtonTarget(1);
        faderGuide = CreatePart("Fader Destination", faderStart - Vector3.right * (front * 0.80f * unit)
            - Vector3.up * (0.095f * unit), new Vector3(0.24f, 0.025f, 0.46f) * unit);
        faderGuide.GetComponent<Renderer>().sharedMaterial = activeMaterial;
        for (int i = 0; i < songButtons.Length; i++)
        {
            string label = i == 0 ? "A" : "B";
            Vector3 position = new Vector3(2.75f * front, 1.83f, i == 0 ? -2.45f : 2.45f) * unit;
            Transform button = CreatePart("Track " + label, position, new Vector3(0.68f, 0.22f, 0.84f) * unit);
            songButtons[i] = button.GetComponent<Renderer>();
            controlRenderers[3 + i] = songButtons[i];
            AddTarget(3 + i, "Choose Track " + label, position, new Vector3(0.85f, 0.45f, 1.05f) * unit);
            controlLabels[3 + i] = AddControlLabel("Deck " + label + " Callout",
                position / unit + Vector3.up * 0.52f);
            controlLabels[3 + i].text = "CHANGE " + label;
            AddMenuButtonTarget(3 + i);
        }

        this.platterCenter = new Vector3(platterCenter.x, 1.88f * unit, -platterCenter.z);
        AddTarget(2, "Turntable Grab Area", this.platterCenter, new Vector3(3.70f, 0.40f, 3.70f) * unit);
        scratchRenderer = CreatePart("Turntable Cue", this.platterCenter + new Vector3(0f, 0f, -1.78f * unit),
            new Vector3(0.34f, 0.11f, 0.16f) * unit).GetComponent<Renderer>();
        controlRenderers[2] = scratchRenderer;
        controlLabels[2] = AddControlLabel("Record Callout", scratchRenderer.transform.localPosition / unit
            + Vector3.up * 0.65f);

        var panel = new LivingRoomBillboard(rig.transform, "DJ Instructions",
            new Vector2(660, 320), unit * 0.01f, Ready);
        djPopup = panel.Transform;
        djPopup.localPosition = new Vector3(-1.60f * front, 7.70f, 0f) * unit;
        panel.Transform.localRotation = Quaternion.LookRotation(new Vector3(-front, 0.10f, 0f));
        instructionTitle = panel.AddText("Heading", new Rect(24, 20, 612, 47), 36, Ready, FontStyle.Bold);
        prompt = panel.AddText("Goal", new Rect(32, 73, 596, 105), 32, Color.white);
        var track = panel.AddPanel("Progress Track", new Rect(42, 187, 576, 10), Idle);
        progressFill = LivingRoomBillboard.AddGraphic(track, "Progress Fill", Ready).rectTransform;
        progressFill.anchorMin = Vector2.zero;
        progressFill.anchorMax = Vector2.up;
        progressFill.offsetMin = progressFill.offsetMax = Vector2.zero;
        panel.AddPanel("Action Bar", new Rect(26, 211, 608, 55), Ready);
        instructionAction = panel.AddText("Next Action", new Rect(39, 216, 582, 45), 30,
            new Color(0.15f, 0.025f, 0.085f), FontStyle.Bold);
        navigationHint = panel.AddText("Movement Hint", new Rect(25, 275, 610, 30), 23,
            new Color(0.79f, 0.78f, 0.86f));
        ShowStep();
    }

    private Text AddControlLabel(string label, Vector3 position)
    {
        var tag = new LivingRoomBillboard(rig.transform, label, new Vector2(240, 62), unit * 0.0068f, Ready);
        tag.Transform.localPosition = position * unit;
        tag.Transform.localRotation = Quaternion.LookRotation(new Vector3(-front, -0.55f, 0f));
        return tag.AddText("Control", new Rect(10, 6, 220, 50), 32, Color.white, FontStyle.Bold);
    }

    private void AddMenuButtonTarget(int index)
    {
        var face = (RectTransform)controlLabels[index].rectTransform.parent.parent;
        face.gameObject.SetActive(false);
        var collider = face.gameObject.AddComponent<BoxCollider>();
        collider.size = new Vector3(face.rect.width, face.rect.height, 8f);
        menuButtonTargets[index] = collider;

        // The starter rig's poke interactor queries solid colliders on the Default layer.
        var button = face.gameObject.AddComponent<XRSimpleInteractable>();
        button.colliders.Add(collider);
        button.colliders.Add(targets[index]);
        button.hoverFilters.Add(new XRHoverFilterDelegate((hand, _) => hand is XRPokeInteractor));
        button.selectFilters.Add(new XRSelectFilterDelegate((hand, _) => hand is XRPokeInteractor));
        face.gameObject.AddComponent<XRPokeFilter>();
        var background = face.Find("Background").GetComponent<Image>();
        Color restingColor = background.color;
        button.lastHoverExited.AddListener(_ => background.color = restingColor);
        button.firstHoverEntered.AddListener(args =>
        {
            if (held >= 0 || !Expects(index))
                return;
            background.color = new Color(0.48f, 0.045f, 0.25f);
            var poke = (XRPokeInteractor)args.interactorObject;
            var node = controllers[0] != null && poke.transform.IsChildOf(controllers[0])
                ? XRNode.LeftHand : XRNode.RightHand;
            InputDevices.GetDeviceAtXRNode(node).SendHapticImpulse(0u, 0.30f, 0.055f);
            Begin(index, Vector3.zero);
        });
        face.gameObject.SetActive(true);
    }

    private AudioSource CreateDeckAudio(string name)
    {
        var channel = new GameObject(name);
        channel.transform.SetParent(rig.transform, false);
        var source = channel.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 1f;
        source.rolloffMode = AudioRolloffMode.Linear;
        source.minDistance = 2.5f;
        source.maxDistance = 15f;
        return source;
    }

    private Transform CreatePart(string name, Vector3 center, Vector3 scale)
    {
        GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
        part.name = name;
        Collider collider = part.GetComponent<Collider>();
        collider.enabled = false;
        UnityEngine.Object.Destroy(collider);
        part.transform.SetParent(rig.transform, false);
        part.transform.localPosition = center;
        part.transform.localScale = scale;
        return part.transform;
    }

    private void AddTarget(int index, string name, Vector3 position, Vector3 size)
    {
        var target = new GameObject(name);
        target.transform.SetParent(rig.transform, false);
        target.transform.localPosition = position;
        var collider = target.AddComponent<BoxCollider>();
        collider.size = size + Vector3.one * (0.32f * unit);
        targets[index] = collider;
        if (index == 1 || index >= 3)
            return;
        var xr = target.AddComponent<XRSimpleInteractable>();
        xr.selectEntered.AddListener(args =>
        {
            if (held >= 0 || !Expects(index) || !LivingRoomEntrance.IsHandGrab(args.interactorObject, collider))
                return;
            interactor = args.interactorObject;
            interactable = xr;
            Begin(index, XRPoint());
        });
        xr.selectExited.AddListener(args =>
        {
            if (interactor == args.interactorObject && interactable == xr)
                Release();
        });
    }

    private bool Expects(int index)
    {
        if (songs.Length < 2)
            return false;

        return step switch
        {
            Step.PickSongs => index == 1 || index >= 3,
            Step.Fader or Step.Crossfade => index == 0,
            Step.Complete => index <= 2,
            Step.Scratch => index == 2,
            _ => index == 1
        };
    }

    private void Begin(int index, Vector3 point)
    {
        if (step == Step.PickSongs && index >= 3)
        {
            int slot = index - 3;
            int next = (songSelection[slot] + 1) % songs.Length;
            if (next == songSelection[1 - slot])
            {
                if (songs.Length == 2)
                    songSelection[1 - slot] = songSelection[slot];
                else
                    next = (next + 1) % songs.Length;
            }
            songSelection[slot] = next;
            Release();
            ShowStep();
            return;
        }
        if (index == 1)
        {
            if (step == Step.Complete)
            {
                Advance(Step.PickSongs);
                return;
            }
            step = Step.Fader;
            faderTravel = 0f;
            music.Stop();
            secondTrack.Stop();
            music.clip = songs[songSelection[0]];
            secondTrack.clip = songs[songSelection[1]];
            music.loop = secondTrack.loop = true;
            secondTrack.volume = 0f;
            SetMusicLevel();
            music.Play();
            scratchTravel = 0f;
            platterOffset = 0f;
            fader.localPosition = faderStart;
            Release();
            ShowStep();
            return;
        }

        held = index;
        lastPoint = point;
    }

    public void Update()
    {
        if (boothMovement != null && camera != null)
        {
            Vector3 offset = camera.transform.position - deck.position;
            offset.y = 0f;
            boothMovement.enabled = held < 0 && offset.sqrMagnitude < 2.25f;
            navigationHint.text = held >= 0 ? "Release to move" : boothMovement.enabled
                ? "Left stick: move around the booth" : "Teleport closer to the booth";
        }
        else
        {
            navigationHint.text = "Teleport to the front edge of the booth";
        }
        if (music != null && adjustingPitch)
        {
            float target = originalPitch * (1f + turnPitch);
            music.pitch = Mathf.Lerp(music.pitch, target, 1f - Mathf.Exp(-Time.deltaTime * 18f));
            if (held != 2 && turnPitch == 0f && Mathf.Abs(music.pitch - originalPitch) < 0.001f)
            {
                music.pitch = originalPitch;
                adjustingPitch = false;
            }
        }
        turnPitch = 0f;

        UpdateControllerTriggers();
        if (interactor != null && held >= 0)
            Drag(XRPoint());
        UpdateInteractionFeedback();
    }

    private void UpdateControllerTriggers()
    {
        float bestDistance = ControllerReach * ControllerReach;
        nearControl = -1;
        targets[0].transform.localPosition = fader.localPosition;
        for (int hand = 0; hand < controllers.Length; hand++)
        {
            XRNode node = hand == 0 ? XRNode.LeftHand : XRNode.RightHand;
            InputDevice device = InputDevices.GetDeviceAtXRNode(node);
            bool tracked = device.isValid && controllers[hand] != null
                && (!device.TryGetFeatureValue(CommonUsages.isTracked, out bool isTracked) || isTracked);
            float trigger = 0f;
            if (tracked)
                device.TryGetFeatureValue(CommonUsages.trigger, out trigger);
            bool pressed = trigger >= (triggerPressed[hand] ? 0.36f : 0.60f);
            bool started = pressed && !triggerPressed[hand];
            triggerPressed[hand] = pressed;
            triggerGrace[hand] = started ? 0.15f : pressed
                ? Mathf.Max(0f, triggerGrace[hand] - Time.unscaledDeltaTime) : 0f;

            if (triggerOwner == hand)
            {
                if (pressed)
                    Drag(deck.InverseTransformPoint(controllers[hand].position));
                else
                    Release();
                continue;
            }

            if (!tracked || held >= 0)
                continue;

            Vector3 point = controllers[hand].position;
            int target = ClosestControl(point, out float distance);
            if (target < 0)
                continue;
            if (distance < bestDistance)
            {
                nearControl = target;
                bestDistance = distance;
            }

            if (triggerGrace[hand] > 0f)
            {
                triggerGrace[hand] = 0f;
                Begin(target, deck.InverseTransformPoint(point));
                if (held >= 0)
                    triggerOwner = hand;
                device.SendHapticImpulse(0u, 0.30f, 0.055f);
            }
        }
    }

    private int ClosestControl(Vector3 point, out float distance)
    {
        distance = ControllerReach * ControllerReach;
        int nearest = -1;
        for (int i = 0; i < targets.Length; i++)
        {
            if (!targets[i].enabled || menuButtonTargets[i] != null)
                continue;
            float gap = DistanceToControl(targets[i], point);
            if (gap >= distance)
                continue;
            distance = gap;
            nearest = i;
        }
        return nearest;
    }

    private static float DistanceToControl(BoxCollider target, Vector3 point)
    {
        return (target.ClosestPoint(point) - point).sqrMagnitude;
    }

    private void UpdateInteractionFeedback()
    {
        if (camera != null)
        {
            Vector3 head = camera.transform.position;
            Vector3 facing = djPopup.position - head;
            if (facing.sqrMagnitude > 0.01f)
                djPopup.rotation = Quaternion.LookRotation(facing, Vector3.up);
            djPopup.gameObject.SetActive((djPopup.position - head).sqrMagnitude > 0.38f * 0.38f);
        }

        float progress = step switch
        {
            Step.Fader or Step.Crossfade or Step.Complete => Mathf.Clamp01(faderTravel / (unit * 0.80f)),
            Step.Scratch => Mathf.Clamp01(scratchTravel / ScratchGoalDegrees),
            _ => 0f
        };
        progressFill.anchorMax = new Vector2(progress, 1f);

        int highlighted = held >= 0 ? held : nearControl;
        for (int i = 0; i < controlRenderers.Length; i++)
        {
            controlGlow.SetColor(EmissionColor, Ready * (i == highlighted ? 0.92f : 0.38f));
            controlRenderers[i].SetPropertyBlock(Expects(i) ? controlGlow : null);
        }

        if (held == 0)
        {
            int percent = Mathf.RoundToInt(progress * 100f);
            SetActionText(step is Step.Crossfade or Step.Complete ? $"A {100 - percent}%    B {percent}%"
                : $"LEVEL {percent}%");
        }
        else if (held == 2)
        {
            SetActionText(step == Step.Complete ? "SCRATCHING" : $"SCRATCH {Mathf.Min(100, Mathf.RoundToInt(scratchTravel / ScratchGoalDegrees * 100f))}%");
        }
        else
        {
            SetActionText(nearControl == 0 ? "SLIDER IN REACH: HOLD + PUSH"
                : nearControl == 2 ? "RECORD IN REACH: HOLD + TURN"
                : actionHint);
        }
    }

    private void SetActionText(string value)
    {
        instructionAction.text = value;
    }

    private Vector3 XRPoint()
    {
        return deck.InverseTransformPoint(interactor.GetAttachTransform(interactable).position);
    }

    private void Drag(Vector3 point)
    {
        if (held == 0)
        {
            faderTravel = Mathf.Clamp(faderTravel - (point.x - lastPoint.x) * front * 1.25f, 0f, unit);
            fader.localPosition = faderStart - Vector3.right * (faderTravel * front);
            if (step is Step.Crossfade or Step.Complete)
            {
                float blend = Mathf.Clamp01(faderTravel / (unit * 0.80f));
                music.volume = originalVolume * (1f - blend);
                secondTrack.volume = originalSecondVolume * blend;
                if (blend >= 1f && step == Step.Crossfade)
                    Advance(Step.Complete);
            }
            else
            {
                SetMusicLevel();
                if (faderTravel >= unit * 0.80f)
                    Advance(Step.Scratch);
            }
        }
        else
        {
            Vector3 from = Vector3.ProjectOnPlane(lastPoint - platterCenter, Vector3.up);
            Vector3 to = Vector3.ProjectOnPlane(point - platterCenter, Vector3.up);
            if (from.sqrMagnitude > 0.06f * unit * unit && to.sqrMagnitude > 0.06f * unit * unit)
            {
                float delta = Mathf.Clamp(Vector3.SignedAngle(from, to, Vector3.up), -20f, 20f);
                platterOffset = Mathf.Repeat(platterOffset + delta, 360f);
                scratchTravel += Mathf.Abs(delta);
                adjustingPitch = true;
                turnPitch = Mathf.Clamp(delta / Mathf.Max(Time.deltaTime, 0.005f) / 360f, -0.42f, 0.42f);
                if (scratchTravel >= ScratchGoalDegrees && step == Step.Scratch)
                    FinishScratch();
            }
        }
        lastPoint = point;
    }

    private void FinishScratch()
    {
        faderTravel = 0f;
        fader.localPosition = faderStart;
        secondTrack.Play();
        Advance(Step.Crossfade);
    }

    private void Advance(Step next)
    {
        step = next;
        Release();
        ShowStep();
    }

    private void Release()
    {
        held = -1;
        triggerOwner = -1;
        interactor = null;
        interactable = null;
    }

    private void ShowStep()
    {
        faderRenderer.sharedMaterial = Expects(0) ? activeMaterial : idleMaterial;
        foreach (Renderer button in songButtons)
            button.gameObject.SetActive(step == Step.PickSongs && songs.Length >= 2);
        for (int i = 0; i < targets.Length; i++)
        {
            bool available = Expects(i);
            targets[i].enabled = available;
            controlLabels[i].rectTransform.parent.parent.gameObject.SetActive(available);
        }
        controlLabels[0].text = step == Step.Fader ? "VOLUME SLIDER" : "CROSSFADER";
        controlLabels[1].text = step == Step.Complete ? "NEW MIX" : "START MIX";
        controlLabels[2].text = "SCRATCH HERE";
        faderGuide.gameObject.SetActive(step is Step.Fader or Step.Crossfade);
        foreach (Renderer button in songButtons)
            button.sharedMaterial = activeMaterial;
        cueRenderer.sharedMaterial = Expects(1) ? activeMaterial : idleMaterial;
        scratchRenderer.sharedMaterial = Expects(2) ? activeMaterial : idleMaterial;
        (string heading, string goal, string action) = songs.Length < 2
            ? ("DJ BOOTH", "Two tracks are needed to start a mix.", "TRACKS UNAVAILABLE")
            : step switch
        {
            Step.PickSongs => ("START YOUR MIX",
                $"A  {TrackLabel(0)}\nB  {TrackLabel(1)}\nTouch CHANGE A or CHANGE B to switch tracks.", "TOUCH START MIX"),
            Step.Fader => ("1 / 3   VOLUME SLIDER", "Hold the index trigger at the slider.\nPush it away to the pink marker.",
                "HOLD TRIGGER + PUSH AWAY"),
            Step.Scratch => ("2 / 3   SCRATCH THE RECORD", "Reach to SCRATCH HERE on the record.\nHold the index trigger and turn it.",
                "HOLD TRIGGER + TURN"),
            Step.Crossfade => ("3 / 3   CROSSFADER", $"Hold the trigger at the slider.\nPush to the pink marker to mix in B.",
                "HOLD TRIGGER + PUSH AWAY"),
            _ => ("FREE MIX", "Keep blending or scratch the record.\nTouch NEW MIX to choose new tracks.", "CROSSFADER  /  SCRATCH  /  NEW MIX")
        };
        instructionTitle.text = heading;
        prompt.text = goal;
        actionHint = action;
        SetActionText(action);
    }

    private string TrackLabel(int slot)
    {
        string title = songs[songSelection[slot]].name;
        return title.Length > 24 ? title.Substring(0, 23) + "..." : title;
    }

    private void SetMusicLevel()
    {
        if (music != null)
            music.volume = originalVolume * Mathf.Lerp(0.25f, 1f, Mathf.Clamp01(faderTravel / (unit * 0.80f)));
    }

    public void Dispose()
    {
        if (boothMovement != null)
        {
            boothMovement.enabled = false;
            boothMovement.rightHandMoveInput = originalRightMove;
            boothMovement.moveSpeed = originalMoveSpeed;
            boothMovement.enabled = originalMoveEnabled;
        }
        if (music != null)
        {
            music.Stop();
            music.clip = originalClip;
            music.loop = originalLoop;
            music.volume = originalVolume;
            music.pitch = originalPitch;
        }
        if (secondTrack != null)
        {
            secondTrack.Stop();
            secondTrack.clip = originalSecondClip;
            secondTrack.loop = originalSecondLoop;
            secondTrack.volume = originalSecondVolume;
        }
        UnityEngine.Object.Destroy(rig);
        UnityEngine.Object.Destroy(activeMaterial);
        UnityEngine.Object.Destroy(idleMaterial);
    }
}


internal sealed class LivingRoomBillboard
{
    private readonly Vector2 size;
    private readonly RectTransform content;

    internal RectTransform Transform { get; }

    internal LivingRoomBillboard(UnityEngine.Transform parent, string name, Vector2 size, float scale, Color accent)
    {
        this.size = size;
        var canvas = new GameObject(name, typeof(RectTransform), typeof(Canvas)).GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        Transform = canvas.GetComponent<RectTransform>();
        Transform.SetParent(parent, false);
        Transform.sizeDelta = size;
        Transform.localScale = Vector3.one * scale;
        AddGraphic(Transform, "Frame", accent).rectTransform.sizeDelta = size;

        var background = AddGraphic(Transform, "Background", new Color(0.027f, 0.035f, 0.048f));
        content = background.rectTransform;
        content.anchorMin = Vector2.zero;
        content.anchorMax = Vector2.one;
        content.offsetMin = new Vector2(5, 5);
        content.offsetMax = new Vector2(-5, -5);
        background.gameObject.AddComponent<RectMask2D>();
    }

    internal RectTransform AddPanel(string name, Rect bounds, Color color)
    {
        RectTransform rect = AddGraphic(content, name, color).rectTransform;
        Place(rect, bounds);
        return rect;
    }

    internal Text AddText(string name, Rect bounds, int fontSize, Color color, FontStyle style = FontStyle.Normal)
    {
        var text = new GameObject(name, typeof(RectTransform), typeof(Text)).GetComponent<Text>();
        text.rectTransform.SetParent(content, false);
        Place(text.rectTransform, bounds);
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.color = color;
        text.alignment = TextAnchor.MiddleCenter;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        text.resizeTextForBestFit = true;
        text.resizeTextMinSize = Mathf.Max(14, fontSize - 15);
        text.resizeTextMaxSize = fontSize;
        text.raycastTarget = false;
        return text;
    }

    internal static Image AddGraphic(UnityEngine.Transform parent, string name, Color color)
    {
        var image = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        image.rectTransform.SetParent(parent, false);
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private void Place(RectTransform rect, Rect bounds)
    {
        // Convert the panel's reference-pixel coordinates to anchors in the masked background.
        rect.anchorMin = new Vector2(bounds.xMin / size.x, 1f - bounds.yMax / size.y);
        rect.anchorMax = new Vector2(bounds.xMax / size.x, 1f - bounds.yMin / size.y);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }
}
