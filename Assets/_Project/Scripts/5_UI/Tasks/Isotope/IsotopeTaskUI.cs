using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using TMPro;
using TheLastSignal.Entities.Player;
using TheLastSignal.Entities.Environment;

namespace TheLastSignal.UI.Tasks
{
    /// <summary>
    /// Sci-Fi Brutalist Reactor Isotope Decryption Minigame (Mastermind deduction).
    /// Strategic time-stalling puzzle featuring:
    /// - 4-slot radioactive isotope sequence (α, β, γ, Ω, Δ, Σ)
    /// - 5 attempts maximum with real-time diagnostic sensor feedback pins:
    ///   * Green Pin: Correct isotope in exact correct position
    ///   * Yellow Pin: Correct isotope present in wrong position
    ///   * Dark Pin: Isotope not present in sequence
    /// - Dual-Bay Brutalist UI chassis matching TerminalBezel standards
    /// - Core meltdown / purge lockout penalty (2.5s) on exhausting 5 attempts
    /// - Full mouse and New Input System keyboard navigation (1-6, Backspace, Enter, Esc)
    /// - Completely self-contained procedural PCM audio synthesis
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class IsotopeTaskUI : MonoBehaviour
    {
        private static IsotopeTaskUI _instance;
        public static IsotopeTaskUI Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = UnityEngine.Object.FindAnyObjectByType<IsotopeTaskUI>(FindObjectsInactive.Include);
                }
                return _instance;
            }
            private set => _instance = value;
        }

        #region Constants & Structs

        public const int CodeLength = 4;
        public const int MaxAttempts = 5;
        public const int IsotopePoolSize = 6;

        public static readonly string[] IsotopeSymbols = { "α", "β", "γ", "Ω", "Δ", "Σ" };
        public static readonly string[] IsotopeNames   = { "ALPHA", "BETA", "GAMMA", "OMEGA", "DELTA", "SIGMA" };
        public static readonly Color[] IsotopeColors  = 
        {
            new Color(0f, 1f, 0.5f, 1f),       // Alpha: Emerald Green
            new Color(0f, 0.94f, 1f, 1f),      // Beta: Sky Cyan
            new Color(0.75f, 0.25f, 1f, 1f),   // Gamma: Electric Violet
            new Color(1f, 0.72f, 0f, 1f),      // Omega: Nuclear Amber
            new Color(1f, 0.25f, 0.25f, 1f),   // Delta: Hazard Red
            new Color(0.25f, 0.55f, 1f, 1f)    // Sigma: Cobalt Blue
        };

        public class AttemptRow
        {
            public GameObject rowRoot;
            public TextMeshProUGUI rowLabel;
            public SlotUI[] slots = new SlotUI[CodeLength];
            public Image[] pinImages = new Image[CodeLength];
            public int[] chosenIsotopes = new int[CodeLength]; // -1 = empty
            public bool isEvaluated = false;
        }

        public class SlotUI
        {
            public GameObject rootGo;
            public Image bgImage;
            public Outline outline;
            public TextMeshProUGUI glyphText;
            public Image glowImage;
        }

        #endregion

        #region Serialized UI References

        [Header("UI Panels")]
        [SerializeField] private GameObject rootPanel;
        [SerializeField] private RectTransform terminalBox;
        [SerializeField] private TextMeshProUGUI headerText;
        [SerializeField] private TextMeshProUGUI instructionText;
        [SerializeField] private Button closeButton;

        [Header("Matrix Bay")]
        [SerializeField] private RectTransform matrixBay;
        [SerializeField] private Button[] isotopeButtons = new Button[IsotopePoolSize];
        [SerializeField] private Button clearButton;
        [SerializeField] private Button submitButton;
        [SerializeField] private TextMeshProUGUI submitButtonText;

        [Header("Telemetry Bay")]
        [SerializeField] private TextMeshProUGUI containmentStateText;
        [SerializeField] private TextMeshProUGUI radiationFluxText;
        [SerializeField] private TextMeshProUGUI attemptsLeftText;
        [SerializeField] private TextMeshProUGUI accuracyText;
        [SerializeField] private TextMeshProUGUI coreTempText;
        [SerializeField] private Image coreTempFill;

        [Header("Audio")]
        [SerializeField] private AudioSource audioSource;

        #endregion

        #region Internal State

        private AttemptRow[] rows = new AttemptRow[MaxAttempts];
        private int currentAttemptIndex = 0;
        private int currentSlotIndex = 0;
        private int[] secretCode = new int[CodeLength];

        private PlayerController activePlayer;
        private TaskLocation activeTaskLocation;

        private bool isFinished = false;
        private bool isLockoutPurging = false;
        private float purgeCooldownTimer = 0f;
        private const float PurgeCooldownDuration = 2.5f;

        private float fluxJitterTimer = 0f;
        private float displayedFlux = 0.048f;

        // Procedural Audio Clips
        private AudioClip keyInsertClip;
        private AudioClip pinPingClip;
        private AudioClip errorBuzzClip;
        private AudioClip purgeAlarmClip;
        private AudioClip successChimeClip;

        // Visual Colors
        private readonly Color ColorSlotEmptyBg   = new Color(0.08f, 0.12f, 0.17f, 1f);
        private readonly Color ColorSlotActiveBorder= new Color(0f, 0.94f, 1f, 1f);
        private readonly Color ColorSlotInactiveBorder= new Color(0.18f, 0.26f, 0.36f, 1f);

        private readonly Color ColorPinUnlit      = new Color(0.12f, 0.16f, 0.22f, 1f);
        private readonly Color ColorPinGreen      = new Color(0f, 1f, 0.5f, 1f);
        private readonly Color ColorPinYellow     = new Color(1f, 0.84f, 0f, 1f);
        private readonly Color ColorPinDark       = new Color(0.22f, 0.14f, 0.16f, 1f);

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
            if (clearButton != null)
            {
                clearButton.onClick.RemoveAllListeners();
                clearButton.onClick.AddListener(ClearCurrentSlot);
            }
            if (submitButton != null)
            {
                submitButton.onClick.RemoveAllListeners();
                submitButton.onClick.AddListener(SubmitAttempt);
            }

            if (isotopeButtons != null)
            {
                for (int i = 0; i < IsotopePoolSize; i++)
                {
                    int isoIdx = i;
                    if (i < isotopeButtons.Length && isotopeButtons[i] != null)
                    {
                        isotopeButtons[i].onClick.RemoveAllListeners();
                        isotopeButtons[i].onClick.AddListener(() => OnIsotopeButtonClicked(isoIdx));
                    }
                }
            }
        }

        private void Update()
        {
            if (!IsOpen || isFinished) return;

            // Handle Core Purge Lockout Countdown
            if (isLockoutPurging)
            {
                purgeCooldownTimer -= Time.deltaTime;
                if (coreTempFill != null)
                {
                    coreTempFill.fillAmount = Mathf.Clamp01(purgeCooldownTimer / PurgeCooldownDuration);
                }
                if (coreTempText != null)
                {
                    coreTempText.text = $"CORE PURGING // COOLING: {Mathf.Max(0f, purgeCooldownTimer):F1}s";
                }

                if (purgeCooldownTimer <= 0f)
                {
                    isLockoutPurging = false;
                    purgeCooldownTimer = 0f;
                    ResetPuzzleForRetry();
                }

                if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                {
                    CloseMinigame();
                }
                return;
            }

            // Radiation Flux Jitter for Immersion
            fluxJitterTimer += Time.deltaTime;
            if (fluxJitterTimer >= 0.15f)
            {
                fluxJitterTimer = 0f;
                float jitter = UnityEngine.Random.Range(-0.004f, 0.005f);
                displayedFlux = Mathf.Max(0.012f, 0.048f + jitter);
                if (radiationFluxText != null)
                {
                    radiationFluxText.text = $"{displayedFlux:F3} Sv/h (NOMINAL)";
                }
            }

            HandleKeyboardInput();
        }

        #endregion

        #region Interaction & Controls (New Input System)

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

            // Isotope hotkeys 1-6
            if (kb.digit1Key.wasPressedThisFrame || kb.numpad1Key.wasPressedThisFrame) InsertIsotope(0);
            else if (kb.digit2Key.wasPressedThisFrame || kb.numpad2Key.wasPressedThisFrame) InsertIsotope(1);
            else if (kb.digit3Key.wasPressedThisFrame || kb.numpad3Key.wasPressedThisFrame) InsertIsotope(2);
            else if (kb.digit4Key.wasPressedThisFrame || kb.numpad4Key.wasPressedThisFrame) InsertIsotope(3);
            else if (kb.digit5Key.wasPressedThisFrame || kb.numpad5Key.wasPressedThisFrame) InsertIsotope(4);
            else if (kb.digit6Key.wasPressedThisFrame || kb.numpad6Key.wasPressedThisFrame) InsertIsotope(5);

            // Backspace / Delete
            if (kb.backspaceKey.wasPressedThisFrame || kb.deleteKey.wasPressedThisFrame)
            {
                ClearCurrentSlot();
            }

            // Enter / Space to Submit
            if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame)
            {
                SubmitAttempt();
            }
        }

        public void OnIsotopeButtonClicked(int isoIndex)
        {
            if (isFinished || isLockoutPurging) return;
            InsertIsotope(isoIndex);
        }

        private void InsertIsotope(int isoIndex)
        {
            if (currentAttemptIndex >= MaxAttempts) return;
            AttemptRow row = rows[currentAttemptIndex];

            // If current row already has 4 isotopes, replace the last one or wrap
            if (currentSlotIndex >= CodeLength)
            {
                currentSlotIndex = CodeLength - 1;
            }

            row.chosenIsotopes[currentSlotIndex] = isoIndex;
            UpdateSlotVisual(row.slots[currentSlotIndex], isoIndex);

            PlayKeyInsert(0.5f);

            // Advance slot index if not at the end
            if (currentSlotIndex < CodeLength - 1)
            {
                currentSlotIndex++;
            }

            UpdateActiveSlotOutlines();
            UpdateSubmitButtonState();
        }

        public void ClearCurrentSlot()
        {
            if (isFinished || isLockoutPurging || currentAttemptIndex >= MaxAttempts) return;
            AttemptRow row = rows[currentAttemptIndex];

            // If current slot is filled, clear it; otherwise step back and clear
            if (row.chosenIsotopes[currentSlotIndex] != -1)
            {
                row.chosenIsotopes[currentSlotIndex] = -1;
                UpdateSlotVisual(row.slots[currentSlotIndex], -1);
            }
            else if (currentSlotIndex > 0)
            {
                currentSlotIndex--;
                row.chosenIsotopes[currentSlotIndex] = -1;
                UpdateSlotVisual(row.slots[currentSlotIndex], -1);
            }

            PlayKeyInsert(0.35f);
            UpdateActiveSlotOutlines();
            UpdateSubmitButtonState();
        }

        private void UpdateSubmitButtonState()
        {
            if (submitButtonText == null) return;
            if (currentAttemptIndex >= MaxAttempts) return;

            AttemptRow row = rows[currentAttemptIndex];
            bool full = true;
            for (int i = 0; i < CodeLength; i++)
            {
                if (row.chosenIsotopes[i] == -1) { full = false; break; }
            }

            submitButtonText.text = full 
                ? "<b>[TRANSMIT SEQUENCE / ENTER]</b>" 
                : $"<b>[SELECT {CountEmptySlots(row)} MORE ISOTOPES]</b>";
        }

        private int CountEmptySlots(AttemptRow row)
        {
            int c = 0;
            for (int i = 0; i < CodeLength; i++) if (row.chosenIsotopes[i] == -1) c++;
            return c;
        }

        #endregion

        #region Mastermind Decryption Logic

        public void SubmitAttempt()
        {
            if (isFinished || isLockoutPurging || currentAttemptIndex >= MaxAttempts) return;
            AttemptRow row = rows[currentAttemptIndex];

            // Check if all 4 slots are filled
            for (int i = 0; i < CodeLength; i++)
            {
                if (row.chosenIsotopes[i] == -1)
                {
                    PlayErrorBuzz();
                    StartCoroutine(FlashIncompleteRow(row));
                    return;
                }
            }

            // Start diagnostic evaluation animation
            StartCoroutine(EvaluateAttemptRoutine(row));
        }

        private IEnumerator EvaluateAttemptRoutine(AttemptRow row)
        {
            row.isEvaluated = true;

            // Direct 1-to-1 Positional Sensor Feedback!
            // Pin[i] corresponds directly to Slot[i] for crystal-clear player deduction.
            Color[] slotPinColors = new Color[CodeLength];
            int greenCount = 0;

            for (int i = 0; i < CodeLength; i++)
            {
                if (row.chosenIsotopes[i] == secretCode[i])
                {
                    slotPinColors[i] = ColorPinGreen;
                    greenCount++;
                }
                else
                {
                    // Check if this isotope is in the secret code at another position
                    bool existsElsewhere = false;
                    for (int j = 0; j < CodeLength; j++)
                    {
                        if (i != j && row.chosenIsotopes[i] == secretCode[j])
                        {
                            existsElsewhere = true;
                            break;
                        }
                    }

                    if (existsElsewhere)
                    {
                        slotPinColors[i] = ColorPinYellow; // Gần đúng (Đúng ký hiệu nhưng sai vị trí)!
                    }
                    else
                    {
                        slotPinColors[i] = ColorPinDark;   // Không có trong mật mã!
                    }
                }
            }

            // Light up diagnostic pins one by one with tactile sound
            for (int i = 0; i < CodeLength; i++)
            {
                yield return new WaitForSeconds(0.18f);
                if (row.pinImages[i] != null)
                {
                    row.pinImages[i].color = slotPinColors[i];
                    PlayDiagnosticPing(i);
                }
                // Tint the slot's outline to visually match its diagnostic pin!
                if (row.slots[i].outline != null)
                {
                    if (slotPinColors[i] == ColorPinGreen) row.slots[i].outline.effectColor = ColorPinGreen;
                    else if (slotPinColors[i] == ColorPinYellow) row.slots[i].outline.effectColor = ColorPinYellow;
                    else row.slots[i].outline.effectColor = new Color(0.18f, 0.22f, 0.28f, 1f);
                }
            }

            yield return new WaitForSeconds(0.2f);

            // Update Telemetry
            int totalAttemptsUsed = currentAttemptIndex + 1;
            if (attemptsLeftText != null)
            {
                attemptsLeftText.text = $"{MaxAttempts - totalAttemptsUsed} / {MaxAttempts}";
            }
            if (accuracyText != null)
            {
                int acc = (greenCount * 25);
                accuracyText.text = $"{acc}% MATCH";
            }

            // Check Win Condition: 4 Green Pins
            if (greenCount == CodeLength)
            {
                TriggerDecryptionSuccess();
                yield break;
            }

            // Check Loss Condition: Max attempts reached
            if (currentAttemptIndex >= MaxAttempts - 1)
            {
                TriggerPurgeFailure();
                yield break;
            }

            // Advance to next attempt row
            currentAttemptIndex++;
            currentSlotIndex = 0;
            UpdateActiveSlotOutlines();
            UpdateSubmitButtonState();
        }

        private IEnumerator FlashIncompleteRow(AttemptRow row)
        {
            for (int f = 0; f < 3; f++)
            {
                for (int i = 0; i < CodeLength; i++)
                {
                    if (row.slots[i].outline != null)
                    {
                        row.slots[i].outline.effectColor = Color.red;
                    }
                }
                yield return new WaitForSeconds(0.08f);
                UpdateActiveSlotOutlines();
                yield return new WaitForSeconds(0.08f);
            }
        }

        private void TriggerDecryptionSuccess()
        {
            isFinished = true;
            PlaySuccessChime();

            if (containmentStateText != null)
            {
                containmentStateText.text = "<color=#00FF7F>DECRYPTED // CORE STABILIZED</color>";
            }
            if (accuracyText != null)
            {
                accuracyText.text = "100% MAXIMUM";
            }
            if (coreTempText != null)
            {
                coreTempText.text = "CORE TEMPERATURE NOMINAL (284 K)";
            }
            if (coreTempFill != null)
            {
                coreTempFill.color = ColorPinGreen;
            }

            StartCoroutine(VictoryRoutine());
        }

        private IEnumerator VictoryRoutine()
        {
            // Flash current row with gold pulse
            AttemptRow winRow = rows[currentAttemptIndex];
            for (int step = 0; step < 3; step++)
            {
                for (int i = 0; i < CodeLength; i++)
                {
                    if (winRow.slots[i].bgImage != null) winRow.slots[i].bgImage.color = new Color(0.2f, 0.45f, 0.25f, 1f);
                }
                yield return new WaitForSeconds(0.12f);
                for (int i = 0; i < CodeLength; i++)
                {
                    if (winRow.slots[i].bgImage != null) winRow.slots[i].bgImage.color = ColorSlotEmptyBg;
                }
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

        private void TriggerPurgeFailure()
        {
            isLockoutPurging = true;
            purgeCooldownTimer = PurgeCooldownDuration;

            PlayPurgeAlarm();

            if (containmentStateText != null)
            {
                containmentStateText.text = "<color=#FF3333>CONTAINMENT PURGE // REBOOTING</color>";
            }
            if (coreTempText != null)
            {
                coreTempText.text = "OVERHEAT DETECTED // FLUSHING COOLANT";
            }
            if (coreTempFill != null)
            {
                coreTempFill.color = Color.red;
                coreTempFill.fillAmount = 1f;
            }
        }

        private void ResetPuzzleForRetry()
        {
            // Re-generate secret code and reset attempts
            int seed = (int)(Time.time * 100f);
            GenerateSecretCode(seed);

            currentAttemptIndex = 0;
            currentSlotIndex = 0;

            for (int r = 0; r < MaxAttempts; r++)
            {
                AttemptRow row = rows[r];
                row.isEvaluated = false;
                for (int c = 0; c < CodeLength; c++)
                {
                    row.chosenIsotopes[c] = -1;
                    UpdateSlotVisual(row.slots[c], -1);
                    if (row.pinImages[c] != null) row.pinImages[c].color = ColorPinUnlit;
                }
            }

            if (containmentStateText != null)
            {
                containmentStateText.text = "<color=#00F0FF>AWAITING ISOTOPE SEQUENCE</color>";
            }
            if (attemptsLeftText != null)
            {
                attemptsLeftText.text = $"{MaxAttempts} / {MaxAttempts}";
            }
            if (accuracyText != null)
            {
                accuracyText.text = "0% NOMINAL";
            }
            if (coreTempText != null)
            {
                coreTempText.text = "CORE TEMPERATURE NORMAL (312 K)";
            }
            if (coreTempFill != null)
            {
                coreTempFill.color = ColorPinGreen;
                coreTempFill.fillAmount = 1f;
            }

            UpdateActiveSlotOutlines();
            UpdateSubmitButtonState();
        }

        #endregion

        #region Visual Helpers

        private void UpdateSlotVisual(SlotUI slot, int isotopeIndex)
        {
            if (slot == null) return;

            if (isotopeIndex >= 0 && isotopeIndex < IsotopePoolSize)
            {
                slot.glyphText.text = IsotopeSymbols[isotopeIndex];
                slot.glyphText.color = IsotopeColors[isotopeIndex];
                slot.bgImage.color = new Color(IsotopeColors[isotopeIndex].r * 0.15f, 
                                               IsotopeColors[isotopeIndex].g * 0.15f, 
                                               IsotopeColors[isotopeIndex].b * 0.15f, 1f);
                if (slot.glowImage != null)
                {
                    slot.glowImage.color = new Color(IsotopeColors[isotopeIndex].r, 
                                                     IsotopeColors[isotopeIndex].g, 
                                                     IsotopeColors[isotopeIndex].b, 0.25f);
                }
            }
            else
            {
                slot.glyphText.text = "";
                slot.bgImage.color = ColorSlotEmptyBg;
                if (slot.glowImage != null)
                {
                    slot.glowImage.color = Color.clear;
                }
            }
        }

        private void UpdateActiveSlotOutlines()
        {
            for (int r = 0; r < MaxAttempts; r++)
            {
                AttemptRow row = rows[r];
                bool isCurrentRow = (r == currentAttemptIndex && !isFinished && !isLockoutPurging);

                if (row.rowLabel != null)
                {
                    row.rowLabel.color = isCurrentRow ? new Color(0f, 0.94f, 1f, 1f) : new Color(0.4f, 0.5f, 0.62f, 1f);
                }

                // If row is already evaluated, preserve its diagnostic green/yellow/dark feedback outlines
                if (row.isEvaluated) continue;

                for (int c = 0; c < CodeLength; c++)
                {
                    if (row.slots[c].outline != null)
                    {
                        bool isActiveSlot = isCurrentRow && (c == currentSlotIndex);
                        row.slots[c].outline.effectColor = isActiveSlot 
                            ? ColorSlotActiveBorder 
                            : (isCurrentRow ? new Color(0.25f, 0.40f, 0.55f, 1f) : ColorSlotInactiveBorder);
                    }
                }
            }
        }

        #endregion

        #region Secret Code Generator

        private void GenerateSecretCode(int seed)
        {
            System.Random rng = new System.Random(seed);
            List<int> pool = new List<int> { 0, 1, 2, 3, 4, 5 };
            for (int i = 0; i < CodeLength; i++)
            {
                int idx = rng.Next(pool.Count);
                secretCode[i] = pool[idx];
                pool.RemoveAt(idx);
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
            isLockoutPurging = false;
            purgeCooldownTimer = 0f;

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
                headerText.text = $"REACTOR ISOTOPE DECRYPTION // {taskLocation.StationDisplayName.ToUpper()} [STAGE {currentStep}/{targetSteps}]";
            }

            // Generate secret code
            int seed = 42;
            if (taskLocation != null)
            {
                seed = taskLocation.StationId.GetHashCode() + (int)(Time.time * 10f);
            }
            GenerateSecretCode(seed);

            // Reset Board State
            currentAttemptIndex = 0;
            currentSlotIndex = 0;

            for (int r = 0; r < MaxAttempts; r++)
            {
                AttemptRow row = rows[r];
                row.isEvaluated = false;
                for (int c = 0; c < CodeLength; c++)
                {
                    row.chosenIsotopes[c] = -1;
                    UpdateSlotVisual(row.slots[c], -1);
                    if (row.pinImages[c] != null) row.pinImages[c].color = ColorPinUnlit;
                }
            }

            if (containmentStateText != null)
            {
                containmentStateText.text = "<color=#00F0FF>AWAITING ISOTOPE SEQUENCE</color>";
            }
            if (attemptsLeftText != null)
            {
                attemptsLeftText.text = $"{MaxAttempts} / {MaxAttempts}";
            }
            if (accuracyText != null)
            {
                accuracyText.text = "0% NOMINAL";
            }
            if (coreTempText != null)
            {
                coreTempText.text = "CORE TEMPERATURE NORMAL (312 K)";
            }
            if (coreTempFill != null)
            {
                coreTempFill.color = ColorPinGreen;
                coreTempFill.fillAmount = 1f;
            }

            UpdateActiveSlotOutlines();
            UpdateSubmitButtonState();
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
            if (keyInsertClip == null) keyInsertClip = CreateKeyInsertClip();
            if (pinPingClip == null) pinPingClip = CreatePinPingClip();
            if (errorBuzzClip == null) errorBuzzClip = CreateErrorBuzzClip();
            if (purgeAlarmClip == null) purgeAlarmClip = CreatePurgeAlarmClip();
            if (successChimeClip == null) successChimeClip = CreateSuccessChimeClip();
        }

        private void PlayKeyInsert(float volume = 0.45f)
        {
            if (audioSource != null && keyInsertClip != null)
            {
                audioSource.PlayOneShot(keyInsertClip, volume);
            }
        }

        private void PlayDiagnosticPing(int pinIndex)
        {
            if (audioSource != null && pinPingClip != null)
            {
                float pitch = 1.0f + (pinIndex * 0.12f);
                audioSource.pitch = pitch;
                audioSource.PlayOneShot(pinPingClip, 0.55f);
                audioSource.pitch = 1.0f;
            }
        }

        private void PlayErrorBuzz()
        {
            if (audioSource != null && errorBuzzClip != null)
            {
                audioSource.PlayOneShot(errorBuzzClip, 0.7f);
            }
        }

        private void PlayPurgeAlarm()
        {
            if (audioSource != null && purgeAlarmClip != null)
            {
                audioSource.PlayOneShot(purgeAlarmClip, 0.85f);
            }
        }

        private void PlaySuccessChime()
        {
            if (audioSource != null && successChimeClip != null)
            {
                audioSource.PlayOneShot(successChimeClip, 0.9f);
            }
        }

        private static AudioClip CreateKeyInsertClip()
        {
            int sampleRate = 44100;
            float duration = 0.04f;
            int samples = (int)(sampleRate * duration);
            float[] data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / sampleRate;
                float env = 1f - (t / duration);
                data[i] = Mathf.Sin(2f * Mathf.PI * 1600f * t) * env * 0.45f;
            }
            AudioClip clip = AudioClip.Create("ISO_KeyInsert", samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip CreatePinPingClip()
        {
            int sampleRate = 44100;
            float duration = 0.08f;
            int samples = (int)(sampleRate * duration);
            float[] data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / sampleRate;
                float env = 1f - Mathf.Pow(t / duration, 0.6f);
                data[i] = Mathf.Sin(2f * Mathf.PI * 880f * t) * env * 0.5f;
            }
            AudioClip clip = AudioClip.Create("ISO_PinPing", samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip CreateErrorBuzzClip()
        {
            int sampleRate = 44100;
            float duration = 0.16f;
            int samples = (int)(sampleRate * duration);
            float[] data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / sampleRate;
                float env = 1f - (t / duration);
                float square = Mathf.Sign(Mathf.Sin(2f * Mathf.PI * 130f * t));
                data[i] = square * env * 0.45f;
            }
            AudioClip clip = AudioClip.Create("ISO_ErrorBuzz", samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip CreatePurgeAlarmClip()
        {
            int sampleRate = 44100;
            float duration = 0.5f;
            int samples = (int)(sampleRate * duration);
            float[] data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / sampleRate;
                float freq = Mathf.Lerp(600f, 220f, t / duration);
                float env = 1f - (t / duration);
                float wave = Mathf.Sign(Mathf.Sin(2f * Mathf.PI * freq * t)) * 0.4f;
                data[i] = wave * env;
            }
            AudioClip clip = AudioClip.Create("ISO_PurgeAlarm", samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip CreateSuccessChimeClip()
        {
            int sampleRate = 44100;
            float duration = 0.8f;
            int samples = (int)(sampleRate * duration);
            float[] data = new float[samples];
            float[] freqs = { 329.63f, 415.30f, 493.88f, 659.25f }; // E Major chord

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / sampleRate;
                float env = 1f - Mathf.Pow(t / duration, 0.8f);
                float sum = 0f;
                for (int f = 0; f < freqs.Length; f++)
                {
                    float noteStart = f * 0.07f;
                    if (t >= noteStart)
                    {
                        float localT = t - noteStart;
                        sum += Mathf.Sin(2f * Mathf.PI * freqs[f] * localT) * 0.22f;
                    }
                }
                data[i] = sum * env;
            }
            AudioClip clip = AudioClip.Create("ISO_SuccessChime", samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        #endregion

        #region Runtime UI Hierarchy Factory

        public static IsotopeTaskUI GetOrCreateInstance()
        {
            if (Instance != null) return Instance;

            // Ensure EventSystem with InputSystemUIInputModule exists in scene
            if (UnityEngine.EventSystems.EventSystem.current == null)
            {
                GameObject esGo = new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem), typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
            }

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
                Debug.LogWarning("[IsotopeTaskUI] No Canvas found in scene.");
                return null;
            }

            _instance = CreateIsotopeUIHierarchy(canvas);
            return _instance;
        }

        public static IsotopeTaskUI CreateIsotopeUIHierarchy(Canvas canvas)
        {
            Sprite chassisSprite = Resources.Load<Sprite>("UI/Tasks/TerminalBezel");

            // Fullscreen Dark Vignette Overlay
            GameObject root = new GameObject("IsotopeMinigamePanel", typeof(RectTransform), typeof(AudioSource));
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

            // Center Terminal Box (Enlarged to 1120 x 720 for optimal readability)
            GameObject box = new GameObject("TerminalBox", typeof(RectTransform), typeof(Image));
            box.transform.SetParent(root.transform, false);
            RectTransform boxRect = box.GetComponent<RectTransform>();
            boxRect.sizeDelta = new Vector2(1120, 720);
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

            // Header Title (Enlarged font 24)
            GameObject headerGo = new GameObject("HeaderTitle", typeof(RectTransform), typeof(TextMeshProUGUI));
            headerGo.transform.SetParent(box.transform, false);
            RectTransform headerRect = headerGo.GetComponent<RectTransform>();
            headerRect.anchorMin = new Vector2(0f, 1f);
            headerRect.anchorMax = new Vector2(1f, 1f);
            headerRect.pivot = new Vector2(0.5f, 1f);
            headerRect.anchoredPosition = new Vector2(0, -30);
            headerRect.sizeDelta = new Vector2(-140, 42);
            TextMeshProUGUI headerText = headerGo.GetComponent<TextMeshProUGUI>();
            headerText.text = "REACTOR ISOTOPE DECRYPTION // MK-VI";
            headerText.fontSize = 24;
            headerText.alignment = TextAlignmentOptions.Center;
            headerText.fontStyle = FontStyles.Bold;
            headerText.color = new Color(0.92f, 0.96f, 1f, 1f);

            // Instruction Subtext (Enlarged font 16.5)
            GameObject instGo = new GameObject("InstructionText", typeof(RectTransform), typeof(TextMeshProUGUI));
            instGo.transform.SetParent(box.transform, false);
            RectTransform instRect = instGo.GetComponent<RectTransform>();
            instRect.anchorMin = new Vector2(0f, 1f);
            instRect.anchorMax = new Vector2(1f, 1f);
            instRect.pivot = new Vector2(0.5f, 1f);
            instRect.anchoredPosition = new Vector2(0, -72);
            instRect.sizeDelta = new Vector2(-140, 30);
            TextMeshProUGUI instText = instGo.GetComponent<TextMeshProUGUI>();
            instText.text = "Deduce the 4-isotope sequence using radiation sensor pins. <color=#00FF7F>[5 ATTEMPTS MAX]</color>";
            instText.fontSize = 16.5f;
            instText.alignment = TextAlignmentOptions.Center;
            instText.color = new Color(0.72f, 0.82f, 0.92f, 1f);

            // Close Button [ESC / X] (Enlarged to 44x44, raycastable image, non-blocking text)
            GameObject closeGo = new GameObject("CloseButton", typeof(RectTransform), typeof(Image), typeof(Button));
            closeGo.transform.SetParent(box.transform, false);
            RectTransform closeRect = closeGo.GetComponent<RectTransform>();
            closeRect.anchorMin = new Vector2(1f, 1f);
            closeRect.anchorMax = new Vector2(1f, 1f);
            closeRect.pivot = new Vector2(1f, 1f);
            closeRect.anchoredPosition = new Vector2(-26, -26);
            closeRect.sizeDelta = new Vector2(44, 44);
            Image closeImg = closeGo.GetComponent<Image>();
            closeImg.color = new Color(0.78f, 0.15f, 0.15f, 1f);
            closeImg.raycastTarget = true;
            Button closeBtn = closeGo.GetComponent<Button>();
            closeBtn.targetGraphic = closeImg;
            closeBtn.navigation = new Navigation { mode = Navigation.Mode.None };
            ColorBlock cColors = closeBtn.colors;
            cColors.normalColor = Color.white;
            cColors.highlightedColor = new Color(1.3f, 0.85f, 0.85f, 1f);
            cColors.pressedColor = new Color(0.6f, 0.2f, 0.2f, 1f);
            closeBtn.colors = cColors;

            GameObject closeTextGo = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            closeTextGo.transform.SetParent(closeGo.transform, false);
            TextMeshProUGUI closeTxt = closeTextGo.GetComponent<TextMeshProUGUI>();
            closeTxt.text = "X";
            closeTxt.fontSize = 28;
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
            contentRect.offsetMin = new Vector2(26, 22);
            contentRect.offsetMax = new Vector2(-26, -110);

            // ------------------------------------------
            // BAY 1 (LEFT): Isotope Matrix Bay (634 x 588)
            // ------------------------------------------
            GameObject matrixBayGo = new GameObject("MatrixBay", typeof(RectTransform), typeof(Image));
            matrixBayGo.transform.SetParent(contentGo.transform, false);
            RectTransform matrixBayRect = matrixBayGo.GetComponent<RectTransform>();
            matrixBayRect.anchorMin = new Vector2(0f, 0f);
            matrixBayRect.anchorMax = new Vector2(0f, 1f);
            matrixBayRect.pivot = new Vector2(0f, 0.5f);
            matrixBayRect.anchoredPosition = new Vector2(0, 0);
            matrixBayRect.sizeDelta = new Vector2(634, 0);
            Image matrixBayImg = matrixBayGo.GetComponent<Image>();
            matrixBayImg.color = new Color(0.045f, 0.07f, 0.10f, 0.95f);
            Outline mBayOutline = matrixBayGo.AddComponent<Outline>();
            mBayOutline.effectColor = new Color(0.18f, 0.28f, 0.38f, 1f);
            mBayOutline.effectDistance = new Vector2(1, -1);

            // Bay Title
            GameObject mBayTitleGo = new GameObject("BayTitle", typeof(RectTransform), typeof(TextMeshProUGUI));
            mBayTitleGo.transform.SetParent(matrixBayGo.transform, false);
            RectTransform mbtRect = mBayTitleGo.GetComponent<RectTransform>();
            mbtRect.anchorMin = new Vector2(0f, 1f);
            mbtRect.anchorMax = new Vector2(1f, 1f);
            mbtRect.pivot = new Vector2(0.5f, 1f);
            mbtRect.anchoredPosition = new Vector2(0, -12);
            mbtRect.sizeDelta = new Vector2(-20, 26);
            TextMeshProUGUI mbtText = mBayTitleGo.GetComponent<TextMeshProUGUI>();
            mbtText.text = "[ DECRYPTION MATRIX // 5 ATTEMPTS ]";
            mbtText.fontSize = 15.5f;
            mbtText.fontStyle = FontStyles.Bold;
            mbtText.alignment = TextAlignmentOptions.Center;
            mbtText.color = new Color(0.45f, 0.75f, 0.95f, 1f);

            // Rows Container (5 rows of 4 slots)
            GameObject rowsContGo = new GameObject("RowsContainer", typeof(RectTransform));
            rowsContGo.transform.SetParent(matrixBayGo.transform, false);
            RectTransform rowsContRect = rowsContGo.GetComponent<RectTransform>();
            rowsContRect.anchorMin = new Vector2(0f, 1f);
            rowsContRect.anchorMax = new Vector2(1f, 1f);
            rowsContRect.pivot = new Vector2(0.5f, 1f);
            rowsContRect.anchoredPosition = new Vector2(0, -36);
            rowsContRect.sizeDelta = new Vector2(-20, 370);

            // ------------------------------------------
            // BAY 2 (RIGHT): Telemetry Bay (414 x 588)
            // ------------------------------------------
            GameObject telemBayGo = new GameObject("TelemetryBay", typeof(RectTransform), typeof(Image));
            telemBayGo.transform.SetParent(contentGo.transform, false);
            RectTransform telemBayRect = telemBayGo.GetComponent<RectTransform>();
            telemBayRect.anchorMin = new Vector2(1f, 0f);
            telemBayRect.anchorMax = new Vector2(1f, 1f);
            telemBayRect.pivot = new Vector2(1f, 0.5f);
            telemBayRect.anchoredPosition = new Vector2(0, 0);
            telemBayRect.sizeDelta = new Vector2(414, 0);
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
            tbtRect.sizeDelta = new Vector2(-20, 26);
            TextMeshProUGUI tbtText = tBayTitleGo.GetComponent<TextMeshProUGUI>();
            tbtText.text = "[ CORE SENSORS & TELEMETRY ]";
            tbtText.fontSize = 15.5f;
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
            scRect.anchoredPosition = new Vector2(0, -38);
            scRect.sizeDelta = new Vector2(-24, 155);
            Image scImg = statusCardGo.GetComponent<Image>();
            scImg.color = new Color(0.08f, 0.12f, 0.17f, 0.9f);

            TextMeshProUGUI containmentTxt = CreateTelemetryRow(statusCardGo, 0, "CONTAINMENT:", "LOCKED // ACTIVE");
            TextMeshProUGUI fluxTxt = CreateTelemetryRow(statusCardGo, 1, "RADIATION FLUX:", "0.048 Sv/h (NOMINAL)");
            TextMeshProUGUI attemptsTxt = CreateTelemetryRow(statusCardGo, 2, "ATTEMPTS LEFT:", "5 / 5");
            TextMeshProUGUI accuracyTxt = CreateTelemetryRow(statusCardGo, 3, "SPECTRAL MATCH:", "0% NOMINAL");

            // Core Temperature / Meltdown Lockout Bar
            GameObject tempCardGo = new GameObject("TempCard", typeof(RectTransform), typeof(Image));
            tempCardGo.transform.SetParent(telemBayGo.transform, false);
            RectTransform tcRect = tempCardGo.GetComponent<RectTransform>();
            tcRect.anchorMin = new Vector2(0f, 1f);
            tcRect.anchorMax = new Vector2(1f, 1f);
            tcRect.pivot = new Vector2(0.5f, 1f);
            tcRect.anchoredPosition = new Vector2(0, -204);
            tcRect.sizeDelta = new Vector2(-24, 84);
            Image tcImg = tempCardGo.GetComponent<Image>();
            tcImg.color = new Color(0.08f, 0.12f, 0.17f, 0.9f);

            CreateTelemetryRow(tempCardGo, 0, "CORE MONITOR:", "<color=#00FF7F>NORMAL // STABLE</color>");

            // Progress Bar Frame for Core Temp
            GameObject barFrameGo = new GameObject("TempBarFrame", typeof(RectTransform), typeof(Image));
            barFrameGo.transform.SetParent(tempCardGo.transform, false);
            RectTransform bfRect = barFrameGo.GetComponent<RectTransform>();
            bfRect.anchorMin = new Vector2(0f, 0f);
            bfRect.anchorMax = new Vector2(1f, 0f);
            bfRect.pivot = new Vector2(0.5f, 0f);
            bfRect.anchoredPosition = new Vector2(0, 10);
            bfRect.sizeDelta = new Vector2(-20, 24);
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
            barTxt.text = "CORE TEMPERATURE NORMAL (312 K)";
            barTxt.fontSize = 12;
            barTxt.fontStyle = FontStyles.Bold;
            barTxt.alignment = TextAlignmentOptions.Center;
            barTxt.color = Color.white;

            // Sensor Diagnostic Legend Card
            GameObject legendCardGo = new GameObject("LegendCard", typeof(RectTransform), typeof(Image));
            legendCardGo.transform.SetParent(telemBayGo.transform, false);
            RectTransform lcRect = legendCardGo.GetComponent<RectTransform>();
            lcRect.anchorMin = new Vector2(0f, 1f);
            lcRect.anchorMax = new Vector2(1f, 1f);
            lcRect.pivot = new Vector2(0.5f, 1f);
            lcRect.anchoredPosition = new Vector2(0, -298);
            lcRect.sizeDelta = new Vector2(-24, 120);
            Image lcImg = legendCardGo.GetComponent<Image>();
            lcImg.color = new Color(0.08f, 0.12f, 0.17f, 0.9f);

            GameObject legendTextGo = new GameObject("LegendText", typeof(RectTransform), typeof(TextMeshProUGUI));
            legendTextGo.transform.SetParent(legendCardGo.transform, false);
            RectTransform ltRect = legendTextGo.GetComponent<RectTransform>();
            ltRect.anchorMin = Vector2.zero;
            ltRect.anchorMax = Vector2.one;
            ltRect.offsetMin = new Vector2(12, 8);
            ltRect.offsetMax = new Vector2(-12, -8);
            TextMeshProUGUI legTxt = legendTextGo.GetComponent<TextMeshProUGUI>();
            legTxt.text = "<b>SENSOR DIAGNOSTIC RESPONSE:</b>\n" +
                          "<color=#00FF7F>● GREEN PIN</color> : Valid isotope in EXACT slot\n" +
                          "<color=#FFD700>● YELLOW PIN</color>: Valid isotope in WRONG slot\n" +
                          "<color=#607080>○ DARK PIN</color>  : Isotope NOT present in code";
            legTxt.fontSize = 13;
            legTxt.lineSpacing = 14f;
            legTxt.color = new Color(0.85f, 0.9f, 0.95f, 1f);

            // Controls Guideline Plate
            GameObject controlsCardGo = new GameObject("ControlsCard", typeof(RectTransform), typeof(Image));
            controlsCardGo.transform.SetParent(telemBayGo.transform, false);
            RectTransform ccRect = controlsCardGo.GetComponent<RectTransform>();
            ccRect.anchorMin = new Vector2(0f, 0f);
            ccRect.anchorMax = new Vector2(1f, 0f);
            ccRect.pivot = new Vector2(0.5f, 0f);
            ccRect.anchoredPosition = new Vector2(0, 14);
            ccRect.sizeDelta = new Vector2(-24, 125);
            Image ccImg = controlsCardGo.GetComponent<Image>();
            ccImg.color = new Color(0.06f, 0.09f, 0.13f, 1f);

            GameObject ctrlTextGo = new GameObject("CtrlText", typeof(RectTransform), typeof(TextMeshProUGUI));
            ctrlTextGo.transform.SetParent(controlsCardGo.transform, false);
            RectTransform ctRect = ctrlTextGo.GetComponent<RectTransform>();
            ctRect.anchorMin = Vector2.zero;
            ctRect.anchorMax = Vector2.one;
            ctRect.offsetMin = new Vector2(12, 8);
            ctRect.offsetMax = new Vector2(-12, -8);
            TextMeshProUGUI ctrlTxt = ctrlTextGo.GetComponent<TextMeshProUGUI>();
            ctrlTxt.text = "<b>DECRYPTION CONTROLS:</b>\n" +
                           "• <b>[1 - 6]</b>: Insert Isotope (or click buttons below)\n" +
                           "• <b>[BACKSPACE / DEL]</b>: Clear Current Slot\n" +
                           "• <b>[ENTER / SPACE]</b>: Transmit & Evaluate Attempt\n" +
                           "• <b>[ESC / CLICK X]</b>: Emergency Disengage & Close";
            ctrlTxt.fontSize = 12.5f;
            ctrlTxt.lineSpacing = 14f;
            ctrlTxt.color = new Color(0.7f, 0.82f, 0.92f, 1f);

            // ==========================================
            // BUILD 5 ATTEMPT ROWS
            // ==========================================
            IsotopeTaskUI ui = root.AddComponent<IsotopeTaskUI>();
            ui.rootPanel = root;
            ui.terminalBox = boxRect;
            ui.headerText = headerText;
            ui.instructionText = instText;
            ui.closeButton = closeBtn;

            ui.matrixBay = matrixBayRect;
            ui.containmentStateText = containmentTxt;
            ui.radiationFluxText = fluxTxt;
            ui.attemptsLeftText = attemptsTxt;
            ui.accuracyText = accuracyTxt;
            ui.coreTempText = barTxt;
            ui.coreTempFill = barFillImg;
            ui.audioSource = root.GetComponent<AudioSource>();

            for (int r = 0; r < MaxAttempts; r++)
            {
                ui.rows[r] = CreateAttemptRow(rowsContRect, r, ui);
            }

            // ==========================================
            // BUILD BOTTOM INPUT TRAY (6 Isotope Buttons + Clear + Submit)
            // ==========================================
            GameObject trayGo = new GameObject("InputTray", typeof(RectTransform));
            trayGo.transform.SetParent(matrixBayGo.transform, false);
            RectTransform trayRect = trayGo.GetComponent<RectTransform>();
            trayRect.anchorMin = new Vector2(0f, 0f);
            trayRect.anchorMax = new Vector2(1f, 0f);
            trayRect.pivot = new Vector2(0.5f, 0f);
            trayRect.anchoredPosition = new Vector2(0, 14);
            trayRect.sizeDelta = new Vector2(-20, 145);

            // 6 Isotope Selector Buttons (Enlarged to 94x70 with clear click response)
            for (int i = 0; i < IsotopePoolSize; i++)
            {
                float btnX = -250f + (i * 100f);
                GameObject bGo = new GameObject($"IsoBtn_{i}", typeof(RectTransform), typeof(Image), typeof(Button));
                bGo.transform.SetParent(trayGo.transform, false);
                RectTransform bRect = bGo.GetComponent<RectTransform>();
                bRect.anchorMin = new Vector2(0.5f, 1f);
                bRect.anchorMax = new Vector2(0.5f, 1f);
                bRect.pivot = new Vector2(0.5f, 1f);
                bRect.anchoredPosition = new Vector2(btnX, 0);
                bRect.sizeDelta = new Vector2(94, 70);

                Image bImg = bGo.GetComponent<Image>();
                bImg.color = new Color(IsotopeColors[i].r * 0.16f, IsotopeColors[i].g * 0.16f, IsotopeColors[i].b * 0.16f, 1f);
                bImg.raycastTarget = true;

                Outline bOutline = bGo.AddComponent<Outline>();
                bOutline.effectColor = IsotopeColors[i];
                bOutline.effectDistance = new Vector2(1.5f, -1.5f);

                GameObject bTxtGo = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
                bTxtGo.transform.SetParent(bGo.transform, false);
                RectTransform btRect2 = bTxtGo.GetComponent<RectTransform>();
                btRect2.anchorMin = Vector2.zero;
                btRect2.anchorMax = Vector2.one;
                btRect2.offsetMin = Vector2.zero;
                btRect2.offsetMax = Vector2.zero;
                TextMeshProUGUI bTxt = bTxtGo.GetComponent<TextMeshProUGUI>();
                bTxt.text = $"<size=26><b>{IsotopeSymbols[i]}</b></size>\n<size=12><color=#88AABB>[{i + 1}] {IsotopeNames[i]}</color></size>";
                bTxt.alignment = TextAlignmentOptions.Center;
                bTxt.color = IsotopeColors[i];
                bTxt.raycastTarget = false;

                Button bBtn = bGo.GetComponent<Button>();
                bBtn.targetGraphic = bImg;
                bBtn.navigation = new Navigation { mode = Navigation.Mode.None };
                ColorBlock bColors = bBtn.colors;
                bColors.normalColor = Color.white;
                bColors.highlightedColor = new Color(1.3f, 1.3f, 1.3f, 1f);
                bColors.pressedColor = new Color(0.7f, 0.7f, 0.7f, 1f);
                bBtn.colors = bColors;

                ui.isotopeButtons[i] = bBtn;
            }

            // Action Buttons (Clear & Transmit)
            // Clear Button (Backspace)
            GameObject clearBtnGo = new GameObject("ClearButton", typeof(RectTransform), typeof(Image), typeof(Button));
            clearBtnGo.transform.SetParent(trayGo.transform, false);
            RectTransform cbRect = clearBtnGo.GetComponent<RectTransform>();
            cbRect.anchorMin = new Vector2(0f, 0f);
            cbRect.anchorMax = new Vector2(0f, 0f);
            cbRect.pivot = new Vector2(0f, 0f);
            cbRect.anchoredPosition = new Vector2(0, 4);
            cbRect.sizeDelta = new Vector2(160, 52);
            Image cbImg = clearBtnGo.GetComponent<Image>();
            cbImg.color = new Color(0.28f, 0.10f, 0.12f, 1f);
            cbImg.raycastTarget = true;
            Outline cbOutline = clearBtnGo.AddComponent<Outline>();
            cbOutline.effectColor = new Color(0.9f, 0.25f, 0.3f, 1f);
            cbOutline.effectDistance = new Vector2(1.5f, -1.5f);

            GameObject cbTxtGo = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            cbTxtGo.transform.SetParent(clearBtnGo.transform, false);
            RectTransform cbtRect = cbTxtGo.GetComponent<RectTransform>();
            cbtRect.anchorMin = Vector2.zero;
            cbtRect.anchorMax = Vector2.one;
            cbtRect.offsetMin = Vector2.zero;
            cbtRect.offsetMax = Vector2.zero;
            TextMeshProUGUI cbTxt = cbTxtGo.GetComponent<TextMeshProUGUI>();
            cbTxt.text = "<b>[BACKSPACE]</b>\n<size=12>CLEAR</size>";
            cbTxt.fontSize = 13.5f;
            cbTxt.fontStyle = FontStyles.Bold;
            cbTxt.alignment = TextAlignmentOptions.Center;
            cbTxt.color = new Color(1f, 0.45f, 0.5f, 1f);
            cbTxt.raycastTarget = false;

            Button cbBtn = clearBtnGo.GetComponent<Button>();
            cbBtn.targetGraphic = cbImg;
            cbBtn.navigation = new Navigation { mode = Navigation.Mode.None };
            ColorBlock cbColors = cbBtn.colors;
            cbColors.normalColor = Color.white;
            cbColors.highlightedColor = new Color(1.3f, 0.85f, 0.85f, 1f);
            cbColors.pressedColor = new Color(0.6f, 0.2f, 0.2f, 1f);
            cbBtn.colors = cbColors;
            ui.clearButton = cbBtn;

            // Submit / Transmit Button
            GameObject subBtnGo = new GameObject("SubmitButton", typeof(RectTransform), typeof(Image), typeof(Button));
            subBtnGo.transform.SetParent(trayGo.transform, false);
            RectTransform sbRect = subBtnGo.GetComponent<RectTransform>();
            sbRect.anchorMin = new Vector2(1f, 0f);
            sbRect.anchorMax = new Vector2(1f, 0f);
            sbRect.pivot = new Vector2(1f, 0f);
            sbRect.anchoredPosition = new Vector2(0, 4);
            sbRect.sizeDelta = new Vector2(434, 52);
            Image sbImg = subBtnGo.GetComponent<Image>();
            sbImg.color = new Color(0.06f, 0.24f, 0.18f, 1f);
            sbImg.raycastTarget = true;
            Outline sbOutline = subBtnGo.AddComponent<Outline>();
            sbOutline.effectColor = new Color(0f, 1f, 0.5f, 1f);
            sbOutline.effectDistance = new Vector2(1.5f, -1.5f);

            GameObject sbTxtGo = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            sbTxtGo.transform.SetParent(subBtnGo.transform, false);
            RectTransform sbtRect = sbTxtGo.GetComponent<RectTransform>();
            sbtRect.anchorMin = Vector2.zero;
            sbtRect.anchorMax = Vector2.one;
            sbtRect.offsetMin = Vector2.zero;
            sbtRect.offsetMax = Vector2.zero;
            TextMeshProUGUI sbTxt = sbTxtGo.GetComponent<TextMeshProUGUI>();
            sbTxt.text = "<b>[TRANSMIT SEQUENCE / ENTER]</b>";
            sbTxt.fontSize = 15f;
            sbTxt.alignment = TextAlignmentOptions.Center;
            sbTxt.color = new Color(0.4f, 1f, 0.7f, 1f);
            sbTxt.raycastTarget = false;

            Button sbBtn = subBtnGo.GetComponent<Button>();
            sbBtn.targetGraphic = sbImg;
            sbBtn.navigation = new Navigation { mode = Navigation.Mode.None };
            ColorBlock sbColors = sbBtn.colors;
            sbColors.normalColor = Color.white;
            sbColors.highlightedColor = new Color(1.2f, 1.3f, 1.2f, 1f);
            sbColors.pressedColor = new Color(0.6f, 0.8f, 0.6f, 1f);
            sbBtn.colors = sbColors;
            ui.submitButton = sbBtn;
            ui.submitButtonText = sbTxt;

            // Explicitly bind all button click events immediately!
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
            rowRect.anchoredPosition = new Vector2(0, -8 - (rowIndex * 35));
            rowRect.sizeDelta = new Vector2(-20, 28);

            GameObject lblGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            lblGo.transform.SetParent(rowGo.transform, false);
            RectTransform lblRect = lblGo.GetComponent<RectTransform>();
            lblRect.anchorMin = new Vector2(0f, 0f);
            lblRect.anchorMax = new Vector2(0.48f, 1f);
            lblRect.offsetMin = Vector2.zero;
            lblRect.offsetMax = Vector2.zero;
            TextMeshProUGUI lblTxt = lblGo.GetComponent<TextMeshProUGUI>();
            lblTxt.text = label;
            lblTxt.fontSize = 13.5f;
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
            valTxt.fontSize = 14f;
            valTxt.fontStyle = FontStyles.Bold;
            valTxt.alignment = TextAlignmentOptions.MidlineRight;
            valTxt.color = Color.white;

            return valTxt;
        }

        private static AttemptRow CreateAttemptRow(RectTransform parent, int rowIndex, IsotopeTaskUI parentUI)
        {
            AttemptRow row = new AttemptRow();
            float posY = -4f - (rowIndex * 70f);

            GameObject rowGo = new GameObject($"AttemptRow_{rowIndex}", typeof(RectTransform), typeof(Image));
            rowGo.transform.SetParent(parent, false);
            RectTransform rowRect = rowGo.GetComponent<RectTransform>();
            rowRect.anchorMin = new Vector2(0.5f, 1f);
            rowRect.anchorMax = new Vector2(0.5f, 1f);
            rowRect.pivot = new Vector2(0.5f, 1f);
            rowRect.anchoredPosition = new Vector2(0, posY);
            rowRect.sizeDelta = new Vector2(612, 64);

            Image rowBg = rowGo.GetComponent<Image>();
            rowBg.color = new Color(0.06f, 0.09f, 0.13f, 0.85f);
            Outline rowOutline = rowGo.AddComponent<Outline>();
            rowOutline.effectColor = new Color(0.14f, 0.20f, 0.28f, 1f);
            rowOutline.effectDistance = new Vector2(1, -1);
            row.rowRoot = rowGo;

            // Row Label
            GameObject lblGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            lblGo.transform.SetParent(rowGo.transform, false);
            RectTransform lblRect = lblGo.GetComponent<RectTransform>();
            lblRect.anchorMin = new Vector2(0f, 0.5f);
            lblRect.anchorMax = new Vector2(0f, 0.5f);
            lblRect.pivot = new Vector2(0f, 0.5f);
            lblRect.anchoredPosition = new Vector2(12, 0);
            lblRect.sizeDelta = new Vector2(115, 32);
            TextMeshProUGUI lblTxt = lblGo.GetComponent<TextMeshProUGUI>();
            lblTxt.text = $"ATTEMPT 0{rowIndex + 1}";
            lblTxt.fontSize = 13.5f;
            lblTxt.fontStyle = FontStyles.Bold;
            lblTxt.alignment = TextAlignmentOptions.MidlineLeft;
            lblTxt.color = new Color(0.45f, 0.55f, 0.68f, 1f);
            row.rowLabel = lblTxt;

            // 4 Slot Boxes (each 56 x 56 px, spaced 68px apart)
            for (int c = 0; c < CodeLength; c++)
            {
                int slotIdx = c;
                float slotX = 145f + (c * 68f);
                SlotUI slot = CreateSlotBox(rowGo.transform, slotX);
                row.slots[c] = slot;
                row.chosenIsotopes[c] = -1;

                // Native Button for reliable slot clicking
                Button slotBtn = slot.rootGo.AddComponent<Button>();
                slotBtn.targetGraphic = slot.bgImage;
                slotBtn.navigation = new Navigation { mode = Navigation.Mode.None };
                ColorBlock sbColors = slotBtn.colors;
                sbColors.normalColor = Color.white;
                sbColors.highlightedColor = new Color(1.2f, 1.2f, 1.2f, 1f);
                sbColors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
                slotBtn.colors = sbColors;

                int capturedRow = rowIndex;
                int capturedSlot = slotIdx;
                slotBtn.onClick.AddListener(() =>
                {
                    if (parentUI.currentAttemptIndex == capturedRow)
                    {
                        parentUI.currentSlotIndex = capturedSlot;
                        parentUI.UpdateActiveSlotOutlines();
                    }
                });
            }

            // Diagnostic Sensor Pins Bay (Right side of row)
            GameObject pinBayGo = new GameObject("PinBay", typeof(RectTransform), typeof(Image));
            pinBayGo.transform.SetParent(rowGo.transform, false);
            RectTransform pinBayRect = pinBayGo.GetComponent<RectTransform>();
            pinBayRect.anchorMin = new Vector2(1f, 0.5f);
            pinBayRect.anchorMax = new Vector2(1f, 0.5f);
            pinBayRect.pivot = new Vector2(1f, 0.5f);
            pinBayRect.anchoredPosition = new Vector2(-12, 0);
            pinBayRect.sizeDelta = new Vector2(144, 46);
            Image pinBayImg = pinBayGo.GetComponent<Image>();
            pinBayImg.color = new Color(0.04f, 0.06f, 0.08f, 1f);
            Outline pbOutline = pinBayGo.AddComponent<Outline>();
            pbOutline.effectColor = new Color(0.18f, 0.25f, 0.32f, 1f);
            pbOutline.effectDistance = new Vector2(1, -1);

            // 4 Sensor Pins (each 24 x 24 px) in a clean row inside PinBay
            for (int p = 0; p < CodeLength; p++)
            {
                float pinX = -51f + (p * 34f);
                GameObject pinGo = new GameObject($"Pin_{p}", typeof(RectTransform), typeof(Image));
                pinGo.transform.SetParent(pinBayGo.transform, false);
                RectTransform pr = pinGo.GetComponent<RectTransform>();
                pr.anchorMin = new Vector2(0.5f, 0.5f);
                pr.anchorMax = new Vector2(0.5f, 0.5f);
                pr.pivot = new Vector2(0.5f, 0.5f);
                pr.anchoredPosition = new Vector2(pinX, 0);
                pr.sizeDelta = new Vector2(24, 24);

                Image pinImg = pinGo.GetComponent<Image>();
                pinImg.color = new Color(0.12f, 0.16f, 0.22f, 1f);

                Outline pOut = pinGo.AddComponent<Outline>();
                pOut.effectColor = new Color(0.25f, 0.35f, 0.45f, 1f);
                pOut.effectDistance = new Vector2(1, -1);

                row.pinImages[p] = pinImg;
            }

            return row;
        }

        private static SlotUI CreateSlotBox(Transform parent, float posX)
        {
            SlotUI slot = new SlotUI();
            GameObject slotGo = new GameObject("Slot", typeof(RectTransform), typeof(Image));
            slotGo.transform.SetParent(parent, false);
            RectTransform sr = slotGo.GetComponent<RectTransform>();
            sr.anchorMin = new Vector2(0f, 0.5f);
            sr.anchorMax = new Vector2(0f, 0.5f);
            sr.pivot = new Vector2(0.5f, 0.5f);
            sr.anchoredPosition = new Vector2(posX, 0);
            sr.sizeDelta = new Vector2(56, 56);

            Image sImg = slotGo.GetComponent<Image>();
            sImg.color = new Color(0.08f, 0.12f, 0.17f, 1f);
            sImg.raycastTarget = true;
            slot.bgImage = sImg;
            slot.rootGo = slotGo;

            Outline sOutline = slotGo.AddComponent<Outline>();
            sOutline.effectColor = new Color(0.18f, 0.26f, 0.36f, 1f);
            sOutline.effectDistance = new Vector2(1.5f, -1.5f);
            slot.outline = sOutline;

            GameObject glowGo = new GameObject("Glow", typeof(RectTransform), typeof(Image));
            glowGo.transform.SetParent(slotGo.transform, false);
            RectTransform gr = glowGo.GetComponent<RectTransform>();
            gr.anchorMin = Vector2.zero;
            gr.anchorMax = Vector2.one;
            gr.offsetMin = Vector2.zero;
            gr.offsetMax = Vector2.zero;
            Image glowImg = glowGo.GetComponent<Image>();
            glowImg.color = Color.clear;
            glowImg.raycastTarget = false;
            slot.glowImage = glowImg;

            GameObject txtGo = new GameObject("Glyph", typeof(RectTransform), typeof(TextMeshProUGUI));
            txtGo.transform.SetParent(slotGo.transform, false);
            RectTransform tr = txtGo.GetComponent<RectTransform>();
            tr.anchorMin = Vector2.zero;
            tr.anchorMax = Vector2.one;
            tr.offsetMin = Vector2.zero;
            tr.offsetMax = Vector2.zero;
            TextMeshProUGUI txt = txtGo.GetComponent<TextMeshProUGUI>();
            txt.text = "";
            txt.fontSize = 32;
            txt.fontStyle = FontStyles.Bold;
            txt.alignment = TextAlignmentOptions.Center;
            txt.color = Color.white;
            txt.raycastTarget = false;
            slot.glyphText = txt;

            return slot;
        }

        #endregion
    }
}
