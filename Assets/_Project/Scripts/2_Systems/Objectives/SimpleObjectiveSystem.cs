using System;

namespace TheLastSignal.Systems.Objectives
{
    /// <summary>
    /// Single-objective tracker for the prototype ("Reach the Exit"). Pure C# — GameManager owns
    /// the instance and raises EventChannelSO updates. Not the Task Ticket System.
    /// </summary>
    public class SimpleObjectiveSystem : IObjectiveSystem
    {
        private readonly Action<ObjectiveData> onObjectiveChanged;
        private ObjectiveData current;

        public SimpleObjectiveSystem(Action<ObjectiveData> onObjectiveChanged)
        {
            this.onObjectiveChanged = onObjectiveChanged;
            current = default;
        }

        public ObjectiveData CurrentObjective => current;

        public bool IsCompleted => current.Status == ObjectiveStatus.Completed;

        public void Initialize()
        {
        }

        public void Tick(float deltaTime)
        {
        }

        public void Shutdown()
        {
            current = default;
        }

        public void Begin(ObjectiveData initial)
        {
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

            current.Status = ObjectiveStatus.Completed;
            Publish();
            return true;
        }

        public void ResetObjective()
        {
            if (string.IsNullOrEmpty(current.Id))
            {
                current = ObjectiveData.ReachTheExit();
            }

            current.Status = ObjectiveStatus.NotStarted;
            Publish();
        }

        private void Publish()
        {
            onObjectiveChanged?.Invoke(current);
        }
    }
}
