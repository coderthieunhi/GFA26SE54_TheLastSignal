using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TheLastSignal.Systems.Comms
{
    /// <summary>
    /// Local microphone capture system for Push-to-Talk voice chat.
    /// Captures ~60ms audio chunks at 16000Hz, quantizes them to 16-bit PCM,
    /// and fires callbacks for network transmission.
    /// </summary>
    public class VoiceRecorder : IDisposable
    {
        public const int SampleRate = 16000;
        public const int ChunkSize = 960; // 60ms chunks at 16kHz
        private const float NoiseGateThreshold = 0.004f;

        private string selectedDevice = null;
        private AudioClip recordingClip = null;
        private int lastSamplePosition = 0;
        private bool isInitialized = false;
        private bool isPushToTalkActive = false;
        private float currentVoiceLevel = 0f;

        private readonly float[] sampleBuffer = new float[ChunkSize * 4];
        private int bufferedSampleCount = 0;

        public bool IsHardwareAvailable => isInitialized;
        public bool IsPushToTalkActive => isPushToTalkActive;
        public float CurrentVoiceLevel => currentVoiceLevel;

        /// <summary>
        /// Fired when an audio chunk is ready to transmit.
        /// (pcmData, sampleCount, sampleRate, rmsLevel)
        /// </summary>
        public event Action<byte[], int, int, float> OnVoiceChunkReady;

        public VoiceRecorder()
        {
            InitializeMicrophone();
        }

        private void InitializeMicrophone()
        {
            if (Microphone.devices == null || Microphone.devices.Length == 0)
            {
                Debug.LogWarning("<color=orange>[VoiceRecorder]</color> No microphone hardware detected. Push-to-talk voice input will be inactive.");
                isInitialized = false;
                return;
            }

            selectedDevice = Microphone.devices[0];
            // Start 1-second circular buffer. Keeping it running eliminates OS driver latency on PTT press.
            recordingClip = Microphone.Start(selectedDevice, true, 1, SampleRate);
            lastSamplePosition = Microphone.GetPosition(selectedDevice);
            isInitialized = recordingClip != null;

            if (isInitialized)
            {
                Debug.Log($"<color=cyan>[VoiceRecorder]</color> Initialized microphone '{selectedDevice}' at {SampleRate}Hz.");
            }
        }

        /// <summary>
        /// Reads input and captures microphone audio. Should be called each frame in Update().
        /// </summary>
        /// <param name="allowTransmit">Set to false if player is The Shade or comms are blocked.</param>
        public void Update(bool allowTransmit)
        {
            if (!isInitialized || recordingClip == null) return;

            // 1. Read Push-to-Talk input (V Key)
            bool pttPressed = Keyboard.current != null && Keyboard.current.vKey.isPressed;

            isPushToTalkActive = pttPressed && allowTransmit;

            int currentSamplePos = Microphone.GetPosition(selectedDevice);
            if (currentSamplePos < 0) return;

            int samplesAvailable = currentSamplePos - lastSamplePosition;
            if (samplesAvailable < 0)
            {
                // Circular wrap-around past 1-second mark
                samplesAvailable += SampleRate;
            }

            if (samplesAvailable <= 0) return;

            // If PTT is NOT active or transmission is forbidden (The Shade), discard audio to avoid latency lag
            if (!isPushToTalkActive)
            {
                lastSamplePosition = currentSamplePos;
                bufferedSampleCount = 0;
                currentVoiceLevel = 0f;
                return;
            }

            // Read available samples from circular clip
            float[] temp = new float[samplesAvailable];
            if (lastSamplePosition + samplesAvailable <= SampleRate)
            {
                recordingClip.GetData(temp, lastSamplePosition);
            }
            else
            {
                // Wrap-around read
                int part1 = SampleRate - lastSamplePosition;
                int part2 = samplesAvailable - part1;
                float[] temp1 = new float[part1];
                float[] temp2 = new float[part2];
                recordingClip.GetData(temp1, lastSamplePosition);
                recordingClip.GetData(temp2, 0);
                Array.Copy(temp1, 0, temp, 0, part1);
                Array.Copy(temp2, 0, temp, part1, part2);
            }

            lastSamplePosition = currentSamplePos;

            // Append to accumulator buffer
            for (int i = 0; i < samplesAvailable; i++)
            {
                if (bufferedSampleCount < sampleBuffer.Length)
                {
                    sampleBuffer[bufferedSampleCount++] = temp[i];
                }
            }

            // Dispatch full chunks of ChunkSize (60ms)
            while (bufferedSampleCount >= ChunkSize)
            {
                float[] chunk = new float[ChunkSize];
                Array.Copy(sampleBuffer, 0, chunk, 0, ChunkSize);

                // Shift remaining samples down
                bufferedSampleCount -= ChunkSize;
                for (int i = 0; i < bufferedSampleCount; i++)
                {
                    sampleBuffer[i] = sampleBuffer[i + ChunkSize];
                }

                float rms = VoiceAudioSynthesizer.CalculateRms(chunk, ChunkSize);
                currentVoiceLevel = rms;

                // Send chunk if above noise gate or recently speaking
                if (rms >= NoiseGateThreshold)
                {
                    byte[] pcmData = VoiceAudioSynthesizer.FloatToPcm16(chunk, ChunkSize);
                    OnVoiceChunkReady?.Invoke(pcmData, ChunkSize, SampleRate, rms);
                }
            }
        }

        public void Dispose()
        {
            if (!string.IsNullOrEmpty(selectedDevice) && Microphone.IsRecording(selectedDevice))
            {
                Microphone.End(selectedDevice);
            }
            recordingClip = null;
            isInitialized = false;
        }
    }
}
