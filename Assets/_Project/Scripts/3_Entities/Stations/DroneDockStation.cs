using UnityEngine;
using UnityEngine.Rendering.Universal;
using TheLastSignal.Entities.Drone;

namespace TheLastSignal.Entities.Stations
{
    /// <summary>
    /// Physical drone docking station & recharge pad located in the Airlock room.
    /// Provides the initial landing cradle for ReconDrone_Spectre and ambient status lighting.
    /// </summary>
    public class DroneDockStation : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private SpriteRenderer dockRenderer;
        [SerializeField] private Light2D dockLight;
        [SerializeField] private BoxCollider2D dockCollider;

        [Header("Collision Settings")]
        [SerializeField] private Vector2 colliderSize = new Vector2(1.5f, 1.5f);

        [Header("Lighting Settings")]
        [SerializeField] private Color activeChargeColor = new Color(0f, 0.95f, 1f, 1f);
        [SerializeField] private Color idleStandbyColor = new Color(0f, 0.5f, 0.6f, 0.6f);
        [SerializeField] private float pulseSpeed = 2.0f;
        [SerializeField] private float minIntensity = 0.4f;
        [SerializeField] private float maxIntensity = 0.9f;

        private DroneController drone;
        private static Sprite cachedDockSprite;

        private void Awake()
        {
            // Set to Layer 9 (Obstacles / Environment) to solidly block player movement
            gameObject.layer = 9;

            if (dockRenderer == null)
            {
                dockRenderer = GetComponent<SpriteRenderer>();
            }

            if (dockLight == null)
            {
                dockLight = GetComponentInChildren<Light2D>();
            }

            if (dockCollider == null)
            {
                dockCollider = GetComponent<BoxCollider2D>();
                if (dockCollider == null)
                {
                    dockCollider = gameObject.AddComponent<BoxCollider2D>();
                }
            }
            dockCollider.size = colliderSize;
            dockCollider.isTrigger = false;

            EnsureDockVisuals();
        }

        private void Start()
        {
            drone = FindAnyObjectByType<DroneController>();
            if (drone != null)
            {
                drone.OnFlightStateChanged += HandleFlightStateChanged;
                IgnoreDroneCollision();
                UpdateDockState(drone.IsFlightActive);
            }
        }

        private void OnDestroy()
        {
            if (drone != null)
            {
                drone.OnFlightStateChanged -= HandleFlightStateChanged;
            }
        }

        /// <summary>
        /// Allows the recon drone to land and hover over the dock station freely
        /// while players are solidly blocked by the physical collider.
        /// </summary>
        public void IgnoreDroneCollision()
        {
            if (dockCollider == null) dockCollider = GetComponent<BoxCollider2D>();
            if (dockCollider == null) return;

            if (drone == null) drone = FindAnyObjectByType<DroneController>();
            if (drone != null && drone.TryGetComponent(out Collider2D droneCol))
            {
                Physics2D.IgnoreCollision(dockCollider, droneCol, true);
            }
        }

        private void Update()
        {
            if (dockLight != null)
            {
                // Soft breathing pulse on the recharge pad
                float t = (Mathf.Sin(Time.time * pulseSpeed) + 1f) * 0.5f;
                dockLight.intensity = Mathf.Lerp(minIntensity, maxIntensity, t);
            }
        }

        private void HandleFlightStateChanged(bool inFlight)
        {
            UpdateDockState(inFlight);
        }

        private void UpdateDockState(bool inFlight)
        {
            if (dockLight == null) return;

            if (inFlight)
            {
                // Drone is deployed in mission
                dockLight.color = idleStandbyColor;
                dockLight.pointLightOuterRadius = 1.0f;
            }
            else
            {
                // Check if drone is physically resting on the pad
                bool isDroneDocked = drone != null && Vector2.Distance(drone.transform.position, transform.position) < 0.8f;
                dockLight.color = isDroneDocked ? activeChargeColor : idleStandbyColor;
                dockLight.pointLightOuterRadius = isDroneDocked ? 1.4f : 1.0f;
            }
        }

