using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using TheLastSignal.Entities.Environment;

namespace TheLastSignal.UI
{
    /// <summary>
    /// Retro CRT surveillance and power grid terminal UI.
    /// Shows live camera feed of the inspected room and biometric/monster threat readouts.
    /// </summary>
    public class RoomInspectionTerminalUI : MonoBehaviour
    {
        [Header("UI Roots")]
        [SerializeField] private GameObject terminalPanel;
        [SerializeField] private RawImage cameraFeedImage;

        [Header("Readout Texts")]
        [SerializeField] private TextMeshProUGUI headerText;
        [SerializeField] private TextMeshProUGUI camIdText;
        [SerializeField] private TextMeshProUGUI sectorInfoText;
        [SerializeField] private TextMeshProUGUI personnelText;
        [SerializeField] private TextMeshProUGUI threatStatusText;
        [SerializeField] private TextMeshProUGUI liveIndicatorText;

        [Header("Sector Buttons")]
        [SerializeField] private Button btnNorth;
        [SerializeField] private Button btnWest;
        [SerializeField] private Button btnEast;
        [SerializeField] private Button btnSouth;
        [SerializeField] private Button btnClose;

        private RoomInspectionConsole console;
        private Coroutine blinkCoroutine;

        public bool IsVisible => terminalPanel != null && terminalPanel.activeSelf;

        private void Awake()
        {
            EnsureUIHierarchy();
        }

        public void Initialize(RoomInspectionConsole consoleRef, RenderTexture rt)
        {
            console = consoleRef;

            EnsureUIHierarchy();

            if (cameraFeedImage != null && rt != null)
            {
                cameraFeedImage.texture = rt;
            }

            // Hook up buttons
            if (btnNorth != null) { btnNorth.onClick.RemoveAllListeners(); btnNorth.onClick.AddListener(() => console.InspectSector(RoomSector.North_CrewQuarters)); }
            if (btnWest != null) { btnWest.onClick.RemoveAllListeners(); btnWest.onClick.AddListener(() => console.InspectSector(RoomSector.West_MedBay)); }
            if (btnEast != null) { btnEast.onClick.RemoveAllListeners(); btnEast.onClick.AddListener(() => console.InspectSector(RoomSector.East_ConsoleAlpha)); }
            if (btnSouth != null) { btnSouth.onClick.RemoveAllListeners(); btnSouth.onClick.AddListener(() => console.InspectSector(RoomSector.South_Airlock)); }
            if (btnClose != null) { btnClose.onClick.RemoveAllListeners(); btnClose.onClick.AddListener(() => console.CloseTerminal()); }
        }

        public void Show()
        {
            if (terminalPanel != null)
            {
                terminalPanel.SetActive(true);
            }

            if (blinkCoroutine != null) StopCoroutine(blinkCoroutine);
            blinkCoroutine = StartCoroutine(BlinkLiveIndicator());
        }

        public void Hide()
        {
            if (terminalPanel != null)
            {
                terminalPanel.SetActive(false);
            }

            if (blinkCoroutine != null)
            {
                StopCoroutine(blinkCoroutine);
                blinkCoroutine = null;
            }
        }

        public void UpdateFeed(SectorData sectorData, bool monsterDetected, string monsterInfo, int crewCount)
        {
            if (camIdText != null)
            {
                camIdText.text = $"LIVE CAM: {sectorData.sectorCode}";
            }

            if (sectorInfoText != null)
            {
                sectorInfoText.text = $"KHU VỰC: <color=#66DDFF>{sectorData.displayName.ToUpper()}</color> | CẤP ĐIỆN: <color=#FFDD44>AUX OVERRIDE [100%]</color>";
            }

            if (personnelText != null)
            {
                if (crewCount > 0)
                {
                    personnelText.text = $"NHÂN SỰ PHÁT HIỆN: <color=#55FF88>{crewCount} THÀNH VIÊN ĐANG Ở TRONG PHÒNG</color>";
                }
                else
                {
                    personnelText.text = "NHÂN SỰ PHÁT HIỆN: <color=#AAAAAA>0 (PHÒNG TRỐNG)</color>";
                }
            }

            if (threatStatusText != null)
            {
                if (monsterDetected)
                {
                    threatStatusText.text = $"<color=#FF2222>⚠️ CẢNH BÁO NGUY HIỂM: PHÁT HIỆN THỰC THỂ LẠ ({monsterInfo.ToUpper()})!</color>";
                }
                else
                {
                    threatStatusText.text = "<color=#22FF66>● TÌNH TRẠNG: AN TOÀN - KHÔNG PHÁT HIỆN MỐI ĐE DỌA</color>";
                }
            }

            UpdateButtonVisuals(sectorData.sector);
        }

