using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;
using TheLastSignal.Core.Bootstrap;
using TheLastSignal.Entities.Player;
using TheLastSignal.Systems.Comms;

namespace TheLastSignal.UI
{
    /// <summary>
    /// Tactical Sci-Fi In-Game Chat & Voice Comms HUD overlay.
    /// Features:
    /// - Text chat broadcast with role and slot-based color tags.
    /// - Push-to-Talk voice status indicator with live VU audio meters and speaker display.
    /// - Strict lockout feedback for The Shade role (Comms Blocked / No Transceiver).
    /// - Non-intrusive auto-fading background.
    /// - Pauses player WASD movement during typing to avoid walking into walls.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class SurvivorChatUI : MonoBehaviour
    {
        public static SurvivorChatUI Instance { get; private set; }

        [Header("UI References")]
        [SerializeField] private GameObject chatRoot;
        [SerializeField] private CanvasGroup chatCanvasGroup;
        [SerializeField] private TextMeshProUGUI headerTitleText;
        [SerializeField] private TextMeshProUGUI voiceStatusText;
        [SerializeField] private TextMeshProUGUI messagesLogText;
        [SerializeField] private ScrollRect scrollRect;
        [SerializeField] private TMP_InputField inputField;
        [SerializeField] private Image voiceMeterFill;

        private PlayerController localPlayer;
        private SurvivorCommsAgent localCommsAgent;

        private readonly List<string> messageHistory = new List<string>();
        private const int MaxMessagesHistory = 60;

        private float lastActivityTime = 0f;
        private const float FadeDelaySeconds = 6f;
        private float blockedAlertTimer = 0f;
        private string blockedAlertReason = string.Empty;
        private float submitCooldown = 0f;

        private static readonly Color[] SlotColors = new Color[]
        {
            new Color(0.2f, 0.8f, 1.0f, 1f),  // P1 Cyan
            new Color(1.0f, 0.65f, 0.2f, 1f), // P2 Orange
            new Color(0.35f, 0.95f, 0.45f, 1f),// P3 Green
            new Color(0.95f, 0.45f, 0.95f, 1f) // P4 Magenta
        };

        private void Awake()
        {
            Instance = this;

            // Dedicated Server safety guard
            if (GameBootstrap.CurrentMode == LaunchMode.DedicatedServer)
            {
                gameObject.SetActive(false);
                return;
            }

            EnsureCanvasAndHierarchy();
            lastActivityTime = Time.time;
        }

        private void OnEnable()
        {
            SurvivorCommsAgent.OnGlobalChatMessageReceived += HandleGlobalChatMessageReceived;
            SurvivorCommsAgent.OnGlobalVoiceStateChanged += HandleGlobalVoiceStateChanged;
            SurvivorCommsAgent.OnCommsBlocked += HandleCommsBlocked;
        }

        private void OnDisable()
        {
            SurvivorCommsAgent.OnGlobalChatMessageReceived -= HandleGlobalChatMessageReceived;
            SurvivorCommsAgent.OnGlobalVoiceStateChanged -= HandleGlobalVoiceStateChanged;
            SurvivorCommsAgent.OnCommsBlocked -= HandleCommsBlocked;

            // Ensure player movement is unlocked on disable
            if (localPlayer != null)
            {
                localPlayer.IsMovementLocked = false;
            }
        }

        private void Start()
        {
            AddSystemMessage("Tactical Crew Radio Transceiver online. Press [ENTER] to chat, hold [V] for voice.");
        }

        private void Update()
        {
            // Acquire local player and comms agent if needed
            if (localPlayer == null)
            {
                localPlayer = PlayerController.Local;
            }

            if (localPlayer != null && localCommsAgent == null)
            {
                localCommsAgent = localPlayer.GetComponent<SurvivorCommsAgent>();
                if (localCommsAgent == null)
                {
                    localCommsAgent = localPlayer.gameObject.AddComponent<SurvivorCommsAgent>();
                }
            }

            bool isMonster = (localCommsAgent != null && localCommsAgent.IsMonster) || (localPlayer != null && localPlayer.IsMonster);
            if (chatRoot != null)
            {
                bool shouldBeActive = !isMonster;
                if (chatRoot.activeSelf != shouldBeActive)
                {
                    chatRoot.SetActive(shouldBeActive);
                    if (isMonster && inputField != null && inputField.isFocused)
                    {
                        CancelChat();
                    }
                }
            }

            // Read keyboard input for Enter and Esc
            HandleKeyboardInput();

            // Handle HUD auto-fade and blocked warnings
            UpdateVisualFadeAndFeedback();
        }

        private void HandleKeyboardInput()
        {
            if (submitCooldown > 0f)
            {
                submitCooldown -= Time.deltaTime;
                return;
            }

            Keyboard kb = Keyboard.current;
            bool enterPressed = kb != null && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame);
            bool escPressed = kb != null && kb.escapeKey.wasPressedThisFrame;

            bool isMonster = (localCommsAgent != null && localCommsAgent.IsMonster) || (localPlayer != null && localPlayer.IsMonster);

            if (isMonster)
            {
                if (enterPressed)
                {
                    HandleCommsBlocked("THE SHADE HAS NO TRANSCEIVER // CANNOT TRANSMIT TEXT");
                }
                return;
            }

            if (inputField == null) return;

            if (inputField.isFocused)
            {
                // Ensure player cannot move while typing
                if (localPlayer != null)
                {
                    localPlayer.IsMovementLocked = true;
                }

                if (escPressed)
                {
                    CancelChat();
                }
                else if (enterPressed)
                {
                    SubmitChat(inputField.text);
                }
            }
            else
            {
                if (enterPressed)
                {
                    OpenChatInput();
                }
            }
        }

