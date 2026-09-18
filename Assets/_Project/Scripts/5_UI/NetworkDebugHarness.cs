using System;
using Unity.Netcode;
using UnityEngine;
using TheLastSignal.Core;

namespace TheLastSignal.UI
{
    /// <summary>
    /// ============================ TEMP / DEBUG UI — STORY 3.1 ONLY ============================
    /// Throwaway IMGUI harness whose ONLY job is to prove that NGO + Relay + Authentication work
    /// end-to-end. It has exactly two controls: a "Host" button and a join-code field + "Join" button.
    ///
    /// THIS IS NOT THE LOBBY. Story 3.2 builds the real lobby in LobbyUI.cs and this whole file should
    /// be deleted then. Do not add features here, and do not let gameplay code depend on it.
    /// =========================================================================================
    /// </summary>
    public class NetworkDebugHarness : MonoBehaviour
    {
        [SerializeField] private NetworkManagerExt networkManagerExt;

        private string joinCodeInput = string.Empty;
        private string hostJoinCode = string.Empty;
        private string status = "Starting Unity Services…";
        private bool busy;

        private void Awake()
        {
            if (networkManagerExt == null)
            {
                networkManagerExt = FindAnyObjectByType<NetworkManagerExt>();
            }
        }

        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(10f, 10f, 320f, 260f), GUI.skin.box);

            GUILayout.Label("<b>TEMP DEBUG — Story 3.1</b>\nNot the Lobby (that is Story 3.2).",
                new GUIStyle(GUI.skin.label) { richText = true, wordWrap = true });

            GUILayout.Space(4f);
            GUILayout.Label($"Services: {DescribeServices()}");
            GUILayout.Label($"Network: {DescribeNetwork()}");
            GUILayout.Label($"Status: {status}", new GUIStyle(GUI.skin.label) { wordWrap = true });
            GUILayout.Space(6f);

            bool canAct = !busy
                          && networkManagerExt != null
                          && GameManager.Instance != null
                          && GameManager.Instance.IsSignedIn
                          && NetworkManager.Singleton != null
                          && !NetworkManager.Singleton.IsListening;

            GUI.enabled = canAct;

            // Control 1 — Host: allocate Relay, then start as host.
            if (GUILayout.Button("Host", GUILayout.Height(30f)))
            {
                HostAsync();
            }

            GUILayout.Space(6f);

            // Control 2 — join code field + Join button: join Relay allocation, then start as client.
            GUILayout.Label("Join code:");
            joinCodeInput = GUILayout.TextField(joinCodeInput ?? string.Empty, 12);

            if (GUILayout.Button("Join", GUILayout.Height(30f)))
            {
                JoinAsync();
            }

            GUI.enabled = true;

            if (!string.IsNullOrEmpty(hostJoinCode))
            {
                GUILayout.Space(6f);
                GUILayout.Label("Share this join code:");
                // Shown in a text field purely so it can be selected and copied; the value is not read.
                GUILayout.TextField(hostJoinCode);
            }

            GUILayout.EndArea();
        }

        private async void HostAsync()
        {
            busy = true;
            status = "Allocating Relay…";

            try
            {
                hostJoinCode = await networkManagerExt.CreateRelay();

                if (networkManagerExt.StartHost())
                {
                    status = "Hosting.";

                    // The Host owns the session (ARCHITECTURE_RULES.md Rule 1). In NetworkTestScene
                    // GameManager.startSessionOnPlay is false, so the Epic 1 loop starts here instead
                    // of at scene load — otherwise it would spawn a local player before NGO exists.
                    // Story 3.2's real lobby should own this trigger.
                    GameManager.Instance?.StartSession();
                }
                else
                {
                    status = "StartHost() was refused by NetworkManager.";
                }
            }
            catch (Exception ex)
            {
                status = $"Host failed ({ex.GetType().Name}): {ex.Message}";
                Debug.LogError($"[NetworkDebugHarness] Host failed: {ex}");
            }
            finally
            {
                busy = false;
            }
        }

        private async void JoinAsync()
        {
            busy = true;
            status = "Joining Relay…";

            try
            {
                await networkManagerExt.JoinRelay(joinCodeInput);

                if (networkManagerExt.StartClient())
                {
                    status = "Client started.";
                }
                else
                {
                    status = "StartClient() was refused by NetworkManager.";
                }
            }
            catch (Exception ex)
            {
                status = $"Join failed ({ex.GetType().Name}): {ex.Message}";
                Debug.LogError($"[NetworkDebugHarness] Join failed: {ex}");
            }
            finally
            {
                busy = false;
            }
        }

        private static string DescribeServices()
        {
            if (GameManager.Instance == null)
            {
                return "no GameManager in scene";
            }

            return GameManager.Instance.ServicesState.ToString();
        }

        private static string DescribeNetwork()
        {
            NetworkManager manager = NetworkManager.Singleton;
            if (manager == null)
            {
                return "no NetworkManager";
            }

            if (manager.IsHost)
            {
                return $"Host ({manager.ConnectedClientsIds.Count} connected)";
            }

            if (manager.IsClient)
            {
                return manager.IsConnectedClient ? "Client (connected)" : "Client (connecting…)";
            }

            return "offline";
        }
    }
}
