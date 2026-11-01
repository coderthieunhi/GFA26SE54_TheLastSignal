using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace TheLastSignal.Environment
{
    /// <summary>
    /// Subtle horror medical examination lamp flicker.
    /// Provides gentle hum/buzz flicker with occasional sharp tension dip.
    /// </summary>
    [RequireComponent(typeof(Light2D))]
    public class VP3_SurgicalLampFlicker : MonoBehaviour
    {
        [Header("Intensity Settings")]
        [SerializeField] private float baseIntensity = 1.55f;
        [SerializeField] private float microFlickerAmount = 0.08f;
        [SerializeField] private float microFlickerSpeed = 12.0f;

        [Header("Occasional Voltage Dip")]
        [SerializeField] private float dipProbability = 0.008f;
        [SerializeField] private float dipDuration = 0.08f;
        [SerializeField] private float dipIntensityMultiplier = 0.35f;

        private Light2D lampLight;
        private float dipTimer = 0f;
        private float noiseOffset;

        private void Awake()
        {
            lampLight = GetComponent<Light2D>();
            noiseOffset = Random.Range(0f, 100f);
        }

        private void Update()
        {
            if (lampLight == null) return;

            if (dipTimer > 0f)
            {
                dipTimer -= Time.deltaTime;
                lampLight.intensity = baseIntensity * dipIntensityMultiplier;
                return;
            }

            if (Random.value < dipProbability)
            {
                dipTimer = dipDuration + Random.Range(0f, 0.05f);
                lampLight.intensity = baseIntensity * dipIntensityMultiplier;
                return;
            }

            float noise = Mathf.PerlinNoise((Time.time * microFlickerSpeed) + noiseOffset, 0f);
            float targetInt = baseIntensity + (noise - 0.5f) * 2f * microFlickerAmount;
            lampLight.intensity = targetInt;
        }
    }
}
