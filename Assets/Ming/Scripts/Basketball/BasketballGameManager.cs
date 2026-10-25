
using UnityEngine;
using TMPro;
using System.Collections;

public class BasketballGameManager : MonoBehaviour
{
    [Header("References")]
    public Rigidbody ball;
    public Transform spawnPoint;
    public Transform playerHead;
    public Transform hoopCenter;
    public TMP_Text scoreText;
    public ParticleSystem scoreEffect;
    public AudioSource scoreSound;

    [Header("Game Rules")]
    public float minThrowDistance = 1.8f;
    public float missResetDelay = 6f;
    public float scoreResetDelay = 2f;
    public float maxDistanceFromSpawn = 8f;
    public float resetBelowY = -0.8f;

    private int score = 0;

    private bool isHeld;
    private bool inFlight;
    private bool validShot;
    private bool upperPassed;
    private bool scoredThisShot;

    private float upperPassTime;
    private float resetDeadline =
        float.PositiveInfinity;

    private void Start()
    {
        UpdateScoreText();
        ResetBall();
    }

    private void Update()
    {
        // Never reset while the player is holding the ball.
        if (isHeld)
            return;

        if (ball == null || spawnPoint == null)
            return;

        // Automatically reset after scoring or timeout.
        if (Time.time >= resetDeadline)
        {
            ResetBall();
            return;
        }

        if (!inFlight)
            return;

        Vector3 difference =
            ball.position - spawnPoint.position;

        // Horizontal distance, ignoring height.
        difference.y = 0f;

        bool outOfBounds =
            difference.magnitude > maxDistanceFromSpawn;

        bool fellBelowGround =
            ball.position.y < resetBelowY;

        if (outOfBounds || fellBelowGround)
        {
            ResetBall();
        }
    }

    public void OnBallGrabbed()
    {
        isHeld = true;
        inFlight = false;
        validShot = false;
        upperPassed = false;
        scoredThisShot = false;

        resetDeadline = float.PositiveInfinity;

        // Unlock the hovering ball.
        ball.isKinematic = false;
        ball.constraints = RigidbodyConstraints.None;
        ball.useGravity = false;

        Debug.Log("Basketball grabbed");
    }

    
    public void OnBallReleased()
    {
        isHeld = false;
        inFlight = true;

        // No distance restriction.
        validShot = true;

        upperPassed = false;
        scoredThisShot = false;

        // Restore normal physics.
        ball.isKinematic = false;
        ball.constraints = RigidbodyConstraints.None;
        ball.useGravity = true;

        resetDeadline = Time.time + missResetDelay;

        StartCoroutine(VerifyReleasePhysics());

        Debug.Log("Basketball released");
    }


    private IEnumerator VerifyReleasePhysics()
    {
        yield return null;

        if (isHeld || !inFlight)
            yield break;

        ball.isKinematic = false;
        ball.constraints = RigidbodyConstraints.None;
        ball.useGravity = true;
        Debug.Log(
            $"Gravity={ball.useGravity}, " +
            $"Kinematic={ball.isKinematic}, " +
            $"Constraints={ball.constraints}, " +
            $"Velocity={ball.linearVelocity}, " +
            $"WorldGravity={Physics.gravity}"
        );
    }

    public void EnterUpper(Rigidbody other)
    {
        if (other != ball)
            return;

        if (!inFlight || !validShot || scoredThisShot)
            return;

        // Ball must be moving downward.
        if (ball.linearVelocity.y >= -0.1f)
            return;

        upperPassed = true;
        upperPassTime = Time.time;
    }

    public void EnterLower(Rigidbody other)
    {
        if (other != ball)
            return;

        if (!inFlight || !validShot || scoredThisShot)
            return;

        if (!upperPassed)
            return;

        if (Time.time - upperPassTime > 1f)
            return;

        if (ball.linearVelocity.y >= -0.1f)
            return;

        // Successful shot.
        scoredThisShot = true;
        inFlight = false;

        score++;
        UpdateScoreText();

        if (scoreEffect != null)
            scoreEffect.Play();

        if (scoreSound != null)
            scoreSound.Play();

        resetDeadline = Time.time + scoreResetDelay;

        Debug.Log("Basket scored! Total: " + score);
    }

    
    public void ResetBall()
    {
        if (ball == null || spawnPoint == null || isHeld)
            return;

        isHeld = false;
        inFlight = false;
        validShot = false;
        upperPassed = false;
        scoredThisShot = false;

        resetDeadline = float.PositiveInfinity;

        // Keep the Rigidbody dynamic.
        ball.isKinematic = false;
        ball.constraints = RigidbodyConstraints.None;

        // Suspend gravity while waiting for the next shot.
        ball.useGravity = false;

        ball.linearVelocity = Vector3.zero;
        ball.angularVelocity = Vector3.zero;

        ball.position = spawnPoint.position;
        ball.rotation = spawnPoint.rotation;
    }


    private void FixedUpdate()
    {
        if (ball == null || spawnPoint == null)
            return;

        // Let XR Interaction Toolkit control the ball
        // while the player is holding it.
        if (isHeld)
            return;

        // Ball has been released or has just scored.
        // It must obey normal physics.
        if (inFlight || scoredThisShot)
        {
            ball.isKinematic = false;
            ball.constraints = RigidbodyConstraints.None;
            ball.useGravity = true;

            return;
        }

        // Waiting for the next shot:
        // keep the ball at its spawn position.
        ball.isKinematic = false;
        ball.constraints = RigidbodyConstraints.None;
        ball.useGravity = false;

        ball.linearVelocity = Vector3.zero;
        ball.angularVelocity = Vector3.zero;

        ball.position = spawnPoint.position;
        ball.rotation = spawnPoint.rotation;
    }

    private void UpdateScoreText()
    {
        if (scoreText != null)
            scoreText.text = "SCORE: " + score;
    }
}
