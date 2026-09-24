using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using TheLastSignal.Entities.Camera;
using TheLastSignal.Entities.Interaction;

namespace TheLastSignal.Entities.Player
{
    /// <summary>
    /// Single-player prototype controller: reads local keyboard input, applies 8-directional
    /// physics-based movement, and detects nearby interactable objects. Movement application is kept
    /// separate from input reading (ReadMovementInput vs ApplyMovement) so that when multiplayer lands,
    /// input capture can stay local while ApplyMovement moves behind a ServerRpc on the Host — see
    /// ARCHITECTURE_RULES.md Rule 3.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(CapsuleCollider2D))]
    public class PlayerController : NetworkBehaviour
    {
        /// <summary>
        /// True when an NGO session is actually running. Used so every ownership check degrades
        /// cleanly to single-player behaviour in the Epic 1 scene, which has no NetworkManager.
        /// Qualified because inside a NetworkBehaviour, `NetworkManager` is the instance property.
        /// </summary>
        private static bool IsNetworkSessionActive =>
            Unity.Netcode.NetworkManager.Singleton != null &&
            Unity.Netcode.NetworkManager.Singleton.IsListening;

        [Header("Movement")]
        [SerializeField] private float moveSpeed = 3f;
        [SerializeField] private float acceleration = 15.5f;
        [SerializeField] private float deceleration = 21.5f;

        [Header("Corner Assist")]
        [SerializeField] private bool enableCornerAssist = true;
        [SerializeField] private float cornerRayDistance = 0.45f;
        [SerializeField] private float cornerSideOffset = 0.35f;
        [SerializeField] private float cornerNudgeSpeed = 2.6f;
        [SerializeField] private LayerMask collisionLayers;

        [Header("Interaction Detection")]
        [SerializeField] private float interactionRange = 0.55f;
        [SerializeField] private LayerMask interactableLayer;
        [SerializeField, Range(0f, 1f)] private float facingDotThreshold = 0.4f;

        public float MoveSpeed => moveSpeed;
        public float InteractionRange => interactionRange;

        private Rigidbody2D rb;
        private CapsuleCollider2D capsuleCollider;
        private Vector2 moveInput;
        private Vector2 currentVelocity;
        private Vector2 facingDirection = Vector2.down;
        private PlayerDirection facing = PlayerDirection.Down;
        private bool controlEnabled = false;
        private int slotIndex;

        /// <summary>Raw (already clamped to length 1) movement input for this frame.</summary>
        public Vector2 MoveInput => moveInput;

        /// <summary>Current actual velocity after acceleration and deceleration smoothing.</summary>
        public Vector2 CurrentVelocity => currentVelocity;

        /// <summary>The last non-zero movement direction; persists while idle so the player keeps facing it.</summary>
        public Vector2 FacingDirection => facingDirection;

        /// <summary>
        /// The last non-zero movement direction collapsed to one of 4 cardinal directions (dominant
        /// axis wins; ties resolve to horizontal). Persists while idle.
        /// </summary>
        public PlayerDirection Facing => facing;

        /// <summary>True while the player has non-zero movement input or is decelerating to a stop.</summary>
        public bool IsMoving => currentVelocity.sqrMagnitude > 0.01f || moveInput.sqrMagnitude > 0.0001f;

        /// <summary>Spawn slot this instance occupies (0 = P1 … 3 = P4).</summary>
        public int SlotIndex => slotIndex;

        public bool ControlEnabled => controlEnabled;

        /// <summary>
        /// The interactable collider the player should be able to interact with right now, or null if none is
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

            capsuleCollider = GetComponent<CapsuleCollider2D>();
            if (capsuleCollider != null)
            {
                // Frictionless material eliminates sticking against wall colliders on diagonal contact
                PhysicsMaterial2D zeroFriction = new PhysicsMaterial2D("Player_ZeroFriction")
                {
                    friction = 0f,
                    bounciness = 0f
                };
                capsuleCollider.sharedMaterial = zeroFriction;
            }

