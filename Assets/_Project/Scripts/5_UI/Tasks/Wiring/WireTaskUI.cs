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
    /// Sci-Fi Brutalist Electrical Wire Bridging Minigame (Among Us style).
    /// Synchronized with Reactor Memory and Valve Balancing terminal aesthetic:
    /// - Heavy metallic brutalist chassis (TerminalBezel) with hazard stripe header
    /// - Dual terminal bays: Feed Bus (A-Bay) on left, Load Bus (B-Bay) on right
    /// - Recessed center bridging conduit trough
    /// - Hex socket ports with colored cores and status indicator LEDs
    /// - 4-stage connection progress LEDs
    /// - Procedural audio synthesis for tactile connect clicks and victory chime
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
                    _instance = Object.FindAnyObjectByType<WireTaskUI>(FindObjectsInactive.Include);
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

        [Header("Progress LEDs")]
        [SerializeField] private Image[] connectionLeds = new Image[4];

        [Header("Wiring Sockets")]
        [SerializeField] private List<WirePointUI> leftPoints = new List<WirePointUI>();
        [SerializeField] private List<WirePointUI> rightPoints = new List<WirePointUI>();

        [Header("Audio")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip wireConnectedSfx;
        [SerializeField] private AudioClip taskCompletedSfx;

        private Canvas canvas;
        private PlayerController activePlayer;
        private TaskLocation activeTaskLocation;
        private int connectedCount = 0;
        private bool isFinished = false;

        private AudioClip proceduralConnectClip;
        private AudioClip proceduralCompleteClip;

        // Visual Colors
        private readonly Color LedInactive = new Color(0.18f, 0.22f, 0.28f, 1f);
        private readonly Color LedActive = new Color(0f, 1f, 0.5f, 1f);

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

            Canvas canvas = Object.FindAnyObjectByType<Canvas>();
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

            Image bgOverlay = root.AddComponent<Image>();
            bgOverlay.color = new Color(0.02f, 0.04f, 0.06f, 0.88f);

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
            headerRect.anchoredPosition = new Vector2(0, -32);
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
            instRect.anchoredPosition = new Vector2(0, -68);
            instRect.sizeDelta = new Vector2(-120, 26);
            TextMeshProUGUI instText = instGo.GetComponent<TextMeshProUGUI>();
            instText.text = "Connect matching colored terminal lines [Drag Left to Right]";
            instText.fontSize = 16;
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
            progressRect.anchoredPosition = new Vector2(0, -250);
            progressRect.sizeDelta = new Vector2(600, 28);
            TextMeshProUGUI progressText = progressGo.GetComponent<TextMeshProUGUI>();
            progressText.text = "WIRES BRIDGED: <color=#00FF7F><b>0</b></color> / 4";
            progressText.fontSize = 18;
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

                GameObject hGo = new GameObject("ConduitHeader", typeof(RectTransform), typeof(TextMeshProUGUI));
                hGo.transform.SetParent(conGo.transform, false);
                RectTransform hr = hGo.GetComponent<RectTransform>();
                hr.anchoredPosition = new Vector2(0, 155);
                hr.sizeDelta = new Vector2(320, 24);
                TextMeshProUGUI ht = hGo.GetComponent<TextMeshProUGUI>();
                ht.text = "INTER-BUS BRIDGING DUCT";
                ht.fontSize = 12;
                ht.fontStyle = FontStyles.Bold;
                ht.alignment = TextAlignmentOptions.Center;
                ht.color = new Color(0.35f, 0.50f, 0.65f, 1f);
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
                lblR.sizeDelta = new Vector2(60, 20);
                lblR.anchoredPosition = isLeft ? new Vector2(48, 0) : new Vector2(-48, 0);
                TextMeshProUGUI lt = lblGo.GetComponent<TextMeshProUGUI>();
                lt.text = isLeft ? $"[A-{index + 1}]" : $"[B-{index + 1}]";
                lt.fontSize = 12;
                lt.fontStyle = FontStyles.Bold;
                lt.alignment = isLeft ? TextAlignmentOptions.Left : TextAlignmentOptions.Right;
                lt.color = isLeft ? new Color(0.45f, 0.65f, 0.85f, 1f) : new Color(0.70f, 0.80f, 0.90f, 1f);
                lt.raycastTarget = false;
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
            lblR.sizeDelta = new Vector2(60, 20);
            lblR.anchoredPosition = isLeft ? new Vector2(48, 0) : new Vector2(-48, 0);
            TextMeshProUGUI lt = lblGo.GetComponent<TextMeshProUGUI>();
            lt.text = isLeft ? $"[A-{index + 1}]" : $"[B-{index + 1}]";
            lt.fontSize = 12;
            lt.fontStyle = FontStyles.Bold;
            lt.alignment = isLeft ? TextAlignmentOptions.Left : TextAlignmentOptions.Right;
            lt.color = isLeft ? new Color(0.45f, 0.65f, 0.85f, 1f) : new Color(0.70f, 0.80f, 0.90f, 1f);
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
            pointUI.Configure(isLeft, coreImg, lightImg, wireRect);

            return pointUI;
        }

        #endregion

        #region Runtime Upgrade & Layout

        /// <summary>
        /// Automatically upgrades/re-aligns any existing scene hierarchy to the synchronized brutalist terminal layout.
        /// </summary>
        public void EnsureUpgradedVisuals()
        {
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

            // Remove/hide masterStatusLight (Bỏ cục vàng/đỏ góc trên)
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
                headRect.anchoredPosition = new Vector2(0, -32);
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
                ir.anchoredPosition = new Vector2(0, -68);
                ir.sizeDelta = new Vector2(-120, 26);
                TextMeshProUGUI it = instGo.GetComponent<TextMeshProUGUI>();
                it.text = "Connect matching colored terminal lines [Drag Left to Right]";
                it.fontSize = 16;
                it.alignment = TextAlignmentOptions.Center;
                it.color = new Color(0.72f, 0.82f, 0.92f, 1f);
                instructionText = it;
            }
            else if (instT is RectTransform ir)
            {
                ir.anchorMin = new Vector2(0f, 1f);
                ir.anchorMax = new Vector2(1f, 1f);
                ir.pivot = new Vector2(0.5f, 1f);
                ir.anchoredPosition = new Vector2(0, -68);
                ir.sizeDelta = new Vector2(-120, 26);
                if (instT.TryGetComponent(out TextMeshProUGUI it))
                {
                    it.text = "Connect matching colored terminal lines [Drag Left to Right]";
                    it.fontSize = 16;
                    it.alignment = TextAlignmentOptions.Center;
                    it.color = new Color(0.72f, 0.82f, 0.92f, 1f);
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

            // Check / Create Decorative Bays behind sockets
            EnsureBayPanels(boxT);

            // Reposition sockets and set WireSocket sprite
            float[] socketY = new float[] { 114f, 50f, -14f, -78f };
            for (int i = 0; i < 4; i++)
            {
                Transform lp = boxT.Find($"LeftSocket_{i}");
                if (lp is RectTransform lpr)
                {
                    lpr.sizeDelta = new Vector2(60, 60);
                    lpr.anchoredPosition = new Vector2(-310, socketY[i]);
                    UpgradeSocketVisual(lp, socketSprite, true, i);
                }

                Transform rp = boxT.Find($"RightSocket_{i}");
                if (rp is RectTransform rpr)
                {
                    rpr.sizeDelta = new Vector2(60, 60);
                    rpr.anchoredPosition = new Vector2(310, socketY[i]);
                    UpgradeSocketVisual(rp, socketSprite, false, i);
                }
            }

            // Check / Create ProgressFrame
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
                ptRect.anchoredPosition = new Vector2(0, -250);
                ptRect.sizeDelta = new Vector2(600, 28);
                if (ptT.TryGetComponent(out TextMeshProUGUI pt))
                {
                    pt.fontSize = 18;
                    pt.alignment = TextAlignmentOptions.Center;
                    progressText = pt;
                }
            }

            GenerateProceduralAudio();
        }

        #endregion

        /// <summary>
        /// Opens the wiring minigame for the interacting player and task station.
        /// </summary>
        public void Open(PlayerController player, TaskLocation station)
        {
            activePlayer = player;
            activeTaskLocation = station;
            connectedCount = 0;
            isFinished = false;

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
                headerText.text = $"ELECTRICAL JUNCTION PANEL // {station.StationDisplayName.ToUpper()}";
            }

            UpdateProgressText();

            // Lock player controls while interacting with panel
            if (activePlayer != null)
            {
                activePlayer.SetControlEnabled(false);
            }

            SetupWires();

            if (rootPanel != null)
            {
                rootPanel.SetActive(true);
            }
        }

        private void SetupWires()
        {
            List<WireColorType> colors = new List<WireColorType>
            {
                WireColorType.Red,
                WireColorType.Blue,
                WireColorType.Yellow,
                WireColorType.Green
            };

            for (int i = 0; i < leftPoints.Count && i < colors.Count; i++)
            {
                leftPoints[i].Initialize(this, canvas, colors[i]);
            }

            List<WireColorType> shuffledColors = new List<WireColorType>(colors);
            for (int i = 0; i < shuffledColors.Count; i++)
            {
                int rnd = Random.Range(i, shuffledColors.Count);
                (shuffledColors[i], shuffledColors[rnd]) = (shuffledColors[rnd], shuffledColors[i]);
            }

            for (int i = 0; i < rightPoints.Count && i < shuffledColors.Count; i++)
            {
                rightPoints[i].Initialize(this, canvas, shuffledColors[i]);
            }
        }

        public WirePointUI CheckConnectTarget(Vector2 screenPos, WireColorType wireColor)
        {
            Camera eventCam = (canvas != null && canvas.renderMode == RenderMode.ScreenSpaceOverlay) ? null : canvas.worldCamera;

            foreach (var rightPoint in rightPoints)
            {
                if (rightPoint.IsConnected) continue;

                if (RectTransformUtility.RectangleContainsScreenPoint((RectTransform)rightPoint.transform, screenPos, eventCam))
                {
                    if (rightPoint.ColorType == wireColor)
                    {
                        return rightPoint;
                    }
                }
            }
            return null;
        }

        public void OnWireConnected(WirePointUI left, WirePointUI right)
        {
            connectedCount++;
            PlaySfx(wireConnectedSfx);
            UpdateProgressText();

            if (connectedCount >= leftPoints.Count && !isFinished)
            {
                StartCoroutine(CompleteRoutine());
            }
        }

        private void UpdateProgressText()
        {
            if (progressText != null)
            {
                if (connectedCount >= 4)
                {
                    progressText.text = "<color=#00FF7F><b>>>> CIRCUIT COMPLETED — ALL TERMINALS BRIDGED <<<</b></color>";
                }
                else
                {
                    progressText.text = $"WIRES BRIDGED: <color=#00FF7F><b>{connectedCount}</b></color> / 4";
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

            PlaySfx(taskCompletedSfx ?? proceduralCompleteClip);

            yield return new WaitForSeconds(0.85f);

            // Notify station
            if (activeTaskLocation != null)
            {
                activeTaskLocation.SetCompleted(true);
                if (activeTaskLocation.TryGetComponent(out StationRepair repair))
                {
                    repair.CompleteRepair();
                }
            }

            CloseMinigame();
        }

        public void CloseMinigame()
        {
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
                proceduralConnectClip = CreateTone(587.33f, 0.16f, 0.45f); // High D5 connect spark beep
            }

            if (proceduralCompleteClip == null)
            {
                proceduralCompleteClip = CreateChimeTone();
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
            float duration = 0.55f;
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

        #endregion
    }
}
