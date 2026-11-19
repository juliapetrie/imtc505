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
    private float phase;
    private float motionEnergy;

    public LivingRoomPartyLighting(Transform room)
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

        ledMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        // Interior wall perimeter in the LivingRoom model's local coordinates.
        Vector3[] corners =
        {
            new Vector3(-3.38f, 0.88f, -1.26f),
            new Vector3(0.67f, 0.88f, -1.26f),
            new Vector3(0.67f, 0.88f, 2.46f),
            new Vector3(-3.38f, 0.88f, 2.46f)
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
                led.transform.localScale = new Vector3(0.038f, 0.042f,
                    edge.magnitude / segmentsPerWall * 0.99f);
                MeshRenderer renderer = led.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = ledMaterial;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                segments[wall * segmentsPerWall + i] = renderer;
            }

            var fixture = new GameObject("Colour Wash " + (wall + 1));
            fixture.transform.SetParent(rig.transform, false);
            Vector3 center = (corners[0] + corners[2]) * 0.5f;
            fixture.transform.localPosition = Vector3.Lerp((start + end) * 0.5f, center, 0.23f)
                + Vector3.down * 0.12f;
            Light wash = fixture.AddComponent<Light>();
            wash.type = LightType.Point;
            wash.range = 3.1f;
            wash.shadows = LightShadows.None;
            washes[wall] = wash;
        }
    }

    public void Update(float pulse, float elapsed)
    {
        // Follow gradual changes in the music without modulating brightness on each beat.
        motionEnergy = Mathf.Lerp(motionEnergy, pulse, 1f - Mathf.Exp(-elapsed * 0.6f));
        phase = Mathf.Repeat(phase + elapsed * Mathf.Lerp(0.045f, 0.075f, motionEnergy), 1f);
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
            washes[i].intensity = 0.85f;
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
    }

    private Color Palette(float position)
    {
        float hue = 0.59f + 0.30f * (0.5f + 0.5f * Mathf.Sin((position - phase) * 2f * Mathf.PI));
        return Color.HSVToRGB(hue, 0.82f, 1f);
    }
}
