using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace TheLastSignal.Juice
{
    /// <summary>
    /// Animates a high-voltage sequential surge wave (Chaser Pulse) along Node B's perimeter red conduit.
    /// Creates dramatic cinematic emergency lighting: pulses travel around the room in sequence,
    /// simulating overload current running through the bulkhead pipes.
    /// </summary>
    [ExecuteAlways]
    public class NodeBRedConduitChaser : MonoBehaviour
    {
        [Header("Chaser Surge Timing")]
        [Tooltip("Interval between successive surge waves in seconds.")]
        public float surgeInterval = 4.2f;

        [Tooltip("Speed of the surge head in lights per second.")]
        public float surgeSpeed = 10.0f;

        [Tooltip("Width of the surge pulse wave in number of lights.")]
        public float waveWidth = 3.0f;

        [Header("Lighting Modulation")]
        [Range(0f, 1f)] public float ambientIntensity = 0.36f;
        [Range(0.5f, 2.5f)] public float peakIntensity = 1.30f;

        public Color ambientColor = new Color(0.90f, 0.12f, 0.10f, 1f);
        public Color surgeColor = new Color(1.0f, 0.42f, 0.18f, 1f); // Hot plasma orange-red core

        [Header("Controlled Light Array")]
        public List<Light2D> conduitLights = new List<Light2D>();

        [Header("LED Rail Malfunctions (Survival Horror Tension)")]
        [Tooltip("Indices of lights experiencing voltage drop micro-stutter / PWM flicker.")]
        public List<int> faultyLightIndices = new List<int> { 1, 14 };

        [Tooltip("Indices of lights with burnt-out diode modules (permanent dim output).")]
        public List<int> deadLightIndices = new List<int> { 8 };

        [Range(0f, 1f)] public float flickerDepth = 0.45f;
        public float flickerFrequency = 26.0f;

        // Internal timing state
        private float _timer = 0f;
        private float _waveHead = -10f;
        private bool _waveActive = false;

        private void OnEnable()
        {
            _timer = 0f;
            _waveHead = -10f;
            _waveActive = false;
        }

        public void SetWavePosition(float headIndex)
        {
            _waveActive = true;
            _waveHead = headIndex;
            ApplyModulation();
        }

        public void ClearWave()
        {
            _waveActive = false;
            _waveHead = -10f;
            ApplyModulation();
        }

        private void Update()
        {
            if (conduitLights == null || conduitLights.Count == 0) return;

            float dt = Application.isPlaying ? Time.deltaTime : 0.016f;
            _timer += dt;

            // Trigger new surge wave on interval
            if (!_waveActive && _timer >= surgeInterval)
            {
                _timer = 0f;
                _waveActive = true;
                _waveHead = -waveWidth;
            }

            // Advance the wave head along the light array
            if (_waveActive)
            {
                _waveHead += surgeSpeed * dt;
                if (_waveHead > conduitLights.Count + waveWidth + 1f)
                {
                    _waveActive = false;
                    _waveHead = -10f;
                }
            }

            ApplyModulation();
        }

        public void ApplyModulation()
        {
            if (conduitLights == null) return;

            for (int i = 0; i < conduitLights.Count; i++)
            {
                var l = conduitLights[i];
                if (l == null) continue;

                float surgeFactor = 0f;
                if (_waveActive)
                {
                    float dist = Mathf.Abs(i - _waveHead);
                    if (dist < waveWidth)
                    {
                        // Smooth cosine bell-curve for peak surge
                        surgeFactor = Mathf.Cos((dist / waveWidth) * (Mathf.PI * 0.5f));
                        surgeFactor = Mathf.Clamp01(surgeFactor);
                    }
                }

                float baseAmbient = ambientIntensity;
                if (deadLightIndices != null && deadLightIndices.Contains(i))
                {
                    baseAmbient *= 0.45f;
                }

                // Smoothly interpolate intensity and color
                float targetIntensity = Mathf.Lerp(baseAmbient, peakIntensity, surgeFactor);
                Color targetColor = Color.Lerp(ambientColor, surgeColor, surgeFactor);

                // Apply voltage drop micro-stutter / PWM flicker on faulty units
                if (faultyLightIndices != null && faultyLightIndices.Contains(i))
                {
#if UNITY_EDITOR
                    float sampleTime = Application.isPlaying ? Time.time : (float)UnityEditor.EditorApplication.timeSinceStartup;
#else
                    float sampleTime = Time.time;
#endif
                    float noise = Mathf.PerlinNoise(sampleTime * flickerFrequency, i * 19.7f);
                    float microDip = (noise < 0.28f) ? 0.35f : (noise > 0.85f ? 1.15f : 1.0f);
                    targetIntensity *= Mathf.Lerp(1.0f, microDip, flickerDepth);
                }

                l.intensity = targetIntensity;
                l.color = targetColor;
            }
        }
    }
}
