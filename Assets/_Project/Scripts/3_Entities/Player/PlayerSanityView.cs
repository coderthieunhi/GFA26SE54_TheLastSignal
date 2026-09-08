using UnityEngine;

namespace TheLastSignal.Entities.Player
{
    /// <summary>
    /// Purely visual reaction to sanity changes (screen effects, animation, etc.). Subscribes to
    /// OnSanityChanged and never computes or writes sanity state itself, per ARCHITECTURE_RULES.md Rule 2.
    /// </summary>
    public class PlayerSanityView : MonoBehaviour
    {
        private void OnEnable()
        {
            // TODO: subscribe to the OnSanityChanged EventChannelSO (or a GameEventListener component).
        }

        private void OnDisable()
        {
            // TODO: unsubscribe from the OnSanityChanged EventChannelSO.
        }

        private void HandleSanityChanged(float newSanity)
        {
            // TODO: drive purely visual feedback (vignette, audio, animation) from newSanity.
        }
    }
}
