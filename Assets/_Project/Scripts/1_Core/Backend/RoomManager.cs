using System;
using System.Collections;
using System.Threading.Tasks;
using UnityEngine;

namespace TheLastSignal.Core.Backend
{
    /// <summary>
    /// Điều phối phiên phòng chờ (Lobby Session Manager):
    /// - Quản lý trạng thái phòng hiện tại, thông tin người chơi cục bộ.
    /// - Thực hiện Polling định kỳ để đồng bộ trạng thái phòng từ Backend.
    /// - Phát các sự kiện cho UI lắng nghe và cập nhật.
    /// - Tự động dọn dẹp khi thoát phòng hoặc tắt ứng dụng.
    /// </summary>
    public class RoomManager : MonoBehaviour
    {
        public static RoomManager Instance { get; private set; }

        [Header("Backend Connection")]
        [SerializeField] private string backendUrl = "http://localhost:8080";
        [SerializeField] private float pollIntervalSeconds = 1.5f;
        [SerializeField] private int requestTimeoutSeconds = 5;

        public const string BackendUrlPrefKey = "TLS_BackendUrl";

        public string BackendUrl
        {
            get => backendUrl;
            set
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    string formatted = value.Trim();
                    if (!formatted.StartsWith("http://") && !formatted.StartsWith("https://"))
                    {
                        formatted = "http://" + formatted;
                    }
                    backendUrl = formatted.TrimEnd('/');
                    PlayerPrefs.SetString(BackendUrlPrefKey, backendUrl);
                    PlayerPrefs.Save();
                    apiClient = new RoomApiClient(backendUrl, requestTimeoutSeconds);
                }
            }
        }

        public RoomDto CurrentRoom { get; private set; }
        public string CurrentRoomCode => CurrentRoom != null ? CurrentRoom.roomCode : string.Empty;
        public string LocalPlayerId { get; private set; }
        public string LocalDisplayName { get; private set; }
        public bool IsInRoom => CurrentRoom != null && !CurrentRoom.IsClosed;
        public bool IsHost => CurrentRoom != null && string.Equals(CurrentRoom.hostPlayerId, LocalPlayerId, StringComparison.OrdinalIgnoreCase);

        public bool LocalPlayerIsReady
        {
            get
            {
                if (CurrentRoom == null || CurrentRoom.players == null) return false;
                PlayerDto p = CurrentRoom.players.Find(x => string.Equals(x.playerId, LocalPlayerId, StringComparison.OrdinalIgnoreCase));
                return p != null && p.isReady;
            }
        }

        // Sự kiện thông báo cho UI và các hệ thống khác
        public event Action<RoomDto> OnRoomJoined;
        public event Action<RoomDto> OnRoomUpdated;
        public event Action<RoomDto> OnMatchStarting;
        public event Action OnRoomLeft;
        public event Action<string> OnError;

        private RoomApiClient apiClient;
        private Coroutine pollCoroutine;
        private bool isStartingMatch = false;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            if (PlayerPrefs.HasKey(BackendUrlPrefKey))
            {
                string savedUrl = PlayerPrefs.GetString(BackendUrlPrefKey);
                if (!string.IsNullOrWhiteSpace(savedUrl))
                {
                    backendUrl = savedUrl;
                }
            }

            apiClient = new RoomApiClient(backendUrl, requestTimeoutSeconds);
        }

        private void OnDestroy()
        {
            StopPolling();
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void OnApplicationQuit()
        {
            if (IsInRoom && !string.IsNullOrEmpty(LocalPlayerId))
            {
                // Đồng bộ hủy phòng nhanh khi tắt game
                _ = apiClient.LeaveRoomAsync(CurrentRoomCode, LocalPlayerId);
            }
        }

        /// <summary>
        /// Tạo phòng mới với tên hiển thị của Host.
        /// </summary>
        public async Task<bool> CreateRoomAsync(string displayName)
        {
            if (string.IsNullOrWhiteSpace(displayName))
            {
                OnError?.Invoke("Vui lòng nhập tên người chơi.");
                return false;
            }

            try
            {
                LocalDisplayName = displayName.Trim();
                RoomDto room = await apiClient.CreateRoomAsync(LocalDisplayName);

                CurrentRoom = room;
                LocalPlayerId = !string.IsNullOrEmpty(room.assignedPlayerId) ? room.assignedPlayerId : room.hostPlayerId;
                isStartingMatch = false;

                Debug.Log($"[RoomManager] Tạo phòng thành công! Mã phòng: {CurrentRoomCode}, HostPlayerId: {LocalPlayerId}");
                OnRoomJoined?.Invoke(CurrentRoom);

                StartPolling();
                return true;
            }
            catch (RoomApiException ex)
            {
                Debug.LogWarning($"[RoomManager] Tạo phòng thất bại: {ex.Message}");
                OnError?.Invoke(ex.Message);
                return false;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[RoomManager] Lỗi hệ thống khi tạo phòng: {ex.Message}");
                OnError?.Invoke($"Lỗi bất ngờ: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Tham gia vào phòng có sẵn bằng mã phòng.
        /// </summary>
        public async Task<bool> JoinRoomAsync(string roomCode, string displayName)
        {
            if (string.IsNullOrWhiteSpace(displayName))
            {
                OnError?.Invoke("Vui lòng nhập tên người chơi.");
                return false;
            }

            if (string.IsNullOrWhiteSpace(roomCode) || roomCode.Trim().Length != 6)
            {
                OnError?.Invoke("Mã phòng phải gồm 6 ký tự.");
                return false;
            }

            try
            {
                LocalDisplayName = displayName.Trim();
                string cleanCode = roomCode.Trim().ToUpperInvariant();

                RoomDto room = await apiClient.JoinRoomAsync(cleanCode, LocalDisplayName);

                CurrentRoom = room;
                LocalPlayerId = room.assignedPlayerId;

                // Nếu backend không trả assignedPlayerId, tìm trong danh sách theo tên
                if (string.IsNullOrEmpty(LocalPlayerId) && room.players != null)
                {
                    PlayerDto found = room.players.Find(p => string.Equals(p.displayName, LocalDisplayName, StringComparison.OrdinalIgnoreCase));
                    if (found != null) LocalPlayerId = found.playerId;
                }

                isStartingMatch = false;
                Debug.Log($"[RoomManager] Tham gia phòng {cleanCode} thành công! PlayerId: {LocalPlayerId}");
                OnRoomJoined?.Invoke(CurrentRoom);

                StartPolling();
                return true;
            }
            catch (RoomApiException ex)
            {
                Debug.LogWarning($"[RoomManager] Tham gia phòng thất bại: {ex.Message}");
                OnError?.Invoke(ex.Message);
                return false;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[RoomManager] Lỗi khi tham gia phòng: {ex.Message}");
                OnError?.Invoke($"Lỗi kết nối: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Bật/Tắt trạng thái Sẵn sàng của người chơi cục bộ.
        /// </summary>
        public async Task<bool> ToggleReadyAsync()
        {
            if (!IsInRoom || string.IsNullOrEmpty(LocalPlayerId))
            {
                OnError?.Invoke("Bạn hiện chưa ở trong phòng.");
                return false;
            }

            bool newReadyState = !LocalPlayerIsReady;

            try
            {
                RoomDto updated = await apiClient.ToggleReadyAsync(CurrentRoomCode, LocalPlayerId, newReadyState);
                CurrentRoom = updated;
                OnRoomUpdated?.Invoke(CurrentRoom);
                return true;
            }
            catch (RoomApiException ex)
            {
                OnError?.Invoke(ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Rời phòng hiện tại.
        /// </summary>
        public async Task<bool> LeaveRoomAsync()
        {
            if (!IsInRoom) return true;

            StopPolling();
            string codeToLeave = CurrentRoomCode;
            string idToLeave = LocalPlayerId;

            CurrentRoom = null;
            LocalPlayerId = null;
            OnRoomLeft?.Invoke();

            try
            {
                if (!string.IsNullOrEmpty(codeToLeave) && !string.IsNullOrEmpty(idToLeave))
                {
                    await apiClient.LeaveRoomAsync(codeToLeave, idToLeave);
                }
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[RoomManager] Lỗi khi rời phòng: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Bắt đầu trận đấu (Chỉ dành cho Host).
        /// </summary>
        public async Task<bool> StartRoomAsync()
        {
            if (!IsInRoom || !IsHost)
            {
                OnError?.Invoke("Chỉ có Chủ phòng mới có quyền Bắt đầu trận đấu.");
                return false;
            }

            try
            {
                isStartingMatch = true;
                GameSessionDto session = await apiClient.StartRoomAsync(CurrentRoomCode, LocalPlayerId);

                if (CurrentRoom != null)
                {
                    CurrentRoom.status = "IN_MATCH";
                    CurrentRoom.serverHost = session.serverHost;
                    CurrentRoom.serverPort = session.serverPort;
                    CurrentRoom.sessionId = session.sessionId;
                    CurrentRoom.yourJoinToken = session.yourJoinToken;
                }

                StopPolling();
                Debug.Log($"[RoomManager] Trận đấu đã bắt đầu! Máy chủ: {session.serverHost}:{session.serverPort}");
                OnMatchStarting?.Invoke(CurrentRoom);
                return true;
            }
            catch (RoomApiException ex)
            {
                isStartingMatch = false;
                OnError?.Invoke(ex.Message);
                return false;
            }
            catch (Exception ex)
            {
                isStartingMatch = false;
                OnError?.Invoke($"Không thể khởi động trận đấu: {ex.Message}");
                return false;
            }
        }

        private void StartPolling()
        {
            StopPolling();
            pollCoroutine = StartCoroutine(PollRoomRoutine());
        }

        private void StopPolling()
        {
            if (pollCoroutine != null)
            {
                StopCoroutine(pollCoroutine);
                pollCoroutine = null;
            }
        }

        private IEnumerator PollRoomRoutine()
        {
            WaitForSeconds wait = new WaitForSeconds(pollIntervalSeconds);
            int consecutiveErrors = 0;

            while (IsInRoom && !isStartingMatch)
            {
                yield return wait;

                if (!IsInRoom || isStartingMatch) break;

                Task<RoomDto> task = apiClient.GetRoomAsync(CurrentRoomCode, LocalPlayerId);
                while (!task.IsCompleted)
                {
                    yield return null;
                }

                if (task.IsFaulted)
                {
                    consecutiveErrors++;
                    Debug.LogWarning($"[RoomManager] Polling lỗi ({consecutiveErrors} lần liên tiếp): {task.Exception?.GetBaseException().Message}");

                    if (consecutiveErrors >= 4)
                    {
                        OnError?.Invoke("Mất kết nối với máy chủ phòng chờ sau nhiều lần thử.");
                        yield return new WaitForSeconds(3f);
                    }
                    continue;
                }

                consecutiveErrors = 0;
                RoomDto freshRoom = task.Result;

                if (freshRoom == null) continue;

                CurrentRoom = freshRoom;

                // Kiểm tra nếu phòng bị đóng
                if (freshRoom.IsClosed)
                {
                    Debug.LogWarning("[RoomManager] Phòng đã bị đóng bởi máy chủ.");
                    StopPolling();
                    CurrentRoom = null;
                    OnError?.Invoke("Phòng đã bị đóng hoặc Host đã giải tán phòng.");
                    OnRoomLeft?.Invoke();
                    yield break;
                }

                // Kiểm tra nếu trận đấu đã bắt đầu (nhận diện cho các máy Khách)
                if (freshRoom.IsInMatch)
                {
                    Debug.Log($"[RoomManager] Phát hiện trận đấu đã bắt đầu! Máy chủ: {freshRoom.serverHost}:{freshRoom.serverPort}");
                    isStartingMatch = true;
                    StopPolling();
                    OnMatchStarting?.Invoke(freshRoom);
                    yield break;
                }

                OnRoomUpdated?.Invoke(CurrentRoom);
            }
        }
    }
}
