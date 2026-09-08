using UnityEngine;

namespace TheLastSignal.Entities.Monster
{
    /// <summary>
    /// Reports raw perception data (raycasts, collider triggers) to the Host so MonsterFSM can decide
    /// what to do with it. This class never decides monster behavior itself, per ARCHITECTURE_RULES.md
    /// Rule 2/3 — it only reports; the Host decides.
    /// </summary>
    public class MonsterSensor : MonoBehaviour
    {
        private void OnTriggerEnter2D(Collider2D other)
        {
            // TODO: report the detected collider to the Host (e.g. via ServerRpc on a NetworkBehaviour)
            // so MonsterFSM can evaluate detection. Do not decide state transitions here.
        }
    }
}
