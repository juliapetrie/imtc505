using UnityEngine;

public class StoryPolesInfo : MonoBehaviour
{
    [SerializeField] private GameObject infoPanel;
    [SerializeField] private Transform playerCamera;

    [SerializeField] private float distance = 2.0f;
    [SerializeField] private float heightOffset = 0.4f;

    private void Start()
    {
        if (infoPanel != null)
            infoPanel.SetActive(false);
    }

    public void ToggleInfo()
    {
        if (infoPanel == null || playerCamera == null)
            return;

        bool shouldShow = !infoPanel.activeSelf;

        if (shouldShow)
        {
            Vector3 forward = playerCamera.forward;
            forward.y = 0f;
            forward.Normalize();

            Vector3 targetPosition =
                playerCamera.position + forward * distance;

            // 比玩家眼睛高 0.4 m
            targetPosition.y =
                playerCamera.position.y + heightOffset;

            infoPanel.transform.position = targetPosition;

            infoPanel.transform.rotation =
                Quaternion.LookRotation(forward, Vector3.up);
        }

        infoPanel.SetActive(shouldShow);
    }
}