using UnityEngine;
using UnityEngine.InputSystem;

namespace TheLastSignal.Entities.Player
{
    /// <summary>
    /// Single-player prototype controller: reads local keyboard input, applies 8-directional
    /// physics-based movement, and detects nearby interactable objects. Movement application is kept
    /// separate from input reading (ReadMovementInput vs ApplyMovement) so that when multiplayer lands,
    /// input capture can stay local while ApplyMovement moves behind a ServerRpc on the Host — see
    /// ARCHITECTURE_RULES.md Rule 3. Story 1.2 owns actually triggering interaction on CurrentInteractable.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(CapsuleCollider2D))]
    public class PlayerController : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float moveSpeed = 5f;

        [Header("Interaction Detection")]
        [SerializeField] private float interactionRange = 2f;
        [SerializeField] private LayerMask interactableLayer;
        [SerializeField, Range(0f, 1f)] private float facingDotThreshold = 0.5f;

        private Rigidbody2D rb;
        private Vector2 moveInput;
        private Vector2 facingDirection = Vector2.down;

        /// <summary>Raw (already clamped to length 1) movement input for this frame.</summary>
        public Vector2 MoveInput => moveInput;

        /// <summary>The last non-zero movement direction; persists while idle so the player keeps facing it.</summary>
        public Vector2 FacingDirection => facingDirection;

        /// <summary>True while the player has non-zero movement input.</summary>
        public bool IsMoving => moveInput.sqrMagnitude > 0.0001f;

        /// <summary>
        /// The interactable the player should be able to interact with right now, or null if none is
        /// in range. Recomputed every frame, so it never goes stale when the player moves out of range.
        /// </summary>
        public Collider2D CurrentInteractable { get; private set; }

        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;

            int playerLayer = LayerMask.NameToLayer("Player");
            if (playerLayer >= 0)
            {
                gameObject.layer = playerLayer;
            }
        }

        private void Update()
        {
            ReadMovementInput();
            UpdateInteractionDetection();
        }

        private void FixedUpdate()
        {
            ApplyMovement(moveInput);
        }

        /// <summary>
        /// Reads raw keyboard state via the new Input System. Kept separate from ApplyMovement so this
        /// method (and only this method) needs to change when input capture is later relayed to the
        /// Host instead of applied locally.
        /// </summary>
        private void ReadMovementInput()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                moveInput = Vector2.zero;
                return;
            }

            float x = 0f;
            float y = 0f;

            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed)
            {
                x -= 1f;
            }
            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed)
            {
                x += 1f;
            }
            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed)
            {
                y -= 1f;
            }
            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed)
            {
                y += 1f;
            }

            // Clamp (rather than only normalizing) so single-axis input stays length 1 while
            // diagonal input is scaled down to length 1 instead of sqrt(2).
            moveInput = Vector2.ClampMagnitude(new Vector2(x, y), 1f);

            if (moveInput.sqrMagnitude > 0.0001f)
            {
                facingDirection = moveInput.normalized;
            }
        }

        /// <summary>
        /// Applies movement to the Rigidbody2D via MovePosition so collisions are respected. This is the
        /// method that will move behind a ServerRpc (Host-only) once multiplayer input relay exists —
        /// see ARCHITECTURE_RULES.md Rule 3.
        /// </summary>
        private void ApplyMovement(Vector2 direction)
        {
            Vector2 targetPosition = rb.position + direction * (moveSpeed * Time.fixedDeltaTime);
            rb.MovePosition(targetPosition);
        }

        /// <summary>
        /// Finds interactable colliders within range and selects the one the player is facing; falls
        /// back to the closest one if none is faced directly. Only identifies the target — Story 1.2
        /// handles actually triggering interaction on it.
        /// </summary>
        private void UpdateInteractionDetection()
        {
            Collider2D[] hits = Physics2D.OverlapCircleAll(rb.position, interactionRange, interactableLayer);

            if (hits.Length == 0)
            {
                CurrentInteractable = null;
                return;
            }

            Collider2D facedHit = null;
            float facedBestDot = facingDotThreshold;
            Collider2D closestHit = null;
            float closestSqrDistance = float.MaxValue;

            foreach (Collider2D hit in hits)
            {
                Vector2 toHit = (Vector2)hit.bounds.center - rb.position;
                float sqrDistance = toHit.sqrMagnitude;

                if (sqrDistance < closestSqrDistance)
                {
                    closestSqrDistance = sqrDistance;
                    closestHit = hit;
                }

                if (sqrDistance > 0.0001f)
                {
                    float dot = Vector2.Dot(toHit.normalized, facingDirection);
                    if (dot > facedBestDot)
                    {
                        facedBestDot = dot;
                        facedHit = hit;
                    }
                }
            }

            CurrentInteractable = facedHit != null ? facedHit : closestHit;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, interactionRange);
        }
    }
}
