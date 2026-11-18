using System.Collections;
using UnityEngine;

/// <summary>
/// Put on any object in the campus scene. When you come back from a memory,
/// it puts the player where they left (in front of the portal they used).
/// </summary>
public class CampusSpawn : MonoBehaviour
{
    [Tooltip("The XR Origin root. Leave empty to find the object tagged Player.")]
    public Transform xrOrigin;

    IEnumerator Start()
    {
        if (!GameState.HasReturnPoint) yield break;

        if (xrOrigin == null)
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player == null) yield break;
            xrOrigin = player.transform;
        }

        // Wait a frame so XR tracking has initialised.
        yield return null;

        var cc = xrOrigin.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false; // otherwise it fights the teleport

        xrOrigin.SetPositionAndRotation(GameState.ReturnPosition, GameState.ReturnRotation);

        if (cc != null) cc.enabled = true;
    }
}