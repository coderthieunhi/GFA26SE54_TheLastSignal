using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;
using TheLastSignal.Systems.Sanity;

namespace TheLastSignal.Entities.Player
{
    /// <summary>
    /// Purely visual reaction to this player's own replicated sanity (vignette/desaturation + a
    /// low-sanity hallucination flicker). Subscribes to PlayerSanityState.OnSanityChanged and never
    /// computes or writes sanity state itself, per ARCHITECTURE_RULES.md Rule 2. Only active for the
    /// local/owning player — a teammate's screen must not react to your sanity.
    ///
    /// The hallucination effect is client-only and purely cosmetic (a UI flicker/shake driven by
    /// Random/Time): it never reads the Shade's position, state, or any other hidden information, per
    /// the design brief's explicit constraint.
    /// </summary>
    [RequireComponent(typeof(PlayerSanityState))]
    public class PlayerSanityView : MonoBehaviour
    {
        [Header("Vignette (optional — safe to leave unassigned)")]
        [SerializeField] private Image vignetteImage;
        [SerializeField] private Color vignetteColor = new Color(0.4f, 0f, 0.05f, 1f);
        [SerializeField, Range(0f, 1f)] private float maxVignetteAlpha = 0.6f;

        [Header("Hallucination (Breaking level only)")]
        [SerializeField] private float hallucinationFlickerSpeed = 7f;
        [SerializeField, Range(0f, 0.3f)] private float hallucinationShakeAmount = 0.08f;

        private PlayerSanityState sanityState;
        private SanityConfigSO config;
        private float currentSanity = 100f;
        private Vector3 vignetteBasePosition;

        private void Awake()
        {
            sanityState = GetComponent<PlayerSanityState>();

            if (vignetteImage != null)
            {
                vignetteBasePosition = vignetteImage.rectTransform.localPosition;
            }
        }

        /// <summary>Lets GameManager/a HUD factory supply the thresholds this view classifies against,
        /// without this purely-visual class owning its own copy of balance data.</summary>
        public void SetConfig(SanityConfigSO sanityConfig)
        {
            config = sanityConfig;
        }

        private void OnEnable()
        {
            if (sanityState != null)
            {
                sanityState.OnSanityChanged += HandleSanityChanged;
            }
        }

        private void OnDisable()
        {
            if (sanityState != null)
            {
                sanityState.OnSanityChanged -= HandleSanityChanged;
            }

            if (vignetteImage != null)
            {
                vignetteImage.rectTransform.localPosition = vignetteBasePosition;
            }
        }

        private void Update()
        {
            if (!IsLocallyOwned() || vignetteImage == null || config == null)
            {
                return;
            }

            SanityLevel level = config.LevelFor(currentSanity);
            if (level != SanityLevel.Breaking)
            {
                vignetteImage.rectTransform.localPosition = vignetteBasePosition;
                return;
            }

            // Cosmetic-only flicker/shake: derived purely from Time and Random, never from any
            // gameplay state (positions, Shade data, etc.) per the design's hidden-information rule.
            float flicker = 0.5f + 0.5f * Mathf.Sin(Time.time * hallucinationFlickerSpeed);
            Color c = vignetteColor;
            c.a = Mathf.Lerp(maxVignetteAlpha * 0.7f, maxVignetteAlpha, flicker);
            vignetteImage.color = c;

            Vector2 shakeOffset = Random.insideUnitCircle * hallucinationShakeAmount * 10f;
            vignetteImage.rectTransform.localPosition = vignetteBasePosition + (Vector3)shakeOffset;
        }

        private bool IsLocallyOwned()
        {
            bool networkSessionActive = NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
            return !networkSessionActive || (sanityState != null && sanityState.IsOwner);
        }

        private void HandleSanityChanged(float newSanity)
        {
            currentSanity = newSanity;

            if (vignetteImage == null)
            {
                return;
            }

            if (!IsLocallyOwned())
            {
                vignetteImage.enabled = false;
                return;
            }

            vignetteImage.enabled = true;

            float maxSanity = config != null ? config.MaxSanity : 100f;
            float darkness = maxSanity > 0.01f ? 1f - Mathf.Clamp01(newSanity / maxSanity) : 0f;

            Color c = vignetteColor;
            c.a = darkness * maxVignetteAlpha;
            vignetteImage.color = c;
        }
    }
}
