using UnityEngine;
using TheLastSignal.Systems.Tasks;

namespace TheLastSignal.Entities.Stations
{
    /// <summary>
    /// Base view for every interactable station. Concrete stations (GeneratorStation, RadarStation, ...)
    /// override Interact() to forward player input via ServerRpc and OnTicketAssigned() to react
    /// visually to a task ticket assigned by TaskTicketSystem. Never contains game rules itself, per
    /// ARCHITECTURE_RULES.md Rule 2.
    /// </summary>
    public abstract class StationView : MonoBehaviour
    {
        /// <summary>Called when a player interacts with this station. Must route through a ServerRpc.</summary>
        public abstract void Interact();

        /// <summary>Called when TaskTicketSystem (via OnTaskGenerated) assigns a ticket to this station.</summary>
        public abstract void OnTicketAssigned(TaskData ticket);
    }
}
