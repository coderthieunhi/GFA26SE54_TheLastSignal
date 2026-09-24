using System.Collections.Generic;
using UnityEngine;
using TheLastSignal.Core.Interaction;
using TheLastSignal.Entities.Player;

namespace TheLastSignal.Entities.Environment
{
    public enum DoorState
    {
        Closed,
        Open
    }

    /// <summary>
    /// Manages state for interactive facility doors. Implements unified IInteractable interface.
    /// Toggles BoxCollider2D blocker and updates visual state when interacted with ([E]).
    /// Maintains static registry for network synchronization.
    /// </summary>
    [RequireComponent(typeof(BoxCollider2D))]
    public class DoorController : MonoBehaviour, IInteractable
    {
        private static readonly Dictionary<string, DoorController> doorRegistry = new Dictionary<string, DoorController>();

        public static DoorController GetDoor(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            doorRegistry.TryGetValue(name, out var door);
            return door;
        }

        public static IReadOnlyDictionary<string, DoorController> AllDoors => doorRegistry;

        [Header("State")]
        [SerializeField] private DoorState currentState = DoorState.Closed;
        [SerializeField] private string doorName = "Door";

        [Header("Components")]
        [SerializeField] private BoxCollider2D physicalCollider;
        [SerializeField] private SpriteRenderer spriteRenderer;

        [Header("Visual Feedback")]
        [SerializeField] private Color closedColor = new Color(0.95f, 0.45f, 0.15f, 1f); // Warning orange/red
        [SerializeField] private Color openColor = new Color(0.2f, 0.8f, 0.4f, 0.35f);    // Translucent green

        public string DoorName => doorName;
        public DoorState CurrentState => currentState;
        public bool IsOpen => currentState == DoorState.Open;

        public string InteractionPrompt => currentState == DoorState.Open ? $"Close {doorName}" : $"Open {doorName}";
        public string GetInteractionPrompt() => InteractionPrompt;

        public bool CanInteract(PlayerController player) => true;

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

            RegisterDoor();
            ApplyState();
        }

        private void OnDestroy()
        {
            UnregisterDoor();
        }

        private void RegisterDoor()
        {
            string key = string.IsNullOrEmpty(doorName) ? gameObject.name : doorName;
            doorRegistry[key] = this;
        }

        private void UnregisterDoor()
        {
            string key = string.IsNullOrEmpty(doorName) ? gameObject.name : doorName;
            if (doorRegistry.TryGetValue(key, out var registered) && registered == this)
            {
                doorRegistry.Remove(key);
            }
        }

        public void Initialize(string name, DoorState initialState = DoorState.Closed)
        {
            UnregisterDoor();
            doorName = name;
            currentState = initialState;
            RegisterDoor();
            ApplyState();
        }

        /// <summary>
        /// Interaction handler called by PlayerController via unified IInteractable.
        /// </summary>
        public void Interact(PlayerController player)
        {
            OnInteract();
        }

        /// <summary>
        /// Backward compatibility interaction handler.
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
