using UnityEngine;

namespace TheLastSignal.Entities.Player
{
    /// <summary>
    /// Represents a designated spawn location for a player in the facility.
    /// Provides gizmo visualization in the Unity Editor and supports indexing for 1-4 players.
    /// </summary>
    public class SpawnPoint : MonoBehaviour
    {
        [SerializeField] private int playerIndex = 0;
        [SerializeField] private bool isOccupied = false;

        /// <summary>The player slot index (0 for P1, 1 for P2, 2 for P3, 3 for P4).</summary>
        public int PlayerIndex => playerIndex;

        /// <summary>Whether this spawn point currently has a player spawned at it.</summary>
        public bool IsOccupied
        {
            get => isOccupied;
            set => isOccupied = value;
        }

        public Vector3 Position => transform.position;

        public void Initialize(int index)
        {
            playerIndex = index;
            isOccupied = false;
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            Color gizmoColor;
            switch (playerIndex)
            {
                case 0: gizmoColor = new Color(0.2f, 0.8f, 1.0f, 0.8f); break; // Cyan P1
                case 1: gizmoColor = new Color(1.0f, 0.5f, 0.2f, 0.8f); break; // Orange P2
                case 2: gizmoColor = new Color(0.4f, 1.0f, 0.4f, 0.8f); break; // Green P3
                case 3: gizmoColor = new Color(0.9f, 0.4f, 0.9f, 0.8f); break; // Magenta P4
                default: gizmoColor = Color.yellow; break;
            }

            Gizmos.color = isOccupied ? Color.gray : gizmoColor;
            Gizmos.DrawWireSphere(transform.position, 0.4f);

            // Draw directional pointer (facing down into the room)
            Gizmos.DrawLine(transform.position, transform.position + Vector3.down * 0.6f);

            // Draw player label
            UnityEditor.Handles.Label(transform.position + Vector3.up * 0.5f, $"P{playerIndex + 1}");
        }
#endif
    }
}
