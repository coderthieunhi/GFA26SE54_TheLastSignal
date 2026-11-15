using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using TheLastSignal.Core.Bootstrap;
using TheLastSignal.Entities.Drone;

namespace TheLastSignal.UI
{
    /// <summary>
    /// Brutalist Sci-Fi Full-Screen CRT Overlay for Recon Drone (FR-SUR-06, BR-17, GDD 9.1.1).
    /// Features:
    /// - True full-screen 1920x1080 canvas anchoring (no center clumping).
    /// - High-tech top telemetry header with real-time grid coordinates, velocity, and battery gauge.
    /// - Crisp bottom controls & operator vulnerability monitor.
    /// - Dedicated bottom-right circular motion radar scope with concentric range rings and blips.
    /// - Hostile proximity warning banner (The Shade detection).
    /// - Tactical center reticle and subtle CRT surveillance corner brackets.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class DroneHUDOverlay : MonoBehaviour
    {
        public static DroneHUDOverlay Instance { get; private set; }

        [Header("Telemetry Labels")]
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI signalText;
        [SerializeField] private TextMeshProUGUI coordsText;
        [SerializeField] private TextMeshProUGUI flightSpeedText;
        [SerializeField] private TextMeshProUGUI operatorStatusText;

        [Header("Battery Display")]
        [SerializeField] private Image batteryBarFill;
        [SerializeField] private TextMeshProUGUI batteryText;
        [SerializeField] private GameObject lowBatteryWarningBanner;

        [Header("Radar Display")]
        [SerializeField] private RectTransform radarScopeRect;
        [SerializeField] private RectTransform radarSweepArm;
        [SerializeField] private TextMeshProUGUI radarTargetCountText;
        [SerializeField] private GameObject radarBlipContainer;
        [SerializeField] private GameObject threatAlertBanner;
        [SerializeField] private TextMeshProUGUI threatAlertText;

        [Header("Root")]
        [SerializeField] private GameObject overlayRoot;

        [Header("Reticle & Crosshair")]
        [SerializeField] private bool showCenterReticle = false;

        private DroneController activeDrone;
        private readonly List<GameObject> activeBlipObjects = new List<GameObject>();
        private float sweepAngle = 0f;
        private bool isOpen = false;

        private void Awake()
        {
            Instance = this;

            // Dedicated Server safety guard (ARCHITECTURE_RULES.md Rule 2 & 4)
            if (GameBootstrap.CurrentMode == LaunchMode.DedicatedServer)
            {
                gameObject.SetActive(false);
                return;
            }

            EnsureCanvasAndHierarchy();
            if (!showCenterReticle)
            {
                RemoveExistingReticles();
            }
            Close();
        }

        public void Open(DroneController drone)
        {
            if (drone == null) return;

            activeDrone = drone;
            activeDrone.OnBatteryChanged += HandleBatteryChanged;
            activeDrone.OnRadarSweep += HandleRadarSweep;
            activeDrone.OnFlightStateChanged += HandleFlightStateChanged;

            isOpen = true;
            if (overlayRoot != null) overlayRoot.SetActive(true);

            if (!showCenterReticle)
            {
                RemoveExistingReticles();
            }

            HandleBatteryChanged(activeDrone.CurrentBattery);
            if (lowBatteryWarningBanner != null) lowBatteryWarningBanner.SetActive(false);
            if (threatAlertBanner != null) threatAlertBanner.SetActive(false);
        }

        public void Close()
        {
            isOpen = false;
            if (activeDrone != null)
            {
                activeDrone.OnBatteryChanged -= HandleBatteryChanged;
                activeDrone.OnRadarSweep -= HandleRadarSweep;
                activeDrone.OnFlightStateChanged -= HandleFlightStateChanged;
                activeDrone = null;
            }

            ClearBlips();
            if (overlayRoot != null) overlayRoot.SetActive(false);
        }

        private void HandleFlightStateChanged(bool active)
        {
            if (!active)
            {
                Close();
            }
        }

        private void HandleBatteryChanged(float battery)
        {
            if (!isOpen || activeDrone == null) return;

            float pct = activeDrone.BatteryPercent;
            if (batteryBarFill != null)
            {
                batteryBarFill.fillAmount = pct;
                if (pct > 0.5f)
                    batteryBarFill.color = new Color(0.0f, 0.95f, 0.85f, 1f); // Vibrant Cyan
                else if (pct > 0.2f)
                    batteryBarFill.color = new Color(1.0f, 0.72f, 0.12f, 1f); // Amber
                else
                    batteryBarFill.color = new Color(1.0f, 0.20f, 0.20f, 1f); // Critical Red
            }

            if (batteryText != null)
            {
                string status = pct > 0.5f ? "NOMINAL" : (pct > 0.2f ? "CAUTION" : "<color=#FF2222>CRITICAL LOW</color>");
                batteryText.text = $"BATTERY: {battery:0.0}% [{status}]";
            }

            if (lowBatteryWarningBanner != null)
            {
                lowBatteryWarningBanner.SetActive(pct <= 0.2f);
            }
        }

        private void HandleRadarSweep(IReadOnlyList<RadarPingData> targets)
        {
            if (!isOpen || activeDrone == null || radarBlipContainer == null) return;

            ClearBlips();
            bool threatFound = false;
            string threatInfo = "";
            float maxRadarRadius = activeDrone.RadarRadius > 0.1f ? activeDrone.RadarRadius : 12f;
            float uiScopeRadius = 78f; // Half radius of radar scope inner circle

            int count = targets != null ? targets.Count : 0;
            if (radarTargetCountText != null)
            {
                radarTargetCountText.text = $"TARGETS: [{count}] IN RANGE";
            }

            for (int i = 0; i < count; i++)
            {
                RadarPingData data = targets[i];
                if (data.IsHostile)
                {
                    threatFound = true;
                    threatInfo = $"{data.EntityName} ({data.Distance:0.0}m)";
                }

                // Create visual blip on radar scope
                GameObject blip = new GameObject($"Blip_{i}", typeof(RectTransform), typeof(Image));
                blip.transform.SetParent(radarBlipContainer.transform, false);

                RectTransform rt = blip.GetComponent<RectTransform>();
                rt.sizeDelta = data.IsHostile ? new Vector2(10f, 10f) : new Vector2(7f, 7f);

                // Map relative offset [-radarRadius, +radarRadius] to radar scope circle
                Vector2 normOffset = data.RelativeOffset / maxRadarRadius;
                if (normOffset.magnitude > 1f) normOffset = normOffset.normalized;
                rt.anchoredPosition = normOffset * uiScopeRadius;

                Image img = blip.GetComponent<Image>();
                img.color = data.IsHostile ? new Color(1f, 0.15f, 0.15f, 1f) : new Color(0.15f, 1f, 0.40f, 1f);

                activeBlipObjects.Add(blip);
            }

            if (threatAlertBanner != null)
            {
                threatAlertBanner.SetActive(threatFound);
                if (threatFound && threatAlertText != null)
                {
                    threatAlertText.text = $"<b><color=#FF2222>⚠ WARNING: HOSTILE ANOMALY IN RANGE // {threatInfo}</color></b>";
                }
            }
        }

        private void ClearBlips()
        {
            for (int i = 0; i < activeBlipObjects.Count; i++)
            {
                if (activeBlipObjects[i] != null) Destroy(activeBlipObjects[i]);
            }
            activeBlipObjects.Clear();
        }

        private void Update()
        {
            if (!isOpen || activeDrone == null) return;

            // 1. Rotate radar sweep arm smoothly
            sweepAngle = (sweepAngle + (180f * Time.deltaTime)) % 360f;
            if (radarSweepArm != null)
            {
                radarSweepArm.localRotation = Quaternion.Euler(0f, 0f, -sweepAngle);
            }

            // 2. Update telemetry readouts
            Vector3 pos = activeDrone.transform.position;
            if (coordsText != null)
            {
                coordsText.text = $"GRID: [X: {pos.x:+00.0;-00.0} | Y: {pos.y:+00.0;-00.0}]";
            }

            if (flightSpeedText != null)
            {
                Rigidbody2D rb = activeDrone.GetComponent<Rigidbody2D>();
                float spd = rb != null ? rb.linearVelocity.magnitude : 0f;
                flightSpeedText.text = $"VELOCITY: {spd:0.0} m/s [VECTOR ACTIVE]";
            }

            if (signalText != null)
            {
                float noise = (Mathf.Sin(Time.time * 8f) * 0.8f) + 99.1f;
                signalText.text = $"<color=#55CCAA>UPLINK: ACTIVE</color> | FREQ: 864.20 MHz | SIGNAL: <color=#00FFCC>{noise:0.0}% NOMINAL</color>";
            }

            // 3. Monitor Operator physical body status
            if (operatorStatusText != null && activeDrone.CurrentOperator != null)
            {
                var pilotHealth = activeDrone.CurrentOperator.GetComponent<TheLastSignal.Entities.Player.PlayerHealth>();
                if (pilotHealth != null)
                {
                    if (pilotHealth.HealthPercent < 0.99f)
                    {
                        operatorStatusText.text = "<b><color=#FF2222>PILOT BODY: UNDER ATTACK AT AIRLOCK CONSOLE!</color></b>";
                    }
                    else
                    {
                        operatorStatusText.text = "<color=#00FFCC>PILOT BODY: STATIONARY @ AIRLOCK CONSOLE [VULNERABLE]</color>";
                    }
                }
            }
        }

        // =====================================================================
        // FULLSCREEN PROCEDURAL UI LAYOUT
        // =====================================================================

        private void EnsureCanvasAndHierarchy()
        {
            // 1. Ensure this GameObject stretches full-screen
            RectTransform selfRect = GetComponent<RectTransform>();
            if (selfRect == null) selfRect = gameObject.AddComponent<RectTransform>();
            selfRect.anchorMin = Vector2.zero;
            selfRect.anchorMax = Vector2.one;
            selfRect.offsetMin = Vector2.zero;
            selfRect.offsetMax = Vector2.zero;
            selfRect.pivot = new Vector2(0.5f, 0.5f);

            // 2. Check if already under a Canvas; if not, create root Canvas
            Canvas parentCanvas = GetComponentInParent<Canvas>();
            if (parentCanvas == null)
            {
                GameObject canvasObj = GameObject.Find("LobbyCanvas");
                if (canvasObj == null)
                {
                    Canvas anyCanvas = FindAnyObjectByType<Canvas>();
                    if (anyCanvas != null) canvasObj = anyCanvas.gameObject;
                }

                if (canvasObj != null)
                {
                    transform.SetParent(canvasObj.transform, false);
                    selfRect.anchorMin = Vector2.zero;
                    selfRect.anchorMax = Vector2.one;
                    selfRect.offsetMin = Vector2.zero;
                    selfRect.offsetMax = Vector2.zero;
                }
                else
                {
                    Canvas c = gameObject.AddComponent<Canvas>();
                    c.renderMode = RenderMode.ScreenSpaceOverlay;
                    c.sortingOrder = 95;
                    CanvasScaler cs = gameObject.AddComponent<CanvasScaler>();
                    cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                    cs.referenceResolution = new Vector2(1920, 1080);
                    gameObject.AddComponent<GraphicRaycaster>();
                }
            }

            // 3. Clear any old procedural children to avoid duplicates
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                DestroyImmediate(transform.GetChild(i).gameObject);
            }

            BuildProceduralDroneHUD();
        }

