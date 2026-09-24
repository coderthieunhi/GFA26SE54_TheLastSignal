using UnityEngine;

namespace TheLastSignal.Entities.Player
{
    /// <summary>
    /// Drives the player's Animator component and visual representation from PlayerController's observed movement state.
    /// Strictly visual — no gameplay decisions. Feeds IsMoving/MoveX/MoveY every frame so an Idle/Moving
    /// blend (and later, directional Idle/Walk states) can read them without any restructuring.
    /// Also updates directional sprites if no animation clips are actively driving SpriteRenderer.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class PlayerAnimator : MonoBehaviour
    {
        private static readonly int IsMovingParam = Animator.StringToHash("IsMoving");
        private static readonly int MoveXParam = Animator.StringToHash("MoveX");
        private static readonly int MoveYParam = Animator.StringToHash("MoveY");

        [SerializeField] private PlayerController playerController;
        [SerializeField] private SpriteRenderer spriteRenderer;

        [Header("Directional Idle Sprites (Optional / Fallback)")]
        [SerializeField] private Sprite spriteNorth;
        [SerializeField] private Sprite spriteSouth;
        [SerializeField] private Sprite spriteEast;
        [SerializeField] private Sprite spriteWest;
        [SerializeField] private Sprite spriteNorthEast;
        [SerializeField] private Sprite spriteNorthWest;
        [SerializeField] private Sprite spriteSouthEast;
        [SerializeField] private Sprite spriteSouthWest;

        private Animator animator;

        private void Awake()
        {
            animator = GetComponent<Animator>();
            if (playerController == null)
            {
                playerController = GetComponent<PlayerController>();
            }
            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponent<SpriteRenderer>();
            }
        }

        private void Update()
        {
            if (playerController == null)
            {
                return;
            }

            Vector2 facing = playerController.FacingDirection;
            bool isMoving = playerController.IsMoving;

            if (animator != null && animator.enabled)
            {
                animator.SetBool(IsMovingParam, isMoving);
                animator.SetFloat(MoveXParam, facing.x);
                animator.SetFloat(MoveYParam, facing.y);
            }

            UpdateDirectionalSprite(facing);
        }

        private void UpdateDirectionalSprite(Vector2 dir)
        {
            if (spriteRenderer == null) return;

            Sprite target = ResolveSpriteForDirection(dir);
            if (target != null && spriteRenderer.sprite != target)
            {
                spriteRenderer.sprite = target;
            }
        }

        private Sprite ResolveSpriteForDirection(Vector2 dir)
        {
            float x = dir.x;
            float y = dir.y;

            // 8-direction resolution
            if (Mathf.Abs(x) > 0.38f && Mathf.Abs(y) > 0.38f)
            {
                if (x > 0 && y > 0) return spriteNorthEast != null ? spriteNorthEast : spriteEast;
                if (x < 0 && y > 0) return spriteNorthWest != null ? spriteNorthWest : spriteWest;
                if (x > 0 && y < 0) return spriteSouthEast != null ? spriteSouthEast : spriteSouth;
                if (x < 0 && y < 0) return spriteSouthWest != null ? spriteSouthWest : spriteSouth;
            }

            if (Mathf.Abs(x) >= Mathf.Abs(y))
            {
                return x >= 0 ? spriteEast : spriteWest;
            }
            else
            {
                return y >= 0 ? spriteNorth : spriteSouth;
            }
        }
    }
}
