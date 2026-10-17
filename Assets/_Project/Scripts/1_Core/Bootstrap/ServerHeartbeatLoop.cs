using System;
using System.Threading.Tasks;
using UnityEngine;
using TheLastSignal.Core.Backend;

namespace TheLastSignal.Core.Bootstrap
{
    /// <summary>
    /// TLS-E03-S03: periodically calls IBackendClient.SendHeartbeatAsync (docs/pivot/02_BACKEND_CONTRACT.md
    /// §3.2, proposed default every 10s). A failed/unreachable backend must not crash or end the match —
    /// it is logged and simply retried on the next interval, per the brief. Ticked from
    /// GameManager.Update() only in DedicatedServer mode.
    /// </summary>
    public class ServerHeartbeatLoop
    {
        private readonly IBackendClient backendClient;
        private readonly float intervalSeconds;

        private float timeSinceLastHeartbeat;
        private bool heartbeatInFlight;

        public float UptimeSeconds { get; private set; }

        /// <summary>Raised if the backend's heartbeat response carries a Shutdown directive.</summary>
        public event Action ShutdownRequested;

        public ServerHeartbeatLoop(IBackendClient backendClient, float intervalSeconds = 10f)
        {
            this.backendClient = backendClient;
            this.intervalSeconds = Mathf.Max(1f, intervalSeconds);
        }

        public void Tick(float deltaTime, int connectedPlayerCount, string sessionState)
        {
            UptimeSeconds += deltaTime;
            timeSinceLastHeartbeat += deltaTime;

            if (heartbeatInFlight || timeSinceLastHeartbeat < intervalSeconds)
            {
                return;
            }

            timeSinceLastHeartbeat = 0f;
            _ = SendHeartbeatAsync(connectedPlayerCount, sessionState);
        }

        private async Task SendHeartbeatAsync(int connectedPlayerCount, string sessionState)
        {
            heartbeatInFlight = true;

            try
            {
                HeartbeatResult result = await backendClient.SendHeartbeatAsync(connectedPlayerCount, sessionState, UptimeSeconds);

                if (result.Directive == HeartbeatDirective.Shutdown)
                {
                    Debug.Log("[ServerHeartbeatLoop] Backend heartbeat directive: Shutdown.");
                    ShutdownRequested?.Invoke();
                }
            }
            catch (Exception ex)
            {
                // Per the brief: backend unreachable must not crash or end the match — log and retry
                // on the next interval only.
                Debug.LogWarning($"[ServerHeartbeatLoop] Heartbeat failed ({ex.GetType().Name}: {ex.Message}); will retry in {intervalSeconds}s.");
            }
            finally
            {
                heartbeatInFlight = false;
            }
        }
    }
}
