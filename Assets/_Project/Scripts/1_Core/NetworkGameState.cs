using Unity.Netcode;
using UnityEngine;
using TheLastSignal.Systems.Monster;
using TheLastSignal.Systems.Objectives;
using TheLastSignal.Core.EventBus;
using TheLastSignal.Entities.Environment;
using TheLastSignal.Entities.Player;

namespace TheLastSignal.Core
{
    /// <summary>
    /// The only class allowed to touch both the 2_Systems/ (Pure C#) layer and the Netcode layer. Holds
    /// every NetworkVariable in the project and bridges Host-side system state to replicated state that
    /// clients can read, and to EventChannelSO events that clients can subscribe to. Per
    /// ARCHITECTURE_RULES.md Rule 1, all writes to these NetworkVariables happen only when IsServer is true.
    ///
    /// ==================== Story 3.5 — Host-Authoritative States ====================
    /// Exactly four kinds of gameplay state are now Host-owned. All are read-everyone/write-server:
    ///   1. Session Phase        -> networkSessionState (GameSessionState: GameStart..GameOver).
    ///      Set only via GameManager.SetState() on the Host; clients mirror it via
    ///      GameManager.ApplyReplicatedSessionState(), never write it themselves.
    ///   2. Door Open/Closed     -> owned PER-DOOR by DoorController's own NetworkVariable&lt;DoorState&gt;,
    ///      NOT here — 12 independent doors can't share one singleton NetworkVariable. See DoorController.cs.
    ///   3. Reactor Activated    -> networkReactorActivated (bool). Set only via RequestActivateReactorRpc
    ///      after Host-side validation (not already activated, session is in Gameplay phase, requesting
    ///      player is actually in range).
    ///   4. Objective Phase + Evacuation Countdown -> networkObjectivePhase/networkObjectiveStatus (the
    ///      2-phase objective from SimpleObjectiveSystem) and networkEvacuationSeconds (ticked only by
    ///      the Host's GameManager.Update(), never by a client).
    /// A client requests a change via ServerRpc; the Host validates and writes the NetworkVariable; every
    /// peer (Host included) reacts only through the NetworkVariable's OnValueChanged callback — never by
    /// mutating local state directly. See ARCHITECTURE_RULES.md Rule 2/3.
    ///
    /// OUT OF SCOPE for Story 3.5 (left as TODOs, not attempted):
    ///   // TODO: Task Ticket System synchronization not yet implemented.
    ///   // TODO: Enemy (The Shade) state synchronization not yet implemented.
    ///   // TODO: Inventory/Pin item synchronization not yet implemented.
    /// =================================================================================
    /// </summary>
    public class NetworkGameState : NetworkBehaviour
    {
        /// <summary>Local-process singleton, mirroring GameManager's own convention. Set on spawn.</summary>
        public static NetworkGameState Instance { get; private set; }

        [SerializeField] private FloatEventChannelSO onSanityChanged;
        [SerializeField] private MonsterStateEventChannelSO onMonsterStateChanged;

        // Pre-existing Story 1 scaffold state — unrelated to Story 3.5, still TODO.
        private readonly NetworkVariable<float> sanity = new NetworkVariable<float>();
        private readonly NetworkVariable<MonsterState> monsterState = new NetworkVariable<MonsterState>();

