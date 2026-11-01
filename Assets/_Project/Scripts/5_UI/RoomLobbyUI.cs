using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using TheLastSignal.Core;
using TheLastSignal.Core.Backend;
using TheLastSignal.Core.Bootstrap;

namespace TheLastSignal.UI
{
    /// <summary>
    /// Giao diện Phòng chờ (Lobby UI) kết nối trực tiếp với Spring Boot Backend:
    /// 1. Màn hình Menu: Nhập tên, Tạo phòng (nhận mã 6 ký tự), Nhập mã để Vào phòng.
    /// 2. Màn hình Phòng chờ: Hiển thị mã phòng, Copy mã, Danh sách người chơi, Ready toggle, Host Start, Rời phòng.
    /// 3. Xử lý ngoại lệ đầy đủ: Mã sai, phòng đầy, chưa ready, mất mạng, server timeout.
    /// 4. Chuyển tiếp tự động vào trận đấu khi Host bấm Start (kết nối Netcode vào Dedicated Server).
    /// </summary>
    public class RoomLobbyUI : MonoBehaviour
    {
        [Header("Backend & Network Wiring")]
        [SerializeField] private RoomManager roomManager;
        [SerializeField] private NetworkManagerExt networkManagerExt;

        [Header("Configuration")]
        [SerializeField] private bool autoBuildUiIfMissing = true;

        // UI references (tự động tạo qua code nếu để trống trong Inspector)
        private GameObject menuPanel;
        private GameObject roomPanel;
        private GameObject errorBanner;

        private TMP_InputField nameInputField;
        private TMP_InputField serverUrlInputField;
        private TMP_InputField joinCodeInputField;
        private Button createRoomButton;
        private Button joinRoomButton;

        private TextMeshProUGUI roomCodeDisplay;
        private Button copyCodeButton;
        private TextMeshProUGUI copyButtonText;
        private TextMeshProUGUI playerCountText;
        private Transform playerListContainer;
        private Button readyButton;
        private TextMeshProUGUI readyButtonText;
        private Button startMatchButton;
        private TextMeshProUGUI startButtonText;
        private Button leaveButton;

        private TextMeshProUGUI statusText;
        private TextMeshProUGUI errorBannerText;

        private readonly List<GameObject> spawnedPlayerRows = new List<GameObject>();
        private Coroutine copyFeedbackCoroutine;
        private Coroutine errorBannerCoroutine;
        private bool isBusy = false;

        private const string DisplayNamePrefKey = "TLS_PlayerDisplayName";
        private GameObject legacyLobbyPanel;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoEnsureInScene()
        {
            if (GameBootstrap.CurrentMode == LaunchMode.DedicatedServer) return;

#if UNITY_EDITOR
            // ARCHITECTURE_RULES.md Rule 7 (preserve legacy mode): lets a solo Play session skip the
            // backend-requiring RoomLobbyUI entirely and fall through to the legacy LobbyPanel +
            // GameManager.startSessionOnPlay, exactly like before RoomLobbyUI existed. See
            // "TheLastSignal/Launch Mode/Skip Online Lobby On Next Play".
            if (UnityEditor.SessionState.GetBool(EditorSkipLobbyKey, false))
            {
                Debug.Log("[RoomLobbyUI] Editor override active: skipping auto-spawn for this Play session " +
                          "(TheLastSignal/Launch Mode/Skip Online Lobby On Next Play).");
                return;
            }
#endif

            RoomLobbyUI existing = FindAnyObjectByType<RoomLobbyUI>(FindObjectsInactive.Include);
            if (existing != null) return;

            Canvas canvas = null;
            GameObject lobbyCanvasGo = GameObject.Find("LobbyCanvas");
            if (lobbyCanvasGo != null)
            {
                canvas = lobbyCanvasGo.GetComponent<Canvas>();
            }

            if (canvas == null)
            {
                canvas = FindAnyObjectByType<Canvas>(FindObjectsInactive.Include);
            }

            GameObject uiGo = new GameObject("RoomLobbyUI", typeof(RectTransform));
            if (canvas != null)
            {
                uiGo.transform.SetParent(canvas.transform, false);
            }
            uiGo.AddComponent<RoomLobbyUI>();
        }

