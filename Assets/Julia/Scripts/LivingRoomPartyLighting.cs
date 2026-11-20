using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

internal sealed class LivingRoomPartyLighting
{
    private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
    private const int SegmentCount = 64;
    private readonly GameObject rig;
    private readonly Material ledMaterial;
    private readonly MaterialPropertyBlock properties = new MaterialPropertyBlock();
    private readonly MeshRenderer[] segments = new MeshRenderer[SegmentCount];
    private readonly Light[] washes = new Light[4];
    private readonly Light daylight;
    private readonly float oldDaylight;
    private readonly AmbientMode oldAmbientMode;
    private readonly Color oldAmbient;
    private readonly float oldReflections;
    private readonly List<GameObject> speakerRings = new List<GameObject>();
    private readonly Material ringMaterial;
    private readonly Mesh ringMesh;
    private float phase;
    public float Phase => phase;
    private float motionEnergy;

    public LivingRoomPartyLighting(Transform room, Vector3 djPosition)
    {
        rig = new GameObject("Music Reactive Party Lights");
        rig.transform.SetParent(room, false);

        oldAmbientMode = RenderSettings.ambientMode;
        oldAmbient = RenderSettings.ambientLight;
        oldReflections = RenderSettings.reflectionIntensity;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.045f, 0.039f, 0.086f);
        RenderSettings.reflectionIntensity = 0.18f;

        foreach (GameObject root in room.gameObject.scene.GetRootGameObjects())
        {
            if (root.name == "Directional Light" && root.TryGetComponent(out daylight))
            {
                oldDaylight = daylight.intensity;
                daylight.intensity = 0.055f;
                break;
            }
        }

        ringMaterial = new Material(Resources.Load<Shader>("LivingRoomSpeakerRing"));
        ringMesh = CreateRingMesh();
        ledMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        // Mount the strip on the room-facing side of the trim, below the roof.
        const float stripHeight = 0.042f;
        const float stripDepth = 0.038f;
        const float height = 1.818f;
        Vector3[] corners =
        {
            new Vector3(-3.375f, height, -1.255f),
            new Vector3(0.650f, height, -1.255f),
            new Vector3(0.650f, height, 2.800f),
            new Vector3(-3.375f, height, 2.725f)
        };

        const int segmentsPerWall = SegmentCount / 4;
        for (int wall = 0; wall < corners.Length; wall++)
        {
            Vector3 start = corners[wall];
            Vector3 end = corners[(wall + 1) % corners.Length];
            Vector3 edge = end - start;
            for (int i = 0; i < segmentsPerWall; i++)
            {
                GameObject led = GameObject.CreatePrimitive(PrimitiveType.Cube);
                led.name = "LED " + (wall * segmentsPerWall + i + 1);
                Object.Destroy(led.GetComponent<Collider>());
                led.transform.SetParent(rig.transform, false);
                led.transform.localPosition = start + edge * ((i + 0.5f) / segmentsPerWall);
                led.transform.localRotation = Quaternion.LookRotation(edge);
                led.transform.localScale = new Vector3(stripDepth, stripHeight,
                    edge.magnitude / segmentsPerWall);
                MeshRenderer renderer = led.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = ledMaterial;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                segments[wall * segmentsPerWall + i] = renderer;
            }

            Vector3 center = (corners[0] + corners[2]) * 0.5f;
            Vector3 position = Vector3.Lerp((start + end) * 0.5f, center, 0.23f);
            position.y = 0.798f;
            washes[wall] = AddAccentLight("Colour Wash " + (wall + 1), position,
                new Color(0.46f, 0.32f, 0.75f), 3.1f, 0.85f);
        }

