using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Serialization;
using TheLastSignal.Core;
using TheLastSignal.Core.Interaction;
using TheLastSignal.Entities.Player;
using TheLastSignal.Entities.Monster;
using TheLastSignal.Systems.Monster;

namespace TheLastSignal.Environment.Interactables
{
    public enum SafeLightJunctionState
    {
        Inactive,
        Activating,
        Active,
        Brownout,
        Deactivating
    }

    /// <summary>
    /// Reusable, interactive Safe-Light Junction for top-down 2D survival horror.
    /// Emits warm amber safe-zone light at the intersection of four corridors connecting four rooms.
    /// Repels and weakens The Shade, creating a temporary safe zone for survivors.
    /// 
    /// Follows Host-authoritative Netcode for GameObjects architecture with seamless single-player fallback.
    /// </summary>
    [SelectionBase]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CircleCollider2D))]
    [RequireComponent(typeof(NetworkObject))]
    public class SafeLightJunction : NetworkBehaviour, IInteractable
    {
        #region Static Registry & Spatial Queries

        private static readonly List<SafeLightJunction> activeJunctions = new List<SafeLightJunction>();

        public static IReadOnlyList<SafeLightJunction> ActiveJunctions => activeJunctions;

        /// <summary>
        /// Checks whether a world position is inside any currently active Safe-Light Junction radius.
        /// Zero allocations.
        /// </summary>
        public static bool IsPositionInSafeZone(Vector2 worldPosition)
        {
            for (int i = 0; i < activeJunctions.Count; i++)
            {
                var j = activeJunctions[i];
                if (j != null && (j.CurrentState == SafeLightJunctionState.Active || j.CurrentState == SafeLightJunctionState.Brownout))
                {
                    float distSqr = (worldPosition - (Vector2)j.transform.position).sqrMagnitude;
                    if (distSqr <= j.safeRadius * j.safeRadius)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// Checks whether a given player is currently protected within an active Safe-Light Junction.
        /// </summary>
        public static bool IsPlayerInSafeZone(PlayerController player)
        {
            if (player == null) return false;
            return IsPositionInSafeZone(player.transform.position);
        }

        #endregion

        #region Inspector Configuration

        [Header("Identification")]
        [SerializeField] private string junctionName = "Junction Node A";
        [SerializeField] private string interactionPromptText = "[E] Activate Safe-Light Junction";

        [Header("State & Durations")]
        [Tooltip("Starting state for the device.")]
        [SerializeField] private SafeLightJunctionState initialState = SafeLightJunctionState.Inactive;
        [Tooltip("Active duration in seconds. Set to 0 or negative for permanent until manual failure.")]
        [SerializeField] private float activeDuration = 25.0f;
        [Tooltip("Interaction radius for the player pressing [E].")]
        [SerializeField] private float interactionRange = 1.6f;

        [Header("Safe Zone & Enemy Behavior (Gameplay Logic)")]
        [Tooltip("Radius of the safe zone protecting survivors against The Shade.")]
        [SerializeField] private float safeRadius = 6.0f;
        [Tooltip("Speed multiplier debuff applied to monster players inside the light (e.g. 0.6 = -40% speed).")]
        [SerializeField] [Range(0.1f, 1f)] private float monsterSpeedDebuff = 0.6f;
        [Tooltip("How often to scan for players/enemies in the safe zone (seconds).")]
        [SerializeField] private float scanInterval = 0.2f;

        [Header("Visuals")]
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private Sprite inactiveSprite;
        [SerializeField] private Sprite activatingSprite;
        [SerializeField] private Sprite activeSprite;
        [SerializeField] private Sprite brownoutSprite;

        [Header("Visuals - Core Animation (Active State)")]
        [Tooltip("Animated swirling core frames cycled when in Active state.")]
        [SerializeField] private Sprite[] activeCoreAnimationFrames;
        [Tooltip("Animation speed in frames per second for the swirling core.")]
        [SerializeField] private float coreAnimationFps = 6.0f;

        [Header("Lighting - Layered Rig")]
        [SerializeField] private Light2D centerLight;
        [SerializeField] private Light2D floorLight;
        [SerializeField] private List<Light2D> corridorLights = new List<Light2D>();
        [SerializeField] private List<Light2D> lensLights = new List<Light2D>();

        [Header("Lighting - Intensity & Tuning")]
        [Tooltip("Central core point light intensity when fully active.")]
        [SerializeField] private float coreLightIntensity = 2.2f;
        [Tooltip("Ambient floor pool point light intensity when fully active.")]
        [SerializeField] private float localFloorLightIntensity = 1.6f;
        [Tooltip("Four directional corridor projector beams intensity when fully active.")]
        [SerializeField] private float emitterLightIntensity = 1.4f;
        [Tooltip("Optic lens glow intensity at emitter aperture.")]
        [SerializeField] private float lensLightIntensity = 1.35f;
        [Tooltip("Standby/inactive indicator light intensity.")]
        [SerializeField] private float inactiveLightIntensity = 0.35f;

        [Header("Lighting - Radius & Geometry")]
        [Tooltip("Visual outer radius of the local floor light pool illuminating the intersection.")]
        [SerializeField] private float visualLightRadius = 7.5f;
        [Tooltip("Visual inner radius of the local floor light pool.")]
        [SerializeField] private float floorInnerRadius = 2.2f;
        [Tooltip("Visual inner radius of the central core light.")]
        [SerializeField] private float coreInnerRadius = 0.3f;
        [Tooltip("Visual outer radius of the central core light.")]
        [SerializeField] private float coreOuterRadius = 2.0f;
        [Tooltip("Corridor projector beam reach / outer radius down the corridors.")]
        [SerializeField] private float emitterOuterRadius = 11.5f;
        [Tooltip("Corridor projector beam start / inner radius.")]
        [SerializeField] private float emitterInnerRadius = 0.5f;
        [Tooltip("Corridor projector beam focused inner spot angle.")]
        [SerializeField] private float emitterInnerAngle = 26.0f;
        [Tooltip("Corridor projector beam outer spot cone angle.")]
        [SerializeField] private float emitterOuterAngle = 52.0f;
        [Tooltip("Optic lens glow radius at emitter aperture.")]
        [SerializeField] private float lensLightRadius = 0.95f;

        [Header("Lighting - Colors & Timing")]
        [SerializeField] private Color warmAmberColor = new Color(1.0f, 0.75f, 0.32f, 1.0f);
        [SerializeField] private Color coreWhiteAmberColor = new Color(1.0f, 0.88f, 0.55f, 1.0f);
        [SerializeField] private Color inactiveIndicatorColor = new Color(0.95f, 0.18f, 0.12f, 1.0f);
        [SerializeField] private Color brownoutWarningColor = new Color(1.0f, 0.28f, 0.16f, 1.0f);
        [Tooltip("Duration of the activation transition sequence.")]
        [FormerlySerializedAs("activationDelay")]
        [SerializeField] private float activationTransitionDuration = 1.2f;

        [Header("Brownout Warning (Last 5s)")]
        [Tooltip("Time before deactivation when Brownout flicker & alarm begins.")]
        [SerializeField] private float brownoutDuration = 5.0f;

        [Header("Visual Effects (VFX)")]
        [Tooltip("Diagonal steam purge particle systems triggered upon activation startup.")]
        [SerializeField] private ParticleSystem steamPurgeParticles;

        [Header("Audio")]
        [SerializeField] private AudioSource audioSource;
        [Tooltip("Whether to play a background humming drone while the junction is active (set to false to silence humming).")]
        [SerializeField] private bool playActiveHum = false;
        [SerializeField] private AudioClip startupSound;
        [SerializeField] private AudioClip activeHumSound;
        [SerializeField] private AudioClip brownoutAlarmSound;
        [SerializeField] private AudioClip deactivateSound;
        [SerializeField] private AudioClip steamHissSound;

        #endregion

        #region Network State & Fallbacks

        /// <summary>Server-authoritative state replicated to all peers.</summary>
        private readonly NetworkVariable<SafeLightJunctionState> networkState = new NetworkVariable<SafeLightJunctionState>(
            SafeLightJunctionState.Inactive, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        /// <summary>Replicated remaining duration timer.</summary>
        private readonly NetworkVariable<float> networkTimer = new NetworkVariable<float>(
            0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        /// <summary>Single-player non-networked fallback state.</summary>
        private SafeLightJunctionState localState = SafeLightJunctionState.Inactive;
        private float localTimer = 0f;

        public SafeLightJunctionState CurrentState => IsSpawned ? networkState.Value : localState;
        public float RemainingTime => IsSpawned ? networkTimer.Value : localTimer;
        public float SafeRadius => safeRadius;
        public float SafeZoneRadius => safeRadius;
        public float VisualLightRadius => visualLightRadius;
        public float CoreLightIntensity => coreLightIntensity;
        public float LocalFloorLightIntensity => localFloorLightIntensity;
        public float EmitterLightIntensity => emitterLightIntensity;
        public float ActivationTransitionDuration => activationTransitionDuration;
        public string JunctionName => junctionName;
        public Sprite[] ActiveCoreAnimationFrames => activeCoreAnimationFrames;
        public float CoreAnimationFps => coreAnimationFps;

        public event Action<SafeLightJunctionState> OnStateChanged;

        #endregion

        #region Internal Tracking

        private CircleCollider2D interactionTrigger;
        private Coroutine activationCoroutine;
        private Coroutine visualActivationCoroutine;
        private float nextScanTime = 0f;
        private readonly List<PlayerController> debuffedMonsters = new List<PlayerController>();

        #endregion

        #region IInteractable Implementation

        public string InteractionPrompt => GetInteractionPrompt();

        public string GetInteractionPrompt()
        {
            if (CurrentState == SafeLightJunctionState.Inactive)
            {
                return interactionPromptText;
            }
            else if (CurrentState == SafeLightJunctionState.Active && activeDuration > 0)
            {
                int sec = Mathf.CeilToInt(RemainingTime);
                return $"Safe-Light Active ({sec}s)";
            }
            else if (CurrentState == SafeLightJunctionState.Brownout && activeDuration > 0)
            {
                int sec = Mathf.CeilToInt(RemainingTime);
                return $"WARNING: Power Failure in {sec}s!";
            }
            return "";
        }

        public bool CanInteract(PlayerController player)
        {
            if (player == null) return false;
            if (CurrentState != SafeLightJunctionState.Inactive) return false;
            if (player.IsMonster) return false; // Survivors activate safe light

            float distSqr = ((Vector2)transform.position - (Vector2)player.transform.position).sqrMagnitude;
            float maxRange = interactionRange + 0.6f;
            return distSqr <= maxRange * maxRange;
        }

        public void Interact(PlayerController player)
        {
            if (!CanInteract(player)) return;

            if (IsSpawned)
            {
                RequestActivateServerRpc();
            }
            else
            {
                // Single-player local flow
                StartActivationLocally();
            }
        }

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            EnsureComponents();
            localState = initialState;
            ApplyVisualState(localState);
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            networkState.OnValueChanged += HandleNetworkStateChanged;

            if (IsServer)
            {
                networkState.Value = initialState;
            }
            else
            {
                // Client sync initial state
                ApplyVisualState(networkState.Value);
            }
        }

        public override void OnNetworkDespawn()
        {
            networkState.OnValueChanged -= HandleNetworkStateChanged;
            base.OnNetworkDespawn();
        }

        private void OnEnable()
        {
            if (CurrentState == SafeLightJunctionState.Active && !activeJunctions.Contains(this))
            {
                activeJunctions.Add(this);
            }
        }

        private void OnDisable()
        {
            activeJunctions.Remove(this);
            RevertAllMonsterDebuffs();
        }

        private void Update()
        {
            // Dynamic visual flicker during Brownout warning for all peers
            if (CurrentState == SafeLightJunctionState.Brownout)
            {
                ApplyBrownoutFlicker();
            }
            else if (CurrentState == SafeLightJunctionState.Active)
            {
                ApplyActiveLightSubtleBreathing();
                UpdateActiveCoreAnimation();
            }

            // Authoritative Host or Local Single-Player timer update
            bool isAuthority = !IsSpawned || IsServer;
            if (!isAuthority) return;

            if (CurrentState == SafeLightJunctionState.Active || CurrentState == SafeLightJunctionState.Brownout)
            {
                if (activeDuration > 0)
                {
                    float currentTimer = IsSpawned ? networkTimer.Value : localTimer;
                    currentTimer -= Time.deltaTime;

                    if (IsSpawned) networkTimer.Value = Mathf.Max(0f, currentTimer);
                    else localTimer = Mathf.Max(0f, currentTimer);

                    // Transition to Brownout when remaining time drops below brownoutDuration
                    if (CurrentState == SafeLightJunctionState.Active && currentTimer <= brownoutDuration && currentTimer > 0f)
                    {
                        if (IsSpawned && IsServer) networkState.Value = SafeLightJunctionState.Brownout;
                        else if (!IsSpawned) SetLocalState(SafeLightJunctionState.Brownout);
                    }

                    if (currentTimer <= 0f)
                    {
                        DeactivateDevice();
                    }
                }

                // Periodic Safe-zone scanning
                if (Time.time >= nextScanTime)
                {
                    nextScanTime = Time.time + scanInterval;
                    TickSafeZoneEffects();
                }
            }
        }

        #endregion

        #region State Flow & Activation

        [ServerRpc(RequireOwnership = false)]
        private void RequestActivateServerRpc(ServerRpcParams rpcParams = default)
        {
            if (networkState.Value != SafeLightJunctionState.Inactive) return;

            // Host validates sender range if PlayerController can be identified
            ulong senderClientId = rpcParams.Receive.SenderClientId;
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.ConnectedClients.TryGetValue(senderClientId, out var client))
            {
                if (client.PlayerObject != null)
                {
                    float distSqr = (transform.position - client.PlayerObject.transform.position).sqrMagnitude;
                    float maxAllowed = (interactionRange + 1.2f) * (interactionRange + 1.2f);
                    if (distSqr > maxAllowed) return;
                }
            }

            StartActivationServer();
        }

        private void StartActivationServer()
        {
            if (activationCoroutine != null) StopCoroutine(activationCoroutine);
            activationCoroutine = StartCoroutine(ServerActivationSequence());
        }

        private IEnumerator ServerActivationSequence()
        {
            networkState.Value = SafeLightJunctionState.Activating;
            yield return new WaitForSeconds(activationTransitionDuration);

            networkState.Value = SafeLightJunctionState.Active;
            networkTimer.Value = activeDuration;
            activationCoroutine = null;
        }

        private void StartActivationLocally()
        {
            if (activationCoroutine != null) StopCoroutine(activationCoroutine);
            activationCoroutine = StartCoroutine(LocalActivationSequence());
        }

        private IEnumerator LocalActivationSequence()
        {
            SetLocalState(SafeLightJunctionState.Activating);
            yield return new WaitForSeconds(activationTransitionDuration);

            localTimer = activeDuration;
            SetLocalState(SafeLightJunctionState.Active);
            activationCoroutine = null;
        }

        private void DeactivateDevice()
        {
            if (IsSpawned && IsServer)
            {
                networkState.Value = SafeLightJunctionState.Inactive;
                networkTimer.Value = 0f;
            }
            else if (!IsSpawned)
            {
                SetLocalState(SafeLightJunctionState.Inactive);
            }
        }

        private void HandleNetworkStateChanged(SafeLightJunctionState oldState, SafeLightJunctionState newState)
        {
            ApplyVisualState(newState);
            OnStateChanged?.Invoke(newState);
        }

        private void SetLocalState(SafeLightJunctionState newState)
        {
            if (localState == newState) return;
            localState = newState;
            ApplyVisualState(newState);
            OnStateChanged?.Invoke(newState);
        }

        #endregion

        #region Visuals, Lighting, VFX & Audio

        private void ApplyVisualState(SafeLightJunctionState state)
        {
            if (visualActivationCoroutine != null)
            {
                StopCoroutine(visualActivationCoroutine);
                visualActivationCoroutine = null;
            }

            switch (state)
            {
                case SafeLightJunctionState.Inactive:
                    if (spriteRenderer != null && inactiveSprite != null) spriteRenderer.sprite = inactiveSprite;
                    ConfigureLights(active: false, warmGlow: false);
                    StopSteamPurge();
                    PlaySound(deactivateSound);
                    if (audioSource != null && audioSource.isPlaying)
                    {
                        audioSource.Stop();
                    }
                    activeJunctions.Remove(this);
                    RevertAllMonsterDebuffs();
                    break;

                case SafeLightJunctionState.Activating:
                    if (spriteRenderer != null && activatingSprite != null) spriteRenderer.sprite = activatingSprite;
                    PlaySteamPurge();
                    visualActivationCoroutine = StartCoroutine(AnimateActivationSequence());
                    PlaySound(startupSound);
                    break;

                case SafeLightJunctionState.Active:
                    if (spriteRenderer != null)
                    {
                        if (activeCoreAnimationFrames != null && activeCoreAnimationFrames.Length > 0 && activeCoreAnimationFrames[0] != null)
                        {
                            spriteRenderer.sprite = activeCoreAnimationFrames[0];
                        }
                        else if (activeSprite != null)
                        {
                            spriteRenderer.sprite = activeSprite;
                        }
                    }
                    StopSteamPurge();
                    ConfigureLights(active: true, warmGlow: true, intensityMultiplier: 1.0f);
                    if (playActiveHum && activeHumSound != null)
                    {
                        PlaySound(activeHumSound, loop: true);
                    }
                    else if (audioSource != null && audioSource.isPlaying && audioSource.loop)
                    {
                        audioSource.Stop();
                        audioSource.loop = false;
                    }
                    if (!activeJunctions.Contains(this)) activeJunctions.Add(this);
                    break;

                case SafeLightJunctionState.Brownout:
                    if (spriteRenderer != null && brownoutSprite != null) spriteRenderer.sprite = brownoutSprite;
                    StopSteamPurge();
                    ConfigureLights(active: true, warmGlow: true, intensityMultiplier: 0.9f);
                    if (brownoutAlarmSound != null) PlaySound(brownoutAlarmSound, loop: true);
                    if (!activeJunctions.Contains(this)) activeJunctions.Add(this);
                    break;
            }
        }

        private IEnumerator AnimateActivationSequence()
        {
            float duration = Mathf.Max(0.1f, activationTransitionDuration);
            float elapsed = 0f;

            // Base starting state for transition
            if (centerLight != null)
            {
                centerLight.enabled = true;
                centerLight.color = coreWhiteAmberColor;
                centerLight.intensity = inactiveLightIntensity;
                centerLight.pointLightInnerRadius = coreInnerRadius;
                centerLight.pointLightOuterRadius = coreOuterRadius;
            }

            if (floorLight != null)
            {
                floorLight.enabled = false;
                floorLight.intensity = 0f;
                floorLight.pointLightInnerRadius = floorInnerRadius;
                floorLight.pointLightOuterRadius = floorInnerRadius;
                floorLight.color = warmAmberColor;
            }

            for (int i = 0; i < corridorLights.Count; i++)
            {
                if (corridorLights[i] != null)
                {
                    corridorLights[i].enabled = false;
                    corridorLights[i].intensity = 0f;
                    corridorLights[i].color = warmAmberColor;
                    corridorLights[i].pointLightInnerRadius = emitterInnerRadius;
                    corridorLights[i].pointLightOuterRadius = emitterInnerRadius;
                    corridorLights[i].pointLightInnerAngle = emitterInnerAngle;
                    corridorLights[i].pointLightOuterAngle = emitterOuterAngle;
                }
            }

            for (int i = 0; i < lensLights.Count; i++)
            {
                if (lensLights[i] != null)
                {
                    lensLights[i].enabled = false;
                    lensLights[i].intensity = 0f;
                    lensLights[i].color = coreWhiteAmberColor;
                    lensLights[i].pointLightInnerRadius = 0.1f;
                    lensLights[i].pointLightOuterRadius = lensLightRadius;
                }
            }

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float progress = Mathf.Clamp01(elapsed / duration);

                // Phase 1: Core intensifies (0% -> 35%)
                if (centerLight != null)
                {
                    float coreT = Mathf.Clamp01(progress / 0.35f);
                    centerLight.intensity = Mathf.Lerp(inactiveLightIntensity, coreLightIntensity * 1.15f, coreT);
                }

                // Phase 2: Directional projector beams shoot down corridors & lenses ignite (35% -> 65%)
                if (progress >= 0.35f)
                {
                    for (int i = 0; i < corridorLights.Count; i++)
                    {
                        var cl = corridorLights[i];
                        if (cl == null) continue;

                        float staggerThreshold = 0.35f + (i * 0.07f);
                        if (progress >= staggerThreshold)
                        {
                            cl.enabled = true;
                            float singleT = Mathf.Clamp01((progress - staggerThreshold) / 0.15f);
                            float smoothT = Mathf.SmoothStep(0f, 1f, singleT);
                            cl.intensity = Mathf.Lerp(0f, emitterLightIntensity, smoothT);
                            cl.pointLightOuterRadius = Mathf.Lerp(emitterInnerRadius + 1.2f, emitterOuterRadius, smoothT);
                        }
                    }

                    for (int i = 0; i < lensLights.Count; i++)
                    {
                        var ll = lensLights[i];
                        if (ll == null) continue;

                        float staggerThreshold = 0.35f + (i * 0.07f);
                        if (progress >= staggerThreshold)
                        {
                            ll.enabled = true;
                            float singleT = Mathf.Clamp01((progress - staggerThreshold) / 0.15f);
                            ll.intensity = Mathf.Lerp(0f, lensLightIntensity, singleT);
                        }
                    }
                }

                // Phase 3: Local floor pool expands to configured radius (55% -> 100%)
                if (progress >= 0.55f && floorLight != null)
                {
                    floorLight.enabled = true;
                    float floorT = Mathf.Clamp01((progress - 0.55f) / 0.45f);
                    float smoothFloor = Mathf.SmoothStep(0f, 1f, floorT);
                    floorLight.intensity = Mathf.Lerp(0f, localFloorLightIntensity, smoothFloor);
                    floorLight.pointLightOuterRadius = Mathf.Lerp(floorInnerRadius, visualLightRadius, smoothFloor);
                }

                yield return null;
            }

            // Settle into full Active state
            ConfigureLights(active: true, warmGlow: true, intensityMultiplier: 1.0f);
            visualActivationCoroutine = null;
        }

        private void ApplyBrownoutFlicker()
        {
            float flicker = UnityEngine.Random.Range(0.45f, 1.05f);
            if (UnityEngine.Random.value < 0.14f) flicker = 0.18f; // sporadic brownout dip

            Color flickAmber = Color.Lerp(warmAmberColor, brownoutWarningColor, UnityEngine.Random.Range(0.4f, 0.95f));
            Color flickCore = Color.Lerp(coreWhiteAmberColor, brownoutWarningColor, UnityEngine.Random.Range(0.5f, 1.0f));

            if (centerLight != null)
            {
                centerLight.intensity = coreLightIntensity * flicker;
                centerLight.color = flickCore;
            }

            if (floorLight != null)
            {
                floorLight.intensity = localFloorLightIntensity * flicker;
                floorLight.color = flickAmber;
            }

            for (int i = 0; i < corridorLights.Count; i++)
            {
                if (corridorLights[i] != null)
                {
                    corridorLights[i].intensity = emitterLightIntensity * flicker;
                    corridorLights[i].color = flickAmber;
                }
            }

            for (int i = 0; i < lensLights.Count; i++)
            {
                if (lensLights[i] != null)
                {
                    lensLights[i].intensity = lensLightIntensity * flicker;
                    lensLights[i].color = flickCore;
                }
            }
        }

        public void PlaySteamPurge()
        {
            if (steamPurgeParticles != null)
            {
                steamPurgeParticles.Play(true);
            }
            if (steamHissSound != null && audioSource != null)
            {
                audioSource.PlayOneShot(steamHissSound, 0.85f);
            }
        }

        public void StopSteamPurge()
        {
            if (steamPurgeParticles != null)
            {
                steamPurgeParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
        }

        private void ApplyActiveLightSubtleBreathing()
        {
            // Restrained, subtle industrial hum fluctuation (+-2.5%)
            float breath = 1.0f + Mathf.Sin(Time.time * 3.5f) * 0.025f + (Mathf.PerlinNoise(Time.time * 8f, 0f) - 0.5f) * 0.02f;
            if (centerLight != null) centerLight.intensity = coreLightIntensity * breath;
            if (floorLight != null) floorLight.intensity = localFloorLightIntensity * breath;
            for (int i = 0; i < corridorLights.Count; i++)
            {
                if (corridorLights[i] != null) corridorLights[i].intensity = emitterLightIntensity * breath;
            }
            for (int i = 0; i < lensLights.Count; i++)
            {
                if (lensLights[i] != null) lensLights[i].intensity = lensLightIntensity * breath;
            }
        }

        private void UpdateActiveCoreAnimation()
        {
            if (spriteRenderer == null) return;
            if (activeCoreAnimationFrames == null || activeCoreAnimationFrames.Length == 0) return;

            int frameIndex = (int)(Time.time * coreAnimationFps) % activeCoreAnimationFrames.Length;
            if (frameIndex < 0) frameIndex = 0;
            Sprite targetFrame = activeCoreAnimationFrames[frameIndex];
            if (targetFrame != null && spriteRenderer.sprite != targetFrame)
            {
                spriteRenderer.sprite = targetFrame;
            }
        }

        private void ConfigureLights(bool active, bool warmGlow, float intensityMultiplier = 1.0f)
        {
            if (centerLight != null)
            {
                centerLight.enabled = true;
                centerLight.color = warmGlow ? coreWhiteAmberColor : inactiveIndicatorColor;
                centerLight.intensity = (warmGlow ? coreLightIntensity : inactiveLightIntensity) * intensityMultiplier;
                centerLight.pointLightInnerRadius = warmGlow ? coreInnerRadius : 0.2f;
                centerLight.pointLightOuterRadius = warmGlow ? coreOuterRadius : 1.2f;
            }

            if (floorLight != null)
            {
                floorLight.enabled = active;
                floorLight.color = warmAmberColor;
                floorLight.intensity = localFloorLightIntensity * intensityMultiplier;
                floorLight.pointLightInnerRadius = floorInnerRadius;
                floorLight.pointLightOuterRadius = visualLightRadius;
            }

            foreach (var light in corridorLights)
            {
                if (light != null)
                {
                    light.enabled = active;
                    light.color = warmAmberColor;
                    light.intensity = emitterLightIntensity * intensityMultiplier;
                    light.pointLightInnerRadius = emitterInnerRadius;
                    light.pointLightOuterRadius = emitterOuterRadius;
                    light.pointLightInnerAngle = emitterInnerAngle;
                    light.pointLightOuterAngle = emitterOuterAngle;
                }
            }

            foreach (var light in lensLights)
            {
                if (light != null)
                {
                    light.enabled = active;
                    light.color = warmGlow ? coreWhiteAmberColor : inactiveIndicatorColor;
                    light.intensity = (warmGlow ? lensLightIntensity : inactiveLightIntensity * 0.35f) * intensityMultiplier;
                    light.pointLightInnerRadius = 0.1f;
                    light.pointLightOuterRadius = lensLightRadius;
                }
            }
        }

        private void PlaySound(AudioClip clip, bool loop = false)
        {
            if (audioSource == null || clip == null) return;
            if (loop)
            {
                audioSource.loop = true;
                audioSource.clip = clip;
                audioSource.Play();
            }
            else
            {
                audioSource.PlayOneShot(clip);
            }
        }

        #endregion

        #region Safe Zone Logic & The Shade Integration

        private void TickSafeZoneEffects()
        {
            // 1. Debuff Player-Controlled Monsters within safeRadius
            var players = FindObjectsByType<PlayerController>(FindObjectsInactive.Exclude);
            for (int i = 0; i < players.Length; i++)
            {
                var p = players[i];
                if (p == null) continue;

                if (p.IsMonster)
                {
                    float distSqr = (transform.position - p.transform.position).sqrMagnitude;
                    bool inLight = distSqr <= (safeRadius * safeRadius);

                    if (inLight)
                    {
                        p.SpeedMultiplier = monsterSpeedDebuff;
                        if (!debuffedMonsters.Contains(p)) debuffedMonsters.Add(p);
                    }
                    else if (debuffedMonsters.Contains(p))
                    {
                        p.SpeedMultiplier = 1.0f;
                        debuffedMonsters.Remove(p);
                    }
                }
            }

            // 2. Repel / Stun AI Monsters (The Shade)
            var aiMonsters = FindObjectsByType<MonsterView>(FindObjectsInactive.Exclude);
            for (int i = 0; i < aiMonsters.Length; i++)
            {
                var monster = aiMonsters[i];
                if (monster == null) continue;

                float distSqr = (transform.position - monster.transform.position).sqrMagnitude;
                if (distSqr <= (safeRadius * safeRadius))
                {
                    // Repel or weaken The Shade via NetworkGameState if authority
                    if (NetworkGameState.Instance != null && (!IsSpawned || IsServer))
                    {
                        NetworkGameState.Instance.ServerSetMonsterState(MonsterState.Stun);
                    }
                }
            }
        }

        private void RevertAllMonsterDebuffs()
        {
            for (int i = 0; i < debuffedMonsters.Count; i++)
            {
                if (debuffedMonsters[i] != null)
                {
                    debuffedMonsters[i].SpeedMultiplier = 1.0f;
                }
            }
            debuffedMonsters.Clear();
        }

        #endregion

        #region Setup Helpers & Gizmos

        private void EnsureComponents()
        {
            if (interactionTrigger == null)
            {
                interactionTrigger = GetComponent<CircleCollider2D>();
                if (interactionTrigger == null) interactionTrigger = gameObject.AddComponent<CircleCollider2D>();
            }
            interactionTrigger.isTrigger = true;
            interactionTrigger.radius = interactionRange;

            if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();

#if UNITY_EDITOR
            if (activeCoreAnimationFrames == null || activeCoreAnimationFrames.Length == 0)
            {
                var frames = new List<Sprite>();
                for (int i = 0; i < 4; i++)
                {
                    var s = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(
                        $"Assets/_Project/Art/Environment/Interactables/SafeLightJunction/SafeLightJunction_Active_F{i}.png");
                    if (s != null) frames.Add(s);
                }
                if (frames.Count > 0) activeCoreAnimationFrames = frames.ToArray();
            }
#endif

            if (audioSource == null) audioSource = GetComponent<AudioSource>();

            if (audioSource != null)
            {
                audioSource.playOnAwake = false;
                audioSource.loop = false;
                audioSource.spatialBlend = 0.5f;
            }

            if (centerLight == null)
            {
                var tr = transform.Find("Light_Center_Amber");
                if (tr != null) centerLight = tr.GetComponent<Light2D>();
            }

            if (floorLight == null)
            {
                var tr = transform.Find("Light_LocalFloor");
                if (tr != null) floorLight = tr.GetComponent<Light2D>();
            }

            if (corridorLights == null || corridorLights.Count == 0)
            {
                corridorLights = new List<Light2D>();
                foreach (var dir in new[] { "North", "South", "West", "East" })
                {
                    var tr = transform.Find("CorridorLight_" + dir);
                    if (tr != null)
                    {
                        var l = tr.GetComponent<Light2D>();
                        if (l != null) corridorLights.Add(l);
                    }
                }
            }

            if (lensLights == null || lensLights.Count == 0)
            {
                lensLights = new List<Light2D>();
                foreach (var dir in new[] { "North", "South", "West", "East" })
                {
                    var tr = transform.Find("LensGlow_" + dir);
                    if (tr != null)
                    {
                        var l = tr.GetComponent<Light2D>();
                        if (l != null) lensLights.Add(l);
                    }
                }
            }

            if (steamPurgeParticles == null)
            {
                var tr = transform.Find("VFX_SteamPurge");
                if (tr != null) steamPurgeParticles = tr.GetComponent<ParticleSystem>();
            }

            // Ensure procedural clips if unassigned
            CreateDefaultAudioClips();
        }

        private void CreateDefaultAudioClips()
        {
            if (startupSound == null) startupSound = GenerateToneClip("SafeLight_Startup", 240, 0.4f, true);
            if (activeHumSound == null) activeHumSound = GenerateToneClip("SafeLight_Hum", 75, 1.2f, false);
            if (brownoutAlarmSound == null) brownoutAlarmSound = GenerateToneClip("SafeLight_BrownoutAlarm", 360, 0.6f, true);
            if (deactivateSound == null) deactivateSound = GenerateToneClip("SafeLight_PowerDown", 110, 0.5f, true);
            if (steamHissSound == null) steamHissSound = GenerateNoiseBurstClip("SafeLight_SteamHiss", 0.65f);
        }

        private static AudioClip GenerateToneClip(string name, float freq, float length, bool isPercussive)
        {
            int sampleRate = 44100;
            int samples = (int)(sampleRate * length);
            float[] data = new float[samples];

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / sampleRate;
                float env = isPercussive ? Mathf.Exp(-t * 6f) : 1f;
                float wave = Mathf.Sin(2f * Mathf.PI * freq * t) * 0.7f + Mathf.Sin(4f * Mathf.PI * freq * t) * 0.3f;
                if (isPercussive) wave += (UnityEngine.Random.value * 2f - 1f) * 0.2f;
                data[i] = wave * env * 0.35f;
            }

            AudioClip clip = AudioClip.Create(name, samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip GenerateNoiseBurstClip(string name, float length)
        {
            int sampleRate = 44100;
            int samples = (int)(sampleRate * length);
            float[] data = new float[samples];
            float last = 0f;

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / samples;
                float env = Mathf.Sin(t * Mathf.PI);
                if (t > 0.25f) env = Mathf.Exp(-(t - 0.25f) * 4.5f);

                float white = UnityEngine.Random.value * 2f - 1f;
                last = Mathf.Lerp(last, white, 0.35f); // filtered steam hiss
                data[i] = last * env * 0.4f;
            }

            AudioClip clip = AudioClip.Create(name, samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private void OnDrawGizmosSelected()
        {
            // 1. Interaction radius (Yellow)
            Gizmos.color = new Color(1f, 0.9f, 0.2f, 0.4f);
            Gizmos.DrawWireSphere(transform.position, interactionRange);

            // 2. Safe zone gameplay protection radius (Amber)
            Gizmos.color = new Color(1f, 0.75f, 0.2f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, safeRadius);

            // 3. Visual floor illumination radius (Soft Cyan)
            Gizmos.color = new Color(0.35f, 0.85f, 1f, 0.35f);
            Gizmos.DrawWireSphere(transform.position, visualLightRadius);
        }

        #endregion
    }
}
