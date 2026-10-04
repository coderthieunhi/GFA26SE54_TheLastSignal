namespace TheLastSignal.Core
{
    /// <summary>
    /// Prototype game-session lifecycle. Lobby/netcode match flow can wrap this later;
    /// this story only needs a local Start -> Gameplay -> Win/Lose loop.
    /// </summary>
    public enum GameSessionState
    {
        GameStart,
        InitializeGame,
        SpawnPlayer,
        InitializeObjective,
        Gameplay,
        Evacuation,
        Win,
        Lose,
        GameComplete,
        GameOver,

        /// <summary>
        /// Story 3.6: a Client-only terminal state reached when this peer's own connection was lost
        /// mid-session. This project has no Host Migration (ARCHITECTURE_RULES.md), so losing
        /// connection while in a live session can only mean the Host disconnected. Never set on the
        /// Host, and never pushed through NetworkGameState (see GameManager.HandleHostDisconnected).
        /// </summary>
        HostDisconnected
    }
}