        AddAccentLight("Warm Seating Fill", new Vector3(-0.75f, 0.92f, 0.60f),
            new Color(1f, 0.73f, 0.59f), 2.4f, 0.48f);
        // Position the light at the bulb inside the imported floor-lamp shade.
        AddAccentLight("Floor Lamp Bulb", new Vector3(-1.064f, 0.503f, 1.508f),
            new Color(1f, 0.76f, 0.48f), 1.25f, 0.24f);
        Vector3 booth = room.InverseTransformPoint(djPosition);
        AddAccentLight("DJ Booth Fill", new Vector3(booth.x, 0.87f, booth.z),
            new Color(0.52f, 0.71f, 1f), 1.7f, 0.42f);
    }

    private Light AddAccentLight(string name, Vector3 position, Color colour, float range, float intensity)
    {
        var fixture = new GameObject(name);
        fixture.transform.SetParent(rig.transform, false);
        fixture.transform.localPosition = position;
        Light light = fixture.AddComponent<Light>();
        light.type = LightType.Point;
        light.range = range;
        light.color = colour;
        light.intensity = intensity;
        light.shadows = LightShadows.None;
        return light;
    }

    public void AddSpeakerRing(Transform parent, Vector3 center, float radius)
    {
        var ring = new GameObject("Speaker LED Ring");
        ring.transform.SetParent(parent, false);
        ring.transform.localPosition = center;
        ring.transform.localScale = Vector3.one * radius;
        ring.AddComponent<MeshFilter>().sharedMesh = ringMesh;
        var renderer = ring.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = ringMaterial;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        speakerRings.Add(ring);
    }

    private static Mesh CreateRingMesh()
    {
        const int sides = 48;
        var points = new Vector3[(sides + 1) * 2];
        var uv = new Vector2[points.Length];
        var triangles = new int[sides * 6];
        for (int i = 0; i <= sides; i++)
        {
            float fraction = (float)i / sides;
            float angle = fraction * 2f * Mathf.PI;
            Vector3 radial = new Vector3(0f, Mathf.Cos(angle), Mathf.Sin(angle));
            int v = i * 2;
            points[v] = radial * 0.94f;
            points[v + 1] = radial;
            uv[v] = uv[v + 1] = new Vector2(fraction, 0f);
            if (i == sides)
                continue;

            int t = i * 6;
            triangles[t] = v;
            triangles[t + 1] = v + 1;
            triangles[t + 2] = v + 2;
            triangles[t + 3] = v + 2;
            triangles[t + 4] = v + 1;
            triangles[t + 5] = v + 3;
        }
        return new Mesh { name = "Speaker LED Ring", vertices = points, uv = uv, triangles = triangles };
    }

    public void Update(float pulse, float elapsed)
    {
        // Follow gradual changes in the music without modulating brightness on each beat.
        motionEnergy = Mathf.Lerp(motionEnergy, pulse, 1f - Mathf.Exp(-elapsed * 0.6f));
        phase = Mathf.Repeat(phase + elapsed * Mathf.Lerp(0.045f, 0.075f, motionEnergy), 1f);
        ringMaterial.SetFloat("_Phase", phase);
        for (int i = 0; i < segments.Length; i++)
        {
            float position = (float)i / segments.Length;
            properties.SetColor(BaseColor, Palette(position) * 0.65f);
            segments[i].SetPropertyBlock(properties);
        }

        for (int i = 0; i < washes.Length; i++)
        {
            float position = (i + 0.5f) / washes.Length;
            washes[i].color = Color.Lerp(new Color(0.46f, 0.32f, 0.75f), Palette(position), 0.18f);
        }
    }

    public void Dispose()
    {
        if (daylight != null)
            daylight.intensity = oldDaylight;
        RenderSettings.ambientMode = oldAmbientMode;
        RenderSettings.ambientLight = oldAmbient;
        RenderSettings.reflectionIntensity = oldReflections;
        Object.Destroy(rig);
        Object.Destroy(ledMaterial);
        foreach (GameObject ring in speakerRings)
            Object.Destroy(ring);
        Object.Destroy(ringMesh);
        Object.Destroy(ringMaterial);
    }

    private Color Palette(float position)
    {
        float hue = 0.59f + 0.30f * (0.5f + 0.5f * Mathf.Sin((position - phase) * 2f * Mathf.PI));
        return Color.HSVToRGB(hue, 0.82f, 1f);
    }
}
