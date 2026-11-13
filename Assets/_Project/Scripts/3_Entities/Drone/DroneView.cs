using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using TheLastSignal.Core.Bootstrap;

namespace TheLastSignal.Entities.Drone
{
    /// <summary>
    /// Visual and audio representation of the Recon Drone.
    /// Strictly decoupled from game logic, per ARCHITECTURE_RULES.md Rule 2.
    /// Handles:
    /// - Tactical drone chassis rendering & rotor animation.
    /// - Dynamic flight banking and sinusoidal hover float.
    /// - Steerable URP Light2D spotlight cone.
    /// - Looping rotor motor hum audio with velocity pitch modulation.
    /// - Radar sweep chirp and low-battery alert audio synthesis.
    /// </summary>
    [RequireComponent(typeof(DroneController))]
    public class DroneView : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private SpriteRenderer chassisRenderer;
        [SerializeField] private Transform visualRoot;
        [SerializeField] private Light2D spotlightLight2D;
        [SerializeField] private AudioSource motorAudioSource;
        [SerializeField] private AudioSource sfxAudioSource;

        [Header("Animation Settings")]
        [SerializeField] private float hoverFrequency = 3.5f;
        [SerializeField] private float hoverAmplitude = 0.06f;
        [SerializeField] private float maxBankAngle = 8.0f;
        [SerializeField] private float rotorSpinSpeed = 1440f;

        private DroneController controller;
        private Transform[] rotorProps;
        private Vector3 baseVisualLocalPos;
        private float lastPingTime;
        private float lastLowBatteryWarnTime;
        private Sprite proceduralDroneSprite;

        private void Awake()
        {
            controller = GetComponent<DroneController>();

            // Dedicated Server safety guard (ARCHITECTURE_RULES.md Rule 2 & 4)
            if (GameBootstrap.CurrentMode == LaunchMode.DedicatedServer)
            {
                enabled = false;
                if (chassisRenderer != null) chassisRenderer.enabled = false;
                if (spotlightLight2D != null) spotlightLight2D.enabled = false;
                return;
            }

            EnsureVisualHierarchy();
            EnsureAudioSources();
            EnsureChassisSprite();
        }

        private void EnsureVisualHierarchy()
        {
            if (visualRoot == null)
            {
                Transform found = transform.Find("VisualRoot");
                if (found != null)
                {
                    visualRoot = found;
                }
                else
                {
                    GameObject vGo = new GameObject("VisualRoot");
                    vGo.transform.SetParent(transform, false);
                    visualRoot = vGo.transform;
                }
            }
            baseVisualLocalPos = visualRoot.localPosition;

            if (chassisRenderer == null)
            {
                chassisRenderer = visualRoot.GetComponent<SpriteRenderer>();
                if (chassisRenderer == null)
                {
                    chassisRenderer = visualRoot.gameObject.AddComponent<SpriteRenderer>();
                    chassisRenderer.sortingLayerName = "Characters";
                    chassisRenderer.sortingOrder = 10;
                }
            }

            CreateRotorProps();
            EnsureSpotlightLight2D();
        }

        private void EnsureChassisSprite()
        {
            if (chassisRenderer != null && chassisRenderer.sprite == null)
            {
                proceduralDroneSprite = CreateTactileDroneSprite();
                chassisRenderer.sprite = proceduralDroneSprite;
            }
        }

