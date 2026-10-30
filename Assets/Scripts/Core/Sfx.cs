using System;
using System.Collections.Generic;
using UnityEngine;

public enum SfxId
{
    Shoot,
    Hit,
    Boom,
    Zap,
    Coin,
    Pop,
    CoreHit,
    Build,
    WaveStart,
    GameOver,
    Upgrade,
}

// Procedurally synthesized placeholder sounds, so the game has audio feedback without any
// imported assets. Swap in authored clips later by replacing Synthesize().
public static class Sfx
{
    private const int SampleRate = 44100;
    private const int VoiceCount = 14;

    private static readonly Dictionary<SfxId, AudioClip> clips = new Dictionary<SfxId, AudioClip>();
    private static readonly Dictionary<SfxId, float> lastPlayed = new Dictionary<SfxId, float>();
    private static AudioSource[] voices;
    private static int nextVoice;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        clips.Clear();
        lastPlayed.Clear();
        voices = null;
        nextVoice = 0;
    }

    public static void Play(SfxId id, float pitch = 1f, float volumeScale = 1f)
    {
        (float volume, float minInterval) = Settings(id);

        float now = Time.unscaledTime;
        if (lastPlayed.TryGetValue(id, out float last) && now - last < minInterval) return;
        lastPlayed[id] = now;

        EnsureVoices();
        AudioSource voice = voices[nextVoice];
        nextVoice = (nextVoice + 1) % voices.Length;

        voice.pitch = pitch * UnityEngine.Random.Range(0.93f, 1.07f);
        voice.PlayOneShot(GetClip(id), volume * volumeScale);
    }

    // Per-sound volume and a minimum gap between repeats, so many towers firing at once
    // doesn't turn into noise.
    private static (float volume, float minInterval) Settings(SfxId id)
    {
        switch (id)
        {
            case SfxId.Shoot: return (0.18f, 0.06f);
            case SfxId.Hit: return (0.16f, 0.05f);
            case SfxId.Boom: return (0.55f, 0.08f);
            case SfxId.Zap: return (0.28f, 0.07f);
            case SfxId.Coin: return (0.22f, 0.05f);
            case SfxId.Pop: return (0.3f, 0.04f);
            case SfxId.CoreHit: return (0.5f, 0.15f);
            case SfxId.Build: return (0.5f, 0.1f);
            case SfxId.WaveStart: return (0.4f, 0.5f);
            case SfxId.GameOver: return (0.6f, 1f);
            case SfxId.Upgrade: return (0.45f, 0.2f);
            default: return (0.3f, 0.05f);
        }
    }

    private static void EnsureVoices()
    {
        if (voices != null && voices[0] != null) return;

        var host = new GameObject("Sfx");
        UnityEngine.Object.DontDestroyOnLoad(host);
        voices = new AudioSource[VoiceCount];
        for (int i = 0; i < VoiceCount; i++)
        {
            AudioSource source = host.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            voices[i] = source;
        }
    }

    private static AudioClip GetClip(SfxId id)
    {
        if (clips.TryGetValue(id, out AudioClip clip) && clip != null) return clip;

        float[] data = Synthesize(id);
        clip = AudioClip.Create(id.ToString(), data.Length, 1, SampleRate, false);
        clip.SetData(data, 0);
        clips[id] = clip;
        return clip;
    }

    private static float[] Synthesize(SfxId id)
    {
        var rng = new System.Random((int)id * 7919 + 17);
        float Noise() => (float)(rng.NextDouble() * 2.0 - 1.0);

        switch (id)
        {
            case SfxId.Shoot:
                return Render(0.08f, (t, p) => Square(Sweep(t, 0.08f, 900f, 320f, ref p.phase)) * Decay(t, 40f) * 0.6f);
            case SfxId.Hit:
                return Render(0.05f, (t, p) => (Noise() * 0.7f + Mathf.Sin(Sweep(t, 0.05f, 1400f, 900f, ref p.phase)) * 0.4f) * Decay(t, 70f));
            case SfxId.Boom:
                return Render(0.6f, (t, p) =>
                {
                    p.low += (Noise() - p.low) * 0.08f;
                    float body = Mathf.Sin(Sweep(t, 0.6f, 110f, 35f, ref p.phase)) * Decay(t, 7f);
                    return (p.low * 2.2f * Decay(t, 6f) + body * 0.8f);
                });
            case SfxId.Zap:
                return Render(0.18f, (t, p) =>
                {
                    float buzz = Mathf.Sign(Mathf.Sin(t * Mathf.PI * 2f * 70f));
                    float tone = Mathf.Sin(Sweep(t, 0.18f, 2200f, 1200f, ref p.phase));
                    return (Noise() * 0.5f * (buzz * 0.5f + 0.5f) + tone * 0.35f) * Decay(t, 18f);
                });
            case SfxId.Coin:
                return Render(0.2f, (t, p) =>
                {
                    float freq = t < 0.06f ? 1318.5f : 1760f;
                    p.phase += freq / SampleRate * Mathf.PI * 2f;
                    float env = t < 0.06f ? Decay(t, 20f) : Decay(t - 0.06f, 16f);
                    return Mathf.Sin(p.phase) * env * 0.7f;
                });
            case SfxId.Pop:
                return Render(0.1f, (t, p) => (Mathf.Sin(Sweep(t, 0.1f, 520f, 110f, ref p.phase)) * 0.8f + Noise() * 0.25f) * Decay(t, 30f));
            case SfxId.CoreHit:
                return Render(0.35f, (t, p) =>
                {
                    p.low += (Noise() - p.low) * 0.05f;
                    return (Mathf.Sin(Sweep(t, 0.35f, 95f, 45f, ref p.phase)) * 0.9f + p.low * 1.2f) * Decay(t, 10f);
                });
            case SfxId.Build:
                return Render(0.22f, (t, p) => (Mathf.Sin(Sweep(t, 0.22f, 200f, 80f, ref p.phase)) * 0.9f + Noise() * 0.3f * Decay(t, 60f)) * Decay(t, 14f));
            case SfxId.WaveStart:
                return Render(0.7f, (t, p) =>
                {
                    float vibrato = 1f + Mathf.Sin(t * 30f) * 0.01f;
                    p.phase += 220f * vibrato / SampleRate;
                    float saw = (p.phase % 1f) * 2f - 1f;
                    float fifth = ((p.phase * 1.5f) % 1f) * 2f - 1f;
                    float env = Mathf.Min(1f, t / 0.05f) * Decay(t, 3.5f);
                    return (saw + fifth) * 0.3f * env;
                });
            case SfxId.GameOver:
                return Render(1.3f, (t, p) =>
                {
                    p.phase += Mathf.Lerp(330f, 90f, t / 1.3f) / SampleRate;
                    float saw = (p.phase % 1f) * 2f - 1f;
                    return saw * 0.45f * Decay(t, 1.8f);
                });
            case SfxId.Upgrade:
            {
                // Rising major arpeggio: C6, E6, G6, C7.
                float[] notes = { 1046.5f, 1318.5f, 1568f, 2093f };
                return Render(0.42f, (t, p) =>
                {
                    int step = Mathf.Min(notes.Length - 1, (int)(t / 0.07f));
                    p.phase += notes[step] / SampleRate * Mathf.PI * 2f;
                    float local = t - step * 0.07f;
                    float env = step == notes.Length - 1 ? Decay(local, 6f) : Decay(local, 14f);
                    return (Mathf.Sin(p.phase) + Mathf.Sin(p.phase * 2f) * 0.25f) * env * 0.55f;
                });
            }
            default:
                return new float[1];
        }
    }

    private class SynthState
    {
        public float phase;
        public float low;
    }

    private static float[] Render(float duration, Func<float, SynthState, float> sample)
    {
        var data = new float[Mathf.CeilToInt(duration * SampleRate)];
        var state = new SynthState();
        for (int i = 0; i < data.Length; i++)
        {
            data[i] = Mathf.Clamp(sample(i / (float)SampleRate, state), -1f, 1f);
        }
        return data;
    }

    // Advances a phase accumulator through an exponential frequency sweep and returns it in radians.
    private static float Sweep(float t, float duration, float from, float to, ref float phase)
    {
        float freq = from * Mathf.Pow(to / from, Mathf.Clamp01(t / duration));
        phase += freq / SampleRate * Mathf.PI * 2f;
        return phase;
    }

    private static float Square(float phase) => Mathf.Sin(phase) >= 0f ? 1f : -1f;

    private static float Decay(float t, float rate) => Mathf.Exp(-t * rate);
}
