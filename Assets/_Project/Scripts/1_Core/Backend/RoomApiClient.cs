using System;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace TheLastSignal.Core.Backend
{
    /// <summary>
    /// Exception chuyên biệt khi giao tiếp với Room API của Backend.
    /// Mang theo HTTP status code và thông điệp lỗi tiếng Việt thân thiện.
    /// </summary>
    public class RoomApiException : Exception
    {
        public long StatusCode { get; }
        public string BackendMessage { get; }

        public RoomApiException(long statusCode, string backendMessage, string friendlyMessage)
            : base(friendlyMessage)
        {
            StatusCode = statusCode;
            BackendMessage = backendMessage;
        }
    }

    /// <summary>
    /// Client HTTP chuyên trách kết nối tới các endpoint /api/rooms của Spring Boot Backend.
    /// Sử dụng UnityWebRequest và async/await.
    /// </summary>
    public class RoomApiClient
    {
        private readonly string baseUrl;
        private readonly int timeoutSeconds;

        public RoomApiClient(string baseUrl = "http://localhost:8080", int timeoutSeconds = 5)
        {
            this.baseUrl = (baseUrl ?? "http://localhost:8080").TrimEnd('/');
            this.timeoutSeconds = Mathf.Max(1, timeoutSeconds);
        }

        /// <summary>
        /// Tạo phòng mới với tên hiển thị của Host: POST /api/rooms
        /// </summary>
        public async Task<RoomDto> CreateRoomAsync(string hostDisplayName)
        {
            string url = $"{baseUrl}/api/rooms";
            string body = JsonUtility.ToJson(new CreateRoomRequest { hostDisplayName = hostDisplayName });
            RoomApiResponse response = await SendRequestAsync<RoomApiResponse>(url, "POST", body);
            return response.data;
        }

        /// <summary>
        /// Tham gia phòng bằng mã: POST /api/rooms/{roomCode}/join
        /// </summary>
        public async Task<RoomDto> JoinRoomAsync(string roomCode, string displayName)
        {
            string url = $"{baseUrl}/api/rooms/{Uri.EscapeDataString(roomCode.Trim().ToUpperInvariant())}/join";
            string body = JsonUtility.ToJson(new JoinRoomRequest { displayName = displayName });
            RoomApiResponse response = await SendRequestAsync<RoomApiResponse>(url, "POST", body);
            return response.data;
        }

        /// <summary>
        /// Lấy chi tiết thông tin phòng: GET /api/rooms/{roomCode}
        /// </summary>
        public async Task<RoomDto> GetRoomAsync(string roomCode)
        {
            string url = $"{baseUrl}/api/rooms/{Uri.EscapeDataString(roomCode.Trim().ToUpperInvariant())}";
            RoomApiResponse response = await SendRequestAsync<RoomApiResponse>(url, "GET", null);
            return response.data;
        }

        /// <summary>
        /// Bật/Tắt trạng thái Sẵn sàng: PUT /api/rooms/{roomCode}/ready?ready=true/false
        /// </summary>
        public async Task<RoomDto> ToggleReadyAsync(string roomCode, string playerId, bool ready)
        {
            string url = $"{baseUrl}/api/rooms/{Uri.EscapeDataString(roomCode.Trim().ToUpperInvariant())}/ready?ready={(ready ? "true" : "false")}";
            string body = JsonUtility.ToJson(new PlayerActionRequest { playerId = playerId });
            RoomApiResponse response = await SendRequestAsync<RoomApiResponse>(url, "PUT", body);
            return response.data;
        }

        /// <summary>
        /// Rời khỏi phòng: POST /api/rooms/{roomCode}/leave
        /// </summary>
        public async Task<RoomDto> LeaveRoomAsync(string roomCode, string playerId)
        {
            string url = $"{baseUrl}/api/rooms/{Uri.EscapeDataString(roomCode.Trim().ToUpperInvariant())}/leave";
            string body = JsonUtility.ToJson(new PlayerActionRequest { playerId = playerId });
            RoomApiResponse response = await SendRequestAsync<RoomApiResponse>(url, "POST", body);
            return response.data;
        }

        /// <summary>
        /// Bắt đầu trận đấu (Chỉ Host): POST /api/rooms/{roomCode}/start
        /// </summary>
        public async Task<GameSessionDto> StartRoomAsync(string roomCode, string playerId)
        {
            string url = $"{baseUrl}/api/rooms/{Uri.EscapeDataString(roomCode.Trim().ToUpperInvariant())}/start";
            string body = JsonUtility.ToJson(new PlayerActionRequest { playerId = playerId });
            SessionApiResponse response = await SendRequestAsync<SessionApiResponse>(url, "POST", body);
            return response.data;
        }

        private async Task<T> SendRequestAsync<T>(string url, string method, string jsonBody) where T : BaseApiResponse
        {
            using UnityWebRequest request = new UnityWebRequest(url, method);
            if (!string.IsNullOrEmpty(jsonBody))
            {
                byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            }
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.timeout = timeoutSeconds;

            UnityWebRequestAsyncOperation operation = request.SendWebRequest();
            while (!operation.isDone)
            {
                await Task.Yield();
            }

            string responseText = request.downloadHandler != null ? request.downloadHandler.text : string.Empty;
            long statusCode = request.responseCode;

            if (request.result == UnityWebRequest.Result.ConnectionError)
            {
                throw new RoomApiException(0, request.error,
                    $"Không thể kết nối đến máy chủ Backend ({request.error}). Vui lòng kiểm tra server tại {baseUrl}.");
            }

            if (request.result == UnityWebRequest.Result.ProtocolError || statusCode >= 400)
            {
                string backendMessage = string.Empty;
                if (!string.IsNullOrEmpty(responseText))
                {
                    try
                    {
                        BaseApiResponse errObj = JsonUtility.FromJson<BaseApiResponse>(responseText);
                        if (errObj != null && !string.IsNullOrEmpty(errObj.message))
                        {
                            backendMessage = errObj.message;
                        }
                    }
                    catch
                    {
                        // Bỏ qua lỗi parse JSON lỗi
                    }
                }

                string friendlyMessage = FormatErrorMessage(statusCode, backendMessage);
                throw new RoomApiException(statusCode, backendMessage, friendlyMessage);
            }

            try
            {
                T responseObj = JsonUtility.FromJson<T>(responseText);
                if (responseObj == null || !responseObj.success)
                {
                    string msg = responseObj != null ? responseObj.message : "Dữ liệu trả về không hợp lệ";
                    throw new RoomApiException(statusCode, msg, msg);
                }
                return responseObj;
            }
            catch (RoomApiException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new RoomApiException(statusCode, ex.Message, "Không thể giải mã phản hồi JSON từ máy chủ.");
            }
        }

        private static string FormatErrorMessage(long statusCode, string backendMessage)
        {
            if (!string.IsNullOrEmpty(backendMessage))
            {
                return backendMessage;
            }

            switch (statusCode)
            {
                case 404:
                    return "Mã phòng không tồn tại hoặc phòng đã bị đóng.";
                case 409:
                    return "Phòng đã đủ người chơi hoặc trận đấu đã bắt đầu.";
                case 400:
                    return "Yêu cầu không hợp lệ. Vui lòng kiểm tra lại thông tin.";
                case 403:
                    return "Chỉ chủ phòng mới có quyền thực hiện thao tác này.";
                case 500:
                    return "Máy chủ Backend gặp sự cố nội bộ (500). Vui lòng thử lại sau.";
                default:
                    return $"Lỗi kết nối máy chủ (HTTP {statusCode}).";
            }
        }
    }
}
