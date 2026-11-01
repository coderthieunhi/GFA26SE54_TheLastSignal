using System.Collections.Generic;
using UnityEngine;
using TheLastSignal.Systems;

namespace TheLastSignal.Systems.Lighting
{
    /// <summary>
    /// Host-authoritative source of truth for "is this point lit". Pure C# — never runs on clients
    /// (ARCHITECTURE_RULES.md Rule 1). Answers every gameplay light question from plain data and math
    /// (GameplayLightSource descriptors + one Physics2D.Linecast for cone occlusion), never from a
    /// Light2D — the headless server cannot render, so it must not depend on rendering components.
    ///
    /// Sources are discovered fresh every Tick (not push-registered) via FindObjectsByType, the same
    /// "look it up when needed" convention NetworkGameState/PlayerSpawner already use elsewhere in this
    /// project — this keeps it correct across RestartSession() and regardless of whether a given source
    /// (e.g. a player's flashlight cone) exists yet when Initialize() runs.
    /// </summary>
    public class LightingController : IGameSystem
    {
        /// <summary>Set while a session is active so other Host-side code (GameManager) can reach it
        /// without GameManager having to store its own reference. Mirrors GameManager.Instance's own
        /// static-singleton convention.</summary>
        public static LightingController Current { get; private set; }

        private readonly LightingConfigSO config;
        private readonly List<GameplayLightSource> sources = new List<GameplayLightSource>();

        public ShadeLightExposure ShadeExposure { get; }

        public LightingController(LightingConfigSO config)
        {
            this.config = config;
            ShadeExposure = new ShadeLightExposure(config != null ? config.ShadeSpeedDebuffMultiplier : 0.7f);
        }

        /// <inheritdoc />
        public void Initialize()
        {
            Current = this;
            RefreshSources();
        }

        /// <inheritdoc />
        public void Tick(float deltaTime)
        {
            // Cheap at this project's scale (a handful of zones/cones) — re-scanning every frame means
            // a flashlight that spawns after Initialize() (every player does) is picked up without any
            // push-registration machinery, and a RestartSession() never needs explicit re-registration.
            RefreshSources();
        }

        /// <inheritdoc />
        public void Shutdown()
        {
            sources.Clear();
            if (Current == this)
            {
                Current = null;
            }
        }

        private void RefreshSources()
        {
            sources.Clear();
            sources.AddRange(Object.FindObjectsByType<GameplayLightSource>(FindObjectsInactive.Exclude));
        }

        /// <summary>True if any enabled source (zone, circle, or any cone) covers <paramref name="position"/>.</summary>
        public bool IsLit(Vector2 position)
        {
            return IsLitExcludingOwner(position, excludeOwnerId: null);
        }

        /// <summary>
        /// Same as <see cref="IsLit"/>, but ignores Cone sources owned by <paramref name="excludeOwnerId"/>.
        /// Used by SanitySystem so a Survivor's own flashlight doesn't count toward their own
        /// "in light" recovery bonus (the design calls for "another player's cone", not your own — your
        /// own flashlight only ever halves the darkness drain, see SanitySystem).
        /// </summary>
        public bool IsLitExcludingOwner(Vector2 position, string excludeOwnerId)
        {
            for (int i = 0; i < sources.Count; i++)
            {
                GameplayLightSource source = sources[i];
                if (source == null || !source.IsEnabled)
                {
                    continue;
                }

                if (source.Type == GameplayLightType.Cone && !string.IsNullOrEmpty(excludeOwnerId)
                    && source.OwnerId == excludeOwnerId)
                {
                    continue;
                }

                if (Covers(source, position))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Hook for the Shade epic: identical lighting test as IsLit, named for that call site's intent.
        /// No Shade-specific rule exists yet (e.g. a cone's own owner is never excluded here, since the
        /// Shade never owns a cone) — this is intentionally just IsLit under a clearer name.
        /// </summary>
        public bool IsExposedToLight(Vector2 shadePosition)
        {
            return IsLit(shadePosition);
        }

        private bool Covers(GameplayLightSource source, Vector2 position)
        {
            switch (source.Type)
            {
                case GameplayLightType.Zone:
                    return source.ZoneBounds != null && source.ZoneBounds.OverlapPoint(position);

                case GameplayLightType.Circle:
                    return (source.Position - position).sqrMagnitude <= source.CircleRadius * source.CircleRadius;

                case GameplayLightType.Cone:
                    return InCone(source, position) && !OccludedByWall(source.Position, position);

                default:
                    return false;
            }
        }

        private static bool InCone(GameplayLightSource source, Vector2 position)
        {
            Vector2 toPoint = position - source.Position;
            float distance = toPoint.magnitude;
            if (distance > source.ConeRange)
            {
                return false;
            }

            if (distance <= 0.0001f)
            {
                return true;
            }

            Vector2 aimDirection = AngleToDirection(source.AimAngleDegrees);
            float angleToPoint = Vector2.Angle(aimDirection, toPoint);
            return angleToPoint <= source.ConeAngleDegrees * 0.5f;
        }

        private bool OccludedByWall(Vector2 from, Vector2 to)
        {
            LayerMask wallLayer = config != null ? config.WallLayer : 0;
            if (wallLayer.value == 0)
            {
                return false;
            }

            // Single occlusion check per the design ("one Physics2D.Linecast against the wall layer" —
            // no per-pixel shadow simulation).
            return Physics2D.Linecast(from, to, wallLayer);
        }

        private static Vector2 AngleToDirection(float degrees)
        {
            float radians = degrees * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
        }
    }
}