        private void BuildProceduralDroneHUD()
        {
            // Root container stretched full-screen
            overlayRoot = new GameObject("OverlayRoot", typeof(RectTransform));
            overlayRoot.transform.SetParent(transform, false);
            RectTransform rootRt = overlayRoot.GetComponent<RectTransform>();
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.offsetMin = Vector2.zero;
            rootRt.offsetMax = Vector2.zero;

            // 1. Subtle CRT Tint & Surveillance Corner Brackets
            GameObject tintGo = new GameObject("CRT_Tint", typeof(RectTransform), typeof(Image));
            tintGo.transform.SetParent(overlayRoot.transform, false);
            RectTransform tintRt = tintGo.GetComponent<RectTransform>();
            tintRt.anchorMin = Vector2.zero;
            tintRt.anchorMax = Vector2.one;
            tintRt.offsetMin = Vector2.zero;
            tintRt.offsetMax = Vector2.zero;
            Image tintImg = tintGo.GetComponent<Image>();
            tintImg.color = new Color(0.02f, 0.05f, 0.07f, 0.18f); // Very light sci-fi glass tint
            tintImg.raycastTarget = false;

            CreateCornerBrackets(overlayRoot.transform);

            // 2. Full-Width Top Telemetry Header Bar
            GameObject topBar = new GameObject("TopBar", typeof(RectTransform), typeof(Image));
            topBar.transform.SetParent(overlayRoot.transform, false);
            RectTransform topBarRt = topBar.GetComponent<RectTransform>();
            topBarRt.anchorMin = new Vector2(0f, 1f);
            topBarRt.anchorMax = new Vector2(1f, 1f);
            topBarRt.pivot = new Vector2(0.5f, 1f);
            topBarRt.anchoredPosition = Vector2.zero;
            topBarRt.sizeDelta = new Vector2(0f, 68f);
            Image topBarBg = topBar.GetComponent<Image>();
            topBarBg.color = new Color(0.05f, 0.09f, 0.13f, 0.90f);

            // Bottom accent border line for TopBar
            GameObject topBorder = new GameObject("BorderLine", typeof(RectTransform), typeof(Image));
            topBorder.transform.SetParent(topBar.transform, false);
            RectTransform tbRt = topBorder.GetComponent<RectTransform>();
            tbRt.anchorMin = new Vector2(0f, 0f);
            tbRt.anchorMax = new Vector2(1f, 0f);
            tbRt.sizeDelta = new Vector2(0f, 2f);
            tbRt.anchoredPosition = Vector2.zero;
            Image tbImg = topBorder.GetComponent<Image>();
            tbImg.color = new Color(0.0f, 0.9f, 0.8f, 0.5f);

            // Left: Header Title & Uplink Telemetry
            GameObject titleGo = new GameObject("TitleText", typeof(RectTransform), typeof(TextMeshProUGUI));
            titleGo.transform.SetParent(topBar.transform, false);
            titleText = titleGo.GetComponent<TextMeshProUGUI>();
            titleText.text = "<b><color=#00FFCC>RECON DRONE FEED</color> // <color=#FFFFFF>AIRLOCK CRT TERMINAL</color></b>";
            titleText.fontSize = 17;
            titleText.alignment = TextAlignmentOptions.Left;
            RectTransform titleRt = titleGo.GetComponent<RectTransform>();
            titleRt.anchorMin = new Vector2(0f, 0.5f);
            titleRt.anchorMax = new Vector2(0f, 0.5f);
            titleRt.pivot = new Vector2(0f, 0.5f);
            titleRt.anchoredPosition = new Vector2(35f, 10f);
            titleRt.sizeDelta = new Vector2(500f, 26f);

            GameObject sigGo = new GameObject("SignalText", typeof(RectTransform), typeof(TextMeshProUGUI));
            sigGo.transform.SetParent(topBar.transform, false);
            signalText = sigGo.GetComponent<TextMeshProUGUI>();
            signalText.text = "<color=#55CCAA>UPLINK: ACTIVE</color> | FREQ: 864.20 MHz | SIGNAL: <color=#00FFCC>99.2% NOMINAL</color>";
            signalText.fontSize = 12;
            signalText.color = new Color(0.65f, 0.80f, 0.85f, 1f);
            RectTransform sigRt = sigGo.GetComponent<RectTransform>();
            sigRt.anchorMin = new Vector2(0f, 0.5f);
            sigRt.anchorMax = new Vector2(0f, 0.5f);
            sigRt.pivot = new Vector2(0f, 0.5f);
            sigRt.anchoredPosition = new Vector2(35f, -14f);
            sigRt.sizeDelta = new Vector2(500f, 22f);

            // Center: Grid Coordinates & Velocity
            GameObject coordsGo = new GameObject("CoordsText", typeof(RectTransform), typeof(TextMeshProUGUI));
            coordsGo.transform.SetParent(topBar.transform, false);
            coordsText = coordsGo.GetComponent<TextMeshProUGUI>();
            coordsText.text = "GRID: [X: +00.0 | Y: +00.0]";
            coordsText.fontSize = 13;
            coordsText.fontStyle = FontStyles.Bold;
            coordsText.alignment = TextAlignmentOptions.Center;
            coordsText.color = new Color(0.7f, 0.9f, 1f, 1f);
            RectTransform coordsRt = coordsGo.GetComponent<RectTransform>();
            coordsRt.anchorMin = new Vector2(0.5f, 0.5f);
            coordsRt.anchorMax = new Vector2(0.5f, 0.5f);
            coordsRt.anchoredPosition = new Vector2(0f, 10f);
            coordsRt.sizeDelta = new Vector2(350f, 24f);

            GameObject speedGo = new GameObject("SpeedText", typeof(RectTransform), typeof(TextMeshProUGUI));
            speedGo.transform.SetParent(topBar.transform, false);
            flightSpeedText = speedGo.GetComponent<TextMeshProUGUI>();
            flightSpeedText.text = "VELOCITY: 4.8 m/s [VECTOR ACTIVE]";
            flightSpeedText.fontSize = 12;
            flightSpeedText.alignment = TextAlignmentOptions.Center;
            flightSpeedText.color = new Color(0.55f, 0.75f, 0.85f, 1f);
            RectTransform spdRt = speedGo.GetComponent<RectTransform>();
            spdRt.anchorMin = new Vector2(0.5f, 0.5f);
            spdRt.anchorMax = new Vector2(0.5f, 0.5f);
            spdRt.anchoredPosition = new Vector2(0f, -14f);
            spdRt.sizeDelta = new Vector2(350f, 22f);

            // Right: Tactical Battery Gauge
            GameObject battCont = new GameObject("BatteryContainer", typeof(RectTransform));
            battCont.transform.SetParent(topBar.transform, false);
            RectTransform battContRt = battCont.GetComponent<RectTransform>();
            battContRt.anchorMin = new Vector2(1f, 0.5f);
            battContRt.anchorMax = new Vector2(1f, 0.5f);
            battContRt.pivot = new Vector2(1f, 0.5f);
            battContRt.anchoredPosition = new Vector2(-35f, 0f);
            battContRt.sizeDelta = new Vector2(320f, 50f);

            GameObject battLabel = new GameObject("BatteryLabel", typeof(RectTransform), typeof(TextMeshProUGUI));
            battLabel.transform.SetParent(battCont.transform, false);
            batteryText = battLabel.GetComponent<TextMeshProUGUI>();
            batteryText.text = "BATTERY: 100.0% [NOMINAL]";
            batteryText.fontSize = 13;
            batteryText.fontStyle = FontStyles.Bold;
            batteryText.alignment = TextAlignmentOptions.Right;
            batteryText.color = Color.white;
            RectTransform bltRt = battLabel.GetComponent<RectTransform>();
            bltRt.anchorMin = new Vector2(0f, 0.5f);
            bltRt.anchorMax = new Vector2(1f, 1f);
            bltRt.offsetMin = Vector2.zero;
            bltRt.offsetMax = Vector2.zero;

            GameObject battTrack = new GameObject("BatteryTrack", typeof(RectTransform), typeof(Image));
            battTrack.transform.SetParent(battCont.transform, false);
            RectTransform btRt = battTrack.GetComponent<RectTransform>();
            btRt.anchorMin = new Vector2(0f, 0f);
            btRt.anchorMax = new Vector2(1f, 0.42f);
            btRt.offsetMin = Vector2.zero;
            btRt.offsetMax = Vector2.zero;
            Image btImg = battTrack.GetComponent<Image>();
            btImg.color = new Color(0.10f, 0.15f, 0.22f, 1f);

            GameObject battFill = new GameObject("BatteryFill", typeof(RectTransform), typeof(Image));
            battFill.transform.SetParent(battTrack.transform, false);
            RectTransform bfRt = battFill.GetComponent<RectTransform>();
            bfRt.anchorMin = Vector2.zero;
            bfRt.anchorMax = Vector2.one;
            bfRt.offsetMin = new Vector2(2f, 2f);
            bfRt.offsetMax = new Vector2(-2f, -2f);
            batteryBarFill = battFill.GetComponent<Image>();
            batteryBarFill.type = Image.Type.Filled;
            batteryBarFill.fillMethod = Image.FillMethod.Horizontal;
            batteryBarFill.fillAmount = 1f;
            batteryBarFill.color = new Color(0.0f, 0.95f, 0.85f, 1f);

            // 3. Threat Alert Banner (Centered below TopBar)
            threatAlertBanner = new GameObject("ThreatAlertBanner", typeof(RectTransform), typeof(Image));
            threatAlertBanner.transform.SetParent(overlayRoot.transform, false);
            RectTransform thRt = threatAlertBanner.GetComponent<RectTransform>();
            thRt.anchorMin = new Vector2(0.5f, 1f);
            thRt.anchorMax = new Vector2(0.5f, 1f);
            thRt.pivot = new Vector2(0.5f, 1f);
            thRt.anchoredPosition = new Vector2(0f, -78f);
            thRt.sizeDelta = new Vector2(620f, 36f);
            Image thBg = threatAlertBanner.GetComponent<Image>();
            thBg.color = new Color(0.65f, 0.08f, 0.08f, 0.92f);

            GameObject thTextGo = new GameObject("ThreatText", typeof(RectTransform), typeof(TextMeshProUGUI));
            thTextGo.transform.SetParent(threatAlertBanner.transform, false);
            threatAlertText = thTextGo.GetComponent<TextMeshProUGUI>();
            threatAlertText.text = "<b><color=#FFFFFF>⚠ WARNING: HOSTILE ANOMALY (THE SHADE) DETECTED IN SENSOR RANGE</color></b>";
            threatAlertText.fontSize = 13;
            threatAlertText.fontStyle = FontStyles.Bold;
            threatAlertText.alignment = TextAlignmentOptions.Center;
            RectTransform thtRt = thTextGo.GetComponent<RectTransform>();
            thtRt.anchorMin = Vector2.zero;
            thtRt.anchorMax = Vector2.one;
            thtRt.offsetMin = Vector2.zero;
            thtRt.offsetMax = Vector2.zero;
            threatAlertBanner.SetActive(false);

            // Low Battery Banner
            lowBatteryWarningBanner = new GameObject("LowBatteryBanner", typeof(RectTransform), typeof(Image));
            lowBatteryWarningBanner.transform.SetParent(overlayRoot.transform, false);
            RectTransform lbRt = lowBatteryWarningBanner.GetComponent<RectTransform>();
            lbRt.anchorMin = new Vector2(0.5f, 1f);
            lbRt.anchorMax = new Vector2(0.5f, 1f);
            lbRt.pivot = new Vector2(0.5f, 1f);
            lbRt.anchoredPosition = new Vector2(0f, -120f);
            lbRt.sizeDelta = new Vector2(540f, 32f);
            Image lbBg = lowBatteryWarningBanner.GetComponent<Image>();
            lbBg.color = new Color(0.70f, 0.40f, 0.05f, 0.92f);

            GameObject lbtGo = new GameObject("LowBatteryText", typeof(RectTransform), typeof(TextMeshProUGUI));
            lbtGo.transform.SetParent(lowBatteryWarningBanner.transform, false);
            TextMeshProUGUI lbtText = lbtGo.GetComponent<TextMeshProUGUI>();
            lbtText.text = "<b>CRITICAL LOW BATTERY // RECALL TO AIRLOCK STATION REQUIRED</b>";
            lbtText.fontSize = 12;
            lbtText.alignment = TextAlignmentOptions.Center;
            lbtText.color = Color.white;
            RectTransform lbtrRt = lbtGo.GetComponent<RectTransform>();
            lbtrRt.anchorMin = Vector2.zero;
            lbtrRt.anchorMax = Vector2.one;
            lbtrRt.offsetMin = Vector2.zero;
            lbtrRt.offsetMax = Vector2.zero;
            lowBatteryWarningBanner.SetActive(false);

            // 4. Tactical Motion Radar Scope (Cleanly docked in Bottom-Right Corner)
            GameObject radarScopeGo = new GameObject("RadarScopeWidget", typeof(RectTransform), typeof(Image));
            radarScopeGo.transform.SetParent(overlayRoot.transform, false);
            radarScopeRect = radarScopeGo.GetComponent<RectTransform>();
            radarScopeRect.anchorMin = new Vector2(1f, 0f);
            radarScopeRect.anchorMax = new Vector2(1f, 0f);
            radarScopeRect.pivot = new Vector2(1f, 0f);
            radarScopeRect.anchoredPosition = new Vector2(-30f, 75f);
            radarScopeRect.sizeDelta = new Vector2(210f, 210f);
            Image radarBg = radarScopeGo.GetComponent<Image>();
            radarBg.color = new Color(0.04f, 0.08f, 0.12f, 0.92f);

            // Radar Header Label
            GameObject radarTitleGo = new GameObject("RadarTitle", typeof(RectTransform), typeof(TextMeshProUGUI));
            radarTitleGo.transform.SetParent(radarScopeGo.transform, false);
            TextMeshProUGUI rTitle = radarTitleGo.GetComponent<TextMeshProUGUI>();
            rTitle.text = "MOTION RADAR // 12m";
            rTitle.fontSize = 11;
            rTitle.fontStyle = FontStyles.Bold;
            rTitle.alignment = TextAlignmentOptions.Center;
            rTitle.color = new Color(0.0f, 1f, 0.85f, 1f);
            RectTransform rtlRt = radarTitleGo.GetComponent<RectTransform>();
            rtlRt.anchorMin = new Vector2(0f, 1f);
            rtlRt.anchorMax = new Vector2(1f, 1f);
            rtlRt.pivot = new Vector2(0.5f, 1f);
            rtlRt.anchoredPosition = new Vector2(0f, -8f);
            rtlRt.sizeDelta = new Vector2(210f, 20f);

            // Concentric Range Rings (4m, 8m, 12m)
            CreateRadarRings(radarScopeGo.transform);

            // Rotating Sweep Arm
            GameObject sweepGo = new GameObject("RadarSweepArm", typeof(RectTransform), typeof(Image));
            sweepGo.transform.SetParent(radarScopeGo.transform, false);
            radarSweepArm = sweepGo.GetComponent<RectTransform>();
            radarSweepArm.anchorMin = new Vector2(0.5f, 0.48f);
            radarSweepArm.anchorMax = new Vector2(0.5f, 0.48f);
            radarSweepArm.pivot = new Vector2(0.5f, 0f);
            radarSweepArm.anchoredPosition = Vector2.zero;
            radarSweepArm.sizeDelta = new Vector2(2f, 80f);
            Image sweepImg = sweepGo.GetComponent<Image>();
            sweepImg.color = new Color(0.0f, 1f, 0.85f, 0.65f);

            // Center Drone Marker (Cyan Dot)
            GameObject centerDot = new GameObject("CenterDroneDot", typeof(RectTransform), typeof(Image));
            centerDot.transform.SetParent(radarScopeGo.transform, false);
            RectTransform cdRt = centerDot.GetComponent<RectTransform>();
            cdRt.anchorMin = new Vector2(0.5f, 0.48f);
            cdRt.anchorMax = new Vector2(0.5f, 0.48f);
            cdRt.anchoredPosition = Vector2.zero;
            cdRt.sizeDelta = new Vector2(6f, 6f);
            Image cdImg = centerDot.GetComponent<Image>();
            cdImg.color = new Color(0.0f, 1f, 1f, 1f);

            // Radar Blip Container
            radarBlipContainer = new GameObject("RadarBlips", typeof(RectTransform));
            radarBlipContainer.transform.SetParent(radarScopeGo.transform, false);
            RectTransform blipsRt = radarBlipContainer.GetComponent<RectTransform>();
            blipsRt.anchorMin = new Vector2(0.5f, 0.48f);
            blipsRt.anchorMax = new Vector2(0.5f, 0.48f);
            blipsRt.anchoredPosition = Vector2.zero;
            blipsRt.sizeDelta = new Vector2(170f, 170f);

            // Radar Footer Target Counter
            GameObject radarFooterGo = new GameObject("RadarFooter", typeof(RectTransform), typeof(TextMeshProUGUI));
            radarFooterGo.transform.SetParent(radarScopeGo.transform, false);
            radarTargetCountText = radarFooterGo.GetComponent<TextMeshProUGUI>();
            radarTargetCountText.text = "TARGETS: [0] IN RANGE";
            radarTargetCountText.fontSize = 10;
            radarTargetCountText.alignment = TextAlignmentOptions.Center;
            radarTargetCountText.color = new Color(0.6f, 0.8f, 0.9f, 0.9f);
            RectTransform rftRt = radarFooterGo.GetComponent<RectTransform>();
            rftRt.anchorMin = new Vector2(0f, 0f);
            rftRt.anchorMax = new Vector2(1f, 0f);
            rftRt.pivot = new Vector2(0.5f, 0f);
            rftRt.anchoredPosition = new Vector2(0f, 6f);
            rftRt.sizeDelta = new Vector2(210f, 18f);

            // 5. Full-Width Bottom Controls & Pilot Status Bar
            GameObject bottomBar = new GameObject("BottomBar", typeof(RectTransform), typeof(Image));
            bottomBar.transform.SetParent(overlayRoot.transform, false);
            RectTransform botBarRt = bottomBar.GetComponent<RectTransform>();
            botBarRt.anchorMin = new Vector2(0f, 0f);
            botBarRt.anchorMax = new Vector2(1f, 0f);
            botBarRt.pivot = new Vector2(0.5f, 0f);
            botBarRt.anchoredPosition = Vector2.zero;
            botBarRt.sizeDelta = new Vector2(0f, 54f);
            Image botBarBg = bottomBar.GetComponent<Image>();
            botBarBg.color = new Color(0.05f, 0.09f, 0.13f, 0.90f);

            // Top accent border line for BottomBar
            GameObject botBorder = new GameObject("BorderLine", typeof(RectTransform), typeof(Image));
            botBorder.transform.SetParent(bottomBar.transform, false);
            RectTransform bbRt = botBorder.GetComponent<RectTransform>();
            bbRt.anchorMin = new Vector2(0f, 1f);
            bbRt.anchorMax = new Vector2(1f, 1f);
            bbRt.sizeDelta = new Vector2(0f, 2f);
            bbRt.anchoredPosition = Vector2.zero;
            Image bbImg = botBorder.GetComponent<Image>();
            bbImg.color = new Color(0.0f, 0.9f, 0.8f, 0.5f);

            // Left: Operator Body Status
            GameObject opGo = new GameObject("OperatorStatusText", typeof(RectTransform), typeof(TextMeshProUGUI));
            opGo.transform.SetParent(bottomBar.transform, false);
            operatorStatusText = opGo.GetComponent<TextMeshProUGUI>();
            operatorStatusText.text = "<color=#00FFCC>PILOT BODY: STATIONARY @ AIRLOCK CONSOLE [VULNERABLE]</color>";
            operatorStatusText.fontSize = 13;
            operatorStatusText.fontStyle = FontStyles.Bold;
            operatorStatusText.alignment = TextAlignmentOptions.Left;
            RectTransform opRt = opGo.GetComponent<RectTransform>();
            opRt.anchorMin = new Vector2(0f, 0.5f);
            opRt.anchorMax = new Vector2(0f, 0.5f);
            opRt.pivot = new Vector2(0f, 0.5f);
            opRt.anchoredPosition = new Vector2(35f, 0f);
            opRt.sizeDelta = new Vector2(550f, 30f);

            // Right: Flight Controls Prompt
            GameObject ctrlGo = new GameObject("ControlsText", typeof(RectTransform), typeof(TextMeshProUGUI));
            ctrlGo.transform.SetParent(bottomBar.transform, false);
            TextMeshProUGUI ctrlText = ctrlGo.GetComponent<TextMeshProUGUI>();
            ctrlText.text = "<b>[W A S D]</b>: Flight Vector  |  <b>[MOUSE]</b>: Steer Spotlight  |  <b><color=#FFAA00>[ESC]</color></b>: Disengage Feed";
            ctrlText.fontSize = 13;
            ctrlText.alignment = TextAlignmentOptions.Right;
            ctrlText.color = Color.white;
            RectTransform ctrlRt = ctrlGo.GetComponent<RectTransform>();
            ctrlRt.anchorMin = new Vector2(1f, 0.5f);
            ctrlRt.anchorMax = new Vector2(1f, 0.5f);
            ctrlRt.pivot = new Vector2(1f, 0.5f);
            ctrlRt.anchoredPosition = new Vector2(-35f, 0f);
            ctrlRt.sizeDelta = new Vector2(700f, 30f);

            // 6. Tactical Center Reticle (Disabled by default per user request)
            if (showCenterReticle)
            {
                CreateTacticalCenterReticle(overlayRoot.transform);
            }
        }

