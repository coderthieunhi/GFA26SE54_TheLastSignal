using UnityEngine;

namespace TheLastSignal.Entities.Player
{
    /// <summary>Designer-tunable defaults for PlayerFlashlight. See Epic 5 design brief.</summary>
    [CreateAssetMenu(menuName = "TheLastSignal/Configs/Flashlight Config", fileName = "FlashlightConfigSO")]
    public class FlashlightConfigSO : ScriptableObject
    {
        [SerializeField] private float range = 6f;
        [SerializeField] private float coneAngleDegrees = 40f;
        [Tooltip("Minimum seconds between aim-angle ServerRpc sends (~10-15 Hz max per the design).")]
        [SerializeField, Range(1f / 15f, 1f / 10f)] private float aimSendIntervalSeconds = 1f / 12f;

        public float Range => range;
        public float ConeAngleDegrees => coneAngleDegrees;
        public float AimSendIntervalSeconds => aimSendIntervalSeconds;
    }
}
