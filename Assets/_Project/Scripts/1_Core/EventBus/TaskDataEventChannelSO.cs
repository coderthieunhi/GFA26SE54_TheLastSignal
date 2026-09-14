using UnityEngine;
using TheLastSignal.Systems.Tasks;

namespace TheLastSignal.Core.EventBus
{
    /// <summary>Concrete TaskData-payload channel. Backs the OnTaskGenerated.asset event (see EVENTS.md).</summary>
    [CreateAssetMenu(menuName = "TheLastSignal/Events/Task Data Event Channel", fileName = "NewTaskDataEventChannel")]
    public class TaskDataEventChannelSO : EventChannelSO<TaskData>
    {
    }
}
