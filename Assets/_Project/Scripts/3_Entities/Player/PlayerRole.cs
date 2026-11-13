using System;
using Unity.Netcode;
using UnityEngine;
using TheLastSignal.Core;

namespace TheLastSignal.Entities.Player
{
    /// <summary>
    /// Replicated Survivor role assignment. Server-authoritative (Server write, Everyone read), exactly
    /// like PlayerHealth/PlayerSanityState/PlayerFlashlight — clients can only ever read their own and
    /// others' roles, never choose or change them (design brief: "Clients cannot choose or change it").
    /// Every player (including slot 3, the Shade) has this component for prefab uniformity; GameManager
    /// only ever calls ServerAssignRole for the three Survivor slots.
    /// </summary>
    public class PlayerRole : NetworkBehaviour
    {
        private static bool IsNetworkSessionActive =>
            NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;

        [SerializeField] private RoleConfigSO roleConfig;

        private readonly NetworkVariable<SurvivorRole> role = new NetworkVariable<SurvivorRole>(
            SurvivorRole.DroneOperator, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        [SerializeField] private SurvivorRole offlineRole = SurvivorRole.DroneOperator;

        public event Action<SurvivorRole> OnRoleChanged;

        public SurvivorRole Role => IsNetworkSessionActive && IsSpawned ? role.Value : offlineRole;

        /// <summary>This player's role data from the shared RoleConfigSO. Every peer holds the same
        /// asset reference, so once Role itself replicates, Data is correct locally with nothing
        /// further to send over the network.</summary>
        public RoleData Data => roleConfig != null ? roleConfig.GetData(Role) : null;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            role.OnValueChanged += HandleRoleChanged;
            OnRoleChanged?.Invoke(Role);
        }

        public override void OnNetworkDespawn()
        {
            role.OnValueChanged -= HandleRoleChanged;
            base.OnNetworkDespawn();
        }

        private void Start()
        {
            if (!IsNetworkSessionActive)
            {
                OnRoleChanged?.Invoke(offlineRole);
            }
        }

        private void Update()
        {
#if UNITY_EDITOR || DEBUG
            bool isLocal = !IsNetworkSessionActive || (IsSpawned && IsOwner);
            if (isLocal && UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.f2Key.wasPressedThisFrame)
            {
                SurvivorRole nextRole = Role switch
                {
                    SurvivorRole.Engineer => SurvivorRole.DroneOperator,
                    SurvivorRole.DroneOperator => SurvivorRole.Logistics,
                    _ => SurvivorRole.Engineer
                };
                if (IsNetworkSessionActive && !IsServer)
                {
                    RequestSwitchRoleServerRpc(nextRole);
                }
                else
                {
                    ServerAssignRole(nextRole);
                }
                Debug.Log($"<color=cyan>[PlayerRole DEBUG]</color> Switched role to: <b>{nextRole}</b>");
            }
#endif
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void RequestSwitchRoleServerRpc(SurvivorRole newRole)
        {
            ServerAssignRole(newRole);
        }

        /// <summary>Server-only (and the offline-fallback writer). Called once by GameManager.AssignRoles
        /// at match start. Quietly no-ops on a non-server peer — same convention as
        /// PlayerSanityState.ResetSanity, not a misuse warning, since every peer's own PlayerController
        /// calling through ResetState-style flows is expected.</summary>
        public void ServerAssignRole(SurvivorRole newRole)
        {
            if (IsNetworkSessionActive)
            {
                if (IsServer)
                {
                    role.Value = newRole;
                }
            }
            else
            {
                offlineRole = newRole;
                OnRoleChanged?.Invoke(offlineRole);
            }
        }

        private void HandleRoleChanged(SurvivorRole previous, SurvivorRole current)
        {
            OnRoleChanged?.Invoke(current);
        }
    }
}
