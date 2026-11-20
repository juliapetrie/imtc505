using UnityEngine;

/// <summary>
/// Put on a Point Light placed in the flames. Makes the light flicker like a fire:
/// intensity and colour wander smoothly, and the light jitters a little in position.
/// </summary>
[RequireComponent(typeof(Light))]
public class FireLightFlicker : MonoBehaviour
{
    [Header("Intensity")]
    public float baseIntensity = 3f;
    [Range(0f, 1f), Tooltip("0.3 = intensity wanders between 70% and 130%.")]
    public float intensityVariation = 0.3f;
    public float speed = 3f;

    [Header("Colour (warm orange <-> yellow)")]
    public Color coolColor = new Color(1f, 0.45f, 0.1f);
    public Color hotColor = new Color(1f, 0.7f, 0.25f);

    [Header("Movement")]
    [Tooltip("Metres the light shakes around, which makes shadows dance.")]
    public float positionJitter = 0.03f;

    Light fireLight;
    Vector3 startPos;
    float seed;

    void Awake()
    {
        fireLight = GetComponent<Light>();
        startPos = transform.localPosition;
        seed = Random.value * 100f;
    }

    void Update()
    {
        float t = Time.time * speed;
        // two noise layers: slow swell + fast flutter
        float slow = Mathf.PerlinNoise(t * 0.5f, seed);
        float fast = Mathf.PerlinNoise(t * 2.5f, seed + 10f);
        float n = Mathf.Lerp(slow, fast, 0.35f);

        fireLight.intensity = baseIntensity * (1f + (n - 0.5f) * 2f * intensityVariation);
        fireLight.color = Color.Lerp(coolColor, hotColor, n);

        float jx = (Mathf.PerlinNoise(t, seed + 20f) - 0.5f) * 2f * positionJitter;
        float jz = (Mathf.PerlinNoise(seed + 30f, t) - 0.5f) * 2f * positionJitter;
        transform.localPosition = startPos + new Vector3(jx, 0f, jz);
    }
}
