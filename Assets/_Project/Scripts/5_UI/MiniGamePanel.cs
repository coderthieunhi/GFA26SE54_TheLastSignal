using UnityEngine;
using TheLastSignal.Systems.Tasks;

namespace TheLastSignal.UI
{
    /// <summary>
    /// Presents the active minigame/task-ticket UI to the local player. Strictly visual — captures
    /// player input and forwards it up via ServerRpc per ARCHITECTURE_RULES.md Rule 3; never resolves
    /// the minigame outcome itself.
    /// </summary>
    public class MiniGamePanel : MonoBehaviour
    {
        /// <summary>Opens the panel for the given ticket, assigned via OnTaskGenerated.</summary>
        public void Show(TaskData ticket)
        {
            // TODO: populate the panel UI from ticket and make it visible.
        }

        /// <summary>Hides the panel.</summary>
        public void Hide()
        {
            // TODO: clear panel state and make it invisible.
        }
    }
}
