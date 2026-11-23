using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// One look for the campus. Stage 0 = original, 1 = after memory 1, and so on.
/// Everything here is blended between stages, so you can tweak each stage in the Inspector.
/// </summary>
[Serializable]
public class EnvironmentStage
{
    public string label = "Stage";

    [Header("Fog")]
    public Color fogColor = new Color(0.30f, 0.33f, 0.40f);
    [Min(0f)] public float fogDensity = 0.04f;

    [Header("Ambient light")]
    public Color ambientColor = new Color(0.10f, 0.11f, 0.16f);

    [Header("Sun / moon")]
    public Color lightColor = Color.white;
    [Min(0f)] public float lightIntensity = 0.3f;
    [Tooltip("Lower X = lower sun. Around 5-10 gives a sunset.")]
    public Vector3 lightRotation = new Vector3(50f, -30f, 0f);

    [Header("Sky")]
    public Color skyTint = Color.gray;
    [Min(0f)] public float skyExposure = 0.6f;

    [Header("Ground mist")]
    [Tooltip("Tint of the mist. Alpha = how see-through it is.")]
    public Color mistColor = new Color(0.45f, 0.50f, 0.60f, 0.30f);
    [Tooltip("How much mist is spawned. 0 = none, 1 = the amount set on the particle system.")]
    [Range(0f, 1f)] public float mistAmount = 1f;

    [Header("Clouds")]
    [Tooltip("Tint of the cloud layer. Alpha = how thick the clouds look.")]
    public Color cloudTint = new Color(0.25f, 0.27f, 0.33f, 0.85f);
    [Tooltip("How fast the clouds drift (texture offset per second).")]
    public float cloudSpeed = 0.0015f;

    [Header("Street lamps")]
    [Range(0f, 1f)] public float lampGlow = 0.3f;

    [Header("Colour grading")]
    [Tooltip("Weight of your grading Volume. Make the Volume the dull, grey look: 1 = grey, 0 = full colour.")]
    [Range(0f, 1f)] public float gradeWeight = 1f;
}

[Serializable]
public class StageObject
{
    public GameObject target;
    [Tooltip("Switched on once this many memories are completed.")]
    [Min(0)] public int enableAtStage = 1;
}

/// <summary>
/// Put on an object in the campus scene. Reads GameState.MemoriesCompleted and
/// changes fog, lighting, sky, mist, clouds and lamps to match. After returning
/// from a memory it starts at the previous look and blends to the new one, with
/// lamps, mist and stage objects changing in a wave that spreads out from the portal.
/// </summary>
public class CampusProgress : MonoBehaviour
{
    [Header("Stages (index = memories completed)")]
    public EnvironmentStage[] stages;

    [Header("Scene references (all optional)")]
    [Tooltip("Your directional light. Must be Mixed or Realtime, not Baked.")]
    public Light mainLight;
    [Tooltip("A global Volume holding the dull/grey colour grade.")]
    public Volume gradeVolume;
    public Light[] lampLights;
    public Renderer[] lampRenderers;

    [Header("Mist and clouds (optional)")]
    [Tooltip("Particle systems for low-lying mist. Place them around the campus.")]
    public ParticleSystem[] mistSystems;
    [Tooltip("A big inside-out dome or sphere with an unlit, transparent cloud texture.")]
    public Renderer cloudDome;

    [Header("Lamp glow")]
    [ColorUsage(false, true)] public Color lampEmissionColor = new Color(1f, 0.6f, 0.2f) * 3f;
    public string emissionProperty = "_EmissionColor";

    [Header("Material / skybox property names")]
    [Tooltip("Panoramic, Cubemap and 6-sided skyboxes use _Tint and _Exposure. Procedural uses _SkyTint.")]
    public string skyTintProperty = "_Tint";
    public string skyExposureProperty = "_Exposure";
    [Tooltip("URP Unlit and URP Particles use _BaseColor.")]
    public string mistTintProperty = "_BaseColor";
    public string cloudTintProperty = "_BaseColor";

    [Header("Things that appear at a stage")]
    public StageObject[] stageObjects;

    [Header("Spatial spread")]
    [Tooltip("Where the change starts. Leave empty to use the portal you last used.")]
    public Transform revealOrigin;
    [Tooltip("How fast the change spreads outward, in metres per second. 0 = everything changes together.")]
    public float revealSpeed = 6f;
    [Tooltip("How long each lamp or patch of mist takes to finish changing once the wave reaches it.")]
    public float elementFade = 1.5f;

