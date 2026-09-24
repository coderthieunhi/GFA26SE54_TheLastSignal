using TheLastSignal.Entities.Player;

namespace TheLastSignal.Core.Interaction
{
    /// <summary>
    /// Common interface for any interactive entity in the game (Doors, Generators, Items, Stations, etc.).
    /// </summary>
    public interface IInteractable
    {
        /// <summary>
        /// Prompt text shown to the player (e.g. "[E] Open Door", "[E] Close Door", "[E] Repair Generator").
        /// </summary>
        string InteractionPrompt { get; }

        /// <summary>
        /// Gets the prompt message displayed to the player when approaching this object.
        /// Maintained for backward compatibility.
        /// </summary>
        string GetInteractionPrompt();

        /// <summary>
        /// Checks if the given player is currently allowed to interact with this object.
        /// </summary>
        /// <param name="player">The player attempting the interaction.</param>
        /// <returns>True if interaction is valid; otherwise false.</returns>
        bool CanInteract(PlayerController player);

        /// <summary>
        /// Executes the interaction logic when triggered by the player.
        /// </summary>
        /// <param name="player">The player who initiated the interaction.</param>
        void Interact(PlayerController player);
    }
}
