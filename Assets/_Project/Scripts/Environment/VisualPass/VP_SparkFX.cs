using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace TheLastSignal.Environment.VisualPass
{
    public class VP_SparkFX : MonoBehaviour
    {
        [SerializeField] private ParticleSystem sparkParticles;
        [SerializeField] private Light2D flashLight;
        [SerializeField] private float minInterval = 6.0f;
        [SerializeField] private float maxInterval = 12.0f;
        [SerializeField] private float flashDuration = 0.07f;
        [SerializeField] private float flashIntensity = 1.6f;

        private float _timer;
        private float _flashTimer;

        private void Start()
        {
            if (sparkParticles == null) sparkParticles = GetComponent<ParticleSystem>();
            if (flashLight == null) flashLight = GetComponent<Light2D>();
            if (flashLight != null) flashLight.intensity = 0f;
            _timer = Random.Range(1.5f, 4.0f); // Quick first spark
        }

        private void Update()
        {
            if (_flashTimer > 0f)
            {
                _flashTimer -= Time.deltaTime;
                if (_flashTimer <= 0f && flashLight != null)
                {
                    flashLight.intensity = 0f;
                }
            }

            _timer -= Time.deltaTime;
            if (_timer <= 0f)
            {
                TriggerSpark();
                _timer = Random.Range(minInterval, maxInterval);
            }
        }

        public void TriggerSpark()
        {
            if (sparkParticles != null)
            {
                int count = Random.Range(4, 7);
                sparkParticles.Emit(count);
            }

            if (flashLight != null)
            {
                flashLight.intensity = flashIntensity;
                _flashTimer = flashDuration;
            }
        }
    }
}
