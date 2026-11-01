using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using TheLastSignal.Entities.Camera;
using TheLastSignal.Core;
using TheLastSignal.Core.Interaction;
using TheLastSignal.Entities.Environment;

namespace TheLastSignal.Entities.Player
{
    /// <summary>
    /// Player controller supporting both single-player and multiplayer:
    /// - Local owner reads input, applies 8-directional physics movement, and detects interactables (Client-Side Prediction).
    /// - Continuous NetworkTransform and NetworkVariables synchronize position, movement state, and facing direction across all peers.
    /// - Remote instances run as kinematic bodies with smooth interpolation, avoiding physics fighting.
    /// - Authoritative interactions (doors, consoles) are routed via ServerRpc and replicated down to all clients.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(CapsuleCollider2D))]
    public class PlayerController : NetworkBehaviour
    {
        private static readonly Color[] SlotColors = new Color[]
        {
            new Color(0.2f, 0.8f, 1.0f, 1f),  // P1 Cyan
            new Color(1.0f, 0.55f, 0.2f, 1f), // P2 Orange
            new Color(0.35f, 0.9f, 0.45f, 1f),// P3 Green
            new Color(0.9f, 0.4f, 0.9f, 1f)   // P4 Magenta
        };

        /// <summary>
        /// True when an NGO session is actually running. Used so every ownership check degrades
        /// cleanly to single-player behaviour in the Epic 1 scene, which has no NetworkManager.
        /// </summary>
        private static bool IsNetworkSessionActive =>
            NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;

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
        [SerializeField] private float interactionRange = 1.4f;
        [SerializeField] private LayerMask interactableLayer;
        [SerializeField, Range(0f, 1f)] private float facingDotThreshold = 0.2f;

        private Rigidbody2D rb;
        private CapsuleCollider2D capsuleCollider;

        private Vector2 moveInput;
        private Vector2 currentVelocity;
        private Vector2 facingDirection = Vector2.down;
        private PlayerDirection facing = PlayerDirection.Down;
        private bool controlEnabled = false;
        private int slotIndex;
        private Vector3 lastRemotePosition;
        private bool isRemoteMoving;
        [SerializeField] private bool isMonster = false;
        private float speedMultiplier = 1.0f;

        public bool IsMonster
        {
            get => isMonster || CompareTag("Monster") || slotIndex == 3;
            set => isMonster = value;
        }

        public float SpeedMultiplier
        {
            get => speedMultiplier;
            set => speedMultiplier = Mathf.Clamp(value, 0.1f, 3.0f);
        }

