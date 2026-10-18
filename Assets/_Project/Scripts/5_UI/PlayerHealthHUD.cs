using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using TheLastSignal.Entities.Player;

namespace TheLastSignal.UI
{
    /// <summary>
    /// In-game Screen HUD for Survivor Health & Vital Signs.
    /// Features:
    /// - Smooth health bar with delayed ghost damage-drop animation.
    /// - Color grading (Healthy Green -> Warning Orange -> Critical Red -> Downed Pulsing Red).
    /// - Numerical HP counter (Current / Max).
    /// - Status badge (STABLE, INJURED, CRITICAL, DOWNED).
    /// - Screen edge damage flash effect when hurt.
    /// - Auto-builds its UI inside HUD_Canvas if not manually wired in inspector.
    /// Strictly visual, adheres to ARCHITECTURE_RULES.md Rule 2.
    /// </summary>
    public class PlayerHealthHUD : MonoBehaviour
    {
        [Header("Manual UI Bindings (Optional - Auto-generates if empty)")]
        [SerializeField] private GameObject hudPanel;
        [SerializeField] private Image healthFillImage;
        [SerializeField] private Image ghostFillImage;
        [SerializeField] private TextMeshProUGUI hpText;
        [SerializeField] private TextMeshProUGUI statusBadgeText;
        [SerializeField] private Image damageFlashOverlay;

        [Header("Color Tuning")]
        [SerializeField] private Color healthyColor = new Color(0.18f, 0.80f, 0.44f, 1f);   // #2ECC71
        [SerializeField] private Color warningColor = new Color(0.95f, 0.61f, 0.07f, 1f);   // #F39C12
        [SerializeField] private Color criticalColor = new Color(0.91f, 0.30f, 0.24f, 1f);  // #E74C3C
        [SerializeField] private Color downedColor = new Color(1.0f, 0.15f, 0.15f, 1f);

        [Header("Animation")]
        [SerializeField] private float ghostLagSpeed = 2.5f;

        private PlayerHealth boundPlayerHealth;
        private Coroutine flashCoroutine;
        private float lastKnownHealth = 100f;

        private void OnEnable()
        {
            PlayerHealth.OnLocalPlayerHealthSpawned += BindToPlayer;
            PlayerHealth.OnLocalPlayerHealthDespawned += UnbindFromPlayer;

            if (PlayerHealth.Local != null)
            {
                BindToPlayer(PlayerHealth.Local);
            }
        }

        private void OnDisable()
        {
            PlayerHealth.OnLocalPlayerHealthSpawned -= BindToPlayer;
            PlayerHealth.OnLocalPlayerHealthDespawned -= UnbindFromPlayer;

            if (boundPlayerHealth != null)
            {
                UnbindFromPlayer(boundPlayerHealth);
            }
        }

        private void Start()
        {
            EnsureUIElements();

            if (boundPlayerHealth == null && PlayerHealth.Local != null)
            {
                BindToPlayer(PlayerHealth.Local);
            }
        }

        private void Update()
        {
            // Smoothly animate ghost fill bar toward current fill
            if (healthFillImage != null && ghostFillImage != null)
            {
                if (ghostFillImage.fillAmount > healthFillImage.fillAmount)
                {
                    ghostFillImage.fillAmount = Mathf.MoveTowards(
                        ghostFillImage.fillAmount,
                        healthFillImage.fillAmount,
                        ghostLagSpeed * Time.deltaTime
                    );
                }
                else
                {
                    ghostFillImage.fillAmount = healthFillImage.fillAmount;
                }
            }

            // Pulsing badge when downed
            if (boundPlayerHealth != null && boundPlayerHealth.IsDowned && statusBadgeText != null)
            {
                float alpha = 0.5f + 0.5f * Mathf.Sin(Time.time * 6f);
                statusBadgeText.color = new Color(downedColor.r, downedColor.g, downedColor.b, alpha);
            }
        }

        public void BindToPlayer(PlayerHealth playerHealth)
        {
            if (playerHealth == null || boundPlayerHealth == playerHealth) return;

            if (boundPlayerHealth != null)
            {
                UnbindFromPlayer(boundPlayerHealth);
            }

            boundPlayerHealth = playerHealth;
            boundPlayerHealth.OnHealthChanged += HandleHealthChanged;
            boundPlayerHealth.OnLifeStateChanged += HandleLifeStateChanged;

            lastKnownHealth = boundPlayerHealth.CurrentHealth;

            EnsureUIElements();
            if (hudPanel != null) hudPanel.SetActive(true);

            UpdateHealthVisuals(boundPlayerHealth.CurrentHealth, boundPlayerHealth.MaxHealth, false);
            UpdateLifeStateVisuals(boundPlayerHealth.LifeState);
        }