        private void HandleInputFieldSubmit(string text)
        {
            SubmitChat(text);
        }

        public void OpenChatInput()
        {
            if (inputField == null) return;

            inputField.gameObject.SetActive(true);
            inputField.Select();
            inputField.ActivateInputField();

            if (UnityEngine.EventSystems.EventSystem.current != null)
            {
                UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(inputField.gameObject);
            }

            if (localPlayer != null)
            {
                localPlayer.IsMovementLocked = true;
            }

            submitCooldown = 0.15f;
            lastActivityTime = Time.time;
        }

        public void SubmitChat(string text = null)
        {
            if (inputField == null) return;

            if (text == null) text = inputField.text;

            inputField.text = string.Empty;
            inputField.DeactivateInputField();

            if (UnityEngine.EventSystems.EventSystem.current != null)
            {
                UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
            }

            if (localPlayer != null)
            {
                localPlayer.IsMovementLocked = false;
            }

            submitCooldown = 0.20f;
            lastActivityTime = Time.time;

            if (!string.IsNullOrWhiteSpace(text))
            {
                if (localCommsAgent != null)
                {
                    localCommsAgent.SendChatMessage(text);
                }
            }
        }

        public void CancelChat()
        {
            if (inputField == null) return;

            inputField.text = string.Empty;
            inputField.DeactivateInputField();

            if (UnityEngine.EventSystems.EventSystem.current != null)
            {
                UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
            }

            if (localPlayer != null)
            {
                localPlayer.IsMovementLocked = false;
            }

            submitCooldown = 0.15f;
            lastActivityTime = Time.time;
        }

        private void HandleGlobalChatMessageReceived(string senderName, string message, int slotIndex)
        {
            int colorIdx = Mathf.Clamp(slotIndex, 0, SlotColors.Length - 1);
            string hexColor = ColorUtility.ToHtmlStringRGB(SlotColors[colorIdx]);
            string timestamp = DateTime.Now.ToString("HH:mm");

            string formatted = $"<color=#778899>[{timestamp}]</color> <color=#{hexColor}><b>[{senderName}]</b></color>: {message}";
            AddMessage(formatted);
        }

        public void AddSystemMessage(string text)
        {
            string timestamp = DateTime.Now.ToString("HH:mm");
            string formatted = $"<color=#778899>[{timestamp}]</color> <color=#00E5FF><b>[TRANSCEIVER]</b></color>: <i>{text}</i>";
            AddMessage(formatted);
        }

