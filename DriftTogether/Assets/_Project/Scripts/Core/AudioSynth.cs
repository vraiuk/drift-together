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
            // Два аккорда, плавно перетекающих по лупу: Am9 → Fmaj7.
            float[] chordA = { 110f, 130.81f, 164.81f, 196f, 246.94f };
            float[] chordB = { 87.31f, 130.81f, 174.61f, 220f, 261.63f };
            float[] amps = { 0.5f, 0.35f, 0.4f, 0.22f, 0.14f };
            for (int i = 0; i < buf.Length; i++)
            {
                float t = (float)i / SampleRate;
                float phaseLoop = t / seconds * 2f * Mathf.PI; // loop-safe LFOs
                float blend = 0.5f + 0.5f * Mathf.Sin(phaseLoop - Mathf.PI * 0.5f); // 0→1→0
                float sample = 0f;
                for (int v = 0; v < amps.Length; v++)
                {
                    float vib = 1f + 0.002f * Mathf.Sin(phaseLoop * (2 + v));
                    float trem = 0.75f + 0.25f * Mathf.Sin(phaseLoop * (1 + v) + v * 1.7f);
                    sample += amps[v] * (1f - blend) * trem * Mathf.Sin(2f * Mathf.PI * chordA[v] * vib * t);
                    sample += amps[v] * blend * trem * Mathf.Sin(2f * Mathf.PI * chordB[v] * vib * t);
                    sample += amps[v] * 0.13f * trem * Mathf.Sin(2f * Mathf.PI * chordA[v] * 2f * t + v);
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

        /// <summary>Ночной лес: редкие птичьи трели и сверчки, разреженный луп ~24 c.</summary>
        public static AudioClip CreateForestLife()
        {
            float seconds = 24f;
            var buf = NewBuffer(seconds);

            // Сверчки: ритмичные высокие стрекотания в двух «гнёздах» лупа.
            for (int burst = 0; burst < 26; burst++)
            {
                int start = _rng.Next(buf.Length - 6000);
                float baseFreq = 3800f + (float)_rng.NextDouble() * 900f;
                int chirps = 3 + _rng.Next(3);
                for (int c = 0; c < chirps; c++)
                {
                    int cs = start + c * 2600;
                    for (int i = 0; i < 1600 && cs + i < buf.Length; i++)
                    {
                        float t = (float)i / SampleRate;
                        float env = Mathf.Sin(Mathf.Clamp01(i / 1600f) * Mathf.PI);
                        buf[cs + i] += Mathf.Sin(2f * Mathf.PI * baseFreq * t) *
                                       env * 0.045f * (0.6f + 0.4f * Mathf.Sin(t * 240f));
                    }
                }
            }

            // Птицы: редкие двух-трёхнотные свисты со скольжением.
            for (int call = 0; call < 6; call++)
            {
                int start = _rng.Next(buf.Length - 30000);
                int notes = 2 + _rng.Next(2);
                for (int n = 0; n < notes; n++)
                {
                    int ns = start + n * 7000;
                    float f0 = 1400f + (float)_rng.NextDouble() * 900f;
                    float f1 = f0 * (0.85f + (float)_rng.NextDouble() * 0.4f);
                    for (int i = 0; i < 5200 && ns + i < buf.Length; i++)
                    {
                        float t = (float)i / SampleRate;
                        float k = i / 5200f;
                        float env = Mathf.Sin(Mathf.Clamp01(k) * Mathf.PI);
                        float freq = Mathf.Lerp(f0, f1, k);
                        buf[ns + i] += Mathf.Sin(2f * Mathf.PI * freq * t) * env * 0.09f;
                    }
                }
            }

            FadeLoopSeam(buf);
            Normalize(buf, 0.3f);
            return MakeClip("ForestLife", buf);
        }

        /// <summary>Бурная вода порогов: яркий шум с клокотанием (луп 5 c).</summary>
        public static AudioClip CreateRapidsLoop()
        {
            float seconds = 5f;
            var buf = NewBuffer(seconds);
            float lp = 0f;
            float bubblePhase = 0f;
            for (int i = 0; i < buf.Length; i++)
            {
                float t = (float)i / SampleRate;
                float phaseLoop = t / seconds * 2f * Mathf.PI;
                lp = Mathf.Lerp(lp, NextNoise(), 0.35f); // ярче обычной воды
                float churn = 0.75f + 0.25f * Mathf.Sin(phaseLoop * 5f + Mathf.Sin(phaseLoop * 3f));
                bubblePhase += 0.0004f + Mathf.Abs(NextNoise()) * 0.0002f;
                float bubbles = Mathf.Sin(bubblePhase * 900f) * Mathf.Abs(NextNoise()) * 0.25f;
                buf[i] = lp * churn + bubbles;
            }
            FadeLoopSeam(buf);
            Normalize(buf, 0.45f);
            return MakeClip("RapidsLoop", buf);
        }

        /// <summary>Рёв водопада: плотный низкий гул + шипение (луп 6 c, 3D-источник).</summary>
        public static AudioClip CreateWaterfallRoar()
        {
            float seconds = 6f;
            var buf = NewBuffer(seconds);
            float brown = 0f, lp = 0f;
            for (int i = 0; i < buf.Length; i++)
            {
                float t = (float)i / SampleRate;
                float phaseLoop = t / seconds * 2f * Mathf.PI;
                brown = Mathf.Clamp(brown + NextNoise() * 0.05f, -1f, 1f) * 0.995f;
                lp = Mathf.Lerp(lp, NextNoise(), 0.6f);
                float swell = 0.85f + 0.15f * Mathf.Sin(phaseLoop * 2f);
                buf[i] = brown * 0.85f * swell + lp * 0.3f;
            }
            FadeLoopSeam(buf);
            Normalize(buf, 0.85f);
            return MakeClip("WaterfallRoar", buf);
        }

        /// <summary>Шаг по брёвнам плота: короткий деревянный стук.</summary>
        public static AudioClip CreateFootstep()
        {
            float seconds = 0.14f;
            var buf = NewBuffer(seconds);
            for (int i = 0; i < buf.Length; i++)
            {
                float t = (float)i / SampleRate;
                float knock = Mathf.Sin(2f * Mathf.PI * (150f - 60f * t) * t) * Mathf.Exp(-t * 40f);
                float wood = Mathf.Sin(2f * Mathf.PI * 520f * t) * Mathf.Exp(-t * 70f) * 0.35f;
                buf[i] = knock + wood + NextNoise() * Mathf.Exp(-t * 90f) * 0.1f;
            }
            Normalize(buf, 0.28f);
            return MakeClip("Footstep", buf);
        }

        /// <summary>Падение в воду всем телом: большой всплеск.</summary>
        public static AudioClip CreateBigSplash()
        {
            float seconds = 1.1f;
            var buf = NewBuffer(seconds);
            float lp = 0f;
            for (int i = 0; i < buf.Length; i++)
            {
                float t = (float)i / SampleRate;
                float plunge = Mathf.Sin(2f * Mathf.PI * (90f - 55f * t) * t) * Mathf.Exp(-t * 7f);
                lp = Mathf.Lerp(lp, NextNoise(), 0.4f);
                float spray = lp * Mathf.Exp(-t * 4.5f) * (t < 0.06f ? t / 0.06f : 1f);
                float drips = t > 0.35f
                    ? Mathf.Sin(2f * Mathf.PI * 900f * t) * Mathf.Abs(NextNoise()) * Mathf.Exp(-(t - 0.35f) * 6f) * 0.12f
                    : 0f;
                buf[i] = plunge * 0.8f + spray * 0.9f + drips;
            }
            Normalize(buf, 0.7f);
            return MakeClip("BigSplash", buf);
        }

        /// <summary>Заброс удочки: свист лески и бульк поплавка.</summary>
        public static AudioClip CreateCastPlop()
        {
            float seconds = 0.75f;
            var buf = NewBuffer(seconds);
            float lp = 0f;
            for (int i = 0; i < buf.Length; i++)
            {
                float t = (float)i / SampleRate;
                // свист (0–0.3 c)
                lp = Mathf.Lerp(lp, NextNoise(), 0.55f);
                float whip = t < 0.3f ? lp * Mathf.Sin(t / 0.3f * Mathf.PI) * 0.35f : 0f;
                // бульк (0.42 c)
                float plop = 0f;
                if (t > 0.42f)
                {
                    float pt = t - 0.42f;
                    plop = Mathf.Sin(2f * Mathf.PI * (320f + 480f * pt) * pt) * Mathf.Exp(-pt * 22f) * 0.8f;
                }
                buf[i] = whip + plop;
            }
            Normalize(buf, 0.45f);
            return MakeClip("CastPlop", buf);
        }

        /// <summary>Поклёвка: короткий двойной бульк.</summary>
        public static AudioClip CreateBiteBlup()
        {
            float seconds = 0.4f;
            var buf = NewBuffer(seconds);
            for (int i = 0; i < buf.Length; i++)
            {
                float t = (float)i / SampleRate;
                float b1 = Mathf.Sin(2f * Mathf.PI * (260f + 300f * t) * t) * Mathf.Exp(-t * 26f);
                float b2 = t > 0.16f
                    ? Mathf.Sin(2f * Mathf.PI * (300f + 380f * (t - 0.16f)) * (t - 0.16f)) *
                      Mathf.Exp(-(t - 0.16f) * 26f)
                    : 0f;
                buf[i] = b1 * 0.8f + b2;
            }
            Normalize(buf, 0.5f);
            return MakeClip("BiteBlup", buf);
        }

        /// <summary>Якорная цепь: серия металлических щелчков с затуханием.</summary>
        public static AudioClip CreateChainRattle()
        {
            float seconds = 0.9f;
            var buf = NewBuffer(seconds);
            for (int link = 0; link < 9; link++)
            {
                int start = (int)(link * 0.09f * SampleRate) + _rng.Next(900);
                float amp = 1f - link * 0.08f;
                float freq = 1900f + (float)_rng.NextDouble() * 700f;
                for (int i = 0; i < 1800 && start + i < buf.Length; i++)
                {
                    float t = (float)i / SampleRate;
                    buf[start + i] += (Mathf.Sin(2f * Mathf.PI * freq * t) * 0.6f +
                                       Mathf.Sin(2f * Mathf.PI * freq * 1.51f * t) * 0.3f) *
                                      Mathf.Exp(-t * 55f) * amp;
                }
            }
            Normalize(buf, 0.4f);
            return MakeClip("ChainRattle", buf);
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
