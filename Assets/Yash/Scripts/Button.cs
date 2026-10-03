using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables; // XRI 3.x. For XRI 2.x, delete this line.

/// <summary>
/// A 3D VR button. Works with ray, poke, or direct interactors.
/// Add this to a GameObject with a Collider. It adds an XRSimpleInteractable
/// automatically. When the button is pressed (selected), the prefab spawns.
/// </summary>
[RequireComponent(typeof(XRSimpleInteractable))]
public class VRSpawnButton : MonoBehaviour
{
    [Header("Prefab Settings")]
    [SerializeField] private GameObject prefabToSpawn;
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private Transform parent;

    [Header("Behaviour")]
    [SerializeField] private bool spawnOnlyOnce = false;
    [SerializeField] private bool destroyPreviousInstance = false;
    [SerializeField] private float cooldown = 0.5f;

    [Header("Feedback")]
    [SerializeField] private Transform visualToPress;      // Optional: mesh that dips when pressed
    [SerializeField] private float pressDepth = 0.01f;
    [SerializeField] private AudioSource clickSound;       // Optional
    [SerializeField] private float hapticAmplitude = 0.5f;
    [SerializeField] private float hapticDuration = 0.1f;

    private XRSimpleInteractable interactable;
    private GameObject currentInstance;
    private Vector3 visualStartLocalPos;
    private float lastPressTime = -999f;
    private bool used;

    private void Awake()
    {
        interactable = GetComponent<XRSimpleInteractable>();
        if (visualToPress != null)
            visualStartLocalPos = visualToPress.localPosition;
    }

    private void OnEnable()
    {
        interactable.selectEntered.AddListener(OnPressed);
        interactable.selectExited.AddListener(OnReleased);
    }

    private void OnDisable()
    {
        interactable.selectEntered.RemoveListener(OnPressed);
        interactable.selectExited.RemoveListener(OnReleased);
    }

    private void OnPressed(SelectEnterEventArgs args)
    {
        if (prefabToSpawn == null)
        {
            Debug.LogWarning("VRSpawnButton: No prefab assigned.", this);
            return;
        }

        if (Time.time - lastPressTime < cooldown) return;
        if (spawnOnlyOnce && used) return;

        lastPressTime = Time.time;
        used = true;

        if (destroyPreviousInstance && currentInstance != null)
            Destroy(currentInstance);

        Vector3 pos = spawnPoint != null ? spawnPoint.position : transform.position + transform.forward * 0.3f;
        Quaternion rot = spawnPoint != null ? spawnPoint.rotation : transform.rotation;
        currentInstance = Instantiate(prefabToSpawn, pos, rot, parent);

        // Feedback
        if (visualToPress != null)
            visualToPress.localPosition = visualStartLocalPos + Vector3.down * pressDepth;

        if (clickSound != null)
            clickSound.Play();

        if (args.interactorObject is UnityEngine.XR.Interaction.Toolkit.Interactors.XRBaseInputInteractor controllerInteractor)
            controllerInteractor.SendHapticImpulse(hapticAmplitude, hapticDuration);
    }

    private void OnReleased(SelectExitEventArgs args)
    {
        if (visualToPress != null)
            visualToPress.localPosition = visualStartLocalPos;
    }
}