namespace TheLastSignal.Core
{
    /// <summary>
    /// Pure, dependency-free role-permission checks — extracted out of LogisticsStunAbility so EditMode
    /// tests can assert "a Survivor can only trigger their own role's ability" without any
    /// NetworkBehaviour/MonoBehaviour object graph. Add a predicate here per role-gated ability.
    /// </summary>
    public static class RoleAbilityValidator
    {
        public static bool CanUseLogisticsAbility(SurvivorRole role)
        {
            return role == SurvivorRole.Logistics;
        }

        public static bool CanUseDroneAbility(SurvivorRole role)
        {
            return role == SurvivorRole.DroneOperator;
        }
    }
}
