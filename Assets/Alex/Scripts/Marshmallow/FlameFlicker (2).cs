using UnityEngine;

/// <summary>
/// Put on the "Flames" object of campfire_split. Every child (Flame_0..Flame_4) gets its own
/// smooth random flicker: stretching up and down, getting slightly fatter/thinner, swaying and slowly twisting.
/// The flame pieces have their pivot at their base, so they grow upward from the logs.
/// </summary>
public class FlameFlicker : MonoBehaviour
{
    [Tooltip("How fast the flames flicker.")]
    public float speed = 2.5f;

    [Range(0f, 1f), Tooltip("How much the height changes (0.25 = +/-25%).")]
    public float heightVariation = 0.25f;

    [Range(0f, 1f), Tooltip("How much the width changes.")]
    public float widthVariation = 0.12f;

    [Tooltip("Max tilt in degrees as the flame sways.")]
    public float swayDegrees = 6f;

    [Tooltip("Slow twist around the flame's own up axis.")]
    public float twistDegreesPerSecond = 20f;

    Transform[] flames;
    Vector3[] baseScale;
    Quaternion[] baseRot;
    float[] seed;

    void Awake()
    {
        int n = transform.childCount;
        flames = new Transform[n];
        baseScale = new Vector3[n];
        baseRot = new Quaternion[n];
        seed = new float[n];
        for (int i = 0; i < n; i++)
        {
            flames[i] = transform.GetChild(i);
            baseScale[i] = flames[i].localScale;
            baseRot[i] = flames[i].localRotation;
            seed[i] = Random.value * 100f;   // each flame flickers differently
        }
    }

    void Update()
    {
        float t = Time.time * speed;
        for (int i = 0; i < flames.Length; i++)
        {
            float s = seed[i];
            float h = 1f + (Mathf.PerlinNoise(t, s) - 0.5f) * 2f * heightVariation;
            float w = 1f + (Mathf.PerlinNoise(s + 50f, t * 0.7f) - 0.5f) * 2f * widthVariation;
            flames[i].localScale = Vector3.Scale(baseScale[i], new Vector3(w, h, w));

            float tiltX = (Mathf.PerlinNoise(t * 0.6f, s + 10f) - 0.5f) * 2f * swayDegrees;
            float tiltZ = (Mathf.PerlinNoise(s + 20f, t * 0.6f) - 0.5f) * 2f * swayDegrees;
            float twist = Time.time * twistDegreesPerSecond + s * 10f;
            flames[i].localRotation = baseRot[i] * Quaternion.Euler(tiltX, twist, tiltZ);
        }
    }
}
