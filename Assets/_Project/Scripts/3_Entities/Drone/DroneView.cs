using UnityEngine;

namespace TheLastSignal.Entities.Drone
{
    /// <summary>
    /// Visual/movement representation of the player-assistive drone entity. Reacts to replicated state;
    /// contains no gameplay decision logic, per ARCHITECTURE_RULES.md Rule 2.
    /// </summary>
    public class DroneView : MonoBehaviour
    {
        private void Update()
        {
            // TODO: interpolate toward observed/replicated drone position and drive visuals.
        }
    }
}
