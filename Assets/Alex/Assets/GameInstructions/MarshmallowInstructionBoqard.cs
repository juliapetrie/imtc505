using System.Collections;
using UnityEngine;

/// <summary>
/// World-space instruction board. It is placed once in front of the XR camera
/// when the scene starts, then remains at that world position (no head-following).
/// </summary>
public sealed class MarshmallowInstructionBoard : MonoBehaviour
{
    [Header("Board")]
    [SerializeField] private GameObject panel;
    [SerializeField] private Canvas worldCanvas;

    [Header("Opening behavior")]
    [SerializeField] private bool showWhenSceneStarts = true;
    [SerializeField] private bool placeOnceInFrontOfPlayer = true;
    [Min(0.8f)]
    [SerializeField] private float distanceFromCamera = 2.0f;
    [SerializeField] private float verticalOffset = 0.0f;

    private void Awake()
    {
        // Keep the root active so ShowBoard() can be called again later.
        if (panel != null)
            panel.SetActive(false);
    }

    private IEnumerator Start()
    {
        // Give the XR tracked camera one update to establish its headset pose.
        yield return null;

        Camera cameraToUse = Camera.main;
        if (cameraToUse == null)
            cameraToUse = FindFirstObjectByType<Camera>();

        if (cameraToUse != null)
        {
            if (worldCanvas != null)
                worldCanvas.worldCamera = cameraToUse;

            if (placeOnceInFrontOfPlayer)
            {
                Vector3 flatForward = Vector3.ProjectOnPlane(
                    cameraToUse.transform.forward, Vector3.up);

                if (flatForward.sqrMagnitude < 0.001f)
                    flatForward = cameraToUse.transform.forward;

                flatForward.Normalize();
                transform.position = cameraToUse.transform.position
                    + flatForward * distanceFromCamera
                    + Vector3.up * verticalOffset;
                transform.rotation = Quaternion.LookRotation(flatForward, Vector3.up);
            }
        }
        else
        {
            Debug.LogWarning(
                "[MarshmallowInstructionBoard] No camera found. Board keeps its scene position.",
                this);
        }

        if (showWhenSceneStarts)
            ShowBoard();
    }

    public void ShowBoard()
    {
        if (panel != null)
            panel.SetActive(true);
    }

    // Connected to CloseButton > Button.onClick inside the prefab.
    public void CloseBoard()
    {
        if (panel != null)
            panel.SetActive(false);
    }
}