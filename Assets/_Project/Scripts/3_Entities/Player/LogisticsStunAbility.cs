using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using TheLastSignal.Core;
using TheLastSignal.Systems.Lighting;

namespace TheLastSignal.Entities.Player
{
    /// <summary>
    /// Logistics' stun ability against the Shade. Server-validated: role check (only a Logistics may
    /// trigger it — design brief: "enforce that a Survivor can only trigger their own role's ability"),
    /// cooldown, range, and line of sight (one Physics2D.Linecast against the same wall layer
    /// LightingController already uses, per the design's "no per-pixel shadow simulation" spirit — see
    /// LightingConfigSO). On success it calls ShadeStunState.ServerApplyStun on whichever player
    /// currently occupies slot 3 — the only thing resembling "the Shade" that exists yet.
    /// </summary>
    [RequireComponent(typeof(PlayerRole))]
    [RequireComponent(typeof(PlayerController))]
    public class LogisticsStunAbility : NetworkBehaviour
    {
        private static bool IsNetworkSessionActive =>
            NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;

        [SerializeField] private LightingConfigSO lightingConfig;

        private readonly NetworkVariable<float> cooldownRemaining = new NetworkVariable<float>(
            0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private PlayerRole playerRole;
        private PlayerController playerController;
        private float offlineCooldownRemaining;

        public float CooldownRemaining => IsNetworkSessionActive && IsSpawned ? cooldownRemaining.Value : offlineCooldownRemaining;
        public bool IsReady => CooldownRemaining <= 0f;

        private void Awake()
        {
            playerRole = GetComponent<PlayerRole>();
            playerController = GetComponent<PlayerController>();
        }

        private void Update()
        {
            bool isLocalOwner = (IsNetworkSessionActive && IsSpawned) ? IsOwner : !IsNetworkSessionActive;

            // Server-side cooldown countdown (also drives the offline fallback countdown directly).
            if ((IsNetworkSessionActive && IsServer) || !IsNetworkSessionActive)
            {
                TickCooldown(Time.deltaTime);
            }

            if (!isLocalOwner)
            {
                return;
            }

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.qKey.wasPressedThisFrame)
            {
                RequestUseAbility();
            }
        }

        private void TickCooldown(float deltaTime)
        {
            if (IsNetworkSessionActive)
            {
                if (cooldownRemaining.Value > 0f)
                {
                    cooldownRemaining.Value = Mathf.Max(0f, cooldownRemaining.Value - deltaTime);
                }
            }
            else if (offlineCooldownRemaining > 0f)
            {
                offlineCooldownRemaining = Mathf.Max(0f, offlineCooldownRemaining - deltaTime);
            }
        }

        public void RequestUseAbility()
        {
            if (IsNetworkSessionActive)
            {
                RequestUseAbilityRpc();
            }
            else
            {
                TryUseAbility(playerRole, playerController);
            }
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        private void RequestUseAbilityRpc(RpcParams rpcParams = default)
        {
            TryUseAbility(playerRole, playerController);
        }

        /// <summary>
        /// Server-authoritative (and offline-fallback) validation + application. Rejects silently
        /// (logs only) on any failed check — never throws, matching this project's existing
        /// reject-with-a-log convention (DoorController.RequestToggleRpc, NetworkGameState's reactor Rpc).
        /// </summary>
        private void TryUseAbility(PlayerRole role, PlayerController controller)
        {
            if (!RoleAbilityValidator.CanUseLogisticsAbility(role.Role))
            {
                Debug.LogWarning($"[LogisticsStunAbility] Rejected: {controller.name} is not Logistics.");
                return;
            }

            if (!IsReady)
            {
                Debug.LogWarning($"[LogisticsStunAbility] Rejected: ability still on cooldown ({CooldownRemaining:0.0}s).");
                return;
            }

            PlayerController target = FindShadeController();
            if (target == null)
            {
                Debug.LogWarning("[LogisticsStunAbility] Rejected: no Shade (slot 3) player found.");
                return;
            }

            RoleData data = role.Data;
            Vector2 selfPosition = controller.transform.position;
            Vector2 targetPosition = target.transform.position;
            float range = data != null ? data.stunRange : 4f;

            if ((targetPosition - selfPosition).sqrMagnitude > range * range)
            {
                Debug.LogWarning("[LogisticsStunAbility] Rejected: Shade out of range.");
                return;
            }

            if (IsOccludedByWall(selfPosition, targetPosition))
            {
                Debug.LogWarning("[LogisticsStunAbility] Rejected: no line of sight to the Shade.");
                return;
            }

            if (target.TryGetComponent(out ShadeStunState stunState))
            {
                stunState.ServerApplyStun(data != null ? data.stunDuration : 3f);
            }

            if (IsNetworkSessionActive)
            {
                cooldownRemaining.Value = data != null ? data.stunCooldown : 15f;
            }
            else
            {
                offlineCooldownRemaining = data != null ? data.stunCooldown : 15f;
            }

            Debug.Log($"<color=cyan>[LogisticsStunAbility]</color> {controller.name} stunned the Shade for {(data != null ? data.stunDuration : 3f)}s.");
        }

        private static PlayerController FindShadeController()
        {
            var activePlayers = PlayerHealth.ActivePlayers;
            for (int i = 0; i < activePlayers.Count; i++)
            {
                PlayerHealth health = activePlayers[i];
                if (health == null)
                {
                    continue;
                }

                if (health.TryGetComponent(out PlayerController candidate) && candidate.SlotIndex == 3)
                {
                    return candidate;
                }
            }

            return null;
        }

        private bool IsOccludedByWall(Vector2 from, Vector2 to)
        {
            LayerMask wallLayer = lightingConfig != null ? lightingConfig.WallLayer : (LayerMask)0;
            if (wallLayer.value == 0)
            {
                return false;
            }

            return Physics2D.Linecast(from, to, wallLayer);
        }
    }
}