        // ---- Story 3.5: Host-authoritative gameplay state ----
        private readonly NetworkVariable<GameSessionState> networkSessionState = new NetworkVariable<GameSessionState>(
            GameSessionState.GameStart, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<bool> networkReactorActivated = new NetworkVariable<bool>(
            false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<int> networkObjectivePhase = new NetworkVariable<int>(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<ObjectiveStatus> networkObjectiveStatus = new NetworkVariable<ObjectiveStatus>(
            ObjectiveStatus.NotStarted, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<float> networkEvacuationSeconds = new NetworkVariable<float>(
            0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private ReactorCoreConsole reactorConsoleCache;

        public override void OnNetworkSpawn()
        {
            Instance = this;

            // TODO: subscribe to sanity.OnValueChanged / monsterState.OnValueChanged to raise the
            // matching EventChannelSO on every peer (Host included) when replicated values change.
            // TODO: Enemy (The Shade) state synchronization not yet implemented.

            networkSessionState.OnValueChanged += HandleSessionStateChanged;
            networkReactorActivated.OnValueChanged += HandleReactorActivatedChanged;
            networkObjectivePhase.OnValueChanged += HandleObjectiveChanged;
            networkObjectiveStatus.OnValueChanged += HandleObjectiveChanged;
            networkEvacuationSeconds.OnValueChanged += HandleEvacuationSecondsChanged;

            if (!IsServer)
            {
                // Late-join / non-Host safety net: apply whatever the Host has already replicated right
                // now, in case a NetworkVariable's already-synced initial value doesn't itself raise
                // OnValueChanged on this peer.
                GameManager.Instance?.ApplyReplicatedSessionState(networkSessionState.Value);
                GameManager.Instance?.ApplyReplicatedObjective(networkObjectivePhase.Value, networkObjectiveStatus.Value);
                GameManager.Instance?.ApplyReplicatedEvacuationSeconds(networkEvacuationSeconds.Value);
                ResolveReactorConsole()?.ApplyNetworkActivation(networkReactorActivated.Value);
            }
        }

        public override void OnNetworkDespawn()
        {
            // TODO: unsubscribe from sanity/monsterState OnValueChanged callbacks (Story 1 scaffold).

            networkSessionState.OnValueChanged -= HandleSessionStateChanged;
            networkReactorActivated.OnValueChanged -= HandleReactorActivatedChanged;
            networkObjectivePhase.OnValueChanged -= HandleObjectiveChanged;
            networkObjectiveStatus.OnValueChanged -= HandleObjectiveChanged;
            networkEvacuationSeconds.OnValueChanged -= HandleEvacuationSecondsChanged;

            if (Instance == this)
            {
                Instance = null;
            }
        }

        /// <summary>Called by the Host to push a new sanity value from SanitySystem into replicated state.</summary>
        public void ServerSetSanity(float newValue)
        {
            // TODO: guard with IsServer, then assign sanity.Value = newValue.
        }

        /// <summary>Called by the Host to push a new monster state from MonsterFSM into replicated state.</summary>
        public void ServerSetMonsterState(MonsterState newState)
        {
            // TODO: Enemy (The Shade) state synchronization not yet implemented.
        }

        /// <summary>Client entry point for input: forwards a station interaction request to the Host.</summary>
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void RequestInteractRpc(RpcParams rpcParams = default)
        {
            // TODO: Task Ticket System synchronization not yet implemented.
        }

        /// <summary>
        /// Called only by GameManager, and only when it is Host-authoritative (see
        /// GameManager.IsHostAuthoritative), to replicate the session phase to every client.
        /// </summary>
        public void ServerSetSessionState(GameSessionState state)
        {
            if (!IsServer)
            {
                return;
            }

            networkSessionState.Value = state;

            if (state == GameSessionState.GameStart)
            {
                // A new session is starting: the reactor must not still read "activated" from a
                // previous run (this mirrors ReactorCoreConsole.ResetConsole(), which the Host's own
                // local copy also runs off this same GameStart transition via onGameSessionStateChanged).
                networkReactorActivated.Value = false;
            }
        }

        /// <summary>
        /// Called only by GameManager's Host-authoritative objective callbacks to replicate the current
        /// 2-phase objective (phase index + status) to every client.
        /// </summary>
        public void ServerSetObjective(int phase, ObjectiveStatus status)
        {
            if (!IsServer)
            {
                return;
            }

            networkObjectivePhase.Value = phase;
            networkObjectiveStatus.Value = status;
        }

        /// <summary>
        /// Called only by GameManager's Host-authoritative timer callback, once per Host tick, to
        /// replicate the remaining Evacuation Countdown seconds to every client.
        /// </summary>
        public void ServerSetEvacuationSeconds(float seconds)
        {
            if (!IsServer)
            {
                return;
            }

            networkEvacuationSeconds.Value = seconds;
        }

        /// <summary>
        /// Client entry point for activating the Reactor Core console. The Host validates that the
        /// console isn't already activated, the session is actually in the Gameplay phase, and the
        /// requesting player is really within interaction range of the console (server-computed from
        /// the sender's own PlayerObject position — never trusts a client-supplied position) before
        /// applying the change. This is the ONLY path that may set networkReactorActivated.
        /// </summary>
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void RequestActivateReactorRpc(RpcParams rpcParams = default)
        {
            if (networkReactorActivated.Value)
            {
                return;
            }

            if (GameManager.Instance == null || GameManager.Instance.CurrentState != GameSessionState.Gameplay)
            {
                return;
            }

            ReactorCoreConsole console = ResolveReactorConsole();
            if (console == null)
            {
                return;
            }

            ulong senderId = rpcParams.Receive.SenderClientId;
            if (!IsSenderInRangeOf(senderId, console.transform.position))
            {
                Debug.LogWarning($"[NetworkGameState] Rejected reactor activation from client {senderId}: out of range.");
                return;
            }

            networkReactorActivated.Value = true;
            GameManager.Instance.NotifyReactorActivated();
        }

        /// <summary>
        /// Server-side range check shared by any interaction that needs to validate "is this client's
        /// player actually near the target" without trusting a client-supplied position.
        /// </summary>
        private bool IsSenderInRangeOf(ulong clientId, Vector3 targetPosition)
        {
            if (NetworkManager == null || !NetworkManager.ConnectedClients.TryGetValue(clientId, out NetworkClient client)
                || client.PlayerObject == null)
            {
                return false;
            }

            PlayerController player = client.PlayerObject.GetComponent<PlayerController>();
            float range = (player != null ? player.InteractionRange : 1f) + 1.2f;
            return ((Vector2)client.PlayerObject.transform.position - (Vector2)targetPosition).sqrMagnitude <= range * range;
        }

        private ReactorCoreConsole ResolveReactorConsole()
        {
            if (reactorConsoleCache == null)
            {
                reactorConsoleCache = FindAnyObjectByType<ReactorCoreConsole>();
            }

            return reactorConsoleCache;
        }

        private void HandleSessionStateChanged(GameSessionState previous, GameSessionState current)
        {
            if (IsServer)
            {
                // The Host's own GameManager.SetState() already applied this locally before pushing it
                // here; re-applying its own echo would be redundant (harmless, but pointless).
                return;
            }

            GameManager.Instance?.ApplyReplicatedSessionState(current);
        }

        private void HandleReactorActivatedChanged(bool previous, bool current)
        {
            // No IsServer guard: unlike session state, nothing else updates the Host's own console
            // visuals/availability — this callback is the only path that ever does, on every peer.
            ResolveReactorConsole()?.ApplyNetworkActivation(current);
        }

        private void HandleObjectiveChanged(int previous, int current)
        {
            if (IsServer)
            {
                return;
            }

            GameManager.Instance?.ApplyReplicatedObjective(networkObjectivePhase.Value, networkObjectiveStatus.Value);
        }

        private void HandleObjectiveChanged(ObjectiveStatus previous, ObjectiveStatus current)
        {
            if (IsServer)
            {
                return;
            }

            GameManager.Instance?.ApplyReplicatedObjective(networkObjectivePhase.Value, networkObjectiveStatus.Value);
        }

        private void HandleEvacuationSecondsChanged(float previous, float current)
        {
            if (IsServer)
            {
                return;
            }

            GameManager.Instance?.ApplyReplicatedEvacuationSeconds(current);
        }
    }
}
