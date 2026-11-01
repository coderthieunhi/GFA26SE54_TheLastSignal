using Unity.Netcode;
using UnityEngine;

namespace TheLastSignal.Entities.Player
{
    /// <summary>
    /// Replicated stamina capacity, set once from the player's assigned role (Engineer: larger
    /// capacity). No drain/regen loop exists yet — there is no sprint/exertion mechanic anywhere in
    /// this project to consume it — so this is deliberately a capacity-only scaffold: it replicates
    /// Max/Current (current starts full) so the role's effect is visible and testable, and gives a
    /// future stamina-consuming system (sprinting, repair effort, ...) a ready server-authoritative
    /// value to drain rather than inventing one then. See the task's own report for this call-out.
    /// </summary>
    [RequireComponent(typeof(PlayerRole))]
    public class PlayerStamina : NetworkBehaviour
    {
        private static bool IsNetworkSessionActive =>
            NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;

        private readonly NetworkVariable<float> maxStamina = new NetworkVariable<float>(
            100f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<float> currentStamina = new NetworkVariable<float>(
            100f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private float offlineMax = 100f;
        private float offlineCurrent = 100f;

        private PlayerRole playerRole;

        public float MaxStamina => IsNetworkSessionActive && IsSpawned ? maxStamina.Value : offlineMax;
        public float CurrentStamina => IsNetworkSessionActive && IsSpawned ? currentStamina.Value : offlineCurrent;

        private void Awake()
        {
            playerRole = GetComponent<PlayerRole>();
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            playerRole.OnRoleChanged += HandleRoleChanged;
            ApplyCapacityForCurrentRole();
        }

        public override void OnNetworkDespawn()
        {
            playerRole.OnRoleChanged -= HandleRoleChanged;
            base.OnNetworkDespawn();
        }

        private void Start()
        {
            if (!IsNetworkSessionActive)
            {
                ApplyCapacityForCurrentRole();
            }
        }

        private void HandleRoleChanged(TheLastSignal.Core.SurvivorRole role)
        {
            ApplyCapacityForCurrentRole();
        }

        /// <summary>Server-only (and the offline-fallback writer). Resets capacity to the role's
        /// configured value and refills current to full — called once the role is known, and again on
        /// RestartSession via the same OnRoleChanged event firing with the freshly-reassigned role.</summary>
        private void ApplyCapacityForCurrentRole()
        {
            float capacity = playerRole.Data != null ? playerRole.Data.staminaCapacity : 100f;

            if (IsNetworkSessionActive)
            {
                if (!IsServer)
                {
                    return;
                }

                maxStamina.Value = capacity;
                currentStamina.Value = capacity;
            }
            else
            {
                offlineMax = capacity;
                offlineCurrent = capacity;
            }
        }
    }
}
