using UnityEngine;

namespace TheLastSignal.UI
{
    /// <summary>
    /// Drives the in-match HUD (sanity meter, task prompts, monster indicator). Strictly visual —
    /// subscribes to OnSanityChanged, OnTaskGenerated, and OnMonsterStateChanged and never contains
    /// game rules, per ARCHITECTURE_RULES.md Rule 2.
    /// </summary>
    public class HUDManager : MonoBehaviour
    {
        private void OnEnable()
        {
            // TODO: subscribe to OnSanityChanged, OnTaskGenerated, OnMonsterStateChanged.
        }

        private void OnDisable()
        {
            // TODO: unsubscribe from all channels subscribed to in OnEnable.
        }
    }
}
