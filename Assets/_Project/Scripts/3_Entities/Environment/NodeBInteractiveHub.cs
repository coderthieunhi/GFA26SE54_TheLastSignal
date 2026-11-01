using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using TMPro;
using TheLastSignal.Core.Interaction;
using TheLastSignal.Entities.Player;

namespace TheLastSignal.Entities.Environment
{
    public enum NodeBPowerRoute
    {
        North_Airlock = 0,
        South_CargoHold = 1,
        West_MessHall = 2,
        East_VentShaft = 3,
        Blackout_Overload = 4
    }

    /// <summary>
    /// Zero-Sum Auxiliary Power Shunt Breaker Matrix at Node B Central Pedestal.
    /// Hardcore Survival Horror Mechanic:
    /// - Strictly ONLY 1 Cardinal Route can be powered at a time.
    /// - Powering one route forces the other 3 routes to lose power (corridor lights out, blast doors slam locked).
    /// - Requires holding [E] for 2.5s (freezing survivor in place, vulnerable to attack).
    /// - Switching emits mechanical grinding & high-voltage arc flashes that alert nearby monsters.
    /// - Limited fuse durability (3 switches max): Overload triggers a 15s facility Blackout!
    /// - The Monster can smash the matrix in 1.0s to trigger instant blackout and isolate survivors.
    /// </summary>
    [RequireComponent(typeof(Collider2D), typeof(AudioSource))]
    public class NodeBJunctionTerminal : InteractableBase
    {
        [Header("Zero-Sum State")]
        [SerializeField] private NodeBPowerRoute activeRoute = NodeBPowerRoute.North_Airlock;
        [SerializeField] private int maxFuseSwitches = 3;
        [SerializeField] private int remainingFuseSwitches = 3;
        [SerializeField] private float survivorChannelDuration = 2.5f;
        [SerializeField] private float monsterChannelDuration = 1.0f;
        [SerializeField] private float emergencyResetDuration = 5.0f;

        [Header("Door References")]
        [SerializeField] private DoorController doorNorth;
        [SerializeField] private DoorController doorSouth;
        [SerializeField] private DoorController doorWest;
        [SerializeField] private DoorController doorEast;

        [Header("Light References")]
        [SerializeField] private Light2D keySpotlight;
        [SerializeField] private Light2D coreIndicatorLight;
        [SerializeField] private List<Light2D> northCorridorLights = new List<Light2D>();
        [SerializeField] private List<Light2D> southCorridorLights = new List<Light2D>();
        [SerializeField] private List<Light2D> westCorridorLights = new List<Light2D>();
        [SerializeField] private List<Light2D> eastCorridorLights = new List<Light2D>();
        [SerializeField] private List<Light2D> redNeonStrips = new List<Light2D>();

        [Header("Audio Feedback")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip clankClip;
        [SerializeField] private AudioClip arcFlashClip;
        [SerializeField] private AudioClip smashClip;
        [SerializeField] private AudioClip alarmClip;
        [SerializeField] private AudioClip humClip;

        // Channeling Runtime State
        private PlayerController activeChanneler = null;
        private float channelProgress = 0f;
        private bool isChannelingAsMonster = false;
        private bool isChannelingReset = false;

        // In-world Progress Bar UI
        private GameObject progressCanvasGo;
        private Slider progressSlider;
        private Image progressFillImage;
        private TextMeshProUGUI progressText;

        public NodeBPowerRoute ActiveRoute => activeRoute;
        public int RemainingFuseSwitches => remainingFuseSwitches;
        public bool IsBlackout => activeRoute == NodeBPowerRoute.Blackout_Overload;
        public bool IsChanneling => activeChanneler != null;

        private void Awake()
        {
            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
            }

            if (audioSource != null)
            {
                audioSource.playOnAwake = false;
                audioSource.spatialBlend = 0.6f;
            }

            ResolveReferences();
            CreateProceduralAudioClips();
            EnsureProgressBarUI();
            ApplyPowerRoute(activeRoute, playAudio: false, alertMonster: false);
        }

