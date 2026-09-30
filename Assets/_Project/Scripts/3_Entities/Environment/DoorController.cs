using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using TheLastSignal.Core.Interaction;
using TheLastSignal.Entities.Player;

namespace TheLastSignal.Entities.Environment
{
    public enum DoorState
    {
        Closed,
        Open
    }

    /// <summary>
    /// Manages state for interactive facility doors. Implements unified IInteractable interface.
    /// Toggles BoxCollider2D blocker and updates visual state when interacted with ([E]).
    /// Maintains static registry for network synchronization.
    ///
    /// Story 3.5: Door Open/Closed is Host-authoritative, replicated via its OWN per-door
    /// NetworkVariable&lt;DoorState&gt; (one door, one NetworkObject, one NetworkVariable — 12 doors can't
    /// share a singleton). A client interacting sends RequestToggleRpc; the Host validates (requester
    /// actually in range) and writes networkState; every peer (Host included) reacts only via
    /// networkState's OnValueChanged callback, never by mutating state directly
    /// (ARCHITECTURE_RULES.md Rule 2/3). In single-player (no NetworkManager, IsSpawned always false)
    /// Interact() falls back to mutating locally, exactly as before this story.
    /// </summary>
    [RequireComponent(typeof(BoxCollider2D))]
    [RequireComponent(typeof(NetworkObject))]
    public class DoorController : NetworkBehaviour, IInteractable
    {
        private static readonly Dictionary<string, DoorController> doorRegistry = new Dictionary<string, DoorController>();

        public static DoorController GetDoor(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            doorRegistry.TryGetValue(name, out var door);
            return door;
        }

        public static IReadOnlyDictionary<string, DoorController> AllDoors => doorRegistry;

        [Header("State")]
        [Tooltip("Starting state, applied to the NetworkVariable by the Host on spawn.")]
        [SerializeField] private DoorState initialState = DoorState.Closed;
        [SerializeField] private string doorName = "Door";

        [Header("Components")]
        [SerializeField] private BoxCollider2D physicalCollider;
        [SerializeField] private SpriteRenderer spriteRenderer;

        [Header("Door Sprites")]
        [SerializeField] private Sprite closedSprite;
        [SerializeField] private Sprite openSprite;

        [Header("Visual Feedback (Color Fallback)")]
        [SerializeField] private Color closedColor = new Color(0.95f, 0.45f, 0.15f, 1f); // Warning orange/red
        [SerializeField] private Color openColor = new Color(0.2f, 0.8f, 0.4f, 0.35f);    // Translucent green

        /// <summary>Host-write, everyone-read. The single source of truth for this door's state.</summary>
        private readonly NetworkVariable<DoorState> networkState = new NetworkVariable<DoorState>(
            DoorState.Closed, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        /// <summary>Non-networked fallback used only when there is no NetworkManager at all (single-player).</summary>
        private DoorState localState = DoorState.Closed;

        public event System.Action<DoorState> OnStateChanged;

        public DoorState CurrentState => IsSpawned ? networkState.Value : localState;
        public bool IsOpen => CurrentState == DoorState.Open;
        public string DoorName
        {
            get => doorName;
            set => doorName = value;
        }

        public string InteractionPrompt => CurrentState == DoorState.Open ? $"Close {doorName}" : $"Open {doorName}";
        public string GetInteractionPrompt() => InteractionPrompt;

        public bool CanInteract(PlayerController player) => true;

        private void Awake()
        {
            if (physicalCollider == null)
            {
                physicalCollider = GetComponent<BoxCollider2D>();
            }

            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponent<SpriteRenderer>();
            }

            localState = initialState;
            RegisterDoor();
            ApplyState();
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            if (IsServer)
            {
                networkState.Value = initialState;
            }

            networkState.OnValueChanged += HandleNetworkStateChanged;

            // Late-join safety net + immediate visual sync to whatever the Host has already replicated.
            ApplyState();
        }

        public override void OnNetworkDespawn()
        {
            networkState.OnValueChanged -= HandleNetworkStateChanged;
            base.OnNetworkDespawn();
        }

        public override void OnDestroy()
        {
            UnregisterDoor();
            base.OnDestroy();
        }

        private void RegisterDoor()
        {
            string key = string.IsNullOrEmpty(doorName) ? gameObject.name : doorName;
            doorRegistry[key] = this;
        }

