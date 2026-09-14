using System;
using UnityEngine;
using TheLastSignal.Core;
using TheLastSignal.Systems.Tasks;
using TheLastSignal.Systems.Monster;
using TheLastSignal.Systems.Objectives;

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
}
