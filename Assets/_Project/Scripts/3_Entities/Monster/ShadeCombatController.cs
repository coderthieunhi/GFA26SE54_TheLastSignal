using System;
using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using TheLastSignal.Entities.Player;
using TheLastSignal.Systems.Lighting;

namespace TheLastSignal.Entities.Monster
{
    /// <summary>
    /// Core combat controller for The Shade (Monster / Apex Predator role).
    /// Features:
    /// 1. Claw Slash [M1 / F]: 50 DMG (exactly 2 hits to down a 100 HP Survivor), 0.7s cooldown, 1.8m strike arc.
    /// 2. Shadow Dash [M2 / SPACE]: 4.5m rapid dash in 0.18s, 6.0s cooldown, obstacle collision raycasting.
    /// 3. Lighting & Stun integration: 125% speed in darkness, 70% speed under light, 0% speed while EMP stunned.
    /// 4. Server-authoritative with offline single-player fallback.
    /// 5. Debug Hotkey [F7 / F8] to toggle The Shade role instantly in editor / solo practice.
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    public class ShadeCombatController : NetworkBehaviour
    {
        private static bool IsNetworkSessionActive =>
            NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;

        [Header("Claw Slash Tunables (2 Hits to Down)")]
        [SerializeField] private float slashDamage = 50.0f;
        [SerializeField] private float slashCooldown = 0.7f;
        [SerializeField] private float slashRange = 1.8f;

        [Header("Shadow Dash Tunables")]
        [SerializeField] private float dashCooldown = 6.0f;
        [SerializeField] private float dashDistance = 4.5f;
        [SerializeField] private float dashDuration = 0.18f;

        [Header("Layer Masks")]
        [SerializeField] private LayerMask obstacleLayer;

        private PlayerController playerController;
        private PlayerHealth playerHealth;
        private ShadeStunState stunState;
        private ShadeVisualView visualView;

        private float slashTimer = 0f;
        private float dashTimer = 0f;
        private bool isDashing = false;
        private bool isExposedToLight = false;
        private PlayerHealth nearestSurvivor = null;

        // Public accessors for HUD
        public float SlashDamage => slashDamage;
        public float SlashCooldown => slashCooldown;
        public float SlashCooldownRemaining => Mathf.Max(0f, slashTimer);
        public float DashCooldown => dashCooldown;
        public float DashCooldownRemaining => Mathf.Max(0f, dashTimer);
        public float DashCooldownPercent => Mathf.Clamp01(dashTimer / dashCooldown);
        public bool IsDashing => isDashing;
        public bool IsExposedToLight => isExposedToLight;
        public bool IsStunned => stunState != null && stunState.IsStunned;
        public PlayerHealth NearestSurvivorInRange => nearestSurvivor;

        public event Action OnSlashPerformed;
        public event Action OnDashPerformed;

        private void Awake()
        {
            playerController = GetComponent<PlayerController>();
            playerHealth = GetComponent<PlayerHealth>();
            stunState = GetComponent<ShadeStunState>();

            visualView = GetComponent<ShadeVisualView>();
            if (visualView == null)
            {
                visualView = gameObject.AddComponent<ShadeVisualView>();
            }

            obstacleLayer = LayerMask.GetMask("Obstacle", "Default");
        }

        private void Update()
        {
            // Debug toggle hotkey: F7 or F8 toggles The Shade on local player
            CheckDebugToggleHotkey();

            // Only active when playing as The Shade
            if (playerController == null || !playerController.IsMonster)
            {
                return;
            }

            // Update timers
            if (slashTimer > 0f) slashTimer -= Time.deltaTime;
            if (dashTimer > 0f) dashTimer -= Time.deltaTime;

            // Update nearest target detection
            UpdateTargetDetection();

            // Authority check for local input and lighting speed adjustments
            if (IsNetworkSessionActive && !IsOwner)
            {
                return;
            }

            // Light exposure & speed debuff evaluation
            UpdateLightExposureSpeed();

            // Stun & LifeState guard
            if (IsStunned || (playerHealth != null && !playerHealth.IsAlive))
            {
                return;
            }

            // Read attack & dash inputs
            ReadCombatInputs();
        }

        private void CheckDebugToggleHotkey()
        {
            Keyboard kb = Keyboard.current;
            if (kb == null) return;

            if (kb.f7Key.wasPressedThisFrame || kb.f8Key.wasPressedThisFrame)
            {
                playerController.IsMonster = !playerController.IsMonster;
                visualView.SetShadeAppearance(playerController.IsMonster);

                string mode = playerController.IsMonster ? "<color=red>THE SHADE (PREDATOR)</color>" : "<color=green>SURVIVOR</color>";
                Debug.Log($"<color=yellow>[ShadeCombatController]</color> Player role toggled to: {mode}!");
            }
        }

        private void UpdateLightExposureSpeed()
        {
            if (IsStunned)
            {
                playerController.SpeedMultiplier = 0f;
                return;
            }

            if (LightingController.Current != null)
            {
                isExposedToLight = LightingController.Current.IsLit(transform.position);
            }
            else
            {
                isExposedToLight = false;
            }

            // 1.25x predator speed in shadows, 0.70x slowed under light (-30% debuff)
            playerController.SpeedMultiplier = isExposedToLight ? 0.70f : 1.25f;
        }

        private void ReadCombatInputs()
        {
            Mouse mouse = Mouse.current;
            Keyboard kb = Keyboard.current;

            // 1. Claw Slash [Left Mouse / F Key]
            bool slashPressed = (mouse != null && mouse.leftButton.wasPressedThisFrame) ||
                                (kb != null && kb.fKey.wasPressedThisFrame);

            if (slashPressed && slashTimer <= 0f && !isDashing)
            {
                ExecuteClawSlash();
            }

            // 2. Shadow Dash [Right Mouse / Space Key]
            bool dashPressed = (mouse != null && mouse.rightButton.wasPressedThisFrame) ||
                               (kb != null && kb.spaceKey.wasPressedThisFrame);

            if (dashPressed && dashTimer <= 0f && !isDashing)
            {
                ExecuteShadowDash();
            }
        }

        private Vector2 GetAimDirection()
        {
            UnityEngine.Camera cam = UnityEngine.Camera.main;
            if (cam != null && Mouse.current != null)
            {
                Vector2 mouseScreen = Mouse.current.position.ReadValue();
                Vector3 mouseWorld = cam.ScreenToWorldPoint(new Vector3(mouseScreen.x, mouseScreen.y, -cam.transform.position.z));
                Vector2 dir = ((Vector2)mouseWorld - (Vector2)transform.position).normalized;
                if (dir.sqrMagnitude > 0.01f) return dir;
            }

            return playerController.FacingDirection.sqrMagnitude > 0.01f ? playerController.FacingDirection : Vector2.down;
        }

        // =====================================================================
        // SKILL 1: CLAW SLASH (50 DMG - 2 HITS TO DOWN)
        // =====================================================================

        private void ExecuteClawSlash()
        {
            slashTimer = slashCooldown;
            Vector2 origin = transform.position;
            Vector2 aimDir = GetAimDirection();

            OnSlashPerformed?.Invoke();

            if (IsNetworkSessionActive)
            {
                PerformClawSlashServerRpc(origin, aimDir);
            }
            else
            {
                ResolveClawSlashHit(origin, aimDir);
            }
        }

        [ServerRpc]
        private void PerformClawSlashServerRpc(Vector2 origin, Vector2 aimDir)
        {
            ResolveClawSlashHit(origin, aimDir);
        }

        private void ResolveClawSlashHit(Vector2 origin, Vector2 aimDir)
        {
            Vector2 attackCenter = origin + (aimDir * (slashRange * 0.55f));
            Collider2D[] hits = Physics2D.OverlapCircleAll(attackCenter, slashRange * 0.65f);

            bool hitAnySurvivor = false;
            for (int i = 0; i < hits.Length; i++)
            {
                Collider2D col = hits[i];
                if (col.gameObject == gameObject) continue;

                PlayerHealth targetHealth = col.GetComponent<PlayerHealth>();
                if (targetHealth == null)
                {
                    targetHealth = col.GetComponentInParent<PlayerHealth>();
                }

                if (targetHealth != null && targetHealth.IsAlive)
                {
                    // Ignore fellow monster
                    if (targetHealth.TryGetComponent(out PlayerController targetCtrl) && targetCtrl.IsMonster)
                    {
                        continue;
                    }

                    targetHealth.TakeDamage(slashDamage);
                    hitAnySurvivor = true;
                    Debug.Log($"<color=red>[The Shade]</color> Claw Slash HIT {targetHealth.name} for {slashDamage} DMG! [HP Remaining: {targetHealth.CurrentHealth:0}/{targetHealth.MaxHealth:0}]");
                }
            }

            // Broadcast visual and sound effect to all clients
            if (IsNetworkSessionActive)
            {
                PlaySlashFxClientRpc(attackCenter, aimDir, hitAnySurvivor);
            }
            else
            {
                if (visualView != null)
                {
                    visualView.PlaySlashEffect(attackCenter, aimDir, hitAnySurvivor);
                }
            }
        }

        [ClientRpc]
        private void PlaySlashFxClientRpc(Vector2 position, Vector2 aimDir, bool hitSurvivor)
        {
            if (visualView != null)
            {
                visualView.PlaySlashEffect(position, aimDir, hitSurvivor);
            }
        }

        // =====================================================================
        // SKILL 2: SHADOW DASH (4.5m BURST TRANSLATION - 6.0s COOLDOWN)
        // =====================================================================

        private void ExecuteShadowDash()
        {
            dashTimer = dashCooldown;
            Vector2 startPos = transform.position;
            Vector2 dashDir = GetAimDirection();

            OnDashPerformed?.Invoke();

            if (IsNetworkSessionActive)
            {
                PerformShadowDashServerRpc(startPos, dashDir);
            }
            else
            {
                StartCoroutine(PerformDashCoroutine(startPos, dashDir));
            }
        }

        [ServerRpc]
        private void PerformShadowDashServerRpc(Vector2 startPos, Vector2 dashDir)
        {
            // Server calculates end position and verifies obstacle raycast
            RaycastHit2D hit = Physics2D.Raycast(startPos, dashDir, dashDistance, obstacleLayer);
            float actualDistance = hit.collider != null ? Mathf.Max(0.5f, hit.distance - 0.45f) : dashDistance;
            Vector2 targetPos = startPos + (dashDir * actualDistance);

            // Broadcast dash FX to all peers
            PlayDashFxClientRpc(startPos, targetPos);

            // Execute translation on Server
            StartCoroutine(PerformDashCoroutine(startPos, dashDir));
        }

        [ClientRpc]
        private void PlayDashFxClientRpc(Vector2 startPos, Vector2 endPos)
        {
            if (visualView != null)
            {
                visualView.PlayDashEffect(startPos, endPos);
            }
        }

        private IEnumerator PerformDashCoroutine(Vector2 startPos, Vector2 dashDir)
        {
            isDashing = true;

            // Raycast check so we never clip through outer walls
            RaycastHit2D hit = Physics2D.Raycast(startPos, dashDir, dashDistance, obstacleLayer);
            float actualDist = hit.collider != null ? Mathf.Max(0.5f, hit.distance - 0.45f) : dashDistance;
            Vector2 targetPos = startPos + (dashDir * actualDist);

            if (!IsNetworkSessionActive && visualView != null)
            {
                visualView.PlayDashEffect(startPos, targetPos);
            }

            Rigidbody2D rb = GetComponent<Rigidbody2D>();
            float elapsed = 0f;

            while (elapsed < dashDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / dashDuration;
                // Ease-out quadratic curve for snappy start and smooth deceleration
                float curve = 1f - (1f - t) * (1f - t);
                Vector2 newPos = Vector2.Lerp(startPos, targetPos, curve);

                if (rb != null)
                {
                    rb.MovePosition(newPos);
                }
                else
                {
                    transform.position = newPos;
                }

                yield return null;
            }

            if (rb != null) rb.MovePosition(targetPos);
            else transform.position = targetPos;

            isDashing = false;
        }

        // =====================================================================
        // TARGET PERCEPTION (FOR HUD RETICLE FEEDBACK)
        // =====================================================================

        private void UpdateTargetDetection()
        {
            nearestSurvivor = null;
            float nearestDistSqr = slashRange * slashRange;
            Vector2 myPos = transform.position;

            var activePlayers = PlayerHealth.ActivePlayers;
            for (int i = 0; i < activePlayers.Count; i++)
            {
                PlayerHealth pHealth = activePlayers[i];
                if (pHealth == null || !pHealth.IsAlive || pHealth.gameObject == gameObject) continue;

                if (pHealth.TryGetComponent(out PlayerController targetCtrl) && targetCtrl.IsMonster)
                {
                    continue; // Skip fellow monster
                }

                float distSqr = ((Vector2)pHealth.transform.position - myPos).sqrMagnitude;
                if (distSqr <= nearestDistSqr)
                {
                    nearestDistSqr = distSqr;
                    nearestSurvivor = pHealth;
                }
            }
        }
    }
}
