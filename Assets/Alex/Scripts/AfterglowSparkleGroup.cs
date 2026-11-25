using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Put this on a parent object that holds lots of separate props (benches, bins, lamp posts...).
/// When the scene starts it adds an AfterglowSparkle to each prop underneath, so every prop sparkles
/// on its own when touched, instead of the whole group reacting together.
///
/// Use this INSTEAD of AfterglowSparkle on the parent. A prop that already has its own
/// AfterglowSparkle is left alone, so you can still hand-tune individual props.
/// Don't put an XR interactable on the parent itself, or every prop will share it.
///
/// To control how the sparkles look and behave, make a Settings Template (see the field below).
/// </summary>
[DisallowMultipleComponent]
public sealed class AfterglowSparkleGroup : MonoBehaviour
{
    [Tooltip("How many levels down the individual props are.\n" +
             "1 = the direct children of this object, 2 = their children, and so on.")]
    [SerializeField, Min(1)] private int depth = 1;

    [Tooltip("Also set up props that are switched off right now. They start sparkling once enabled.")]
    [SerializeField] private bool includeInactive = true;

    [Header("Settings for every prop")]
    [Tooltip("Optional. An AfterglowSparkle on a SWITCHED-OFF object (keep it outside this group). " +
             "Its settings are copied onto every prop, so you can edit them here before pressing Play.")]
    [SerializeField] private AfterglowSparkle settingsTemplate;

    private void Awake()
    {
        if (GetComponentInParent<XRBaseInteractable>() != null)
        {
            Debug.LogWarning("AfterglowSparkleGroup: this object or one of its parents has an XR interactable, " +
                             "so the props below will share it and sparkle together. Remove it from the group.", this);
        }

        if (settingsTemplate != null && settingsTemplate.gameObject.activeInHierarchy)
        {
            Debug.LogWarning("AfterglowSparkleGroup: the Settings Template is on an active object, so it will sparkle " +
                             "by itself too. Switch that object off (untick the box next to its name).", settingsTemplate);
        }

        // Collect first, then add: adding sparkles creates new child objects.
        var props = new List<Transform>();
        Collect(transform, 1, props);

        foreach (var prop in props)
        {
            if (prop.GetComponent<AfterglowSparkle>() != null) continue;
            if (!HasVisibleMesh(prop)) continue;

            var sparkle = prop.gameObject.AddComponent<AfterglowSparkle>();
            if (settingsTemplate != null) sparkle.ApplySettingsFrom(settingsTemplate);
        }
    }

    private void Collect(Transform parent, int level, List<Transform> result)
    {
        foreach (Transform child in parent)
        {
            if (!includeInactive && !child.gameObject.activeInHierarchy) continue;
            if (level == depth) result.Add(child);
            else Collect(child, level + 1, result);
        }
    }

    private static bool HasVisibleMesh(Transform root)
    {
        foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            if (renderer is MeshRenderer || renderer is SkinnedMeshRenderer) return true;
        return false;
    }
}