namespace TheLastSignal.Systems.Objectives
{
    /// <summary>
    /// Host-owned objective tracker for the prototype game loop. Epic 8 can replace the
    /// implementation with TaskTicketSystem while GameManager keeps talking to this contract.
    /// </summary>
    public interface IObjectiveSystem : IGameSystem
    {
        ObjectiveData CurrentObjective { get; }

        bool IsCompleted { get; }

        void Begin(ObjectiveData initial);

        bool TryComplete();

        void ResetObjective();
    }
}
