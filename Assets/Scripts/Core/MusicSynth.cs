using System;

// Renders the game's music and ambience loops from code. Pure math, no Unity objects, so it
// runs on a worker thread while the scene starts up.
//
// The music is one 8-bar piece in D minor at 100 BPM (Dm - Bb - Gm - A, two bars each), split
// into layers that loop in sync: a calm pad + music-box bells that always play, a drum and bass
// layer for when a wave is on, and taiko + brass stabs that join for boss waves. Notes that ring
// past the loop end wrap round to the start, so every layer loops seamlessly.
public static class MusicSynth
{
    public const int Rate = 22050;
    private const float Beat = 60f / 100f;
    private const int Bars = 8;
    private const float LoopSeconds = Bars * 4 * Beat;
    private const float AmbienceSeconds = 24f;
    private const float TwoPi = (float)(Math.PI * 2.0);

    private static readonly int[] ChordRoots = { 50, 46, 43, 45 }; // D3, Bb2, G2, A2
    private static readonly bool[] ChordMinor = { true, false, true, false };
    private const float ChordBeats = 8f;

    public class Tracks
    {
        public float[] ambience;
        public float[] calm;
        public float[] combat;
        public float[] boss;
    }

    public static Tracks RenderAll() => new Tracks
    {
        ambience = Ambience(),
        calm = Calm(),
        combat = Combat(),
        boss = Boss(),
    };

    // ---------------- Calm: pad, sub drone, music-box bells ----------------

    private static float[] Calm()
    {
        var buf = NewLoop(LoopSeconds);

        for (int c = 0; c < ChordRoots.Length; c++)
        {
            float start = c * ChordBeats * Beat;
            float hold = ChordBeats * Beat;
            int root = ChordRoots[c];
            int third = root + (ChordMinor[c] ? 3 : 4);
            foreach (int note in new[] { root, third, root + 7, third + 12 }) Pad(buf, start, hold, Freq(note), 0.09f);
            Drone(buf, start, hold, Freq(root - 12), 0.22f);
        }

        // Sparse eerie music-box line, one phrase per chord: (beat, MIDI note).
        float[,] bells =
        {
            { 0f, 81 }, { 1.5f, 77 }, { 3f, 74 }, { 6f, 76 },
            { 8f, 82 }, { 9.5f, 77 }, { 11f, 74 }, { 14f, 72 },
            { 16f, 79 }, { 17.5f, 74 }, { 19f, 70 }, { 22f, 74 },
            { 24f, 76 }, { 25.5f, 73 }, { 27f, 69 }, { 29f, 64 },
        };
        for (int i = 0; i < bells.GetLength(0); i++) Bell(buf, bells[i, 0] * Beat, Freq((int)bells[i, 1]), 0.07f);

        return Normalize(buf, 0.85f);
    }

    private static void Pad(float[] buf, float start, float hold, float freq, float amp)
    {
        const float release = 2f;
        float p1 = 0f, p2 = 0f, p3 = 0f, lp1 = 0f, lp2 = 0f;
        Add(buf, start, hold + release, t =>
        {
            p1 += freq / Rate;
            p2 += freq * 1.007f / Rate;
            p3 += freq * 0.993f / Rate;
            float raw = (Saw(p1) + Saw(p2) + Saw(p3)) / 3f;
            float cutoff = 650f + 350f * (float)Math.Sin(TwoPi * (start + t) / LoopSeconds * 2f);
            float a = Coef(cutoff);
            lp1 += (raw - lp1) * a;
            lp2 += (lp1 - lp2) * a;
            return lp2 * Attack(t, 1.4f) * Release(t, hold, release) * amp;
        });
    }

    private static void Drone(float[] buf, float start, float hold, float freq, float amp)
    {
        const float release = 2f;
        float p = 0f;
        Add(buf, start, hold + release, t =>
        {
            p += freq / Rate;
            float s = (float)Math.Sin(TwoPi * p) + 0.3f * (float)Math.Sin(TwoPi * p * 2f);
            return s * Attack(t, 1f) * Release(t, hold, release) * amp;
        });
    }

