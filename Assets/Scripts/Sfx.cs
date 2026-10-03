using System.Collections.Generic;
using UnityEngine;

namespace LaundryMonster
{
    /// <summary>
    /// Every sound in the game, synthesised at runtime. No audio files, same as the
    /// art: the project still has zero imported assets.
    /// Clips are built once on first use and cached.
    /// </summary>
    public static class Sfx
    {
        const int SampleRate = 44100;
        static readonly Dictionary<string, AudioClip> Cache = new Dictionary<string, AudioClip>();

        // ---------- the sound set ----------

        /// <summary>Washer finished: a polite two-tone chime. It will repeat until you deal with it.</summary>
        public static AudioClip WasherDone => Sequence("washerDone",
            new[] { 880f, 1174f }, 0.11f, 0.35f, false);

        /// <summary>Dryer finished: lower and flatter, more industrial, more annoying.</summary>
        public static AudioClip DryerDone => Sequence("dryerDone",
            new[] { 392f, 392f }, 0.16f, 0.32f, true);

        /// <summary>The wrinkle clock, ticking. Pitch rises as it runs out.</summary>
        public static AudioClip Tick => Sequence("tick", new[] { 1600f }, 0.035f, 0.18f, true);

        /// <summary>A garment just wrinkled. Deflating.</summary>
        public static AudioClip Wrinkled => Sweep("wrinkled", 520f, 180f, 0.35f, 0.3f);

        /// <summary>Folded one. Should feel like progress.</summary>
        public static AudioClip Fold => Sequence("fold", new[] { 300f, 450f }, 0.06f, 0.3f, false);

        /// <summary>Put away. The only sound that means points.</summary>
        public static AudioClip Deliver => Sequence("deliver",
            new[] { 659f, 880f, 1319f }, 0.08f, 0.33f, false);

        public static AudioClip PickUp => Sequence("pickup", new[] { 520f }, 0.05f, 0.18f, false);
        public static AudioClip Drop => Sequence("drop", new[] { 240f }, 0.06f, 0.18f, false);
        public static AudioClip Start => Sequence("start", new[] { 300f, 380f }, 0.07f, 0.25f, false);

        /// <summary>A pocket disaster. Harsh on purpose.</summary>
        public static AudioClip Disaster => Sweep("disaster", 300f, 70f, 0.55f, 0.45f, true);

        /// <summary>Pockets checked, nothing found. Reassuring.</summary>
        public static AudioClip Safe => Sequence("safe", new[] { 700f, 900f }, 0.05f, 0.2f, false);

        public static AudioClip DayEnd => Sequence("dayEnd",
            new[] { 523f, 659f, 784f, 1047f }, 0.11f, 0.35f, false);

        public static AudioClip RunOver => Sweep("runOver", 400f, 55f, 1.1f, 0.45f, true);

        /// <summary>The dryer catches. Low, long and bad.</summary>
        public static AudioClip Fire => Sweep("fire", 190f, 42f, 0.95f, 0.5f, true);

        /// <summary>A soft footfall. Quiet on purpose - it plays twice a second.</summary>
        public static AudioClip Step => Sweep("step", 190f, 110f, 0.055f, 0.5f);

        /// <summary>Lint trap emptied. Quietly satisfying, never triumphant - it fixed nothing yet.</summary>
        public static AudioClip LintClear => Sequence("lintClear", new[] { 440f, 320f }, 0.07f, 0.22f, false);

        /// <summary>The Monster is reaching for The Chair. Low, rising, unmistakable.</summary>
        public static AudioClip Growl => Sweep("growl", 70f, 160f, 0.75f, 0.5f, true);

        /// <summary>It got one. Downward, final.</summary>
        public static AudioClip Snatch => Sweep("snatch", 420f, 90f, 0.4f, 0.5f, true);

        // ---------- synthesis ----------

        /// <summary>A run of tones back to back, each with a click-free envelope.</summary>
        static AudioClip Sequence(string name, float[] freqs, float segDur, float vol, bool square)
        {
            AudioClip cached;
            if (Cache.TryGetValue(name, out cached) && cached != null) return cached;

            int perSeg = Mathf.Max(1, Mathf.RoundToInt(SampleRate * segDur));
            var data = new float[perSeg * freqs.Length];

            for (int s = 0; s < freqs.Length; s++)
            {
                float f = freqs[s];
                for (int i = 0; i < perSeg; i++)
                {
                    float t = i / (float)SampleRate;
                    float raw = square
                        ? (Mathf.Sin(2f * Mathf.PI * f * t) >= 0f ? 1f : -1f)
                        : Mathf.Sin(2f * Mathf.PI * f * t);
                    data[s * perSeg + i] = raw * Envelope(i / (float)perSeg) * vol;
                }
            }

            var clip = AudioClip.Create(name, data.Length, 1, SampleRate, false);
            clip.SetData(data, 0);
            Cache[name] = clip;
            return clip;
        }

        /// <summary>A glide from one pitch to another. Used for the bad news.</summary>
        static AudioClip Sweep(string name, float from, float to, float dur, float vol, bool square = false)
        {
            AudioClip cached;
            if (Cache.TryGetValue(name, out cached) && cached != null) return cached;

            int n = Mathf.Max(1, Mathf.RoundToInt(SampleRate * dur));
            var data = new float[n];
            float phase = 0f;

            for (int i = 0; i < n; i++)
            {
                float u = i / (float)n;
                float f = Mathf.Lerp(from, to, u);
                phase += 2f * Mathf.PI * f / SampleRate;
                float raw = square ? (Mathf.Sin(phase) >= 0f ? 1f : -1f) : Mathf.Sin(phase);
                data[i] = raw * Envelope(u) * vol;
            }

            var clip = AudioClip.Create(name, n, 1, SampleRate, false);
            clip.SetData(data, 0);
            Cache[name] = clip;
            return clip;
        }

        /// <summary>Short attack, long decay. Without this every clip clicks at the edges.</summary>
        static float Envelope(float u)
        {
            const float attack = 0.06f;
            if (u < attack) return u / attack;
            return Mathf.Pow(1f - (u - attack) / (1f - attack), 1.6f);
        }
    }
}
