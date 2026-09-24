using UnityEngine;
using TheLastSignal.Core.Interaction;
using TheLastSignal.Entities.Player;

namespace TheLastSignal.Entities.Environment
{
    /// <summary>
    /// Placeholder facility door. Implements unified IInteractable to hook into PlayerController interaction system.
    /// Manages open/closed state, toggling its solid collider to block or allow player passage.
    /// </summary>
    public class Door : MonoBehaviour, IInteractable
    {
        [Header("State")]
        [SerializeField] private bool isOpen = false;
        [SerializeField] private string doorName = "Door";

        [Header("Colliders")]
        [Tooltip("Solid obstacle collider that physically blocks the player when closed.")]
        [SerializeField] private Collider2D solidCollider;

        [Tooltip("Trigger collider on the Interactable layer that detects interaction range.")]
        [SerializeField] private Collider2D interactableTrigger;

        [Header("Visual Feedback")]
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private Color closedColor = new Color(0.95f, 0.45f, 0.15f, 1f); // Orange/Red warning
        [SerializeField] private Color openColor = new Color(0.2f, 0.8f, 0.4f, 0.35f);    // Translucent Green

        public bool IsOpen => isOpen;

        public string InteractionPrompt => isOpen ? $"Close {doorName}" : $"Open {doorName}";
        public string GetInteractionPrompt() => InteractionPrompt;

        public bool CanInteract(PlayerController player) => true;

        private void Awake()
        {
            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            }

            // If colliders were not manually linked in inspector, auto-resolve
            if (solidCollider == null || interactableTrigger == null)
            {
                Collider2D[] colliders = GetComponentsInChildren<Collider2D>(true);
                foreach (var col in colliders)
                {
                    if (col.isTrigger && interactableTrigger == null)
                    {
                        interactableTrigger = col;
                    }
                    else if (!col.isTrigger && solidCollider == null)
                    {
                        solidCollider = col;
                    }
                }
            }

            ApplyState(isOpen);
        }

        public void Interact(PlayerController player)
        {
            SetOpen(!isOpen);
        }

        public void Interact(GameObject player)
        {
            SetOpen(!isOpen);
        }

        public void SetOpen(bool open)
        {
            isOpen = open;
            ApplyState(isOpen);
            Debug.Log($"[Door] {doorName} is now {(isOpen ? "OPEN (Passable)" : "CLOSED (Blocked)")}");
        }

        private void ApplyState(bool open)
        {
            if (solidCollider != null)
            {
                solidCollider.enabled = !open;
            }

            if (spriteRenderer != null)
            {
                spriteRenderer.color = open ? openColor : closedColor;
            }
        }
    }
}
