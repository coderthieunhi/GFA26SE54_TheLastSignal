using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace TheLastSignal.Entities.Player
{
    /// <summary>
    /// Per-Survivor replicated sanity value. Written ONLY by SanitySystem (server-side, via
    /// ServerSetSanity) — mirrors PlayerHealth's shape (NetworkVariable&lt;float&gt;, Server write,
    /// Everyone read) rather than NetworkGameState's singleton pattern, because sanity is per-player
    /// state exactly like health, not session-wide state like the objective phase or evac timer. The
    /// Shade has no sanity (see SanitySystem); this component is still present on every player so it can
    /// be added/removed uniformly by PlayerSpawner without special-casing a slot.
    /// </summary>
    public class PlayerSanityState : NetworkBehaviour
    {
        private static bool IsNetworkSessionActive =>
            NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;

        /// <summary>All currently active instances — SanitySystem iterates this instead of doing its
        /// own FindObjectsByType every 0.5s tick.</summary>
        private static readonly List<PlayerSanityState> activeStates = new List<PlayerSanityState>();
        public static IReadOnlyList<PlayerSanityState> ActiveStates => activeStates;

        private readonly NetworkVariable<float> sanity = new NetworkVariable<float>(
            100f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private float offlineSanity = 100f;

        /// <summary>Invoked on every peer whenever the replicated (or offline) sanity value changes.</summary>
        public event Action<float> OnSanityChanged;

        public float Sanity => IsNetworkSessionActive && IsSpawned ? sanity.Value : offlineSanity;

        /// <summary>Stable per-player id SanitySystem/threshold events key off. OwnerClientId is 0 for
        /// every player in single-player/offline mode, which is fine — there is only one player then.</summary>
        public ulong PlayerId => IsNetworkSessionActive && IsSpawned ? OwnerClientId : 0ul;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            sanity.OnValueChanged += HandleNetworkSanityChanged;

            if (IsServer)
            {
                sanity.Value = 100f;
            }

            OnSanityChanged?.Invoke(Sanity);
        }

        public override void OnNetworkDespawn()
        {
            sanity.OnValueChanged -= HandleNetworkSanityChanged;
            base.OnNetworkDespawn();
        }

        private void OnEnable()
        {
            if (!activeStates.Contains(this))
            {
                activeStates.Add(this);
            }
        }

        private void OnDisable()
        {
            activeStates.Remove(this);
        }

        public override void OnDestroy()
        {
            activeStates.Remove(this);
            base.OnDestroy();
        }

        private void Start()
        {
            if (!IsNetworkSessionActive)
            {
                offlineSanity = 100f;
                OnSanityChanged?.Invoke(offlineSanity);
            }
        }

        /// <summary>Server-only (and the offline-fallback writer). Called by SanitySystem every tick.</summary>
        public void ServerSetSanity(float value)
        {
            if (IsNetworkSessionActive)
            {
                if (!IsServer)
                {
                    Debug.LogWarning("[PlayerSanityState] ServerSetSanity called on a non-server peer; ignored.");
                    return;
                }

                sanity.Value = value;
            }
            else
            {
                offlineSanity = value;
                OnSanityChanged?.Invoke(offlineSanity);
            }
        }

        /// <summary>
        /// Resets to full sanity on session start/restart, mirroring PlayerHealth.ResetHealth() exactly:
        /// called from PlayerController.ResetState() on every peer, but only ever writes the
        /// NetworkVariable when this peer is actually authoritative (quietly no-ops otherwise — a real
        /// client calling this for its own locally-owned object on spawn is expected, not a misuse, so
        /// unlike ServerSetSanity it does not warn).
        /// </summary>
        public void ResetSanity()
        {
            if (IsNetworkSessionActive)
            {
                if (IsServer)
                {
                    sanity.Value = 100f;
                }
            }
            else
            {
                offlineSanity = 100f;
                OnSanityChanged?.Invoke(offlineSanity);
            }
        }

        private void HandleNetworkSanityChanged(float previous, float current)
        {
            OnSanityChanged?.Invoke(current);
        }
    }
}
