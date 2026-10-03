using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables; // XRI 3.x. For XRI 2.x, delete this line.

/// <summary>
/// A 3D VR button that spawns a prefab when pressed (select), with debug logging
/// to help track down why a spawn happens at the wrong time.
/// Check the Console (or adb logcat on Quest) for "[VRSpawnButton]" messages.
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
    [Tooltip("Ignore presses for this many seconds after the scene starts.")]
    [SerializeField] private float startupGracePeriod = 1.0f;
    [Tooltip("Spawn when the press is released instead of when it begins.")]
    [SerializeField] private bool spawnOnRelease = false;

    [Header("Feedback")]
    [SerializeField] private Transform visualToPress;
    [SerializeField] private float pressDepth = 0.01f;
    [SerializeField] private AudioSource clickSound;
    [SerializeField] private float hapticAmplitude = 0.5f;
    [SerializeField] private float hapticDuration = 0.1f;

    [Header("Debug")]
    [SerializeField] private bool debugLogs = true;

    private XRSimpleInteractable interactable;
    private GameObject currentInstance;
    private Vector3 visualStartLocalPos;
    private float lastPressTime = -999f;
    private bool used;

    private void Log(string message)
    {
        if (debugLogs)
            Debug.Log($"[VRSpawnButton] t={Time.time:F2}s | {message}", this);
    }

    private void Awake()
    {
        interactable = GetComponent<XRSimpleInteractable>();
        if (visualToPress != null)
            visualStartLocalPos = visualToPress.localPosition;

        Log($"Awake on '{name}'. Prefab = {(prefabToSpawn != null ? prefabToSpawn.name : "NONE")}");
    }

    private void Start()
    {
        Log($"Start. Ignoring presses until t={startupGracePeriod:F2}s.");
    }

    private void OnEnable()
    {
        interactable.hoverEntered.AddListener(OnHoverEntered);
        interactable.hoverExited.AddListener(OnHoverExited);
        interactable.selectEntered.AddListener(OnSelectEntered);
        interactable.selectExited.AddListener(OnSelectExited);
        Log("Listeners registered.");
    }

    private void OnDisable()
    {
        interactable.hoverEntered.RemoveListener(OnHoverEntered);
        interactable.hoverExited.RemoveListener(OnHoverExited);
        interactable.selectEntered.RemoveListener(OnSelectEntered);
        interactable.selectExited.RemoveListener(OnSelectExited);
        Log("Listeners removed.");
    }

    private void OnHoverEntered(HoverEnterEventArgs args)
    {
        Log($"HOVER ENTER by '{args.interactorObject.transform.name}' (hover only, no spawn).");
    }

    private void OnHoverExited(HoverExitEventArgs args)
    {
        Log($"HOVER EXIT by '{args.interactorObject.transform.name}'.");
    }

    private void OnSelectEntered(SelectEnterEventArgs args)
    {
        Log($"SELECT ENTER by '{args.interactorObject.transform.name}' " +
            $"({args.interactorObject.GetType().Name}).");

        PressVisual(true);

        if (!spawnOnRelease)
            TrySpawn(args);
    }

    private void OnSelectExited(SelectExitEventArgs args)
    {
        Log($"SELECT EXIT by '{args.interactorObject.transform.name}'. " +
            $"Canceled = {args.isCanceled}.");

        PressVisual(false);

        if (spawnOnRelease && !args.isCanceled)
            TrySpawn(args);
    }

    private void TrySpawn(BaseInteractionEventArgs args)
    {
        if (prefabToSpawn == null)
        {
            Debug.LogWarning("[VRSpawnButton] BLOCKED: No prefab assigned.", this);
            return;
        }

        if (Time.time < startupGracePeriod)
        {
            Log($"BLOCKED: still in startup grace period ({Time.time:F2}s < {startupGracePeriod:F2}s).");
            return;
        }

        if (Time.time - lastPressTime < cooldown)
        {
            Log($"BLOCKED: cooldown active ({Time.time - lastPressTime:F2}s since last press).");
            return;
        }

        if (spawnOnlyOnce && used)
        {
            Log("BLOCKED: spawnOnlyOnce is on and sphere already spawned.");
            return;
        }

        lastPressTime = Time.time;
        used = true;

        if (destroyPreviousInstance && currentInstance != null)
        {
            Log($"Destroying previous instance '{currentInstance.name}'.");
            Destroy(currentInstance);
        }

        Vector3 pos = spawnPoint != null ? spawnPoint.position : transform.position + transform.forward * 0.3f;
        Quaternion rot = spawnPoint != null ? spawnPoint.rotation : transform.rotation;

        currentInstance = Instantiate(prefabToSpawn, pos, rot, parent);
        Log($"SPAWNED '{currentInstance.name}' at {pos}.");

        if (clickSound != null)
            clickSound.Play();

        if (args is SelectEnterEventArgs enterArgs &&
            enterArgs.interactorObject is UnityEngine.XR.Interaction.Toolkit.Interactors.XRBaseInputInteractor controller)
        {
            controller.SendHapticImpulse(hapticAmplitude, hapticDuration);
        }
    }

    private void PressVisual(bool pressed)
    {
        if (visualToPress == null) return;
        visualToPress.localPosition = pressed
            ? visualStartLocalPos + Vector3.down * pressDepth
            : visualStartLocalPos;
    }
}