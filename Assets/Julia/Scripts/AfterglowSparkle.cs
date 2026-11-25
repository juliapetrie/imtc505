using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// The title logo's twinkling glints, carried into the world. Add this to any campus object: when the
/// player points at it (hover) or grabs/presses it (select), four-point stars twinkle over its surface,
/// layered on top of the object without touching its materials. Afterwards the object can keep a faint
/// "afterglow" so the places you've touched stay lit.
///
/// Every visible part of the object (each child MeshRenderer) gets its own emitter that hugs that part,
/// so a bench sparkles along its seat, back and legs rather than in a box around it.
///
/// Needs a collider for the XR rays/hands to hit. If the object has no interactable, an
/// XRSimpleInteractable (and a BoxCollider, if needed) is added automatically.
/// Other scripts can call <see cref="Ignite"/> directly, or <see cref="IgniteOn"/> for any GameObject.
///
/// The effect stays switched off until the player has completed <see cref="requiredMemories"/> memories
/// (see GameState). AfterglowSparkleGroup can add this to many props at once and copy a template's settings.
/// </summary>
[DisallowMultipleComponent]
public sealed class AfterglowSparkle : MonoBehaviour
{
    // TouchOnly is last so scenes saved with the older options keep their setting.
    public enum Trigger { HoverOrSelect, SelectOnly, ScriptOnly, TouchOnly }

    [Header("Unlock")]
    [Tooltip("How many memories the player must finish before this can sparkle. 0 = always on.")]
    [SerializeField, Min(0)] private int requiredMemories = 3;
    [Tooltip("For testing: sparkle regardless of how many memories are finished.")]
    [SerializeField] private bool ignoreRequirement = false;

    [Header("When it sparkles")]
    [Tooltip("Touch Only: a hand has to reach the object (pointing a ray from afar does nothing).\n" +
             "Hover Or Select: pointing at it or touching it.\nSelect Only: grabbing it.\n" +
             "Script Only: only when code calls Ignite().")]
    [SerializeField] private Trigger trigger = Trigger.TouchOnly;
    [Tooltip("Touch Only: how close (metres) the hand must get to the object's colliders.")]
    [SerializeField, Min(0f)] private float touchDistance = 0.1f;
    [Tooltip("Keep a faint twinkle forever after the first touch.")]
    [SerializeField] private bool linger = true;
    [Tooltip("Seconds of full sparkle after the player lets go / looks away.")]
    [SerializeField, Min(0f)] private float glowSeconds = 1.5f;
    [Tooltip("Seconds to ease from full sparkle down to the lingering (or zero) rate.")]
    [SerializeField, Min(0.01f)] private float fadeSeconds = 2.5f;

    [Header("How it looks (0 = sized automatically from the object)")]
    [SerializeField, Min(0f)] private float starsPerSecond = 0f;
    [SerializeField, Range(0f, 1f)] private float lingerFraction = 0.12f;
    [SerializeField, Min(0f)] private float starSize = 0f;
    [SerializeField, Min(0)] private int burstOnTouch = -1;
    [SerializeField] private Color coreColor = new(1f, 0.91f, 0.75f);
    [SerializeField] private Color tipColor = new(1f, 0.76f, 0.51f);
    [Tooltip("Optional. Leave empty to use a generated additive particle material. " +
             "Assign one if sparkles render pink in a headset build (shader stripped).")]
    [SerializeField] private Material materialOverride;

    [Header("Performance")]
    [Tooltip("Most parts that get their own sparkles. The largest parts go first; the rest are skipped.")]
    [SerializeField, Min(1)] private int maxEmitters = 24;
    [Tooltip("Skip parts with less surface area than this (square metres). 0 = keep every part. " +
             "The largest part is always kept.")]
    [SerializeField, Min(0f)] private float minPartArea = 0f;

    private static Material sharedMaterial;
    private static Texture2D starTexture;

    private struct Emitter
    {
        public ParticleSystem system;
        public float share;         // fraction of the object's sparkles this part gets
    }