        private void EnsureDockVisuals()
        {
            if (dockRenderer == null)
            {
                dockRenderer = gameObject.AddComponent<SpriteRenderer>();
            }

            dockRenderer.sortingLayerName = "Characters";
            dockRenderer.sortingOrder = 4; // Below drone (order 10)

            if (dockRenderer.sprite == null)
            {
                if (cachedDockSprite == null)
                {
                    cachedDockSprite = CreateDockPadSprite();
                }
                dockRenderer.sprite = cachedDockSprite;
            }

            if (dockLight == null)
            {
                GameObject lightGo = new GameObject("Light_Drone_Dock");
                lightGo.transform.SetParent(transform, false);
                dockLight = lightGo.AddComponent<Light2D>();
                dockLight.lightType = Light2D.LightType.Point;
                dockLight.pointLightInnerRadius = 0.1f;
                dockLight.pointLightOuterRadius = 1.3f;
                dockLight.color = activeChargeColor;
                dockLight.intensity = 0.75f;
            }
        }

        private static Sprite CreateDockPadSprite()
        {
            int size = 64;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            Color clear = new Color(0, 0, 0, 0);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    tex.SetPixel(x, y, clear);
                }
            }

            Vector2 center = new Vector2(size / 2f, size / 2f);
            Color darkHull = new Color(0.12f, 0.14f, 0.18f, 1f);
            Color rimPlate = new Color(0.24f, 0.28f, 0.36f, 1f);
            Color hazardYellow = new Color(0.92f, 0.72f, 0.10f, 1f);
            Color hazardBlack = new Color(0.08f, 0.08f, 0.10f, 1f);
            Color cyanCoil = new Color(0.0f, 0.85f, 0.95f, 1f);
            Color cyanCore = new Color(0.3f, 0.95f, 1.0f, 1f);
            Color gridLine = new Color(0.18f, 0.22f, 0.28f, 1f);
            Color ledGreen = new Color(0.0f, 1.0f, 0.6f, 1f);

            // 1. Draw beveled octagonal base plate
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Abs(x - center.x);
                    float dy = Mathf.Abs(y - center.y);
                    if (dx + dy <= 42f && dx <= 28f && dy <= 28f)
                    {
                        if (dx >= 26f || dy >= 26f || (dx + dy >= 40f))
                        {
                            tex.SetPixel(x, y, rimPlate);
                        }
                        else
                        {
                            tex.SetPixel(x, y, darkHull);
                        }
                    }
                }
            }

            // 2. Hazard chevron stripes along top and bottom edges
            for (int x = 12; x <= 52; x++)
            {
                Color c = ((x / 4) % 2 == 0) ? hazardYellow : hazardBlack;
                tex.SetPixel(x, 56, c);
                tex.SetPixel(x, 57, c);
                tex.SetPixel(x, 6, c);
                tex.SetPixel(x, 7, c);
            }

            // 3. Inner grid lines
            for (int i = 18; i <= 46; i += 7)
            {
                for (int y = 14; y <= 50; y++)
                {
                    if (tex.GetPixel(i, y).a > 0.5f) tex.SetPixel(i, y, gridLine);
                }
                for (int x = 14; x <= 50; x++)
                {
                    if (tex.GetPixel(x, i).a > 0.5f) tex.SetPixel(x, i, gridLine);
                }
            }

            // 4. Circular induction charging ring
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), center);
                    if (dist >= 11f && dist <= 13.5f)
                    {
                        tex.SetPixel(x, y, cyanCoil);
                    }
                    else if (dist >= 5f && dist <= 6.5f)
                    {
                        tex.SetPixel(x, y, cyanCore);
                    }
                }
            }

            // 5. 4 Corner alignment brackets
            Vector2[] brackets = new Vector2[]
            {
                new Vector2(18, 46), new Vector2(46, 46),
                new Vector2(18, 18), new Vector2(46, 18)
            };
            foreach (var b in brackets)
            {
                int bx = (int)b.x;
                int by = (int)b.y;
                for (int ox = -2; ox <= 2; ox++)
                {
                    for (int oy = -2; oy <= 2; oy++)
                    {
                        if (Mathf.Abs(ox) == 2 || Mathf.Abs(oy) == 2)
                        {
                            tex.SetPixel(bx + ox, by + oy, rimPlate);
                        }
                    }
                }
            }

            // 6. 4 Status LEDs
            tex.SetPixel(10, 10, ledGreen); tex.SetPixel(11, 10, ledGreen);
            tex.SetPixel(53, 10, ledGreen); tex.SetPixel(54, 10, ledGreen);
            tex.SetPixel(10, 53, ledGreen); tex.SetPixel(11, 53, ledGreen);
            tex.SetPixel(53, 53, ledGreen); tex.SetPixel(54, 53, ledGreen);

            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 48f);
        }
    }
}