        private void AddMessage(string formattedMessage)
        {
            messageHistory.Add(formattedMessage);
            if (messageHistory.Count > MaxMessagesHistory)
            {
                messageHistory.RemoveAt(0);
            }

            if (messagesLogText != null)
            {
                messagesLogText.text = string.Join("\n", messageHistory);
            }

            lastActivityTime = Time.time;

            // Auto scroll to bottom
            if (scrollRect != null)
            {
                Canvas.ForceUpdateCanvases();
                scrollRect.verticalNormalizedPosition = 0f;
            }
        }

        private void HandleGlobalVoiceStateChanged(SurvivorCommsAgent agent, bool speaking, float level)
        {
            lastActivityTime = Time.time;

            if (voiceStatusText == null) return;

            if (speaking)
            {
                bool isLocal = (agent == localCommsAgent);
                if (isLocal)
                {
                    int bars = Mathf.Clamp(Mathf.RoundToInt(level * 10f), 1, 10);
                    string meter = new string('|', bars);
                    voiceStatusText.text = $"<b><color=#00FFCC>[PTT ACTIVE]</color></b> <color=#FFFFFF>TRANSMITTING</color> <color=#00E5FF>[{meter}]</color>";
                }
                else
                {
                    string name = agent != null ? agent.GetFormattedSenderName() : "Remote Crew";
                    voiceStatusText.text = $"<b><color=#FFAA33>[RADIO RX]</color></b> <color=#FFFFFF>{name}</color> <color=#FFAA00>)))</color>";
                }

                if (voiceMeterFill != null)
                {
                    voiceMeterFill.fillAmount = Mathf.Clamp01(level);
                    voiceMeterFill.color = isLocal ? new Color(0f, 1f, 0.8f, 1f) : new Color(1f, 0.65f, 0.2f, 1f);
                }
            }
            else
            {
                voiceStatusText.text = "<b><color=#00E5FF>CREW TRANSCEIVER</color></b> <color=#556677>// 142.8 MHz</color>";
                if (voiceMeterFill != null) voiceMeterFill.fillAmount = 0f;
            }
        }

        private void HandleCommsBlocked(string reason)
        {
            blockedAlertTimer = 2.5f;
            blockedAlertReason = reason;
            lastActivityTime = Time.time;

            // Play electronic error audio
            AudioSource.PlayClipAtPoint(VoiceAudioSynthesizer.GetCommsDeniedClip(), Vector3.zero, 0.90f);

            // Screen shake feedback
            TheLastSignal.Entities.Camera.CameraFollow.Shake(0.08f, 0.12f);
        }

        private void UpdateVisualFadeAndFeedback()
        {
            // Blocked alert display takes priority
            if (blockedAlertTimer > 0f)
            {
                blockedAlertTimer -= Time.deltaTime;
                if (voiceStatusText != null)
                {
                    float flash = Mathf.PingPong(Time.time * 8f, 1f);
                    string col = flash > 0.5f ? "#FF2233" : "#FFAAAA";
                    voiceStatusText.text = $"<b><color={col}>[COMMS BLOCKED] {blockedAlertReason}</color></b>";
                }
            }

            // Auto-fade background when idle
            if (chatCanvasGroup != null)
            {
                bool isTyping = inputField != null && inputField.isFocused;
                bool isVoiceActive = (Time.time - lastActivityTime) < 1.0f;
                bool isRecent = (Time.time - lastActivityTime) < FadeDelaySeconds;

                float targetAlpha = (isTyping || isVoiceActive || isRecent) ? 1.0f : 0.35f;
                chatCanvasGroup.alpha = Mathf.MoveTowards(chatCanvasGroup.alpha, targetAlpha, Time.deltaTime * 3f);
            }
        }

        // =====================================================================
        // PROCEDURAL CANVAS AND UI BUILDER
        // =====================================================================

