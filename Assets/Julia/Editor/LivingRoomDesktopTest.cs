using System.Collections;
using System.Linq;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

internal static class LivingRoomDesktopTest
{
    private const string Menu = "Tools/Living Room/Play with XR Simulator";
    private const string ScenePath = "Assets/Julia/Scenes/LivingRoom.unity";
    private const string SimulatorGuid = "58d0a4ac86f2348deb02f3880c71378e";
    private const string PendingKey = "IMTC505.LivingRoom.DesktopTest";

    [MenuItem(Menu)]
    private static void Play()
    {
        if (SceneManager.GetActiveScene().path != ScenePath)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            EditorSceneManager.OpenScene(ScenePath);
        }

        SessionState.SetBool(PendingKey, true);
        EditorApplication.EnterPlaymode();
    }

    [MenuItem(Menu, true)]
    private static bool CanPlay() => !EditorApplication.isPlayingOrWillChangePlaymode;

    [MenuItem("Tools/Living Room/Test DJ Button Touches", true)]
    private static bool CanTestTouches() =>
        EditorApplication.isPlaying && SceneManager.GetActiveScene().path == ScenePath;

    [MenuItem("Tools/Living Room/Test DJ Button Touches")]
    private static void TestTouches()
    {
        var dj = Object.FindAnyObjectByType<LivingRoomDJ>();
        if (dj != null)
            dj.StartCoroutine(CheckButtonTouches());
    }

    private static IEnumerator CheckButtonTouches()
    {
        var booth = GameObject.Find("DJ Booth Challenge");
        if (booth == null)
            throw new System.InvalidOperationException("The DJ booth has not started.");
        var hands = Object.FindObjectsByType<XRPokeInteractor>()
            .Where(hand => hand.isActiveAndEnabled && hand.GetComponentInParent<HandshakePlayerAvatar>())
            .OrderBy(hand => hand.handedness).ToArray();
        if (hands.Length != 2)
            throw new System.InvalidOperationException("Expected two active poke interactors on the player rig.");

        var goal = booth.transform.Find("DJ Instructions/Background/Goal").GetComponent<Text>();
        var title = booth.transform.Find("DJ Instructions/Background/Heading").GetComponent<Text>();
        if (title.text != "START YOUR MIX")
            throw new System.InvalidOperationException("Start the contact test at the track selection step.");

        var probe = new GameObject("DJ touch test fingertip");
        try
        {
            int index = 0;
            foreach (string name in new[] { "Deck A Callout", "Deck B Callout", "Cue Callout" })
            {
                var hand = hands[index++ % hands.Length];
                var originalTip = hand.attachTransform;
                var button = booth.transform.Find(name);
                try
                {
                    probe.transform.position = button.position - button.forward * 0.4f;
                    hand.attachTransform = probe.transform;
                    yield return null;
                    yield return null;
                    string before = goal.text;
                    probe.transform.position = button.position;
                    yield return null;
                    yield return null;
                    string after = goal.text;
                    if (before == after)
                        throw new System.InvalidOperationException(name + " did not respond to contact.");
                    yield return new WaitForSecondsRealtime(0.2f);
                    if (after != goal.text)
                        throw new System.InvalidOperationException(name + " repeated while held.");
                }
                finally
                {
                    hand.attachTransform = originalTip;
                }
            }
            if (!title.text.Contains("VOLUME SLIDER"))
                throw new System.InvalidOperationException("Start Mix did not start the volume task.");
            Debug.Log("DJ touch test passed for Change A, Change B and Start Mix using both rig interactors.");
        }
        finally
        {
            Object.Destroy(probe);
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void InstallSimulator()
    {
        if (!SessionState.GetBool(PendingKey, false))
            return;
        SessionState.EraseBool(PendingKey);
        if (SceneManager.GetActiveScene().path != ScenePath)
            return;

        string path = AssetDatabase.GUIDToAssetPath(SimulatorGuid);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null)
        {
            Debug.LogError("Living Room desktop testing requires the XR Interaction Simulator sample prefab.");
            return;
        }

        Object.Instantiate(prefab);
    }
}
