using System.Threading.Tasks;

namespace TheLastSignal.Core.Backend
{
    /// <summary>Why a join token was rejected. Mirrors docs/pivot/02_BACKEND_CONTRACT.md §3.1.</summary>
    public enum JoinTokenRejectReason
    {
        None,
        TokenInvalid,
        TokenExpired,
        TokenAlreadyUsed,
        TokenWrongMatch,
        MatchFull,
        MatchNotFound,
        BackendUnreachable
    }

    /// <summary>Result of validating one client's join token (docs/pivot/02_BACKEND_CONTRACT.md §3.1).</summary>
    public readonly struct JoinTokenValidationResult
    {
        public readonly bool Approved;
        public readonly JoinTokenRejectReason Reason;
        public readonly string AccountId;
        public readonly string DisplayName;
        public readonly string Role;
        public readonly int SlotIndex;

        private JoinTokenValidationResult(bool approved, JoinTokenRejectReason reason, string accountId, string displayName, string role, int slotIndex)
        {
            Approved = approved;
            Reason = reason;
            AccountId = accountId;
            DisplayName = displayName;
            Role = role;
            SlotIndex = slotIndex;
        }

        public static JoinTokenValidationResult Approve(string accountId, string displayName, string role, int slotIndex) =>
            new JoinTokenValidationResult(true, JoinTokenRejectReason.None, accountId, displayName, role, slotIndex);

        public static JoinTokenValidationResult Reject(JoinTokenRejectReason reason) =>
            new JoinTokenValidationResult(false, reason, string.Empty, string.Empty, string.Empty, -1);
    }

    public enum HeartbeatDirective
    {
        None,
        Shutdown
    }

    /// <summary>Result of one heartbeat call (docs/pivot/02_BACKEND_CONTRACT.md §3.2). Wired in TLS-E03-S03.</summary>
    public readonly struct HeartbeatResult
    {
        public readonly bool Acknowledged;
        public readonly HeartbeatDirective Directive;

        public HeartbeatResult(bool acknowledged, HeartbeatDirective directive)
        {
            Acknowledged = acknowledged;
            Directive = directive;
        }
    }

    public enum MatchOutcome
    {
        SurvivorVictory,
        ShadeVictory,
        Aborted
    }

    /// <summary>One player's row in a match result report (docs/pivot/02_BACKEND_CONTRACT.md §3.3).</summary>
    [System.Serializable]
    public struct MatchPlayerResult
    {
        public string AccountId;
        public string Role;
        public bool Incapacitated;
        public string FinalStatus;
    }

    /// <summary>Wired in TLS-E03-S03.</summary>
    [System.Serializable]
    public class MatchResult
    {
        public MatchOutcome Outcome;
        public float DurationSeconds;
        public MatchPlayerResult[] Players = System.Array.Empty<MatchPlayerResult>();
    }

    /// <summary>
    /// TLS-E03-S02: the dedicated server's only path to the Java/Spring Boot backend
    /// (ARCHITECTURE_RULES.md Rule 6 — the backend is the source of truth; gameplay code never talks
    /// HTTP directly). <see cref="StubBackendClient"/> lets the server run a full match with zero
    /// backend dependency; <see cref="HttpBackendClient"/> implements the real calls once a backend
    /// exists, per docs/pivot/02_BACKEND_CONTRACT.md.
    /// </summary>
    public interface IBackendClient
    {
        Task<JoinTokenValidationResult> ValidateTokenAsync(string joinToken, ulong clientTransportId);

        /// <summary>Wired in TLS-E03-S03.</summary>
        Task<HeartbeatResult> SendHeartbeatAsync(int connectedPlayerCount, string sessionState, float uptimeSeconds);

        /// <summary>Wired in TLS-E03-S03.</summary>
        Task<bool> PostResultAsync(MatchResult result);
    }
}
