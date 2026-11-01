using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace TheLastSignal.Environment
{
    /// <summary>
    /// Quarantine bulkhead door indicator breathing/slow pulse.
    /// Simulates industrial security lock status.
    /// </summary>
    [RequireComponent(typeof(Light2D))]
    public class VP3_DoorLightPulse : MonoBehaviour
    {
        [SerializeField] private float minIntensity = 0.45f;
        [SerializeField] private float maxIntensity = 0.90f;
        [SerializeField] private float pulseSpeed = 1.8f;
        [SerializeField] private float phaseOffset = 0f;

        private Light2D doorLight;

        private void Awake()
        {
            doorLight = GetComponent<Light2D>();
        }

        private void Update()
        {
            if (doorLight == null) return;
            // Smooth sine wave pulse with slight sharpness at peak
            float t = (Mathf.Sin((Time.time * pulseSpeed) + phaseOffset) + 1f) * 0.5f;
            doorLight.intensity = Mathf.Lerp(minIntensity, maxIntensity, t * t);
        }
    }
}
