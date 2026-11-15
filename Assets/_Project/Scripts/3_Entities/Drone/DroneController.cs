using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using TheLastSignal.Core;
using TheLastSignal.Entities.Camera;
using TheLastSignal.Entities.Player;
using TheLastSignal.Entities.Stations;
using TheLastSignal.Systems.Lighting;

namespace TheLastSignal.Entities.Drone
{
    public struct RadarPingData
    {
        public Vector2 WorldPosition;
        public Vector2 RelativeOffset;
        public float Distance;
        public bool IsHostile;
        public string EntityName;
    }

    /// <summary>
    /// Recon Drone Controller (FR-SUR-06, BR-17, F-07).
    /// Steered exclusively by the Drone Operator from the Airlock Console terminal.
    /// Features:
    /// - 8-directional smooth flight vector.
    /// - Mouse-steered directional spotlight cone integrated with GameplayLightSource & Light2D.
    /// - Active motion radar scanning for The Shade (Hunter) and friendly Survivors.
    /// - Drone battery consumption and emergency auto-recall at 0%.
    /// - Pilot vulnerability monitoring: auto-aborts feed if operator body is attacked.
    /// - Replicated over Netcode for GameObjects (NGO) with single-player offline fallback.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class DroneController : NetworkBehaviour
    {
        private static bool IsNetworkSessionActive =>
            NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;

        public static DroneController Instance { get; private set; }

        [Header("Flight Parameters")]
        [SerializeField] private float flightSpeed = 4.8f;
        [SerializeField] private float acceleration = 18f;
        [SerializeField] private float deceleration = 24f;

        [Header("Spotlight Configuration")]
        [SerializeField] private float spotlightRange = 8.0f;
        [SerializeField] private float spotlightAngle = 45f;
        [SerializeField] private Transform spotlightPivot;
        [SerializeField] private GameplayLightSource gameplayLightSource;

        [Header("Battery & Radar Tunables")]
        [SerializeField] private float maxBattery = 100f;
        [SerializeField] private float batteryDrainRate = 4.0f;     // ~25 seconds of continuous flight
        [SerializeField] private float batteryRechargeRate = 12.0f;  // ~8.3 seconds full recharge while docked
        [SerializeField] private float radarRadius = 12.0f;
        [SerializeField] private float radarPingInterval = 1.5f;

        [Header("Collision Layers")]
        [SerializeField] private LayerMask obstacleLayers;

        // Replicated Netcode state (Server authoring, everyone reading)
        private readonly NetworkVariable<Vector2> netPosition = new NetworkVariable<Vector2>(
            Vector2.zero, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<float> netAimAngle = new NetworkVariable<float>(
            0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<bool> netIsActive = new NetworkVariable<bool>(
            false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<float> netBattery = new NetworkVariable<float>(
            100f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        // Local state
        private Rigidbody2D rb;
        private CircleCollider2D circleCollider;
        private PlayerController currentOperator;
        private PlayerHealth operatorHealth;
        private Vector2 moveInput;
        private Vector2 currentVelocity;
        private float aimAngleDegrees;
        private float currentBattery = 100f;
        private bool isFlightActive = false;
        private float radarTimer = 0f;
        private readonly List<RadarPingData> detectedEntities = new List<RadarPingData>();

        // Offline / client prediction tracking
        private Vector2 dockAnchorPosition;
        private Vector2 pilotAnchorPosition;

        // Public accessors
        public bool IsFlightActive => IsNetworkSessionActive && IsSpawned ? netIsActive.Value : isFlightActive;
        public float CurrentBattery => IsNetworkSessionActive && IsSpawned ? netBattery.Value : currentBattery;
        public float MaxBattery => maxBattery;
        public float BatteryPercent => maxBattery > 0.001f ? Mathf.Clamp01(CurrentBattery / maxBattery) : 0f;
        public float RadarRadius => radarRadius;
        public float AimAngle => IsNetworkSessionActive && IsSpawned ? netAimAngle.Value : aimAngleDegrees;
        public PlayerController CurrentOperator => currentOperator;
        public IReadOnlyList<RadarPingData> DetectedEntities => detectedEntities;

        // Events
        public event Action<float> OnBatteryChanged;
        public event Action<bool> OnFlightStateChanged;
        public event Action<float> OnAimChanged;
        public event Action<IReadOnlyList<RadarPingData>> OnRadarSweep;
        public event Action<string> OnEmergencyDisengage;

        private void Awake()
        {
            Instance = this;
            rb = GetComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.constraints = RigidbodyConstraints2D.FreezeAll; // Parked firmly on the map while unpiloted
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;

            circleCollider = GetComponent<CircleCollider2D>();
            if (circleCollider == null)
            {
                circleCollider = gameObject.AddComponent<CircleCollider2D>();
                circleCollider.radius = 0.35f;
            }

            PhysicsMaterial2D zeroFriction = new PhysicsMaterial2D("Drone_ZeroFriction")
            {
                friction = 0f,
                bounciness = 0f
            };
            circleCollider.sharedMaterial = zeroFriction;

            if (obstacleLayers.value == 0)
            {
                int obstacleLayer = LayerMask.NameToLayer("Obstacle");
                if (obstacleLayer >= 0) obstacleLayers |= (1 << obstacleLayer);
                int defaultLayer = LayerMask.NameToLayer("Default");
                if (defaultLayer >= 0) obstacleLayers |= (1 << defaultLayer);
            }

            dockAnchorPosition = transform.position;
            ConfigureSpotlightSource();
        }

        private void ConfigureSpotlightSource()
        {
            if (spotlightPivot == null)
            {
                Transform foundPivot = transform.Find("SpotlightPivot");
                if (foundPivot != null)
                {
                    spotlightPivot = foundPivot;
                }
                else
                {
                    GameObject pivotGo = new GameObject("SpotlightPivot");
                    pivotGo.transform.SetParent(transform, false);
                    spotlightPivot = pivotGo.transform;
                }
            }

            if (gameplayLightSource == null)
            {
                gameplayLightSource = spotlightPivot.GetComponentInChildren<GameplayLightSource>();
                if (gameplayLightSource == null)
                {
                    GameObject lightGo = new GameObject("GameplayLightSource_DroneCone");
                    lightGo.transform.SetParent(spotlightPivot, false);
                    gameplayLightSource = lightGo.AddComponent<GameplayLightSource>();
                }
            }

            if (gameplayLightSource != null)
            {
                gameplayLightSource.ConfigureCone(spotlightRange, spotlightAngle);
                gameplayLightSource.ServerSetEnabled(false);
            }
        }

        private void Start()
        {
            IgnoreAllPlayerCollisions();
        }

        /// <summary>
        /// Prevents the drone from physically colliding with or pushing player bodies,
        /// and allows smooth landing on the physical dock station.
        /// </summary>
        public void IgnoreAllPlayerCollisions()
        {
            if (circleCollider == null) circleCollider = GetComponent<CircleCollider2D>();
            if (circleCollider == null) return;

            PlayerController[] players = FindObjectsByType<PlayerController>(FindObjectsInactive.Include);
            for (int i = 0; i < players.Length; i++)
            {
                if (players[i] != null && players[i].TryGetComponent(out Collider2D col))
                {
                    Physics2D.IgnoreCollision(circleCollider, col, true);
                }
            }

            // Ignore collision with dock station pad so drone can rest/land on it cleanly
            DroneDockStation dock = FindAnyObjectByType<DroneDockStation>();
            if (dock != null && dock.TryGetComponent(out Collider2D dockCol))
            {
                Physics2D.IgnoreCollision(circleCollider, dockCol, true);
            }
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            netPosition.OnValueChanged += (prev, curr) =>
            {
                if (!IsOwner) transform.position = curr;
            };
            netAimAngle.OnValueChanged += (prev, curr) =>
            {
                if (!IsOwner)
                {
                    aimAngleDegrees = curr;
                    UpdateSpotlightRotation(curr);
                    OnAimChanged?.Invoke(curr);
                }
            };
            netIsActive.OnValueChanged += (prev, curr) =>
            {
                isFlightActive = curr;
                OnFlightStateChanged?.Invoke(curr);
                if (gameplayLightSource != null && IsServer)
                {
                    gameplayLightSource.ServerSetEnabled(curr);
                }
            };
            netBattery.OnValueChanged += (prev, curr) =>
            {
                currentBattery = curr;
                OnBatteryChanged?.Invoke(curr);
            };

            if (IsServer)
            {
                netBattery.Value = maxBattery;
                netIsActive.Value = false;
                netPosition.Value = transform.position;
            }
        }

        public void SetDockPosition(Vector2 position)
        {
            dockAnchorPosition = position;
        }

        /// <summary>
        /// Applies role tunables from RoleConfigSO if available.
        /// </summary>
        public void ApplyRoleConfig(RoleData data)
        {
            if (data == null) return;
            if (data.droneBatteryCapacity > 0f) maxBattery = data.droneBatteryCapacity;
            if (data.droneBatteryDrainRate > 0f) batteryDrainRate = data.droneBatteryDrainRate;
            if (data.droneBatteryRechargeRate > 0f) batteryRechargeRate = data.droneBatteryRechargeRate;
            if (data.droneMoveSpeed > 0f) flightSpeed = data.droneMoveSpeed;
            if (data.droneSpotlightRange > 0f) spotlightRange = data.droneSpotlightRange;
            if (data.droneSpotlightAngle > 0f) spotlightAngle = data.droneSpotlightAngle;
            if (data.droneRadarRadius > 0f) radarRadius = data.droneRadarRadius;
            if (data.droneRadarPingInterval > 0f) radarPingInterval = data.droneRadarPingInterval;

            if (gameplayLightSource != null)
            {
                gameplayLightSource.ConfigureCone(spotlightRange, spotlightAngle);
            }
        }

        /// <summary>
        /// Engages drone flight feed for the given operator.
        /// Activates the drone at its current persistent map position (like real-life drone).
        /// </summary>
        public void EngageFlight(PlayerController pilot, Vector2? initialLaunchPos = null)
        {
            if (pilot == null) return;

            currentOperator = pilot;
            pilotAnchorPosition = pilot.transform.position;
            IgnoreAllPlayerCollisions();

            // Retain the drone's current physical position on the map
            Vector2 activePos = (Vector2)transform.position;
            if (activePos == Vector2.zero && initialLaunchPos.HasValue)
            {
                activePos = initialLaunchPos.Value;
                transform.position = activePos;
            }

            if (rb != null)
            {
                rb.position = activePos;
                rb.linearVelocity = Vector2.zero;
                rb.constraints = RigidbodyConstraints2D.FreezeRotation; // Unlock position for flight
            }

            isFlightActive = true;
            currentVelocity = Vector2.zero;

            // Lock the pilot's physical body in place (vulnerable at the terminal)
            currentOperator.SetControlEnabled(false);

            // Subscribe to pilot damage & downed events for emergency disengage
            if (currentOperator.TryGetComponent(out operatorHealth))
            {
                operatorHealth.OnLifeStateChanged += HandleOperatorLifeStateChanged;
                operatorHealth.OnHealthChanged += HandleOperatorHealthChanged;
            }

            // Bind Camera to the Drone
            UnityEngine.Camera mainCam = UnityEngine.Camera.main;
            if (mainCam != null && mainCam.TryGetComponent(out CameraFollow cameraFollow))
            {
                cameraFollow.SetTarget(transform);
            }

            if (gameplayLightSource != null)
            {
                gameplayLightSource.ServerSetEnabled(true);
            }

            if (IsNetworkSessionActive)
            {
                if (IsServer)
                {
                    netIsActive.Value = true;
                    netPosition.Value = activePos;
                }
                else
                {
                    RequestEngageFlightRpc(activePos);
                }
            }

            OnFlightStateChanged?.Invoke(true);
            Debug.Log($"<color=#00FFCC>[DroneController]</color> Operator {pilot.name} engaged recon drone flight at persistent position {activePos}.");
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void RequestEngageFlightRpc(Vector2 launchPosition)
        {
            isFlightActive = true;
            netIsActive.Value = true;
            netPosition.Value = launchPosition;
            if (gameplayLightSource != null) gameplayLightSource.ServerSetEnabled(true);
        }

        /// <summary>
        /// Disengages drone flight feed, parking the drone in-place at its current location.
        /// </summary>
        public void DisengageFlight(string reason = "Operator Disengaged")
        {
            if (!isFlightActive && !netIsActive.Value) return;

            isFlightActive = false;
            currentVelocity = Vector2.zero;
            Vector2 landedPos = transform.position;

            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
                rb.position = landedPos;
                rb.constraints = RigidbodyConstraints2D.FreezeAll; // Freeze firmly in place where it powered down
            }

            // Unsubscribe from operator health events
            if (operatorHealth != null)
            {
                operatorHealth.OnLifeStateChanged -= HandleOperatorLifeStateChanged;
                operatorHealth.OnHealthChanged -= HandleOperatorHealthChanged;
                operatorHealth = null;
            }

            // Restore pilot body movement
            if (currentOperator != null)
            {
                currentOperator.transform.position = pilotAnchorPosition;
                if (currentOperator.TryGetComponent(out Rigidbody2D pilotRb))
                {
                    pilotRb.position = pilotAnchorPosition;
                    pilotRb.linearVelocity = Vector2.zero;
                }
                currentOperator.SetControlEnabled(true);

                // Restore Camera back to the operator
                UnityEngine.Camera mainCam = UnityEngine.Camera.main;
                if (mainCam != null && mainCam.TryGetComponent(out CameraFollow cameraFollow))
                {
                    cameraFollow.SetTarget(currentOperator.transform);
                }
            }

            // DO NOT TELEPORT BACK TO DOCK! The drone stays right where it is on the map!
            if (gameplayLightSource != null)
            {
                gameplayLightSource.ServerSetEnabled(false);
            }

            if (IsNetworkSessionActive)
            {
                if (IsServer)
                {
                    netIsActive.Value = false;
                    netPosition.Value = landedPos;
                }
                else
                {
                    RequestDisengageFlightRpc(landedPos);
                }
            }

            OnFlightStateChanged?.Invoke(false);
            OnEmergencyDisengage?.Invoke(reason);
            currentOperator = null;

            Debug.Log($"<color=#FFAA00>[DroneController]</color> Recon drone powered down and parked at {landedPos}: {reason}");
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void RequestDisengageFlightRpc(Vector2 landedPos)
        {
            isFlightActive = false;
            netIsActive.Value = false;
            netPosition.Value = landedPos;
            if (gameplayLightSource != null) gameplayLightSource.ServerSetEnabled(false);
        }

        private void HandleOperatorLifeStateChanged(PlayerLifeState newState)
        {
            if (newState == PlayerLifeState.Downed || newState == PlayerLifeState.Dead)
            {
                DisengageFlight("PILOT INCAPACITATED!");
            }
        }

        private void HandleOperatorHealthChanged(float current, float max)
        {
            if (current < max)
            {
                // Operator is taking damage while at the console!
                DisengageFlight("WARNING: OPERATOR BODY UNDER ATTACK!");
            }
        }

        private void Update()
        {
            bool isLocallyControlled = isFlightActive && (currentOperator != null && (!IsNetworkSessionActive || currentOperator.IsOwner));

            if (isLocallyControlled)
            {
                ReadFlightInput();
                UpdateSpotlightAim();
                HandleFlightHotkeys();
                UpdateBatteryDrain(Time.deltaTime);
                UpdateRadarSweep(Time.deltaTime);
                SyncNetworkState();
            }
            else if (!IsFlightActive)
            {
                // Recharging at dock
                UpdateBatteryRecharge(Time.deltaTime);
            }
        }

        private void FixedUpdate()
        {
            if (isFlightActive && currentOperator != null)
            {
                currentOperator.transform.position = pilotAnchorPosition;
                if (currentOperator.TryGetComponent(out Rigidbody2D pilotRb))
                {
                    pilotRb.position = pilotAnchorPosition;
                    pilotRb.linearVelocity = Vector2.zero;
                }
            }

            bool isLocallyControlled = isFlightActive && (currentOperator != null && (!IsNetworkSessionActive || currentOperator.IsOwner));
            if (!isLocallyControlled) return;

            ApplyFlightPhysics(moveInput);
        }

        private void ReadFlightInput()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;

            float x = 0f;
            float y = 0f;

            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) y += 1f;
            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) y -= 1f;
            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) x -= 1f;
            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) x += 1f;

            moveInput = Vector2.ClampMagnitude(new Vector2(x, y), 1f);
        }

        private void ApplyFlightPhysics(Vector2 direction)
        {
            Vector2 targetVelocity = direction * flightSpeed;
            float rate = direction.sqrMagnitude > 0.0001f ? acceleration : deceleration;
            currentVelocity = Vector2.MoveTowards(currentVelocity, targetVelocity, rate * Time.fixedDeltaTime);

            Vector2 newPos = rb.position + (currentVelocity * Time.fixedDeltaTime);
            rb.MovePosition(newPos);
        }

        private void UpdateSpotlightAim()
        {
            Mouse mouse = Mouse.current;
            UnityEngine.Camera cam = UnityEngine.Camera.main;

            if (mouse != null && cam != null)
            {
                Vector2 mouseScreen = mouse.position.ReadValue();
                Vector3 mouseWorld = cam.ScreenToWorldPoint(new Vector3(mouseScreen.x, mouseScreen.y, 0f));
                Vector2 dir = (Vector2)mouseWorld - (Vector2)transform.position;
                if (dir.sqrMagnitude > 0.01f)
                {
                    aimAngleDegrees = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                }
            }
            else if (moveInput.sqrMagnitude > 0.01f)
            {
                aimAngleDegrees = Mathf.Atan2(moveInput.y, moveInput.x) * Mathf.Rad2Deg;
            }

            UpdateSpotlightRotation(aimAngleDegrees);
            OnAimChanged?.Invoke(aimAngleDegrees);

            if (gameplayLightSource != null)
            {
                gameplayLightSource.ServerSetAimAngle(aimAngleDegrees);
            }
        }

        private void UpdateSpotlightRotation(float angle)
        {
            if (spotlightPivot != null)
            {
                spotlightPivot.rotation = Quaternion.Euler(0f, 0f, angle);
            }
        }

        private void HandleFlightHotkeys()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;

            // [ESC] disengages drone feed
            if (keyboard.escapeKey.wasPressedThisFrame)
            {
                DisengageFlight("Operator Terminated Connection");
            }
        }