        private void RemoveExistingReticles()
        {
            Transform[] allChildren = GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < allChildren.Length; i++)
            {
                if (allChildren[i] != null && allChildren[i].name == "CenterReticle")
                {
                    DestroyImmediate(allChildren[i].gameObject);
                }
            }
        }

        private void CreateRadarRings(Transform parent)
        {
            float[] ringDiameters = new float[] { 54f, 108f, 162f };
            for (int i = 0; i < ringDiameters.Length; i++)
            {
                GameObject ring = new GameObject($"RangeRing_{i}", typeof(RectTransform), typeof(Image));
                ring.transform.SetParent(parent, false);
                RectTransform rRt = ring.GetComponent<RectTransform>();
                rRt.anchorMin = new Vector2(0.5f, 0.48f);
                rRt.anchorMax = new Vector2(0.5f, 0.48f);
                rRt.anchoredPosition = Vector2.zero;
                rRt.sizeDelta = new Vector2(ringDiameters[i], ringDiameters[i]);
                Image rImg = ring.GetComponent<Image>();
                rImg.color = new Color(0.12f, 0.35f, 0.32f, 0.35f);
            }
        }

        private void CreateCornerBrackets(Transform parent)
        {
            Vector2[] anchors = new Vector2[]
            {
                new Vector2(0f, 1f), // Top Left
                new Vector2(1f, 1f), // Top Right
                new Vector2(0f, 0f), // Bottom Left
                new Vector2(1f, 0f)  // Bottom Right
            };

            Vector2[] pivots = new Vector2[]
            {
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0f, 0f),
                new Vector2(1f, 0f)
            };

            Vector2[] offsets = new Vector2[]
            {
                new Vector2(25f, -80f),
                new Vector2(-25f, -80f),
                new Vector2(25f, 70f),
                new Vector2(-25f, 70f)
            };

            for (int i = 0; i < 4; i++)
            {
                GameObject bracket = new GameObject($"Bracket_{i}", typeof(RectTransform), typeof(Image));
                bracket.transform.SetParent(parent, false);
                RectTransform bRt = bracket.GetComponent<RectTransform>();
                bRt.anchorMin = anchors[i];
                bRt.anchorMax = anchors[i];
                bRt.pivot = pivots[i];
                bRt.anchoredPosition = offsets[i];
                bRt.sizeDelta = new Vector2(24f, 24f);
                Image bImg = bracket.GetComponent<Image>();
                bImg.color = new Color(0.0f, 0.85f, 0.75f, 0.35f);
                bImg.raycastTarget = false;
            }
        }

        private void CreateTacticalCenterReticle(Transform parent)
        {
            GameObject reticle = new GameObject("CenterReticle", typeof(RectTransform));
            reticle.transform.SetParent(parent, false);
            RectTransform retRt = reticle.GetComponent<RectTransform>();
            retRt.anchorMin = new Vector2(0.5f, 0.5f);
            retRt.anchorMax = new Vector2(0.5f, 0.5f);
            retRt.anchoredPosition = Vector2.zero;
            retRt.sizeDelta = new Vector2(40f, 40f);

            // 4 ticks
            float offset = 12f;
            float length = 8f;
            float thickness = 2f;

            // North
            CreateTick(reticle.transform, new Vector2(0f, offset + length / 2f), new Vector2(thickness, length));
            // South
            CreateTick(reticle.transform, new Vector2(0f, -(offset + length / 2f)), new Vector2(thickness, length));
            // West
            CreateTick(reticle.transform, new Vector2(-(offset + length / 2f), 0f), new Vector2(length, thickness));
            // East
            CreateTick(reticle.transform, new Vector2(offset + length / 2f, 0f), new Vector2(length, thickness));
        }

        private static void CreateTick(Transform parent, Vector2 pos, Vector2 size)
        {
            GameObject tick = new GameObject("Tick", typeof(RectTransform), typeof(Image));
            tick.transform.SetParent(parent, false);
            RectTransform tRt = tick.GetComponent<RectTransform>();
            tRt.anchoredPosition = pos;
            tRt.sizeDelta = size;
            Image tImg = tick.GetComponent<Image>();
            tImg.color = new Color(0.0f, 1f, 0.85f, 0.45f);
            tImg.raycastTarget = false;
        }
    }
}
