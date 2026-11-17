using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using TheLastSignal.Core;
using TheLastSignal.Entities.Player;

namespace TheLastSignal.Systems.Comms
{
    /// <summary>
    /// Network-synchronized communication agent attached to each Player entity.
    /// Handles Server-authoritative routing of global text chat and voice packets.
    /// Strictly excludes The Shade role from sending or receiving radio transmissions (ARCHITECTURE_RULES Rule 5).
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    public class SurvivorCommsAgent : NetworkBehaviour
    {
        private PlayerController playerController;
        private PlayerRole playerRole;
        private PlayerVoiceEmitter voiceEmitter;
        private VoiceRecorder voiceRecorder;

        private bool wasPttPressedLastFrame = false;

        private static bool IsNetworkSessionActive =>
            NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;

        // Static Events for UI and Audio listeners across all peers
        public static event Action<string, string, int> OnGlobalChatMessageReceived;
        public static event Action<SurvivorCommsAgent, bool, float> OnGlobalVoiceStateChanged;
        public static event Action<string> OnCommsBlocked;

        public PlayerController Controller
        {
            get
            {
                if (playerController == null) playerController = GetComponent<PlayerController>();
                return playerController;
            }
        }

        public PlayerVoiceEmitter Emitter
        {
            get
            {
                if (voiceEmitter == null) voiceEmitter = GetComponent<PlayerVoiceEmitter>();
                return voiceEmitter;
            }
        }

        public bool IsMonster
        {
            get
            {
                if (playerController == null) playerController = GetComponent<PlayerController>();
                return playerController != null && playerController.IsMonster;
            }
        }

        private void Awake()
        {
            playerController = GetComponent<PlayerController>();
            playerRole = GetComponent<PlayerRole>();

            voiceEmitter = GetComponent<PlayerVoiceEmitter>();
            if (voiceEmitter == null)
            {
                voiceEmitter = gameObject.AddComponent<PlayerVoiceEmitter>();
            }

            voiceEmitter.OnSpeakingStateChanged += HandleRemoteSpeakingStateChanged;
        }

        private void HandleRemoteSpeakingStateChanged(bool speaking, float level)
        {
            OnGlobalVoiceStateChanged?.Invoke(this, speaking, level);
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            if (IsOwner)
            {
                InitializeVoiceRecorder();
            }
        }

        private void Start()
        {
            // Single-player / offline fallback: Owner of the single local player
            if (!IsNetworkSessionActive && voiceRecorder == null)
            {
                InitializeVoiceRecorder();
            }
        }

        private void InitializeVoiceRecorder()
        {
            if (voiceRecorder == null)
            {
                voiceRecorder = new VoiceRecorder();
                voiceRecorder.OnVoiceChunkReady += HandleLocalVoiceChunkReady;
            }
        }

        public override void OnNetworkDespawn()
        {
            DisposeVoiceRecorder();
            base.OnNetworkDespawn();
        }

        private void OnDestroy()
        {
            if (voiceEmitter != null)
            {
                voiceEmitter.OnSpeakingStateChanged -= HandleRemoteSpeakingStateChanged;
            }
            DisposeVoiceRecorder();
        }

        private void DisposeVoiceRecorder()
        {
            if (voiceRecorder != null)
            {
                voiceRecorder.OnVoiceChunkReady -= HandleLocalVoiceChunkReady;
                voiceRecorder.Dispose();
                voiceRecorder = null;
            }
        }

        private void Update()
        {
            bool isLocal = !IsNetworkSessionActive || (IsSpawned && IsOwner);
            if (!isLocal) return;

            bool isMonster = IsMonster;
            bool allowTransmit = !isMonster;

            // Update voice recording capture
            if (voiceRecorder != null)
            {
                voiceRecorder.Update(allowTransmit);

                // Notify UI of local transmission state
                if (voiceRecorder.IsPushToTalkActive)
                {
                    OnGlobalVoiceStateChanged?.Invoke(this, true, voiceRecorder.CurrentVoiceLevel);
                }
            }

            // Check if player attempts to press Push-to-Talk while being The Shade
            bool pttKeyPressed = Keyboard.current != null && Keyboard.current.vKey.isPressed;
            if (isMonster && pttKeyPressed && !wasPttPressedLastFrame)
            {
                TriggerCommsBlockedFeedback("THE SHADE HAS NO TRANSCEIVER // CANNOT TRANSMIT VOICE");
            }
            wasPttPressedLastFrame = pttKeyPressed;
        }

        private void HandleLocalVoiceChunkReady(byte[] pcmData, int sampleCount, int sampleRate, float rms)
        {
            // Monster role check safety guard
            if (IsMonster) return;

            if (IsNetworkSessionActive)
            {
                SubmitVoicePacketServerRpc(pcmData, sampleCount, sampleRate);
            }
            else
            {
                // Single-player / offline mic loopback test
                if (voiceEmitter != null)
                {
                    float[] samples = VoiceAudioSynthesizer.Pcm16ToFloat(pcmData, pcmData.Length);
                    voiceEmitter.PlayVoiceChunk(samples, sampleRate);
                }
            }
        }

        public void TriggerCommsBlockedFeedback(string reason)
        {
            OnCommsBlocked?.Invoke(reason);
        }

        // =====================================================================
        // TEXT CHAT TRANSMISSION
        // =====================================================================

        /// <summary>
        /// Submits a global text chat message.
        /// Rejects transmission if local player is The Shade.
        /// </summary>
        public void SendChatMessage(string message)
        {
            if (string.IsNullOrWhiteSpace(message)) return;

            // Reject if Monster
            if (IsMonster)
            {
                TriggerCommsBlockedFeedback("THE SHADE HAS NO TRANSCEIVER // CANNOT TRANSMIT TEXT");
                return;
            }

            // Clean message
            string cleanMsg = message.Trim();
            if (cleanMsg.Length > 120)
            {
                cleanMsg = cleanMsg.Substring(0, 120);
            }

            if (IsNetworkSessionActive)
            {
                SubmitChatMessageServerRpc(cleanMsg);
            }
            else
            {
                // Single-player / offline delivery
                string senderName = GetFormattedSenderName();
                int slot = playerController != null ? playerController.SlotIndex : 0;
                OnGlobalChatMessageReceived?.Invoke(senderName, cleanMsg, slot);
            }
        }

        [ServerRpc]
        private void SubmitChatMessageServerRpc(string message, ServerRpcParams rpcParams = default)
        {
            // Strict Server-Authoritative check: Reject if sender is Monster
            if (playerController != null && playerController.IsMonster)
            {
                Debug.LogWarning($"<color=orange>[SurvivorCommsAgent]</color> Server rejected chat from Monster (ClientId={rpcParams.Receive.SenderClientId}).");
                return;
            }

            string senderName = GetFormattedSenderName();
            int slot = playerController != null ? playerController.SlotIndex : 0;

            // Hidden Information (Rule 5): Filter recipients so The Shade client NEVER receives human radio comms
            List<ulong> survivorClientIds = GetSurvivorClientIds();
            if (survivorClientIds.Count == 0) return;

            ClientRpcParams targetParams = new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = survivorClientIds
                }
            };

