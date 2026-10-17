using System;
using UnityEngine;

namespace TheLastSignal.Core.Bootstrap
{
    /// <summary>
    /// TLS-E03-S01: resolves which <see cref="LaunchMode"/> this process runs in, once, before any
    /// scene object exists. Runs via [RuntimeInitializeOnLoadMethod(BeforeSceneLoad)] specifically so
    /// that every other script — GameManager, LobbyUI, HUDManager, CameraFollow — can safely read
    /// <see cref="CurrentMode"/> from their own Awake()/OnEnable() with zero script-execution-order risk:
    /// BeforeSceneLoad is guaranteed to finish before the scene (and therefore every scene object's
    /// Awake()) even begins loading. With no "-mode" argument this resolves to LegacyClientHost, so the
    /// game behaves exactly as it did before this story (ARCHITECTURE_RULES.md Rule 7).
    /// </summary>
    public static class GameBootstrap
    {
        public static LaunchMode CurrentMode { get; private set; } = LaunchMode.LegacyClientHost;
        public static LaunchModeConfig Config { get; private set; } = new LaunchModeConfig();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ResolveLaunchMode()
        {
            Config = ServerBootstrap.ParseArgs(Environment.GetCommandLineArgs());

#if UNITY_EDITOR
            // The Editor's own process obviously isn't launched with "-mode server", so there would
            // otherwise be no way to Play-test DedicatedServer mode without building. This override is
            // SessionState (not EditorPrefs), so it clears itself when the Editor closes rather than
            // silently persisting across sessions/projects. See the TheLastSignal/Launch Mode menu.
            if (UnityEditor.SessionState.GetBool(EditorForceServerModeKey, false))
            {
                Config.Mode = LaunchMode.DedicatedServer;
                Debug.Log("[GameBootstrap] Editor override active: forcing DedicatedServer mode for this " +
                          "Play session (TheLastSignal/Launch Mode/Force Dedicated Server On Next Play).");
            }
#endif

            CurrentMode = Config.Mode;

            Debug.Log($"[GameBootstrap] Launch mode resolved: {ServerBootstrap.DescribeForLog(Config)}");
        }

#if UNITY_EDITOR
        private const string EditorForceServerModeKey = "TheLastSignal.GameBootstrap.ForceServerMode";

        [UnityEditor.MenuItem("TheLastSignal/Launch Mode/Force Dedicated Server On Next Play")]
        private static void EditorForceDedicatedServerNextPlay()
        {
            UnityEditor.SessionState.SetBool(EditorForceServerModeKey, true);
            Debug.Log("[GameBootstrap] Will force DedicatedServer mode on the next Play session in this " +
                      "Editor session. Use 'Clear Forced Launch Mode' to go back to normal Play, or just " +
                      "restart the Editor (this clears automatically).");
        }

        [UnityEditor.MenuItem("TheLastSignal/Launch Mode/Clear Forced Launch Mode")]
        private static void EditorClearForcedLaunchMode()
        {
            UnityEditor.SessionState.SetBool(EditorForceServerModeKey, false);
            Debug.Log("[GameBootstrap] Forced launch mode cleared; next Play session uses normal/CLI-resolved mode.");
        }
#endif
    }
}
