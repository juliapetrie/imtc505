using UnityEngine;

/// <summary>
/// Per-side marshmallow cooking. Put on the marshmallow object that has a MeshFilter
/// (e.g. the capsule). Each vertex has its own cook value: parts of the surface that
/// face the fire cook faster, so you must rotate the stick to toast it evenly.
///
/// Needs a material that displays vertex colours, e.g. "Universal Render Pipeline/Particles/Lit"
/// (or a Shader Graph with a Vertex Color node). URP Lit ignores vertex colours.
/// </summary>
[RequireComponent(typeof(MeshFilter))]
public class RoastableMarshmallow : MonoBehaviour
{
    public CampfireHeatZone fire;

    [Tooltip("Seconds for a side facing the fire, at full heat, to go from raw to burnt.")]
    public float secondsToBurn = 10f;

    [Range(0f, 1f), Tooltip("How much the side facing AWAY from the fire still cooks (0 = not at all).")]
    public float ambientHeat = 0.05f;

    [Tooltip("0 = raw, 1 = burnt")]
    public Gradient cookGradient;

    [Header("Result")]
    [Range(0f, 1f)] public float averageCook;
    [Range(0f, 1f)] public float evenness;   // 1 = all sides equally cooked

    Mesh mesh;
    Vector3[] verts, normals;
    Color[] colors;
    float[] cook;

    void Reset() { cookGradient = DefaultGradient(); }

    static Gradient DefaultGradient()
    {
        var g = new Gradient();
        g.SetKeys(
            new[]
            {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(new Color(1f, 0.85f, 0.45f), 0.35f),  // golden
                new GradientColorKey(new Color(0.55f, 0.3f, 0.1f), 0.7f),  // brown
                new GradientColorKey(new Color(0.07f, 0.04f, 0.03f), 1f),  // burnt
            },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
        return g;
    }

    void Awake()
    {
        if (fire == null) fire = FindFirstObjectByType<CampfireHeatZone>();
        if (cookGradient == null || cookGradient.colorKeys.Length < 2) cookGradient = DefaultGradient();

        var mf = GetComponent<MeshFilter>();
        mesh = mf.mesh;                 // instance, so the shared asset isn't modified
        verts = mesh.vertices;
        normals = mesh.normals;
        cook = new float[verts.Length];
        colors = new Color[verts.Length];
        ApplyColors();
    }

    void Update()
    {
        if (fire == null) return;

        float heat = fire.GetHeat(transform.position);   // 0 outside the radius, 0..1 inside
        if (heat <= 0f) return;

        Vector3 firePos = fire.CenterPosition;
        Quaternion rot = transform.rotation;
        float rate = Time.deltaTime / secondsToBurn;

        for (int i = 0; i < verts.Length; i++)
        {
            Vector3 worldPos = transform.TransformPoint(verts[i]);
            Vector3 worldNormal = rot * normals[i];
            Vector3 toFire = (firePos - worldPos).normalized;
            float facing = Mathf.Max(0f, Vector3.Dot(worldNormal, toFire));   // 0 = away, 1 = facing fire
            float exposure = Mathf.Lerp(ambientHeat, 1f, facing);
            cook[i] = Mathf.Clamp01(cook[i] + heat * exposure * rate);
        }
        ApplyColors();
    }

    void ApplyColors()
    {
        float sum = 0f, min = 1f;
        for (int i = 0; i < cook.Length; i++)
        {
            colors[i] = cookGradient.Evaluate(cook[i]);
            sum += cook[i];
            if (cook[i] < min) min = cook[i];
        }
        mesh.colors = colors;
        averageCook = cook.Length > 0 ? sum / cook.Length : 0f;
        float max = 0f; foreach (float c in cook) if (c > max) max = c;
        evenness = 1f - (max - min);
    }

    public void ResetCooking()
    {
        System.Array.Clear(cook, 0, cook.Length);
        ApplyColors();
    }
}