    private readonly List<Emitter> emitters = new();
    private bool awoken;            // Awake has run (settings copied later need a rebuild)
    private bool initialized;       // unlocked, built, and hooked up to an interactable
    private bool built;
    private XRBaseInteractable interactable;
    private float fullRate;
    private float level;            // 0..1 current emission strength
    private float holdUntil;        // keep full strength until this time
    private int activeHolds;        // hovers/selects currently on us
    private bool touched;
    private bool handTouching;      // TouchOnly: a hand is within touchDistance right now

    private bool Unlocked => ignoreRequirement || GameState.MemoriesCompleted >= requiredMemories;

    /// <summary>Sparkle any object, adding the component if it isn't there yet.</summary>
    public static AfterglowSparkle IgniteOn(GameObject target)
    {
        var sparkle = target.GetComponent<AfterglowSparkle>();
        if (sparkle == null) sparkle = target.AddComponent<AfterglowSparkle>();
        sparkle.Ignite();
        return sparkle;
    }

    /// <summary>One burst of glitter followed by the glow/fade.</summary>
    [ContextMenu("Ignite (preview)")]
    public void Ignite()
    {
        if (!Unlocked) return;
        Build();
        touched = true;
        level = 1f;
        holdUntil = Time.time + glowSeconds;
        int burst = burstOnTouch >= 0 ? burstOnTouch : Mathf.CeilToInt(fullRate * 0.6f);
        foreach (var emitter in emitters)
            emitter.system.Emit(Mathf.CeilToInt(burst * emitter.share));
    }

    /// <summary>
    /// Copies another sparkle's settings onto this one. Used by AfterglowSparkleGroup so every prop
    /// can share the settings of a template that was edited in the Editor.
    /// </summary>
    public void ApplySettingsFrom(AfterglowSparkle template)
    {
        if (template == null || template == this) return;
        JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(template), this);