            int playerLayer = LayerMask.NameToLayer("Player");
            if (playerLayer >= 0)
            {
                gameObject.layer = playerLayer;
            }

            if (collisionLayers.value == 0)
            {
                int obstacleLayer = LayerMask.NameToLayer("Obstacle");
                if (obstacleLayer >= 0)
                {
                    collisionLayers |= (1 << obstacleLayer);
                }
                int defaultLayer = LayerMask.NameToLayer("Default");
                if (defaultLayer >= 0)
                {
                    collisionLayers |= (1 << defaultLayer);
                }
            }
        }

        private void Start()
        {
            // When NGO is running, ownership is not known yet at Start — OnNetworkSpawn binds the
            // camera instead, so a remote player's instance never steals this client's camera.
            if (IsNetworkSessionActive)
            {
                return;
            }

            BindLocalCamera();
        }

        /// <summary>
        /// Story 3.1: the locally owned instance claims this client's camera and enables its own input.
        /// Remote instances stay input-dead and are driven by NetworkTransform replication.
        /// </summary>
        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            if (!IsOwner)
            {
                return;
            }

            // Owner-authoritative NetworkTransform means the Host cannot teleport a client-owned body,
            // so each owner places itself on its own spawn slot. The slot is derived from OwnerClientId
            // so no extra RPC or Host coordination is needed for this story.
            BindSlot((int)OwnerClientId);
            ResetState(ResolveNetworkSpawnPosition(slotIndex));

