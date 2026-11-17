using System;
using UnityEngine;

namespace TheLastSignal.Systems.Comms
{
    /// <summary>
    /// Audio receiver and playback component for voice chat.
    /// Emits incoming voice chunks from the player's physical in-world position
    /// blended with tactical radio transceiver audio filters (Band-pass & squelch chirps).
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class PlayerVoiceEmitter : MonoBehaviour
    {
        private AudioSource voiceAudioSource;
        private AudioSource radioEffectsAudioSource;
        private AudioHighPassFilter highPass;
        private AudioLowPassFilter lowPass;

        private double nextPlayDspTime = 0.0;
        private float lastChunkReceivedTime = 0f;
        private bool isSpeaking = false;
        private float currentVoiceLevel = 0f;

        private const float SilenceTimeoutSeconds = 0.22f;

        public bool IsSpeaking => isSpeaking;
        public float CurrentVoiceLevel => currentVoiceLevel;

        public event Action<bool, float> OnSpeakingStateChanged;

        private void Awake()
        {
            EnsureAudioComponents();
        }

        private void EnsureAudioComponents()
        {
            voiceAudioSource = GetComponent<AudioSource>();
            if (voiceAudioSource == null)
            {
                voiceAudioSource = gameObject.AddComponent<AudioSource>();
            }

            voiceAudioSource.playOnAwake = false;
            voiceAudioSource.loop = false;
            voiceAudioSource.spatialBlend = 0.30f; // Subtle 2D/3D tactical radio positioning
            voiceAudioSource.rolloffMode = AudioRolloffMode.Linear;
            voiceAudioSource.minDistance = 2f;
            voiceAudioSource.maxDistance = 25f;

            // Space suit / walkie-talkie radio frequency band-pass filter
            highPass = GetComponent<AudioHighPassFilter>();
            if (highPass == null) highPass = gameObject.AddComponent<AudioHighPassFilter>();
            highPass.cutoffFrequency = 450f;

            lowPass = GetComponent<AudioLowPassFilter>();
            if (lowPass == null) lowPass = gameObject.AddComponent<AudioLowPassFilter>();
            lowPass.cutoffFrequency = 3400f;

            // Dedicated AudioSource for squelch radio chirps so they don't interrupt voice stream
            Transform fxChild = transform.Find("RadioEffectsSource");
            if (fxChild != null)
            {
                radioEffectsAudioSource = fxChild.GetComponent<AudioSource>();
            }
            else
            {
                GameObject fxGo = new GameObject("RadioEffectsSource");
                fxGo.transform.SetParent(transform, false);
                radioEffectsAudioSource = fxGo.AddComponent<AudioSource>();
                radioEffectsAudioSource.playOnAwake = false;
                radioEffectsAudioSource.spatialBlend = 0.30f;
            }
        }

        private void Update()
        {
            if (isSpeaking && Time.time - lastChunkReceivedTime > SilenceTimeoutSeconds)
            {
                isSpeaking = false;
                currentVoiceLevel = 0f;
                OnSpeakingStateChanged?.Invoke(false, 0f);

                // Play channel closing squelch burst
                if (radioEffectsAudioSource != null)
                {
                    radioEffectsAudioSource.PlayOneShot(VoiceAudioSynthesizer.GetRadioChirpOffClip(), 0.75f);
                }
            }
        }

        /// <summary>
        /// Enqueues and plays a voice chunk from the network.
        /// </summary>
        public void PlayVoiceChunk(float[] samples, int sampleRate)
        {
            if (samples == null || samples.Length == 0) return;

            if (voiceAudioSource == null) EnsureAudioComponents();

            // First chunk in a new transmission: play radio opening squelch chirp
            if (!isSpeaking)
            {
                isSpeaking = true;
                if (radioEffectsAudioSource != null)
                {
                    radioEffectsAudioSource.PlayOneShot(VoiceAudioSynthesizer.GetRadioChirpOnClip(), 0.70f);
                }
                // Small jitter buffer of 40ms to avoid underrun
                nextPlayDspTime = AudioSettings.dspTime + 0.040;
            }

            lastChunkReceivedTime = Time.time;
            currentVoiceLevel = VoiceAudioSynthesizer.CalculateRms(samples, samples.Length);
            OnSpeakingStateChanged?.Invoke(true, currentVoiceLevel);

            AudioClip chunk = AudioClip.Create("VoiceStreamChunk", samples.Length, 1, sampleRate, false);
            chunk.SetData(samples, 0);

            double currentDsp = AudioSettings.dspTime;
            if (nextPlayDspTime < currentDsp)
            {
                nextPlayDspTime = currentDsp + 0.010;
            }

            voiceAudioSource.clip = chunk;
            voiceAudioSource.PlayScheduled(nextPlayDspTime);

            double chunkDuration = (double)samples.Length / sampleRate;
            nextPlayDspTime += chunkDuration;
        }

        /// <summary>
        /// Immediately silences voice playback (e.g. on player death or disconnect).
        /// </summary>
        public void StopVoice()
        {
            if (voiceAudioSource != null && voiceAudioSource.isPlaying)
            {
                voiceAudioSource.Stop();
            }
            if (isSpeaking)
            {
                isSpeaking = false;
                currentVoiceLevel = 0f;
                OnSpeakingStateChanged?.Invoke(false, 0f);
            }
        }
    }
}