        // Network synchronization variables (Owner writable, replicated to all peers)
        private readonly NetworkVariable<Vector2> netFacingDirection = new NetworkVariable<Vector2>(
            Vector2.down, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        private readonly NetworkVariable<bool> netIsMoving = new NetworkVariable<bool>(
            false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        private readonly NetworkVariable<PlayerDirection> netFacing = new NetworkVariable<PlayerDirection>(
            PlayerDirection.Down, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        /// <summary>Raw (already clamped to length 1) movement input for this frame.</summary>
        public Vector2 MoveInput => moveInput;

        /// <summary>Current actual velocity after acceleration and deceleration smoothing.</summary>
        public Vector2 CurrentVelocity => currentVelocity;

        /// <summary>The last non-zero movement direction; persists while idle so the player keeps facing it.</summary>
        public Vector2 FacingDirection => (IsSpawned && !IsOwner) ? netFacingDirection.Value : facingDirection;

        /// <summary>
        /// The last non-zero movement direction collapsed to one of 4 cardinal directions. Persists while idle.
        /// </summary>
        public PlayerDirection Facing => (IsSpawned && !IsOwner) ? netFacing.Value : facing;

        /// <summary>True while the player has non-zero movement input or is decelerating to a stop.</summary>
        public bool IsMoving => (IsSpawned && !IsOwner)
            ? (netIsMoving.Value || isRemoteMoving)
            : (currentVelocity.sqrMagnitude > 0.01f || moveInput.sqrMagnitude > 0.0001f);

        /// <summary>Spawn slot this instance occupies (0 = P1 … 3 = P4).</summary>
        public int SlotIndex => slotIndex;

        public float MoveSpeed => moveSpeed;

        public float InteractionRange => interactionRange;

        public bool ControlEnabled => controlEnabled;

        /// <summary>
        /// The interactable collider the player should be able to interact with right now, or null if none is in range.
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

            EnsureSpriteRenderer();
        }

        private void EnsureSpriteRenderer()
        {
            if (TryGetComponent(out SpriteRenderer sr) && sr.sprite == null)
            {
                if (TryGetComponent(out PlayerAnimator anim) && anim.IdleSouth != null)
                {
                    sr.sprite = anim.IdleSouth;
                    return;
                }
                Texture2D tex = Texture2D.whiteTexture;
                Sprite placeholder = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 16f);
                placeholder.name = "Placeholder_Square";
                sr.sprite = placeholder;
            }
        }

        private void Start()
        {
            // When NGO is running, ownership is not known yet at Start — OnNetworkSpawn binds the camera instead.
            if (IsNetworkSessionActive)
            {
                return;
            }

            BindLocalCamera();
        }

        /// <summary>
        /// Initializes player slot, visuals, physics authority, and network synchronization.
        /// </summary>
        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            int slot = (int)(OwnerClientId % (ulong)PlayerSpawnPoint.MaxSlots);
            BindSlot(slot);

            if (!IsOwner)
            {
                // Remote proxy: switch to Kinematic so local physics simulation doesn't fight NetworkTransform
                if (rb != null)
                {
                    rb.bodyType = RigidbodyType2D.Kinematic;
                    rb.linearVelocity = Vector2.zero;
                }

                // Place initial position at spawn point to prevent jump from default prefab position
                Vector2 initialSpawnPos = ResolveNetworkSpawnPosition(slotIndex);
                transform.position = new Vector3(initialSpawnPos.x, initialSpawnPos.y, transform.position.z);
                lastRemotePosition = transform.position;
                return;
            }

            // Local owner: place body on own spawn slot and enable controls
            ResetState(ResolveNetworkSpawnPosition(slotIndex));
            SetControlEnabled(true);

            // Story 3.5: doors and the reactor console are now Host-authoritative via their own
            // NetworkVariables (DoorController, NetworkGameState) — those replicate automatically to
            // newly-spawned clients as part of NGO's own spawn payload, so no manual per-player sync is
            // needed here anymore (this used to call SyncAllDoorsToClient).
        }

        /// <summary>
        /// Finds this slot's spawn point in the loaded scene. Supports both PlayerSpawnPoint and legacy SpawnPoint.
        /// </summary>
        private Vector2 ResolveNetworkSpawnPosition(int slot)
        {
            PlayerSpawnPoint[] points = FindObjectsByType<PlayerSpawnPoint>(FindObjectsInactive.Exclude);
            for (int i = 0; i < points.Length; i++)
            {
                if (points[i] != null && points[i].SlotIndex == slot)
                {
                    return points[i].Position;
                }
            }

            SpawnPoint[] legacyPoints = FindObjectsByType<SpawnPoint>(FindObjectsInactive.Exclude);
            for (int i = 0; i < legacyPoints.Length; i++)
            {
                if (legacyPoints[i] != null && legacyPoints[i].PlayerIndex == slot)
                {
                    return legacyPoints[i].Position;
                }
            }

            return transform.position;
        }

        /// <summary>Assigns this instance to a spawn slot so co-op can address P1–P4.</summary>
        public void BindSlot(int slot)
        {
            slotIndex = Mathf.Clamp(slot, 0, PlayerSpawnPoint.MaxSlots - 1);
            gameObject.name = slotIndex == 0 ? "Player" : $"Player_P{slotIndex + 1}";
            ApplySlotVisuals(slotIndex);
        }

