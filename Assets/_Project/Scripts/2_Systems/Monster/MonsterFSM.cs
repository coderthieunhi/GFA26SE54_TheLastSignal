namespace TheLastSignal.Systems.Monster
{
    /// <summary>
    /// Host-authoritative finite state machine driving the monster's behavior (Lurk/Stalk/Attack/Stun).
    /// Raises OnMonsterStateChanged (via NetworkGameState) on transitions. Pure C# — never runs on
    /// clients, per ARCHITECTURE_RULES.md Rule 1.
    /// </summary>
    public class MonsterFSM : IGameSystem
    {
        public MonsterFSM(MonsterConfigSO config)
        {
            // TODO FR-AI-01: cache config and set the initial state to Lurk.
        }

        /// <inheritdoc />
        public void Initialize()
        {
            // TODO FR-AI-01: place the monster and reset state to Lurk.
        }

        /// <inheritdoc />
        public void Tick(float deltaTime)
        {
            // TODO FR-AI-02: evaluate Lurk -> Stalk transition conditions.
            // TODO FR-AI-03: evaluate Stalk -> Attack transition conditions.
            // TODO FR-AI-04: evaluate Attack resolution and Stun entry conditions.
            // TODO FR-AI-05: evaluate Stun -> Lurk recovery.
        }

        /// <inheritdoc />
        public void Shutdown()
        {
            // TODO FR-AI-01: clear monster state.
        }

        // TODO: expose a read-only CurrentState so NetworkGameState can mirror it into a NetworkVariable
        // and raise OnMonsterStateChanged when it changes.
    }
}
