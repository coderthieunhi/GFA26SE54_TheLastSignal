using System;
using System.Collections.Generic;

namespace TheLastSignal.Core.Backend
{
    /// <summary>
    /// Wrapper cơ bản cho phản hồi lỗi hoặc thông điệp chung từ Spring Boot.
    /// </summary>
    [Serializable]
    public class BaseApiResponse
    {
        public bool success;
        public string message;
        public string timestamp;
    }

    /// <summary>
    /// Wrapper phản hồi chứa RoomDto (tối ưu hóa cho JsonUtility).
    /// </summary>
    [Serializable]
    public class RoomApiResponse : BaseApiResponse
    {
        public RoomDto data;
    }

    /// <summary>
    /// Wrapper phản hồi chứa GameSessionDto (tối ưu hóa cho JsonUtility).
    /// </summary>
    [Serializable]
    public class SessionApiResponse : BaseApiResponse
    {
        public GameSessionDto data;
    }

    /// <summary>
    /// Thông tin người chơi trong phòng chờ.
    /// </summary>
    [Serializable]
    public class PlayerDto
    {
        public string playerId;
        public string displayName;
        public bool isHost;
        public bool isReady;
    }

    /// <summary>
    /// Trạng thái phòng chờ và danh sách thành viên.
    /// </summary>
    [Serializable]
    public class RoomDto
    {
        public string roomCode;
        public string hostPlayerId;
        public string status; // WAITING, STARTING, IN_MATCH, CLOSED
        public int currentPlayers;
        public int maxPlayers;
        public List<PlayerDto> players = new List<PlayerDto>();
        public string createdAt;

        // Các trường kết nối được backend bổ sung
        public string assignedPlayerId;
        public string serverHost;
        public int serverPort;
        public string sessionId;
        public string relayJoinCode;

        /// <summary>TLS-E03-S05: this caller's OWN join token, populated only when GetRoomAsync passed
        /// a playerId - never any other player's token (the shared players[] list never carries one at
        /// all, per the backend contract). Null/empty if this request didn't identify a player.</summary>
        public string yourJoinToken;

        public bool IsInMatch => string.Equals(status, "IN_MATCH", StringComparison.OrdinalIgnoreCase);
        public bool IsWaiting => string.Equals(status, "WAITING", StringComparison.OrdinalIgnoreCase);
        public bool IsStarting => string.Equals(status, "STARTING", StringComparison.OrdinalIgnoreCase);
        public bool IsClosed => string.Equals(status, "CLOSED", StringComparison.OrdinalIgnoreCase);
    }

    [Serializable]
    public class CreateRoomRequest
    {
        public string hostDisplayName;
    }

    [Serializable]
    public class JoinRoomRequest
    {
        public string displayName;
    }

    [Serializable]
    public class PlayerActionRequest
    {
        public string playerId;
    }

    [Serializable]
    public class GameSessionDto
    {
        public string sessionId;
        public string roomCode;
        public string status;
        public string serverHost;
        public int serverPort;
        public string startedAt;

        /// <summary>TLS-E03-S05: the host's own join token - returned once, directly from the /start
        /// call, since the host doesn't need a follow-up poll to learn it.</summary>
        public string yourJoinToken;
    }
}
