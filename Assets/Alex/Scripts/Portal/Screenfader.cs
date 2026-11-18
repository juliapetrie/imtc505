using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// VR-safe screen fade. Builds a quad that sits just in front of the camera
/// (screen-space overlays don't work in VR) and survives scene loads.
/// Put ONE of these in your first scene (e.g. the campus).
/// </summary>
public class ScreenFader : MonoBehaviour
{
    public static ScreenFader Instance { get; private set; }

    [Tooltip("White feels like light/memory; black feels like a normal cut.")]
    public Color fadeColor = Color.white;
    public float defaultDuration = 1.5f;

    [Tooltip("Optional. Leave empty to use the built-in Sprites/Default shader.")]
    public Material fadeMaterial;

    Material _mat;
    Renderer _renderer;
    Transform _quad;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        BuildQuad();
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDestroy()
    {
        if (Instance == this) SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void BuildQuad()
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = "FadeQuad";
        Destroy(go.GetComponent<Collider>());
        _quad = go.transform;
        _quad.SetParent(transform, false);

        _renderer = go.GetComponent<Renderer>();
        _renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _renderer.receiveShadows = false;

        _mat = fadeMaterial != null ? new Material(fadeMaterial) : new Material(Shader.Find("Sprites/Default"));
        _mat.renderQueue = 5000; // draw on top of everything
        _renderer.material = _mat;

        SetAlpha(0f);
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        AttachToCamera();
    }

    void AttachToCamera()
    {
        var cam = Camera.main; // your XR camera needs the MainCamera tag
        if (cam == null) return;
        _quad.SetParent(cam.transform, false);
        _quad.localPosition = new Vector3(0f, 0f, 0.5f);
        _quad.localRotation = Quaternion.identity;
        _quad.localScale = new Vector3(4f, 4f, 1f);
    }

    // Park the quad on this object so it isn't destroyed with the old camera.
    void DetachFromCamera()
    {
        _quad.SetParent(transform, true);
    }

    void SetAlpha(float a)
    {
        var c = fadeColor;
        c.a = a;
        _mat.color = c;
        _renderer.enabled = a > 0.001f;
    }

    public IEnumerator FadeTo(float targetAlpha, float duration = -1f)
    {
        if (duration < 0f) duration = defaultDuration;
        AttachToCamera();
        float start = _mat.color.a;
        for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / Mathf.Max(duration, 0.01f))
        {
            SetAlpha(Mathf.Lerp(start, targetAlpha, t));
            yield return null;
        }
        SetAlpha(targetAlpha);
    }

    /// <summary>
    /// Call this from other scripts. The coroutine runs on the fader, which
    /// survives scene loads (a coroutine on the portal would die with its scene).
    /// </summary>
    public void LoadScene(string sceneName, float duration = -1f)
    {
        StartCoroutine(LoadSceneWithFade(sceneName, duration));
    }

    /// <summary>Fade out, load the scene, fade back in.</summary>
    public IEnumerator LoadSceneWithFade(string sceneName, float duration = -1f)
    {
        yield return FadeTo(1f, duration);

        DetachFromCamera();
        var op = SceneManager.LoadSceneAsync(sceneName);
        if (op == null)
        {
            // Scene name is wrong or the scene isn't in Build Settings.
            Debug.LogError($"ScreenFader: couldn't load scene '{sceneName}'. Is it in Build Settings and spelled correctly?");
            AttachToCamera();
            yield return FadeTo(0f, duration);
            yield break;
        }
        while (!op.isDone) yield return null;

        // sceneLoaded re-attached the quad to the new camera; it's still fully opaque here.
        yield return null;
        yield return FadeTo(0f, duration);
    }
}