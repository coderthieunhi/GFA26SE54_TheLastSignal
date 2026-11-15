using System.Collections;
using UnityEngine;
using TheLastSignal.Entities.Environment.Interactables;

namespace TheLastSignal.Entities.Environment
{
    /// <summary>
    /// View-layer controller for Node C's Emergency Floodlight Override system.
    /// Listens to SafeLightJunction authoritative state changes and updates:
    /// - West Emergency Override Console screen & status LEDs
    /// - North Overhead Floodlight Gantry hardware & optics
    /// - South Overhead Floodlight Gantry hardware & optics
    /// - Emergency Brownout warning flicker (final 1.5s)
    /// </summary>
    [DisallowMultipleComponent]
    public class NodeCFloodlightVisuals : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private SafeLightJunction junction;
        [SerializeField] private SpriteRenderer consoleRenderer;
        [SerializeField] private SpriteRenderer northGantryRenderer;
        [SerializeField] private SpriteRenderer southGantryRenderer;
        [SerializeField] private SpriteRenderer northBeamRenderer;
        [SerializeField] private SpriteRenderer southBeamRenderer;

        [Header("Console Sprites")]
        [SerializeField] private Sprite consoleReadySprite;
        [SerializeField] private Sprite consoleActiveSprite;
        [SerializeField] private Sprite consoleWarningSprite;
        [SerializeField] private Sprite consoleRechargeSprite;

        [Header("North Floodlight Gantry Sprites (Facing South)")]
        [SerializeField] private Sprite northReadySprite;
        [SerializeField] private Sprite northActiveSprite;
        [SerializeField] private Sprite northWarningSprite;
        [SerializeField] private Sprite northRechargeSprite;

        [Header("South Floodlight Gantry Sprites (Facing North)")]
        [SerializeField] private Sprite southReadySprite;
        [SerializeField] private Sprite southActiveSprite;
        [SerializeField] private Sprite southWarningSprite;
        [SerializeField] private Sprite southRechargeSprite;

        private Coroutine flickerCoroutine;

        private void Awake()
        {
            if (junction == null) junction = GetComponent<SafeLightJunction>();
            AutoFindReferences();
        }

        private void OnEnable()
        {
            if (junction != null)
            {
                junction.OnStateChanged += HandleStateChanged;
                ApplyStateVisuals(junction.CurrentState);
            }
        }

        private void OnDisable()
        {
            if (junction != null)
            {
                junction.OnStateChanged -= HandleStateChanged;
            }
            StopFlicker();
        }

        private void Start()
        {
            if (junction != null)
            {
                ApplyStateVisuals(junction.CurrentState);
            }
        }

        public void AutoFindReferences()
        {
            if (consoleRenderer == null)
            {
                var consoleGo = GameObject.Find("Prop_NodeC_Bridge_Console");
                if (consoleGo != null) consoleRenderer = consoleGo.GetComponent<SpriteRenderer>();
            }

            if (northGantryRenderer == null)
            {
                var northGo = GameObject.Find("Prop_NodeC_Gantry_Arch_North");
                if (northGo != null) northGantryRenderer = northGo.GetComponent<SpriteRenderer>();
            }

            if (southGantryRenderer == null)
            {
                var southGo = GameObject.Find("Prop_NodeC_Gantry_Arch_South");
                if (southGo != null) southGantryRenderer = southGo.GetComponent<SpriteRenderer>();
            }

            if (northBeamRenderer == null)
            {
                var northBeamGo = GameObject.Find("Prop_NodeC_Gantry_Beam_North");
                if (northBeamGo != null) northBeamRenderer = northBeamGo.GetComponent<SpriteRenderer>();
            }

            if (southBeamRenderer == null)
            {
                var southBeamGo = GameObject.Find("Prop_NodeC_Gantry_Beam_South");
                if (southBeamGo != null) southBeamRenderer = southBeamGo.GetComponent<SpriteRenderer>();
            }
        }

        private void HandleStateChanged(SafeLightJunctionState newState)
        {
            ApplyStateVisuals(newState);
        }

        public void ApplyStateVisuals(SafeLightJunctionState state)
        {
            StopFlicker();

            switch (state)
            {
                case SafeLightJunctionState.Inactive:
                    SetSprites(consoleReadySprite, northReadySprite, southReadySprite);
                    SetBeams(false);
                    break;

                case SafeLightJunctionState.Activating:
                case SafeLightJunctionState.Active:
                    SetSprites(consoleActiveSprite, northActiveSprite, southActiveSprite);
                    SetBeams(true, Color.white);
                    break;

                case SafeLightJunctionState.Brownout:
                    SetSprites(consoleWarningSprite, northWarningSprite, southWarningSprite);
                    SetBeams(true, new Color(1.0f, 0.65f, 0.25f, 0.95f));
                    flickerCoroutine = StartCoroutine(BrownoutFlickerRoutine());
                    break;

                case SafeLightJunctionState.Cooldown:
                case SafeLightJunctionState.Deactivating:
                    SetSprites(consoleRechargeSprite, northRechargeSprite, southRechargeSprite);
                    SetBeams(false);
                    break;
            }
        }

        private void SetBeams(bool active, Color tint = default)
        {
            if (tint == default) tint = Color.white;
            if (northBeamRenderer != null)
            {
                northBeamRenderer.enabled = active;
                northBeamRenderer.color = tint;
            }
            if (southBeamRenderer != null)
            {
                southBeamRenderer.enabled = active;
                southBeamRenderer.color = tint;
            }
        }

        private void SetSprites(Sprite console, Sprite north, Sprite south)
        {
            if (consoleRenderer != null && console != null) consoleRenderer.sprite = console;
            if (northGantryRenderer != null && north != null) northGantryRenderer.sprite = north;
            if (southGantryRenderer != null && south != null) southGantryRenderer.sprite = south;
        }

        private void StopFlicker()
        {
            if (flickerCoroutine != null)
            {
                StopCoroutine(flickerCoroutine);
                flickerCoroutine = null;
            }
        }

        private IEnumerator BrownoutFlickerRoutine()
        {
            Color warnColor = new Color(1.0f, 0.65f, 0.25f, 0.95f);
            Color dimWarnColor = new Color(1.0f, 0.35f, 0.10f, 0.60f);

            while (junction != null && junction.CurrentState == SafeLightJunctionState.Brownout)
            {
                // Rapid irregular flicker between warning amber, voltage sag, and momentary blackout
                SetSprites(consoleWarningSprite, northWarningSprite, southWarningSprite);
                SetBeams(true, warnColor);
                yield return new WaitForSeconds(Random.Range(0.08f, 0.16f));

                SetBeams(true, dimWarnColor);
                yield return new WaitForSeconds(Random.Range(0.04f, 0.08f));

                SetSprites(consoleWarningSprite, northReadySprite, southReadySprite);
                SetBeams(false);
                yield return new WaitForSeconds(Random.Range(0.03f, 0.09f));
            }
            flickerCoroutine = null;
        }
    }
}
