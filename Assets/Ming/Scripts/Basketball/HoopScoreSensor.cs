using UnityEngine;

public class HoopScoreSensor : MonoBehaviour
{
    public BasketballGameManager gameManager;
    public bool isUpperSensor;

    private void OnTriggerEnter(Collider other)
    {
        Rigidbody ball = other.attachedRigidbody;

        if (gameManager == null || ball == null)
            return;

        if (isUpperSensor)
            gameManager.EnterUpper(ball);
        else
            gameManager.EnterLower(ball);
    }
}
