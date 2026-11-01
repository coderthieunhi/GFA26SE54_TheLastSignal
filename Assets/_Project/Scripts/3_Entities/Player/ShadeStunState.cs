using System;
using Unity.Netcode;
using UnityEngine;

namespace TheLastSignal.Entities.Player
{
    /// <summary>
    /// Clean hook for the Shade epic: a replicated, time-limited stun flag any ability can apply
    /// (currently only LogisticsStunAbility). Present on every player prefab instance for uniformity
    /// (see PlayerRole's own note) but only ever meaningful for whichever slot ends up being the Shade
    /// — no Shade-specific entity/component exists yet (see docs/pivot), so this intentionally does
    /// nothing on its own: it only tracks the flag and counts it down. The future Shade controller
    /// subscribes to OnStunStateChanged and applies its own speed-zero/control-lock effect; this class
    /// does not implement any Shade ability itself, per the brief's scope limit.
    /// </summary>
    public class ShadeStunState : NetworkBehaviour
    {
        private static bool IsNetworkSessionActive =>
            NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;

        private readonly NetworkVariable<bool> isStunned = new NetworkVariable<bool>(
            false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private float serverStunRemaining;
        private bool offlineStunned;

        /// <summary>Raised on every peer whenever the stun flag changes (not every tick).</summary>
        public event Action<bool> OnStunStateChanged;

        public bool IsStunned => IsNetworkSessionActive && IsSpawned ? isStunned.Value : offlineStunned;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            isStunned.OnValueChanged += HandleStunChanged;
        }

        public override void OnNetworkDespawn()
        {
            isStunned.OnValueChanged -= HandleStunChanged;
            base.OnNetworkDespawn();
        }

        private void Update()
        {
            if (IsNetworkSessionActive)
            {
                if (!IsServer || !isStunned.Value)
                {
                    return;
                }

                serverStunRemaining -= Time.deltaTime;
                if (serverStunRemaining <= 0f)
                {
                    isStunned.Value = false;
                }
            }
        }

        /// <summary>Server-only. Applies (or refreshes) the stun for <paramref name="durationSeconds"/>.</summary>
        public void ServerApplyStun(float durationSeconds)
        {
            if (IsNetworkSessionActive)
            {
                if (!IsServer)
                {
                    return;
                }

                serverStunRemaining = Mathf.Max(serverStunRemaining, durationSeconds);
                isStunned.Value = true;
            }
            else
            {
                offlineStunned = true;
                OnStunStateChanged?.Invoke(true);
            }
        }

        private void HandleStunChanged(bool previous, bool current)
        {
            OnStunStateChanged?.Invoke(current);
        }
    }
}
