using UnityEngine;

namespace TheLastSignal.Core
{
    /// <summary>
    /// Centralizes scene load/unload calls (e.g. Lobby -> Gameplay) so the rest of the project never
    /// calls UnityEngine.SceneManagement directly.
    /// </summary>
    public class SceneLoader : MonoBehaviour
    {
        /// <summary>Loads the named scene, optionally networked via NetworkManager.SceneManager on the Host.</summary>
        public void LoadScene(string sceneName)
        {
            // TODO: on the Host, use NetworkManager.SceneManager.LoadScene for a networked scene load;
            // otherwise fall back to a local SceneManager.LoadScene for menu-only scenes.
        }
    }
}