    private static void Bell(float[] buf, float start, float freq, float amp)
    {
        float p = 0f;
        Add(buf, start, 3f, t =>
        {
            p += freq / Rate;
            float s = (float)Math.Sin(TwoPi * p) + 0.35f * (float)Math.Sin(TwoPi * p * 2.76f) * Decay(t, 6f);
            return s * Attack(t, 0.004f) * Decay(t, 1.6f) * amp;
        });
    }

    // ---------------- Combat: kick, snare, hats, driving bass ----------------

    private static float[] Combat()
    {
        var buf = NewLoop(LoopSeconds);
        var rng = new Random(23);
        int[] bassPattern = { 0, 0, 12, 0, 0, 0, 7, 0 };

        for (int bar = 0; bar < Bars; bar++)
        {
            float barStart = bar * 4;
            Kick(buf, barStart * Beat, 0.9f);
            Kick(buf, (barStart + 2) * Beat, 0.9f);
            if (bar % 2 == 1) Kick(buf, (barStart + 3.5f) * Beat, 0.6f);

            Snare(buf, (barStart + 1) * Beat, 0.55f, rng);
            Snare(buf, (barStart + 3) * Beat, 0.55f, rng);

            // Hats tighten to 16ths for the second half, then a snare fill into the loop.
            float step = bar >= 4 ? 0.25f : 0.5f;
            for (float b = 0f; b < 4f; b += step)
            {
                bool offbeat = Math.Abs(b % 1f - 0.5f) < 0.01f;
                Hat(buf, (barStart + b) * Beat, offbeat ? 0.22f : 0.12f, rng);
            }
            if (bar == Bars - 1)
            {
                for (int i = 0; i < 4; i++) Snare(buf, (barStart + 3f + i * 0.25f) * Beat, 0.25f + i * 0.08f, rng);
            }

            int root = ChordRoots[bar / 2] - 12;
            for (int i = 0; i < 8; i++) Bass(buf, (barStart + i * 0.5f) * Beat, Freq(root + bassPattern[i]), i == 0 ? 0.5f : 0.38f);
        }

        return Normalize(buf, 0.9f);
    }

    private static void Kick(float[] buf, float start, float amp)
    {
        float p = 0f;
        Add(buf, start, 0.45f, t =>
        {
            float freq = 45f + 85f * Decay(t, 30f);
            p += freq / Rate;
            float click = t < 0.004f ? 0.5f : 0f;
            return ((float)Math.Sin(TwoPi * p) * Decay(t, 9f) + click) * amp;
        });
    }

    private static void Snare(float[] buf, float start, float amp, Random rng)
    {
        float p = 0f, lp = 0f;
        Add(buf, start, 0.3f, t =>
        {
            p += 190f / Rate;
            float n = Noise(rng);
            lp += (n - lp) * 0.45f;
            return (lp * Decay(t, 20f) + (float)Math.Sin(TwoPi * p) * Decay(t, 30f) * 0.5f) * amp;
        });
    }

    private static void Hat(float[] buf, float start, float amp, Random rng)
    {
        float lp = 0f;
        Add(buf, start, 0.08f, t =>
        {
            float n = Noise(rng);
            lp += (n - lp) * 0.5f;
            return (n - lp) * Decay(t, 70f) * amp;
        });
    }

    private static void Bass(float[] buf, float start, float freq, float amp)
    {
        float p = 0f, lp = 0f;
        Add(buf, start, 0.32f, t =>
        {
            p += freq / Rate;
            float raw = Saw(p) * 0.7f + ((p - Math.Floor(p)) < 0.5 ? 0.3f : -0.3f);
            lp += (raw - lp) * Coef(180f + 1100f * Decay(t, 14f));
            return lp * Attack(t, 0.005f) * Release(t, 0.24f, 0.06f) * amp;
        });
    }

    // ---------------- Boss: taiko, brass stabs, string tremolo ----------------

