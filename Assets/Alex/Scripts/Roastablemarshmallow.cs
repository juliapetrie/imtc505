using UnityEngine;

/// <summary>
/// Put this on the marshmallow. While inside the campfire's heat radius it "cooks"
/// and its colour follows the gradient: white -> golden -> brown -> burnt.
/// </summary>
public class RoastableMarshmallow : MonoBehaviour
{
    public CampfireHeatZone fire;

    [Tooltip("Renderer(s) of the marshmallow model. Leave empty to auto-find in children.")]
    public Renderer[] renderers;

    [Tooltip("Which material slots to tint. Empty = tint every slot. " +
             "The Marshmallows GLB has 3 materials; check which slot is the white one.")]
    public int[] tintMaterialSlots;

    [Tooltip("Seconds at full heat to go from raw to burnt.")]
    public float secondsToBurn = 10f;

    [Tooltip("0 = raw, 1 = burnt")]
    public Gradient cookGradient;

    [Range(0f, 1f)] public float cookProgress;
    public float CookProgress => cookProgress;

    struct Slot { public Material mat; public int prop; public Color baseTint; }
    System.Collections.Generic.List<Slot> slots = new System.Collections.Generic.List<Slot>();

    static readonly int[] colorProps =
    {
        Shader.PropertyToID("_BaseColor"),        // URP Lit
        Shader.PropertyToID("_BaseColorFactor"),  // glTFast shader graph
        Shader.PropertyToID("_Color"),            // Built-in
    };

    void Reset()
    {
        cookGradient = new Gradient
        {
            colorKeys = new[]
            {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(new Color(1f, 0.85f, 0.45f), 0.35f),  // golden
                new GradientColorKey(new Color(0.55f, 0.3f, 0.1f), 0.7f),  // brown
                new GradientColorKey(new Color(0.07f, 0.04f, 0.03f), 1f),  // burnt
            },
            alphaKeys = new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) }
        };
    }

    void Awake()
    {
        if (renderers == null || renderers.Length == 0)
            renderers = GetComponentsInChildren<Renderer>();

        foreach (var r in renderers)
        {
            var mats = r.materials; // instances, so we don't edit the asset
            for (int i = 0; i < mats.Length; i++)
            {
                if (tintMaterialSlots != null && tintMaterialSlots.Length > 0
                    && System.Array.IndexOf(tintMaterialSlots, i) < 0) continue;

                foreach (int p in colorProps)
                {
                    if (!mats[i].HasProperty(p)) continue;
                    slots.Add(new Slot { mat = mats[i], prop = p, baseTint = mats[i].GetColor(p) });
                    break;
                }
            }
            r.materials = mats;
        }
        Apply();
    }

    void Update()
    {
        if (fire == null) return;
        float heat = fire.GetHeat(transform.position);
        if (heat > 0f)
        {
            cookProgress = Mathf.Clamp01(cookProgress + heat * Time.deltaTime / secondsToBurn);
            Apply();
        }
    }

    void Apply()
    {
        Color c = cookGradient.Evaluate(cookProgress);
        foreach (var s in slots) s.mat.SetColor(s.prop, s.baseTint * c);
    }

    public void ResetCooking() { cookProgress = 0f; Apply(); }
}