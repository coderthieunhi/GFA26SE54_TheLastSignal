using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace TheLastSignal.Environment.VisualPass
{
    [RequireComponent(typeof(Light2D))]
    public class VP_FlickerLight : MonoBehaviour
    {
        public enum FlickerMode
        {
            SubtlePulse,
            IrregularGlitches
        }

        [SerializeField] private FlickerMode mode = FlickerMode.SubtlePulse;
        [SerializeField] private float baseIntensity = 1.0f;
        [SerializeField] private float pulseSpeed = 2.0f;
        [SerializeField] private float pulseAmount = 0.15f;
        [SerializeField] private float glitchChance = 0.08f;

        private Light2D _light;
        private float _timeOffset;

        private void Awake()
        {
            _light = GetComponent<Light2D>();
            _timeOffset = Random.Range(0f, 100f);
            if (baseIntensity <= 0f && _light != null)
            {
                baseIntensity = _light.intensity;
            }
        }

        private void Update()
        {
            if (_light == null) return;

            float time = Time.time + _timeOffset;

            if (mode == FlickerMode.SubtlePulse)
            {
                // Smooth sine wave pulse with high-frequency micro jitter
                float sine = Mathf.Sin(time * pulseSpeed) * pulseAmount;
                float jitter = (Mathf.PerlinNoise(time * 8f, 0f) - 0.5f) * 0.05f;
                _light.intensity = Mathf.Max(0.1f, baseIntensity + sine + jitter);
            }
            else // IrregularGlitches
            {
                // Occasional irregular flicker/drop
                float noise = Mathf.PerlinNoise(time * 12f, 42.5f);
                if (noise < glitchChance)
                {
                    _light.intensity = baseIntensity * Random.Range(0.2f, 0.6f);
                }
                else
                {
                    float microJitter = (Mathf.PerlinNoise(time * 5f, 0f) - 0.5f) * 0.1f;
                    _light.intensity = Mathf.Max(0.2f, baseIntensity + microJitter);
                }
            }
        }
    }
}