        private void CreateRotorProps()
        {
            rotorProps = new Transform[4];
            Vector2[] rotorOffsets = new Vector2[]
            {
                new Vector2(-0.35f, 0.32f),  // Top Left
                new Vector2(0.35f, 0.32f),   // Top Right
                new Vector2(-0.35f, -0.32f), // Bottom Left
                new Vector2(0.35f, -0.32f)   // Bottom Right
            };

            Sprite bladeSprite = CreateRotorBladeSprite();

            for (int i = 0; i < 4; i++)
            {
                string rName = $"RotorProp_{i}";
                Transform existing = visualRoot.Find(rName);
                if (existing != null)
                {
                    rotorProps[i] = existing;
                }
                else
                {
                    GameObject propGo = new GameObject(rName);
                    propGo.transform.SetParent(visualRoot, false);
                    propGo.transform.localPosition = new Vector3(rotorOffsets[i].x, rotorOffsets[i].y, -0.05f);
                    SpriteRenderer sr = propGo.AddComponent<SpriteRenderer>();
                    sr.sprite = bladeSprite;
                    sr.sortingLayerName = "Characters";
                    sr.sortingOrder = 11;
                    sr.color = new Color(0.7f, 0.85f, 1f, 0.85f);
                    rotorProps[i] = propGo.transform;
                }
            }
        }

        private void EnsureSpotlightLight2D()
        {
            if (spotlightLight2D == null)
            {
                spotlightLight2D = GetComponentInChildren<Light2D>();
                if (spotlightLight2D == null)
                {
                    Transform pivot = transform.Find("SpotlightPivot");
                    if (pivot == null)
                    {
                        GameObject pivotGo = new GameObject("SpotlightPivot");
                        pivotGo.transform.SetParent(transform, false);
                        pivot = pivotGo.transform;
                    }

                    GameObject lightGo = new GameObject("Light2D_DroneSpotlight");
                    lightGo.transform.SetParent(pivot, false);
                    spotlightLight2D = lightGo.AddComponent<Light2D>();
                    spotlightLight2D.lightType = Light2D.LightType.Point;
                    spotlightLight2D.pointLightInnerAngle = 20f;
                    spotlightLight2D.pointLightOuterAngle = 48f;
                    spotlightLight2D.pointLightInnerRadius = 0.5f;
                    spotlightLight2D.pointLightOuterRadius = 8.5f;
                    spotlightLight2D.color = new Color(0.85f, 0.95f, 1.0f, 1.0f);
                    spotlightLight2D.intensity = 1.6f;
                }
            }

            if (spotlightLight2D != null)
            {
                spotlightLight2D.enabled = false;
            }
        }

        private void EnsureAudioSources()
        {
            if (motorAudioSource == null)
            {
                GameObject motorGo = new GameObject("Audio_MotorHum");
                motorGo.transform.SetParent(transform, false);
                motorAudioSource = motorGo.AddComponent<AudioSource>();
                motorAudioSource.loop = true;
                motorAudioSource.playOnAwake = false;
                motorAudioSource.spatialBlend = 0.35f;
                motorAudioSource.volume = 0.35f;
                motorAudioSource.clip = DroneAudioSynthesizer.GetMotorHumClip();
            }

            if (sfxAudioSource == null)
            {
                GameObject sfxGo = new GameObject("Audio_SFX");
                sfxGo.transform.SetParent(transform, false);
                sfxAudioSource = sfxGo.AddComponent<AudioSource>();
                sfxAudioSource.loop = false;
                sfxAudioSource.playOnAwake = false;
                sfxAudioSource.spatialBlend = 0.2f;
                sfxAudioSource.volume = 0.5f;
            }
        }

        private void OnEnable()
        {
            if (controller != null)
            {
                controller.OnFlightStateChanged += HandleFlightStateChanged;
                controller.OnAimChanged += HandleAimChanged;
                controller.OnBatteryChanged += HandleBatteryChanged;
                controller.OnRadarSweep += HandleRadarSweep;
                HandleFlightStateChanged(controller.IsFlightActive);
            }
        }

        private void OnDisable()
        {
            if (controller != null)
            {
                controller.OnFlightStateChanged -= HandleFlightStateChanged;
                controller.OnAimChanged -= HandleAimChanged;
                controller.OnBatteryChanged -= HandleBatteryChanged;
                controller.OnRadarSweep -= HandleRadarSweep;
            }

            if (motorAudioSource != null && motorAudioSource.isPlaying)
            {
                motorAudioSource.Stop();
            }
        }