        private void Start()
        {
            ResolveReferences();
            ApplyPowerRoute(activeRoute, playAudio: false, alertMonster: false);
        }

        private void Update()
        {
            HandleChannelingInput();
        }

        public override string GetInteractionPrompt()
        {
            if (IsBlackout)
            {
                return "[E] Giữ 5.0s: QUAY TAY KHỞI ĐỘNG LẠI CẦU CHÌ KHẨN CẤP";
            }

            NodeBPowerRoute nextRoute = GetNextRoute(activeRoute);
            string nextName = GetRouteDisplayName(nextRoute);

            return $"[E] Giữ 2.5s: Chuyển Nguồn -> {nextName} [Cầu chì: {remainingFuseSwitches}/{maxFuseSwitches}] | Quái vật [E]: Phá Hủy";
        }

        public override bool CanInteract(PlayerController player)
        {
            if (player == null) return false;
            if (activeChanneler != null && activeChanneler != player) return false;

            bool isMonster = player.IsMonster;

            if (IsBlackout)
            {
                // In blackout, only survivors can crank the manual reset
                if (isMonster) return false;
            }

            Collider2D col = GetComponent<Collider2D>();
            if (col != null)
            {
                Vector2 closest = col.ClosestPoint(player.transform.position);
                float distSqr = (closest - (Vector2)player.transform.position).sqrMagnitude;
                float maxAllowed = Mathf.Max(player.InteractionRange + 0.8f, 2.5f);
                if (distSqr > maxAllowed * maxAllowed) return false;
            }

            return true;
        }

        protected override void ProcessInteraction(PlayerController player)
        {
            if (activeChanneler != null) return;

            bool isMonster = player.IsMonster;
            bool isReset = IsBlackout;

            StartChanneling(player, isMonster, isReset);
        }

        private void StartChanneling(PlayerController player, bool asMonster, bool isReset)
        {
            activeChanneler = player;
            isChannelingAsMonster = asMonster;
            isChannelingReset = isReset;
            channelProgress = 0f;

            if (clankClip != null && audioSource != null)
            {
                audioSource.PlayOneShot(clankClip, 0.7f);
            }

            if (progressCanvasGo != null)
            {
                progressCanvasGo.SetActive(true);
                if (progressSlider != null) progressSlider.value = 0f;

                if (progressText != null)
                {
                    if (asMonster)
                    {
                        progressText.text = "ĐANG PHÁ HỦY CẦU DAO (0%)...";
                        progressText.color = new Color(1f, 0.25f, 0.15f);
                    }
                    else if (isReset)
                    {
                        progressText.text = "ĐANG QUAY TAY KHỞI ĐỘNG LẠI (0%)...";
                        progressText.color = new Color(1f, 0.85f, 0.2f);
                    }
                    else
                    {
                        NodeBPowerRoute next = GetNextRoute(activeRoute);
                        progressText.text = $"ĐANG GẠT CẦU CHÌ -> {GetRouteDisplayName(next)} (0%)...";
                        progressText.color = new Color(0.25f, 0.9f, 1f);
                    }
                }

                if (progressFillImage != null)
                {
                    progressFillImage.color = asMonster ? new Color(0.9f, 0.2f, 0.1f, 0.9f)
                        : (isReset ? new Color(1.0f, 0.8f, 0.2f, 0.9f) : new Color(0.2f, 0.85f, 1f, 0.9f));
                }
            }

            Debug.Log($"<color=yellow>[NodeBJunctionTerminal]</color> {(asMonster ? "Monster" : "Survivor")} started channeling.");
        }

