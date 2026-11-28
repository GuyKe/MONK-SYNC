using System;
using System.Collections.Generic;
using UnityEngine;

namespace Monk.Audio
{
    /// <summary>
    /// Synthesises every sound the map needs, so the project needs no audio files.
    /// Looping clips use only whole-number frequencies over a whole number of seconds, so they loop without clicks.
    /// </summary>
    public static class ProceduralAudio
    {
        private const int SampleRate = 44100;
        private const float Tau = Mathf.PI * 2f;
        private static readonly Dictionary<string, AudioClip> Cache = new Dictionary<string, AudioClip>();

        private static AudioClip Build(string name, float seconds, Func<int, float, float> sample)
        {
            if (Cache.TryGetValue(name, out var cached) && cached != null) return cached;

            int n = Mathf.RoundToInt(seconds * SampleRate);
            var data = new float[n];
            float peak = 0.0001f;
            for (int i = 0; i < n; i++)
            {
                float v = sample(i, i / (float)SampleRate);
                data[i] = v;
                peak = Mathf.Max(peak, Mathf.Abs(v));
            }
            float gain = 0.9f / peak;
            for (int i = 0; i < n; i++) data[i] *= gain;

            var clip = AudioClip.Create(name, n, 1, SampleRate, false);
            clip.SetData(data, 0);
            Cache[name] = clip;
            return clip;
        }

        private static float Sin(float f, float t, float phase = 0f) => Mathf.Sin(Tau * f * t + phase);

        /// <summary>Steady 60 Hz fluorescent buzz with harmonics. 1 s loop.</summary>
        public static AudioClip Hum() => Build("hum", 1f, (i, t) =>
            0.50f * Sin(60, t) + 0.32f * Sin(120, t, 0.4f) + 0.18f * Sin(180, t, 1.1f) +
            0.10f * Sin(240, t) + 0.12f * Sin(61, t) + 0.05f * Sin(420, t, 2f) + 0.02f * Sin(5700, t) + 0.015f * Sin(7260, t, 1f));

        /// <summary>Distant ventilation: low rumble plus airy hiss, slowly breathing. 4 s loop.</summary>
        public static AudioClip VentRumble()
        {
            var rng = new System.Random(7);
            var rumble = new float[24];
            var hiss = new float[12];
            for (int k = 0; k < rumble.Length; k++) rumble[k] = (float)rng.NextDouble() * Tau;
            for (int k = 0; k < hiss.Length; k++) hiss[k] = (float)rng.NextDouble() * Tau;

            return Build("vent", 4f, (i, t) =>
            {
                float low = 0f;
                for (int k = 0; k < rumble.Length; k++) low += Sin(30 + k * 7, t, rumble[k]) / (1f + k * 0.3f);
                float air = 0f;
                for (int k = 0; k < hiss.Length; k++) air += Sin(300 + k * 53, t, hiss[k]);
                float breath = 0.7f + 0.3f * Sin(0.25f, t);
                float swell = 0.5f + 0.5f * Sin(0.5f, t, 1f);
                return (low * 0.16f * breath + air * 0.05f * swell);
            });
        }

        /// <summary>Wet carpet squelch: low-passed noise plus a falling bubble.</summary>
        public static AudioClip Squelch()
        {
            var rng = new System.Random(11);
            float lp = 0f, phase = 0f;
            return Build("squelch", 0.28f, (i, t) =>
            {
                float noise = (float)rng.NextDouble() * 2f - 1f;
                lp += (noise - lp) * Mathf.Lerp(0.35f, 0.05f, t / 0.28f);
                float f = Mathf.Lerp(430f, 130f, Mathf.Clamp01(t / 0.2f));
                phase += Tau * f / SampleRate;
                return lp * 1.4f * Mathf.Exp(-t * 14f) + Mathf.Sin(phase) * 0.5f * Mathf.Exp(-t * 18f);
            });
        }

        /// <summary>Splash for shallow water.</summary>
        public static AudioClip Splash()
        {
            var rng = new System.Random(13);
            float lp = 0f;
            return Build("splash", 0.45f, (i, t) =>
            {
                float noise = (float)rng.NextDouble() * 2f - 1f;
                float hp = noise - lp;
                lp += (noise - lp) * 0.2f;
                return hp * Mathf.Exp(-t * 9f) * (0.6f + 0.4f * Mathf.Sin(t * 90f));
            });
        }

