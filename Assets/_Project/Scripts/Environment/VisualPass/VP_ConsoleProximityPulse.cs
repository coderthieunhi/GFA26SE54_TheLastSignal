using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace TheLastSignal.Environment.VisualPass
{
    public class VP_ConsoleProximityPulse : MonoBehaviour
    {
        [SerializeField] private Light2D targetLight;
        [SerializeField] private float proximityRange = 2.8f;
        [SerializeField] private float baseIntensity = 1.25f;

        private Transform _playerTransform;

        private void Start()
        {
            if (targetLight == null)
            {
                targetLight = GetComponent<Light2D>();
            }
            FindPlayer();
        }

        private void FindPlayer()
        {
            var playerGo = GameObject.FindGameObjectWithTag("Player");
            if (playerGo != null)
            {
                _playerTransform = playerGo.transform;
            }
        }

        private void Update()
        {
            if (targetLight == null) return;

            if (_playerTransform == null)
            {
                FindPlayer();
            }

            float dist = (_playerTransform != null) ? Vector2.Distance(transform.position, _playerTransform.position) : 999f;
            bool isNear = dist <= proximityRange;

            float pulseSpeed = isNear ? 3.5f : 1.2f;
            float pulseAmount = isNear ? 0.35f : 0.08f;
            float pulse = Mathf.Sin(Time.time * pulseSpeed) * pulseAmount;

            targetLight.intensity = Mathf.Max(0.2f, baseIntensity + pulse);
        }
    }
}