            ReceiveChatMessageClientRpc(senderName, message, slot, targetParams);
        }

        [ClientRpc]
        private void ReceiveChatMessageClientRpc(string senderName, string message, int slotIndex, ClientRpcParams clientRpcParams = default)
        {
            // Client-side guard (defense-in-depth)
            if (IsMonster) return;

            OnGlobalChatMessageReceived?.Invoke(senderName, message, slotIndex);
        }

        // =====================================================================
        // VOICE CHAT TRANSMISSION
        // =====================================================================

        [ServerRpc]
        private void SubmitVoicePacketServerRpc(byte[] voiceData, int sampleCount, int sampleRate, ServerRpcParams rpcParams = default)
        {
            // Strict Server-Authoritative check: Reject if sender is Monster
            if (playerController != null && playerController.IsMonster)
            {
                return;
            }

            ulong senderClientId = rpcParams.Receive.SenderClientId;

            // Target ONLY survivors, excluding the sender itself
            List<ulong> survivorClientIds = GetSurvivorClientIds(senderClientId);
            if (survivorClientIds.Count == 0) return;

            ClientRpcParams targetParams = new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = survivorClientIds
                }
            };

            ReceiveVoicePacketClientRpc(voiceData, sampleCount, sampleRate, targetParams);
        }

        [ClientRpc]
        private void ReceiveVoicePacketClientRpc(byte[] voiceData, int sampleCount, int sampleRate, ClientRpcParams clientRpcParams = default)
        {
            // Client-side guard: Monster cannot hear human radio
            if (IsMonster) return;

            float[] samples = VoiceAudioSynthesizer.Pcm16ToFloat(voiceData, voiceData.Length);
            if (voiceEmitter != null)
            {
                voiceEmitter.PlayVoiceChunk(samples, sampleRate);
            }
        }

        // =====================================================================
        // RECIPIENT FILTERING HELPERS (RULE 5)
        // =====================================================================

        private List<ulong> GetSurvivorClientIds(ulong excludeClientId = ulong.MaxValue)
        {
            List<ulong> survivors = new List<ulong>();
            if (NetworkManager.Singleton == null) return survivors;

            IReadOnlyList<NetworkClient> clients = NetworkManager.Singleton.ConnectedClientsList;
            for (int i = 0; i < clients.Count; i++)
            {
                NetworkClient c = clients[i];
                if (c.ClientId == excludeClientId) continue;

                if (c.PlayerObject != null && c.PlayerObject.TryGetComponent(out PlayerController ctrl))
                {
                    // Exclude monster roles
                    if (!ctrl.IsMonster)
                    {
                        survivors.Add(c.ClientId);
                    }
                }
            }

            return survivors;
        }

        public string GetFormattedSenderName()
        {
            int slot = playerController != null ? playerController.SlotIndex : 0;
            string roleTitle = GetRoleTitle();
            return $"P{slot + 1} - {roleTitle}";
        }

        public string GetRoleTitle()
        {
            if (playerController != null && playerController.IsMonster) return "The Shade";

            if (playerRole != null)
            {
                return playerRole.Role switch
                {
                    SurvivorRole.Engineer => "Engineer",
                    SurvivorRole.DroneOperator => "Drone Operator",
                    SurvivorRole.Logistics => "Logistics Officer",
                    _ => "Survivor"
                };
            }

            return "Survivor";
        }
    }
}
