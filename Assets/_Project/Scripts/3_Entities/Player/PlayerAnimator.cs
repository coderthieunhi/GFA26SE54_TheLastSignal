using UnityEngine;

namespace TheLastSignal.Entities.Player
{
    /// <summary>
    /// Drives the player's Animator component from observed, already-replicated state (movement,
    /// interact, etc.). Strictly visual — no gameplay decisions.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class PlayerAnimator : MonoBehaviour
    {
        private Animator animator;

        private void Awake()
        {
            animator = GetComponent<Animator>();
        }

        private void Update()
        {
            // TODO: read observed movement/state and set the corresponding Animator parameters.
        }
    }
}
