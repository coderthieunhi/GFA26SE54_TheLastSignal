using UnityEngine;

namespace TheLastSignal.Systems.Comms
{
    /// <summary>
    /// Utility class for audio encoding, decoding, RMS calculation,
    /// and procedural synthesis of tactical radio squelch chirps and static clicks.
    /// Eliminates external audio asset dependencies.
    /// </summary>
    public static class VoiceAudioSynthesizer
    {
        private static AudioClip cachedChirpOn;
        private static AudioClip cachedChirpOff;
        private static AudioClip cachedDenied;

        /// <summary>
        /// Converts an array of normalized floats (-1.0f to 1.0f) to 16-bit little-endian PCM bytes.
        /// </summary>
        public static byte[] FloatToPcm16(float[] samples, int count)
        {
            if (samples == null || count <= 0) return new byte[0];

            byte[] pcm = new byte[count * 2];
            for (int i = 0; i < count; i++)
            {
                short val = (short)Mathf.Clamp(samples[i] * 32767f, -32768f, 32767f);
                pcm[i * 2] = (byte)(val & 0xFF);
                pcm[i * 2 + 1] = (byte)((val >> 8) & 0xFF);
            }
            return pcm;
        }

        /// <summary>
        /// Converts an array of 16-bit little-endian PCM bytes back to normalized floats (-1.0f to 1.0f).
        /// </summary>
        public static float[] Pcm16ToFloat(byte[] pcm, int byteCount)
        {
            if (pcm == null || byteCount <= 0) return new float[0];

            int sampleCount = byteCount / 2;
            float[] samples = new float[sampleCount];
            for (int i = 0; i < sampleCount; i++)
            {
                short val = (short)(pcm[i * 2] | (pcm[i * 2 + 1] << 8));
                samples[i] = val / 32768f;
            }
            return samples;
        }

        /// <summary>
        /// Calculates the Root Mean Square (RMS) volume level of audio samples.
        /// Returns value in [0.0, 1.0].
        /// </summary>
        public static float CalculateRms(float[] samples, int count)
        {
            if (samples == null || count <= 0) return 0f;

            float sum = 0f;
            for (int i = 0; i < count; i++)
            {
                sum += samples[i] * samples[i];
            }
            return Mathf.Clamp01(Mathf.Sqrt(sum / count) * 3.5f); // Scale for intuitive UI meters
        }

        /// <summary>
        /// Generates a realistic high-frequency radio squelch chirp when Push-to-Talk activates (~45ms).
        /// </summary>
        public static AudioClip GetRadioChirpOnClip()
        {
            if (cachedChirpOn != null) return cachedChirpOn;

            int sampleRate = 44100;
            int samples = (int)(sampleRate * 0.045f);
            cachedChirpOn = AudioClip.Create("Radio_ChirpOn", samples, 1, sampleRate, false);

            float[] data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / sampleRate;
                // Dual tone: 1450Hz shifting to 1950Hz
                float freq = i < samples / 2 ? 1450f : 1950f;
                float env = Mathf.Sin((float)i / samples * Mathf.PI);
                float noise = (Random.value * 2f - 1f) * 0.08f;
                data[i] = (Mathf.Sin(2f * Mathf.PI * freq * t) * 0.45f + noise) * env;
            }

            cachedChirpOn.SetData(data, 0);
            return cachedChirpOn;
        }

        /// <summary>
        /// Generates a radio channel closing squelch burst and release click (~55ms).
        /// </summary>
        public static AudioClip GetRadioChirpOffClip()
        {
            if (cachedChirpOff != null) return cachedChirpOff;

            int sampleRate = 44100;
            int samples = (int)(sampleRate * 0.055f);
            cachedChirpOff = AudioClip.Create("Radio_ChirpOff", samples, 1, sampleRate, false);

            float[] data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / samples;
                float noise = (Random.value * 2f - 1f) * (1f - t);
                float pop = Mathf.Sin(2f * Mathf.PI * 320f * (i / (float)sampleRate)) * Mathf.Exp(-t * 20f);
                data[i] = (noise * 0.35f + pop * 0.4f);
            }

            cachedChirpOff.SetData(data, 0);
            return cachedChirpOff;
        }

        /// <summary>
        /// Generates an electronic buzz / comms jammed warning sound when The Shade attempts to transmit (~130ms).
        /// </summary>
        public static AudioClip GetCommsDeniedClip()
        {
            if (cachedDenied != null) return cachedDenied;

            int sampleRate = 44100;
            int samples = (int)(sampleRate * 0.130f);
            cachedDenied = AudioClip.Create("Radio_CommsDenied", samples, 1, sampleRate, false);

            float[] data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / sampleRate;
                // Harsh low frequency square/saw buzz
                float buzz = Mathf.Sign(Mathf.Sin(2f * Mathf.PI * 115f * t));
                float grit = (Random.value * 2f - 1f) * 0.25f;
                float env = 1f - ((float)i / samples);
                data[i] = (buzz * 0.45f + grit) * env;
            }

            cachedDenied.SetData(data, 0);
            return cachedDenied;
        }
    }
}
