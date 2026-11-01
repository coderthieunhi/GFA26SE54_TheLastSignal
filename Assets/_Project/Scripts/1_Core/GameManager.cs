using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Services.Authentication;
using Unity.Services.Core;
using TheLastSignal.Core.Backend;
using TheLastSignal.Core.Bootstrap;
using TheLastSignal.Core.EventBus;
using TheLastSignal.Entities;
using TheLastSignal.Entities.Player;
using TheLastSignal.Systems;
using TheLastSignal.Systems.Lighting;
using TheLastSignal.Systems.Objectives;
using TheLastSignal.Systems.Sanity;

namespace TheLastSignal.Core
{
    /// <summary>
    /// Top-level orchestrator for a game session:
    /// Game Start -> Initialize Game -> Spawn Player -> Initialize Objective -> Gameplay -> Win/Lose.
    /// Host-side 2_Systems/ are created here. This story does not start Sanity, Task tickets, or The Shade.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("Session")]
        [SerializeField] private bool startSessionOnPlay = true;
        [SerializeField] private float evacuationDuration = 45f;
        [SerializeField] private PlayerSpawner playerSpawner;

        [Header("Events")]
        [SerializeField] private GameSessionStateEventChannelSO onGameSessionStateChanged;
        [SerializeField] private ObjectiveDataEventChannelSO onObjectiveChanged;
        [SerializeField] private FloatEventChannelSO onEvacuationTimerTicked;
        [SerializeField] private SanityThresholdEventChannelSO onSanityThresholdCrossed;

        [Header("Epic 5: Lighting & Sanity")]
        [SerializeField] private LightingConfigSO lightingConfig;
        [SerializeField] private SanityConfigSO sanityConfig;

        /// <summary>Lifecycle of the Unity Gaming Services bring-up (Story 3.1).</summary>
        public enum ServicesStatus
        {
            Uninitialized,
            Initializing,
            SignedIn,
            Failed
        }

        private GameSessionState currentState = GameSessionState.GameStart;
        private SimpleObjectiveSystem objectiveSystem;
        private LightingController lightingController;
        private SanitySystem sanitySystem;
        private IGameSystem[] activeSystems = System.Array.Empty<IGameSystem>();
        private ServicesStatus servicesStatus = ServicesStatus.Uninitialized;

        // TLS-E03-S02/S03: DedicatedServer-only state. Null in LegacyClientHost/Client modes.
        private IBackendClient currentBackendClient;
        private ServerConnectionApproval serverConnectionApproval;
        private ServerHeartbeatLoop serverHeartbeatLoop;
        private ServerMatchLifecycle serverMatchLifecycle;
        private bool matchResultReported;

        /// <summary>
        /// Client-side mirror of the replicated objective, used only when objectiveSystem is null (a
        /// pure client never runs InitializeGame/StartSession locally — see IsHostAuthoritative).
        /// </summary>
        private ObjectiveData replicatedObjective;

        /// <summary>True when an NGO session is actually running (mirrors PlayerController's own gate).</summary>
        private static bool IsNetworkSessionActive =>
            NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;

        /// <summary>
        /// True when this peer is allowed to actually drive session/objective/countdown transitions:
        /// either there is no network session at all (Epic 1 single-player), or this peer is the Host.
        /// A non-Host client must never call StartSession/RestartSession or tick the objective system —
        /// it only mirrors what NetworkGameState replicates (see ApplyReplicated* below).
        /// </summary>
        public bool IsHostAuthoritative =>
            !IsNetworkSessionActive || (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer);

        /// <summary>Current state of Unity Services init + anonymous sign-in.</summary>
        public ServicesStatus ServicesState => servicesStatus;

        /// <summary>True once anonymous sign-in has completed and Relay calls are allowed.</summary>
        public bool IsSignedIn => servicesStatus == ServicesStatus.SignedIn;

        public GameSessionState CurrentState => currentState;
        public bool IsPlaying => currentState == GameSessionState.Gameplay || currentState == GameSessionState.Evacuation;
        public bool IsEvacuating => currentState == GameSessionState.Evacuation;
        public float EvacuationDuration => evacuationDuration;

