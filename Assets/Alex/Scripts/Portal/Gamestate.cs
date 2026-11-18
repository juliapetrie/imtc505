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

    public static bool HasReturnPoint { get; private set; }
    public static Vector3 ReturnPosition { get; private set; }
    public static Quaternion ReturnRotation { get; private set; }

    public static bool IsCompleted(string memoryId) => completedMemories.Contains(memoryId);

    public static void CompleteMemory(string memoryId)
    {
        // Ignore blanks and repeats so revisiting a memory can't inflate the count.
        if (string.IsNullOrEmpty(memoryId) || !completedMemories.Add(memoryId)) return;

        Debug.Log($"Memory '{memoryId}' complete. Memories completed: {MemoriesCompleted}/{TotalMemories}");
        OnMemoryCompleted?.Invoke(MemoriesCompleted);
    }

    public static void SetReturnPoint(Vector3 position, Quaternion rotation)
    {
        ReturnPosition = position;
        ReturnRotation = rotation;
        HasReturnPoint = true;
    }

    // Call from a restart button so a new playthrough starts fresh.
    public static void Reset()
    {
        completedMemories.Clear();
        HasReturnPoint = false;
    }
}