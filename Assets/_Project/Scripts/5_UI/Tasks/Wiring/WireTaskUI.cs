using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using TheLastSignal.Entities.Player;
using TheLastSignal.Entities.Environment;

namespace TheLastSignal.UI.Tasks
{
    /// <summary>
    /// Sci-Fi Brutalist Electrical Wire Bridging Minigame (Sequential Calibration Mode).
    /// Synchronized with Reactor Memory, Concentric Cipher, and Valve Balancing terminal aesthetic:
    /// - Heavy metallic brutalist chassis (TerminalBezel) with hazard stripe header
    /// - Dual terminal bays: Feed Bus (A-Bay) on left, Load Bus (B-Bay) on right
    /// - Center inter-bus bridging conduit with active Sequential Calibration Roadmap
    /// - Mandatory low-to-high voltage bridging sequence: [+5V -> +12V -> +24V -> +48V]
    /// - Breaker Overload Fault & screen shake on sequence violation
    /// - Procedural audio synthesis for relay connects, breaker fault sparks, and completion chime
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class WireTaskUI : MonoBehaviour
    {
        private static WireTaskUI _instance;
        public static WireTaskUI Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = UnityEngine.Object.FindAnyObjectByType<WireTaskUI>(FindObjectsInactive.Include);
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
        [SerializeField] private TextMeshProUGUI progressText;
        [SerializeField] private Button closeButton;

        [Header("Sequence HUD Elements")]
        [SerializeField] private TextMeshProUGUI activeDirectiveText;
        [SerializeField] private TextMeshProUGUI telemetryLogText;
        [SerializeField] private Image[] sequenceBadgeBgs = new Image[4];
        [SerializeField] private TextMeshProUGUI[] sequenceBadgeTexts = new TextMeshProUGUI[4];
        [SerializeField] private Outline[] sequenceBadgeOutlines = new Outline[4];

        [Header("Progress LEDs")]
        [SerializeField] private Image[] connectionLeds = new Image[4];

        [Header("Wiring Sockets")]
        [SerializeField] private List<WirePointUI> leftPoints = new List<WirePointUI>();
        [SerializeField] private List<WirePointUI> rightPoints = new List<WirePointUI>();

        [Header("Audio")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip wireConnectedSfx;
        [SerializeField] private AudioClip taskCompletedSfx;

        // Sequence Order: Low Voltage Ground Reference -> High Tension Power
        private readonly WireColorType[] RequiredSequence = new WireColorType[]
        {
            WireColorType.Green,   // Step 1: +5V Logic
            WireColorType.Blue,    // Step 2: +12V Control
            WireColorType.Yellow,  // Step 3: +24V Auxiliary
            WireColorType.Red      // Step 4: +48V High Tension
        };

        private Canvas canvas;
        private PlayerController activePlayer;
        private TaskLocation activeTaskLocation;
        private int currentSequenceStep = 0;
        private int connectedCount = 0;
        private bool isFinished = false;
        private bool isFaultLocked = false;

        private AudioClip proceduralConnectClip;
        private AudioClip proceduralCompleteClip;
        private AudioClip proceduralOverloadClip;
        private AudioClip proceduralMismatchClip;

        // Visual Colors
        private readonly Color LedInactive = new Color(0.18f, 0.22f, 0.28f, 1f);
        private readonly Color LedActive = new Color(0f, 1f, 0.5f, 1f);
        private readonly Color LedFault = new Color(0.95f, 0.15f, 0.15f, 1f);

        public bool IsOpen => rootPanel != null && rootPanel.activeSelf;
        public bool IsFaultLocked => isFaultLocked;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;

            canvas = GetComponentInParent<Canvas>();

            EnsureUpgradedVisuals();
            GenerateProceduralAudio();

            if (closeButton != null)
            {
                closeButton.onClick.RemoveAllListeners();
                closeButton.onClick.AddListener(CloseMinigame);
            }

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

            // Emergency exit with ESC when in danger
            if (UnityEngine.InputSystem.Keyboard.current != null &&
                UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                CloseMinigame();
            }
        }

        public void Configure(GameObject root, Image light, TextMeshProUGUI header, TextMeshProUGUI inst,
                              TextMeshProUGUI progress, Button close, List<WirePointUI> left, List<WirePointUI> right,
                              Image[] leds = null, AudioSource audio = null)
        {
            rootPanel = root;
            masterStatusLight = light;
            headerText = header;
            instructionText = inst;
            progressText = progress;
            closeButton = close;
            leftPoints = left;
            rightPoints = right;
            if (leds != null) connectionLeds = leds;

            if (audio != null)
            {
                audioSource = audio;
            }
            else if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
            }

            if (closeButton != null)
            {
                closeButton.onClick.RemoveAllListeners();
                closeButton.onClick.AddListener(CloseMinigame);
            }

            GenerateProceduralAudio();
        }

