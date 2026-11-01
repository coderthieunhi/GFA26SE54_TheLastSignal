using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace TheLastSignal.Juice
{
    /// <summary>
    /// Simulates periodic auxiliary power brownouts in neglected station bays.
    /// Occasionally plunges the room into brief darkness, forcing reliance on flashlight.
    /// </summary>
    public class FacilityPowerBrownout : MonoBehaviour
    {
        [Header("Target Lights")]
        public List<Light2D> bayLights = new List<Light2D>();

        [Header("Timing")]
        public float minInterval = 18f;
        public float maxInterval = 28f;
        public float blackoutDuration = 0.65f;

        private float _timer;
        private bool _isBrownoutActive;
        private Dictionary<Light2D, float> _cachedBaseIntensities = new Dictionary<Light2D, float>();

        private void Start()
        {
            foreach (var l in bayLights)
            {
                if (l != null && !_cachedBaseIntensities.ContainsKey(l))
                {
                    _cachedBaseIntensities[l] = l.intensity;
                }
            }
            ResetTimer();
        }

        private void Update()
        {
            if (_isBrownoutActive) return;

            _timer -= Time.deltaTime;
            if (_timer <= 0f)
            {
                StartCoroutine(ExecuteBrownoutSequence());
            }
        }

        private void ResetTimer()
        {
            _timer = Random.Range(minInterval, maxInterval);
        }

        private IEnumerator ExecuteBrownoutSequence()
        {
            _isBrownoutActive = true;

            // 1. Initial warning stutter (2 rapid micro-drops)
            for (int i = 0; i < 2; i++)
            {
                SetIntensityMultiplier(0.2f);
                yield return new WaitForSeconds(0.06f);
                SetIntensityMultiplier(0.9f);
                yield return new WaitForSeconds(0.06f);
            }

            // 2. Full blackout
            SetIntensityMultiplier(0.0f);
            yield return new WaitForSeconds(blackoutDuration);

            // 3. Electrical surge recovery
            SetIntensityMultiplier(1.25f);
            yield return new WaitForSeconds(0.12f);

            // 4. Settle back to normal
            float elapsed = 0f;
            float settleTime = 0.25f;
            while (elapsed < settleTime)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / settleTime;
                SetIntensityMultiplier(Mathf.Lerp(1.25f, 1.0f, t));
                yield return null;
            }

            SetIntensityMultiplier(1.0f);
            ResetTimer();
            _isBrownoutActive = false;
        }

        private void SetIntensityMultiplier(float mult)
        {
            foreach (var kvp in _cachedBaseIntensities)
            {
                if (kvp.Key != null)
                {
                    kvp.Key.intensity = kvp.Value * mult;
                }
            }
        }

        private void OnDisable()
        {
            SetIntensityMultiplier(1.0f);
            _isBrownoutActive = false;
        }
    }
}
