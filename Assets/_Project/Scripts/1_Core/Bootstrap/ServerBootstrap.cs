using System;
using UnityEngine;

namespace TheLastSignal.Core.Bootstrap
{
    /// <summary>
    /// TLS-E03-S01: parses the process's command-line arguments into a <see cref="LaunchModeConfig"/>.
    /// Pure parsing only — no Unity lifecycle, no NetworkManager/GameManager calls (that wiring lives in
    /// GameManager.Start(), gated on the mode this class resolves). Never throws: every malformed or
    /// missing value falls back to a safe default and logs a warning instead, per the brief's
    /// "never a crash" requirement.
    /// </summary>
    public static class ServerBootstrap
    {
        private const string ModeArg = "-mode";
        private const string PortArg = "-port";
        private const string MatchIdArg = "-matchId";
        private const string BackendUrlArg = "-backendUrl";
        private const string ServerSecretArg = "-serverSecret";
        private const string MaxPlayersArg = "-maxPlayers";
        private const string BindAddressArg = "-bindAddress";

        /// <summary>TLS-E03-S02 addition beyond the original Phase 1 contract — see
        /// LaunchModeConfig.TestJoinToken for why this exists (StubBackendClient only).</summary>
        private const string TestTokenArg = "-testToken";

        /// <summary>TLS-E03-S05 — see LaunchModeConfig.StubAcceptAnyToken. Off unless explicitly passed.</summary>
        private const string StubAcceptAnyArg = "-stubAcceptAny";

        /// <summary>TLS-E03-S03 additions — see LaunchModeConfig.EmptyTimeoutSeconds/MatchTimeoutSeconds.</summary>
        private const string EmptyTimeoutArg = "-emptyTimeoutSeconds";
        private const string MatchTimeoutArg = "-matchTimeoutSeconds";

        /// <summary>
        /// Parses <paramref name="args"/> (pass <c>Environment.GetCommandLineArgs()</c>) into a config.
        /// With no recognized arguments at all, returns the all-defaults LegacyClientHost config — i.e.
        /// the game behaves exactly as before when launched with no special arguments.
        /// </summary>
        public static LaunchModeConfig ParseArgs(string[] args)
        {
            LaunchModeConfig config = new LaunchModeConfig();

            if (args == null || args.Length == 0)
            {
                return config;
            }

            for (int i = 0; i < args.Length; i++)
            {
                string key = args[i];
                string value = (i + 1 < args.Length) ? args[i + 1] : null;

                switch (key)
                {
                    case ModeArg:
                        config.Mode = ParseMode(value);
                        i++;
                        break;

                    case PortArg:
                        config.Port = ParseUShort(value, config.Port, PortArg);
                        i++;
                        break;

                    case MatchIdArg:
                        config.MatchId = value ?? config.MatchId;
                        i++;
                        break;

                    case BackendUrlArg:
                        config.BackendUrl = value ?? config.BackendUrl;
                        i++;
                        break;

                    case ServerSecretArg:
                        config.ServerSecret = value ?? config.ServerSecret;
                        i++;
                        break;

                    case MaxPlayersArg:
                        config.MaxPlayers = ParseMaxPlayers(value, config.MaxPlayers);
                        i++;
                        break;

                    case BindAddressArg:
                        config.BindAddress = string.IsNullOrWhiteSpace(value) ? config.BindAddress : value;
                        i++;
                        break;

                    case TestTokenArg:
                        config.TestJoinToken = string.IsNullOrEmpty(value) ? config.TestJoinToken : value;
                        i++;
                        break;

                    case StubAcceptAnyArg:
                        config.StubAcceptAnyToken = ParseBool(value, config.StubAcceptAnyToken, StubAcceptAnyArg);
                        i++;
                        break;

                    case EmptyTimeoutArg:
                        config.EmptyTimeoutSeconds = ParseFloat(value, config.EmptyTimeoutSeconds, EmptyTimeoutArg);
                        i++;
                        break;

                    case MatchTimeoutArg:
                        config.MatchTimeoutSeconds = ParseFloat(value, config.MatchTimeoutSeconds, MatchTimeoutArg);
                        i++;
                        break;

                    default:
                        // Unknown token (including Unity's own "-batchmode", "-nographics", the exe
                        // path, etc.) — ignore and move on. Never treat an unrecognized argument as fatal.
                        break;
                }
            }

            return config;
        }

