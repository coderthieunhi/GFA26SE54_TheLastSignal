using TheLastSignal.Systems.Tasks;
using TheLastSignal.Entities.Player;

namespace TheLastSignal.Entities.Stations
{
    /// <summary>Generator station view. Presents generator-specific visuals and interaction prompts.</summary>
    public class GeneratorStation : StationView
    {
        private void Awake()
        {
            promptMessage = "[E] Repair Generator";
        }

        /// <inheritdoc />
        protected override void ProcessInteraction(PlayerController player)
        {
            // TODO: forward this interaction to the Host via ServerRpc.
        }

        /// <inheritdoc />
        public override void OnTicketAssigned(TaskData ticket)
        {
            // TODO: react visually (icon, prompt, minigame panel) to the assigned ticket.
        }
    }
}
