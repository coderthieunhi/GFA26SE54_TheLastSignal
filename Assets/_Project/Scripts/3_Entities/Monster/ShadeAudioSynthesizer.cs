using System;
using UnityEngine;

namespace TheLastSignal.Entities.Monster
{
    /// <summary>
    /// Procedural audio generator for The Shade (Monster/Hunter role).
    /// Synthesizes visceral claw slashes, flesh impacts, shadow dashes, and demonic growls
    /// directly in memory via AudioClip.Create, ensuring zero external audio file dependencies.
    /// </summary>
    public static class ShadeAudioSynthesizer
    {
        private static AudioClip clawSlashClip;
        private static AudioClip fleshImpactClip;
        private static AudioClip shadowDashClip;
        private static AudioClip shadeGrowlClip;
        private static AudioClip clawScrapeClip;
        private static AudioClip survivorPainClip;

        private const int SampleRate = 44100;

        /// <summary>Sharp cutting metallic whoosh with guttural predator snarl attack (0.24s).</summary>
        public static AudioClip GetClawSlashClip()
        {
            if (clawSlashClip == null)
            {
                float duration = 0.24f;
                int length = Mathf.FloorToInt(SampleRate * duration);
                float[] samples = new float[length];

                for (int i = 0; i < length; i++)
                {
                    float t = (float)i / SampleRate;
                    float progress = t / duration;

                    // Pitch drops rapidly: 980Hz -> 140Hz (cutting whoosh)
                    float freq = Mathf.Lerp(980f, 140f, progress);
                    float phase = 2f * Mathf.PI * freq * t;
                    float tone = Mathf.Sin(phase);

                    // Alien guttural rasp layer (120Hz frequency modulated)
                    float snarl = Mathf.Sin(2f * Mathf.PI * 120f * t * (1f + 0.3f * Mathf.Sin(2f * Mathf.PI * 40f * t)));

                    // High frequency tearing noise
                    float noise = (UnityEngine.Random.value * 2f - 1f) * Mathf.Exp(-progress * 12f);

                    // Exponential envelope
                    float env = Mathf.Sin(progress * Mathf.PI);
                    samples[i] = Mathf.Clamp((tone * 0.50f + snarl * 0.30f + noise * 0.35f) * env * 0.85f, -1f, 1f);
                }

                clawSlashClip = AudioClip.Create("Shade_ClawSlash", length, 1, SampleRate, false);
                clawSlashClip.SetData(samples, 0);
            }
            return clawSlashClip;
        }

        /// <summary>Brutal, wet flesh impact with bone-crunch distortion and deep sub-bass (0.30s).</summary>
        public static AudioClip GetFleshImpactClip()
        {
            if (fleshImpactClip == null)
            {
                float duration = 0.30f;
                int length = Mathf.FloorToInt(SampleRate * duration);
                float[] samples = new float[length];

                for (int i = 0; i < length; i++)
                {
                    float t = (float)i / SampleRate;
                    float progress = t / duration;

                    // Deep blunt sub-bass impact thud (50Hz)
                    float thud = Mathf.Sin(2f * Mathf.PI * 50f * t) * Mathf.Exp(-progress * 14f);

                    // Bone-crunch distortion crack
                    float crunch = (Mathf.Sin(2f * Mathf.PI * 380f * t) + (UnityEngine.Random.value * 2f - 1f) * 0.5f)
                                   * Mathf.Exp(-progress * 20f);

                    // Wet squelch splatter
                    float squelch = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(600f, 120f, progress) * t) * (UnityEngine.Random.value * 0.4f);

                    samples[i] = Mathf.Clamp((thud * 0.70f + crunch * 0.50f + squelch * 0.30f), -1f, 1f) * 0.90f;
                }

                fleshImpactClip = AudioClip.Create("Shade_FleshImpact", length, 1, SampleRate, false);
                fleshImpactClip.SetData(samples, 0);
            }
            return fleshImpactClip;
        }

