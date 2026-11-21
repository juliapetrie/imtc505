using UnityEngine;
using UnityEngine.VFX;

/// <summary>
/// Put on the same object as the portal's Visual Effect component.
/// Sets which cubemap shows through THIS portal and which way it faces.
///
/// Requires EXPOSED properties in the VFX Graph:
///  - a Cubemap property  (default name "OthersideCubemap")
///  - a Float property    (default name "OthersideRotation"), optional
/// each connected to the matching slot on the "Otherside" output.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(VisualEffect))]
public class PortalVisual : MonoBehaviour
{
    [Header("Cubemap")]
    [Tooltip("The cubemap shown through this portal (the memory's 'sky').")]
    public Cubemap othersideCubemap;

    [Tooltip("Exact name of the exposed Cubemap property in the VFX Graph blackboard.")]
    public string cubemapProperty = "OthersideCubemap";

    [Header("Direction")]
    [Tooltip("Turns the cubemap around the vertical axis, in degrees. Set it once per memory until the focal point faces the right way.")]
    [Range(-180f, 180f)] public float yawOffset = 0f;

    [Tooltip("Also turn the cubemap with the portal, so the focal point always appears straight through the doorway however you place or rotate the portal.")]
    public bool followPortalYaw = true;

    [Tooltip("Tick if rotating the portal makes the scene turn the wrong way.")]
    public bool invertFollow = false;

    [Tooltip("Exact name of the exposed Float property in the VFX Graph blackboard.")]
    public string rotationProperty = "OthersideRotation";

    VisualEffect _vfx;

    void OnEnable() => Apply();

    // Some VFX versions reset exposed values on init, so apply again at Start.
    void Start() => Apply();

    // Updates the portal in the Scene view when you change a field in the Inspector.
    void OnValidate() => Apply();

    // Only needed so the portal can follow its own rotation.
    void Update()
    {
        if (followPortalYaw) ApplyRotation();
    }

    /// <summary>Call from other scripts if you ever want to change it at runtime.</summary>
    public void SetCubemap(Cubemap cubemap)
    {
        othersideCubemap = cubemap;
        Apply();
    }

    void Apply()
    {
        if (_vfx == null) _vfx = GetComponent<VisualEffect>();
        if (_vfx == null) return;

        ApplyCubemap();
        ApplyRotation();
    }

    void ApplyCubemap()
    {
        if (othersideCubemap == null) return;

        if (!_vfx.HasTexture(cubemapProperty))
        {
            Debug.LogWarning(
                $"PortalVisual: the VFX has no exposed texture property named '{cubemapProperty}'. " +
                "Check the name matches the VFX Graph blackboard exactly.", this);
            return;
        }

        _vfx.SetTexture(cubemapProperty, othersideCubemap);
    }

    void ApplyRotation()
    {
        if (_vfx == null) _vfx = GetComponent<VisualEffect>();
        // No warning here: the rotation property is optional until you add it to the graph.
        if (_vfx == null || !_vfx.HasFloat(rotationProperty)) return;

        float degrees = yawOffset;
        if (followPortalYaw)
        {
            float portalYaw = transform.eulerAngles.y;
            degrees += invertFollow ? -portalYaw : portalYaw;
        }

        _vfx.SetFloat(rotationProperty, degrees);
    }
}