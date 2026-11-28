using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Remembers progress between scene loads. Static, so no GameObject needed.
/// MemoriesCompleted is the marker: it goes up by one each time the player
/// returns from a memory. Other scripts can read it to change the campus.
/// </summary>
public static class GameState
{
    [Tooltip("How many memories there are in total.")]
    public static int TotalMemories = 3;

    static readonly HashSet<string> completedMemories = new HashSet<string>();

    /// <summary>0, 1, 2 or 3. The main progress marker.</summary>
    public static int MemoriesCompleted => completedMemories.Count;

    /// <summary>0 to 1, handy for lerping values (fog, colours, particle counts).</summary>
    public static float Progress01 =>
        TotalMemories > 0 ? Mathf.Clamp01((float)MemoriesCompleted / TotalMemories) : 0f;

    /// <summary>Fires after a memory is completed, with the new total.</summary>
    public static event Action<int> OnMemoryCompleted;

    /// <summary>The memory finished most recently. Lets the campus play a closing animation once.</summary>
    public static string LastCompletedMemoryId { get; private set; }

    public static void ClearLastCompleted() => LastCompletedMemoryId = null;

    /// <summary>The campus look the player has already seen (0 to TotalMemories). Used to blend from the old look to the new one.</summary>
    public static int AppliedStage { get; set; }

    public static bool HasReturnPoint { get; private set; }
    public static Vector3 ReturnPosition { get; private set; }
    public static Quaternion ReturnRotation { get; private set; }

    // Where each portal was placed when it appeared (by memory id), so the campus can put it back after a reload.
    struct PortalPlacement
    {
        public Vector3 position;
        public Quaternion rotation;
    }

    static readonly Dictionary<string, PortalPlacement> portalPlacements = new Dictionary<string, PortalPlacement>();

    public static bool IsCompleted(string memoryId) => completedMemories.Contains(memoryId);

    public static void CompleteMemory(string memoryId)
    {
        // Ignore blanks and repeats so revisiting a memory can't inflate the count.
        if (string.IsNullOrEmpty(memoryId) || !completedMemories.Add(memoryId)) return;

        LastCompletedMemoryId = memoryId;
        Debug.Log($"Memory '{memoryId}' complete. Memories completed: {MemoriesCompleted}/{TotalMemories}");
        OnMemoryCompleted?.Invoke(MemoriesCompleted);
    }

    public static void SetReturnPoint(Vector3 position, Quaternion rotation)
    {
        ReturnPosition = position;
        ReturnRotation = rotation;
        HasReturnPoint = true;
    }

    /// <summary>Remember where a portal appeared, so it can be put back after the campus reloads.</summary>
    public static void SavePortalPlacement(string portalId, Vector3 position, Quaternion rotation)
    {
        if (string.IsNullOrEmpty(portalId)) return;
        portalPlacements[portalId] = new PortalPlacement { position = position, rotation = rotation };
    }

    public static bool TryGetPortalPlacement(string portalId, out Vector3 position, out Quaternion rotation)
    {
        if (!string.IsNullOrEmpty(portalId) && portalPlacements.TryGetValue(portalId, out var placement))
        {
            position = placement.position;
            rotation = placement.rotation;
            return true;
        }

        position = default;
        rotation = Quaternion.identity;
        return false;
    }

    // Call from a restart button so a new playthrough starts fresh.
    public static void Reset()
    {
        completedMemories.Clear();
        portalPlacements.Clear();
        LastCompletedMemoryId = null;
        AppliedStage = 0;
        HasReturnPoint = false;
    }
}