        if (!awoken) return;                                // Awake hasn't run yet; it will use the new settings
        if (initialized) { Rebuild(); return; }             // already set up: rebuild with the new look
        if (Unlocked) { Initialize(); enabled = true; }     // locked itself with the old settings: reopen
    }

    private void Awake()
    {
        awoken = true;
        Initialize();
    }

    // Does the one-time setup. Safe to call again: it does nothing once set up, and nothing while locked.
    private void Initialize()
    {
        if (initialized) return;
        if (!Unlocked) { enabled = false; return; }         // locked: no particles, no interactable
        initialized = true;

        Build();
        if (trigger == Trigger.ScriptOnly) return;

        interactable = GetComponentInParent<XRBaseInteractable>();
        if (interactable == null)
        {
            if (GetComponentInChildren<Collider>() == null) FitBoxCollider();
            interactable = gameObject.AddComponent<XRSimpleInteractable>();
        }
    }

    // Throws away the sparkles and builds them again with the current settings.
    private void Rebuild()
    {
        foreach (var emitter in emitters)
            if (emitter.system != null) Destroy(emitter.system.gameObject);
        emitters.Clear();
        built = false;
        level = 0f;
        Build();

        // The trigger setting may have changed, so register the event listeners again.
        OnDisable();
        if (isActiveAndEnabled && trigger != Trigger.ScriptOnly) OnEnable();
    }

    private void OnEnable()
    {
        // TouchOnly doesn't listen to events: it checks hand distance each frame (see UpdateTouch).
        if (interactable == null || trigger == Trigger.TouchOnly) return;
        if (trigger == Trigger.HoverOrSelect)
        {
            interactable.hoverEntered.AddListener(OnHoverEntered);
            interactable.hoverExited.AddListener(OnHoverExited);
        }
        interactable.selectEntered.AddListener(OnSelectEntered);
        interactable.selectExited.AddListener(OnSelectExited);
    }

    private void OnDisable()
    {
        if (interactable == null) return;
        interactable.hoverEntered.RemoveListener(OnHoverEntered);
        interactable.hoverExited.RemoveListener(OnHoverExited);
        interactable.selectEntered.RemoveListener(OnSelectEntered);
        interactable.selectExited.RemoveListener(OnSelectExited);
        activeHolds = 0;
        handTouching = false;
    }

    private void OnHoverEntered(HoverEnterEventArgs _) => Hold();
    private void OnHoverExited(HoverExitEventArgs _) => Release();
    private void OnSelectEntered(SelectEnterEventArgs _) => Hold();
    private void OnSelectExited(SelectExitEventArgs _) => Release();

    private void Hold()
    {
        activeHolds++;
        Ignite();
    }

    private void Release()
    {
        activeHolds = Mathf.Max(0, activeHolds - 1);
        holdUntil = Time.time + glowSeconds;
    }

    // The toolkit reports a far ray and a hand in contact the same way (both "hover"), so for TouchOnly
    // we look at every interactor currently hovering or grabbing and only count ones physically close.
    private void UpdateTouch()
    {
        if (trigger != Trigger.TouchOnly || interactable == null) return;
        bool touching = HandWithinReach(interactable.interactorsHovering)
                        || HandWithinReach(interactable.interactorsSelecting);
        if (touching && !handTouching) Hold();
        else if (!touching && handTouching) Release();
        handTouching = touching;
    }

    private bool HandWithinReach<T>(List<T> interactors) where T : UnityEngine.XR.Interaction.Toolkit.Interactors.IXRInteractor
    {
        foreach (var interactor in interactors)
        {
            Vector3 hand = interactor.transform.position;
            foreach (var collider in interactable.colliders)
            {
                if (collider == null || !collider.enabled) continue;
                // ClosestPoint isn't supported on non-convex mesh colliders; use their bounds instead.
                Vector3 nearest = collider is MeshCollider { convex: false }
                    ? collider.bounds.ClosestPoint(hand)
                    : collider.ClosestPoint(hand);
                if ((nearest - hand).sqrMagnitude <= touchDistance * touchDistance) return true;
            }
        }
        return false;
    }

    private void Update()
    {
        UpdateTouch();
        if (emitters.Count == 0) return;
        float rest = touched && linger ? lingerFraction : 0f;
        float target = activeHolds > 0 || Time.time < holdUntil ? 1f : rest;
        level = Mathf.MoveTowards(level, target, Time.deltaTime / fadeSeconds);
        foreach (var emitter in emitters)
        {
            var emission = emitter.system.emission;
            emission.rateOverTime = fullRate * level * emitter.share;
        }
    }

    // ---------------------------------------------------------------- setup

    private void Build()
    {
        if (built) return;
        built = true;

        var parts = new List<Renderer>();
        foreach (var r in GetComponentsInChildren<Renderer>())
            if (r is MeshRenderer || r is SkinnedMeshRenderer) parts.Add(r);

        Bounds bounds = WorldBounds(parts);
        float reach = Mathf.Max(bounds.extents.magnitude, 0.05f);
        float size = starSize > 0f ? starSize : Mathf.Clamp(reach * 0.06f, 0.012f, 0.09f);

        // Largest parts first; area decides each part's share of the sparkles.
        parts.Sort((a, b) => SurfaceArea(b.bounds).CompareTo(SurfaceArea(a.bounds)));

        // Drop tiny parts (the largest is always kept), then cap how many parts get an emitter.
        if (minPartArea > 0f && parts.Count > 1)
        {
            var largest = parts[0];
            parts.RemoveAll(p => p != largest && SurfaceArea(p.bounds) < minPartArea);
        }
        if (parts.Count > maxEmitters) parts.RemoveRange(maxEmitters, parts.Count - maxEmitters);

        float totalArea = 0f;
        foreach (var part in parts) totalArea += SurfaceArea(part.bounds);

        fullRate = starsPerSecond > 0f ? starsPerSecond : Mathf.Clamp(totalArea * 40f, 8f, 160f);

        if (parts.Count == 0)
        {
            // Nothing visible to hug: sparkle around the object's position.
            var system = CreateSystem(transform, size, 1f);
            var shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.25f;
            emitters.Add(new Emitter { system = system, share = 1f });
            system.Play();
            return;
        }

        foreach (var part in parts)
        {
            float share = totalArea > 0f ? SurfaceArea(part.bounds) / totalArea : 1f / parts.Count;
            var system = CreateSystem(part.transform, size, share);
            ConfigureShape(system, part);
            emitters.Add(new Emitter { system = system, share = share });
            system.Play();
        }
    }

    private ParticleSystem CreateSystem(Transform parent, float size, float share)
    {
        var host = new GameObject("Afterglow sparkles");
        host.transform.SetParent(parent, false);
        var system = host.AddComponent<ParticleSystem>();
        system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = system.main;
        main.loop = true;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Shape;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.4f, 2.9f); // same periods as the logo
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(size * 0.45f, size);
        main.startColor = new ParticleSystem.MinMaxGradient(coreColor, tipColor);
        main.maxParticles = Mathf.CeilToInt(fullRate * share * 3.5f) + 16;

        var emission = system.emission;
        emission.rateOverTime = 0f;

        // Gentle upward drift, like the logo's glints rising as they fade.
        var velocity = system.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.x = new ParticleSystem.MinMaxCurve(0f, 0f);
        velocity.y = new ParticleSystem.MinMaxCurve(size * 0.25f, size * 0.6f);
        velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);

        // Twinkle: fade in and out, swelling as it peaks (sin² pulse).
        var color = system.colorOverLifetime;
        color.enabled = true;
        var pulse = new Gradient();
        pulse.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.5f, 0.2f), new GradientAlphaKey(1f, 0.5f),
                    new GradientAlphaKey(0.5f, 0.8f), new GradientAlphaKey(0f, 1f) });
        color.color = pulse;

        var sizeOverLife = system.sizeOverLifetime;
        sizeOverLife.enabled = true;
        sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f,
            new AnimationCurve(new Keyframe(0f, 0.55f), new Keyframe(0.5f, 1f), new Keyframe(1f, 0.55f)));

        var render = host.GetComponent<ParticleSystemRenderer>();
        render.renderMode = ParticleSystemRenderMode.Billboard;
        render.sharedMaterial = materialOverride != null ? materialOverride : SharedMaterial();
        render.shadowCastingMode = ShadowCastingMode.Off;
        render.receiveShadows = false;
        render.minParticleSize = 0f;
        return system;
    }

    private static void ConfigureShape(ParticleSystem system, Renderer part)
    {
        var shape = system.shape;
        shape.enabled = true;
        // Otherwise Unity tints each star by the object's material colour (dark objects = dark stars).
        shape.useMeshColors = false;
        // Statically batched renderers share one combined mesh, so they can't be emitted from directly.
        bool batched = part.isPartOfStaticBatch || part.gameObject.isStatic;

        // Best: emit from the real surface (needs a readable mesh).
        if (!batched && part is MeshRenderer meshRenderer
            && meshRenderer.TryGetComponent(out MeshFilter filter)
            && filter.sharedMesh != null && filter.sharedMesh.isReadable)
        {
            shape.shapeType = ParticleSystemShapeType.MeshRenderer;
            shape.meshRenderer = meshRenderer;
            shape.meshShapeType = ParticleSystemMeshShapeType.Triangle;
            shape.normalOffset = 0.005f;
            return;
        }
        if (!batched && part is SkinnedMeshRenderer skinned
            && skinned.sharedMesh != null && skinned.sharedMesh.isReadable)
        {
            shape.shapeType = ParticleSystemShapeType.SkinnedMeshRenderer;
            shape.skinnedMeshRenderer = skinned;
            shape.meshShapeType = ParticleSystemMeshShapeType.Triangle;
            shape.normalOffset = 0.005f;
            return;
        }

        // Otherwise: the faces of this part's own box, turned and scaled with the part,
        // so it still sits snugly on each piece rather than around the whole object.
        shape.shapeType = ParticleSystemShapeType.BoxShell;
        var host = system.transform;
        if (!batched)
        {
            Bounds local = part.localBounds;
            host.localPosition = local.center;
            host.localRotation = Quaternion.identity;
            shape.scale = local.size * 1.01f;
        }
        else
        {
            Bounds world = part.bounds;
            host.position = world.center;
            host.rotation = Quaternion.identity;
            Vector3 s = host.lossyScale;
            shape.scale = new Vector3(world.size.x / Mathf.Max(Mathf.Abs(s.x), 1e-4f),
                                      world.size.y / Mathf.Max(Mathf.Abs(s.y), 1e-4f),
                                      world.size.z / Mathf.Max(Mathf.Abs(s.z), 1e-4f)) * 1.01f;
        }
    }

    private static float SurfaceArea(Bounds b) =>
        2f * (b.size.x * b.size.y + b.size.y * b.size.z + b.size.z * b.size.x);

    private Bounds WorldBounds(List<Renderer> renderers)
    {
        var bounds = new Bounds(transform.position, Vector3.one * 0.2f);
        for (int i = 0; i < renderers.Count; i++)
        {
            if (i == 0) bounds = renderers[i].bounds;
            else bounds.Encapsulate(renderers[i].bounds);
        }
        return bounds;
    }

    private void FitBoxCollider()
    {
        var parts = new List<Renderer>();
        foreach (var r in GetComponentsInChildren<Renderer>())
            if (r is not ParticleSystemRenderer) parts.Add(r);
        Bounds world = WorldBounds(parts);
        var box = gameObject.AddComponent<BoxCollider>();
        box.center = transform.InverseTransformPoint(world.center);
        Vector3 s = transform.lossyScale;
        box.size = new Vector3(world.size.x / Mathf.Max(Mathf.Abs(s.x), 1e-4f),
                               world.size.y / Mathf.Max(Mathf.Abs(s.y), 1e-4f),
                               world.size.z / Mathf.Max(Mathf.Abs(s.z), 1e-4f));
    }

    // ---------------------------------------------------------------- look

    private static Material SharedMaterial()
    {
        if (sharedMaterial == null) sharedMaterial = CreateMaterial(StarTexture());
        return sharedMaterial;
    }

    private static Texture2D StarTexture()
    {
        if (starTexture != null) return starTexture;
        starTexture = CreateStarTexture();
        starTexture.Apply(true, true); // upload and free the CPU copy
        return starTexture;
    }

    /// <summary>Transparent, additive, unlit particle material: glints add light on top of what's behind.
    /// Used at runtime, and by the editor tool that saves it as a material asset for builds.</summary>
    public static Material CreateMaterial(Texture texture)
    {
        var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        bool urp = shader != null;
        if (!urp) shader = Shader.Find("Sprites/Default");

        var material = new Material(shader) { name = "Afterglow Sparkle" };
        material.mainTexture = texture;
        if (urp)
        {
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 2f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.One);
            material.SetFloat("_ZWrite", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.SetColor("_BaseColor", Color.white);
            material.renderQueue = (int)RenderQueue.Transparent;
        }
        return material;
    }

    /// <summary>The logo's four-point glint (slightly narrower than tall): a crisp star with a small,
    /// tight glow. Returned readable (pixels kept) so the editor tool can save it as a PNG.</summary>
    public static Texture2D CreateStarTexture()
    {
        const int n = 128;
        var texture = new Texture2D(n, n, TextureFormat.RGBA32, true)
        {
            name = "Afterglow Star",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Trilinear,
            anisoLevel = 4,
            mipMapBias = -0.6f, // stay sharp when the star is only a few pixels wide
        };
        var pixels = new Color[n * n];
        float edge = 2.5f / n; // ~1 texel of anti-aliasing
        for (int y = 0; y < n; y++)
        for (int x = 0; x < n; x++)
        {
            float u = (x + 0.5f) / n * 2f - 1f;
            float v = (y + 0.5f) / n * 2f - 1f;
            float ax = Mathf.Abs(u) / 0.78f, ay = Mathf.Abs(v);
            // Concave diamond: points along the axes, pinched in between.
            float star = 1f - Mathf.Pow(Mathf.Pow(ax, 0.5f) + Mathf.Pow(ay, 0.5f), 2f);
            float body = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(star / edge + 0.5f));
            float glow = Mathf.Exp(-(u * u + v * v) * 16f) * 0.22f;
            pixels[y * n + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(body + glow));
        }
        texture.SetPixels(pixels);
        texture.Apply(true, false);
        return texture;
    }
}