using UnityEngine;
using TheLastSignal.Systems.Objectives;

namespace TheLastSignal.Core.EventBus
{
    /// <summary>Concrete ObjectiveData-payload channel. Backs the OnObjectiveChanged.asset event (see EVENTS.md).</summary>
    [CreateAssetMenu(menuName = "TheLastSignal/Events/Objective Data Event Channel", fileName = "NewObjectiveDataEventChannel")]
    public class ObjectiveDataEventChannelSO : EventChannelSO<ObjectiveData>
    {
    }
}
