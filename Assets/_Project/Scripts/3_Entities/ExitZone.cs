using UnityEngine;
using TheLastSignal.Core;
using TheLastSignal.Entities.Player;

namespace TheLastSignal.Entities
{
    /// <summary>
    /// Prototype win trigger. Does not own objective rules — reports to GameManager (1_Core),
    /// which updates SimpleObjectiveSystem. Replaced by Task stations in Epic 8.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class ExitZone : MonoBehaviour
    {
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
                Debug.Log("[ExitZone] Player reached exit zone; notifying win condition.");
                GameManager.Instance.NotifyExitReached();
            }
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.2f, 1f, 0.4f, 0.35f);
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
