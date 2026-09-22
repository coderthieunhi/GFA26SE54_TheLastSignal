using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using TheLastSignal.Core;

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
        [SerializeField] private TextMeshProUGUI allReadyText;

        [Header("Player List")]
        [SerializeField] private Transform playerListContainer;

        private LocalJoinStatus localJoinStatus = LocalJoinStatus.Idle;
        private string localJoinMessage = string.Empty;
        private bool subscribedToRoster;
        private readonly List<GameObject> spawnedRows = new List<GameObject>();

        private void Awake()
        {
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

                // The Host's own roster entry appears via NetworkSessionManager's
                // OnClientConnectedCallback hookup, which fires for the Host's local connection too -
                // no separate "add myself" call is needed here.
                SetLocalStatus(LocalJoinStatus.Idle, string.Empty);

                if (joinCodeDisplayField != null)
                {
                    joinCodeDisplayField.text = joinCode;
                }
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

            string code = joinCodeInputField.text;
            SetLocalStatus(LocalJoinStatus.Connecting, "Connecting...");
            SetControlsInteractable(false);

            try
            {
                await networkManagerExt.JoinRelay(code);

                if (!networkManagerExt.StartClient())
                {
                    SetLocalStatus(LocalJoinStatus.Failed, "Could not start Client.");
                    SetControlsInteractable(true);
                    return;
                }

                // Deliberately still "Connecting": we only clear it once our own entry appears in the
                // shared roster (HandlePlayersChanged) - that presence itself is "Connected."
            }
            catch (Exception ex)
            {
                SetLocalStatus(LocalJoinStatus.Failed, $"Join failed: {ex.Message}");
                SetControlsInteractable(true);
            }
        }

        private void HandleLocalDisconnected(ulong clientId)
        {
            // A client is only ever notified about its OWN disconnection, so no id check is needed here.
            // Catches a join attempt that never produced a roster entry (bad code, timeout, refusal).
            if (localJoinStatus == LocalJoinStatus.Connecting)
            {
                SetLocalStatus(LocalJoinStatus.Failed, "Disconnected before joining.");
            }

            SetControlsInteractable(true);
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

            if (localJoinStatus == LocalJoinStatus.Connecting && HasLocalEntry())
            {
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
                    joinLine = "Connecting...";
                    break;
                case LocalJoinStatus.Failed:
                    joinLine = $"Failed: {localJoinMessage}";
                    break;
                default:
                    joinLine = string.Empty;
                    break;
            }

            statusText.text = string.IsNullOrEmpty(joinLine) ? servicesLine : $"{servicesLine}\n{joinLine}";
        }
    }
}