    [Header("Timing")]
    [Tooltip("Wait after arriving so the fade-in and the portal closing finish first.")]
    public float startDelay = 1.5f;
    [Tooltip("Fog, sky and sun take this long. The spread above can take longer on a big campus.")]
    public float transitionDuration = 4f;

    [Header("Testing")]
    [Tooltip("-1 = real progress. 0 to 3 = force that stage (Play mode only).")]
    public int debugStage = -1;

    Material _sky;
    Material _cloudMat;
    float _cloudSpeed;
    Vector3 _origin;
    float _maxDist;

    float[] _lampLightBase;
    float[] _lampLightDist;
    readonly List<Material> _lampMats = new List<Material>();
    readonly List<float> _lampMatDist = new List<float>();

    Material[] _mistMats;
    float[] _mistBaseRate;
    float[] _mistDist;

    float[] _stageObjDist;

    void Reset()
    {
        stages = new[]
        {
            MakeStage("Original",
                new Color(0.30f, 0.33f, 0.40f), 0.045f, new Color(0.10f, 0.11f, 0.16f),
                new Color(0.55f, 0.62f, 0.85f), 0.25f, new Vector3(55f, -30f, 0f),
                new Color(0.35f, 0.38f, 0.48f), 0.5f,
                new Color(0.45f, 0.50f, 0.60f, 0.30f), 1.00f,
                new Color(0.25f, 0.27f, 0.33f, 0.85f), 0.0015f, 0.25f, 1.0f),
            MakeStage("After memory 1",
                new Color(0.55f, 0.38f, 0.42f), 0.032f, new Color(0.25f, 0.17f, 0.20f),
                new Color(0.95f, 0.55f, 0.40f), 0.6f, new Vector3(25f, -30f, 0f),
                new Color(0.70f, 0.50f, 0.55f), 0.9f,
                new Color(0.70f, 0.50f, 0.50f, 0.25f), 0.80f,
                new Color(0.60f, 0.40f, 0.45f, 0.70f), 0.0020f, 0.6f, 0.6f),
            MakeStage("After memory 2",
                new Color(0.85f, 0.45f, 0.30f), 0.022f, new Color(0.40f, 0.24f, 0.20f),
                new Color(1.00f, 0.50f, 0.25f), 0.9f, new Vector3(12f, -30f, 0f),
                new Color(0.90f, 0.60f, 0.50f), 1.1f,
                new Color(0.95f, 0.55f, 0.40f, 0.20f), 0.55f,
                new Color(0.95f, 0.50f, 0.35f, 0.55f), 0.0025f, 0.85f, 0.25f),
            MakeStage("After memory 3",
                new Color(1.00f, 0.62f, 0.38f), 0.012f, new Color(0.60f, 0.40f, 0.30f),
                new Color(1.00f, 0.65f, 0.35f), 1.2f, new Vector3(6f, -30f, 0f),
                new Color(1.00f, 0.85f, 0.70f), 1.3f,
                new Color(1.00f, 0.70f, 0.50f, 0.15f), 0.30f,
                new Color(1.00f, 0.80f, 0.60f, 0.40f), 0.0030f, 1.0f, 0.0f),
        };
    }

    static EnvironmentStage MakeStage(string label,
        Color fog, float density, Color ambient,
        Color lightColor, float lightIntensity, Vector3 lightRotation,
        Color skyTint, float exposure,
        Color mistColor, float mistAmount,
        Color cloudTint, float cloudSpeed, float lamps, float grade)
    {
        return new EnvironmentStage
        {
            label = label, fogColor = fog, fogDensity = density, ambientColor = ambient,
            lightColor = lightColor, lightIntensity = lightIntensity, lightRotation = lightRotation,
            skyTint = skyTint, skyExposure = exposure,
            mistColor = mistColor, mistAmount = mistAmount,
            cloudTint = cloudTint, cloudSpeed = cloudSpeed,
            lampGlow = lamps, gradeWeight = grade
        };
    }

