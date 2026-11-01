using System;
using System.Collections.Generic;
using UnityEngine;
using TheLastSignal.Entities.Player;
using TheLastSignal.Systems.Lighting;

namespace TheLastSignal.Systems.Sanity
{
    /// <summary>
    /// Host-authoritative owner of every Survivor's sanity value. Pure C# — never runs on clients, per
    /// ARCHITECTURE_RULES.md Rule 1. Writes each player's own PlayerSanityState (a per-player
    /// NetworkVariable, mirroring PlayerHealth's shape) rather than a NetworkGameState singleton, since
    /// sanity is per-player state like health, not session-wide state like the objective phase.
    ///
    /// Ticks internally on a fixed 0.5s cadence (design: "Tick every 0.5s") regardless of how often
    /// GameManager.Update() calls Tick(deltaTime) — accumulates deltaTime and steps 0-N times per call.
    /// </summary>
    public class SanitySystem : IGameSystem
    {
        private readonly SanityConfigSO config;
        private readonly LightingController lighting;
        private readonly Action<ulong, SanityLevel> onThresholdCrossed;
        private readonly Dictionary<ulong, SanityLevel> lastLevelByPlayer = new Dictionary<ulong, SanityLevel>();

        private float accumulatorSeconds;

        public SanitySystem(SanityConfigSO config, LightingController lighting, Action<ulong, SanityLevel> onThresholdCrossed)
        {
            this.config = config;
            this.lighting = lighting;
            this.onThresholdCrossed = onThresholdCrossed;
        }

        /// <inheritdoc />
        public void Initialize()
        {
            accumulatorSeconds = 0f;
            lastLevelByPlayer.Clear();

            IReadOnlyList<PlayerSanityState> states = PlayerSanityState.ActiveStates;
            for (int i = 0; i < states.Count; i++)
            {
                PlayerSanityState state = states[i];
                if (state == null || IsShade(state))
                {
                    continue;
                }

                float starting = config != null ? config.StartingSanity : 100f;
                state.ServerSetSanity(starting);
                lastLevelByPlayer[state.PlayerId] = LevelFor(starting);
            }
        }

        /// <inheritdoc />
        public void Tick(float deltaTime)
        {
            if (config == null)
            {
                return;
            }

            accumulatorSeconds += deltaTime;
            float interval = Mathf.Max(0.01f, config.TickIntervalSeconds);

            // Accumulator pattern: correct even if GameManager.Update() is ever called at a different
            // cadence than 0.5s — steps 0, 1, or several times depending on how much time actually passed.
            while (accumulatorSeconds >= interval)
            {
                accumulatorSeconds -= interval;
                StepOnce(interval);
            }
        }

        /// <inheritdoc />
        public void Shutdown()
        {
            lastLevelByPlayer.Clear();
        }

        private void StepOnce(float tickSeconds)
        {
            IReadOnlyList<PlayerSanityState> states = PlayerSanityState.ActiveStates;

            for (int i = 0; i < states.Count; i++)
            {
                PlayerSanityState state = states[i];
                if (state == null || !state.isActiveAndEnabled || IsShade(state))
                {
                    continue;
                }

                ApplyTickToPlayer(state, states, tickSeconds);
            }
        }

        private void ApplyTickToPlayer(PlayerSanityState state, IReadOnlyList<PlayerSanityState> allStates, float tickSeconds)
        {
            PlayerController controller = state.GetComponent<PlayerController>();
            Vector2 position = controller != null ? (Vector2)controller.transform.position : Vector2.zero;

            bool ownFlashlightOn = false;
            string ownConeOwnerId = null;
            if (controller != null && controller.TryGetComponent(out PlayerFlashlight flashlight))
            {
                ownFlashlightOn = flashlight.IsOn;
                ownConeOwnerId = flashlight.OwnerIdString;
            }

            bool inLight = lighting != null && lighting.IsLitExcludingOwner(position, ownConeOwnerId);
            bool nearTeammate = IsNearAnyTeammate(state, controller, allStates);

            float newValue = ApplyTick(config, state.Sanity, inLight, ownFlashlightOn, nearTeammate, tickSeconds);
            state.ServerSetSanity(newValue);

            SanityLevel newLevel = LevelFor(newValue);
            if (!lastLevelByPlayer.TryGetValue(state.PlayerId, out SanityLevel previousLevel) || previousLevel != newLevel)
            {
                lastLevelByPlayer[state.PlayerId] = newLevel;
                onThresholdCrossed?.Invoke(state.PlayerId, newLevel);
            }
        }

        private bool IsNearAnyTeammate(PlayerSanityState self, PlayerController selfController, IReadOnlyList<PlayerSanityState> allStates)
        {
            if (selfController == null)
            {
                return false;
            }

            float radiusSqr = config.TeammateRadius * config.TeammateRadius;
            Vector2 selfPosition = selfController.transform.position;

            for (int i = 0; i < allStates.Count; i++)
            {
                PlayerSanityState other = allStates[i];
                if (other == null || other == self || IsShade(other))
                {
                    continue;
                }

                PlayerController otherController = other.GetComponent<PlayerController>();
                if (otherController == null)
                {
                    continue;
                }

                if (((Vector2)otherController.transform.position - selfPosition).sqrMagnitude <= radiusSqr)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Pure, dependency-free core of a single Sanity tick — no GameObjects, no NetworkVariables.
        /// Exposed (rather than kept as an inline instance method) specifically so EditMode tests can
        /// exercise drain/recovery/stacking/clamping directly, without building a PlayerController/
        /// PlayerFlashlight/NetworkBehaviour object graph just to reach this math. See
        /// SanitySystemTests.cs. Implements exactly the design brief's rules:
        /// - in light: +LightRecoveryPerSecond.
        /// - else: -DarknessDrainPerSecond, halved (by FlashlightDrainMultiplier) if the player's own
        ///   flashlight is on (their own light never counts as "in light" for the recovery branch above —
        ///   see LightingController.IsLitExcludingOwner).
        /// - near a teammate: +TeammateRecoveryPerSecond, additive on top of whichever branch above ran.
        /// - clamped to [0, MaxSanity].
        /// </summary>
        public static float ApplyTick(SanityConfigSO config, float currentValue, bool inLight, bool ownFlashlightOn, bool nearTeammate, float tickSeconds)
        {
            float delta;
            if (inLight)
            {
                delta = config.LightRecoveryPerSecond;
            }
            else
            {
                float drain = config.DarknessDrainPerSecond;
                if (ownFlashlightOn)
                {
                    drain *= config.FlashlightDrainMultiplier;
                }

                delta = -drain;
            }

            if (nearTeammate)
            {
                delta += config.TeammateRecoveryPerSecond;
            }

            return Mathf.Clamp(currentValue + delta * tickSeconds, 0f, config.MaxSanity);
        }

        private SanityLevel LevelFor(float value)
        {
            return config != null ? config.LevelFor(value) : SanityLevel.Normal;
        }

        /// <summary>Slot 3 is always the Shade (see GameCharacterRole.forSlot) — "the Shade has no
        /// Sanity" per the design, so it is skipped entirely rather than ticked and ignored.</summary>
        private static bool IsShade(PlayerSanityState state)
        {
            PlayerController controller = state.GetComponent<PlayerController>();
            return controller != null && controller.SlotIndex == 3;
        }
    }
}
