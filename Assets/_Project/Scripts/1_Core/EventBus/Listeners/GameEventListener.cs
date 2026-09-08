using UnityEngine;
using UnityEngine.Events;
using TheLastSignal.Core.EventBus;

namespace TheLastSignal.Core.EventBus.Listeners
{
    /// <summary>
    /// Generic, Inspector-wireable MonoBehaviour that subscribes to one <see cref="EventChannelSO{T}"/>
    /// and forwards its payload to a UnityEvent. Reused for all three known channels (OnSanityChanged,
    /// OnTaskGenerated, OnMonsterStateChanged) by declaring a concrete, non-generic subclass per payload
    /// type — no changes to this file are needed to add a new channel.
    /// </summary>
    /// <typeparam name="T">The payload type of the channel being listened to.</typeparam>
    public abstract class GameEventListener<T> : MonoBehaviour
    {
        [SerializeField] private EventChannelSO<T> channel;
        [SerializeField] private UnityEvent<T> response;

        private void OnEnable()
        {
            if (channel != null)
            {
                channel.OnEventRaised += Respond;
            }
        }

        private void OnDisable()
        {
            if (channel != null)
            {
                channel.OnEventRaised -= Respond;
            }
        }

        private void Respond(T payload)
        {
            response?.Invoke(payload);
        }
    }
}