        public void ApplySlotVisuals(int slot)
        {
            if (TryGetComponent(out SpriteRenderer sr))
            {
                bool isPlaceholder = sr.sprite == null
                    || sr.sprite.name.Contains("Placeholder")
                    || (sr.sprite.texture != null && sr.sprite.texture.width <= 16);

                if (isPlaceholder)
                {
                    int colorIndex = Mathf.Clamp(slot, 0, SlotColors.Length - 1);
                    sr.color = SlotColors[colorIndex];
                }
                else
                {
                    sr.color = Color.white;
                }
            }
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
        /// Teleports the player to <paramref name="worldPosition"/> and clears movement / interactable state.
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
            lastRemotePosition = transform.position;

            BindLocalCamera();
        }

        private void BindLocalCamera()
        {
            if (IsSpawned)
            {
                if (!IsOwner)
                {
                    return;
                }
            }
            else if (slotIndex != 0)
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
            if (IsSpawned && !IsOwner)
            {
                // Remote proxy: derive displacement speed for smooth animation fallback
                Vector3 currentPos = transform.position;
                float dt = Time.deltaTime > 0f ? Time.deltaTime : 0.02f;
                float speedSqr = (currentPos - lastRemotePosition).sqrMagnitude / (dt * dt);
                isRemoteMoving = speedSqr > 0.01f;
                lastRemotePosition = currentPos;
                return;
            }

            if (!controlEnabled)
            {
                moveInput = Vector2.zero;
                CurrentInteractable = null;
                return;
            }

            ReadMovementInput();
            UpdateInteractionDetection();
            HandleInteractionInput();

            // Replicate movement state for other clients to read
            if (IsSpawned && IsOwner)
            {
                bool moving = IsMoving;
                if (netIsMoving.Value != moving)
                {
                    netIsMoving.Value = moving;
                }

                if ((netFacingDirection.Value - facingDirection).sqrMagnitude > 0.001f)
                {
                    netFacingDirection.Value = facingDirection;
                    netFacing.Value = facing;
                }
            }
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
        /// Attempts to interact with <see cref="CurrentInteractable"/>.
        /// Story 3.5: Door and ReactorCoreConsole are Host-authoritative — Door routes through its own
        /// NetworkVariable-backed ServerRpc internally (see DoorController.Interact), and
        /// ReactorCoreConsole is special-cased here to route through NetworkGameState's ServerRpc, since
        /// its authoritative bool lives there (a console can't itself be a NetworkBehaviour — see
        /// ReactorCoreConsole.ApplyNetworkActivation). Everything else (TaskLocation, StationView, ...)
        /// still resolves purely locally per peer.
        /// // TODO: Task Ticket System synchronization not yet implemented.
        /// </summary>
        public void TriggerInteraction()
        {
            if (CurrentInteractable == null)
            {
                return;
            }

            if (CurrentInteractable.TryGetComponent(out ReactorCoreConsole reactor) ||
                CurrentInteractable.GetComponentInParent<ReactorCoreConsole>() is { } parentReactor && (reactor = parentReactor) != null)
            {
                if (!reactor.CanInteract(this))
                {
                    return;
                }

                if (IsSpawned && NetworkGameState.Instance != null)
                {
                    NetworkGameState.Instance.RequestActivateReactorRpc();
                }
                else
                {
                    reactor.Interact(this);
                }
                return;
            }

            // Unified IInteractable (Door [self-networked via DoorController], TaskLocation, StationView, InteractableBase)
            if (CurrentInteractable.TryGetComponent(out IInteractable interactable) ||
                CurrentInteractable.GetComponentInParent<IInteractable>() is { } parentInteractable && (interactable = parentInteractable) != null)
            {
                if (interactable.CanInteract(this))
                {
                    interactable.Interact(this);
                }
                return;
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
        /// Reads raw keyboard state via the new Input System.
        /// </summary>
        private void ReadMovementInput()
        {
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

            moveInput = Vector2.ClampMagnitude(new Vector2(x, y), 1f);

            if (moveInput.sqrMagnitude > 0.0001f)
            {
                facingDirection = moveInput.normalized;
                facing = ResolveCardinalDirection(moveInput);
            }
        }

        private static PlayerDirection ResolveCardinalDirection(Vector2 input)
        {
            if (Mathf.Abs(input.x) >= Mathf.Abs(input.y))
            {
                return input.x >= 0f ? PlayerDirection.Right : PlayerDirection.Left;
            }

            return input.y >= 0f ? PlayerDirection.Up : PlayerDirection.Down;
        }

        private void ApplyMovement(Vector2 direction)
        {
            Vector2 targetVelocity = direction * (moveSpeed * speedMultiplier);
            float rate = direction.sqrMagnitude > 0.0001f ? acceleration : deceleration;
            currentVelocity = Vector2.MoveTowards(currentVelocity, targetVelocity, rate * Time.fixedDeltaTime);

            Vector2 moveDelta = currentVelocity * Time.fixedDeltaTime;

            if (enableCornerAssist && currentVelocity.sqrMagnitude > 0.01f)
            {
                Vector2 nudge = CalculateCornerNudge(currentVelocity.normalized);
                if (nudge != Vector2.zero)
                {
                    float originalMagnitude = moveDelta.magnitude;
                    moveDelta = (moveDelta + nudge).normalized * originalMagnitude;
                }
            }

            Vector2 targetPosition = rb.position + moveDelta;
            rb.MovePosition(targetPosition);
        }

        private readonly RaycastHit2D[] cornerRaycastBuffer = new RaycastHit2D[8];

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

        private Vector2 CalculateCornerNudge(Vector2 moveDir)
        {
            Vector2 pos = rb.position;
            Vector2 perp = new Vector2(-moveDir.y, moveDir.x);

            RaycastHit2D leftHit = RaycastSolid(pos - perp * cornerSideOffset, moveDir, cornerRayDistance, collisionLayers);
            RaycastHit2D rightHit = RaycastSolid(pos + perp * cornerSideOffset, moveDir, cornerRayDistance, collisionLayers);

            if (leftHit.collider != null && rightHit.collider != null)
            {
                return Vector2.zero;
            }

            if (leftHit.collider != null && rightHit.collider == null)
            {
                if (RaycastSolid(pos, perp, cornerSideOffset + 0.1f, collisionLayers).collider == null)
                {
                    return perp * (cornerNudgeSpeed * Time.fixedDeltaTime);
                }
            }
            else if (rightHit.collider != null && leftHit.collider == null)
            {
                if (RaycastSolid(pos, -perp, cornerSideOffset + 0.1f, collisionLayers).collider == null)
                {
                    return -perp * (cornerNudgeSpeed * Time.fixedDeltaTime);
                }
            }

            return Vector2.zero;
        }

        private void UpdateInteractionDetection()
        {
            int extraMask = LayerMask.GetMask("Interactable", "Default", "Obstacle", "Obstacles");
            int effectiveLayerMask = interactableLayer.value != 0 ? (interactableLayer.value | extraMask) : extraMask;
            Vector2 playerPos = rb != null ? rb.position : (Vector2)transform.position;
            Collider2D[] hits = Physics2D.OverlapCircleAll(playerPos, interactionRange, effectiveLayerMask);

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
                bool isInteractable = hit.TryGetComponent(out IInteractable _) || hit.GetComponentInParent<IInteractable>() != null;
                bool isDoor = hit.TryGetComponent(out DoorController _) || hit.GetComponentInParent<DoorController>() != null;

                if (!isInteractable && !isDoor)
                {
                    continue;
                }

                Vector2 closestPoint = hit.ClosestPoint(playerPos);
                Vector2 toHit = closestPoint - playerPos;
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

            // Prefer object faced directly; otherwise pick closest interactable within range
            CurrentInteractable = facedHit != null ? facedHit : (closestSqrDistance <= (interactionRange * interactionRange) ? closestHit : null);
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