    void Start()
    {
        if (stages == null || stages.Length == 0)
        {
            Debug.LogWarning("CampusProgress: no stages set up.", this);
            return;
        }

        CacheReferences();

        int last = stages.Length - 1;
        int target = Mathf.Clamp(debugStage >= 0 ? debugStage : GameState.MemoriesCompleted, 0, last);
        int from = Mathf.Clamp(Mathf.Min(GameState.AppliedStage, target), 0, last);

        // Make sure the fog and ambient settings below actually take effect.
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.ambientMode = AmbientMode.Flat;

        // Start on the look the player last saw.
        Apply(stages[from], stages[from], 0f, 0f);
        SetStageObjects(from);

        if (from == target)
        {
            GameState.AppliedStage = target;
            return;
        }

        StartCoroutine(Transition(from, target));
    }

    void Update()
    {
        // Drift the clouds.
        if (_cloudMat != null && _cloudSpeed != 0f)
            _cloudMat.mainTextureOffset += new Vector2(_cloudSpeed * Time.deltaTime, 0f);
    }

    IEnumerator Transition(int from, int to)
    {
        yield return new WaitForSeconds(startDelay);

        var a = stages[from];
        var b = stages[to];

        float wave = revealSpeed > 0f ? _maxDist / revealSpeed + elementFade : 0f;
        float duration = Mathf.Max(transitionDuration, wave);

        for (float e = 0f; e < duration; e += Time.deltaTime)
        {
            float g = Mathf.SmoothStep(0f, 1f, e / Mathf.Max(transitionDuration, 0.01f));
            Apply(a, b, g, e);
            ActivateReachedObjects(from, to, e);
            yield return null;
        }

        Apply(b, b, 0f, 0f);
        SetStageObjects(to);
        DynamicGI.UpdateEnvironment(); // refresh ambient/reflections once at the end
        GameState.AppliedStage = to;
    }

    // ---------------------------------------------------------------- setup

    Vector3 GetOrigin()
    {
        if (revealOrigin != null) return revealOrigin.position;
        if (GameState.HasReturnPoint) return GameState.ReturnPosition;
        var cam = Camera.main;
        return cam != null ? cam.transform.position : Vector3.zero;
    }

    float FlatDistance(Vector3 p)
    {
        return Vector2.Distance(new Vector2(p.x, p.z), new Vector2(_origin.x, _origin.z));
    }

    void CacheReferences()
    {
        _origin = GetOrigin();
        _maxDist = 0f;

        // Work on copies so we never change the skybox/material assets in your project.
        if (RenderSettings.skybox != null)
        {
            _sky = new Material(RenderSettings.skybox);
            RenderSettings.skybox = _sky;
        }

        if (cloudDome != null)
            _cloudMat = cloudDome.material;

        // Lamp lights
        int lightCount = lampLights != null ? lampLights.Length : 0;
        _lampLightBase = new float[lightCount];
        _lampLightDist = new float[lightCount];
        for (int i = 0; i < lightCount; i++)
        {
            if (lampLights[i] == null) continue;
            _lampLightBase[i] = lampLights[i].intensity;
            _lampLightDist[i] = FlatDistance(lampLights[i].transform.position);
            _maxDist = Mathf.Max(_maxDist, _lampLightDist[i]);
        }

        // Lamp glow materials
        if (lampRenderers != null)
        {
            foreach (var r in lampRenderers)
            {
                if (r == null) continue;
                var m = r.material; // makes a per-renderer copy
                m.EnableKeyword("_EMISSION");
                float d = FlatDistance(r.transform.position);
                _lampMats.Add(m);
                _lampMatDist.Add(d);
                _maxDist = Mathf.Max(_maxDist, d);
            }
        }

        // Mist
        int mistCount = mistSystems != null ? mistSystems.Length : 0;
        _mistMats = new Material[mistCount];
        _mistBaseRate = new float[mistCount];
        _mistDist = new float[mistCount];
        for (int i = 0; i < mistCount; i++)
        {
            var ps = mistSystems[i];
            if (ps == null) continue;
            _mistBaseRate[i] = ps.emission.rateOverTimeMultiplier;
            _mistDist[i] = FlatDistance(ps.transform.position);
            _maxDist = Mathf.Max(_maxDist, _mistDist[i]);
            var psr = ps.GetComponent<ParticleSystemRenderer>();
            if (psr != null) _mistMats[i] = psr.material;
        }

        // Stage objects
        int objCount = stageObjects != null ? stageObjects.Length : 0;
        _stageObjDist = new float[objCount];
        for (int i = 0; i < objCount; i++)
        {
            if (stageObjects[i] == null || stageObjects[i].target == null) continue;
            _stageObjDist[i] = FlatDistance(stageObjects[i].target.transform.position);
            _maxDist = Mathf.Max(_maxDist, _stageObjDist[i]);
        }
    }

