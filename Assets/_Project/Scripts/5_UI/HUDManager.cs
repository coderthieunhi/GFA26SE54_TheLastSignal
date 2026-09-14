using UnityEngine;
using TMPro;
using TheLastSignal.Core;
using TheLastSignal.Core.EventBus;
using TheLastSignal.Systems.Objectives;

namespace TheLastSignal.UI
{
    /// <summary>
    /// Drives the in-match HUD. Strictly visual — subscribes to EventChannelSO events and never
    /// contains game rules, per ARCHITECTURE_RULES.md Rule 2.
    /// </summary>
    public class HUDManager : MonoBehaviour
    {
        [Header("Events")]
        [SerializeField] private ObjectiveDataEventChannelSO onObjectiveChanged;
        [SerializeField] private GameSessionStateEventChannelSO onGameSessionStateChanged;

        [Header("UI")]
        [SerializeField] private TextMeshProUGUI objectiveText;
        [SerializeField] private TextMeshProUGUI sessionStatusText;

        private void OnEnable()
        {
            if (onObjectiveChanged != null)
            {
                onObjectiveChanged.OnEventRaised += HandleObjectiveChanged;
            }

            if (onGameSessionStateChanged != null)
            {
                onGameSessionStateChanged.OnEventRaised += HandleSessionStateChanged;
            }
        }

        private void Start()
        {
            if (GameManager.Instance == null)
            {
                return;
            }

            HandleObjectiveChanged(GameManager.Instance.CurrentObjective);
            HandleSessionStateChanged(GameManager.Instance.CurrentState);
        }

        private void OnDisable()
        {
            if (onObjectiveChanged != null)
            {
                onObjectiveChanged.OnEventRaised -= HandleObjectiveChanged;
            }

            if (onGameSessionStateChanged != null)
            {
                onGameSessionStateChanged.OnEventRaised -= HandleSessionStateChanged;
            }
        }

        private void HandleObjectiveChanged(ObjectiveData objective)
        {
            if (objectiveText == null)
            {
                return;
            }

            objectiveText.text = string.IsNullOrEmpty(objective.Description)
                ? string.Empty
                : $"Objective: {objective.Description} ({objective.Status})";
        }

        private void HandleSessionStateChanged(GameSessionState state)
        {
            if (sessionStatusText == null)
            {
                return;
            }

            switch (state)
            {
                case GameSessionState.Gameplay:
                    sessionStatusText.text = string.Empty;
                    break;
                case GameSessionState.GameComplete:
                    sessionStatusText.text = "Game Complete — press R to restart";
                    break;
                case GameSessionState.GameOver:
                    sessionStatusText.text = "Game Over — press R to restart";
                    break;
                default:
                    sessionStatusText.text = string.Empty;
                    break;
            }
        }
    }
}
