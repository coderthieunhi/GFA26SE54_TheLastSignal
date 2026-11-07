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
    /// Sci-Fi Brutalist Memory Sequence Minigame (Simon Says / Reactor Memory Keypad).
    /// Features:
    /// - 3x3 Monitor Display on the left that flashes a sequence of patterns.
    /// - 3x3 Tactile Keypad on the right where the player replicates the sequence.
    /// - Multi-stage progression (Stages 1 through 5).
    /// - Full mouse click and keyboard (1-9 / NumPad) input support.
    /// - Procedural audio feedback (distinct musical tones for each key, error buzzer, victory chime).
    /// - Server-authoritative ready and full player control lock/release.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class MemoryTaskUI : MonoBehaviour
    {
        private static MemoryTaskUI _instance;
        public static MemoryTaskUI Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = UnityEngine.Object.FindAnyObjectByType<MemoryTaskUI>(FindObjectsInactive.Include);
                }
                return _instance;
            }
            private set => _instance = value;
        }

        [Header("UI Panels")]
        [SerializeField] private GameObject rootPanel;
        [SerializeField] private TextMeshProUGUI headerText;
        [SerializeField] private TextMeshProUGUI instructionText;
        [SerializeField] private TextMeshProUGUI stageText;
        [SerializeField] private Button closeButton;

        [Header("Monitor (Left - 3x3 Display)")]
        [SerializeField] private Image[] monitorTiles = new Image[9];
        [SerializeField] private TextMeshProUGUI monitorStatusText;

        [Header("Keypad (Right - 3x3 Input)")]
        [SerializeField] private Button[] keypadButtons = new Button[9];
        [SerializeField] private Image[] keypadTileImages = new Image[9];
        [SerializeField] private TextMeshProUGUI[] keypadTileTexts = new TextMeshProUGUI[9];

        [Header("Progress")]
        [SerializeField] private Image[] stageLeds = new Image[5];

        [Header("Tuning Parameters")]
        [SerializeField] private int totalStages = 5;
        [SerializeField] private float flashDuration = 0.32f;
        [SerializeField] private float pauseDuration = 0.14f;

        [Header("Audio")]
        [SerializeField] private AudioSource audioSource;

        private PlayerController activePlayer;
        private TaskLocation activeTaskLocation;

        private List<int> sequence = new List<int>();
        private int currentStage = 0;
        private int playerInputIndex = 0;
        private bool isShowingSequence = false;
        private bool isFinished = false;
        private Coroutine activePlayRoutine;

        // Colors
        private readonly Color MonitorTileNormal = new Color(0.08f, 0.11f, 0.15f, 1f);
        private readonly Color MonitorTileFlash = new Color(0f, 0.9f, 1f, 1f);       // Neon Cyan
        private readonly Color KeypadTileNormal = new Color(0.13f, 0.16f, 0.20f, 1f);
        private readonly Color KeypadTileFlash = new Color(0.13f, 0.85f, 0.45f, 1f);   // Neon Green
        private readonly Color KeypadTileError = new Color(0.95f, 0.22f, 0.22f, 1f);   // Neon Red
        private readonly Color LedInactive = new Color(0.18f, 0.22f, 0.28f, 1f);
        private readonly Color LedActive = new Color(0f, 1f, 0.5f, 1f);

        // Procedural Audio Clips
        private AudioClip[] noteClips;
        private AudioClip successClip;
        private AudioClip errorClip;

        public bool IsOpen => rootPanel != null && rootPanel.activeSelf;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;

            EnsureUpgradedVisuals();
            ResetAllTileVisuals();

            if (closeButton != null)
            {
                closeButton.onClick.RemoveAllListeners();
                closeButton.onClick.AddListener(CloseMinigame);
            }

            SetupKeypadListeners();
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

        private void SetupKeypadListeners()
        {
            for (int i = 0; i < keypadButtons.Length; i++)
            {
                int index = i;
                if (keypadButtons[i] != null)
                {
                    keypadButtons[i].onClick.RemoveAllListeners();
                    keypadButtons[i].onClick.AddListener(() => OnKeypadButtonPressed(index));
                }
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

            if (isFinished || isShowingSequence) return;

            // Keyboard input support (Keys 1-9 on number row & NumPad)
            HandleKeyboardInput();
        }

        private void HandleKeyboardInput()
        {
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb == null) return;

            if (kb.digit1Key.wasPressedThisFrame || kb.numpad1Key.wasPressedThisFrame) OnKeypadButtonPressed(0);
            else if (kb.digit2Key.wasPressedThisFrame || kb.numpad2Key.wasPressedThisFrame) OnKeypadButtonPressed(1);
            else if (kb.digit3Key.wasPressedThisFrame || kb.numpad3Key.wasPressedThisFrame) OnKeypadButtonPressed(2);
            else if (kb.digit4Key.wasPressedThisFrame || kb.numpad4Key.wasPressedThisFrame) OnKeypadButtonPressed(3);
            else if (kb.digit5Key.wasPressedThisFrame || kb.numpad5Key.wasPressedThisFrame) OnKeypadButtonPressed(4);
            else if (kb.digit6Key.wasPressedThisFrame || kb.numpad6Key.wasPressedThisFrame) OnKeypadButtonPressed(5);
            else if (kb.digit7Key.wasPressedThisFrame || kb.numpad7Key.wasPressedThisFrame) OnKeypadButtonPressed(6);
            else if (kb.digit8Key.wasPressedThisFrame || kb.numpad8Key.wasPressedThisFrame) OnKeypadButtonPressed(7);
            else if (kb.digit9Key.wasPressedThisFrame || kb.numpad9Key.wasPressedThisFrame) OnKeypadButtonPressed(8);
        }

        public void Open(PlayerController player, TaskLocation station)
        {
            if (player == null)
            {
                player = PlayerController.Local ?? UnityEngine.Object.FindAnyObjectByType<PlayerController>();
            }

            activePlayer = player;
            activeTaskLocation = station;
            isFinished = false;
            currentStage = 0;
            playerInputIndex = 0;

            EnsureUpgradedVisuals();
            ResetAllTileVisuals();

            if (activePlayer != null)
            {
                activePlayer.SetControlEnabled(false);
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
                headerText.text = $"REACTOR MEMORY CORE // {station.StationDisplayName.ToUpper()}{pipelineInfo}";
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

            GenerateNewSequence();
            StartStage(0);
        }

        private void GenerateNewSequence()
        {
            sequence.Clear();
            for (int i = 0; i < totalStages; i++)
            {
                sequence.Add(Random.Range(0, 9));
            }
        }

        private void StartStage(int stage)
        {
            currentStage = stage;
            playerInputIndex = 0;

            ResetAllTileVisuals();
            UpdateStageLeds();

            if (instructionText != null)
            {
                instructionText.text = $"STAGE <color=#00FF7F>{currentStage + 1}</color> / {totalStages} — MEMORIZE PATTERN";
            }

            if (stageText != null)
            {
                stageText.text = $"STAGE: {currentStage + 1} / {totalStages}";
            }

            if (monitorStatusText != null)
            {
                monitorStatusText.text = "<color=#00E5FF>OBSERVING SEQUENCE...</color>";
            }

            if (activePlayRoutine != null)
            {
                StopCoroutine(activePlayRoutine);
            }
            activePlayRoutine = StartCoroutine(PlaySequenceRoutine());
        }

        private IEnumerator PlaySequenceRoutine()
        {
            isShowingSequence = true;
            SetKeypadInteractable(false);
            yield return new WaitForSeconds(0.4f);

            // Play the pattern up to currentStage (e.g. stage 0 plays 1 note, stage 1 plays 2 notes, etc.)
            int notesToPlay = currentStage + 1;
            for (int i = 0; i < notesToPlay; i++)
            {
                int tileIndex = sequence[i];

                // Flash monitor tile and play audio
                yield return FlashMonitorTile(tileIndex, MonitorTileFlash, flashDuration);
                yield return new WaitForSeconds(pauseDuration);
            }

            yield return new WaitForSeconds(0.15f);

            // Player's turn
            isShowingSequence = false;
            SetKeypadInteractable(true);

            if (instructionText != null)
            {
                instructionText.text = "REPLICATE SEQUENCE ON KEYPAD [1 - 9]";
            }

            if (monitorStatusText != null)
            {
                monitorStatusText.text = "<color=#A0FFA0>AWAITING INPUT...</color>";
            }
        }

        private void OnKeypadButtonPressed(int index)
        {
            if (isShowingSequence || isFinished) return;

            // Play the corresponding audio note
            PlayTone(index);

            // Check correctness
            int expected = sequence[playerInputIndex];

            if (index == expected)
            {
                // Correct button pressed
                StartCoroutine(FlashKeypadTile(index, KeypadTileFlash, 0.18f));
                playerInputIndex++;

                int stageRequiredCount = currentStage + 1;
                if (playerInputIndex >= stageRequiredCount)
                {
                    // Stage completed!
                    if (currentStage + 1 >= totalStages)
                    {
                        StartCoroutine(CompleteRoutine());
                    }
                    else
                    {
                        StartCoroutine(StageSuccessRoutine());
                    }
                }
            }
            else
            {
                // Wrong button pressed
                StartCoroutine(WrongInputRoutine(index));
            }
        }

        private IEnumerator StageSuccessRoutine()
        {
            isShowingSequence = true;
            SetKeypadInteractable(false);

            if (monitorStatusText != null)
            {
                monitorStatusText.text = "<color=#00FF7F>SEQUENCE MATCHED!</color>";
            }

            PlaySuccessSound();

            // Quick green ripple on all keypad tiles
            yield return FlashAllKeypadTiles(KeypadTileFlash, 0.25f);
            yield return new WaitForSeconds(0.35f);

            StartStage(currentStage + 1);
        }

        private IEnumerator WrongInputRoutine(int wrongIndex)
        {
            isShowingSequence = true;
            SetKeypadInteractable(false);

            PlayErrorSound();

            if (instructionText != null)
            {
                instructionText.text = "<color=#FF3B30><b>>>> INPUT MISMATCH - REPLAYING SEQUENCE <<<</b></color>";
            }

            if (monitorStatusText != null)
            {
                monitorStatusText.text = "<color=#FF3B30>PATTERN MISMATCH</color>";
            }

            // Flash red on keypad
            yield return FlashAllKeypadTiles(KeypadTileError, 0.35f);
            yield return new WaitForSeconds(0.4f);

            // Re-play current stage sequence
            StartStage(currentStage);
        }

        private IEnumerator CompleteRoutine()
        {
            isFinished = true;
            isShowingSequence = true;
            SetKeypadInteractable(false);

            PlaySuccessSound();

            // Light all stage LEDs
            for (int i = 0; i < stageLeds.Length; i++)
            {
                if (stageLeds[i] != null) stageLeds[i].color = LedActive;
            }

            if (instructionText != null)
            {
                instructionText.text = "<color=#00FF7F><b>>>> MEMORY VERIFIED — ACCESS GRANTED <<<</b></color>";
            }

            if (monitorStatusText != null)
            {
                monitorStatusText.text = "<color=#00FF7F>CORE STABILIZED</color>";
            }

            if (stageText != null)
            {
                stageText.text = "<color=#00FF7F>TASK COMPLETE</color>";
            }

            // Flash all tiles green
            for (int i = 0; i < monitorTiles.Length; i++)
            {
                if (monitorTiles[i] != null) monitorTiles[i].color = KeypadTileFlash;
                if (keypadTileImages[i] != null) keypadTileImages[i].color = KeypadTileFlash;
            }

            yield return new WaitForSeconds(0.85f);

            ResetAllTileVisuals();

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

            if (activePlayRoutine != null)
            {
                StopCoroutine(activePlayRoutine);
                activePlayRoutine = null;
            }

            ResetAllTileVisuals();

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

        private IEnumerator FlashMonitorTile(int index, Color color, float duration)
        {
            if (index < 0 || index >= monitorTiles.Length || monitorTiles[index] == null) yield break;

            Image img = monitorTiles[index];
            img.color = color;
            PlayTone(index);

            yield return new WaitForSeconds(duration);

            img.color = MonitorTileNormal;
        }

        private IEnumerator FlashKeypadTile(int index, Color color, float duration)
        {
            if (index < 0 || index >= keypadTileImages.Length || keypadTileImages[index] == null) yield break;

            Image img = keypadTileImages[index];
            img.color = color;

            yield return new WaitForSeconds(duration);

            img.color = KeypadTileNormal;
        }

        private IEnumerator FlashAllKeypadTiles(Color color, float duration)
        {
            for (int i = 0; i < keypadTileImages.Length; i++)
            {
                if (keypadTileImages[i] != null) keypadTileImages[i].color = color;
            }

            yield return new WaitForSeconds(duration);

            for (int i = 0; i < keypadTileImages.Length; i++)
            {
                if (keypadTileImages[i] != null) keypadTileImages[i].color = KeypadTileNormal;
            }
        }

        private void SetKeypadInteractable(bool interactable)
        {
            for (int i = 0; i < keypadButtons.Length; i++)
            {
                if (keypadButtons[i] != null)
                {
                    keypadButtons[i].interactable = interactable;
                }
            }
        }

        private void UpdateStageLeds()
        {
            for (int i = 0; i < stageLeds.Length; i++)
            {
                if (stageLeds[i] == null) continue;
                stageLeds[i].color = i <= currentStage ? LedActive : LedInactive;
            }
        }

        public void ResetAllTileVisuals()
        {
            if (monitorTiles != null)
            {
                for (int i = 0; i < monitorTiles.Length; i++)
                {
                    if (monitorTiles[i] != null)
                    {
                        monitorTiles[i].color = MonitorTileNormal;
                    }
                }
            }
            if (keypadTileImages != null)
            {
                for (int i = 0; i < keypadTileImages.Length; i++)
                {
                    if (keypadTileImages[i] != null)
                    {
                        keypadTileImages[i].color = KeypadTileNormal;
                    }
                }
            }
        }

        /// <summary>
        /// Automatically upgrades/re-aligns any existing scene hierarchy to the un-cluttered, non-overlapping brutalist layout.
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
            if (boxT == null) return;

            // Monitor Housing
            Transform mHouseT = boxT.Find("MonitorHousing");
            if (mHouseT is RectTransform mHouseRect)
            {
                mHouseRect.sizeDelta = new Vector2(340, 360);
                mHouseRect.anchoredPosition = new Vector2(-200, 18);

                Transform mHeadT = mHouseT.Find("MonitorHeader");
                if (mHeadT is RectTransform mhRect)
                {
                    mhRect.anchoredPosition = new Vector2(0, 155);
                    mhRect.sizeDelta = new Vector2(300, 24);
                }

                float tileSize = 74f;
                float spacing = 12f;
                float startX = -(tileSize + spacing);
                float startY = 70f;

                for (int r = 0; r < 3; r++)
                {
                    for (int c = 0; c < 3; c++)
                    {
                        int index = r * 3 + c;
                        Transform tT = mHouseT.Find($"MonitorTile_{index}");
                        if (tT is RectTransform tRect)
                        {
                            tRect.sizeDelta = new Vector2(tileSize, tileSize);
                            tRect.anchoredPosition = new Vector2(startX + c * (tileSize + spacing), startY - r * (tileSize + spacing));
                        }
                    }
                }

                Transform mStatT = mHouseT.Find("MonitorStatusText");
                if (mStatT is RectTransform msRect)
                {
                    msRect.anchoredPosition = new Vector2(0, -152);
                    msRect.sizeDelta = new Vector2(300, 24);
                }
            }

            // Keypad Housing
            Transform kHouseT = boxT.Find("KeypadHousing");
            if (kHouseT is RectTransform kHouseRect)
            {
                kHouseRect.sizeDelta = new Vector2(340, 360);
                kHouseRect.anchoredPosition = new Vector2(200, 18);

                Transform kHeadT = kHouseT.Find("KeypadHeader");
                if (kHeadT is RectTransform khRect)
                {
                    khRect.anchoredPosition = new Vector2(0, 155);
                    khRect.sizeDelta = new Vector2(300, 24);
                }

                float tileSize = 74f;
                float spacing = 12f;
                float startX = -(tileSize + spacing);
                float startY = 70f;

                for (int r = 0; r < 3; r++)
                {
                    for (int c = 0; c < 3; c++)
                    {
                        int index = r * 3 + c;
                        Transform bT = kHouseT.Find($"KeypadBtn_{index + 1}");
                        if (bT is RectTransform bRect)
                        {
                            bRect.sizeDelta = new Vector2(tileSize, tileSize);
                            bRect.anchoredPosition = new Vector2(startX + c * (tileSize + spacing), startY - r * (tileSize + spacing));
                        }
                    }
                }

                Transform kStatT = kHouseT.Find("KeypadStatusText");
                if (kStatT is RectTransform ksRect)
                {
                    ksRect.anchoredPosition = new Vector2(0, -152);
                    ksRect.sizeDelta = new Vector2(300, 24);
                }
            }

            // Progress Frame
            Transform pfT = boxT.Find("ProgressFrame");
            if (pfT is RectTransform pfRect)
            {
                pfRect.anchoredPosition = new Vector2(0, -205);
                pfRect.sizeDelta = new Vector2(380, 20);

                float ledWidth = 58f;
                float ledSpacing = 14f;
                float ledStartX = -(2 * (ledWidth + ledSpacing));

                for (int i = 0; i < 5; i++)
                {
                    Transform ledT = pfT.Find($"StageLed_{i + 1}");
                    if (ledT is RectTransform lRect)
                    {
                        lRect.sizeDelta = new Vector2(ledWidth, 14);
                        lRect.anchoredPosition = new Vector2(ledStartX + i * (ledWidth + ledSpacing), 0);
                    }
                }
            }

            // Stage Text
            Transform stgT = boxT.Find("StageText");
            if (stgT is RectTransform stgRect)
            {
                stgRect.anchoredPosition = new Vector2(0, -250);
                stgRect.sizeDelta = new Vector2(600, 28);
            }
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

            // 9 musical frequencies (Major scale from C4 to D5)
            float[] freqs = new float[] {
                261.63f, // 1: C4
                293.66f, // 2: D4
                329.63f, // 3: E4
                349.23f, // 4: F4
                392.00f, // 5: G4
                440.00f, // 6: A4
                493.88f, // 7: B4
                523.25f, // 8: C5
                587.33f  // 9: D5
            };

            noteClips = new AudioClip[9];
            for (int i = 0; i < 9; i++)
            {
                noteClips[i] = CreateSineTone(freqs[i], 0.28f, 0.45f);
            }

            successClip = CreateChimeTone();
            errorClip = CreateErrorTone();
        }

        private void PlayTone(int index)
        {
            if (audioSource == null || noteClips == null) return;
            if (index >= 0 && index < noteClips.Length && noteClips[index] != null)
            {
                audioSource.PlayOneShot(noteClips[index]);
            }
        }

        private void PlaySuccessSound()
        {
            if (audioSource != null && successClip != null)
            {
                audioSource.PlayOneShot(successClip);
            }
        }

        private void PlayErrorSound()
        {
            if (audioSource != null && errorClip != null)
            {
                audioSource.PlayOneShot(errorClip);
            }
        }

        private static AudioClip CreateSineTone(float frequency, float duration, float volume = 0.5f)
        {
            int sampleRate = 44100;
            int samples = (int)(sampleRate * duration);
            float[] data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / sampleRate;
                float envelope = 1f;
                if (t < 0.02f) envelope = t / 0.02f;
                else if (t > duration - 0.06f) envelope = (duration - t) / 0.06f;
                data[i] = Mathf.Sin(2f * Mathf.PI * frequency * t) * volume * envelope;
            }
            AudioClip clip = AudioClip.Create($"Tone_{frequency}", samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip CreateChimeTone()
        {
            int sampleRate = 44100;
            float duration = 0.45f;
            int samples = (int)(sampleRate * duration);
            float[] data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Clamp01((duration - t) / duration);
                data[i] = (Mathf.Sin(2f * Mathf.PI * 523.25f * t) * 0.3f +
                           Mathf.Sin(2f * Mathf.PI * 659.25f * t) * 0.3f +
                           Mathf.Sin(2f * Mathf.PI * 783.99f * t) * 0.3f) * env;
            }
            AudioClip clip = AudioClip.Create("Chime", samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip CreateErrorTone()
        {
            int sampleRate = 44100;
            float duration = 0.35f;
            int samples = (int)(sampleRate * duration);
            float[] data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Clamp01((duration - t) / duration);
                // Low harsh square/saw wave tone
                float wave = Mathf.Sign(Mathf.Sin(2f * Mathf.PI * 130f * t));
                data[i] = wave * 0.35f * env;
            }
            AudioClip clip = AudioClip.Create("ErrorBuzz", samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        #endregion

        #region Hierarchy Builder

        public static MemoryTaskUI GetOrCreateInstance()
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
                Debug.LogWarning("[MemoryTaskUI] No Canvas found in scene.");
                return null;
            }

            _instance = CreateMemoryUIHierarchy(canvas);
            return _instance;
        }

        public static MemoryTaskUI CreateMemoryUIHierarchy(Canvas canvas)
        {
            Sprite bezelSprite = Resources.Load<Sprite>("UI/Tasks/TerminalBezel");

            // Root panel (Full Screen Vignette)
            GameObject root = new GameObject("MemoryMinigamePanel", typeof(RectTransform), typeof(AudioSource));
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
            headerTxt.text = "REACTOR MEMORY CORE // MK-IV";
            headerTxt.fontSize = 22;
            headerTxt.alignment = TextAlignmentOptions.Center;
            headerTxt.fontStyle = FontStyles.Bold;
            headerTxt.color = new Color(0.92f, 0.96f, 1f, 1f);

            // Subtitle / Instruction
            GameObject instGo = new GameObject("InstructionText", typeof(RectTransform), typeof(TextMeshProUGUI));
            instGo.transform.SetParent(box.transform, false);
            RectTransform instRect = instGo.GetComponent<RectTransform>();
            instRect.anchorMin = new Vector2(0f, 1f);
            instRect.anchorMax = new Vector2(1f, 1f);
            instRect.pivot = new Vector2(0.5f, 1f);
            instRect.anchoredPosition = new Vector2(0, -68);
            instRect.sizeDelta = new Vector2(-120, 26);
            TextMeshProUGUI instTxt = instGo.GetComponent<TextMeshProUGUI>();
            instTxt.text = "Memorize the pattern on the monitor, then replicate it on the keypad";
            instTxt.fontSize = 16;
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

            // ================= LEFT: MONITOR DISPLAY (3x3) =================
            GameObject monitorBox = new GameObject("MonitorHousing", typeof(RectTransform), typeof(Image));
            monitorBox.transform.SetParent(box.transform, false);
            RectTransform mBoxRect = monitorBox.GetComponent<RectTransform>();
            mBoxRect.sizeDelta = new Vector2(340, 360);
            mBoxRect.anchoredPosition = new Vector2(-200, 18);
            Image mBoxImg = monitorBox.GetComponent<Image>();
            mBoxImg.color = new Color(0.04f, 0.07f, 0.10f, 1f);

            Outline mOutline = monitorBox.AddComponent<Outline>();
            mOutline.effectColor = new Color(0.15f, 0.35f, 0.55f, 1f);
            mOutline.effectDistance = new Vector2(3, -3);

            // Monitor Header
            GameObject mHeadGo = new GameObject("MonitorHeader", typeof(RectTransform), typeof(TextMeshProUGUI));
            mHeadGo.transform.SetParent(monitorBox.transform, false);
            RectTransform mhRect = mHeadGo.GetComponent<RectTransform>();
            mhRect.anchoredPosition = new Vector2(0, 155);
            mhRect.sizeDelta = new Vector2(300, 24);
            TextMeshProUGUI mhTxt = mHeadGo.GetComponent<TextMeshProUGUI>();
            mhTxt.text = "SYSTEM PATTERN OUTPUT";
            mhTxt.fontSize = 14;
            mhTxt.fontStyle = FontStyles.Bold;
            mhTxt.alignment = TextAlignmentOptions.Center;
            mhTxt.color = new Color(0.4f, 0.7f, 0.95f, 1f);

            // Monitor 3x3 Grid
            Image[] mTiles = new Image[9];
            float tileSize = 74f;
            float spacing = 12f;
            float startX = -(tileSize + spacing);
            float startY = 70f;

            for (int r = 0; r < 3; r++)
            {
                for (int c = 0; c < 3; c++)
                {
                    int index = r * 3 + c;
                    GameObject tile = new GameObject($"MonitorTile_{index}", typeof(RectTransform), typeof(Image));
                    tile.transform.SetParent(monitorBox.transform, false);
                    RectTransform tRect = tile.GetComponent<RectTransform>();
                    tRect.sizeDelta = new Vector2(tileSize, tileSize);
                    tRect.anchoredPosition = new Vector2(startX + c * (tileSize + spacing), startY - r * (tileSize + spacing));

                    Image tImg = tile.GetComponent<Image>();
                    tImg.color = new Color(0.08f, 0.11f, 0.15f, 1f);

                    Outline tOutline = tile.AddComponent<Outline>();
                    tOutline.effectColor = new Color(0.12f, 0.22f, 0.32f, 1f);
                    tOutline.effectDistance = new Vector2(2, -2);

                    mTiles[index] = tImg;
                }
            }

            // Monitor Subtext Status
            GameObject mStatGo = new GameObject("MonitorStatusText", typeof(RectTransform), typeof(TextMeshProUGUI));
            mStatGo.transform.SetParent(monitorBox.transform, false);
            RectTransform msRect = mStatGo.GetComponent<RectTransform>();
            msRect.anchoredPosition = new Vector2(0, -152);
            msRect.sizeDelta = new Vector2(300, 24);
            TextMeshProUGUI msTxt = mStatGo.GetComponent<TextMeshProUGUI>();
            msTxt.text = "<color=#00E5FF>OBSERVING SEQUENCE...</color>";
            msTxt.fontSize = 13;
            msTxt.alignment = TextAlignmentOptions.Center;

            // ================= RIGHT: INPUT KEYPAD (3x3) =================
            GameObject keypadBox = new GameObject("KeypadHousing", typeof(RectTransform), typeof(Image));
            keypadBox.transform.SetParent(box.transform, false);
            RectTransform kBoxRect = keypadBox.GetComponent<RectTransform>();
            kBoxRect.sizeDelta = new Vector2(340, 360);
            kBoxRect.anchoredPosition = new Vector2(200, 18);
            Image kBoxImg = keypadBox.GetComponent<Image>();
            kBoxImg.color = new Color(0.06f, 0.08f, 0.11f, 1f);

            Outline kOutline = keypadBox.AddComponent<Outline>();
            kOutline.effectColor = new Color(0.25f, 0.32f, 0.4f, 1f);
            kOutline.effectDistance = new Vector2(3, -3);

            // Keypad Header
            GameObject kHeadGo = new GameObject("KeypadHeader", typeof(RectTransform), typeof(TextMeshProUGUI));
            kHeadGo.transform.SetParent(keypadBox.transform, false);
            RectTransform khRect = kHeadGo.GetComponent<RectTransform>();
            khRect.anchoredPosition = new Vector2(0, 155);
            khRect.sizeDelta = new Vector2(300, 24);
            TextMeshProUGUI khTxt = kHeadGo.GetComponent<TextMeshProUGUI>();
            khTxt.text = "MANUAL INPUT KEYPAD";
            khTxt.fontSize = 14;
            khTxt.fontStyle = FontStyles.Bold;
            khTxt.alignment = TextAlignmentOptions.Center;
            khTxt.color = new Color(0.85f, 0.9f, 0.95f, 1f);

            Button[] kBtns = new Button[9];
            Image[] kImgs = new Image[9];
            TextMeshProUGUI[] kTexts = new TextMeshProUGUI[9];

            for (int r = 0; r < 3; r++)
            {
                for (int c = 0; c < 3; c++)
                {
                    int index = r * 3 + c;
                    GameObject btnGo = new GameObject($"KeypadBtn_{index + 1}", typeof(RectTransform), typeof(Image), typeof(Button));
                    btnGo.transform.SetParent(keypadBox.transform, false);
                    RectTransform bRect = btnGo.GetComponent<RectTransform>();
                    bRect.sizeDelta = new Vector2(tileSize, tileSize);
                    bRect.anchoredPosition = new Vector2(startX + c * (tileSize + spacing), startY - r * (tileSize + spacing));

                    Image bImg = btnGo.GetComponent<Image>();
                    bImg.color = new Color(0.13f, 0.16f, 0.20f, 1f);

                    Outline bOutline = btnGo.AddComponent<Outline>();
                    bOutline.effectColor = new Color(0.28f, 0.38f, 0.48f, 1f);
                    bOutline.effectDistance = new Vector2(2, -2);

                    Button btn = btnGo.GetComponent<Button>();
                    ColorBlock cb = btn.colors;
                    cb.normalColor = Color.white;
                    cb.highlightedColor = new Color(1.2f, 1.2f, 1.2f, 1f);
                    cb.pressedColor = new Color(0.2f, 0.9f, 0.5f, 1f);
                    cb.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.6f);
                    btn.colors = cb;

                    // Button Number Label
                    GameObject numGo = new GameObject("NumberText", typeof(RectTransform), typeof(TextMeshProUGUI));
                    numGo.transform.SetParent(btnGo.transform, false);
                    RectTransform numRect = numGo.GetComponent<RectTransform>();
                    numRect.anchorMin = Vector2.zero;
                    numRect.anchorMax = Vector2.one;
                    numRect.sizeDelta = Vector2.zero;
                    TextMeshProUGUI numTxt = numGo.GetComponent<TextMeshProUGUI>();
                    numTxt.text = (index + 1).ToString();
                    numTxt.fontSize = 28;
                    numTxt.fontStyle = FontStyles.Bold;
                    numTxt.alignment = TextAlignmentOptions.Center;
                    numTxt.color = new Color(0.75f, 0.82f, 0.90f, 1f);

                    kBtns[index] = btn;
                    kImgs[index] = bImg;
                    kTexts[index] = numTxt;
                }
            }

            // Keypad Subtext
            GameObject kStatGo = new GameObject("KeypadStatusText", typeof(RectTransform), typeof(TextMeshProUGUI));
            kStatGo.transform.SetParent(keypadBox.transform, false);
            RectTransform ksRect = kStatGo.GetComponent<RectTransform>();
            ksRect.anchoredPosition = new Vector2(0, -152);
            ksRect.sizeDelta = new Vector2(300, 24);
            TextMeshProUGUI ksTxt = kStatGo.GetComponent<TextMeshProUGUI>();
            ksTxt.text = "<color=#8B949E>CLICK OR PRESS [1-9]</color>";
            ksTxt.fontSize = 13;
            ksTxt.alignment = TextAlignmentOptions.Center;

            // ================= BOTTOM: STAGE PROGRESS LEDS =================
            GameObject progFrame = new GameObject("ProgressFrame", typeof(RectTransform));
            progFrame.transform.SetParent(box.transform, false);
            RectTransform pfRect = progFrame.GetComponent<RectTransform>();
            pfRect.anchoredPosition = new Vector2(0, -205);
            pfRect.sizeDelta = new Vector2(380, 20);

            Image[] leds = new Image[5];
            float ledWidth = 58f;
            float ledSpacing = 14f;
            float ledStartX = -(2 * (ledWidth + ledSpacing));

            for (int i = 0; i < 5; i++)
            {
                GameObject ledGo = new GameObject($"StageLed_{i + 1}", typeof(RectTransform), typeof(Image));
                ledGo.transform.SetParent(progFrame.transform, false);
                RectTransform lRect = ledGo.GetComponent<RectTransform>();
                lRect.sizeDelta = new Vector2(ledWidth, 14);
                lRect.anchoredPosition = new Vector2(ledStartX + i * (ledWidth + ledSpacing), 0);

                Image lImg = ledGo.GetComponent<Image>();
                lImg.color = new Color(0.18f, 0.22f, 0.28f, 1f);

                Outline lOutline = ledGo.AddComponent<Outline>();
                lOutline.effectColor = new Color(0.28f, 0.35f, 0.42f, 1f);
                lOutline.effectDistance = new Vector2(1, -1);

                leds[i] = lImg;
            }

            // Bottom Stage text & info
            GameObject stgGo = new GameObject("StageText", typeof(RectTransform), typeof(TextMeshProUGUI));
            stgGo.transform.SetParent(box.transform, false);
            RectTransform stgRect = stgGo.GetComponent<RectTransform>();
            stgRect.anchoredPosition = new Vector2(0, -250);
            stgRect.sizeDelta = new Vector2(600, 28);
            TextMeshProUGUI stgTxt = stgGo.GetComponent<TextMeshProUGUI>();
            stgTxt.text = "<color=#00FF7F><b>STAGE: 1 / 5</b></color>";
            stgTxt.fontSize = 18;
            stgTxt.alignment = TextAlignmentOptions.Center;

            // Configure component
            AudioSource rootAudio = root.GetComponent<AudioSource>();
            if (rootAudio != null)
            {
                rootAudio.playOnAwake = false;
            }

            MemoryTaskUI memTask = root.AddComponent<MemoryTaskUI>();
            memTask.Configure(root, headerTxt, instTxt, stgTxt, closeBtn,
                              mTiles, msTxt, kBtns, kImgs, kTexts, leds, rootAudio);

            root.SetActive(false);
            return memTask;
        }

        public void Configure(GameObject root, TextMeshProUGUI header, TextMeshProUGUI inst, TextMeshProUGUI stage,
                              Button close, Image[] mTiles, TextMeshProUGUI mStatus, Button[] kBtns, Image[] kImgs,
                              TextMeshProUGUI[] kTexts, Image[] leds, AudioSource audio = null)
        {
            rootPanel = root;
            headerText = header;
            instructionText = inst;
            stageText = stage;
            closeButton = close;
            monitorTiles = mTiles;
            monitorStatusText = mStatus;
            keypadButtons = kBtns;
            keypadTileImages = kImgs;
            keypadTileTexts = kTexts;
            stageLeds = leds;

            if (audio != null)
            {
                audioSource = audio;
            }
            else if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
                if (audioSource == null)
                {
                    audioSource = gameObject.AddComponent<AudioSource>();
                }
            }

            if (closeButton != null)
            {
                closeButton.onClick.RemoveAllListeners();
                closeButton.onClick.AddListener(CloseMinigame);
            }

            SetupKeypadListeners();
            GenerateProceduralAudio();
        }

        #endregion
    }
}
