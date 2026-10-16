using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Remembers progress between scene loads. Static, so no GameObject needed.
/// LightLevel (0..1) is what you'll later use to brighten the campus.
/// </summary>
///

public static class GameState
{
    public static float LightLevel { get; private set; }

    static readonly HashSet<string> completedMemories = new HashSet<string>();

    public static bool HasReturnPoint { get; private set; }
    public static Vector3 ReturnPosition { get; private set; }
    public static Quaternion ReturnRotation { get; private set; }

    public static bool IsCompleted(string memoryId) => completedMemories.Contains(memoryId);
    public static int CompletedCount => completedMemories.Count;

    public static void CompleteMemory(string memoryId, float lightGain)
    {
        if (string.IsNullOrEmpty(memoryId) || !completedMemories.Add(memoryId)) return;
        LightLevel = Mathf.Clamp01(LightLevel + lightGain);
        Debug.Log($"Memory '{memoryId}' complete. Light level: {LightLevel:0.00}");
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
        LightLevel = 0f;
        completedMemories.Clear();
        HasReturnPoint = false;
    }
}