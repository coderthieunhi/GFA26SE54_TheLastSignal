using UnityEngine;
using TheLastSignal.Systems.Monster;

namespace TheLastSignal.Core.EventBus
{
    /// <summary>Concrete MonsterState-payload channel. Backs the OnMonsterStateChanged.asset event (see EVENTS.md).</summary>
    [CreateAssetMenu(menuName = "TheLastSignal/Events/Monster State Event Channel", fileName = "NewMonsterStateEventChannel")]
    public class MonsterStateEventChannelSO : EventChannelSO<MonsterState>
    {
    }
}
