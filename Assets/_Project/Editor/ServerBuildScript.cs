using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace TheLastSignal.Editor
{
    /// <summary>
    /// TLS-E03-S04: builds the headless Dedicated Server player via a menu item, using the installed
    /// Editor's Server build subtarget. Verified present on this machine before writing this script:
    /// LinuxStandaloneSupport already ships linux64_server_nondevelopment_il2cpp (and friends) — no
    /// extra Unity Hub module install was needed here, contrary to the audit's original "unverified,
    /// check Hub" note. Output is gitignored (Builds/), never checked in.
    /// </summary>
    public static class ServerBuildScript
    {
        private const string OutputDirectory = "Builds/DedicatedServer/Linux";
        private const string ExecutableName = "TheLastSignalServer";

        [MenuItem("TheLastSignal/Build/Dedicated Server (Linux)")]
        public static void BuildLinuxDedicatedServer()
        {
            Build(BuildTarget.StandaloneLinux64, OutputDirectory, ExecutableName);
        }

        /// <summary>
        /// Windows headless fallback — per docs/pivot/01_PIVOT_PLAN.md's Stop criteria, if the Linux
        /// module ever turns out unusable on a teammate's machine, a Windows server build
        /// (-batchmode -nographics) is the documented interim target. Not the primary path, but kept
        /// one menu click away rather than undocumented.
        /// </summary>
        [MenuItem("TheLastSignal/Build/Dedicated Server (Windows, fallback)")]
        public static void BuildWindowsDedicatedServer()
        {
            Build(BuildTarget.StandaloneWindows64, "Builds/DedicatedServer/Windows", ExecutableName + ".exe");
        }

        private static void Build(BuildTarget target, string outputDirectory, string executableName)
        {
            List<string> enabledScenes = new List<string>();
            foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
            {
                if (scene.enabled)
                {
                    enabledScenes.Add(scene.path);
                }
            }

            if (enabledScenes.Count == 0)
            {
                Debug.LogError("[ServerBuildScript] No enabled scenes in Build Settings — aborting.");
                return;
            }

            Directory.CreateDirectory(outputDirectory);
            string outputPath = Path.Combine(outputDirectory, executableName);

            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = enabledScenes.ToArray(),
                locationPathName = outputPath,
                target = target,
                subtarget = (int)StandaloneBuildSubtarget.Server,
                options = BuildOptions.None
            };

            Debug.Log($"[ServerBuildScript] Building Dedicated Server ({target}) -> {outputPath} " +
                      $"with scenes: {string.Join(", ", enabledScenes)}");

            BuildReport report = BuildPipeline.BuildPlayer(options);

            if (report.summary.result == BuildResult.Succeeded)
            {
                Debug.Log($"[ServerBuildScript] Build succeeded: {report.summary.totalSize} bytes, {report.summary.totalTime}. Output: {outputPath}");
            }
            else
            {
                Debug.LogError($"[ServerBuildScript] Build {report.summary.result} with {report.summary.totalErrors} error(s). See the Console for details.");
            }
        }
    }
}
