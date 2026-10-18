namespace TheLastSignal.Entities.Player
{
    /// <summary>
    /// Life state of a player/survivor as specified in Capstone requirements:
    /// - Alive: standard operating state with full interaction and movement.
    /// - Downed: health reached 0; movement reduced to crawl, requires revive interaction.
    /// - Dead: eliminated / bleedout expired.
    /// </summary>
    public enum PlayerLifeState : byte
    {
        Alive = 0,
        Downed = 1,
        Dead = 2
    }
}