        /// <summary>
        /// Retrieves the existing WireTaskUI in the scene or creates one automatically under a Canvas.
        /// </summary>
        public static WireTaskUI GetOrCreateInstance()
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
                Debug.LogWarning("[WireTaskUI] No Canvas found in scene to create WireTaskUI.");
                return null;
            }

            _instance = CreateWiringUIHierarchy(canvas);
            return _instance;
        }

        #region Hierarchy Builder

        public static WireTaskUI CreateWiringUIHierarchy(Canvas canvas)
        {
            Sprite chassisSprite = Resources.Load<Sprite>("UI/Tasks/TerminalBezel");

            // Root panel (Full Screen Vignette)
            GameObject root = new GameObject("WiringMinigamePanel", typeof(RectTransform), typeof(AudioSource));
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

            // Center Panel (TerminalBox)
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
                boxImg.color = new Color(0.11f, 0.13f, 0.17f, 1f);
            }

            // Header Title
            GameObject headerGo = new GameObject("HeaderTitle", typeof(RectTransform), typeof(TextMeshProUGUI));
            headerGo.transform.SetParent(box.transform, false);
            RectTransform headerRect = headerGo.GetComponent<RectTransform>();
            headerRect.anchorMin = new Vector2(0f, 1f);
            headerRect.anchorMax = new Vector2(1f, 1f);
            headerRect.pivot = new Vector2(0.5f, 1f);
            headerRect.anchoredPosition = new Vector2(0, -30);
            headerRect.sizeDelta = new Vector2(-120, 36);
            TextMeshProUGUI headerText = headerGo.GetComponent<TextMeshProUGUI>();
            headerText.text = "ELECTRICAL JUNCTION PANEL // POWER DISTRIBUTION";
            headerText.fontSize = 22;
            headerText.fontStyle = FontStyles.Bold;
            headerText.alignment = TextAlignmentOptions.Center;
            headerText.color = new Color(0.92f, 0.96f, 1f, 1f);

            // Instruction Subtext
            GameObject instGo = new GameObject("InstructionText", typeof(RectTransform), typeof(TextMeshProUGUI));
            instGo.transform.SetParent(box.transform, false);
            RectTransform instRect = instGo.GetComponent<RectTransform>();
            instRect.anchorMin = new Vector2(0f, 1f);
            instRect.anchorMax = new Vector2(1f, 1f);
            instRect.pivot = new Vector2(0.5f, 1f);
            instRect.anchoredPosition = new Vector2(0, -66);
            instRect.sizeDelta = new Vector2(-120, 24);
            TextMeshProUGUI instText = instGo.GetComponent<TextMeshProUGUI>();
            instText.text = "SEQUENTIAL CALIBRATION: Bridge terminals in strict order [+5V -> +12V -> +24V -> +48V]";
            instText.fontSize = 15;
            instText.alignment = TextAlignmentOptions.Center;
            instText.color = new Color(0.72f, 0.85f, 0.95f, 1f);

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

            // Background Decorative Bays (drawn behind sockets)
            EnsureBayPanels(box.transform);

            // Sockets
            float[] socketY = new float[] { 114f, 50f, -14f, -78f };
            List<WirePointUI> leftPoints = new List<WirePointUI>();
            List<WirePointUI> rightPoints = new List<WirePointUI>();

            for (int i = 0; i < 4; i++)
            {
                WirePointUI leftPt = CreateSocket(box.transform, $"LeftSocket_{i}", new Vector2(-310, socketY[i]), true, i);
                leftPoints.Add(leftPt);

                WirePointUI rightPt = CreateSocket(box.transform, $"RightSocket_{i}", new Vector2(310, socketY[i]), false, i);
                rightPoints.Add(rightPt);
            }

            // Bottom Progress Frame (4 LEDs)
            GameObject pfGo = new GameObject("ProgressFrame", typeof(RectTransform));
            pfGo.transform.SetParent(box.transform, false);
            RectTransform pfRect = pfGo.GetComponent<RectTransform>();
            pfRect.anchoredPosition = new Vector2(0, -205);
            pfRect.sizeDelta = new Vector2(320, 20);

            Image[] leds = new Image[4];
            float ledW = 62f;
            float ledSp = 16f;
            float ledStartX = -(1.5f * (ledW + ledSp));

            for (int i = 0; i < 4; i++)
            {
                GameObject ledGo = new GameObject($"StageLed_{i + 1}", typeof(RectTransform), typeof(Image));
                ledGo.transform.SetParent(pfGo.transform, false);
                RectTransform lr = ledGo.GetComponent<RectTransform>();
                lr.sizeDelta = new Vector2(ledW, 14);
                lr.anchoredPosition = new Vector2(ledStartX + i * (ledW + ledSp), 0);

                Image li = ledGo.GetComponent<Image>();
                li.color = new Color(0.18f, 0.22f, 0.28f, 1f);

                Outline lo = ledGo.AddComponent<Outline>();
                lo.effectColor = new Color(0.28f, 0.35f, 0.42f, 1f);
                lo.effectDistance = new Vector2(1, -1);

                leds[i] = li;
            }

            // Progress Text
            GameObject progressGo = new GameObject("ProgressText", typeof(RectTransform), typeof(TextMeshProUGUI));
            progressGo.transform.SetParent(box.transform, false);
            RectTransform progressRect = progressGo.GetComponent<RectTransform>();
            progressRect.anchoredPosition = new Vector2(0, -248);
            progressRect.sizeDelta = new Vector2(700, 28);
            TextMeshProUGUI progressText = progressGo.GetComponent<TextMeshProUGUI>();
            progressText.text = "WIRES BRIDGED: <color=#00FF7F><b>0</b></color> / 4  //  NEXT REQUIRED: +5V [LOGIC]";
            progressText.fontSize = 17;
            progressText.alignment = TextAlignmentOptions.Center;
            progressText.color = new Color(0.85f, 0.92f, 1f, 1f);

            AudioSource audioSrc = root.GetComponent<AudioSource>();
            if (audioSrc != null)
            {
                audioSrc.playOnAwake = false;
            }

            WireTaskUI wireTask = root.AddComponent<WireTaskUI>();
            wireTask.Configure(root, null, headerText, instText, progressText, closeBtn, leftPoints, rightPoints, leds, audioSrc);

            root.SetActive(false);
            return wireTask;
        }

        private static void EnsureBayPanels(Transform boxT)
        {
            // Center Conduit Chamber (drawn deepest)
            Transform conduit = boxT.Find("ConduitChamber");
            if (conduit == null)
            {
                GameObject conGo = new GameObject("ConduitChamber", typeof(RectTransform), typeof(Image));
                conGo.transform.SetParent(boxT, false);
                conGo.transform.SetAsFirstSibling();
                RectTransform cr = conGo.GetComponent<RectTransform>();
                cr.anchoredPosition = new Vector2(0, 18);
                cr.sizeDelta = new Vector2(350, 360);
                Image ci = conGo.GetComponent<Image>();
                ci.color = new Color(0.03f, 0.04f, 0.06f, 1f);
                ci.raycastTarget = false;

                Outline co = conGo.AddComponent<Outline>();
                co.effectColor = new Color(0.12f, 0.18f, 0.24f, 1f);
                co.effectDistance = new Vector2(2, -2);
            }

            // Header of Conduit
            Transform conduitHeader = conduit != null ? conduit.Find("ConduitHeader") : boxT.Find("ConduitChamber/ConduitHeader");
            if (conduitHeader == null && conduit != null)
            {
                GameObject hGo = new GameObject("ConduitHeader", typeof(RectTransform), typeof(TextMeshProUGUI));
                hGo.transform.SetParent(conduit, false);
                RectTransform hr = hGo.GetComponent<RectTransform>();
                hr.anchoredPosition = new Vector2(0, 155);
                hr.sizeDelta = new Vector2(320, 24);
                TextMeshProUGUI ht = hGo.GetComponent<TextMeshProUGUI>();
                ht.text = "SEQUENTIAL CALIBRATION // PROTOCOL";
                ht.fontSize = 12;
                ht.fontStyle = FontStyles.Bold;
                ht.alignment = TextAlignmentOptions.Center;
                ht.color = new Color(0.35f, 0.70f, 0.95f, 1f);
                ht.raycastTarget = false;
            }

            // Left Bay
            Transform leftBay = boxT.Find("SourceBayHousing");
            if (leftBay == null)
            {
                GameObject bayGo = new GameObject("SourceBayHousing", typeof(RectTransform), typeof(Image));
                bayGo.transform.SetParent(boxT, false);
                bayGo.transform.SetSiblingIndex(1);
                RectTransform br = bayGo.GetComponent<RectTransform>();
                br.anchoredPosition = new Vector2(-310, 18);
                br.sizeDelta = new Vector2(230, 360);
                Image bi = bayGo.GetComponent<Image>();
                bi.color = new Color(0.04f, 0.07f, 0.10f, 1f);
                bi.raycastTarget = false;

                Outline bo = bayGo.AddComponent<Outline>();
                bo.effectColor = new Color(0.15f, 0.35f, 0.55f, 1f);
                bo.effectDistance = new Vector2(3, -3);

                GameObject hGo = new GameObject("BayHeader", typeof(RectTransform), typeof(TextMeshProUGUI));
                hGo.transform.SetParent(bayGo.transform, false);
                RectTransform hr = hGo.GetComponent<RectTransform>();
                hr.anchoredPosition = new Vector2(0, 155);
                hr.sizeDelta = new Vector2(210, 24);
                TextMeshProUGUI ht = hGo.GetComponent<TextMeshProUGUI>();
                ht.text = "FEED BUS [A-BAY]";
                ht.fontSize = 13;
                ht.fontStyle = FontStyles.Bold;
                ht.alignment = TextAlignmentOptions.Center;
                ht.color = new Color(0.4f, 0.7f, 0.95f, 1f);
                ht.raycastTarget = false;
            }

            // Right Bay
            Transform rightBay = boxT.Find("LoadBayHousing");
            if (rightBay == null)
            {
                GameObject bayGo = new GameObject("LoadBayHousing", typeof(RectTransform), typeof(Image));
                bayGo.transform.SetParent(boxT, false);
                bayGo.transform.SetSiblingIndex(2);
                RectTransform br = bayGo.GetComponent<RectTransform>();
                br.anchoredPosition = new Vector2(310, 18);
                br.sizeDelta = new Vector2(230, 360);
                Image bi = bayGo.GetComponent<Image>();
                bi.color = new Color(0.06f, 0.08f, 0.11f, 1f);
                bi.raycastTarget = false;

                Outline bo = bayGo.AddComponent<Outline>();
                bo.effectColor = new Color(0.25f, 0.32f, 0.40f, 1f);
                bo.effectDistance = new Vector2(3, -3);

                GameObject hGo = new GameObject("BayHeader", typeof(RectTransform), typeof(TextMeshProUGUI));
                hGo.transform.SetParent(bayGo.transform, false);
                RectTransform hr = hGo.GetComponent<RectTransform>();
                hr.anchoredPosition = new Vector2(0, 155);
                hr.sizeDelta = new Vector2(210, 24);
                TextMeshProUGUI ht = hGo.GetComponent<TextMeshProUGUI>();
                ht.text = "LOAD BUS [B-BAY]";
                ht.fontSize = 13;
                ht.fontStyle = FontStyles.Bold;
                ht.alignment = TextAlignmentOptions.Center;
                ht.color = new Color(0.85f, 0.9f, 0.95f, 1f);
                ht.raycastTarget = false;
            }
        }

        private static void UpgradeSocketVisual(Transform socketT, Sprite socketSprite, bool isLeft, int index)
        {
            if (socketT.TryGetComponent(out Image si) && socketSprite != null)
            {
                si.sprite = socketSprite;
                si.color = Color.white;
            }

            // Socket Core
            Transform coreT = socketT.Find("SocketCore");
            if (coreT is RectTransform cr)
            {
                cr.sizeDelta = new Vector2(28, 28);
                cr.anchoredPosition = Vector2.zero;
            }

            // Light Indicator
            Transform lightT = socketT.Find("LightIndicator");
            if (lightT is RectTransform lr)
            {
                lr.sizeDelta = new Vector2(14, 14);
                lr.anchoredPosition = isLeft ? new Vector2(-42, 0) : new Vector2(42, 0);
            }

            // Terminal Label
            Transform lblT = socketT.Find("TerminalLabel");
            if (lblT == null)
            {
                GameObject lblGo = new GameObject("TerminalLabel", typeof(RectTransform), typeof(TextMeshProUGUI));
                lblGo.transform.SetParent(socketT, false);
                RectTransform lblR = lblGo.GetComponent<RectTransform>();
                lblR.sizeDelta = new Vector2(90, 22);
                lblR.anchoredPosition = isLeft ? new Vector2(65, 0) : new Vector2(-65, 0);
                TextMeshProUGUI lt = lblGo.GetComponent<TextMeshProUGUI>();
                lt.text = isLeft ? $"[A-{index + 1}]" : $"[B-{index + 1}]";
                lt.fontSize = 13;
                lt.fontStyle = FontStyles.Bold;
                lt.alignment = isLeft ? TextAlignmentOptions.Left : TextAlignmentOptions.Right;
                lt.color = isLeft ? new Color(0.85f, 0.92f, 1f, 1f) : new Color(0.75f, 0.85f, 0.95f, 1f);
                lt.raycastTarget = false;
            }
            else if (lblT is RectTransform lblR)
            {
                lblR.sizeDelta = new Vector2(90, 22);
                lblR.anchoredPosition = isLeft ? new Vector2(65, 0) : new Vector2(-65, 0);
                if (lblT.TryGetComponent(out TextMeshProUGUI lt))
                {
                    lt.fontSize = 13;
                    lt.fontStyle = FontStyles.Bold;
                    lt.alignment = isLeft ? TextAlignmentOptions.Left : TextAlignmentOptions.Right;
                }
            }
        }

        private static WirePointUI CreateSocket(Transform parent, string name, Vector2 anchoredPos, bool isLeft, int index)
        {
            Sprite socketSprite = Resources.Load<Sprite>("UI/Tasks/WireSocket");

            GameObject socketGo = new GameObject(name, typeof(RectTransform), typeof(Image));
            socketGo.transform.SetParent(parent, false);

            RectTransform sRect = socketGo.GetComponent<RectTransform>();
            sRect.sizeDelta = new Vector2(60, 60);
            sRect.anchoredPosition = anchoredPos;

            Image sImg = socketGo.GetComponent<Image>();
            if (socketSprite != null)
            {
                sImg.sprite = socketSprite;
                sImg.color = Color.white;
            }
            else
            {
                sImg.color = new Color(0.15f, 0.18f, 0.22f, 1f);
            }

            Outline sOutline = socketGo.AddComponent<Outline>();
            sOutline.effectColor = new Color(0.12f, 0.18f, 0.24f, 1f);
            sOutline.effectDistance = new Vector2(2, -2);

            // Inner colored plug core
            GameObject coreGo = new GameObject("SocketCore", typeof(RectTransform), typeof(Image));
            coreGo.transform.SetParent(socketGo.transform, false);
            RectTransform coreRect = coreGo.GetComponent<RectTransform>();
            coreRect.sizeDelta = new Vector2(28, 28);
            coreRect.anchoredPosition = Vector2.zero;
            Image coreImg = coreGo.GetComponent<Image>();

            // Indicator light (LED)
            GameObject lightGo = new GameObject("LightIndicator", typeof(RectTransform), typeof(Image));
            lightGo.transform.SetParent(socketGo.transform, false);
            RectTransform lRect = lightGo.GetComponent<RectTransform>();
            lRect.sizeDelta = new Vector2(14, 14);
            lRect.anchoredPosition = isLeft ? new Vector2(-42, 0) : new Vector2(42, 0);
            Image lightImg = lightGo.GetComponent<Image>();
            lightImg.color = new Color(0.18f, 0.22f, 0.28f, 1f);

            Outline lOutline = lightGo.AddComponent<Outline>();
            lOutline.effectColor = new Color(0.28f, 0.35f, 0.42f, 1f);
            lOutline.effectDistance = new Vector2(1, -1);

            // Terminal Label
            GameObject lblGo = new GameObject("TerminalLabel", typeof(RectTransform), typeof(TextMeshProUGUI));
            lblGo.transform.SetParent(socketGo.transform, false);
            RectTransform lblR = lblGo.GetComponent<RectTransform>();
            lblR.sizeDelta = new Vector2(90, 22);
            lblR.anchoredPosition = isLeft ? new Vector2(65, 0) : new Vector2(-65, 0);
            TextMeshProUGUI lt = lblGo.GetComponent<TextMeshProUGUI>();
            lt.text = isLeft ? $"[A-{index + 1}]" : $"[B-{index + 1}]";
            lt.fontSize = 13;
            lt.fontStyle = FontStyles.Bold;
            lt.alignment = isLeft ? TextAlignmentOptions.Left : TextAlignmentOptions.Right;
            lt.color = isLeft ? new Color(0.85f, 0.92f, 1f, 1f) : new Color(0.75f, 0.85f, 0.95f, 1f);
            lt.raycastTarget = false;

            // Wire Line (only for left socket)
            RectTransform wireRect = null;
            if (isLeft)
            {
                GameObject wireGo = new GameObject("WireLine", typeof(RectTransform), typeof(Image));
                wireGo.transform.SetParent(socketGo.transform, false);
                wireGo.transform.SetAsFirstSibling();
                wireRect = wireGo.GetComponent<RectTransform>();
                wireRect.pivot = new Vector2(0f, 0.5f);
                wireRect.anchoredPosition = Vector2.zero;
                wireRect.sizeDelta = new Vector2(0, 16);

                Image wImg = wireGo.GetComponent<Image>();
                wImg.raycastTarget = false;
                wireGo.SetActive(false);
            }

            WirePointUI pointUI = socketGo.AddComponent<WirePointUI>();
            pointUI.Configure(isLeft, coreImg, lightImg, wireRect, lt);

            return pointUI;
        }

        #endregion

        #region Runtime Upgrade & Layout

        /// <summary>
        /// Automatically upgrades/re-aligns any existing scene hierarchy to the synchronized brutalist terminal layout.
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

            if (rootPanel == null) return;
            Transform boxT = rootPanel.transform.Find("TerminalBox");
            if (boxT == null)
            {
                boxT = rootPanel.transform.Find("ElectricalBox");
            }
            if (boxT == null) return;

            boxT.name = "TerminalBox";
            RectTransform boxRect = boxT.GetComponent<RectTransform>();
            if (boxRect != null)
            {
                boxRect.sizeDelta = new Vector2(960, 640);
                boxRect.anchoredPosition = Vector2.zero;
            }

            Sprite bezelSprite = Resources.Load<Sprite>("UI/Tasks/TerminalBezel");
            Sprite socketSprite = Resources.Load<Sprite>("UI/Tasks/WireSocket");

            if (boxT.TryGetComponent(out Image boxImg) && bezelSprite != null)
            {
                boxImg.sprite = bezelSprite;
                boxImg.color = Color.white;
            }

            // Solid dark background chassis backing so world never shows through bezel
            Transform chassisBg = boxT.Find("TerminalChassisBackground");
            if (chassisBg == null)
            {
                GameObject cbgGo = new GameObject("TerminalChassisBackground", typeof(RectTransform), typeof(Image));
                cbgGo.transform.SetParent(boxT, false);
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

            // Remove/hide masterStatusLight
            if (masterStatusLight != null)
            {
                masterStatusLight.gameObject.SetActive(false);
            }
            Transform oldMl = boxT.Find("MasterLight");
            if (oldMl != null) oldMl.gameObject.SetActive(false);

            // Header Title
            Transform headT = boxT.Find("HeaderTitle");
            if (headT is RectTransform headRect)
            {
                headRect.anchorMin = new Vector2(0f, 1f);
                headRect.anchorMax = new Vector2(1f, 1f);
                headRect.pivot = new Vector2(0.5f, 1f);
                headRect.anchoredPosition = new Vector2(0, -30);
                headRect.sizeDelta = new Vector2(-120, 36);

                if (headT.TryGetComponent(out TextMeshProUGUI headTxt))
                {
                    headTxt.fontSize = 22;
                    headTxt.fontStyle = FontStyles.Bold;
                    headTxt.alignment = TextAlignmentOptions.Center;
                    headTxt.color = new Color(0.92f, 0.96f, 1f, 1f);
                    headerText = headTxt;
                }
            }

            // Instruction Subtext
            Transform instT = boxT.Find("InstructionText");
            if (instT == null)
            {
                GameObject instGo = new GameObject("InstructionText", typeof(RectTransform), typeof(TextMeshProUGUI));
                instGo.transform.SetParent(boxT, false);
                RectTransform ir = instGo.GetComponent<RectTransform>();
                ir.anchorMin = new Vector2(0f, 1f);
                ir.anchorMax = new Vector2(1f, 1f);
                ir.pivot = new Vector2(0.5f, 1f);
                ir.anchoredPosition = new Vector2(0, -66);
                ir.sizeDelta = new Vector2(-120, 24);
                TextMeshProUGUI it = instGo.GetComponent<TextMeshProUGUI>();
                it.text = "SEQUENTIAL CALIBRATION: Bridge terminals in strict order [+5V -> +12V -> +24V -> +48V]";
                it.fontSize = 15;
                it.alignment = TextAlignmentOptions.Center;
                it.color = new Color(0.72f, 0.85f, 0.95f, 1f);
                instructionText = it;
            }
            else if (instT is RectTransform ir)
            {
                ir.anchorMin = new Vector2(0f, 1f);
                ir.anchorMax = new Vector2(1f, 1f);
                ir.pivot = new Vector2(0.5f, 1f);
                ir.anchoredPosition = new Vector2(0, -66);
                ir.sizeDelta = new Vector2(-120, 24);
                if (instT.TryGetComponent(out TextMeshProUGUI it))
                {
                    it.text = "SEQUENTIAL CALIBRATION: Bridge terminals in strict order [+5V -> +12V -> +24V -> +48V]";
                    it.fontSize = 15;
                    it.alignment = TextAlignmentOptions.Center;
                    it.color = new Color(0.72f, 0.85f, 0.95f, 1f);
                    instructionText = it;
                }
            }

            // Close Button
            Transform closeT = boxT.Find("CloseButton");
            if (closeT is RectTransform cr)
            {
                cr.anchorMin = new Vector2(1f, 1f);
                cr.anchorMax = new Vector2(1f, 1f);
                cr.pivot = new Vector2(1f, 1f);
                cr.anchoredPosition = new Vector2(-36, -30);
                cr.sizeDelta = new Vector2(32, 32);
                if (closeT.TryGetComponent(out Image ci)) ci.color = new Color(0.75f, 0.15f, 0.15f, 1f);
            }

            // Decorative Bays
            EnsureBayPanels(boxT);

            // Center Conduit Chamber HUD elements
            EnsureConduitHUD(boxT);

            // Sockets
            float[] socketY = new float[] { 114f, 50f, -14f, -78f };
            if (leftPoints == null || leftPoints.Count < 4) leftPoints = new List<WirePointUI>();
            if (rightPoints == null || rightPoints.Count < 4) rightPoints = new List<WirePointUI>();

            for (int i = 0; i < 4; i++)
            {
                Transform lp = boxT.Find($"LeftSocket_{i}");
                if (lp is RectTransform lpr)
                {
                    lpr.sizeDelta = new Vector2(60, 60);
                    lpr.anchoredPosition = new Vector2(-310, socketY[i]);
                    UpgradeSocketVisual(lp, socketSprite, true, i);
                    if (!leftPoints.Contains(lp.GetComponent<WirePointUI>()))
                    {
                        leftPoints.Add(lp.GetComponent<WirePointUI>());
                    }
                }

                Transform rp = boxT.Find($"RightSocket_{i}");
                if (rp is RectTransform rpr)
                {
                    rpr.sizeDelta = new Vector2(60, 60);
                    rpr.anchoredPosition = new Vector2(310, socketY[i]);
                    UpgradeSocketVisual(rp, socketSprite, false, i);
                    if (!rightPoints.Contains(rp.GetComponent<WirePointUI>()))
                    {
                        rightPoints.Add(rp.GetComponent<WirePointUI>());
                    }
                }
            }

            // ProgressFrame
            Transform pfT = boxT.Find("ProgressFrame");
            if (pfT == null)
            {
                GameObject pfGo = new GameObject("ProgressFrame", typeof(RectTransform));
                pfGo.transform.SetParent(boxT, false);
                RectTransform pfRect = pfGo.GetComponent<RectTransform>();
                pfRect.anchoredPosition = new Vector2(0, -205);
                pfRect.sizeDelta = new Vector2(320, 20);

                connectionLeds = new Image[4];
                float ledW = 62f;
                float ledSp = 16f;
                float ledStartX = -(1.5f * (ledW + ledSp));

                for (int i = 0; i < 4; i++)
                {
                    GameObject ledGo = new GameObject($"StageLed_{i + 1}", typeof(RectTransform), typeof(Image));
                    ledGo.transform.SetParent(pfGo.transform, false);
                    RectTransform lr = ledGo.GetComponent<RectTransform>();
                    lr.sizeDelta = new Vector2(ledW, 14);
                    lr.anchoredPosition = new Vector2(ledStartX + i * (ledW + ledSp), 0);

                    Image li = ledGo.GetComponent<Image>();
                    li.color = new Color(0.18f, 0.22f, 0.28f, 1f);

                    Outline lo = ledGo.AddComponent<Outline>();
                    lo.effectColor = new Color(0.28f, 0.35f, 0.42f, 1f);
                    lo.effectDistance = new Vector2(1, -1);

                    connectionLeds[i] = li;
                }
            }
            else if (pfT is RectTransform pfRect)
            {
                pfRect.anchoredPosition = new Vector2(0, -205);
                pfRect.sizeDelta = new Vector2(320, 20);
                if (connectionLeds == null || connectionLeds.Length != 4)
                {
                    connectionLeds = new Image[4];
                    for (int i = 0; i < 4; i++)
                    {
                        Transform lt = pfT.Find($"StageLed_{i + 1}");
                        if (lt != null && lt.TryGetComponent(out Image li)) connectionLeds[i] = li;
                    }
                }
            }

            // Progress Text
            Transform ptT = boxT.Find("ProgressText");
            if (ptT is RectTransform ptRect)
            {
                ptRect.anchorMin = new Vector2(0.5f, 0.5f);
                ptRect.anchorMax = new Vector2(0.5f, 0.5f);
                ptRect.pivot = new Vector2(0.5f, 0.5f);
                ptRect.anchoredPosition = new Vector2(0, -248);
                ptRect.sizeDelta = new Vector2(700, 28);
                if (ptT.TryGetComponent(out TextMeshProUGUI pt))
                {
                    pt.fontSize = 17;
                    pt.alignment = TextAlignmentOptions.Center;
                    progressText = pt;
                }
            }

            GenerateProceduralAudio();
        }

        private void EnsureConduitHUD(Transform boxT)
        {
            Transform conduit = boxT.Find("ConduitChamber");
            if (conduit == null) return;

            // 1. Sequence Roadmap Badges Bar
            Transform barT = conduit.Find("SequenceRoadmapBar");
            if (barT == null)
            {
                GameObject barGo = new GameObject("SequenceRoadmapBar", typeof(RectTransform));
                barGo.transform.SetParent(conduit, false);
                RectTransform br = barGo.GetComponent<RectTransform>();
                br.anchoredPosition = new Vector2(0, 118);
                br.sizeDelta = new Vector2(330, 32);
                barT = barGo.transform;

                sequenceBadgeBgs = new Image[4];
                sequenceBadgeTexts = new TextMeshProUGUI[4];
                sequenceBadgeOutlines = new Outline[4];

                string[] labels = new string[] { "+5V", "+12V", "+24V", "+48V" };
                float badgeW = 74f;
                float badgeSpacing = 8f;
                float startX = -(1.5f * (badgeW + badgeSpacing));

                for (int i = 0; i < 4; i++)
                {
                    GameObject badgeGo = new GameObject($"Badge_{i}", typeof(RectTransform), typeof(Image), typeof(Outline));
                    badgeGo.transform.SetParent(barT, false);
                    RectTransform bRect = badgeGo.GetComponent<RectTransform>();
                    bRect.sizeDelta = new Vector2(badgeW, 26);
                    bRect.anchoredPosition = new Vector2(startX + i * (badgeW + badgeSpacing), 0);

                    Image bi = badgeGo.GetComponent<Image>();
                    bi.color = new Color(0.08f, 0.10f, 0.14f, 0.8f);
                    sequenceBadgeBgs[i] = bi;

                    Outline bo = badgeGo.GetComponent<Outline>();
                    bo.effectColor = new Color(0.2f, 0.28f, 0.38f, 1f);
                    bo.effectDistance = new Vector2(1, -1);
                    sequenceBadgeOutlines[i] = bo;

                    GameObject txtGo = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
                    txtGo.transform.SetParent(badgeGo.transform, false);
                    RectTransform tr = txtGo.GetComponent<RectTransform>();
                    tr.anchorMin = Vector2.zero;
                    tr.anchorMax = Vector2.one;
                    tr.sizeDelta = Vector2.zero;
                    TextMeshProUGUI bt = txtGo.GetComponent<TextMeshProUGUI>();
                    bt.text = labels[i];
                    bt.fontSize = 12;
                    bt.fontStyle = FontStyles.Bold;
                    bt.alignment = TextAlignmentOptions.Center;
                    bt.color = Color.white;
                    sequenceBadgeTexts[i] = bt;
                }
            }
            else
            {
                if (sequenceBadgeBgs == null || sequenceBadgeBgs.Length != 4)
                {
                    sequenceBadgeBgs = new Image[4];
                    sequenceBadgeTexts = new TextMeshProUGUI[4];
                    sequenceBadgeOutlines = new Outline[4];
                    for (int i = 0; i < 4; i++)
                    {
                        Transform bt = barT.Find($"Badge_{i}");
                        if (bt != null)
                        {
                            sequenceBadgeBgs[i] = bt.GetComponent<Image>();
                            sequenceBadgeOutlines[i] = bt.GetComponent<Outline>();
                            Transform tt = bt.Find("Text");
                            if (tt != null) sequenceBadgeTexts[i] = tt.GetComponent<TextMeshProUGUI>();
                        }
                    }
                }
            }

            // 2. Active Directive Banner Box
            Transform dirT = conduit.Find("DirectiveBox");
            if (dirT == null)
            {
                GameObject dirGo = new GameObject("DirectiveBox", typeof(RectTransform), typeof(Image), typeof(Outline));
                dirGo.transform.SetParent(conduit, false);
                RectTransform dr = dirGo.GetComponent<RectTransform>();
                dr.anchoredPosition = new Vector2(0, 75);
                dr.sizeDelta = new Vector2(330, 28);

                Image di = dirGo.GetComponent<Image>();
                di.color = new Color(0.04f, 0.07f, 0.12f, 0.95f);

                Outline @do = dirGo.GetComponent<Outline>();
                @do.effectColor = new Color(0.18f, 0.45f, 0.75f, 1f);
                @do.effectDistance = new Vector2(1, -1);

                GameObject dtGo = new GameObject("DirectiveText", typeof(RectTransform), typeof(TextMeshProUGUI));
                dtGo.transform.SetParent(dirGo.transform, false);
                RectTransform dtr = dtGo.GetComponent<RectTransform>();
                dtr.anchorMin = Vector2.zero;
                dtr.anchorMax = Vector2.one;
                dtr.sizeDelta = Vector2.zero;
                activeDirectiveText = dtGo.GetComponent<TextMeshProUGUI>();
                activeDirectiveText.text = "ACTIVE TARGET: [PHASE 1/4] CONNECT +5V LOGIC LINE (GREEN)";
                activeDirectiveText.fontSize = 12.5f;
                activeDirectiveText.fontStyle = FontStyles.Bold;
                activeDirectiveText.alignment = TextAlignmentOptions.Center;
                activeDirectiveText.color = new Color(0f, 1f, 0.5f, 1f);
            }
            else
            {
                Transform dt = dirT.Find("DirectiveText");
                if (dt != null) activeDirectiveText = dt.GetComponent<TextMeshProUGUI>();
            }

            // 3. Center Telemetry Status Log (Bottom of chamber)
            Transform logT = conduit.Find("TelemetryLog");
            if (logT == null)
            {
                GameObject logGo = new GameObject("TelemetryLog", typeof(RectTransform), typeof(TextMeshProUGUI));
                logGo.transform.SetParent(conduit, false);
                RectTransform lr = logGo.GetComponent<RectTransform>();
                lr.anchoredPosition = new Vector2(0, -145);
                lr.sizeDelta = new Vector2(330, 42);
                telemetryLogText = logGo.GetComponent<TextMeshProUGUI>();
                telemetryLogText.text = "BUS STATUS: NOMINAL // AWAITING +5V GROUND REFERENCE";
                telemetryLogText.fontSize = 11f;
                telemetryLogText.alignment = TextAlignmentOptions.Center;
                telemetryLogText.color = new Color(0.45f, 0.65f, 0.85f, 1f);
            }
            else
            {
                telemetryLogText = logT.GetComponent<TextMeshProUGUI>();
            }
        }

        #endregion

        /// <summary>
        /// Opens the wiring minigame for the interacting player and task station.
        /// </summary>
        public void Open(PlayerController player, TaskLocation station)
        {
            if (player == null)
            {
                player = PlayerController.Local ?? UnityEngine.Object.FindAnyObjectByType<PlayerController>();
            }

            activePlayer = player;
            activeTaskLocation = station;
            currentSequenceStep = 0;
            connectedCount = 0;
            isFinished = false;
            isFaultLocked = false;

            if (canvas == null)
            {
                canvas = GetComponentInParent<Canvas>();
            }

            EnsureUpgradedVisuals();

            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
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
                headerText.text = $"ELECTRICAL JUNCTION PANEL // {station.StationDisplayName.ToUpper()}{pipelineInfo}";
            }

            SetupWires();

            // Lock player controls while interacting with panel
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

        private void SetupWires()
        {
            currentSequenceStep = 0;
            connectedCount = 0;
            isFinished = false;
            isFaultLocked = false;

            // Both bays are shuffled independently so positions are unpredictable!
            List<WireColorType> leftColors = new List<WireColorType>
            {
                WireColorType.Green,
                WireColorType.Blue,
                WireColorType.Yellow,
                WireColorType.Red
            };
            for (int i = 0; i < leftColors.Count; i++)
            {
                int rnd = Random.Range(i, leftColors.Count);
                (leftColors[i], leftColors[rnd]) = (leftColors[rnd], leftColors[i]);
            }

            List<WireColorType> rightColors = new List<WireColorType>
            {
                WireColorType.Green,
                WireColorType.Blue,
                WireColorType.Yellow,
                WireColorType.Red
            };
            for (int i = 0; i < rightColors.Count; i++)
            {
                int rnd = Random.Range(i, rightColors.Count);
                (rightColors[i], rightColors[rnd]) = (rightColors[rnd], rightColors[i]);
            }

            for (int i = 0; i < leftPoints.Count && i < leftColors.Count; i++)
            {
                leftPoints[i].Initialize(this, canvas, leftColors[i], i);
            }

            for (int i = 0; i < rightPoints.Count && i < rightColors.Count; i++)
            {
                rightPoints[i].Initialize(this, canvas, rightColors[i], i);
            }

            UpdateSequenceHUD();
            UpdateProgressText();
            SetTelemetryStatus("BUS STATUS: NOMINAL // AWAITING +5V GROUND REFERENCE");
        }

        public WirePointUI FindHoveredTarget(Vector2 screenPos)
        {
            Camera eventCam = (canvas != null && canvas.renderMode == RenderMode.ScreenSpaceOverlay) ? null : canvas.worldCamera;

            foreach (var rightPoint in rightPoints)
            {
                if (rightPoint.IsConnected) continue;

                if (RectTransformUtility.RectangleContainsScreenPoint((RectTransform)rightPoint.transform, screenPos, eventCam))
                {
                    return rightPoint;
                }
            }
            return null;
        }

        public bool TryConnectWire(WirePointUI leftPoint, WirePointUI rightPoint)
        {
            if (isFinished || isFaultLocked) return false;
            if (leftPoint == null || rightPoint == null) return false;
            if (leftPoint.IsConnected || rightPoint.IsConnected) return false;

            // Rule 1: Voltage Port Mismatch (e.g. 5V into 12V socket)
            if (leftPoint.ColorType != rightPoint.ColorType)
            {
                PlaySfx(proceduralMismatchClip);
                string lVolt = WireColorHelper.GetVoltageText(leftPoint.ColorType);
                string rVolt = WireColorHelper.GetVoltageText(rightPoint.ColorType);
                SetTelemetryStatus($"<color=#FF9933><b>[PORT MISMATCH] Incompatible pin: {lVolt} cannot mate with {rVolt} socket!</b></color>");
                leftPoint.ResetWire();
                return false;
            }

            // Rule 2: Sequential Calibration Order (Low to High voltage)
            WireColorType requiredType = RequiredSequence[currentSequenceStep];
            if (leftPoint.ColorType != requiredType)
            {
                // Sequence Violation: Breaker Overload Fault!
                TriggerBreakerFault(leftPoint, rightPoint);
                return false;
            }

            // Success! Complete this connection
            leftPoint.ConnectTo(rightPoint);
            currentSequenceStep++;
            connectedCount++;

            float pitchFreq = 480f + currentSequenceStep * 90f;
            PlaySfx(CreateTone(pitchFreq, 0.16f, 0.45f));

            UpdateSequenceHUD();
            UpdateProgressText();

            if (connectedCount >= RequiredSequence.Length && !isFinished)
            {
                StartCoroutine(CompleteRoutine());
            }
            else
            {
                WireColorType nextType = RequiredSequence[currentSequenceStep];
                string nextVolt = WireColorHelper.GetVoltageText(nextType);
                string nextRole = WireColorHelper.GetSubsystem(nextType);
                SetTelemetryStatus($"<color=#00E5FF><b>[PHASE {connectedCount} CLOSED] BUS ENERGIZED. PROCEED WITH {nextVolt} [{nextRole}].</b></color>");
            }

            return true;
        }

        private void TriggerBreakerFault(WirePointUI attemptedLeft, WirePointUI attemptedRight)
        {
            if (isFaultLocked || isFinished) return;
            StartCoroutine(BreakerFaultRoutine(attemptedLeft, attemptedRight));
        }

        private IEnumerator BreakerFaultRoutine(WirePointUI attemptedLeft, WirePointUI attemptedRight)
        {
            isFaultLocked = true;

            if (attemptedLeft != null) attemptedLeft.ResetWire();

            PlaySfx(proceduralOverloadClip);

            WireColorType expectedType = RequiredSequence[currentSequenceStep];
            string attemptedVolt = WireColorHelper.GetVoltageText(attemptedLeft.ColorType);
            string expectedVolt = WireColorHelper.GetVoltageText(expectedType);

            SetTelemetryStatus($"<color=#FF3333><b>[!] [BREAKER OVERLOAD] SEQUENCE FAULT! [!]\nCANNOT ENERGIZE {attemptedVolt} BEFORE {expectedVolt}!</b></color>");

            // Screen shake on TerminalBox
            Transform boxT = rootPanel != null ? rootPanel.transform.Find("TerminalBox") : null;
            RectTransform boxRect = boxT != null ? boxT.GetComponent<RectTransform>() : null;
            Vector2 origPos = boxRect != null ? boxRect.anchoredPosition : Vector2.zero;

            float shakeTime = 0.4f;
            float elapsed = 0f;
            while (elapsed < shakeTime)
            {
                elapsed += Time.unscaledDeltaTime;
                float p = 1f - (elapsed / shakeTime);
                if (boxRect != null)
                {
                    boxRect.anchoredPosition = origPos + new Vector2(Random.Range(-7f, 7f) * p, Random.Range(-5f, 5f) * p);
                }
                yield return null;
            }
            if (boxRect != null) boxRect.anchoredPosition = origPos;

            // Flash all Stage LEDs Red
            if (connectionLeds != null)
            {
                for (int flash = 0; flash < 3; flash++)
                {
                    for (int i = 0; i < connectionLeds.Length; i++)
                    {
                        if (connectionLeds[i] != null) connectionLeds[i].color = LedFault;
                    }
                    yield return new WaitForSecondsRealtime(0.09f);
                    for (int i = 0; i < connectionLeds.Length; i++)
                    {
                        if (connectionLeds[i] != null) connectionLeds[i].color = LedInactive;
                    }
                    yield return new WaitForSecondsRealtime(0.09f);
                }
            }

            // Disconnect all connected wires (Breaker Trip discharges everything)
            foreach (var lp in leftPoints)
            {
                if (lp != null) lp.ResetWire();
            }
            foreach (var rp in rightPoints)
            {
                if (rp != null) rp.ResetWire();
            }

            currentSequenceStep = 0;
            connectedCount = 0;

            UpdateSequenceHUD();
            UpdateProgressText();

            SetTelemetryStatus("<color=#FF9944><b>[CIRCUIT BREAKER RESET] ALL BUSES DISCHARGED. RE-CALIBRATING...</b></color>");

            yield return new WaitForSecondsRealtime(0.35f);
            SetTelemetryStatus("BUS STATUS: NOMINAL // AWAITING +5V GROUND REFERENCE");
            isFaultLocked = false;
        }

        private void UpdateSequenceHUD()
        {
            if (sequenceBadgeBgs != null && sequenceBadgeBgs.Length == 4)
            {
                for (int i = 0; i < 4; i++)
                {
                    WireColorType type = RequiredSequence[i];
                    Color c = WireColorHelper.GetColor(type);
                    string v = WireColorHelper.GetVoltageText(type);

                    if (sequenceBadgeBgs[i] == null) continue;

                    if (i < currentSequenceStep)
                    {
                        // Completed
                        sequenceBadgeBgs[i].color = new Color(0.05f, 0.35f, 0.15f, 0.95f);
                        if (sequenceBadgeOutlines[i] != null)
                        {
                            sequenceBadgeOutlines[i].effectColor = new Color(0f, 1f, 0.5f, 1f);
                        }
                        if (sequenceBadgeTexts[i] != null)
                        {
                            sequenceBadgeTexts[i].text = $"<b>[OK] {v}</b>";
                            sequenceBadgeTexts[i].color = Color.white;
                        }
                    }
                    else if (i == currentSequenceStep)
                    {
                        // Active Target
                        sequenceBadgeBgs[i].color = c * 0.35f;
                        if (sequenceBadgeOutlines[i] != null)
                        {
                            sequenceBadgeOutlines[i].effectColor = c;
                        }
                        if (sequenceBadgeTexts[i] != null)
                        {
                            sequenceBadgeTexts[i].text = $"<b>>> {v}</b>";
                            sequenceBadgeTexts[i].color = Color.white;
                        }
                    }
                    else
                    {
                        // Pending
                        sequenceBadgeBgs[i].color = new Color(0.08f, 0.10f, 0.14f, 0.8f);
                        if (sequenceBadgeOutlines[i] != null)
                        {
                            sequenceBadgeOutlines[i].effectColor = new Color(0.2f, 0.28f, 0.38f, 1f);
                        }
                        if (sequenceBadgeTexts[i] != null)
                        {
                            sequenceBadgeTexts[i].text = $"<color=#667788>{v}</color>";
                        }
                    }
                }
            }

            // Update Directive Banner
            if (activeDirectiveText != null)
            {
                if (currentSequenceStep < RequiredSequence.Length)
                {
                    WireColorType target = RequiredSequence[currentSequenceStep];
                    string v = WireColorHelper.GetVoltageText(target);
                    string r = WireColorHelper.GetSubsystem(target);
                    string cName = target.ToString().ToUpper();
                    activeDirectiveText.text = $">>> TARGET: [PHASE {currentSequenceStep + 1}/4] CONNECT {v} {r} ({cName}) <<<";
                    activeDirectiveText.color = WireColorHelper.GetColor(target);
                }
                else
                {
                    activeDirectiveText.text = ">>> ALL CIRCUIT PHASES SYNCHRONIZED & LOCKED <<<";
                    activeDirectiveText.color = new Color(0f, 1f, 0.5f, 1f);
                }
            }
        }

        private void UpdateProgressText()
        {
            if (progressText != null)
            {
                if (connectedCount >= 4)
                {
                    progressText.text = "<color=#00FF7F><b>>>> CIRCUIT COMPLETED -- ALL BUSES ENERGIZED <<<</b></color>";
                }
                else
                {
                    WireColorType nextType = RequiredSequence[currentSequenceStep];
                    string nextVolt = WireColorHelper.GetVoltageText(nextType);
                    string nextRole = WireColorHelper.GetSubsystem(nextType);
                    progressText.text = $"WIRES BRIDGED: <color=#00FF7F><b>{connectedCount}</b></color> / 4  //  NEXT REQUIRED: <color=#00E5FF><b>{nextVolt} [{nextRole}]</b></color>";
                }
            }

            if (connectionLeds != null)
            {
                for (int i = 0; i < connectionLeds.Length; i++)
                {
                    if (connectionLeds[i] != null)
                    {
                        connectionLeds[i].color = i < connectedCount ? LedActive : LedInactive;
                    }
                }
            }
        }

        private void SetTelemetryStatus(string msg)
        {
            if (telemetryLogText != null)
            {
                telemetryLogText.text = msg;
            }
        }

        private IEnumerator CompleteRoutine()
        {
            isFinished = true;

            if (connectionLeds != null)
            {
                for (int i = 0; i < connectionLeds.Length; i++)
                {
                    if (connectionLeds[i] != null) connectionLeds[i].color = LedActive;
                }
            }

            SetTelemetryStatus("<color=#00FF7F><b>>>> SYSTEM VOLTAGE NOMINAL // APOMAT RELAYS LOCKED <<<</b></color>");
            PlaySfx(taskCompletedSfx ?? proceduralCompleteClip);

            yield return new WaitForSeconds(0.9f);

            // Notify station
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

            // Restore player control
            if (activePlayer != null)
            {
                activePlayer.SetControlEnabled(true);
                activePlayer = null;
            }

            activeTaskLocation = null;
        }

        #region Audio Synthesis

        private void GenerateProceduralAudio()
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
            }

            if (proceduralConnectClip == null)
            {
                proceduralConnectClip = CreateTone(587.33f, 0.16f, 0.45f);
            }

            if (proceduralCompleteClip == null)
            {
                proceduralCompleteClip = CreateChimeTone();
            }

            if (proceduralOverloadClip == null)
            {
                proceduralOverloadClip = CreateOverloadTone();
            }

            if (proceduralMismatchClip == null)
            {
                proceduralMismatchClip = CreateMismatchTone();
            }
        }

        private void PlaySfx(AudioClip clip)
        {
            if (audioSource == null) return;
            AudioClip target = clip ?? proceduralConnectClip;
            if (target != null)
            {
                audioSource.PlayOneShot(target);
            }
        }

        private static AudioClip CreateTone(float frequency, float duration, float volume = 0.5f)
        {
            int sampleRate = 44100;
            int samples = (int)(sampleRate * duration);
            float[] data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / sampleRate;
                float envelope = 1f;
                if (t < 0.015f) envelope = t / 0.015f;
                else if (t > duration - 0.04f) envelope = (duration - t) / 0.04f;
                data[i] = Mathf.Sin(2f * Mathf.PI * frequency * t) * volume * envelope;
            }
            AudioClip clip = AudioClip.Create($"WireTone_{frequency}", samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip CreateChimeTone()
        {
            int sampleRate = 44100;
            float duration = 0.6f;
            int samples = (int)(sampleRate * duration);
            float[] data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Clamp01((duration - t) / duration);
                data[i] = (Mathf.Sin(2f * Mathf.PI * 523.25f * t) * 0.35f +
                           Mathf.Sin(2f * Mathf.PI * 659.25f * t) * 0.35f +
                           Mathf.Sin(2f * Mathf.PI * 783.99f * t) * 0.35f) * env;
            }
            AudioClip clip = AudioClip.Create("WiringCompleteChime", samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip CreateOverloadTone()
        {
            int sampleRate = 44100;
            float duration = 0.45f;
            int samples = (int)(sampleRate * duration);
            float[] data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Clamp01((duration - t) / duration);
                float freq = Mathf.Lerp(180f, 42f, t / duration);
                float wave = Mathf.Sign(Mathf.Sin(2f * Mathf.PI * freq * t)) * 0.4f;
                float noise = (Random.value * 2f - 1f) * 0.35f;
                data[i] = Mathf.Clamp((wave + noise) * env * 0.75f, -1f, 1f);
            }
            AudioClip clip = AudioClip.Create("WireOverloadTone", samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip CreateMismatchTone()
        {
            int sampleRate = 44100;
            float duration = 0.15f;
            int samples = (int)(sampleRate * duration);
            float[] data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Sin(Mathf.PI * (t / duration));
                float wave = Mathf.Sign(Mathf.Sin(2f * Mathf.PI * 160f * t)) * 0.3f;
                data[i] = wave * env;
            }
            AudioClip clip = AudioClip.Create("WireMismatchTone", samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        #endregion
    }
}
