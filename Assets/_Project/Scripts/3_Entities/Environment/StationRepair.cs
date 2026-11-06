using Unity.Netcode;
using UnityEngine;
using TheLastSignal.Entities.Player;

namespace TheLastSignal.Entities.Environment
{
    /// <summary>
    /// Server-authoritative repair progress for a TaskLocation. No repair system existed in this
    /// project before this (TaskLocation.Interact was a placeholder log only — see its own comments),
    /// so this is new, minimal scaffolding: each [E] press adds a fixed chunk of progress, scaled by
    /// the repairing player's role (Engineer: faster — design brief: "every Survivor can repair any
    /// machine... Engineer repairs faster"). Mirrors DoorController's shape: its own per-entity
    /// NetworkVariable (Server write, Everyone read), a ServerRpc entry point, an offline fallback.
    /// </summary>
    [RequireComponent(typeof(TaskLocation))]
    [RequireComponent(typeof(NetworkObject))]
    public class StationRepair : NetworkBehaviour
    {
        private static bool IsNetworkSessionActive =>
            NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;

        [Tooltip("Progress added per [E] interact at repairSpeedMultiplier = 1 (Mechanic/Logistics baseline).")]
        [SerializeField, Range(0.05f, 1f)] private float baseProgressPerInteract = 0.34f;

        [Tooltip("Optional: the room's light zone, switched on when this station completes — closes " +
                 "the hook Epic 5 part 1's RepairStationLightZone left for 'a real repair-confirmation " +
                 "signal' (see that class's own comments).")]
        [SerializeField] private RepairStationLightZone lightZone;

        private readonly NetworkVariable<float> progress = new NetworkVariable<float>(
            0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<bool> completed = new NetworkVariable<bool>(
            false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private TaskLocation taskLocation;
        private float offlineProgress;
        private bool offlineCompleted;

        public float Progress => IsSpawned ? progress.Value : offlineProgress;
        public bool IsCompleted => IsSpawned ? completed.Value : offlineCompleted;

        private void Awake()
        {
            taskLocation = GetComponent<TaskLocation>();
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            progress.OnValueChanged += HandleProgressChanged;
            completed.OnValueChanged += HandleCompletedChanged;
            ApplyCompletionVisual(IsCompleted);
        }

        public override void OnNetworkDespawn()
        {
            progress.OnValueChanged -= HandleProgressChanged;
            completed.OnValueChanged -= HandleCompletedChanged;
            base.OnNetworkDespawn();
        }

        /// <summary>Called by TaskLocation.Interact — routes through the Host-validated ServerRpc when
        /// networked, falls back to a direct local update in single-player (same pattern as
        /// DoorController.Interact).</summary>
        public void RequestRepair(PlayerController requester)
        {
            if (IsCompleted)
            {
                return;
            }

            if (IsSpawned)
            {
                RequestRepairRpc();
            }
            else
            {
                ApplyRepair(requester);
            }
        }

        /// <summary>
        /// Instantly completes station repair (e.g., when a dedicated task minigame is completed).
        /// </summary>
        public void CompleteRepair()
        {
            if (IsCompleted)
            {
                return;
            }

            if (IsSpawned)
            {
                if (!IsServer)
                {
                    CompleteRepairRpc();
                    return;
                }

                progress.Value = 1f;
                completed.Value = true;
                lightZone?.ServerSetLit(true);
            }
            else
            {
                offlineProgress = 1f;
                offlineCompleted = true;
                ApplyCompletionVisual(true);
                lightZone?.ServerSetLit(true);
            }
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void CompleteRepairRpc(RpcParams rpcParams = default)
        {
            progress.Value = 1f;
            completed.Value = true;
            lightZone?.ServerSetLit(true);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void RequestRepairRpc(RpcParams rpcParams = default)
        {
            ulong senderId = rpcParams.Receive.SenderClientId;
            if (NetworkManager == null || !NetworkManager.ConnectedClients.TryGetValue(senderId, out NetworkClient client)
                || client.PlayerObject == null)
            {
                return;
            }

            if (!client.PlayerObject.TryGetComponent(out PlayerController requester))
            {
                return;
            }

            if (!IsSenderInRange(requester))
            {
                Debug.LogWarning($"[StationRepair] Rejecting repair on '{taskLocation.StationDisplayName}' from client {senderId}: out of range.");
                return;
            }

            ApplyRepair(requester);
        }

        private void ApplyRepair(PlayerController requester)
        {
            if (IsCompleted)
            {
                return;
            }

            float multiplier = 1f;
            if (requester != null && requester.TryGetComponent(out PlayerRole role) && role.Data != null)
            {
                multiplier = role.Data.repairSpeedMultiplier;
            }

            float newValue = Mathf.Clamp01(Progress + baseProgressPerInteract * multiplier);
            bool nowCompleted = newValue >= 1f;

            if (IsSpawned)
            {
                if (!IsServer)
                {
                    return;
                }

                progress.Value = newValue;
                if (nowCompleted)
                {
                    completed.Value = true;
                    lightZone?.ServerSetLit(true);
                }
            }
            else
            {
                offlineProgress = newValue;
                offlineCompleted = nowCompleted;
                ApplyCompletionVisual(nowCompleted);
                if (nowCompleted)
                {
                    lightZone?.ServerSetLit(true);
                }
            }
        }

        private bool IsSenderInRange(PlayerController requester)
        {
            float range = requester.InteractionRange + 1.2f;
            return ((Vector2)requester.transform.position - (Vector2)transform.position).sqrMagnitude <= range * range;
        }

        private void HandleProgressChanged(float previous, float current)
        {
        }

        private void HandleCompletedChanged(bool previous, bool current)
        {
            ApplyCompletionVisual(current);
        }

        private void ApplyCompletionVisual(bool isCompleted)
        {
            taskLocation.SetCompleted(isCompleted);
        }
    }
}
