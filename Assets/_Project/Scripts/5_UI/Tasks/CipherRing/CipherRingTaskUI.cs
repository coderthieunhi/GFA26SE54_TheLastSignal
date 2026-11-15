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
    /// Sci-Fi Brutalist Concentric Cipher Ring Alignment Minigame.
    /// Strategic time-stalling puzzle featuring:
    /// - 3 concentric mechanical gear-coupled rings (Outer, Mid, Inner)
    /// - Full 3-Way Cascading Mechanical Linkage:
    ///   * Turning Outer (1.0x) rotates Mid (+0.65x) & Inner (-0.35x)
    ///   * Turning Mid (1.0x) rotates Outer (-0.50x) & Inner (+0.60x)
    ///   * Turning Inner (1.0x) rotates Mid (-0.45x) & Outer (+0.30x)
    /// - Central vertical laser baseline emitter with optic prism alignment detection
    /// - Pneumatic deadbolt lock mechanism with 1.2s stabilization hold
    /// - Dual-Bay Brutalist UI chassis matching TerminalBezel standards
    /// - Full mouse drag and New Input System keyboard controls (1-3, A/D, Arrows, Esc)
    /// - Procedural anti-aliased ring sprite generation & procedural PCM audio synthesis
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class CipherRingTaskUI : MonoBehaviour
    {
        private static CipherRingTaskUI _instance;
        public static CipherRingTaskUI Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = UnityEngine.Object.FindAnyObjectByType<CipherRingTaskUI>(FindObjectsInactive.Include);
                }
                return _instance;
            }
            private set => _instance = value;
        }

        #region Constants & Structs

        public const int NumRings = 3; // 0 = Outer, 1 = Mid, 2 = Inner
        public const float AlignmentTolerance = 6.5f; // Degrees
        public const float RequiredHoldDuration = 1.2f; // Seconds to stabilize lock

        public class RingData
        {
            public int index;
            public string name;
            public float currentAngle; // 0 to 360 deg. 0 deg = top vertical laser baseline!
            public bool isAligned;

            // UI Elements
            public GameObject rootGo;
            public RectTransform ringRect;
            public Image ringImage;
            public Outline ringOutline;
            public Image notchImage;
            public Image notchGlow;
            public TextMeshProUGUI telemetryAngleText;
            public TextMeshProUGUI telemetryStatusText;
            public Image activeHighlightRing;
        }

        #endregion

        #region Serialized UI References

        [Header("UI Panels")]
        [SerializeField] private GameObject rootPanel;
        [SerializeField] private RectTransform terminalBox;
        [SerializeField] private TextMeshProUGUI headerText;
        [SerializeField] private TextMeshProUGUI instructionText;
        [SerializeField] private Button closeButton;

        [Header("Cipher Chamber Bay")]
        [SerializeField] private RectTransform chamberBay;
        [SerializeField] private RectTransform chamberCenter;
        [SerializeField] private Image laserBeamImage;
        [SerializeField] private Image laserEmitterDiode;
        [SerializeField] private Image laserCoreDiode;
        [SerializeField] private Button[] ringSelectButtons = new Button[NumRings];

        [Header("Telemetry Bay")]
        [SerializeField] private TextMeshProUGUI lockStateText;
        [SerializeField] private TextMeshProUGUI beamFluxText;
        [SerializeField] private TextMeshProUGUI stabilizationPercentText;
        [SerializeField] private Image stabilizationBarFill;

        [Header("Audio")]
        [SerializeField] private AudioSource audioSource;

        #endregion

        #region Internal State

        private RingData[] rings = new RingData[NumRings];
        private int activeRingIndex = 0; // 0 = Outer, 1 = Mid, 2 = Inner

        private PlayerController activePlayer;
        private TaskLocation activeTaskLocation;

        private bool isFinished = false;
        private float holdTimer = 0f;

        // Mouse Drag Tracking
        private bool isDragging = false;
        private int draggedRingIndex = -1;
        private float previousMouseAngle = 0f;

        // Gear Ratchet Sound Accumulator
        private float accumulatedRatchetAngle = 0f;
        private const float AnglePerRatchetClick = 8f;

        private float fluxJitterTimer = 0f;
        private float displayedFlux = 640.0f;

        // Procedural Audio Clips
        private AudioClip ratchetClickClip;
        private AudioClip prismPulseClip;
        private AudioClip deadboltLockClip;
        private AudioClip successChimeClip;

        // Visual Colors
        private readonly Color ColorChamberBg     = new Color(0.045f, 0.07f, 0.10f, 0.95f);
        private readonly Color ColorRingNormal    = new Color(0.22f, 0.32f, 0.44f, 1f);
        private readonly Color ColorRingSelected  = new Color(0f, 0.94f, 1f, 1f);
        private readonly Color ColorLaserIdle     = new Color(0f, 0.94f, 1f, 0.4f);
        private readonly Color ColorLaserLocked   = new Color(0f, 1f, 0.5f, 1f);
        private readonly Color ColorNotchUnlit    = new Color(0.40f, 0.50f, 0.62f, 1f);
        private readonly Color ColorNotchLit      = new Color(0f, 1f, 0.5f, 1f);

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

            if (ringSelectButtons != null)
            {
                for (int i = 0; i < NumRings; i++)
                {
                    int idx = i;
                    if (i < ringSelectButtons.Length && ringSelectButtons[i] != null)
                    {
                        ringSelectButtons[i].onClick.RemoveAllListeners();
                        ringSelectButtons[i].onClick.AddListener(() => SelectRing(idx));
                    }
                }
            }
        }

        private void Update()
        {
            if (!IsOpen || isFinished) return;

            // Handle Laser Flux Readout Jitter
            fluxJitterTimer += Time.deltaTime;
            if (fluxJitterTimer >= 0.12f)
            {
                fluxJitterTimer = 0f;
                float jitter = UnityEngine.Random.Range(-2.5f, 2.8f);
                displayedFlux = 640.0f + jitter;
                if (beamFluxText != null)
                {
                    beamFluxText.text = $"{displayedFlux:F1} THz // WAVELENGTH 468nm";
                }
            }

            HandleKeyboardInput();
            HandleMouseDrag();
            UpdateAlignmentState();
        }

        #endregion

        #region Interaction & Controls (New Input System & Mouse Drag)

        private void HandleKeyboardInput()
        {
            var kb = Keyboard.current;
            if (kb == null) return;

            // ESC to Close
            if (kb.escapeKey.wasPressedThisFrame)
            {
                CloseMinigame();
                return;
            }

            // Ring Selection Keys (1 = Outer, 2 = Mid, 3 = Inner)
            if (kb.digit1Key.wasPressedThisFrame || kb.numpad1Key.wasPressedThisFrame) SelectRing(0);
            else if (kb.digit2Key.wasPressedThisFrame || kb.numpad2Key.wasPressedThisFrame) SelectRing(1);
            else if (kb.digit3Key.wasPressedThisFrame || kb.numpad3Key.wasPressedThisFrame) SelectRing(2);

            // Cycle selection with W / S or Up / Down
            if (kb.wKey.wasPressedThisFrame || kb.upArrowKey.wasPressedThisFrame)
            {
                SelectRing((activeRingIndex + 1) % NumRings);
            }
            else if (kb.sKey.wasPressedThisFrame || kb.downArrowKey.wasPressedThisFrame)
            {
                SelectRing((activeRingIndex - 1 + NumRings) % NumRings);
            }

            // Continuous rotation with A / D or Left / Right
            float rotateSpeed = 65f; // Deg/sec
            float delta = 0f;

            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed)
            {
                delta -= rotateSpeed * Time.deltaTime;
            }
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed)
            {
                delta += rotateSpeed * Time.deltaTime;
            }

            if (Mathf.Abs(delta) > 0.0001f)
            {
                ApplyGearCoupledRotation(activeRingIndex, delta);
            }
        }

        private void HandleMouseDrag()
        {
            var mouse = Mouse.current;
            if (mouse == null) return;

            Vector2 mousePos = mouse.position.ReadValue();

            if (mouse.leftButton.wasPressedThisFrame)
            {
                // Check if mouse clicked inside the chamber
                if (chamberCenter != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    chamberCenter, mousePos, null, out Vector2 localPoint))
                {
                    float dist = localPoint.magnitude;
                    // Determine which ring was clicked based on radius
                    if (dist >= 140f && dist <= 200f) { BeginDragRing(0, localPoint); } // Outer
                    else if (dist >= 90f && dist < 140f) { BeginDragRing(1, localPoint); } // Mid
                    else if (dist >= 35f && dist < 90f)  { BeginDragRing(2, localPoint); } // Inner
                }
            }
            else if (mouse.leftButton.isPressed && isDragging)
            {
                if (chamberCenter != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    chamberCenter, mousePos, null, out Vector2 localPoint))
                {
                    float currentAngle = Mathf.Atan2(localPoint.x, localPoint.y) * Mathf.Rad2Deg;
                    float deltaAngle = Mathf.DeltaAngle(previousMouseAngle, currentAngle);

                    if (Mathf.Abs(deltaAngle) > 0.01f)
                    {
                        ApplyGearCoupledRotation(draggedRingIndex, deltaAngle);
                        previousMouseAngle = currentAngle;
                    }
                }
            }
            else if (mouse.leftButton.wasReleasedThisFrame && isDragging)
            {
                isDragging = false;
                draggedRingIndex = -1;
            }
        }

        private void BeginDragRing(int ringIdx, Vector2 localPoint)
        {
            isDragging = true;
            draggedRingIndex = ringIdx;
            SelectRing(ringIdx);
            previousMouseAngle = Mathf.Atan2(localPoint.x, localPoint.y) * Mathf.Rad2Deg;
        }

        public void SelectRing(int ringIdx)
        {
            activeRingIndex = Mathf.Clamp(ringIdx, 0, NumRings - 1);
            UpdateRingSelectionVisuals();
            PlayRatchetSound(0.25f);
        }

        private void UpdateRingSelectionVisuals()
        {
            for (int i = 0; i < NumRings; i++)
            {
                bool isSel = (i == activeRingIndex);
                if (rings[i]?.activeHighlightRing != null)
                {
                    rings[i].activeHighlightRing.gameObject.SetActive(isSel);
                }
                if (rings[i]?.ringOutline != null)
                {
                    rings[i].ringOutline.effectColor = isSel ? ColorRingSelected : new Color(0.18f, 0.28f, 0.38f, 1f);
                    rings[i].ringOutline.effectDistance = isSel ? new Vector2(2, -2) : new Vector2(1, -1);
                }
            }
        }

        #endregion

        #region Gear-Coupled Rotation Logic

        /// <summary>
        /// Rotates the manipulated ring by delta, driving both remaining rings via full 3-way cascading gear linkage!
        /// </summary>
        private void ApplyGearCoupledRotation(int ringIndex, float delta)
        {
            if (isFinished) return;

            switch (ringIndex)
            {
                case 0: // Outer Ring Manipulated (1.0x) -> Drives Mid (+0.65x) & Inner (-0.35x)
                    RotateRing(0, delta);
                    RotateRing(1, delta * 0.65f);
                    RotateRing(2, -delta * 0.35f);
                    break;

                case 1: // Mid Ring Manipulated (1.0x) -> Drives Outer (-0.50x) & Inner (+0.60x)
                    RotateRing(1, delta);
                    RotateRing(0, -delta * 0.50f);
                    RotateRing(2, delta * 0.60f);
                    break;

                case 2: // Inner Ring Manipulated (1.0x) -> Drives Mid (-0.45x) & Outer (+0.30x)
                    RotateRing(2, delta);
                    RotateRing(1, -delta * 0.45f);
                    RotateRing(0, delta * 0.30f);
                    break;
            }

            // Audio Ratchet Tick Accumulation
            accumulatedRatchetAngle += Mathf.Abs(delta);
            if (accumulatedRatchetAngle >= AnglePerRatchetClick)
            {
                accumulatedRatchetAngle = 0f;
                PlayRatchetSound(0.35f);
            }
        }

        private void RotateRing(int index, float delta)
        {
            RingData ring = rings[index];
            if (ring == null) return;

            ring.currentAngle = (ring.currentAngle + delta) % 360f;
            if (ring.currentAngle < 0f) ring.currentAngle += 360f;

            if (ring.ringRect != null)
            {
                // In Unity 2D, rotating clockwise means negative Z angle
                ring.ringRect.localEulerAngles = new Vector3(0, 0, -ring.currentAngle);
            }
        }

        #endregion

        #region Alignment Evaluation & Stabilization Lock

        private void UpdateAlignmentState()
        {
            int alignedCount = 0;

            for (int i = 0; i < NumRings; i++)
            {
                RingData ring = rings[i];
                if (ring == null) continue;

                // 0 deg is top vertical laser baseline
                float angleDiff = Mathf.Abs(Mathf.DeltaAngle(ring.currentAngle, 0f));
                bool wasAligned = ring.isAligned;
                ring.isAligned = (angleDiff <= AlignmentTolerance);

                if (ring.isAligned) alignedCount++;

                // Sound when entering alignment notch
                if (ring.isAligned && !wasAligned)
                {
                    PlayPrismPulse();
                }

                // Update Notch & Telemetry visuals
                Color notchCol = ring.isAligned ? ColorNotchLit : ColorNotchUnlit;
                if (ring.notchImage != null) ring.notchImage.color = notchCol;
                if (ring.notchGlow != null) ring.notchGlow.color = ring.isAligned ? new Color(0f, 1f, 0.5f, 0.6f) : Color.clear;

                if (ring.telemetryAngleText != null)
                {
                    ring.telemetryAngleText.text = $"{ring.currentAngle:F1}°";
                }
                if (ring.telemetryStatusText != null)
                {
                    float matchPct = Mathf.Clamp01(1f - (angleDiff / 90f)) * 100f;
                    ring.telemetryStatusText.text = ring.isAligned 
                        ? "<color=#00FF7F>LOCKED [✔]</color>" 
                        : $"<color=#88AABB>{matchPct:F0}% DRIFT</color>";
                }
            }

            // Check if all 3 rings are aligned simultaneously along laser beam
            bool allAligned = (alignedCount == NumRings);

            if (allAligned)
            {
                holdTimer += Time.deltaTime;
                float progress = Mathf.Clamp01(holdTimer / RequiredHoldDuration);

                if (stabilizationBarFill != null)
                {
                    stabilizationBarFill.fillAmount = progress;
                    stabilizationBarFill.color = ColorLaserLocked;
                }
                if (stabilizationPercentText != null)
                {
                    stabilizationPercentText.text = $"STABILIZING OPTICAL LOCK: {(int)(progress * 100)}%";
                }
                if (laserBeamImage != null)
                {
                    laserBeamImage.color = ColorLaserLocked;
                }
                if (lockStateText != null)
                {
                    lockStateText.text = "<color=#00FF7F>BEAM CONDUCTING // ENGAGING</color>";
                }

                if (holdTimer >= RequiredHoldDuration)
                {
                    TriggerDecryptionSuccess();
                }
            }
            else
            {
                // Gradually decay stabilization progress if alignment breaks
                holdTimer = Mathf.Max(0f, holdTimer - Time.deltaTime * 2.5f);
                float progress = Mathf.Clamp01(holdTimer / RequiredHoldDuration);

                if (stabilizationBarFill != null)
                {
                    stabilizationBarFill.fillAmount = progress;
                    stabilizationBarFill.color = ColorRingSelected;
                }
                if (stabilizationPercentText != null)
                {
                    stabilizationPercentText.text = alignedCount > 0 
                        ? $"BEAM PARTIAL: {alignedCount} / 3 NOTCHES" 
                        : "AWAITING BEAM ALIGNMENT (0%)";
                }
                if (laserBeamImage != null)
                {
                    laserBeamImage.color = ColorLaserIdle;
                }
                if (lockStateText != null)
                {
                    lockStateText.text = "<color=#00F0FF>OPTIC BEAM DISENGAGED</color>";
                }
            }
        }

        private void TriggerDecryptionSuccess()
        {
            isFinished = true;
            PlayDeadboltLock();
            PlaySuccessChime();

            if (lockStateText != null)
            {
                lockStateText.text = "<color=#00FF7F>DEADBOLT LOCKED // 100% NOMINAL</color>";
            }
            if (stabilizationPercentText != null)
            {
                stabilizationPercentText.text = "PNEUMATIC DEADBOLT SECURED";
            }

            StartCoroutine(VictoryRoutine());
        }

        private IEnumerator VictoryRoutine()
        {
            // Flash laser and chamber with brilliant power surge
            for (int step = 0; step < 3; step++)
            {
                if (laserBeamImage != null) laserBeamImage.color = Color.white;
                yield return new WaitForSeconds(0.1f);
                if (laserBeamImage != null) laserBeamImage.color = ColorLaserLocked;
                yield return new WaitForSeconds(0.12f);
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
            holdTimer = 0f;
            isDragging = false;

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
                headerText.text = $"CONCENTRIC CIPHER TERMINAL // {taskLocation.StationDisplayName.ToUpper()} [STAGE {currentStep}/{targetSteps}]";
            }

            // Scramble initial angles with non-aligned offsets
            int seed = 42;
            if (taskLocation != null)
            {
                seed = taskLocation.StationId.GetHashCode() + (int)(Time.time * 10f);
            }
            System.Random rng = new System.Random(seed);

            // Randomize starting angles between 45° and 315° (away from 0° alignment)
            rings[0].currentAngle = rng.Next(60, 300);
            rings[1].currentAngle = rng.Next(60, 300);
            rings[2].currentAngle = rng.Next(60, 300);

            for (int i = 0; i < NumRings; i++)
            {
                if (rings[i]?.ringRect != null)
                {
                    rings[i].ringRect.localEulerAngles = new Vector3(0, 0, -rings[i].currentAngle);
                }
            }

            SelectRing(0);
            UpdateAlignmentState();
            BindAllButtons();

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
            if (ratchetClickClip == null) ratchetClickClip = CreateRatchetClickClip();
            if (prismPulseClip == null) prismPulseClip = CreatePrismPulseClip();
            if (deadboltLockClip == null) deadboltLockClip = CreateDeadboltLockClip();
            if (successChimeClip == null) successChimeClip = CreateSuccessChimeClip();
        }

        private void PlayRatchetSound(float volume = 0.35f)
        {
            if (audioSource != null && ratchetClickClip != null)
            {
                audioSource.PlayOneShot(ratchetClickClip, volume);
            }
        }

        private void PlayPrismPulse()
        {
            if (audioSource != null && prismPulseClip != null)
            {
                audioSource.PlayOneShot(prismPulseClip, 0.65f);
            }
        }

        private void PlayDeadboltLock()
        {
            if (audioSource != null && deadboltLockClip != null)
            {
                audioSource.PlayOneShot(deadboltLockClip, 0.90f);
            }
        }

        private void PlaySuccessChime()
        {
            if (audioSource != null && successChimeClip != null)
            {
                audioSource.PlayOneShot(successChimeClip, 0.90f);
            }
        }

        private static AudioClip CreateRatchetClickClip()
        {
            int sampleRate = 44100;
            float duration = 0.035f;
            int samples = (int)(sampleRate * duration);
            float[] data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / sampleRate;
                float env = 1f - (t / duration);
                float noise = (UnityEngine.Random.value * 2f - 1f) * 0.35f;
                float tone = Mathf.Sin(2f * Mathf.PI * 1800f * t) * 0.65f;
                data[i] = (tone + noise) * env * 0.45f;
            }
            AudioClip clip = AudioClip.Create("CR_Ratchet", samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip CreatePrismPulseClip()
        {
            int sampleRate = 44100;
            float duration = 0.22f;
            int samples = (int)(sampleRate * duration);
            float[] data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / sampleRate;
                float env = 1f - Mathf.Pow(t / duration, 0.7f);
                float tone = Mathf.Sin(2f * Mathf.PI * 1046.5f * t) * 0.5f + // C6
                             Mathf.Sin(2f * Mathf.PI * 2093.0f * t) * 0.3f;
                data[i] = tone * env * 0.55f;
            }
            AudioClip clip = AudioClip.Create("CR_PrismPulse", samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip CreateDeadboltLockClip()
        {
            int sampleRate = 44100;
            float duration = 0.35f;
            int samples = (int)(sampleRate * duration);
            float[] data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / sampleRate;
                float env = 1f - Mathf.Pow(t / duration, 0.8f);
                float thud = Mathf.Sin(2f * Mathf.PI * 65f * t) * 0.6f;
                float snap = Mathf.Sin(2f * Mathf.PI * 800f * t) * 0.4f * Mathf.Exp(-t * 25f);
                data[i] = (thud + snap) * env * 0.85f;
            }
            AudioClip clip = AudioClip.Create("CR_Deadbolt", samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip CreateSuccessChimeClip()
        {
            int sampleRate = 44100;
            float duration = 0.85f;
            int samples = (int)(sampleRate * duration);
            float[] data = new float[samples];
            float[] freqs = { 261.63f, 329.63f, 392.00f, 523.25f, 659.25f }; // C major 9th

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / sampleRate;
                float env = 1f - Mathf.Pow(t / duration, 0.85f);
                float sum = 0f;
                for (int f = 0; f < freqs.Length; f++)
                {
                    float noteStart = f * 0.06f;
                    if (t >= noteStart)
                    {
                        float localT = t - noteStart;
                        sum += Mathf.Sin(2f * Mathf.PI * freqs[f] * localT) * 0.20f;
                    }
                }
                data[i] = sum * env;
            }
            AudioClip clip = AudioClip.Create("CR_SuccessChime", samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        #endregion

        #region Runtime UI Hierarchy Factory

        public static CipherRingTaskUI GetOrCreateInstance()
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
                Debug.LogWarning("[CipherRingTaskUI] No Canvas found in scene.");
                return null;
            }

            _instance = CreateCipherRingUIHierarchy(canvas);
            return _instance;
        }

        public static CipherRingTaskUI CreateCipherRingUIHierarchy(Canvas canvas)
        {
            Sprite chassisSprite = Resources.Load<Sprite>("UI/Tasks/TerminalBezel");

            // Fullscreen Dark Vignette Overlay
            GameObject root = new GameObject("CipherRingMinigamePanel", typeof(RectTransform), typeof(AudioSource));
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
            headerText.text = "CONCENTRIC CIPHER TERMINAL // ROTARY COUPLER";
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
            instText.text = "Rotate coupled mechanical rings to align optical notches along the <color=#00FF7F>CENTRAL LASER BASELINE</color>.";
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
            // BAY 1 (LEFT): Optical Alignment Chamber (530 x 480)
            // ------------------------------------------
            GameObject chamberBayGo = new GameObject("ChamberBay", typeof(RectTransform), typeof(Image));
            chamberBayGo.transform.SetParent(contentGo.transform, false);
            RectTransform chamberBayRect = chamberBayGo.GetComponent<RectTransform>();
            chamberBayRect.anchorMin = new Vector2(0f, 0f);
            chamberBayRect.anchorMax = new Vector2(0f, 1f);
            chamberBayRect.pivot = new Vector2(0f, 0.5f);
            chamberBayRect.anchoredPosition = new Vector2(0, 0);
            chamberBayRect.sizeDelta = new Vector2(530, 0);
            Image chamberBayImg = chamberBayGo.GetComponent<Image>();
            chamberBayImg.color = new Color(0.045f, 0.07f, 0.10f, 0.95f);
            Outline cBayOutline = chamberBayGo.AddComponent<Outline>();
            cBayOutline.effectColor = new Color(0.18f, 0.28f, 0.38f, 1f);
            cBayOutline.effectDistance = new Vector2(1, -1);

            // Bay Title
            GameObject cBayTitleGo = new GameObject("BayTitle", typeof(RectTransform), typeof(TextMeshProUGUI));
            cBayTitleGo.transform.SetParent(chamberBayGo.transform, false);
            RectTransform cbtRect = cBayTitleGo.GetComponent<RectTransform>();
            cbtRect.anchorMin = new Vector2(0f, 1f);
            cbtRect.anchorMax = new Vector2(1f, 1f);
            cbtRect.pivot = new Vector2(0.5f, 1f);
            cbtRect.anchoredPosition = new Vector2(0, -12);
            cbtRect.sizeDelta = new Vector2(-20, 24);
            TextMeshProUGUI cbtText = cBayTitleGo.GetComponent<TextMeshProUGUI>();
            cbtText.text = "[ OPTICAL ALIGNMENT CHAMBER // 3-TIER GEAR LINKAGE ]";
            cbtText.fontSize = 13;
            cbtText.fontStyle = FontStyles.Bold;
            cbtText.alignment = TextAlignmentOptions.Center;
            cbtText.color = new Color(0.45f, 0.75f, 0.95f, 1f);

            // Chamber Center Hub (Center for 3 concentric rings)
            GameObject chamberCenterGo = new GameObject("ChamberCenter", typeof(RectTransform));
            chamberCenterGo.transform.SetParent(chamberBayGo.transform, false);
            RectTransform ccRect = chamberCenterGo.GetComponent<RectTransform>();
            ccRect.anchorMin = new Vector2(0.5f, 0.5f);
            ccRect.anchorMax = new Vector2(0.5f, 0.5f);
            ccRect.pivot = new Vector2(0.5f, 0.5f);
            ccRect.anchoredPosition = new Vector2(0, 16);
            ccRect.sizeDelta = new Vector2(400, 400);

            // Vertical Laser Baseline (from (0,0) to (0, 205))
            GameObject laserGo = new GameObject("LaserBaseline", typeof(RectTransform), typeof(Image));
            laserGo.transform.SetParent(chamberCenterGo.transform, false);
            RectTransform laserRect = laserGo.GetComponent<RectTransform>();
            laserRect.anchorMin = new Vector2(0.5f, 0.5f);
            laserRect.anchorMax = new Vector2(0.5f, 0.5f);
            laserRect.pivot = new Vector2(0.5f, 0f);
            laserRect.anchoredPosition = new Vector2(0, 0);
            laserRect.sizeDelta = new Vector2(4, 208);
            Image laserImg = laserGo.GetComponent<Image>();
            laserImg.color = new Color(0f, 0.94f, 1f, 0.45f);
            laserImg.raycastTarget = false;

            // Top Laser Emitter Diode
            GameObject emitterGo = new GameObject("LaserEmitter", typeof(RectTransform), typeof(Image));
            emitterGo.transform.SetParent(chamberCenterGo.transform, false);
            RectTransform emRect = emitterGo.GetComponent<RectTransform>();
            emRect.anchorMin = new Vector2(0.5f, 0.5f);
            emRect.anchorMax = new Vector2(0.5f, 0.5f);
            emRect.pivot = new Vector2(0.5f, 0.5f);
            emRect.anchoredPosition = new Vector2(0, 206);
            emRect.sizeDelta = new Vector2(18, 14);
            Image emImg = emitterGo.GetComponent<Image>();
            emImg.color = new Color(0f, 1f, 0.5f, 1f);

            // Central Core Diode (Hub)
            GameObject coreGo = new GameObject("CoreDiode", typeof(RectTransform), typeof(Image));
            coreGo.transform.SetParent(chamberCenterGo.transform, false);
            RectTransform coreRect = coreGo.GetComponent<RectTransform>();
            coreRect.anchorMin = new Vector2(0.5f, 0.5f);
            coreRect.anchorMax = new Vector2(0.5f, 0.5f);
            coreRect.pivot = new Vector2(0.5f, 0.5f);
            coreRect.anchoredPosition = Vector2.zero;
            coreRect.sizeDelta = new Vector2(48, 48);
            Image coreImg = coreGo.GetComponent<Image>();
            coreImg.color = new Color(0.12f, 0.18f, 0.25f, 1f);
            coreGo.transform.SetAsLastSibling();

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
            tbtText.text = "[ ALIGNMENT TELEMETRY & OPTICS ]";
            tbtText.fontSize = 13;
            tbtText.fontStyle = FontStyles.Bold;
            tbtText.alignment = TextAlignmentOptions.Center;
            tbtText.color = new Color(0.45f, 0.75f, 0.95f, 1f);

            // Ring Diagnostics Status Card
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

            TextMeshProUGUI outerStatusTxt = CreateTelemetryRow(statusCardGo, 0, "[1] OUTER RING:", "0.0° // DRIFT");
            TextMeshProUGUI midStatusTxt   = CreateTelemetryRow(statusCardGo, 1, "[2] MID RING:", "0.0° // DRIFT");
            TextMeshProUGUI innerStatusTxt = CreateTelemetryRow(statusCardGo, 2, "[3] INNER RING:", "0.0° // DRIFT");
            TextMeshProUGUI beamFluxTxt    = CreateTelemetryRow(statusCardGo, 3, "BEAM FREQUENCY:", "640.0 THz // 468nm");

            // Pneumatic Deadbolt Lock Progress Section
            GameObject lockCardGo = new GameObject("LockCard", typeof(RectTransform), typeof(Image));
            lockCardGo.transform.SetParent(telemBayGo.transform, false);
            RectTransform lcRect = lockCardGo.GetComponent<RectTransform>();
            lcRect.anchorMin = new Vector2(0f, 1f);
            lcRect.anchorMax = new Vector2(1f, 1f);
            lcRect.pivot = new Vector2(0.5f, 1f);
            lcRect.anchoredPosition = new Vector2(0, -192);
            lcRect.sizeDelta = new Vector2(-28, 76);
            Image lcImg = lockCardGo.GetComponent<Image>();
            lcImg.color = new Color(0.08f, 0.12f, 0.17f, 0.9f);

            TextMeshProUGUI lockStateTxt = CreateTelemetryRow(lockCardGo, 0, "DEADBOLT STATE:", "<color=#00F0FF>DISENGAGED</color>");

            // Progress Bar Frame for Stabilization Lock
            GameObject barFrameGo = new GameObject("LockBarFrame", typeof(RectTransform), typeof(Image));
            barFrameGo.transform.SetParent(lockCardGo.transform, false);
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
            barFillImg.fillAmount = 0f;
            barFillImg.color = new Color(0f, 0.94f, 1f, 1f);

            GameObject barTextGo = new GameObject("BarText", typeof(RectTransform), typeof(TextMeshProUGUI));
            barTextGo.transform.SetParent(barFrameGo.transform, false);
            RectTransform btRect = barTextGo.GetComponent<RectTransform>();
            btRect.anchorMin = Vector2.zero;
            btRect.anchorMax = Vector2.one;
            btRect.offsetMin = Vector2.zero;
            btRect.offsetMax = Vector2.zero;
            TextMeshProUGUI barTxt = barTextGo.GetComponent<TextMeshProUGUI>();
            barTxt.text = "AWAITING BEAM ALIGNMENT (0%)";
            barTxt.fontSize = 10;
            barTxt.fontStyle = FontStyles.Bold;
            barTxt.alignment = TextAlignmentOptions.Center;
            barTxt.color = Color.white;

            // Gear Ratio Coupling Legend Card
            GameObject legendCardGo = new GameObject("LegendCard", typeof(RectTransform), typeof(Image));
            legendCardGo.transform.SetParent(telemBayGo.transform, false);
            RectTransform legcRect = legendCardGo.GetComponent<RectTransform>();
            legcRect.anchorMin = new Vector2(0f, 1f);
            legcRect.anchorMax = new Vector2(1f, 1f);
            legcRect.pivot = new Vector2(0.5f, 1f);
            legcRect.anchoredPosition = new Vector2(0, -278);
            legcRect.sizeDelta = new Vector2(-28, 86);
            Image legcImg = legendCardGo.GetComponent<Image>();
            legcImg.color = new Color(0.08f, 0.12f, 0.17f, 0.9f);

            GameObject legendTextGo = new GameObject("LegendText", typeof(RectTransform), typeof(TextMeshProUGUI));
            legendTextGo.transform.SetParent(legendCardGo.transform, false);
            RectTransform ltRect = legendTextGo.GetComponent<RectTransform>();
            ltRect.anchorMin = Vector2.zero;
            ltRect.anchorMax = Vector2.one;
            ltRect.offsetMin = new Vector2(10, 6);
            ltRect.offsetMax = new Vector2(-10, -6);
            TextMeshProUGUI legTxt = legendTextGo.GetComponent<TextMeshProUGUI>();
            legTxt.text = "<b>FULL 3-WAY GEAR RATIO LINKAGE:</b>\n" +
                          "<color=#00F0FF>• [1] OUTER</color> : Drives Mid (+0.65x) & Inner (-0.35x)\n" +
                          "<color=#00F0FF>• [2] MID</color>   : Drives Outer (-0.50x) & Inner (+0.60x)\n" +
                          "<color=#00F0FF>• [3] INNER</color> : Drives Mid (-0.45x) & Outer (+0.30x)";
            legTxt.fontSize = 11;
            legTxt.lineSpacing = 12f;
            legTxt.color = new Color(0.85f, 0.9f, 0.95f, 1f);

            // Controls Guideline Plate
            GameObject controlsCardGo = new GameObject("ControlsCard", typeof(RectTransform), typeof(Image));
            controlsCardGo.transform.SetParent(telemBayGo.transform, false);
            RectTransform ccardRect = controlsCardGo.GetComponent<RectTransform>();
            ccardRect.anchorMin = new Vector2(0f, 0f);
            ccardRect.anchorMax = new Vector2(1f, 0f);
            ccardRect.pivot = new Vector2(0.5f, 0f);
            ccardRect.anchoredPosition = new Vector2(0, 12);
            ccardRect.sizeDelta = new Vector2(-28, 84);
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
            ctrlTxt.text = "<b>ROTARY ACTUATOR CONTROLS:</b>\n" +
                           "• <b>[1 / 2 / 3]</b>: Select Outer / Mid / Inner Ring\n" +
                           "• <b>[A / D] or [ARROWS]</b>: Rotate Selected Ring\n" +
                           "• <b>[DRAG MOUSE]</b>: Click & Drag Directly on Ring\n" +
                           "• <b>[ESC]</b>: Emergency Disengage & Close";
            ctrlTxt.fontSize = 10.5f;
            ctrlTxt.lineSpacing = 10f;
            ctrlTxt.color = new Color(0.7f, 0.82f, 0.92f, 1f);

            // ==========================================
            // BUILD 3 CONCENTRIC RINGS
            // ==========================================
            CipherRingTaskUI ui = root.AddComponent<CipherRingTaskUI>();
            ui.rootPanel = root;
            ui.terminalBox = boxRect;
            ui.headerText = headerText;
            ui.instructionText = instText;
            ui.closeButton = closeBtn;

            ui.chamberBay = chamberBayRect;
            ui.chamberCenter = ccRect;
            ui.laserBeamImage = laserImg;
            ui.laserEmitterDiode = emImg;
            ui.laserCoreDiode = coreImg;

            ui.lockStateText = lockStateTxt;
            ui.beamFluxText = beamFluxTxt;
            ui.stabilizationPercentText = barTxt;
            ui.stabilizationBarFill = barFillImg;
            ui.audioSource = root.GetComponent<AudioSource>();

            // Generate Ring 0 (Outer: dia 370), Ring 1 (Mid: dia 270), Ring 2 (Inner: dia 170)
            float[] ringDiameters = { 370f, 270f, 170f };
            float[] ringNotchRadii = { 185f, 135f, 85f };
            string[] ringNames = { "OUTER RING", "MID RING", "INNER RING" };
            TextMeshProUGUI[] statusTexts = { outerStatusTxt, midStatusTxt, innerStatusTxt };

            for (int i = 0; i < NumRings; i++)
            {
                ui.rings[i] = CreateRingObject(chamberCenterGo.transform, i, ringNames[i], 
                    ringDiameters[i], ringNotchRadii[i], statusTexts[i], ui);
            }

            // Bottom Ring Selector Quick-Tabs in Chamber Bay (3 tabs: [1] OUTER, [2] MID, [3] INNER)
            GameObject tabsGo = new GameObject("RingSelectorTabs", typeof(RectTransform));
            tabsGo.transform.SetParent(chamberBayGo.transform, false);
            RectTransform tabsRect = tabsGo.GetComponent<RectTransform>();
            tabsRect.anchorMin = new Vector2(0.5f, 0f);
            tabsRect.anchorMax = new Vector2(0.5f, 0f);
            tabsRect.pivot = new Vector2(0.5f, 0f);
            tabsRect.anchoredPosition = new Vector2(0, 12);
            tabsRect.sizeDelta = new Vector2(490, 36);

            for (int i = 0; i < NumRings; i++)
            {
                int rIdx = i;
                float tabX = -160f + (i * 160f);
                GameObject tabGo = new GameObject($"Tab_{i}", typeof(RectTransform), typeof(Image), typeof(Button));
                tabGo.transform.SetParent(tabsGo.transform, false);
                RectTransform tr = tabGo.GetComponent<RectTransform>();
                tr.sizeDelta = new Vector2(150, 34);
                tr.anchoredPosition = new Vector2(tabX, 0);

                Image tImg = tabGo.GetComponent<Image>();
                tImg.color = new Color(0.08f, 0.12f, 0.17f, 1f);
                Outline to = tabGo.AddComponent<Outline>();
                to.effectColor = new Color(0.20f, 0.30f, 0.42f, 1f);

                GameObject tTxtGo = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
                tTxtGo.transform.SetParent(tabGo.transform, false);
                RectTransform ttr = tTxtGo.GetComponent<RectTransform>();
                ttr.anchorMin = Vector2.zero;
                ttr.anchorMax = Vector2.one;
                ttr.offsetMin = Vector2.zero;
                ttr.offsetMax = Vector2.zero;
                TextMeshProUGUI tTxt = tTxtGo.GetComponent<TextMeshProUGUI>();
                tTxt.text = $"<b>[{i + 1}] {ringNames[i]}</b>";
                tTxt.fontSize = 11;
                tTxt.alignment = TextAlignmentOptions.Center;
                tTxt.color = new Color(0.7f, 0.85f, 0.95f, 1f);

                ui.ringSelectButtons[i] = tabGo.GetComponent<Button>();
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

        private static RingData CreateRingObject(Transform parent, int ringIdx, string ringName, 
            float diameter, float notchRadius, TextMeshProUGUI statusText, CipherRingTaskUI parentUI)
        {
            RingData ring = new RingData();
            ring.index = ringIdx;
            ring.name = ringName;
            ring.currentAngle = 0f;
            ring.isAligned = false;
            ring.telemetryStatusText = statusText;

            // Ring Root RectTransform (rotates around Z)
            GameObject ringGo = new GameObject($"Ring_{ringIdx}_{ringName}", typeof(RectTransform));
            ringGo.transform.SetParent(parent, false);
            RectTransform ringRect = ringGo.GetComponent<RectTransform>();
            ringRect.sizeDelta = new Vector2(diameter, diameter);
            ringRect.anchoredPosition = Vector2.zero;
            ring.rootGo = ringGo;
            ring.ringRect = ringRect;

            // Generate circular ring image using procedural anti-aliased ring sprite
            int textureSize = 256;
            int outerR = 120;
            int innerR = 100;
            Sprite ringSprite = GenerateProceduralRingSprite(textureSize, innerR, outerR, new Color(0.24f, 0.34f, 0.46f, 1f));

            GameObject ringVisualGo = new GameObject("RingVisual", typeof(RectTransform), typeof(Image));
            ringVisualGo.transform.SetParent(ringGo.transform, false);
            RectTransform rvr = ringVisualGo.GetComponent<RectTransform>();
            rvr.anchorMin = Vector2.zero;
            rvr.anchorMax = Vector2.one;
            rvr.offsetMin = Vector2.zero;
            rvr.offsetMax = Vector2.zero;
            Image ringImg = ringVisualGo.GetComponent<Image>();
            ringImg.sprite = ringSprite;
            ringImg.color = Color.white;
            ringImg.raycastTarget = false;
            ring.ringImage = ringImg;

            // 12 Radial Ticks around rim for tactical feedback
            for (int t = 0; t < 12; t++)
            {
                float tickAngle = t * 30f;
                float rad = tickAngle * Mathf.Deg2Rad;
                float tx = Mathf.Sin(rad) * (notchRadius - 2f);
                float ty = Mathf.Cos(rad) * (notchRadius - 2f);

                GameObject tickGo = new GameObject($"Tick_{t}", typeof(RectTransform), typeof(Image));
                tickGo.transform.SetParent(ringGo.transform, false);
                RectTransform tr = tickGo.GetComponent<RectTransform>();
                tr.sizeDelta = new Vector2(3, 10);
                tr.anchoredPosition = new Vector2(tx, ty);
                tr.localEulerAngles = new Vector3(0, 0, -tickAngle);
                Image tImg = tickGo.GetComponent<Image>();
                tImg.color = new Color(0.40f, 0.52f, 0.65f, 0.75f);
                tImg.raycastTarget = false;
            }

            // Key Optical Prism Notch (at top 0° relative to ring)
            GameObject notchGo = new GameObject("OpticNotch", typeof(RectTransform), typeof(Image));
            notchGo.transform.SetParent(ringGo.transform, false);
            RectTransform nr = notchGo.GetComponent<RectTransform>();
            nr.sizeDelta = new Vector2(16, 22);
            nr.anchoredPosition = new Vector2(0, notchRadius);
            Image nImg = notchGo.GetComponent<Image>();
            nImg.color = new Color(0.40f, 0.50f, 0.62f, 1f);
            ring.notchImage = nImg;

            Outline nOutline = notchGo.AddComponent<Outline>();
            nOutline.effectColor = Color.black;
            nOutline.effectDistance = new Vector2(1, -1);

            // Notch Glow
            GameObject glowGo = new GameObject("NotchGlow", typeof(RectTransform), typeof(Image));
            glowGo.transform.SetParent(notchGo.transform, false);
            RectTransform gr = glowGo.GetComponent<RectTransform>();
            gr.sizeDelta = new Vector2(28, 34);
            gr.anchoredPosition = Vector2.zero;
            Image gImg = glowGo.GetComponent<Image>();
            gImg.color = Color.clear;
            gImg.raycastTarget = false;
            ring.notchGlow = gImg;

            // Active Highlight Ring
            Sprite selSprite = GenerateProceduralRingSprite(textureSize, outerR + 2, outerR + 6, new Color(0f, 0.94f, 1f, 1f));
            GameObject selVisualGo = new GameObject("SelectionVisual", typeof(RectTransform), typeof(Image));
            selVisualGo.transform.SetParent(ringGo.transform, false);
            RectTransform svr = selVisualGo.GetComponent<RectTransform>();
            svr.anchorMin = Vector2.zero;
            svr.anchorMax = Vector2.one;
            svr.offsetMin = new Vector2(-4, -4);
            svr.offsetMax = new Vector2(4, 4);
            Image selImg = selVisualGo.GetComponent<Image>();
            selImg.sprite = selSprite;
            selImg.color = Color.white;
            selImg.raycastTarget = false;
            selVisualGo.SetActive(false);
            ring.activeHighlightRing = selImg;

            return ring;
        }

        private static Sprite GenerateProceduralRingSprite(int size, int innerRadius, int outerRadius, Color ringColor)
        {
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
            Color[] colors = new Color[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), center);
                    if (dist >= innerRadius && dist <= outerRadius)
                    {
                        // Anti-aliased smooth edges
                        float edgeInner = Mathf.Clamp01((dist - innerRadius) / 1.5f);
                        float edgeOuter = Mathf.Clamp01((outerRadius - dist) / 1.5f);
                        float alpha = Mathf.Min(edgeInner, edgeOuter);
                        colors[y * size + x] = new Color(ringColor.r, ringColor.g, ringColor.b, ringColor.a * alpha);
                    }
                    else
                    {
                        colors[y * size + x] = Color.clear;
                    }
                }
            }

            tex.SetPixels(colors);
            tex.Apply();

            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        #endregion
    }
}