        private void HandleChannelingInput()
        {
            if (activeChanneler == null) return;

            // 1. Proximity check
            Collider2D col = GetComponent<Collider2D>();
            Vector2 closest = col != null ? col.ClosestPoint(activeChanneler.transform.position) : (Vector2)transform.position;
            float distSqr = (closest - (Vector2)activeChanneler.transform.position).sqrMagnitude;
            float maxAllowedDist = Mathf.Max(activeChanneler.InteractionRange + 0.8f, 2.5f);

            if (distSqr > maxAllowedDist * maxAllowedDist)
            {
                CancelChanneling();
                return;
            }

            // 2. Key hold check (must hold [E])
            bool isHoldingE = false;
#if ENABLE_INPUT_SYSTEM
            if (UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.eKey.isPressed)
            {
                isHoldingE = true;
            }
#endif
            if (Input.GetKey(KeyCode.E)) isHoldingE = true;

            if (!isHoldingE)
            {
                CancelChanneling();
                return;
            }

            // 3. Progress computation
            float duration = isChannelingAsMonster ? monsterChannelDuration : (isChannelingReset ? emergencyResetDuration : survivorChannelDuration);
            channelProgress += Time.deltaTime;

            float pct = Mathf.Clamp01(channelProgress / duration);
            if (progressSlider != null) progressSlider.value = pct;

            if (progressText != null)
            {
                int p = Mathf.RoundToInt(pct * 100f);
                if (isChannelingAsMonster) progressText.text = $"ĐANG PHÁ HỦY CẦU DAO ({p}%)...";
                else if (isChannelingReset) progressText.text = $"ĐANG QUAY TAY KHỞI ĐỘNG LẠI ({p}%)...";
                else
                {
                    NodeBPowerRoute next = GetNextRoute(activeRoute);
                    progressText.text = $"ĐANG GẠT CẦU CHÌ -> {GetRouteDisplayName(next)} ({p}%)...";
                }
            }

            if (channelProgress >= duration)
            {
                CompleteChanneling();
            }
        }

        private void CompleteChanneling()
        {
            if (progressCanvasGo != null) progressCanvasGo.SetActive(false);

            if (isChannelingAsMonster)
            {
                // Monster destroys the matrix
                TriggerBlackout(isSabotage: true);
            }
            else if (isChannelingReset)
            {
                // Survivor restores emergency reserve
                ResetFromBlackout();
            }
            else
            {
                // Survivor switches route
                SwitchToNextRoute();
            }

            activeChanneler = null;
        }

        private void CancelChanneling()
        {
            if (progressCanvasGo != null) progressCanvasGo.SetActive(false);
            activeChanneler = null;
            channelProgress = 0f;
            Debug.Log("[NodeBJunctionTerminal] Channeling cancelled.");
        }

        private void SwitchToNextRoute()
        {
            remainingFuseSwitches--;

            if (remainingFuseSwitches <= 0)
            {
                // Fuse overload blowout!
                TriggerBlackout(isSabotage: false);
                return;
            }

            NodeBPowerRoute nextRoute = GetNextRoute(activeRoute);
            ApplyPowerRoute(nextRoute, playAudio: true, alertMonster: true);
        }

        private void TriggerBlackout(bool isSabotage)
        {
            activeRoute = NodeBPowerRoute.Blackout_Overload;
            remainingFuseSwitches = 0;

            if (isSabotage)
            {
                PlaySound(smashClip);
                Debug.Log("<color=red>[NodeBJunctionTerminal] MONSTER SABOTAGE! Breaker smashed, facility blackout!</color>");
            }
            else
            {
                PlaySound(arcFlashClip);
                Debug.Log("<color=red>[NodeBJunctionTerminal] FUSE OVERLOAD BLOWOUT! Matrix tripped, total blackout!</color>");
            }

            StartCoroutine(AlarmRoutine());
            StartCoroutine(ArcFlashSparksRoutine());
            ApplyPowerRoute(activeRoute, playAudio: false, alertMonster: true);
        }

