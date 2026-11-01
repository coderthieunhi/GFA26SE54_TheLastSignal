using UnityEngine;
using TheLastSignal.Core.Bootstrap;

namespace TheLastSignal.Systems.Lighting
{
    /// <summary>
    /// Pure data/shape description of one gameplay light source, read every frame by the server-only
    /// LightingController (see ARCHITECTURE_RULES.md Rule 1 — the headless server never renders, so it
    /// answers "is this point lit" from this component's plain fields, never from a Light2D). Any
    /// Light2D/SpriteRenderer visuals a designer attaches to the same GameObject are for clients only;
    /// this component disables them on a Dedicated Server in Awake(), extending the same guard
    /// CameraFollow already uses (see docs/pivot/01_PIVOT_PLAN.md §1.5, which flags exactly this gap).
    /// </summary>
    public class GameplayLightSource : MonoBehaviour
    {
        [Header("Shape")]
        [SerializeField] private GameplayLightType type = GameplayLightType.Zone;
        [Tooltip("Zone only: the trigger bounds LightingController tests with OverlapPoint. " +
                 "Defaults to this GameObject's own BoxCollider2D if left empty.")]
        [SerializeField] private BoxCollider2D zoneBounds;
        [Tooltip("Circle only: radius around this transform's position.")]
        [SerializeField] private float circleRadius = 3f;
        [Tooltip("Cone only: distance the cone reaches. Overwritten at runtime for a flashlight cone " +
                 "by PlayerFlashlight from FlashlightConfigSO.")]
        [SerializeField] private float coneRange = 6f;
        [Tooltip("Cone only: full angle of the cone in degrees (not half-angle).")]
        [SerializeField] private float coneAngleDegrees = 40f;

        [Header("State")]
        [SerializeField] private bool enabledByDefault = true;
        [Tooltip("Identifies the owner for Cone sources (a flashlight) so LightingController can exclude " +
                 "'my own cone' from my own sanity-recovery check. Empty for Zone/Circle sources.")]
        [SerializeField] private string ownerId = string.Empty;

        private bool runtimeEnabled;
        private float currentAimAngleDegrees;

        public GameplayLightType Type => type;
        public bool IsEnabled => runtimeEnabled;
        public string OwnerId => ownerId;
        public float ConeRange => coneRange;
        public float ConeAngleDegrees => coneAngleDegrees;
        public float AimAngleDegrees => currentAimAngleDegrees;
        public float CircleRadius => circleRadius;
        public BoxCollider2D ZoneBounds => zoneBounds;
        public Vector2 Position => transform.position;

        private void Awake()
        {
            runtimeEnabled = enabledByDefault;

            if (zoneBounds == null)
            {
                zoneBounds = GetComponent<BoxCollider2D>();
            }

            // Headless: a Dedicated Server never renders, so any light/sprite dressing on this object
            // (or its children, e.g. a flashlight's Light2D cone visual) is dead weight it should never
            // carry. Extends the guard CameraFollow already applies for the Camera (see
            // docs/pivot/01_PIVOT_PLAN.md, which separately flags DoorIndicatorLight's Light2D as the
            // same still-open gap — fixed alongside this component, see DoorIndicatorLight.cs).
            if (GameBootstrap.CurrentMode == LaunchMode.DedicatedServer)
            {
                Light2DStripper.StripVisuals(gameObject);
            }
        }

        /// <summary>Server-only. Turns this source on/off (e.g. a repair-station zone, or a flashlight).</summary>
        public void ServerSetEnabled(bool enabled)
        {
            runtimeEnabled = enabled;
        }

        /// <summary>Server-only. Cone sources only — current aim direction in degrees (0 = +X, CCW).</summary>
        public void ServerSetAimAngle(float degrees)
        {
            currentAimAngleDegrees = degrees;
        }

        /// <summary>Assigns the owner id once, typically at spawn (e.g. a flashlight's OwnerClientId).</summary>
        public void SetOwnerId(string id)
        {
            ownerId = id;
        }

        /// <summary>Lets a dynamic source (PlayerFlashlight) configure its range/angle from its own config.</summary>
        public void ConfigureCone(float range, float angleDegrees)
        {
            type = GameplayLightType.Cone;
            coneRange = range;
            coneAngleDegrees = angleDegrees;
        }

        /// <summary>Configures this source as a Zone bounded by <paramref name="bounds"/>. Exposed for
        /// tooling/tests that build a GameplayLightSource purely in code; level-authored Zones normally
        /// just set the Type/Zone Bounds fields in the Inspector instead.</summary>
        public void ConfigureZone(BoxCollider2D bounds)
        {
            type = GameplayLightType.Zone;
            zoneBounds = bounds;
        }

        /// <summary>Configures this source as a Circle of the given radius. See ConfigureZone's note.</summary>
        public void ConfigureCircle(float radius)
        {
            type = GameplayLightType.Circle;
            circleRadius = radius;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.92f, 0.3f, 0.5f);
            switch (type)
            {
                case GameplayLightType.Circle:
                    Gizmos.DrawWireSphere(transform.position, circleRadius);
                    break;
                case GameplayLightType.Cone:
                    Gizmos.DrawWireSphere(transform.position, coneRange);
                    break;
                case GameplayLightType.Zone:
                    if (zoneBounds != null)
                    {
                        Gizmos.matrix = zoneBounds.transform.localToWorldMatrix;
                        Gizmos.DrawWireCube(zoneBounds.offset, zoneBounds.size);
                    }
                    break;
            }
        }
    }
}