        public void EnsureCanvasAndHierarchy()
        {
            if (transform.parent != null)
            {
                transform.SetParent(null, false);
            }

            Canvas canvas = GetComponent<Canvas>();
            if (canvas == null) canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 95; // Just below ShadeHUDOverlay (96) and Popups (100)

            CanvasScaler scaler = GetComponent<CanvasScaler>();
            if (scaler == null) scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            if (GetComponent<GraphicRaycaster>() == null) gameObject.AddComponent<GraphicRaycaster>();

            if (chatRoot != null && chatRoot.transform.childCount > 0)
            {
                if (inputField == null) inputField = GetComponentInChildren<TMP_InputField>();
                if (inputField != null)
                {
                    inputField.onSubmit.RemoveListener(HandleInputFieldSubmit);
                    inputField.onSubmit.AddListener(HandleInputFieldSubmit);
                }
                return;
            }

            // Build Procedural Hierarchy
            BuildProceduralUI();
        }

        private void BuildProceduralUI()
        {
            // 1. Root Container (Bottom Left)
            chatRoot = new GameObject("ChatRoot");
            chatRoot.transform.SetParent(transform, false);
            RectTransform rootRt = chatRoot.AddComponent<RectTransform>();
            rootRt.anchorMin = new Vector2(0f, 0f);
            rootRt.anchorMax = new Vector2(0f, 0f);
            rootRt.pivot = new Vector2(0f, 0f);
            rootRt.anchoredPosition = new Vector2(30f, 118f);
            rootRt.sizeDelta = new Vector2(440f, 240f);

            chatCanvasGroup = chatRoot.AddComponent<CanvasGroup>();

            // Panel Background
            Image bg = chatRoot.AddComponent<Image>();
            bg.color = new Color(0.04f, 0.07f, 0.11f, 0.82f); // Dark translucent sci-fi navy

            // 2. Header Bar
            GameObject headerGo = new GameObject("HeaderBar");
            headerGo.transform.SetParent(chatRoot.transform, false);
            RectTransform headerRt = headerGo.AddComponent<RectTransform>();
            headerRt.anchorMin = new Vector2(0f, 1f);
            headerRt.anchorMax = new Vector2(1f, 1f);
            headerRt.pivot = new Vector2(0.5f, 1f);
            headerRt.anchoredPosition = new Vector2(0f, 0f);
            headerRt.sizeDelta = new Vector2(0f, 28f);

            Image headerBg = headerGo.AddComponent<Image>();
            headerBg.color = new Color(0.08f, 0.14f, 0.20f, 0.95f);

            // Voice Status Text inside Header
            GameObject statusTextGo = new GameObject("VoiceStatusText");
            statusTextGo.transform.SetParent(headerGo.transform, false);
            RectTransform stRt = statusTextGo.AddComponent<RectTransform>();
            stRt.anchorMin = new Vector2(0f, 0f);
            stRt.anchorMax = new Vector2(1f, 1f);
            stRt.offsetMin = new Vector2(10f, 0f);
            stRt.offsetMax = new Vector2(-10f, 0f);

            voiceStatusText = statusTextGo.AddComponent<TextMeshProUGUI>();
            voiceStatusText.fontSize = 13f;
            voiceStatusText.fontStyle = FontStyles.Bold;
            voiceStatusText.alignment = TextAlignmentOptions.MidlineLeft;
            voiceStatusText.text = "<b><color=#00E5FF>CREW TRANSCEIVER</color></b> <color=#556677>// 142.8 MHz</color>";

            // 3. Scrollable Message View
            GameObject scrollGo = new GameObject("MessagesScrollView");
            scrollGo.transform.SetParent(chatRoot.transform, false);
            RectTransform scrollRt = scrollGo.AddComponent<RectTransform>();
            scrollRt.anchorMin = new Vector2(0f, 0f);
            scrollRt.anchorMax = new Vector2(1f, 1f);
            scrollRt.offsetMin = new Vector2(10f, 40f);
            scrollRt.offsetMax = new Vector2(-10f, -32f);

            scrollRect = scrollGo.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;

            // Content Text
            GameObject contentGo = new GameObject("ContentText");
            contentGo.transform.SetParent(scrollGo.transform, false);
            RectTransform contentRt = contentGo.AddComponent<RectTransform>();
            contentRt.anchorMin = new Vector2(0f, 0f);
            contentRt.anchorMax = new Vector2(1f, 1f);
            contentRt.offsetMin = Vector2.zero;
            contentRt.offsetMax = Vector2.zero;

            messagesLogText = contentGo.AddComponent<TextMeshProUGUI>();
            messagesLogText.fontSize = 13f;
            messagesLogText.lineSpacing = 4f;
            messagesLogText.alignment = TextAlignmentOptions.BottomLeft;
            messagesLogText.overflowMode = TextOverflowModes.Truncate;
            messagesLogText.text = string.Empty;

            scrollRect.content = contentRt;

            // 4. Input Field Container
            GameObject inputGo = new GameObject("ChatInputField");
            inputGo.transform.SetParent(chatRoot.transform, false);
            RectTransform inputRt = inputGo.AddComponent<RectTransform>();
            inputRt.anchorMin = new Vector2(0f, 0f);
            inputRt.anchorMax = new Vector2(1f, 0f);
            inputRt.pivot = new Vector2(0.5f, 0f);
            inputRt.anchoredPosition = new Vector2(0f, 5f);
            inputRt.sizeDelta = new Vector2(-16f, 30f);

            Image inputBg = inputGo.AddComponent<Image>();
            inputBg.color = new Color(0.06f, 0.10f, 0.16f, 0.95f);

            // Input Text Viewport
            GameObject textArea = new GameObject("TextArea");
            textArea.transform.SetParent(inputGo.transform, false);
            RectTransform textAreaRt = textArea.AddComponent<RectTransform>();
            textAreaRt.anchorMin = Vector2.zero;
            textAreaRt.anchorMax = Vector2.one;
            textAreaRt.offsetMin = new Vector2(8f, 2f);
            textAreaRt.offsetMax = new Vector2(-8f, -2f);

            // Input Text
            GameObject textGo = new GameObject("Text");
            textGo.transform.SetParent(textArea.transform, false);
            RectTransform textRt = textGo.AddComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = Vector2.zero;
            textRt.offsetMax = Vector2.zero;

            TextMeshProUGUI inputText = textGo.AddComponent<TextMeshProUGUI>();
            inputText.fontSize = 13f;
            inputText.color = Color.white;
            inputText.alignment = TextAlignmentOptions.MidlineLeft;

            // Placeholder Text
            GameObject placeholderGo = new GameObject("Placeholder");
            placeholderGo.transform.SetParent(textArea.transform, false);
            RectTransform phRt = placeholderGo.AddComponent<RectTransform>();
            phRt.anchorMin = Vector2.zero;
            phRt.anchorMax = Vector2.one;
            phRt.offsetMin = Vector2.zero;
            phRt.offsetMax = Vector2.zero;

            TextMeshProUGUI placeholderText = placeholderGo.AddComponent<TextMeshProUGUI>();
            placeholderText.fontSize = 13f;
            placeholderText.fontStyle = FontStyles.Italic;
            placeholderText.color = new Color(0.45f, 0.55f, 0.65f, 0.8f);
            placeholderText.text = "Press [ENTER] to broadcast to crew...";
            placeholderText.alignment = TextAlignmentOptions.MidlineLeft;

            inputField = inputGo.AddComponent<TMP_InputField>();
            inputField.textViewport = textAreaRt;
            inputField.textComponent = inputText;
            inputField.placeholder = placeholderText;
            inputField.characterLimit = 120;
            inputField.lineType = TMP_InputField.LineType.SingleLine;
            inputField.onSubmit.AddListener(HandleInputFieldSubmit);
        }

        /// <summary>
        /// Global helper to spawn or fetch the SurvivorChatUI in the scene.
        /// </summary>
        public static SurvivorChatUI GetOrCreateInstance()
        {
            if (Instance != null) return Instance;

            SurvivorChatUI found = FindAnyObjectByType<SurvivorChatUI>();
            if (found != null)
            {
                Instance = found;
                return Instance;
            }

            GameObject uiGo = new GameObject("SurvivorChatUI");
            SurvivorChatUI created = uiGo.AddComponent<SurvivorChatUI>();
            created.EnsureCanvasAndHierarchy();
            return created;
        }
    }
}
