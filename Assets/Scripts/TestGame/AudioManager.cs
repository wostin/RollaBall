using UnityEngine;

/// <summary>
/// Manages all game audio. Attach to a dedicated AudioManager GameObject.
/// Assign AudioClips via the Inspector.
/// </summary>
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Audio Sources")]
    [Tooltip("Looping source for ball rolling sound")]
    public AudioSource rollSource;

    [Tooltip("One-shot source for SFX")]
    public AudioSource sfxSource;

    [Header("Audio Clips")]
    [Tooltip("Clip played while the ball is rolling")]
    public AudioClip rollClip;

    [Tooltip("Clip played when collecting a pickup")]
    public AudioClip pickupClip;

    [Tooltip("Clip played when hitting a wall or box")]
    public AudioClip collisionClip;

    [Tooltip("Clip played when the enemy catches the player")]
    public AudioClip enemyHitClip;

    [Tooltip("Clip played when the player wins")]
    public AudioClip winClip;

    [Tooltip("Clip played when the player loses")]
    public AudioClip loseClip;

    [Header("Settings")]
    [Range(0f, 1f)]
    public float rollVolume = 0.4f;
    [Range(0f, 1f)]
    public float sfxVolume = 1f;

    void Awake()
    {
        // Singleton pattern
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    // ---- Rolling ----
    public void StartRolling()
    {
        if (rollSource == null || rollClip == null) return;
        if (!rollSource.isPlaying)
        {
            rollSource.clip = rollClip;
            rollSource.loop = true;
            rollSource.volume = rollVolume;
            rollSource.Play();
        }
    }

    public void StopRolling()
    {
        if (rollSource != null && rollSource.isPlaying)
            rollSource.Stop();
    }

    public void SetRollSpeed(float normalizedSpeed)
    {
        if (rollSource == null) return;
        // Pitch and volume scale with speed for realism
        rollSource.pitch  = Mathf.Lerp(0.8f, 1.4f, normalizedSpeed);
        rollSource.volume = Mathf.Lerp(0.1f, rollVolume, normalizedSpeed);
    }

    // ---- One-shot SFX helpers ----
    public void PlayPickup()      => PlaySFX(pickupClip);
    public void PlayCollision()   => PlaySFX(collisionClip);
    public void PlayEnemyHit()    => PlaySFX(enemyHitClip);
    public void PlayWin()         => PlaySFX(winClip);
    public void PlayLose()        => PlaySFX(loseClip);

    private void PlaySFX(AudioClip clip)
    {
        if (sfxSource == null || clip == null) return;
        sfxSource.PlayOneShot(clip, sfxVolume);
    }
}
