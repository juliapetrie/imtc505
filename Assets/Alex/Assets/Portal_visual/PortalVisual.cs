using UnityEngine;
using UnityEngine.VFX;

/// <summary>
/// Put on the same object as the portal's Visual Effect component.
/// Sets which cubemap shows through THIS portal, so every portal can share
/// one VFX Graph asset but display a different memory.
///
/// Requires an EXPOSED Cubemap property in the VFX Graph that is connected to
/// the "Otherside" output's _Otherside_Texture slot (see setup notes).
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(VisualEffect))]
public class PortalVisual : MonoBehaviour
{
    [Tooltip("The cubemap shown through this portal (the memory's 'sky').")]
    public Cubemap othersideCubemap;

    [Tooltip("Exact name of the exposed Cubemap property in the VFX Graph blackboard.")]
    public string cubemapProperty = "OthersideCubemap";

    VisualEffect _vfx;

    void OnEnable() => Apply();

    // Some VFX versions reset exposed values on init, so apply again at Start.
    void Start() => Apply();

    // Updates the portal in the Scene view when you change the field in the Inspector.
    void OnValidate() => Apply();

    /// <summary>Call from other scripts if you ever want to change it at runtime.</summary>
    public void SetCubemap(Cubemap cubemap)
    {
        othersideCubemap = cubemap;
        Apply();
    }

    void Apply()
    {
        if (othersideCubemap == null) return;

        if (_vfx == null) _vfx = GetComponent<VisualEffect>();
        if (_vfx == null) return;

        if (!_vfx.HasTexture(cubemapProperty))
        {
            Debug.LogWarning(
                $"PortalVisual: the VFX has no exposed texture property named '{cubemapProperty}'. " +
                "Check the name matches the VFX Graph blackboard exactly.", this);
            return;
        }

        _vfx.SetTexture(cubemapProperty, othersideCubemap);
    }
}