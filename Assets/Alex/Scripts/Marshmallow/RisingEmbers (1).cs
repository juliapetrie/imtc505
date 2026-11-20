using UnityEngine;

/// <summary>
/// Put on the "Sparks" object of campfire_split. Each child (Spark_0..Spark_3) floats up from the
/// flames, drifts sideways, grows then shrinks away, and starts over, like a rising ember.
/// </summary>
public class RisingEmbers : MonoBehaviour
{
    [Tooltip("Height (in the campfire's local units) where embers start and where they vanish.")]
    public float startHeight = 0.7f;
    public float endHeight = 2.3f;

    [Tooltip("Seconds for one ember to rise from start to end.")]
    public float riseSeconds = 3f;

    [Tooltip("How far embers drift sideways.")]
    public float drift = 0.12f;

    [Tooltip("Spin speed in degrees per second.")]
    public float spinDegreesPerSecond = 90f;

    Transform[] embers;
    Vector3[] baseScale;
    Vector2[] baseXZ;
    float[] offset, seed;

    void Awake()
    {
        int n = transform.childCount;
        embers = new Transform[n];
        baseScale = new Vector3[n];
        baseXZ = new Vector2[n];
        offset = new float[n];
        seed = new float[n];
        for (int i = 0; i < n; i++)
        {
            embers[i] = transform.GetChild(i);
            baseScale[i] = embers[i].localScale;
            baseXZ[i] = new Vector2(embers[i].localPosition.x, embers[i].localPosition.z);
            offset[i] = (float)i / Mathf.Max(1, n);      // spread them out in time
            seed[i] = Random.value * 100f;
        }
    }

    void Update()
    {
        for (int i = 0; i < embers.Length; i++)
        {
            float p = Mathf.Repeat(Time.time / riseSeconds + offset[i], 1f);   // 0..1 along the rise
            float y = Mathf.Lerp(startHeight, endHeight, p);
            float dx = (Mathf.PerlinNoise(seed[i], Time.time * 0.5f) - 0.5f) * 2f * drift;
            float dz = (Mathf.PerlinNoise(Time.time * 0.5f, seed[i] + 7f) - 0.5f) * 2f * drift;
            embers[i].localPosition = new Vector3(baseXZ[i].x + dx, y, baseXZ[i].y + dz);

            float size = Mathf.Sin(p * Mathf.PI);                               // grow, then shrink away
            embers[i].localScale = baseScale[i] * size;
            embers[i].Rotate(0f, spinDegreesPerSecond * Time.deltaTime, 0f, Space.Self);
        }
    }
}
