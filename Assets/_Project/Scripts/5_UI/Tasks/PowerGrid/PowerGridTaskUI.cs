using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using TMPro;
using TheLastSignal.Entities.Player;
using TheLastSignal.Entities.Environment;

namespace TheLastSignal.UI.Tasks
{
    /// <summary>
    /// Sci-Fi Brutalist 4x4 High-Voltage Power Grid Flow Minigame.
    /// Strategic time-stalling puzzle featuring:
    /// - 4x4 rotating conduit matrix (Straight, Corner, Tee, Cross, Overload nodes)
    /// - Live BFS electrical flow propagation with cyan/gold emission
    /// - Sensitive overload capacitors that trip circuit breaker with dynamic cooling & fault recovery
    /// - Deterministic path solver ensuring guaranteed 100% solvability with serpentine depth (8-11 steps)
    /// - Dual-Bay Brutalist UI chassis matching TerminalBezel visual standards
    /// - Full mouse (L-click CW, R-click CCW) and New Input System keyboard navigation
    /// - Completely self-contained procedural PCM audio synthesis
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class PowerGridTaskUI : MonoBehaviour
    {
        private static PowerGridTaskUI _instance;
        public static PowerGridTaskUI Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = UnityEngine.Object.FindAnyObjectByType<PowerGridTaskUI>(FindObjectsInactive.Include);
                }
                return _instance;
            }
            private set => _instance = value;
        }

        #region Enums & Structs

        public enum ConduitType
        {
            Straight,   // Connects 2 opposite sides (N-S or E-W)
            Corner,     // Connects 2 adjacent sides (90° bend)
            Tee,        // Connects 3 sides (3-way junction)
            Cross,      // Connects all 4 sides (4-way junction)
            Overload    // Faulty coil: If energized, trips the main breaker!
        }

        [Flags]
        public enum PortFlags
        {
            None  = 0,
            North = 1 << 0, // 1
            East  = 1 << 1, // 2
            South = 1 << 2, // 4
            West  = 1 << 3  // 8
        }

        public class GridCell
        {
            public int row;
            public int col;
            public ConduitType conduitType;
            public PortFlags basePorts;
            public int rotationIndex; // 0 = 0°, 1 = 90°, 2 = 180°, 3 = 270°
            public bool isEnergized;
            public bool isOverload;

            // UI Elements
            public GameObject rootGo;
            public RectTransform rotatorRect;
            public Image bgImage;
            public Outline bgOutline;
            public GameObject selectionReticleGo;
            public Image centerHub;
            public Image armNorth;
            public Image armEast;
            public Image armSouth;
            public Image armWest;
            public Image armNorthGlow;
            public Image armEastGlow;
            public Image armSouthGlow;
            public Image armWestGlow;
            public Image centerHubGlow;
            public GameObject overloadBadgeGo;
            public TextMeshProUGUI overloadIcon;

            public PortFlags CurrentPorts => GetRotatedPorts(basePorts, rotationIndex);
        }

        #endregion

        #region Serialized UI References

        [Header("UI Panels")]
        [SerializeField] private GameObject rootPanel;
        [SerializeField] private RectTransform terminalBox;
        [SerializeField] private TextMeshProUGUI headerText;
        [SerializeField] private TextMeshProUGUI instructionText;
        [SerializeField] private Button closeButton;

        [Header("Grid Bay")]
        [SerializeField] private RectTransform gridBay;
        [SerializeField] private RectTransform gridContainer;
        [SerializeField] private Image inputTerminalBadge;
        [SerializeField] private Image outputTerminalBadge;

        [Header("Telemetry Bay")]
        [SerializeField] private TextMeshProUGUI circuitStateText;
        [SerializeField] private TextMeshProUGUI busPotentialText;
        [SerializeField] private TextMeshProUGUI nodesEnergizedText;
        [SerializeField] private TextMeshProUGUI efficiencyText;
        [SerializeField] private TextMeshProUGUI breakerStatusText;
        [SerializeField] private Image breakerCooldownFill;
        [SerializeField] private TextMeshProUGUI breakerCooldownText;

        [Header("Audio")]
        [SerializeField] private AudioSource audioSource;

        #endregion

        #region Internal State

        private const int GridRows = 4;
        private const int GridCols = 4;
        private GridCell[,] grid = new GridCell[GridRows, GridCols];

        private PlayerController activePlayer;
        private TaskLocation activeTaskLocation;

        private int selectedRow = 0;
        private int selectedCol = 0;
        private bool isFinished = false;

        // Breaker Trip & Cooling System
        private bool isBreakerTripped = false;
        private float tripCooldownTimer = 0f;
        private const float TripCooldownDuration = 1.5f;

        private float voltageJitterTimer = 0f;
        private float displayedVoltage = 480.0f;

        // Procedural Audio Clips
        private AudioClip relayClickClip;
        private AudioClip sparkZapClip;
        private AudioClip powerSurgeClip;
        private AudioClip humPulseClip;

        // Visual Colors
        private readonly Color ColorCellBgNormal  = new Color(0.08f, 0.12f, 0.17f, 1f);
        private readonly Color ColorCellBorderNorm= new Color(0.18f, 0.26f, 0.36f, 1f);
        private readonly Color ColorCellBgOverload= new Color(0.14f, 0.08f, 0.09f, 1f);
        private readonly Color ColorCellBorderOver= new Color(0.38f, 0.16f, 0.18f, 1f);

        private readonly Color ColorUnpowered     = new Color(0.20f, 0.28f, 0.38f, 1f);
        private readonly Color ColorEnergized     = new Color(0f, 0.94f, 1f, 1f);
        private readonly Color ColorEnergizedGlow = new Color(0f, 0.94f, 1f, 0.45f);
        private readonly Color ColorOverloadRed   = new Color(1f, 0.25f, 0.25f, 1f);
        private readonly Color ColorSuccess       = new Color(0f, 1f, 0.5f, 1f);

        public bool IsOpen => rootPanel != null && rootPanel.activeSelf;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;

            EnsureAudioSource();
            GenerateProceduralAudio();
            BindAllButtons();
        }

        public void BindAllButtons()
        {
            if (closeButton != null)
            {
                closeButton.onClick.RemoveAllListeners();
                closeButton.onClick.AddListener(CloseMinigame);
            }
        }

        private void Update()
        {
            if (!IsOpen || isFinished) return;

            // Handle Breaker Trip Cooldown
            if (tripCooldownTimer > 0f)
            {
                tripCooldownTimer -= Time.deltaTime;
                if (breakerCooldownFill != null)
                {
                    breakerCooldownFill.fillAmount = Mathf.Clamp01(tripCooldownTimer / TripCooldownDuration);
                }
                if (breakerCooldownText != null)
                {
                    breakerCooldownText.text = $"COOLING RELAY: {Mathf.Max(0f, tripCooldownTimer):F1}s";
                }

                if (tripCooldownTimer <= 0f)
                {
                    tripCooldownTimer = 0f;
                    // Check if fault was disconnected while cooling
                    EvaluateCircuitFlow();
                }
            }

            // Voltage Readout Jitter for Sci-Fi Realism
            voltageJitterTimer += Time.deltaTime;
            if (voltageJitterTimer >= 0.12f)
            {
                voltageJitterTimer = 0f;
                float jitter = UnityEngine.Random.Range(-1.6f, 1.8f);
                displayedVoltage = 480.0f + jitter;
                if (busPotentialText != null)
                {
                    busPotentialText.text = $"{displayedVoltage:F1} VAC // 60Hz";
                }
            }

            HandleKeyboardInput();
            SmoothRotateCells();
        }

        #endregion

        #region Interaction & Controls (New Input System)

        private void HandleKeyboardInput()
        {
            var kb = Keyboard.current;
            if (kb == null) return;

            // Close on ESC
            if (kb.escapeKey.wasPressedThisFrame)
            {
                CloseMinigame();
                return;
            }

            // Grid Navigation
            bool selChanged = false;
            if (kb.wKey.wasPressedThisFrame || kb.upArrowKey.wasPressedThisFrame)
            {
                selectedRow = (selectedRow - 1 + GridRows) % GridRows;
                selChanged = true;
            }
            else if (kb.sKey.wasPressedThisFrame || kb.downArrowKey.wasPressedThisFrame)
            {
                selectedRow = (selectedRow + 1) % GridRows;
                selChanged = true;
            }
            else if (kb.aKey.wasPressedThisFrame || kb.leftArrowKey.wasPressedThisFrame)
            {
                selectedCol = (selectedCol - 1 + GridCols) % GridCols;
                selChanged = true;
            }
            else if (kb.dKey.wasPressedThisFrame || kb.rightArrowKey.wasPressedThisFrame)
            {
                selectedCol = (selectedCol + 1) % GridCols;
                selChanged = true;
            }

            if (selChanged)
            {
                UpdateSelectionHighlights();
                PlayRelayClick(0.20f);
            }

            // Rotation Actions
            if (kb.spaceKey.wasPressedThisFrame || kb.eKey.wasPressedThisFrame || 
                kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)
            {
                RotateCellAt(selectedRow, selectedCol, clockwise: true);
            }
            else if (kb.qKey.wasPressedThisFrame)
            {
                RotateCellAt(selectedRow, selectedCol, clockwise: false);
            }
        }

        public void OnCellClicked(int r, int c, bool clockwise)
        {
            if (isFinished) return;

            selectedRow = r;
            selectedCol = c;
            UpdateSelectionHighlights();
            RotateCellAt(r, c, clockwise);
        }

        private void RotateCellAt(int r, int c, bool clockwise)
        {
            if (r < 0 || r >= GridRows || c < 0 || c >= GridCols) return;
            GridCell cell = grid[r, c];
            if (cell == null) return;

            if (clockwise)
            {
                cell.rotationIndex = (cell.rotationIndex + 1) % 4;
            }
            else
            {
                cell.rotationIndex = (cell.rotationIndex + 3) % 4;
            }

            PlayRelayClick(0.48f);
            EvaluateCircuitFlow();
        }

        private void UpdateSelectionHighlights()
        {
            for (int r = 0; r < GridRows; r++)
            {
                for (int c = 0; c < GridCols; c++)
                {
                    if (grid[r, c]?.selectionReticleGo != null)
                    {
                        bool isSel = (r == selectedRow && c == selectedCol);
                        grid[r, c].selectionReticleGo.SetActive(isSel);
                    }
                }
            }
        }

        private void SmoothRotateCells()
        {
            for (int r = 0; r < GridRows; r++)
            {
                for (int c = 0; c < GridCols; c++)
                {
                    GridCell cell = grid[r, c];
                    if (cell != null && cell.rotatorRect != null)
                    {
                        float targetAngle = -cell.rotationIndex * 90f;
                        float currentZ = cell.rotatorRect.localEulerAngles.z;
                        if (currentZ > 180f) currentZ -= 360f;

                        float newZ = Mathf.MoveTowardsAngle(currentZ, targetAngle, 720f * Time.deltaTime);
                        cell.rotatorRect.localEulerAngles = new Vector3(0, 0, newZ);
                    }
                }
            }
        }

        #endregion

        #region Circuit Simulation (BFS Flow & Overload Logic)

        private void EvaluateCircuitFlow()
        {
            // Reset energized state
            for (int r = 0; r < GridRows; r++)
            {
                for (int c = 0; c < GridCols; c++)
                {
                    if (grid[r, c] != null) grid[r, c].isEnergized = false;
                }
            }

            int energizedCount = 0;
            bool hitOverload = false;
            GridCell overloadCell = null;

            // Power IN is at West port of (0, 0)
            GridCell startCell = grid[0, 0];
            bool powerConnectedAtInlet = (startCell != null && (startCell.CurrentPorts & PortFlags.West) != 0);

            if (powerConnectedAtInlet)
            {
                Queue<GridCell> queue = new Queue<GridCell>();
                startCell.isEnergized = true;
                energizedCount++;
                queue.Enqueue(startCell);

                if (startCell.isOverload)
                {
                    hitOverload = true;
                    overloadCell = startCell;
                }

                while (queue.Count > 0 && !hitOverload)
                {
                    GridCell curr = queue.Dequeue();
                    PortFlags ports = curr.CurrentPorts;

                    // North neighbor (r - 1, c)
                    if ((ports & PortFlags.North) != 0 && curr.row > 0)
                    {
                        GridCell nb = grid[curr.row - 1, curr.col];
                        if (nb != null && !nb.isEnergized && (nb.CurrentPorts & PortFlags.South) != 0)
                        {
                            nb.isEnergized = true;
                            energizedCount++;
                            queue.Enqueue(nb);
                            if (nb.isOverload) { hitOverload = true; overloadCell = nb; break; }
                        }
                    }

                    // East neighbor (r, c + 1)
                    if ((ports & PortFlags.East) != 0 && curr.col < GridCols - 1)
                    {
                        GridCell nb = grid[curr.row, curr.col + 1];
                        if (nb != null && !nb.isEnergized && (nb.CurrentPorts & PortFlags.West) != 0)
                        {
                            nb.isEnergized = true;
                            energizedCount++;
                            queue.Enqueue(nb);
                            if (nb.isOverload) { hitOverload = true; overloadCell = nb; break; }
                        }
                    }

                    // South neighbor (r + 1, c)
                    if ((ports & PortFlags.South) != 0 && curr.row < GridRows - 1)
                    {
                        GridCell nb = grid[curr.row + 1, curr.col];
                        if (nb != null && !nb.isEnergized && (nb.CurrentPorts & PortFlags.North) != 0)
                        {
                            nb.isEnergized = true;
                            energizedCount++;
                            queue.Enqueue(nb);
                            if (nb.isOverload) { hitOverload = true; overloadCell = nb; break; }
                        }
                    }

                    // West neighbor (r, c - 1)
                    if ((ports & PortFlags.West) != 0 && curr.col > 0)
                    {
                        GridCell nb = grid[curr.row, curr.col - 1];
                        if (nb != null && !nb.isEnergized && (nb.CurrentPorts & PortFlags.East) != 0)
                        {
                            nb.isEnergized = true;
                            energizedCount++;
                            queue.Enqueue(nb);
                            if (nb.isOverload) { hitOverload = true; overloadCell = nb; break; }
                        }
                    }
                }
            }

            // Handle Overload Hit
            if (hitOverload)
            {
                TriggerBreakerTrip(overloadCell);
                return;
            }

            // Overload is NOT hit!
            if (isBreakerTripped)
            {
                // Fault has been successfully disconnected by the player!
                if (tripCooldownTimer <= 0f)
                {
                    // Breaker re-arms!
                    isBreakerTripped = false;
                    PlayRelayClick(0.6f);

                    if (breakerStatusText != null)
                    {
                        breakerStatusText.text = "<color=#00FF7F>ARMED // READY</color>";
                    }
                    if (breakerCooldownFill != null)
                    {
                        breakerCooldownFill.color = ColorSuccess;
                        breakerCooldownFill.fillAmount = 1f;
                    }
                    if (breakerCooldownText != null)
                    {
                        breakerCooldownText.text = "NORMAL OPERATING STATE";
                    }
                }
                else
                {
                    // Still cooling down briefly before closing relay
                    for (int r = 0; r < GridRows; r++)
                    {
                        for (int c = 0; c < GridCols; c++)
                        {
                            if (grid[r, c] != null) grid[r, c].isEnergized = false;
                        }
                    }
                    UpdateConduitGraphics();
                    return;
                }
            }

            // Normal Flow Operation
            UpdateConduitGraphics();

            // Telemetry Updates
            if (nodesEnergizedText != null)
            {
                nodesEnergizedText.text = $"{energizedCount} / 16 ({(energizedCount * 100 / 16)}%)";
            }

            // Check Win Condition: Cell (3,3) is energized AND has East port open to output sink
            GridCell endCell = grid[GridRows - 1, GridCols - 1];
            bool circuitComplete = (endCell != null && endCell.isEnergized && (endCell.CurrentPorts & PortFlags.East) != 0);

            if (efficiencyText != null)
            {
                int eff = circuitComplete ? 100 : Mathf.Clamp(energizedCount * 6, 0, 85);
                efficiencyText.text = $"{eff}% NOMINAL";
            }

            if (circuitComplete)
            {
                TriggerCircuitSuccess();
            }
            else
            {
                if (circuitStateText != null)
                {
                    if (!powerConnectedAtInlet)
                    {
                        circuitStateText.text = "<color=#8899AA>DISCONNECTED // [IN] INLET CLOSED</color>";
                    }
                    else
                    {
                        circuitStateText.text = "<color=#00F0FF>ROUTING // IN PROGRESS</color>";
                    }
                }
            }
        }

        private void TriggerBreakerTrip(GridCell trippedCell)
        {
            bool firstTrip = !isBreakerTripped;
            isBreakerTripped = true;

            if (firstTrip)
            {
                tripCooldownTimer = TripCooldownDuration;
                PlaySparkZap();
                StartCoroutine(FlashOverloadRoutine(trippedCell));
            }

            // Cut all conduit power while breaker is tripped
            for (int r = 0; r < GridRows; r++)
            {
                for (int c = 0; c < GridCols; c++)
                {
                    if (grid[r, c] != null) grid[r, c].isEnergized = false;
                }
            }
            UpdateConduitGraphics();

            if (circuitStateText != null)
            {
                circuitStateText.text = "<color=#FF3333>OVERLOAD // BREAKER TRIPPED</color>";
            }
            if (breakerStatusText != null)
            {
                breakerStatusText.text = "<color=#FF3333>FAULT DETECTED // DISCONNECT [!]</color>";
            }
            if (nodesEnergizedText != null)
            {
                nodesEnergizedText.text = "0 / 16 (TRIPPED)";
            }
            if (efficiencyText != null)
            {
                efficiencyText.text = "0% (FAULT)";
            }
            if (breakerCooldownFill != null)
            {
                breakerCooldownFill.color = ColorOverloadRed;
            }
            if (breakerCooldownText != null && tripCooldownTimer <= 0f)
            {
                breakerCooldownText.text = "FAULT STILL CONNECTED - ROTATE AWAY";
            }
        }

        private IEnumerator FlashOverloadRoutine(GridCell cell)
        {
            if (cell == null) yield break;
            for (int i = 0; i < 4; i++)
            {
                if (cell.bgImage != null) cell.bgImage.color = new Color(0.65f, 0.12f, 0.12f, 1f);
                yield return new WaitForSeconds(0.08f);
                if (cell.bgImage != null) cell.bgImage.color = ColorCellBgOverload;
                yield return new WaitForSeconds(0.08f);
            }
        }

        private void TriggerCircuitSuccess()
        {
            isFinished = true;

            PlayPowerSurge();

            if (circuitStateText != null)
            {
                circuitStateText.text = "<color=#00FF7F>CIRCUIT CLOSED // POWER RESTORED</color>";
            }
            if (breakerStatusText != null)
            {
                breakerStatusText.text = "<color=#00FF7F>ONLINE // 100% NOMINAL</color>";
            }
            if (efficiencyText != null)
            {
                efficiencyText.text = "100% MAXIMUM";
            }

            StartCoroutine(VictoryRoutine());
        }

        private IEnumerator VictoryRoutine()
        {
            // Flash conduits with power surge effect
            for (int step = 0; step < 3; step++)
            {
                SetAllEnergizedColor(Color.white);
                yield return new WaitForSeconds(0.1f);
                SetAllEnergizedColor(ColorSuccess);
                yield return new WaitForSeconds(0.12f);
            }

            yield return new WaitForSeconds(0.6f);

            // Notify repair station
            if (activeTaskLocation != null)
            {
                activeTaskLocation.OnSubTaskCompleted(activePlayer, this);
            }
            else
            {
                CloseMinigame();
            }
        }

        private void SetAllEnergizedColor(Color col)
        {
            for (int r = 0; r < GridRows; r++)
            {
                for (int c = 0; c < GridCols; c++)
                {
                    GridCell cell = grid[r, c];
                    if (cell != null && cell.isEnergized)
                    {
                        if (cell.centerHub != null) cell.centerHub.color = col;
                        if (cell.armNorth != null && cell.armNorth.gameObject.activeSelf) cell.armNorth.color = col;
                        if (cell.armEast != null && cell.armEast.gameObject.activeSelf) cell.armEast.color = col;
                        if (cell.armSouth != null && cell.armSouth.gameObject.activeSelf) cell.armSouth.color = col;
                        if (cell.armWest != null && cell.armWest.gameObject.activeSelf) cell.armWest.color = col;
                    }
                }
            }
        }

        private void UpdateConduitGraphics()
        {
            for (int r = 0; r < GridRows; r++)
            {
                for (int c = 0; c < GridCols; c++)
                {
                    GridCell cell = grid[r, c];
                    if (cell == null) continue;

                    Color conduitCol = cell.isEnergized ? ColorEnergized : ColorUnpowered;
                    Color glowCol = cell.isEnergized ? ColorEnergizedGlow : Color.clear;

                    if (cell.centerHub != null) cell.centerHub.color = conduitCol;
                    if (cell.centerHubGlow != null) cell.centerHubGlow.color = glowCol;

                    if (cell.armNorth != null && cell.armNorth.gameObject.activeSelf)
                    {
                        cell.armNorth.color = conduitCol;
                        if (cell.armNorthGlow != null) cell.armNorthGlow.color = glowCol;
                    }
                    if (cell.armEast != null && cell.armEast.gameObject.activeSelf)
                    {
                        cell.armEast.color = conduitCol;
                        if (cell.armEastGlow != null) cell.armEastGlow.color = glowCol;
                    }
                    if (cell.armSouth != null && cell.armSouth.gameObject.activeSelf)
                    {
                        cell.armSouth.color = conduitCol;
                        if (cell.armSouthGlow != null) cell.armSouthGlow.color = glowCol;
                    }
                    if (cell.armWest != null && cell.armWest.gameObject.activeSelf)
                    {
                        cell.armWest.color = conduitCol;
                        if (cell.armWestGlow != null) cell.armWestGlow.color = glowCol;
                    }
                }
            }

            // Highlight terminal badges
            if (inputTerminalBadge != null)
            {
                inputTerminalBadge.color = (grid[0, 0] != null && grid[0, 0].isEnergized) ? ColorSuccess : ColorUnpowered;
            }
            if (outputTerminalBadge != null)
            {
                GridCell end = grid[GridRows - 1, GridCols - 1];
                outputTerminalBadge.color = (end != null && end.isEnergized && (end.CurrentPorts & PortFlags.East) != 0) 
                    ? ColorSuccess : ColorUnpowered;
            }
        }

        #endregion

        #region Solvable Puzzle Generator

        private void GenerateGridPuzzle(int seed)
        {
            System.Random rng = new System.Random(seed);

            // Step 1: Generate a serpentine solution path from (0,0) to (3,3)
            List<Vector2Int> path = GenerateSerpentinePath(rng);

            Dictionary<Vector2Int, int> pathIndexMap = new Dictionary<Vector2Int, int>();
            for (int i = 0; i < path.Count; i++)
            {
                pathIndexMap[path[i]] = i;
            }

            // Step 2: Assign conduits to path cells
            for (int i = 0; i < path.Count; i++)
            {
                Vector2Int curr = path[i];
                PortFlags inPort = (i == 0) ? PortFlags.West : GetPortFromDelta(curr, path[i - 1]);
                PortFlags outPort = (i == path.Count - 1) ? PortFlags.East : GetPortFromDelta(curr, path[i + 1]);

                PortFlags requiredPorts = inPort | outPort;
                GridCell cell = grid[curr.x, curr.y];
                cell.isOverload = false;

                // Pick conduit type and base ports
                ConfigureConduitForPorts(cell, requiredPorts, rng);
            }

            // Step 3: Assign non-path cells
            List<Vector2Int> nonPathNeighbors = new List<Vector2Int>();
            List<Vector2Int> otherNonPath = new List<Vector2Int>();

            for (int r = 0; r < GridRows; r++)
            {
                for (int c = 0; c < GridCols; c++)
                {
                    Vector2Int pt = new Vector2Int(r, c);
                    if (pathIndexMap.ContainsKey(pt)) continue;

                    bool isAdjacentToPath = false;
                    foreach (Vector2Int dir in new Vector2Int[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right })
                    {
                        if (pathIndexMap.ContainsKey(pt + dir))
                        {
                            isAdjacentToPath = true;
                            break;
                        }
                    }

                    if (isAdjacentToPath) nonPathNeighbors.Add(pt);
                    else otherNonPath.Add(pt);
                }
            }

            // Place 2 Overload nodes in nonPathNeighbors
            int overloadPlaced = 0;
            while (overloadPlaced < 2 && nonPathNeighbors.Count > 0)
            {
                int idx = rng.Next(nonPathNeighbors.Count);
                Vector2Int pos = nonPathNeighbors[idx];
                nonPathNeighbors.RemoveAt(idx);

                GridCell cell = grid[pos.x, pos.y];
                cell.isOverload = true;
                cell.conduitType = ConduitType.Overload;
                cell.basePorts = (rng.Next(2) == 0) ? (PortFlags.North | PortFlags.East) : (PortFlags.North | PortFlags.East | PortFlags.South);
                cell.rotationIndex = rng.Next(4);
                overloadPlaced++;
            }

            // Fill all remaining non-path cells with random conduits
            List<Vector2Int> allRemaining = new List<Vector2Int>(nonPathNeighbors);
            allRemaining.AddRange(otherNonPath);

            foreach (Vector2Int pt in allRemaining)
            {
                GridCell cell = grid[pt.x, pt.y];
                cell.isOverload = false;
                int typeRoll = rng.Next(4);
                switch (typeRoll)
                {
                    case 0:
                        cell.conduitType = ConduitType.Straight;
                        cell.basePorts = PortFlags.North | PortFlags.South;
                        break;
                    case 1:
                        cell.conduitType = ConduitType.Corner;
                        cell.basePorts = PortFlags.North | PortFlags.East;
                        break;
                    case 2:
                        cell.conduitType = ConduitType.Tee;
                        cell.basePorts = PortFlags.North | PortFlags.East | PortFlags.West;
                        break;
                    case 3:
                    default:
                        cell.conduitType = ConduitType.Cross;
                        cell.basePorts = PortFlags.North | PortFlags.East | PortFlags.South | PortFlags.West;
                        break;
                }
                cell.rotationIndex = rng.Next(4);
            }

            // Step 4: Scramble path cells by rotating them by 90°, 180°, or 270°
            for (int i = 0; i < path.Count; i++)
            {
                Vector2Int pt = path[i];
                GridCell cell = grid[pt.x, pt.y];
                int scrambleSteps = rng.Next(1, 4);
                cell.rotationIndex = (cell.rotationIndex + scrambleSteps) % 4;
            }

            // Crucial Fix: Ensure Cell (0,0) starts with West port CLOSED
            // so power doesn't prematurely flood the grid on frame 0!
            while ((grid[0, 0].CurrentPorts & PortFlags.West) != 0)
            {
                grid[0, 0].rotationIndex = (grid[0, 0].rotationIndex + 1) % 4;
            }

            // Apply base arm visibility & cell backgrounds
            ApplyCellVisualTopology();
        }

        private List<Vector2Int> GenerateSerpentinePath(System.Random rng)
        {
            for (int attempt = 0; attempt < 120; attempt++)
            {
                List<Vector2Int> path = new List<Vector2Int>();
                HashSet<Vector2Int> visited = new HashSet<Vector2Int>();
                Vector2Int curr = new Vector2Int(0, 0);
                path.Add(curr);
                visited.Add(curr);

                bool success = false;
                for (int step = 0; step < 24; step++)
                {
                    List<Vector2Int> validMoves = new List<Vector2Int>();
                    Vector2Int[] dirs = { new Vector2Int(-1, 0), new Vector2Int(1, 0), new Vector2Int(0, -1), new Vector2Int(0, 1) };
                    
                    for (int d = 0; d < dirs.Length; d++)
                    {
                        int r = rng.Next(d, dirs.Length);
                        (dirs[d], dirs[r]) = (dirs[r], dirs[d]);
                    }

                    foreach (var d in dirs)
                    {
                        Vector2Int next = curr + d;
                        if (next.x >= 0 && next.x < GridRows && next.y >= 0 && next.y < GridCols && !visited.Contains(next))
                        {
                            if (next == new Vector2Int(3, 3))
                            {
                                if (path.Count >= 7) validMoves.Add(next);
                            }
                            else
                            {
                                validMoves.Add(next);
                            }
                        }
                    }

                    if (validMoves.Count == 0) break;

                    curr = validMoves[0];
                    path.Add(curr);
                    visited.Add(curr);

                    if (curr == new Vector2Int(3, 3) && path.Count >= 8)
                    {
                        success = true;
                        break;
                    }
                }

                if (success) return path;
            }

            // Guaranteed Fallback Serpentine Path (11 steps)
            return new List<Vector2Int>
            {
                new Vector2Int(0, 0),
                new Vector2Int(0, 1),
                new Vector2Int(0, 2),
                new Vector2Int(1, 2),
                new Vector2Int(1, 1),
                new Vector2Int(1, 0),
                new Vector2Int(2, 0),
                new Vector2Int(2, 1),
                new Vector2Int(2, 2),
                new Vector2Int(2, 3),
                new Vector2Int(3, 3)
            };
        }

        private static PortFlags GetPortFromDelta(Vector2Int from, Vector2Int to)
        {
            if (to.x < from.x) return PortFlags.North;
            if (to.x > from.x) return PortFlags.South;
            if (to.y > from.y) return PortFlags.East;
            if (to.y < from.y) return PortFlags.West;
            return PortFlags.None;
        }

        private void ConfigureConduitForPorts(GridCell cell, PortFlags requiredPorts, System.Random rng)
        {
            bool isOpposite = (requiredPorts == (PortFlags.North | PortFlags.South)) || 
                              (requiredPorts == (PortFlags.East | PortFlags.West));

            if (isOpposite)
            {
                int roll = rng.Next(10);
                if (roll < 6)
                {
                    cell.conduitType = ConduitType.Straight;
                    cell.basePorts = PortFlags.North | PortFlags.South;
                    cell.rotationIndex = (requiredPorts == (PortFlags.East | PortFlags.West)) ? 1 : 0;
                }
                else if (roll < 8)
                {
                    cell.conduitType = ConduitType.Tee;
                    cell.basePorts = PortFlags.North | PortFlags.South | PortFlags.East;
                    cell.rotationIndex = (requiredPorts == (PortFlags.East | PortFlags.West)) ? 1 : 0;
                }
                else
                {
                    cell.conduitType = ConduitType.Cross;
                    cell.basePorts = PortFlags.North | PortFlags.East | PortFlags.South | PortFlags.West;
                    cell.rotationIndex = 0;
                }
            }
            else
            {
                int roll = rng.Next(10);
                if (roll < 7)
                {
                    cell.conduitType = ConduitType.Corner;
                    cell.basePorts = PortFlags.North | PortFlags.East;
                    cell.rotationIndex = GetCornerRotationIndex(requiredPorts);
                }
                else
                {
                    cell.conduitType = ConduitType.Tee;
                    cell.basePorts = PortFlags.North | PortFlags.East | PortFlags.West;
                    cell.rotationIndex = GetTeeRotationIndexContaining(requiredPorts);
                }
            }
        }

        private static int GetCornerRotationIndex(PortFlags ports)
        {
            if (ports == (PortFlags.North | PortFlags.East)) return 0;
            if (ports == (PortFlags.East  | PortFlags.South)) return 1;
            if (ports == (PortFlags.South | PortFlags.West)) return 2;
            if (ports == (PortFlags.West  | PortFlags.North)) return 3;
            return 0;
        }

        private static int GetTeeRotationIndexContaining(PortFlags required)
        {
            for (int i = 0; i < 4; i++)
            {
                PortFlags rot = GetRotatedPorts(PortFlags.North | PortFlags.East | PortFlags.West, i);
                if ((rot & required) == required) return i;
            }
            return 0;
        }

        public static PortFlags GetRotatedPorts(PortFlags basePorts, int rotationSteps)
        {
            PortFlags p = basePorts;
            int steps = ((rotationSteps % 4) + 4) % 4;
            for (int i = 0; i < steps; i++)
            {
                PortFlags next = PortFlags.None;
                if ((p & PortFlags.North) != 0) next |= PortFlags.East;
                if ((p & PortFlags.East)  != 0) next |= PortFlags.South;
                if ((p & PortFlags.South) != 0) next |= PortFlags.West;
                if ((p & PortFlags.West)  != 0) next |= PortFlags.North;
                p = next;
            }
            return p;
        }

        private void ApplyCellVisualTopology()
        {
            for (int r = 0; r < GridRows; r++)
            {
                for (int c = 0; c < GridCols; c++)
                {
                    GridCell cell = grid[r, c];
                    if (cell == null) continue;

                    // Arms visibility inside rotator matches basePorts
                    if (cell.armNorth != null) cell.armNorth.gameObject.SetActive((cell.basePorts & PortFlags.North) != 0);
                    if (cell.armEast != null)  cell.armEast.gameObject.SetActive((cell.basePorts & PortFlags.East) != 0);
                    if (cell.armSouth != null) cell.armSouth.gameObject.SetActive((cell.basePorts & PortFlags.South) != 0);
                    if (cell.armWest != null)  cell.armWest.gameObject.SetActive((cell.basePorts & PortFlags.West) != 0);

                    // Cell Background styling
                    if (cell.bgImage != null)
                    {
                        cell.bgImage.color = cell.isOverload ? ColorCellBgOverload : ColorCellBgNormal;
                    }
                    if (cell.bgOutline != null)
                    {
                        cell.bgOutline.effectColor = cell.isOverload ? ColorCellBorderOver : ColorCellBorderNorm;
                    }

                    // Overload badge visibility
                    if (cell.overloadBadgeGo != null)
                    {
                        cell.overloadBadgeGo.SetActive(cell.isOverload);
                    }

                    // Snap initial visual angle
                    if (cell.rotatorRect != null)
                    {
                        cell.rotatorRect.localEulerAngles = new Vector3(0, 0, -cell.rotationIndex * 90f);
                    }
                }
            }
        }

        #endregion

        #region Public API: Open & Close

        public void Open(PlayerController player, TaskLocation taskLocation)
        {
            if (player == null)
            {
                player = PlayerController.Local ?? UnityEngine.Object.FindAnyObjectByType<PlayerController>();
            }

            activePlayer = player;
            activeTaskLocation = taskLocation;
            isFinished = false;
            isBreakerTripped = false;
            tripCooldownTimer = 0f;

            if (activePlayer != null)
            {
                activePlayer.SetControlEnabled(false);
            }

            // Header Title
            if (headerText != null && taskLocation != null)
            {
                int currentStep = 1;
                int targetSteps = 3;
                if (taskLocation.TryGetComponent(out StationRepair repair))
                {
                    currentStep = repair.CompletedSteps + 1;
                    targetSteps = repair.GetTargetStepsForPlayer(player);
                }
                headerText.text = $"POWER ROUTING TERMINAL // {taskLocation.StationDisplayName.ToUpper()} [STAGE {currentStep}/{targetSteps}]";
            }

            // Generate puzzle using stable seed
            int seed = 42;
            if (taskLocation != null)
            {
                seed = taskLocation.StationId.GetHashCode() + (int)(Time.time * 10f);
            }
            GenerateGridPuzzle(seed);

            // Reset UI
            selectedRow = 0;
            selectedCol = 0;
            UpdateSelectionHighlights();

            if (breakerStatusText != null)
            {
                breakerStatusText.text = "<color=#00FF7F>ARMED // READY</color>";
            }
            if (breakerCooldownFill != null)
            {
                breakerCooldownFill.color = ColorSuccess;
                breakerCooldownFill.fillAmount = 1f;
            }
            if (breakerCooldownText != null)
            {
                breakerCooldownText.text = "NORMAL OPERATING STATE";
            }

            BindAllButtons();

            if (rootPanel != null)
            {
                rootPanel.SetActive(true);
            }

            // Initial Flow check
            EvaluateCircuitFlow();
        }

        public void CloseMinigame()
        {
            if (activeTaskLocation != null && !isFinished)
            {
                if (activeTaskLocation.TryGetComponent(out StationRepair repair))
                {
                    repair.ReleaseRepairLock(activePlayer);
                }
            }

            if (activePlayer != null)
            {
                activePlayer.SetControlEnabled(true);
            }

            if (rootPanel != null)
            {
                rootPanel.SetActive(false);
            }
        }

        #endregion

        #region Audio Synthesis

        private void EnsureAudioSource()
        {
            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
                if (audioSource == null)
                {
                    audioSource = gameObject.AddComponent<AudioSource>();
                }
            }
            if (audioSource != null)
            {
                audioSource.playOnAwake = false;
                audioSource.spatialBlend = 0f;
            }
        }

        private void GenerateProceduralAudio()
        {
            if (relayClickClip == null) relayClickClip = CreateRelayClickClip();
            if (sparkZapClip == null) sparkZapClip = CreateSparkZapClip();
            if (powerSurgeClip == null) powerSurgeClip = CreatePowerSurgeClip();
            if (humPulseClip == null) humPulseClip = CreateHumPulseClip();
        }

        private void PlayRelayClick(float volume = 0.45f)
        {
            if (audioSource != null && relayClickClip != null)
            {
                audioSource.PlayOneShot(relayClickClip, volume);
            }
        }

        private void PlaySparkZap()
        {
            if (audioSource != null && sparkZapClip != null)
            {
                audioSource.PlayOneShot(sparkZapClip, 0.85f);
            }
        }

        private void PlayPowerSurge()
        {
            if (audioSource != null && powerSurgeClip != null)
            {
                audioSource.PlayOneShot(powerSurgeClip, 0.9f);
            }
        }

        private static AudioClip CreateRelayClickClip()
        {
            int sampleRate = 44100;
            float duration = 0.05f;
            int samples = (int)(sampleRate * duration);
            float[] data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / sampleRate;
                float env = 1f - (t / duration);
                float noise = (UnityEngine.Random.value * 2f - 1f) * 0.4f;
                float tone = Mathf.Sin(2f * Mathf.PI * 1400f * t) * 0.6f;
                data[i] = (tone + noise) * env * 0.5f;
            }
            AudioClip clip = AudioClip.Create("PG_RelayClick", samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip CreateSparkZapClip()
        {
            int sampleRate = 44100;
            float duration = 0.32f;
            int samples = (int)(sampleRate * duration);
            float[] data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / sampleRate;
                float env = 1f - Mathf.Pow(t / duration, 0.7f);
                float buzz = Mathf.Sign(Mathf.Sin(2f * Mathf.PI * 90f * t)) * 0.45f;
                float noise = (UnityEngine.Random.value * 2f - 1f) * 0.55f;
                data[i] = (buzz + noise) * env * 0.75f;
            }
            AudioClip clip = AudioClip.Create("PG_SparkZap", samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip CreatePowerSurgeClip()
        {
            int sampleRate = 44100;
            float duration = 0.75f;
            int samples = (int)(sampleRate * duration);
            float[] data = new float[samples];
            float[] freqs = { 261.63f, 329.63f, 392.00f, 523.25f };

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / sampleRate;
                float env = 1f - Mathf.Pow(t / duration, 0.9f);
                float sum = 0f;
                for (int f = 0; f < freqs.Length; f++)
                {
                    float noteStart = f * 0.08f;
                    if (t >= noteStart)
                    {
                        float localT = t - noteStart;
                        sum += Mathf.Sin(2f * Mathf.PI * freqs[f] * localT) * 0.22f;
                    }
                }
                data[i] = sum * env;
            }
            AudioClip clip = AudioClip.Create("PG_PowerSurge", samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip CreateHumPulseClip()
        {
            int sampleRate = 44100;
            float duration = 0.15f;
            int samples = (int)(sampleRate * duration);
            float[] data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Sin(Mathf.PI * (t / duration));
                data[i] = Mathf.Sin(2f * Mathf.PI * 120f * t) * 0.35f * env;
            }
            AudioClip clip = AudioClip.Create("PG_HumPulse", samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        #endregion

        #region Runtime UI Hierarchy Factory

        public static PowerGridTaskUI GetOrCreateInstance()
        {
            if (Instance != null) return Instance;

            Canvas canvas = null;
            GameObject hudCanvas = GameObject.Find("HUD_Canvas");
            if (hudCanvas != null)
            {
                canvas = hudCanvas.GetComponent<Canvas>();
            }
            if (canvas == null)
            {
                canvas = UnityEngine.Object.FindAnyObjectByType<Canvas>();
            }

            if (canvas == null)
            {
                Debug.LogWarning("[PowerGridTaskUI] No Canvas found in scene.");
                return null;
            }

            _instance = CreatePowerGridUIHierarchy(canvas);
            return _instance;
        }

        public static PowerGridTaskUI CreatePowerGridUIHierarchy(Canvas canvas)
        {
            Sprite chassisSprite = Resources.Load<Sprite>("UI/Tasks/TerminalBezel");

            // Fullscreen Dark Vignette Overlay
            GameObject root = new GameObject("PowerGridMinigamePanel", typeof(RectTransform), typeof(AudioSource));
            root.transform.SetParent(canvas.transform, false);

            RectTransform rootRect = root.GetComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;

            Canvas panelCanvas = root.AddComponent<Canvas>();
            panelCanvas.overrideSorting = true;
            panelCanvas.sortingOrder = 1000;
            root.AddComponent<GraphicRaycaster>();

            Image bgOverlay = root.AddComponent<Image>();
            bgOverlay.color = new Color(0.02f, 0.04f, 0.06f, 0.90f);
            bgOverlay.raycastTarget = true;

            // Center Terminal Box (960 x 640)
            GameObject box = new GameObject("TerminalBox", typeof(RectTransform), typeof(Image));
            box.transform.SetParent(root.transform, false);
            RectTransform boxRect = box.GetComponent<RectTransform>();
            boxRect.sizeDelta = new Vector2(960, 640);
            boxRect.anchoredPosition = Vector2.zero;
            Image boxImg = box.GetComponent<Image>();
            if (chassisSprite != null)
            {
                boxImg.sprite = chassisSprite;
                boxImg.color = Color.white;
            }
            else
            {
                boxImg.color = new Color(0.12f, 0.14f, 0.17f, 1f);
                Outline outline = box.AddComponent<Outline>();
                outline.effectColor = new Color(0.35f, 0.42f, 0.5f, 1f);
                outline.effectDistance = new Vector2(3, -3);
            }

            // Solid Dark Background Chassis Backing
            GameObject cbgGo = new GameObject("TerminalChassisBackground", typeof(RectTransform), typeof(Image));
            cbgGo.transform.SetParent(box.transform, false);
            cbgGo.transform.SetAsFirstSibling();
            RectTransform cbgRect = cbgGo.GetComponent<RectTransform>();
            cbgRect.anchorMin = Vector2.zero;
            cbgRect.anchorMax = Vector2.one;
            cbgRect.sizeDelta = new Vector2(-16, -16);
            cbgRect.anchoredPosition = Vector2.zero;
            Image cbgImg = cbgGo.GetComponent<Image>();
            cbgImg.color = new Color(0.06f, 0.08f, 0.11f, 1f);
            cbgImg.raycastTarget = false;

            // Header Title
            GameObject headerGo = new GameObject("HeaderTitle", typeof(RectTransform), typeof(TextMeshProUGUI));
            headerGo.transform.SetParent(box.transform, false);
            RectTransform headerRect = headerGo.GetComponent<RectTransform>();
            headerRect.anchorMin = new Vector2(0f, 1f);
            headerRect.anchorMax = new Vector2(1f, 1f);
            headerRect.pivot = new Vector2(0.5f, 1f);
            headerRect.anchoredPosition = new Vector2(0, -32);
            headerRect.sizeDelta = new Vector2(-120, 36);
            TextMeshProUGUI headerText = headerGo.GetComponent<TextMeshProUGUI>();
            headerText.text = "POWER ROUTING TERMINAL // MK-VII";
            headerText.fontSize = 20;
            headerText.alignment = TextAlignmentOptions.Center;
            headerText.fontStyle = FontStyles.Bold;
            headerText.color = new Color(0.92f, 0.96f, 1f, 1f);

            // Instruction Subtext
            GameObject instGo = new GameObject("InstructionText", typeof(RectTransform), typeof(TextMeshProUGUI));
            instGo.transform.SetParent(box.transform, false);
            RectTransform instRect = instGo.GetComponent<RectTransform>();
            instRect.anchorMin = new Vector2(0f, 1f);
            instRect.anchorMax = new Vector2(1f, 1f);
            instRect.pivot = new Vector2(0.5f, 1f);
            instRect.anchoredPosition = new Vector2(0, -68);
            instRect.sizeDelta = new Vector2(-120, 26);
            TextMeshProUGUI instText = instGo.GetComponent<TextMeshProUGUI>();
            instText.text = "Rotate conduits to route power from <color=#00FF7F>[PWR IN]</color> to <color=#00F0FF>[PWR SINK]</color>. <color=#FF4444>Avoid overload fuses!</color>";
            instText.fontSize = 14;
            instText.alignment = TextAlignmentOptions.Center;
            instText.color = new Color(0.72f, 0.82f, 0.92f, 1f);

            // Close Button [ESC / X]
            GameObject closeGo = new GameObject("CloseButton", typeof(RectTransform), typeof(Image), typeof(Button));
            closeGo.transform.SetParent(box.transform, false);
            RectTransform closeRect = closeGo.GetComponent<RectTransform>();
            closeRect.anchorMin = new Vector2(1f, 1f);
            closeRect.anchorMax = new Vector2(1f, 1f);
            closeRect.pivot = new Vector2(1f, 1f);
            closeRect.anchoredPosition = new Vector2(-28, -28);
            closeRect.sizeDelta = new Vector2(34, 34);
            Image closeImg = closeGo.GetComponent<Image>();
            closeImg.color = new Color(0.75f, 0.15f, 0.15f, 1f);
            closeImg.raycastTarget = true;
            Button closeBtn = closeGo.GetComponent<Button>();
            closeBtn.targetGraphic = closeImg;
            closeBtn.navigation = new Navigation { mode = Navigation.Mode.None };

            GameObject closeTextGo = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            closeTextGo.transform.SetParent(closeGo.transform, false);
            TextMeshProUGUI closeTxt = closeTextGo.GetComponent<TextMeshProUGUI>();
            closeTxt.text = "X";
            closeTxt.fontSize = 20;
            closeTxt.fontStyle = FontStyles.Bold;
            closeTxt.alignment = TextAlignmentOptions.Center;
            closeTxt.color = Color.white;
            closeTxt.raycastTarget = false;

            // ==========================================
            // MAIN CONTENT AREA: Twin-Bay Brutalist Layout
            // ==========================================
            GameObject contentGo = new GameObject("MainContentArea", typeof(RectTransform));
            contentGo.transform.SetParent(box.transform, false);
            RectTransform contentRect = contentGo.GetComponent<RectTransform>();
            contentRect.anchorMin = Vector2.zero;
            contentRect.anchorMax = Vector2.one;
            contentRect.offsetMin = new Vector2(28, 28);
            contentRect.offsetMax = new Vector2(-28, -98);

            // ------------------------------------------
            // BAY 1 (LEFT): 4x4 Power Matrix Bay (530 x 480)
            // ------------------------------------------
            GameObject gridBayGo = new GameObject("GridBay", typeof(RectTransform), typeof(Image));
            gridBayGo.transform.SetParent(contentGo.transform, false);
            RectTransform gridBayRect = gridBayGo.GetComponent<RectTransform>();
            gridBayRect.anchorMin = new Vector2(0f, 0f);
            gridBayRect.anchorMax = new Vector2(0f, 1f);
            gridBayRect.pivot = new Vector2(0f, 0.5f);
            gridBayRect.anchoredPosition = new Vector2(0, 0);
            gridBayRect.sizeDelta = new Vector2(530, 0);
            Image gridBayImg = gridBayGo.GetComponent<Image>();
            gridBayImg.color = new Color(0.045f, 0.07f, 0.10f, 0.95f);
            Outline gBayOutline = gridBayGo.AddComponent<Outline>();
            gBayOutline.effectColor = new Color(0.18f, 0.28f, 0.38f, 1f);
            gBayOutline.effectDistance = new Vector2(1, -1);

            // Bay Title
            GameObject gBayTitleGo = new GameObject("BayTitle", typeof(RectTransform), typeof(TextMeshProUGUI));
            gBayTitleGo.transform.SetParent(gridBayGo.transform, false);
            RectTransform gbtRect = gBayTitleGo.GetComponent<RectTransform>();
            gbtRect.anchorMin = new Vector2(0f, 1f);
            gbtRect.anchorMax = new Vector2(1f, 1f);
            gbtRect.pivot = new Vector2(0.5f, 1f);
            gbtRect.anchoredPosition = new Vector2(0, -12);
            gbtRect.sizeDelta = new Vector2(-20, 24);
            TextMeshProUGUI gbtText = gBayTitleGo.GetComponent<TextMeshProUGUI>();
            gbtText.text = "[ 4x4 CONDUIT MATRIX // POWER BUS ]";
            gbtText.fontSize = 13;
            gbtText.fontStyle = FontStyles.Bold;
            gbtText.alignment = TextAlignmentOptions.Center;
            gbtText.color = new Color(0.45f, 0.75f, 0.95f, 1f);

            // Terminal Input Indicator Badge (Left of (0,0)) - Clean ASCII >>
            GameObject inBadgeGo = new GameObject("InputTerminalBadge", typeof(RectTransform), typeof(Image));
            inBadgeGo.transform.SetParent(gridBayGo.transform, false);
            RectTransform inbRect = inBadgeGo.GetComponent<RectTransform>();
            inbRect.anchorMin = new Vector2(0f, 0.5f);
            inbRect.anchorMax = new Vector2(0f, 0.5f);
            inbRect.pivot = new Vector2(0f, 0.5f);
            inbRect.anchoredPosition = new Vector2(10, 156);
            inbRect.sizeDelta = new Vector2(38, 48);
            Image inbImg = inBadgeGo.GetComponent<Image>();
            inbImg.color = new Color(0f, 1f, 0.5f, 1f);

            GameObject inTextGo = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            inTextGo.transform.SetParent(inBadgeGo.transform, false);
            TextMeshProUGUI inTxt = inTextGo.GetComponent<TextMeshProUGUI>();
            inTxt.text = "[IN]\n>>";
            inTxt.fontSize = 11;
            inTxt.fontStyle = FontStyles.Bold;
            inTxt.alignment = TextAlignmentOptions.Center;
            inTxt.color = Color.black;

            // Terminal Output Indicator Badge (Right of (3,3)) - Clean ASCII >>
            GameObject outBadgeGo = new GameObject("OutputTerminalBadge", typeof(RectTransform), typeof(Image));
            outBadgeGo.transform.SetParent(gridBayGo.transform, false);
            RectTransform outbRect = outBadgeGo.GetComponent<RectTransform>();
            outbRect.anchorMin = new Vector2(1f, 0.5f);
            outbRect.anchorMax = new Vector2(1f, 0.5f);
            outbRect.pivot = new Vector2(1f, 0.5f);
            outbRect.anchoredPosition = new Vector2(-10, -156);
            outbRect.sizeDelta = new Vector2(38, 48);
            Image outbImg = outBadgeGo.GetComponent<Image>();
            outbImg.color = new Color(0f, 0.94f, 1f, 1f);

            GameObject outTextGo = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            outTextGo.transform.SetParent(outBadgeGo.transform, false);
            TextMeshProUGUI outTxt = outTextGo.GetComponent<TextMeshProUGUI>();
            outTxt.text = ">>\n[OUT]";
            outTxt.fontSize = 11;
            outTxt.fontStyle = FontStyles.Bold;
            outTxt.alignment = TextAlignmentOptions.Center;
            outTxt.color = Color.black;

            // Grid Container (416 x 416)
            GameObject gridContGo = new GameObject("GridContainer", typeof(RectTransform));
            gridContGo.transform.SetParent(gridBayGo.transform, false);
            RectTransform gridContRect = gridContGo.GetComponent<RectTransform>();
            gridContRect.anchorMin = new Vector2(0.5f, 0.5f);
            gridContRect.anchorMax = new Vector2(0.5f, 0.5f);
            gridContRect.pivot = new Vector2(0.5f, 0.5f);
            gridContRect.anchoredPosition = new Vector2(0, -8);
            gridContRect.sizeDelta = new Vector2(416, 416);

            // ------------------------------------------
            // BAY 2 (RIGHT): Telemetry Bay (350 x 480)
            // ------------------------------------------
            GameObject telemBayGo = new GameObject("TelemetryBay", typeof(RectTransform), typeof(Image));
            telemBayGo.transform.SetParent(contentGo.transform, false);
            RectTransform telemBayRect = telemBayGo.GetComponent<RectTransform>();
            telemBayRect.anchorMin = new Vector2(1f, 0f);
            telemBayRect.anchorMax = new Vector2(1f, 1f);
            telemBayRect.pivot = new Vector2(1f, 0.5f);
            telemBayRect.anchoredPosition = new Vector2(0, 0);
            telemBayRect.sizeDelta = new Vector2(350, 0);
            Image telemBayImg = telemBayGo.GetComponent<Image>();
            telemBayImg.color = new Color(0.045f, 0.07f, 0.10f, 0.95f);
            Outline tBayOutline = telemBayGo.AddComponent<Outline>();
            tBayOutline.effectColor = new Color(0.18f, 0.28f, 0.38f, 1f);
            tBayOutline.effectDistance = new Vector2(1, -1);

            // Telemetry Bay Title
            GameObject tBayTitleGo = new GameObject("BayTitle", typeof(RectTransform), typeof(TextMeshProUGUI));
            tBayTitleGo.transform.SetParent(telemBayGo.transform, false);
            RectTransform tbtRect = tBayTitleGo.GetComponent<RectTransform>();
            tbtRect.anchorMin = new Vector2(0f, 1f);
            tbtRect.anchorMax = new Vector2(1f, 1f);
            tbtRect.pivot = new Vector2(0.5f, 1f);
            tbtRect.anchoredPosition = new Vector2(0, -12);
            tbtRect.sizeDelta = new Vector2(-20, 24);
            TextMeshProUGUI tbtText = tBayTitleGo.GetComponent<TextMeshProUGUI>();
            tbtText.text = "[ GRID TELEMETRY & BREAKER ]";
            tbtText.fontSize = 13;
            tbtText.fontStyle = FontStyles.Bold;
            tbtText.alignment = TextAlignmentOptions.Center;
            tbtText.color = new Color(0.45f, 0.75f, 0.95f, 1f);

            // Status Card Container
            GameObject statusCardGo = new GameObject("StatusCard", typeof(RectTransform), typeof(Image));
            statusCardGo.transform.SetParent(telemBayGo.transform, false);
            RectTransform scRect = statusCardGo.GetComponent<RectTransform>();
            scRect.anchorMin = new Vector2(0f, 1f);
            scRect.anchorMax = new Vector2(1f, 1f);
            scRect.pivot = new Vector2(0.5f, 1f);
            scRect.anchoredPosition = new Vector2(0, -42);
            scRect.sizeDelta = new Vector2(-28, 140);
            Image scImg = statusCardGo.GetComponent<Image>();
            scImg.color = new Color(0.08f, 0.12f, 0.17f, 0.9f);

            // Readout texts inside Status Card
            TextMeshProUGUI circuitStateTxt = CreateTelemetryRow(statusCardGo, 0, "CIRCUIT STATE:", "DISCONNECTED");
            TextMeshProUGUI busPotentialTxt = CreateTelemetryRow(statusCardGo, 1, "BUS POTENTIAL:", "480.0 VAC // 60Hz");
            TextMeshProUGUI nodesEnergizedTxt = CreateTelemetryRow(statusCardGo, 2, "ENERGIZED NODES:", "0 / 16 (0%)");
            TextMeshProUGUI efficiencyTxt = CreateTelemetryRow(statusCardGo, 3, "GRID EFFICIENCY:", "0% NOMINAL");

            // Breaker Lockout Section
            GameObject breakerCardGo = new GameObject("BreakerCard", typeof(RectTransform), typeof(Image));
            breakerCardGo.transform.SetParent(telemBayGo.transform, false);
            RectTransform bcRect = breakerCardGo.GetComponent<RectTransform>();
            bcRect.anchorMin = new Vector2(0f, 1f);
            bcRect.anchorMax = new Vector2(1f, 1f);
            bcRect.pivot = new Vector2(0.5f, 1f);
            bcRect.anchoredPosition = new Vector2(0, -192);
            bcRect.sizeDelta = new Vector2(-28, 76);
            Image bcImg = breakerCardGo.GetComponent<Image>();
            bcImg.color = new Color(0.08f, 0.12f, 0.17f, 0.9f);

            TextMeshProUGUI breakerStatusTxt = CreateTelemetryRow(breakerCardGo, 0, "RELAY BREAKER:", "<color=#00FF7F>ARMED // READY</color>");

            // Progress Bar Frame for Breaker Cooldown
            GameObject barFrameGo = new GameObject("CooldownBarFrame", typeof(RectTransform), typeof(Image));
            barFrameGo.transform.SetParent(breakerCardGo.transform, false);
            RectTransform bfRect = barFrameGo.GetComponent<RectTransform>();
            bfRect.anchorMin = new Vector2(0f, 0f);
            bfRect.anchorMax = new Vector2(1f, 0f);
            bfRect.pivot = new Vector2(0.5f, 0f);
            bfRect.anchoredPosition = new Vector2(0, 10);
            bfRect.sizeDelta = new Vector2(-20, 20);
            Image bfImg = barFrameGo.GetComponent<Image>();
            bfImg.color = new Color(0.03f, 0.05f, 0.08f, 1f);

            GameObject barFillGo = new GameObject("BarFill", typeof(RectTransform), typeof(Image));
            barFillGo.transform.SetParent(barFrameGo.transform, false);
            RectTransform bfillRect = barFillGo.GetComponent<RectTransform>();
            bfillRect.anchorMin = Vector2.zero;
            bfillRect.anchorMax = Vector2.one;
            bfillRect.offsetMin = new Vector2(2, 2);
            bfillRect.offsetMax = new Vector2(-2, -2);
            Image barFillImg = barFillGo.GetComponent<Image>();
            barFillImg.type = Image.Type.Filled;
            barFillImg.fillMethod = Image.FillMethod.Horizontal;
            barFillImg.fillOrigin = (int)Image.OriginHorizontal.Left;
            barFillImg.fillAmount = 1f;
            barFillImg.color = new Color(0f, 1f, 0.5f, 1f);

            GameObject barTextGo = new GameObject("BarText", typeof(RectTransform), typeof(TextMeshProUGUI));
            barTextGo.transform.SetParent(barFrameGo.transform, false);
            RectTransform btRect = barTextGo.GetComponent<RectTransform>();
            btRect.anchorMin = Vector2.zero;
            btRect.anchorMax = Vector2.one;
            btRect.offsetMin = Vector2.zero;
            btRect.offsetMax = Vector2.zero;
            TextMeshProUGUI barTxt = barTextGo.GetComponent<TextMeshProUGUI>();
            barTxt.text = "NORMAL OPERATING STATE";
            barTxt.fontSize = 10;
            barTxt.fontStyle = FontStyles.Bold;
            barTxt.alignment = TextAlignmentOptions.Center;
            barTxt.color = Color.white;

            // Legend Section (Conduit Schematics)
            GameObject legendCardGo = new GameObject("LegendCard", typeof(RectTransform), typeof(Image));
            legendCardGo.transform.SetParent(telemBayGo.transform, false);
            RectTransform lcRect = legendCardGo.GetComponent<RectTransform>();
            lcRect.anchorMin = new Vector2(0f, 1f);
            lcRect.anchorMax = new Vector2(1f, 1f);
            lcRect.pivot = new Vector2(0.5f, 1f);
            lcRect.anchoredPosition = new Vector2(0, -278);
            lcRect.sizeDelta = new Vector2(-28, 86);
            Image lcImg = legendCardGo.GetComponent<Image>();
            lcImg.color = new Color(0.08f, 0.12f, 0.17f, 0.9f);

            GameObject legendTextGo = new GameObject("LegendText", typeof(RectTransform), typeof(TextMeshProUGUI));
            legendTextGo.transform.SetParent(legendCardGo.transform, false);
            RectTransform ltRect = legendTextGo.GetComponent<RectTransform>();
            ltRect.anchorMin = Vector2.zero;
            ltRect.anchorMax = Vector2.one;
            ltRect.offsetMin = new Vector2(10, 6);
            ltRect.offsetMax = new Vector2(-10, -6);
            TextMeshProUGUI legTxt = legendTextGo.GetComponent<TextMeshProUGUI>();
            legTxt.text = "<b>SCHEMATIC LEGEND:</b>\n" +
                          "<color=#00F0FF>[│] STRAIGHT</color>    <color=#00F0FF>[└] CORNER (90°)</color>\n" +
                          "<color=#00F0FF>[┴] TEE (3-WAY)</color>  <color=#00F0FF>[┼] CROSS (4-WAY)</color>\n" +
                          "<color=#FF3333><b>[!] OVERLOAD CAPACITOR (TRIPS BREAKER)</b></color>";
            legTxt.fontSize = 11;
            legTxt.lineSpacing = 12f;
            legTxt.color = new Color(0.85f, 0.9f, 0.95f, 1f);

            // Controls Guideline Plate
            GameObject controlsCardGo = new GameObject("ControlsCard", typeof(RectTransform), typeof(Image));
            controlsCardGo.transform.SetParent(telemBayGo.transform, false);
            RectTransform ccRect = controlsCardGo.GetComponent<RectTransform>();
            ccRect.anchorMin = new Vector2(0f, 0f);
            ccRect.anchorMax = new Vector2(1f, 0f);
            ccRect.pivot = new Vector2(0.5f, 0f);
            ccRect.anchoredPosition = new Vector2(0, 12);
            ccRect.sizeDelta = new Vector2(-28, 84);
            Image ccImg = controlsCardGo.GetComponent<Image>();
            ccImg.color = new Color(0.06f, 0.09f, 0.13f, 1f);

            GameObject ctrlTextGo = new GameObject("CtrlText", typeof(RectTransform), typeof(TextMeshProUGUI));
            ctrlTextGo.transform.SetParent(controlsCardGo.transform, false);
            RectTransform ctRect = ctrlTextGo.GetComponent<RectTransform>();
            ctRect.anchorMin = Vector2.zero;
            ctRect.anchorMax = Vector2.one;
            ctRect.offsetMin = new Vector2(10, 6);
            ctRect.offsetMax = new Vector2(-10, -6);
            TextMeshProUGUI ctrlTxt = ctrlTextGo.GetComponent<TextMeshProUGUI>();
            ctrlTxt.text = "<b>ACTUATOR CONTROLS:</b>\n" +
                           "• <b>[L-CLICK / SPACE / E]</b>: Rotate Conduit 90° CW\n" +
                           "• <b>[R-CLICK / Q]</b>: Rotate Conduit 90° CCW\n" +
                           "• <b>[ARROWS / WASD]</b>: Navigate Grid Matrix\n" +
                           "• <b>[ESC]</b>: Emergency Disengage & Close";
            ctrlTxt.fontSize = 10.5f;
            ctrlTxt.lineSpacing = 10f;
            ctrlTxt.color = new Color(0.7f, 0.82f, 0.92f, 1f);

            // ==========================================
            // BUILD 4x4 CELL MATRIX
            // ==========================================
            PowerGridTaskUI ui = root.AddComponent<PowerGridTaskUI>();
            ui.rootPanel = root;
            ui.terminalBox = boxRect;
            ui.headerText = headerText;
            ui.instructionText = instText;
            ui.closeButton = closeBtn;

            ui.gridBay = gridBayRect;
            ui.gridContainer = gridContRect;
            ui.inputTerminalBadge = inbImg;
            ui.outputTerminalBadge = outbImg;

            ui.circuitStateText = circuitStateTxt;
            ui.busPotentialText = busPotentialTxt;
            ui.nodesEnergizedText = nodesEnergizedTxt;
            ui.efficiencyText = efficiencyTxt;
            ui.breakerStatusText = breakerStatusTxt;
            ui.breakerCooldownFill = barFillImg;
            ui.breakerCooldownText = barTxt;
            ui.audioSource = root.GetComponent<AudioSource>();

            for (int r = 0; r < GridRows; r++)
            {
                for (int c = 0; c < GridCols; c++)
                {
                    GridCell cell = CreateCellGameObject(gridContRect, r, c, ui);
                    ui.grid[r, c] = cell;
                }
            }

            ui.BindAllButtons();

            root.SetActive(false);
            return ui;
        }

        private static TextMeshProUGUI CreateTelemetryRow(GameObject parent, int rowIndex, string label, string defaultValue)
        {
            GameObject rowGo = new GameObject($"Row_{rowIndex}", typeof(RectTransform));
            rowGo.transform.SetParent(parent.transform, false);
            RectTransform rowRect = rowGo.GetComponent<RectTransform>();
            rowRect.anchorMin = new Vector2(0f, 1f);
            rowRect.anchorMax = new Vector2(1f, 1f);
            rowRect.pivot = new Vector2(0.5f, 1f);
            rowRect.anchoredPosition = new Vector2(0, -10 - (rowIndex * 30));
            rowRect.sizeDelta = new Vector2(-20, 24);

            GameObject lblGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            lblGo.transform.SetParent(rowGo.transform, false);
            RectTransform lblRect = lblGo.GetComponent<RectTransform>();
            lblRect.anchorMin = new Vector2(0f, 0f);
            lblRect.anchorMax = new Vector2(0.48f, 1f);
            lblRect.offsetMin = Vector2.zero;
            lblRect.offsetMax = Vector2.zero;
            TextMeshProUGUI lblTxt = lblGo.GetComponent<TextMeshProUGUI>();
            lblTxt.text = label;
            lblTxt.fontSize = 11;
            lblTxt.fontStyle = FontStyles.Bold;
            lblTxt.alignment = TextAlignmentOptions.MidlineLeft;
            lblTxt.color = new Color(0.6f, 0.72f, 0.85f, 1f);

            GameObject valGo = new GameObject("Value", typeof(RectTransform), typeof(TextMeshProUGUI));
            valGo.transform.SetParent(rowGo.transform, false);
            RectTransform valRect = valGo.GetComponent<RectTransform>();
            valRect.anchorMin = new Vector2(0.48f, 0f);
            valRect.anchorMax = new Vector2(1f, 1f);
            valRect.offsetMin = Vector2.zero;
            valRect.offsetMax = Vector2.zero;
            TextMeshProUGUI valTxt = valGo.GetComponent<TextMeshProUGUI>();
            valTxt.text = defaultValue;
            valTxt.fontSize = 11.5f;
            valTxt.fontStyle = FontStyles.Bold;
            valTxt.alignment = TextAlignmentOptions.MidlineRight;
            valTxt.color = Color.white;

            return valTxt;
        }

        private static GridCell CreateCellGameObject(RectTransform parent, int r, int c, PowerGridTaskUI parentUI)
        {
            GridCell cell = new GridCell();
            cell.row = r;
            cell.col = c;

            float posX = -156f + (c * 104f);
            float posY = 156f - (r * 104f);

            GameObject cellGo = new GameObject($"Cell_{r}_{c}", typeof(RectTransform), typeof(Image), typeof(Button));
            cellGo.transform.SetParent(parent, false);
            RectTransform cellRect = cellGo.GetComponent<RectTransform>();
            cellRect.sizeDelta = new Vector2(94, 94);
            cellRect.anchoredPosition = new Vector2(posX, posY);

            Image cellBg = cellGo.GetComponent<Image>();
            cellBg.color = new Color(0.08f, 0.12f, 0.17f, 1f);
            cell.bgImage = cellBg;
            cell.rootGo = cellGo;

            Outline cellOutline = cellGo.AddComponent<Outline>();
            cellOutline.effectColor = new Color(0.18f, 0.26f, 0.36f, 1f);
            cellOutline.effectDistance = new Vector2(1, -1);
            cell.bgOutline = cellOutline;

            // Rotator RectTransform (rotates visually with conduits)
            GameObject rotGo = new GameObject("Rotator", typeof(RectTransform));
            rotGo.transform.SetParent(cellGo.transform, false);
            RectTransform rotRect = rotGo.GetComponent<RectTransform>();
            rotRect.anchorMin = Vector2.zero;
            rotRect.anchorMax = Vector2.one;
            rotRect.offsetMin = Vector2.zero;
            rotRect.offsetMax = Vector2.zero;
            cell.rotatorRect = rotRect;

            // Conduit Arms (Seamless 37px arms connecting cleanly to cell edges!)
            // North Arm
            GameObject nGlow = CreateGlow(rotGo, new Vector2(0, 28.5f), new Vector2(24, 37));
            GameObject nArm = CreateArm(rotGo, new Vector2(0, 28.5f), new Vector2(16, 37));
            cell.armNorth = nArm.GetComponent<Image>();
            cell.armNorthGlow = nGlow.GetComponent<Image>();

            // East Arm
            GameObject eGlow = CreateGlow(rotGo, new Vector2(28.5f, 0), new Vector2(37, 24));
            GameObject eArm = CreateArm(rotGo, new Vector2(28.5f, 0), new Vector2(37, 16));
            cell.armEast = eArm.GetComponent<Image>();
            cell.armEastGlow = eGlow.GetComponent<Image>();

            // South Arm
            GameObject sGlow = CreateGlow(rotGo, new Vector2(0, -28.5f), new Vector2(24, 37));
            GameObject sArm = CreateArm(rotGo, new Vector2(0, -28.5f), new Vector2(16, 37));
            cell.armSouth = sArm.GetComponent<Image>();
            cell.armSouthGlow = sGlow.GetComponent<Image>();

            // West Arm
            GameObject wGlow = CreateGlow(rotGo, new Vector2(-28.5f, 0), new Vector2(37, 24));
            GameObject wArm = CreateArm(rotGo, new Vector2(-28.5f, 0), new Vector2(37, 16));
            cell.armWest = wArm.GetComponent<Image>();
            cell.armWestGlow = wGlow.GetComponent<Image>();

            // Center Hub
            GameObject hubGlow = CreateGlow(rotGo, Vector2.zero, new Vector2(32, 32));
            GameObject hub = CreateArm(rotGo, Vector2.zero, new Vector2(22, 22));
            cell.centerHub = hub.GetComponent<Image>();
            cell.centerHubGlow = hubGlow.GetComponent<Image>();

            // Overload Badge (Parented to cellGo so it stays ALWAYS UPRIGHT!)
            GameObject oBadgeGo = new GameObject("OverloadBadge", typeof(RectTransform), typeof(Image));
            oBadgeGo.transform.SetParent(cellGo.transform, false);
            RectTransform obRect = oBadgeGo.GetComponent<RectTransform>();
            obRect.anchorMin = new Vector2(1f, 1f);
            obRect.anchorMax = new Vector2(1f, 1f);
            obRect.pivot = new Vector2(1f, 1f);
            obRect.anchoredPosition = new Vector2(-4, -4);
            obRect.sizeDelta = new Vector2(28, 18);
            Image obImg = oBadgeGo.GetComponent<Image>();
            obImg.color = new Color(0.40f, 0.08f, 0.10f, 0.95f);
            obImg.raycastTarget = false;
            Outline obOutline = oBadgeGo.AddComponent<Outline>();
            obOutline.effectColor = new Color(1f, 0.25f, 0.25f, 1f);
            obOutline.effectDistance = new Vector2(1, -1);

            GameObject oIconGo = new GameObject("Icon", typeof(RectTransform), typeof(TextMeshProUGUI));
            oIconGo.transform.SetParent(oBadgeGo.transform, false);
            RectTransform oRect = oIconGo.GetComponent<RectTransform>();
            oRect.anchorMin = Vector2.zero;
            oRect.anchorMax = Vector2.one;
            oRect.offsetMin = Vector2.zero;
            oRect.offsetMax = Vector2.zero;
            TextMeshProUGUI oTxt = oIconGo.GetComponent<TextMeshProUGUI>();
            oTxt.text = "!";
            oTxt.fontSize = 13;
            oTxt.fontStyle = FontStyles.Bold;
            oTxt.alignment = TextAlignmentOptions.Center;
            oTxt.color = new Color(1f, 0.25f, 0.25f, 1f);
            oTxt.raycastTarget = false;

            oBadgeGo.SetActive(false);
            cell.overloadBadgeGo = oBadgeGo;
            cell.overloadIcon = oTxt;

            // Selection Reticle: Hollow 3px border frame (conduits stay fully visible!)
            GameObject selGo = new GameObject("SelectionReticle", typeof(RectTransform));
            selGo.transform.SetParent(cellGo.transform, false);
            RectTransform selRect = selGo.GetComponent<RectTransform>();
            selRect.anchorMin = Vector2.zero;
            selRect.anchorMax = Vector2.one;
            selRect.offsetMin = new Vector2(-2, -2);
            selRect.offsetMax = new Vector2(2, 2);

            Color selColor = new Color(0f, 0.94f, 1f, 1f);
            CreateBorderEdge(selGo, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0, 0), new Vector2(0, 3), selColor); // Top
            CreateBorderEdge(selGo, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 0), new Vector2(0, 3), selColor); // Bottom
            CreateBorderEdge(selGo, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(0, 0), new Vector2(3, 0), selColor); // Left
            CreateBorderEdge(selGo, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(0, 0), new Vector2(3, 0), selColor); // Right

            selGo.SetActive(false);
            cell.selectionReticleGo = selGo;

            // Wire Click Event (Left = CW, Right = CCW)
            EventTrigger trigger = cellGo.AddComponent<EventTrigger>();
            
            EventTrigger.Entry clickEntry = new EventTrigger.Entry();
            clickEntry.eventID = EventTriggerType.PointerClick;
            clickEntry.callback.AddListener((data) =>
            {
                PointerEventData pData = (PointerEventData)data;
                bool cw = (pData.button != PointerEventData.InputButton.Right);
                parentUI.OnCellClicked(r, c, cw);
            });
            trigger.triggers.Add(clickEntry);

            EventTrigger.Entry enterEntry = new EventTrigger.Entry();
            enterEntry.eventID = EventTriggerType.PointerEnter;
            enterEntry.callback.AddListener((data) =>
            {
                parentUI.selectedRow = r;
                parentUI.selectedCol = c;
                parentUI.UpdateSelectionHighlights();
            });
            trigger.triggers.Add(enterEntry);

            return cell;
        }

        private static void CreateBorderEdge(GameObject parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 pos, Vector2 size, Color color)
        {
            GameObject edge = new GameObject("Edge", typeof(RectTransform), typeof(Image));
            edge.transform.SetParent(parent.transform, false);
            RectTransform r = edge.GetComponent<RectTransform>();
            r.anchorMin = anchorMin;
            r.anchorMax = anchorMax;
            r.pivot = pivot;
            r.anchoredPosition = pos;
            r.sizeDelta = size;
            Image img = edge.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
        }

        private static GameObject CreateArm(GameObject parent, Vector2 pos, Vector2 size)
        {
            GameObject arm = new GameObject("Arm", typeof(RectTransform), typeof(Image));
            arm.transform.SetParent(parent.transform, false);
            RectTransform r = arm.GetComponent<RectTransform>();
            r.sizeDelta = size;
            r.anchoredPosition = pos;
            Image img = arm.GetComponent<Image>();
            img.color = new Color(0.20f, 0.28f, 0.38f, 1f);
            img.raycastTarget = false;
            return arm;
        }

        private static GameObject CreateGlow(GameObject parent, Vector2 pos, Vector2 size)
        {
            GameObject glow = new GameObject("Glow", typeof(RectTransform), typeof(Image));
            glow.transform.SetParent(parent.transform, false);
            RectTransform r = glow.GetComponent<RectTransform>();
            r.sizeDelta = size;
            r.anchoredPosition = pos;
            Image img = glow.GetComponent<Image>();
            img.color = Color.clear;
            img.raycastTarget = false;
            return glow;
        }

        #endregion
    }
}
