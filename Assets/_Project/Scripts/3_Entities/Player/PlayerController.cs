using Unity.Netcode;
using UnityEngine;

namespace TheLastSignal.Entities.Player
{
    /// <summary>
    /// Captures local player input and forwards it to the Host via ServerRpc. Per ARCHITECTURE_RULES.md
    /// Rule 2/3, this class never computes Sanity/Station/Monster state itself — input goes up, state
    /// comes down through NetworkVariable sync or ClientRpc.
    /// </summary>
    public class PlayerController : NetworkBehaviour
    {
        private void Update()
        {
            if (!IsOwner)
            {
                return;
            }

            // TODO: read movement/interact input here and forward it via ServerRpc(s) on this
            // or a related NetworkBehaviour. No local Sanity/Station/Monster state math.
        }
    }
}
