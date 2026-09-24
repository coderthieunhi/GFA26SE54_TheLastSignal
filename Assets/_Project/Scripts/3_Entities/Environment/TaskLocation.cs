using UnityEngine;
using TheLastSignal.Core.Interaction;
using TheLastSignal.Entities.Player;

namespace TheLastSignal.Entities.Environment
{
    public enum TaskStationType
    {
        Generator,
        Computer,
        Mechanical,
        ControlPanel,
        Exit
    }

    /// <summary>
    /// Placeholder location for a gameplay objective or task station.
    /// Implements IInteractable so PlayerController can detect and interact with it via [E].
    /// Epic 8 (Task Ticket System) can easily hook concrete minigames, tickets, or objectives here.
    /// </summary>
    public class TaskLocation : MonoBehaviour, IInteractable
    {
        [Header("Task Info")]
        [SerializeField] private string stationId = "TASK_01";
        [SerializeField] private string stationDisplayName = "Station";
        [SerializeField] private TaskStationType stationType = TaskStationType.Generator;
        [SerializeField] private bool isCompleted = false;

        [Header("Feedback")]
        [SerializeField] private SpriteRenderer markerRenderer;
        [SerializeField] private Color activeColor = Color.white;
        [SerializeField] private Color completedColor = new Color(0.3f, 1f, 0.5f, 0.8f);

        public string StationId => stationId;
        public string StationDisplayName => stationDisplayName;
        public TaskStationType StationType => stationType;
        public bool IsCompleted => isCompleted;

        public string InteractionPrompt => isCompleted ? $"{stationDisplayName} (Finished)" : $"Interact with {stationDisplayName}";
        public string GetInteractionPrompt() => InteractionPrompt;
        public bool CanInteract(PlayerController player) => !isCompleted;

        private void Awake()
        {
            if (markerRenderer == null)
            {
                markerRenderer = GetComponentInChildren<SpriteRenderer>();
            }
        }

        public void Initialize(string id, string displayName, TaskStationType type)
        {
            stationId = id;
            stationDisplayName = displayName;
            stationType = type;
            isCompleted = false;
        }

        public void Interact(PlayerController player)
        {
            Interact(player != null ? player.gameObject : null);
        }

        public void Interact(GameObject player)
        {
            Debug.Log($"<color=yellow>[TaskLocation]</color> Player interacted with {stationDisplayName} ({stationId} - {stationType})!");
            // Future Epic 8 integration hook:
            // TaskTicketSystem.Instance.TryStartTask(stationId, player);
        }

        public void SetCompleted(bool completed)
        {
            isCompleted = completed;
            if (markerRenderer != null)
            {
                markerRenderer.color = completed ? completedColor : activeColor;
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            Color gizmoColor;
            switch (stationType)
            {
                case TaskStationType.Generator: gizmoColor = new Color(1f, 0.8f, 0.1f, 0.85f); break; // Yellow
                case TaskStationType.Computer: gizmoColor = new Color(0.2f, 0.8f, 1f, 0.85f); break;   // Cyan
                case TaskStationType.Mechanical: gizmoColor = new Color(0.9f, 0.5f, 0.2f, 0.85f); break; // Orange
                case TaskStationType.ControlPanel: gizmoColor = new Color(0.9f, 0.2f, 0.3f, 0.85f); break; // Red
                case TaskStationType.Exit: gizmoColor = new Color(0.2f, 1f, 0.4f, 0.85f); break;       // Green
                default: gizmoColor = Color.white; break;
            }

            Gizmos.color = gizmoColor;
            Gizmos.DrawWireCube(transform.position, new Vector3(1f, 1f, 0.1f));

            UnityEditor.Handles.Label(transform.position + Vector3.up * 0.7f, $"[{stationType}] {stationDisplayName}");
        }
#endif
    }
}