        private void UnregisterDoor()
        {
            string key = string.IsNullOrEmpty(doorName) ? gameObject.name : doorName;
            if (doorRegistry.TryGetValue(key, out var registered) && registered == this)
            {
                doorRegistry.Remove(key);
            }
        }

        public void Initialize(string name, DoorState startState = DoorState.Closed)
        {
            UnregisterDoor();
            doorName = name;
            initialState = startState;
            localState = startState;
            if (IsSpawned && IsServer)
            {
                networkState.Value = startState;
            }
            RegisterDoor();
            if (physicalCollider == null) physicalCollider = GetComponent<BoxCollider2D>();
            if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
            ApplyState();
        }

        public void SetSprites(Sprite closed, Sprite open)
        {
            closedSprite = closed;
            openSprite = open;
            if (physicalCollider == null) physicalCollider = GetComponent<BoxCollider2D>();
            if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
            ApplyState();
        }

        public void SetColors(Color closed, Color open)
        {
            closedColor = closed;
            openColor = open;
            ApplyState();
        }

        /// <summary>
        /// Interaction handler called by PlayerController via unified IInteractable. Routes through the
        /// Host-validated ServerRpc when networked; falls back to a direct local toggle in single-player.
        /// </summary>
        public void Interact(PlayerController player)
        {
            if (IsSpawned)
            {
                RequestToggleRpc();
            }
            else
            {
                localState = localState == DoorState.Closed ? DoorState.Open : DoorState.Closed;
                ApplyState();
                Debug.Log($"[DoorController] {doorName} is now {localState} (single-player, local only).");
            }
        }

        /// <summary>
        /// Backward compatibility interaction handler.
        /// </summary>
        public void Interact(GameObject player)
        {
            Interact(player != null ? player.GetComponent<PlayerController>() : null);
        }

        /// <summary>
        /// Client entry point for toggling this door. The Host validates that the requesting player is
        /// actually within interaction range (server-computed from the sender's own PlayerObject
        /// position — never trusts a client-supplied position) before applying the change. This is the
        /// ONLY path that may write networkState while networked.
        /// </summary>
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void RequestToggleRpc(RpcParams rpcParams = default)
        {
            ulong senderId = rpcParams.Receive.SenderClientId;
            if (!IsSenderInRange(senderId))
            {
                Debug.LogWarning($"[DoorController] Rejecting toggle for '{doorName}' from client {senderId}: out of range.");
                return;
            }

            networkState.Value = networkState.Value == DoorState.Closed ? DoorState.Open : DoorState.Closed;
            Debug.Log($"<color=cyan>[DoorController]</color> {doorName} toggled to {networkState.Value}");
        }

        private bool IsSenderInRange(ulong clientId)
        {
            if (NetworkManager == null || !NetworkManager.ConnectedClients.TryGetValue(clientId, out NetworkClient client)
                || client.PlayerObject == null)
            {
                return false;
            }

            PlayerController player = client.PlayerObject.GetComponent<PlayerController>();
            float range = (player != null ? player.InteractionRange : 1f) + 1.2f;
            return ((Vector2)client.PlayerObject.transform.position - (Vector2)transform.position).sqrMagnitude <= range * range;
        }

        /// <summary>Host-only direct setter, kept for any future Host-side tooling that needs to force a state.</summary>
        public void SetState(DoorState state)
        {
            if (IsSpawned)
            {
                if (IsServer)
                {
                    networkState.Value = state;
                }
            }
            else
            {
                localState = state;
                ApplyState();
            }
        }

        private void HandleNetworkStateChanged(DoorState previous, DoorState current)
        {
            ApplyState();
        }

        private void ApplyState()
        {
            bool isOpen = CurrentState == DoorState.Open;

            if (physicalCollider != null)
            {
                // Physical blocker: when Closed, acts as solid obstacle (isTrigger = false).
                // When Open, acts as passable trigger (isTrigger = true) so player can walk through
                // while still allowing the interaction system to detect and close it via [E].
                physicalCollider.isTrigger = isOpen;
                physicalCollider.enabled = true;
            }

            if (spriteRenderer != null)
            {
                if (isOpen && openSprite != null)
                {
                    spriteRenderer.sprite = openSprite;
                    spriteRenderer.color = Color.white;
                }
                else if (!isOpen && closedSprite != null)
                {
                    spriteRenderer.sprite = closedSprite;
                    spriteRenderer.color = Color.white;
                }
                else
                {
                    spriteRenderer.color = isOpen ? openColor : closedColor;
                }
            }

            OnStateChanged?.Invoke(CurrentState);
        }
    }
}
