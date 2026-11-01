using System;
using System.Collections.Generic;
using UnityEngine;

// New ids go at the END: prefabs and scenes store these as their integer index.
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
    RifleShot,
    LightShot,
    CannonShot,
    SniperShot,
    BowShot,
    MissileLaunch,
    Ricochet,
    Splat,
}

// Procedurally synthesized sounds, so the game has audio without any imported assets.
// Weapons are built from layers (a bright noise crack, a low thump, a filtered noise tail and
// a short room reverb) so they read as real guns rather than chiptune. Each sound renders a
// few variants with different noise so rapid fire doesn't repeat the exact same waveform.
public static class Sfx
{
    private const int SampleRate = 44100;
    private const int VoiceCount = 16;
    private const int VariantCount = 3;

    private static readonly Dictionary<int, AudioClip> clips = new Dictionary<int, AudioClip>();
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
        float master = GameSettings.SoundVolume;
        if (master <= 0f) return;

        float now = Time.unscaledTime;
        if (lastPlayed.TryGetValue(id, out float last) && now - last < minInterval) return;
        lastPlayed[id] = now;

        EnsureVoices();
        AudioSource voice = voices[nextVoice];
        nextVoice = (nextVoice + 1) % voices.Length;

        voice.pitch = pitch * UnityEngine.Random.Range(0.94f, 1.06f);
        voice.PlayOneShot(GetClip(id, UnityEngine.Random.Range(0, VariantCount)), volume * volumeScale * master);
    }

    // Per-sound volume and a minimum gap between repeats, so many towers firing at once
    // doesn't turn into noise.
    private static (float volume, float minInterval) Settings(SfxId id)
    {
        switch (id)
        {
            case SfxId.Shoot: return (0.18f, 0.06f);
            case SfxId.Hit: return (0.22f, 0.045f);
            case SfxId.Boom: return (0.6f, 0.08f);
            case SfxId.Zap: return (0.28f, 0.07f);
            case SfxId.Coin: return (0.22f, 0.05f);
            case SfxId.Pop: return (0.3f, 0.04f);
            case SfxId.CoreHit: return (0.5f, 0.15f);
            case SfxId.Build: return (0.5f, 0.1f);
            case SfxId.WaveStart: return (0.55f, 0.5f);
            case SfxId.GameOver: return (0.6f, 1f);
            case SfxId.Upgrade: return (0.45f, 0.2f);
            case SfxId.RifleShot: return (0.4f, 0.04f);
            case SfxId.LightShot: return (0.24f, 0.05f);
            case SfxId.CannonShot: return (0.55f, 0.1f);
            case SfxId.SniperShot: return (0.45f, 0.1f);
            case SfxId.BowShot: return (0.45f, 0.08f);
            case SfxId.MissileLaunch: return (0.38f, 0.1f);
            case SfxId.Ricochet: return (0.16f, 0.08f);
            case SfxId.Splat: return (0.4f, 0.04f);
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

    private static AudioClip GetClip(SfxId id, int variant)
    {
        int key = (int)id * VariantCount + variant;
        if (clips.TryGetValue(key, out AudioClip clip) && clip != null) return clip;

        float[] data = Synthesize(id, variant);
        clip = AudioClip.Create($"{id}_{variant}", data.Length, 1, SampleRate, false);
        clip.SetData(data, 0);
        clips[key] = clip;
        return clip;
    }

    private static float[] Synthesize(SfxId id, int variant)
    {
        var rng = new System.Random((int)id * 7919 + variant * 104729 + 17);
        float Noise() => (float)(rng.NextDouble() * 2.0 - 1.0);
        float[] data;

        switch (id)
        {
            case SfxId.Shoot:
                return Render(0.08f, (t, p) => Square(Sweep(t, 0.08f, 900f, 320f, ref p.phase)) * Decay(t, 40f) * 0.6f);

            // Bullet into flesh: a dull wet slap with a little snap on top.
            case SfxId.Hit:
                return Render(0.16f, (t, p) =>
                {
                    float n = Noise();
                    float slap = Lp(ref p.low, n, 0.25f) * Decay(t, 45f) * 2.2f;
                    float thud = Mathf.Sin(Sweep(t, 0.1f, 180f, 70f, ref p.phase)) * Decay(t, 38f) * 0.8f;
                    float snap = (n - p.low) * Decay(t, 260f) * 0.35f;
                    return Saturate((slap + thud + snap) * 1.3f);
                });

            case SfxId.Boom:
                data = Render(1.7f, (t, p) =>
                {
                    float n = Noise();
                    float crack = (n - Lp(ref p.low, n, 0.3f)) * Decay(t, 50f) * 0.9f;
                    float body = Mathf.Sin(Sweep(t, 0.6f, 95f, 28f, ref p.phase)) * Decay(t, 4.5f);
                    float rumble = Lp(ref p.low2, Lp(ref p.low3, n, 0.05f), 0.08f) * Decay(t, 2.6f) * 9f;
                    float debris = Lp(ref p.low4, n, 0.2f) * Decay(t, 11f) * 0.9f;
                    return Saturate((crack + body + rumble + debris) * 1.5f);
                });
                Room(data, 1.5f, 0.35f);
                return Finish(data);

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

            // A low war horn: lips into the note slightly flat, the filter opens as it swells.
            case SfxId.WaveStart:
                data = Render(1.9f, (t, p) =>
                {
                    float vibrato = 1f + Mathf.Sin(t * 31f) * 0.006f * Mathf.Clamp01((t - 0.4f) * 3f);
                    float freq = 110f * (1f - 0.07f * Decay(t, 9f)) * vibrato;
                    p.phase += freq / SampleRate;
                    p.phase2 += freq * 1.5f * 1.003f / SampleRate;
                    float raw = Saw(p.phase) + Saw(p.phase2) * 0.55f + Mathf.Sin(p.phase * Mathf.PI) * 0.6f;
                    float env = Mathf.Min(1f, t / 0.2f) * (t < 1.15f ? 1f : Decay(t - 1.15f, 5f));
                    float bright = 0.015f + 0.1f * env;
                    float tone = Lp(ref p.low2, Lp(ref p.low, raw, bright), bright * 1.5f);
                    float breath = Noise() * 0.04f * env;
                    return Saturate((tone + breath) * env * 1.4f);
                });
                Room(data, 1.6f, 0.4f);
                return Finish(data);

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

            // Hero rifle: sharp crack, chest thump, short gassy tail and a bit of room.
            case SfxId.RifleShot:
                data = Render(0.6f, (t, p) =>
                {
                    float n = Noise();
                    float crack = (n - Lp(ref p.low, n, 0.35f)) * Decay(t, 110f) * 1.2f;
                    float gas = Lp(ref p.low2, n, 0.07f);
                    float body = gas * Decay(t, 22f) * 3.2f;
                    float thump = Mathf.Sin(Sweep(t, 0.12f, 165f, 55f, ref p.phase)) * Decay(t, 28f) * 0.85f;
                    float tail = gas * Decay(t, 7f) * 0.8f;
                    return Saturate((crack + body + thump + tail) * 1.5f);
                });
                Room(data, 1f, 0.28f);
                return Finish(data);

            // Gun tower and soldiers: same recipe, smaller and tighter so it can fire fast.
            case SfxId.LightShot:
                data = Render(0.35f, (t, p) =>
                {
                    float n = Noise();
                    float crack = (n - Lp(ref p.low, n, 0.4f)) * Decay(t, 170f);
                    float body = Lp(ref p.low2, n, 0.1f) * Decay(t, 38f) * 2.6f;
                    float thump = Mathf.Sin(Sweep(t, 0.08f, 210f, 85f, ref p.phase)) * Decay(t, 40f) * 0.6f;
                    return Saturate((crack + body + thump) * 1.4f);
                });
                Room(data, 0.7f, 0.18f);
                return Finish(data);

            case SfxId.CannonShot:
                data = Render(1.4f, (t, p) =>
                {
                    float n = Noise();
                    float crack = (n - Lp(ref p.low, n, 0.25f)) * Decay(t, 60f) * 0.8f;
                    float body = Mathf.Sin(Sweep(t, 0.4f, 115f, 32f, ref p.phase)) * Decay(t, 6f) * 1.1f;
                    float rumble = Lp(ref p.low2, Lp(ref p.low3, n, 0.06f), 0.1f) * Decay(t, 3.8f) * 8f;
                    return Saturate((crack + body + rumble) * 1.6f);
                });
                Room(data, 1.4f, 0.32f);
                return Finish(data);

            // Sniper: the loudest crack, then an echo rolling back off the treeline.
            case SfxId.SniperShot:
                data = Render(1.3f, (t, p) =>
                {
                    float n = Noise();
                    float crack = (n - Lp(ref p.low, n, 0.45f)) * Decay(t, 140f) * 1.5f;
                    float body = Lp(ref p.low2, n, 0.09f) * Decay(t, 16f) * 2.6f;
                    float thump = Mathf.Sin(Sweep(t, 0.15f, 220f, 60f, ref p.phase)) * Decay(t, 22f) * 0.75f;
                    return Saturate((crack + body + thump) * 1.6f);
                });
                Echo(data, 0.17f, 0.38f);
                Room(data, 1.2f, 0.22f);
                return Finish(data);

            // Bowstring twang plus the arrow's whoosh.
            case SfxId.BowShot:
                data = Render(0.4f, (t, p) =>
                {
                    float n = Noise();
                    float freq = 190f * (1f + 0.08f * Decay(t, 40f));
                    p.phase += freq / SampleRate * Mathf.PI * 2f;
                    float twang = (Mathf.Sin(p.phase) + Mathf.Sin(p.phase * 2f) * 0.5f + Mathf.Sin(p.phase * 3f) * 0.25f) * Decay(t, 20f) * 0.45f;
                    float click = n * Decay(t, 350f) * 0.4f;
                    float band = Lp(ref p.low, n, 0.25f) - Lp(ref p.low2, n, 0.04f);
                    float whoosh = band * Mathf.Min(1f, t / 0.05f) * Decay(Mathf.Max(0f, t - 0.05f), 11f) * 1.6f;
                    return Saturate(twang + click + whoosh);
                });
                return Finish(data);

            // Ignition pop, then a rocket hiss that brightens as it leaves.
            case SfxId.MissileLaunch:
                data = Render(1f, (t, p) =>
                {
                    float n = Noise();
                    float pop = (n - Lp(ref p.low, n, 0.3f)) * Decay(t, 80f) * 0.7f
                              + Mathf.Sin(Sweep(t, 0.1f, 150f, 60f, ref p.phase)) * Decay(t, 25f) * 0.7f;
                    float hissBright = 0.05f + 0.35f * Mathf.Min(1f, t / 0.3f);
                    float hiss = Lp(ref p.low2, n, hissBright) * Mathf.Min(1f, t / 0.04f) * Decay(t, 3.5f) * 1.1f;
                    float whistle = Mathf.Sin(Sweep(t, 1f, 500f, 1100f, ref p.phase2)) * Mathf.Min(1f, t / 0.1f) * Decay(t, 3f) * 0.12f;
                    return Saturate((pop + hiss + whistle) * 1.3f);
                });
                Room(data, 1f, 0.2f);
                return Finish(data);

            // Bullet glancing off stone: tick plus a falling whine.
            case SfxId.Ricochet:
                data = Render(0.4f, (t, p) =>
                {
                    float n = Noise();
                    float tick = (n - Lp(ref p.low, n, 0.4f)) * Decay(t, 200f) * 0.9f;
                    float whine = Mathf.Sin(Sweep(t, 0.35f, 3300f, 1600f, ref p.phase)) * Mathf.Min(1f, t / 0.008f) * Decay(t, 9f) * 0.3f;
                    return tick + whine;
                });
                return Finish(data);

            // Zombie death: wet thud, a gurgle and a couple of bone crunches.
            case SfxId.Splat:
                data = Render(0.35f, (t, p) =>
                {
                    float n = Noise();
                    float thud = Mathf.Sin(Sweep(t, 0.15f, 135f, 45f, ref p.phase)) * Decay(t, 18f) * 0.9f;
                    float gurgle = 0.6f + 0.4f * Mathf.Sin(t * Mathf.PI * 2f * 32f);
                    float wet = Lp(ref p.low, n, 0.16f) * Decay(t, 14f) * 3f * gurgle;
                    if (t < 0.09f && rng.NextDouble() < 0.0025) p.crunch = n > 0f ? 1f : -1f;
                    p.crunch *= 0.93f;
                    return Saturate((thud + wet + p.crunch * 0.55f) * 1.3f);
                });
                return Finish(data);

            default:
                return new float[1];
        }
    }

    private class SynthState
    {
        public float phase;
        public float phase2;
        public float low;
        public float low2;
        public float low3;
        public float low4;
        public float crunch;
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

    // Short fade on the last samples (reverb tails get cut at the buffer end) and a final clamp.
    private static float[] Finish(float[] data)
    {
        int fade = Mathf.Min(data.Length, SampleRate / 20);
        for (int i = 0; i < data.Length; i++)
        {
            float gain = i >= data.Length - fade ? (data.Length - i) / (float)fade : 1f;
            data[i] = Mathf.Clamp(data[i] * gain, -1f, 1f);
        }
        return data;
    }

    // Small Schroeder-style room: four damped feedback combs mixed back under the dry sound.
    private static void Room(float[] data, float size, float mix)
    {
        int[] baseDelays = { 1557, 1617, 1491, 1422 };
        var wet = new float[data.Length];
        foreach (int baseDelay in baseDelays)
        {
            var line = new float[Mathf.Max(1, Mathf.RoundToInt(baseDelay * size))];
            int index = 0;
            float damp = 0f;
            for (int i = 0; i < data.Length; i++)
            {
                float delayed = line[index];
                damp += (delayed - damp) * 0.35f;
                line[index] = data[i] + damp * 0.74f;
                wet[i] += delayed;
                index = (index + 1) % line.Length;
            }
        }
        for (int i = 0; i < data.Length; i++) data[i] += wet[i] * mix * 0.25f;
    }

    // Darkening feedback echo, like a shot bouncing back from distant trees.
    private static void Echo(float[] data, float delaySeconds, float feedback)
    {
        int delay = Mathf.RoundToInt(delaySeconds * SampleRate);
        float lp = 0f;
        for (int i = delay; i < data.Length; i++)
        {
            lp += (data[i - delay] - lp) * 0.25f;
            data[i] += lp * feedback;
        }
    }

    // Advances a phase accumulator through an exponential frequency sweep and returns it in radians.
    private static float Sweep(float t, float duration, float from, float to, ref float phase)
    {
        float freq = from * Mathf.Pow(to / from, Mathf.Clamp01(t / duration));
        phase += freq / SampleRate * Mathf.PI * 2f;
        return phase;
    }

    // One-pole low-pass; coefficient near 0 = darker, near 1 = brighter.
    private static float Lp(ref float state, float input, float coefficient)
    {
        state += (input - state) * coefficient;
        return state;
    }

    private static float Saturate(float x) => x / (1f + Mathf.Abs(x));

    private static float Saw(float cycles) => (cycles - Mathf.Floor(cycles)) * 2f - 1f;

    private static float Square(float phase) => Mathf.Sin(phase) >= 0f ? 1f : -1f;

    private static float Decay(float t, float rate) => Mathf.Exp(-t * rate);
}
