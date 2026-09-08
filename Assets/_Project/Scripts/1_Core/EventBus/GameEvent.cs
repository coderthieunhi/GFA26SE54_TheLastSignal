using UnityEngine;

namespace TheLastSignal.Core.EventBus
{
    /// <summary>
    /// Marker base class shared by every ScriptableObject-based event channel in the project.
    /// Concrete, payload-typed channels derive from <see cref="EventChannelSO{T}"/>, not from this class directly.
    /// </summary>
    public abstract class GameEvent : ScriptableObject
    {
    }
}
