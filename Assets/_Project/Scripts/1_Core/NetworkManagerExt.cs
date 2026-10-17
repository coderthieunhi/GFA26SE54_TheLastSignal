using System;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;

namespace TheLastSignal.Core
{
    /// <summary>
    /// Thin wrapper over Unity Netcode's NetworkManager. Centralizes Relay allocation plus
    /// StartHost/StartClient/Shutdown so the rest of the project never talks to NetworkManager or
    /// UnityTransport directly.
    /// Story 3.1 scope: Relay + transport wiring only. Lobby/session browsing is Story 3.2.
    /// </summary>
    [RequireComponent(typeof(NetworkManager))]
    public class NetworkManagerExt : MonoBehaviour
    {
        /// <summary>Relay counts peers excluding the host, so 3 here means a 4-player session.</summary>
        private const int DefaultMaxConnections = 3;

        /// <summary>Raw UDP. Relay also accepts "dtls" and "wss"; udp avoids TLS cert issues in dev.</summary>
        private const string RelayConnectionType = "udp";

        [SerializeField, Range(1, 15)] private int maxConnections = DefaultMaxConnections;

        private NetworkManager networkManager;
        private UnityTransport transport;

        /// <summary>Join code from the most recent CreateRelay / JoinRelay call, or null.</summary>
        public string JoinCode { get; private set; }

        private void Awake()
        {
            networkManager = GetComponent<NetworkManager>();
            transport = networkManager.NetworkConfig?.NetworkTransport as UnityTransport;

            if (transport == null)
            {
                transport = GetComponent<UnityTransport>();
            }

            if (transport == null)
            {
                Debug.LogError("[NetworkManagerExt] No UnityTransport found on the NetworkManager. " +
                               "Relay cannot be wired up.");
            }
        }

        /// <summary>
        /// Allocates a Relay server, applies it to UnityTransport, and returns the join code other
        /// players type in. Region is left unset so Relay picks the default for this account.
        /// Requires an anonymous sign-in to have completed first (see GameManager).
        /// </summary>
        public async Task<string> CreateRelay()
        {
            if (transport == null)
            {
                throw new InvalidOperationException("UnityTransport is missing; cannot create a Relay allocation.");
            }

            Allocation allocation = await RelayService.Instance.CreateAllocationAsync(maxConnections);
            string joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

            transport.SetRelayServerData(allocation.ToRelayServerData(RelayConnectionType));

            JoinCode = joinCode;
            Debug.Log($"[NetworkManagerExt] Relay allocation created. Join code: {joinCode}");
            return joinCode;
        }

        /// <summary>
        /// Joins an existing Relay allocation by code and applies it to UnityTransport.
        /// Requires an anonymous sign-in to have completed first (see GameManager).
        /// </summary>
        public async Task JoinRelay(string joinCode)
        {
            if (transport == null)
            {
                throw new InvalidOperationException("UnityTransport is missing; cannot join a Relay allocation.");
            }

            if (string.IsNullOrWhiteSpace(joinCode))
            {
                throw new ArgumentException("Join code is empty.", nameof(joinCode));
            }

            JoinAllocation joinAllocation = await RelayService.Instance.JoinAllocationAsync(joinCode.Trim());

            transport.SetRelayServerData(joinAllocation.ToRelayServerData(RelayConnectionType));

            JoinCode = joinCode.Trim();
            Debug.Log($"[NetworkManagerExt] Joined Relay allocation with code: {JoinCode}");
        }

        /// <summary>Starts this instance as the Host (listen server) for a Host-Client session.</summary>
        public bool StartHost()
        {
            return networkManager != null && networkManager.StartHost();
        }

        /// <summary>
        /// TLS-E03-S01: starts this instance as a headless Dedicated Server, binding Unity Transport
        /// directly to <paramref name="bindAddress"/>:<paramref name="port"/> — no Relay allocation, no
        /// Unity Services dependency (ARCHITECTURE_RULES.md Rule 6/decision: direct IP:port only).
        /// Uses UnityTransport.SetConnectionData's forceOverrideCommandLineArgs=true overload
        /// deliberately: by default that method also re-parses its own "-port"/"-ip" command-line
        /// arguments and would silently override whatever is passed here — forcing it off keeps
        /// ServerBootstrap's parsed LaunchModeConfig as the single source of truth for these values.
        /// </summary>
        public bool StartServerDirect(string bindAddress, ushort port)
        {
            if (transport == null || networkManager == null)
            {
                Debug.LogError("[NetworkManagerExt] Cannot start dedicated server: transport or NetworkManager missing.");
                return false;
            }

            transport.SetConnectionData(forceOverrideCommandLineArgs: true, ipv4Address: bindAddress, port: port, listenAddress: bindAddress);
            return networkManager.StartServer();
        }

        /// <summary>Starts this instance as a Client connecting to the Relay-allocated Host.</summary>
        public bool StartClient()
        {
            return networkManager != null && networkManager.StartClient();
        }

        /// <summary>
        /// TLS-E03-S02: connects directly to a Dedicated Server by IP:port, presenting
        /// <paramref name="joinTokenPayload"/> as the connection approval payload — no Relay, no join
        /// code. NetworkConfig.ConnectionApproval must be true on the CLIENT too (not just the server):
        /// NGO's own ConnectionRequestMessage only includes ConnectionData when the sending peer's own
        /// NetworkConfig.ConnectionApproval is set, so this is set here rather than left to the caller.
        /// </summary>
        public bool ConnectDirect(string serverAddress, ushort port, byte[] joinTokenPayload)
        {
            if (transport == null || networkManager == null)
            {
                Debug.LogError("[NetworkManagerExt] Cannot connect: transport or NetworkManager missing.");
                return false;
            }

            transport.SetConnectionData(forceOverrideCommandLineArgs: true, ipv4Address: serverAddress, port: port);
            networkManager.NetworkConfig.ConnectionApproval = true;
            networkManager.NetworkConfig.ConnectionData = joinTokenPayload ?? Array.Empty<byte>();

            return networkManager.StartClient();
        }

        /// <summary>Shuts down the active Netcode session, whether Host or Client.</summary>
        public void Shutdown()
        {
            if (networkManager != null && networkManager.IsListening)
            {
                networkManager.Shutdown();
            }

            JoinCode = null;
        }
    }
}
