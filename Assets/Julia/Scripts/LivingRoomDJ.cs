using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public sealed class LivingRoomDJ : MonoBehaviour
{
    [SerializeField, Min(0f)] private float platterRpm = 33.33f;
    [SerializeField, Min(1f)] private float demoBpm = 120f;
    [SerializeField, Range(0f, 0.4f)] private float speakerTravel = 0.22f;
    [Tooltip("Optional. Leave empty to react to the scene's audio mix.")]
    [SerializeField] private AudioSource musicSource;
    [Tooltip("Optional second track. Assign a separate AudioSource with a clip to unlock the crossfade step.")]
    [SerializeField] private AudioSource secondMusicSource;

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
        challenge = new LivingRoomDJChallenge(deck.transform, unit, forward, platterCenter, musicSource, secondMusicSource);
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
        entrance?.Update(elapsed, partyLighting.Phase);

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
        if (musicSource != null)
        {
            musicSource.GetSpectrumData(spectrum, 0, FFTWindow.BlackmanHarris);
            if (secondMusicSource != null && secondMusicSource != musicSource && secondMusicSource.isPlaying)
            {
                secondMusicSource.GetSpectrumData(rightSpectrum, 0, FFTWindow.BlackmanHarris);
                for (int i = 0; i < spectrum.Length; i++)
                    spectrum[i] = Mathf.Max(spectrum[i] * musicSource.volume,
                        rightSpectrum[i] * secondMusicSource.volume);
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
    private enum Step { Start, Fader, Cue, Scratch, Crossfade, Complete }

    private static readonly Color Ready = new Color(0.16f, 0.95f, 0.82f);
    private static readonly Color Idle = new Color(0.17f, 0.21f, 0.35f);
    private readonly GameObject rig;
    private readonly Transform deck;
    private readonly float unit;
    private readonly float front;
    private readonly Camera camera;
    private readonly AudioSource music;
    private readonly AudioSource secondTrack;
    private readonly float originalVolume;
    private readonly float originalSecondVolume;
    private readonly float originalPitch;
    private readonly bool hasCrossfade;
    private float turnPitch;
    private bool adjustingPitch;
    private readonly TextMesh prompt;
    private readonly Transform fader;
    private readonly Renderer faderRenderer;
    private readonly Renderer cueRenderer;
    private readonly Renderer scratchRenderer;
    private readonly Material activeMaterial;
    private readonly Material idleMaterial;
    private readonly Collider[] targets = new Collider[3];
    private readonly Vector3 faderStart;
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

    public LivingRoomDJChallenge(Transform deck, float unit, float front, Vector3 platterCenter, AudioSource music, AudioSource secondTrack)
    {
        this.deck = deck;
        this.unit = unit;
        this.front = front;
        camera = Camera.main;
        this.music = music;
        originalVolume = music != null ? music.volume : 1f;
        originalPitch = music != null ? music.pitch : 1f;
        this.secondTrack = secondTrack;
        hasCrossfade = music != null && music.clip != null && secondTrack != null
            && secondTrack != music && secondTrack.clip != null;
        if (hasCrossfade)
        {
            originalSecondVolume = secondTrack.volume;
            secondTrack.volume = 0f;
        }
        rig = new GameObject("DJ Booth Challenge");
        rig.transform.SetParent(deck, false);

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        activeMaterial = new Material(shader) { color = Ready };
        activeMaterial.EnableKeyword("_EMISSION");
        activeMaterial.SetColor("_EmissionColor", Ready * 0.38f);
        idleMaterial = new Material(shader) { color = Idle };

        faderStart = new Vector3(1.25f * front, 1.62f, -1.44f) * unit;
        fader = CreatePart("Fader Cap", faderStart, new Vector3(0.36f, 0.20f, 0.38f) * unit);
        faderRenderer = fader.GetComponent<Renderer>();
        AddTarget(0, "Fader Drag Area", faderStart - Vector3.right * (0.48f * unit * front),
            new Vector3(1.85f, 0.44f, 0.62f) * unit);

        Vector3 pad = new Vector3(1.87f * front, 1.58f, 2.90f) * unit;
        cueRenderer = CreatePart("Cue Pad", pad, new Vector3(0.54f, 0.16f, 0.60f) * unit)
            .GetComponent<Renderer>();
        AddTarget(1, "Cue Pad Button", pad, new Vector3(0.78f, 0.40f, 0.82f) * unit);

        this.platterCenter = new Vector3(platterCenter.x, 1.88f * unit, -platterCenter.z);
        AddTarget(2, "Turntable Grab Area", this.platterCenter, new Vector3(3.70f, 0.40f, 3.70f) * unit);
        scratchRenderer = CreatePart("Turntable Cue", this.platterCenter + new Vector3(0f, 0f, -1.78f * unit),
            new Vector3(0.34f, 0.11f, 0.16f) * unit).GetComponent<Renderer>();

        var panel = new GameObject("DJ Instructions");
        panel.transform.SetParent(rig.transform, false);
        panel.transform.localPosition = new Vector3(-1.60f * front, 4.15f, 0f) * unit;
        panel.transform.localRotation = Quaternion.LookRotation(new Vector3(-front, -0.19f, 0f));
        prompt = panel.AddComponent<TextMesh>();
        prompt.anchor = TextAnchor.MiddleCenter;
        prompt.alignment = TextAlignment.Center;
        prompt.fontSize = 80;
        prompt.characterSize = 0.039f * unit;
        prompt.color = new Color(0.87f, 0.98f, 1f);
        ShowStep();
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
        collider.size = size;
        targets[index] = collider;
        var xr = target.AddComponent<XRSimpleInteractable>();
        xr.selectEntered.AddListener(args =>
        {
            if (held >= 0 || !Expects(index))
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
        return step switch
        {
            Step.Fader or Step.Crossfade => index == 0,
            Step.Scratch => index == 2,
            _ => index == 1
        };
    }

    private void Begin(int index, Vector3 point)
    {
        if (index == 1)
        {
            if (step == Step.Cue && music != null && music.clip != null && !music.isPlaying)
                music.Play();

            step = step == Step.Cue ? Step.Scratch : Step.Fader;
            if (step == Step.Fader)
            {
                faderTravel = 0f;
                if (hasCrossfade)
                {
                    secondTrack.Stop();
                    secondTrack.volume = 0f;
                }
                SetMusicLevel();
                scratchTravel = 0f;
                platterOffset = 0f;
                fader.localPosition = faderStart;
            }
            Release();
            ShowStep();
            return;
        }

        held = index;
        lastPoint = point;
    }

    public void Update()
    {
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

        if (interactor != null && held >= 0)
        {
            Drag(XRPoint());
            return;
        }

        Mouse mouse = Mouse.current;
        if (mouse == null || camera == null)
            return;
        Ray ray = camera.ScreenPointToRay(mouse.position.ReadValue());
        if (mouse.leftButton.wasPressedThisFrame && held < 0)
        {
            for (int i = 0; i < targets.Length; i++)
            {
                if (Expects(i) && targets[i].Raycast(ray, out RaycastHit hit, 4f))
                {
                    Begin(i, Project(ray, deck.InverseTransformPoint(hit.point)));
                    break;
                }
            }
        }
        if (held >= 0 && mouse.leftButton.isPressed)
            Drag(Project(ray, lastPoint));
        if (mouse.leftButton.wasReleasedThisFrame)
            Release();
    }

    private Vector3 XRPoint()
    {
        Transform attach = interactor.GetAttachTransform(interactable);
        Vector3 fallback = deck.InverseTransformPoint(attach.position);
        if (interactor is XRRayInteractor rayInteractor && rayInteractor.rayOriginTransform != null)
        {
            Transform origin = rayInteractor.rayOriginTransform;
            return Project(new Ray(origin.position, origin.forward), fallback);
        }
        return fallback;
    }

    private Vector3 Project(Ray ray, Vector3 fallback)
    {
        var plane = new Plane(deck.up, deck.TransformPoint(Vector3.up * (1.88f * unit)));
        return plane.Raycast(ray, out float distance)
            ? deck.InverseTransformPoint(ray.GetPoint(distance))
            : fallback;
    }

    private void Drag(Vector3 point)
    {
        if (held == 0)
        {
            faderTravel = Mathf.Clamp(faderTravel - (point.x - lastPoint.x) * front, 0f, unit);
            fader.localPosition = faderStart - Vector3.right * (faderTravel * front);
            if (step == Step.Crossfade)
            {
                float blend = Mathf.Clamp01(faderTravel / (unit * 0.80f));
                music.volume = originalVolume * (1f - blend);
                secondTrack.volume = originalSecondVolume * blend;
                if (blend >= 1f)
                    Advance(Step.Complete);
            }
            else
            {
                SetMusicLevel();
                if (faderTravel >= unit * 0.80f)
                    Advance(Step.Cue);
            }
        }
        else
        {
            Vector3 from = Vector3.ProjectOnPlane(lastPoint - platterCenter, Vector3.up);
            Vector3 to = Vector3.ProjectOnPlane(point - platterCenter, Vector3.up);
            if (from.sqrMagnitude > 0.06f * unit * unit && to.sqrMagnitude > 0.06f * unit * unit)
            {
                float delta = Vector3.SignedAngle(from, to, Vector3.up);
                platterOffset = Mathf.Repeat(platterOffset + delta, 360f);
                scratchTravel += delta;
                adjustingPitch = true;
                turnPitch = Mathf.Clamp(delta / Mathf.Max(Time.deltaTime, 0.005f) / 360f, -0.42f, 0.42f);
                if (Mathf.Abs(scratchTravel) >= 65f)
                    FinishScratch();
            }
        }
        lastPoint = point;
    }

    private void FinishScratch()
    {
        if (!hasCrossfade)
        {
            Advance(Step.Complete);
            return;
        }

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
        interactor = null;
        interactable = null;
    }

    private void ShowStep()
    {
        faderRenderer.sharedMaterial = Expects(0) ? activeMaterial : idleMaterial;
        int total = hasCrossfade ? 4 : 3;
        cueRenderer.sharedMaterial = Expects(1) ? activeMaterial : idleMaterial;
        scratchRenderer.sharedMaterial = step == Step.Scratch ? activeMaterial : idleMaterial;
        prompt.text = step switch
        {
            Step.Start => "DJ CHALLENGE\nSelect the lit pad to start",
            Step.Fader => $"1 / {total}  SET THE LEVEL\nHold the lit fader and push it away",
            Step.Cue => $"2 / {total}  CUE THE TRACK\nSelect the lit pad",
            Step.Scratch => $"3 / {total}  SCRATCH THE RECORD\nHold the marked deck and turn it",
            Step.Crossfade => "4 / 4  BLEND INTO TRACK 2\nPush the lit fader all the way forward",
            _ => "MIX COMPLETE\nSelect the lit pad to replay"
        };
    }

    private void SetMusicLevel()
    {
        if (music != null)
            music.volume = originalVolume * Mathf.Lerp(0.25f, 1f, Mathf.Clamp01(faderTravel / (unit * 0.80f)));
    }

    public void Dispose()
    {
        if (music != null)
        {
            music.volume = originalVolume;
            music.pitch = originalPitch;
        }
        if (hasCrossfade && secondTrack != null)
        {
            secondTrack.Stop();
            secondTrack.volume = originalSecondVolume;
        }
        UnityEngine.Object.Destroy(rig);
        UnityEngine.Object.Destroy(activeMaterial);
        UnityEngine.Object.Destroy(idleMaterial);
    }
}