        private void Awake()
        {
            // Dedicated Server không hiển thị UI này
            if (GameBootstrap.CurrentMode == LaunchMode.DedicatedServer)
            {
                gameObject.SetActive(false);
                return;
            }

            legacyLobbyPanel = GameObject.Find("LobbyPanel");

            if (roomManager == null)
            {
                roomManager = FindAnyObjectByType<RoomManager>();
                if (roomManager == null)
                {
                    GameObject rmGo = new GameObject("RoomManager");
                    roomManager = rmGo.AddComponent<RoomManager>();
                }
            }

            if (networkManagerExt == null)
            {
                networkManagerExt = FindAnyObjectByType<NetworkManagerExt>();
            }

            if (autoBuildUiIfMissing && menuPanel == null)
            {
                BuildRuntimeUi();
            }

            LoadSavedDisplayName();
            ShowMenuView();

            // Nếu có Legacy LobbyPanel, tạm ẩn để hiển thị RoomLobbyUI
            if (legacyLobbyPanel != null && legacyLobbyPanel.activeSelf)
            {
                legacyLobbyPanel.SetActive(false);
            }
        }

        private void Start()
        {
            if (roomManager != null)
            {
                roomManager.OnRoomJoined += HandleRoomJoined;
                roomManager.OnRoomUpdated += HandleRoomUpdated;
                roomManager.OnMatchStarting += HandleMatchStarting;
                roomManager.OnRoomLeft += HandleRoomLeft;
                roomManager.OnError += HandleErrorReceived;
            }

            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientConnectedCallback += HandleNetcodeConnected;
                NetworkManager.Singleton.OnClientDisconnectCallback += HandleNetcodeDisconnected;
            }
        }