            // Control is enabled locally by the owner because `controlEnabled` is a client-side input
            // gate, and GameManager.StartSession only runs on the Host.
            // TODO (Story 3.2+): drive this from replicated session state so control follows
            // Gameplay/Win/Lose for every client instead of being enabled on spawn.
            SetControlEnabled(true);
        }

        /// <summary>
        /// Finds this slot's <see cref="PlayerSpawnPoint"/> in the loaded scene. Falls back to the
        /// current position so a scene without spawn points never teleports the player to the origin.
        /// </summary>
        private Vector2 ResolveNetworkSpawnPosition(int slot)
        {
            PlayerSpawnPoint[] points = FindObjectsByType<PlayerSpawnPoint>();
            for (int i = 0; i < points.Length; i++)
            {
                if (points[i] != null && points[i].SlotIndex == slot)
                {
                    return points[i].Position;
                }
            }

            return transform.position;
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
                currentVelocity = Vector2.zero;
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
            currentVelocity = Vector2.zero;
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
            // a scene-wide "find the player" lookup on the camera's side (ARCHITECTURE_RULES.md): each
            // client's camera must target only its own local player, so the player is the one
            // announcing itself here rather than the camera searching for one.
            if (IsSpawned)
            {
                // Networked: only the locally owned instance may claim this client's camera.
                if (!IsOwner)
                {
                    return;
                }
            }
            else if (slotIndex != 0)
            {
                // Non-networked (Epic 1): P1 drives the camera.
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
            UpdateInteractionDetection();
            HandleInteractionInput();
        }

        private void HandleInteractionInput()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.eKey.wasPressedThisFrame)
            {
                TriggerInteraction();
            }
        }

        /// <summary>
        /// Attempts to interact with <see cref="CurrentInteractable"/>. Supports any <see cref="IInteractable"/>
        /// implementer (e.g. Door) as well as legacy <see cref="Stations.StationView"/>.
        /// </summary>
        public void TriggerInteraction()
        {
            if (CurrentInteractable == null)
            {
                return;
            }

            // Check for IInteractable on the collider or its parents
            if (CurrentInteractable.TryGetComponent(out IInteractable interactable) ||
                CurrentInteractable.GetComponentInParent<IInteractable>() is { } parentInteractable && (interactable = parentInteractable) != null)
            {
                if (interactable.CanInteract)
                {
                    interactable.Interact(gameObject);
                }
                return;
            }

            // Check for Core.Interaction.IInteractable (StationView, InteractableBase)
            if (CurrentInteractable.TryGetComponent(out TheLastSignal.Core.Interaction.IInteractable coreInteractable) ||
                CurrentInteractable.GetComponentInParent<TheLastSignal.Core.Interaction.IInteractable>() is { } parentCore && (coreInteractable = parentCore) != null)
            {
                if (coreInteractable.CanInteract(this))
                {
                    coreInteractable.Interact(this);
                }
                return;
            }

            // Fallback for StationView if not implementing IInteractable
            if (CurrentInteractable.TryGetComponent(out Stations.StationView station) ||
                CurrentInteractable.GetComponentInParent<Stations.StationView>() is { } parentStation && (station = parentStation) != null)
            {
                station.Interact(this);
            }
        }

        private void FixedUpdate()
        {
            if (!controlEnabled)
            {
                return;
            }

            ApplyMovement(moveInput);
        }

        /// <summary>
        /// Reads raw keyboard state via the new Input System. Kept separate from ApplyMovement so this
        /// method (and only this method) needs to change when input capture is later relayed to the
        /// Host instead of applied locally.
        /// </summary>
        private void ReadMovementInput()
        {
            // Story 3.1 (owner-authoritative / Option A): only the owning client captures input for
            // this body. Gated on IsSpawned so the non-networked Epic 1 scene, where nothing is ever
            // NGO-spawned and IsOwner is therefore always false, keeps working unchanged.
            if (IsSpawned && !IsOwner)
            {
                return;
            }

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
        /// Applies movement to the Rigidbody2D via MovePosition with smooth acceleration/deceleration
        /// and smart corner assist. This is the method that will move behind a ServerRpc (Host-only)
        /// once multiplayer input relay exists — see ARCHITECTURE_RULES.md Rule 3.
        /// </summary>
        private void ApplyMovement(Vector2 direction)
        {
            Vector2 targetVelocity = direction * moveSpeed;
            float rate = direction.sqrMagnitude > 0.0001f ? acceleration : deceleration;
            currentVelocity = Vector2.MoveTowards(currentVelocity, targetVelocity, rate * Time.fixedDeltaTime);

            Vector2 moveDelta = currentVelocity * Time.fixedDeltaTime;

            if (enableCornerAssist && currentVelocity.sqrMagnitude > 0.01f)
            {
                Vector2 nudge = CalculateCornerNudge(currentVelocity.normalized);
                moveDelta += nudge;
            }

            Vector2 targetPosition = rb.position + moveDelta;
            rb.MovePosition(targetPosition);
        }

        private readonly RaycastHit2D[] cornerRaycastBuffer = new RaycastHit2D[8];

        /// <summary>
        /// Performs a raycast that ignores triggers and the player's own collider,
        /// returning only solid obstacle hits that would physically block movement.
        /// </summary>
        private RaycastHit2D RaycastSolid(Vector2 origin, Vector2 direction, float distance, LayerMask layerMask)
        {
            ContactFilter2D filter = new ContactFilter2D();
            filter.useTriggers = false;
            filter.SetLayerMask(layerMask);

            int count = Physics2D.Raycast(origin, direction, filter, cornerRaycastBuffer, distance);
            RaycastHit2D closestHit = default;
            float minDistance = float.MaxValue;

            for (int i = 0; i < count; i++)
            {
                RaycastHit2D hit = cornerRaycastBuffer[i];
                if (hit.collider != null && !hit.collider.isTrigger && hit.collider != capsuleCollider)
                {
                    if (hit.distance < minDistance)
                    {
                        minDistance = hit.distance;
                        closestHit = hit;
                    }
                }
            }

            return closestHit;
        }

        /// <summary>
        /// Detects if the player's collider is catching on an obstacle corner/doorframe while the
        /// other side has open clearance, and calculates a perpendicular nudge vector to slide smoothly.
        /// </summary>
        private Vector2 CalculateCornerNudge(Vector2 moveDir)
        {
            Vector2 pos = rb.position;
            Vector2 perp = new Vector2(-moveDir.y, moveDir.x);

            RaycastHit2D leftHit = RaycastSolid(pos - perp * cornerSideOffset, moveDir, cornerRayDistance, collisionLayers);
            RaycastHit2D rightHit = RaycastSolid(pos + perp * cornerSideOffset, moveDir, cornerRayDistance, collisionLayers);

            // If both sides hit, it's a flat wall or dead end - no nudge
            if (leftHit.collider != null && rightHit.collider != null)
            {
                return Vector2.zero;
            }

            // Left side caught on corner, right side is open -> nudge right (+perp)
            if (leftHit.collider != null && rightHit.collider == null)
            {
                // Verify nudge path is clear of solid obstacles
                if (RaycastSolid(pos, perp, cornerSideOffset + 0.1f, collisionLayers).collider == null)
                {
                    return perp * (cornerNudgeSpeed * Time.fixedDeltaTime);
                }
            }
            // Right side caught on corner, left side is open -> nudge left (-perp)
            else if (rightHit.collider != null && leftHit.collider == null)
            {
                // Verify nudge path is clear of solid obstacles
                if (RaycastSolid(pos, -perp, cornerSideOffset + 0.1f, collisionLayers).collider == null)
                {
                    return -perp * (cornerNudgeSpeed * Time.fixedDeltaTime);
                }
            }

            return Vector2.zero;
        }

        /// <summary>
        /// Finds interactable colliders within range and selects the one the player is facing; falls
        /// back to the closest one if none is faced directly. Only selects colliders that actually
        /// implement <see cref="IInteractable"/> (or legacy <see cref="Stations.StationView"/>),
        /// ignoring any non-interactive colliders that may share the interactable layer (such as TaskLocationMarker).
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
                // Filter: only consider colliders that have an IInteractable (Entities or Core) or StationView component
                bool isEntity = hit.TryGetComponent(out IInteractable _) || hit.GetComponentInParent<IInteractable>() != null;
                bool isCore = hit.TryGetComponent(out TheLastSignal.Core.Interaction.IInteractable _) || hit.GetComponentInParent<TheLastSignal.Core.Interaction.IInteractable>() != null;
                bool isStation = hit.TryGetComponent(out Stations.StationView _) || hit.GetComponentInParent<Stations.StationView>() != null;

                if (!isEntity && !isCore && !isStation)
                {
                    continue;
                }

                Vector2 closestPoint = hit.ClosestPoint(rb.position);
                Vector2 toHit = closestPoint - rb.position;
                float sqrDistance = toHit.sqrMagnitude;

                if (sqrDistance < closestSqrDistance)
                {
                    closestSqrDistance = sqrDistance;
                    closestHit = hit;
                }

                // If player is directly inside or overlapping the collider (e.g. passing through open door), treat as faced
                if (sqrDistance <= 0.04f)
                {
                    facedHit = hit;
                }
                else
                {
                    float dot = Vector2.Dot(toHit.normalized, facingDirection);
                    if (dot > facedBestDot)
                    {
                        facedBestDot = dot;
                        facedHit = hit;
                    }
                }
            }

            // Only allow interaction if faced directly, or if immediately adjacent/overlapping
            CurrentInteractable = facedHit != null ? facedHit : (closestSqrDistance <= 0.18f ? closestHit : null);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, interactionRange);

            if (enableCornerAssist)
            {
                Vector2 pos = Application.isPlaying && rb != null ? rb.position : (Vector2)transform.position;
                Vector2 dir = facingDirection.sqrMagnitude > 0.001f ? facingDirection : Vector2.down;
                Vector2 perp = new Vector2(-dir.y, dir.x);

                Gizmos.color = Color.cyan;
                Gizmos.DrawRay(pos - perp * cornerSideOffset, dir * cornerRayDistance);
                Gizmos.DrawRay(pos + perp * cornerSideOffset, dir * cornerRayDistance);
            }
        }
    }
}