    // ---------------------------------------------------------------- blending

    // 0 before the wave reaches an element, 1 once it has fully passed.
    float ElementT(float dist, float elapsed, float globalT)
    {
        if (revealSpeed <= 0f) return globalT;
        float t = (elapsed - dist / revealSpeed) / Mathf.Max(elementFade, 0.01f);
        return Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));
    }

    void Apply(EnvironmentStage a, EnvironmentStage b, float g, float elapsed)
    {
        // Global: fog, ambient, sun, sky, grade and clouds change everywhere at once.
        RenderSettings.fogColor = Color.Lerp(a.fogColor, b.fogColor, g);
        RenderSettings.fogDensity = Mathf.Lerp(a.fogDensity, b.fogDensity, g);
        RenderSettings.ambientLight = Color.Lerp(a.ambientColor, b.ambientColor, g);

        if (mainLight != null)
        {
            mainLight.color = Color.Lerp(a.lightColor, b.lightColor, g);
            mainLight.intensity = Mathf.Lerp(a.lightIntensity, b.lightIntensity, g);
            mainLight.transform.rotation = Quaternion.Slerp(
                Quaternion.Euler(a.lightRotation), Quaternion.Euler(b.lightRotation), g);
        }

        if (_sky != null)
        {
            if (_sky.HasProperty(skyTintProperty))
                _sky.SetColor(skyTintProperty, Color.Lerp(a.skyTint, b.skyTint, g));
            if (_sky.HasProperty(skyExposureProperty))
                _sky.SetFloat(skyExposureProperty, Mathf.Lerp(a.skyExposure, b.skyExposure, g));
        }

        if (gradeVolume != null)
            gradeVolume.weight = Mathf.Lerp(a.gradeWeight, b.gradeWeight, g);

        if (_cloudMat != null && _cloudMat.HasProperty(cloudTintProperty))
            _cloudMat.SetColor(cloudTintProperty, Color.Lerp(a.cloudTint, b.cloudTint, g));
        _cloudSpeed = Mathf.Lerp(a.cloudSpeed, b.cloudSpeed, g);

        // Spreading outward from the portal: lamps and mist.
        for (int i = 0; i < _lampLightDist.Length; i++)
        {
            if (lampLights[i] == null) continue;
            float t = ElementT(_lampLightDist[i], elapsed, g);
            lampLights[i].intensity = _lampLightBase[i] * Mathf.Lerp(a.lampGlow, b.lampGlow, t);
        }

        for (int i = 0; i < _lampMats.Count; i++)
        {
            float t = ElementT(_lampMatDist[i], elapsed, g);
            _lampMats[i].SetColor(emissionProperty, lampEmissionColor * Mathf.Lerp(a.lampGlow, b.lampGlow, t));
        }

        for (int i = 0; i < _mistBaseRate.Length; i++)
        {
            var ps = mistSystems[i];
            if (ps == null) continue;
            float t = ElementT(_mistDist[i], elapsed, g);

            var emission = ps.emission;
            emission.rateOverTimeMultiplier = _mistBaseRate[i] * Mathf.Lerp(a.mistAmount, b.mistAmount, t);

            var m = _mistMats[i];
            if (m != null && m.HasProperty(mistTintProperty))
                m.SetColor(mistTintProperty, Color.Lerp(a.mistColor, b.mistColor, t));
        }
    }

    // ---------------------------------------------------------------- stage objects

    void SetStageObjects(int stage)
    {
        if (stageObjects == null) return;
        foreach (var so in stageObjects)
            if (so != null && so.target != null)
                so.target.SetActive(stage >= so.enableAtStage);
    }

    // Switch objects on as the wave reaches them.
    void ActivateReachedObjects(int from, int to, float elapsed)
    {
        if (stageObjects == null) return;
        for (int i = 0; i < stageObjects.Length; i++)
        {
            var so = stageObjects[i];
            if (so == null || so.target == null) continue;
            if (so.enableAtStage <= from || so.enableAtStage > to) continue;
            if (so.target.activeSelf) continue;

            float when = revealSpeed > 0f ? _stageObjDist[i] / revealSpeed : 0f;
            if (elapsed >= when) so.target.SetActive(true);
        }
    }
}
