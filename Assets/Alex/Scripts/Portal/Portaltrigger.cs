using UnityEngine;

/// <summary>
/// Put on the portal object (needs a Collider with "Is Trigger" ticked).
/// Use the same script for BOTH directions:
///  - Campus portal: set sceneToLoad = memory scene, memoryId = e.g. "Memory_Maya",
///    tick saveReturnPoint and assign returnPoint.
///  - Memory exit portal: set sceneToLoad = campus scene, tick completesMemory.
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
    [Range(0f, 1f)] public float lightGain = 0.2f;

    [Header("Feedback (optional)")]
    public AudioSource enterSound;

    bool _used;

    void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    void OnTriggerEnter(Collider other)
    {
        if (_used) return;
        if (!other.CompareTag("Player") && !other.transform.root.CompareTag("Player")) return;

        _used = true;

        if (saveReturnPoint && returnPoint != null)
            GameState.SetReturnPoint(returnPoint.position, returnPoint.rotation);

        if (completesMemory)
            GameState.CompleteMemory(memoryId, lightGain);

        if (enterSound != null) enterSound.Play();

        if (ScreenFader.Instance == null)
        {
            Debug.LogError("No ScreenFader in the scene. Add one to your first scene.");
            UnityEngine.SceneManagement.SceneManager.LoadScene(sceneToLoad);
            return;
        }

        StartCoroutine(ScreenFader.Instance.LoadSceneWithFade(sceneToLoad, fadeDuration));
    }
}