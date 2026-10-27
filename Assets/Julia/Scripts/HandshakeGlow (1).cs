using System.Collections;
using UnityEngine;
using UnityEngine.Animations.Rigging;

// Add to NPC_Male beside VRHandshakeAuto. Assign the existing RightHandShakePoint.
public class HandshakeGlow : MonoBehaviour
{
    public Transform handshakePoint;
    public Color glowColor = new Color(1f, 0.8f, 0.3f, 1f);
    [Tooltip("Approximate diameter of the glow in metres.")]
    public float startingSize = 0.08f;
    public float fullSize = 0.22f;
    public float growTime = 1.5f;
    public float fadeSpeed = 5f;
    public float lightIntensity = 1.5f;
    public float lightRange = 0.6f;
    Rig handshakeRig;
    ParticleSystem particles;
    Light glowLight;
    float age, visibility;

    IEnumerator Start()
    {
        if (!handshakePoint) { Debug.LogError("HandshakeGlow: Assign RightHandShakePoint.", this); enabled = false; yield break; }
        yield return null;
        Transform rigTransform = transform.Find("Handshake Rig (runtime)");
        if (!rigTransform) { Debug.LogError("HandshakeGlow: Runtime rig not found. Check that VRHandshakeAuto is enabled and ready.", this); enabled = false; yield break; }
        handshakeRig = rigTransform.GetComponent<Rig>();
        if (!handshakeRig) { enabled = false; yield break; }
        var obj = new GameObject("Handshake Glow (runtime)");
        obj.transform.SetParent(handshakePoint, false);
        obj.transform.localPosition = Vector3.zero;
        obj.transform.localRotation = Quaternion.identity;
        // Use world-space scaling so parent model scale does not shrink the effect.
        obj.transform.SetParent(null, true);
        particles = obj.AddComponent<ParticleSystem>();
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = particles.main;
        main.loop = true; main.playOnAwake = false; main.duration = 2f;
        main.startLifetime = 0.4f; main.startSpeed = 0.015f;
        main.startSize = startingSize; main.startColor = glowColor;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        var emission = particles.emission; emission.rateOverTime = 35f;
        var shape = particles.shape; shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = 0.025f;
        var col = particles.colorOverLifetime; col.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(new[] { new GradientColorKey(glowColor, 0f), new GradientColorKey(glowColor, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.65f, 0.2f), new GradientAlphaKey(0f, 1f) });
        col.color = new ParticleSystem.MinMaxGradient(gradient);
        glowLight = obj.AddComponent<Light>(); glowLight.type = LightType.Point;
        glowLight.color = glowColor; glowLight.range = lightRange; glowLight.intensity = 0f;
        particles.Play();
    }
    void LateUpdate()
    {
        if (!handshakeRig || !handshakePoint || !particles) return;
        particles.transform.position = handshakePoint.position;
        bool active = handshakeRig.weight > 0.05f;
        age = active ? age + Time.deltaTime : 0f;
        visibility = Mathf.MoveTowards(visibility, active ? 1f : 0f, fadeSpeed * Time.deltaTime);
        float progress = Mathf.Clamp01(age / Mathf.Max(0.01f, growTime));
        var main = particles.main;
        main.startSize = Mathf.Lerp(startingSize, fullSize, progress);
        var emission = particles.emission; emission.rateOverTime = 35f * visibility;
        glowLight.intensity = lightIntensity * visibility * Mathf.Lerp(0.5f, 1f, progress);
    }
    void OnDestroy() { if (particles) Destroy(particles.gameObject); }
}
