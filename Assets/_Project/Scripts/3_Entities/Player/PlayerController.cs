using UnityEngine;
using UnityEngine.InputSystem;
using TheLastSignal.Core.Interaction;
using TheLastSignal.Entities.Camera;

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

        public float InteractionRange => interactionRange;

        private Rigidbody2D rb;
        private Vector2 moveInput;
        private Vector2 facingDirection = Vector2.down;
        private PlayerDirection facing = PlayerDirection.Down;
        private bool controlEnabled = false;
        private int slotIndex;

        /// <summary>Raw (already clamped to length 1) movement input for this frame.</summary>
        public Vector2 MoveInput => moveInput;

        /// <summary>The last non-zero movement direction; persists while idle so the player keeps facing it.</summary>
        public Vector2 FacingDirection => facingDirection;

        /// <summary>
        /// The last non-zero movement direction collapsed to one of 4 cardinal directions (dominant
        /// axis wins; ties resolve to horizontal). Persists while idle. For consumers that need a
        /// discrete Up/Down/Left/Right value (animation, flashlight, interaction, role abilities, ...)
        /// instead of the continuous <see cref="FacingDirection"/> vector.
        /// </summary>
        public PlayerDirection Facing => facing;

        /// <summary>True while the player has non-zero movement input.</summary>
        public bool IsMoving => moveInput.sqrMagnitude > 0.0001f;

        /// <summary>Spawn slot this instance occupies (0 = P1 … 3 = P4).</summary>
        public int SlotIndex => slotIndex;

        public bool ControlEnabled => controlEnabled;

        /// <summary>
        /// The interactable the player should be able to interact with right now, or null if none is
        /// in range. Recomputed every frame, so it never goes stale when the player moves out of range.
        /// </summary>
        public IInteractable CurrentInteractable { get; private set; }

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

        private void Start()
        {
            BindLocalCamera();
        }

        /// <summary>Assigns this instance to a spawn slot so co-op can address P1–P4 later.</summary>
        public void BindSlot(int slot)
        {
            slotIndex = Mathf.Clamp(slot, 0, PlayerSpawnPoint.MaxSlots - 1);
            gameObject.name = slotIndex == 0 ? "Player" : $"Player_P{slotIndex + 1}";
        }

        public void SetControlEnabled(bool enabled)
        {
            controlEnabled = enabled;
            if (!enabled)
            {
                moveInput = Vector2.zero;
            }
        }

        /// <summary>
        /// Teleports the player to <paramref name="worldPosition"/> and clears movement / interactable
        /// state so a session restart does not inherit the previous run.
        /// </summary>
        public void ResetState(Vector2 worldPosition)
        {
            if (rb == null)
            {
                rb = GetComponent<Rigidbody2D>();
            }

            moveInput = Vector2.zero;
            facingDirection = Vector2.down;
            facing = PlayerDirection.Down;
            CurrentInteractable = null;
            controlEnabled = false;

            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
            rb.position = worldPosition;
            transform.position = new Vector3(worldPosition.x, worldPosition.y, transform.position.z);

            BindLocalCamera();
        }

        private void BindLocalCamera()
        {
            // Hand this player's Transform to the local camera as its follow target. Deliberately not
            // a scene-wide "find the player" lookup on the camera's side (ARCHITECTURE_RULES.md): in a
            // future co-op version, each client's camera must target only its own local player, so the
            // player is the one announcing itself here rather than the camera searching for one.
            if (slotIndex != 0)
            {
                return;
            }

            UnityEngine.Camera mainCamera = UnityEngine.Camera.main;
            if (mainCamera != null && mainCamera.TryGetComponent(out CameraFollow cameraFollow))
            {
                cameraFollow.SetTarget(transform);
            }
        }

        private void Update()
        {
            if (!controlEnabled)
            {
                moveInput = Vector2.zero;
                CurrentInteractable = null;
                return;
            }

            ReadMovementInput();
            ReadInteractionInput();
            UpdateInteractionDetection();
        }

        private void FixedUpdate()
        {
            if (!controlEnabled)
            {
                return;
            }

            ApplyMovement(moveInput);
        }

        private void ReadInteractionInput()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.eKey.wasPressedThisFrame)
            {
                AttemptInteraction();
            }
        }

        private void AttemptInteraction()
        {
            if (CurrentInteractable != null)
            {
                CurrentInteractable.Interact(this);
            }
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
                facing = ResolveCardinalDirection(moveInput);
            }
        }

        /// <summary>
        /// Collapses a movement vector to one of 4 cardinal directions by picking the axis with the
        /// larger absolute component (ties resolve to horizontal), so diagonal input always resolves to
        /// exactly one unambiguous direction instead of a blended/diagonal state.
        /// </summary>
        private static PlayerDirection ResolveCardinalDirection(Vector2 input)
        {
            if (Mathf.Abs(input.x) >= Mathf.Abs(input.y))
            {
                return input.x >= 0f ? PlayerDirection.Right : PlayerDirection.Left;
            }

            return input.y >= 0f ? PlayerDirection.Up : PlayerDirection.Down;
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

            IInteractable facedInteractable = null;
            float facedBestDot = facingDotThreshold;
            IInteractable closestInteractable = null;
            float closestSqrDistance = float.MaxValue;

            foreach (Collider2D hit in hits)
            {
                IInteractable interactable = hit.GetComponent<IInteractable>();
                if (interactable == null || !interactable.CanInteract(this))
                {
                    continue;
                }

                Vector2 toHit = (Vector2)hit.bounds.center - rb.position;
                float sqrDistance = toHit.sqrMagnitude;

                if (sqrDistance < closestSqrDistance)
                {
                    closestSqrDistance = sqrDistance;
                    closestInteractable = interactable;
                }

                if (sqrDistance > 0.0001f)
                {
                    float dot = Vector2.Dot(toHit.normalized, facingDirection);
                    if (dot > facedBestDot)
                    {
                        facedBestDot = dot;
                        facedInteractable = interactable;
                    }
                }
            }

            CurrentInteractable = facedInteractable != null ? facedInteractable : closestInteractable;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, interactionRange);
        }
    }
}