        /// <summary>Sub-bass vacuum whoosh when dashing through shadows (0.32s).</summary>
        public static AudioClip GetShadowDashClip()
        {
            if (shadowDashClip == null)
            {
                float duration = 0.32f;
                int length = Mathf.FloorToInt(SampleRate * duration);
                float[] samples = new float[length];

                for (int i = 0; i < length; i++)
                {
                    float t = (float)i / SampleRate;
                    float progress = t / duration;

                    // Downward frequency sweep from 260Hz to 32Hz (sub-bass vacuum)
                    float freq = Mathf.Lerp(260f, 32f, progress);
                    float tone = Mathf.Sin(2f * Mathf.PI * freq * t);

                    // Low frequency rumble noise
                    float rumble = (UnityEngine.Random.value * 2f - 1f) * 0.30f * Mathf.Sin(progress * Mathf.PI);

                    // Swelling envelope
                    float env = Mathf.Sin(progress * Mathf.PI);
                    samples[i] = Mathf.Clamp((tone * 0.75f + rumble * 0.35f) * env * 0.95f, -1f, 1f);
                }

                shadowDashClip = AudioClip.Create("Shade_ShadowDash", length, 1, SampleRate, false);
                shadowDashClip.SetData(samples, 0);
            }
            return shadowDashClip;
        }

        /// <summary>Metal claw scraping floor brake sound upon dash landing (0.20s).</summary>
        public static AudioClip GetClawScrapeClip()
        {
            if (clawScrapeClip == null)
            {
                float duration = 0.20f;
                int length = Mathf.FloorToInt(SampleRate * duration);
                float[] samples = new float[length];

                for (int i = 0; i < length; i++)
                {
                    float t = (float)i / SampleRate;
                    float progress = t / duration;

                    // Metallic screech sweep 1200Hz -> 450Hz
                    float freq = Mathf.Lerp(1200f, 450f, progress);
                    float metal = Mathf.Sin(2f * Mathf.PI * freq * t) * (0.6f + 0.4f * (UnityEngine.Random.value * 2f - 1f));
                    float env = Mathf.Exp(-progress * 9f);

                    samples[i] = Mathf.Clamp(metal * env * 0.65f, -1f, 1f);
                }

                clawScrapeClip = AudioClip.Create("Shade_ClawScrape", length, 1, SampleRate, false);
                clawScrapeClip.SetData(samples, 0);
            }
            return clawScrapeClip;
        }

        /// <summary>Gasping pain groan when Survivor is violently struck (0.25s).</summary>
        public static AudioClip GetSurvivorPainClip()
        {
            if (survivorPainClip == null)
            {
                float duration = 0.25f;
                int length = Mathf.FloorToInt(SampleRate * duration);
                float[] samples = new float[length];

                for (int i = 0; i < length; i++)
                {
                    float t = (float)i / SampleRate;
                    float progress = t / duration;

                    // Human vocal formant groan (180Hz descending to 90Hz)
                    float vocal = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(180f, 90f, progress) * t);
                    vocal += 0.5f * Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(360f, 180f, progress) * t);
                    float breathNoise = (UnityEngine.Random.value * 2f - 1f) * 0.25f;

                    float env = Mathf.Sin(progress * Mathf.PI) * Mathf.Exp(-progress * 4f);
                    samples[i] = Mathf.Clamp((vocal * 0.7f + breathNoise * 0.3f) * env * 0.70f, -1f, 1f);
                }

                survivorPainClip = AudioClip.Create("Survivor_Pain", length, 1, SampleRate, false);
                survivorPainClip.SetData(samples, 0);
            }
            return survivorPainClip;
        }

        /// <summary>Low menacing guttural predator growl (0.55s).</summary>
        public static AudioClip GetShadeGrowlClip()
        {
            if (shadeGrowlClip == null)
            {
                float duration = 0.55f;
                int length = Mathf.FloorToInt(SampleRate * duration);
                float[] samples = new float[length];

                for (int i = 0; i < length; i++)
                {
                    float t = (float)i / SampleRate;
                    float progress = t / duration;

                    // 55Hz modulated by 18Hz tremolo
                    float tremolo = 0.6f + 0.4f * Mathf.Sin(2f * Mathf.PI * 18f * t);
                    float growl = Mathf.Sin(2f * Mathf.PI * 55f * t) * tremolo;

                    float env = Mathf.Sin(progress * Mathf.PI);
                    samples[i] = growl * env * 0.6f;
                }

                shadeGrowlClip = AudioClip.Create("Shade_Growl", length, 1, SampleRate, false);
                shadeGrowlClip.SetData(samples, 0);
            }
            return shadeGrowlClip;
        }
    }
}
