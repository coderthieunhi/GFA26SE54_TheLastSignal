using System;
using Unity.Netcode;
using UnityEngine;

namespace TheLastSignal.Core
{
    /// <summary>
    /// One row of shared lobby state: which client this is, and whether they've marked themselves
    /// ready. Deliberately excludes role selection and ping — those are out of scope for Story 3.2.
    /// Must stay an unmanaged blittable struct (NetworkList{T}'s constraint) — primitives only.
    /// INetworkSerializeByMemcpy is a zero-method marker: without it NGO's codegen never generates a
    /// serializer for this type and NetworkList throws at runtime the first time it replicates a change.
    /// </summary>
    public struct LobbyPlayerData : IEquatable<LobbyPlayerData>, INetworkSerializeByMemcpy
    {
        public ulong ClientId;
        public bool IsReady;

        public bool Equals(LobbyPlayerData other) =>
            ClientId == other.ClientId && IsReady == other.IsReady;
    }

    /// <summary>
    /// Host-authoritative owner of the shared lobby roster. Holds a NetworkList&lt;LobbyPlayerData&gt;
    /// that only the Host ever writes to (ARCHITECTURE_RULES.md Rule 1) — LobbyUI (every peer) only
    /// reads it via <see cref="Players"/> and its OnListChanged event, and only ever asks to change its
    /// own ready flag through <see cref="SubmitReadyStateServerRpc"/> (Rule 3: input goes up, state
    /// comes down). Roster membership itself is driven by NetworkManager's connect/disconnect
    /// callbacks, not by anything a client says about itself.
    /// </summary>
    public class NetworkSessionManager : NetworkBehaviour
    {
        private readonly NetworkList<LobbyPlayerData> players = new NetworkList<LobbyPlayerData>();

        /// <summary>Read-only view of the shared lobby roster. Subscribe to Players.OnListChanged to react.</summary>
        public NetworkList<LobbyPlayerData> Players => players;

        /// <summary>
        /// True once the roster is non-empty and every entry is ready. Purely a computed read of
        /// already-replicated state — exposed for a future story to act on, not wired to start
        /// anything here (Story 3.2 is explicitly lobby-only).
        /// </summary>
        public bool AllPlayersReady
        {
            get
            {
                if (players.Count == 0)
                {
                    return false;
                }

                for (int i = 0; i < players.Count; i++)
                {
                    if (!players[i].IsReady)
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            if (!IsServer)
            {
                return;
            }

            // In-scene NetworkObjects (this one) finish spawning before NetworkManager invokes
            // OnClientConnectedCallback for the Host's own local connection, so subscribing here is
            // enough to pick up the Host itself — no separate "add myself" call needed on the Host button.
            NetworkManager.OnClientConnectedCallback += HandleClientConnected;
            NetworkManager.OnClientDisconnectCallback += HandleClientDisconnected;
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer && NetworkManager != null)
            {
                NetworkManager.OnClientConnectedCallback -= HandleClientConnected;
                NetworkManager.OnClientDisconnectCallback -= HandleClientDisconnected;
            }

            base.OnNetworkDespawn();
        }

        private void HandleClientConnected(ulong clientId)
        {
            if (!IsServer)
            {
                return;
            }

            if (IndexOfClient(clientId) >= 0)
            {
                return;
            }

            players.Add(new LobbyPlayerData { ClientId = clientId, IsReady = false });
        }

        private void HandleClientDisconnected(ulong clientId)
        {
            if (!IsServer)
            {
                return;
            }

            int index = IndexOfClient(clientId);
            if (index >= 0)
            {
                players.RemoveAt(index);
            }
        }

        /// <summary>
        /// Client entry point for the Ready toggle. The Host validates that the caller already has a
        /// roster entry (Rule 3: clients never set their own ready flag directly on shared state — they
        /// ask, the Host applies) and updates only that entry.
        /// </summary>
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void SubmitReadyStateServerRpc(bool isReady, RpcParams rpcParams = default)
        {
            ulong senderId = rpcParams.Receive.SenderClientId;
            int index = IndexOfClient(senderId);

            if (index < 0)
            {
                Debug.LogWarning($"[NetworkSessionManager] Ready state from client {senderId} ignored: no roster entry.");
                return;
            }

            players[index] = new LobbyPlayerData { ClientId = senderId, IsReady = isReady };
        }

        private int IndexOfClient(ulong clientId)
        {
            for (int i = 0; i < players.Count; i++)
            {
                if (players[i].ClientId == clientId)
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
