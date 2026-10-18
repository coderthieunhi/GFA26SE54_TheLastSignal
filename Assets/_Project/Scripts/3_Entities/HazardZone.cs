using UnityEngine;
using TheLastSignal.Core;
using TheLastSignal.Entities.Player;

namespace TheLastSignal.Entities
{
    /// <summary>
    /// Prototype hazard / death zone. Serves as a placeholder lose trigger before Sanity
    /// or The Shade systems are introduced in later epics.
    /// Reports to GameManager (1_Core), conforming to ARCHITECTURE_RULES.md Rule 2.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class HazardZone : MonoBehaviour
    {
#pragma warning disable CS0414
        [SerializeField] private string hazardName = "Hazard Zone";
#pragma warning restore CS0414
        private bool isTriggered = false;

        public bool IsTriggered => isTriggered;

        private void Awake()
        {
            // Hazard Zone is superseded by PlayerHealth down/revive system.
            // Self-deactivate so it never affects gameplay or displays visuals.
            gameObject.SetActive(false);
        }

        public void ResetTrigger()
        {
            isTriggered = false;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            // Deprecated: Hazard trigger disabled.
        }

        private void OnDrawGizmos()
        {
            // Gizmo hidden to clean up scene view
        }
    }
}