        private void HandleFlightStateChanged(bool active)
        {
            if (visualRoot != null)
            {
                visualRoot.gameObject.SetActive(true); // Drone remains physically visible on the map at all times
                if (!active)
                {
                    visualRoot.localPosition = baseVisualLocalPos;
                    visualRoot.localRotation = Quaternion.identity;
                }
            }

            if (spotlightLight2D != null)
            {
                spotlightLight2D.enabled = active;
            }

            if (active)
            {
                if (motorAudioSource != null && !motorAudioSource.isPlaying)
                {
                    motorAudioSource.Play();
                }

                if (sfxAudioSource != null)
                {
                    sfxAudioSource.PlayOneShot(DroneAudioSynthesizer.GetTerminalEngageClip(), 0.6f);
                }
            }
            else
            {
                if (motorAudioSource != null && motorAudioSource.isPlaying)
                {
                    motorAudioSource.Stop();
                }

                if (sfxAudioSource != null)
                {
                    sfxAudioSource.PlayOneShot(DroneAudioSynthesizer.GetTerminalDisengageClip(), 0.5f);
                }
            }
        }

        private void HandleAimChanged(float angleDegrees)
        {
            // Spotlight orientation is handled by controller's spotlightPivot
        }

        private void HandleBatteryChanged(float battery)
        {
            if (controller != null && controller.IsFlightActive && battery <= 20f)
            {
                if (Time.time - lastLowBatteryWarnTime > 3.0f)
                {
                    lastLowBatteryWarnTime = Time.time;
                    if (sfxAudioSource != null)
                    {
                        sfxAudioSource.PlayOneShot(DroneAudioSynthesizer.GetLowBatteryClip(), 0.5f);
                    }
                }
            }
        }

        private void HandleRadarSweep(IReadOnlyList<RadarPingData> targets)
        {
            if (targets != null && targets.Count > 0)
            {
                if (Time.time - lastPingTime > 1.2f)
                {
                    lastPingTime = Time.time;
                    if (sfxAudioSource != null)
                    {
                        sfxAudioSource.PlayOneShot(DroneAudioSynthesizer.GetRadarPingClip(), 0.45f);
                    }
                }
            }
        }

        private void Update()
        {
            if (controller == null || !controller.IsFlightActive) return;

            // 1. Sinusoidal hover float
            float hoverOffset = Mathf.Sin(Time.time * hoverFrequency) * hoverAmplitude;
            if (visualRoot != null)
            {
                visualRoot.localPosition = baseVisualLocalPos + new Vector3(0f, hoverOffset, 0f);

                // 2. Flight banking tilt based on velocity X
                Rigidbody2D rb = GetComponent<Rigidbody2D>();
                if (rb != null)
                {
                    float vx = rb.linearVelocity.x;
                    float targetBank = Mathf.Clamp(-vx * 2.2f, -maxBankAngle, maxBankAngle);
                    visualRoot.localRotation = Quaternion.Euler(0f, 0f, targetBank);

                    // 3. Motor hum pitch modulation with speed
                    if (motorAudioSource != null)
                    {
                        float speed = rb.linearVelocity.magnitude;
                        motorAudioSource.pitch = Mathf.Lerp(1.0f, 1.22f, speed / 5.0f);
                    }
                }
            }

            // 4. Spin rotor blades
            float rotorDelta = rotorSpinSpeed * Time.deltaTime;
            if (rotorProps != null)
            {
                for (int i = 0; i < rotorProps.Length; i++)
                {
                    if (rotorProps[i] != null)
                    {
                        rotorProps[i].Rotate(0f, 0f, (i % 2 == 0 ? 1f : -1f) * rotorDelta);
                    }
                }
            }
        }

        // =====================================================================
        // PROCEDURAL PIXEL-ART TACTILE ASSETS GENERATION
        // =====================================================================