        private static LaunchMode ParseMode(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                Debug.LogWarning($"[ServerBootstrap] '{ModeArg}' given with no value; defaulting to {LaunchMode.LegacyClientHost}.");
                return LaunchMode.LegacyClientHost;
            }

            switch (value.Trim().ToLowerInvariant())
            {
                case "server":
                case "dedicatedserver":
                case "dedicated-server":
                    return LaunchMode.DedicatedServer;

                case "client":
                    return LaunchMode.Client;

                case "legacy":
                case "legacyclienthost":
                case "host":
                    return LaunchMode.LegacyClientHost;

                default:
                    Debug.LogWarning($"[ServerBootstrap] Unrecognized '{ModeArg}' value \"{value}\"; defaulting to {LaunchMode.LegacyClientHost}.");
                    return LaunchMode.LegacyClientHost;
            }
        }

        private static ushort ParseUShort(string value, ushort fallback, string argNameForLog)
        {
            if (ushort.TryParse(value, out ushort parsed))
            {
                return parsed;
            }

            Debug.LogWarning($"[ServerBootstrap] '{argNameForLog}' value \"{value}\" is not a valid port; defaulting to {fallback}.");
            return fallback;
        }

        private static float ParseFloat(string value, float fallback, string argNameForLog)
        {
            if (float.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float parsed) && parsed >= 0f)
            {
                return parsed;
            }

            Debug.LogWarning($"[ServerBootstrap] '{argNameForLog}' value \"{value}\" is not a valid non-negative number; defaulting to {fallback}.");
            return fallback;
        }

        private static bool ParseBool(string value, bool fallback, string argNameForLog)
        {
            if (bool.TryParse(value, out bool parsed))
            {
                return parsed;
            }

            Debug.LogWarning($"[ServerBootstrap] '{argNameForLog}' value \"{value}\" is not a valid bool; defaulting to {fallback}.");
            return fallback;
        }

        private static int ParseMaxPlayers(string value, int fallback)
        {
            if (!int.TryParse(value, out int parsed))
            {
                Debug.LogWarning($"[ServerBootstrap] '{MaxPlayersArg}' value \"{value}\" is not a valid integer; defaulting to {fallback}.");
                return fallback;
            }

            int clamped = Mathf.Clamp(parsed, 1, PlayerSpawner.MaxPlayers);
            if (clamped != parsed)
            {
                Debug.LogWarning($"[ServerBootstrap] '{MaxPlayersArg}' value {parsed} is out of range; clamped to {clamped}.");
            }

            return clamped;
        }

        /// <summary>
        /// A log-safe description of <paramref name="config"/> — every field except ServerSecret, which
        /// is masked to its length only (per the brief: never log secrets or tokens in full).
        /// </summary>
        public static string DescribeForLog(LaunchModeConfig config)
        {
            string maskedSecret = string.IsNullOrEmpty(config.ServerSecret)
                ? "<empty>"
                : $"<set, {config.ServerSecret.Length} chars>";

            return $"Mode={config.Mode}, Port={config.Port}, MatchId=\"{config.MatchId}\", " +
                   $"BackendUrl=\"{config.BackendUrl}\", ServerSecret={maskedSecret}, " +
                   $"MaxPlayers={config.MaxPlayers}, BindAddress=\"{config.BindAddress}\", " +
                   $"TestJoinToken=\"{config.TestJoinToken}\" (StubBackendClient only), " +
                   $"StubAcceptAnyToken={config.StubAcceptAnyToken}, " +
                   $"EmptyTimeoutSeconds={config.EmptyTimeoutSeconds}, MatchTimeoutSeconds={config.MatchTimeoutSeconds}";
        }
    }
}
