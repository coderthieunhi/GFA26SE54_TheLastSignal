using UnityEngine;

namespace TheLastSignal.Systems.Sanity
{
    /// <summary>
    /// Designer-tunable configuration for <see cref="SanitySystem"/>. Values are placeholders only —
    /// real balance numbers are owned by the design team, see SRS FR-SAN-01..06.
    /// </summary>
    [CreateAssetMenu(menuName = "TheLastSignal/Configs/Sanity Config", fileName = "SanityConfigSO")]
    public class SanityConfigSO : ScriptableObject
    {
        [SerializeField] private float baseDrainRatePerSecond = 1f; // placeholder, see SRS FR-SAN-02
        [SerializeField] private float maxSanity = 100f; // placeholder, see SRS FR-SAN-01
        [SerializeField] private float lowSanityThreshold = 25f; // placeholder, see SRS FR-SAN-04

        public float BaseDrainRatePerSecond => baseDrainRatePerSecond;
        public float MaxSanity => maxSanity;
        public float LowSanityThreshold => lowSanityThreshold;
    }
}
