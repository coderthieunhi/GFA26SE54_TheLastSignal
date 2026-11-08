using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using TheLastSignal.Entities.Player;
using TheLastSignal.Entities.Environment;

namespace TheLastSignal.UI.Tasks
{
    /// <summary>
    /// Sci-Fi Brutalist Power Surge Calibration & Timing Alignment Minigame.
    /// Used primarily at Generator, High-Voltage Power Relays, and Mechanical Pulse stations.
    /// Synchronized with Wire Bridging, Valve Balancing, Memory Sequence, and Waveform Tuning aesthetic:
    /// - Heavy metallic brutalist chassis (TerminalBezel)
    /// - High-precision calibration track with graduation scale and moving sweeper needle
    /// - Dynamic randomized Safe Zones and Critical Bullseye targets across 3 tightening stages
    /// - Tactile controls: [SPACEBAR], [E], or click the giant industrial [CALIBRATE] button
    /// - Procedural audio synthesis for rhythmic ticks, hydraulic latch clicks, error buzz, and victory chime
    /// - Instant station 100% completion via StationRepair.CompleteRepair()
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class CalibrationTaskUI : MonoBehaviour
    {
        private static CalibrationTaskUI _instance;
        public static CalibrationTaskUI Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = UnityEngine.Object.FindAnyObjectByType<CalibrationTaskUI>(FindObjectsInactive.Include);
                }
                return _instance;
            }
            private set => _instance = value;
        }

        [Header("UI Panels")]
        [SerializeField] private GameObject rootPanel;
        [SerializeField] private RectTransform terminalBox;
        [SerializeField] private TextMeshProUGUI headerText;
        [SerializeField] private TextMeshProUGUI instructionText;
        [SerializeField] private Button closeButton;

        [Header("Track & Targets")]
        [SerializeField] private RectTransform trackArea;
        [SerializeField] private RectTransform safeZoneRect;
        [SerializeField] private Image safeZoneImage;
        [SerializeField] private RectTransform bullseyeRect;
        [SerializeField] private RectTransform needleRect;
        [SerializeField] private Image needleImage;

        [Header("Controls & Stages")]
        [SerializeField] private Button calibrateButton;
        [SerializeField] private TextMeshProUGUI stageText;
        [SerializeField] private TextMeshProUGUI statusFeedbackText;
        [SerializeField] private TextMeshProUGUI efficiencyText;
        [SerializeField] private Image[] stageLeds = new Image[3];

        [Header("Audio")]
        [SerializeField] private AudioSource audioSource;

        // Track parameters
        private const float TrackWidth = 720f;
        private const float MinTrackX = -360f;
        private const float MaxTrackX = 360f;

        // Stage configuration: [0] = Stage 1, [1] = Stage 2, [2] = Stage 3
        private readonly float[] stageSpeeds = { 1.0f, 1.45f, 1.95f };
        private readonly float[] stageZoneWidths = { 160f, 115f, 80f };

        private int currentStage = 0; // 0 to 2 (representing stages 1 to 3)
        private float needleNormalizedTime = 0f;
        private float currentTargetX = 0f;
        private float currentZoneWidth = 160f;
        private bool isFinished = false;
        private bool isHandlingHit = false;
        private float lastBounceDir = 0f;

        private PlayerController activePlayer;
        private TaskLocation activeTaskLocation;

        // Procedural Audio clips
        private AudioClip tickClip;
        private AudioClip latchNormalClip;
        private AudioClip latchCritClip;
        private AudioClip errorBuzzClip;
        private AudioClip completeChimeClip;

        // Colors
        private readonly Color LedInactive = new Color(0.18f, 0.22f, 0.28f, 1f);
        private readonly Color LedCurrent = new Color(0f, 0.90f, 1f, 1f);
        private readonly Color LedCompleted = new Color(0f, 1f, 0.50f, 1f);
        private readonly Color NeedleDefaultColor = Color.white;
        private readonly Color NeedleHitColor = new Color(0f, 1f, 0.50f, 1f);
        private readonly Color NeedleCritColor = new Color(1f, 0.85f, 0.20f, 1f);
        private readonly Color NeedleMissColor = new Color(1f, 0.20f, 0.20f, 1f);

        public bool IsOpen => rootPanel != null && rootPanel.activeSelf;

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
            EnsureUpgradedVisuals();

            if (closeButton != null)
            {
                closeButton.onClick.RemoveAllListeners();
                closeButton.onClick.AddListener(CloseMinigame);
            }

            if (calibrateButton != null)
            {
                calibrateButton.onClick.RemoveAllListeners();
                calibrateButton.onClick.AddListener(AttemptCalibration);
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

            // Emergency exit with ESC
            if (UnityEngine.InputSystem.Keyboard.current != null &&
                UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                CloseMinigame();
                return;
            }

            if (isFinished) return;

            // Trigger calibration via Keyboard [SPACE] or [E]
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null)
            {
                if (kb.spaceKey.wasPressedThisFrame || kb.eKey.wasPressedThisFrame)
                {
                    AttemptCalibration();
                }
            }

            // Animate moving sweeper needle along track
            UpdateNeedleMovement();
        }

        public void Open(PlayerController player, TaskLocation taskLocation)
        {
            if (player == null)
            {
                player = PlayerController.Local ?? UnityEngine.Object.FindAnyObjectByType<PlayerController>();
            }

            activePlayer = player;
            activeTaskLocation = taskLocation;
            isFinished = false;
            isHandlingHit = false;
            currentStage = 0;
            needleNormalizedTime = Random.Range(0.1f, 0.4f);

            if (activePlayer != null)
            {
                activePlayer.SetControlEnabled(false);
            }

            SetupStage(currentStage);

            if (headerText != null && taskLocation != null)
            {
                string pipelineInfo = string.Empty;
                if (taskLocation.TryGetComponent(out StationRepair repair))
                {
                    int step = repair.CompletedSteps + 1;
                    int target = repair.GetTargetStepsForPlayer(player);
                    pipelineInfo = $" [STAGE {step}/{target}]";
                }
                headerText.text = $"POWER SURGE CALIBRATOR // {taskLocation.StationDisplayName.ToUpper()}{pipelineInfo}";
            }

            if (closeButton != null)
            {
                closeButton.onClick.RemoveAllListeners();
                closeButton.onClick.AddListener(CloseMinigame);
            }

            if (calibrateButton != null)
            {
                calibrateButton.onClick.RemoveAllListeners();
                calibrateButton.onClick.AddListener(AttemptCalibration);
            }

            if (rootPanel != null)
            {
                rootPanel.SetActive(true);
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

        private void SetupStage(int stageIndex)
        {
            currentStage = Mathf.Clamp(stageIndex, 0, 2);
            currentZoneWidth = stageZoneWidths[currentStage];

            // Pick a new distinct target zone position within bounds
            float maxSafeOffset = (TrackWidth * 0.5f) - (currentZoneWidth * 0.5f) - 20f;
            currentTargetX = Random.Range(-maxSafeOffset, maxSafeOffset);

            // Avoid placing too close to previous spot if possible
            if (Mathf.Abs(currentTargetX) < 40f)
            {
                currentTargetX = (Random.value > 0.5f) ? 140f : -140f;
            }

            if (safeZoneRect != null)
            {
                safeZoneRect.sizeDelta = new Vector2(currentZoneWidth, 80f);
                safeZoneRect.anchoredPosition = new Vector2(currentTargetX, 0f);
            }

            if (bullseyeRect != null)
            {
                bullseyeRect.anchoredPosition = new Vector2(currentTargetX, 0f);
            }

            if (stageText != null)
            {
                stageText.text = $"CYCLE: <b>{currentStage + 1} / 3</b>";
            }

            if (statusFeedbackText != null)
            {
                statusFeedbackText.text = (currentStage == 0)
                    ? "PRESS <color=#00E5FF>[SPACE]</color> WHEN NEEDLE IS IN THE SAFE GREEN ZONE"
                    : $"STAGE {currentStage + 1} ENGAGED — FREQUENCY ACCELERATED!";
            }

            if (efficiencyText != null)
            {
                efficiencyText.text = $"GRID SYNC: {Mathf.RoundToInt((currentStage / 3f) * 100f)}%";
            }

            UpdateStageLeds();
        }

        private void UpdateNeedleMovement()
        {
            if (isHandlingHit || isFinished) return;

            float speed = stageSpeeds[currentStage];
            needleNormalizedTime += Time.deltaTime * speed;

            // PingPong between 0.0 and 1.0
            float pingPong = Mathf.PingPong(needleNormalizedTime, 1.0f);
            float currentNeedleX = Mathf.Lerp(MinTrackX, MaxTrackX, pingPong);

            if (needleRect != null)
            {
                needleRect.anchoredPosition = new Vector2(currentNeedleX, 0f);
            }

            // Play subtle tick on bounce
            float dir = (pingPong > 0.98f) ? 1f : (pingPong < 0.02f) ? -1f : 0f;
            if (dir != 0f && dir != lastBounceDir)
            {
                lastBounceDir = dir;
                PlayTickSound();
            }
            else if (dir == 0f)
            {
                lastBounceDir = 0f;
            }
        }

        private void AttemptCalibration()
        {
            if (isHandlingHit || isFinished || !IsOpen) return;

            if (needleRect == null) return;

            float needleX = needleRect.anchoredPosition.x;
            float distance = Mathf.Abs(needleX - currentTargetX);
            float halfZone = currentZoneWidth * 0.5f;

            if (distance <= 12f)
            {
                // CRITICAL BULLSEYE HIT!
                StartCoroutine(HandleHitRoutine(true));
            }
            else if (distance <= halfZone)
            {
                // NORMAL SAFE ZONE HIT!
                StartCoroutine(HandleHitRoutine(false));
            }
            else
            {
                // MISSED! OVERHEAT WARNING!
                StartCoroutine(HandleMissRoutine());
            }
        }

        private IEnumerator HandleHitRoutine(bool isCritical)
        {
            isHandlingHit = true;

            // Flash needle
            if (needleImage != null)
            {
                needleImage.color = isCritical ? NeedleCritColor : NeedleHitColor;
            }

            if (isCritical)
            {
                PlaySfx(latchCritClip);
                if (statusFeedbackText != null)
                {
                    statusFeedbackText.text = "<color=#FFD700><b>★ CRITICAL BULLSEYE! PERFECT PULSE LOCK ★</b></color>";
                }
            }
            else
            {
                PlaySfx(latchNormalClip);
                if (statusFeedbackText != null)
                {
                    statusFeedbackText.text = "<color=#00FF7F><b>>>> PULSE SYNCHRONIZED — ADVANCING CYCLE <<<</b></color>";
                }
            }

            yield return new WaitForSeconds(0.35f);

            if (needleImage != null)
            {
                needleImage.color = NeedleDefaultColor;
            }

            currentStage++;

            if (currentStage >= 3)
            {
                StartCoroutine(CompleteRoutine());
            }
            else
            {
                SetupStage(currentStage);
                isHandlingHit = false;
            }
        }

        private IEnumerator HandleMissRoutine()
        {
            isHandlingHit = true;

            // Flash needle red & play buzz
            if (needleImage != null)
            {
                needleImage.color = NeedleMissColor;
            }

            PlaySfx(errorBuzzClip);

            if (statusFeedbackText != null)
            {
                statusFeedbackText.text = "<color=#FF3B30><b>[!] SURGE MISALIGNMENT — RE-CALIBRATING... [!]</b></color>";
            }

            // Punch shake the terminal panel slightly
            if (terminalBox != null)
            {
                Vector2 origPos = terminalBox.anchoredPosition;
                for (int i = 0; i < 6; i++)
                {
                    terminalBox.anchoredPosition = origPos + new Vector2(Random.Range(-7f, 7f), Random.Range(-4f, 4f));
                    yield return new WaitForSeconds(0.03f);
                }
                terminalBox.anchoredPosition = origPos;
            }
            else
            {
                yield return new WaitForSeconds(0.25f);
            }

            if (needleImage != null)
            {
                needleImage.color = NeedleDefaultColor;
            }

            // If at Stage 3, drop back to Stage 2 for high stakes tension, else reset position
            if (currentStage > 1)
            {
                currentStage = 1;
            }

            SetupStage(currentStage);
            isHandlingHit = false;
        }

        private IEnumerator CompleteRoutine()
        {
            isFinished = true;

            if (stageText != null)
            {
                stageText.text = "CYCLE: <color=#00FF7F><b>3 / 3 [COMPLETE]</b></color>";
            }

            if (efficiencyText != null)
            {
                efficiencyText.text = "GRID SYNC: <color=#00FF7F><b>100% OPTIMAL</b></color>";
            }

            if (statusFeedbackText != null)
            {
                statusFeedbackText.text = "<color=#00FF7F><b>>>> POWER SURGE CALIBRATED — SYSTEM FULLY STABILIZED <<<</b></color>";
            }

            UpdateStageLeds();

            PlaySfx(completeChimeClip);

            yield return new WaitForSeconds(0.85f);

            // Notify Station
            if (activeTaskLocation != null)
            {
                activeTaskLocation.OnSubTaskCompleted(activePlayer, this);
            }
            else
            {
                CloseMinigame();
            }
        }

        private void UpdateStageLeds()
        {
            if (stageLeds == null) return;

            for (int i = 0; i < stageLeds.Length; i++)
            {
                if (stageLeds[i] == null) continue;

                if (i < currentStage)
                {
                    stageLeds[i].color = LedCompleted;
                }
                else if (i == currentStage && !isFinished)
                {
                    stageLeds[i].color = LedCurrent;
                }
                else
                {
                    stageLeds[i].color = LedInactive;
                }
            }

            if (isFinished)
            {
                foreach (var led in stageLeds)
                {
                    if (led != null) led.color = LedCompleted;
                }
            }
        }

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
            }
        }

        private void GenerateProceduralAudio()
        {
            if (tickClip == null)
            {
                tickClip = CreateTickTone();
            }

            if (latchNormalClip == null)
            {
                latchNormalClip = CreateLatchTone(false);
            }

            if (latchCritClip == null)
            {
                latchCritClip = CreateLatchTone(true);
            }

            if (errorBuzzClip == null)
            {
                errorBuzzClip = CreateBuzzTone();
            }

            if (completeChimeClip == null)
            {
                completeChimeClip = CreateChimeTone();
            }
        }

        private void PlayTickSound()
        {
            if (audioSource != null && tickClip != null)
            {
                audioSource.PlayOneShot(tickClip, 0.22f);
            }
        }

        private void PlaySfx(AudioClip clip)
        {
            if (audioSource != null && clip != null)
            {
                audioSource.PlayOneShot(clip, 0.75f);
            }
        }

        private static AudioClip CreateTickTone()
        {
            int sampleRate = 44100;
            float duration = 0.03f;
            int samples = (int)(sampleRate * duration);
            float[] data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / sampleRate;
                float env = 1f - (t / duration);
                data[i] = Mathf.Sin(2f * Mathf.PI * 900f * t) * 0.25f * env;
            }
            AudioClip clip = AudioClip.Create("CalibTick", samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip CreateLatchTone(bool isCritical)
        {
            int sampleRate = 44100;
            float duration = 0.18f;
            int samples = (int)(sampleRate * duration);
            float[] data = new float[samples];
            float freq1 = isCritical ? 880f : 587.33f;
            float freq2 = isCritical ? 1760f : 1174.66f;

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / sampleRate;
                float env = 1f - Mathf.Pow(t / duration, 0.6f);
                float wave = Mathf.Sin(2f * Mathf.PI * freq1 * t) * 0.45f +
                             Mathf.Sin(2f * Mathf.PI * freq2 * t) * 0.35f;
                data[i] = wave * env;
            }
            AudioClip clip = AudioClip.Create(isCritical ? "CalibLatchCrit" : "CalibLatchNormal", samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip CreateBuzzTone()
        {
            int sampleRate = 44100;
            float duration = 0.30f;
            int samples = (int)(sampleRate * duration);
            float[] data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Clamp01((duration - t) / duration);
                float wave = Mathf.Sign(Mathf.Sin(2f * Mathf.PI * 115f * t)); // Low buzz
                data[i] = wave * 0.45f * env;
            }
            AudioClip clip = AudioClip.Create("CalibBuzz", samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip CreateChimeTone()
        {
            int sampleRate = 44100;
            float duration = 0.65f;
            int samples = (int)(sampleRate * duration);
            float[] data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Clamp01((duration - t) / duration);
                data[i] = (Mathf.Sin(2f * Mathf.PI * 523.25f * t) * 0.30f +
                           Mathf.Sin(2f * Mathf.PI * 659.25f * t) * 0.30f +
                           Mathf.Sin(2f * Mathf.PI * 783.99f * t) * 0.30f +
                           Mathf.Sin(2f * Mathf.PI * 1046.50f * t) * 0.25f) * env;
            }
            AudioClip clip = AudioClip.Create("CalibCompleteChime", samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        #endregion

        #region Hierarchy Builder & Upgrade

        public void Configure(GameObject root, RectTransform box, TextMeshProUGUI header, TextMeshProUGUI inst,
                              Button close, RectTransform track, RectTransform safeZone, Image safeImg,
                              RectTransform bullseye, RectTransform needle, Image needleImg,
                              Button calibBtn, TextMeshProUGUI stageTxt, TextMeshProUGUI statTxt,
                              TextMeshProUGUI effTxt, Image[] leds)
        {
            rootPanel = root;
            terminalBox = box;
            headerText = header;
            instructionText = inst;
            closeButton = close;
            trackArea = track;
            safeZoneRect = safeZone;
            safeZoneImage = safeImg;
            bullseyeRect = bullseye;
            needleRect = needle;
            needleImage = needleImg;
            calibrateButton = calibBtn;
            stageText = stageTxt;
            statusFeedbackText = statTxt;
            efficiencyText = effTxt;
            if (leds != null) stageLeds = leds;

            if (closeButton != null)
            {
                closeButton.onClick.RemoveAllListeners();
                closeButton.onClick.AddListener(CloseMinigame);
            }

            if (calibrateButton != null)
            {
                calibrateButton.onClick.RemoveAllListeners();
                calibrateButton.onClick.AddListener(AttemptCalibration);
            }

            EnsureAudioSource();
            GenerateProceduralAudio();
        }

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

            Sprite bezelSprite = Resources.Load<Sprite>("UI/Tasks/TerminalBezel");
            if (terminalBox != null)
            {
                if (terminalBox.TryGetComponent(out Image bImg) && bezelSprite != null)
                {
                    bImg.sprite = bezelSprite;
                    bImg.color = Color.white;
                }

                // Solid dark background chassis backing so world never shows through bezel
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
            }
        }

        public static CalibrationTaskUI GetOrCreateInstance()
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
                Debug.LogWarning("[CalibrationTaskUI] No Canvas found in scene.");
                return null;
            }

            _instance = CreateCalibrationUIHierarchy(canvas);
            return _instance;
        }

        public static CalibrationTaskUI CreateCalibrationUIHierarchy(Canvas canvas)
        {
            Sprite bezelSprite = Resources.Load<Sprite>("UI/Tasks/TerminalBezel");

            // Fullscreen dark overlay vignette
            GameObject root = new GameObject("CalibrationMinigamePanel", typeof(RectTransform), typeof(AudioSource));
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

            // Center Terminal Chassis Box
            GameObject box = new GameObject("TerminalBox", typeof(RectTransform), typeof(Image));
            box.transform.SetParent(root.transform, false);
            RectTransform boxRect = box.GetComponent<RectTransform>();
            boxRect.sizeDelta = new Vector2(960, 640);
            boxRect.anchoredPosition = Vector2.zero;

            Image boxImg = box.GetComponent<Image>();
            if (bezelSprite != null)
            {
                boxImg.sprite = bezelSprite;
                boxImg.color = Color.white;
            }
            else
            {
                boxImg.color = new Color(0.11f, 0.13f, 0.17f, 1f);
                Outline outline = box.AddComponent<Outline>();
                outline.effectColor = new Color(0.2f, 0.35f, 0.5f, 1f);
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
            TextMeshProUGUI headerTxt = headerGo.GetComponent<TextMeshProUGUI>();
            headerTxt.text = "POWER SURGE CALIBRATOR // PULSE ALIGNMENT";
            headerTxt.fontSize = 20;
            headerTxt.alignment = TextAlignmentOptions.Center;
            headerTxt.fontStyle = FontStyles.Bold;
            headerTxt.color = new Color(0.92f, 0.96f, 1f, 1f);

            // Instruction Subtitle
            GameObject instGo = new GameObject("InstructionText", typeof(RectTransform), typeof(TextMeshProUGUI));
            instGo.transform.SetParent(box.transform, false);
            RectTransform instRect = instGo.GetComponent<RectTransform>();
            instRect.anchorMin = new Vector2(0f, 1f);
            instRect.anchorMax = new Vector2(1f, 1f);
            instRect.pivot = new Vector2(0.5f, 1f);
            instRect.anchoredPosition = new Vector2(0, -68);
            instRect.sizeDelta = new Vector2(-120, 26);
            TextMeshProUGUI instTxt = instGo.GetComponent<TextMeshProUGUI>();
            instTxt.text = "Press [SPACE] or Click [LOCK] when needle is in the Green Safe Zone";
            instTxt.fontSize = 15;
            instTxt.alignment = TextAlignmentOptions.Center;
            instTxt.color = new Color(0.72f, 0.82f, 0.92f, 1f);

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
            TextMeshProUGUI ctTxt = closeTextGo.GetComponent<TextMeshProUGUI>();
            ctTxt.text = "X";
            ctTxt.fontSize = 18;
            ctTxt.fontStyle = FontStyles.Bold;
            ctTxt.alignment = TextAlignmentOptions.Center;
            ctTxt.color = Color.white;
            ctTxt.raycastTarget = false;

            // ================= 1. CALIBRATION CHAMBER (TOP / CENTER) =================
            GameObject chamberBox = new GameObject("ChamberHousing", typeof(RectTransform), typeof(Image));
            chamberBox.transform.SetParent(box.transform, false);
            RectTransform chamberRect = chamberBox.GetComponent<RectTransform>();
            chamberRect.sizeDelta = new Vector2(860, 270);
            chamberRect.anchoredPosition = new Vector2(0, 68);
            Image chamberImg = chamberBox.GetComponent<Image>();
            chamberImg.color = new Color(0.02f, 0.05f, 0.08f, 1f);

            Outline chOutline = chamberBox.AddComponent<Outline>();
            chOutline.effectColor = new Color(0.12f, 0.30f, 0.45f, 1f);
            chOutline.effectDistance = new Vector2(3, -3);

            // Chamber Header Label
            GameObject chHeadGo = new GameObject("ChamberHeader", typeof(RectTransform), typeof(TextMeshProUGUI));
            chHeadGo.transform.SetParent(chamberBox.transform, false);
            RectTransform chHeadRect = chHeadGo.GetComponent<RectTransform>();
            chHeadRect.anchoredPosition = new Vector2(0, 114);
            chHeadRect.sizeDelta = new Vector2(500, 22);
            TextMeshProUGUI chHeadTxt = chHeadGo.GetComponent<TextMeshProUGUI>();
            chHeadTxt.text = "CALIBRATION WAVEGUIDE // PULSE RESONANCE TRACK";
            chHeadTxt.fontSize = 12;
            chHeadTxt.fontStyle = FontStyles.Bold;
            chHeadTxt.alignment = TextAlignmentOptions.Center;
            chHeadTxt.color = new Color(0.35f, 0.70f, 0.95f, 1f);

            // Track Chamber Channel
            GameObject trackGo = new GameObject("TrackArea", typeof(RectTransform), typeof(Image));
            trackGo.transform.SetParent(chamberBox.transform, false);
            RectTransform trackRect = trackGo.GetComponent<RectTransform>();
            trackRect.sizeDelta = new Vector2(TrackWidth, 90f);
            trackRect.anchoredPosition = new Vector2(0, -8);
            Image trackImg = trackGo.GetComponent<Image>();
            trackImg.color = new Color(0.05f, 0.08f, 0.12f, 1f);

            Outline to = trackGo.AddComponent<Outline>();
            to.effectColor = new Color(0.18f, 0.28f, 0.38f, 1f);
            to.effectDistance = new Vector2(2, -2);

            // Metallic Guide Rails (Top and Bottom)
            CreateRail(trackGo.transform, "UpperRail", new Vector2(0, 44), new Vector2(TrackWidth, 3));
            CreateRail(trackGo.transform, "LowerRail", new Vector2(0, -44), new Vector2(TrackWidth, 3));

            // Graduation Scale Ticks
            CreateGraduationScale(trackGo.transform, TrackWidth);

            // Safe Zone Target Box
            GameObject safeGo = new GameObject("SafeZone", typeof(RectTransform), typeof(Image));
            safeGo.transform.SetParent(trackGo.transform, false);
            RectTransform safeRect = safeGo.GetComponent<RectTransform>();
            safeRect.sizeDelta = new Vector2(160f, 84f);
            safeRect.anchoredPosition = Vector2.zero;
            Image safeImg = safeGo.GetComponent<Image>();
            safeImg.color = new Color(0f, 1f, 0.50f, 0.18f); // Translucent green fill

            Outline sOutline = safeGo.AddComponent<Outline>();
            sOutline.effectColor = new Color(0f, 1f, 0.50f, 0.85f);
            sOutline.effectDistance = new Vector2(2, -2);

            // Safe Zone Label Tag
            GameObject safeLbl = new GameObject("SafeLabel", typeof(RectTransform), typeof(TextMeshProUGUI));
            safeLbl.transform.SetParent(safeGo.transform, false);
            RectTransform slr = safeLbl.GetComponent<RectTransform>();
            slr.anchoredPosition = new Vector2(0, 32);
            slr.sizeDelta = new Vector2(150, 16);
            TextMeshProUGUI slt = safeLbl.GetComponent<TextMeshProUGUI>();
            slt.text = "[SAFE ZONE]";
            slt.fontSize = 10;
            slt.fontStyle = FontStyles.Bold;
            slt.alignment = TextAlignmentOptions.Center;
            slt.color = new Color(0f, 1f, 0.5f, 0.75f);

            // Critical Bullseye (Center Gold Strip)
            GameObject bullGo = new GameObject("Bullseye", typeof(RectTransform), typeof(Image));
            bullGo.transform.SetParent(trackGo.transform, false);
            RectTransform bullRect = bullGo.GetComponent<RectTransform>();
            bullRect.sizeDelta = new Vector2(14f, 84f);
            bullRect.anchoredPosition = Vector2.zero;
            Image bullImg = bullGo.GetComponent<Image>();
            bullImg.color = new Color(1f, 0.85f, 0.20f, 0.90f); // Bright Gold Bullseye

            Outline bo = bullGo.AddComponent<Outline>();
            bo.effectColor = new Color(1f, 0.95f, 0.40f, 1f);
            bo.effectDistance = new Vector2(1, -1);

            // Moving Sweeper Needle
            GameObject needleGo = new GameObject("SweeperNeedle", typeof(RectTransform), typeof(Image));
            needleGo.transform.SetParent(trackGo.transform, false);
            RectTransform nRect = needleGo.GetComponent<RectTransform>();
            nRect.sizeDelta = new Vector2(8f, 96f);
            nRect.anchoredPosition = new Vector2(MinTrackX, 0f);
            Image needleImg = needleGo.GetComponent<Image>();
            needleImg.color = Color.white;

            Outline no = needleGo.AddComponent<Outline>();
            no.effectColor = new Color(0f, 0.90f, 1f, 1f); // Neon Cyan glow outline
            no.effectDistance = new Vector2(2, -2);

            // Needle Head Arrow Caps (Top & Bottom)
            CreateNeedleCap(needleGo.transform, "TopCap", new Vector2(0, 48), true);
            CreateNeedleCap(needleGo.transform, "BottomCap", new Vector2(0, -48), false);

            // ================= 2. CONTROLS & STATUS BAY (BOTTOM) =================
            GameObject bayBox = new GameObject("ControlsBay", typeof(RectTransform), typeof(Image));
            bayBox.transform.SetParent(box.transform, false);
            RectTransform bayRect = bayBox.GetComponent<RectTransform>();
            bayRect.sizeDelta = new Vector2(860, 185);
            bayRect.anchoredPosition = new Vector2(0, -182);
            Image bayImg = bayBox.GetComponent<Image>();
            bayImg.color = new Color(0.04f, 0.07f, 0.10f, 1f);

            Outline bayOutline = bayBox.AddComponent<Outline>();
            bayOutline.effectColor = new Color(0.18f, 0.25f, 0.35f, 1f);
            bayOutline.effectDistance = new Vector2(3, -3);

            // --- POD 1: CYCLE PROGRESS (LEFT) ---
            GameObject progPod = CreatePod(bayBox.transform, "ProgressPod", new Vector2(-280, 0), new Vector2(260, 165));
            CreatePodTitle(progPod.transform, "CALIBRATION PROGRESS", new Color(0f, 0.90f, 1f, 1f));

            // 3 Progress LEDs
            Image[] leds = new Image[3];
            float ledSpacing = 50f;
            for (int i = 0; i < 3; i++)
            {
                GameObject ledGo = new GameObject($"StageLed_{i}", typeof(RectTransform), typeof(Image));
                ledGo.transform.SetParent(progPod.transform, false);
                RectTransform lr = ledGo.GetComponent<RectTransform>();
                lr.anchoredPosition = new Vector2(-ledSpacing + i * ledSpacing, 32);
                lr.sizeDelta = new Vector2(20, 20);
                Image li = ledGo.GetComponent<Image>();
                li.color = new Color(0.18f, 0.22f, 0.28f, 1f);
                Outline lo = ledGo.AddComponent<Outline>();
                lo.effectColor = new Color(0.28f, 0.35f, 0.45f, 1f);
                lo.effectDistance = new Vector2(1, -1);
                leds[i] = li;

                GameObject ltxtGo = new GameObject("LedNum", typeof(RectTransform), typeof(TextMeshProUGUI));
                ltxtGo.transform.SetParent(ledGo.transform, false);
                RectTransform ltr = ltxtGo.GetComponent<RectTransform>();
                ltr.anchorMin = Vector2.zero;
                ltr.anchorMax = Vector2.one;
                ltr.offsetMin = Vector2.zero;
                ltr.offsetMax = Vector2.zero;
                TextMeshProUGUI ltt = ltxtGo.GetComponent<TextMeshProUGUI>();
                ltt.text = $"{i + 1}";
                ltt.fontSize = 11;
                ltt.fontStyle = FontStyles.Bold;
                ltt.alignment = TextAlignmentOptions.Center;
                ltt.color = Color.white;
            }

            // Cycle Text
            GameObject stgGo = new GameObject("CycleText", typeof(RectTransform), typeof(TextMeshProUGUI));
            stgGo.transform.SetParent(progPod.transform, false);
            RectTransform stgr = stgGo.GetComponent<RectTransform>();
            stgr.anchoredPosition = new Vector2(0, -10);
            stgr.sizeDelta = new Vector2(230, 24);
            TextMeshProUGUI stgt = stgGo.GetComponent<TextMeshProUGUI>();
            stgt.text = "CYCLE: <b>1 / 3</b>";
            stgt.fontSize = 15;
            stgt.alignment = TextAlignmentOptions.Center;
            stgt.color = new Color(0.85f, 0.95f, 1f, 1f);

            // Efficiency Text
            GameObject effGo = new GameObject("EfficiencyText", typeof(RectTransform), typeof(TextMeshProUGUI));
            effGo.transform.SetParent(progPod.transform, false);
            RectTransform effr = effGo.GetComponent<RectTransform>();
            effr.anchoredPosition = new Vector2(0, -42);
            effr.sizeDelta = new Vector2(230, 20);
            TextMeshProUGUI efft = effGo.GetComponent<TextMeshProUGUI>();
            efft.text = "GRID SYNC: 0%";
            efft.fontSize = 12;
            efft.fontStyle = FontStyles.Bold;
            efft.alignment = TextAlignmentOptions.Center;
            efft.color = new Color(0.55f, 0.70f, 0.85f, 1f);

            // --- POD 2: GIANT TACTILE BUTTON (CENTER) ---
            GameObject btnPod = CreatePod(bayBox.transform, "ActionPod", new Vector2(0, 0), new Vector2(270, 165));
            CreatePodTitle(btnPod.transform, "TRIGGER PULSE LATCH", new Color(1f, 0.75f, 0.20f, 1f));

            // Giant Calibrate Button
            GameObject calibGo = new GameObject("CalibrateButton", typeof(RectTransform), typeof(Image), typeof(Button));
            calibGo.transform.SetParent(btnPod.transform, false);
            RectTransform cbRect = calibGo.GetComponent<RectTransform>();
            cbRect.anchoredPosition = new Vector2(0, 6);
            cbRect.sizeDelta = new Vector2(230, 52);
            Image cbImg = calibGo.GetComponent<Image>();
            cbImg.color = new Color(0.10f, 0.22f, 0.32f, 1f);

            Outline cbo = calibGo.AddComponent<Outline>();
            cbo.effectColor = new Color(0f, 0.90f, 1f, 0.8f);
            cbo.effectDistance = new Vector2(2, -2);

            Button calibBtn = calibGo.GetComponent<Button>();

            // Button Label
            GameObject cbTextGo = new GameObject("ButtonText", typeof(RectTransform), typeof(TextMeshProUGUI));
            cbTextGo.transform.SetParent(calibGo.transform, false);
            RectTransform cbtr = cbTextGo.GetComponent<RectTransform>();
            cbtr.anchorMin = Vector2.zero;
            cbtr.anchorMax = Vector2.one;
            cbtr.offsetMin = Vector2.zero;
            cbtr.offsetMax = Vector2.zero;
            TextMeshProUGUI cbtt = cbTextGo.GetComponent<TextMeshProUGUI>();
            cbtt.text = "[ LOCK PULSE ]";
            cbtt.fontSize = 17;
            cbtt.fontStyle = FontStyles.Bold;
            cbtt.alignment = TextAlignmentOptions.Center;
            cbtt.color = Color.white;
            cbtt.raycastTarget = false;

            // Shortcut hint label
            GameObject hintGo = new GameObject("ShortcutHint", typeof(RectTransform), typeof(TextMeshProUGUI));
            hintGo.transform.SetParent(btnPod.transform, false);
            RectTransform htr = hintGo.GetComponent<RectTransform>();
            htr.anchoredPosition = new Vector2(0, -42);
            htr.sizeDelta = new Vector2(240, 20);
            TextMeshProUGUI htt = hintGo.GetComponent<TextMeshProUGUI>();
            htt.text = "SHORTCUT: <b>[SPACE]</b> OR <b>[E]</b>";
            htt.fontSize = 12;
            htt.fontStyle = FontStyles.Bold;
            htt.alignment = TextAlignmentOptions.Center;
            htt.color = new Color(0.4f, 0.8f, 1f, 0.75f);

            // --- POD 3: SYSTEM FEEDBACK & LOG (RIGHT) ---
            GameObject statPod = CreatePod(bayBox.transform, "StatusPod", new Vector2(280, 0), new Vector2(260, 165));
            CreatePodTitle(statPod.transform, "CALIBRATION LOG", new Color(0.6f, 0.8f, 0.95f, 1f));

            // Status Display Box
            GameObject sBox = new GameObject("StatusBox", typeof(RectTransform), typeof(Image));
            sBox.transform.SetParent(statPod.transform, false);
            RectTransform sbr = sBox.GetComponent<RectTransform>();
            sbr.anchoredPosition = new Vector2(0, -6);
            sbr.sizeDelta = new Vector2(230, 80);
            Image sbi = sBox.GetComponent<Image>();
            sbi.color = new Color(0.02f, 0.04f, 0.06f, 1f);

            Outline so = sBox.AddComponent<Outline>();
            so.effectColor = new Color(0.15f, 0.25f, 0.35f, 0.4f);
            so.effectDistance = new Vector2(1, -1);

            GameObject statTxtGo = new GameObject("FeedbackText", typeof(RectTransform), typeof(TextMeshProUGUI));
            statTxtGo.transform.SetParent(sBox.transform, false);
            RectTransform str = statTxtGo.GetComponent<RectTransform>();
            str.anchorMin = Vector2.zero;
            str.anchorMax = Vector2.one;
            str.offsetMin = new Vector2(8, 8);
            str.offsetMax = new Vector2(-8, -8);
            TextMeshProUGUI stt = statTxtGo.GetComponent<TextMeshProUGUI>();
            stt.text = "AWAITING TIMED INPUT...";
            stt.fontSize = 11;
            stt.fontStyle = FontStyles.Bold;
            stt.alignment = TextAlignmentOptions.Center;
            stt.color = new Color(0.75f, 0.85f, 0.95f, 1f);

            // Configure component
            CalibrationTaskUI taskUI = root.AddComponent<CalibrationTaskUI>();
            taskUI.Configure(root, boxRect, headerTxt, instTxt, closeBtn,
                             trackRect, safeRect, safeImg, bullRect,
                             nRect, needleImg, calibBtn, stgt, stt, efft, leds);

            return taskUI;
        }

        private static GameObject CreatePod(Transform parent, string name, Vector2 pos, Vector2 size)
        {
            GameObject pod = new GameObject(name, typeof(RectTransform), typeof(Image));
            pod.transform.SetParent(parent, false);
            RectTransform pr = pod.GetComponent<RectTransform>();
            pr.anchoredPosition = pos;
            pr.sizeDelta = size;
            Image pi = pod.GetComponent<Image>();
            pi.color = new Color(0.06f, 0.09f, 0.12f, 1f);

            Outline po = pod.AddComponent<Outline>();
            po.effectColor = new Color(0.15f, 0.22f, 0.30f, 1f);
            po.effectDistance = new Vector2(2, -2);
            return pod;
        }

        private static void CreatePodTitle(Transform parent, string title, Color color)
        {
            GameObject tGo = new GameObject("PodTitle", typeof(RectTransform), typeof(TextMeshProUGUI));
            tGo.transform.SetParent(parent, false);
            RectTransform tr = tGo.GetComponent<RectTransform>();
            tr.anchoredPosition = new Vector2(0, 68);
            tr.sizeDelta = new Vector2(250, 20);
            TextMeshProUGUI tt = tGo.GetComponent<TextMeshProUGUI>();
            tt.text = title;
            tt.fontSize = 12;
            tt.fontStyle = FontStyles.Bold;
            tt.alignment = TextAlignmentOptions.Center;
            tt.color = color;
        }

        private static void CreateRail(Transform parent, string name, Vector2 pos, Vector2 size)
        {
            GameObject rail = new GameObject(name, typeof(RectTransform), typeof(Image));
            rail.transform.SetParent(parent, false);
            RectTransform rr = rail.GetComponent<RectTransform>();
            rr.anchoredPosition = pos;
            rr.sizeDelta = size;
            Image ri = rail.GetComponent<Image>();
            ri.color = new Color(0.20f, 0.35f, 0.48f, 1f);
            ri.raycastTarget = false;
        }

        private static void CreateNeedleCap(Transform parent, string name, Vector2 pos, bool isTop)
        {
            GameObject cap = new GameObject(name, typeof(RectTransform), typeof(Image));
            cap.transform.SetParent(parent, false);
            RectTransform cr = cap.GetComponent<RectTransform>();
            cr.anchoredPosition = pos;
            cr.sizeDelta = new Vector2(16, 8);
            Image ci = cap.GetComponent<Image>();
            ci.color = new Color(0f, 0.90f, 1f, 1f);
            ci.raycastTarget = false;
        }

        private static void CreateGraduationScale(Transform parent, float width)
        {
            float halfW = width * 0.5f;
            int numMajorTicks = 5; // 0%, 25%, 50%, 75%, 100%
            string[] labels = { "0%", "25%", "50%", "75%", "100%" };

            for (int i = 0; i < numMajorTicks; i++)
            {
                float norm = (float)i / (numMajorTicks - 1);
                float x = Mathf.Lerp(-halfW + 10f, halfW - 10f, norm);

                // Major tick line
                GameObject tickGo = new GameObject($"Tick_{labels[i]}", typeof(RectTransform), typeof(Image));
                tickGo.transform.SetParent(parent, false);
                RectTransform tr = tickGo.GetComponent<RectTransform>();
                tr.anchoredPosition = new Vector2(x, 0f);
                tr.sizeDelta = new Vector2(2, 70);
                Image ti = tickGo.GetComponent<Image>();
                ti.color = new Color(0.25f, 0.40f, 0.55f, 0.35f);
                ti.raycastTarget = false;

                // Scale label
                GameObject lblGo = new GameObject($"TickLbl_{labels[i]}", typeof(RectTransform), typeof(TextMeshProUGUI));
                lblGo.transform.SetParent(parent, false);
                RectTransform lr = lblGo.GetComponent<RectTransform>();
                lr.anchoredPosition = new Vector2(x, -34);
                lr.sizeDelta = new Vector2(50, 16);
                TextMeshProUGUI lt = lblGo.GetComponent<TextMeshProUGUI>();
                lt.text = labels[i];
                lt.fontSize = 9;
                lt.alignment = TextAlignmentOptions.Center;
                lt.color = new Color(0.35f, 0.55f, 0.70f, 0.50f);
                lt.raycastTarget = false;
            }
        }

        #endregion
    }
}