        private void ResetFromBlackout()
        {
            remainingFuseSwitches = 2; // Limited emergency reserve
            activeRoute = NodeBPowerRoute.North_Airlock; // Default lifeline back north

            PlaySound(clankClip);
            ApplyPowerRoute(activeRoute, playAudio: true, alertMonster: false);
            Debug.Log("<color=green>[NodeBJunctionTerminal] Emergency power hand-cranked. Reserve route active: NORTH.</color>");
        }

        public void ApplyPowerRoute(NodeBPowerRoute route, bool playAudio, bool alertMonster)
        {
            activeRoute = route;

            if (doorNorth == null || northCorridorLights.Count == 0)
            {
                ResolveReferences();
            }

            // 1. Resolve Door States (Only active route door is OPEN; other 3 are SEALED CLOSED)
            if (doorNorth != null) doorNorth.SetState(route == NodeBPowerRoute.North_Airlock ? DoorState.Open : DoorState.Closed);
            if (doorSouth != null) doorSouth.SetState(route == NodeBPowerRoute.South_CargoHold ? DoorState.Open : DoorState.Closed);
            if (doorWest != null) doorWest.SetState(route == NodeBPowerRoute.West_MessHall ? DoorState.Open : DoorState.Closed);
            if (doorEast != null) doorEast.SetState(route == NodeBPowerRoute.East_VentShaft ? DoorState.Open : DoorState.Closed);

            // 2. Resolve Corridor Lights (Only active route is illuminated; other 3 are PITCH-BLACK)
            SetLightsActive(northCorridorLights, route == NodeBPowerRoute.North_Airlock);
            SetLightsActive(southCorridorLights, route == NodeBPowerRoute.South_CargoHold);
            SetLightsActive(westCorridorLights, route == NodeBPowerRoute.West_MessHall);
            SetLightsActive(eastCorridorLights, route == NodeBPowerRoute.East_VentShaft);

            // 3. Central Console Visuals
            if (coreIndicatorLight != null)
            {
                switch (route)
                {
                    case NodeBPowerRoute.North_Airlock:
                        coreIndicatorLight.color = new Color(0.15f, 0.90f, 1.0f, 1f); // Cyan
                        coreIndicatorLight.intensity = 0.75f;
                        break;
                    case NodeBPowerRoute.South_CargoHold:
                        coreIndicatorLight.color = new Color(1.0f, 0.65f, 0.15f, 1f); // Amber / Logistics
                        coreIndicatorLight.intensity = 0.75f;
                        break;
                    case NodeBPowerRoute.West_MessHall:
                        coreIndicatorLight.color = new Color(1.0f, 0.85f, 0.35f, 1f); // Warm Gold
                        coreIndicatorLight.intensity = 0.75f;
                        break;
                    case NodeBPowerRoute.East_VentShaft:
                        coreIndicatorLight.color = new Color(0.20f, 0.95f, 0.75f, 1f); // Emerald Teal
                        coreIndicatorLight.intensity = 0.75f;
                        break;
                    case NodeBPowerRoute.Blackout_Overload:
                        coreIndicatorLight.color = new Color(1.0f, 0.15f, 0.10f, 1f); // Warning Crimson
                        coreIndicatorLight.intensity = 1.1f;
                        break;
                }
            }

            if (keySpotlight != null)
            {
                if (route == NodeBPowerRoute.Blackout_Overload)
                {
                    keySpotlight.intensity = 0.0f; // Spotlight dies in blackout!
                }
                else
                {
                    keySpotlight.intensity = (remainingFuseSwitches <= 1) ? 1.0f : 1.75f;
                }
            }

            // 4. Audio Feedback
            if (playAudio)
            {
                PlaySound(arcFlashClip);
                if (humClip != null && audioSource != null) audioSource.PlayOneShot(humClip, 0.5f);
            }

            // 5. Monster Alert (Noise detection)
            if (alertMonster)
            {
                AlertNearbyMonsters();
            }

            Debug.Log($"<color=cyan>[NodeBJunctionTerminal]</color> Shunted power to: {route}. Doors & lights updated across all 4 corridors.");
        }