    private static float[] Boss()
    {
        var buf = NewLoop(LoopSeconds);
        var rng = new Random(57);

        for (int bar = 0; bar < Bars; bar++)
        {
            float barStart = bar * 4;
            Taiko(buf, barStart * Beat, 1f, rng);
            Taiko(buf, (barStart + 2.5f) * Beat, 0.7f, rng);
            if (bar % 2 == 1)
            {
                Taiko(buf, (barStart + 3.25f) * Beat, 0.55f, rng);
                Taiko(buf, (barStart + 3.5f) * Beat, 0.75f, rng);
            }
        }

        for (int c = 0; c < ChordRoots.Length; c++)
        {
            float start = c * ChordBeats * Beat;
            int root = ChordRoots[c];
            Brass(buf, start, 2.4f, root, 0.32f);
            Brass(buf, start + 6f * Beat, 0.7f, root, 0.24f);

            int top = root + 19; // fifth, an octave up
            Tremolo(buf, start, ChordBeats * Beat, Freq(top), 0.07f);
            Tremolo(buf, start, ChordBeats * Beat, Freq(top + (ChordMinor[c] ? 3 : 4) - 7), 0.05f);
        }

        return Normalize(buf, 0.9f);
    }

    private static void Taiko(float[] buf, float start, float amp, Random rng)
    {
        float p = 0f, lp = 0f;
        Add(buf, start, 1.1f, t =>
        {
            float freq = 42f + 38f * Decay(t, 12f);
            p += freq / Rate;
            float n = Noise(rng);
            lp += (n - lp) * 0.12f;
            return ((float)Math.Sin(TwoPi * p) * Decay(t, 4.5f) + lp * Decay(t, 14f) * 1.8f) * amp;
        });
    }

    private static void Brass(float[] buf, float start, float hold, int root, float amp)
    {
        const float release = 0.5f;
        float[] freqs = { Freq(root - 12), Freq(root - 5), Freq(root), Freq(root) * 1.006f };
        var phases = new float[freqs.Length];
        float lp1 = 0f, lp2 = 0f;
        Add(buf, start, hold + release, t =>
        {
            float raw = 0f;
            for (int i = 0; i < freqs.Length; i++)
            {
                phases[i] += freqs[i] / Rate;
                raw += Saw(phases[i]);
            }
            raw /= freqs.Length;
            float swell = Math.Min(1f, t / 0.12f) * (0.55f + 0.45f * Decay(t, 2.5f));
            float a = Coef(200f + 1600f * swell);
            lp1 += (raw - lp1) * a;
            lp2 += (lp1 - lp2) * a;
            float s = lp2 * 2.2f;
            return s / (1f + Math.Abs(s)) * Release(t, hold, release) * amp;
        });
    }

    private static void Tremolo(float[] buf, float start, float hold, float freq, float amp)
    {
        float p = 0f, lp = 0f;
        float sixteenth = Beat / 4f;
        Add(buf, start, hold, t =>
        {
            p += freq / Rate;
            lp += (Saw(p) - lp) * Coef(2400f);
            float pos = t / sixteenth;
            float pulse = 1f - (pos - (float)Math.Floor(pos));
            return lp * pulse * pulse * Attack(t, 0.3f) * Release(t, hold - 0.1f, 0.1f) * amp;
        });
    }

    // ---------------- Ambience: wind, crickets, a distant owl ----------------

