using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace TheLastSignal.Systems.Lighting
{
    /// <summary>
    /// Shared headless-mode guard: disables every Light2D (and its SpriteRenderer, if any) under a
    /// GameObject. A Dedicated Server never renders (ARCHITECTURE_RULES.md Rule 2/4), so these
    /// components are dead weight on it. Used by GameplayLightSource, PlayerFlashlightView, and
    /// DoorIndicatorLight so the same guard isn't re-implemented three different ways.
    /// </summary>
    public static class Light2DStripper
    {
        public static void StripVisuals(GameObject root)
        {
            if (root == null)
            {
                return;
            }

            Light2D[] lights = root.GetComponentsInChildren<Light2D>(true);
            for (int i = 0; i < lights.Length; i++)
            {
                lights[i].enabled = false;
            }

            SpriteRenderer[] renderers = root.GetComponentsInChildren<SpriteRenderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                renderers[i].enabled = false;
            }
        }
    }
}
