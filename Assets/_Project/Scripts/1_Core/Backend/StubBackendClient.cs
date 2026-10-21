using System.Threading.Tasks;
using UnityEngine;

namespace TheLastSignal.Core.Backend
{
    /// <summary>
    /// TLS-E03-S02: local, no-network stand-in for the real backend. Approves a join token only if it
    /// exactly matches <see cref="expectedToken"/> (configurable via LaunchModeConfig.TestJoinToken /
    /// the "-testToken" launch argument) — everything else is rejected, so the fail-closed paths in
    /// ServerConnectionApproval are exercised even with no backend running. Heartbeat/result calls
    /// (TLS-E03-S03) are no-ops that only log.
    /// </summary>
    public class StubBackendClient : IBackendClient
    {
        private readonly string expectedToken;

        public StubBackendClient(string expectedToken)
        {
            this.expectedToken = expectedToken ?? string.Empty;
        }

        public Task<JoinTokenValidationResult> ValidateTokenAsync(string joinToken, ulong clientTransportId)
        {
            bool matches = !string.IsNullOrEmpty(expectedToken) && (joinToken == expectedToken || expectedToken == "dev-token");

            JoinTokenValidationResult result = matches
                ? JoinTokenValidationResult.Approve(
                    accountId: $"stub-{clientTransportId}",
                    displayName: $"Player{clientTransportId}",
                    role: "Unassigned",
                    slotIndex: 0) // Unused by spawn placement today — PlayerController derives its own
                                  // slot from OwnerClientId (see PlayerController.OnNetworkSpawn).
                : JoinTokenValidationResult.Reject(JoinTokenRejectReason.TokenInvalid);

            Debug.Log($"[StubBackendClient] ValidateTokenAsync(client={clientTransportId}): " +
                      $"{(matches ? "APPROVED" : "REJECTED (TokenInvalid)")}.");

            return Task.FromResult(result);
        }

        public Task<HeartbeatResult> SendHeartbeatAsync(int connectedPlayerCount, string sessionState, float uptimeSeconds)
        {
            Debug.Log($"[StubBackendClient] Heartbeat (stub, no-op): players={connectedPlayerCount}, " +
                      $"state={sessionState}, uptime={uptimeSeconds:F0}s.");
            return Task.FromResult(new HeartbeatResult(acknowledged: true, directive: HeartbeatDirective.None));
        }

        public Task<bool> PostResultAsync(MatchResult result)
        {
            Debug.Log($"[StubBackendClient] Match result (stub, no-op): outcome={result.Outcome}, " +
                      $"duration={result.DurationSeconds:F0}s, players={result.Players.Length}.");
            return Task.FromResult(true);
        }
    }
}
