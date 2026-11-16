using UnityEngine;
using UnityEngine.UI;
using TMPro;
using TheLastSignal.Core.Bootstrap;
using TheLastSignal.Entities.Player;
using TheLastSignal.Entities.Monster;

namespace TheLastSignal.UI
{
    /// <summary>
    /// Brutalist Sci-Fi HUD Overlay for The Shade (Apex Predator role).
    /// Features:
    /// - Only visible when the local player is acting as The Shade.
    /// - Dynamic strike reticle: turns bright red with '[STRIKE RANGE]' when a Survivor is within 1.8m.
    /// - 2 Simple Skill cards: [M1/F] Claw Slash (50 DMG) & [M2/Space] Shadow Dash (6s cooldown).
    /// - Environmental light exposure and EMP stun status display.
    /// - F7/F8 toggle hotkey hint for instant editor testing.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class ShadeHUDOverlay : MonoBehaviour
    {
        public static ShadeHUDOverlay Instance { get; private set; }

        [Header("Telemetry Labels")]
        [SerializeField] private TextMeshProUGUI headerTitleText;
        [SerializeField] private TextMeshProUGUI statusText;

        [Header("The Shade Health Display")]
        [SerializeField] private Image shadeHealthFill;
        [SerializeField] private TextMeshProUGUI shadeHpText;

        [Header("Skill Displays")]
        [SerializeField] private TextMeshProUGUI slashSkillText;
        [SerializeField] private TextMeshProUGUI dashSkillText;
        [SerializeField] private Image dashCooldownFill;

        [Header("Root")]
        [SerializeField] private GameObject overlayRoot;

        private PlayerController localPlayer;
        private ShadeCombatController combatController;
        private static Sprite cachedSolidWhite;

        private void Awake()
        {
            Instance = this;

            // Dedicated Server safety guard
            if (GameBootstrap.CurrentMode == LaunchMode.DedicatedServer)
            {
                gameObject.SetActive(false);
                return;
            }

            EnsureCanvasAndHierarchy();
            if (overlayRoot != null) overlayRoot.SetActive(false);
        }

        private void Update()
        {
            // Find local player and combat controller if not cached
            if (localPlayer == null)
            {
                localPlayer = PlayerController.Local;
                if (localPlayer != null)
                {
                    combatController = localPlayer.GetComponent<ShadeCombatController>();
                }
            }

            // Only show HUD if local player is The Shade
            bool isShade = localPlayer != null && localPlayer.IsMonster;
            if (overlayRoot != null && overlayRoot.activeSelf != isShade)
            {
                overlayRoot.SetActive(isShade);
            }

            if (!isShade || combatController == null)
            {
                return;
            }

            // 1. Update The Shade Health Bar
            PlayerHealth localHealth = localPlayer != null ? localPlayer.GetComponent<PlayerHealth>() : null;
            if (localHealth != null)
            {
                if (shadeHealthFill != null)
                {
                    shadeHealthFill.fillAmount = localHealth.HealthPercent;
                }
                if (shadeHpText != null)
                {
                    shadeHpText.text = $"<b>{(int)localHealth.CurrentHealth}</b> / {(int)localHealth.MaxHealth} HP";
                }
            }

            // 2. Strike Range Prompt in Skill Card
            bool inStrikeRange = combatController.NearestSurvivorInRange != null;

            // 3. Update Environmental Status
            if (statusText != null)
            {
                if (combatController.IsStunned)
                {
                    statusText.text = "<b><color=#00D4FF>[EMP STUN] DISRUPTED // CANNOT MOVE OR ATTACK</color></b>";
                }
                else if (combatController.IsExposedToLight)
                {
                    statusText.text = "<b><color=#FFAA00>[EXPOSED] LIGHT HAZARD // -30% SPEED DEBUFF</color></b>";
                }
                else
                {
                    statusText.text = "<b><color=#FF2244>[SHADOWS] APEX PREDATOR // +25% SPEED BOOST</color></b>";
                }
            }

            // 3. Update Skill 1 (Claw Slash)
            if (slashSkillText != null)
            {
                if (combatController.SlashCooldownRemaining > 0f)
                {
                    slashSkillText.text = $"<b>[M1 / F] CLAW SLASH</b>  <color=#FFAA00>[{combatController.SlashCooldownRemaining:0.0}s]</color>";
                }
                else
                {
                    slashSkillText.text = "<b>[M1 / F] CLAW SLASH</b>  <color=#FF2233>[READY - 50 DMG]</color>";
                }
            }

            // 4. Update Skill 2 (Shadow Dash)
            if (dashSkillText != null)
            {
                if (combatController.DashCooldownRemaining > 0f)
                {
                    dashSkillText.text = $"<b>[M2 / SPACE] SHADOW DASH</b>  <color=#FFAA00>[{combatController.DashCooldownRemaining:0.0}s]</color>";
                }
                else
                {
                    dashSkillText.text = "<b>[M2 / SPACE] SHADOW DASH</b>  <color=#00FFCC>[READY - 4.5m]</color>";
                }
            }

            if (dashCooldownFill != null)
            {
                dashCooldownFill.fillAmount = combatController.DashCooldownPercent;
            }
        }

        // =====================================================================
        // FULLSCREEN PROCEDURAL UI LAYOUT
        // =====================================================================

        private void EnsureCanvasAndHierarchy()
        {
            if (transform.parent != null)
            {
                transform.SetParent(null, false);
            }

            // Ensure ScreenSpaceOverlay Canvas
            Canvas canvas = GetComponent<Canvas>();
            if (canvas == null) canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 96; // Render just above Survivor HUD

            CanvasScaler scaler = GetComponent<CanvasScaler>();
            if (scaler == null) scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            if (GetComponent<GraphicRaycaster>() == null) gameObject.AddComponent<GraphicRaycaster>();

            // NOTE: Do not touch root RectTransform offsets/anchors. CanvasScaler manages it.

            // If overlayRoot already exists and has children, preserve it
            if (overlayRoot != null && overlayRoot.transform.childCount > 0)
            {
                RectTransform rootRt = overlayRoot.GetComponent<RectTransform>();
                if (rootRt != null)
                {
                    rootRt.anchorMin = Vector2.zero;
                    rootRt.anchorMax = Vector2.one;
                    rootRt.offsetMin = Vector2.zero;
                    rootRt.offsetMax = Vector2.zero;
                    rootRt.pivot = new Vector2(0.5f, 0.5f);
                }

                // Bind references if not serialized
                if (shadeHealthFill == null)
                {
                    Transform t = overlayRoot.transform.Find("TopBar/Shade_Health_Bar_Container/HP_Track/HP_Fill");
                    if (t != null) shadeHealthFill = t.GetComponent<Image>();
                }
                if (shadeHpText == null)
                {
                    Transform t = overlayRoot.transform.Find("TopBar/Shade_Health_Bar_Container/HP_Text");
                    if (t != null) shadeHpText = t.GetComponent<TextMeshProUGUI>();
                }
                if (statusText == null)
                {
                    Transform t = overlayRoot.transform.Find("BottomBar/StatusText");
                    if (t != null) statusText = t.GetComponent<TextMeshProUGUI>();
                }
                if (slashSkillText == null)
                {
                    Transform t = overlayRoot.transform.Find("BottomBar/SlashSkillText");
                    if (t != null) slashSkillText = t.GetComponent<TextMeshProUGUI>();
                }
                if (dashSkillText == null)
                {
                    Transform t = overlayRoot.transform.Find("BottomBar/DashSkillText");
                    if (t != null) dashSkillText = t.GetComponent<TextMeshProUGUI>();
                }

                Canvas.ForceUpdateCanvases();
                return;
            }

            // Clear old children if any
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                DestroyImmediate(transform.GetChild(i).gameObject);
            }

            BuildProceduralShadeHUD();
            Canvas.ForceUpdateCanvases();
        }

