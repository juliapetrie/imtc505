using UnityEngine;

[RequireComponent(typeof(Canvas))]
public sealed class LobbyHeaderCamera : MonoBehaviour
{
    private Canvas header;

    private void OnEnable()
    {
        header = GetComponent<Canvas>();
        BindCamera();
    }

    private void LateUpdate()
    {
        if (header.worldCamera == null || !header.worldCamera.isActiveAndEnabled)
            BindCamera();
    }

    private void BindCamera()
    {
        if (Camera.main != null)
            header.worldCamera = Camera.main;
    }
}
