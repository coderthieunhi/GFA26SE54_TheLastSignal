using UnityEngine;
using UnityEngine.Rendering.Universal;
using TheLastSignal.Core.Bootstrap;
using TheLastSignal.Systems.Lighting;

namespace TheLastSignal.Entities.Player
{
    /// <summary>
    /// Purely visual reaction to PlayerFlashlight's replicated state (enabled, aim angle). Never
    /// computes or decides anything — reads PlayerFlashlight's NetworkVariable-backed events and drives
    /// a URP Light2D "Spot" (point light with outer angle) to match, per ARCHITECTURE_RULES.md Rule 2.
    /// Subscribes to PlayerFlashlight's own events (which it re-fires from OnValueChanged on every
    /// peer, including a late joiner once the initial NetworkVariable sync completes) rather than a
    /// one-shot RPC, so a late joiner sees the correct cone immediately.
    /// </summary>
    [RequireComponent(typeof(Light2D))]
    public class PlayerFlashlightView : MonoBehaviour
    {
        [SerializeField] private PlayerFlashlight flashlight;
        private Light2D light2D;

        private void Awake()
        {
            light2D = GetComponent<Light2D>();

            if (flashlight == null)
            {
                flashlight = GetComponentInParent<PlayerFlashlight>();
            }

            // Headless: a Dedicated Server never renders (ARCHITECTURE_RULES.md Rule 2/4) — strip this
            // cone's Light2D so the server never carries render-only state.
            if (GameBootstrap.CurrentMode == LaunchMode.DedicatedServer)
            {
                Light2DStripper.StripVisuals(gameObject);
                enabled = false;
            }
        }

        private void OnEnable()
        {
            if (flashlight != null)
            {
                flashlight.OnFlashlightToggled += HandleToggled;
                flashlight.OnAimChanged += HandleAimChanged;
                HandleToggled(flashlight.IsOn);
                HandleAimChanged(flashlight.AimAngleDegrees);
            }
        }

        private void OnDisable()
        {
            if (flashlight != null)
            {
                flashlight.OnFlashlightToggled -= HandleToggled;
                flashlight.OnAimChanged -= HandleAimChanged;
            }
        }

        private void HandleToggled(bool isOn)
        {
            if (light2D != null)
            {
                light2D.enabled = isOn;
            }
        }

        private void HandleAimChanged(float degrees)
        {
            transform.rotation = Quaternion.Euler(0f, 0f, degrees);
        }
    }
}
