using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace TheLastSignal.Juice
{
    /// <summary>
    /// Smoothly modulates Light2D intensity in a subtle sine-wave pulse.
    /// Ideal for emergency warning beacons and low-power standby lights.
    /// </summary>
    [RequireComponent(typeof(Light2D))]
    public class PulsingLight2D : MonoBehaviour
    {
        [Tooltip("Minimum light intensity during pulse.")]
        [Range(0f, 2f)]
        public float minIntensity = 0.35f;

        [Tooltip("Maximum light intensity during pulse.")]
        [Range(0f, 3f)]
        public float maxIntensity = 0.75f;

        [Tooltip("Speed of pulse cycle in radians per second.")]
        public float pulseSpeed = 2.2f;

        [Tooltip("Phase offset in radians to desynchronize multiple lights.")]
        public float phaseOffset = 0f;

        private Light2D _light2D;

        private void Awake()
        {
            _light2D = GetComponent<Light2D>();
        }

        private void Update()
        {
            if (_light2D == null) return;
            float t = (Mathf.Sin(Time.time * pulseSpeed + phaseOffset) + 1f) * 0.5f;
            _light2D.intensity = Mathf.Lerp(minIntensity, maxIntensity, t);
        }
    }
}