        private void BuildProceduralShadeHUD()
        {
            overlayRoot = new GameObject("ShadeOverlayRoot", typeof(RectTransform));
            overlayRoot.transform.SetParent(transform, false);
            RectTransform rootRt = overlayRoot.GetComponent<RectTransform>();
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.offsetMin = Vector2.zero;
            rootRt.offsetMax = Vector2.zero;

            Sprite white = GetSolidWhiteSprite();

            // 1. Top Header Bar
            GameObject topBar = new GameObject("TopBar", typeof(RectTransform), typeof(Image));
            topBar.transform.SetParent(overlayRoot.transform, false);
            RectTransform topRt = topBar.GetComponent<RectTransform>();
            topRt.anchorMin = new Vector2(0f, 1f);
            topRt.anchorMax = new Vector2(1f, 1f);
            topRt.pivot = new Vector2(0.5f, 1f);
            topRt.sizeDelta = new Vector2(0f, 58f);
            Image topBg = topBar.GetComponent<Image>();
            topBg.color = new Color(0.08f, 0.04f, 0.05f, 0.90f);

            // Red accent border
            GameObject border = new GameObject("BorderLine", typeof(RectTransform), typeof(Image));
            border.transform.SetParent(topBar.transform, false);
            RectTransform bRt = border.GetComponent<RectTransform>();
            bRt.anchorMin = new Vector2(0f, 0f);
            bRt.anchorMax = new Vector2(1f, 0f);
            bRt.sizeDelta = new Vector2(0f, 2f);
            Image bImg = border.GetComponent<Image>();
            bImg.color = new Color(1.0f, 0.15f, 0.20f, 0.75f);

            // Header Title
            GameObject titleGo = new GameObject("HeaderTitle", typeof(RectTransform), typeof(TextMeshProUGUI));
            titleGo.transform.SetParent(topBar.transform, false);
            headerTitleText = titleGo.GetComponent<TextMeshProUGUI>();
            headerTitleText.text = "<b><color=#FF2233>THE SHADE</color></b>  <color=#888888>[APEX PREDATOR]</color>";
            headerTitleText.fontSize = 15;
            headerTitleText.alignment = TextAlignmentOptions.Left;
            RectTransform titleRt = titleGo.GetComponent<RectTransform>();
            titleRt.anchorMin = new Vector2(0f, 0.5f);
            titleRt.anchorMax = new Vector2(0f, 0.5f);
            titleRt.pivot = new Vector2(0f, 0.5f);
            titleRt.anchoredPosition = new Vector2(30f, 0f);
            titleRt.sizeDelta = new Vector2(320f, 30f);

            // Center: The Shade Vitality Bar
            GameObject hpBarContainer = new GameObject("Shade_Health_Bar_Container", typeof(RectTransform));
            hpBarContainer.transform.SetParent(topBar.transform, false);
            RectTransform hpContainerRt = hpBarContainer.GetComponent<RectTransform>();
            hpContainerRt.anchorMin = new Vector2(0.5f, 0.5f);
            hpContainerRt.anchorMax = new Vector2(0.5f, 0.5f);
            hpContainerRt.pivot = new Vector2(0.5f, 0.5f);
            hpContainerRt.anchoredPosition = new Vector2(0f, 0f);
            hpContainerRt.sizeDelta = new Vector2(340f, 26f);

            // HP Bar Background Track
            GameObject hpTrackGo = new GameObject("HP_Track", typeof(RectTransform), typeof(Image));
            hpTrackGo.transform.SetParent(hpBarContainer.transform, false);
            RectTransform hpTrackRt = hpTrackGo.GetComponent<RectTransform>();
            hpTrackRt.anchorMin = Vector2.zero;
            hpTrackRt.anchorMax = Vector2.one;
            hpTrackRt.sizeDelta = Vector2.zero;
            Image hpTrackBg = hpTrackGo.GetComponent<Image>();
            hpTrackBg.color = new Color(0.12f, 0.04f, 0.06f, 0.95f);

            // Red border on HP track
            GameObject hpBorder = new GameObject("Border", typeof(RectTransform), typeof(Image));
            hpBorder.transform.SetParent(hpTrackGo.transform, false);
            RectTransform hpBorderRt = hpBorder.GetComponent<RectTransform>();
            hpBorderRt.anchorMin = new Vector2(0f, 0f);
            hpBorderRt.anchorMax = new Vector2(1f, 0f);
            hpBorderRt.sizeDelta = new Vector2(0f, 2f);
            Image hpBorderImg = hpBorder.GetComponent<Image>();
            hpBorderImg.color = new Color(1.0f, 0.15f, 0.20f, 0.85f);

            // Fill Bar
            GameObject hpFillGo = new GameObject("HP_Fill", typeof(RectTransform), typeof(Image));
            hpFillGo.transform.SetParent(hpTrackGo.transform, false);
            RectTransform hpFillRt = hpFillGo.GetComponent<RectTransform>();
            hpFillRt.anchorMin = Vector2.zero;
            hpFillRt.anchorMax = Vector2.one;
            hpFillRt.sizeDelta = new Vector2(-4f, -4f);
            shadeHealthFill = hpFillGo.GetComponent<Image>();
            shadeHealthFill.type = Image.Type.Filled;
            shadeHealthFill.fillMethod = Image.FillMethod.Horizontal;
            shadeHealthFill.fillOrigin = (int)Image.OriginHorizontal.Left;
            shadeHealthFill.fillAmount = 1f;
            shadeHealthFill.color = new Color(0.95f, 0.15f, 0.20f, 1f);

            // HP Text
            GameObject hpTextGo = new GameObject("HP_Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            hpTextGo.transform.SetParent(hpBarContainer.transform, false);
            shadeHpText = hpTextGo.GetComponent<TextMeshProUGUI>();
            shadeHpText.text = "<b>100 / 100 HP</b>";
            shadeHpText.fontSize = 12;
            shadeHpText.fontStyle = FontStyles.Bold;
            shadeHpText.alignment = TextAlignmentOptions.Center;
            shadeHpText.color = Color.white;
            RectTransform hpTextRt = hpTextGo.GetComponent<RectTransform>();
            hpTextRt.anchorMin = Vector2.zero;
            hpTextRt.anchorMax = Vector2.one;
            hpTextRt.sizeDelta = Vector2.zero;

            // Top Right: Debug Key hint
            GameObject hintGo = new GameObject("DebugHint", typeof(RectTransform), typeof(TextMeshProUGUI));
            hintGo.transform.SetParent(topBar.transform, false);
            TextMeshProUGUI hintText = hintGo.GetComponent<TextMeshProUGUI>();
            hintText.text = "<b><color=#FFAA00>[F7 / F8]</color></b>: Toggle Survivor / Shade Mode";
            hintText.fontSize = 12;
            hintText.alignment = TextAlignmentOptions.Right;
            hintText.color = new Color(0.7f, 0.8f, 0.9f, 0.8f);
            RectTransform hintRt = hintGo.GetComponent<RectTransform>();
            hintRt.anchorMin = new Vector2(1f, 0.5f);
            hintRt.anchorMax = new Vector2(1f, 0.5f);
            hintRt.pivot = new Vector2(1f, 0.5f);
            hintRt.anchoredPosition = new Vector2(-30f, 0f);
            hintRt.sizeDelta = new Vector2(320f, 24f);

            // 2. Bottom Status & Skills Bar
            GameObject bottomBar = new GameObject("BottomBar", typeof(RectTransform), typeof(Image));
            bottomBar.transform.SetParent(overlayRoot.transform, false);
            RectTransform botRt = bottomBar.GetComponent<RectTransform>();
            botRt.anchorMin = new Vector2(0f, 0f);
            botRt.anchorMax = new Vector2(1f, 0f);
            botRt.pivot = new Vector2(0.5f, 0f);
            botRt.sizeDelta = new Vector2(0f, 54f);
            Image botBg = bottomBar.GetComponent<Image>();
            botBg.color = new Color(0.08f, 0.04f, 0.05f, 0.90f);

            // Red accent top border
            GameObject botBorder = new GameObject("BorderLine", typeof(RectTransform), typeof(Image));
            botBorder.transform.SetParent(bottomBar.transform, false);
            RectTransform bbRt = botBorder.GetComponent<RectTransform>();
            bbRt.anchorMin = new Vector2(0f, 1f);
            bbRt.anchorMax = new Vector2(1f, 1f);
            bbRt.sizeDelta = new Vector2(0f, 2f);
            Image bbImg = botBorder.GetComponent<Image>();
            bbImg.color = new Color(1.0f, 0.15f, 0.20f, 0.75f);

            // Left: Environmental Light & Stun Status
            GameObject statGo = new GameObject("StatusText", typeof(RectTransform), typeof(TextMeshProUGUI));
            statGo.transform.SetParent(bottomBar.transform, false);
            statusText = statGo.GetComponent<TextMeshProUGUI>();
            statusText.text = "<b><color=#FF2244>[SHADOWS] APEX PREDATOR // +25% SPEED BOOST</color></b>";
            statusText.fontSize = 13;
            statusText.alignment = TextAlignmentOptions.Left;
            RectTransform statRt = statGo.GetComponent<RectTransform>();
            statRt.anchorMin = new Vector2(0f, 0.5f);
            statRt.anchorMax = new Vector2(0f, 0.5f);
            statRt.pivot = new Vector2(0f, 0.5f);
            statRt.anchoredPosition = new Vector2(35f, 0f);
            statRt.sizeDelta = new Vector2(600f, 30f);

            // Right: The 2 Combat Skills
            Sprite slashIconSprite = Resources.Load<Sprite>("Monster/Icon_Skill_ClawSlash");
            Sprite dashIconSprite = Resources.Load<Sprite>("Monster/Icon_Skill_ShadowDash");

            // Skill 1: Claw Slash
            GameObject slashIconGo = new GameObject("SlashIcon", typeof(RectTransform), typeof(Image));
            slashIconGo.transform.SetParent(bottomBar.transform, false);
            RectTransform siRt = slashIconGo.GetComponent<RectTransform>();
            siRt.anchorMin = new Vector2(1f, 0.5f);
            siRt.anchorMax = new Vector2(1f, 0.5f);
            siRt.pivot = new Vector2(1f, 0.5f);
            siRt.anchoredPosition = new Vector2(-490f, 0f);
            siRt.sizeDelta = new Vector2(36f, 36f);
            Image siImg = slashIconGo.GetComponent<Image>();
            siImg.sprite = slashIconSprite;

            GameObject slashGo = new GameObject("SlashSkillText", typeof(RectTransform), typeof(TextMeshProUGUI));
            slashGo.transform.SetParent(bottomBar.transform, false);
            slashSkillText = slashGo.GetComponent<TextMeshProUGUI>();
            slashSkillText.text = "<b>[M1 / F] CLAW SLASH</b>  <color=#FF2233>[50 DMG]</color>";
            slashSkillText.fontSize = 12;
            slashSkillText.alignment = TextAlignmentOptions.Left;
            slashSkillText.color = Color.white;
            RectTransform slashRt = slashGo.GetComponent<RectTransform>();
            slashRt.anchorMin = new Vector2(1f, 0.5f);
            slashRt.anchorMax = new Vector2(1f, 0.5f);
            slashRt.pivot = new Vector2(1f, 0.5f);
            slashRt.anchoredPosition = new Vector2(-280f, 0f);
            slashRt.sizeDelta = new Vector2(200f, 30f);

            // Skill 2: Shadow Dash
            GameObject dashIconGo = new GameObject("DashIcon", typeof(RectTransform), typeof(Image));
            dashIconGo.transform.SetParent(bottomBar.transform, false);
            RectTransform diRt = dashIconGo.GetComponent<RectTransform>();
            diRt.anchorMin = new Vector2(1f, 0.5f);
            diRt.anchorMax = new Vector2(1f, 0.5f);
            diRt.pivot = new Vector2(1f, 0.5f);
            diRt.anchoredPosition = new Vector2(-235f, 0f);
            diRt.sizeDelta = new Vector2(36f, 36f);
            Image diImg = dashIconGo.GetComponent<Image>();
            diImg.sprite = dashIconSprite;

            // Cooldown overlay on Dash Icon
            GameObject cdFillGo = new GameObject("DashCooldownFill", typeof(RectTransform), typeof(Image));
            cdFillGo.transform.SetParent(dashIconGo.transform, false);
            RectTransform cdRt = cdFillGo.GetComponent<RectTransform>();
            cdRt.anchorMin = Vector2.zero;
            cdRt.anchorMax = Vector2.one;
            cdRt.offsetMin = Vector2.zero;
            cdRt.offsetMax = Vector2.zero;
            dashCooldownFill = cdFillGo.GetComponent<Image>();
            dashCooldownFill.sprite = white;
            dashCooldownFill.type = Image.Type.Filled;
            dashCooldownFill.fillMethod = Image.FillMethod.Radial360;
            dashCooldownFill.fillClockwise = false;
            dashCooldownFill.fillOrigin = 2; // Top
            dashCooldownFill.color = new Color(0f, 0f, 0f, 0.70f);

            GameObject dashGo = new GameObject("DashSkillText", typeof(RectTransform), typeof(TextMeshProUGUI));
            dashGo.transform.SetParent(bottomBar.transform, false);
            dashSkillText = dashGo.GetComponent<TextMeshProUGUI>();
            dashSkillText.text = "<b>[M2 / SPACE] SHADOW DASH</b>  <color=#00FFCC>[4.5m]</color>";
            dashSkillText.fontSize = 12;
            dashSkillText.alignment = TextAlignmentOptions.Left;
            dashSkillText.color = Color.white;
            RectTransform dashRt = dashGo.GetComponent<RectTransform>();
            dashRt.anchorMin = new Vector2(1f, 0.5f);
            dashRt.anchorMax = new Vector2(1f, 0.5f);
            dashRt.pivot = new Vector2(1f, 0.5f);
            dashRt.anchoredPosition = new Vector2(-30f, 0f);
            dashRt.sizeDelta = new Vector2(200f, 30f);

            // Default to inactive so it never obstructs scene view in editor or survivor gameplay
            if (overlayRoot != null)
            {
                overlayRoot.SetActive(false);
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!Application.isPlaying && overlayRoot != null && overlayRoot.activeSelf)
            {
                overlayRoot.SetActive(false);
            }
        }
#endif

        private static Sprite GetSolidWhiteSprite()
        {
            if (cachedSolidWhite == null)
            {
                Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                tex.filterMode = FilterMode.Point;
                tex.SetPixel(0, 0, Color.white);
                tex.SetPixel(0, 1, Color.white);
                tex.SetPixel(1, 0, Color.white);
                tex.SetPixel(1, 1, Color.white);
                tex.Apply();
                cachedSolidWhite = Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 100f);
            }
            return cachedSolidWhite;
        }

        /// <summary>
        /// Ensures a persistent singleton ShadeHUDOverlay exists in the scene.
        /// </summary>
        public static ShadeHUDOverlay GetOrCreate()
        {
            if (Instance != null) return Instance;

            ShadeHUDOverlay existing = FindAnyObjectByType<ShadeHUDOverlay>(FindObjectsInactive.Include);
            if (existing != null) return existing;

            GameObject go = new GameObject("ShadeHUDOverlay", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.transform.SetParent(null, false);

            Canvas canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 96;

            CanvasScaler scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            ShadeHUDOverlay overlay = go.AddComponent<ShadeHUDOverlay>();
            overlay.EnsureCanvasAndHierarchy();
            return overlay;
        }
    }
}
