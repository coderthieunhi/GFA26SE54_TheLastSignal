using System;
using UnityEngine;

namespace TheLastSignal.Core
{
    /// <summary>Per-role tunables. Fields beyond staminaCapacity/repairSpeedMultiplier are only
    /// meaningful for the role that actually uses them (currently Logistics' stun) — kept on every
    /// entry rather than role-specific subclasses so a future role's ability can reuse them without a
    /// data-shape change, per the design brief's "keep the config fields ready" note.</summary>
    [Serializable]
    public class RoleData
    {
        public SurvivorRole role;
        public string displayName;
        [Tooltip("Replicated to every Survivor's PlayerStamina at role assignment.")]
        public float staminaCapacity = 100f;
        [Tooltip("Multiplies StationRepair's progress-per-interact for whoever is repairing.")]
        public float repairSpeedMultiplier = 1f;

        [Header("Logistics stun ability (unused by other roles)")]
        public float stunDuration = 3f;
        public float stunCooldown = 15f;
        public float stunRange = 4f;
    }

    /// <summary>
    /// Designer-tunable per-role data: name, stamina capacity, repair speed multiplier, and ability
    /// parameters. Consumed by PlayerRole/PlayerStamina/LogisticsStunAbility/StationRepair — no magic
    /// numbers in those classes.
    /// </summary>
    [CreateAssetMenu(menuName = "TheLastSignal/Configs/Role Config", fileName = "RoleConfigSO")]
    public class RoleConfigSO : ScriptableObject
    {
        [SerializeField] private RoleData engineer = new RoleData
        {
            role = SurvivorRole.Engineer,
            displayName = "Engineer",
            staminaCapacity = 140f,
            repairSpeedMultiplier = 1.4f
        };

        [SerializeField] private RoleData mechanic = new RoleData
        {
            role = SurvivorRole.Mechanic,
            displayName = "Mechanic",
            staminaCapacity = 100f,
            repairSpeedMultiplier = 1f
        };

        [SerializeField] private RoleData logistics = new RoleData
        {
            role = SurvivorRole.Logistics,
            displayName = "Logistics",
            staminaCapacity = 100f,
            repairSpeedMultiplier = 1f,
            stunDuration = 3f,
            stunCooldown = 15f,
            stunRange = 4f
        };

        public RoleData GetData(SurvivorRole role)
        {
            return role switch
            {
                SurvivorRole.Engineer => engineer,
                SurvivorRole.Logistics => logistics,
                _ => mechanic
            };
        }
    }
}
