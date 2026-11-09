using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// Put on the marshmallow bag (needs a collider + XRSimpleInteractable).
/// When a hand grabs the bag, a fresh marshmallow is spawned and placed straight into that hand.
/// </summary>
[RequireComponent(typeof(XRSimpleInteractable))]
public class MarshmallowBag : MonoBehaviour
{
    [Tooltip("Marshmallow prefab: Rigidbody + collider + XRGrabInteractable + RoastableMarshmallow.")]
    public XRGrabInteractable marshmallowPrefab;

    [Tooltip("0 = unlimited.")]
    public int maxMarshmallows = 0;

    XRSimpleInteractable bag;
    int spawned;

    void Awake() => bag = GetComponent<XRSimpleInteractable>();
    void OnEnable() => bag.selectEntered.AddListener(OnGrabbed);
    void OnDisable() => bag.selectEntered.RemoveListener(OnGrabbed);

    void OnGrabbed(SelectEnterEventArgs args)
    {
        if (marshmallowPrefab == null) { Debug.LogWarning("MarshmallowBag: assign Marshmallow Prefab.", this); return; }
        if (maxMarshmallows > 0 && spawned >= maxMarshmallows) return;
        StartCoroutine(SpawnIntoHand(args.interactorObject, args.interactableObject));
    }

    IEnumerator SpawnIntoHand(IXRSelectInteractor interactor, IXRSelectInteractable bagInteractable)
    {
        Transform attach = interactor.GetAttachTransform(bagInteractable);
        XRGrabInteractable m = Instantiate(marshmallowPrefab, attach.position, attach.rotation);
        spawned++;

        yield return null;   // let the current grab on the bag finish first

        var manager = bag.interactionManager;
        if (manager == null || m == null) yield break;
        manager.SelectExit(interactor, bagInteractable);   // let go of the bag
        manager.SelectEnter(interactor, m);                // hold the new marshmallow
    }
}