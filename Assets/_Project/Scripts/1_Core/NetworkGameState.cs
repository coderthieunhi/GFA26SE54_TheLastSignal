using Unity.Netcode;
using UnityEngine;
using TheLastSignal.Systems.Monster;
using TheLastSignal.Core.EventBus;

namespace TheLastSignal.Core
{
    /// <summary>
    /// The only class allowed to touch both the 2_Systems/ (Pure C#) layer and the Netcode layer. Holds
    /// every NetworkVariable in the project and bridges Host-side system state to replicated state that
    /// clients can read, and to EventChannelSO events that clients can subscribe to. Per
    /// ARCHITECTURE_RULES.md Rule 1, all writes to these NetworkVariables happen only when IsServer is true.
    /// </summary>
    public class NetworkGameState : NetworkBehaviour
    {
        [SerializeField] private FloatEventChannelSO onSanityChanged;
        [SerializeField] private MonsterStateEventChannelSO onMonsterStateChanged;

        private readonly NetworkVariable<float> sanity = new NetworkVariable<float>();
        private readonly NetworkVariable<MonsterState> monsterState = new NetworkVariable<MonsterState>();

        public override void OnNetworkSpawn()
        {
            // TODO: subscribe to sanity.OnValueChanged / monsterState.OnValueChanged to raise the
            // matching EventChannelSO on every peer (Host included) when replicated values change.
        }

        public override void OnNetworkDespawn()
        {
            // TODO: unsubscribe from NetworkVariable OnValueChanged callbacks.
        }

        /// <summary>Called by the Host to push a new sanity value from SanitySystem into replicated state.</summary>
        public void ServerSetSanity(float newValue)
        {
            // TODO: guard with IsServer, then assign sanity.Value = newValue.
        }

        /// <summary>Called by the Host to push a new monster state from MonsterFSM into replicated state.</summary>
        public void ServerSetMonsterState(MonsterState newState)
        {
            // TODO: guard with IsServer, then assign monsterState.Value = newState.
        }

        /// <summary>Client entry point for input: forwards a station interaction request to the Host.</summary>
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void RequestInteractRpc(RpcParams rpcParams = default)
        {
            // TODO: validate via NetworkAuthoritySystem, then route to the relevant 2_Systems/ class.
        }
    }
}
