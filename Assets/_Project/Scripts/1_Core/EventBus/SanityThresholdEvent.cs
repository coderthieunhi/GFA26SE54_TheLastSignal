using TheLastSignal.Systems.Sanity;

namespace TheLastSignal.Core.EventBus
{
    /// <summary>Payload for SanityThresholdEventChannelSO: which player crossed, and their new level.</summary>
    public readonly struct SanityThresholdEvent
    {
        public readonly ulong PlayerId;
        public readonly SanityLevel Level;

        public SanityThresholdEvent(ulong playerId, SanityLevel level)
        {
            PlayerId = playerId;
            Level = level;
        }
    }
}
