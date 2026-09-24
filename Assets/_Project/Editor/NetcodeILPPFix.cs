#if UNITY_EDITOR
using System;
using System.Collections;
using System.Reflection;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;

namespace TheLastSignal.Editor
{
    /// <summary>
    /// Fixes a known Unity Netcode for GameObjects bug where ILPPMessageProvider.__network_message_types
    /// static list accumulates duplicate items (e.g. Allowed Count: 100 | Index Count: 25) when entering/exiting
    /// Play Mode without Domain Reload or during MPPM virtual player sessions.
    /// </summary>
    [InitializeOnLoad]
    public static class NetcodeILPPFix
    {
        static NetcodeILPPFix()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.ExitingEditMode || change == PlayModeStateChange.EnteredPlayMode)
            {
                SanitizeMessageTypes();
            }
        }

        public static void SanitizeMessageTypes()
        {
            try
            {
                Type providerType = typeof(NetworkManager).Assembly.GetType("Unity.Netcode.ILPPMessageProvider");
                Type enumType = typeof(NetworkManager).Assembly.GetType("Unity.Netcode.NetworkMessageTypes");
                if (providerType == null || enumType == null)
                {
                    return;
                }

                FieldInfo field = providerType.GetField("__network_message_types", BindingFlags.NonPublic | BindingFlags.Static);
                if (field == null)
                {
                    return;
                }

                if (field.GetValue(null) is IList list)
                {
                    int expectedCount = Enum.GetValues(enumType).Length;
                    if (list.Count > expectedCount)
                    {
                        Debug.LogWarning($"[NetcodeILPPFix] Trimming duplicate Netcode message types ({list.Count} -> {expectedCount}).");
                        while (list.Count > expectedCount)
                        {
                            list.RemoveAt(list.Count - 1);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[NetcodeILPPFix] Failed to sanitize message types: {ex.Message}");
            }
        }
    }
}
#endif
