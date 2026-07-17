using UnityEngine;

namespace DriftTogether.Core
{
    /// <summary>
    /// Procedurally synthesizes every sound in the game at startup, so the
    /// project needs no external audio assets at all.
    /// </summary>
    public static class AudioSynth
    {
        const int SampleRate = 44100;

        static System.Random _rng = new System.Random(12345);

        static float NextNoise() => (float)(_rng.NextDouble() * 2.0 - 1.0);

        static AudioClip MakeClip(string name, float[] samples)
        {
            var clip = AudioClip.Create(name, samples.Length, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        static float[] NewBuffer(float seconds) => new float[(int)(seconds * SampleRate)];

        static void Normalize(float[] buf, float peak)
        {
            float max = 0f;
            for (int i = 0; i < buf.Length; i++)
                max = Mathf.Max(max, Mathf.Abs(buf[i]));
            if (max < 1e-5f)
                return;
            float k = peak / max;
            for (int i = 0; i < buf.Length; i++)
                buf[i] *= k;
        }

        /// <summary>Calm ambient pad: slowly evolving minor chord, seamless loop (~16 s).</summary>
        public static AudioClip CreateAmbientMusic()
        {
            float seconds = 16f;
            var buf = NewBuffer(seconds);
            // A minor-ish pad: A2, C3, E3, G3 with slow detune and tremolo.
            float[] freqs = { 110f, 130.81f, 164.81f, 196f };
            float[] amps = { 0.5f, 0.35f, 0.4f, 0.22f };
            for (int i = 0; i < buf.Length; i++)
            {
                float t = (float)i / SampleRate;
                float phaseLoop = t / seconds * 2f * Mathf.PI; // for loop-safe LFOs
                float sample = 0f;
                for (int v = 0; v < freqs.Length; v++)
                {
                    float vib = 1f + 0.002f * Mathf.Sin(phaseLoop * (2 + v));
                    float trem = 0.75f + 0.25f * Mathf.Sin(phaseLoop * (1 + v) + v * 1.7f);
                    sample += amps[v] * trem * Mathf.Sin(2f * Mathf.PI * freqs[v] * vib * t);
                    // soft octave shimmer
                    sample += amps[v] * 0.15f * trem * Mathf.Sin(2f * Mathf.PI * freqs[v] * 2f * t + v);
                }
                buf[i] = sample;
            }
            Normalize(buf, 0.35f);
            return MakeClip("AmbientPad", buf);
        }

        /// <summary>Flowing water: low-passed noise with gentle loop-safe swell (~6 s loop).</summary>
        public static AudioClip CreateWaterLoop()
        {
            float seconds = 6f;
            var buf = NewBuffer(seconds);
            float lp = 0f, lp2 = 0f;
            for (int i = 0; i < buf.Length; i++)
            {
                float t = (float)i / SampleRate;
                float phaseLoop = t / seconds * 2f * Mathf.PI;
                lp = Mathf.Lerp(lp, NextNoise(), 0.08f);
                lp2 = Mathf.Lerp(lp2, lp, 0.15f);
                float swell = 0.8f + 0.2f * Mathf.Sin(phaseLoop * 3f);
                buf[i] = lp2 * swell;
            }
            FadeLoopSeam(buf);
            Normalize(buf, 0.4f);
            return MakeClip("WaterLoop", buf);
        }

        /// <summary>Campfire crackle: brown noise bed plus random pops (~5 s loop).</summary>
        public static AudioClip CreateCampfireLoop()
        {
            float seconds = 5f;
            var buf = NewBuffer(seconds);
            float brown = 0f;
            for (int i = 0; i < buf.Length; i++)
            {
                brown = Mathf.Clamp(brown + NextNoise() * 0.02f, -1f, 1f) * 0.998f;
                buf[i] = brown * 0.8f;
            }
            // pops
            int pops = 70;
            for (int p = 0; p < pops; p++)
            {
                int start = _rng.Next(buf.Length - 2000);
                float amp = 0.3f + (float)_rng.NextDouble() * 0.6f;
                int len = 200 + _rng.Next(900);
                for (int i = 0; i < len && start + i < buf.Length; i++)
                {
                    float env = Mathf.Exp(-i / (len * 0.18f));
                    buf[start + i] += NextNoise() * amp * env;
                }
            }
            FadeLoopSeam(buf);
            Normalize(buf, 0.5f);
            return MakeClip("CampfireLoop", buf);
        }

        /// <summary>Single paddle stroke: watery whoosh with a soft plop.</summary>
        public static AudioClip CreatePaddleStroke()
        {
            float seconds = 0.5f;
            var buf = NewBuffer(seconds);
            float lp = 0f;
            for (int i = 0; i < buf.Length; i++)
            {
                float t = (float)i / SampleRate;
                float env = Mathf.Sin(Mathf.Clamp01(t / seconds) * Mathf.PI);
                env *= Mathf.Exp(-t * 5f);
                lp = Mathf.Lerp(lp, NextNoise(), 0.25f);
                float plop = 0.5f * Mathf.Sin(2f * Mathf.PI * (180f - 120f * t) * t) * Mathf.Exp(-t * 14f);
                buf[i] = lp * env * 0.9f + plop;
            }
            Normalize(buf, 0.5f);
            return MakeClip("PaddleStroke", buf);
        }

        /// <summary>Hard collision: low thud plus woody knock.</summary>
        public static AudioClip CreateCollision()
        {
            float seconds = 0.6f;
            var buf = NewBuffer(seconds);
            for (int i = 0; i < buf.Length; i++)
            {
                float t = (float)i / SampleRate;
                float thud = Mathf.Sin(2f * Mathf.PI * (70f - 25f * t) * t) * Mathf.Exp(-t * 9f);
                float knock = Mathf.Sin(2f * Mathf.PI * 420f * t) * Mathf.Exp(-t * 30f) * 0.5f;
                float crunch = NextNoise() * Mathf.Exp(-t * 25f) * 0.35f;
                buf[i] = thud + knock + crunch;
            }
            Normalize(buf, 0.75f);
            return MakeClip("Collision", buf);
        }

        /// <summary>Soft bump for weak scrapes.</summary>
        public static AudioClip CreateSoftBump()
        {
            float seconds = 0.3f;
            var buf = NewBuffer(seconds);
            for (int i = 0; i < buf.Length; i++)
            {
                float t = (float)i / SampleRate;
                float thud = Mathf.Sin(2f * Mathf.PI * 95f * t) * Mathf.Exp(-t * 16f);
                buf[i] = thud + NextNoise() * Mathf.Exp(-t * 40f) * 0.15f;
            }
            Normalize(buf, 0.4f);
            return MakeClip("SoftBump", buf);
        }

        /// <summary>Mushroom pickup: warm two-note chime.</summary>
        public static AudioClip CreateMushroomChime()
        {
            float seconds = 0.9f;
            var buf = NewBuffer(seconds);
            for (int i = 0; i < buf.Length; i++)
            {
                float t = (float)i / SampleRate;
                float a = Mathf.Sin(2f * Mathf.PI * 659.26f * t) * Mathf.Exp(-t * 5f);
                float b = t > 0.12f
                    ? Mathf.Sin(2f * Mathf.PI * 987.77f * (t - 0.12f)) * Mathf.Exp(-(t - 0.12f) * 5f)
                    : 0f;
                float shimmer = Mathf.Sin(2f * Mathf.PI * 1975.5f * t) * Mathf.Exp(-t * 8f) * 0.2f;
                buf[i] = a * 0.6f + b * 0.55f + shimmer;
            }
            Normalize(buf, 0.5f);
            return MakeClip("MushroomChime", buf);
        }

        /// <summary>Checkpoint / campfire rest: cozy low chime.</summary>
        public static AudioClip CreateCampfireRest()
        {
            float seconds = 1.4f;
            var buf = NewBuffer(seconds);
            float[] notes = { 329.63f, 392f, 493.88f };
            for (int i = 0; i < buf.Length; i++)
            {
                float t = (float)i / SampleRate;
                float s = 0f;
                for (int n = 0; n < notes.Length; n++)
                {
                    float start = n * 0.18f;
                    if (t > start)
                        s += Mathf.Sin(2f * Mathf.PI * notes[n] * (t - start)) * Mathf.Exp(-(t - start) * 3.5f);
                }
                buf[i] = s * 0.4f;
            }
            Normalize(buf, 0.45f);
            return MakeClip("CampfireRest", buf);
        }

        /// <summary>Finish fanfare: gentle rising arpeggio.</summary>
        public static AudioClip CreateFinish()
        {
            float seconds = 2.2f;
            var buf = NewBuffer(seconds);
            float[] notes = { 440f, 554.37f, 659.26f, 880f };
            for (int i = 0; i < buf.Length; i++)
            {
                float t = (float)i / SampleRate;
                float s = 0f;
                for (int n = 0; n < notes.Length; n++)
                {
                    float start = n * 0.22f;
                    if (t > start)
                    {
                        float lt = t - start;
                        s += Mathf.Sin(2f * Mathf.PI * notes[n] * lt) * Mathf.Exp(-lt * 2.2f) * (0.5f + n * 0.12f);
                    }
                }
                buf[i] = s * 0.35f;
            }
            Normalize(buf, 0.5f);
            return MakeClip("FinishFanfare", buf);
        }

        /// <summary>UI click.</summary>
        public static AudioClip CreateClick()
        {
            float seconds = 0.12f;
            var buf = NewBuffer(seconds);
            for (int i = 0; i < buf.Length; i++)
            {
                float t = (float)i / SampleRate;
                buf[i] = Mathf.Sin(2f * Mathf.PI * 750f * t) * Mathf.Exp(-t * 45f);
            }
            Normalize(buf, 0.35f);
            return MakeClip("UIClick", buf);
        }

        /// <summary>Fades the loop edges to zero so looping clips do not click at the seam.</summary>
        static void FadeLoopSeam(float[] buf)
        {
            int fade = Mathf.Min(4000, buf.Length / 8);
            for (int i = 0; i < fade; i++)
            {
                float k = (float)i / fade;
                buf[i] *= k;
                buf[buf.Length - 1 - i] *= k;
            }
        }
    }
}
