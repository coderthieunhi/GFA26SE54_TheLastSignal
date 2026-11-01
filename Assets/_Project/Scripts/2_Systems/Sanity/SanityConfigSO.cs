using UnityEngine;

namespace TheLastSignal.Systems.Sanity
{
    /// <summary>
    /// Designer-tunable configuration for SanitySystem. Values are the Epic 5 design brief's defaults —
    /// no magic numbers in SanitySystem itself.
    /// </summary>
    [CreateAssetMenu(menuName = "TheLastSignal/Configs/Sanity Config", fileName = "SanityConfigSO")]
    public class SanityConfigSO : ScriptableObject
    {
        [Header("Range")]
        [SerializeField] private float maxSanity = 100f;
        [SerializeField] private float startingSanity = 100f;
        [SerializeField] private float tickIntervalSeconds = 0.5f;

        [Header("Drain / Recovery (per second)")]
        [SerializeField] private float darknessDrainPerSecond = 0.5f;
        [Tooltip("Multiplies darknessDrainPerSecond while the player's own flashlight is on and they " +
                 "are still in darkness (does not by itself count as 'in light').")]
        [SerializeField] private float flashlightDrainMultiplier = 0.5f;
        [Tooltip("Applies while in any lit zone or another player's flashlight cone.")]
        [SerializeField] private float lightRecoveryPerSecond = 1.0f;
        [SerializeField] private float teammateRecoveryPerSecond = 0.5f;
        [SerializeField] private float teammateRadius = 4f;

        [Header("Thresholds (crossing either direction raises an event)")]
        [SerializeField] private float uneasyThreshold = 70f;
        [SerializeField] private float distressedThreshold = 40f;
        [SerializeField] private float breakingThreshold = 15f;

        public float MaxSanity => maxSanity;
        public float StartingSanity => startingSanity;
        public float TickIntervalSeconds => tickIntervalSeconds;
        public float DarknessDrainPerSecond => darknessDrainPerSecond;
        public float FlashlightDrainMultiplier => flashlightDrainMultiplier;
        public float LightRecoveryPerSecond => lightRecoveryPerSecond;
        public float TeammateRecoveryPerSecond => teammateRecoveryPerSecond;
        public float TeammateRadius => teammateRadius;
        public float UneasyThreshold => uneasyThreshold;
        public float DistressedThreshold => distressedThreshold;
        public float BreakingThreshold => breakingThreshold;

        /// <summary>Classifies a sanity value into its named band. Values are checked in descending
        /// order so exactly-on-a-threshold lands in the lower (more severe-leaning) band consistently.</summary>
        public SanityLevel LevelFor(float sanityValue)
        {
            if (sanityValue <= breakingThreshold)
            {
                return SanityLevel.Breaking;
            }

            if (sanityValue <= distressedThreshold)
            {
                return SanityLevel.Distressed;
            }

            if (sanityValue <= uneasyThreshold)
            {
                return SanityLevel.Uneasy;
            }

            return SanityLevel.Normal;
        }
    }
}
