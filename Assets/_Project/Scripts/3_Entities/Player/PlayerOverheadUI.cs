using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace TheLastSignal.Entities.Player
{
    /// <summary>
    /// Displays an overhead world-space mini health bar and status indicator above each Survivor.
    /// Visible to all players in the room, showing teammate health and downed status.
    /// Purely visual view component, adheres to ARCHITECTURE_RULES.md Rule 2.
    /// </summary>
    [RequireComponent(typeof(PlayerHealth))]
    public class PlayerOverheadUI : MonoBehaviour
    {
        [Header("Overhead Positioning")]
        [SerializeField] private Vector3 offset = new Vector3(0f, 0.95f, 0f);

        [Header("Colors")]
        [SerializeField] private Color healthyColor = new Color(0.18f, 0.8f, 0.44f, 1f);   // Green
        [SerializeField] private Color injuredColor = new Color(0.95f, 0.61f, 0.07f, 1f);  // Orange
        [SerializeField] private Color criticalColor = new Color(0.91f, 0.30f, 0.24f, 1f); // Red
        [SerializeField] private Color downedColor = new Color(1.0f, 0.15f, 0.15f, 1f);   // Bright Pulsing Red

        private PlayerHealth playerHealth;
        private PlayerController playerController;

        private GameObject canvasObj;
        private Image fillBarImage;
        private TextMeshProUGUI statusLabel;
        private RectTransform barRect;

        private void Awake()
        {
            playerHealth = GetComponent<PlayerHealth>();
            playerController = GetComponent<PlayerController>();

            BuildOverheadUI();
        }

        private void OnEnable()
        {
            if (playerHealth != null)
            {
                playerHealth.OnHealthChanged += HandleHealthChanged;
                playerHealth.OnLifeStateChanged += HandleLifeStateChanged;
            }
        }

        private void OnDisable()
        {
            if (playerHealth != null)
            {
                playerHealth.OnHealthChanged -= HandleHealthChanged;
                playerHealth.OnLifeStateChanged -= HandleLifeStateChanged;
            }
        }

        private void LateUpdate()
        {
            if (canvasObj == null) return;

            // Follow player position without inheriting rotation or flipping
            canvasObj.transform.position = transform.position + offset;
            canvasObj.transform.rotation = Quaternion.identity;

            // Pulse effect when downed
            if (playerHealth != null && playerHealth.IsDowned && statusLabel != null)
            {
                float alpha = 0.5f + 0.5f * Mathf.Sin(Time.time * 6f);
                statusLabel.color = new Color(downedColor.r, downedColor.g, downedColor.b, alpha);
            }
        }

        private void HandleHealthChanged(float current, float max)
        {
            if (fillBarImage == null) return;

            float ratio = max > 0.001f ? Mathf.Clamp01(current / max) : 0f;
            fillBarImage.fillAmount = ratio;

            if (playerHealth != null && playerHealth.IsDowned)
            {
                fillBarImage.color = downedColor;
            }
            else
            {
                fillBarImage.color = ratio > 0.5f ? healthyColor : (ratio > 0.25f ? injuredColor : criticalColor);
            }
        }

        private void HandleLifeStateChanged(PlayerLifeState state)
        {
            if (statusLabel == null) return;

            switch (state)
            {
                case PlayerLifeState.Alive:
                    string nameLabel = playerController != null ? playerController.gameObject.name : "Survivor";
                    statusLabel.text = nameLabel;
                    statusLabel.color = Color.white;
                    if (fillBarImage != null)
                    {
                        float ratio = playerHealth != null ? playerHealth.HealthPercent : 1f;
                        fillBarImage.color = ratio > 0.5f ? healthyColor : (ratio > 0.25f ? injuredColor : criticalColor);
                    }
                    break;

                case PlayerLifeState.Downed:
                    statusLabel.text = "<b><color=#FF2222>[DOWNED]</color></b>";
                    if (fillBarImage != null)
                    {
                        fillBarImage.fillAmount = 0f;
                        fillBarImage.color = downedColor;
                    }
                    break;

                case PlayerLifeState.Dead:
                    statusLabel.text = "<b><color=#888888>[DEAD]</color></b>";
                    if (fillBarImage != null) fillBarImage.fillAmount = 0f;
                    break;
            }
        }

        private void BuildOverheadUI()
        {
            // Create root World-Space Canvas
            canvasObj = new GameObject("Overhead_Health_Canvas");
            canvasObj.transform.SetParent(null); // Keep at root to decouple rotation/scale
            canvasObj.transform.position = transform.position + offset;
            canvasObj.transform.localScale = Vector3.one * 0.015f; // scale down canvas pixels to world units

            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 20; // Above characters and obstacles

            RectTransform canvasRect = canvasObj.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(80f, 24f);

            // Background Bar
            GameObject bgObj = new GameObject("Bar_BG");
            bgObj.transform.SetParent(canvasObj.transform, false);
            Image bgImage = bgObj.AddComponent<Image>();
            bgImage.color = new Color(0.1f, 0.12f, 0.15f, 0.85f);
            RectTransform bgRect = bgObj.GetComponent<RectTransform>();
            bgRect.anchorMin = new Vector2(0.5f, 0.5f);
            bgRect.anchorMax = new Vector2(0.5f, 0.5f);
            bgRect.anchoredPosition = new Vector2(0f, -4f);
            bgRect.sizeDelta = new Vector2(64f, 7f);

            // Fill Bar
            GameObject fillObj = new GameObject("Bar_Fill");
            fillObj.transform.SetParent(bgObj.transform, false);
            fillBarImage = fillObj.AddComponent<Image>();
            fillBarImage.type = Image.Type.Filled;
            fillBarImage.fillMethod = Image.FillMethod.Horizontal;
            fillBarImage.fillOrigin = (int)Image.OriginHorizontal.Left;
            fillBarImage.fillAmount = 1f;
            fillBarImage.color = healthyColor;

            RectTransform fillRect = fillObj.GetComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.sizeDelta = Vector2.zero;

            // Status Label
            GameObject labelObj = new GameObject("Status_Label");
            labelObj.transform.SetParent(canvasObj.transform, false);
            statusLabel = labelObj.AddComponent<TextMeshProUGUI>();
            statusLabel.alignment = TextAlignmentOptions.Center;
            statusLabel.fontSize = 11;
            statusLabel.fontStyle = FontStyles.Bold;
            statusLabel.text = playerController != null ? playerController.gameObject.name : "P1";
            statusLabel.color = Color.white;

            RectTransform labelRect = labelObj.GetComponent<RectTransform>();
            labelRect.anchorMin = new Vector2(0.5f, 0.5f);
            labelRect.anchorMax = new Vector2(0.5f, 0.5f);
            labelRect.anchoredPosition = new Vector2(0f, 7f);
            labelRect.sizeDelta = new Vector2(100f, 14f);
        }

        private void OnDestroy()
        {
            if (canvasObj != null)
            {
                Destroy(canvasObj);
            }
        }
    }
}
