using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

internal sealed class LivingRoomMixamo
{
    private static readonly string[] Names =
        { "WaveDanceA", "WaveDanceB", "Walk", "SillyDance", "TalkA", "TalkB", "TalkC", "SitTalk" };

    public GameObject[] Models { get; }
    public GameObject SeatedModel { get; }
    public AnimationClip Walk { get; }
    public AnimationClip Sit { get; }
    private readonly AnimationClip[] dances;
    private readonly AnimationClip[] talks;

    public LivingRoomMixamo()
    {
        var models = new List<GameObject>();
        var clips = new AnimationClip[Names.Length];
        var unavailable = new List<string>();
        for (int i = 0; i < Names.Length; i++)
        {
            string path = "PartyCast_" + Names[i];
            GameObject model = Resources.Load<GameObject>(path);
            Animator animator = model != null ? model.GetComponentInChildren<Animator>() : null;
            if (animator != null && animator.avatar != null && animator.avatar.isValid && animator.avatar.isHuman)
            {
                if (i == Names.Length - 1)
                    SeatedModel = model;
                else
                    models.Add(model);
            }

            AnimationClip[] importedClips = Resources.LoadAll<AnimationClip>(path);
            foreach (AnimationClip clip in importedClips)
            {
                if (clip.humanMotion && clip.length > 0f && (clips[i] == null || clip.length > clips[i].length))
                    clips[i] = clip;
            }

            if (animator == null || animator.avatar == null || !animator.avatar.isValid
                || !animator.avatar.isHuman || clips[i] == null)
                unavailable.Add($"{Names[i]} (rig: {(animator != null ? animator.avatar : null)}, clips: {importedClips.Length})");
        }

        Walk = clips[2];
        Sit = clips[7];
        dances = new[] { clips[0], clips[1], clips[3] };
        // TalkB is seated. Its character can reuse the standing clips.
        talks = new[] { clips[4], clips[6] };
        if (models.Count > 0 && Walk != null && Dance(0) != null && Talk(0) != null)
            Models = models.ToArray();

        if (unavailable.Count > 0)
            Debug.LogWarning("Some Mixamo party assets are unavailable: " + string.Join(", ", unavailable));
    }

    public AnimationClip Dance(int index) => Pick(dances, index);
    public AnimationClip Talk(int index) => Pick(talks, index);

    private static AnimationClip Pick(AnimationClip[] options, int index)
    {
        for (int i = 0; i < options.Length; i++)
        {
            AnimationClip clip = options[(index + i) % options.Length];
            if (clip != null)
                return clip;
        }
        return null;
    }
}

internal sealed class PartyMotion
{
    private readonly PlayableGraph graph;
    private readonly AnimationMixerPlayable mixer;
    private readonly AnimationClipPlayable[] slots = new AnimationClipPlayable[2];
    private int active = 1;
    private float blend = 1f;
    private AnimationClip current;

    public PartyMotion(Animator animator)
    {
        animator.enabled = true;
        animator.applyRootMotion = false;
        animator.runtimeAnimatorController = null;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        graph = PlayableGraph.Create("Party guest animation");
        graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
        mixer = AnimationMixerPlayable.Create(graph, 2);
        AnimationPlayableOutput.Create(graph, "Mixamo", animator).SetSourcePlayable(mixer);
        graph.Play();
    }

    public void Set(AnimationClip clip, float offset = 0f, float speed = 1f)
    {
        if (clip == null || clip == current)
            return;

        int next = 1 - active;
        if (slots[next].IsValid())
        {
            mixer.DisconnectInput(next);
            graph.DestroyPlayable(slots[next]);
        }
        slots[next] = AnimationClipPlayable.Create(graph, clip);
        slots[next].SetTime(clip.length * Mathf.Repeat(offset, 1f));
        slots[next].SetSpeed(speed);
        slots[next].SetApplyFootIK(true);
        graph.Connect(slots[next], 0, mixer, next);
        blend = slots[active].IsValid() ? 0f : 1f;
        active = next;
        current = clip;
    }

    public void Update(float elapsed)
    {
        blend = Mathf.MoveTowards(blend, 1f, elapsed * 4f);
        mixer.SetInputWeight(active, blend);
        mixer.SetInputWeight(1 - active, 1f - blend);
        foreach (AnimationClipPlayable slot in slots)
        {
            if (slot.IsValid() && slot.GetTime() >= slot.GetAnimationClip().length)
                slot.SetTime(slot.GetTime() % slot.GetAnimationClip().length);
        }
        graph.Evaluate(elapsed);
    }

    public void Dispose()
    {
        if (graph.IsValid())
            graph.Destroy();
    }
}
