namespace TheLastSignal.Systems.Lighting
{
    /// <summary>The three shapes LightingController understands. See GameplayLightSource.</summary>
    public enum GameplayLightType
    {
        /// <summary>Static area bounded by a BoxCollider2D trigger (Safe Zone, repair-station room, ...).</summary>
        Zone,

        /// <summary>Directional cone: range + half-angle from the source's current aim direction (flashlight).</summary>
        Cone,

        /// <summary>Simple radius around the source's position (e.g. a flare, a station light).</summary>
        Circle
    }
}
