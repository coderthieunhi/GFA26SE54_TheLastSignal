using UnityEngine;

namespace TheLastSignal.Entities.Environment
{
    /// <summary>
    /// Minimal marker component for task and objective locations.
    /// Stores locationId for Epic 8 (Task System) queries without implementing task logic yet.
    /// </summary>
    public class TaskLocationMarker : MonoBehaviour
    {
        public string locationId;

        public void Initialize(string id)
        {
            locationId = id;
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.2f, 0.9f, 1.0f, 0.7f);
            Gizmos.DrawWireSphere(transform.position, 0.35f);
            UnityEditor.Handles.Label(transform.position + Vector3.up * 0.4f, locationId);
        }
#endif
    }
}
