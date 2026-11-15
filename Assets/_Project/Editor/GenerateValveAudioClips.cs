using System;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEditor;

namespace TheLastSignal.Editor
{
    /// <summary>
    /// Generates physical 16-bit PCM RIFF .wav sound assets directly into Assets/_Project/Resources/Audio/.
    /// Runs automatically on editor compile or manually via menu.
    /// This guarantees Unity imports them as native, rock-solid AudioClips.
    /// </summary>
    public static class GenerateValveAudioClips
    {
        private const string OutputDir = "Assets/_Project/Resources/Audio";
        private const string RatchetPath = OutputDir + "/sfx_valve_ratchet.wav";
        private const string CompletePath = OutputDir + "/sfx_valve_complete.wav";

        [InitializeOnLoadMethod]
        [MenuItem("TheLastSignal/Audio/Generate Valve Audio Clips")]
        public static void GenerateAudioAssets()
        {
            if (!Directory.Exists(OutputDir))
            {
                Directory.CreateDirectory(OutputDir);
            }

            bool generatedAny = false;

            if (!File.Exists(RatchetPath) || new FileInfo(RatchetPath).Length == 0)
            {
                CreateRatchetWav(RatchetPath);
                generatedAny = true;
            }

            if (!File.Exists(CompletePath) || new FileInfo(CompletePath).Length == 0)
            {
                CreateCompleteWav(CompletePath);
                generatedAny = true;
            }

            if (generatedAny)
            {
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                Debug.Log("<color=green>[GenerateValveAudioClips]</color> Successfully generated and imported valve sound assets!");
            }
        }

        private static void CreateRatchetWav(string fullPath)
        {
            int sampleRate = 44100;
            float duration = 0.12f; // 120ms punchy metallic click
            int numSamples = (int)(sampleRate * duration);
            byte[] pcmData = new byte[numSamples * 2];
            System.Random rng = new System.Random(42);

            for (int i = 0; i < numSamples; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Pow(Mathf.Max(0f, 1f - (t / duration)), 1.2f);

                // Sharp mechanical tooth impact (first 12ms: noise burst + 3200Hz metallic tooth ring)
                float tooth = 0f;
                if (t < 0.012f)
                {
                    float noise = ((float)rng.NextDouble() * 2f - 1f) * 0.45f;
                    float toothPing = Mathf.Sin(2f * Mathf.PI * 3200f * t) * 0.50f;
                    tooth = (noise + toothPing) * Mathf.Exp(-t * 260f);
                }

                // Resonant gear tooth harmonics (780Hz & 1560Hz)
                float teeth = Mathf.Sin(2f * Mathf.PI * 780f * t) * 0.45f +
                              Mathf.Sin(2f * Mathf.PI * 1560f * t) * 0.30f;

                // Deep metallic body clank (240Hz)
                float chassis = Mathf.Sin(2f * Mathf.PI * 240f * t) * Mathf.Exp(-t * 28f) * 0.40f;

                float sample = Mathf.Clamp((tooth + teeth + chassis) * env, -1f, 1f);
                short pcmSample = (short)(sample * 32000f);

                pcmData[i * 2] = (byte)(pcmSample & 0xFF);
                pcmData[i * 2 + 1] = (byte)((pcmSample >> 8) & 0xFF);
            }

            WriteWavFile(fullPath, pcmData, sampleRate, 1, 16);
            Debug.Log($"[GenerateValveAudioClips] Written ratchet WAV to {fullPath}");
        }

        private static void CreateCompleteWav(string fullPath)
        {
            int sampleRate = 44100;
            float duration = 0.70f; // 700ms pneumatic lock chord
            int numSamples = (int)(sampleRate * duration);
            byte[] pcmData = new byte[numSamples * 2];

            for (int i = 0; i < numSamples; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Max(0f, (duration - t) / duration);

                // Sub-bass hydraulic valve lock thud (85Hz)
                float sub = Mathf.Sin(2f * Mathf.PI * 85f * t) * Mathf.Exp(-t * 10f) * 0.50f;

                // Harmonious major chime chord (C5 523Hz + G5 784Hz + C6 1046Hz)
                float c5 = Mathf.Sin(2f * Mathf.PI * 523.25f * t) * 0.30f;
                float g5 = Mathf.Sin(2f * Mathf.PI * 783.99f * t) * 0.25f;
                float c6 = Mathf.Sin(2f * Mathf.PI * 1046.50f * t) * 0.20f;

                float sample = Mathf.Clamp((sub + (c5 + g5 + c6) * env) * 0.95f, -1f, 1f);
                short pcmSample = (short)(sample * 32000f);

                pcmData[i * 2] = (byte)(pcmSample & 0xFF);
                pcmData[i * 2 + 1] = (byte)((pcmSample >> 8) & 0xFF);
            }

            WriteWavFile(fullPath, pcmData, sampleRate, 1, 16);
            Debug.Log($"[GenerateValveAudioClips] Written complete WAV to {fullPath}");
        }

        private static void WriteWavFile(string path, byte[] pcmData, int sampleRate, short channels, short bitsPerSample)
        {
            using (FileStream fs = new FileStream(path, FileMode.Create, FileAccess.Write))
            using (BinaryWriter bw = new BinaryWriter(fs))
            {
                int subchunk2Size = pcmData.Length;
                int chunkSize = 36 + subchunk2Size;
                int byteRate = sampleRate * channels * (bitsPerSample / 8);
                short blockAlign = (short)(channels * (bitsPerSample / 8));

                bw.Write(Encoding.ASCII.GetBytes("RIFF"));
                bw.Write(chunkSize);
                bw.Write(Encoding.ASCII.GetBytes("WAVE"));
                bw.Write(Encoding.ASCII.GetBytes("fmt "));
                bw.Write(16); // PCM chunk size
                bw.Write((short)1); // PCM format
                bw.Write(channels);
                bw.Write(sampleRate);
                bw.Write(byteRate);
                bw.Write(blockAlign);
                bw.Write(bitsPerSample);
                bw.Write(Encoding.ASCII.GetBytes("data"));
                bw.Write(subchunk2Size);
                bw.Write(pcmData);
            }
        }
    }
}
