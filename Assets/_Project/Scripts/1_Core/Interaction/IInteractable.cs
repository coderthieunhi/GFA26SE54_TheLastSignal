using TheLastSignal.Entities.Player;

namespace TheLastSignal.Core.Interaction
{
    /// <summary>
    /// Common interface for any interactive entity in the game (Doors, Generators, Items, Stations, etc.).
    /// </summary>
    public interface IInteractable
    {
        /// <summary>
        /// Gets the prompt message displayed to the player when approaching this object.
        /// For example: "[E] Repair Generator" or "[E] Open Door".
        /// </summary>
        /// <returns>The prompt string to display on the UI.</returns>
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
