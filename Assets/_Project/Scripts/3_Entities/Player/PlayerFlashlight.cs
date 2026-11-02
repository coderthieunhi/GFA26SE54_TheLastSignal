using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using TheLastSignal.Systems.Lighting;

namespace TheLastSignal.Entities.Player
{
    /// <summary>
    /// Server-authoritative per-Survivor flashlight. Owns the two replicated values (on/aim) and the
    /// server-only GameplayLightSource describing the cone for LightingController. Mirrors
    /// PlayerHealth's shape: NetworkVariable&lt;T&gt; (Server write, Everyone read), a ServerRpc entry
    /// point per input, and an offline/local fallback when there is no NetworkManager at all.
    ///
    /// Aim direction: the cone aims from the player's position toward the mouse cursor's world
    /// position (standard top-down mouse-look), computed locally by the owner each frame via
    /// Camera.main.ScreenToWorldPoint and sent through the same quantized RequestAim path as before.
    /// Falls back to the player's movement-facing direction (PlayerController.FacingDirection) if no
    /// Mouse device or Camera.main is available (e.g. controller-only input).
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    public class PlayerFlashlight : NetworkBehaviour
    {
        private static bool IsNetworkSessionActive =>
            NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;

        [Header("Config")]
        [SerializeField] private FlashlightConfigSO config;

        [Header("Cone Source (child GameObject so its visuals strip independently of the player sprite)")]
        [SerializeField] private GameplayLightSource coneSource;

        private readonly NetworkVariable<bool> flashlightOn = new NetworkVariable<bool>(
            true, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<float> aimAngleDegrees = new NetworkVariable<float>(
            0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private PlayerController playerController;
        private UnityEngine.Camera cachedCamera;
        private float lastSentAngle;
        private float sendCooldownRemaining;

        // Offline fallback (no NetworkManager at all — Epic 1 single-player/solo practice).
        private bool offlineOn = true;

        public bool IsOn => IsNetworkSessionActive && IsSpawned ? flashlightOn.Value : offlineOn;
        public float AimAngleDegrees => IsNetworkSessionActive && IsSpawned ? aimAngleDegrees.Value : lastSentAngle;

        /// <summary>The id GameplayLightSource.OwnerId was set to for this flashlight's cone — SanitySystem
        /// reads this so its "exclude my own cone" check (LightingController.IsLitExcludingOwner) uses the
        /// exact same string the cone itself was registered under.</summary>
        public string OwnerIdString => coneSource != null ? coneSource.OwnerId : string.Empty;

        public event System.Action<bool> OnFlashlightToggled;
        public event System.Action<float> OnAimChanged;

        private void Awake()
        {
            playerController = GetComponent<PlayerController>();

            if (coneSource == null)
            {
                coneSource = GetComponentInChildren<GameplayLightSource>();
            }

            if (coneSource != null && config != null)
            {
                coneSource.ConfigureCone(config.Range, config.ConeAngleDegrees);
            }
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            flashlightOn.OnValueChanged += HandleOnChanged;
            aimAngleDegrees.OnValueChanged += HandleAimChanged;

            if (IsServer)
            {
                flashlightOn.Value = true;
                if (coneSource != null)
                {
                    coneSource.SetOwnerId(OwnerClientId.ToString());
                    coneSource.ServerSetEnabled(true);
                }
            }

            OnFlashlightToggled?.Invoke(IsOn);
            OnAimChanged?.Invoke(AimAngleDegrees);
        }

        public override void OnNetworkDespawn()
        {
            flashlightOn.OnValueChanged -= HandleOnChanged;
            aimAngleDegrees.OnValueChanged -= HandleAimChanged;
            base.OnNetworkDespawn();
        }

        private void Start()
        {
            if (!IsNetworkSessionActive && coneSource != null && config != null)
            {
                coneSource.SetOwnerId("offline");
                coneSource.ServerSetEnabled(offlineOn);
            }
        }

        private void Update()
        {
            bool isLocalOwner = (IsNetworkSessionActive && IsSpawned) ? IsOwner : !IsNetworkSessionActive;
            if (!isLocalOwner)
            {
                return;
            }

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.fKey.wasPressedThisFrame)
            {
                RequestToggle();
            }

            // Rate-limited, quantized aim reporting (design: ~10-15 Hz max, only when changed).
            sendCooldownRemaining -= Time.deltaTime;
            float currentAngle = QuantizeAngle(MouseAimAngle());
            if (sendCooldownRemaining <= 0f && !Mathf.Approximately(currentAngle, lastSentAngle))
            {
                lastSentAngle = currentAngle;
                sendCooldownRemaining = config != null ? config.AimSendIntervalSeconds : 0.08f;
                RequestAim(currentAngle);
            }
        }

        /// <summary>Angle from the player's position to the mouse cursor's world position. Orthographic
        /// 2D camera: ScreenToWorldPoint needs a z equal to the camera's distance from the world's z=0
        /// plane (the player's own plane), not the mouse's screen z (which Mouse.position has none of).</summary>
        private float MouseAimAngle()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null)
            {
                return FacingToAngle();
            }

            if (cachedCamera == null)
            {
                cachedCamera = UnityEngine.Camera.main;
            }

            if (cachedCamera == null)
            {
                return FacingToAngle();
            }

            Vector3 screenPos = mouse.position.ReadValue();
            screenPos.z = -cachedCamera.transform.position.z;
            Vector3 worldPos = cachedCamera.ScreenToWorldPoint(screenPos);

            Vector2 direction = (Vector2)worldPos - (Vector2)transform.position;
            if (direction.sqrMagnitude < 0.0001f)
            {
                return FacingToAngle();
            }

            return Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        }

        private float FacingToAngle()
        {
            Vector2 facing = playerController != null ? playerController.FacingDirection : Vector2.down;
            if (facing.sqrMagnitude < 0.0001f)
            {
                facing = Vector2.down;
            }

            return Mathf.Atan2(facing.y, facing.x) * Mathf.Rad2Deg;
        }

        private static float QuantizeAngle(float degrees)
        {
            const float step = 5f;
            return Mathf.Round(degrees / step) * step;
        }

        public void RequestToggle()
        {
            if (IsNetworkSessionActive)
            {
                RequestToggleRpc();
            }
            else
            {
                offlineOn = !offlineOn;
                coneSource?.ServerSetEnabled(offlineOn);
                OnFlashlightToggled?.Invoke(offlineOn);
            }
        }

        public void RequestAim(float degrees)
        {
            if (IsNetworkSessionActive)
            {
                RequestAimRpc(degrees);
            }
            else
            {
                coneSource?.ServerSetAimAngle(degrees);
                OnAimChanged?.Invoke(degrees);
            }
        }

        /// <summary>Rejects toggles from anyone but the owner: SendTo.Server + RpcInvokePermission.Owner
        /// means only the owning client's invocation is ever delivered to this method at all.</summary>
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        private void RequestToggleRpc()
        {
            flashlightOn.Value = !flashlightOn.Value;
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        private void RequestAimRpc(float degrees)
        {
            aimAngleDegrees.Value = QuantizeAngle(degrees);
        }

        private void HandleOnChanged(bool previous, bool current)
        {
            if (IsServer && coneSource != null)
            {
                coneSource.ServerSetEnabled(current);
            }

            OnFlashlightToggled?.Invoke(current);
        }

        private void HandleAimChanged(float previous, float current)
        {
            if (IsServer && coneSource != null)
            {
                coneSource.ServerSetAimAngle(current);
            }

            OnAimChanged?.Invoke(current);
        }
    }
}
