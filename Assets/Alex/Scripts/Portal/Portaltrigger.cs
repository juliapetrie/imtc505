using UnityEngine;

/// <summary>
/// Put on the portal object (needs a Collider; "Is Trigger" should be ticked).
/// Use the same script for BOTH directions:
///  - Campus portal: set sceneToLoad = memory scene, memoryId = e.g. "Memory_Maya",
///    tick saveReturnPoint and assign returnPoint (a child of the portal, in front of it).
///  - Memory exit portal: set sceneToLoad = campus scene, tick completesMemory.
///
/// The portal fires in two ways, whichever happens first:
///  1. Physics: a collider tagged Player (or whose root is tagged Player) touches the trigger.
///  2. Position: the player's tracked position (head/camera, or the point you assign) is inside the
///     collider. This works with the XR Device Simulator, avatar rigs, and rigs with no Rigidbody.
/// </summary>
[RequireComponent(typeof(Collider))]
public class PortalTrigger : MonoBehaviour
{
    [Header("Where to go")]
    public string sceneToLoad;
    public float fadeDuration = 1.5f;

    [Header("Leaving the campus")]
    public bool saveReturnPoint = true;
    [Tooltip("Empty object placed a little in front of the portal, so you don't land inside it.")]
    public Transform returnPoint;

    [Header("Leaving a memory")]
    public bool completesMemory = false;
    public string memoryId;

    [Header("Detection")]
    [Tooltip("Also trigger when the player's tracked position is inside this portal's collider.")]
    public bool detectByPosition = true;
    [Tooltip("Optional. The object whose position counts as 'the player' (the avatar's head or the camera). " +
             "Leave empty to use the MainCamera, or failing that the object tagged Player.")]
    public Transform detectionPoint;
    [Tooltip("Writes what touches the portal to the Console. Turn on while debugging.")]
    public bool logDebug = false;

    [Header("Feedback (optional)")]
    public AudioSource enterSound;

    bool _used;
    Collider[] _colliders;
    Transform _fallbackPoint;
    float _nextLookup;

    void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    void Awake()
    {
        _colliders = GetComponents<Collider>();

        if (string.IsNullOrEmpty(sceneToLoad))
            Debug.LogError("PortalTrigger: 'Scene To Load' is empty, so this portal can't go anywhere.", this);

        foreach (var c in _colliders)
            if (!c.isTrigger)
                Debug.LogWarning("PortalTrigger: a collider on this portal doesn't have 'Is Trigger' ticked, " +
                                 "so the player will bump into it instead of walking through.", this);
    }

    void Update()
    {
        if (_used || !detectByPosition) return;

        Transform point = GetDetectionPoint();
        if (point == null) return;

        Vector3 position = point.position;
        foreach (var c in _colliders)
        {
            if (c == null || !c.enabled) continue;
            if (c is MeshCollider { convex: false }) continue; // ClosestPoint isn't supported on these

            if ((c.ClosestPoint(position) - position).sqrMagnitude < 0.000001f)
            {
                Enter("position check");
                return;
            }
        }
    }

    Transform GetDetectionPoint()
    {
        if (detectionPoint != null) return detectionPoint;
        if (_fallbackPoint != null && _fallbackPoint.gameObject.activeInHierarchy) return _fallbackPoint;

        // Look again a couple of times a second, not every frame.
        if (Time.unscaledTime < _nextLookup) return null;
        _nextLookup = Time.unscaledTime + 0.5f;

        var cam = Camera.main;
        if (cam != null)
        {
            _fallbackPoint = cam.transform;
        }
        else
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) _fallbackPoint = player.transform;
        }

        return _fallbackPoint;
    }

    void OnTriggerEnter(Collider other)
    {
        if (logDebug)
            Debug.Log($"PortalTrigger touched by '{other.name}' (root '{other.transform.root.name}', tag '{other.tag}').", this);

        if (_used) return;
        if (!other.CompareTag("Player") && !other.transform.root.CompareTag("Player")) return;

        Enter("physics trigger");
    }

    void Enter(string how)
    {
        if (_used || string.IsNullOrEmpty(sceneToLoad)) return;
        _used = true;

        if (logDebug) Debug.Log($"PortalTrigger: entered by {how}, loading '{sceneToLoad}'.", this);

        if (saveReturnPoint && returnPoint != null)
            GameState.SetReturnPoint(returnPoint.position, returnPoint.rotation);
        else if (saveReturnPoint)
            Debug.LogWarning("PortalTrigger: no Return Point assigned, so the player won't come back beside this portal.", this);

        if (completesMemory)
            GameState.CompleteMemory(memoryId);

        if (enterSound != null) enterSound.Play();

        if (ScreenFader.Instance == null)
        {
            Debug.LogError("No ScreenFader in the scene. Add one to your first scene.");
            UnityEngine.SceneManagement.SceneManager.LoadScene(sceneToLoad);
            return;
        }

        ScreenFader.Instance.LoadScene(sceneToLoad, fadeDuration);
    }
}