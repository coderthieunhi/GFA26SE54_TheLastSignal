using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;
using TheLastSignal.Entities.Player;

namespace TheLastSignal.UI
{
    /// <summary>
    /// Tactical Command Terminal UI displayed when interacting with the Apex-IV Holo-Console in Bedroom.
    /// Provides detailed storyline objectives, step-by-step game completion instructions (win conditions),
    /// survival guidance, and key bindings.
    /// </summary>
    public class BedroomMissionTerminalUI : MonoBehaviour
    {
        private static BedroomMissionTerminalUI instance;
        public static BedroomMissionTerminalUI Instance => instance;

        [Header("UI Root")]
        [SerializeField] private GameObject terminalPanel;

        [Header("References")]
        [SerializeField] private Button btnClose;
        [SerializeField] private TextMeshProUGUI headerStatusText;

        private PlayerController activePlayer;
        private Coroutine pulseCoroutine;

        public bool IsOpen => terminalPanel != null && terminalPanel.activeSelf;

        private void Awake()
        {
            if (instance != null && instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            instance = this;
            EnsureUIHierarchy();
        }

        private void Update()
        {
            if (!IsOpen) return;

            // Close on Escape or E key
            Keyboard kb = Keyboard.current;
            if (kb != null && (kb.escapeKey.wasPressedThisFrame || kb.eKey.wasPressedThisFrame))
            {
                CloseTerminal();
                return;
            }

            // Close if player moves too far away
            if (activePlayer != null)
            {
                float sqrDist = (activePlayer.transform.position - transform.position).sqrMagnitude;
                if (sqrDist > 9.0f) // > 3 meters
                {
                    CloseTerminal();
                }
            }
        }

        public void OpenTerminal(PlayerController player)
        {
            activePlayer = player;
            EnsureUIHierarchy();

            if (terminalPanel != null)
            {
                terminalPanel.SetActive(true);
            }

            if (pulseCoroutine != null) StopCoroutine(pulseCoroutine);
            pulseCoroutine = StartCoroutine(PulseStatusIndicator());
        }

        public void CloseTerminal()
        {
            if (terminalPanel != null)
            {
                terminalPanel.SetActive(false);
            }

            if (pulseCoroutine != null)
            {
                StopCoroutine(pulseCoroutine);
                pulseCoroutine = null;
            }

            activePlayer = null;
        }

        private IEnumerator PulseStatusIndicator()
        {
            while (true)
            {
                if (headerStatusText != null)
                {
                    headerStatusText.text = "<color=#EF4444>[BÁO ĐỘNG ĐỎ]</color> THẤT THOÁT NĂNG LƯỢNG - THỰC THỂ KHÔNG XÁC ĐỊNH XÂM NHẬP TRẠM";
                }
                yield return new WaitForSeconds(0.7f);
                if (headerStatusText != null)
                {
                    headerStatusText.text = "<color=#F59E0B>[CẢNH BÁO]</color> THẤT THOÁT NĂNG LƯỢNG - THỰC THỂ KHÔNG XÁC ĐỊNH XÂM NHẬP TRẠM";
                }
                yield return new WaitForSeconds(0.7f);
            }
        }

        public void EnsureUIHierarchy()
        {
            if (terminalPanel != null) return;

            Transform existing = transform.Find("Apex_Terminal_Panel");
            if (existing != null)
            {
                terminalPanel = existing.gameObject;
                BindExistingComponents();
                return;
            }

            BuildTerminalUI();
        }

        private void BindExistingComponents()
        {
            if (terminalPanel == null) return;
            btnClose = terminalPanel.GetComponentInChildren<Button>(true);
            var texts = terminalPanel.GetComponentsInChildren<TextMeshProUGUI>(true);
            foreach (var t in texts)
            {
                if (t.name == "Text_HeaderStatus") headerStatusText = t;
            }
        }

        private void BuildTerminalUI()
        {
            Canvas parentCanvas = GetComponentInParent<Canvas>();
            if (parentCanvas == null)
            {
                Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
                foreach (Canvas c in canvases)
                {
                    if (c.name.Contains("HUD"))
                    {
                        parentCanvas = c;
                        break;
                    }
                }
                if (parentCanvas == null && canvases.Length > 0)
                {
                    parentCanvas = canvases[0];
                }
            }

            if (parentCanvas == null) return;

            // 1. Root Modal Panel
            GameObject panelGo = new GameObject("Apex_Terminal_Panel");
            panelGo.layer = 5;
            panelGo.transform.SetParent(parentCanvas.transform, false);
            terminalPanel = panelGo;

            RectTransform rtPanel = panelGo.AddComponent<RectTransform>();
            rtPanel.anchorMin = new Vector2(0.5f, 0.5f);
            rtPanel.anchorMax = new Vector2(0.5f, 0.5f);
            rtPanel.pivot = new Vector2(0.5f, 0.5f);
            rtPanel.sizeDelta = new Vector2(1280, 720);

            // Dark semi-transparent sci-fi backdrop
            Image panelImg = panelGo.AddComponent<Image>();
            panelImg.color = new Color(0.04f, 0.07f, 0.11f, 0.95f);

            // Subtle border outline
            Outline outline = panelGo.AddComponent<Outline>();
            outline.effectColor = new Color(0.22f, 0.74f, 0.97f, 0.5f);
            outline.effectDistance = new Vector2(2, -2);

            // 2. Header Bar
            GameObject headerTitle = CreateText(panelGo.transform, "Text_MainTitle",
                "APEX-IV STRATEGIC COMMAND TERMINAL // USS AETHELWULF",
                new Vector2(0, 315), new Vector2(1200, 36), 22, FontStyles.Bold, new Color(0.22f, 0.74f, 0.97f, 1f));

            GameObject statusGo = CreateText(panelGo.transform, "Text_HeaderStatus",
                "<color=#EF4444>[BÁO ĐỘNG ĐỎ]</color> THẤT THOÁT NĂNG LƯỢNG - THỰC THỂ KHÔNG XÁC ĐỊNH XÂM NHẬP TRẠM",
                new Vector2(0, 280), new Vector2(1200, 26), 15, FontStyles.Bold, new Color(0.96f, 0.62f, 0.04f, 1f));
            headerStatusText = statusGo.GetComponent<TextMeshProUGUI>();

            // Divider Line
            CreateDivider(panelGo.transform, new Vector2(0, 260), 1200, new Color(0.15f, 0.28f, 0.42f, 0.8f));

            // 3. Left Section: Game Objectives & Win Condition
            GameObject leftContainer = new GameObject("Section_Objectives");
            leftContainer.layer = 5;
            leftContainer.transform.SetParent(panelGo.transform, false);
            RectTransform rtLeft = leftContainer.AddComponent<RectTransform>();
            rtLeft.anchorMin = new Vector2(0.5f, 0.5f);
            rtLeft.anchorMax = new Vector2(0.5f, 0.5f);
            rtLeft.pivot = new Vector2(0.5f, 0.5f);
            rtLeft.anchoredPosition = new Vector2(-305, 0);
            rtLeft.sizeDelta = new Vector2(590, 480);

            CreateText(leftContainer.transform, "Title_Objectives",
                "■ QUY TRÌNH THOÁT HIỂM & ĐIỀU KIỆN CHIẾN THẮNG (WIN CONDITIONS)",
                new Vector2(0, 215), new Vector2(580, 28), 16, FontStyles.Bold, new Color(0.38f, 0.85f, 0.98f, 1f), TextAlignmentOptions.Left);

            string objectivesContent =
                "<color=#22C55E><b>MỤC TIÊU SỐNG CÒN: SƠ TÁN KHỎI TRẠM QUA KHOANG AIRLOCK</b></color>\n\n" +
                "<b>1. KHÔI PHỤC ĐIỆN PHỤ (SUBSTATION):</b>\n" +
                "• Lưới điện chính tê liệt. Di chuyển tới trạm biến áp Substation.\n" +
                "• Kích hoạt rơ-le để mở các cửa an ninh ngăn cách phân khu.\n\n" +
                "<b>2. KHỞI ĐỘNG LÒ PHẢN ỨNG (REACTOR CORE):</b>\n" +
                "• Di chuyển về phía Đông, tiến vào buồng phản ứng hạt nhân.\n" +
                "• Tương tác bảng điều khiển chính để xả năng lượng khẩn cấp.\n" +
                "• <color=#EF4444><b>CẢNH BÁO:</b> Hệ thống đếm ngược Meltdown (60s) sẽ bắt đầu!</color>\n\n" +
                "<b>3. THU HỒI THẺ GHI ĐÈ AN NINH (MEDBAY):</b>\n" +
                "• Cửa thoát hiểm Airlock bị phong tỏa tự động.\n" +
                "• Tìm Thẻ An Ninh (Security Keycard) tại MedBay để mở chốt.\n\n" +
                "<b>4. SƠ TÁN KHẨN CẤP (AIRLOCK EVACUATION) -> CHIẾN THẮNG:</b>\n" +
                "• Chạy tháo thân quay lại khoang Airlock trước khi đếm ngược về 0!";

            CreateText(leftContainer.transform, "Text_ObjectivesBody",
                objectivesContent,
                new Vector2(0, -10), new Vector2(580, 410), 13.5f, FontStyles.Normal, new Color(0.85f, 0.90f, 0.95f, 1f), TextAlignmentOptions.TopLeft);

            // 4. Right Section: Survival Instructions & Controls
            GameObject rightContainer = new GameObject("Section_Survival");
            rightContainer.layer = 5;
            rightContainer.transform.SetParent(panelGo.transform, false);
            RectTransform rtRight = rightContainer.AddComponent<RectTransform>();
            rtRight.anchorMin = new Vector2(0.5f, 0.5f);
            rtRight.anchorMax = new Vector2(0.5f, 0.5f);
            rtRight.pivot = new Vector2(0.5f, 0.5f);
            rtRight.anchoredPosition = new Vector2(305, 0);
            rtRight.sizeDelta = new Vector2(590, 480);

            CreateText(rightContainer.transform, "Title_Survival",
                "■ HƯỚNG DẪN SINH TỒN & PHÍM ĐIỀU KHIỂN (SURVIVAL PROTOCOL)",
                new Vector2(0, 215), new Vector2(580, 28), 16, FontStyles.Bold, new Color(0.96f, 0.62f, 0.04f, 1f), TextAlignmentOptions.Left);

            string survivalContent =
                "<b>BẢNG PHÍM ĐIỀU KHIỂN:</b>\n" +
                "• <b>[W / A / S / D]</b>: Di chuyển nhân vật\n" +
                "• <b>[Shift (Giữ)]</b>: Chạy nước rút (Sprint)\n" +
                "• <b>[F]</b>: Bật / Tắt Đèn pin (Flashlight)\n" +
                "• <b>[E]</b>: Tương tác thiết bị / Bảng điều khiển / Mở cửa\n" +
                "• <b>[ESC] hoặc [E]</b>: Đóng giao diện chỉ huy này\n\n" +
                "<b>QUY TẮC SỐNG CÒN TRONG BÓNG TỐI:</b>\n" +
                "• <color=#F87171><b>THỰC THỂ BÓNG TỐI (THE SHADE):</b></color> Sinh vật lạ săn mồi trong bóng tối. Tránh tiếp cận trực diện!\n" +
                "• <b>ÁNH SÁNG LÀ VŨ KHÍ:</b> Thực thể bị cản trở bởi ánh sáng mạnh. Luôn kiểm tra pin đèn pin trước khi tiến vào hành lang tối.\n" +
                "• <b>SƠ ĐỒ TRẠM VŨ TRỤ:</b>\n" +
                "  <color=#67E8F9>Cabin 01 (Hiện tại) → Corridor → MedBay → Substation → Reactor Core → Airlock (Thoát hiểm)</color>";

            CreateText(rightContainer.transform, "Text_SurvivalBody",
                survivalContent,
                new Vector2(0, -10), new Vector2(580, 410), 13.5f, FontStyles.Normal, new Color(0.85f, 0.90f, 0.95f, 1f), TextAlignmentOptions.TopLeft);

            // Divider Line
            CreateDivider(panelGo.transform, new Vector2(0, -250), 1200, new Color(0.15f, 0.28f, 0.42f, 0.8f));

            // 5. Bottom Close Button
            btnClose = CreateButton(panelGo.transform, "Btn_CloseTerminal",
                "[ESC] / [E] ĐÓNG BẢN ĐỒ CHỈ HUY & TIẾP TỤC SỨ MỆNH",
                new Vector2(0, -290), new Vector2(560, 45), new Color(0.10f, 0.35f, 0.48f, 1f));
            btnClose.onClick.AddListener(CloseTerminal);

            panelGo.SetActive(false);
        }

        private GameObject CreateText(Transform parent, string name, string content, Vector2 pos, Vector2 size, float fontSize, FontStyles style, Color color, TextAlignmentOptions alignment = TextAlignmentOptions.Center)
        {
            GameObject go = new GameObject(name);
            go.layer = 5;
            go.transform.SetParent(parent, false);

            RectTransform rt = go.AddComponent<RectTransform>();
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;

            TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = content;
            tmp.fontSize = fontSize;
            tmp.fontStyle = style;
            tmp.color = color;
            tmp.alignment = alignment;
            tmp.textWrappingMode = TextWrappingModes.Normal;

            return go;
        }

        private void CreateDivider(Transform parent, Vector2 pos, float width, Color color)
        {
            GameObject go = new GameObject("Divider");
            go.layer = 5;
            go.transform.SetParent(parent, false);

            RectTransform rt = go.AddComponent<RectTransform>();
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(width, 2);

            Image img = go.AddComponent<Image>();
            img.color = color;
        }

        private Button CreateButton(Transform parent, string name, string label, Vector2 pos, Vector2 size, Color baseCol)
        {
            GameObject go = new GameObject(name);
            go.layer = 5;
            go.transform.SetParent(parent, false);

            RectTransform rt = go.AddComponent<RectTransform>();
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;

            Image img = go.AddComponent<Image>();
            img.color = baseCol;

            Button btn = go.AddComponent<Button>();
            ColorBlock cb = btn.colors;
            cb.normalColor = baseCol;
            cb.highlightedColor = new Color(baseCol.r + 0.15f, baseCol.g + 0.15f, baseCol.b + 0.15f, 1f);
            cb.pressedColor = new Color(baseCol.r - 0.08f, baseCol.g - 0.08f, baseCol.b - 0.08f, 1f);
            cb.selectedColor = cb.normalColor;
            btn.colors = cb;

            CreateText(go.transform, "Text_Label", label, Vector2.zero, size, 15, FontStyles.Bold, new Color(0.95f, 0.98f, 1f, 1f));

            return btn;
        }
    }
}
