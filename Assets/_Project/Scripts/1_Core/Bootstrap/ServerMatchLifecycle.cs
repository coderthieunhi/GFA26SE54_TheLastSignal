using System;
using Unity.Netcode;
using UnityEngine;

namespace TheLastSignal.Core.Bootstrap
{
    /// <summary>
    /// TLS-E03-S03: aborts the match if the server has had zero connected clients for
    /// <c>emptyTimeoutSeconds</c>, or if the match has simply run longer than
    /// <c>matchTimeoutSeconds</c>, regardless of player count. Ticked from GameManager.Update() only in
    /// DedicatedServer mode. Fires <see cref="Aborted"/> at most once — callers must not need to guard
    /// against a double-fire themselves for THIS signal, though GameManager still guards the overall
    /// "send exactly one result" rule across all of Win/Lose/Abort.
    /// </summary>
    public class ServerMatchLifecycle
    {
        public enum AbortCause
        {
            ServerEmpty,
            MatchTimeout
        }

        private readonly NetworkManager networkManager;
        private readonly float emptyTimeoutSeconds;
        private readonly float matchTimeoutSeconds;

        private float emptyElapsedSeconds;
        private float matchElapsedSeconds;
        private bool aborted;

        public float MatchElapsedSeconds => matchElapsedSeconds;

        /// <summary>Raised exactly once, the first time either timeout is hit.</summary>
        public event Action<AbortCause> Aborted;

        public ServerMatchLifecycle(NetworkManager networkManager, float emptyTimeoutSeconds, float matchTimeoutSeconds)
        {
            this.networkManager = networkManager;
            this.emptyTimeoutSeconds = Mathf.Max(0f, emptyTimeoutSeconds);
            this.matchTimeoutSeconds = Mathf.Max(0f, matchTimeoutSeconds);
        }

        public void Tick(float deltaTime)
        {
            if (aborted)
            {
                return;
            }

            matchElapsedSeconds += deltaTime;

            if (matchTimeoutSeconds > 0f && matchElapsedSeconds >= matchTimeoutSeconds)
            {
                Fire(AbortCause.MatchTimeout);
                return;
            }

            int connectedCount = networkManager != null ? networkManager.ConnectedClientsIds.Count : 0;
            if (connectedCount > 0)
            {
                emptyElapsedSeconds = 0f;
                return;
            }

            emptyElapsedSeconds += deltaTime;
            if (emptyTimeoutSeconds > 0f && emptyElapsedSeconds >= emptyTimeoutSeconds)
            {
                Fire(AbortCause.ServerEmpty);
            }
        }

        private void Fire(AbortCause cause)
        {
            aborted = true;
            Debug.LogWarning($"[ServerMatchLifecycle] Aborting match: {cause} (elapsed={matchElapsedSeconds:F0}s, emptyFor={emptyElapsedSeconds:F0}s).");
            Aborted?.Invoke(cause);
        }
    }
}
