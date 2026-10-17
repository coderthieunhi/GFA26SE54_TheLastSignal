namespace TheLastSignal.Core.Bootstrap
{
    /// <summary>
    /// TLS-E03-S01: which code path this process runs. Preserves the existing Relay/Host-Client flow
    /// as <see cref="LegacyClientHost"/> (ARCHITECTURE_RULES.md Rule 7) while adding the two new
    /// dedicated-server paths. Resolved once at startup by <see cref="GameBootstrap"/>.
    /// </summary>
    public enum LaunchMode
    {
        /// <summary>Today's Unity Relay + Host/Join flow. The default when no launch arguments are given.</summary>
        LegacyClientHost,

        /// <summary>Headless, no local player, no Relay. Listens directly via Unity Transport.</summary>
        DedicatedServer,

        /// <summary>Connects directly to a DedicatedServer by IP:port + join token (DirectConnectUI, TLS-E03-S02).</summary>
        Client
    }
}
