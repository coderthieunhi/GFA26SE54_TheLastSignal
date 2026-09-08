using Unity.Netcode;
using UnityEngine;

namespace TheLastSignal.Core
{
    /// <summary>
    /// Thin wrapper over Unity Netcode's NetworkManager. Centralizes StartHost/StartClient/Shutdown
    /// calls and connection-approval hookup so the rest of the project never talks to NetworkManager
    /// directly.
    /// </summary>
    [RequireComponent(typeof(NetworkManager))]
    public class NetworkManagerExt : MonoBehaviour
    {
        private NetworkManager networkManager;

        private void Awake()
        {
            networkManager = GetComponent<NetworkManager>();
        }

        /// <summary>Starts this instance as the Host (listen server) for a Host-Client session.</summary>
        public void StartHost()
        {
            // TODO: configure Unity Transport connection data, then call networkManager.StartHost().
        }

        /// <summary>Starts this instance as a Client connecting to a Host.</summary>
        public void StartClient()
        {
            // TODO: configure Unity Transport connection data, then call networkManager.StartClient().
        }

        /// <summary>Shuts down the active Netcode session, whether Host or Client.</summary>
        public void Shutdown()
        {
            // TODO: call networkManager.Shutdown() and clean up session state.
        }
    }
}