        public ObjectiveData CurrentObjective =>
            objectiveSystem != null ? objectiveSystem.CurrentObjective : replicatedObjective;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            if (playerSpawner == null)
            {
                playerSpawner = GetComponent<PlayerSpawner>();
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void Start()
        {
            // TLS-E03-S01: a Dedicated Server never touches Unity Services/Relay (Rule 6) and never
            // waits on startSessionOnPlay/Keyboard — it binds Unity Transport directly and starts the
            // match itself. LegacyClientHost and Client both fall through unchanged below; Client mode
            // has no distinct behavior yet (DirectConnectUI arrives in TLS-E03-S02) so for now it behaves
            // identically to today's LegacyClientHost Client role.
            if (GameBootstrap.CurrentMode == LaunchMode.DedicatedServer)
            {
                StartDedicatedServer();
                return;
            }

            // Story 3.1: kicked off without awaiting so the Epic 1 single-player session start is not
            // delayed behind a network round-trip. Relay calls gate on IsSignedIn instead.
            _ = InitializeUnityServicesAsync();

            if (startSessionOnPlay)
            {
                StartSession();
            }
        }

        /// <summary>
        /// TLS-E03-S01: binds Unity Transport directly (no Relay) via NetworkManagerExt, then starts the
        /// match with no button press. If either step fails, exits the process (Application.Quit is a
        /// no-op in the Editor, so this is a deliberate no-op there — real behavior only applies to a
        /// built Dedicated Server) rather than idling forever doing nothing an orchestrator can observe.
        /// TLS-E03-S02 will replace the "start immediately" step with "start once the backend/stub has
        /// approved the expected players" — until then NGO's default ConnectionApproval=false means any
        /// connecting client is accepted and spawned, which is expected and temporary for this phase.
        /// </summary>
        private void StartDedicatedServer()
        {
            NetworkManagerExt networkManagerExt = FindAnyObjectByType<NetworkManagerExt>();
            if (networkManagerExt == null || NetworkManager.Singleton == null)
            {
                Debug.LogError("[GameManager] DedicatedServer mode: no NetworkManagerExt/NetworkManager found in the scene. Exiting.");
                Application.Quit(1);
                return;
            }

            // TLS-E03-S02: wire join-token connection approval BEFORE the server starts listening —
            // NetworkConfig.ConnectionApproval/ConnectionApprovalCallback must be set before StartServer()
            // so no connection slips through unapproved. Empty BackendUrl selects the local stub (no
            // backend dependency, matching the spike's goal); a real BackendUrl selects the HTTP client
            // per docs/pivot/02_BACKEND_CONTRACT.md. ServerSecret is never logged (see ServerBootstrap).
            currentBackendClient = string.IsNullOrEmpty(GameBootstrap.Config.BackendUrl)
                ? new StubBackendClient(GameBootstrap.Config.TestJoinToken, GameBootstrap.Config.StubAcceptAnyToken)
                : new HttpBackendClient(GameBootstrap.Config.BackendUrl, GameBootstrap.Config.MatchId, GameBootstrap.Config.ServerSecret);

            Debug.Log($"[GameManager] DedicatedServer using {currentBackendClient.GetType().Name} for join-token validation.");
            serverConnectionApproval = new ServerConnectionApproval(NetworkManager.Singleton, currentBackendClient, GameBootstrap.Config.MaxPlayers);
            serverConnectionApproval.Attach();

            // TLS-E03-S03: heartbeat (backend unreachable must never crash/end the match — see
            // ServerHeartbeatLoop) and the empty-server/match-timeout abort watchdog. Both are ticked
            // from Update() below, DedicatedServer mode only.
            serverHeartbeatLoop = new ServerHeartbeatLoop(currentBackendClient);
            serverHeartbeatLoop.ShutdownRequested += HandleBackendShutdownRequested;

            serverMatchLifecycle = new ServerMatchLifecycle(
                NetworkManager.Singleton, GameBootstrap.Config.EmptyTimeoutSeconds, GameBootstrap.Config.MatchTimeoutSeconds);
            serverMatchLifecycle.Aborted += HandleMatchAborted;

            bool listening = networkManagerExt.StartServerDirect(GameBootstrap.Config.BindAddress, GameBootstrap.Config.Port);
            if (!listening)
            {
                Debug.LogError($"[GameManager] DedicatedServer mode: failed to start listening on " +
                                $"{GameBootstrap.Config.BindAddress}:{GameBootstrap.Config.Port}. Exiting.");
                Application.Quit(1);
                return;
            }

            Debug.Log($"[GameManager] DedicatedServer listening on {GameBootstrap.Config.BindAddress}:{GameBootstrap.Config.Port}. Starting match.");
            ServerStartMatch();
        }

        /// <summary>
        /// TLS-E03-S01: headless, keyboard-free entry point that starts a match. Equivalent to the Host
        /// pressing [R] today (StartSession()/RestartSession()), but callable without any input device —
        /// the server has no local player and very likely no keyboard at all (Rule 4).
        /// </summary>
        public void ServerStartMatch()
        {
            if (!IsHostAuthoritative)
            {
                Debug.LogWarning("[GameManager] ServerStartMatch ignored: not Host-authoritative.");
                return;
            }

            StartSession();
        }

        /// <summary>
        /// Story 3.1: brings up Unity Gaming Services and signs in anonymously so Relay can allocate.
        /// Runs once. On failure it logs and stops — it deliberately does not retry in a loop, because
        /// the usual causes (project not linked, Relay/Authentication service not enabled on the Unity
        /// Dashboard) are configuration problems a retry cannot fix.
        /// </summary>
        private async Task InitializeUnityServicesAsync()
        {
            if (servicesStatus != ServicesStatus.Uninitialized)
            {
                return;
            }

            servicesStatus = ServicesStatus.Initializing;

            try
            {
                if (UnityServices.State != ServicesInitializationState.Initialized)
                {
                    await UnityServices.InitializeAsync();
                }

                if (!AuthenticationService.Instance.IsSignedIn)
                {
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();
                }

                servicesStatus = ServicesStatus.SignedIn;
                Debug.Log($"[GameManager] Unity Services ready. Signed in anonymously as PlayerId: {AuthenticationService.Instance.PlayerId}");
            }
            catch (Exception ex)
            {
                servicesStatus = ServicesStatus.Failed;
                Debug.LogError(
                    $"[GameManager] Unity Services init / anonymous sign-in FAILED ({ex.GetType().Name}): {ex.Message}\n" +
                    "Hosting and joining are disabled. Check that the project is linked to a Unity Dashboard " +
                    "project and that Authentication + Relay are enabled for it.");
            }
        }

        private void Update()
        {
            // Story 3.5: a non-Host client never drives its own session — it only ever displays what
            // NetworkGameState replicates (see ApplyReplicated* below). Ticking, restarting, or the
            // debug fail-key locally on a client would silently desync it from the Host.
            if (!IsHostAuthoritative)
            {
                return;
            }

            // TLS-E03-S03: heartbeat + empty-server/match-timeout watchdog, DedicatedServer mode only.
            if (GameBootstrap.CurrentMode == LaunchMode.DedicatedServer)
            {
                int connectedCount = NetworkManager.Singleton != null ? NetworkManager.Singleton.ConnectedClientsIds.Count : 0;
                serverHeartbeatLoop?.Tick(Time.deltaTime, connectedCount, currentState.ToString());
                serverMatchLifecycle?.Tick(Time.deltaTime);
            }

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.rKey.wasPressedThisFrame)
                {
                    RestartSession();
                    return;
                }

            }

