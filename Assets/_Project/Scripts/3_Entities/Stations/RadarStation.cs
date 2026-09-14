using TheLastSignal.Systems.Tasks;
using TheLastSignal.Entities.Player;

namespace TheLastSignal.Entities.Stations
{
    /// <summary>Radar station view. Presents radar-specific visuals and interaction prompts.</summary>
    public class RadarStation : StationView
    {
        private void Awake()
        {
            promptMessage = "[E] Use Radar";
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
