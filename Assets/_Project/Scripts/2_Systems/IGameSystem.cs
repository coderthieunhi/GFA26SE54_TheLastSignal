namespace TheLastSignal.Systems
{
    /// <summary>
    /// Common lifecycle contract for every Host-side gameplay system in 2_Systems/. Per
    /// ARCHITECTURE_RULES.md Rule 1, implementers are either Pure C# or a NetworkBehaviour that only
    /// runs this lifecycle when IsServer is true.
    /// </summary>
    public interface IGameSystem
    {
        /// <summary>Called once when the Host starts the match, before the first Tick.</summary>
        void Initialize();

        /// <summary>Called once per Host simulation step.</summary>
        /// <param name="deltaTime">Seconds elapsed since the previous Tick.</param>
        void Tick(float deltaTime);

        /// <summary>Called once when the match ends or the system is torn down.</summary>
        void Shutdown();
    }
}
