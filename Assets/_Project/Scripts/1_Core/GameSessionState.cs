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
        GameOver
    }
}
