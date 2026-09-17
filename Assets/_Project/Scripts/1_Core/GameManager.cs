using UnityEngine;
using UnityEngine.InputSystem;
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

        private GameSessionState currentState = GameSessionState.GameStart;
        private SimpleObjectiveSystem objectiveSystem;
        private IGameSystem[] activeSystems = System.Array.Empty<IGameSystem>();

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
            if (startSessionOnPlay)
            {
                StartSession();
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