        /// <summary>Dull thud (concrete, wallpaper over drywall, crates).</summary>
        public static AudioClip Thud()
        {
            var rng = new System.Random(17);
            float phase = 0f;
            return Build("thud", 0.4f, (i, t) =>
            {
                float f = 35f + 60f * Mathf.Exp(-t * 18f);
                phase += Tau * f / SampleRate;
                float click = t < 0.006f ? ((float)rng.NextDouble() * 2f - 1f) * 0.6f : 0f;
                return Mathf.Sin(phase) * Mathf.Exp(-t * 9f) + click;
            });
        }

        /// <summary>Metallic clank.</summary>
        public static AudioClip Clank() => Build("clank", 1f, (i, t) =>
            0.5f * Sin(523, t) * Mathf.Exp(-t * 5f) + 0.4f * Sin(1180, t) * Mathf.Exp(-t * 7f) +
            0.3f * Sin(1710, t) * Mathf.Exp(-t * 9f) + 0.25f * Sin(2340, t) * Mathf.Exp(-t * 12f) +
            0.15f * Sin(3050, t) * Mathf.Exp(-t * 15f));

        /// <summary>Glass breaking: a burst of noise followed by many bright tinkles.</summary>
        public static AudioClip Shatter()
        {
            var rng = new System.Random(29);
            const int events = 48;
            var tk = new float[events]; var fk = new float[events]; var ak = new float[events];
            for (int k = 0; k < events; k++)
            {
                tk[k] = (float)rng.NextDouble() * 0.9f;
                fk[k] = 2500f + (float)rng.NextDouble() * 4500f;
                ak[k] = 0.2f + (float)rng.NextDouble() * 0.8f;
            }
            float lp = 0f;
            return Build("shatter", 1.3f, (i, t) =>
            {
                float noise = (float)rng.NextDouble() * 2f - 1f;
                float hp = noise - lp;
                lp += (noise - lp) * 0.25f;
                float s = hp * Mathf.Exp(-t * 12f) * 1.2f;
                for (int k = 0; k < events; k++)
                {
                    float dt = t - tk[k];
                    if (dt > 0f) s += ak[k] * 0.5f * Mathf.Sin(Tau * fk[k] * dt) * Mathf.Exp(-dt * 38f);
                }
                return s;
            });
        }

        public static AudioClip Beep() => Build("beep", 0.14f, (i, t) =>
            Sin(1500, t) * Mathf.Clamp01(t * 200f) * Mathf.Clamp01((0.14f - t) * 200f));

        public static AudioClip Click()
        {
            var rng = new System.Random(19);
            return Build("click", 0.06f, (i, t) =>
                (((float)rng.NextDouble() * 2f - 1f) * 0.5f + Sin(2000, t)) * Mathf.Exp(-t * 80f));
        }

        /// <summary>Heavy door slamming shut.</summary>
        public static AudioClip Slam()
        {
            var rng = new System.Random(23);
            float phase = 0f, lp = 0f;
            return Build("slam", 0.8f, (i, t) =>
            {
                float f = 28f + 50f * Mathf.Exp(-t * 14f);
                phase += Tau * f / SampleRate;
                float noise = (float)rng.NextDouble() * 2f - 1f;
                lp += (noise - lp) * 0.15f;
                return Mathf.Sin(phase) * Mathf.Exp(-t * 6f) + lp * 0.6f * Mathf.Exp(-t * 10f) +
                       0.25f * Sin(310, t) * Mathf.Exp(-t * 8f);
            });
        }

        /// <summary>Grinding of a heavy door on its rail. 1 s loop.</summary>
        public static AudioClip Rumble() => Build("rumble", 1f, (i, t) =>
            0.6f * Sin(42, t) + 0.4f * Sin(63, t, 1f) + 0.25f * Sin(97, t, 2f) + 0.12f * Sin(211, t, 0.5f) + 0.08f * Sin(433, t));
    }
}
