using System;

namespace TheLastSignal.Systems.Objectives
{
    /// <summary>
    /// Sequential 2-phase objective tracker for the session loop:
    /// Phase 1: Reach Reactor Core & Activate Console.
    /// Phase 2: Evacuate back to Airlock before Meltdown countdown expires.
    /// Pure C# — GameManager owns the instance and raises EventChannelSO updates.
    /// </summary>
    public class SimpleObjectiveSystem : IObjectiveSystem
    {
        private readonly Action<ObjectiveData> onObjectiveChanged;
        private readonly Action<float> onTimerTicked;
        private readonly Action onTimeout;
        private float evacuationDuration;

        private ObjectiveData current;
        private int currentPhase = 0; // 0 = Reactor, 1 = Evacuate
        private float remainingTime;
        private bool isTimerActive = false;

        public SimpleObjectiveSystem(
            Action<ObjectiveData> onObjectiveChanged,
            Action<float> onTimerTicked = null,
            Action onTimeout = null,
            float evacuationDuration = 45f)
        {
            this.onObjectiveChanged = onObjectiveChanged;
            this.onTimerTicked = onTimerTicked;
            this.onTimeout = onTimeout;
            this.evacuationDuration = evacuationDuration;
            this.remainingTime = evacuationDuration;
            current = default;
        }

        public ObjectiveData CurrentObjective => current;

        public bool IsCompleted => currentPhase == 1 && current.Status == ObjectiveStatus.Completed;

        public int CurrentPhase => currentPhase;

        public bool IsEvacuating => currentPhase == 1 && current.Status == ObjectiveStatus.InProgress;

        public float RemainingTime => remainingTime;

        public void SetEvacuationDuration(float duration)
        {
            evacuationDuration = duration;
            if (!isTimerActive)
            {
                remainingTime = duration;
            }
        }

        public void Initialize()
        {
            currentPhase = 0;
            isTimerActive = false;
            remainingTime = evacuationDuration;
        }

        public void Tick(float deltaTime)
        {
            if (!isTimerActive)
            {
                return;
            }

            remainingTime -= deltaTime;
            if (remainingTime < 0f)
            {
                remainingTime = 0f;
            }

            onTimerTicked?.Invoke(remainingTime);

            if (remainingTime <= 0f)
            {
                isTimerActive = false;
                current.Status = ObjectiveStatus.Failed;
                Publish();
                onTimeout?.Invoke();
            }
        }

        public void Shutdown()
        {
            isTimerActive = false;
            current = default;
            currentPhase = 0;
        }

        public void Begin(ObjectiveData initial)
        {
            currentPhase = 0;
            isTimerActive = false;
            remainingTime = evacuationDuration;

            current = initial;
            current.Status = ObjectiveStatus.InProgress;
            Publish();
        }

        public bool TryComplete()
        {
            if (current.Status != ObjectiveStatus.InProgress)
            {
                return false;
            }

            if (currentPhase == 0)
            {
                // Complete Phase 1 (Reactor Core) -> Transition to Phase 2 (Evacuation)
                current.Status = ObjectiveStatus.Completed;
                Publish();

                currentPhase = 1;
                current = ObjectiveData.EvacuateAirlock();
                current.Status = ObjectiveStatus.InProgress;
                remainingTime = evacuationDuration;
                isTimerActive = true;
                Publish();

                // Initial timer tick broadcast
                onTimerTicked?.Invoke(remainingTime);
                return true;
            }
            else if (currentPhase == 1)
            {
                // Complete Phase 2 (Airlock Evacuation reached in time)
                isTimerActive = false;
                current.Status = ObjectiveStatus.Completed;
                Publish();
                return true;
            }

            return false;
        }

        public void ResetObjective()
        {
            currentPhase = 0;
            isTimerActive = false;
            remainingTime = evacuationDuration;

            current = ObjectiveData.ActivateReactor();
            current.Status = ObjectiveStatus.NotStarted;
            Publish();
        }

        private void Publish()
        {
            onObjectiveChanged?.Invoke(current);
        }
    }
}
