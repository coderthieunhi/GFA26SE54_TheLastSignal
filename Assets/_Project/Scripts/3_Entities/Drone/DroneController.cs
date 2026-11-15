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
using TheLastSignal.UI;

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
        [SerializeField] private float flightSpeed = 2.4f; // Reduced by 50% from 4.8f for tactical control
        [SerializeField] private float acceleration = 12f;
        [SerializeField] private float deceleration = 16f;

        [Header("Spotlight Configuration")]
        [SerializeField] private float spotlightRange = 8.0f;
        [SerializeField] private float spotlightAngle = 45f;
        [SerializeField] private Transform spotlightPivot;
        [SerializeField] private GameplayLightSource gameplayLightSource;

        [Header("Battery & Radar Tunables")]
        [SerializeField] private float maxBattery = 100f;
        [SerializeField] private float batteryDrainRate = 4.0f;     // ~25 seconds of continuous flight
        [SerializeField] private float batteryRechargeRate = 0.8333f; // ~120 seconds (2 minutes) full recharge while docked
        [SerializeField] private float radarRadius = 12.0f;
        [SerializeField] private float radarPingInterval = 1.5f;

        [Header("Collision Layers")]
        [SerializeField] private LayerMask obstacleLayers;

        [Header("Dock Settings")]
        [SerializeField] private float dockRechargeRadius = 1.6f;

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
        private bool isReturningToDock = false;
        private readonly List<Vector2> flightPathWaypoints = new List<Vector2>();
        private const float WAYPOINT_SPACING = 0.8f;
        private float rthStuckTimer = 0f;
        private Vector2 rthLastPosition;
        private const float RTH_CRUISE_SPEED = 3.2f;
        private float radarTimer = 0f;
        private readonly List<RadarPingData> detectedEntities = new List<RadarPingData>();
        private float previousOperatorHealth = -1f;
        private float networkSyncTimer = 0f;
        private const float NETWORK_SYNC_INTERVAL = 0.05f; // 20 Hz sync rate

        // Offline / client prediction tracking
        private Vector2 dockAnchorPosition;
        private Vector2 pilotAnchorPosition;

        // Public accessors
        public bool IsFlightActive => ((IsNetworkSessionActive && IsSpawned ? netIsActive.Value : isFlightActive) || isReturningToDock);
        public bool IsOperatorControlled => isFlightActive && currentOperator != null;
        public bool IsReturningToDock => isReturningToDock;
        public Vector2 DockPosition => dockAnchorPosition;
        public float CurrentBattery => IsNetworkSessionActive && IsSpawned ? netBattery.Value : currentBattery;
        public float MaxBattery => maxBattery;
        public float BatteryPercent => maxBattery > 0.001f ? Mathf.Clamp01(CurrentBattery / maxBattery) : 0f;
        public float BatteryRechargeRate => batteryRechargeRate;
        public float BatteryDrainRate => batteryDrainRate;
        public float RadarRadius => radarRadius;
        public float AimAngle => IsNetworkSessionActive && IsSpawned ? netAimAngle.Value : aimAngleDegrees;
        public PlayerController CurrentOperator => currentOperator;
        public IReadOnlyList<RadarPingData> DetectedEntities => detectedEntities;
        public bool IsAtDockStation => Vector2.Distance(transform.position, dockAnchorPosition) <= dockRechargeRadius;

        // Events
        public event Action<float> OnBatteryChanged;
        public event Action<bool> OnFlightStateChanged;
        public event Action<bool> OnAutoReturnStateChanged;
        public event Action<float> OnAimChanged;
        public event Action<IReadOnlyList<RadarPingData>> OnRadarSweep;
        public event Action<string> OnEmergencyDisengage;

        private void Awake()
        {
            Instance = this;

            // Reduce drone size by 1/4 (25% reduction -> 0.75x scale) for tight corridor navigation
            transform.localScale = new Vector3(0.75f, 0.75f, 1f);

            rb = GetComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.constraints = RigidbodyConstraints2D.FreezeAll; // Parked firmly on the map while unpiloted
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;

            circleCollider = GetComponent<CircleCollider2D>();
            if (circleCollider == null)
            {
                circleCollider = gameObject.AddComponent<CircleCollider2D>();
            }
            circleCollider.radius = 0.25f; // Proportional 0.25m navigation radius (reduced by 1/4)

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
                bool isLocalDriver = isFlightActive && currentOperator != null && (!IsNetworkSessionActive || currentOperator.IsOwner);
                if (!isLocalDriver && !IsOwner) transform.position = curr;
            };
            netAimAngle.OnValueChanged += (prev, curr) =>
            {
                bool isLocalDriver = isFlightActive && currentOperator != null && (!IsNetworkSessionActive || currentOperator.IsOwner);
                if (!isLocalDriver && !IsOwner)
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
            isReturningToDock = false;
            flightPathWaypoints.Clear();
            flightPathWaypoints.Add(activePos);
            currentVelocity = Vector2.zero;

            // Lock the pilot's physical body in place (vulnerable at the terminal)
            currentOperator.SetControlEnabled(false);

            // Subscribe to pilot damage & downed events for emergency disengage
            if (currentOperator.TryGetComponent(out operatorHealth))
            {
                previousOperatorHealth = operatorHealth.CurrentHealth;
                operatorHealth.OnLifeStateChanged += HandleOperatorLifeStateChanged;
                operatorHealth.OnHealthChanged += HandleOperatorHealthChanged;
            }

            // Bind Camera to the Drone (Local pilot only)
            bool isLocalPilot = !IsNetworkSessionActive || pilot.IsOwner;
            if (isLocalPilot)
            {
                UnityEngine.Camera mainCam = UnityEngine.Camera.main;
                if (mainCam != null && mainCam.TryGetComponent(out CameraFollow cameraFollow))
                {
                    cameraFollow.SetTarget(transform);
                }
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
        /// Disengages operator control and handles parking or docking the drone.
        /// </summary>
        public void DisengageFlight(string reason = "Operator Disengaged")
        {
            if (!isFlightActive && !isReturningToDock && !netIsActive.Value) return;

            ReleaseOperatorControl();
            if (isReturningToDock)
            {
                FinishDocking();
            }
            else
            {
                ExitFlightAndReturnToDock(reason);
            }
        }

        /// <summary>
        /// Immediately releases operator control (frees player & restores camera)
        /// while commanding the drone to autonomously navigate back to the docking station.
        /// </summary>
        public void ExitFlightAndReturnToDock(string reason = "Operator Disengaged")
        {
            ReleaseOperatorControl();
            StartAutoReturnFlight();
            Debug.Log($"<color=#00FFCC>[DroneController]</color> {reason} -> Operator control released, Recon Drone autonomously returning to dock.");
        }

        private void ReleaseOperatorControl()
        {
            isFlightActive = false;

            // Close CRT HUD Overlay immediately
            if (DroneHUDOverlay.Instance != null && DroneHUDOverlay.Instance.IsOpen)
            {
                DroneHUDOverlay.Instance.Close();
            }

            // Unsubscribe from operator health events
            if (operatorHealth != null)
            {
                operatorHealth.OnLifeStateChanged -= HandleOperatorLifeStateChanged;
                operatorHealth.OnHealthChanged -= HandleOperatorHealthChanged;
                operatorHealth = null;
            }
            previousOperatorHealth = -1f;

            // Restore pilot body movement and Camera
            if (currentOperator != null)
            {
                currentOperator.transform.position = pilotAnchorPosition;
                if (currentOperator.TryGetComponent(out Rigidbody2D pilotRb))
                {
                    pilotRb.position = pilotAnchorPosition;
                    pilotRb.linearVelocity = Vector2.zero;
                }
                currentOperator.SetControlEnabled(true);

                bool isLocalPilot = !IsNetworkSessionActive || currentOperator.IsOwner;
                if (isLocalPilot)
                {
                    UnityEngine.Camera mainCam = UnityEngine.Camera.main;
                    if (mainCam != null && mainCam.TryGetComponent(out CameraFollow cameraFollow))
                    {
                        cameraFollow.SetTarget(currentOperator.transform);
                        cameraFollow.SetLookaheadOffset(Vector2.zero, 0.25f);
                    }
                }
            }

            currentOperator = null;
        }

        public void FinishDocking()
        {
            isReturningToDock = false;
            isFlightActive = false;
            flightPathWaypoints.Clear();
            currentVelocity = Vector2.zero;

            if (rb != null)
            {
                rb.position = dockAnchorPosition;
                rb.linearVelocity = Vector2.zero;
                rb.constraints = RigidbodyConstraints2D.FreezeAll;
            }
            transform.position = dockAnchorPosition;

            if (gameplayLightSource != null)
            {
                gameplayLightSource.ServerSetEnabled(false);
            }

            if (IsNetworkSessionActive)
            {
                if (IsServer)
                {
                    netIsActive.Value = false;
                    netPosition.Value = dockAnchorPosition;
                }
                else
                {
                    RequestDisengageFlightRpc(dockAnchorPosition);
                }
            }

            OnFlightStateChanged?.Invoke(false);
            OnAutoReturnStateChanged?.Invoke(false);
            Debug.Log("<color=#00FFCC>[DroneController]</color> Recon Drone safely docked and locked on station cradle. Charging initiated.");
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
                ExitFlightAndReturnToDock("PILOT INCAPACITATED!");
            }
        }

        private void HandleOperatorHealthChanged(float current, float max)
        {
            if (previousOperatorHealth > 0f && current < previousOperatorHealth - 0.05f)
            {
                // Operator actually took new damage while at the console!
                ExitFlightAndReturnToDock("WARNING: OPERATOR BODY UNDER ATTACK!");
            }
            previousOperatorHealth = current;
        }

        private void Update()
        {
            if (isFlightActive && currentOperator != null)
            {
                bool isLocallyControlled = !IsNetworkSessionActive || currentOperator.IsOwner;
                if (isLocallyControlled)
                {
                    ReadFlightInput();
                    UpdateSpotlightAim();
                    UpdateCameraLookahead();
                    HandleFlightHotkeys();
                    UpdateBatteryDrain(Time.deltaTime);
                    UpdateRadarSweep(Time.deltaTime);
                    SyncNetworkState();
                }
            }
            else if (isReturningToDock)
            {
                // Autonomous return cruise: minimal battery consumption, synced to network
                UpdateBatteryDrain(Time.deltaTime * 0.4f);
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

            if (isReturningToDock)
            {
                ExecuteAutoReturnFlightPhysics();
            }
            else if (isFlightActive)
            {
                bool isLocallyControlled = currentOperator != null && (!IsNetworkSessionActive || currentOperator.IsOwner);
                if (isLocallyControlled)
                {
                    RecordFlightBreadcrumb();
                    ApplyFlightPhysics(moveInput);
                }
            }
        }

        /// <summary>
        /// Engages autonomous return-to-base flight, navigating along recorded breadcrumbs back to dock.
        /// </summary>
        public void StartAutoReturnFlight()
        {
            Vector2 currentPos = rb != null ? rb.position : (Vector2)transform.position;
            if (Vector2.Distance(currentPos, dockAnchorPosition) <= 0.40f)
            {
                FinishDocking();
                return;
            }

            isReturningToDock = true;
            isFlightActive = false;
            rthStuckTimer = 0f;
            rthLastPosition = currentPos;

            if (rb != null)
            {
                rb.constraints = RigidbodyConstraints2D.FreezeRotation;
            }

            OnFlightStateChanged?.Invoke(true);
            OnAutoReturnStateChanged?.Invoke(true);

            if (gameplayLightSource != null)
            {
                gameplayLightSource.ServerSetEnabled(true);
            }

            Debug.Log("<color=#00FFCC>[DroneController]</color> Autonomous Return-to-Home engaged: Navigating to Dock.");
        }

        private void RecordFlightBreadcrumb()
        {
            Vector2 currentPos = rb != null ? rb.position : (Vector2)transform.position;
            if (flightPathWaypoints.Count == 0)
            {
                flightPathWaypoints.Add(dockAnchorPosition);
                flightPathWaypoints.Add(currentPos);
                return;
            }

            Vector2 lastPoint = flightPathWaypoints[flightPathWaypoints.Count - 1];
            if ((currentPos - lastPoint).sqrMagnitude >= (WAYPOINT_SPACING * WAYPOINT_SPACING))
            {
                flightPathWaypoints.Add(currentPos);
            }
        }

        private void ExecuteAutoReturnFlightPhysics()
        {
            Vector2 currentPos = rb != null ? rb.position : (Vector2)transform.position;

            // 1. Proximity check to dock cradle
            float distToDock = Vector2.Distance(currentPos, dockAnchorPosition);
            if (distToDock <= 0.40f)
            {
                // Smooth magnetic suction onto cradle
                Vector2 snapPos = Vector2.MoveTowards(currentPos, dockAnchorPosition, 2.5f * Time.fixedDeltaTime);
                if (rb != null)
                {
                    rb.position = snapPos;
                    rb.linearVelocity = Vector2.zero;
                }
                transform.position = snapPos;
                currentVelocity = Vector2.zero;

                if (distToDock <= 0.06f)
                {
                    FinishDocking();
                }
                return;
            }

            // 2. Intelligent String-Pulling Shortcut: Direct Line of Sight to Dock
            bool directToDock = false;
            Vector2 toDock = dockAnchorPosition - currentPos;
            float dockDist = toDock.magnitude;
            if (dockDist > 0.05f)
            {
                RaycastHit2D hit = Physics2D.CircleCast(currentPos, 0.12f, toDock.normalized, dockDist, obstacleLayers);
                if (hit.collider == null)
                {
                    directToDock = true;
                    flightPathWaypoints.Clear();
                }
            }

            // 3. String-Pulling on Waypoint History: Skip all intermediate points if earlier points are directly visible
            Vector2 targetNavPos;
            if (directToDock || flightPathWaypoints.Count == 0)
            {
                targetNavPos = dockAnchorPosition;
            }
            else
            {
                // Scan from 0 (earliest / closest to dock) up to current to find furthest visible shortcut
                for (int i = 0; i < flightPathWaypoints.Count; i++)
                {
                    Vector2 wp = flightPathWaypoints[i];
                    Vector2 toWp = wp - currentPos;
                    float wpDist = toWp.magnitude;
                    if (wpDist < 0.1f) continue;

                    RaycastHit2D hit = Physics2D.CircleCast(currentPos, 0.12f, toWp.normalized, wpDist, obstacleLayers);
                    if (hit.collider == null)
                    {
                        // Direct line of sight to waypoint i found! Prune all waypoints after i
                        if (flightPathWaypoints.Count > i + 1)
                        {
                            flightPathWaypoints.RemoveRange(i + 1, flightPathWaypoints.Count - (i + 1));
                        }
                        break;
                    }
                }

                targetNavPos = flightPathWaypoints[flightPathWaypoints.Count - 1];

                // 4. Waypoint arrival check with generous tolerance
                float distToWp = Vector2.Distance(currentPos, targetNavPos);
                if (distToWp <= 0.70f)
                {
                    flightPathWaypoints.RemoveAt(flightPathWaypoints.Count - 1);
                    targetNavPos = flightPathWaypoints.Count > 0 
                        ? flightPathWaypoints[flightPathWaypoints.Count - 1] 
                        : dockAnchorPosition;
                }
            }

            // 5. Anti-Stuck Detection: if drone stopped making progress for > 0.25s
            if ((currentPos - rthLastPosition).sqrMagnitude < 0.002f)
            {
                rthStuckTimer += Time.fixedDeltaTime;
                if (rthStuckTimer > 0.25f)
                {
                    rthStuckTimer = 0f;
                    if (flightPathWaypoints.Count > 0)
                    {
                        flightPathWaypoints.RemoveAt(flightPathWaypoints.Count - 1);
                    }
                }
            }
            else
            {
                rthStuckTimer = 0f;
            }
            rthLastPosition = currentPos;

            // 6. Steer towards targetNavPos with wall feeler deflection
            Vector2 desiredDir = (targetNavPos - currentPos).normalized;
            Vector2 moveDir = desiredDir;

            // Feeler to deflect smoothly around door frames / wall corners
            RaycastHit2D feeler = Physics2D.CircleCast(currentPos, 0.15f, desiredDir, 0.6f, obstacleLayers);
            if (feeler.collider != null && feeler.distance < 0.35f)
            {
                Vector2 slideDir = Vector2.Perpendicular(feeler.normal);
                if (Vector2.Dot(slideDir, desiredDir) < 0f) slideDir = -slideDir;
                moveDir = (desiredDir * 0.4f + slideDir * 0.6f).normalized;
            }

            Vector2 targetVelocity = moveDir * RTH_CRUISE_SPEED;
            currentVelocity = Vector2.MoveTowards(currentVelocity, targetVelocity, 20f * Time.fixedDeltaTime);
            if (rb != null)
            {
                rb.linearVelocity = currentVelocity;
            }

            // Aim spotlight in flight direction
            if (moveDir.sqrMagnitude > 0.01f)
            {
                aimAngleDegrees = Mathf.Atan2(moveDir.y, moveDir.x) * Mathf.Rad2Deg;
                UpdateSpotlightRotation(aimAngleDegrees);
                OnAimChanged?.Invoke(aimAngleDegrees);
                if (gameplayLightSource != null)
                {
                    gameplayLightSource.ServerSetAimAngle(aimAngleDegrees);
                }
            }
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

            if (rb != null)
            {
                rb.linearVelocity = currentVelocity;
            }
        }

        private const float GIMBAL_TURN_SPEED = 480f; // degrees per second
        private float lastBumpSoundTime = 0f;

        private void UpdateSpotlightAim()
        {
            Mouse mouse = Mouse.current;
            UnityEngine.Camera cam = UnityEngine.Camera.main;
            float targetAngle = aimAngleDegrees;

            if (mouse != null && cam != null)
            {
                Vector2 mouseScreen = mouse.position.ReadValue();
                float camDistance = Mathf.Abs(cam.transform.position.z - transform.position.z);
                Vector3 mouseWorld = cam.ScreenToWorldPoint(new Vector3(mouseScreen.x, mouseScreen.y, camDistance));
                mouseWorld.z = 0f;

                Vector2 dir = (Vector2)mouseWorld - (Vector2)transform.position;
                if (dir.sqrMagnitude > 0.09f) // 0.3m minimum deadzone to prevent erratic spinning
                {
                    targetAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                }
            }
            else if (moveInput.sqrMagnitude > 0.01f)
            {
                targetAngle = Mathf.Atan2(moveInput.y, moveInput.x) * Mathf.Rad2Deg;
            }

            // Smooth motorized gimbal rotation
            aimAngleDegrees = Mathf.MoveTowardsAngle(aimAngleDegrees, targetAngle, GIMBAL_TURN_SPEED * Time.deltaTime);

            UpdateSpotlightRotation(aimAngleDegrees);
            OnAimChanged?.Invoke(aimAngleDegrees);

            if (gameplayLightSource != null)
            {
                gameplayLightSource.ServerSetAimAngle(aimAngleDegrees);
            }
        }

        private void UpdateCameraLookahead()
        {
            UnityEngine.Camera mainCam = UnityEngine.Camera.main;
            if (mainCam != null && mainCam.TryGetComponent(out CameraFollow camFollow))
            {
                Vector2 aimDir = new Vector2(Mathf.Cos(aimAngleDegrees * Mathf.Deg2Rad), Mathf.Sin(aimAngleDegrees * Mathf.Deg2Rad));
                Vector2 lookahead = (aimDir * 1.6f) + (currentVelocity * 0.35f);
                camFollow.SetLookaheadOffset(lookahead, 0.30f);
            }
        }

        private void UpdateSpotlightRotation(float angle)
        {
            if (spotlightPivot != null)
            {
                spotlightPivot.rotation = Quaternion.Euler(0f, 0f, angle);
            }
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (Time.time - lastBumpSoundTime > 0.18f)
            {
                lastBumpSoundTime = Time.time;
                if (TryGetComponent(out DroneView view))
                {
                    view.PlayCollisionBumpSFX();
                }
            }
        }

        private void HandleFlightHotkeys()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;

            // [ESC] or [Space] immediately exits operator control and commands drone to fly back to dock
            if (keyboard.escapeKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame)
            {
                ExitFlightAndReturnToDock("Operator Pressed ESC - Returning to Dock");
            }
        }

        private void UpdateBatteryDrain(float dt)
        {
            if (currentBattery <= 0f) return; // Prevent repeated triggers while on autopilot

            currentBattery = Mathf.Max(0f, currentBattery - (batteryDrainRate * dt));
            OnBatteryChanged?.Invoke(currentBattery);

            if (currentBattery <= 0f)
            {
                ExitFlightAndReturnToDock("BATTERY DEPLETED // AUTO-RECALL TRIGGERED");
            }
        }

        private void UpdateBatteryRecharge(float dt)
        {
            // Physical restriction: Drone must be physically rested on or beside the dock cradle in Airlock
            if (!IsAtDockStation) return;

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

            // Throttle network replication to 20 Hz (50ms) to prevent network congestion
            networkSyncTimer += Time.deltaTime;
            if (networkSyncTimer < NETWORK_SYNC_INTERVAL) return;
            networkSyncTimer = 0f;

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
