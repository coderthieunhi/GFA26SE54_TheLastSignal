using UnityEngine;

namespace TheLastSignal.Entities.Interaction
{
    /// <summary>
    /// Common contract for any in-game entity that can be interacted with by a player.
    /// Used by PlayerController when pressing the interaction key (E).
    /// </summary>
    public interface IInteractable
    {
        /// <summary>Prompt text shown to the player (e.g. "Open Door", "Close Door", "Examine").</summary>
        string InteractionPrompt { get; }

        /// <summary>Whether this entity can currently be interacted with.</summary>
        bool CanInteract { get; }

        /// <summary>Triggered when a player interacts with this object.</summary>
        /// <param name="player">The GameObject of the player initiating the interaction.</param>
        void Interact(GameObject player);
    }
}