            for (int i = 0; i < activeSystems.Length; i++)
            {
                activeSystems[i].Tick(Time.deltaTime);
            }
        }

        public void StartSession()
        {
            if (!IsHostAuthoritative)
            {
                Debug.LogWarning("[GameManager] StartSession ignored: only the Host may start/restart a networked session.");
                return;
            }

            SetState(GameSessionState.GameStart);
            ResetTriggerZones();
            InitializeGame();
            SpawnPlayer();
            InitializeObjective();
            SetState(GameSessionState.Gameplay);
            playerSpawner?.SetControlEnabled(true);
        }

        public void RestartSession()
        {
            StartSession();
        }

        // TODO: ExitZone/HazardZone-triggered Win/Lose transitions below are NOT Host-validated —
        // Story 3.5 only covers Door and ReactorCoreConsole interactions (per its explicit scope). A
        // client reaching the exit or hitting a hazard still only updates their OWN local view here,
        // exactly as before this story; it is not replicated to other peers.
        public void NotifyExitReached()
        {
            if (currentState != GameSessionState.Evacuation)
            {
                // Player stepped on exit zone during Phase 1 (Reactor not yet activated)
                return;
            }

            if (objectiveSystem == null || !objectiveSystem.TryComplete())
            {
                return;
            }

            Debug.Log("[GameManager] Player safely returned to Airlock! Evacuation successful.");
            HandleWin();
        }

        /// <summary>
        /// Called ONLY by NetworkGameState.RequestActivateReactorRpc after it has already validated the
        /// request server-side (not already activated, in Gameplay phase, requester in range) — by the
        /// time this runs we are guaranteed to be on the Host, since Rpc(SendTo.Server) method bodies
        /// only ever execute there. The IsHostAuthoritative guard below is defense-in-depth in case this
        /// is ever called from somewhere else in the future.
        /// </summary>
        public void NotifyReactorActivated()
        {
            if (!IsHostAuthoritative)
            {
                return;
            }

            if (currentState != GameSessionState.Gameplay)
            {
                Debug.LogWarning($"[GameManager] NotifyReactorActivated ignored because currentState is {currentState} (expected Gameplay).");
                return;
            }

            if (objectiveSystem != null)
            {
                objectiveSystem.TryComplete();
            }

            Debug.Log("[GameManager] Reactor Core activated! Initiating emergency evacuation countdown.");
            SetState(GameSessionState.Evacuation);
        }

        public void NotifyAllPlayersDowned()
        {
            if (!IsPlaying)
            {
                return;
            }

            Debug.Log("[GameManager] All survivors are downed! The Shade claims victory.");
            NotifyPlayerDied("All Survivors Downed");
        }

        public void NotifyPlayerDied(string reason = "Hazard")
        {
            if (!IsPlaying)
            {
                return;
            }

            Debug.Log($"[GameManager] Player failed due to: {reason}");
            HandleLose();
        }

        public void NotifyPlayerFailed()
        {
            NotifyPlayerDied("Player Failed");
        }

        public void ReturnToMenu()
        {
            Debug.Log("[GameManager] Return to menu requested.");
            // Future epic hook: UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
        }

        /// <summary>
        /// Story 3.6: called by LobbyUI on a Client when NGO reports this peer's own connection was
        /// lost while it had an active roster entry. This project has no Host Migration
        /// (ARCHITECTURE_RULES.md), so the Host disconnecting ends the session for everyone else.
        /// Sets <see cref="currentState"/> directly and raises the same EventChannelSOs HUDManager
        /// already listens to (NOT via SetState(), which would try to push to NetworkGameState with
        /// Server write permission this client no longer has) so the HUD shows a clear "session
        /// ended" message instead of freezing on a now-ownerless session, and does not throw.
        /// </summary>
        public void HandleHostDisconnected()
        {
            playerSpawner?.SetControlEnabled(false);

            currentState = GameSessionState.HostDisconnected;
            onGameSessionStateChanged?.Raise(currentState);
            onObjectiveChanged?.Raise(default);
            onEvacuationTimerTicked?.Raise(0f);
        }

        private void ResetTriggerZones()
        {
            ExitZone[] exitZones = FindObjectsByType<ExitZone>(FindObjectsInactive.Include);
            for (int i = 0; i < exitZones.Length; i++)
            {
                exitZones[i].ResetTrigger();
            }

            HazardZone[] hazardZones = FindObjectsByType<HazardZone>(FindObjectsInactive.Include);
            for (int i = 0; i < hazardZones.Length; i++)
            {
                hazardZones[i].ResetTrigger();
            }
        }

        private void InitializeGame()
        {
            SetState(GameSessionState.InitializeGame);

            for (int i = 0; i < activeSystems.Length; i++)
            {
                activeSystems[i].Shutdown();
            }

            objectiveSystem = new SimpleObjectiveSystem(
                PublishObjective,
                PublishTimer,
                HandleTimeout,
                evacuationDuration);
            objectiveSystem.Initialize();

            // Epic 5: lightingController must Tick (and so refresh its source cache) before sanitySystem
            // reads IsLit each step — array order below is load-bearing, see LightingController.Tick.
            lightingController = new LightingController(lightingConfig);
            sanitySystem = new SanitySystem(sanityConfig, lightingController, PublishSanityThresholdCrossed);
            lightingController.Initialize();
            sanitySystem.Initialize();

            activeSystems = new IGameSystem[] { objectiveSystem, lightingController, sanitySystem };

            PrepareMap();
        }

        private void PrepareMap()
        {
            // Ensure prototype map grid is present and active
            GameObject grid = GameObject.Find("Facility_Grid");
            if (grid != null && !grid.activeSelf)
            {
                grid.SetActive(true);
            }
        }

        private void SpawnPlayer()
        {
            SetState(GameSessionState.SpawnPlayer);

            if (playerSpawner == null)
            {
                Debug.LogError("[GameManager] PlayerSpawner is missing.");
                return;
            }

            playerSpawner.SpawnPlayers();

            // Epic 5: hand each spawned player's purely-visual sanity view the threshold config it needs
            // to classify its own replicated value — PlayerSanityView never owns balance data itself.
            IReadOnlyList<PlayerController> spawned = playerSpawner.SpawnedPlayers;
            for (int i = 0; i < spawned.Count; i++)
            {
                if (spawned[i] != null && spawned[i].TryGetComponent(out PlayerSanityView sanityView))
                {
                    sanityView.SetConfig(sanityConfig);
                }
            }

            AssignRoles(spawned);
        }

        /// <summary>
        /// TLS-E05-S02: server-side, random, unique role assignment for the three Survivor slots
        /// (0-2) — slot 3 is always the Shade and never receives one (design brief). Host-authoritative
        /// only (guarded by the IsHostAuthoritative check in SpawnPlayer's own call chain); clients
        /// never choose or influence this — they only ever read the replicated PlayerRole.Role.
        /// </summary>
        private void AssignRoles(IReadOnlyList<PlayerController> spawnedPlayers)
        {
            List<PlayerController> survivors = new List<PlayerController>();
            for (int i = 0; i < spawnedPlayers.Count; i++)
            {
                if (spawnedPlayers[i] != null && spawnedPlayers[i].SlotIndex != 3)
                {
                    survivors.Add(spawnedPlayers[i]);
                }
            }

            survivors.Sort((a, b) => a.SlotIndex.CompareTo(b.SlotIndex));

            SurvivorRole[] roles = RoleAssignmentUtility.AssignRandomRoles(survivors.Count, new System.Random());
            for (int i = 0; i < survivors.Count && i < roles.Length; i++)
            {
                if (survivors[i].TryGetComponent(out PlayerRole playerRole))
                {
                    playerRole.ServerAssignRole(roles[i]);
                }
            }
        }

        private void InitializeObjective()
        {
            SetState(GameSessionState.InitializeObjective);
            objectiveSystem.ResetObjective();
            objectiveSystem.Begin(ObjectiveData.ActivateReactor());
        }

        private void HandleTimeout()
        {
            if (currentState != GameSessionState.Evacuation)
            {
                return;
            }

            Debug.Log("[GameManager] Evacuation countdown reached 0! Reactor meltdown.");
            NotifyPlayerDied("Reactor Meltdown");
        }

        /// <summary>SanitySystem's threshold-crossed callback. Raises the session-wide EventChannelSO —
        /// this is "any player's sanity crossed a band," unlike per-player sanity itself (which replicates
        /// via each player's own PlayerSanityState, not an EventChannelSO — see that class's docs).</summary>
        private void PublishSanityThresholdCrossed(ulong playerId, SanityLevel level)
        {
            onSanityThresholdCrossed?.Raise(new SanityThresholdEvent(playerId, level));
        }

        private void PublishTimer(float seconds)
        {
            if (onEvacuationTimerTicked != null)
            {
                onEvacuationTimerTicked.Raise(seconds);
            }

            // Only reached when IsHostAuthoritative (Update() won't tick activeSystems otherwise), so
            // pushing here is always the Host's own countdown, never a client's.
            if (IsNetworkSessionActive)
            {
                NetworkGameState.Instance?.ServerSetEvacuationSeconds(seconds);
            }
        }

        private void HandleWin()
        {
            SetState(GameSessionState.Win);
            playerSpawner?.SetControlEnabled(false);
            SetState(GameSessionState.GameComplete);

            if (GameBootstrap.CurrentMode == LaunchMode.DedicatedServer)
            {
                ReportResultAndShutdown(MatchOutcome.SurvivorVictory);
            }
        }

        private void HandleLose()
        {
            SetState(GameSessionState.Lose);
            playerSpawner?.SetControlEnabled(false);
            SetState(GameSessionState.GameOver);

            // No Shade entity exists yet (no AI/human Shade implemented) — every current lose condition
            // (hazard, reactor meltdown) is mapped onto ShadeVictory, the closest fit in the result
            // contract's vocabulary, without inventing new gameplay (per the brief's scope limits).
            if (GameBootstrap.CurrentMode == LaunchMode.DedicatedServer)
            {
                ReportResultAndShutdown(MatchOutcome.ShadeVictory);
            }
        }

        /// <summary>TLS-E03-S03: the heartbeat loop's backend-requested-shutdown directive.</summary>
        private void HandleBackendShutdownRequested()
        {
            Debug.Log("[GameManager] Backend requested shutdown via heartbeat directive.");
            ReportResultAndShutdown(MatchOutcome.Aborted);
        }

        /// <summary>TLS-E03-S03: the empty-server-timeout / match-timeout watchdog firing.</summary>
        private void HandleMatchAborted(ServerMatchLifecycle.AbortCause cause)
        {
            Debug.Log($"[GameManager] Match aborted: {cause}.");
            ReportResultAndShutdown(MatchOutcome.Aborted);
        }

        /// <summary>
        /// TLS-E03-S03: builds and sends the match result exactly once — guarded by
        /// <see cref="matchResultReported"/> so a normal Win/Lose racing against the watchdog (or the
        /// watchdog firing twice) can never send a second result — then shuts the process down cleanly.
        /// Per the brief: "send exactly one result per match" and "shut the process down cleanly after
        /// the result."
        /// </summary>
        private void ReportResultAndShutdown(MatchOutcome outcome)
        {
            if (matchResultReported)
            {
                return;
            }

            matchResultReported = true;
            MatchResult result = new MatchResult
            {
                Outcome = outcome,
                DurationSeconds = serverMatchLifecycle != null ? serverMatchLifecycle.MatchElapsedSeconds : 0f,
                Players = BuildPlayerResults(outcome)
            };

            _ = PostResultThenQuitAsync(result);
        }

        /// <summary>
        /// TLS-E03-S03 simplification: the current game has no per-player health/incapacitation system
        /// yet (only one shared Win/Lose outcome for the whole session) — every currently-approved player
        /// gets the same Incapacitated/FinalStatus derived from the overall outcome, not individually
        /// tracked. Revisit once per-player state exists.
        /// </summary>
        private MatchPlayerResult[] BuildPlayerResults(MatchOutcome outcome)
        {
            if (serverConnectionApproval == null || serverConnectionApproval.ApprovedPlayers.Count == 0)
            {
                return System.Array.Empty<MatchPlayerResult>();
            }

            bool incapacitated = outcome != MatchOutcome.SurvivorVictory;
            string finalStatus = outcome == MatchOutcome.SurvivorVictory ? "Evacuated" : "Incapacitated";

            MatchPlayerResult[] players = new MatchPlayerResult[serverConnectionApproval.ApprovedPlayers.Count];
            int i = 0;
            foreach (ServerConnectionApproval.ApprovedPlayer player in serverConnectionApproval.ApprovedPlayers)
            {
                players[i++] = new MatchPlayerResult
                {
                    AccountId = player.AccountId,
                    Role = player.Role,
                    Incapacitated = incapacitated,
                    FinalStatus = finalStatus
                };
            }

            return players;
        }

        private async Task PostResultThenQuitAsync(MatchResult result)
        {
            bool reported = false;

            if (currentBackendClient != null)
            {
                try
                {
                    reported = await currentBackendClient.PostResultAsync(result);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[GameManager] PostResultAsync threw ({ex.GetType().Name}: {ex.Message}) — result may be lost.");
                }
            }

            Debug.Log($"[GameManager] Match result ({result.Outcome}) reporting {(reported ? "succeeded" : "did NOT succeed")}. Shutting down.");

            // Application.Quit is a no-op in the Editor (by design, same as StartDedicatedServer's own
            // Quit calls) — only a built Dedicated Server actually exits here.
            int exitCode = result.Outcome == MatchOutcome.Aborted ? 2 : 0;
            Application.Quit(exitCode);
        }

        /// <summary>
        /// Sets the local session phase and raises the HUD-facing event exactly as before Story 3.5.
        /// Additionally, when this peer is Host-authoritative in a live network session, pushes the new
        /// phase into NetworkGameState so every client observes the same phase at the same time — the
        /// Host is the only peer that ever reaches this push (see IsHostAuthoritative call sites: this
        /// method is only invoked from Host-gated code paths).
        /// </summary>
        private void SetState(GameSessionState newState)
        {
            currentState = newState;
            if (onGameSessionStateChanged != null)
            {
                onGameSessionStateChanged.Raise(newState);
            }

            if (IsNetworkSessionActive)
            {
                NetworkGameState.Instance?.ServerSetSessionState(newState);
            }

            Debug.Log($"[GameManager] State -> {newState}");
        }

        private void PublishObjective(ObjectiveData objective)
        {
            if (onObjectiveChanged != null)
            {
                onObjectiveChanged.Raise(objective);
            }

            if (IsNetworkSessionActive && objectiveSystem != null)
            {
                NetworkGameState.Instance?.ServerSetObjective(objectiveSystem.CurrentPhase, objective.Status);
            }
        }

        /// <summary>
        /// Called only by NetworkGameState's session-state NetworkVariable callback, only on non-Host
        /// peers. Mirrors the replicated phase into local display state (for HUDManager, via the same
        /// EventChannelSO it already listens to) WITHOUT re-running any Host-only transition side
        /// effects (spawning, zone resets, etc.) and WITHOUT pushing back to the network.
        /// </summary>
        public void ApplyReplicatedSessionState(GameSessionState state)
        {
            currentState = state;
            onGameSessionStateChanged?.Raise(state);
        }

        /// <summary>
        /// Called only by NetworkGameState's objective NetworkVariable callbacks, only on non-Host
        /// peers. Reconstructs the display ObjectiveData from the replicated phase index + status
        /// (only the phase/status are replicated, not the description text — see NetworkGameState.cs).
        /// </summary>
        public void ApplyReplicatedObjective(int phase, ObjectiveStatus status)
        {
            replicatedObjective = phase == 0 ? ObjectiveData.ActivateReactor() : ObjectiveData.EvacuateAirlock();
            replicatedObjective.Status = status;
            onObjectiveChanged?.Raise(replicatedObjective);
        }

        /// <summary>
        /// Called only by NetworkGameState's evacuation-countdown NetworkVariable callback, only on
        /// non-Host peers, once per Host tick.
        /// </summary>
        public void ApplyReplicatedEvacuationSeconds(float seconds)
        {
            onEvacuationTimerTicked?.Raise(seconds);
        }
    }
}
