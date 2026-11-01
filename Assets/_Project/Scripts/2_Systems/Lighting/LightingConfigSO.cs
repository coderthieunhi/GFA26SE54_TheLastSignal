using UnityEngine;

namespace TheLastSignal.Systems.Lighting
{
    /// <summary>Designer-tunable configuration for LightingController. See Epic 5 design brief.</summary>
    [CreateAssetMenu(menuName = "TheLastSignal/Configs/Lighting Config", fileName = "LightingConfigSO")]
    public class LightingConfigSO : ScriptableObject
    {
        [Tooltip("Layer LightingController's cone occlusion Linecast treats as solid. Matches the " +
                 "'Obstacle' layer PlayerController/PlayerSpawner already collide against.")]
        [SerializeField] private LayerMask wallLayer = 0;

        [Tooltip("The Shade's movement speed is multiplied by this while exposed to light (hook only — " +
                 "no Shade entity applies it yet, see ShadeLightExposure).")]
        [SerializeField] private float shadeSpeedDebuffMultiplier = 0.7f;

        public LayerMask WallLayer => wallLayer;

        public float ShadeSpeedDebuffMultiplier => shadeSpeedDebuffMultiplier;
    }
}
