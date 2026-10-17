using System;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace TheLastSignal.Core.Backend
{
    /// <summary>
    /// TLS-E03-S02: real implementation of <see cref="IBackendClient"/> against the Java/Spring Boot
    /// endpoints described in docs/pivot/02_BACKEND_CONTRACT.md — Bearer server-secret auth, endpoints
    /// under /api/v1/matches/{matchId}/... Not exercised by the spike itself (no backend exists yet),
    /// but implemented now so the Java side can build against a stable, already-agreed contract.
    /// Unreachable/erroring/timed-out calls are treated as rejections by the caller
    /// (ServerConnectionApproval), never as approvals — "fail closed" per the brief.
    /// </summary>
    public class HttpBackendClient : IBackendClient
    {
        // Wire-format DTOs: field names intentionally match the JSON contract's camelCase keys exactly
        // (JsonUtility maps by exact field name, not by [JsonProperty]-style attributes).
        [Serializable]
        private class ValidateTokenRequestDto
        {
            public string joinToken;
            public ulong clientTransportId;
        }

        [Serializable]
        private class ValidateTokenResponseDto
        {
            public bool approved;
            public string reason;
            public string accountId;
            public string displayName;
            public string role;
            public int slotIndex;
        }

        [Serializable]
        private class HeartbeatRequestDto
        {
            public int connectedPlayerCount;
            public string sessionState;
            public float uptimeSeconds;
        }

        [Serializable]
        private class HeartbeatResponseDto
        {
            public bool acknowledged;
            public string directive; // absent/null in JSON when there is none; JsonUtility leaves it null.
        }

        [Serializable]
        private class MatchPlayerResultDto
        {
            public string accountId;
            public string role;
            public bool incapacitated;
            public string finalStatus;
        }

        [Serializable]
        private class MatchResultRequestDto
        {
            public string outcome;
            public float durationSeconds;
            public MatchPlayerResultDto[] players;
        }

        [Serializable]
        private class MatchResultResponseDto
        {
            public bool recorded;
            public bool duplicate;
        }

        private readonly string backendUrl;
        private readonly string matchId;
        private readonly string serverSecret;
        private readonly int timeoutSeconds;

        public HttpBackendClient(string backendUrl, string matchId, string serverSecret, int timeoutSeconds = 3)
        {
            this.backendUrl = (backendUrl ?? string.Empty).TrimEnd('/');
            this.matchId = matchId ?? string.Empty;
            this.serverSecret = serverSecret ?? string.Empty;
            this.timeoutSeconds = Mathf.Max(1, timeoutSeconds);
        }

        public async Task<JoinTokenValidationResult> ValidateTokenAsync(string joinToken, ulong clientTransportId)
        {
            string url = $"{backendUrl}/api/v1/matches/{matchId}/validate-token";
            ValidateTokenRequestDto requestDto = new ValidateTokenRequestDto
            {
                joinToken = joinToken ?? string.Empty,
                clientTransportId = clientTransportId
            };

            UnityWebRequest.Result outcome;
            string responseBody;
            long statusCode;

            try
            {
                (outcome, responseBody, statusCode) = await PostJsonAsync(url, JsonUtility.ToJson(requestDto));
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[HttpBackendClient] ValidateTokenAsync threw ({ex.GetType().Name}); treating as unreachable.");
                return JoinTokenValidationResult.Reject(JoinTokenRejectReason.BackendUnreachable);
            }

            if (outcome != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"[HttpBackendClient] validate-token failed: {outcome}, HTTP {statusCode}. Failing closed.");
                return JoinTokenValidationResult.Reject(JoinTokenRejectReason.BackendUnreachable);
            }

            ValidateTokenResponseDto response;
            try
            {
                response = JsonUtility.FromJson<ValidateTokenResponseDto>(responseBody);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[HttpBackendClient] validate-token response was not valid JSON ({ex.GetType().Name}). Failing closed.");
                return JoinTokenValidationResult.Reject(JoinTokenRejectReason.BackendUnreachable);
            }

            if (!response.approved)
            {
                return JoinTokenValidationResult.Reject(ParseRejectReason(response.reason));
            }

            return JoinTokenValidationResult.Approve(response.accountId, response.displayName, response.role, response.slotIndex);
        }

        /// <summary>
        /// TLS-E03-S03: POST /api/v1/matches/{matchId}/heartbeat (docs/pivot/02_BACKEND_CONTRACT.md §3.2).
        /// Per the brief, a failed/unreachable backend must not crash or end the match — any failure here
        /// returns Acknowledged=false/Directive=None so the caller (ServerHeartbeatLoop) just retries next
        /// interval; it never throws out of this method.
        /// </summary>
        public async Task<HeartbeatResult> SendHeartbeatAsync(int connectedPlayerCount, string sessionState, float uptimeSeconds)
        {
            string url = $"{backendUrl}/api/v1/matches/{matchId}/heartbeat";
            HeartbeatRequestDto requestDto = new HeartbeatRequestDto
            {
                connectedPlayerCount = connectedPlayerCount,
                sessionState = sessionState ?? string.Empty,
                uptimeSeconds = uptimeSeconds
            };

            UnityWebRequest.Result outcome;
            string responseBody;
            long statusCode;

            try
            {
                (outcome, responseBody, statusCode) = await PostJsonAsync(url, JsonUtility.ToJson(requestDto));
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[HttpBackendClient] SendHeartbeatAsync threw ({ex.GetType().Name}: {ex.Message}).");
                return new HeartbeatResult(acknowledged: false, directive: HeartbeatDirective.None);
            }

            if (outcome != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"[HttpBackendClient] heartbeat failed: {outcome}, HTTP {statusCode}.");
                return new HeartbeatResult(acknowledged: false, directive: HeartbeatDirective.None);
            }

            HeartbeatResponseDto response;
            try
            {
                response = JsonUtility.FromJson<HeartbeatResponseDto>(responseBody);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[HttpBackendClient] heartbeat response was not valid JSON ({ex.GetType().Name}).");
                return new HeartbeatResult(acknowledged: false, directive: HeartbeatDirective.None);
            }

            HeartbeatDirective directive = response.directive == "SHUTDOWN" ? HeartbeatDirective.Shutdown : HeartbeatDirective.None;
            return new HeartbeatResult(response.acknowledged, directive);
        }

        /// <summary>
        /// TLS-E03-S03: POST /api/v1/matches/{matchId}/result (docs/pivot/02_BACKEND_CONTRACT.md §3.3),
        /// with an "Idempotency-Key: {matchId}-result" header and bounded exponential-backoff retries (up
        /// to ~60s total) — "a match result must never be silently lost" per the contract doc. A 400
        /// (malformed payload) is treated as non-transient and not retried, since retrying the same bad
        /// payload can't help. Called at most once per match by GameManager regardless of outcome.
        /// </summary>
        public async Task<bool> PostResultAsync(MatchResult result)
        {
            string url = $"{backendUrl}/api/v1/matches/{matchId}/result";
            string idempotencyKey = $"{matchId}-result";

            MatchPlayerResultDto[] playerDtos = new MatchPlayerResultDto[result.Players.Length];
            for (int i = 0; i < result.Players.Length; i++)
            {
                MatchPlayerResult p = result.Players[i];
                playerDtos[i] = new MatchPlayerResultDto
                {
                    accountId = p.AccountId,
                    role = p.Role,
                    incapacitated = p.Incapacitated,
                    finalStatus = p.FinalStatus
                };
            }

            string jsonBody = JsonUtility.ToJson(new MatchResultRequestDto
            {
                outcome = result.Outcome.ToString(),
                durationSeconds = result.DurationSeconds,
                players = playerDtos
            });

            const float maxTotalWaitSeconds = 60f;
            float waited = 0f;
            float backoffSeconds = 2f;

            while (true)
            {
                UnityWebRequest.Result outcome;
                long statusCode;

                try
                {
                    (outcome, _, statusCode) = await PostJsonAsync(url, jsonBody, idempotencyKey);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[HttpBackendClient] PostResultAsync threw ({ex.GetType().Name}: {ex.Message}).");
                    outcome = UnityWebRequest.Result.ConnectionError;
                    statusCode = 0;
                }

                if (outcome == UnityWebRequest.Result.Success)
                {
                    Debug.Log($"[HttpBackendClient] Match result reported (HTTP {statusCode}).");
                    return true;
                }

                if (statusCode == 400)
                {
                    Debug.LogError($"[HttpBackendClient] Match result rejected as malformed (HTTP 400) — not retrying.");
                    return false;
                }

                if (waited >= maxTotalWaitSeconds)
                {
                    Debug.LogError($"[HttpBackendClient] Match result could not be delivered after {waited:F0}s of retries " +
                                    $"({outcome}, HTTP {statusCode}). Giving up — this result is at risk of being lost; manual follow-up needed.");
                    return false;
                }

                float delay = Mathf.Min(backoffSeconds, maxTotalWaitSeconds - waited);
                Debug.LogWarning($"[HttpBackendClient] Match result POST failed ({outcome}, HTTP {statusCode}); retrying in {delay:F0}s.");
                await Task.Delay(TimeSpan.FromSeconds(delay));
                waited += delay;
                backoffSeconds = Mathf.Min(backoffSeconds * 2f, 16f);
            }
        }

        private async Task<(UnityWebRequest.Result outcome, string body, long statusCode)> PostJsonAsync(string url, string jsonBody, string idempotencyKey = null)
        {
            using UnityWebRequest request = new UnityWebRequest(url, "POST");
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Authorization", $"Bearer {serverSecret}");
            if (!string.IsNullOrEmpty(idempotencyKey))
            {
                request.SetRequestHeader("Idempotency-Key", idempotencyKey);
            }
            request.timeout = timeoutSeconds;

            UnityWebRequestAsyncOperation operation = request.SendWebRequest();
            while (!operation.isDone)
            {
                await Task.Yield();
            }

            return (request.result, request.downloadHandler.text, request.responseCode);
        }

        private static JoinTokenRejectReason ParseRejectReason(string reason)
        {
            switch (reason)
            {
                case "TOKEN_EXPIRED": return JoinTokenRejectReason.TokenExpired;
                case "TOKEN_ALREADY_USED": return JoinTokenRejectReason.TokenAlreadyUsed;
                case "TOKEN_WRONG_MATCH": return JoinTokenRejectReason.TokenWrongMatch;
                case "MATCH_FULL": return JoinTokenRejectReason.MatchFull;
                case "MATCH_NOT_FOUND": return JoinTokenRejectReason.MatchNotFound;
                default: return JoinTokenRejectReason.TokenInvalid;
            }
        }
    }
}
