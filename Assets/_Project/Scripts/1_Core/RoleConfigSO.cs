using UnityEngine;

namespace TheLastSignal.Core
{
    /// <summary>
    /// Designer-tunable configuration describing the player roles available in a match. Consumed by
    /// GameManager/LobbyUI when assigning roles to connected clients.
    /// </summary>
    [CreateAssetMenu(menuName = "TheLastSignal/Configs/Role Config", fileName = "RoleConfigSO")]
    public class RoleConfigSO : ScriptableObject
    {
        // TODO: define the list of available roles and any per-role settings once the design is finalized.
    }
}
