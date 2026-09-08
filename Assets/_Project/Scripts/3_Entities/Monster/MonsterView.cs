using UnityEngine;
using UnityEngine.AI;

namespace TheLastSignal.Entities.Monster
{
    /// <summary>
    /// Subscribes to OnMonsterStateChanged and drives the visual/navigation representation of the
    /// monster (NavMeshAgent movement, animation) on every peer. Never decides state itself, per
    /// ARCHITECTURE_RULES.md Rule 2.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class MonsterView : MonoBehaviour
    {
        private NavMeshAgent agent;

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
        }

        private void OnEnable()
        {
            // TODO: subscribe to the OnMonsterStateChanged EventChannelSO (or a GameEventListener component).
        }

        private void OnDisable()
        {
            // TODO: unsubscribe from the OnMonsterStateChanged EventChannelSO.
        }

        private void HandleMonsterStateChanged(Systems.Monster.MonsterState newState)
        {
            // TODO: drive NavMeshAgent destination/speed and animation from newState.
        }
    }
}
