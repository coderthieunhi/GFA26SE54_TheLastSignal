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
        [SerializeField] private FloatEventChannelSO onEvacuationTimerTicked;

        [Header("UI")]
        [SerializeField] private TextMeshProUGUI objectiveText;
        [SerializeField] private TextMeshProUGUI sessionStatusText;
        [SerializeField] private TextMeshProUGUI countdownText;

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

            if (onEvacuationTimerTicked != null)
            {
                onEvacuationTimerTicked.OnEventRaised += HandleTimerTicked;
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

            if (onEvacuationTimerTicked != null)
            {
                onEvacuationTimerTicked.OnEventRaised -= HandleTimerTicked;
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
            if (countdownText != null)
            {
                if (state == GameSessionState.Evacuation)
                {
                    countdownText.gameObject.SetActive(true);
                }
                else
                {
                    countdownText.gameObject.SetActive(false);
                    countdownText.text = string.Empty;
                }
            }

            if (sessionStatusText == null)
            {
                return;
            }

            switch (state)
            {
                case GameSessionState.Gameplay:
                case GameSessionState.Evacuation:
                    sessionStatusText.text = string.Empty;
                    break;
                case GameSessionState.Win:
                case GameSessionState.GameComplete:
                    sessionStatusText.text = "<b><color=#44FF44>MISSION COMPLETE</color></b>\n<size=24>Evacuation Successful — Press [R] to Restart</size>";
                    break;
                case GameSessionState.Lose:
                case GameSessionState.GameOver:
                    sessionStatusText.text = "<b><color=#FF4444>GAME OVER</color></b>\n<size=24>Reactor Meltdown / Failed — Press [R] to Restart</size>";
                    break;
                case GameSessionState.HostDisconnected:
                    sessionStatusText.text = "<b><color=#FFAA00>HOST DISCONNECTED</color></b>\n<size=24>Session Ended — Return to the Lobby to Host or Join a New Game</size>";
                    break;
                default:
                    sessionStatusText.text = string.Empty;
                    break;
            }
        }

        private void HandleTimerTicked(float remainingSeconds)
        {
            if (countdownText == null)
            {
                return;
            }

            if (!countdownText.gameObject.activeSelf)
            {
                countdownText.gameObject.SetActive(true);
            }

            int minutes = Mathf.FloorToInt(remainingSeconds / 60f);
            int seconds = Mathf.FloorToInt(remainingSeconds % 60f);
            int tenths = Mathf.FloorToInt((remainingSeconds * 10f) % 10f);

            string colorHex = remainingSeconds <= 15f ? "#FF3333" : (remainingSeconds <= 30f ? "#FFAA00" : "#55FF55");
            countdownText.text = $"<color={colorHex}>EVACUATION: {minutes:00}:{seconds:00}.{tenths}</color>";
        }
    }
}
