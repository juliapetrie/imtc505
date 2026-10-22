using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Controls when a portal appears. Put this on a CONTROLLER object (not on the
/// portal itself, since the portal gets switched off) and drag the portal into
/// the "portal" slot.
///
/// Memory scene: portal starts hidden. Call Reveal() when the player is ready
/// to return (e.g. from the handshake finishing), or set revealAfterSeconds.
/// Campus scene: leave startHidden off so the portal is always there, and
/// optionally tick hideIfMemoryCompleted.
/// </summary>

public class PortalReveal : MonoBehaviour
{
    public GameObject portal;

    [Header("Visibility")] public bool startHidden = true;

    [Tooltip("Automatically reveal after this many seconds. -1 = only reveal when Reveal() is called.")]
    public float revealAfterSeconds = -1f;

    [Header("Reveal animation")] public float growDuration = 1.5f;
    public AudioSource revealSound;

    [Header("Placement (optional)")]
    [Tooltip("Spawn the portal in front of the player so they don't have to look for it.")]
    public bool placeInFrontOfPlayer = false;

    public float distanceFromPlayer = 1.5f;

    [Header("Campus use (optional)")] [Tooltip("Hide this portal once its memory has been finished.")]
    public bool hideIfMemoryCompleted = false;

    public string memoryId;

    [Header("Events")] public UnityEvent onRevealed;

    Vector3 _fullScale;
    bool _revealed;

    void Awake()
    {
        if (portal == null)
        {
            Debug.LogError("PortalReveal: assign the portal object.", this);
            return;
        }

        _fullScale = portal.transform.localScale;

        if (hideIfMemoryCompleted && GameState.IsCompleted(memoryId))
        {
            _revealed = true; // locks it so Reveal() can't bring it back
            portal.SetActive(false);
            return;
        }

        if (startHidden) portal.SetActive(false);
    }

    void Start()
    {
        if (startHidden && revealAfterSeconds >= 0f)
            Invoke(nameof(Reveal), revealAfterSeconds);
    }

    /// <summary>Hook this to the handshake finishing (button, UnityEvent, or code).</summary>
    public void Reveal()
    {
        if (_revealed || portal == null) return;
        _revealed = true;
        StartCoroutine(GrowIn());
    }

    IEnumerator GrowIn()
    {
        if (placeInFrontOfPlayer) PlaceInFrontOfPlayer();

        // Keep the trigger off until the portal has finished appearing.
        var colliders = portal.GetComponentsInChildren<Collider>(true);
        foreach (var c in colliders) c.enabled = false;

        portal.transform.localScale = Vector3.zero;
        portal.SetActive(true);
        if (revealSound != null) revealSound.Play();

        for (float t = 0f; t < 1f; t += Time.deltaTime / Mathf.Max(growDuration, 0.01f))
        {
            portal.transform.localScale = _fullScale * Mathf.SmoothStep(0f, 1f, t);
            yield return null;
        }

        portal.transform.localScale = _fullScale;
        foreach (var c in colliders) c.enabled = true;
        onRevealed?.Invoke();
    }

    void PlaceInFrontOfPlayer()
    {
        var cam = Camera.main;
        if (cam == null) return;

        Vector3 forward = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up).normalized;
        if (forward == Vector3.zero) forward = Vector3.forward;

        Vector3 pos = cam.transform.position + forward * distanceFromPlayer;
        pos.y = portal.transform.position.y; // keep the portal's own height
        portal.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(-forward));
    }
}