        private void UpdateBatteryDrain(float dt)
        {
            currentBattery = Mathf.Max(0f, currentBattery - (batteryDrainRate * dt));
            OnBatteryChanged?.Invoke(currentBattery);

            if (currentBattery <= 0f)
            {
                DisengageFlight("BATTERY DEPLETED // AUTO-RECALL TRIGGERED");
            }
        }

        private void UpdateBatteryRecharge(float dt)
        {
            if (currentBattery < maxBattery)
            {
                currentBattery = Mathf.Min(maxBattery, currentBattery + (batteryRechargeRate * dt));
                OnBatteryChanged?.Invoke(currentBattery);
            }
        }

        private void UpdateRadarSweep(float dt)
        {
            radarTimer += dt;
            if (radarTimer < radarPingInterval) return;
            radarTimer = 0f;

            ExecuteRadarSweep();
        }

        public void ExecuteRadarSweep()
        {
            detectedEntities.Clear();
            Vector2 dronePos = transform.position;

            // 1. Check all registered player health entries
            var activePlayers = PlayerHealth.ActivePlayers;
            for (int i = 0; i < activePlayers.Count; i++)
            {
                PlayerHealth pHealth = activePlayers[i];
                if (pHealth == null || !pHealth.IsAlive) continue;

                if (pHealth.TryGetComponent(out PlayerController pCtrl))
                {
                    if (pCtrl == currentOperator) continue; // Don't ping self as remote target

                    Vector2 targetPos = pCtrl.transform.position;
                    float dist = Vector2.Distance(dronePos, targetPos);
                    if (dist <= radarRadius)
                    {
                        bool isHunter = pCtrl.IsMonster || pCtrl.SlotIndex == 3;
                        detectedEntities.Add(new RadarPingData
                        {
                            WorldPosition = targetPos,
                            RelativeOffset = targetPos - dronePos,
                            Distance = dist,
                            IsHostile = isHunter,
                            EntityName = isHunter ? "THE SHADE [THREAT]" : $"SURVIVOR ({pCtrl.name})"
                        });
                    }
                }
            }

            // 2. Physics overlap for any standalone monsters or objects
            Collider2D[] colliders = Physics2D.OverlapCircleAll(dronePos, radarRadius);
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider2D col = colliders[i];
                if (col == null || col.gameObject == gameObject) continue;
                if (currentOperator != null && (col.gameObject == currentOperator.gameObject || col.transform.IsChildOf(currentOperator.transform))) continue;

                if (col.CompareTag("Monster"))
                {
                    Vector2 targetPos = col.transform.position;
                    float dist = Vector2.Distance(dronePos, targetPos);
                    bool alreadyListed = false;
                    for (int k = 0; k < detectedEntities.Count; k++)
                    {
                        if (Vector2.Distance(detectedEntities[k].WorldPosition, targetPos) < 0.5f)
                        {
                            alreadyListed = true;
                            break;
                        }
                    }

                    if (!alreadyListed)
                    {
                        detectedEntities.Add(new RadarPingData
                        {
                            WorldPosition = targetPos,
                            RelativeOffset = targetPos - dronePos,
                            Distance = dist,
                            IsHostile = true,
                            EntityName = "THE SHADE [THREAT]"
                        });
                    }
                }
            }

            OnRadarSweep?.Invoke(detectedEntities);
        }

        private void SyncNetworkState()
        {
            if (!IsNetworkSessionActive) return;

            if (IsServer)
            {
                netPosition.Value = transform.position;
                netAimAngle.Value = aimAngleDegrees;
                netBattery.Value = currentBattery;
            }
            else
            {
                RequestSyncDroneStateRpc(transform.position, aimAngleDegrees, currentBattery);
            }
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void RequestSyncDroneStateRpc(Vector2 pos, float aimAngle, float battery)
        {
            netPosition.Value = pos;
            netAimAngle.Value = aimAngle;
            netBattery.Value = battery;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, radarRadius);

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, spotlightRange);
        }
    }
}
