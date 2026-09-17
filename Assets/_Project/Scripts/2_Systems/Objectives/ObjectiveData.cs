namespace TheLastSignal.Systems.Objectives
{
    /// <summary>
    /// Plain data for the current session objective. Intentionally small so Epic 8 can swap this
    /// for <see cref="TheLastSignal.Systems.Tasks.TaskData"/> without rewriting the session loop.
    /// </summary>
    [System.Serializable]
    public struct ObjectiveData
    {
        public string Id;
        public string Description;
        public ObjectiveStatus Status;

        public static ObjectiveData ActivateReactor()
        {
            return new ObjectiveData
            {
                Id = "activate_reactor",
                Description = "Reach Reactor Core & Activate Console",
                Status = ObjectiveStatus.NotStarted
            };
        }

        public static ObjectiveData EvacuateAirlock()
        {
            return new ObjectiveData
            {
                Id = "evacuate_airlock",
                Description = "EVACUATE! Return to Airlock before Meltdown!",
                Status = ObjectiveStatus.NotStarted
            };
        }

        public static ObjectiveData ReachTheExit()
        {
            return new ObjectiveData
            {
                Id = "reach_exit",
                Description = "Reach the Exit",
                Status = ObjectiveStatus.NotStarted
            };
        }
    }
}
