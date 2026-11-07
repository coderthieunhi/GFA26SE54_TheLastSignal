using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using TheLastSignal.Entities.Player;
using TheLastSignal.Entities.Environment;

namespace TheLastSignal.UI.Tasks
{
    /// <summary>
    /// Sci-Fi Brutalist Radio Frequency / Waveform Synchronization Minigame.
    /// Used primarily at Computer / Communications / Transceiver stations.
    /// Synchronized with Reactor Memory, Wire Bridging, and Valve Balancing aesthetic:
    /// - Heavy metallic brutalist chassis (TerminalBezel)
    /// - Dual-trace CRT Oscilloscope with graticule grid lines
    /// - Continuous travelling wave visualization (WaveformGraphic)
    /// - Frequency [A / D] and Amplitude [W / S] tuning knobs/sliders
    /// - Signal Lock stabilization meter with procedural radio static fading
    /// - Instant station 100% completion via StationRepair.CompleteRepair()
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class WaveformTaskUI : MonoBehaviour
    {
        private static WaveformTaskUI _instance;
        public static WaveformTaskUI Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = Object.FindAnyObjectByType<WaveformTaskUI>(FindObjectsInactive.Include);
                }
                return _instance;
            }
            private set => _instance = value;
        }

        [Header("UI Panels")]
        [SerializeField] private GameObject rootPanel;
        [SerializeField] private TextMeshProUGUI headerText;
        [SerializeField] private TextMeshProUGUI instructionText;
        [SerializeField] private Button closeButton;

        [Header("Oscilloscope")]
        [SerializeField] private WaveformGraphic waveformGraphic;

        [Header("Controls: Frequency")]
        [SerializeField] private Slider frequencySlider;
        [SerializeField] private TextMeshProUGUI frequencyReadoutText;
        [SerializeField] private Button freqMinusButton;
        [SerializeField] private Button freqPlusButton;

        [Header("Controls: Amplitude")]
        [SerializeField] private Slider amplitudeSlider;
        [SerializeField] private TextMeshProUGUI amplitudeReadoutText;
        [SerializeField] private Button ampMinusButton;
        [SerializeField] private Button ampPlusButton;

        [Header("Signal Lock Meter")]
        [SerializeField] private Image lockGaugeBar;
        [SerializeField] private TextMeshProUGUI lockPercentText;
        [SerializeField] private TextMeshProUGUI lockStatusText;
        [SerializeField] private Image[] lockIndicatorLeds = new Image[3];

        [Header("Audio")]
        [SerializeField] private AudioSource sfxAudioSource;
        [SerializeField] private AudioSource staticAudioSource;

        // Tuning parameters
        private const float MinFrequency = 0.5f;
        private const float MaxFrequency = 6.0f;
        private const float MinAmplitude = 0.1f;
        private const float MaxAmplitude = 1.0f;
        private const float RequiredHoldSeconds = 0.85f;

        // Current target values
        private float targetFrequency = 2.5f;
        private float targetAmplitude = 0.6f;

        // Current player values
        private float playerFrequency = 1.0f;
        private float playerAmplitude = 0.3f;

        // Lock progress
        private float lockHoldTimer = 0f;
        private float matchFactor = 0f;
        private bool isFinished = false;

        private PlayerController activePlayer;
        private TaskLocation activeTaskLocation;

        // Procedural audio clips
        private AudioClip radioStaticClip;
        private AudioClip lockSuccessClip;
        private AudioClip tickClip;

        // Indicator colors
        private readonly Color LedInactive = new Color(0.18f, 0.22f, 0.28f, 1f);
        private readonly Color LedDetected = new Color(1f, 0.65f, 0.15f, 1f);   // Amber
        private readonly Color LedAligned = new Color(0f, 0.90f, 1f, 1f);        // Cyan
        private readonly Color LedLocked = new Color(0f, 1f, 0.50f, 1f);         // Neon Green

        public bool IsOpen => rootPanel != null && rootPanel.activeSelf;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;

            EnsureAudioSources();
            GenerateProceduralAudio();
            EnsureUpgradedVisuals();

            if (closeButton != null)
            {
                closeButton.onClick.RemoveAllListeners();
                closeButton.onClick.AddListener(CloseMinigame);
            }

            if (frequencySlider != null)
            {
                frequencySlider.onValueChanged.RemoveAllListeners();
                frequencySlider.onValueChanged.AddListener(OnFreqSliderChanged);
            }

            if (amplitudeSlider != null)
            {
                amplitudeSlider.onValueChanged.RemoveAllListeners();
                amplitudeSlider.onValueChanged.AddListener(OnAmpSliderChanged);
            }

            if (freqMinusButton != null)
            {
                freqMinusButton.onClick.RemoveAllListeners();
                freqMinusButton.onClick.AddListener(() => StepFrequency(-0.1f));
            }

            if (freqPlusButton != null)
            {
                freqPlusButton.onClick.RemoveAllListeners();
                freqPlusButton.onClick.AddListener(() => StepFrequency(0.1f));
            }

            if (ampMinusButton != null)
            {
                ampMinusButton.onClick.RemoveAllListeners();
                ampMinusButton.onClick.AddListener(() => StepAmplitude(-0.05f));
            }

            if (ampPlusButton != null)
            {
                ampPlusButton.onClick.RemoveAllListeners();
                ampPlusButton.onClick.AddListener(() => StepAmplitude(0.05f));
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

            // Keyboard controls (A / D for Frequency, W / S for Amplitude)
            HandleKeyboardInput();

            // Calculate signal alignment & stability
            UpdateSignalAnalysis();

            // Modulate radio static noise volume based on match factor
            UpdateAudioAmbience();
        }

        public void Open(PlayerController player, TaskLocation taskLocation)
        {
            activePlayer = player;
            activeTaskLocation = taskLocation;
            isFinished = false;
            lockHoldTimer = 0f;

            if (activePlayer != null)
            {
                activePlayer.SetControlEnabled(false);
            }

            // Pick distinct target carrier frequency & amplitude
            targetFrequency = Random.Range(18, 52) * 0.1f; // 1.8 to 5.2 kHz
            targetAmplitude = Random.Range(35, 85) * 0.01f; // 0.35 to 0.85 V

            // Initialize player sliders safely away from target
            playerFrequency = (targetFrequency > 3.5f) ? 1.2f : 5.4f;
            playerAmplitude = (targetAmplitude > 0.6f) ? 0.25f : 0.85f;

            if (frequencySlider != null)
            {
                frequencySlider.SetValueWithoutNotify(playerFrequency);
            }
            if (amplitudeSlider != null)
            {
                amplitudeSlider.SetValueWithoutNotify(playerAmplitude);
            }

            if (waveformGraphic != null)
            {
                waveformGraphic.TargetFrequency = targetFrequency;
                waveformGraphic.TargetAmplitude = targetAmplitude;
                waveformGraphic.PlayerFrequency = playerFrequency;
                waveformGraphic.PlayerAmplitude = playerAmplitude;
                waveformGraphic.IsLocked = false;
                waveformGraphic.MatchFactor = 0f;
            }

            UpdateReadouts();

            if (headerText != null && taskLocation != null)
            {
                string pipelineInfo = string.Empty;
                if (taskLocation.TryGetComponent(out StationRepair repair))
                {
                    int step = repair.CompletedSteps + 1;
                    int target = repair.GetTargetStepsForPlayer(player);
                    pipelineInfo = $" [STAGE {step}/{target}]";
                }
                headerText.text = $"RADIO FREQUENCY SYNCHRONIZER // {taskLocation.StationDisplayName.ToUpper()}{pipelineInfo}";
            }

            if (rootPanel != null)
            {
                rootPanel.SetActive(true);
            }

            // Start looping radio static sound
            if (staticAudioSource != null && radioStaticClip != null)
            {
                staticAudioSource.clip = radioStaticClip;
                staticAudioSource.loop = true;
                staticAudioSource.volume = 0.30f;
                staticAudioSource.Play();
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

            if (staticAudioSource != null)
            {
                staticAudioSource.Stop();
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

        private void HandleKeyboardInput()
        {
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb == null) return;

            float freqStep = 1.6f * Time.deltaTime;
            float ampStep = 0.5f * Time.deltaTime;

            bool changed = false;

            // Frequency: A / LeftArrow decreases, D / RightArrow increases
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed)
            {
                playerFrequency = Mathf.Clamp(playerFrequency - freqStep, MinFrequency, MaxFrequency);
                changed = true;
            }
            else if (kb.dKey.isPressed || kb.rightArrowKey.isPressed)
            {
                playerFrequency = Mathf.Clamp(playerFrequency + freqStep, MinFrequency, MaxFrequency);
                changed = true;
            }

            // Amplitude: S / DownArrow decreases, W / UpArrow increases
            if (kb.sKey.isPressed || kb.downArrowKey.isPressed)
            {
                playerAmplitude = Mathf.Clamp(playerAmplitude - ampStep, MinAmplitude, MaxAmplitude);
                changed = true;
            }
            else if (kb.wKey.isPressed || kb.upArrowKey.isPressed)
            {
                playerAmplitude = Mathf.Clamp(playerAmplitude + ampStep, MinAmplitude, MaxAmplitude);
                changed = true;
            }

            if (changed)
            {
                if (frequencySlider != null) frequencySlider.SetValueWithoutNotify(playerFrequency);
                if (amplitudeSlider != null) amplitudeSlider.SetValueWithoutNotify(playerAmplitude);
                ApplyValuesToWaveform();
            }
        }

        private void StepFrequency(float delta)
        {
            if (isFinished) return;
            playerFrequency = Mathf.Clamp(playerFrequency + delta, MinFrequency, MaxFrequency);
            if (frequencySlider != null) frequencySlider.SetValueWithoutNotify(playerFrequency);
            PlayTickSound();
            ApplyValuesToWaveform();
        }

        private void StepAmplitude(float delta)
        {
            if (isFinished) return;
            playerAmplitude = Mathf.Clamp(playerAmplitude + delta, MinAmplitude, MaxAmplitude);
            if (amplitudeSlider != null) amplitudeSlider.SetValueWithoutNotify(playerAmplitude);
            PlayTickSound();
            ApplyValuesToWaveform();
        }

        private void OnFreqSliderChanged(float val)
        {
            if (isFinished) return;
            playerFrequency = val;
            ApplyValuesToWaveform();
        }

        private void OnAmpSliderChanged(float val)
        {
            if (isFinished) return;
            playerAmplitude = val;
            ApplyValuesToWaveform();
        }

        private void ApplyValuesToWaveform()
        {
            if (waveformGraphic != null)
            {
                waveformGraphic.PlayerFrequency = playerFrequency;
                waveformGraphic.PlayerAmplitude = playerAmplitude;
            }
            UpdateReadouts();
        }

        private void UpdateReadouts()
        {
            if (frequencyReadoutText != null)
            {
                frequencyReadoutText.text = $"{playerFrequency:F2} kHz";
            }

            if (amplitudeReadoutText != null)
            {
                amplitudeReadoutText.text = $"{playerAmplitude:F2} V";
            }
        }

        private void UpdateSignalAnalysis()
        {
            float fDiff = Mathf.Abs(playerFrequency - targetFrequency);
            float aDiff = Mathf.Abs(playerAmplitude - targetAmplitude);

            // Normalized error [0..1]
            float normFreqError = Mathf.Clamp01(fDiff / (MaxFrequency - MinFrequency));
            float normAmpError = Mathf.Clamp01(aDiff / (MaxAmplitude - MinAmplitude));
            float totalError = (normFreqError * 0.70f) + (normAmpError * 0.30f);

            // Match factor from 0.0 to 1.0
            matchFactor = Mathf.Clamp01(1f - (totalError * 2.2f));

            if (waveformGraphic != null)
            {
                waveformGraphic.MatchFactor = matchFactor;
            }

            // Lock tolerance window: Frequency within ~0.20 kHz, Amplitude within ~0.09 V
            bool inLockZone = (fDiff <= 0.20f) && (aDiff <= 0.09f);

            if (inLockZone)
            {
                lockHoldTimer += Time.deltaTime / RequiredHoldSeconds;
            }
            else
            {
                // Decay stabilization progress when moved out of tune
                lockHoldTimer = Mathf.MoveTowards(lockHoldTimer, 0f, Time.deltaTime * 2.2f);
            }

            lockHoldTimer = Mathf.Clamp01(lockHoldTimer);

            // Update Lock Gauge Bar & Texts
            if (lockGaugeBar != null)
            {
                lockGaugeBar.fillAmount = lockHoldTimer;
                lockGaugeBar.color = inLockZone ? LedLocked : LedAligned;
            }

            int matchPercent = Mathf.RoundToInt(matchFactor * 100f);
            if (lockPercentText != null)
            {
                lockPercentText.text = inLockZone ? $"STABILIZING: {Mathf.RoundToInt(lockHoldTimer * 100f)}%" : $"SIGNAL MATCH: {matchPercent}%";
            }

            if (lockStatusText != null)
            {
                if (inLockZone)
                {
                    lockStatusText.text = "<color=#00FF7F><b>>>> FREQUENCY ALIGNED — HOLD SIGNAL! <<<</b></color>";
                }
                else if (matchFactor > 0.65f)
                {
                    lockStatusText.text = "<color=#00E5FF>APPROACHING CARRIER FREQUENCY...</color>";
                }
                else
                {
                    lockStatusText.text = "<color=#8FA0B2>SEEKING RF CARRIER WAVE...</color>";
                }
            }

            // Update Indicator LEDs
            if (lockIndicatorLeds != null && lockIndicatorLeds.Length >= 3)
            {
                // LED 1: Signal Detected (> 40% match)
                if (lockIndicatorLeds[0] != null)
                    lockIndicatorLeds[0].color = matchFactor > 0.40f ? LedDetected : LedInactive;

                // LED 2: Wave Aligned (In Lock Zone)
                if (lockIndicatorLeds[1] != null)
                    lockIndicatorLeds[1].color = inLockZone ? LedAligned : LedInactive;

                // LED 3: Frequency Locked (Timer > 80%)
                if (lockIndicatorLeds[2] != null)
                    lockIndicatorLeds[2].color = lockHoldTimer > 0.80f ? LedLocked : LedInactive;
            }

            // Trigger Victory Lock
            if (lockHoldTimer >= 1.0f && !isFinished)
            {
                StartCoroutine(CompleteRoutine());
            }
        }

        private void UpdateAudioAmbience()
        {
            if (staticAudioSource == null) return;

            // Radio static fades away as matchFactor reaches 1.0
            float targetStaticVol = Mathf.Lerp(0.32f, 0.02f, matchFactor);
            if (isFinished) targetStaticVol = 0f;

            staticAudioSource.volume = Mathf.MoveTowards(staticAudioSource.volume, targetStaticVol, Time.deltaTime * 2.5f);
        }

        private IEnumerator CompleteRoutine()
        {
            isFinished = true;

            // Snap player wave to target for visual harmony
            playerFrequency = targetFrequency;
            playerAmplitude = targetAmplitude;
            ApplyValuesToWaveform();

            if (waveformGraphic != null)
            {
                waveformGraphic.IsLocked = true;
            }

            if (lockGaugeBar != null)
            {
                lockGaugeBar.fillAmount = 1f;
                lockGaugeBar.color = LedLocked;
            }

            if (lockStatusText != null)
            {
                lockStatusText.text = "<color=#00FF7F><b>>>> SIGNAL LOCKED — FREQUENCY SYNCHRONIZED <<<</b></color>";
            }

            if (lockIndicatorLeds != null)
            {
                foreach (var led in lockIndicatorLeds)
                {
                    if (led != null) led.color = LedLocked;
                }
            }

            // Stop static noise and play clean harmonic chime
            if (staticAudioSource != null)
            {
                staticAudioSource.Stop();
            }

            PlaySuccessSound();

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

        #region Audio Synthesis

        private void EnsureAudioSources()
        {
            AudioSource[] sources = GetComponents<AudioSource>();
            if (sources.Length > 0)
            {
                sfxAudioSource = sources[0];
            }
            else
            {
                sfxAudioSource = gameObject.AddComponent<AudioSource>();
            }

            if (sources.Length > 1)
            {
                staticAudioSource = sources[1];
            }
            else
            {
                staticAudioSource = gameObject.AddComponent<AudioSource>();
            }

            sfxAudioSource.playOnAwake = false;
            staticAudioSource.playOnAwake = false;
        }

        private void GenerateProceduralAudio()
        {
            if (radioStaticClip == null)
            {
                radioStaticClip = CreateRadioNoiseClip();
            }

            if (lockSuccessClip == null)
            {
                lockSuccessClip = CreateChimeClip();
            }

            if (tickClip == null)
            {
                tickClip = CreateTickClip();
            }
        }

        private void PlayTickSound()
        {
            if (sfxAudioSource != null && tickClip != null)
            {
                sfxAudioSource.PlayOneShot(tickClip, 0.4f);
            }
        }

        private void PlaySuccessSound()
        {
            if (sfxAudioSource != null && lockSuccessClip != null)
            {
                sfxAudioSource.PlayOneShot(lockSuccessClip, 0.8f);
            }
        }

        private static AudioClip CreateRadioNoiseClip()
        {
            int sampleRate = 22050;
            float duration = 1.5f;
            int samples = (int)(sampleRate * duration);
            float[] data = new float[samples];
            System.Random rand = new System.Random(1337);
            float lastVal = 0f;

            for (int i = 0; i < samples; i++)
            {
                float white = (float)(rand.NextDouble() * 2.0 - 1.0);
                lastVal = (lastVal * 0.72f) + (white * 0.28f);
                data[i] = lastVal * 0.35f;
            }

            AudioClip clip = AudioClip.Create("RadioNoiseLoop", samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip CreateChimeClip()
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
                           Mathf.Sin(2f * Mathf.PI * 1046.50f * t) * 0.20f) * env;
            }

            AudioClip clip = AudioClip.Create("WaveLockChime", samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip CreateTickClip()
        {
            int sampleRate = 44100;
            float duration = 0.04f;
            int samples = (int)(sampleRate * duration);
            float[] data = new float[samples];

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / sampleRate;
                float env = 1f - (t / duration);
                data[i] = Mathf.Sin(2f * Mathf.PI * 1400f * t) * 0.35f * env;
            }

            AudioClip clip = AudioClip.Create("TunerTick", samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        #endregion

        #region Hierarchy Builder & Upgrade

        public void Configure(GameObject root, TextMeshProUGUI header, TextMeshProUGUI inst, Button close,
                              WaveformGraphic graphic, Slider freqSld, TextMeshProUGUI freqTxt, Button freqMinus, Button freqPlus,
                              Slider ampSld, TextMeshProUGUI ampTxt, Button ampMinus, Button ampPlus,
                              Image gaugeBar, TextMeshProUGUI pctTxt, TextMeshProUGUI statTxt, Image[] leds)
        {
            rootPanel = root;
            headerText = header;
            instructionText = inst;
            closeButton = close;
            waveformGraphic = graphic;
            frequencySlider = freqSld;
            frequencyReadoutText = freqTxt;
            freqMinusButton = freqMinus;
            freqPlusButton = freqPlus;
            amplitudeSlider = ampSld;
            amplitudeReadoutText = ampTxt;
            ampMinusButton = ampMinus;
            ampPlusButton = ampPlus;
            lockGaugeBar = gaugeBar;
            lockPercentText = pctTxt;
            lockStatusText = statTxt;
            if (leds != null) lockIndicatorLeds = leds;

            EnsureAudioSources();
            GenerateProceduralAudio();
        }

        public void EnsureUpgradedVisuals()
        {
            Sprite bezelSprite = Resources.Load<Sprite>("UI/Tasks/TerminalBezel");
            if (rootPanel != null && bezelSprite != null)
            {
                Transform boxT = rootPanel.transform.Find("TerminalBox");
                if (boxT != null && boxT.TryGetComponent(out Image bImg))
                {
                    bImg.sprite = bezelSprite;
                    bImg.color = Color.white;
                }
            }
        }

        public static WaveformTaskUI GetOrCreateInstance()
        {
            if (Instance != null) return Instance;

            Canvas canvas = Object.FindAnyObjectByType<Canvas>();
            if (canvas == null)
            {
                Debug.LogWarning("[WaveformTaskUI] No Canvas found in scene.");
                return null;
            }

            _instance = CreateWaveformUIHierarchy(canvas);
            return _instance;
        }

        public static WaveformTaskUI CreateWaveformUIHierarchy(Canvas canvas)
        {
            Sprite bezelSprite = Resources.Load<Sprite>("UI/Tasks/TerminalBezel");

            // Fullscreen dark overlay vignette
            GameObject root = new GameObject("WaveformMinigamePanel", typeof(RectTransform));
            root.transform.SetParent(canvas.transform, false);
            RectTransform rootRect = root.GetComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;

            Image bgOverlay = root.AddComponent<Image>();
            bgOverlay.color = new Color(0.02f, 0.04f, 0.06f, 0.88f);

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
            headerTxt.text = "RADIO FREQUENCY SYNCHRONIZER // TRANSCEIVER-09";
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
            instTxt.text = "Tune Frequency [A / D] and Amplitude [W / S] to align signal with Carrier Wave";
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

            // ================= 1. OSCILLOSCOPE HOUSING (TOP) =================
            GameObject scopeBox = new GameObject("OscilloscopeHousing", typeof(RectTransform), typeof(Image));
            scopeBox.transform.SetParent(box.transform, false);
            RectTransform scopeRect = scopeBox.GetComponent<RectTransform>();
            scopeRect.sizeDelta = new Vector2(860, 280);
            scopeRect.anchoredPosition = new Vector2(0, 68);
            Image scopeImg = scopeBox.GetComponent<Image>();
            scopeImg.color = new Color(0.02f, 0.05f, 0.07f, 1f); // Deep CRT dark glass

            Outline scopeOutline = scopeBox.AddComponent<Outline>();
            scopeOutline.effectColor = new Color(0.12f, 0.32f, 0.44f, 1f);
            scopeOutline.effectDistance = new Vector2(3, -3);

            // Channel Badges (Top of Scope)
            GameObject ch1Go = new GameObject("Ch1Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            ch1Go.transform.SetParent(scopeBox.transform, false);
            RectTransform ch1Rect = ch1Go.GetComponent<RectTransform>();
            ch1Rect.anchoredPosition = new Vector2(-280, 122);
            ch1Rect.sizeDelta = new Vector2(260, 22);
            TextMeshProUGUI ch1Txt = ch1Go.GetComponent<TextMeshProUGUI>();
            ch1Txt.text = "<color=#FFA026>■ CH-1: TARGET CARRIER</color>";
            ch1Txt.fontSize = 12;
            ch1Txt.fontStyle = FontStyles.Bold;
            ch1Txt.alignment = TextAlignmentOptions.Left;

            GameObject ch2Go = new GameObject("Ch2Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            ch2Go.transform.SetParent(scopeBox.transform, false);
            RectTransform ch2Rect = ch2Go.GetComponent<RectTransform>();
            ch2Rect.anchoredPosition = new Vector2(280, 122);
            ch2Rect.sizeDelta = new Vector2(260, 22);
            TextMeshProUGUI ch2Txt = ch2Go.GetComponent<TextMeshProUGUI>();
            ch2Txt.text = "<color=#00E5FF>■ CH-2: LOCAL TUNER</color>";
            ch2Txt.fontSize = 12;
            ch2Txt.fontStyle = FontStyles.Bold;
            ch2Txt.alignment = TextAlignmentOptions.Right;

            // Scope graticule legend (Bottom)
            GameObject scopeDivGo = new GameObject("ScopeDivLabel", typeof(RectTransform), typeof(TextMeshProUGUI));
            scopeDivGo.transform.SetParent(scopeBox.transform, false);
            RectTransform sdivRect = scopeDivGo.GetComponent<RectTransform>();
            sdivRect.anchoredPosition = new Vector2(0, -122);
            sdivRect.sizeDelta = new Vector2(400, 20);
            TextMeshProUGUI sdivTxt = scopeDivGo.GetComponent<TextMeshProUGUI>();
            sdivTxt.text = "100 mV / DIV  --  1.0 ms / DIV  --  50 OHM LOAD";
            sdivTxt.fontSize = 11;
            sdivTxt.alignment = TextAlignmentOptions.Center;
            sdivTxt.color = new Color(0f, 0.9f, 1f, 0.35f);

            // Graticule Lines
            CreateGraticuleLines(scopeBox.transform, 840, 240);

            // Waveform Graphic
            GameObject waveGo = new GameObject("WaveformGraphic", typeof(RectTransform), typeof(WaveformGraphic));
            waveGo.transform.SetParent(scopeBox.transform, false);
            RectTransform waveRect = waveGo.GetComponent<RectTransform>();
            waveRect.sizeDelta = new Vector2(840, 240);
            waveRect.anchoredPosition = Vector2.zero;
            WaveformGraphic waveGraphic = waveGo.GetComponent<WaveformGraphic>();

            // ================= 2. CONTROLS BAY (BOTTOM) =================
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

            // --- POD 1: FREQUENCY (LEFT) ---
            GameObject freqPod = CreatePodHousing(bayBox.transform, "FrequencyPod", new Vector2(-280, 0), new Vector2(260, 165));
            CreatePodHeader(freqPod.transform, "FREQUENCY TUNING [A / D]", new Color(0f, 0.90f, 1f, 1f));
            TextMeshProUGUI freqReadout = CreateReadoutBox(freqPod.transform, "FreqReadout", new Vector2(0, 32), new Vector2(200, 36), "1.00 kHz", new Color(0f, 0.95f, 1f, 1f));
            Button freqMinus = CreateStepButton(freqPod.transform, "FreqMinus", new Vector2(-95, -25), new Vector2(36, 30), "-", new Color(0.12f, 0.18f, 0.24f, 1f), Color.white);
            Button freqPlus = CreateStepButton(freqPod.transform, "FreqPlus", new Vector2(95, -25), new Vector2(36, 30), "+", new Color(0.12f, 0.18f, 0.24f, 1f), Color.white);
            Slider freqSlider = CreateSliderControl(freqPod.transform, "FreqSlider", new Vector2(0, -25), new Vector2(140, 20), MinFrequency, MaxFrequency, new Color(0f, 0.9f, 1f, 0.85f), Color.white);
            CreateSubHint(freqPod.transform, "RANGE: 0.50 - 6.00 kHz", new Vector2(0, -60));

            // --- POD 2: SIGNAL LOCK STABILITY (CENTER) ---
            GameObject lockPod = CreatePodHousing(bayBox.transform, "SignalLockPod", new Vector2(0, 0), new Vector2(270, 165));
            CreatePodHeader(lockPod.transform, "CARRIER SYNC LOCK", new Color(0.4f, 0.85f, 1f, 1f));

            // 3 Indicator LEDs
            Image[] leds = new Image[3];
            float ledSpacing = 40f;
            for (int i = 0; i < 3; i++)
            {
                GameObject ledGo = new GameObject($"LockLed_{i}", typeof(RectTransform), typeof(Image));
                ledGo.transform.SetParent(lockPod.transform, false);
                RectTransform lr = ledGo.GetComponent<RectTransform>();
                lr.anchoredPosition = new Vector2(-ledSpacing + i * ledSpacing, 46);
                lr.sizeDelta = new Vector2(16, 16);
                Image li = ledGo.GetComponent<Image>();
                li.color = new Color(0.18f, 0.22f, 0.28f, 1f);
                Outline lo = ledGo.AddComponent<Outline>();
                lo.effectColor = new Color(0.28f, 0.35f, 0.45f, 1f);
                lo.effectDistance = new Vector2(1, -1);
                leds[i] = li;
            }

            // Lock Gauge Bar Frame
            GameObject gaugeFrame = new GameObject("LockGaugeFrame", typeof(RectTransform), typeof(Image));
            gaugeFrame.transform.SetParent(lockPod.transform, false);
            RectTransform gfRect = gaugeFrame.GetComponent<RectTransform>();
            gfRect.anchoredPosition = new Vector2(0, 12);
            gfRect.sizeDelta = new Vector2(230, 24);
            Image gfImg = gaugeFrame.GetComponent<Image>();
            gfImg.color = new Color(0.06f, 0.10f, 0.14f, 1f);

            Outline gfo = gaugeFrame.AddComponent<Outline>();
            gfo.effectColor = new Color(0.18f, 0.35f, 0.50f, 1f);
            gfo.effectDistance = new Vector2(2, -2);

            // Lock Fill Bar
            GameObject gaugeFill = new GameObject("LockGaugeFill", typeof(RectTransform), typeof(Image));
            gaugeFill.transform.SetParent(gaugeFrame.transform, false);
            RectTransform gfillRect = gaugeFill.GetComponent<RectTransform>();
            gfillRect.anchorMin = Vector2.zero;
            gfillRect.anchorMax = Vector2.one;
            gfillRect.offsetMin = new Vector2(2, 2);
            gfillRect.offsetMax = new Vector2(-2, -2);
            Image gfillImg = gaugeFill.GetComponent<Image>();
            gfillImg.type = Image.Type.Filled;
            gfillImg.fillMethod = Image.FillMethod.Horizontal;
            gfillImg.fillAmount = 0f;
            gfillImg.color = new Color(0f, 1f, 0.5f, 1f);

            // Percent Text
            GameObject pctGo = new GameObject("LockPercentText", typeof(RectTransform), typeof(TextMeshProUGUI));
            pctGo.transform.SetParent(lockPod.transform, false);
            RectTransform pctRect = pctGo.GetComponent<RectTransform>();
            pctRect.anchoredPosition = new Vector2(0, -18);
            pctRect.sizeDelta = new Vector2(240, 20);
            TextMeshProUGUI pctTxt = pctGo.GetComponent<TextMeshProUGUI>();
            pctTxt.text = "SIGNAL MATCH: 0%";
            pctTxt.fontSize = 13;
            pctTxt.fontStyle = FontStyles.Bold;
            pctTxt.alignment = TextAlignmentOptions.Center;
            pctTxt.color = new Color(0.85f, 0.95f, 1f, 1f);

            // Status Subtext
            GameObject statGo = new GameObject("LockStatusText", typeof(RectTransform), typeof(TextMeshProUGUI));
            statGo.transform.SetParent(lockPod.transform, false);
            RectTransform statRect = statGo.GetComponent<RectTransform>();
            statRect.anchoredPosition = new Vector2(0, -48);
            statRect.sizeDelta = new Vector2(250, 24);
            TextMeshProUGUI statTxt = statGo.GetComponent<TextMeshProUGUI>();
            statTxt.text = "SEEKING RF CARRIER...";
            statTxt.fontSize = 11;
            statTxt.fontStyle = FontStyles.Bold;
            statTxt.alignment = TextAlignmentOptions.Center;
            statTxt.color = new Color(0.65f, 0.75f, 0.85f, 1f);

            // --- POD 3: AMPLITUDE (RIGHT) ---
            GameObject ampPod = CreatePodHousing(bayBox.transform, "AmplitudePod", new Vector2(280, 0), new Vector2(260, 165));
            CreatePodHeader(ampPod.transform, "AMPLITUDE TUNING [W / S]", new Color(1f, 0.65f, 0.15f, 1f));
            TextMeshProUGUI ampReadout = CreateReadoutBox(ampPod.transform, "AmpReadout", new Vector2(0, 32), new Vector2(200, 36), "0.30 V", new Color(1f, 0.70f, 0.20f, 1f));
            Button ampMinus = CreateStepButton(ampPod.transform, "AmpMinus", new Vector2(-95, -25), new Vector2(36, 30), "-", new Color(0.18f, 0.14f, 0.10f, 1f), Color.white);
            Button ampPlus = CreateStepButton(ampPod.transform, "AmpPlus", new Vector2(95, -25), new Vector2(36, 30), "+", new Color(0.18f, 0.14f, 0.10f, 1f), Color.white);
            Slider ampSlider = CreateSliderControl(ampPod.transform, "AmpSlider", new Vector2(0, -25), new Vector2(140, 20), MinAmplitude, MaxAmplitude, new Color(1f, 0.65f, 0.15f, 0.85f), Color.white);
            CreateSubHint(ampPod.transform, "RANGE: 0.10 - 1.00 V", new Vector2(0, -60));

            // Attach & Configure Component
            WaveformTaskUI taskUI = root.AddComponent<WaveformTaskUI>();
            taskUI.Configure(root, headerTxt, instTxt, closeBtn, waveGraphic,
                             freqSlider, freqReadout, freqMinus, freqPlus,
                             ampSlider, ampReadout, ampMinus, ampPlus,
                             gfillImg, pctTxt, statTxt, leds);

            return taskUI;
        }

        private static GameObject CreatePodHousing(Transform parent, string name, Vector2 pos, Vector2 size)
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

        private static void CreatePodHeader(Transform parent, string text, Color color)
        {
            GameObject hGo = new GameObject("PodHeader", typeof(RectTransform), typeof(TextMeshProUGUI));
            hGo.transform.SetParent(parent, false);
            RectTransform hr = hGo.GetComponent<RectTransform>();
            hr.anchoredPosition = new Vector2(0, 68);
            hr.sizeDelta = new Vector2(250, 20);
            TextMeshProUGUI ht = hGo.GetComponent<TextMeshProUGUI>();
            ht.text = text;
            ht.fontSize = 12;
            ht.fontStyle = FontStyles.Bold;
            ht.alignment = TextAlignmentOptions.Center;
            ht.color = color;
        }

        private static TextMeshProUGUI CreateReadoutBox(Transform parent, string name, Vector2 pos, Vector2 size, string initial, Color textColor)
        {
            GameObject rBox = new GameObject(name, typeof(RectTransform), typeof(Image));
            rBox.transform.SetParent(parent, false);
            RectTransform rr = rBox.GetComponent<RectTransform>();
            rr.anchoredPosition = pos;
            rr.sizeDelta = size;
            Image ri = rBox.GetComponent<Image>();
            ri.color = new Color(0.02f, 0.04f, 0.06f, 1f);

            Outline ro = rBox.AddComponent<Outline>();
            ro.effectColor = new Color(textColor.r, textColor.g, textColor.b, 0.3f);
            ro.effectDistance = new Vector2(1, -1);

            GameObject tGo = new GameObject("ReadoutText", typeof(RectTransform), typeof(TextMeshProUGUI));
            tGo.transform.SetParent(rBox.transform, false);
            RectTransform tr = tGo.GetComponent<RectTransform>();
            tr.anchorMin = Vector2.zero;
            tr.anchorMax = Vector2.one;
            tr.offsetMin = Vector2.zero;
            tr.offsetMax = Vector2.zero;

            TextMeshProUGUI tt = tGo.GetComponent<TextMeshProUGUI>();
            tt.text = initial;
            tt.fontSize = 17;
            tt.fontStyle = FontStyles.Bold;
            tt.alignment = TextAlignmentOptions.Center;
            tt.color = textColor;
            return tt;
        }

        private static void CreateSubHint(Transform parent, string hint, Vector2 pos)
        {
            GameObject hGo = new GameObject("SubHint", typeof(RectTransform), typeof(TextMeshProUGUI));
            hGo.transform.SetParent(parent, false);
            RectTransform hr = hGo.GetComponent<RectTransform>();
            hr.anchoredPosition = pos;
            hr.sizeDelta = new Vector2(200, 16);
            TextMeshProUGUI ht = hGo.GetComponent<TextMeshProUGUI>();
            ht.text = hint;
            ht.fontSize = 10;
            ht.alignment = TextAlignmentOptions.Center;
            ht.color = new Color(0.45f, 0.55f, 0.65f, 1f);
        }

        private static Button CreateStepButton(Transform parent, string name, Vector2 pos, Vector2 size, string label, Color bgColor, Color textColor)
        {
            GameObject btnGo = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            btnGo.transform.SetParent(parent, false);
            RectTransform bRect = btnGo.GetComponent<RectTransform>();
            bRect.anchoredPosition = pos;
            bRect.sizeDelta = size;
            Image bImg = btnGo.GetComponent<Image>();
            bImg.color = bgColor;

            Outline outline = btnGo.AddComponent<Outline>();
            outline.effectColor = new Color(0.3f, 0.45f, 0.6f, 0.4f);
            outline.effectDistance = new Vector2(1, -1);

            GameObject textGo = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            textGo.transform.SetParent(btnGo.transform, false);
            RectTransform tRect = textGo.GetComponent<RectTransform>();
            tRect.anchorMin = Vector2.zero;
            tRect.anchorMax = Vector2.one;
            tRect.offsetMin = Vector2.zero;
            tRect.offsetMax = Vector2.zero;

            TextMeshProUGUI tmp = textGo.GetComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = 18;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = textColor;
            tmp.raycastTarget = false;

            return btnGo.GetComponent<Button>();
        }

        private static Slider CreateSliderControl(Transform parent, string name, Vector2 pos, Vector2 size, float min, float max, Color fillColor, Color handleColor)
        {
            GameObject sliderGo = new GameObject(name, typeof(RectTransform), typeof(Slider));
            sliderGo.transform.SetParent(parent, false);
            RectTransform sRect = sliderGo.GetComponent<RectTransform>();
            sRect.anchoredPosition = pos;
            sRect.sizeDelta = size;

            Slider slider = sliderGo.GetComponent<Slider>();
            slider.minValue = min;
            slider.maxValue = max;
            slider.direction = Slider.Direction.LeftToRight;

            // Background track
            GameObject bgGo = new GameObject("Background", typeof(RectTransform), typeof(Image));
            bgGo.transform.SetParent(sliderGo.transform, false);
            RectTransform bgRect = bgGo.GetComponent<RectTransform>();
            bgRect.anchorMin = new Vector2(0f, 0.25f);
            bgRect.anchorMax = new Vector2(1f, 0.75f);
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;
            Image bgImg = bgGo.GetComponent<Image>();
            bgImg.color = new Color(0.08f, 0.12f, 0.16f, 1f);

            // Fill Area
            GameObject fillArea = new GameObject("Fill Area", typeof(RectTransform));
            fillArea.transform.SetParent(sliderGo.transform, false);
            RectTransform faRect = fillArea.GetComponent<RectTransform>();
            faRect.anchorMin = new Vector2(0f, 0.25f);
            faRect.anchorMax = new Vector2(1f, 0.75f);
            faRect.offsetMin = new Vector2(5f, 0f);
            faRect.offsetMax = new Vector2(-5f, 0f);

            GameObject fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fill.transform.SetParent(fillArea.transform, false);
            RectTransform fRect = fill.GetComponent<RectTransform>();
            fRect.sizeDelta = Vector2.zero;
            Image fImg = fill.GetComponent<Image>();
            fImg.color = fillColor;
            slider.fillRect = fRect;

            // Handle Slide Area
            GameObject handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
            handleArea.transform.SetParent(sliderGo.transform, false);
            RectTransform haRect = handleArea.GetComponent<RectTransform>();
            haRect.anchorMin = Vector2.zero;
            haRect.anchorMax = Vector2.one;
            haRect.offsetMin = new Vector2(8f, 0f);
            haRect.offsetMax = new Vector2(-8f, 0f);

            GameObject handle = new GameObject("Handle", typeof(RectTransform), typeof(Image));
            handle.transform.SetParent(handleArea.transform, false);
            RectTransform hRect = handle.GetComponent<RectTransform>();
            hRect.sizeDelta = new Vector2(18f, size.y + 6f);
            Image hImg = handle.GetComponent<Image>();
            hImg.color = handleColor;
            slider.handleRect = hRect;
            slider.targetGraphic = hImg;

            return slider;
        }

        private static void CreateGraticuleLines(Transform scopeParent, float width, float height)
        {
            Color primaryLine = new Color(0f, 0.9f, 1f, 0.20f);
            Color secondaryLine = new Color(0f, 0.9f, 1f, 0.08f);

            // Center Horizontal Line
            CreateLine(scopeParent, "Grid_Center_H", new Vector2(0, 0), new Vector2(width, 2), primaryLine);
            // Center Vertical Line
            CreateLine(scopeParent, "Grid_Center_V", new Vector2(0, 0), new Vector2(2, height), primaryLine);

            // Horizontal Secondary Lines
            float[] hOffsets = new float[] { -80f, -40f, 40f, 80f };
            foreach (float y in hOffsets)
            {
                CreateLine(scopeParent, $"Grid_H_{y}", new Vector2(0, y), new Vector2(width, 1), secondaryLine);
            }

            // Vertical Secondary Lines
            float[] vOffsets = new float[] { -300f, -200f, -100f, 100f, 200f, 300f };
            foreach (float x in vOffsets)
            {
                CreateLine(scopeParent, $"Grid_V_{x}", new Vector2(x, 0), new Vector2(1, height), secondaryLine);
            }
        }

        private static void CreateLine(Transform parent, string name, Vector2 pos, Vector2 size, Color color)
        {
            GameObject lineGo = new GameObject(name, typeof(RectTransform), typeof(Image));
            lineGo.transform.SetParent(parent, false);
            RectTransform lr = lineGo.GetComponent<RectTransform>();
            lr.anchoredPosition = pos;
            lr.sizeDelta = size;
            Image li = lineGo.GetComponent<Image>();
            li.color = color;
            li.raycastTarget = false;
        }

        #endregion
    }
}
