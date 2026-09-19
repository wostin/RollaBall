using UnityEngine;

/// <summary>
/// Generates all game sound effects procedurally at runtime — LOUD edition.
/// No audio files needed. Attach to the AudioManager GameObject.
/// </summary>
[RequireComponent(typeof(AudioManager))]
public class ProceduralAudioGenerator : MonoBehaviour
{
    private const int SAMPLE_RATE = 44100;

    void Awake()
    {
        AudioManager am = GetComponent<AudioManager>();
        if (am == null) return;

        // Force max volumes
        am.rollVolume = 1f;
        am.sfxVolume  = 1f;

        am.rollClip      = GenerateRoll();
        am.pickupClip    = GeneratePickup();
        am.collisionClip = GenerateCollision();
        am.enemyHitClip  = GenerateEnemyHit();
        am.winClip       = GenerateWin();
        am.loseClip      = GenerateLose();
    }

    // ------------------------------------------------------------------
    // ROLLING — loud low-frequency rumble
    // ------------------------------------------------------------------
    private AudioClip GenerateRoll()
    {
        int len = SAMPLE_RATE * 2;
        float[] data = new float[len];
        System.Random rng = new System.Random(42);
        float prev = 0f;
        for (int i = 0; i < len; i++)
        {
            float white = (float)(rng.NextDouble() * 2.0 - 1.0);
            prev = prev + 0.12f * (white - prev);   // low-pass
            data[i] = Mathf.Clamp(prev * 2.5f, -1f, 1f);  // boost x2.5
        }
        return MakeClip("Roll", data);
    }

    // ------------------------------------------------------------------
    // PICKUP — loud bright coin ding with harmonics
    // ------------------------------------------------------------------
    private AudioClip GeneratePickup()
    {
        float dur = 0.35f;
        int len = (int)(SAMPLE_RATE * dur);
        float[] data = new float[len];
        for (int i = 0; i < len; i++)
        {
            float t    = (float)i / SAMPLE_RATE;
            float freq = Mathf.Lerp(500f, 1600f, (float)i / len);
            float env  = Mathf.Exp(-4f * t);
            // Fundamental + 2nd harmonic for richness
            float wave = Mathf.Sin(2f * Mathf.PI * freq * t)
                       + 0.5f * Mathf.Sin(4f * Mathf.PI * freq * t);
            data[i] = Mathf.Clamp(wave * env, -1f, 1f);
        }
        return MakeClip("Pickup", data);
    }

    // ------------------------------------------------------------------
    // COLLISION — heavy BOOM (low sine + full noise burst)
    // ------------------------------------------------------------------
    private AudioClip GenerateCollision()
    {
        float dur = 0.30f;
        int len = (int)(SAMPLE_RATE * dur);
        float[] data = new float[len];
        System.Random rng = new System.Random(7);
        for (int i = 0; i < len; i++)
        {
            float t    = (float)i / SAMPLE_RATE;
            float env  = Mathf.Exp(-12f * t);
            float tone = Mathf.Sin(2f * Mathf.PI * 60f * t);
            float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
            data[i] = Mathf.Clamp((tone * 0.7f + noise * 0.3f) * env * 1.5f, -1f, 1f);
        }
        return MakeClip("Collision", data);
    }

    // ------------------------------------------------------------------
    // ENEMY HIT — loud scary growl crash
    // ------------------------------------------------------------------
    private AudioClip GenerateEnemyHit()
    {
        float dur = 0.5f;
        int len = (int)(SAMPLE_RATE * dur);
        float[] data = new float[len];
        System.Random rng = new System.Random(13);
        for (int i = 0; i < len; i++)
        {
            float t    = (float)i / SAMPLE_RATE;
            float freq = Mathf.Lerp(350f, 60f, (float)i / len);
            float env  = Mathf.Exp(-5f * t);
            float wave = Mathf.Sin(2f * Mathf.PI * freq * t);
            float dist = Mathf.Clamp(wave * 3f, -1f, 1f);   // hard clip = distortion
            float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
            data[i] = Mathf.Clamp((dist * 0.7f + noise * 0.3f) * env, -1f, 1f);
        }
        return MakeClip("EnemyHit", data);
    }

    // ------------------------------------------------------------------
    // WIN — loud cheerful ascending fanfare
    // ------------------------------------------------------------------
    private AudioClip GenerateWin()
    {
        float[] noteFreqs = { 523.25f, 659.25f, 783.99f, 1046.5f }; // C5 E5 G5 C6
        float noteDur  = 0.22f;
        float totalDur = noteFreqs.Length * noteDur + 0.3f;
        int   totalLen = (int)(SAMPLE_RATE * totalDur);
        float[] data   = new float[totalLen];

        for (int n = 0; n < noteFreqs.Length; n++)
        {
            int   start   = (int)(n * noteDur * SAMPLE_RATE);
            int   noteLen = (int)(noteDur * SAMPLE_RATE * 1.6f);
            float freq    = noteFreqs[n];
            for (int i = 0; i < noteLen && start + i < totalLen; i++)
            {
                float t   = (float)i / SAMPLE_RATE;
                float env = Mathf.Exp(-4f * t);
                // Fundamental + octave for fullness
                float wave = Mathf.Sin(2f * Mathf.PI * freq * t)
                           + 0.4f * Mathf.Sin(4f * Mathf.PI * freq * t);
                data[start + i] = Mathf.Clamp(data[start + i] + wave * env, -1f, 1f);
            }
        }
        return MakeClip("Win", data);
    }

    // ------------------------------------------------------------------
    // LOSE — loud sad descending siren
    // ------------------------------------------------------------------
    private AudioClip GenerateLose()
    {
        float dur = 0.8f;
        int   len = (int)(SAMPLE_RATE * dur);
        float[] data = new float[len];
        for (int i = 0; i < len; i++)
        {
            float t    = (float)i / SAMPLE_RATE;
            float freq = Mathf.Lerp(480f, 160f, (float)i / len);
            float env  = Mathf.Exp(-2.5f * t);
            float wave  = Mathf.Sin(2f * Mathf.PI * freq * t);
            float wave2 = Mathf.Sin(2f * Mathf.PI * freq * 0.5f * t) * 0.5f;
            data[i] = Mathf.Clamp((wave + wave2) * env, -1f, 1f);
        }
        return MakeClip("Lose", data);
    }

    // ------------------------------------------------------------------
    // Helper
    // ------------------------------------------------------------------
    private AudioClip MakeClip(string clipName, float[] data)
    {
        AudioClip clip = AudioClip.Create(clipName, data.Length, 1, SAMPLE_RATE, false);
        clip.SetData(data, 0);
        return clip;
    }
}