        private void SetLightsActive(List<Light2D> lights, bool active)
        {
            if (lights == null) return;
            foreach (var l in lights)
            {
                if (l != null) l.enabled = active;
            }
        }

        private void AlertNearbyMonsters()
        {
            // Loud arc flash & mechanical noise informs nearby monsters of survivor activity
            Debug.LogWarning("[NodeBJunctionTerminal] Breaker arc flash & noise emitted at Node B!");
        }

        private IEnumerator AlarmRoutine()
        {
            if (alarmClip == null || audioSource == null) yield break;
            audioSource.PlayOneShot(alarmClip, 0.8f);
            yield return new WaitForSeconds(3.0f);
        }

        private IEnumerator ArcFlashSparksRoutine()
        {
            if (keySpotlight == null) yield break;
            for (int i = 0; i < 4; i++)
            {
                keySpotlight.intensity = 2.8f;
                keySpotlight.color = new Color(1f, 0.95f, 0.5f, 1f);
                yield return new WaitForSeconds(0.06f);
                keySpotlight.intensity = 0.1f;
                keySpotlight.color = new Color(1f, 0.1f, 0.1f, 1f);
                yield return new WaitForSeconds(0.08f);
            }
            keySpotlight.intensity = 0.0f;
        }

        private NodeBPowerRoute GetNextRoute(NodeBPowerRoute current)
        {
            switch (current)
            {
                case NodeBPowerRoute.North_Airlock: return NodeBPowerRoute.South_CargoHold;
                case NodeBPowerRoute.South_CargoHold: return NodeBPowerRoute.West_MessHall;
                case NodeBPowerRoute.West_MessHall: return NodeBPowerRoute.East_VentShaft;
                case NodeBPowerRoute.East_VentShaft: return NodeBPowerRoute.North_Airlock;
                default: return NodeBPowerRoute.North_Airlock;
            }
        }

        private string GetRouteDisplayName(NodeBPowerRoute route)
        {
            switch (route)
            {
                case NodeBPowerRoute.North_Airlock: return "BẮC (Airlock)";
                case NodeBPowerRoute.South_CargoHold: return "NAM (Cargo Hold)";
                case NodeBPowerRoute.West_MessHall: return "TÂY (Mess Hall)";
                case NodeBPowerRoute.East_VentShaft: return "ĐÔNG (Vent Shaft)";
                case NodeBPowerRoute.Blackout_Overload: return "MẤT ĐIỆN TOÀN KHU";
                default: return route.ToString();
            }
        }

        private void PlaySound(AudioClip clip)
        {
            if (audioSource != null && clip != null)
            {
                audioSource.PlayOneShot(clip, 0.75f);
            }
        }

        private void ResolveReferences()
        {
            // Resolve 4 Doors
            if (doorNorth == null) doorNorth = GameObject.Find("Door_Airlock_South")?.GetComponent<DoorController>();
            if (doorSouth == null) doorSouth = GameObject.Find("Door_CargoHold_North")?.GetComponent<DoorController>();
            if (doorWest == null) doorWest = GameObject.Find("Door_MessHall")?.GetComponent<DoorController>();
            if (doorEast == null) doorEast = GameObject.Find("Door_VentShaft")?.GetComponent<DoorController>();

            // Resolve Central Lights
            if (keySpotlight == null) keySpotlight = GameObject.Find("Light_NodeB_Junction_KeySpot")?.GetComponent<Light2D>();
            if (coreIndicatorLight == null) coreIndicatorLight = GameObject.Find("Light_NodeB_Junction_CoreLED")?.GetComponent<Light2D>();

            // Resolve Corridor Lights
            CollectLights(northCorridorLights, "Light_Corridor_N_1", "Light_Corridor_N_2", "Light_Door_N_L", "Light_Door_N_R", "Light_Sign_Airlock");
            CollectLights(southCorridorLights, "Light_Corridor_S_1", "Light_Corridor_S_2", "Light_Door_S_L", "Light_Door_S_R", "Light_Sign_CargoHold");
            CollectLights(westCorridorLights, "Light_Corridor_W_1", "Light_Corridor_W_2", "Light_Door_W_T", "Light_Door_W_B", "Light_Sign_MessHall");
            CollectLights(eastCorridorLights, "Light_Corridor_E_1", "Light_Corridor_E_2", "Light_Door_E_T", "Light_Door_E_B", "Light_Sign_VentShaft");

            // Resolve Red Neon Strips
            redNeonStrips.Clear();
            var lightingRoot = GameObject.Find("Lighting");
            if (lightingRoot != null)
            {
                for (int i = 0; i < lightingRoot.transform.childCount; i++)
                {
                    var child = lightingRoot.transform.GetChild(i);
                    if (child.name.StartsWith("Light_NodeB_RedNeonStrip_"))
                    {
                        var l = child.GetComponent<Light2D>();
                        if (l != null) redNeonStrips.Add(l);
                    }
                }
            }
        }

