using UnityEngine;
using UnityEngine.Tilemaps;
using TheLastSignal.Core.Bootstrap;

namespace TheLastSignal.Entities.Camera
{
    /// <summary>
    /// Pure View-layer camera follow. Smoothly tracks an assigned target on X/Y only (Z stays fixed for
    /// 2D top-down orthographic), clamped so it never shows area outside the Story 1.4 map's Tilemap
    /// bounds. Never looks up "the player" itself — per ARCHITECTURE_RULES.md, a client only ever wants
    /// its own local player as the target, so PlayerController (or an equivalent spawn point) calls
    /// <see cref="SetTarget"/> on spawn instead of this component finding a player on its own.
    /// </summary>
    [RequireComponent(typeof(UnityEngine.Camera))]
    public class CameraFollow : MonoBehaviour
    {
        [Header("Follow")]
        [SerializeField] private float smoothTime = 0.12f;

        [Header("Zoom & Tactical FOV")]
        [SerializeField] private float defaultOrthoSize = 5.0f;
        private float targetOrthoSize = 5.0f;
        private float zoomVelocity = 0f;
        [SerializeField] private float zoomSmoothTime = 0.35f;

        [Header("Lookahead Offset")]
        private Vector2 targetOffset = Vector2.zero;
        private Vector2 currentOffset = Vector2.zero;
        private Vector2 offsetVelocity = Vector2.zero;
        [SerializeField] private float offsetSmoothTime = 0.25f;

        [Header("Map Bounds")]
        [Tooltip("If true or if boundsTilemaps is empty, automatically includes all active Tilemaps across all rooms in the scene.")]
        [SerializeField] private bool autoIncludeAllTilemaps = true;

        [Tooltip("Tilemap(s) whose combined bounds define the playable map. If autoIncludeAllTilemaps is false, only these tilemaps are used.")]
        [SerializeField] private Tilemap[] boundsTilemaps;

        public static CameraFollow Instance { get; private set; }

        [Header("Screen Shake")]
        private Vector2 shakeOffset = Vector2.zero;
        private float shakeRemaining = 0f;
        private float shakeIntensity = 0f;

        private UnityEngine.Camera cam;
        private Transform target;
        private Vector3 velocity;
        private bool hasBounds;
        private Vector2 mapMin;
        private Vector2 mapMax;

        private void Awake()
        {
            // TLS-E03-S01: no camera/render target exists on a headless Dedicated Server
            // (ARCHITECTURE_RULES.md Rule 2/4). Disabling this GameObject also disables the Camera,
            // AudioListener, and URP camera data components that live on the same object.
            if (GameBootstrap.CurrentMode == LaunchMode.DedicatedServer)
            {
                gameObject.SetActive(false);
                return;
            }

            Instance = this;

            cam = GetComponent<UnityEngine.Camera>();
            if (cam != null)
            {
                defaultOrthoSize = cam.orthographicSize;
                targetOrthoSize = defaultOrthoSize;
            }
            ComputeMapBounds();
        }

        /// <summary>
        /// Assigns the Transform this camera should follow. Called by the local player on spawn
        /// (e.g. PlayerController.Start()) rather than this component searching the scene for a player.
        /// </summary>
        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>
        /// Triggers a screen shake impulse (duration seconds, magnitude units).
        /// </summary>
        public void TriggerShake(float duration = 0.15f, float magnitude = 0.15f)
        {
            shakeRemaining = duration;
            shakeIntensity = magnitude;
        }

        public static void Shake(float duration = 0.15f, float magnitude = 0.15f)
        {
            if (Instance != null)
            {
                Instance.TriggerShake(duration, magnitude);
            }
        }

        public void SetTargetOrthoSize(float newSize, float smooth = 0.35f)
        {
            targetOrthoSize = newSize;
            zoomSmoothTime = smooth;
        }

        public void ResetOrthoSize(float smooth = 0.35f)
        {
            targetOrthoSize = defaultOrthoSize;
            zoomSmoothTime = smooth;
        }

