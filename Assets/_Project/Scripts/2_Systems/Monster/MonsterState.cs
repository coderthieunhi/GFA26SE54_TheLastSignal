namespace TheLastSignal.Systems.Monster
{
    /// <summary>The finite states the monster AI can occupy, driven by <see cref="MonsterFSM"/>.</summary>
    public enum MonsterState
    {
        Lurk,
        Stalk,
        Attack,
        Stun
    }
}
