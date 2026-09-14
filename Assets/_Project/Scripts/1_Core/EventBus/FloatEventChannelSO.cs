using UnityEngine;

namespace TheLastSignal.Core.EventBus
{
    /// <summary>Concrete float-payload channel. Backs the OnSanityChanged.asset event (see EVENTS.md).</summary>
    [CreateAssetMenu(menuName = "TheLastSignal/Events/Float Event Channel", fileName = "NewFloatEventChannel")]
    public class FloatEventChannelSO : EventChannelSO<float>
    {
    }
}
