namespace TheLastSignal.Systems.Sanity
{
    /// <summary>
    /// Host-authoritative owner of every player's sanity value. Drains, restores, and raises
    /// OnSanityChanged (via NetworkGameState) as sanity changes. Pure C# — never runs on clients,
    /// per ARCHITECTURE_RULES.md Rule 1.
    /// </summary>
    public class SanitySystem : IGameSystem
    {
        public SanitySystem(SanityConfigSO config)
        {
            // TODO FR-SAN-01: cache config and prepare per-player sanity state.
        }

        /// <inheritdoc />
        public void Initialize()
        {
            // TODO FR-SAN-01: initialize every connected player's sanity to its starting value.
        }

        /// <inheritdoc />
        public void Tick(float deltaTime)
        {
            // TODO FR-SAN-02: apply base + situational drain per player for this tick.
            // TODO FR-SAN-03: clamp sanity and detect threshold crossings.
            // TODO FR-SAN-04: notify NetworkGameState of new values so it can update NetworkVariables
            // and raise OnSanityChanged.
        }

        /// <inheritdoc />
        public void Shutdown()
        {
            // TODO FR-SAN-05: clear per-player sanity state.
        }

        // TODO FR-SAN-06: expose methods for other systems (e.g. Tasks, Monster) to apply sanity
        // modifiers (drain spikes, restoration on task completion, etc.).
    }
}
