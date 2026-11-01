using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using TheLastSignal.Core.Interaction;
using TheLastSignal.Entities.Player;
using TheLastSignal.UI;

namespace TheLastSignal.Entities.Environment
{
    public enum RoomSector
    {
        North_CrewQuarters = 0,
        West_MedBay = 1,
        East_ConsoleAlpha = 2,
        South_Airlock = 3
    }

    [System.Serializable]
    public class SectorData
    {
        public RoomSector sector;
        public string displayName;
        public string sectorCode;
        public Vector3 cameraPosition;
        public float cameraOrthoSize = 5f;
        public Bounds sectorBounds;
        public GameObject roomRoot;
        public List<Light2D> roomLights = new List<Light2D>();
    }

    /// <summary>
    /// Interactive Power & Room Inspection Console at Node A center.
    /// Allows the player to route auxiliary power to inspect one room at a time,
    /// viewing a live surveillance feed and biometric/monster threat scan.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class RoomInspectionConsole : InteractableBase
    {
        [Header("Terminal References")]
        [SerializeField] private RoomInspectionTerminalUI terminalUI;
        [SerializeField] private UnityEngine.Camera surveillanceCamera;
        [SerializeField] private RenderTexture renderTexture;

        [Header("Visual Feedback")]
        [SerializeField] private SpriteRenderer consoleRenderer;
        [SerializeField] private Light2D consolePointLight;

        [Header("Sectors Setup")]
        [SerializeField] private List<SectorData> sectors = new List<SectorData>();

        private PlayerController activePlayer;
        private RoomSector currentSector = RoomSector.North_CrewQuarters;
        private bool isTerminalOpen = false;
        private Dictionary<Light2D, float> defaultLightIntensities = new Dictionary<Light2D, float>();

        public bool IsTerminalOpen => isTerminalOpen;
        public RoomSector CurrentSector => currentSector;

        private void Awake()
        {
            if (consoleRenderer == null)
            {
                consoleRenderer = GetComponent<SpriteRenderer>();
            }

            if (consolePointLight == null)
            {
                consolePointLight = GetComponentInChildren<Light2D>();
            }

            promptMessage = "[E] Bảng Điều Khiển Lưới Điện & Giám Sát";

            InitializeSectors();
        }

        private void Start()
        {
            EnsureSurveillanceSystem();
        }

        private void Update()
        {
            if (!isTerminalOpen) return;

            // Handle keyboard shortcuts while terminal is open
            if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha1) || UnityEngine.Input.GetKeyDown(KeyCode.Keypad1))
            {
                InspectSector(RoomSector.North_CrewQuarters);
            }
            else if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha2) || UnityEngine.Input.GetKeyDown(KeyCode.Keypad2))
            {
                InspectSector(RoomSector.West_MedBay);
            }
            else if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha3) || UnityEngine.Input.GetKeyDown(KeyCode.Keypad3))
            {
                InspectSector(RoomSector.East_ConsoleAlpha);
            }
            else if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha4) || UnityEngine.Input.GetKeyDown(KeyCode.Keypad4))
            {
                InspectSector(RoomSector.South_Airlock);
            }
            else if (UnityEngine.Input.GetKeyDown(KeyCode.Escape) || UnityEngine.Input.GetKeyDown(KeyCode.E))
            {
                CloseTerminal();
            }
        }

        public void InitializeSectors()
        {
            if (sectors != null && sectors.Count == 4) return;

            sectors = new List<SectorData>
            {
                new SectorData
                {
                    sector = RoomSector.North_CrewQuarters,
                    displayName = "Khu Thủy Thủ (Crew Quarters)",
                    sectorCode = "SECTOR 01 - NORTH",
                    cameraPosition = new Vector3(0.0f, 43.0f, -10f),
                    cameraOrthoSize = 6.2f,
                    sectorBounds = new Bounds(new Vector3(0.0f, 43.0f, 0f), new Vector3(13f, 13f, 10f)),
                    roomRoot = GameObject.Find("CrewQuarters")
                },
                new SectorData
                {
                    sector = RoomSector.West_MedBay,
                    displayName = "Phòng Y Tế (Med Bay)",
                    sectorCode = "SECTOR 02 - WEST",
                    cameraPosition = new Vector3(-15.5f, 28.0f, -10f),
                    cameraOrthoSize = 4.8f,
                    sectorBounds = new Bounds(new Vector3(-15.5f, 28.0f, 0f), new Vector3(12f, 10f, 10f)),
                    roomRoot = GameObject.Find("MedBay_Decorations")
                },
                new SectorData
                {
                    sector = RoomSector.East_ConsoleAlpha,
                    displayName = "Phòng Điều Khiển (Console Alpha)",
                    sectorCode = "SECTOR 03 - EAST",
                    cameraPosition = new Vector3(15.5f, 28.0f, -10f),
                    cameraOrthoSize = 5.2f,
                    sectorBounds = new Bounds(new Vector3(15.5f, 28.0f, 0f), new Vector3(12f, 11f, 10f)),
                    roomRoot = GameObject.Find("ConsoleAlpha_Decorations")
                },
                new SectorData
                {
                    sector = RoomSector.South_Airlock,
                    displayName = "Khoang Khí Áp (Air Lock)",
                    sectorCode = "SECTOR 04 - SOUTH",
                    cameraPosition = new Vector3(0.0f, 13.0f, -10f),
                    cameraOrthoSize = 5.2f,
                    sectorBounds = new Bounds(new Vector3(0.0f, 13.0f, 0f), new Vector3(11f, 11f, 10f)),
                    roomRoot = GameObject.Find("Airlock_Decorations")
                }
            };

            // Collect lights and store initial intensities
            defaultLightIntensities.Clear();
            foreach (var sec in sectors)
            {
                if (sec.roomRoot != null)
                {
                    sec.roomLights.Clear();
                    var l2ds = sec.roomRoot.GetComponentsInChildren<Light2D>(true);
                    foreach (var l in l2ds)
                    {
                        if (l != null && !l.name.StartsWith("Indicator_"))
                        {
                            sec.roomLights.Add(l);
                            if (!defaultLightIntensities.ContainsKey(l))
                            {
                                defaultLightIntensities[l] = l.intensity;
                            }
                        }
                    }
                }
            }
        }

        public void EnsureSurveillanceSystem()
        {
            if (renderTexture == null)
            {
                renderTexture = new RenderTexture(640, 480, 16, RenderTextureFormat.ARGB32);
                renderTexture.name = "RT_Surveillance_Terminal";
                renderTexture.filterMode = FilterMode.Point;
                renderTexture.Create();
            }

            if (surveillanceCamera == null)
            {
                GameObject camGo = GameObject.Find("Surveillance_Camera_NodeA");
                if (camGo == null)
                {
                    camGo = new GameObject("Surveillance_Camera_NodeA");
                    camGo.transform.position = new Vector3(0.0f, 0.0f, -10f);
                }
                surveillanceCamera = camGo.GetComponent<UnityEngine.Camera>();
                if (surveillanceCamera == null)
                {
                    surveillanceCamera = camGo.AddComponent<UnityEngine.Camera>();
                }

                surveillanceCamera.orthographic = true;
                surveillanceCamera.targetTexture = renderTexture;
                surveillanceCamera.clearFlags = CameraClearFlags.SolidColor;
                surveillanceCamera.backgroundColor = new Color(0.02f, 0.03f, 0.04f, 1f);
                surveillanceCamera.depth = -5;
                surveillanceCamera.enabled = false; // Only renders when terminal is open
            }

            if (terminalUI == null)
            {
                terminalUI = FindAnyObjectByType<RoomInspectionTerminalUI>();
            }
        }

        public override bool CanInteract(PlayerController player)
        {
            if (isTerminalOpen) return false;
            return base.CanInteract(player);
        }

        protected override void ProcessInteraction(PlayerController player)
        {
            activePlayer = player;
            OpenTerminal();
        }

        public void OpenTerminal()
        {
            EnsureSurveillanceSystem();

            isTerminalOpen = true;

            // Freeze player controls while operating terminal
            if (activePlayer != null)
            {
                activePlayer.SetControlEnabled(false);
            }

            // Enable camera
            if (surveillanceCamera != null)
            {
                surveillanceCamera.enabled = true;
            }

            // Bind and show UI
            if (terminalUI != null)
            {
                terminalUI.Initialize(this, renderTexture);
                terminalUI.Show();
            }

            // Inspect the default room
            InspectSector(currentSector);

            Debug.Log("[RoomInspectionConsole] Terminal opened by player.");
        }

        public void CloseTerminal()
        {
            if (!isTerminalOpen) return;

            isTerminalOpen = false;

            // Disable camera
            if (surveillanceCamera != null)
            {
                surveillanceCamera.enabled = false;
            }

            // Restore all room lights to standby
            RestoreAllRoomLights();

            // Hide UI
            if (terminalUI != null)
            {
                terminalUI.Hide();
            }

            // Restore player controls
            if (activePlayer != null)
            {
                activePlayer.SetControlEnabled(true);
                activePlayer = null;
            }

            Debug.Log("[RoomInspectionConsole] Terminal closed. Resumed normal gameplay.");
        }

        public void InspectSector(RoomSector sector)
        {
            currentSector = sector;
            SectorData data = sectors.Find(s => s.sector == sector);
            if (data == null) return;

            // 1. Move camera to sector center
            if (surveillanceCamera != null)
            {
                surveillanceCamera.transform.position = data.cameraPosition;
                surveillanceCamera.orthographicSize = data.cameraOrthoSize;
            }

            // 2. Power and illuminate the selected room, dim others
            ApplySectorLighting(sector);

            // 3. Scan for entities in this sector
            bool monsterDetected = false;
            string monsterInfo = "";
            int crewCount = 0;

            ScanEntitiesInSector(data.sectorBounds, out monsterDetected, out monsterInfo, out crewCount);

            // 4. Update UI diagnostic feed
            if (terminalUI != null)
            {
                terminalUI.UpdateFeed(data, monsterDetected, monsterInfo, crewCount);
            }
        }

        private void ApplySectorLighting(RoomSector activeSector)
        {
            foreach (var sec in sectors)
            {
                bool isActive = (sec.sector == activeSector);
                foreach (var l in sec.roomLights)
                {
                    if (l == null) continue;

                    float baseIntensity = defaultLightIntensities.ContainsKey(l) ? defaultLightIntensities[l] : 1f;

                    if (isActive)
                    {
                        // Turn on or boost light for active inspected room
                        l.enabled = true;
                        l.intensity = Mathf.Max(baseIntensity * 1.35f, 1.2f);
                    }
                    else
                    {
                        // Keep other rooms in standby / dim
                        l.intensity = baseIntensity * 0.4f;
                    }
                }
            }
        }

        private void RestoreAllRoomLights()
        {
            foreach (var sec in sectors)
            {
                foreach (var l in sec.roomLights)
                {
                    if (l == null) continue;
                    if (defaultLightIntensities.ContainsKey(l))
                    {
                        l.intensity = defaultLightIntensities[l];
                    }
                }
            }
        }

        private void ScanEntitiesInSector(Bounds bounds, out bool monsterDetected, out string monsterInfo, out int crewCount)
        {
            monsterDetected = false;
            monsterInfo = "";
            crewCount = 0;

            // Detect players
            var players = FindObjectsByType<PlayerController>(FindObjectsInactive.Exclude);
            foreach (var p in players)
            {
                if (p != null && bounds.Contains(p.transform.position))
                {
                    crewCount++;
                }
            }

            // Detect monster / hostile entities
            // Check by MonsterView, tags, or name
            var allRenderers = FindObjectsByType<SpriteRenderer>(FindObjectsInactive.Exclude);
            foreach (var r in allRenderers)
            {
                if (r == null || !r.gameObject.activeInHierarchy) continue;

                Vector3 pos = r.transform.position;
                if (!bounds.Contains(pos)) continue;

                string lowerName = r.gameObject.name.ToLower();
                bool isMonster = lowerName.Contains("monster") || lowerName.Contains("shade") || 
                                lowerName.Contains("creature") || r.gameObject.CompareTag("Monster");

                if (!isMonster)
                {
                    var mv = r.GetComponentInParent<TheLastSignal.Entities.Monster.MonsterView>();
                    if (mv != null) isMonster = true;
                }

                if (isMonster)
                {
                    monsterDetected = true;
                    monsterInfo = r.gameObject.name;
                    break;
                }
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.35f);
            foreach (var s in sectors)
            {
                Gizmos.DrawWireCube(s.sectorBounds.center, s.sectorBounds.size);
            }
        }
    }
}
