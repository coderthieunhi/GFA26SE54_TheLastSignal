using TheLastSignal.Systems.Tasks;

namespace TheLastSignal.Entities.Stations
{
    /// <summary>Radar station view. Presents radar-specific visuals and interaction prompts.</summary>
    public class RadarStation : StationView
    {
        /// <inheritdoc />
        public override void Interact()
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