        private void UpdateButtonVisuals(RoomSector activeSector)
        {
            SetButtonHighlight(btnNorth, activeSector == RoomSector.North_CrewQuarters);
            SetButtonHighlight(btnWest, activeSector == RoomSector.West_MedBay);
            SetButtonHighlight(btnEast, activeSector == RoomSector.East_ConsoleAlpha);
            SetButtonHighlight(btnSouth, activeSector == RoomSector.South_Airlock);
        }

        private void SetButtonHighlight(Button btn, bool isActive)
        {
            if (btn == null) return;
            var cb = btn.colors;
            Color baseColor = isActive ? new Color(0.1f, 0.45f, 0.6f, 1f) : new Color(0.12f, 0.16f, 0.22f, 1f);
            cb.normalColor = baseColor;
            cb.selectedColor = baseColor;
            btn.colors = cb;

            var txt = btn.GetComponentInChildren<TextMeshProUGUI>();
            if (txt != null)
            {
                txt.color = isActive ? new Color(0.4f, 1f, 0.9f, 1f) : new Color(0.75f, 0.8f, 0.88f, 1f);
            }
        }

        private IEnumerator BlinkLiveIndicator()
        {
            while (true)
            {
                if (liveIndicatorText != null)
                {
                    liveIndicatorText.text = "<color=#22FF44>● LIVE FEED</color> 24FPS";
                }
                yield return new WaitForSeconds(0.6f);
                if (liveIndicatorText != null)
                {
                    liveIndicatorText.text = "<color=#116622>○ LIVE FEED</color> 24FPS";
                }
                yield return new WaitForSeconds(0.6f);
            }
        }

        public void EnsureUIHierarchy()
        {
            if (terminalPanel != null) return;

            // Search if already created
            Transform existing = transform.Find("Terminal_Panel");
            if (existing != null)
            {
                terminalPanel = existing.gameObject;
                BindExistingComponents();
                return;
            }

            // Build dynamic retro UI hierarchy
            BuildTerminalHierarchy();
        }

        private void BindExistingComponents()
        {
            if (terminalPanel == null) return;
            cameraFeedImage = terminalPanel.GetComponentInChildren<RawImage>(true);

            var texts = terminalPanel.GetComponentsInChildren<TextMeshProUGUI>(true);
            foreach (var t in texts)
            {
                if (t.name == "Text_Header") headerText = t;
                else if (t.name == "Text_CamId") camIdText = t;
                else if (t.name == "Text_SectorInfo") sectorInfoText = t;
                else if (t.name == "Text_Personnel") personnelText = t;
                else if (t.name == "Text_ThreatStatus") threatStatusText = t;
                else if (t.name == "Text_LiveIndicator") liveIndicatorText = t;
            }

            var buttons = terminalPanel.GetComponentsInChildren<Button>(true);
            foreach (var b in buttons)
            {
                if (b.name == "Btn_North") btnNorth = b;
                else if (b.name == "Btn_West") btnWest = b;
                else if (b.name == "Btn_East") btnEast = b;
                else if (b.name == "Btn_South") btnSouth = b;
                else if (b.name == "Btn_Close") btnClose = b;
            }
        }

