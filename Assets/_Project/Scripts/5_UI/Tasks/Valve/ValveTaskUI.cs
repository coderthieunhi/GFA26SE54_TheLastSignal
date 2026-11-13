using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using TheLastSignal.Entities.Player;
using TheLastSignal.Entities.Environment;

namespace TheLastSignal.UI.Tasks
{
    /// <summary>
    /// Upgraded Sci-Fi Brutalist Valve Balancing Minigame.
    /// Features:
    /// - Symmetrical Twin-Bay Architecture: Left Pressure Telemetry Bay and Right Actuator Bay
    /// - High-visibility glowing green safe zone dial with needle constrained inside dial boundary
    /// - Clean, non-overlapping 2-line telemetry readouts and control guidance plates
    /// - Generously spaced bottom pressure stabilization bay with distinct margins
    /// - Direct-to-listener & camera audio playback with punchy mechanical ratchet synthesis
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class ValveTaskUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private static ValveTaskUI _instance;
        public static ValveTaskUI Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = UnityEngine.Object.FindAnyObjectByType<ValveTaskUI>(FindObjectsInactive.Include);
                }
                return _instance;
            }
            private set => _instance = value;
        }

        [Header("UI Panels")]
        [SerializeField] private GameObject rootPanel;
        [SerializeField] private Image masterStatusLight;
        [SerializeField] private TextMeshProUGUI headerText;
        [SerializeField] private TextMeshProUGUI instructionText;
        [SerializeField] private TextMeshProUGUI pressureReadoutText;
        [SerializeField] private Button closeButton;

        [Header("Gauge & Needle")]
        [SerializeField] private Image gaugeDialImage;
        [SerializeField] private RectTransform gaugeNeedle;
        [SerializeField] private Image stabilizationBar;
        [SerializeField] private RectTransform stabilizationBarFrame;
        [SerializeField] private TextMeshProUGUI stabilizationPercentText;

        [Header("Valve Wheel")]
        [SerializeField] private RectTransform valveWheel;

        [Header("Tuning Parameters")]
        [SerializeField, Range(0.5f, 5f)] private float requiredHoldSeconds = 2.5f;
        [SerializeField] private float minSafePressure = 50f;
        [SerializeField] private float maxSafePressure = 88f;
        [SerializeField] private float pressureDecayRate = 16f;
        [SerializeField] private float valveSensitivity = 0.25f;

        [Header("Audio")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip valveTurnSfx;
        [SerializeField] private AudioClip successSfx;

        private AudioClip proceduralTurnClip;
        private AudioClip proceduralCompleteClip;
        private float accumulatedAngleForSfx = 0f;
        private const float AnglePerRatchetClick = 10f;

        private Canvas canvas;
        private PlayerController activePlayer;
        private TaskLocation activeTaskLocation;

        private float currentPressure = 15f;
        private float holdTimer = 0f;
        private bool isFinished = false;
        private bool isDraggingValve = false;
        private bool isKeyboardTurning = false;
        private float lastActiveTurnTime = 0f;
        private float previousMouseAngle = 0f;
        private float wheelVisualRotation = 0f;

        // Needle rotation angles corresponding to 0 PSI and 100 PSI on GaugeDial
        private const float NeedleMinAngle = 135f;
        private const float NeedleMaxAngle = -135f;

        public bool IsOpen => rootPanel != null && rootPanel.activeSelf;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;

            canvas = GetComponentInParent<Canvas>();

            if (closeButton != null)
            {
                closeButton.onClick.RemoveAllListeners();
                closeButton.onClick.AddListener(CloseMinigame);
            }

            EnsureAudioSources();
            GenerateProceduralAudio();
            EnsureUpgradedVisuals();

            if (rootPanel != null)
            {
                rootPanel.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }

        private void Update()
        {
            if (!IsOpen) return;

            // Emergency cancel on ESC
            if (UnityEngine.InputSystem.Keyboard.current != null &&
                UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                CloseMinigame();
                return;
            }

            if (isFinished) return;

            // Keyboard steering (A / D or Arrow keys)
            HandleKeyboardInput();

            // Check if player is actively rotating the valve (moving mouse while dragging OR holding keyboard keys)
            bool isActivelyRotating = isKeyboardTurning || (isDraggingValve && (Time.time - lastActiveTurnTime <= 0.12f));

            // Passive decay towards resting pressure (10 PSI) when player stops rotating/holding
            if (!isActivelyRotating && pressureDecayRate > 0f)
            {
                float prevPressure = currentPressure;
                currentPressure = Mathf.MoveTowards(currentPressure, 10f, pressureDecayRate * Time.deltaTime);

                // Naturally rotate the valve wheel backwards as pipe pressure bleeds
                float pressureLost = prevPressure - currentPressure;
                if (valveWheel != null && pressureLost > 0f && !isDraggingValve)
                {
                    float rotSlip = pressureLost / valveSensitivity;
                    wheelVisualRotation += rotSlip;
                    valveWheel.localEulerAngles = new Vector3(0, 0, wheelVisualRotation);
                }
            }

            currentPressure = Mathf.Clamp(currentPressure, 0f, 100f);

            bool inSafeZone = currentPressure >= minSafePressure && currentPressure <= maxSafePressure;

            if (inSafeZone)
            {
                holdTimer += Time.deltaTime;
                if (masterStatusLight != null)
                {
                    masterStatusLight.color = Color.Lerp(new Color(1f, 0.8f, 0f), new Color(0f, 1f, 0.4f), holdTimer / requiredHoldSeconds);
                }
            }
            else
            {
                holdTimer = Mathf.Max(0f, holdTimer - Time.deltaTime * 1.5f);
                if (masterStatusLight != null)
                {
                    masterStatusLight.color = currentPressure > maxSafePressure ? new Color(1f, 0.2f, 0.2f) : new Color(0.9f, 0.5f, 0.1f);
                }
            }

            UpdateGaugeVisuals(inSafeZone);

            if (holdTimer >= requiredHoldSeconds && !isFinished)
            {
                StartCoroutine(CompleteRoutine());
            }
        }

        private void HandleKeyboardInput()
        {
            var kb = UnityEngine.InputSystem.Keyboard.current;
            isKeyboardTurning = false;
            if (kb == null) return;

            // Immediate audio click on initial key down
            if (kb.dKey.wasPressedThisFrame || kb.rightArrowKey.wasPressedThisFrame ||
                kb.aKey.wasPressedThisFrame || kb.leftArrowKey.wasPressedThisFrame)
            {
                PlayValveTurnSound();
                accumulatedAngleForSfx = 0f;
            }

            float keyDelta = 0f;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) keyDelta += 1f;
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) keyDelta -= 1f;

            if (Mathf.Abs(keyDelta) > 0.01f)
            {
                isKeyboardTurning = true;
                lastActiveTurnTime = Time.time;
                float rotSpeed = 160f; // degrees per second
                float rotDelta = -keyDelta * rotSpeed * Time.deltaTime;

                // Synchronize needle speed with wheel speed: exact same 0.25 PSI/deg scaling as mouse drag
                float pressureDelta = keyDelta * (rotSpeed * valveSensitivity) * Time.deltaTime;
                currentPressure += pressureDelta;
                currentPressure = Mathf.Clamp(currentPressure, 0f, 100f);

                wheelVisualRotation += rotDelta;
                if (valveWheel != null)
                {
                    valveWheel.localEulerAngles = new Vector3(0, 0, wheelVisualRotation);
                }

                accumulatedAngleForSfx += Mathf.Abs(rotDelta);
                if (accumulatedAngleForSfx >= AnglePerRatchetClick)
                {
                    PlayValveTurnSound();
                    accumulatedAngleForSfx %= AnglePerRatchetClick;
                }
            }
        }

        private void UpdateGaugeVisuals(bool inSafeZone)
        {
            float t = currentPressure / 100f;
            float needleAngle = Mathf.Lerp(NeedleMinAngle, NeedleMaxAngle, t);

            if (gaugeNeedle != null)
            {
                gaugeNeedle.localEulerAngles = new Vector3(0, 0, needleAngle);
            }

            if (pressureReadoutText != null)
            {
                string statusColor = inSafeZone ? "#00FF7F" : (currentPressure > maxSafePressure ? "#FF3B30" : "#FFA500");
                pressureReadoutText.text = $"<color={statusColor}><size=20><b>{currentPressure:00.0} PSI</b></size></color>\n<size=10><color=#7A96B0>NOMINAL: 50.0 — 88.0 PSI</color></size>";
            }

            float progress01 = Mathf.Clamp01(holdTimer / requiredHoldSeconds);
            if (stabilizationBar != null)
            {
                stabilizationBar.fillAmount = progress01;
                stabilizationBar.color = inSafeZone ? new Color(0f, 0.95f, 0.45f) : new Color(0.95f, 0.50f, 0.10f);
            }

            if (stabilizationPercentText != null)
            {
                if (inSafeZone)
                {
                    int pct = Mathf.RoundToInt(progress01 * 100f);
                    stabilizationPercentText.text = $"<color=#00FF7F><b>>>> IN SAFE ZONE — HOLDING PRESSURE <<<</b></color>  <size=14><color=#A0FFA0>({pct}%)</color></size>";
                }
                else if (currentPressure < minSafePressure)
                {
                    stabilizationPercentText.text = $"<color=#FFA500><b>LOW PRESSURE</b></color>  <size=13><color=#CCCCCC>— Turn Valve Right / Press [D] to Increase Value</color></size>";
                }
                else
                {
                    stabilizationPercentText.text = $"<color=#FF3B30><b>HIGH PRESSURE</b></color>  <size=13><color=#CCCCCC>— Turn Valve Left / Press [A] to Decrease Value</color></size>";
                }
            }
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (isFinished || valveWheel == null) return;
            isDraggingValve = true;
            previousMouseAngle = GetMouseAngleOnValve(eventData.position);
            lastActiveTurnTime = Time.time;
            PlayValveTurnSound();
            accumulatedAngleForSfx = 0f;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (isFinished || !isDraggingValve || valveWheel == null) return;

            float currentAngle = GetMouseAngleOnValve(eventData.position);
            float angleDelta = Mathf.DeltaAngle(previousMouseAngle, currentAngle);

            if (Mathf.Abs(angleDelta) > 0.05f)
            {
                lastActiveTurnTime = Time.time;
            }

            float pressureDelta = -angleDelta * valveSensitivity;
            currentPressure += pressureDelta;
            currentPressure = Mathf.Clamp(currentPressure, 0f, 100f);

            wheelVisualRotation += angleDelta;
            valveWheel.localEulerAngles = new Vector3(0, 0, wheelVisualRotation);

            accumulatedAngleForSfx += Mathf.Abs(angleDelta);
            if (accumulatedAngleForSfx >= AnglePerRatchetClick)
            {
                PlayValveTurnSound();
                accumulatedAngleForSfx %= AnglePerRatchetClick;
            }

            previousMouseAngle = currentAngle;
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            isDraggingValve = false;
        }

        private float GetMouseAngleOnValve(Vector2 screenPos)
        {
            Camera cam = canvas != null && canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            RectTransform parentRect = valveWheel != null ? valveWheel.parent as RectTransform : null;
            if (parentRect != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, screenPos, cam, out Vector2 localPoint))
            {
                Vector2 center = valveWheel.anchoredPosition;
                Vector2 dir = localPoint - center;
                return Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            }
            else if (valveWheel != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(valveWheel, screenPos, cam, out Vector2 fallbackPoint))
            {
                return Mathf.Atan2(fallbackPoint.y, fallbackPoint.x) * Mathf.Rad2Deg;
            }
            return 0f;
        }

        public void Open(PlayerController player, TaskLocation station)
        {
            if (player == null)
            {
                player = PlayerController.Local ?? UnityEngine.Object.FindAnyObjectByType<PlayerController>();
            }

            activePlayer = player;
            activeTaskLocation = station;
            currentPressure = 15f;
            holdTimer = 0f;
            isFinished = false;
            isDraggingValve = false;
            isKeyboardTurning = false;
            lastActiveTurnTime = 0f;
            accumulatedAngleForSfx = 0f;
            if (pressureDecayRate <= 0f) pressureDecayRate = 16f;
            valveSensitivity = 0.25f;

            if (canvas == null)
            {
                canvas = GetComponentInParent<Canvas>();
            }

            EnsureAudioSources();
            GenerateProceduralAudio();
            EnsureUpgradedVisuals();

            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }

            if (masterStatusLight != null)
            {
                masterStatusLight.color = new Color(0.9f, 0.2f, 0.2f);
            }

            if (headerText != null && station != null)
            {
                string pipelineInfo = string.Empty;
                if (station.TryGetComponent(out StationRepair repair))
                {
                    int step = repair.CompletedSteps + 1;
                    int target = repair.GetTargetStepsForPlayer(player);
                    pipelineInfo = $" [STAGE {step}/{target}]";
                }
                headerText.text = $"PRESSURE REGULATOR TERMINAL // {station.StationDisplayName.ToUpper()}{pipelineInfo}";
            }

            if (instructionText != null)
            {
                instructionText.text = "Rotate valve [Drag Wheel or A / D keys] to keep needle in <color=#00FF7F>GREEN ZONE</color>";
            }

            if (activePlayer != null)
            {
                activePlayer.SetControlEnabled(false);
            }

            if (closeButton != null)
            {
                closeButton.onClick.RemoveAllListeners();
                closeButton.onClick.AddListener(CloseMinigame);
            }

            if (rootPanel != null)
            {
                rootPanel.SetActive(true);
            }
        }

        /// <summary>
        /// Automatically upgrades any existing or serialized scene instance to the balanced, non-overlapping twin-bay layout.
        /// </summary>
        public void EnsureUpgradedVisuals()
        {
            if (rootPanel != null)
            {
                Canvas panelCanvas = rootPanel.GetComponent<Canvas>();
                if (panelCanvas == null) panelCanvas = rootPanel.AddComponent<Canvas>();
                panelCanvas.overrideSorting = true;
                panelCanvas.sortingOrder = 1000;

                if (rootPanel.GetComponent<GraphicRaycaster>() == null)
                {
                    rootPanel.AddComponent<GraphicRaycaster>();
                }
            }

            minSafePressure = 50f;
            maxSafePressure = 88f;
            if (pressureDecayRate <= 0f) pressureDecayRate = 16f;
            valveSensitivity = 0.25f;

            // Remove legacy status lights
            if (masterStatusLight != null)
            {
                masterStatusLight.gameObject.SetActive(false);
            }
            if (rootPanel != null)
            {
                Transform ml1 = rootPanel.transform.Find("TerminalBox/MasterLight");
                if (ml1 != null) ml1.gameObject.SetActive(false);
                Transform ml2 = rootPanel.transform.Find("MasterLight");
                if (ml2 != null) ml2.gameObject.SetActive(false);
            }

            Sprite dialSprite = Resources.Load<Sprite>("UI/Tasks/GaugeDial");
            Sprite needleSprite = Resources.Load<Sprite>("UI/Tasks/GaugeNeedle");
            Sprite valveSprite = Resources.Load<Sprite>("UI/Tasks/ValveWheel");
            Sprite bezelSprite = Resources.Load<Sprite>("UI/Tasks/TerminalBezel");

            Transform terminalBox = null;
            if (rootPanel != null)
            {
                terminalBox = rootPanel.transform.Find("TerminalBox") ?? rootPanel.transform;
            }

            if (terminalBox != null)
            {
                RectTransform tRect = terminalBox.GetComponent<RectTransform>();
                if (tRect != null)
                {
                    tRect.sizeDelta = new Vector2(960, 640);
                    tRect.anchoredPosition = Vector2.zero;
                }

                if (terminalBox.TryGetComponent(out Image boxImg) && bezelSprite != null)
                {
                    boxImg.sprite = bezelSprite;
                    boxImg.color = Color.white;
                }

                // Solid dark sci-fi background chassis backing so world never shows through bezel
                Transform chassisBg = terminalBox.Find("TerminalChassisBackground");
                if (chassisBg == null)
                {
                    GameObject cbgGo = new GameObject("TerminalChassisBackground", typeof(RectTransform), typeof(Image));
                    cbgGo.transform.SetParent(terminalBox, false);
                    cbgGo.transform.SetAsFirstSibling();
                    chassisBg = cbgGo.transform;
                }
                RectTransform cbgRect = chassisBg.GetComponent<RectTransform>();
                cbgRect.anchorMin = Vector2.zero;
                cbgRect.anchorMax = Vector2.one;
                cbgRect.sizeDelta = new Vector2(-16, -16);
                cbgRect.anchoredPosition = Vector2.zero;
                Image cbgImg = chassisBg.GetComponent<Image>();
                cbgImg.color = new Color(0.06f, 0.08f, 0.11f, 1f);
                cbgImg.raycastTarget = false;

                // Header Title positioning
                Transform headerT = terminalBox.Find("HeaderTitle");
                if (headerT != null && headerT.TryGetComponent(out RectTransform hdrRect))
                {
                    hdrRect.anchorMin = new Vector2(0f, 1f);
                    hdrRect.anchorMax = new Vector2(1f, 1f);
                    hdrRect.pivot = new Vector2(0.5f, 1f);
                    hdrRect.anchoredPosition = new Vector2(0, -32);
                    hdrRect.sizeDelta = new Vector2(-120, 36);
                    if (headerT.TryGetComponent(out TextMeshProUGUI hTxt))
                    {
                        headerText = hTxt;
                        headerText.fontSize = 20;
                        headerText.fontStyle = FontStyles.Bold;
                        headerText.alignment = TextAlignmentOptions.Center;
                    }
                }

                // Instruction Subtitle positioning
                Transform instT = terminalBox.Find("InstructionText");
                if (instT != null && instT.TryGetComponent(out RectTransform iRect))
                {
                    iRect.anchorMin = new Vector2(0f, 1f);
                    iRect.anchorMax = new Vector2(1f, 1f);
                    iRect.pivot = new Vector2(0.5f, 1f);
                    iRect.anchoredPosition = new Vector2(0, -68);
                    iRect.sizeDelta = new Vector2(-120, 26);
                    if (instT.TryGetComponent(out TextMeshProUGUI iTxt))
                    {
                        instructionText = iTxt;
                        instructionText.fontSize = 15;
                        instructionText.alignment = TextAlignmentOptions.Center;
                    }
                }

                // Close Button [ESC / X]
                Transform closeT = terminalBox.Find("CloseButton");
                if (closeT != null)
                {
                    if (closeT.TryGetComponent(out RectTransform cRect))
                    {
                        cRect.anchorMin = new Vector2(1f, 1f);
                        cRect.anchorMax = new Vector2(1f, 1f);
                        cRect.pivot = new Vector2(1f, 1f);
                        cRect.anchoredPosition = new Vector2(-36, -30);
                        cRect.sizeDelta = new Vector2(32, 32);
                    }
                    if (closeT.TryGetComponent(out Button cBtn))
                    {
                        closeButton = cBtn;
                        closeButton.onClick.RemoveAllListeners();
                        closeButton.onClick.AddListener(CloseMinigame);
                    }
                    foreach (var gfx in closeT.GetComponentsInChildren<Graphic>(true))
                    {
                        if (gfx.gameObject != closeT.gameObject)
                        {
                            gfx.raycastTarget = false;
                        }
                    }
                }

                // ================= 1. LEFT BAY (PRESSURE TELEMETRY) =================
                Transform leftBay = terminalBox.Find("PressureGaugeBay");
                if (leftBay == null)
                {
                    GameObject lbGo = new GameObject("PressureGaugeBay", typeof(RectTransform), typeof(Image));
                    lbGo.transform.SetParent(terminalBox, false);
                    leftBay = lbGo.transform;
                }
                RectTransform lbRect = leftBay.GetComponent<RectTransform>();
                lbRect.sizeDelta = new Vector2(400, 330);
                lbRect.anchoredPosition = new Vector2(-215, 35);
                Image lbImg = leftBay.GetComponent<Image>();
                lbImg.color = new Color(0.04f, 0.07f, 0.10f, 0.95f);
                lbImg.raycastTarget = false;

                Outline lbOutline = leftBay.GetComponent<Outline>() ?? leftBay.gameObject.AddComponent<Outline>();
                lbOutline.effectColor = new Color(0.18f, 0.28f, 0.40f, 0.85f);
                lbOutline.effectDistance = new Vector2(2, -2);

                // Left Bay Header Label
                Transform lbHeader = leftBay.Find("BayHeader");
                if (lbHeader == null)
                {
                    GameObject lbhGo = new GameObject("BayHeader", typeof(RectTransform), typeof(TextMeshProUGUI));
                    lbhGo.transform.SetParent(leftBay, false);
                    lbHeader = lbhGo.transform;
                }
                RectTransform lbhRect = lbHeader.GetComponent<RectTransform>();
                lbhRect.anchorMin = new Vector2(0f, 1f);
                lbhRect.anchorMax = new Vector2(1f, 1f);
                lbhRect.pivot = new Vector2(0.5f, 1f);
                lbhRect.anchoredPosition = new Vector2(0, -14);
                lbhRect.sizeDelta = new Vector2(-20, 22);
                TextMeshProUGUI lbhText = lbHeader.GetComponent<TextMeshProUGUI>();
                lbhText.text = "[ PRESSURE MONITOR // TELEMETRY ]";
                lbhText.fontSize = 12;
                lbhText.fontStyle = FontStyles.Bold;
                lbhText.alignment = TextAlignmentOptions.Center;
                lbhText.color = new Color(0.40f, 0.80f, 1f, 0.90f);
                lbhText.raycastTarget = false;

                // Reparent GaugeHousing into Left Bay
                Transform housing = null;
                if (gaugeNeedle != null && gaugeNeedle.parent != null)
                {
                    housing = gaugeNeedle.parent;
                }
                else if (gaugeDialImage != null)
                {
                    housing = gaugeDialImage.transform;
                }
                else
                {
                    housing = terminalBox.Find("GaugeHousing") ?? leftBay.Find("GaugeHousing");
                }

                if (housing != null)
                {
                    housing.SetParent(leftBay, false);
                    RectTransform housingRect = housing.GetComponent<RectTransform>();
                    housingRect.anchoredPosition = new Vector2(0, 15);
                    housingRect.sizeDelta = new Vector2(240, 240);

                    if (housing.TryGetComponent(out Image hImg))
                    {
                        gaugeDialImage = hImg;
                        if (dialSprite != null)
                        {
                            hImg.sprite = dialSprite;
                            hImg.color = Color.white;
                        }
                    }

                    // Hide old green square safeZoneArc and old NeedleCap
                    Transform oldArc = housing.Find("SafeZoneArc");
                    if (oldArc != null) oldArc.gameObject.SetActive(false);
                    Transform oldCap = housing.Find("NeedleCap");
                    if (oldCap != null) oldCap.gameObject.SetActive(false);
                }

                // Gauge Needle (Contained cleanly inside 240px dial)
                if (gaugeNeedle != null)
                {
                    if (gaugeNeedle.TryGetComponent(out Image needleImg) && needleSprite != null)
                    {
                        needleImg.sprite = needleSprite;
                        needleImg.color = Color.white;
                    }
                    gaugeNeedle.sizeDelta = new Vector2(28, 136);
                    gaugeNeedle.pivot = new Vector2(0.5f, 0.18f);
                    gaugeNeedle.anchoredPosition = Vector2.zero;
                }

                // Left Bay Readout Plate (Matched identically with Right Bay Control Plate)
                Transform readoutPlate = leftBay.Find("ReadoutPlate");
                if (readoutPlate == null)
                {
                    GameObject rpGo = new GameObject("ReadoutPlate", typeof(RectTransform), typeof(Image));
                    rpGo.transform.SetParent(leftBay, false);
                    readoutPlate = rpGo.transform;
                }
                RectTransform rpRect = readoutPlate.GetComponent<RectTransform>();
                rpRect.anchoredPosition = new Vector2(0, -135);
                rpRect.sizeDelta = new Vector2(320, 42);
                Image rpImg = readoutPlate.GetComponent<Image>();
                rpImg.color = new Color(0.02f, 0.04f, 0.06f, 1f);
                rpImg.raycastTarget = false;
                Outline rpOutline = readoutPlate.GetComponent<Outline>() ?? readoutPlate.gameObject.AddComponent<Outline>();
                rpOutline.effectColor = new Color(0.15f, 0.28f, 0.42f, 0.70f);
                rpOutline.effectDistance = new Vector2(1, -1);

                // Disable separate RangeSubtext to prevent text collision
                Transform rangeT = readoutPlate.Find("RangeSubtext");
                if (rangeT != null) rangeT.gameObject.SetActive(false);

                // Consolidated 2-line pressure readout text
                Transform psiT = housing != null ? housing.Find("PsiReadout") : null;
                if (psiT == null) psiT = readoutPlate.Find("PsiReadout");
                if (psiT == null && pressureReadoutText != null) psiT = pressureReadoutText.transform;
                if (psiT != null)
                {
                    psiT.SetParent(readoutPlate, false);
                    RectTransform psiRect = psiT.GetComponent<RectTransform>();
                    psiRect.anchorMin = Vector2.zero;
                    psiRect.anchorMax = Vector2.one;
                    psiRect.sizeDelta = Vector2.zero;
                    psiRect.anchoredPosition = Vector2.zero;
                    if (psiT.TryGetComponent(out TextMeshProUGUI psiTxt))
                    {
                        pressureReadoutText = psiTxt;
                        pressureReadoutText.alignment = TextAlignmentOptions.Center;
                        pressureReadoutText.lineSpacing = 10;
                        pressureReadoutText.raycastTarget = false;
                    }
                }

                // ================= 2. RIGHT BAY (ACTUATOR // VALVE SERVO) =================
                Transform rightBay = terminalBox.Find("ValveActuatorBay");
                if (rightBay == null)
                {
                    GameObject rbGo = new GameObject("ValveActuatorBay", typeof(RectTransform), typeof(Image));
                    rbGo.transform.SetParent(terminalBox, false);
                    rightBay = rbGo.transform;
                }
                RectTransform rbRect = rightBay.GetComponent<RectTransform>();
                rbRect.sizeDelta = new Vector2(400, 330);
                rbRect.anchoredPosition = new Vector2(215, 35);
                Image rbImg = rightBay.GetComponent<Image>();
                rbImg.color = new Color(0.04f, 0.07f, 0.10f, 0.95f);
                rbImg.raycastTarget = false;

                Outline rbOutline = rightBay.GetComponent<Outline>() ?? rightBay.gameObject.AddComponent<Outline>();
                rbOutline.effectColor = new Color(0.18f, 0.28f, 0.40f, 0.85f);
                rbOutline.effectDistance = new Vector2(2, -2);

                // Remove legacy square WheelWell that cuts through the header text
                Transform legacyWell = rightBay.Find("WheelWell");
                if (legacyWell != null)
                {
                    Destroy(legacyWell.gameObject);
                }

                // Right Bay Header Label
                Transform rbHeader = rightBay.Find("BayHeader");
                if (rbHeader == null)
                {
                    GameObject rbhGo = new GameObject("BayHeader", typeof(RectTransform), typeof(TextMeshProUGUI));
                    rbhGo.transform.SetParent(rightBay, false);
                    rbHeader = rbhGo.transform;
                }
                RectTransform rbhRect = rbHeader.GetComponent<RectTransform>();
                rbhRect.anchorMin = new Vector2(0f, 1f);
                rbhRect.anchorMax = new Vector2(1f, 1f);
                rbhRect.pivot = new Vector2(0.5f, 1f);
                rbhRect.anchoredPosition = new Vector2(0, -14);
                rbhRect.sizeDelta = new Vector2(-20, 22);
                TextMeshProUGUI rbhText = rbHeader.GetComponent<TextMeshProUGUI>();
                rbhText.text = "[ MANUAL ACTUATOR // VALVE SERVO-A ]";
                rbhText.fontSize = 12;
                rbhText.fontStyle = FontStyles.Bold;
                rbhText.alignment = TextAlignmentOptions.Center;
                rbhText.color = new Color(0.40f, 0.80f, 1f, 0.90f);
                rbhText.raycastTarget = false;

                // Reparent ValveWheel into Right Bay (Matched in size & position with GaugeDial)
                Transform wheelT = valveWheel != null ? valveWheel : terminalBox.Find("ValveWheel");
                if (wheelT != null)
                {
                    wheelT.SetParent(rightBay, false);
                    valveWheel = wheelT.GetComponent<RectTransform>();
                    valveWheel.sizeDelta = new Vector2(240, 240);
                    valveWheel.anchoredPosition = new Vector2(0, 15);
                    if (valveWheel.TryGetComponent(out Image wImg))
                    {
                        if (valveSprite != null)
                        {
                            wImg.sprite = valveSprite;
                            wImg.color = Color.white;
                        }
                        wImg.raycastTarget = true;
                    }

                    // Attach dedicated DragProxy directly on ValveWheel to guarantee pointer capture
                    var wheelProxy = valveWheel.GetComponent<ValveWheelDragProxy>() ?? valveWheel.gameObject.AddComponent<ValveWheelDragProxy>();
                    wheelProxy.targetUI = this;

                    // Deactivate legacy spokes or knob children
                    for (int i = 0; i < valveWheel.childCount; i++)
                    {
                        valveWheel.GetChild(i).gameObject.SetActive(false);
                    }
                }

                // Also attach DragProxy to rightBay so dragging near the wheel is captured seamlessly
                if (rightBay != null)
                {
                    var bayProxy = rightBay.GetComponent<ValveWheelDragProxy>() ?? rightBay.gameObject.AddComponent<ValveWheelDragProxy>();
                    bayProxy.targetUI = this;
                }

                // Right Bay Controls Guidance Plate (Exact 320x42 match with Left Bay ReadoutPlate)
                Transform controlPlate = rightBay.Find("ControlPlate");
                if (controlPlate == null)
                {
                    GameObject cpGo = new GameObject("ControlPlate", typeof(RectTransform), typeof(Image));
                    cpGo.transform.SetParent(rightBay, false);
                    controlPlate = cpGo.transform;
                }
                RectTransform cpRect = controlPlate.GetComponent<RectTransform>();
                cpRect.anchoredPosition = new Vector2(0, -135);
                cpRect.sizeDelta = new Vector2(320, 42);
                Image cpImg = controlPlate.GetComponent<Image>();
                cpImg.color = new Color(0.02f, 0.04f, 0.06f, 1f);
                cpImg.raycastTarget = false;
                Outline cpOutline = controlPlate.GetComponent<Outline>() ?? controlPlate.gameObject.AddComponent<Outline>();
                cpOutline.effectColor = new Color(0.15f, 0.28f, 0.42f, 0.70f);
                cpOutline.effectDistance = new Vector2(1, -1);

                // Disable separate ControlSubtext to prevent collision
                Transform ctrlSubT = controlPlate.Find("ControlSubtext");
                if (ctrlSubT != null) ctrlSubT.gameObject.SetActive(false);

                // Consolidated 2-line control guidance text
                Transform ctrlTextT = controlPlate.Find("ControlText");
                if (ctrlTextT == null)
                {
                    GameObject cpTextGo = new GameObject("ControlText", typeof(RectTransform), typeof(TextMeshProUGUI));
                    cpTextGo.transform.SetParent(controlPlate, false);
                    ctrlTextT = cpTextGo.transform;
                }
                RectTransform cptRect = ctrlTextT.GetComponent<RectTransform>();
                cptRect.anchorMin = Vector2.zero;
                cptRect.anchorMax = Vector2.one;
                cptRect.sizeDelta = Vector2.zero;
                cptRect.anchoredPosition = Vector2.zero;
                TextMeshProUGUI cptText = ctrlTextT.GetComponent<TextMeshProUGUI>();
                cptText.text = "◄ <color=#FF8866><b>[A]</b></color> DECREASE   |   INCREASE <color=#66DDFF><b>[D]</b></color> ►\n<size=10><color=#7A96B0>DRAG WHEEL OR USE [A] / [D] KEYS</color></size>";
                cptText.fontSize = 12;
                cptText.lineSpacing = 10;
                cptText.alignment = TextAlignmentOptions.Center;
                cptText.color = new Color(0.75f, 0.85f, 0.95f, 1f);
                cptText.raycastTarget = false;

                // ================= 3. BOTTOM BAY (STABILIZATION SYSTEM) =================
                Transform bottomBay = terminalBox.Find("StabilityBay");
                if (bottomBay == null)
                {
                    GameObject bbGo = new GameObject("StabilityBay", typeof(RectTransform), typeof(Image));
                    bbGo.transform.SetParent(terminalBox, false);
                    bottomBay = bbGo.transform;
                }
                RectTransform bbRect = bottomBay.GetComponent<RectTransform>();
                bbRect.sizeDelta = new Vector2(830, 120);
                bbRect.anchoredPosition = new Vector2(0, -215);
                Image bbImg = bottomBay.GetComponent<Image>();
                bbImg.color = new Color(0.04f, 0.07f, 0.10f, 0.95f);
                bbImg.raycastTarget = false;

                Outline bbOutline = bottomBay.GetComponent<Outline>() ?? bottomBay.gameObject.AddComponent<Outline>();
                bbOutline.effectColor = new Color(0.18f, 0.28f, 0.40f, 0.85f);
                bbOutline.effectDistance = new Vector2(2, -2);

                // Bottom Bay Header Label (Spaced with 13px clearance above progress bar)
                Transform bbHeader = bottomBay.Find("BayHeader");
                if (bbHeader == null)
                {
                    GameObject bbhGo = new GameObject("BayHeader", typeof(RectTransform), typeof(TextMeshProUGUI));
                    bbhGo.transform.SetParent(bottomBay, false);
                    bbHeader = bbhGo.transform;
                }
                RectTransform bbhRect = bbHeader.GetComponent<RectTransform>();
                bbhRect.anchorMin = new Vector2(0f, 1f);
                bbhRect.anchorMax = new Vector2(1f, 1f);
                bbhRect.pivot = new Vector2(0.5f, 1f);
                bbhRect.anchoredPosition = new Vector2(0, -10);
                bbhRect.sizeDelta = new Vector2(-20, 18);
                TextMeshProUGUI bbhText = bbHeader.GetComponent<TextMeshProUGUI>();
                bbhText.text = "PRESSURE STABILIZATION LOCK";
                bbhText.fontSize = 11;
                bbhText.fontStyle = FontStyles.Bold;
                bbhText.alignment = TextAlignmentOptions.Center;
                bbhText.color = new Color(0.65f, 0.78f, 0.90f, 1f);
                bbhText.raycastTarget = false;

                // Reparent StabilizationBarFrame into Bottom Bay
                Transform barFrameT = stabilizationBarFrame != null ? stabilizationBarFrame :
                                     (stabilizationBar != null && stabilizationBar.transform.parent != null ? stabilizationBar.transform.parent : terminalBox.Find("StabilityBarFrame"));
                if (barFrameT != null)
                {
                    barFrameT.SetParent(bottomBay, false);
                    stabilizationBarFrame = barFrameT.GetComponent<RectTransform>();
                    stabilizationBarFrame.anchoredPosition = new Vector2(0, 8);
                    stabilizationBarFrame.sizeDelta = new Vector2(770, 22);

                    if (barFrameT.TryGetComponent(out Image bfImg))
                    {
                        bfImg.color = new Color(0.02f, 0.04f, 0.06f, 1f);
                    }
                    Outline bfOutline = barFrameT.GetComponent<Outline>() ?? barFrameT.gameObject.AddComponent<Outline>();
                    bfOutline.effectColor = new Color(0.20f, 0.30f, 0.40f, 0.80f);
                    bfOutline.effectDistance = new Vector2(1, -1);
                }

                // Reparent stabilizationPercentText into Bottom Bay (Spaced with 15px clearance below progress bar)
                Transform pctT = stabilizationPercentText != null ? stabilizationPercentText.transform : terminalBox.Find("PercentText");
                if (pctT != null)
                {
                    pctT.SetParent(bottomBay, false);
                    if (pctT.TryGetComponent(out TextMeshProUGUI pctTextComp))
                    {
                        stabilizationPercentText = pctTextComp;
                        RectTransform pctRect = stabilizationPercentText.rectTransform;
                        pctRect.anchoredPosition = new Vector2(0, -32);
                        pctRect.sizeDelta = new Vector2(770, 28);
                        stabilizationPercentText.alignment = TextAlignmentOptions.Center;
                        stabilizationPercentText.fontSize = 14;
                        stabilizationPercentText.raycastTarget = false;
                    }
                }
            }
        }

        private IEnumerator CompleteRoutine()
        {
            isFinished = true;
            if (masterStatusLight != null)
            {
                masterStatusLight.color = new Color(0f, 1f, 0.4f);
            }

            if (instructionText != null)
            {
                instructionText.text = "<color=#00FF7F><b>>>> PRESSURE STABILIZED SUCCESSFULLY <<<</b></color>";
            }

            if (stabilizationPercentText != null)
            {
                stabilizationPercentText.text = "<color=#00FF7F><b>>>> PRESSURE STABILIZED SUCCESSFULLY <<<</b></color>";
            }

            EnsureAudioSources();
            if (successSfx == null)
            {
                successSfx = Resources.Load<AudioClip>("Audio/sfx_valve_complete");
            }
            AudioClip doneClip = successSfx ?? proceduralCompleteClip ?? CreateValveCompleteTone();
            if (doneClip != null)
            {
                if (audioSource != null)
                {
                    audioSource.spatialBlend = 0f;
                    audioSource.pitch = 1.0f;
                    audioSource.volume = 0.65f;
                    audioSource.PlayOneShot(doneClip, 0.65f);
                }
                else
                {
                    AudioListener listener = UnityEngine.Object.FindAnyObjectByType<AudioListener>();
                    if (listener != null)
                    {
                        if (!listener.TryGetComponent(out AudioSource listenerSrc))
                        {
                            listenerSrc = listener.gameObject.AddComponent<AudioSource>();
                        }
                        if (listenerSrc != null)
                        {
                            listenerSrc.spatialBlend = 0f;
                            listenerSrc.pitch = 1.0f;
                            listenerSrc.volume = 0.65f;
                            listenerSrc.PlayOneShot(doneClip, 0.65f);
                        }
                    }
                }
            }

            yield return new WaitForSeconds(0.6f);

            if (activeTaskLocation != null)
            {
                activeTaskLocation.OnSubTaskCompleted(activePlayer, this);
            }
            else
            {
                CloseMinigame();
            }
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

            if (rootPanel != null)
            {
                rootPanel.SetActive(false);
            }

            if (activePlayer != null)
            {
                activePlayer.SetControlEnabled(true);
                activePlayer = null;
            }

            activeTaskLocation = null;
        }

        public void Configure(GameObject root, Image light, TextMeshProUGUI header, TextMeshProUGUI instruct,
                              TextMeshProUGUI psiText, Button close, Image dial, RectTransform needle,
                              Image stabBar, RectTransform stabFrame, TextMeshProUGUI stabPercent, RectTransform wheel)
        {
            rootPanel = root;
            masterStatusLight = light;
            headerText = header;
            instructionText = instruct;
            pressureReadoutText = psiText;
            closeButton = close;
            gaugeDialImage = dial;
            gaugeNeedle = needle;
            stabilizationBar = stabBar;
            stabilizationBarFrame = stabFrame;
            stabilizationPercentText = stabPercent;
            valveWheel = wheel;

            if (closeButton != null)
            {
                closeButton.onClick.RemoveAllListeners();
                closeButton.onClick.AddListener(CloseMinigame);
            }

            EnsureAudioSources();
            GenerateProceduralAudio();
        }

        #region Audio Synthesis

        private void EnsureAudioSources()
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
                audioSource.volume = 0.35f;
                audioSource.pitch = 1f;
                audioSource.enabled = true;
            }
        }

        private void GenerateProceduralAudio()
        {
            if (valveTurnSfx == null)
            {
                valveTurnSfx = Resources.Load<AudioClip>("Audio/sfx_valve_ratchet");
            }
            if (successSfx == null)
            {
                successSfx = Resources.Load<AudioClip>("Audio/sfx_valve_complete");
            }

            if (proceduralTurnClip == null)
            {
                proceduralTurnClip = CreateValveRatchetTone();
            }
            if (proceduralCompleteClip == null)
            {
                proceduralCompleteClip = CreateValveCompleteTone();
            }
        }

        private void PlayValveTurnSound()
        {
            EnsureAudioSources();
            if (valveTurnSfx == null)
            {
                valveTurnSfx = Resources.Load<AudioClip>("Audio/sfx_valve_ratchet");
            }
            if (proceduralTurnClip == null)
            {
                proceduralTurnClip = CreateValveRatchetTone();
            }
            AudioClip clip = valveTurnSfx ?? proceduralTurnClip;
            if (clip == null) return;

            float pitch = UnityEngine.Random.Range(0.96f, 1.04f);

            // 1. Play directly on Panel AudioSource with comfortable tactile volume (0.35)
            if (audioSource != null)
            {
                audioSource.spatialBlend = 0f;
                audioSource.pitch = pitch;
                audioSource.volume = 0.35f;
                audioSource.PlayOneShot(clip, 0.35f);
            }
            else
            {
                // Fallback only if Panel AudioSource is missing
                AudioListener listener = UnityEngine.Object.FindAnyObjectByType<AudioListener>();
                if (listener != null)
                {
                    if (!listener.TryGetComponent(out AudioSource listenerSrc))
                    {
                        listenerSrc = listener.gameObject.AddComponent<AudioSource>();
                    }
                    if (listenerSrc != null)
                    {
                        listenerSrc.spatialBlend = 0f;
                        listenerSrc.pitch = pitch;
                        listenerSrc.volume = 0.35f;
                        listenerSrc.PlayOneShot(clip, 0.35f);
                    }
                }
            }
        }

        /// <summary>
        /// Crisp, tactile mechanical ratchet sound designed using the proven AudioSynthesis curve.
        /// Stays loud and punchy throughout 140ms with prominent 860Hz/1720Hz metallic teeth harmonics.
        /// </summary>
        private static AudioClip CreateValveRatchetTone()
        {
            int sampleRate = 44100;
            float duration = 0.14f;
            int samples = (int)(sampleRate * duration);
            float[] data = new float[samples];
            System.Random rng = new System.Random(1337);

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / sampleRate;
                float env = 1f - Mathf.Pow(t / duration, 0.55f);

                // Rich metallic gear ratchet harmonics:
                // Heavy body clank (340Hz) + high teeth resonance (860Hz & 1720Hz)
                float thud = Mathf.Sin(2f * Mathf.PI * 340f * t) * 0.40f;
                float ring = Mathf.Sin(2f * Mathf.PI * 860f * t) * 0.40f +
                             Mathf.Sin(2f * Mathf.PI * 1720f * t) * 0.25f;

                // Sharp initial metallic tooth contact snap (first 10ms)
                float snap = (t < 0.010f) ? ((float)rng.NextDouble() * 2f - 1f) * 0.45f : 0f;

                data[i] = Mathf.Clamp((thud + ring + snap) * env, -1f, 1f);
            }

            AudioClip clip = AudioClip.Create("ValveRatchetClick", samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip CreateValveCompleteTone()
        {
            int sampleRate = 44100;
            float duration = 0.75f;
            int samples = (int)(sampleRate * duration);
            float[] data = new float[samples];

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Clamp01((duration - t) / duration);

                // Rich resonant major chord chime (C5 + G5 + C6) with low hydraulic sub-thud
                float thud = Mathf.Sin(2f * Mathf.PI * 90f * t) * Mathf.Exp(-t * 12f) * 0.45f;
                float f1 = Mathf.Sin(2f * Mathf.PI * 523.25f * t) * 0.25f;
                float f2 = Mathf.Sin(2f * Mathf.PI * 783.99f * t) * 0.20f;
                float f3 = Mathf.Sin(2f * Mathf.PI * 1046.50f * t) * 0.15f;

                data[i] = Mathf.Clamp((thud + (f1 + f2 + f3) * env) * 0.95f, -1f, 1f);
            }

            AudioClip clip = AudioClip.Create("ValveCompleteHydraulicChime", samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        #endregion

        public static ValveTaskUI GetOrCreateInstance()
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
                Debug.LogWarning("[ValveTaskUI] No Canvas found in scene.");
                return null;
            }

            _instance = CreateValveUIHierarchy(canvas);
            return _instance;
        }

        public static ValveTaskUI CreateValveUIHierarchy(Canvas canvas)
        {
            Sprite chassisSprite = Resources.Load<Sprite>("UI/Tasks/TerminalBezel");
            Sprite dialSprite = Resources.Load<Sprite>("UI/Tasks/GaugeDial");
            Sprite needleSprite = Resources.Load<Sprite>("UI/Tasks/GaugeNeedle");
            Sprite valveSprite = Resources.Load<Sprite>("UI/Tasks/ValveWheel");

            // Full Screen Dark Vignette Overlay
            GameObject root = new GameObject("ValveMinigamePanel", typeof(RectTransform), typeof(AudioSource));
            root.transform.SetParent(canvas.transform, false);

            RectTransform rootRect = root.GetComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;

            // Enforce top-level canvas sorting order so 2D world sprites can never draw over UI
            Canvas panelCanvas = root.AddComponent<Canvas>();
            panelCanvas.overrideSorting = true;
            panelCanvas.sortingOrder = 1000;
            root.AddComponent<GraphicRaycaster>();

            Image bgOverlay = root.AddComponent<Image>();
            bgOverlay.color = new Color(0.02f, 0.04f, 0.06f, 0.90f);
            bgOverlay.raycastTarget = true;

            // Center Terminal Box
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

            // Solid dark background chassis backing so world never shows through bezel
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
            headerText.text = "PRESSURE REGULATOR TERMINAL // MK-IV";
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
            instText.text = "Rotate valve [Drag Wheel or A / D keys] to keep needle in <color=#00FF7F>GREEN ZONE</color>";
            instText.fontSize = 15;
            instText.alignment = TextAlignmentOptions.Center;
            instText.color = new Color(0.72f, 0.82f, 0.92f, 1f);

            // Close Button [ESC / X]
            GameObject closeGo = new GameObject("CloseButton", typeof(RectTransform), typeof(Image), typeof(Button));
            closeGo.transform.SetParent(box.transform, false);
            RectTransform closeRect = closeGo.GetComponent<RectTransform>();
            closeRect.anchorMin = new Vector2(1f, 1f);
            closeRect.anchorMax = new Vector2(1f, 1f);
            closeRect.pivot = new Vector2(1f, 1f);
            closeRect.anchoredPosition = new Vector2(-36, -30);
            closeRect.sizeDelta = new Vector2(32, 32);
            Image closeImg = closeGo.GetComponent<Image>();
            closeImg.color = new Color(0.75f, 0.15f, 0.15f, 1f);
            Button closeBtn = closeGo.GetComponent<Button>();

            GameObject closeTextGo = new GameObject("CloseText", typeof(RectTransform), typeof(TextMeshProUGUI));
            closeTextGo.transform.SetParent(closeGo.transform, false);
            RectTransform ctRect = closeTextGo.GetComponent<RectTransform>();
            ctRect.anchorMin = Vector2.zero;
            ctRect.anchorMax = Vector2.one;
            ctRect.sizeDelta = Vector2.zero;
            TextMeshProUGUI closeTxt = closeTextGo.GetComponent<TextMeshProUGUI>();
            closeTxt.text = "X";
            closeTxt.fontSize = 18;
            closeTxt.fontStyle = FontStyles.Bold;
            closeTxt.alignment = TextAlignmentOptions.Center;
            closeTxt.color = Color.white;
            closeTxt.raycastTarget = false;

            // ================= LEFT BAY: GAUGE UNIT =================
            GameObject lbGo = new GameObject("PressureGaugeBay", typeof(RectTransform), typeof(Image));
            lbGo.transform.SetParent(box.transform, false);
            RectTransform lbRect = lbGo.GetComponent<RectTransform>();
            lbRect.sizeDelta = new Vector2(400, 330);
            lbRect.anchoredPosition = new Vector2(-215, 35);
            Image lbImg = lbGo.GetComponent<Image>();
            lbImg.color = new Color(0.04f, 0.07f, 0.10f, 0.95f);
            lbImg.raycastTarget = false;
            Outline lbOutline = lbGo.AddComponent<Outline>();
            lbOutline.effectColor = new Color(0.18f, 0.28f, 0.40f, 0.85f);
            lbOutline.effectDistance = new Vector2(2, -2);

            GameObject lbhGo = new GameObject("BayHeader", typeof(RectTransform), typeof(TextMeshProUGUI));
            lbhGo.transform.SetParent(lbGo.transform, false);
            RectTransform lbhRect = lbhGo.GetComponent<RectTransform>();
            lbhRect.anchorMin = new Vector2(0f, 1f);
            lbhRect.anchorMax = new Vector2(1f, 1f);
            lbhRect.pivot = new Vector2(0.5f, 1f);
            lbhRect.anchoredPosition = new Vector2(0, -14);
            lbhRect.sizeDelta = new Vector2(-20, 22);
            TextMeshProUGUI lbhText = lbhGo.GetComponent<TextMeshProUGUI>();
            lbhText.text = "[ PRESSURE MONITOR // TELEMETRY ]";
            lbhText.fontSize = 12;
            lbhText.fontStyle = FontStyles.Bold;
            lbhText.alignment = TextAlignmentOptions.Center;
            lbhText.color = new Color(0.40f, 0.80f, 1f, 0.90f);
            lbhText.raycastTarget = false;

            GameObject gaugeGo = new GameObject("GaugeHousing", typeof(RectTransform), typeof(Image));
            gaugeGo.transform.SetParent(lbGo.transform, false);
            RectTransform gRect = gaugeGo.GetComponent<RectTransform>();
            gRect.sizeDelta = new Vector2(240, 240);
            gRect.anchoredPosition = new Vector2(0, 15);
            Image gImg = gaugeGo.GetComponent<Image>();
            if (dialSprite != null)
            {
                gImg.sprite = dialSprite;
                gImg.color = Color.white;
            }
            else
            {
                gImg.color = new Color(0.08f, 0.1f, 0.12f, 1f);
            }

            GameObject needleGo = new GameObject("Needle", typeof(RectTransform), typeof(Image));
            needleGo.transform.SetParent(gaugeGo.transform, false);
            RectTransform needleRect = needleGo.GetComponent<RectTransform>();
            needleRect.sizeDelta = new Vector2(28, 136);
            needleRect.pivot = new Vector2(0.5f, 0.18f);
            needleRect.anchoredPosition = Vector2.zero;
            Image needleImg = needleGo.GetComponent<Image>();
            if (needleSprite != null)
            {
                needleImg.sprite = needleSprite;
                needleImg.color = Color.white;
            }
            else
            {
                needleImg.color = new Color(1f, 0.25f, 0.15f, 1f);
            }

            GameObject rpGo = new GameObject("ReadoutPlate", typeof(RectTransform), typeof(Image));
            rpGo.transform.SetParent(lbGo.transform, false);
            RectTransform rpRect = rpGo.GetComponent<RectTransform>();
            rpRect.anchoredPosition = new Vector2(0, -135);
            rpRect.sizeDelta = new Vector2(320, 42);
            Image rpImg = rpGo.GetComponent<Image>();
            rpImg.color = new Color(0.02f, 0.04f, 0.06f, 1f);
            rpImg.raycastTarget = false;
            Outline rpOutline = rpGo.AddComponent<Outline>();
            rpOutline.effectColor = new Color(0.15f, 0.28f, 0.42f, 0.70f);
            rpOutline.effectDistance = new Vector2(1, -1);

            GameObject psiGo = new GameObject("PsiReadout", typeof(RectTransform), typeof(TextMeshProUGUI));
            psiGo.transform.SetParent(rpGo.transform, false);
            RectTransform psiRect = psiGo.GetComponent<RectTransform>();
            psiRect.anchorMin = Vector2.zero;
            psiRect.anchorMax = Vector2.one;
            psiRect.sizeDelta = Vector2.zero;
            psiRect.anchoredPosition = Vector2.zero;
            TextMeshProUGUI psiText = psiGo.GetComponent<TextMeshProUGUI>();
            psiText.text = "<color=#00FF7F><size=20><b>15.0 PSI</b></size></color>\n<size=10><color=#7A96B0>NOMINAL: 50.0 — 88.0 PSI</color></size>";
            psiText.lineSpacing = 10;
            psiText.alignment = TextAlignmentOptions.Center;
            psiText.raycastTarget = false;

            // ================= RIGHT BAY: ACTUATOR UNIT =================
            GameObject rbGo = new GameObject("ValveActuatorBay", typeof(RectTransform), typeof(Image));
            rbGo.transform.SetParent(box.transform, false);
            RectTransform rbRect = rbGo.GetComponent<RectTransform>();
            rbRect.sizeDelta = new Vector2(400, 330);
            rbRect.anchoredPosition = new Vector2(215, 35);
            Image rbImg = rbGo.GetComponent<Image>();
            rbImg.color = new Color(0.04f, 0.07f, 0.10f, 0.95f);
            rbImg.raycastTarget = false;
            Outline rbOutline = rbGo.AddComponent<Outline>();
            rbOutline.effectColor = new Color(0.18f, 0.28f, 0.40f, 0.85f);
            rbOutline.effectDistance = new Vector2(2, -2);

            GameObject rbhGo = new GameObject("BayHeader", typeof(RectTransform), typeof(TextMeshProUGUI));
            rbhGo.transform.SetParent(rbGo.transform, false);
            RectTransform rbhRect = rbhGo.GetComponent<RectTransform>();
            rbhRect.anchorMin = new Vector2(0f, 1f);
            rbhRect.anchorMax = new Vector2(1f, 1f);
            rbhRect.pivot = new Vector2(0.5f, 1f);
            rbhRect.anchoredPosition = new Vector2(0, -14);
            rbhRect.sizeDelta = new Vector2(-20, 22);
            TextMeshProUGUI rbhText = rbhGo.GetComponent<TextMeshProUGUI>();
            rbhText.text = "[ MANUAL ACTUATOR // VALVE SERVO-A ]";
            rbhText.fontSize = 12;
            rbhText.fontStyle = FontStyles.Bold;
            rbhText.alignment = TextAlignmentOptions.Center;
            rbhText.color = new Color(0.40f, 0.80f, 1f, 0.90f);
            rbhText.raycastTarget = false;

            GameObject wheelGo = new GameObject("ValveWheel", typeof(RectTransform), typeof(Image));
            wheelGo.transform.SetParent(rbGo.transform, false);
            RectTransform wRect = wheelGo.GetComponent<RectTransform>();
            wRect.sizeDelta = new Vector2(240, 240);
            wRect.anchoredPosition = new Vector2(0, 15);
            Image wImg = wheelGo.GetComponent<Image>();
            if (valveSprite != null)
            {
                wImg.sprite = valveSprite;
                wImg.color = Color.white;
            }
            else
            {
                wImg.color = new Color(0.85f, 0.4f, 0.15f, 1f);
            }
            wImg.raycastTarget = true;

            GameObject cpGo = new GameObject("ControlPlate", typeof(RectTransform), typeof(Image));
            cpGo.transform.SetParent(rbGo.transform, false);
            RectTransform cpRect = cpGo.GetComponent<RectTransform>();
            cpRect.anchoredPosition = new Vector2(0, -135);
            cpRect.sizeDelta = new Vector2(320, 42);
            Image cpImg = cpGo.GetComponent<Image>();
            cpImg.color = new Color(0.02f, 0.04f, 0.06f, 1f);
            cpImg.raycastTarget = false;
            Outline cpOutline = cpGo.AddComponent<Outline>();
            cpOutline.effectColor = new Color(0.15f, 0.28f, 0.42f, 0.70f);
            cpOutline.effectDistance = new Vector2(1, -1);

            GameObject cpTextGo = new GameObject("ControlText", typeof(RectTransform), typeof(TextMeshProUGUI));
            cpTextGo.transform.SetParent(cpGo.transform, false);
            RectTransform cptRect = cpTextGo.GetComponent<RectTransform>();
            cptRect.anchorMin = Vector2.zero;
            cptRect.anchorMax = Vector2.one;
            cptRect.sizeDelta = Vector2.zero;
            cptRect.anchoredPosition = Vector2.zero;
            TextMeshProUGUI cptText = cpTextGo.GetComponent<TextMeshProUGUI>();
            cptText.text = "◄ <color=#FF8866><b>[A]</b></color> DECREASE   |   INCREASE <color=#66DDFF><b>[D]</b></color> ►\n<size=10><color=#7A96B0>DRAG WHEEL OR USE [A] / [D] KEYS</color></size>";
            cptText.fontSize = 12;
            cptText.lineSpacing = 10;
            cptText.alignment = TextAlignmentOptions.Center;
            cptText.color = new Color(0.75f, 0.85f, 0.95f, 1f);
            cptText.raycastTarget = false;

            // ================= BOTTOM BAY: STABILITY SYSTEM =================
            GameObject bbGo = new GameObject("StabilityBay", typeof(RectTransform), typeof(Image));
            bbGo.transform.SetParent(box.transform, false);
            RectTransform bbRect = bbGo.GetComponent<RectTransform>();
            bbRect.sizeDelta = new Vector2(830, 120);
            bbRect.anchoredPosition = new Vector2(0, -215);
            Image bbImg = bbGo.GetComponent<Image>();
            bbImg.color = new Color(0.04f, 0.07f, 0.10f, 0.95f);
            bbImg.raycastTarget = false;
            Outline bbOutline = bbGo.AddComponent<Outline>();
            bbOutline.effectColor = new Color(0.18f, 0.28f, 0.40f, 0.85f);
            bbOutline.effectDistance = new Vector2(2, -2);

            GameObject bbhGo = new GameObject("BayHeader", typeof(RectTransform), typeof(TextMeshProUGUI));
            bbhGo.transform.SetParent(bbGo.transform, false);
            RectTransform bbhRect = bbhGo.GetComponent<RectTransform>();
            bbhRect.anchorMin = new Vector2(0f, 1f);
            bbhRect.anchorMax = new Vector2(1f, 1f);
            bbhRect.pivot = new Vector2(0.5f, 1f);
            bbhRect.anchoredPosition = new Vector2(0, -10);
            bbhRect.sizeDelta = new Vector2(-20, 18);
            TextMeshProUGUI bbhText = bbhGo.GetComponent<TextMeshProUGUI>();
            bbhText.text = "PRESSURE STABILIZATION LOCK";
            bbhText.fontSize = 11;
            bbhText.fontStyle = FontStyles.Bold;
            bbhText.alignment = TextAlignmentOptions.Center;
            bbhText.color = new Color(0.65f, 0.78f, 0.90f, 1f);
            bbhText.raycastTarget = false;

            GameObject barFrameGo = new GameObject("StabilityBarFrame", typeof(RectTransform), typeof(Image));
            barFrameGo.transform.SetParent(bbGo.transform, false);
            RectTransform bFrameRect = barFrameGo.GetComponent<RectTransform>();
            bFrameRect.anchoredPosition = new Vector2(0, 8);
            bFrameRect.sizeDelta = new Vector2(770, 22);
            Image bFrameImg = barFrameGo.GetComponent<Image>();
            bFrameImg.color = new Color(0.02f, 0.04f, 0.06f, 1f);
            Outline barOutline = barFrameGo.AddComponent<Outline>();
            barOutline.effectColor = new Color(0.20f, 0.30f, 0.40f, 0.80f);
            barOutline.effectDistance = new Vector2(1, -1);

            GameObject barFillGo = new GameObject("StabilityBarFill", typeof(RectTransform), typeof(Image));
            barFillGo.transform.SetParent(barFrameGo.transform, false);
            RectTransform bFillRect = barFillGo.GetComponent<RectTransform>();
            bFillRect.anchorMin = Vector2.zero;
            bFillRect.anchorMax = Vector2.one;
            bFillRect.sizeDelta = Vector2.zero;
            Image barFillImg = barFillGo.GetComponent<Image>();
            barFillImg.type = Image.Type.Filled;
            barFillImg.fillMethod = Image.FillMethod.Horizontal;
            barFillImg.fillOrigin = 0;
            barFillImg.fillAmount = 0f;
            barFillImg.color = new Color(0f, 0.95f, 0.45f, 1f);

            GameObject pctGo = new GameObject("PercentText", typeof(RectTransform), typeof(TextMeshProUGUI));
            pctGo.transform.SetParent(bbGo.transform, false);
            RectTransform pctRect = pctGo.GetComponent<RectTransform>();
            pctRect.anchoredPosition = new Vector2(0, -32);
            pctRect.sizeDelta = new Vector2(770, 28);
            TextMeshProUGUI pctText = pctGo.GetComponent<TextMeshProUGUI>();
            pctText.text = "<color=#FFA500><b>LOW PRESSURE</b></color>  <size=13><color=#CCCCCC>— Turn Valve Right / Press [D] to Increase Value</color></size>";
            pctText.fontSize = 14;
            pctText.alignment = TextAlignmentOptions.Center;
            pctText.raycastTarget = false;

            ValveTaskUI valveTask = root.AddComponent<ValveTaskUI>();
            valveTask.Configure(root, null, headerText, instText, psiText, closeBtn,
                                gImg, needleRect, barFillImg, bFrameRect, pctText, wRect);

            var wheelProxy = wheelGo.AddComponent<ValveWheelDragProxy>();
            wheelProxy.targetUI = valveTask;
            var bayProxy = rbGo.AddComponent<ValveWheelDragProxy>();
            bayProxy.targetUI = valveTask;

            root.SetActive(false);
            return valveTask;
        }
    }

    /// <summary>
    /// Captures mouse pointer drag events directly on the valve wheel or its container bay
    /// and delegates them to the parent ValveTaskUI instance.
    /// </summary>
    public class ValveWheelDragProxy : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerDownHandler
    {
        public ValveTaskUI targetUI;

        public void OnPointerDown(PointerEventData eventData)
        {
            // Captures pointer focus immediately on mouse down
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (targetUI != null) targetUI.OnBeginDrag(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (targetUI != null) targetUI.OnDrag(eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (targetUI != null) targetUI.OnEndDrag(eventData);
        }
    }
}
