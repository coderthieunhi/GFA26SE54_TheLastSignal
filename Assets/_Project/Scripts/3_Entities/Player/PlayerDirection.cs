namespace TheLastSignal.Entities.Player
{
    /// <summary>
    /// The four cardinal directions the player can face. Resolved from movement input by
    /// <see cref="PlayerController"/> and persisted while the player is idle, for consumption by
    /// future systems (animation, flashlight, interaction, role abilities, etc.).
    /// </summary>
    public enum PlayerDirection
    {
        Up,
        Down,
        Left,
        Right
    }
}
