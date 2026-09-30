using UnityEngine;
using TheLastSignal.Core;
using TheLastSignal.Core.EventBus;
using TheLastSignal.Core.Interaction;
using TheLastSignal.Entities.Player;

namespace TheLastSignal.Entities.Environment
{
    /// <summary>
    /// Interactive console in Reactor Core room. Triggers Phase 1 completion and initiates
    /// emergency evacuation countdown (Phase 2). Automatically resets state upon session restart.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class ReactorCoreConsole : InteractableBase
    {
        [Header("State")]
        [SerializeField] private bool isActivated = false;

        [Header("Events")]
        [SerializeField] private GameSessionStateEventChannelSO onGameSessionStateChanged;

        [Header("Visuals")]
        [SerializeField] private SpriteRenderer consoleRenderer;
        [SerializeField] private Color normalColor = new Color(0.3f, 0.8f, 1f, 1f);
        [SerializeField] private Color activatedColor = new Color(1f, 0.3f, 0.2f, 1f);

        public bool IsActivated => isActivated;

        private void Awake()
        {
            if (consoleRenderer == null)
            {
                consoleRenderer = GetComponent<SpriteRenderer>();
            }
            promptMessage = "[E] Activate Reactor Core";
            ApplyVisualState();
        }

        private void OnEnable()
        {
            if (onGameSessionStateChanged != null)
            {
                onGameSessionStateChanged.OnEventRaised += HandleSessionStateChanged;
            }
        }

        private void OnDisable()
        {
            if (onGameSessionStateChanged != null)
            {
                onGameSessionStateChanged.OnEventRaised -= HandleSessionStateChanged;
            }
        }

        /// <summary>
        /// Idempotently resets console activation state upon session start / restart.
        /// </summary>
        public void ResetConsole()
        {
            if (!isActivated)
            {
                return;
            }

            isActivated = false;
            isAvailable = true;
            ApplyVisualState();
            Debug.Log("[ReactorCoreConsole] Console state reset to ready.");
        }

        private void HandleSessionStateChanged(GameSessionState state)
        {
            // Reset when a new game session starts
            if (state == GameSessionState.GameStart || state == GameSessionState.InitializeGame)
            {
                ResetConsole();
            }
        }

        public override string GetInteractionPrompt()
        {
            if (isActivated)
            {
                return "Reactor Activated [Evacuate!]";
            }

            return "[E] Activate Reactor Core";
        }

        public override bool CanInteract(PlayerController player)
        {
            if (isActivated)
            {
                return false;
            }

            if (!base.CanInteract(player))
            {
                return false;
            }

            if (GameManager.Instance != null && GameManager.Instance.CurrentState != GameSessionState.Gameplay)
            {
                return false;
            }

            return true;
        }

        protected override void ProcessInteraction(PlayerController player)
        {
            isActivated = true;
            isAvailable = false;
            ApplyVisualState();

            Debug.Log("[ReactorCoreConsole] Player activated the Reactor Core!");

            if (GameManager.Instance != null)
            {
                GameManager.Instance.NotifyReactorActivated();
            }
        }

        private void ApplyVisualState()
        {
            if (consoleRenderer != null)
            {
                consoleRenderer.color = isActivated ? activatedColor : normalColor;
            }
        }

        /// <summary>
        /// Story 3.5: applies Host-replicated activation state. Called by NetworkGameState's
        /// networkReactorActivated NetworkVariable callback on every peer (Host included). This console
        /// itself can't be a NetworkBehaviour (it extends InteractableBase : MonoBehaviour, a sibling
        /// hierarchy to NetworkBehaviour), so the authoritative bool lives on NetworkGameState instead —
        /// this method is the only path that changes isActivated while networked. ProcessInteraction
        /// below is reached only in single-player, where there's no NetworkGameState at all.
        /// </summary>
        public void ApplyNetworkActivation(bool activated)
        {
            isActivated = activated;
            isAvailable = !activated;
            ApplyVisualState();
        }
    }
}
