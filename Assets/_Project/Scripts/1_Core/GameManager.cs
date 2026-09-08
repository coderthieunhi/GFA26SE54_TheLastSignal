using UnityEngine;

namespace TheLastSignal.Core
{
    /// <summary>
    /// Top-level orchestrator for match flow (lobby -> playing -> ended). Owns/creates the Host-side
    /// 2_Systems/ instances and drives their Tick, and coordinates with NetworkGameState to publish
    /// state to clients. Per ARCHITECTURE_RULES.md Rule 1, the systems it drives only execute on the Host.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        /// <summary>High-level phases of a match.</summary>
        public enum GameState
        {
            Lobby,
            Playing,
            Ended
        }

        [SerializeField] private GameState currentState = GameState.Lobby;

        /// <summary>The current phase of the match.</summary>
        public GameState CurrentState => currentState;

        private void Awake()
        {
            // TODO: acquire/validate references to NetworkGameState and other 1_Core singletons.
        }

        private void Start()
        {
            // TODO: initialize Host-side 2_Systems/ instances (SanitySystem, TaskTicketSystem, MonsterFSM, ...)
            // once a network session has started, guarded by IsServer.
        }

        private void Update()
        {
            // TODO: on the Host only, Tick every active IGameSystem with Time.deltaTime.
        }

        /// <summary>Transitions the match to a new <see cref="GameState"/>.</summary>
        public void SetState(GameState newState)
        {
            // TODO: implement state transition rules and side effects (spawning, UI, etc.).
        }
    }
}