        private static Sprite CreateTactileDroneSprite()
        {
            int size = 48;
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
            Color darkHull = new Color(0.12f, 0.15f, 0.20f, 1f);
            Color armorPlate = new Color(0.22f, 0.28f, 0.36f, 1f);
            Color armorHighlight = new Color(0.38f, 0.46f, 0.58f, 1f);
            Color carbonStrut = new Color(0.08f, 0.10f, 0.12f, 1f);
            Color cyanSensor = new Color(0.0f, 0.95f, 1.0f, 1f);
            Color cyanGaze = new Color(0.5f, 1.0f, 1.0f, 1f);
            Color amberNav = new Color(1.0f, 0.65f, 0.1f, 1f);
            Color redNav = new Color(1.0f, 0.2f, 0.2f, 1f);

            // 1. Draw 4 diagonal carbon arms
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - center.x;
                    float dy = y - center.y;
                    if (Mathf.Abs(Mathf.Abs(dx) - Mathf.Abs(dy)) <= 2.5f && (Mathf.Abs(dx) >= 6 && Mathf.Abs(dx) <= 18))
                    {
                        tex.SetPixel(x, y, carbonStrut);
                    }
                }
            }

            // 2. Draw 4 motor nacelle pods at strut ends
            Vector2[] podCenters = new Vector2[]
            {
                new Vector2(center.x - 14, center.y + 14),
                new Vector2(center.x + 14, center.y + 14),
                new Vector2(center.x - 14, center.y - 14),
                new Vector2(center.x + 14, center.y - 14)
            };

            foreach (var pc in podCenters)
            {
                for (int y = (int)pc.y - 4; y <= (int)pc.y + 4; y++)
                {
                    for (int x = (int)pc.x - 4; x <= (int)pc.x + 4; x++)
                    {
                        float d = Vector2.Distance(new Vector2(x, y), pc);
                        if (d <= 3.8f)
                        {
                            tex.SetPixel(x, y, d <= 2.2f ? armorPlate : darkHull);
                        }
                    }
                }
            }

            // Front Nav-lights (Amber) and Rear Nav-lights (Red)
            tex.SetPixel((int)podCenters[0].x, (int)podCenters[0].y + 2, amberNav);
            tex.SetPixel((int)podCenters[1].x, (int)podCenters[1].y + 2, amberNav);
            tex.SetPixel((int)podCenters[2].x, (int)podCenters[2].y - 2, redNav);
            tex.SetPixel((int)podCenters[3].x, (int)podCenters[3].y - 2, redNav);

            // 3. Central Hexagonal Hull
            for (int y = (int)center.y - 10; y <= (int)center.y + 10; y++)
            {
                for (int x = (int)center.x - 10; x <= (int)center.x + 10; x++)
                {
                    float dx = Mathf.Abs(x - center.x);
                    float dy = Mathf.Abs(y - center.y);
                    if (dx + (dy * 0.7f) <= 9.5f)
                    {
                        Color c = armorPlate;
                        if (dx + (dy * 0.7f) >= 8.2f) c = armorHighlight;
                        else if (dy > 6f) c = darkHull;
                        tex.SetPixel(x, y, c);
                    }
                }
            }

            // 4. Central Optical Sensor Eye (glowing cyan dome)
            for (int y = (int)center.y - 4; y <= (int)center.y + 4; y++)
            {
                for (int x = (int)center.x - 4; x <= (int)center.x + 4; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), center);
                    if (d <= 3.6f)
                    {
                        tex.SetPixel(x, y, d <= 1.5f ? cyanGaze : cyanSensor);
                    }
                }
            }

            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 32f);
        }

        private static Sprite CreateRotorBladeSprite()
        {
            int w = 24;
            int h = 6;
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            Color c = new Color(0.8f, 0.9f, 1f, 0.75f);
            Color clear = new Color(0, 0, 0, 0);

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float dx = Mathf.Abs(x - (w / 2f));
                    float dy = Mathf.Abs(y - (h / 2f));
                    if (dx <= 10f && dy <= 1.5f)
                    {
                        tex.SetPixel(x, y, c);
                    }
                    else
                    {
                        tex.SetPixel(x, y, clear);
                    }
                }
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 32f);
        }
    }
}
