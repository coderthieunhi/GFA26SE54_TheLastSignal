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
    /// - Custom pixel-art pipe valve wheel
    /// - High-visibility glowing green safe zone dial
    /// - Non-overlapping multi-line status display:
    ///     Low Pressure
    ///     (Increase Value)
    /// </summary>
    public class ValveTaskUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private static ValveTaskUI _instance;
        public static ValveTaskUI Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = Object.FindAnyObjectByType<ValveTaskUI>(FindObjectsInactive.Include);
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
        [SerializeField] private float valveSensitivity = 0.5f;

        [Header("Audio (Optional)")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioSource turnAudioSource;
        [SerializeField] private AudioClip valveTurnSfx;
        [SerializeField] private AudioClip successSfx;

        private AudioClip proceduralTurnClip;
        private AudioClip proceduralCompleteClip;
        private float accumulatedAngleForSfx = 0f;
        private const float AnglePerRatchetClick = 16f;

        private Canvas canvas;
        private PlayerController activePlayer;
        private TaskLocation activeTaskLocation;

        private float currentPressure = 15f;
        private float holdTimer = 0f;
        private bool isFinished = false;
        private bool isDraggingValve = false;
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

            EnsureUpgradedVisuals();
            EnsureAudioSources();
            GenerateProceduralAudio();

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

            // Passive decay when not adjusting valve
            if (!isDraggingValve)
            {
                currentPressure = Mathf.MoveTowards(currentPressure, 10f, pressureDecayRate * Time.deltaTime);
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
            if (kb == null) return;

            float keyDelta = 0f;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) keyDelta += 1f;
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) keyDelta -= 1f;

            if (Mathf.Abs(keyDelta) > 0.01f)
            {
                float rotDelta = -keyDelta * 160f * Time.deltaTime;
                currentPressure += keyDelta * 48f * Time.deltaTime;
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
                string statusColor = inSafeZone ? "#00FF7F" : (currentPressure > maxSafePressure ? "#FF3B30" : "#E0E0E0");
                pressureReadoutText.text = $"<color={statusColor}><size=30><b>{currentPressure:00.0}</b></size> <size=18>PSI</size></color>";
            }

            float progress01 = Mathf.Clamp01(holdTimer / requiredHoldSeconds);
            if (stabilizationBar != null)
            {
                stabilizationBar.fillAmount = progress01;
                stabilizationBar.color = inSafeZone ? new Color(0f, 0.95f, 0.45f) : new Color(0.95f, 0.45f, 0.1f);
            }

            // Custom user requested 2-line format
            if (stabilizationPercentText != null)
            {
                if (inSafeZone)
                {
                    int pct = Mathf.RoundToInt(progress01 * 100f);
                    stabilizationPercentText.text = $"<color=#00FF7F><b>Optimal Pressure</b></color>\n<size=15><color=#A0FFA0>(Hold Steady - {pct}%)</color></size>";
                }
                else if (currentPressure < minSafePressure)
                {
                    stabilizationPercentText.text = $"<color=#FFA500><b>Low Pressure</b></color>\n<size=15><color=#CCCCCC>(Increase Value)</color></size>";
                }
                else
                {
                    stabilizationPercentText.text = $"<color=#FF3B30><b>High Pressure</b></color>\n<size=15><color=#CCCCCC>(Decrease Value)</color></size>";
                }
            }
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (isFinished || valveWheel == null) return;
            isDraggingValve = true;
            previousMouseAngle = GetMouseAngleOnValve(eventData.position);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (isFinished || !isDraggingValve || valveWheel == null) return;

            float currentAngle = GetMouseAngleOnValve(eventData.position);
            float angleDelta = Mathf.DeltaAngle(previousMouseAngle, currentAngle);

            float pressureDelta = -angleDelta * valveSensitivity;
            currentPressure += pressureDelta;

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
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(valveWheel, screenPos, cam, out Vector2 localPoint))
            {
                return Mathf.Atan2(localPoint.y, localPoint.x) * Mathf.Rad2Deg;
            }
            return 0f;
        }

        public void Open(PlayerController player, TaskLocation station)
        {
            activePlayer = player;
            activeTaskLocation = station;
            currentPressure = 15f;
            holdTimer = 0f;
            isFinished = false;
            isDraggingValve = false;
            accumulatedAngleForSfx = 0f;

            if (canvas == null)
            {
                canvas = GetComponentInParent<Canvas>();
            }

            EnsureUpgradedVisuals();
            EnsureAudioSources();
            GenerateProceduralAudio();

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
        /// Automatically upgrades any existing/serialized scene instance to the new custom sprites and layout.
        /// </summary>
        public void EnsureUpgradedVisuals()
        {
            minSafePressure = 50f;
            maxSafePressure = 88f;

            // 0. Remove yellow/orange status light (User requested: Bỏ cục vàng bên trái trên cùng)
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

            // 1. Terminal Frame
            if (rootPanel != null && rootPanel.transform.Find("TerminalBox") is Transform boxT)
            {
                if (boxT.TryGetComponent(out Image boxImg) && bezelSprite != null)
                {
                    boxImg.sprite = bezelSprite;
                    boxImg.color = Color.white;
                }
            }

            // 2. Gauge Housing & Circular Dial
            Transform housing = null;
            if (gaugeNeedle != null && gaugeNeedle.parent != null)
            {
                housing = gaugeNeedle.parent;
            }
            else if (gaugeDialImage != null)
            {
                housing = gaugeDialImage.transform;
            }
            else if (rootPanel != null)
            {
                housing = rootPanel.transform.Find("TerminalBox/GaugeHousing") ?? rootPanel.transform.Find("GaugeHousing");
            }

            if (housing != null)
            {
                if (housing.TryGetComponent(out Image hImg))
                {
                    gaugeDialImage = hImg;
                    if (dialSprite != null)
                    {
                        hImg.sprite = dialSprite;
                        hImg.color = Color.white;
                    }
                }

                if (housing.TryGetComponent(out RectTransform hRect))
                {
                    hRect.sizeDelta = new Vector2(300, 300);
                }

                // Hide old green square safeZoneArc child completely!
                Transform oldArc = housing.Find("SafeZoneArc");
                if (oldArc != null)
                {
                    oldArc.gameObject.SetActive(false);
                }

                // Hide old square NeedleCap child
                Transform oldCap = housing.Find("NeedleCap");
                if (oldCap != null)
                {
                    oldCap.gameObject.SetActive(false);
                }

                // Adjust digital readout position nicely in lower center of round dial
                Transform psiT = housing.Find("PsiReadout");
                if (psiT != null && psiT.TryGetComponent(out RectTransform psiRect))
                {
                    psiRect.anchoredPosition = new Vector2(0, -65);
                    psiRect.sizeDelta = new Vector2(200, 40);
                }
            }

            // 3. Gauge Needle
            if (gaugeNeedle != null)
            {
                if (gaugeNeedle.TryGetComponent(out Image needleImg) && needleSprite != null)
                {
                    needleImg.sprite = needleSprite;
                    needleImg.color = Color.white;
                }
                gaugeNeedle.sizeDelta = new Vector2(34, 180);
                gaugeNeedle.pivot = new Vector2(0.5f, 0.18f);
                gaugeNeedle.anchoredPosition = Vector2.zero;
            }

            // 4. Valve Wheel - Red Pipe Valve
            if (valveWheel != null)
            {
                valveWheel.sizeDelta = new Vector2(300, 300);
                if (valveWheel.TryGetComponent(out Image wImg) && valveSprite != null)
                {
                    wImg.sprite = valveSprite;
                    wImg.color = Color.white;
                }

                // Deactivate any old square/rectangle children (spokes, knob)
                for (int i = 0; i < valveWheel.childCount; i++)
                {
                    valveWheel.GetChild(i).gameObject.SetActive(false);
                }
            }

            // 5. Layout adjustment: Ensure Progress Bar and Text do not collide!
            if (stabilizationBarFrame != null)
            {
                stabilizationBarFrame.anchoredPosition = new Vector2(0, -185);
                stabilizationBarFrame.sizeDelta = new Vector2(740, 24);
            }
            else if (stabilizationBar != null && stabilizationBar.transform.parent is RectTransform parentFrame)
            {
                parentFrame.anchoredPosition = new Vector2(0, -185);
                parentFrame.sizeDelta = new Vector2(740, 24);
            }

            if (stabilizationPercentText != null)
            {
                RectTransform pctRect = stabilizationPercentText.rectTransform;
                pctRect.anchoredPosition = new Vector2(0, -250);
                pctRect.sizeDelta = new Vector2(650, 65);
                stabilizationPercentText.alignment = TextAlignmentOptions.Center;
                stabilizationPercentText.fontSize = 17;
                stabilizationPercentText.lineSpacing = 15;
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
                stabilizationPercentText.text = "<color=#00FF7F><b>Pressure Stabilized</b></color>\n<size=15><color=#FFFFFF>(Task Completed)</color></size>";
            }

            AudioClip doneClip = successSfx ?? proceduralCompleteClip;
            if (audioSource != null && doneClip != null)
            {
                audioSource.PlayOneShot(doneClip, 0.85f);
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
                AudioSource[] sources = GetComponents<AudioSource>();
                if (sources != null && sources.Length > 0)
                {
                    audioSource = sources[0];
                    if (sources.Length > 1) turnAudioSource = sources[1];
                }
                else
                {
                    audioSource = gameObject.AddComponent<AudioSource>();
                }
            }
            if (turnAudioSource == null)
            {
                AudioSource[] sources = GetComponents<AudioSource>();
                if (sources != null && sources.Length > 1)
                {
                    turnAudioSource = sources[1];
                }
                else
                {
                    turnAudioSource = gameObject.AddComponent<AudioSource>();
                }
            }
            if (audioSource != null) audioSource.playOnAwake = false;
            if (turnAudioSource != null) turnAudioSource.playOnAwake = false;
        }

        private void GenerateProceduralAudio()
        {
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
            AudioClip clip = valveTurnSfx ?? proceduralTurnClip;
            if (turnAudioSource != null && clip != null)
            {
                turnAudioSource.pitch = UnityEngine.Random.Range(0.92f, 1.08f);
                turnAudioSource.PlayOneShot(clip, 0.40f);
            }
            else if (audioSource != null && clip != null)
            {
                audioSource.PlayOneShot(clip, 0.40f);
            }
        }

        private static AudioClip CreateValveRatchetTone()
        {
            int sampleRate = 44100;
            float duration = 0.055f;
            int samples = (int)(sampleRate * duration);
            float[] data = new float[samples];
            System.Random rng = new System.Random(1337);

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / sampleRate;
                float progress = t / duration;

                // Attack envelope (very fast ramp up, sharp exponential decay)
                float env = Mathf.Exp(-progress * 14f);

                // Noise element for metallic friction/grit
                float noise = ((float)rng.NextDouble() * 2f - 1f) * 0.35f * Mathf.Exp(-progress * 22f);

                // Heavy metallic clank harmonics (ratchet gear tooth engagement)
                float h1 = Mathf.Sin(2f * Mathf.PI * 380f * t) * 0.40f;
                float h2 = Mathf.Sin(2f * Mathf.PI * 760f * t) * 0.30f;
                float h3 = Mathf.Sin(2f * Mathf.PI * 1420f * t) * 0.15f;

                float sample = (noise + h1 + h2 + h3) * env;
                data[i] = Mathf.Clamp(sample * 0.85f, -1f, 1f);
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

                data[i] = (thud + (f1 + f2 + f3) * env) * 0.85f;
            }

            AudioClip clip = AudioClip.Create("ValveCompleteHydraulicChime", samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        #endregion

        public static ValveTaskUI GetOrCreateInstance()
        {
            if (Instance != null) return Instance;

            Canvas canvas = Object.FindAnyObjectByType<Canvas>();
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
            GameObject root = new GameObject("ValveMinigamePanel", typeof(RectTransform), typeof(AudioSource), typeof(AudioSource));
            root.transform.SetParent(canvas.transform, false);

            RectTransform rootRect = root.GetComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;

            Image bgOverlay = root.AddComponent<Image>();
            bgOverlay.color = new Color(0.02f, 0.04f, 0.06f, 0.88f);

            // Center Terminal Box (Using TerminalBezel Sprite)
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
            headerText.fontSize = 22;
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
            instText.fontSize = 16;
            instText.alignment = TextAlignmentOptions.Center;
            instText.color = new Color(0.72f, 0.82f, 0.92f, 1f);

            // Master Status LED removed per user request (Bỏ cục vàng bên trái trên cùng)
            Image lightImg = null;

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

            // LEFT SIDE: GAUGE HOUSING
            GameObject gaugeGo = new GameObject("GaugeHousing", typeof(RectTransform), typeof(Image));
            gaugeGo.transform.SetParent(box.transform, false);
            RectTransform gRect = gaugeGo.GetComponent<RectTransform>();
            gRect.sizeDelta = new Vector2(330, 330);
            gRect.anchoredPosition = new Vector2(-200, -18);
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

            // GAUGE NEEDLE
            GameObject needleGo = new GameObject("Needle", typeof(RectTransform), typeof(Image));
            needleGo.transform.SetParent(gaugeGo.transform, false);
            RectTransform needleRect = needleGo.GetComponent<RectTransform>();
            needleRect.sizeDelta = new Vector2(34, 180);
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

            // DIGITAL PSI READOUT
            GameObject psiGo = new GameObject("PsiReadout", typeof(RectTransform), typeof(TextMeshProUGUI));
            psiGo.transform.SetParent(gaugeGo.transform, false);
            RectTransform psiRect = psiGo.GetComponent<RectTransform>();
            psiRect.anchoredPosition = new Vector2(0, -68);
            psiRect.sizeDelta = new Vector2(200, 44);
            TextMeshProUGUI psiText = psiGo.GetComponent<TextMeshProUGUI>();
            psiText.text = "15.0 PSI";
            psiText.fontSize = 24;
            psiText.alignment = TextAlignmentOptions.Center;
            psiText.color = Color.white;

            // RIGHT SIDE: VALVE WHEEL
            GameObject wheelGo = new GameObject("ValveWheel", typeof(RectTransform), typeof(Image));
            wheelGo.transform.SetParent(box.transform, false);
            RectTransform wRect = wheelGo.GetComponent<RectTransform>();
            wRect.sizeDelta = new Vector2(300, 300);
            wRect.anchoredPosition = new Vector2(200, -18);
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

            // BOTTOM: STABILIZATION PROGRESS BAR
            GameObject barFrameGo = new GameObject("StabilityBarFrame", typeof(RectTransform), typeof(Image));
            barFrameGo.transform.SetParent(box.transform, false);
            RectTransform bFrameRect = barFrameGo.GetComponent<RectTransform>();
            bFrameRect.anchoredPosition = new Vector2(0, -185);
            bFrameRect.sizeDelta = new Vector2(740, 24);
            Image bFrameImg = barFrameGo.GetComponent<Image>();
            bFrameImg.color = new Color(0.06f, 0.08f, 0.1f, 1f);

            Outline barOutline = barFrameGo.AddComponent<Outline>();
            barOutline.effectColor = new Color(0.25f, 0.32f, 0.4f, 1f);
            barOutline.effectDistance = new Vector2(2, -2);

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

            // MULTI-LINE INSTRUCTION / STATUS TEXT (Ample spacing below progress bar)
            GameObject pctGo = new GameObject("PercentText", typeof(RectTransform), typeof(TextMeshProUGUI));
            pctGo.transform.SetParent(box.transform, false);
            RectTransform pctRect = pctGo.GetComponent<RectTransform>();
            pctRect.anchoredPosition = new Vector2(0, -250);
            pctRect.sizeDelta = new Vector2(650, 65);
            TextMeshProUGUI pctText = pctGo.GetComponent<TextMeshProUGUI>();
            pctText.text = "<color=#FFA500><b>Low Pressure</b></color>\n<size=15><color=#CCCCCC>(Increase Value)</color></size>";
            pctText.fontSize = 17;
            pctText.alignment = TextAlignmentOptions.Center;
            pctText.lineSpacing = 15;

            ValveTaskUI valveTask = root.AddComponent<ValveTaskUI>();
            valveTask.Configure(root, lightImg, headerText, instText, psiText, closeBtn,
                                gImg, needleRect, barFillImg, bFrameRect, pctText, wRect);

            root.SetActive(false);
            return valveTask;
        }
    }
}
