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
                case GameSessionState.Win:
                case GameSessionState.GameComplete:
                    sessionStatusText.text = "<b><color=#44FF44>MISSION COMPLETE</color></b>\n<size=24>Objective Cleared — Press [R] to Restart</size>";
                    break;
                case GameSessionState.Lose:
                case GameSessionState.GameOver:
                    sessionStatusText.text = "<b><color=#FF4444>GAME OVER</color></b>\n<size=24>Mission Failed — Press [R] to Restart</size>";
                    break;
                default:
                    sessionStatusText.text = string.Empty;
                    break;
            }
        }
    }
}
