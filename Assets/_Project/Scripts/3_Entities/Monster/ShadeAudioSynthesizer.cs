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

        private const int SampleRate = 44100;

        /// <summary>Sharp cutting metallic whoosh of the claw slash (0.22s).</summary>
        public static AudioClip GetClawSlashClip()
        {
            if (clawSlashClip == null)
            {
                float duration = 0.22f;
                int length = Mathf.FloorToInt(SampleRate * duration);
                float[] samples = new float[length];

                for (int i = 0; i < length; i++)
                {
                    float t = (float)i / SampleRate;
                    float progress = t / duration;

                    // Pitch drops rapidly: 900Hz -> 180Hz (cutting whoosh)
                    float freq = Mathf.Lerp(900f, 180f, progress);
                    float phase = 2f * Mathf.PI * freq * t;
                    float tone = Mathf.Sin(phase);

                    // High frequency white noise attack for tearing sound
                    float noise = (UnityEngine.Random.value * 2f - 1f) * Mathf.Exp(-progress * 14f);

                    // Sharp exponential attack and decay
                    float env = Mathf.Sin(progress * Mathf.PI);
                    samples[i] = (tone * 0.65f + noise * 0.35f) * env * 0.75f;
                }

                clawSlashClip = AudioClip.Create("Shade_ClawSlash", length, 1, SampleRate, false);
                clawSlashClip.SetData(samples, 0);
            }
            return clawSlashClip;
        }

        /// <summary>Brutal, wet flesh impact when slash hits a Survivor (0.28s).</summary>
        public static AudioClip GetFleshImpactClip()
        {
            if (fleshImpactClip == null)
            {
                float duration = 0.28f;
                int length = Mathf.FloorToInt(SampleRate * duration);
                float[] samples = new float[length];

                for (int i = 0; i < length; i++)
                {
                    float t = (float)i / SampleRate;
                    float progress = t / duration;

                    // Deep blunt sub-bass impact thud (65Hz)
                    float thud = Mathf.Sin(2f * Mathf.PI * 65f * t) * Mathf.Exp(-progress * 16f);

                    // Bone-crunch distortion crack
                    float crunch = (Mathf.Sin(2f * Mathf.PI * 340f * t) + (UnityEngine.Random.value * 2f - 1f) * 0.4f)
                                   * Mathf.Exp(-progress * 22f);

                    samples[i] = Mathf.Clamp((thud * 0.75f + crunch * 0.55f), -1f, 1f) * 0.85f;
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

                    // Downward frequency sweep from 220Hz to 45Hz
                    float freq = Mathf.Lerp(220f, 45f, progress);
                    float tone = Mathf.Sin(2f * Mathf.PI * freq * t);

                    // Low frequency rumble noise
                    float rumble = (UnityEngine.Random.value * 2f - 1f) * 0.25f;

                    // Swelling envelope
                    float env = Mathf.Sin(progress * Mathf.PI);
                    samples[i] = (tone * 0.75f + rumble * 0.25f) * env * 0.8f;
                }

                shadowDashClip = AudioClip.Create("Shade_ShadowDash", length, 1, SampleRate, false);
                shadowDashClip.SetData(samples, 0);
            }
            return shadowDashClip;
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