        public void BuildTerminalHierarchy()
        {
            Canvas parentCanvas = GetComponentInParent<Canvas>();
            if (parentCanvas == null)
            {
                parentCanvas = FindAnyObjectByType<Canvas>();
            }

            if (parentCanvas == null) return;

            // 1. Root Panel
            GameObject panelGo = new GameObject("Terminal_Panel");
            panelGo.layer = 5;
            panelGo.transform.SetParent(transform, false);
            terminalPanel = panelGo;

            RectTransform rtPanel = panelGo.AddComponent<RectTransform>();
            rtPanel.anchorMin = new Vector2(0.5f, 0.5f);
            rtPanel.anchorMax = new Vector2(0.5f, 0.5f);
            rtPanel.pivot = new Vector2(0.5f, 0.5f);
            rtPanel.sizeDelta = new Vector2(1340, 780);

            Image panelImg = panelGo.AddComponent<Image>();
            panelImg.color = new Color(0.05f, 0.08f, 0.12f, 0.96f);

            // 2. Header Bar
            GameObject headerGo = CreateTextObject(panelGo.transform, "Text_Header", 
                "[ BẢNG ĐIỀU KHIỂN LƯỚI ĐIỆN & HỆ THỐNG GIÁM SÁT NODE A ]", 
                new Vector2(0, 350), new Vector2(1280, 50), 22, FontStyles.Bold, new Color(0.3f, 0.9f, 1f, 1f));
            headerText = headerGo.GetComponent<TextMeshProUGUI>();

            // 3. Left Panel (Control Buttons)
            GameObject leftPanelGo = new GameObject("Left_Controls");
            leftPanelGo.transform.SetParent(panelGo.transform, false);
            RectTransform rtLeft = leftPanelGo.AddComponent<RectTransform>();
            rtLeft.anchorMin = new Vector2(0.5f, 0.5f);
            rtLeft.anchorMax = new Vector2(0.5f, 0.5f);
            rtLeft.pivot = new Vector2(0.5f, 0.5f);
            rtLeft.anchoredPosition = new Vector2(-420, -10);
            rtLeft.sizeDelta = new Vector2(440, 660);

            CreateTextObject(leftPanelGo.transform, "Title_Left", "CHỌN KHU VỰC CẤP ĐIỆN & SOI SÁNG:",
                new Vector2(0, 270), new Vector2(420, 40), 16, FontStyles.Bold, new Color(0.9f, 0.85f, 0.4f, 1f));

            btnNorth = CreateButton(leftPanelGo.transform, "Btn_North", "[1] BẮC: CREW QUARTERS", new Vector2(0, 180), new Vector2(400, 65));
            btnWest = CreateButton(leftPanelGo.transform, "Btn_West", "[2] TÂY: MED BAY", new Vector2(0, 95), new Vector2(400, 65));
            btnEast = CreateButton(leftPanelGo.transform, "Btn_East", "[3] ĐÔNG: CONSOLE ALPHA", new Vector2(0, 10), new Vector2(400, 65));
            btnSouth = CreateButton(leftPanelGo.transform, "Btn_South", "[4] NAM: AIR LOCK", new Vector2(0, -75), new Vector2(400, 65));

            CreateTextObject(leftPanelGo.transform, "Text_Shortcuts", 
                "• Phím tắt: [1] [2] [3] [4] để chọn phòng\n• Bấm [ESC] hoặc [E] để thoát về nhân vật", 
                new Vector2(0, -165), new Vector2(400, 55), 14, FontStyles.Italic, new Color(0.7f, 0.75f, 0.8f, 1f));

            btnClose = CreateButton(leftPanelGo.transform, "Btn_Close", "[ESC] ĐÓNG BẢNG ĐIỀU KHIỂN", new Vector2(0, -250), new Vector2(400, 65), new Color(0.45f, 0.15f, 0.15f, 1f));

            // 4. Right Panel (Monitor Feed & Diagnostics)
            GameObject rightPanelGo = new GameObject("Right_Monitor");
            rightPanelGo.transform.SetParent(panelGo.transform, false);
            RectTransform rtRight = rightPanelGo.AddComponent<RectTransform>();
            rtRight.anchorMin = new Vector2(0.5f, 0.5f);
            rtRight.anchorMax = new Vector2(0.5f, 0.5f);
            rtRight.pivot = new Vector2(0.5f, 0.5f);
            rtRight.anchoredPosition = new Vector2(230, -10);
            rtRight.sizeDelta = new Vector2(800, 660);

            // Monitor Frame
            GameObject frameGo = new GameObject("Monitor_Frame");
            frameGo.transform.SetParent(rightPanelGo.transform, false);
            RectTransform rtFrame = frameGo.AddComponent<RectTransform>();
            rtFrame.anchoredPosition = new Vector2(0, 80);
            rtFrame.sizeDelta = new Vector2(740, 480);
            Image frameImg = frameGo.AddComponent<Image>();
            frameImg.color = new Color(0.02f, 0.04f, 0.06f, 1f);

            // Monitor RawImage (Camera Feed)
            GameObject feedGo = new GameObject("Camera_Feed_Image");
            feedGo.transform.SetParent(frameGo.transform, false);
            RectTransform rtFeed = feedGo.AddComponent<RectTransform>();
            rtFeed.anchoredPosition = Vector2.zero;
            rtFeed.sizeDelta = new Vector2(720, 460);
            cameraFeedImage = feedGo.AddComponent<RawImage>();
            cameraFeedImage.color = Color.white;

            // Overlays on Monitor
            GameObject liveGo = CreateTextObject(frameGo.transform, "Text_LiveIndicator", "● LIVE FEED 24FPS",
                new Vector2(-230, 205), new Vector2(240, 30), 14, FontStyles.Bold, new Color(0.2f, 1f, 0.4f, 1f));
            liveIndicatorText = liveGo.GetComponent<TextMeshProUGUI>();

            GameObject camIdGo = CreateTextObject(frameGo.transform, "Text_CamId", "LIVE CAM: SECTOR 01 - NORTH",
                new Vector2(200, 205), new Vector2(300, 30), 14, FontStyles.Bold, new Color(0.4f, 0.9f, 1f, 1f));
            camIdText = camIdGo.GetComponent<TextMeshProUGUI>();

            // Diagnostics Box below monitor
            GameObject diagBoxGo = new GameObject("Diag_Box");
            diagBoxGo.transform.SetParent(rightPanelGo.transform, false);
            RectTransform rtDiag = diagBoxGo.AddComponent<RectTransform>();
            rtDiag.anchoredPosition = new Vector2(0, -220);
            rtDiag.sizeDelta = new Vector2(740, 100);
            Image diagImg = diagBoxGo.AddComponent<Image>();
            diagImg.color = new Color(0.04f, 0.07f, 0.1f, 0.9f);

            GameObject infoGo = CreateTextObject(diagBoxGo.transform, "Text_SectorInfo", "KHU VỰC: CREW QUARTERS | CẤP ĐIỆN: AUX OVERRIDE",
                new Vector2(0, 28), new Vector2(700, 25), 15, FontStyles.Bold, Color.white);
            sectorInfoText = infoGo.GetComponent<TextMeshProUGUI>();

            GameObject personGo = CreateTextObject(diagBoxGo.transform, "Text_Personnel", "NHÂN SỰ PHÁT HIỆN: 0 (PHÒNG TRỐNG)",
                new Vector2(0, 2), new Vector2(700, 25), 14, FontStyles.Normal, new Color(0.8f, 0.85f, 0.9f, 1f));
            personnelText = personGo.GetComponent<TextMeshProUGUI>();

            GameObject threatGo = CreateTextObject(diagBoxGo.transform, "Text_ThreatStatus", "● TÌNH TRẠNG: AN TOÀN - KHÔNG PHÁT HIỆN MỐI ĐE DỌA",
                new Vector2(0, -24), new Vector2(700, 25), 16, FontStyles.Bold, new Color(0.2f, 1f, 0.4f, 1f));
            threatStatusText = threatGo.GetComponent<TextMeshProUGUI>();

            panelGo.SetActive(false);
        }

