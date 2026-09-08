namespace TheLastSignal.Systems.Power
{
    /// <summary>
    /// Host-authoritative owner of the station power grid (which stations are powered, generator output,
    /// outage propagation). Pure C# — never runs on clients, per ARCHITECTURE_RULES.md Rule 1.
    /// </summary>
    public class PowerGridSystem
    {
        // TODO: define power grid requirements (see SRS) — no requirement IDs assigned yet.

        public void Initialize()
        {
            // TODO: build the initial station/power-node graph.
        }

        public void Tick(float deltaTime)
        {
            // TODO: propagate generator output and station power state across the grid.
        }

        public void Shutdown()
        {
            // TODO: clear power grid state.
        }
    }
}
