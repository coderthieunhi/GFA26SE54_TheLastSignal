using UnityEngine;

namespace TheLastSignal.Entities.Player
{
    /// <summary>
    /// Drives the player's Animator component from PlayerController's observed movement state.
    /// Strictly visual — no gameplay decisions. Feeds IsMoving/MoveX/MoveY every frame so an Idle/Moving
    /// blend (and later, directional Idle/Walk states) can read them without any restructuring.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class PlayerAnimator : MonoBehaviour
    {
        private static readonly int IsMovingParam = Animator.StringToHash("IsMoving");
        private static readonly int MoveXParam = Animator.StringToHash("MoveX");
        private static readonly int MoveYParam = Animator.StringToHash("MoveY");

        [SerializeField] private PlayerController playerController;

        private Animator animator;

        private void Awake()
        {
            animator = GetComponent<Animator>();
            if (playerController == null)
            {
                playerController = GetComponent<PlayerController>();
            }
        }

        private void Update()
        {
            if (playerController == null)
            {
                return;
            }

            // MoveX/MoveY are fed from FacingDirection (not raw MoveInput) so they hold the last
            // non-zero direction while idle, giving a future directional Idle blend tree a value to read.
            Vector2 facing = playerController.FacingDirection;
            animator.SetBool(IsMovingParam, playerController.IsMoving);
            animator.SetFloat(MoveXParam, facing.x);
            animator.SetFloat(MoveYParam, facing.y);
        }
    }
}
