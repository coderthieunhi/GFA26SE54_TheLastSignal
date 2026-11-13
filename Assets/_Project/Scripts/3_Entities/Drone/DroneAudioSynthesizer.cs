using System;
using UnityEngine;

namespace TheLastSignal.Entities.Drone
{
    /// <summary>
    /// Runtime procedural audio generator for the Recon Drone & Drone Station.
    /// Synthesizes tactile, 16-bit equivalent sci-fi sound effects directly via AudioClip.Create,
    /// ensuring zero external audio file dependencies.
    /// </summary>
    public static class DroneAudioSynthesizer
    {
        private static AudioClip motorHumClip;
        private static AudioClip radarPingClip;
        private static AudioClip lowBatteryClip;
        private static AudioClip terminalEngageClip;
        private static AudioClip terminalDisengageClip;
        private static AudioClip accessDeniedClip;

        private const int SampleRate = 44100;

        public static AudioClip GetMotorHumClip()
        {
            if (motorHumClip == null)
            {
                // Seamless 1.0s looping dual-rotor quad-copter drone hum with sub-bass and overtone
                int length = SampleRate;
                float[] samples = new float[length];
                for (int i = 0; i < length; i++)
                {
                    float t = (float)i / SampleRate;
                    // Fundamental rotor blade passing frequency ~140Hz
                    float f1 = Mathf.Sin(2f * Mathf.PI * 140f * t) * 0.35f;
                    // Secondary harmonic ~280Hz
                    float f2 = Mathf.Sin(2f * Mathf.PI * 280f * t) * 0.2f;
                    // Rotor swirl modulation (35Hz)
                    float mod = 0.8f + 0.2f * Mathf.Sin(2f * Mathf.PI * 35f * t);
                    // High-pitch electronic motor whine ~840Hz
                    float whine = Mathf.Sin(2f * Mathf.PI * 840f * t) * 0.05f;

                    samples[i] = (f1 + f2 + whine) * mod * 0.4f;
                }
                motorHumClip = AudioClip.Create("Drone_MotorHum_Loop", length, 1, SampleRate, false);
                motorHumClip.SetData(samples, 0);
            }
            return motorHumClip;
        }

        public static AudioClip GetRadarPingClip()
        {
            if (radarPingClip == null)
            {
                // Crisp 0.16s active sonar chirp: 1100Hz -> 1800Hz with fast exponential decay
                float duration = 0.16f;
                int length = Mathf.FloorToInt(SampleRate * duration);
                float[] samples = new float[length];
                for (int i = 0; i < length; i++)
                {
                    float t = (float)i / SampleRate;
                    float progress = t / duration;
                    float freq = Mathf.Lerp(1100f, 1800f, progress);
                    float envelope = Mathf.Exp(-progress * 8f);
                    samples[i] = Mathf.Sin(2f * Mathf.PI * freq * t) * envelope * 0.35f;
                }
                radarPingClip = AudioClip.Create("Drone_RadarPing", length, 1, SampleRate, false);
                radarPingClip.SetData(samples, 0);
            }
            return radarPingClip;
        }

        public static AudioClip GetLowBatteryClip()
        {
            if (lowBatteryClip == null)
            {
                // Urgent 0.22s double-pip warning tone: 880Hz beep
                float duration = 0.22f;
                int length = Mathf.FloorToInt(SampleRate * duration);
                float[] samples = new float[length];
                for (int i = 0; i < length; i++)
                {
                    float t = (float)i / SampleRate;
                    bool pip1 = t < 0.08f;
                    bool pip2 = t > 0.12f && t < 0.20f;
                    float env = pip1 ? Mathf.Sin(t / 0.08f * Mathf.PI) : (pip2 ? Mathf.Sin((t - 0.12f) / 0.08f * Mathf.PI) : 0f);
                    samples[i] = Mathf.Sin(2f * Mathf.PI * 880f * t) * env * 0.3f;
                }
                lowBatteryClip = AudioClip.Create("Drone_LowBattery_Alert", length, 1, SampleRate, false);
                lowBatteryClip.SetData(samples, 0);
            }
            return lowBatteryClip;
        }

        public static AudioClip GetTerminalEngageClip()
        {
            if (terminalEngageClip == null)
            {
                // 0.35s uplink connect swell: low resonant hum sweeping upward (160Hz -> 520Hz)
                float duration = 0.35f;
                int length = Mathf.FloorToInt(SampleRate * duration);
                float[] samples = new float[length];
                for (int i = 0; i < length; i++)
                {
                    float t = (float)i / SampleRate;
                    float progress = t / duration;
                    float freq = Mathf.Lerp(160f, 520f, progress * progress);
                    float env = Mathf.Sin(progress * Mathf.PI);
                    samples[i] = Mathf.Sin(2f * Mathf.PI * freq * t) * env * 0.4f;
                }
                terminalEngageClip = AudioClip.Create("Drone_TerminalEngage", length, 1, SampleRate, false);
                terminalEngageClip.SetData(samples, 0);
            }
            return terminalEngageClip;
        }

        public static AudioClip GetTerminalDisengageClip()
        {
            if (terminalDisengageClip == null)
            {
                // 0.3s downlink disconnect: sweep downward (480Hz -> 120Hz)
                float duration = 0.3f;
                int length = Mathf.FloorToInt(SampleRate * duration);
                float[] samples = new float[length];
                for (int i = 0; i < length; i++)
                {
                    float t = (float)i / SampleRate;
                    float progress = t / duration;
                    float freq = Mathf.Lerp(480f, 120f, progress);
                    float env = Mathf.Sin(progress * Mathf.PI);
                    samples[i] = Mathf.Sin(2f * Mathf.PI * freq * t) * env * 0.35f;
                }
                terminalDisengageClip = AudioClip.Create("Drone_TerminalDisengage", length, 1, SampleRate, false);
                terminalDisengageClip.SetData(samples, 0);
            }
            return terminalDisengageClip;
        }

        public static AudioClip GetAccessDeniedClip()
        {
            if (accessDeniedClip == null)
            {
                // Low harsh 110Hz square-like denial buzz (0.25s)
                float duration = 0.25f;
                int length = Mathf.FloorToInt(SampleRate * duration);
                float[] samples = new float[length];
                for (int i = 0; i < length; i++)
                {
                    float t = (float)i / SampleRate;
                    float phase = (t * 110f) % 1f;
                    float raw = phase > 0.5f ? 0.4f : -0.4f;
                    float env = 1f - (t / duration);
                    samples[i] = raw * env * 0.35f;
                }
                accessDeniedClip = AudioClip.Create("Drone_AccessDenied", length, 1, SampleRate, false);
                accessDeniedClip.SetData(samples, 0);
            }
            return accessDeniedClip;
        }
    }
}
