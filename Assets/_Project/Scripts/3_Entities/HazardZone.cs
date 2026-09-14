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
        [SerializeField] private string hazardName = "Hazard Zone";
        private bool isTriggered = false;

        public bool IsTriggered => isTriggered;

        private void Reset()
        {
            Collider2D col = GetComponent<Collider2D>();
            if (col != null)
            {
                col.isTrigger = true;
            }
        }

        public void ResetTrigger()
        {
            isTriggered = false;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (isTriggered)
            {
                return;
            }

            if (other.GetComponent<PlayerController>() == null)
            {
                return;
            }

            if (GameManager.Instance != null && GameManager.Instance.IsPlaying)
            {
                isTriggered = true;
                Debug.Log($"[HazardZone] Player entered {hazardName}; notifying game failure.");
                GameManager.Instance.NotifyPlayerDied(hazardName);
            }
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.4f);
            Collider2D col = GetComponent<Collider2D>();
            if (col != null)
            {
                Gizmos.matrix = col.transform.localToWorldMatrix;
                if (col is BoxCollider2D box)
                {
                    Gizmos.DrawCube(box.offset, box.size);
                }
                else
                {
                    Gizmos.DrawCube(Vector3.zero, Vector3.one);
                }
            }
        }
    }
}
