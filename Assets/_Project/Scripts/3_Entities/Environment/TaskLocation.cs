using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TheLastSignal.Core.Interaction;
using TheLastSignal.Entities.Player;
using TheLastSignal.UI.Tasks;

namespace TheLastSignal.Entities.Environment
{
    public enum TaskMinigameType
    {
        Automatic,
        WireBridging,
        ValveBalancing,
        MemorySequence,
        WaveformTuning,
        TimingCalibration
    }
    public enum TaskStationType
    {
        Generator,
        Computer,
        Mechanical,
        ControlPanel,
        Exit
    }

    /// <summary>
    /// Interactive repair station that orchestrates multi-stage minigame sequences.
    /// Manages randomized non-repeating sub-task playlists (5 minigames),
    /// role quotas (3 for Engineer, 5 for Drone Operator / Logistics),
    /// persistent partial progress saving, and seamless intermission transitions.
    /// </summary>
    public class TaskLocation : MonoBehaviour, IInteractable
    {
        [Header("Task Info")]
        [SerializeField] private string stationId = "TASK_01";
        [SerializeField] private string stationDisplayName = "Station";
        [SerializeField] private TaskStationType stationType = TaskStationType.Generator;
        [SerializeField] private bool isCompleted = false;

        [Header("Minigame")]
        [SerializeField] private bool useMinigame = true;
        [SerializeField] private bool forceSpecificMinigame = false;
        [SerializeField] private TaskMinigameType minigameType = TaskMinigameType.Automatic;

        [Header("Multi-Stage Repair Playlist")]
        [SerializeField] private List<TaskMinigameType> subTasksPlaylist = new List<TaskMinigameType>();

        [Header("Feedback")]
        [SerializeField] private SpriteRenderer markerRenderer;
        [SerializeField] private Color activeColor = Color.white;
        [SerializeField] private Color completedColor = new Color(0.3f, 1f, 0.5f, 0.8f);

        public string StationId => stationId;
        public string StationDisplayName => stationDisplayName;
        public TaskStationType StationType => stationType;
        public bool IsCompleted => isCompleted;

        public string InteractionPrompt
        {
            get
            {
                if (isCompleted) return $"{stationDisplayName} (Finished)";

                if (TryGetComponent(out StationRepair repair))
                {
                    if (repair.IsLocked)
                    {
                        return $"{stationDisplayName} (In use)";
                    }

                    int steps = repair.CompletedSteps;
                    if (steps > 0)
                    {
                        return $"Repair {stationDisplayName} [Stage {steps}/5]";
                    }
                }
                return $"Interact with {stationDisplayName}";
            }
        }

        public string GetInteractionPrompt() => InteractionPrompt;

        public bool CanInteract(PlayerController player)
        {
            if (isCompleted) return false;
            if (TryGetComponent(out StationRepair repair))
            {
                if (repair.IsStationLockedByOther(player, out _))
                {
                    return false;
                }
            }
            return true;
        }

        private void Awake()
        {
            if (markerRenderer == null)
            {
                markerRenderer = GetComponentInChildren<SpriteRenderer>();
            }
            EnsureSubTasksPlaylist(forceReshuffle: true);
        }

        public void Initialize(string id, string displayName, TaskStationType type)
        {
            stationId = id;
            stationDisplayName = displayName;
            stationType = type;
            isCompleted = false;
            EnsureSubTasksPlaylist(forceReshuffle: true);
        }

        /// <summary>
        /// Generates a deterministic non-repeating sequence of 5 minigames for this station.
        /// </summary>
        private void EnsureSubTasksPlaylist(bool forceReshuffle = false)
        {
            if (!forceReshuffle && subTasksPlaylist != null && subTasksPlaylist.Count >= 5) return;

            subTasksPlaylist = new List<TaskMinigameType>
            {
                TaskMinigameType.WireBridging,
                TaskMinigameType.ValveBalancing,
                TaskMinigameType.MemorySequence,
                TaskMinigameType.WaveformTuning,
                TaskMinigameType.TimingCalibration
            };

            // Deterministic stable shuffle based on stationId polynomial hash
            int seed = GetStableHash(!string.IsNullOrEmpty(stationId) ? stationId : stationDisplayName);
            System.Random rng = new System.Random(seed);
            for (int i = 0; i < subTasksPlaylist.Count; i++)
            {
                int r = rng.Next(i, subTasksPlaylist.Count);
                (subTasksPlaylist[i], subTasksPlaylist[r]) = (subTasksPlaylist[r], subTasksPlaylist[i]);
            }
        }

