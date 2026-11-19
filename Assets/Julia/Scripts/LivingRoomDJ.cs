using System.Collections.Generic;
using UnityEngine;

public sealed class LivingRoomDJ : MonoBehaviour
{
    [SerializeField, Min(0f)] private float platterRpm = 33.33f;
    [SerializeField, Min(1f)] private float demoBpm = 120f;
    [SerializeField, Range(0f, 0.4f)] private float speakerTravel = 0.22f;
    [Tooltip("Optional. Leave empty to react to the scene's audio mix.")]
    [SerializeField] private AudioSource musicSource;

    [SerializeField] private Transform roomModel;

    private readonly List<RoomSpeaker> roomSpeakers = new List<RoomSpeaker>();
    private readonly float[] spectrum = new float[512];
    private readonly float[] rightSpectrum = new float[512];
    private readonly Transform[] cones = new Transform[2];
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
    private HashSet<int> dotVertices;
    private Vector3 dotCenter;
    private float dotRadius;
    private float dotPhase;
    private float peak;
    private float averageEnergy;
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
        if (roomModel != null)
        {
            partyLighting = new LivingRoomPartyLighting(roomModel);
            foreach (MeshFilter filter in roomModel.GetComponentsInChildren<MeshFilter>())
            {
                switch (filter.name)
                {
                    case "mesh1755818725":
                        SetupScreenDot(filter);
                        break;
                    case "mesh15764063":
                    case "mesh1785736494":
                    case "mesh908259242":
                    case "mesh783608269":
                        roomSpeakers.Add(new RoomSpeaker(filter));
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
        Quaternion rotation = Quaternion.AngleAxis(angle, Vector3.up);

        for (int i = 0; i < vertices.Length; i++)
        {
            int side = platterSides[i];
            if (side == 0)
                continue;

            Vector3 center = new Vector3(platterCenter.x, restVertices[i].y, platterCenter.z * side);
            vertices[i] = center + rotation * (restVertices[i] - center);
            if (i < normals.Length)
                normals[i] = rotation * restNormals[i];
        }

        animatedMesh.vertices = vertices;
        if (normals.Length == vertices.Length)
            animatedMesh.normals = normals;

        float strength = ReadPulse(elapsed);
        pulse = Mathf.Lerp(pulse, strength, 1f - Mathf.Exp(-(strength > pulse ? 24f : 12f) * elapsed));
        AnimateScreenDot(elapsed);
        partyLighting?.Update(pulse, elapsed);

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
            dotVertices = indices;
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

    private void AnimateScreenDot(float elapsed)
    {
        if (animatedScreen == null)
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

        public RoomSpeaker(MeshFilter filter)
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

    private float ReadPulse(float elapsed)
    {
        if (musicSource != null)
            musicSource.GetSpectrumData(spectrum, 0, FFTWindow.BlackmanHarris);
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

    private void OnDestroy()
    {
        foreach (RoomSpeaker speaker in roomSpeakers)
            speaker.Dispose();
        partyLighting?.Dispose();

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
