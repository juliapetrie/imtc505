using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

[RequireComponent(typeof(XRBaseInteractable))]
public class HoverLabel : MonoBehaviour
{
    [SerializeField] GameObject label;
    [SerializeField] bool hideWhileHeld = true;

    XRBaseInteractable interactable;
    Transform cam;

    void Awake()
    {
        interactable = GetComponent<XRBaseInteractable>();
        cam = Camera.main.transform;
        label.SetActive(false);
    }

    void OnEnable()
    {
        interactable.hoverEntered.AddListener(_ => Refresh());
        interactable.hoverExited.AddListener(_ => Refresh());
        interactable.selectEntered.AddListener(_ => Refresh());
        interactable.selectExited.AddListener(_ => Refresh());
    }

    void OnDisable()
    {
        interactable.hoverEntered.RemoveAllListeners();
        interactable.hoverExited.RemoveAllListeners();
        interactable.selectEntered.RemoveAllListeners();
        interactable.selectExited.RemoveAllListeners();
    }

    void Refresh()
    {
        bool show = interactable.isHovered && !(hideWhileHeld && interactable.isSelected);
        label.SetActive(show);
    }

    void LateUpdate()
    {
        if (!label.activeSelf) return;
        // Keep the text facing the player
        label.transform.rotation = Quaternion.LookRotation(label.transform.position - cam.position);
    }
}