        private GameObject CreateTextObject(Transform parent, string name, string text, Vector2 pos, Vector2 size, float fontSize, FontStyles style, Color color)
        {
            GameObject go = new GameObject(name);
            go.layer = 5;
            go.transform.SetParent(parent, false);
            RectTransform rt = go.AddComponent<RectTransform>();
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;

            TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.fontStyle = style;
            tmp.color = color;
            tmp.alignment = TextAlignmentOptions.Center;

            return go;
        }

        private Button CreateButton(Transform parent, string name, string label, Vector2 pos, Vector2 size, Color? normalCol = null)
        {
            GameObject go = new GameObject(name);
            go.layer = 5;
            go.transform.SetParent(parent, false);
            RectTransform rt = go.AddComponent<RectTransform>();
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;

            Image img = go.AddComponent<Image>();
            img.color = normalCol ?? new Color(0.12f, 0.16f, 0.22f, 1f);

            Button btn = go.AddComponent<Button>();
            ColorBlock cb = btn.colors;
            cb.normalColor = normalCol ?? new Color(0.12f, 0.16f, 0.22f, 1f);
            cb.highlightedColor = new Color(0.2f, 0.35f, 0.5f, 1f);
            cb.pressedColor = new Color(0.08f, 0.25f, 0.4f, 1f);
            cb.selectedColor = cb.normalColor;
            btn.colors = cb;

            CreateTextObject(go.transform, "Text_Label", label, Vector2.zero, size, 16, FontStyles.Bold, new Color(0.85f, 0.9f, 0.95f, 1f));

            return btn;
        }
    }
}