        private void OnDestroy()
        {
            if (roomManager != null)
            {
                roomManager.OnRoomJoined -= HandleRoomJoined;
                roomManager.OnRoomUpdated -= HandleRoomUpdated;
                roomManager.OnMatchStarting -= HandleMatchStarting;
                roomManager.OnRoomLeft -= HandleRoomLeft;
                roomManager.OnError -= HandleErrorReceived;
            }

            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientConnectedCallback -= HandleNetcodeConnected;
                NetworkManager.Singleton.OnClientDisconnectCallback -= HandleNetcodeDisconnected;
            }
        }

        // ---------------------------------------------------------------- Hành động người chơi

        /// <summary>
        /// When GameManager.startSessionOnPlay is on (the legacy default - ARCHITECTURE_RULES.md Rule 7),
        /// GameManager.Start() (DefaultExecutionOrder -100, runs before this component's own Start)
        /// already calls StartSession() locally whenever no NGO session is listening, so a playable
        /// single-player session is usually already running underneath this full-screen canvas the
        /// moment Play begins. This button also calls StartSession() itself if nothing is playing yet
        /// (GameManager.IsPlaying false) so solo play works even if that Inspector flag is off, with
        /// zero backend/room-code/network involvement either way.
        /// </summary>
        private void OnPlaySoloClicked()
        {
            if (GameManager.Instance != null && !GameManager.Instance.IsPlaying)
            {
                Debug.Log("[RoomLobbyUI] Chơi một mình (Offline): chưa có session nào chạy, tự khởi động StartSession().");
                GameManager.Instance.StartSession();
            }

            Debug.Log("[RoomLobbyUI] Chơi một mình (Offline): ẩn Lobby để lộ ra session local.");
            gameObject.SetActive(false);
        }

        private async void OnCreateRoomClicked()
        {
            if (isBusy) return;

            if (serverUrlInputField != null && !string.IsNullOrWhiteSpace(serverUrlInputField.text) && roomManager != null)
            {
                roomManager.BackendUrl = serverUrlInputField.text.Trim();
            }

            string displayName = nameInputField != null ? nameInputField.text.Trim() : string.Empty;
            if (string.IsNullOrEmpty(displayName))
            {
                ShowErrorMessage("Vui lòng nhập tên người chơi trước khi tạo phòng.");
                return;
            }

            SaveDisplayName(displayName);
            SetBusy(true, "Đang khởi tạo phòng mới...");

            bool success = await roomManager.CreateRoomAsync(displayName);
            if (!success)
            {
                SetBusy(false);
            }
        }

        private async void OnJoinRoomClicked()
        {
            if (isBusy) return;

            if (serverUrlInputField != null && !string.IsNullOrWhiteSpace(serverUrlInputField.text) && roomManager != null)
            {
                roomManager.BackendUrl = serverUrlInputField.text.Trim();
            }

            string displayName = nameInputField != null ? nameInputField.text.Trim() : string.Empty;
            string roomCode = joinCodeInputField != null ? joinCodeInputField.text.Trim().ToUpperInvariant() : string.Empty;

            if (string.IsNullOrEmpty(displayName))
            {
                ShowErrorMessage("Vui lòng nhập tên người chơi trước khi vào phòng.");
                return;
            }

            if (string.IsNullOrEmpty(roomCode) || roomCode.Length != 6)
            {
                ShowErrorMessage("Mã phòng phải gồm đúng 6 ký tự.");
                return;
            }

            SaveDisplayName(displayName);
            SetBusy(true, $"Đang tham gia phòng {roomCode}...");

            bool success = await roomManager.JoinRoomAsync(roomCode, displayName);
            if (!success)
            {
                SetBusy(false);
            }
        }

        private async void OnReadyToggleClicked()
        {
            if (isBusy || !roomManager.IsInRoom) return;

            SetBusy(true, "Đang cập nhật trạng thái...");
            await roomManager.ToggleReadyAsync();
            SetBusy(false);
        }

        private async void OnStartMatchClicked()
        {
            if (isBusy || !roomManager.IsInRoom || !roomManager.IsHost) return;

            SetBusy(true, "Đang khởi động trận đấu...");
            bool success = await roomManager.StartRoomAsync();
            if (!success)
            {
                SetBusy(false);
            }
        }

        private async void OnLeaveRoomClicked()
        {
            if (isBusy) return;

            SetBusy(true, "Đang rời phòng...");
            await roomManager.LeaveRoomAsync();
            SetBusy(false);
        }

        private void OnCopyCodeClicked()
        {
            string code = roomManager.CurrentRoomCode;
            if (string.IsNullOrEmpty(code)) return;

            GUIUtility.systemCopyBuffer = code;
            Debug.Log($"[RoomLobbyUI] Đã chép mã phòng vào clipboard: {code}");

            if (copyFeedbackCoroutine != null) StopCoroutine(copyFeedbackCoroutine);
            copyFeedbackCoroutine = StartCoroutine(CopyFeedbackRoutine());
        }

        private IEnumerator CopyFeedbackRoutine()
        {
            if (copyButtonText != null)
            {
                string original = copyButtonText.text;
                copyButtonText.text = "Đã chép!";
                yield return new WaitForSeconds(2f);
                copyButtonText.text = original;
            }
        }

        // ---------------------------------------------------------------- Callback từ RoomManager

        private void HandleRoomJoined(RoomDto room)
        {
            SetBusy(false);
            ShowRoomView();
            RefreshRoomView(room);
        }

        private void HandleRoomUpdated(RoomDto room)
        {
            RefreshRoomView(room);
        }

        private void HandleMatchStarting(RoomDto room)
        {
            SetBusy(true, "Trận đấu đang bắt đầu! Đang kết nối vào máy chủ...");

            string host = !string.IsNullOrEmpty(room.serverHost) ? room.serverHost : "127.0.0.1";
            ushort port = (ushort)(room.serverPort > 0 ? room.serverPort : 7777);

            Debug.Log($"[RoomLobbyUI] Kết nối vào Dedicated Game Server: {host}:{port}");

            if (string.IsNullOrEmpty(room.yourJoinToken))
            {
                Debug.LogError("[RoomLobbyUI] yourJoinToken is missing - cannot connect without a real join token.");
                ShowErrorMessage("Không nhận được mã kết nối (join token) từ máy chủ. Vui lòng thử lại.");
                SetBusy(false);
                return;
            }

            if (networkManagerExt != null)
            {
                byte[] tokenBytes = Encoding.UTF8.GetBytes(room.yourJoinToken);

                bool started = networkManagerExt.ConnectDirect(host, port, tokenBytes);
                if (!started)
                {
                    ShowErrorMessage($"Không thể kích hoạt kết nối tới {host}:{port}.");
                    SetBusy(false);
                }
            }
            else if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.StartClient();
            }
        }

        private void HandleRoomLeft()
        {
            SetBusy(false);
            ShowMenuView();
        }

        private void HandleErrorReceived(string errorMessage)
        {
            SetBusy(false);
            ShowErrorMessage(errorMessage);
        }

        private void HandleNetcodeConnected(ulong clientId)
        {
            Debug.Log($"[RoomLobbyUI] Netcode đã kết nối thành công! ClientId: {clientId}");
            // Khi đã kết nối thành công vào game, ẩn toàn bộ UI Lobby để vào game
            gameObject.SetActive(false);
        }

        private void HandleNetcodeDisconnected(ulong clientId)
        {
            Debug.LogWarning($"[RoomLobbyUI] Netcode ngắt kết nối: {clientId}");
            gameObject.SetActive(true);
            ShowErrorMessage("Đã mất kết nối tới máy chủ trận đấu.");
            SetBusy(false);
        }

        // ---------------------------------------------------------------- Quản lý giao diện

        private void ShowMenuView()
        {
            if (menuPanel != null) menuPanel.SetActive(true);
            if (roomPanel != null) roomPanel.SetActive(false);
            SetStatus(string.Empty);
        }

        private void ShowRoomView()
        {
            if (menuPanel != null) menuPanel.SetActive(false);
            if (roomPanel != null) roomPanel.SetActive(true);
            SetStatus(string.Empty);
        }

        private void RefreshRoomView(RoomDto room)
        {
            if (room == null) return;

            if (roomCodeDisplay != null)
            {
                roomCodeDisplay.text = room.roomCode;
            }

            int count = room.players != null ? room.players.Count : 0;
            if (playerCountText != null)
            {
                playerCountText.text = $"Người chơi: {count}/{room.maxPlayers}";
            }

            // Cập nhật danh sách người chơi
            ClearSpawnedPlayerRows();
            if (playerListContainer != null && room.players != null)
            {
                string localId = roomManager.LocalPlayerId;
                for (int i = 0; i < room.players.Count; i++)
                {
                    PlayerDto p = room.players[i];
                    bool isLocal = string.Equals(p.playerId, localId, StringComparison.OrdinalIgnoreCase);
                    GameObject row = CreatePlayerRow(p, isLocal);
                    spawnedPlayerRows.Add(row);
                }
            }

            // Nút Ready
            if (readyButtonText != null)
            {
                bool isReady = roomManager.LocalPlayerIsReady;
                readyButtonText.text = isReady ? "HỦY SẴN SÀNG" : "SẴN SÀNG";
            }

            // Nút Bắt đầu trận (Host)
            if (startMatchButton != null)
            {
                bool isHost = roomManager.IsHost;
                startMatchButton.gameObject.SetActive(isHost);

                if (isHost)
                {
                    bool allReady = room.players != null && room.players.Count >= 1 && room.players.TrueForAll(x => x.isReady);
                    startMatchButton.interactable = allReady && !isBusy;

                    if (startButtonText != null)
                    {
                        startButtonText.text = allReady ? "BẮT ĐẦU TRẬN" : "CHỜ SẴN SÀNG...";
                    }
                }
            }
        }

        private void ClearSpawnedPlayerRows()
        {
            for (int i = 0; i < spawnedPlayerRows.Count; i++)
            {
                if (spawnedPlayerRows[i] != null)
                {
                    Destroy(spawnedPlayerRows[i]);
                }
            }
            spawnedPlayerRows.Clear();
        }

        private GameObject CreatePlayerRow(PlayerDto p, bool isLocal)
        {
            GameObject row = new GameObject($"Row_{p.displayName}", typeof(RectTransform));
            row.transform.SetParent(playerListContainer, false);
            row.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 36f);

            HorizontalLayoutGroup layout = row.AddComponent<HorizontalLayoutGroup>();
            layout.childForceExpandWidth = false;
            layout.childControlWidth = false;
            layout.spacing = 10f;
            layout.childAlignment = TextAnchor.MiddleLeft;

            // Tên người chơi
            string nameStr = isLocal ? $"{p.displayName} (Bạn)" : p.displayName;
            CreateLabel(row.transform, nameStr, 180f, isLocal ? new Color(0.4f, 0.9f, 1f) : Color.white, 16);

            // Badge Host
            if (p.isHost)
            {
                CreateLabel(row.transform, "[CHỦ PHÒNG]", 110f, new Color(1f, 0.85f, 0.2f), 14);
            }
            else
            {
                CreateLabel(row.transform, string.Empty, 110f, Color.clear, 14);
            }

            // Trạng thái Ready
            string readyStr = p.isReady ? "● SẴN SÀNG" : "○ CHƯA SẴN SÀNG";
            Color readyColor = p.isReady ? new Color(0.3f, 1f, 0.4f) : new Color(0.7f, 0.7f, 0.7f);
            CreateLabel(row.transform, readyStr, 140f, readyColor, 15);

            return row;
        }

        private void SetBusy(bool busy, string message = "")
        {
            isBusy = busy;
            if (createRoomButton != null) createRoomButton.interactable = !busy;
            if (joinRoomButton != null) joinRoomButton.interactable = !busy;
            if (readyButton != null) readyButton.interactable = !busy;
            if (leaveButton != null) leaveButton.interactable = !busy;
            if (startMatchButton != null)
            {
                bool allReady = roomManager.CurrentRoom != null &&
                                roomManager.CurrentRoom.players != null &&
                                roomManager.CurrentRoom.players.TrueForAll(x => x.isReady);
                startMatchButton.interactable = !busy && allReady;
            }

            SetStatus(busy ? message : string.Empty);
        }

        private void SetStatus(string message)
        {
            if (statusText != null)
            {
                statusText.text = message;
            }
        }

        private void ShowErrorMessage(string errorMsg)
        {
            if (errorBanner == null || errorBannerText == null)
            {
                Debug.LogWarning($"[RoomLobbyUI] Lỗi: {errorMsg}");
                SetStatus($"<color=#FF5555>{errorMsg}</color>");
                return;
            }

            errorBannerText.text = errorMsg;
            errorBanner.SetActive(true);

            if (errorBannerCoroutine != null) StopCoroutine(errorBannerCoroutine);
            errorBannerCoroutine = StartCoroutine(DismissErrorBannerAfterSeconds(4.5f));
        }

        private IEnumerator DismissErrorBannerAfterSeconds(float seconds)
        {
            yield return new WaitForSeconds(seconds);
            if (errorBanner != null)
            {
                errorBanner.SetActive(false);
            }
        }

        private void SaveDisplayName(string name)
        {
            PlayerPrefs.SetString(DisplayNamePrefKey, name);
            PlayerPrefs.Save();
        }

        private void LoadSavedDisplayName()
        {
            string saved = PlayerPrefs.GetString(DisplayNamePrefKey, "Survivor_" + UnityEngine.Random.Range(100, 999));
            if (nameInputField != null)
            {
                nameInputField.text = saved;
            }

            if (serverUrlInputField != null && roomManager != null)
            {
                serverUrlInputField.text = roomManager.BackendUrl;
            }
        }

        // ---------------------------------------------------------------- Dựng UI bằng Code Runtime

        private void BuildRuntimeUi()
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas == null)
            {
                GameObject canvasGo = new GameObject("RoomLobbyCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                canvas = canvasGo.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;

                // Without this, CanvasScaler defaults to ConstantPixelSize (scale factor 1), so every
                // fixed-pixel size below (panel 480x530, fonts in point sizes, etc.) renders tiny and
                // blurry on anything above ~1080p/HiDPI - ScaleWithScreenSize keeps it crisp and at a
                // consistent apparent size across resolutions instead.
                CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 0.5f;

                transform.SetParent(canvasGo.transform, false);
            }

            RectTransform rootRect = GetComponent<RectTransform>();
            if (rootRect == null) rootRect = gameObject.AddComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;

            // ================= 1. MENU PANEL =================
            menuPanel = new GameObject("MenuPanel", typeof(RectTransform), typeof(Image));
            menuPanel.transform.SetParent(transform, false);
            RectTransform menuRect = menuPanel.GetComponent<RectTransform>();
            menuRect.anchorMin = new Vector2(0.5f, 0.5f);
            menuRect.anchorMax = new Vector2(0.5f, 0.5f);
            menuRect.sizeDelta = new Vector2(480f, 530f);
            menuPanel.GetComponent<Image>().color = new Color(0.08f, 0.1f, 0.16f, 0.95f);

            VerticalLayoutGroup menuLayout = menuPanel.AddComponent<VerticalLayoutGroup>();
            menuLayout.padding = new RectOffset(30, 30, 24, 24);
            menuLayout.spacing = 12f;
            menuLayout.childControlWidth = true;
            menuLayout.childForceExpandWidth = true;

            CreateLabel(menuPanel.transform, "THE LAST SIGNAL", 0, new Color(0.3f, 0.8f, 1f), 24, FontStyles.Bold, TextAlignmentOptions.Center);
            CreateLabel(menuPanel.transform, "Phòng Chờ Trận Đấu", 0, Color.gray, 14, FontStyles.Normal, TextAlignmentOptions.Center);

            Button soloButton = CreateStyledButton(menuPanel.transform, "▶ CHƠI MỘT MÌNH (OFFLINE)", new Color(0.5f, 0.32f, 0.78f), 0, 44f, 16f);
            soloButton.onClick.AddListener(OnPlaySoloClicked);

            CreateLabel(menuPanel.transform, "─── HOẶC CHƠI ONLINE ───", 0, new Color(0.6f, 0.6f, 0.6f), 12, FontStyles.Normal, TextAlignmentOptions.Center);

            CreateLabel(menuPanel.transform, "Tên người chơi:", 0, Color.white, 14);
            nameInputField = CreateInputField(menuPanel.transform, "Nhập tên của bạn...", 24);

            CreateLabel(menuPanel.transform, "Địa chỉ Máy chủ Backend:", 0, new Color(0.75f, 0.75f, 0.75f), 12);
            serverUrlInputField = CreateInputField(menuPanel.transform, "http://localhost:8080", 64);
            if (roomManager != null && !string.IsNullOrEmpty(roomManager.BackendUrl))
            {
                serverUrlInputField.text = roomManager.BackendUrl;
            }

            // Hàng nút chọn nhanh chế độ: Local (localhost) vs Online (Tailscale)
            GameObject presetRow = new GameObject("PresetRow", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            presetRow.transform.SetParent(menuPanel.transform, false);
            HorizontalLayoutGroup presetLayout = presetRow.GetComponent<HorizontalLayoutGroup>();
            presetLayout.spacing = 8f;
            presetLayout.childControlWidth = true;
            presetLayout.childForceExpandWidth = true;

            Button btnLocal = CreateStyledButton(presetRow.transform, "⮞ Chế độ Local (localhost)", new Color(0.2f, 0.25f, 0.35f), 0, 28f, 12f);
            btnLocal.onClick.AddListener(() =>
            {
                string url = "http://localhost:8080";
                if (serverUrlInputField != null) serverUrlInputField.text = url;
                if (roomManager != null) roomManager.BackendUrl = url;
            });

            Button btnTailscale = CreateStyledButton(presetRow.transform, "⮞ Chế độ Tailscale (Online)", new Color(0.18f, 0.42f, 0.58f), 0, 28f, 12f);
            btnTailscale.onClick.AddListener(() =>
            {
                string url = "http://100.124.99.89:8080";
                if (serverUrlInputField != null) serverUrlInputField.text = url;
                if (roomManager != null) roomManager.BackendUrl = url;
            });

            createRoomButton = CreateStyledButton(menuPanel.transform, "TẠO PHÒNG MỚI", new Color(0.18f, 0.45f, 0.85f));
            createRoomButton.onClick.AddListener(OnCreateRoomClicked);

            CreateLabel(menuPanel.transform, "─── HOẶC VÀO BẰNG MÃ PHÒNG ───", 0, new Color(0.6f, 0.6f, 0.6f), 12, FontStyles.Normal, TextAlignmentOptions.Center);

            joinCodeInputField = CreateInputField(menuPanel.transform, "Nhập mã 6 ký tự...", 6);
            joinCodeInputField.characterLimit = 6;
            joinCodeInputField.onValueChanged.AddListener(val => {
                if (val != null) joinCodeInputField.text = val.ToUpperInvariant();
            });

            joinRoomButton = CreateStyledButton(menuPanel.transform, "VÀO PHÒNG", new Color(0.15f, 0.65f, 0.35f));
            joinRoomButton.onClick.AddListener(OnJoinRoomClicked);

            if (legacyLobbyPanel != null)
            {
                Button switchBtn = CreateStyledButton(menuPanel.transform, "⮂ Chuyển sang Direct/Relay Lobby", new Color(0.22f, 0.25f, 0.35f), 0, 32f);
                switchBtn.onClick.AddListener(() =>
                {
                    gameObject.SetActive(false);
                    legacyLobbyPanel.SetActive(true);
                });

                Button backBtn = CreateStyledButton(legacyLobbyPanel.transform, "★ Mở Phòng Chờ Backend (API Room)", new Color(0.18f, 0.45f, 0.85f), 0, 34f);
                backBtn.onClick.AddListener(() =>
                {
                    legacyLobbyPanel.SetActive(false);
                    gameObject.SetActive(true);
                });
            }

            statusText = CreateLabel(menuPanel.transform, string.Empty, 0, new Color(1f, 0.85f, 0.3f), 13, FontStyles.Normal, TextAlignmentOptions.Center);

            // ================= 2. ROOM LOBBY PANEL =================
            roomPanel = new GameObject("RoomLobbyPanel", typeof(RectTransform), typeof(Image));
            roomPanel.transform.SetParent(transform, false);
            RectTransform roomRect = roomPanel.GetComponent<RectTransform>();
            roomRect.anchorMin = new Vector2(0.5f, 0.5f);
            roomRect.anchorMax = new Vector2(0.5f, 0.5f);
            roomRect.sizeDelta = new Vector2(520f, 480f);
            roomPanel.GetComponent<Image>().color = new Color(0.08f, 0.1f, 0.16f, 0.95f);

            VerticalLayoutGroup roomLayout = roomPanel.AddComponent<VerticalLayoutGroup>();
            roomLayout.padding = new RectOffset(30, 30, 24, 24);
            roomLayout.spacing = 12f;
            roomLayout.childControlWidth = true;
            roomLayout.childForceExpandWidth = true;

            // Header mã phòng
            GameObject headerRow = new GameObject("HeaderRow", typeof(RectTransform));
            headerRow.transform.SetParent(roomPanel.transform, false);
            headerRow.GetComponent<RectTransform>().sizeDelta = new Vector2(0, 40);
            HorizontalLayoutGroup headerLayout = headerRow.AddComponent<HorizontalLayoutGroup>();
            headerLayout.spacing = 10f;
            headerLayout.childControlWidth = false;
            headerLayout.childForceExpandWidth = false;
            headerLayout.childAlignment = TextAnchor.MiddleLeft;

            CreateLabel(headerRow.transform, "MÃ PHÒNG:", 100f, Color.white, 16, FontStyles.Bold);
            roomCodeDisplay = CreateLabel(headerRow.transform, "------", 120f, new Color(0.3f, 0.85f, 1f), 22, FontStyles.Bold);

            copyCodeButton = CreateStyledButton(headerRow.transform, "Copy", new Color(0.25f, 0.35f, 0.5f), 70f, 32f);
            copyButtonText = copyCodeButton.GetComponentInChildren<TextMeshProUGUI>();
            copyCodeButton.onClick.AddListener(OnCopyCodeClicked);

            playerCountText = CreateLabel(roomPanel.transform, "Người chơi: 1/4", 0, Color.gray, 14);

            // Danh sách người chơi container
            GameObject listContainer = new GameObject("PlayerListContainer", typeof(RectTransform), typeof(Image));
            listContainer.transform.SetParent(roomPanel.transform, false);
            listContainer.GetComponent<RectTransform>().sizeDelta = new Vector2(0, 180);
            listContainer.GetComponent<Image>().color = new Color(0.04f, 0.06f, 0.1f, 0.8f);

            VerticalLayoutGroup listLayout = listContainer.AddComponent<VerticalLayoutGroup>();
            listLayout.padding = new RectOffset(14, 14, 10, 10);
            listLayout.spacing = 6f;
            listLayout.childControlWidth = true;
            listLayout.childForceExpandWidth = true;
            playerListContainer = listContainer.transform;

            // Thanh nút điều khiển
            GameObject actionsRow = new GameObject("ActionsRow", typeof(RectTransform));
            actionsRow.transform.SetParent(roomPanel.transform, false);
            actionsRow.GetComponent<RectTransform>().sizeDelta = new Vector2(0, 44);
            HorizontalLayoutGroup actionsLayout = actionsRow.AddComponent<HorizontalLayoutGroup>();
            actionsLayout.spacing = 12f;
            actionsLayout.childControlWidth = true;
            actionsLayout.childForceExpandWidth = true;

            readyButton = CreateStyledButton(actionsRow.transform, "SẴN SÀNG", new Color(0.18f, 0.65f, 0.35f));
            readyButtonText = readyButton.GetComponentInChildren<TextMeshProUGUI>();
            readyButton.onClick.AddListener(OnReadyToggleClicked);

            startMatchButton = CreateStyledButton(actionsRow.transform, "BẮT ĐẦU TRẬN", new Color(0.2f, 0.5f, 0.9f));
            startButtonText = startMatchButton.GetComponentInChildren<TextMeshProUGUI>();
            startMatchButton.onClick.AddListener(OnStartMatchClicked);

            leaveButton = CreateStyledButton(roomPanel.transform, "RỜI PHÒNG", new Color(0.6f, 0.18f, 0.18f));
            leaveButton.onClick.AddListener(OnLeaveRoomClicked);

            // ================= 3. ERROR BANNER =================
            errorBanner = new GameObject("ErrorBanner", typeof(RectTransform), typeof(Image));
            errorBanner.transform.SetParent(transform, false);
            RectTransform ebRect = errorBanner.GetComponent<RectTransform>();
            ebRect.anchorMin = new Vector2(0.5f, 1f);
            ebRect.anchorMax = new Vector2(0.5f, 1f);
            ebRect.pivot = new Vector2(0.5f, 1f);
            ebRect.anchoredPosition = new Vector2(0, -20);
            ebRect.sizeDelta = new Vector2(560f, 50f);
            errorBanner.GetComponent<Image>().color = new Color(0.75f, 0.15f, 0.15f, 0.95f);

            errorBannerText = CreateLabel(errorBanner.transform, string.Empty, 520f, Color.white, 14, FontStyles.Bold, TextAlignmentOptions.Center);
            errorBanner.SetActive(false);
        }

        private TextMeshProUGUI CreateLabel(Transform parent, string text, float width, Color color, float size = 16, FontStyles style = FontStyles.Normal, TextAlignmentOptions align = TextAlignmentOptions.MidlineLeft)
        {
            GameObject go = new GameObject("Label", typeof(RectTransform));
            go.transform.SetParent(parent, false);

            if (width > 0)
            {
                go.GetComponent<RectTransform>().sizeDelta = new Vector2(width, 28f);
                LayoutElement le = go.AddComponent<LayoutElement>();
                le.preferredWidth = width;
            }

            TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.fontStyle = style;
            tmp.color = color;
            tmp.alignment = align;
            tmp.raycastTarget = false;
            return tmp;
        }

        private TMP_InputField CreateInputField(Transform parent, string placeholder, int charLimit)
        {
            GameObject go = new GameObject("InputField", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            go.GetComponent<RectTransform>().sizeDelta = new Vector2(0, 38f);
            go.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.08f);

            TMP_InputField input = go.AddComponent<TMP_InputField>();
            input.characterLimit = charLimit;

            GameObject textArea = new GameObject("Text Area", typeof(RectTransform));
            textArea.transform.SetParent(go.transform, false);
            RectTransform taRect = textArea.GetComponent<RectTransform>();
            taRect.anchorMin = Vector2.zero;
            taRect.anchorMax = Vector2.one;
            taRect.offsetMin = new Vector2(10f, 4f);
            taRect.offsetMax = new Vector2(-10f, -4f);

            GameObject textGo = new GameObject("Text", typeof(RectTransform));
            textGo.transform.SetParent(textArea.transform, false);
            RectTransform tRect = textGo.GetComponent<RectTransform>();
            tRect.anchorMin = Vector2.zero;
            tRect.anchorMax = Vector2.one;
            tRect.offsetMin = Vector2.zero;
            tRect.offsetMax = Vector2.zero;

            TextMeshProUGUI textComp = textGo.AddComponent<TextMeshProUGUI>();
            textComp.fontSize = 16;
            textComp.color = Color.white;
            textComp.alignment = TextAlignmentOptions.MidlineLeft;

            GameObject placeholderGo = new GameObject("Placeholder", typeof(RectTransform));
            placeholderGo.transform.SetParent(textArea.transform, false);
            RectTransform pRect = placeholderGo.GetComponent<RectTransform>();
            pRect.anchorMin = Vector2.zero;
            pRect.anchorMax = Vector2.one;
            pRect.offsetMin = Vector2.zero;
            pRect.offsetMax = Vector2.zero;

            TextMeshProUGUI placeholderComp = placeholderGo.AddComponent<TextMeshProUGUI>();
            placeholderComp.text = placeholder;
            placeholderComp.fontSize = 16;
            placeholderComp.fontStyle = FontStyles.Italic;
            placeholderComp.color = new Color(0.6f, 0.6f, 0.6f, 0.5f);
            placeholderComp.alignment = TextAlignmentOptions.MidlineLeft;

            input.textViewport = taRect;
            input.textComponent = textComp;
            input.placeholder = placeholderComp;

            return input;
        }

        private Button CreateStyledButton(Transform parent, string label, Color bgColor, float width = 0, float height = 40f, float fontSize = 15f)
        {
            GameObject go = new GameObject($"Btn_{label}", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);

            if (width > 0)
            {
                go.GetComponent<RectTransform>().sizeDelta = new Vector2(width, height);
                LayoutElement le = go.AddComponent<LayoutElement>();
                le.preferredWidth = width;
                le.preferredHeight = height;
            }
            else
            {
                go.GetComponent<RectTransform>().sizeDelta = new Vector2(0, height);
            }

            Image img = go.GetComponent<Image>();
            img.color = bgColor;

            Button btn = go.GetComponent<Button>();
            btn.targetGraphic = img;

            ColorBlock cb = btn.colors;
            cb.highlightedColor = Color.Lerp(bgColor, Color.white, 0.2f);
            cb.pressedColor = Color.Lerp(bgColor, Color.black, 0.2f);
            cb.disabledColor = new Color(0.3f, 0.3f, 0.3f, 0.5f);
            btn.colors = cb;

            GameObject textGo = new GameObject("Text", typeof(RectTransform));
            textGo.transform.SetParent(go.transform, false);
            RectTransform tRect = textGo.GetComponent<RectTransform>();
            tRect.anchorMin = Vector2.zero;
            tRect.anchorMax = Vector2.one;
            tRect.offsetMin = Vector2.zero;
            tRect.offsetMax = Vector2.zero;

            TextMeshProUGUI tmp = textGo.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = fontSize;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            tmp.raycastTarget = false;

            return btn;
        }

#if UNITY_EDITOR
        private const string EditorSkipLobbyKey = "TheLastSignal.RoomLobbyUI.SkipOnlineLobby";

        [UnityEditor.MenuItem("TheLastSignal/Launch Mode/Skip Online Lobby On Next Play")]
        private static void EditorSkipOnlineLobbyNextPlay()
        {
            UnityEditor.SessionState.SetBool(EditorSkipLobbyKey, true);
            Debug.Log("[RoomLobbyUI] Will skip auto-spawning the online lobby on the next Play session in " +
                      "this Editor session, falling back to the legacy LobbyPanel + GameManager.startSessionOnPlay " +
                      "for quick solo testing. Use 'Restore Online Lobby' to go back, or just restart the Editor " +
                      "(this clears automatically).");
        }

        [UnityEditor.MenuItem("TheLastSignal/Launch Mode/Restore Online Lobby")]
        private static void EditorRestoreOnlineLobby()
        {
            UnityEditor.SessionState.SetBool(EditorSkipLobbyKey, false);
            Debug.Log("[RoomLobbyUI] Online lobby restored; next Play session shows RoomLobbyUI as normal.");
        }
#endif
    }
}
