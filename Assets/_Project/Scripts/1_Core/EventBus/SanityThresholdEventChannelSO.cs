using UnityEngine;

namespace TheLastSignal.Core.EventBus
{
    /// <summary>Concrete channel for sanity threshold crossings. Backs OnSanityThresholdCrossed.asset
    /// (see EVENTS.md). Raised by SanitySystem via GameManager, every time crossed, either direction.</summary>
    [CreateAssetMenu(menuName = "TheLastSignal/Events/Sanity Threshold Event Channel", fileName = "NewSanityThresholdEventChannel")]
    public class SanityThresholdEventChannelSO : EventChannelSO<SanityThresholdEvent>
    {
    }
}