        private static int GetStableHash(string str)
        {
            if (string.IsNullOrEmpty(str)) return 42;
            unchecked
            {
                int hash = 17;
                for (int i = 0; i < str.Length; i++)
                {
                    hash = hash * 31 + str[i];
                }
                return Mathf.Abs(hash);
            }
        }

        public void Interact(PlayerController player)
        {
            if (player == null)
            {
                player = PlayerController.Local ?? Object.FindAnyObjectByType<PlayerController>();
            }

            if (!CanInteract(player)) return;

            Debug.Log($"<color=yellow>[TaskLocation]</color> Player interacted with {stationDisplayName} ({stationId} - {stationType})!");

            if (TryGetComponent(out StationRepair repair))
            {
                if (!repair.TryClaimRepairLock(player, out string lockReason))
                {
                    Debug.LogWarning($"<color=orange>[TaskLocation]</color> Cannot interact: {lockReason}");
                    return;
                }
            }

            if (useMinigame && !isCompleted)
            {
                OpenCurrentSubTask(player);
                return;
            }

            // Fallback: non-minigame direct repair
            if (TryGetComponent(out StationRepair directRepair))
            {
                directRepair.RequestRepair(player);
            }
        }

        public void Interact(GameObject player)
        {
            PlayerController pc = null;
            if (player != null)
            {
                pc = player.GetComponent<PlayerController>() ?? player.GetComponentInParent<PlayerController>();
            }
            if (pc == null)
            {
                pc = PlayerController.Local ?? Object.FindAnyObjectByType<PlayerController>();
            }
            Interact(pc);
        }

        /// <summary>
        /// Resolves the current sub-task minigame type based on playlist and completed steps.
        /// Guaranteed to rotate across all 5 minigames for all task locations unless explicitly forced.
        /// </summary>
        public TaskMinigameType GetCurrentSubTaskType()
        {
            if (forceSpecificMinigame && minigameType != TaskMinigameType.Automatic)
            {
                return minigameType;
            }

            EnsureSubTasksPlaylist();
            int currentStep = 0;
            if (TryGetComponent(out StationRepair repair))
            {
                currentStep = repair.CompletedSteps;
            }

            if (subTasksPlaylist == null || subTasksPlaylist.Count == 0)
            {
                EnsureSubTasksPlaylist(forceReshuffle: true);
            }

            return subTasksPlaylist[currentStep % subTasksPlaylist.Count];
        }

        /// <summary>
        /// Opens the appropriate minigame for the current sub-task.
        /// </summary>
        public void OpenCurrentSubTask(PlayerController player)
        {
            TaskMinigameType activeType = GetCurrentSubTaskType();

            switch (activeType)
            {
                case TaskMinigameType.TimingCalibration:
                    var calibUI = CalibrationTaskUI.Instance ?? CalibrationTaskUI.GetOrCreateInstance();
                    calibUI?.Open(player, this);
                    break;

                case TaskMinigameType.WaveformTuning:
                    var waveUI = WaveformTaskUI.Instance ?? WaveformTaskUI.GetOrCreateInstance();
                    waveUI?.Open(player, this);
                    break;

                case TaskMinigameType.MemorySequence:
                    var memUI = MemoryTaskUI.Instance ?? MemoryTaskUI.GetOrCreateInstance();
                    memUI?.Open(player, this);
                    break;

                case TaskMinigameType.ValveBalancing:
                    var valveUI = ValveTaskUI.Instance ?? ValveTaskUI.GetOrCreateInstance();
                    valveUI?.Open(player, this);
                    break;

                case TaskMinigameType.WireBridging:
                default:
                    var wireUI = WireTaskUI.Instance ?? WireTaskUI.GetOrCreateInstance();
                    wireUI?.Open(player, this);
                    break;
            }
        }

