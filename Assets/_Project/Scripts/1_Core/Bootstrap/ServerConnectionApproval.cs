using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;
using TheLastSignal.Core.Backend;

namespace TheLastSignal.Core.Bootstrap
{
    /// <summary>
    /// TLS-E03-S02: the Dedicated Server's connection gatekeeper. Reads the join token from NGO's
    /// connection payload, validates it through <see cref="IBackendClient"/>, and fails closed —
    /// an invalid token, a backend error, or a timeout (IBackendClient itself is responsible for
    /// timing out and reporting BackendUnreachable) all result in rejection, never approval.
    /// Also enforces maxPlayers and rejects a token already in use by another still-connected client.
    /// Attached only in DedicatedServer mode (see GameManager.StartDedicatedServer); the legacy
    /// Relay/Host-Client path never touches this class and keeps NetworkConfig.ConnectionApproval=false.
    /// </summary>
    public class ServerConnectionApproval
    {
        /// <summary>TLS-E03-S03: one approved player's identity, kept so GameManager can build the
        /// per-player rows in a match result report (docs/pivot/02_BACKEND_CONTRACT.md §3.3) without this
        /// class needing to know anything about MatchResult itself.</summary>
        public readonly struct ApprovedPlayer
        {
            public readonly string AccountId;
            public readonly string Role;

            public ApprovedPlayer(string accountId, string role)
            {
                AccountId = accountId;
                Role = role;
            }
        }

        private readonly NetworkManager networkManager;
        private readonly IBackendClient backendClient;
        private readonly int maxPlayers;

        private readonly Dictionary<ulong, string> approvedTokensByClient = new Dictionary<ulong, string>();
        private readonly Dictionary<ulong, ApprovedPlayer> approvedPlayersByClient = new Dictionary<ulong, ApprovedPlayer>();
        private readonly HashSet<string> tokensInUse = new HashSet<string>();

        /// <summary>Currently-connected approved players, for building a match result's player list.</summary>
        public IReadOnlyCollection<ApprovedPlayer> ApprovedPlayers => approvedPlayersByClient.Values;

        public ServerConnectionApproval(NetworkManager networkManager, IBackendClient backendClient, int maxPlayers)
        {
            this.networkManager = networkManager;
            this.backendClient = backendClient;
            this.maxPlayers = maxPlayers;
        }

        /// <summary>Wires this instance as the server's ConnectionApprovalCallback. Call before StartServer().</summary>
        public void Attach()
        {
            networkManager.NetworkConfig.ConnectionApproval = true;
            networkManager.ConnectionApprovalCallback = HandleApproval;
            networkManager.OnClientDisconnectCallback += HandleClientDisconnected;
        }

        private void HandleApproval(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
        {
            // The response object starts with Approved=false, Pending=false (C# defaults) — if we don't
            // set Pending=true synchronously, right now, NGO's ProcessPendingApprovals() will treat this
            // as an immediate rejection before our async validation below ever gets a chance to run.
            response.Pending = true;
            _ = ValidateAsync(request, response);
        }

        private async Task ValidateAsync(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
        {
            ulong clientId = request.ClientNetworkId;
            string token = (request.Payload != null && request.Payload.Length > 0)
                ? Encoding.UTF8.GetString(request.Payload)
                : string.Empty;

            if (approvedTokensByClient.Count >= maxPlayers)
            {
                Reject(response, clientId, "Match is full.");
                return;
            }

            JoinTokenValidationResult result;
            try
            {
                result = await backendClient.ValidateTokenAsync(token, clientId);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ServerConnectionApproval] ValidateTokenAsync threw ({ex.GetType().Name}) for client {clientId}; failing closed.");
                result = JoinTokenValidationResult.Reject(JoinTokenRejectReason.BackendUnreachable);
            }

            if (!result.Approved)
            {
                Reject(response, clientId, $"Join token rejected: {result.Reason}.");
                return;
            }

            if (tokensInUse.Contains(token))
            {
                Reject(response, clientId, "Join token is already in use by another connection.");
                return;
            }

            // Re-check post-await: another in-flight approval may have filled the last slot while this
            // one was waiting on the backend.
            if (approvedTokensByClient.Count >= maxPlayers)
            {
                Reject(response, clientId, "Match is full.");
                return;
            }

            approvedTokensByClient[clientId] = token;
            approvedPlayersByClient[clientId] = new ApprovedPlayer(result.AccountId, result.Role);
            tokensInUse.Add(token);

            Debug.Log($"[ServerConnectionApproval] Approved client {clientId}: account={result.AccountId}, " +
                      $"role={result.Role}, slot={result.SlotIndex}.");

            // Position/Rotation deliberately left null: PlayerController.OnNetworkSpawn() already resolves
            // its own spawn point from OwnerClientId (Story 3.1-3.4), identically for every transport.
            response.Approved = true;
            response.CreatePlayerObject = true;
            response.Pending = false;
        }

        private void HandleClientDisconnected(ulong clientId)
        {
            if (approvedTokensByClient.TryGetValue(clientId, out string token))
            {
                tokensInUse.Remove(token);
                approvedTokensByClient.Remove(clientId);
            }

            approvedPlayersByClient.Remove(clientId);
        }

        private static void Reject(NetworkManager.ConnectionApprovalResponse response, ulong clientId, string reason)
        {
            Debug.LogWarning($"[ServerConnectionApproval] Rejected client {clientId}: {reason}");
            response.Approved = false;
            response.CreatePlayerObject = false;
            response.Reason = reason;
            response.Pending = false;
        }
    }
}
