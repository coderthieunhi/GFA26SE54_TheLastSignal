using System;
using UnityEngine;
using UnityEngine.Events;
using TheLastSignal.Entities.Player;

namespace TheLastSignal.Core.Interaction
{
    /// <summary>
    /// Base implementation for IInteractable. Provides generic range and availability validation
    /// so PlayerController doesn't hardcode logic.
    /// </summary>
    public abstract class InteractableBase : MonoBehaviour, IInteractable
    {
        [Header("Interaction Settings")]
        [SerializeField] protected string promptMessage = "[E] Tương tác";
        [SerializeField] protected bool isAvailable = true;

        [Header("Events")]
        public UnityEvent<PlayerController> onInteractionSuccess;
        public UnityEvent<PlayerController> onInteractionFailed;

        public event Action<PlayerController> OnInteractionSuccessEvent;
        public event Action<PlayerController> OnInteractionFailedEvent;

        /// <summary>
        /// Gets or sets whether this object is currently busy, locked, or unavailable.
        /// </summary>
        public bool IsAvailable 
        { 
            get => isAvailable; 
            set => isAvailable = value; 
        }

        public virtual string InteractionPrompt => GetInteractionPrompt();

        public virtual string GetInteractionPrompt()
        {
            return promptMessage;
        }

        public virtual bool CanInteract(PlayerController player)
        {
            if (!isAvailable) return false;

            // Use closest point on collider if available, so large objects (e.g. 2x2 consoles) don't fail range check
            Collider2D col = GetComponent<Collider2D>();
            float sqrDistance;
            if (col != null)
            {
                Vector2 closest = col.ClosestPoint(player.transform.position);
                sqrDistance = (closest - (Vector2)player.transform.position).sqrMagnitude;
            }
            else
            {
                sqrDistance = (transform.position - player.transform.position).sqrMagnitude;
            }

            if (sqrDistance > (player.InteractionRange * player.InteractionRange))
            {
                return false;
            }

            return CheckCustomConditions(player);
        }

        /// <summary>
        /// Hook for child classes to evaluate specific requirements (e.g., matching Role, having a Key).
        /// </summary>
        protected virtual bool CheckCustomConditions(PlayerController player)
        {
            return true;
        }

        /// <summary>
        /// Core interaction flow. Validates, triggers hooks, and fires appropriate events.
        /// </summary>
        public void Interact(PlayerController player)
        {
            if (CanInteract(player))
            {
                ProcessInteraction(player);
                onInteractionSuccess?.Invoke(player);
                OnInteractionSuccessEvent?.Invoke(player);
            }
            else
            {
                OnInteractionFailed(player);
                onInteractionFailed?.Invoke(player);
                OnInteractionFailedEvent?.Invoke(player);
            }
        }

        /// <summary>
        /// Hook for child classes to execute their specific interaction logic (e.g., opening a door, repairing a generator).
        /// </summary>
        protected abstract void ProcessInteraction(PlayerController player);

        /// <summary>
        /// Hook for child classes to execute logic when an interaction is attempted but fails.
        /// </summary>
        protected virtual void OnInteractionFailed(PlayerController player) { }
    }
}
