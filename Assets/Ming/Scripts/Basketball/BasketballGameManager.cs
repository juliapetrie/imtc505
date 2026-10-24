
using UnityEngine;
using TMPro;
using System.Collections;

public class BasketballGameManager : MonoBehaviour
{
    [Header("References")]
    public Rigidbody ball;
    public Transform spawnPoint;
    public TMP_Text scoreText;
    public ParticleSystem scoreEffect;
    public AudioSource scoreSound;

    [Header("Settings")]
    public float missResetDelay = 5f;
    public float scoreResetDelay = 2f;

    private int score = 0;
    private bool upperPassed = false;
    private bool scoredThisShot = false;
    private float upperPassTime;
    private Coroutine resetRoutine;

    
    private void Start()
    {
        UpdateScoreText();
        ResetBall();
    }

    public void EnterUpper(Rigidbody other)
    {
        if (other != ball || scoredThisShot)
            return;

        if (ball.linearVelocity.y >= -0.1f)
            return;

        upperPassed = true;
        upperPassTime = Time.time;
    }

    public void EnterLower(Rigidbody other)
    {
        if (other != ball || scoredThisShot)
            return;

        if (!upperPassed || Time.time - upperPassTime > 1f)
            return;

        if (ball.linearVelocity.y >= -0.1f)
            return;

        scoredThisShot = true;
        score++;
        UpdateScoreText();

        if (scoreEffect != null)
            scoreEffect.Play();

        if (scoreSound != null)
            scoreSound.Play();

        ScheduleReset(scoreResetDelay);
    }

    
    public void OnBallGrabbed()
    {
        CancelReset();

        upperPassed = false;
        scoredThisShot = false;

        // 允许篮球跟随 VR 手柄移动
        ball.constraints = RigidbodyConstraints.None;

        // 抓取期间不受重力影响！
        ball.useGravity = false;
    }


    
    public void OnBallReleased()
    {
        // 篮球开始正常飞行
        ball.useGravity = true;

        // 5 秒后自动重置
        ScheduleReset(missResetDelay);
    }


    private void UpdateScoreText()
    {
        if (scoreText != null)
            scoreText.text = "SCORE: " + score;
    }

    private void ScheduleReset(float delay)
    {
        CancelReset();
        resetRoutine = StartCoroutine(ResetAfter(delay));
    }

    private IEnumerator ResetAfter(float delay)
    {
        yield return new WaitForSeconds(delay);
        resetRoutine = null;
        ResetBall();
    }

    private void CancelReset()
    {
        if (resetRoutine != null)
        {
            StopCoroutine(resetRoutine);
            resetRoutine = null;
        }
    }

    
    public void ResetBall()
    {
        if (ball == null || spawnPoint == null)
            return;

        CancelReset();

        // 先恢复可运动状态，以便清除速度
        ball.isKinematic = false;
        ball.constraints = RigidbodyConstraints.None;
        ball.useGravity = false;

        // 清除之前的运动速度
        ball.linearVelocity = Vector3.zero;
        ball.angularVelocity = Vector3.zero;

        // 回到指定位置
        ball.position = spawnPoint.position;
        ball.rotation = spawnPoint.rotation;

        // 冻结篮球，使其悬停
        ball.constraints = RigidbodyConstraints.FreezeAll;

        upperPassed = false;
        scoredThisShot = false;
    }
}