        public void UnbindFromPlayer(PlayerHealth playerHealth)
        {
            if (boundPlayerHealth == playerHealth && boundPlayerHealth != null)
            {
                boundPlayerHealth.OnHealthChanged -= HandleHealthChanged;
                boundPlayerHealth.OnLifeStateChanged -= HandleLifeStateChanged;
                boundPlayerHealth = null;
            }
        }

        private void HandleHealthChanged(float current, float max)
        {
            bool tookDamage = current < lastKnownHealth;
            lastKnownHealth = current;

            UpdateHealthVisuals(current, max, tookDamage);
        }

        private void HandleLifeStateChanged(PlayerLifeState state)
        {
            UpdateLifeStateVisuals(state);
        }

        private void UpdateHealthVisuals(float current, float max, bool tookDamage)
        {
            float ratio = max > 0.001f ? Mathf.Clamp01(current / max) : 0f;

            if (healthFillImage != null)
            {
                healthFillImage.fillAmount = ratio;

                if (boundPlayerHealth != null && boundPlayerHealth.IsDowned)
                {
                    healthFillImage.color = downedColor;
                }
                else
                {
                    healthFillImage.color = ratio > 0.5f ? healthyColor : (ratio > 0.25f ? warningColor : criticalColor);
                }
            }

            if (hpText != null)
            {
                int curInt = Mathf.CeilToInt(current);
                int maxInt = Mathf.CeilToInt(max);
                hpText.text = $"<b>{curInt}</b> / {maxInt}";
            }

            if (statusBadgeText != null && (boundPlayerHealth == null || !boundPlayerHealth.IsDowned))
            {
                if (ratio > 0.5f)
                {
                    statusBadgeText.text = "<color=#2ECC71>[STABLE]</color>";
                }
                else if (ratio > 0.25f)
                {
                    statusBadgeText.text = "<color=#F39C12>[WARNING]</color>";
                }
                else if (ratio > 0f)
                {
                    statusBadgeText.text = "<color=#E74C3C>[CRITICAL]</color>";
                }
            }

            if (tookDamage)
            {
                TriggerDamageFlash();
            }
        }

        private void UpdateLifeStateVisuals(PlayerLifeState state)
        {
            if (statusBadgeText == null) return;

            switch (state)
            {
                case PlayerLifeState.Alive:
                    float ratio = boundPlayerHealth != null ? boundPlayerHealth.HealthPercent : 1f;
                    statusBadgeText.text = ratio > 0.5f ? "<color=#2ECC71>[STABLE]</color>" :
                        (ratio > 0.25f ? "<color=#F39C12>[WARNING]</color>" : "<color=#E74C3C>[CRITICAL]</color>");
                    break;

                case PlayerLifeState.Downed:
                    statusBadgeText.text = "<b><color=#FF2222>[DOWNED - NEED REVIVE]</color></b>";
                    if (healthFillImage != null)
                    {
                        healthFillImage.fillAmount = 0f;
                        healthFillImage.color = downedColor;
                    }
                    if (hpText != null)
                    {
                        hpText.text = $"<b>0</b> / {(boundPlayerHealth != null ? (int)boundPlayerHealth.MaxHealth : 100)}";
                    }
                    TriggerDamageFlash(0.6f);
                    break;

                case PlayerLifeState.Dead:
                    statusBadgeText.text = "<b><color=#7F8C8D>[ELIMINATED]</color></b>";
                    if (healthFillImage != null) healthFillImage.fillAmount = 0f;
                    break;
            }
        }

        private void TriggerDamageFlash(float peakAlpha = 0.35f)
        {
            if (damageFlashOverlay == null) return;

            if (flashCoroutine != null) StopCoroutine(flashCoroutine);
            flashCoroutine = StartCoroutine(DamageFlashRoutine(peakAlpha));
        }

