using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using TheLastSignal.Core;
using TheLastSignal.Core.Interaction;

namespace TheLastSignal.Entities.Player
{
    /// <summary>
    /// Server-authoritative health and life-state component for Survivors.
    /// Manages:
    /// 1. Health points (0 - maxHealth), synchronized via NetworkVariable.
    /// 2. Life state: Alive, Downed (when HP reaches 0), Dead (eliminated).
    /// 3. Server-authoritative damage, healing, and revive mechanics.
    /// 4. Events for HUD, overhead UI, and gameplay systems.
    /// Adheres to ARCHITECTURE_RULES.md Rule 1 (Server owns state) and Rule 3 (Input goes up, state comes down).
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    public class PlayerHealth : NetworkBehaviour, IInteractable
    {
        private static bool IsNetworkSessionActive =>
            NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;

        /// <summary>All currently active PlayerHealth instances in the session.</summary>
        private static readonly List<PlayerHealth> activePlayers = new List<PlayerHealth>();
        public static IReadOnlyList<PlayerHealth> ActivePlayers => activePlayers;

        /// <summary>Static reference to the local client's PlayerHealth instance (null if not spawned).</summary>
        public static PlayerHealth Local { get; private set; }

        public static event Action<PlayerHealth> OnLocalPlayerHealthSpawned;
        public static event Action<PlayerHealth> OnLocalPlayerHealthDespawned;

        [Header("Health Settings")]
        [SerializeField] private float maxHealth = 100f;
        [SerializeField, Range(0.1f, 1f)] private float reviveHealthPercent = 0.35f;
        [SerializeField, Range(0.1f, 1f)] private float downedCrawlSpeedMultiplier = 0.35f;
        [SerializeField] private bool enableBleedout = false;
        [SerializeField] private float bleedoutDuration = 60f;

        [Header("Debug Controls (H: Damage, J: Heal, K: Revive)")]
        [SerializeField] private bool enableDebugKeys = true;

        // Server-authoritative synchronized network state
        private readonly NetworkVariable<float> currentHealth = new NetworkVariable<float>(
            100f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<PlayerLifeState> lifeState = new NetworkVariable<PlayerLifeState>(
            PlayerLifeState.Alive, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<float> bleedoutRemaining = new NetworkVariable<float>(
            0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        // Offline / Single-player fallback state
        private float offlineHealth = 100f;
        private PlayerLifeState offlineLifeState = PlayerLifeState.Alive;

        /// <summary>Invoked on all clients whenever health value changes (currentHealth, maxHealth).</summary>
        public event Action<float, float> OnHealthChanged;

        /// <summary>Invoked on all clients whenever the life state changes (newState).</summary>
        public event Action<PlayerLifeState> OnLifeStateChanged;

        /// <summary>Current health points (0 to MaxHealth).</summary>
        public float CurrentHealth => IsNetworkSessionActive && IsSpawned ? currentHealth.Value : offlineHealth;

        /// <summary>Maximum health points.</summary>
        public float MaxHealth => maxHealth;

        /// <summary>Normalized health ratio between 0 and 1.</summary>
        public float HealthPercent => maxHealth > 0.001f ? Mathf.Clamp01(CurrentHealth / maxHealth) : 0f;

        /// <summary>Current life state: Alive, Downed, or Dead.</summary>
        public PlayerLifeState LifeState => IsNetworkSessionActive && IsSpawned ? lifeState.Value : offlineLifeState;

        public bool IsAlive => LifeState == PlayerLifeState.Alive;
        public bool IsDowned => LifeState == PlayerLifeState.Downed;
        public bool IsDead => LifeState == PlayerLifeState.Dead;
        public float DownedCrawlSpeedMultiplier => downedCrawlSpeedMultiplier;
        public float BleedoutRemaining => IsNetworkSessionActive && IsSpawned ? bleedoutRemaining.Value : 0f;

        private void Awake()
        {
            offlineHealth = maxHealth;
            offlineLifeState = PlayerLifeState.Alive;
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            currentHealth.OnValueChanged += HandleNetworkHealthChanged;
            lifeState.OnValueChanged += HandleNetworkLifeStateChanged;

            if (IsServer)
            {
                currentHealth.Value = maxHealth;
                lifeState.Value = PlayerLifeState.Alive;
                bleedoutRemaining.Value = bleedoutDuration;
            }

            if (IsOwner)
            {
                Local = this;
                OnLocalPlayerHealthSpawned?.Invoke(this);
            }

            // Trigger initial UI sync
            OnHealthChanged?.Invoke(CurrentHealth, maxHealth);
            OnLifeStateChanged?.Invoke(LifeState);
        }

        public override void OnNetworkDespawn()
        {
            currentHealth.OnValueChanged -= HandleNetworkHealthChanged;
            lifeState.OnValueChanged -= HandleNetworkLifeStateChanged;

            if (IsOwner)
            {
                if (Local == this) Local = null;
                OnLocalPlayerHealthDespawned?.Invoke(this);
            }

            base.OnNetworkDespawn();
        }

        private void Start()
        {
            if (!IsNetworkSessionActive)
            {
                Local = this;
                OnLocalPlayerHealthSpawned?.Invoke(this);
                OnHealthChanged?.Invoke(offlineHealth, maxHealth);
                OnLifeStateChanged?.Invoke(offlineLifeState);
            }
        }

        private void OnEnable()
        {
            if (!activePlayers.Contains(this))
            {
                activePlayers.Add(this);
            }
        }

        private void OnDisable()
        {
            activePlayers.Remove(this);
            CheckAllPlayersDownedCondition();
        }

        public override void OnDestroy()
        {
            activePlayers.Remove(this);
            base.OnDestroy();
        }

        private void Update()
        {
            // Bleedout countdown on server when downed
            if (IsServer && enableBleedout && lifeState.Value == PlayerLifeState.Downed)
            {
                bleedoutRemaining.Value -= Time.deltaTime;
                if (bleedoutRemaining.Value <= 0f)
                {
                    bleedoutRemaining.Value = 0f;
                    lifeState.Value = PlayerLifeState.Dead;
                    CheckAllPlayersDownedCondition();
                }
            }

            // Development / test shortcuts for local player
            if (enableDebugKeys && (IsOwner || !IsNetworkSessionActive))
            {
                CheckDebugInputs();
            }
        }

        private void CheckDebugInputs()
        {
            Keyboard kb = Keyboard.current;
            if (kb == null) return;

            if (kb.hKey.wasPressedThisFrame)
            {
                TakeDamageFromClient(25f);
            }
            if (kb.jKey.wasPressedThisFrame)
            {
                HealFromClient(25f);
            }
            if (kb.kKey.wasPressedThisFrame)
            {
                ReviveFromClient();
            }
        }

        private void HandleNetworkHealthChanged(float previous, float current)
        {
            OnHealthChanged?.Invoke(current, maxHealth);
        }

        private void HandleNetworkLifeStateChanged(PlayerLifeState previous, PlayerLifeState current)
        {
            OnLifeStateChanged?.Invoke(current);
        }

        // =====================================================================
        // DAMAGE / HEAL / REVIVE API
        // =====================================================================

        /// <summary>
        /// Applies damage to the player. Server-authoritative.
        /// When HP reaches 0, enters Downed state as required by Capstone specification.
        /// </summary>
        public void TakeDamage(float amount)
        {
            if (amount <= 0f) return;

            if (IsNetworkSessionActive)
            {
                if (!IsServer)
                {
                    Debug.LogWarning("[PlayerHealth] TakeDamage called on client directly. Route via ServerRpc.");
                    return;
                }

                if (lifeState.Value != PlayerLifeState.Alive)
                {
                    return; // Already downed or dead
                }

                float newHealth = Mathf.Max(0f, currentHealth.Value - amount);
                currentHealth.Value = newHealth;

                if (newHealth <= 0f)
                {
                    lifeState.Value = PlayerLifeState.Downed;
                    bleedoutRemaining.Value = bleedoutDuration;
                    Debug.Log($"<color=red>[PlayerHealth]</color> Player {gameObject.name} entered DOWNED state!");
                    CheckAllPlayersDownedCondition();
                }
            }
            else
            {
                // Offline fallback
                if (offlineLifeState != PlayerLifeState.Alive) return;

                offlineHealth = Mathf.Max(0f, offlineHealth - amount);
                if (offlineHealth <= 0f)
                {
                    offlineLifeState = PlayerLifeState.Downed;
                    Debug.Log($"<color=red>[PlayerHealth]</color> Offline player entered DOWNED state!");
                    CheckAllPlayersDownedCondition();
                }

                OnHealthChanged?.Invoke(offlineHealth, maxHealth);
                OnLifeStateChanged?.Invoke(offlineLifeState);
            }
        }

        /// <summary>
        /// Restores health up to MaxHealth. Can only heal while Alive.
        /// </summary>
        public void Heal(float amount)
        {
            if (amount <= 0f) return;

            if (IsNetworkSessionActive)
            {
                if (!IsServer)
                {
                    Debug.LogWarning("[PlayerHealth] Heal called on client directly. Route via ServerRpc.");
                    return;
                }

                if (lifeState.Value != PlayerLifeState.Alive) return;

                currentHealth.Value = Mathf.Min(maxHealth, currentHealth.Value + amount);
            }
            else
            {
                if (offlineLifeState != PlayerLifeState.Alive) return;

                offlineHealth = Mathf.Min(maxHealth, offlineHealth + amount);
                OnHealthChanged?.Invoke(offlineHealth, maxHealth);
            }
        }

        /// <summary>
        /// Revives a downed teammate back to the Alive state.
        /// </summary>
        public void Revive(float restoredHp = -1f)
        {
            float targetHp = restoredHp > 0f ? restoredHp : (maxHealth * reviveHealthPercent);

            if (IsNetworkSessionActive)
            {
                if (!IsServer)
                {
                    Debug.LogWarning("[PlayerHealth] Revive called on client directly. Route via ServerRpc.");
                    return;
                }

                if (lifeState.Value != PlayerLifeState.Downed) return;

                currentHealth.Value = Mathf.Clamp(targetHp, 1f, maxHealth);
                lifeState.Value = PlayerLifeState.Alive;
                Debug.Log($"<color=green>[PlayerHealth]</color> Player {gameObject.name} was REVIVED with {currentHealth.Value} HP!");
                CheckAllPlayersDownedCondition();
            }
            else
            {
                if (offlineLifeState != PlayerLifeState.Downed) return;

                offlineHealth = Mathf.Clamp(targetHp, 1f, maxHealth);
                offlineLifeState = PlayerLifeState.Alive;
                OnHealthChanged?.Invoke(offlineHealth, maxHealth);
                OnLifeStateChanged?.Invoke(offlineLifeState);
                Debug.Log($"<color=green>[PlayerHealth]</color> Offline player was REVIVED with {offlineHealth} HP!");
                CheckAllPlayersDownedCondition();
            }
        }

        // =====================================================================
        // CLIENT REQUESTS (ServerRpc)
        // =====================================================================

        public void TakeDamageFromClient(float amount)
        {
            if (IsNetworkSessionActive)
            {
                RequestTakeDamageServerRpc(amount);
            }
            else
            {
                TakeDamage(amount);
            }
        }

        public void HealFromClient(float amount)
        {
            if (IsNetworkSessionActive)
            {
                RequestHealServerRpc(amount);
            }
            else
            {
                Heal(amount);
            }
        }

        public void ReviveFromClient(float restoredHp = -1f)
        {
            if (IsNetworkSessionActive)
            {
                RequestReviveServerRpc(restoredHp);
            }
            else
            {
                Revive(restoredHp);
            }
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void RequestTakeDamageServerRpc(float amount)
        {
            TakeDamage(amount);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void RequestHealServerRpc(float amount)
        {
            Heal(amount);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void RequestReviveServerRpc(float restoredHp)
        {
            Revive(restoredHp);
        }

        // =====================================================================
        // IINTERACTABLE IMPLEMENTATION (Revive downed teammate)
        // =====================================================================

        public string InteractionPrompt => "[E] Revive Teammate";

        public string GetInteractionPrompt() => InteractionPrompt;

        public bool CanInteract(PlayerController rescuer)
        {
            if (!IsDowned) return false;
            if (rescuer == null || rescuer.gameObject == gameObject) return false;
            if (rescuer.TryGetComponent(out PlayerHealth rescuerHealth) && !rescuerHealth.IsAlive) return false;
            return true;
        }

        public void Interact(PlayerController rescuer)
        {
            if (!CanInteract(rescuer)) return;
            ReviveFromClient();
        }

        // =====================================================================
        // SESSION STATE & GAME OVER EVALUATION
        // =====================================================================

        /// <summary>
        /// Resets health to full and restores Alive state on session start/restart.
        /// </summary>
        public void ResetHealth()
        {
            if (IsNetworkSessionActive)
            {
                if (IsServer)
                {
                    currentHealth.Value = maxHealth;
                    lifeState.Value = PlayerLifeState.Alive;
                    bleedoutRemaining.Value = bleedoutDuration;
                }
            }
            else
            {
                offlineHealth = maxHealth;
                offlineLifeState = PlayerLifeState.Alive;
                OnHealthChanged?.Invoke(offlineHealth, maxHealth);
                OnLifeStateChanged?.Invoke(offlineLifeState);
            }

            CheckAllPlayersDownedCondition();
        }

        private static Coroutine gameOverCoroutine;
        private static PlayerHealth gameOverRunner;

        /// <summary>
        /// Server-authoritative check to verify whether all active survivors are downed or dead.
        /// If all survivors are incapacitated, triggers Game Over after a 1.2s delay.
        /// </summary>
        public static void CheckAllPlayersDownedCondition()
        {
            // Only server or offline host determines game over
            if (IsNetworkSessionActive && NetworkManager.Singleton != null && !NetworkManager.Singleton.IsServer)
            {
                return;
            }

            if (GameManager.Instance == null || !GameManager.Instance.IsPlaying)
            {
                return;
            }

            if (activePlayers.Count == 0)
            {
                return;
            }

            bool allDownedOrDead = true;
            for (int i = 0; i < activePlayers.Count; i++)
            {
                PlayerHealth p = activePlayers[i];
                if (p != null && p.IsAlive)
                {
                    allDownedOrDead = false;
                    break;
                }
            }

            if (allDownedOrDead)
            {
                PlayerHealth runner = null;
                for (int i = 0; i < activePlayers.Count; i++)
                {
                    if (activePlayers[i] != null && activePlayers[i].gameObject.activeInHierarchy)
                    {
                        runner = activePlayers[i];
                        break;
                    }
                }

                if (runner != null && gameOverCoroutine == null)
                {
                    gameOverRunner = runner;
                    gameOverCoroutine = runner.StartCoroutine(DelayedGameOverRoutine());
                }
            }
            else
            {
                if (gameOverCoroutine != null && gameOverRunner != null)
                {
                    gameOverRunner.StopCoroutine(gameOverCoroutine);
                    gameOverCoroutine = null;
                    gameOverRunner = null;
                }
            }
        }

        private static IEnumerator DelayedGameOverRoutine()
        {
            yield return new WaitForSeconds(1.2f);
            gameOverCoroutine = null;
            gameOverRunner = null;

            if (GameManager.Instance == null || !GameManager.Instance.IsPlaying)
            {
                yield break;
            }

            bool allStillDownedOrDead = true;
            for (int i = 0; i < activePlayers.Count; i++)
            {
                PlayerHealth p = activePlayers[i];
                if (p != null && p.IsAlive)
                {
                    allStillDownedOrDead = false;
                    break;
                }
            }

            if (allStillDownedOrDead)
            {
                Debug.Log("<color=red>[PlayerHealth]</color> ALL SURVIVORS ARE DOWNED! Triggering Game Over.");
                GameManager.Instance.NotifyAllPlayersDowned();
            }
        }
    }
}
