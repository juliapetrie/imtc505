using System;
using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// Sequential NPC quest manager. Press F to simulate a finished handshake.
public class NPCManager : MonoBehaviour
{
    [Header("Assign in Inspector")]
    public Transform player;
    public List<NPCController> npcs = new List<NPCController>();
    public bool enableKeyboardTest = true;

    public NPCController CurrentTarget { get; private set; }
    public int CompletedCount { get; private set; }

    // Teammate may also subscribe here instead of subscribing to each NPC.
    public event Action<NPCController> OnInteractionReady;
    public event Action<NPCController> OnTargetChanged;
    public event Action OnAllCompleted;

    private int nextIndex;

    private void Start()
    {
        if (player == null)
        {
            Debug.LogError("NPCManager: assign the Player_Test transform in the Inspector.", this);
            enabled = false;
            return;
        }

        foreach (NPCController npc in npcs)
        {
            if (npc == null) continue;
            npc.Configure(player, false);
            npc.OnInteractionReady += HandleNPCReady;
        }
        ActivateNextTarget();
    }

    private void OnDestroy()
    {
        foreach (NPCController npc in npcs)
            if (npc != null) npc.OnInteractionReady -= HandleNPCReady;
    }

    private void Update()
    {
        if (!enableKeyboardTest) return;
        bool pressedF = false;
#if ENABLE_INPUT_SYSTEM
        pressedF = Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
        pressedF = Input.GetKeyDown(KeyCode.F);
#endif
        if (pressedF && !CompleteCurrentInteraction())
            Debug.Log("Not ready: approach the current NPC until it stops before pressing F.");
    }

    private void HandleNPCReady(NPCController npc)
    {
        if (npc == CurrentTarget) OnInteractionReady?.Invoke(npc);
    }

    private void ActivateNextTarget()
    {
        while (nextIndex < npcs.Count && npcs[nextIndex] == null) nextIndex++;
        if (nextIndex >= npcs.Count)
        {
            CurrentTarget = null;
            Debug.Log("All NPC interactions completed.");
            OnAllCompleted?.Invoke();
            return;
        }

        CurrentTarget = npcs[nextIndex];
        nextIndex++;
        CurrentTarget.SetCurrentTarget(true);
        OnTargetChanged?.Invoke(CurrentTarget);
        Debug.Log($"Current target: {CurrentTarget.name}", this);
    }

    // Future handshake component calls this public method on success.
    public bool CompleteCurrentInteraction()
    {
        if (CurrentTarget == null || !CurrentTarget.CompleteInteraction()) return false;
        CompletedCount++;
        ActivateNextTarget();
        return true;
    }
}
