using UnityEngine;

/// <summary>
/// Plays a basketball sound ONLY when the ball collides with something
/// (floor, walls, hoop, other objects, etc.).
/// Attach this to your basketball GameObject.
/// The ball needs a Collider and a Rigidbody (add these yourself).
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class BasketballSound : MonoBehaviour
{
    [Header("Audio")]
    [Tooltip("Drag and drop your basketball bounce sound here. (Do NOT put it in the AudioSource's own AudioClip slot.)")]
    public AudioClip bounceSound;

    [Range(0f, 1f)]
    [Tooltip("Loudest the bounce can ever be.")]
    public float maxVolume = 1f;

    [Range(0f, 1f)]
    [Tooltip("Quietest the bounce can be (for light taps above the threshold).")]
    public float minVolume = 0.15f;

    [Header("Impact Settings")]
    [Tooltip("Collisions slower than this (m/s) make no sound. Stops rolling/resting jitter from spamming audio.")]
    public float minImpactSpeed = 0.4f;

    [Tooltip("Impact speed (m/s) at which the sound reaches max volume.")]
    public float maxImpactSpeed = 8f;

    [Tooltip("Minimum seconds between sounds, to prevent rapid retriggering.")]
    public float cooldown = 0.08f;

    [Tooltip("Seconds after the scene starts during which collisions are ignored (avoids a false sound when physics first settles).")]
    public float startupDelay = 0.3f;

    [Header("Variation")]
    [Tooltip("Random pitch range so each bounce sounds slightly different.")]
    public Vector2 pitchRange = new Vector2(0.95f, 1.05f);

    [Header("Filtering (optional)")]
    [Tooltip("Collisions with objects on these layers will NOT play a sound (e.g. your hands/controllers).")]
    public LayerMask ignoreLayers;

    private AudioSource audioSource;
    private float lastPlayTime = -999f;
    private float spawnTime;

    // Runs in the editor when the component is first added: set safe defaults
    private void Reset()
    {
        ApplySafeAudioSettings();
    }

    // Runs in the editor when values change: keeps Play On Awake unchecked
    private void OnValidate()
    {
        ApplySafeAudioSettings();
    }

    private void ApplySafeAudioSettings()
    {
        AudioSource src = GetComponent<AudioSource>();
        if (src == null) return;

        src.playOnAwake = false;   // never auto-play
        src.loop = false;
        src.spatialBlend = 1f;     // full 3D so it comes from the ball in VR
        src.rolloffMode = AudioRolloffMode.Linear;
        src.minDistance = 1f;
        src.maxDistance = 25f;
    }

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        spawnTime = Time.time;

        // Safety net: stop anything that already started and clear the
        // AudioSource's own clip so it can't play by itself.
        audioSource.playOnAwake = false;
        audioSource.loop = false;
        audioSource.Stop();
        audioSource.clip = null;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (bounceSound == null)
            return;

        // Ignore the first moments after the scene starts
        if (Time.time - spawnTime < startupDelay)
            return;

        // Skip ignored layers (e.g. hands)
        if (((1 << collision.gameObject.layer) & ignoreLayers.value) != 0)
            return;

        // Cooldown check
        if (Time.time - lastPlayTime < cooldown)
            return;

        // How hard was the hit?
        float impactSpeed = collision.relativeVelocity.magnitude;
        if (impactSpeed < minImpactSpeed)
            return;

        // Louder for harder impacts
        float t = Mathf.InverseLerp(minImpactSpeed, maxImpactSpeed, impactSpeed);
        float volume = Mathf.Lerp(minVolume, maxVolume, t);

        // Slight random pitch for variety
        audioSource.pitch = Random.Range(pitchRange.x, pitchRange.y);
        audioSource.PlayOneShot(bounceSound, volume);

        lastPlayTime = Time.time;
    }
}