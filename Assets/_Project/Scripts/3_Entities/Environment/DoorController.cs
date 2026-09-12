using UnityEngine;
using TheLastSignal.Entities.Interaction;

namespace TheLastSignal.Entities.Environment
{
    public enum DoorState
    {
        Closed,
        Open
    }

    /// <summary>
    /// Manages state for interactive facility doors. Implements project's existing IInteractable interface.
    /// Toggles BoxCollider2D blocker and updates visual state when interacted with ([E]).
    /// </summary>
    [RequireComponent(typeof(BoxCollider2D))]
    public class DoorController : MonoBehaviour, IInteractable
    {
        [Header("State")]
        [SerializeField] private DoorState currentState = DoorState.Closed;
        [SerializeField] private string doorName = "Door";

        [Header("Components")]
        [SerializeField] private BoxCollider2D physicalCollider;
        [SerializeField] private SpriteRenderer spriteRenderer;

        [Header("Visual Feedback")]
        [SerializeField] private Color closedColor = new Color(0.95f, 0.45f, 0.15f, 1f); // Warning orange/red
        [SerializeField] private Color openColor = new Color(0.2f, 0.8f, 0.4f, 0.35f);    // Translucent green

        public DoorState CurrentState => currentState;
        public bool IsOpen => currentState == DoorState.Open;

        public string InteractionPrompt => currentState == DoorState.Open ? $"Close {doorName}" : $"Open {doorName}";
        public bool CanInteract => true;

        private void Awake()
        {
            if (physicalCollider == null)
            {
                physicalCollider = GetComponent<BoxCollider2D>();
            }

            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponent<SpriteRenderer>();
            }

            ApplyState();
        }

        public void Initialize(string name, DoorState initialState = DoorState.Closed)
        {
            doorName = name;
            currentState = initialState;
            ApplyState();
        }

        /// <summary>
        /// Interaction handler called by PlayerController via IInteractable.
        /// </summary>
        public void Interact(GameObject player)
        {
            OnInteract();
        }

        /// <summary>
        /// Interaction handler to toggle door state.
        /// </summary>
        public void OnInteract()
        {
            Toggle();
        }

        /// <summary>
        /// Toggles between Open and Closed states.
        /// </summary>
        public void Toggle()
        {
            currentState = (currentState == DoorState.Closed) ? DoorState.Open : DoorState.Closed;
            ApplyState();
            Debug.Log($"<color=cyan>[DoorController]</color> {doorName} toggled to {currentState}");
        }

        public void SetState(DoorState state)
        {
            currentState = state;
            ApplyState();
        }

        private void ApplyState()
        {
            bool isOpen = (currentState == DoorState.Open);

            if (physicalCollider != null)
            {
                // Physical blocker: when Closed, acts as solid obstacle (isTrigger = false).
                // When Open, acts as passable trigger (isTrigger = true) so player can walk through
                // while still allowing the interaction system to detect and close it via [E].
                physicalCollider.isTrigger = isOpen;
                physicalCollider.enabled = true;
            }

            if (spriteRenderer != null)
            {
                spriteRenderer.color = isOpen ? openColor : closedColor;
            }
        }
    }
}
