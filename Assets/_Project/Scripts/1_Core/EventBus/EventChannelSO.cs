using System;
using UnityEngine;
using TheLastSignal.Systems.Tasks;
using TheLastSignal.Systems.Monster;

namespace TheLastSignal.Core.EventBus
{
    /// <summary>
    /// Generic, reusable event channel asset. The Host-side system that owns a piece of data calls
    /// <see cref="Raise"/> on its channel asset; any number of listeners (direct C# subscribers or
    /// <see cref="Listeners.GameEventListener{T}"/> components) react to <see cref="OnEventRaised"/>.
    /// This class is not meant to be instantiated directly — create one concrete, non-generic subclass
    /// per payload type so Unity can create .asset instances of it (see the three subclasses below).
    /// </summary>
    /// <typeparam name="T">The payload type carried by this channel.</typeparam>
    public abstract class EventChannelSO<T> : GameEvent
    {
        /// <summary>Raised whenever <see cref="Raise"/> is called.</summary>
        public event Action<T> OnEventRaised;

        /// <summary>
        /// Publishes <paramref name="payload"/> to every subscriber. Per ARCHITECTURE_RULES.md, this must
        /// only ever be called from Host-side code (2_Systems/ or NetworkGameState) — never from
        /// 3_Entities/ or 5_UI/, which only ever subscribe.
        /// </summary>
        public void Raise(T payload)
        {
            OnEventRaised?.Invoke(payload);
        }
    }

    /// <summary>Concrete float-payload channel. Backs the OnSanityChanged.asset event (see EVENTS.md).</summary>
    [CreateAssetMenu(menuName = "TheLastSignal/Events/Float Event Channel", fileName = "NewFloatEventChannel")]
    public class FloatEventChannelSO : EventChannelSO<float>
    {
    }

    /// <summary>Concrete TaskData-payload channel. Backs the OnTaskGenerated.asset event (see EVENTS.md).</summary>
    [CreateAssetMenu(menuName = "TheLastSignal/Events/Task Data Event Channel", fileName = "NewTaskDataEventChannel")]
    public class TaskDataEventChannelSO : EventChannelSO<TaskData>
    {
    }

    /// <summary>Concrete MonsterState-payload channel. Backs the OnMonsterStateChanged.asset event (see EVENTS.md).</summary>
    [CreateAssetMenu(menuName = "TheLastSignal/Events/Monster State Event Channel", fileName = "NewMonsterStateEventChannel")]
    public class MonsterStateEventChannelSO : EventChannelSO<MonsterState>
    {
    }
}
