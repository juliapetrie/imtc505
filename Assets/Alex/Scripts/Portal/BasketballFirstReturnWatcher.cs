using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Watches the basketball and fires once, the first time it is back at its start point after being thrown.
/// It reads the public ball and spawnPoint fields of BasketballGameManager, so that script is left untouched.
///
/// How it tells: BasketballGameManager only turns gravity on while the ball is in flight. So "gravity was on,
/// and now the ball is resting at the spawn point" means the ball has been thrown and has been reset.
///
/// Put this on any object (for example the same one as PortalReveal) and assign the fields below.
/// </summary>
public class BasketballFirstReturnWatcher : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The object with the BasketballGameManager. Leave empty to find it in the scene.")]
    public BasketballGameManager game;

    [Tooltip("Optional. The portal controller to reveal. It appears where you placed it in the scene.")]
    public PortalReveal portalReveal;

    [Header("Detection")]
    [Tooltip("How close (metres) the ball must be to its spawn point to count as returned.")]
    public float returnDistance = 0.15f;

    [Header("Events")]
    [Tooltip("Fires once, for anything else you want to trigger (sound, light, a message).")]
    public UnityEvent onFirstReturn;

    bool _wasThrown;
    bool _fired;

    void Awake()
    {
        if (game == null) game = FindFirstObjectByType<BasketballGameManager>();
        if (game == null)
            Debug.LogError("BasketballFirstReturnWatcher: no BasketballGameManager found in the scene.", this);
    }

    void Update()
    {
        if (_fired || game == null || game.ball == null || game.spawnPoint == null) return;

        Rigidbody ball = game.ball;

        // Gravity is only on while the ball is in flight (or just scored and waiting to reset).
        if (ball.useGravity)
        {
            _wasThrown = true;
            return;
        }

        if (!_wasThrown) return;

        // Gravity is off again and the ball has been thrown: has it been put back at its start?
        Vector3 offset = ball.position - game.spawnPoint.position;
        bool atStart = offset.sqrMagnitude <= returnDistance * returnDistance;
        bool still = ball.linearVelocity.sqrMagnitude < 0.01f;

        if (atStart && still) Fire();
    }

    void Fire()
    {
        _fired = true;
        Debug.Log("BasketballFirstReturnWatcher: the ball is back at its start for the first time.", this);

        if (portalReveal != null) portalReveal.Reveal();
        onFirstReturn?.Invoke();
    }
}
