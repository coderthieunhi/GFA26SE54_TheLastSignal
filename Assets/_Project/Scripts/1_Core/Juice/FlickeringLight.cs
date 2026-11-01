using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace TheLastSignal.Juice
{
    [RequireComponent(typeof(Light2D))]
    public class FlickeringLight : MonoBehaviour
    {
        public float minIntensity = 0.1f;
        public float maxIntensity = 1.0f;
        public float flickerChance = 0.1f;
        
        private Light2D light2d;
        private float targetIntensity;
        private float baseIntensity;

        void Awake()
        {
            light2d = GetComponent<Light2D>();
            baseIntensity = light2d.intensity;
            targetIntensity = baseIntensity;
        }

        void Update()
        {
            if (Random.value < flickerChance)
            {
                targetIntensity = Random.Range(minIntensity, maxIntensity);
            }
            light2d.intensity = Mathf.Lerp(light2d.intensity, targetIntensity, Time.deltaTime * 15f);
        }
    }
}