        private IEnumerator DamageFlashRoutine(float peakAlpha)
        {
            damageFlashOverlay.gameObject.SetActive(true);
            damageFlashOverlay.color = new Color(0.8f, 0.05f, 0.05f, peakAlpha);

            float elapsed = 0f;
            float duration = 0.35f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float alpha = Mathf.Lerp(peakAlpha, 0f, elapsed / duration);
                damageFlashOverlay.color = new Color(0.8f, 0.05f, 0.05f, alpha);
                yield return null;
            }

            damageFlashOverlay.color = Color.clear;
            damageFlashOverlay.gameObject.SetActive(false);
        }

        // =====================================================================
        // AUTO-GENERATION OF HUD UI
        // =====================================================================

        private void EnsureUIElements()
        {
            if (hudPanel != null && healthFillImage != null && hpText != null)
            {
                return;
            }

            // Find parent Canvas (preferably HUD_Canvas or any active Canvas)
            Canvas parentCanvas = GetComponentInParent<Canvas>();
            if (parentCanvas == null)
            {
                GameObject canvasObj = GameObject.Find("HUD_Canvas");
                if (canvasObj != null) parentCanvas = canvasObj.GetComponent<Canvas>();
            }

            if (parentCanvas == null)
            {
                parentCanvas = FindAnyObjectByType<Canvas>();
            }

            if (parentCanvas == null) return;

            Transform targetParent = parentCanvas.transform;

            // Damage Flash Overlay (Full Screen)
            if (damageFlashOverlay == null)
            {
                GameObject flashObj = new GameObject("Screen_Damage_Flash");
                flashObj.transform.SetParent(targetParent, false);
                flashObj.transform.SetAsFirstSibling(); // Underneath text
                damageFlashOverlay = flashObj.AddComponent<Image>();
                damageFlashOverlay.color = Color.clear;
                damageFlashOverlay.raycastTarget = false;

                RectTransform flashRect = flashObj.GetComponent<RectTransform>();
                flashRect.anchorMin = Vector2.zero;
                flashRect.anchorMax = Vector2.one;
                flashRect.sizeDelta = Vector2.zero;
                flashObj.SetActive(false);
            }

            // Main Health Panel (Bottom-Left)
            if (hudPanel == null)
            {
                hudPanel = new GameObject("Health_HUD_Panel");
                hudPanel.transform.SetParent(targetParent, false);

                RectTransform panelRect = hudPanel.AddComponent<RectTransform>();
                panelRect.anchorMin = new Vector2(0f, 0f);
                panelRect.anchorMax = new Vector2(0f, 0f);
                panelRect.pivot = new Vector2(0f, 0f);
                panelRect.anchoredPosition = new Vector2(30f, 30f);
                panelRect.sizeDelta = new Vector2(280f, 76f);

                Image panelBg = hudPanel.AddComponent<Image>();
                panelBg.color = new Color(0.06f, 0.08f, 0.11f, 0.88f);

                // Top Header Row: Icon + "VITAL SIGNS" + Status Badge
                GameObject headerRow = new GameObject("Header_Row");
                headerRow.transform.SetParent(hudPanel.transform, false);
                RectTransform headerRect = headerRow.AddComponent<RectTransform>();
                headerRect.anchorMin = new Vector2(0f, 1f);
                headerRect.anchorMax = new Vector2(1f, 1f);
                headerRect.pivot = new Vector2(0.5f, 1f);
                headerRect.anchoredPosition = new Vector2(0f, -8f);
                headerRect.sizeDelta = new Vector2(-24f, 20f);

                // Header Title
                GameObject titleObj = new GameObject("Title_Text");
                titleObj.transform.SetParent(headerRow.transform, false);
                TextMeshProUGUI titleText = titleObj.AddComponent<TextMeshProUGUI>();
                titleText.text = "<color=#00E5FF>SURVIVOR</color> // VITAL SIGNS";
                titleText.fontSize = 13;
                titleText.fontStyle = FontStyles.Bold;
                titleText.alignment = TextAlignmentOptions.Left;
                RectTransform titleRect = titleObj.GetComponent<RectTransform>();
                titleRect.anchorMin = new Vector2(0f, 0f);
                titleRect.anchorMax = new Vector2(0.6f, 1f);
                titleRect.sizeDelta = Vector2.zero;

                // Status Badge Text
                GameObject badgeObj = new GameObject("Status_Badge");
                badgeObj.transform.SetParent(headerRow.transform, false);
                statusBadgeText = badgeObj.AddComponent<TextMeshProUGUI>();
                statusBadgeText.text = "<color=#2ECC71>[STABLE]</color>";
                statusBadgeText.fontSize = 12;
                statusBadgeText.fontStyle = FontStyles.Bold;
                statusBadgeText.alignment = TextAlignmentOptions.Right;
                RectTransform badgeRect = badgeObj.GetComponent<RectTransform>();
                badgeRect.anchorMin = new Vector2(0.6f, 0f);
                badgeRect.anchorMax = new Vector2(1f, 1f);
                badgeRect.sizeDelta = Vector2.zero;

                // Bar Background Track
                GameObject barTrackObj = new GameObject("Health_Bar_Track");
                barTrackObj.transform.SetParent(hudPanel.transform, false);
                Image trackImage = barTrackObj.AddComponent<Image>();
                trackImage.color = new Color(0.15f, 0.08f, 0.08f, 0.95f);
                RectTransform trackRect = barTrackObj.GetComponent<RectTransform>();
                trackRect.anchorMin = new Vector2(0f, 0f);
                trackRect.anchorMax = new Vector2(1f, 0f);
                trackRect.pivot = new Vector2(0.5f, 0f);
                trackRect.anchoredPosition = new Vector2(0f, 12f);
                trackRect.sizeDelta = new Vector2(-24f, 24f);

                // Ghost Damage Fill Bar
                GameObject ghostObj = new GameObject("Ghost_Fill");
                ghostObj.transform.SetParent(barTrackObj.transform, false);
                ghostFillImage = ghostObj.AddComponent<Image>();
                ghostFillImage.type = Image.Type.Filled;
                ghostFillImage.fillMethod = Image.FillMethod.Horizontal;
                ghostFillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
                ghostFillImage.fillAmount = 1f;
                ghostFillImage.color = new Color(0.9f, 0.2f, 0.2f, 0.5f);
                RectTransform ghostRect = ghostObj.GetComponent<RectTransform>();
                ghostRect.anchorMin = Vector2.zero;
                ghostRect.anchorMax = Vector2.one;
                ghostRect.sizeDelta = Vector2.zero;

                // Main Health Fill Bar
                GameObject fillObj = new GameObject("Health_Fill");
                fillObj.transform.SetParent(barTrackObj.transform, false);
                healthFillImage = fillObj.AddComponent<Image>();
                healthFillImage.type = Image.Type.Filled;
                healthFillImage.fillMethod = Image.FillMethod.Horizontal;
                healthFillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
                healthFillImage.fillAmount = 1f;
                healthFillImage.color = healthyColor;
                RectTransform fillRect = fillObj.GetComponent<RectTransform>();
                fillRect.anchorMin = Vector2.zero;
                fillRect.anchorMax = Vector2.one;
                fillRect.sizeDelta = Vector2.zero;

                // HP Number Text Overlay (Centered over the bar)
                GameObject hpTextObj = new GameObject("HP_Text");
                hpTextObj.transform.SetParent(barTrackObj.transform, false);
                hpText = hpTextObj.AddComponent<TextMeshProUGUI>();
                hpText.text = "<b>100</b> / 100";
                hpText.fontSize = 14;
                hpText.fontStyle = FontStyles.Bold;
                hpText.alignment = TextAlignmentOptions.Center;
                hpText.color = Color.white;
                RectTransform hpTextRect = hpTextObj.GetComponent<RectTransform>();
                hpTextRect.anchorMin = Vector2.zero;
                hpTextRect.anchorMax = Vector2.one;
                hpTextRect.sizeDelta = Vector2.zero;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InitializeBootstrap()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += (scene, mode) =>
            {
                EnsureHUDInActiveScene();
            };
            EnsureHUDInActiveScene();
        }

        private static void EnsureHUDInActiveScene()
        {
            if (FindAnyObjectByType<PlayerHealthHUD>() == null)
            {
                GameObject canvasObj = GameObject.Find("HUD_Canvas");
                if (canvasObj == null)
                {
                    Canvas c = FindAnyObjectByType<Canvas>();
                    if (c != null) canvasObj = c.gameObject;
                }

                if (canvasObj != null)
                {
                    canvasObj.AddComponent<PlayerHealthHUD>();
                }
            }
        }
    }
}
