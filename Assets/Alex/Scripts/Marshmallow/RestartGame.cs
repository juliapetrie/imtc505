using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// Put on any object in the scene. Hold the LEFT controller's Y button (or R on a keyboard)
/// to restart. If "Reset Targets" has objects in it, only those are put back at their start
/// (e.g. the marshmallow stick). If the list is empty, the whole scene is reloaded.
/// </summary>
public class RestartGame : MonoBehaviour
{
    public float holdSeconds = 1.5f;

    [Tooltip("Objects to put back at their start position. Leave empty to reload the whole scene instead.")]
    public ResetToStart[] resetTargets;

    [Header("Debug")]
    [Tooltip("Logs button presses and what the restart does to the Console.")]
    public bool debugLog = true;

    InputAction restart;
    float held;
    bool wasPressed;

    void Awake()
    {
        restart = new InputAction("RestartGame", InputActionType.Button);
        // Y on Quest controllers. Several paths, because the name depends on the controller profile.
        restart.AddBinding("<XRController>{LeftHand}/secondaryButton");
        restart.AddBinding("<XRController>{LeftHand}/{SecondaryButton}");
        restart.AddBinding("<OculusTouchController>{LeftHand}/secondaryButton");
        restart.AddBinding("<Keyboard>/r");
    }

    void OnEnable()
    {
        restart.Enable();
        if (debugLog) Debug.Log("RestartGame: ready. Hold Y (left controller) or R for " + holdSeconds + "s. Reset targets: " +
                                (resetTargets != null ? resetTargets.Length : 0), this);
    }

    void OnDisable() => restart.Disable();
    void OnDestroy() => restart?.Dispose();

    void Update()
    {
        bool pressed = restart.IsPressed();
        if (debugLog && pressed != wasPressed)
            Debug.Log(pressed ? "RestartGame: button pressed (keep holding)" : "RestartGame: button released after " + held.ToString("F1") + "s", this);
        wasPressed = pressed;

        if (pressed) held += Time.unscaledDeltaTime;
        else held = 0f;

        if (held >= holdSeconds)
        {
            held = 0f;
            if (resetTargets != null && resetTargets.Length > 0)
            {
                if (debugLog) Debug.Log("RestartGame: resetting " + resetTargets.Length + " object(s)", this);
                foreach (var t in resetTargets) if (t != null) t.ResetNow();
            }
            else
            {
                if (debugLog) Debug.Log("RestartGame: no reset targets, reloading the scene", this);
                SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
            }
        }
    }
}