        /// <summary>
        /// Called when any minigame completes. Advances the repair pipeline and either completes the station
        /// or presents the Intermission screen allowing the player to continue or exit and save.
        /// </summary>
        public void OnSubTaskCompleted(PlayerController player, MonoBehaviour currentMinigameUI)
        {
            if (TryGetComponent(out StationRepair repair))
            {
                repair.AdvanceRepairStep(player, out bool isStationCompleted);

                if (isStationCompleted)
                {
                    SetCompleted(true);
                    repair.ReleaseRepairLock(player);
                    StartCoroutine(CloseMinigameRoutine(currentMinigameUI, 0.85f, player));
                    return;
                }

                // Station requires more sub-tasks!
                // Close current minigame UI
                CloseMinigame(currentMinigameUI);

                // Show Intermission screen
                int current = repair.CompletedSteps;
                int target = repair.GetTargetStepsForPlayer(player);
                TaskMinigameType nextType = subTasksPlaylist[current % subTasksPlaylist.Count];
                string nextName = GetMinigameDisplayName(nextType);

                var intermission = TaskIntermissionUI.Instance ?? TaskIntermissionUI.GetOrCreateInstance();
                if (intermission != null)
                {
                    intermission.ShowIntermission(player, this, current, target, nextName,
                        onContinue: () =>
                        {
                            OpenCurrentSubTask(player);
                        },
                        onExit: () =>
                        {
                            repair.ReleaseRepairLock(player);
                        });
                }
                else
                {
                    // Fallback if intermission UI not in scene: proceed directly
                    OpenCurrentSubTask(player);
                }
            }
            else
            {
                SetCompleted(true);
                CloseMinigame(currentMinigameUI);
                if (player != null) player.SetControlEnabled(true);
            }
        }

        /// <summary>
        /// Helper to close any minigame UI instance.
        /// </summary>
        public void CloseMinigame(MonoBehaviour minigameUI)
        {
            if (minigameUI is WireTaskUI wire) wire.CloseMinigame();
            else if (minigameUI is ValveTaskUI valve) valve.CloseMinigame();
            else if (minigameUI is MemoryTaskUI mem) mem.CloseMinigame();
            else if (minigameUI is WaveformTaskUI wave) wave.CloseMinigame();
            else if (minigameUI is CalibrationTaskUI calib) calib.CloseMinigame();
        }

        private IEnumerator CloseMinigameRoutine(MonoBehaviour minigameUI, float delay, PlayerController player)
        {
            yield return new WaitForSeconds(delay);
            CloseMinigame(minigameUI);
            if (player != null)
            {
                player.SetControlEnabled(true);
            }
        }

        public string GetMinigameDisplayName(TaskMinigameType type)
        {
            switch (type)
            {
                case TaskMinigameType.WireBridging: return "ELECTRICAL WIRE BRIDGING";
                case TaskMinigameType.ValveBalancing: return "PRESSURE VALVE BALANCING";
                case TaskMinigameType.MemorySequence: return "REACTOR MEMORY CORE KEYPAD";
                case TaskMinigameType.WaveformTuning: return "RADIO WAVEFORM SYNCHRONIZER";
                case TaskMinigameType.TimingCalibration: return "POWER SURGE PULSE CALIBRATION";
                default: return "SYSTEM MAINTENANCE";
            }
        }

        public void SetCompleted(bool completed)
        {
            isCompleted = completed;
            if (markerRenderer != null)
            {
                markerRenderer.color = completed ? completedColor : activeColor;
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            Color gizmoColor;
            switch (stationType)
            {
                case TaskStationType.Generator: gizmoColor = new Color(1f, 0.8f, 0.1f, 0.85f); break; // Yellow
                case TaskStationType.Computer: gizmoColor = new Color(0.2f, 0.8f, 1f, 0.85f); break;   // Cyan
                case TaskStationType.Mechanical: gizmoColor = new Color(0.9f, 0.5f, 0.2f, 0.85f); break; // Orange
                case TaskStationType.ControlPanel: gizmoColor = new Color(0.9f, 0.2f, 0.3f, 0.85f); break; // Red
                case TaskStationType.Exit: gizmoColor = new Color(0.2f, 1f, 0.4f, 0.85f); break;       // Green
                default: gizmoColor = Color.white; break;
            }

            Gizmos.color = gizmoColor;
            Gizmos.DrawWireCube(transform.position, new Vector3(1f, 1f, 0.1f));

            UnityEditor.Handles.Label(transform.position + Vector3.up * 0.7f, $"[{stationType}] {stationDisplayName}");
        }
#endif
    }
}