        private void CollectLights(List<Light2D> targetList, params string[] names)
        {
            targetList.Clear();
            foreach (var n in names)
            {
                var l = GameObject.Find(n)?.GetComponent<Light2D>();
                if (l != null) targetList.Add(l);
            }
        }

        private void EnsureProgressBarUI()
        {
            if (progressCanvasGo != null) return;

            Transform existing = transform.Find("Progress_Canvas");
            if (existing != null)
            {
                progressCanvasGo = existing.gameObject;
                progressSlider = progressCanvasGo.GetComponentInChildren<Slider>();
                progressText = progressCanvasGo.GetComponentInChildren<TextMeshProUGUI>();
                progressFillImage = progressSlider != null && progressSlider.fillRect != null ? progressSlider.fillRect.GetComponent<Image>() : null;
                progressCanvasGo.SetActive(false);
                return;
            }

            // Create Canvas
            progressCanvasGo = new GameObject("Progress_Canvas");
            progressCanvasGo.transform.SetParent(transform, false);
            progressCanvasGo.transform.localPosition = new Vector3(0f, 1.25f, 0f);

            Canvas canvas = progressCanvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingLayerName = "Obstacles";
            canvas.sortingOrder = 25;

            RectTransform rtCanvas = progressCanvasGo.GetComponent<RectTransform>();
            rtCanvas.sizeDelta = new Vector2(240, 48);
            rtCanvas.localScale = new Vector3(0.012f, 0.012f, 1f);

            // Background Slider
            GameObject sliderGo = new GameObject("Slider");
            sliderGo.transform.SetParent(progressCanvasGo.transform, false);
            RectTransform rtSlider = sliderGo.AddComponent<RectTransform>();
            rtSlider.sizeDelta = new Vector2(220, 18);
            rtSlider.anchoredPosition = new Vector2(0, -8);

            progressSlider = sliderGo.AddComponent<Slider>();
            progressSlider.minValue = 0f;
            progressSlider.maxValue = 1f;
            progressSlider.value = 0f;

            Image bgImg = sliderGo.AddComponent<Image>();
            bgImg.color = new Color(0.1f, 0.12f, 0.15f, 0.85f);

            // Fill Area
            GameObject fillArea = new GameObject("Fill Area");
            fillArea.transform.SetParent(sliderGo.transform, false);
            RectTransform rtFillArea = fillArea.AddComponent<RectTransform>();
            rtFillArea.sizeDelta = new Vector2(216, 14);

            GameObject fill = new GameObject("Fill");
            fill.transform.SetParent(fillArea.transform, false);
            RectTransform rtFill = fill.AddComponent<RectTransform>();
            rtFill.sizeDelta = new Vector2(0, 0);

            progressFillImage = fill.AddComponent<Image>();
            progressFillImage.color = new Color(0.2f, 0.85f, 1f, 0.95f);
            progressSlider.fillRect = rtFill;

            // Action Text
            GameObject textGo = new GameObject("Text_Action");
            textGo.transform.SetParent(progressCanvasGo.transform, false);
            RectTransform rtText = textGo.AddComponent<RectTransform>();
            rtText.sizeDelta = new Vector2(240, 24);
            rtText.anchoredPosition = new Vector2(0, 16);

            progressText = textGo.AddComponent<TextMeshProUGUI>();
            progressText.text = "ĐANG GẠT CẦU CHÌ CHUYỂN MẠCH...";
            progressText.fontSize = 14;
            progressText.fontStyle = FontStyles.Bold;
            progressText.alignment = TextAlignmentOptions.Center;
            progressText.color = new Color(0.3f, 0.9f, 1f, 1f);

            progressCanvasGo.SetActive(false);
        }

