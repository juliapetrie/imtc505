using System.Collections;
using UnityEngine;

/// <summary>
/// Put this on any object that should light up when the player touches it.
/// Needs a Collider on the object, and a Rigidbody + Collider on the hands
/// (tag the hand objects with "PlayerHand", or change handTag below).
/// </summary>
[DisallowMultipleComponent]
public class GlowOnTouch : MonoBehaviour
{
    [Header("Setup")]
    [Tooltip("Material using the Custom/TouchGlow shader.")]
    public Material glowMaterial;
    [Tooltip("Tag on the player's hand / controller colliders.")]
    public string handTag = "PlayerHand";
    [Tooltip("Leave empty to use all renderers on this object and its children.")]
    public Renderer[] targetRenderers;

    [Header("Animation")]
    public float spreadTime = 1.5f;      // seconds for glow to spread across the object
    public float fadeInTime = 0.6f;
    public bool stayLit = true;          // keep glowing after the hand leaves
    public float litDuration = 3f;       // only used if stayLit is false
    public float fadeOutTime = 1.5f;     // only used if stayLit is false

    [Header("Optional")]
    [Tooltip("A Light (e.g. a point light child) that fades up with the glow.")]
    public Light halo;
    public float haloMaxIntensity = 1.5f;
    public AudioSource touchSound;

    static readonly int AmountId = Shader.PropertyToID("_Amount");
    static readonly int RadiusId = Shader.PropertyToID("_Radius");
    static readonly int TouchPointId = Shader.PropertyToID("_TouchPoint");

    Material[] glowInstances;
    Renderer[] renderers;
    float maxRadius;
    bool activated;
    Coroutine routine;

    void Awake()
    {
        renderers = (targetRenderers != null && targetRenderers.Length > 0)
            ? targetRenderers
            : GetComponentsInChildren<Renderer>();

        // Radius big enough to cover the whole object from any touch point
        Bounds b = new Bounds(transform.position, Vector3.zero);
        foreach (var r in renderers) b.Encapsulate(r.bounds);
        maxRadius = b.size.magnitude + 0.2f;

        // Build a glow overlay child for every renderer. The overlay shares the mesh and
        // puts the glow material on EVERY submesh, so multi-material meshes light up fully.
        var list = new System.Collections.Generic.List<Material>();
        foreach (var r in renderers)
        {
            var mat = new Material(glowMaterial);
            mat.SetFloat(AmountId, 0f);
            mat.SetFloat(RadiusId, 0f);
            if (CreateOverlay(r, mat)) list.Add(mat);
            else Destroy(mat);
        }
        glowInstances = list.ToArray();

        if (halo) halo.intensity = 0f;
    }

    bool CreateOverlay(Renderer source, Material mat)
    {
        Mesh mesh = null;
        Renderer overlay = null;

        var go = new GameObject(source.name + "_Glow");
        go.transform.SetParent(source.transform, false);
        go.layer = source.gameObject.layer;

        if (source is SkinnedMeshRenderer smr && smr.sharedMesh != null)
        {
            mesh = smr.sharedMesh;
            var o = go.AddComponent<SkinnedMeshRenderer>();
            o.sharedMesh = mesh;
            o.bones = smr.bones;
            o.rootBone = smr.rootBone;
            o.localBounds = smr.localBounds;
            overlay = o;
        }
        else if (source is MeshRenderer && source.TryGetComponent(out MeshFilter mf) && mf.sharedMesh != null)
        {
            mesh = mf.sharedMesh;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            overlay = go.AddComponent<MeshRenderer>();
        }

        if (overlay == null) { Destroy(go); return false; }

        var mats = new Material[mesh.subMeshCount];
        for (int i = 0; i < mats.Length; i++) mats[i] = mat;
        overlay.sharedMaterials = mats;
        overlay.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        overlay.receiveShadows = false;
        return true;
    }

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(handTag)) return;
        Touch(other.ClosestPoint(transform.position));
    }

    void OnCollisionEnter(Collision collision)
    {
        if (!collision.collider.CompareTag(handTag)) return;
        Touch(collision.contactCount > 0 ? collision.GetContact(0).point : collision.collider.transform.position);
    }

    public void Touch(Vector3 worldPoint)
    {
        if (activated && stayLit) return; // already glowing
        activated = true;
        if (touchSound) touchSound.Play();
        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(GlowRoutine(worldPoint));
    }

    IEnumerator GlowRoutine(Vector3 point)
    {
        foreach (var m in glowInstances) m.SetVector(TouchPointId, point);

        float t = 0f;
        float total = Mathf.Max(spreadTime, fadeInTime);
        while (t < total)
        {
            t += Time.deltaTime;
            float spread = Mathf.SmoothStep(0f, 1f, t / spreadTime);
            float amount = Mathf.SmoothStep(0f, 1f, t / fadeInTime);
            SetGlow(amount, spread * maxRadius);
            yield return null;
        }
        SetGlow(1f, maxRadius);

        if (stayLit) yield break;

        yield return new WaitForSeconds(litDuration);

        t = 0f;
        while (t < fadeOutTime)
        {
            t += Time.deltaTime;
            SetGlow(1f - Mathf.SmoothStep(0f, 1f, t / fadeOutTime), maxRadius);
            yield return null;
        }
        SetGlow(0f, 0f);
        activated = false;
    }

    void SetGlow(float amount, float radius)
    {
        foreach (var m in glowInstances)
        {
            m.SetFloat(AmountId, amount);
            m.SetFloat(RadiusId, radius);
        }
        if (halo) halo.intensity = amount * haloMaxIntensity;
    }

    void OnDestroy()
    {
        if (glowInstances == null) return;
        foreach (var m in glowInstances) if (m) Destroy(m);
    }
}