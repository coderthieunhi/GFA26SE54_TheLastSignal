using Unity.Netcode;
using UnityEngine;
using TheLastSignal.Entities.Player;

namespace TheLastSignal.Entities.Environment
{
    /// <summary>
    /// Server-authoritative multi-stage repair progression for a TaskLocation.
    /// Manages sequential sub-tasks, persistent progress saving, and role-based quotas:
    /// - Engineer: 3 sub-tasks required (or +1 if station already had >= 3 and < 5 steps)
    /// - Drone Operator / Mechanic / Logistics: 5 sub-tasks required
    /// - Concurrency lock: ensures only 1 survivor repairs at a time
    /// </summary>
    [RequireComponent(typeof(TaskLocation))]
    [RequireComponent(typeof(NetworkObject))]
    public class StationRepair : NetworkBehaviour
    {
        private static bool IsNetworkSessionActive =>
            NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;

        [Tooltip("Progress added per [E] interact at repairSpeedMultiplier = 1 (Mechanic/Logistics baseline).")]
        [SerializeField, Range(0.05f, 1f)] private float baseProgressPerInteract = 0.34f;

        [Tooltip("Optional: the room's light zone, switched on when this station completes.")]
        [SerializeField] private RepairStationLightZone lightZone;

        private readonly NetworkVariable<float> progress = new NetworkVariable<float>(
            0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<bool> completed = new NetworkVariable<bool>(
            false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<int> completedSteps = new NetworkVariable<int>(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<ulong> activeRepairerClientId = new NetworkVariable<ulong>(
            ulong.MaxValue, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private TaskLocation taskLocation;
        private float offlineProgress;
        private bool offlineCompleted;
        private int offlineCompletedSteps = 0;
        private ulong offlineRepairerId = ulong.MaxValue;
        private int engineerSessionStartSteps = -1;

        public float Progress => IsSpawned ? progress.Value : offlineProgress;
        public bool IsCompleted => IsSpawned ? completed.Value : offlineCompleted;
        public int CompletedSteps => IsSpawned ? completedSteps.Value : offlineCompletedSteps;
        public ulong ActiveRepairerClientId => IsSpawned ? activeRepairerClientId.Value : offlineRepairerId;

        private void Awake()
        {
            taskLocation = GetComponent<TaskLocation>();
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            progress.OnValueChanged += HandleProgressChanged;
            completed.OnValueChanged += HandleCompletedChanged;
            completedSteps.OnValueChanged += HandleStepsChanged;

            if (IsServer && NetworkManager != null)
            {
                NetworkManager.OnClientDisconnectCallback += HandleClientDisconnect;
            }

            ApplyCompletionVisual(IsCompleted);
        }

        public override void OnNetworkDespawn()
        {
            progress.OnValueChanged -= HandleProgressChanged;
            completed.OnValueChanged -= HandleCompletedChanged;
            completedSteps.OnValueChanged -= HandleStepsChanged;

            if (IsServer && NetworkManager != null)
            {
                NetworkManager.OnClientDisconnectCallback -= HandleClientDisconnect;
            }

            base.OnNetworkDespawn();
        }

        private void HandleClientDisconnect(ulong clientId)
        {
            if (activeRepairerClientId.Value == clientId)
            {
                activeRepairerClientId.Value = ulong.MaxValue;
            }
        }

        private ulong GetClientId(PlayerController player)
        {
            if (player != null && player.IsSpawned)
            {
                return player.OwnerClientId;
            }
            return 0;
        }

        /// <summary>
        /// Checks whether another survivor is currently working on this station.
        /// </summary>
        public bool IsStationLockedByOther(PlayerController requester, out string reason)
        {
            reason = string.Empty;
            if (IsCompleted) return false;

            ulong myId = GetClientId(requester);
            ulong lockId = ActiveRepairerClientId;

            if (lockId != ulong.MaxValue && lockId != myId)
            {
                reason = "Station is in use by another survivor.";
                return true;
            }
            return false;
        }

        /// <summary>
        /// Claims exclusive repair lock for a survivor while interacting with minigames.
        /// </summary>
        public bool TryClaimRepairLock(PlayerController requester, out string reason)
        {
            reason = string.Empty;
            if (IsCompleted)
            {
                reason = "Station is already repaired.";
                return false;
            }

            ulong myId = GetClientId(requester);

            if (IsSpawned)
            {
                if (activeRepairerClientId.Value != ulong.MaxValue && activeRepairerClientId.Value != myId)
                {
                    reason = "Station is currently in use by another survivor.";
                    return false;
                }

                if (IsServer)
                {
                    activeRepairerClientId.Value = myId;
                }
                else
                {
                    ClaimLockRpc();
                }
            }
            else
            {
                if (offlineRepairerId != ulong.MaxValue && offlineRepairerId != myId)
                {
                    reason = "Station is in use.";
                    return false;
                }
                offlineRepairerId = myId;
            }

            // Record session start steps for Engineer hand-off logic
            if (requester != null && requester.TryGetComponent(out PlayerRole r) && r.Role == TheLastSignal.Core.SurvivorRole.Engineer)
            {
                engineerSessionStartSteps = CompletedSteps;
            }
            else
            {
                engineerSessionStartSteps = -1;
            }

            return true;
        }

        /// <summary>
        /// Releases exclusive repair lock when player exits or finishes minigames.
        /// </summary>
        public void ReleaseRepairLock(PlayerController requester)
        {
            ulong myId = GetClientId(requester);
            if (IsSpawned)
            {
                if (IsServer)
                {
                    if (activeRepairerClientId.Value == myId)
                    {
                        activeRepairerClientId.Value = ulong.MaxValue;
                    }
                }
                else
                {
                    ReleaseLockRpc();
                }
            }
            else
            {
                if (offlineRepairerId == myId)
                {
                    offlineRepairerId = ulong.MaxValue;
                }
            }
        }

        /// <summary>
        /// Computes the target number of minigame steps required for this survivor role.
        /// - Engineer: Normally 3 steps. If station was already >= 3 and < 5, requires 1 additional step.
        /// - Drone Operator / Mechanic / Logistics: 5 steps.
        /// </summary>
        public int GetTargetStepsForPlayer(PlayerController player)
        {
            if (player != null && player.TryGetComponent(out PlayerRole r) && r.Role == TheLastSignal.Core.SurvivorRole.Engineer)
            {
                int current = CompletedSteps;
                if (current >= 3 && current < 5)
                {
                    return current + 1;
                }
                return 3;
            }
            return 5;
        }

        /// <summary>
        /// Advances completed minigames counter and evaluates whether the station is now 100% finished.
        /// Implements the Engineer hand-off rule: if station was already >= 3 and < 5, doing 1 more completes it!
        /// </summary>
        public bool AdvanceRepairStep(PlayerController player, out bool isStationCompleted)
        {
            isStationCompleted = false;
            if (IsCompleted)
            {
                isStationCompleted = true;
                return true;
            }

            bool isEngineer = player != null && player.TryGetComponent(out PlayerRole r) && r.Role == TheLastSignal.Core.SurvivorRole.Engineer;
            int current = CompletedSteps;
            int next = current + 1;

            bool finished = false;
            if (isEngineer)
            {
                // Engineer special rule:
                // If station already had >= 3 steps and < 5 when Engineer started, 1 more minigame completes it!
                if (engineerSessionStartSteps >= 3 && engineerSessionStartSteps < 5)
                {
                    if (next > engineerSessionStartSteps)
                    {
                        finished = true;
                    }
                }
                else if (next >= 3)
                {
                    finished = true;
                }
            }
            else
            {
                // Drone Operator / Mechanic / Logistics: requires 5 steps
                if (next >= 5)
                {
                    finished = true;
                }
            }

            if (IsSpawned)
            {
                if (IsServer)
                {
                    ServerApplyStep(next, finished);
                }
                else
                {
                    AdvanceStepRpc(finished);
                }
            }
            else
            {
                offlineCompletedSteps = next;
                offlineProgress = finished ? 1f : Mathf.Clamp01(next / 5f);
                if (finished)
                {
                    offlineCompleted = true;
                    offlineRepairerId = ulong.MaxValue;
                    ApplyCompletionVisual(true);
                    lightZone?.ServerSetLit(true);
                }
            }

            isStationCompleted = finished;
            return true;
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void ClaimLockRpc(RpcParams rpcParams = default)
        {
            ulong sender = rpcParams.Receive.SenderClientId;
            if (activeRepairerClientId.Value == ulong.MaxValue)
            {
                activeRepairerClientId.Value = sender;
            }
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void ReleaseLockRpc(RpcParams rpcParams = default)
        {
            ulong sender = rpcParams.Receive.SenderClientId;
            if (activeRepairerClientId.Value == sender)
            {
                activeRepairerClientId.Value = ulong.MaxValue;
            }
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void AdvanceStepRpc(bool finished, RpcParams rpcParams = default)
        {
            int next = completedSteps.Value + 1;
            ServerApplyStep(next, finished);
        }

        private void ServerApplyStep(int nextSteps, bool finished)
        {
            completedSteps.Value = nextSteps;
            if (finished || nextSteps >= 5)
            {
                progress.Value = 1f;
                completed.Value = true;
                activeRepairerClientId.Value = ulong.MaxValue;
                lightZone?.ServerSetLit(true);
            }
            else
            {
                progress.Value = Mathf.Clamp01(nextSteps / 5f);
            }
        }

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

                completedSteps.Value = 5;
                progress.Value = 1f;
                completed.Value = true;
                activeRepairerClientId.Value = ulong.MaxValue;
                lightZone?.ServerSetLit(true);
            }
            else
            {
                offlineCompletedSteps = 5;
                offlineProgress = 1f;
                offlineCompleted = true;
                offlineRepairerId = ulong.MaxValue;
                ApplyCompletionVisual(true);
                lightZone?.ServerSetLit(true);
            }
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void CompleteRepairRpc(RpcParams rpcParams = default)
        {
            completedSteps.Value = 5;
            progress.Value = 1f;
            completed.Value = true;
            activeRepairerClientId.Value = ulong.MaxValue;
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
                    completedSteps.Value = 5;
                    completed.Value = true;
                    activeRepairerClientId.Value = ulong.MaxValue;
                    lightZone?.ServerSetLit(true);
                }
            }
            else
            {
                offlineProgress = newValue;
                offlineCompleted = nowCompleted;
                if (nowCompleted)
                {
                    offlineCompletedSteps = 5;
                    offlineRepairerId = ulong.MaxValue;
                }
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

        private void HandleStepsChanged(int previous, int current)
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