        private void CreateProceduralAudioClips()
        {
            if (clankClip == null) clankClip = GenerateToneClip("BreakerClank", 180, 0.25f, isPercussive: true);
            if (arcFlashClip == null) arcFlashClip = GenerateToneClip("ArcFlash", 360, 0.40f, isPercussive: true);
            if (smashClip == null) smashClip = GenerateToneClip("BreakerSmash", 90, 0.45f, isPercussive: true);
            if (alarmClip == null) alarmClip = GenerateToneClip("AlarmBuzzer", 520, 0.8f, isPercussive: false);
            if (humClip == null) humClip = GenerateToneClip("PowerHum", 60, 1.0f, isPercussive: false);
        }

        private static AudioClip GenerateToneClip(string name, float freq, float length, bool isPercussive)
        {
            int sampleRate = 44100;
            int samples = (int)(sampleRate * length);
            float[] data = new float[samples];

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / sampleRate;
                float envelope = isPercussive ? Mathf.Exp(-t * 8f) : 1f;
                float wave = Mathf.Sin(2f * Mathf.PI * freq * t) * 0.7f + Mathf.Sin(4f * Mathf.PI * freq * t) * 0.3f;
                if (isPercussive) wave += (UnityEngine.Random.value * 2f - 1f) * 0.25f;
                data[i] = wave * envelope * 0.4f;
            }

            AudioClip clip = AudioClip.Create(name, samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }

    /// <summary>
    /// NW Tactical Command lore console at Node B.
    /// Provides station surveillance status and audio-visual incident logs.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class NodeBCommandLoreTerminal : InteractableBase
    {
        [Header("Lore Logs")]
        [TextArea(2, 5)]
        [SerializeField] private string[] logs = new string[]
        {
            "NHẬT KÝ CA TRỰC #042: Cảnh báo áp suất đường ống làm mát Vent Shaft phía Đông sụt giảm bất thường. Đã phát hiện vết cào xé kim loại tại ngã tư Node B...",
            "CẢNH BÁO KHẨN: Cửa khoang Cargo Hold phía Bắc đã bị cạy phá từ bên trong. Mọi nhân viên sơ tán về phía Airlock ngay lập tức...",
            "TRUYỀN TIN CUỐI: 'Hệ thống radar phát hiện chuyển động trong đường ống kỹ thuật. Đừng lại gần cửa thông gió khi đèn báo chuyển đỏ!'"
        };

        private int currentLogIndex = 0;

        [Header("Visual Feedback")]
        [SerializeField] private Light2D screenGlow;

        private void Awake()
        {
            if (screenGlow == null)
            {
                var glowGo = GameObject.Find("Light_NodeB_Command_CRT_Glow");
                if (glowGo != null) screenGlow = glowGo.GetComponent<Light2D>();
            }

            UpdatePrompt();
        }

        public override string GetInteractionPrompt()
        {
            return promptMessage;
        }

        protected override void ProcessInteraction(PlayerController player)
        {
            if (logs != null && logs.Length > 0)
            {
                string log = logs[currentLogIndex];
                Debug.Log($"<color=yellow>[Terminal Lệnh Node B]</color> (Mục {currentLogIndex + 1}/{logs.Length}):\n\"{log}\"");
                currentLogIndex = (currentLogIndex + 1) % logs.Length;
                UpdatePrompt();

                if (screenGlow != null)
                {
                    screenGlow.intensity = 0.95f;
                }
            }
        }

        protected override void OnInteractionFailed(PlayerController player)
        {
            Debug.LogWarning($"[NodeBCommandLoreTerminal] {player.name} không thể đọc nhật ký lệnh.");
        }

        private void UpdatePrompt()
        {
            int total = logs != null ? logs.Length : 0;
            promptMessage = $"[E] Nhật Ký Chỉ Huy Trạm ({currentLogIndex + 1}/{total})";
        }
    }

