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
    /// <para>
    /// TLS-E03-S05: an earlier version of this class also accepted ANY token whenever expectedToken
    /// equaled the literal "dev-token" - since "dev-token" is TestJoinToken's own default, that silently
    /// disabled fail-closed validation for the common case. Reverted: this now only ever approves an
    /// exact match, unless <see cref="acceptAnyToken"/> is explicitly set (never the default) for a demo
    /// that genuinely needs it - see LaunchModeConfig.StubAcceptAnyToken / "-stubAcceptAny".
    /// </para>
    /// </summary>
    public class StubBackendClient : IBackendClient
    {
        private readonly string expectedToken;
        private readonly bool acceptAnyToken;

        public StubBackendClient(string expectedToken, bool acceptAnyToken = false)
        {
            this.expectedToken = expectedToken ?? string.Empty;
            this.acceptAnyToken = acceptAnyToken;

            if (acceptAnyToken)
            {
                Debug.LogWarning("[StubBackendClient] acceptAnyToken is ON - every join token will be approved " +
                                  "regardless of value. This is a demo-only opt-in (-stubAcceptAny); never enable it " +
                                  "for anything other than a trusted local demo.");
            }
        }

        public Task<JoinTokenValidationResult> ValidateTokenAsync(string joinToken, ulong clientTransportId)
        {
            bool matches = acceptAnyToken || (!string.IsNullOrEmpty(expectedToken) && joinToken == expectedToken);

            JoinTokenValidationResult result = matches
                ? JoinTokenValidationResult.Approve(
                    accountId: $"stub-{clientTransportId}",
                    displayName: $"Player{clientTransportId}",
                    role: "Unassigned",
                    slotIndex: 0) // Unused by spawn placement today — PlayerController derives its own
                                  // slot from OwnerClientId (see PlayerController.OnNetworkSpawn).
                : JoinTokenValidationResult.Reject(JoinTokenRejectReason.TokenInvalid);

            if (matches && acceptAnyToken)
            {
                Debug.LogWarning($"[StubBackendClient] ValidateTokenAsync(client={clientTransportId}): " +
                                  "APPROVED via acceptAnyToken bypass (token not actually checked).");
            }
            else
            {
                Debug.Log($"[StubBackendClient] ValidateTokenAsync(client={clientTransportId}): " +
                          $"{(matches ? "APPROVED" : "REJECTED (TokenInvalid)")}.");
            }

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