    private static float[] Ambience()
    {
        int loop = (int)(AmbienceSeconds * Rate);
        int fade = Rate; // 1 s crossfade at the seam
        var raw = new float[loop + fade];
        var rng = new Random(91);

        float b1 = 0f, b2 = 0f, w1 = 0f, w2 = 0f;
        for (int i = 0; i < raw.Length; i++)
        {
            float t = i / (float)Rate;
            float n = Noise(rng);
            b1 += (n - b1) * 0.02f;
            b2 += (b1 - b2) * 0.05f;
            w1 += (n - w1) * Coef(520f);
            w2 += (n - w2) * Coef(260f);
            float gust = 0.55f + 0.25f * (float)Math.Sin(TwoPi * t / AmbienceSeconds * 2f) + 0.2f * (float)Math.Sin(TwoPi * t / AmbienceSeconds * 5f + 1f);
            float rustle = (w1 - w2) * (0.4f + 0.6f * gust * gust);
            raw[i] = b2 * 14f * gust + rustle * 2.2f;
        }

        var buf = new float[loop];
        for (int i = 0; i < loop; i++) buf[i] = raw[i];
        for (int i = 0; i < fade; i++)
        {
            float k = i / (float)fade;
            buf[i] = raw[i] * (float)Math.Sqrt(k) + raw[loop + i] * (float)Math.Sqrt(1f - k);
        }

        // Cricket chirps in little trains, swelling in and out over the loop.
        for (float t = 0f; t < AmbienceSeconds; t += 0.7f + (float)rng.NextDouble() * 0.5f)
        {
            float swell = 0.5f + 0.5f * (float)Math.Sin(TwoPi * t / AmbienceSeconds * 3f);
            if (swell < 0.25f) continue;
            for (int k = 0; k < 3; k++) Chirp(buf, t + k * 0.065f, 4300f, 0.035f * swell);
        }

        Owl(buf, 7f, 0.12f);
        Owl(buf, 18.5f, 0.09f);

        return Normalize(buf, 0.8f);
    }

    private static void Chirp(float[] buf, float start, float freq, float amp)
    {
        float p = 0f;
        Add(buf, start, 0.04f, t =>
        {
            p += freq / Rate;
            float env = (float)Math.Sin(Math.PI * t / 0.04f);
            return (float)Math.Sin(TwoPi * p) * env * amp;
        });
    }

    // "Hoo ... hoo-hoo", low and soft.
    private static void Owl(float[] buf, float start, float amp)
    {
        float[] offsets = { 0f, 0.75f, 1.05f };
        foreach (float offset in offsets)
        {
            float p = 0f;
            Add(buf, start + offset, 0.38f, t =>
            {
                float freq = 370f * (1f - 0.05f * t);
                p += freq / Rate;
                float env = (float)Math.Sin(Math.PI * t / 0.38f);
                return (float)Math.Sin(TwoPi * p) * env * env * amp;
            });
        }
    }

    // ---------------- Helpers ----------------

    private static float[] NewLoop(float seconds) => new float[(int)Math.Round(seconds * Rate)];

    // Mixes a voice into the loop; anything past the end wraps to the start.
    private static void Add(float[] buf, float startSeconds, float lengthSeconds, Func<float, float> voice)
    {
        int n = buf.Length;
        int start = (int)(startSeconds * Rate);
        int length = (int)(lengthSeconds * Rate);
        for (int i = 0; i < length; i++)
        {
            buf[(start + i) % n] += voice(i / (float)Rate);
        }
    }

    private static float[] Normalize(float[] buf, float peak)
    {
        float max = 1e-6f;
        foreach (float s in buf) max = Math.Max(max, Math.Abs(s));
        float gain = peak / max;
        for (int i = 0; i < buf.Length; i++) buf[i] *= gain;
        return buf;
    }

    private static float Freq(int midi) => 440f * (float)Math.Pow(2.0, (midi - 69) / 12.0);

    private static float Coef(float cutoffHz) => Math.Min(1f, TwoPi * cutoffHz / Rate);

    private static float Saw(float cycles) => (cycles - (float)Math.Floor(cycles)) * 2f - 1f;

    private static float Noise(Random rng) => (float)(rng.NextDouble() * 2.0 - 1.0);

    private static float Decay(float t, float rate) => (float)Math.Exp(-t * rate);

    private static float Attack(float t, float seconds) => Math.Min(1f, t / seconds);

    private static float Release(float t, float hold, float release) => t < hold ? 1f : Math.Max(0f, 1f - (t - hold) / release);
}
