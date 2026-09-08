using UnityEngine;

namespace TheLastSignal.UI
{
    /// <summary>
    /// Presents the pre-match lobby (host/join buttons, connected player list, role display). Strictly
    /// visual — forwards Host/Client start requests to NetworkManagerExt, contains no game rules.
    /// </summary>
    public class LobbyUI : MonoBehaviour
    {
        /// <summary>Bound to the "Host" button.</summary>
        public void OnHostButtonPressed()
        {
            // TODO: call NetworkManagerExt.StartHost() and switch to the connecting/lobby-populated view.
        }

        /// <summary>Bound to the "Join" button.</summary>
        public void OnJoinButtonPressed()
        {
            // TODO: call NetworkManagerExt.StartClient() and switch to the connecting/lobby-populated view.
        }
    }
}
