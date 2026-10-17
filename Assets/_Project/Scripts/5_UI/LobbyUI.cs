using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using TheLastSignal.Core;
using TheLastSignal.Core.Bootstrap;
using Unity.Services.Relay;

namespace TheLastSignal.UI
{
    /// <summary>
    /// Presents the pre-match lobby (Host/Join controls, join code, connected player list, ready
    /// toggle). Strictly presentation: it reads NetworkSessionManager's NetworkList and reacts to its
    /// OnListChanged event, and only ever asks the Host to change state via
    /// SubmitReadyStateServerRpc — it never mutates the shared list directly (ARCHITECTURE_RULES.md
    /// Rule 2/3). Role selection, ping indicators, and starting the match are out of scope for Story 3.2.
    /// </summary>
    public class LobbyUI : MonoBehaviour
    {
        /// <summary>
        /// Local-only status for THIS client's own in-progress join attempt. Per ARCHITECTURE_RULES.md,
        /// this never gets a shared-list representation — a join that never succeeds never produces an
        /// entry for anyone else to see. Once our own entry exists in the shared list, that presence
        /// itself is "Connected," so this enum has no Connected state of its own.
        /// </summary>
        private enum LocalJoinStatus
        {
            Idle,
            Connecting,
            Failed
        }

        [Header("Wiring")]
        [SerializeField] private NetworkManagerExt networkManagerExt;
        [SerializeField] private NetworkSessionManager sessionManager;

        [Header("Host / Join Controls")]
        [SerializeField] private Button hostButton;
        [SerializeField] private Button joinButton;
        [SerializeField] private TMP_InputField joinCodeInputField;

        [Header("Status")]
        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private TMP_InputField joinCodeDisplayField;
        [SerializeField] private Button copyCodeButton;
        [SerializeField] private TextMeshProUGUI copyButtonText;
        [SerializeField] private TextMeshProUGUI allReadyText;

        [Header("Player List")]
        [SerializeField] private Transform playerListContainer;

        private LocalJoinStatus localJoinStatus = LocalJoinStatus.Idle;
        private string localJoinMessage = string.Empty;
        private bool subscribedToRoster;
        private readonly List<GameObject> spawnedRows = new List<GameObject>();
        private Coroutine copyFeedbackCoroutine;
        private Coroutine connectionTimeoutCoroutine;

        /// <summary>Story 3.6: true once our own roster entry has ever appeared, i.e. we were genuinely
        /// in a session rather than merely attempting (and failing) to join one.</summary>
        private bool hasEverJoinedRoster;

        /// <summary>Story 3.6: which role we last successfully started as, so a disconnect can tell
        /// "I was a Client and the Host vanished" apart from "I was the Host myself".</summary>
        private bool wasHost;

        private void Awake()
        {
            // TLS-E03-S01: this entire Canvas is a client-only view (ARCHITECTURE_RULES.md Rule 2/4) —
            // a Dedicated Server has no Host/Join buttons to click and must not run this UI at all.
            if (GameBootstrap.CurrentMode == LaunchMode.DedicatedServer)
            {
                gameObject.SetActive(false);
                return;
            }

            if (networkManagerExt == null)
            {
                networkManagerExt = FindAnyObjectByType<NetworkManagerExt>();
            }

            if (sessionManager == null)
            {
                sessionManager = FindAnyObjectByType<NetworkSessionManager>();
            }

            if (hostButton != null)
            {
                hostButton.onClick.AddListener(OnHostButtonPressed);
            }

            if (joinButton != null)
            {
                joinButton.onClick.AddListener(OnJoinButtonPressed);
            }

            if (joinCodeInputField != null)
            {
                joinCodeInputField.characterLimit = 12;
                joinCodeInputField.onValueChanged.AddListener(OnJoinCodeInputChanged);
                joinCodeInputField.onSubmit.AddListener(_ => OnJoinButtonPressed());
            }

            if (copyCodeButton != null)
            {
                copyCodeButton.onClick.AddListener(OnCopyCodeButtonPressed);
            }
            else if (joinCodeDisplayField != null)
            {
                CreateDynamicCopyButton();
            }

            if (joinCodeDisplayField != null)
            {
                joinCodeDisplayField.readOnly = true;
                joinCodeDisplayField.onSelect.AddListener(_ => OnCopyCodeButtonPressed());
            }

            SubscribeToRoster();
            RefreshPlayerList();
            RefreshStatusText();
        }

