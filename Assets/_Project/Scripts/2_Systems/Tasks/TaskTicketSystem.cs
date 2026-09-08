namespace TheLastSignal.Systems.Tasks
{
    /// <summary>
    /// Host-authoritative generator and tracker of task tickets (the game's minigame/puzzle prompts).
    /// Raises OnTaskGenerated when a new ticket is created. Pure C# — never runs on clients, per
    /// ARCHITECTURE_RULES.md Rule 1.
    /// </summary>
    public class TaskTicketSystem : IGameSystem
    {
        /// <inheritdoc />
        public void Initialize()
        {
            // TODO FR-PUZ-01: prepare the pool of available task types and scheduling state.
        }

        /// <inheritdoc />
        public void Tick(float deltaTime)
        {
            // TODO FR-PUZ-02: decide when a new task should be generated.
            // TODO FR-PUZ-03: build a TaskData instance and raise OnTaskGenerated (via NetworkGameState).
        }

        /// <inheritdoc />
        public void Shutdown()
        {
            // TODO FR-PUZ-04: clear outstanding tickets.
        }

        // TODO FR-PUZ-05: expose methods for stations to report ticket completion/failure back to this system.
    }
}
