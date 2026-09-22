using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Services.Authentication;
using Unity.Services.Core;
using TheLastSignal.Core.EventBus;
using TheLastSignal.Entities;
using TheLastSignal.Systems;
using TheLastSignal.Systems.Objectives;

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
        private IGameSystem[] activeSystems = System.Array.Empty<IGameSystem>();
        private ServicesStatus servicesStatus = ServicesStatus.Uninitialized;

        /// <summary>Current state of Unity Services init + anonymous sign-in.</summary>
        public ServicesStatus ServicesState => servicesStatus;

        /// <summary>True once anonymous sign-in has completed and Relay calls are allowed.</summary>
        public bool IsSignedIn => servicesStatus == ServicesStatus.SignedIn;

        public GameSessionState CurrentState => currentState;
        public bool IsPlaying => currentState == GameSessionState.Gameplay || currentState == GameSessionState.Evacuation;
        public bool IsEvacuating => currentState == GameSessionState.Evacuation;
        public float EvacuationDuration => evacuationDuration;

        public ObjectiveData CurrentObjective =>
            objectiveSystem != null ? objectiveSystem.CurrentObjective : default;

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
            // Story 3.1: kicked off without awaiting so the Epic 1 single-player session start is not
            // delayed behind a network round-trip. Relay calls gate on IsSignedIn instead.
            _ = InitializeUnityServicesAsync();

            if (startSessionOnPlay)
            {
                StartSession();
            }
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
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.rKey.wasPressedThisFrame)
                {
                    RestartSession();
                    return;
                }

                if (IsPlaying && keyboard.kKey.wasPressedThisFrame)
                {
                    Debug.Log("[GameManager] Debug fail key [K] pressed.");
                    NotifyPlayerDied("Debug Key [K]");
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

        public void NotifyReactorActivated()
        {
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
            activeSystems = new IGameSystem[] { objectiveSystem };

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

        private void PublishTimer(float seconds)
        {
            if (onEvacuationTimerTicked != null)
            {
                onEvacuationTimerTicked.Raise(seconds);
            }
        }

        private void HandleWin()
        {
            SetState(GameSessionState.Win);
            playerSpawner?.SetControlEnabled(false);
            SetState(GameSessionState.GameComplete);
        }

        private void HandleLose()
        {
            SetState(GameSessionState.Lose);
            playerSpawner?.SetControlEnabled(false);
            SetState(GameSessionState.GameOver);
        }

        private void SetState(GameSessionState newState)
        {
            currentState = newState;
            if (onGameSessionStateChanged != null)
            {
                onGameSessionStateChanged.Raise(newState);
            }

            Debug.Log($"[GameManager] State -> {newState}");
        }

        private void PublishObjective(ObjectiveData objective)
        {
            if (onObjectiveChanged != null)
            {
                onObjectiveChanged.Raise(objective);
            }
        }
    }
}
