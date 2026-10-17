namespace TheLastSignal.Core.Bootstrap
{
    /// <summary>
    /// TLS-E03-S01: the resolved launch configuration for this process — either parsed from command-line
    /// arguments by <see cref="ServerBootstrap"/> (a built DedicatedServer), or left at its defaults
    /// (LegacyClientHost, unchanged from today's behavior). Plain data only; never touches Unity APIs.
    /// </summary>
    [System.Serializable]
    public class LaunchModeConfig
    {
        public LaunchMode Mode = LaunchMode.LegacyClientHost;

        /// <summary>UDP port Unity Transport binds to / connects to. Also recognized by Unity Transport
        /// itself as its own "-port" override (see ServerBootstrap's forceOverrideCommandLineArgs note) —
        /// kept identical on purpose so both layers agree on the same value.</summary>
        public ushort Port = 7777;

        /// <summary>Opaque match identifier from the backend. Empty until Epic 4 issues real ones.</summary>
        public string MatchId = string.Empty;

        /// <summary>Base URL of the Java/Spring Boot backend. Empty selects the local stub (TLS-E03-S02).</summary>
        public string BackendUrl = string.Empty;

        /// <summary>Shared secret this server presents to the backend. Never logged in full — see
        /// ServerBootstrap.DescribeForLog.</summary>
        public string ServerSecret = string.Empty;

        /// <summary>Survivor + Shade slot count. Clamped to PlayerSpawnPoint.MaxSlots (4) on parse.</summary>
        public int MaxPlayers = 4;

        /// <summary>UTP bind/listen address. "0.0.0.0" inside a container, published via Docker's -p.</summary>
        public string BindAddress = "0.0.0.0";

        /// <summary>
        /// TLS-E03-S02: not part of the real backend contract — only read by StubBackendClient (selected
        /// when BackendUrl is empty) as the one join-token value it will accept. Lets the spike exercise
        /// approve/reject without a real backend. Configurable via "-testToken"; never used by
        /// HttpBackendClient, which defers entirely to the real backend's own validation.
        /// </summary>
        public string TestJoinToken = "dev-token";

        /// <summary>
        /// TLS-E03-S03: abort the match if zero clients have been connected for this many consecutive
        /// seconds. Configurable via "-emptyTimeoutSeconds". Default 60s.
        /// </summary>
        public float EmptyTimeoutSeconds = 60f;

        /// <summary>
        /// TLS-E03-S03: abort the match if it has run for longer than this, regardless of player count.
        /// Configurable via "-matchTimeoutSeconds". Default 1800s (30 min) — a placeholder ceiling, not a
        /// balanced value; tune once real match pacing exists.
        /// </summary>
        public float MatchTimeoutSeconds = 1800f;
    }
}