        private void Start()
        {
            // All Awake calls in the scene finish before any Start call, so NetworkManager.Singleton
            // (set in NetworkManager's own Awake) is guaranteed non-null here.
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientDisconnectCallback += HandleLocalDisconnected;
            }
        }

        private void Update()
        {
            // Cheap label refresh only (an enum + a couple of string reads) - NOT the player list,
            // which is redrawn exclusively from NetworkSessionManager.Players.OnListChanged below.
            RefreshStatusText();
        }

        private void OnDestroy()
        {
            if (hostButton != null)
            {
                hostButton.onClick.RemoveListener(OnHostButtonPressed);
            }

            if (joinButton != null)
            {
                joinButton.onClick.RemoveListener(OnJoinButtonPressed);
            }

            if (joinCodeInputField != null)
            {
                joinCodeInputField.onValueChanged.RemoveListener(OnJoinCodeInputChanged);
            }

            if (copyCodeButton != null)
            {
                copyCodeButton.onClick.RemoveListener(OnCopyCodeButtonPressed);
            }

            if (subscribedToRoster && sessionManager != null)
            {
                sessionManager.Players.OnListChanged -= HandlePlayersChanged;
            }

            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientDisconnectCallback -= HandleLocalDisconnected;
            }
        }

        private void SubscribeToRoster()
        {
            if (subscribedToRoster || sessionManager == null)
            {
                return;
            }

            sessionManager.Players.OnListChanged += HandlePlayersChanged;
            subscribedToRoster = true;
        }

        // ---------------------------------------------------------------- Host

        /// <summary>Bound to the "Host" button.</summary>
        public async void OnHostButtonPressed()
        {
            if (networkManagerExt == null || !CanAttemptConnection())
            {
                return;
            }

            SetLocalStatus(LocalJoinStatus.Connecting, "Allocating Relay...");
            SetControlsInteractable(false);

            try
            {
                string joinCode = await networkManagerExt.CreateRelay();

                if (!networkManagerExt.StartHost())
                {
                    SetLocalStatus(LocalJoinStatus.Failed, "Could not start Host.");
                    SetControlsInteractable(true);
                    return;
                }

                wasHost = true;

                // The Host's own roster entry appears via NetworkSessionManager's
                // OnClientConnectedCallback hookup, which fires for the Host's local connection too -
                // no separate "add myself" call is needed here.
                SetLocalStatus(LocalJoinStatus.Idle, string.Empty);

                if (joinCodeDisplayField != null)
                {
                    joinCodeDisplayField.text = joinCode.ToUpperInvariant();
                    joinCodeDisplayField.pointSize = 20;
                }
            }
            catch (RelayServiceException relayEx)
            {
                SetLocalStatus(LocalJoinStatus.Failed, $"Relay error ({relayEx.Reason}): {relayEx.Message}");
                SetControlsInteractable(true);
            }
            catch (Exception ex)
            {
                SetLocalStatus(LocalJoinStatus.Failed, $"Host failed: {ex.Message}");
                SetControlsInteractable(true);
            }
        }

        // ---------------------------------------------------------------- Join

        /// <summary>Bound to the "Join" button.</summary>
        public async void OnJoinButtonPressed()
        {
            if (networkManagerExt == null || joinCodeInputField == null || !CanAttemptConnection())
            {
                return;
            }

            string code = joinCodeInputField.text?.Trim().ToUpperInvariant();
            if (string.IsNullOrEmpty(code))
            {
                SetLocalStatus(LocalJoinStatus.Failed, "Please enter a room code.");
                return;
            }

            SetLocalStatus(LocalJoinStatus.Connecting, "Connecting...");
            SetControlsInteractable(false);

            try
            {
                await networkManagerExt.JoinRelay(code);

                if (connectionTimeoutCoroutine != null)
                {
                    StopCoroutine(connectionTimeoutCoroutine);
                }
                connectionTimeoutCoroutine = StartCoroutine(ConnectionTimeoutRoutine());

                if (!networkManagerExt.StartClient())
                {
                    if (connectionTimeoutCoroutine != null)
                    {
                        StopCoroutine(connectionTimeoutCoroutine);
                        connectionTimeoutCoroutine = null;
                    }

                    SetLocalStatus(LocalJoinStatus.Failed, "Could not start Client.");
                    SetControlsInteractable(true);
                    return;
                }

                wasHost = false;

                // Deliberately still "Connecting": we only clear it once our own entry appears in the
                // shared roster (HandlePlayersChanged) - that presence itself is "Connected."
            }
            catch (RelayServiceException relayEx)
            {
                if (connectionTimeoutCoroutine != null)
                {
                    StopCoroutine(connectionTimeoutCoroutine);
                    connectionTimeoutCoroutine = null;
                }

                string friendlyMessage = ParseRelayException(relayEx);
                SetLocalStatus(LocalJoinStatus.Failed, friendlyMessage);
                SetControlsInteractable(true);
            }
            catch (Exception ex)
            {
                if (connectionTimeoutCoroutine != null)
                {
                    StopCoroutine(connectionTimeoutCoroutine);
                    connectionTimeoutCoroutine = null;
                }

                string friendlyMessage = ParseGenericException(ex);
                SetLocalStatus(LocalJoinStatus.Failed, friendlyMessage);
                SetControlsInteractable(true);
            }
        }

        private void HandleLocalDisconnected(ulong clientId)
        {
            if (connectionTimeoutCoroutine != null)
            {
                StopCoroutine(connectionTimeoutCoroutine);
                connectionTimeoutCoroutine = null;
            }

            // A client is only ever notified about its OWN disconnection, so no id check is needed here.
            // Catches a join attempt that never produced a roster entry (bad code, timeout, refusal).
            if (localJoinStatus == LocalJoinStatus.Connecting)
            {
                string reason = NetworkManager.Singleton != null ? NetworkManager.Singleton.DisconnectReason : string.Empty;
                if (!string.IsNullOrEmpty(reason))
                {
                    if (reason.Contains("disconnected by server") || reason.IndexOf("full", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        SetLocalStatus(LocalJoinStatus.Failed, "Room is full (maximum 4 players).");
                    }
                    else
                    {
                        SetLocalStatus(LocalJoinStatus.Failed, reason);
                    }
                }
                else
                {
                    SetLocalStatus(LocalJoinStatus.Failed, "Room is full (maximum 4 players).");
                }

                networkManagerExt?.Shutdown();
            }
            else if (hasEverJoinedRoster && !wasHost)
            {
                // Story 3.6: we were a fully-connected Client with a real roster entry and just lost
                // our connection mid-session. This project has no Host Migration
                // (ARCHITECTURE_RULES.md), so the only thing that can cause this is the Host
                // disconnecting (closed the window, crashed, lost network, etc.) — treat every such
                // loss identically, whether it looked "clean" or not, per the story's requirement.
                GameManager.Instance?.HandleHostDisconnected();
                SetLocalStatus(LocalJoinStatus.Failed, "Host disconnected — session ended. You can host or join a new game.");
                networkManagerExt?.Shutdown();
            }

            // Whatever branch above ran, our view of the roster is now stale/inaccessible - never try
            // to read sessionManager.Players again here, just wipe the displayed rows directly so no
            // "ghost" row lingers on screen after we ourselves have left.
            ClearPlayerListDisplay();
            hasEverJoinedRoster = false;
            wasHost = false;
            SetControlsInteractable(true);
        }

        private void ClearPlayerListDisplay()
        {
            for (int i = 0; i < spawnedRows.Count; i++)
            {
                if (spawnedRows[i] != null)
                {
                    Destroy(spawnedRows[i]);
                }
            }

            spawnedRows.Clear();
        }

        private IEnumerator ConnectionTimeoutRoutine()
        {
            yield return new WaitForSeconds(10f);
            if (localJoinStatus == LocalJoinStatus.Connecting)
            {
                Debug.LogWarning("[LobbyUI] Connection attempt timed out after 10 seconds.");
                networkManagerExt?.Shutdown();
                SetLocalStatus(LocalJoinStatus.Failed, "Connection timed out. Please check room code or network.");
                SetControlsInteractable(true);
            }
            connectionTimeoutCoroutine = null;
        }

        private bool CanAttemptConnection()
        {
            return localJoinStatus != LocalJoinStatus.Connecting
                   && GameManager.Instance != null
                   && GameManager.Instance.IsSignedIn
                   && NetworkManager.Singleton != null
                   && !NetworkManager.Singleton.IsListening;
        }

        // ---------------------------------------------------------------- Roster / player list

        private void HandlePlayersChanged(NetworkListEvent<LobbyPlayerData> changeEvent)
        {
            RefreshPlayerList();

            if (HasLocalEntry())
            {
                // Story 3.6: marks us as a genuine session participant so a later disconnect is
                // correctly attributed to "the Host disconnected" rather than "our join failed".
                hasEverJoinedRoster = true;
            }

            if (localJoinStatus == LocalJoinStatus.Connecting && HasLocalEntry())
            {
                if (connectionTimeoutCoroutine != null)
                {
                    StopCoroutine(connectionTimeoutCoroutine);
                    connectionTimeoutCoroutine = null;
                }

                SetLocalStatus(LocalJoinStatus.Idle, string.Empty);
                SetControlsInteractable(true);
            }
        }

        private bool HasLocalEntry()
        {
            if (sessionManager == null || NetworkManager.Singleton == null)
            {
                return false;
            }

            ulong localId = NetworkManager.Singleton.LocalClientId;
            NetworkList<LobbyPlayerData> list = sessionManager.Players;

            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].ClientId == localId)
                {
                    return true;
                }
            }

            return false;
        }

        private void RefreshPlayerList()
        {
            for (int i = 0; i < spawnedRows.Count; i++)
            {
                if (spawnedRows[i] != null)
                {
                    Destroy(spawnedRows[i]);
                }
            }

            spawnedRows.Clear();

            if (playerListContainer == null || sessionManager == null)
            {
                return;
            }

            ulong localId = NetworkManager.Singleton != null ? NetworkManager.Singleton.LocalClientId : ulong.MaxValue;
            NetworkList<LobbyPlayerData> list = sessionManager.Players;

            for (int i = 0; i < list.Count; i++)
            {
                LobbyPlayerData data = list[i];
                spawnedRows.Add(CreatePlayerRow(data, data.ClientId == localId));
            }

            RefreshAllReadyText();
        }

        private GameObject CreatePlayerRow(LobbyPlayerData data, bool isLocal)
        {
            GameObject row = new GameObject($"Row_Client{data.ClientId}", typeof(RectTransform));
            row.transform.SetParent(playerListContainer, false);
            row.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 36f);

            HorizontalLayoutGroup layout = row.AddComponent<HorizontalLayoutGroup>();
            layout.childForceExpandWidth = false;
            layout.childControlWidth = false;
            layout.spacing = 12f;
            layout.childAlignment = TextAnchor.MiddleLeft;

            string nameLabel = isLocal ? $"Client {data.ClientId} (You)" : $"Client {data.ClientId}";
            CreateRowLabel(row.transform, nameLabel, 220f, Color.white);
            CreateRowLabel(row.transform, data.IsReady ? "Ready" : "Not Ready", 110f,
                data.IsReady ? new Color(0.3f, 1f, 0.4f) : Color.gray);

            // Only the local player's own row gets an interactive control - you can't toggle anyone
            // else's readiness (ARCHITECTURE_RULES.md Rule 3: input only ever changes YOUR own state).
            if (isLocal)
            {
                CreateReadyToggle(row.transform, data.IsReady);
            }

            return row;
        }

        private void CreateRowLabel(Transform parent, string text, float width, Color color)
        {
            GameObject go = new GameObject("Label", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            go.AddComponent<LayoutElement>().preferredWidth = width;

            TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = 24;
            tmp.color = color;
            tmp.raycastTarget = false;
            tmp.alignment = TextAlignmentOptions.MidlineLeft;
        }

        private void CreateReadyToggle(Transform parent, bool currentValue)
        {
            GameObject toggleGo = new GameObject("ReadyToggle", typeof(RectTransform));
            toggleGo.transform.SetParent(parent, false);
            toggleGo.AddComponent<LayoutElement>().preferredWidth = 150f;

            HorizontalLayoutGroup rowLayout = toggleGo.AddComponent<HorizontalLayoutGroup>();
            rowLayout.spacing = 6f;
            rowLayout.childForceExpandWidth = false;
            rowLayout.childControlWidth = false;
            rowLayout.childAlignment = TextAnchor.MiddleLeft;

            Toggle toggle = toggleGo.AddComponent<Toggle>();

            GameObject background = new GameObject("Background", typeof(RectTransform), typeof(Image));
            background.transform.SetParent(toggleGo.transform, false);
            background.GetComponent<RectTransform>().sizeDelta = new Vector2(24f, 24f);
            Image bgImage = background.GetComponent<Image>();
            bgImage.color = new Color(1f, 1f, 1f, 0.25f);
            toggle.targetGraphic = bgImage;

            GameObject checkmark = new GameObject("Checkmark", typeof(RectTransform), typeof(Image));
            checkmark.transform.SetParent(background.transform, false);
            checkmark.GetComponent<RectTransform>().sizeDelta = new Vector2(18f, 18f);
            Image checkImage = checkmark.GetComponent<Image>();
            checkImage.color = new Color(0.2f, 1f, 0.4f, 1f);
            toggle.graphic = checkImage;

            CreateRowLabel(toggleGo.transform, "Ready?", 90f, Color.white);

            // Set the starting value without notifying so rebuilding rows (triggered by our own
            // toggle click round-tripping through the Host) never fires a spurious extra RPC.
            toggle.SetIsOnWithoutNotify(currentValue);
            toggle.onValueChanged.AddListener(OnLocalReadyToggled);
        }

        private void OnLocalReadyToggled(bool isReady)
        {
            // Input goes up via ServerRpc; we never set our own ready flag directly on shared state
            // (ARCHITECTURE_RULES.md Rule 3). The Host validates and applies it, and the resulting
            // NetworkList change is what actually redraws this toggle.
            sessionManager?.SubmitReadyStateServerRpc(isReady);
        }

        private void RefreshAllReadyText()
        {
            if (allReadyText == null || sessionManager == null)
            {
                return;
            }

            bool isHost = NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer;

            // Informational only - Story 3.2 explicitly does not act on AllPlayersReady.
            allReadyText.text = isHost
                ? $"All Players Ready: {(sessionManager.AllPlayersReady ? "Yes" : "No")}"
                : string.Empty;
        }

        // ---------------------------------------------------------------- Status / shared helpers

        private void SetLocalStatus(LocalJoinStatus status, string message)
        {
            localJoinStatus = status;
            localJoinMessage = message;
            RefreshStatusText();
        }

        private void SetControlsInteractable(bool interactable)
        {
            if (hostButton != null)
            {
                hostButton.interactable = interactable;
            }

            if (joinButton != null)
            {
                joinButton.interactable = interactable;
            }
        }

        private void RefreshStatusText()
        {
            if (statusText == null)
            {
                return;
            }

            string servicesLine = GameManager.Instance != null
                ? $"Services: {GameManager.Instance.ServicesState}"
                : "Services: (no GameManager)";

            string joinLine;
            switch (localJoinStatus)
            {
                case LocalJoinStatus.Connecting:
                    joinLine = "<color=#FFFF55>Connecting...</color>";
                    break;
                case LocalJoinStatus.Failed:
                    joinLine = $"<color=#FF5555>Failed: {localJoinMessage}</color>";
                    break;
                default:
                    joinLine = string.Empty;
                    break;
            }

            statusText.text = string.IsNullOrEmpty(joinLine) ? servicesLine : $"{servicesLine}\n{joinLine}";
        }

        private string ParseRelayException(RelayServiceException relayEx)
        {
            // Case 1: Wrong or non-existent code
            if (relayEx.Reason == RelayExceptionReason.JoinCodeNotFound ||
                relayEx.Reason == RelayExceptionReason.EntityNotFound ||
                relayEx.Reason == RelayExceptionReason.InvalidArgument ||
                relayEx.Reason == RelayExceptionReason.InvalidRequest)
            {
                return "Room code does not exist or is invalid. Please check and try again.";
            }

            // Case 2: Expired code
            if (relayEx.Reason == RelayExceptionReason.AllocationNotFound ||
                relayEx.Reason == RelayExceptionReason.Gone ||
                relayEx.Reason == RelayExceptionReason.RequestTimeOut)
            {
                return "Room code has expired or the session has ended.";
            }

            // Case 3: Room full (4 players)
            if (relayEx.Reason == RelayExceptionReason.Conflict ||
                relayEx.Reason == RelayExceptionReason.PreconditionFailed ||
                relayEx.Reason == RelayExceptionReason.NoSuitableRelay)
            {
                return "Room is full (maximum 4 players).";
            }

            string msg = relayEx.Message != null ? relayEx.Message.ToLowerInvariant() : string.Empty;
            if (msg.Contains("full") || msg.Contains("capacity") || msg.Contains("limit") || msg.Contains("exceeded"))
            {
                return "Room is full (maximum 4 players).";
            }
            if (msg.Contains("not found") || msg.Contains("invalid") || msg.Contains("bad"))
            {
                return "Room code does not exist or is invalid. Please check and try again.";
            }
            if (msg.Contains("expire") || msg.Contains("timeout"))
            {
                return "Room code has expired or the session has ended.";
            }

            return $"Relay error ({relayEx.Reason}): {relayEx.Message}";
        }

        private string ParseGenericException(Exception ex)
        {
            string msg = ex.Message != null ? ex.Message.ToLowerInvariant() : string.Empty;
            if (msg.Contains("full") || msg.Contains("capacity") || msg.Contains("limit") || msg.Contains("exceeded"))
            {
                return "Room is full (maximum 4 players).";
            }
            if (msg.Contains("not found") || msg.Contains("invalid") || msg.Contains("empty"))
            {
                return "Room code does not exist or is invalid. Please check and try again.";
            }
            if (msg.Contains("expire") || msg.Contains("timeout"))
            {
                return "Room code has expired or the session has ended.";
            }

            return $"Join failed: {ex.Message}";
        }

        private void OnJoinCodeInputChanged(string val)
        {
            if (val == null || joinCodeInputField == null) return;
            string upper = val.ToUpperInvariant();
            if (val != upper)
            {
                joinCodeInputField.text = upper;
            }
        }

        public void OnCopyCodeButtonPressed()
        {
            string code = joinCodeDisplayField != null ? joinCodeDisplayField.text : networkManagerExt?.JoinCode;
            if (string.IsNullOrWhiteSpace(code))
            {
                return;
            }

            GUIUtility.systemCopyBuffer = code.Trim();
            Debug.Log($"[LobbyUI] Room code copied to clipboard: {code.Trim()}");

            if (copyFeedbackCoroutine != null)
            {
                StopCoroutine(copyFeedbackCoroutine);
            }
            copyFeedbackCoroutine = StartCoroutine(CopyFeedbackRoutine());
        }

        private IEnumerator CopyFeedbackRoutine()
        {
            if (copyButtonText != null)
            {
                string original = copyButtonText.text;
                Color originalColor = copyButtonText.color;
                copyButtonText.text = "Copied!";
                copyButtonText.color = new Color(0.3f, 1f, 0.4f);
                yield return new WaitForSeconds(2f);
                copyButtonText.text = original;
                copyButtonText.color = originalColor;
            }
        }

        private void CreateDynamicCopyButton()
        {
            if (joinCodeDisplayField == null) return;

            RectTransform fieldRect = joinCodeDisplayField.GetComponent<RectTransform>();
            Transform parent = fieldRect.parent;
            if (parent == null) return;

            Vector2 fieldPos = fieldRect.anchoredPosition;
            Vector2 fieldSize = fieldRect.sizeDelta;
            fieldRect.sizeDelta = new Vector2(Mathf.Max(120f, fieldSize.x - 70f), fieldSize.y);

            GameObject buttonGo = new GameObject("Btn_CopyCode", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            buttonGo.transform.SetParent(parent, false);

            RectTransform btnRect = buttonGo.GetComponent<RectTransform>();
            btnRect.anchorMin = fieldRect.anchorMin;
            btnRect.anchorMax = fieldRect.anchorMax;
            btnRect.pivot = fieldRect.pivot;
            btnRect.anchoredPosition = new Vector2(fieldPos.x + fieldRect.sizeDelta.x + 8f, fieldPos.y);
            btnRect.sizeDelta = new Vector2(62f, fieldSize.y);

            Image img = buttonGo.GetComponent<Image>();
            img.color = new Color(0.2f, 0.4f, 0.8f, 1f);

            copyCodeButton = buttonGo.GetComponent<Button>();
            copyCodeButton.targetGraphic = img;
            copyCodeButton.onClick.AddListener(OnCopyCodeButtonPressed);

            GameObject textGo = new GameObject("Text", typeof(RectTransform));
            textGo.transform.SetParent(buttonGo.transform, false);
            RectTransform textRect = textGo.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;

            copyButtonText = textGo.AddComponent<TextMeshProUGUI>();
            copyButtonText.text = "Copy";
            copyButtonText.fontSize = 15;
            copyButtonText.fontStyle = FontStyles.Bold;
            copyButtonText.alignment = TextAlignmentOptions.Center;
            copyButtonText.color = Color.white;
            copyButtonText.raycastTarget = false;
        }
    }
}