    /// <summary>
    /// Searchable South Storage Locker container at Node B.
    /// Provides emergency scrap, filters, and field supplies on inspection.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class NodeBLockerContainer : InteractableBase
    {
        [Header("Locker Settings")]
        [SerializeField] private string lockerTitle = "Tủ Vật Tư Kỹ Thuật";
        [SerializeField] private string lootFoundDescription = "Tìm thấy: Pin Năng Lượng Dự Phòng & Băng Gạc Vô Trùng.";
        [SerializeField] private bool isSearched = false;

        public bool IsSearched => isSearched;

        private void Awake()
        {
            UpdatePrompt();
        }

        public override string GetInteractionPrompt()
        {
            return promptMessage;
        }

        protected override void ProcessInteraction(PlayerController player)
        {
            if (!isSearched)
            {
                isSearched = true;
                Debug.Log($"<color=green>[Locker]</color> {player.name} mở {lockerTitle}: {lootFoundDescription}");
            }
            else
            {
                Debug.Log($"[Locker] {lockerTitle} đã được lục soát trước đó, không còn vật phẩm hữu dụng.");
            }

            UpdatePrompt();
        }

        protected override void OnInteractionFailed(PlayerController player)
        {
            Debug.LogWarning($"[NodeBLockerContainer] {player.name} không thể mở {lockerTitle}.");
        }

        public void ConfigureLocker(string title, string lootDesc)
        {
            lockerTitle = title;
            lootFoundDescription = lootDesc;
            UpdatePrompt();
        }

        private void UpdatePrompt()
        {
            if (!isSearched)
            {
                promptMessage = $"[E] Lục Soát {lockerTitle}";
            }
            else
            {
                promptMessage = $"[E] {lockerTitle} (Đã Lục Soát - Trống)";
            }
        }
    }

    /// <summary>
    /// Interactive dropped technician datapad with glowing cracked screen.
    /// Environmental storytelling lore carrier dropped in blood trail.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class NodeBTechDatapad : InteractableBase
    {
        [Header("Datapad Lore")]
        [TextArea(2, 4)]
        [SerializeField] private string transmission = "GHI CHÚ CHƯA GỬI: '...Nó không phải là hỏng hóc kỹ thuật thông thường. Nó đang săn chúng tôi. Tôi đã ngắt mạch trạm Junction để làm chậm bước chân của nó... Chúa phù hộ chúng ta.'";

        private void Awake()
        {
            promptMessage = "[E] Kiểm Tra Datapad Rơi";
        }

        public override string GetInteractionPrompt()
        {
            return promptMessage;
        }

        protected override void ProcessInteraction(PlayerController player)
        {
            Debug.Log($"<color=red>[Datapad Kỹ Thuật Viên]</color> {player.name} kiểm tra màn hình vỡ:\n\"{transmission}\"");
            promptMessage = "[E] Datapad Rơi (Đã Đọc)";
        }

        protected override void OnInteractionFailed(PlayerController player)
        {
            Debug.LogWarning($"[NodeBTechDatapad] {player.name} không thể với tới datapad.");
        }
    }
}