        public void SetLookaheadOffset(Vector2 offset, float smooth = 0.25f)
        {
            targetOffset = offset;
            offsetSmoothTime = smooth;
        }

        private void ComputeMapBounds()
        {
            hasBounds = false;

            Tilemap[] tilemapsToProcess = boundsTilemaps;
            if (autoIncludeAllTilemaps || tilemapsToProcess == null || tilemapsToProcess.Length == 0)
            {
                tilemapsToProcess = FindObjectsByType<Tilemap>(FindObjectsInactive.Exclude);
            }

            if (tilemapsToProcess == null || tilemapsToProcess.Length == 0)
            {
                return;
            }

            Vector2 min = Vector2.zero;
            Vector2 max = Vector2.zero;

            foreach (Tilemap tilemap in tilemapsToProcess)
            {
                if (tilemap == null)
                {
                    continue;
                }

                Bounds localBounds = tilemap.localBounds;
                Vector3 worldA = tilemap.transform.TransformPoint(localBounds.min);
                Vector3 worldB = tilemap.transform.TransformPoint(localBounds.max);

                Vector2 tileMin = new Vector2(Mathf.Min(worldA.x, worldB.x), Mathf.Min(worldA.y, worldB.y));
                Vector2 tileMax = new Vector2(Mathf.Max(worldA.x, worldB.x), Mathf.Max(worldA.y, worldB.y));

                if (!hasBounds)
                {
                    min = tileMin;
                    max = tileMax;
                    hasBounds = true;
                }
                else
                {
                    min = Vector2.Min(min, tileMin);
                    max = Vector2.Max(max, tileMax);
                }
            }

            mapMin = min;
            mapMax = max;
        }

        private void LateUpdate()
        {
            if (target == null)
            {
                return;
            }

            // Screen Shake calculation
            if (shakeRemaining > 0f)
            {
                shakeRemaining -= Time.deltaTime;
                float progress = Mathf.Clamp01(shakeRemaining * 8f);
                shakeOffset = UnityEngine.Random.insideUnitCircle * (shakeIntensity * progress);
            }
            else
            {
                shakeOffset = Vector2.zero;
            }

            // Smooth lookahead offset
            currentOffset = Vector2.SmoothDamp(currentOffset, targetOffset, ref offsetVelocity, offsetSmoothTime);

            Vector3 desired = new Vector3(target.position.x + currentOffset.x + shakeOffset.x, target.position.y + currentOffset.y + shakeOffset.y, transform.position.z);
            Vector3 smoothed = Vector3.SmoothDamp(transform.position, desired, ref velocity, smoothTime);

            if (hasBounds)
            {
                smoothed = ClampToMapBounds(smoothed);
            }

            transform.position = smoothed;
        }

        /// <summary>
        /// Clamps X/Y to the map bounds, shrunk by the camera's own orthographic half-width/half-height
        /// so the clamp reflects what the camera can actually see, not the map's raw edge coordinates.
        /// </summary>
        private Vector3 ClampToMapBounds(Vector3 position)
        {
            float halfHeight = cam.orthographicSize;
            float halfWidth = halfHeight * cam.aspect;

            position.x = ClampAxis(position.x, mapMin.x, mapMax.x, halfWidth);
            position.y = ClampAxis(position.y, mapMin.y, mapMax.y, halfHeight);
            return position;
        }

        /// <summary>
        /// Clamps one axis to [mapMin + halfExtent, mapMax - halfExtent]. If the map is narrower than
        /// the camera's view on this axis (min > max after shrinking), centers on the map instead of
        /// clamping, so a narrow map doesn't jitter against an inverted clamp range.
        /// </summary>
        private static float ClampAxis(float value, float mapMinAxis, float mapMaxAxis, float halfExtent)
        {
            float min = mapMinAxis + halfExtent;
            float max = mapMaxAxis - halfExtent;

            if (min > max)
            {
                return (mapMinAxis + mapMaxAxis) * 0.5f;
            }

            return Mathf.Clamp(value, min, max);
        }
    }
}
