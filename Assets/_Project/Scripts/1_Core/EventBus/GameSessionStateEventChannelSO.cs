using UnityEngine;
using TheLastSignal.Core;

namespace TheLastSignal.Core.EventBus
{
    /// <summary>Concrete GameSessionState-payload channel. Backs the OnGameSessionStateChanged.asset event (see EVENTS.md).</summary>
    [CreateAssetMenu(menuName = "TheLastSignal/Events/Game Session State Event Channel", fileName = "NewGameSessionStateEventChannel")]
    public class GameSessionStateEventChannelSO : EventChannelSO<GameSessionState>
    {
    }
}
