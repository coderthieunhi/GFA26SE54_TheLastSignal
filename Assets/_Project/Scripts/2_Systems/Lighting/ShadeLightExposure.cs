using System;

namespace TheLastSignal.Systems.Lighting
{
    /// <summary>
    /// Epic 5 hook only: tracks whether "the Shade" is currently exposed to light and the speed debuff
    /// multiplier that exposure should apply. No Shade entity exists yet (see ARCHITECTURE_RULES.md /
    /// docs/pivot — Monster/Shade work hasn't started), so nothing calls <see cref="ServerSetExposed"/>
    /// today. The Shade epic calls LightingController.IsExposedToLight(shadePosition) once per tick and
    /// feeds the result in here; this class only owns the debounced flag + event + configured multiplier
    /// so that future epic doesn't have to re-invent them. Do not implement Shade abilities here.
    /// </summary>
    public sealed class ShadeLightExposure
    {
        public float SpeedMultiplier { get; }
        public bool IsExposed { get; private set; }

        /// <summary>Raised only when IsExposed actually changes (not every tick).</summary>
        public event Action<bool> OnExposureChanged;

        public ShadeLightExposure(float speedMultiplier)
        {
            SpeedMultiplier = speedMultiplier;
        }

        /// <summary>Server-only. Called by whatever evaluates the Shade's position each tick.</summary>
        public void ServerSetExposed(bool exposed)
        {
            if (exposed == IsExposed)
            {
                return;
            }

            IsExposed = exposed;
            OnExposureChanged?.Invoke(exposed);
        }
    }
}
