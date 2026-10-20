using UnityEngine;

/// <summary>
/// Put this on the campfire. Defines a spherical "heat radius" around the flames.
/// </summary>
public class CampfireHeatZone : MonoBehaviour
{
    [Tooltip("Where the flames are. Create an empty child positioned in the middle of the fire.")]
    public Transform heatCenter;

    [Tooltip("Metres. Marshmallows closer than this start cooking.")]
    public float radius = 0.6f;

    [Tooltip("If true, heat is stronger the closer you get. If false, it's simply on/off inside the radius.")]
    public bool strongerWhenCloser = true;

    Vector3 Center => heatCenter != null ? heatCenter.position : transform.position;

    /// <summary>Returns 0 outside the radius, 0..1 inside.</summary>
    public float GetHeat(Vector3 worldPos)
    {
        float d = Vector3.Distance(worldPos, Center);
        if (d >= radius) return 0f;
        return strongerWhenCloser ? 1f - d / radius : 1f;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.4f, 0f, 0.35f);
        Gizmos.DrawWireSphere(Center, radius);
    }
}