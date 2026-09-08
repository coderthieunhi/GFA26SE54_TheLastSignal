namespace TheLastSignal.Systems.Tasks
{
    /// <summary>
    /// Plain data describing a single generated task/ticket. No MonoBehaviour — carried as the payload
    /// of the OnTaskGenerated event and passed between TaskTicketSystem and station views.
    /// </summary>
    [System.Serializable]
    public struct TaskData
    {
        // TODO FR-PUZ-01: define the fields a task needs (id, station type, difficulty, time limit, etc.).
    }
}
