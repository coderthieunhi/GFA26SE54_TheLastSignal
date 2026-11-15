using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;
using TheLastSignal.Entities.Player;

namespace TheLastSignal.UI
{
    /// <summary>
    /// Interactive Crew Orientation & Briefing Tutorial UI.
    /// USS Aethelwulf Onboarding Terminal with 5 Comprehensive Chapters.
    /// HERO SHOWCASE ARCHITECTURE (Zero-Clutter Edition):
    /// - 1 Dominant Hero Showcase per Chapter (Zero fragmented card clutter)
    /// - Interactive Keyboard Simulator in Chapter 01
    /// - Towering Hero Cards in Chapter 02
    /// - Linear 3-Step Connected Flowchart in Chapter 03
    /// - Dual Split-Screen (Darkness vs Safe Light) in Chapter 04
    /// - Mission Pipeline & Dedicated Victory Banner in Chapter 05
    /// </summary>
    public class CrewOrientationTutorialUI : MonoBehaviour
    {
        private static CrewOrientationTutorialUI instance;
        public static CrewOrientationTutorialUI Instance => instance;

        [Header("UI Root Elements")]
        [SerializeField] private GameObject modalPanel;
        [SerializeField] private RectTransform mainCard;

        [Header("Header Elements")]
        [SerializeField] private TextMeshProUGUI headerTitleText;
        [SerializeField] private TextMeshProUGUI headerProgressText;
        [SerializeField] private Button btnClose;

        [Header("Sidebar Navigation")]
        [SerializeField] private Button[] tabButtons = new Button[5];
        [SerializeField] private TextMeshProUGUI[] tabButtonTexts = new TextMeshProUGUI[5];
        [SerializeField] private Image[] tabButtonBgs = new Image[5];
        [SerializeField] private GameObject[] tabActiveIndicators = new GameObject[5];
        [SerializeField] private TextMeshProUGUI sidebarStatusText;

        [Header("Main Content Header")]
        [SerializeField] private TextMeshProUGUI chapterNumberText;
        [SerializeField] private TextMeshProUGUI chapterTitleText;
        [SerializeField] private TextMeshProUGUI chapterSubtitleText;

        [Header("Modular Chapter Views")]
        [SerializeField] private GameObject[] chapterViews = new GameObject[5];

        [Header("Tactical Advice Banner")]
        [SerializeField] private GameObject calloutBoxContainer;
        [SerializeField] private TextMeshProUGUI calloutBoxText;

        [Header("Bottom Bar Controls")]
        [SerializeField] private Button btnPrevious;
        [SerializeField] private Button btnNext;
        [SerializeField] private TextMeshProUGUI prevButtonText;
        [SerializeField] private TextMeshProUGUI nextButtonText;
        [SerializeField] private TextMeshProUGUI completionStatusText;

        // Interactive Keycaps in Chapter 01 Hero Showcase
        private Image keyW_Img, keyA_Img, keyS_Img, keyD_Img;
        private Image keyShift_Img, keyMouse_Img, keyE_Img;
        private TextMeshProUGUI keyW_Txt, keyA_Txt, keyS_Txt, keyD_Txt;
        private TextMeshProUGUI testLocomotionPrompt_Txt;
        private bool hasTestedLocomotion = false;

        // Runtime state
        private PlayerController activePlayer;
        private Action onClosedCallback;
        private int currentTabIndex = 0;
        private readonly bool[] chaptersVisited = new bool[5];
        private bool isOrientationFullyCompleted = false;
        private bool savedCursorVisible;
        private CursorLockMode savedCursorLockMode;
        private readonly List<GameObject> subduedHudObjects = new List<GameObject>();

        public bool IsOpen => modalPanel != null && modalPanel.activeSelf;
        public bool IsOrientationFullyCompleted => isOrientationFullyCompleted;

        // Design Tokens & Colors (USS Aethelwulf Minimalist Sci-Fi Terminal)
        private static readonly Color ColorBackdrop = new Color(0.02f, 0.04f, 0.07f, 0.88f);
        private static readonly Color ColorCardBg = new Color(0.04f, 0.06f, 0.09f, 0.98f);
        private static readonly Color ColorCardBorder = new Color(0.16f, 0.26f, 0.36f, 0.50f);

        // Palette Restraint: 1 Primary Accent (Ice Cyan) + Purposeful Highlights
        private static readonly Color ColorCyan = new Color(0.22f, 0.74f, 0.97f, 1f);       // #38BDF8 Primary
        private static readonly Color ColorGreen = new Color(0.13f, 0.77f, 0.37f, 1f);      // #22C55E Safe Light / Success
        private static readonly Color ColorAmber = new Color(0.96f, 0.62f, 0.04f, 1f);      // #F59E0B Advice only
        private static readonly Color ColorDanger = new Color(0.94f, 0.27f, 0.27f, 1f);     // #EF4444 The Shade only
        private static readonly Color ColorSlate = new Color(0.55f, 0.63f, 0.73f, 1f);      // #8CA1BA Secondary
        private static readonly Color ColorTextLight = new Color(0.96f, 0.98f, 1.0f, 1f);   // #F8FAFC Primary Text
        private static readonly Color ColorTextMuted = new Color(0.72f, 0.80f, 0.88f, 1f);  // #B8CCE0 High Legibility Body

        // Flat Surface Tokens
        private static readonly Color ColorSurfaceFlat = new Color(0.055f, 0.08f, 0.12f, 0.75f);
        private static readonly Color TabNormalBg = new Color(0.045f, 0.07f, 0.10f, 0.70f);
        private static readonly Color TabActiveBg = new Color(0.08f, 0.18f, 0.26f, 0.95f);
        private static readonly Color TabNormalText = new Color(0.55f, 0.65f, 0.72f, 1f);
        private static readonly Color TabActiveText = new Color(0.22f, 0.74f, 0.97f, 1f);

        private void Awake()
        {
            if (instance != null && instance != this)
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

            Keyboard kb = Keyboard.current;
            Mouse mouse = Mouse.current;
            if (kb != null)
            {
                if (kb.escapeKey.wasPressedThisFrame)
                {
                    CloseTutorial();
                    return;
                }

                if (kb.eKey.wasPressedThisFrame)
                {
                    if (currentTabIndex == 4)
                    {
                        int visitedCount = 0;
                        for (int i = 0; i < 5; i++) if (chaptersVisited[i]) visitedCount++;
                        if (visitedCount >= 5 && !isOrientationFullyCompleted)
                        {
                            CompleteOrientation();
                            return;
                        }
                        CloseTutorial();
                        return;
                    }
                    else
                    {
                        NextChapter();
                    }
                }

                // Keyboard Navigation using Arrow Keys and Q/Tab/Space
                if (kb.leftArrowKey.wasPressedThisFrame || kb.qKey.wasPressedThisFrame)
                {
                    PreviousChapter();
                }
                else if (kb.rightArrowKey.wasPressedThisFrame || kb.tabKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame)
                {
                    NextChapter();
                }

                // Direct number keys 1-5
                if (kb.digit1Key.wasPressedThisFrame || kb.numpad1Key.wasPressedThisFrame) SelectChapter(0);
                if (kb.digit2Key.wasPressedThisFrame || kb.numpad2Key.wasPressedThisFrame) SelectChapter(1);
                if (kb.digit3Key.wasPressedThisFrame || kb.numpad3Key.wasPressedThisFrame) SelectChapter(2);
                if (kb.digit4Key.wasPressedThisFrame || kb.numpad4Key.wasPressedThisFrame) SelectChapter(3);
                if (kb.digit5Key.wasPressedThisFrame || kb.numpad5Key.wasPressedThisFrame) SelectChapter(4);

                // Live Interactive Keycap Feedback for Chapter 01
                if (currentTabIndex == 0)
                {
                    UpdateInteractiveKeycaps(kb, mouse);
                }
            }
        }

        private void UpdateInteractiveKeycaps(Keyboard kb, Mouse mouse)
        {
            // Locomotion Keys: W, A, S, D and Arrow Keys
            bool wPressed = kb.wKey.isPressed || kb.upArrowKey.isPressed;
            bool aPressed = kb.aKey.isPressed || kb.leftArrowKey.isPressed;
            bool sPressed = kb.sKey.isPressed || kb.downArrowKey.isPressed;
            bool dPressed = kb.dKey.isPressed || kb.rightArrowKey.isPressed;

            SetKeyHighlight(keyW_Img, keyW_Txt, wPressed, ColorCyan);
            SetKeyHighlight(keyA_Img, keyA_Txt, aPressed, ColorCyan);
            SetKeyHighlight(keyS_Img, keyS_Txt, sPressed, ColorCyan);
            SetKeyHighlight(keyD_Img, keyD_Txt, dPressed, ColorCyan);

            if (wPressed || aPressed || sPressed || dPressed)
            {
                hasTestedLocomotion = true;
            }

            // Sprint Key: Left Shift
            bool shiftPressed = kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed;
            SetKeyHighlight(keyShift_Img, null, shiftPressed, ColorAmber);

            // Interaction Key: E
            bool ePressed = kb.eKey.isPressed;
            SetKeyHighlight(keyE_Img, null, ePressed, ColorGreen);

            // Mouse Aim
            if (mouse != null)
            {
                bool mouseActive = mouse.delta.ReadValue().sqrMagnitude > 1f || mouse.leftButton.isPressed;
                SetKeyHighlight(keyMouse_Img, null, mouseActive, ColorCyan);
            }

            if (testLocomotionPrompt_Txt != null)
            {
                if (hasTestedLocomotion)
                {
                    testLocomotionPrompt_Txt.text = "<color=#22C55E><b>INPUT VERIFIED  •  RESPONSIVE</b></color>";
                }
                else
                {
                    testLocomotionPrompt_Txt.text = "<color=#38BDF8><b>LIVE TEST: PRESS WASD / SHIFT / E / MOUSE</b></color>";
                }
            }
        }

        private void SetKeyHighlight(Image img, TextMeshProUGUI txt, bool isPressed, Color activeColor)
        {
            if (img == null) return;
            img.color = isPressed ? activeColor : new Color(0.10f, 0.16f, 0.24f, 1f);
            if (txt != null)
            {
                txt.color = isPressed ? new Color(0.04f, 0.07f, 0.10f, 1f) : ColorTextLight;
            }
        }

        public void OpenTutorial(PlayerController player, Action onClose)
        {
            activePlayer = player;
            onClosedCallback = onClose;

            EnsureUIHierarchy();

            if (modalPanel != null)
            {
                modalPanel.SetActive(true);
            }

            if (activePlayer != null)
            {
                activePlayer.SetControlEnabled(false);
            }

            FindAndSubdueBackgroundHUD(true);

            savedCursorVisible = Cursor.visible;
            savedCursorLockMode = Cursor.lockState;
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;

            SelectChapter(currentTabIndex);
        }

        public void CloseTutorial()
        {
            if (modalPanel != null)
            {
                modalPanel.SetActive(false);
            }

            if (activePlayer != null)
            {
                activePlayer.SetControlEnabled(true);
                activePlayer = null;
            }

            FindAndSubdueBackgroundHUD(false);

            Cursor.visible = savedCursorVisible;
            Cursor.lockState = savedCursorLockMode;

            onClosedCallback?.Invoke();
            onClosedCallback = null;
        }

        private void FindAndSubdueBackgroundHUD(bool subdue)
        {
            if (subdue)
            {
                subduedHudObjects.Clear();
                string[] hudNames = { "ObjectiveText", "SessionStatusText", "CountdownText", "InteractionPrompt_Container", "SimpleObjectiveView", "PlayerHUD" };
                foreach (var name in hudNames)
                {
                    var go = GameObject.Find(name);
                    if (go != null && go.activeSelf)
                    {
                        go.SetActive(false);
                        subduedHudObjects.Add(go);
                    }
                }
            }
            else
            {
                foreach (var go in subduedHudObjects)
                {
                    if (go != null) go.SetActive(true);
                }
                subduedHudObjects.Clear();
            }
        }

        public void SetProgressStateExplicit(int visitedUpToIndex, bool completed = false)
        {
            currentTabIndex = Mathf.Clamp(visitedUpToIndex, 0, 4);
            for (int i = 0; i < 5; i++)
            {
                chaptersVisited[i] = (i <= visitedUpToIndex);
            }
            isOrientationFullyCompleted = completed;
            UpdateContent();
            UpdateNavigationState();
        }

        public void CompleteOrientation()
        {
            isOrientationFullyCompleted = true;
            PlayerPrefs.SetInt("TLS_CrewOrientationCompleted", 1);
            PlayerPrefs.Save();
            UpdateContent();
            UpdateNavigationState();
            CloseTutorial();
        }

        public void SelectChapter(int index)
        {
            if (index < 0 || index >= 5) return;
            currentTabIndex = index;
            chaptersVisited[index] = true;

            UpdateContent();
            UpdateNavigationState();
        }

        public void NextChapter()
        {
            if (currentTabIndex < 4)
            {
                SelectChapter(currentTabIndex + 1);
            }
            else
            {
                int visitedCount = 0;
                for (int i = 0; i < 5; i++) if (chaptersVisited[i]) visitedCount++;
                if (visitedCount >= 5 && !isOrientationFullyCompleted)
                {
                    CompleteOrientation();
                }
                else
                {
                    CloseTutorial();
                }
            }
        }

        public void PreviousChapter()
        {
            if (currentTabIndex > 0)
            {
                SelectChapter(currentTabIndex - 1);
            }
        }

        private void UpdateContent()
        {
            if (chapterNumberText != null)
            {
                chapterNumberText.text = $"CHAPTER 0{currentTabIndex + 1} // 05";
            }

            switch (currentTabIndex)
            {
                case 0:
                    if (chapterTitleText != null) chapterTitleText.text = "LOCOMOTION & ACTION CONTROLS";
                    if (chapterSubtitleText != null) chapterSubtitleText.text = "Interact with physical keys on your keyboard to verify onboard movement and tool response.";
                    if (calloutBoxText != null) calloutBoxText.text = "<color=#F59E0B><b>TACTICAL ADVICE  —</b></color> <b>Save sprint stamina.</b> An exhausted crew member cannot outrun The Shade in the dark.";
                    break;
                case 1:
                    if (chapterTitleText != null) chapterTitleText.text = "CREW COMPLEMENT SPECIALIZATIONS";
                    if (chapterSubtitleText != null) chapterSubtitleText.text = "Coordinate survivor roles. Combine high repair speed with defensive EMP escorts.";
                    if (calloutBoxText != null) calloutBoxText.text = "<color=#F59E0B><b>TACTICAL ADVICE  —</b></color> <b>The Shade hunts isolated crew.</b> Protect technicians with Logistics EMP while repairing!";
                    break;
                case 2:
                    if (chapterTitleText != null) chapterTitleText.text = "POWER RESTORATION WORKFLOW";
                    if (chapterSubtitleText != null) chapterSubtitleText.text = "Restore offline substations as a coordinated team to engage ceiling Safe Lights and open blast doors.";
                    if (calloutBoxText != null) calloutBoxText.text = "<color=#F59E0B><b>TACTICAL ADVICE  —</b></color> <b>Power brings safety.</b> Restore regional power early to turn on Safe Lights and reveal the predator.";
                    break;
                case 3:
                    if (chapterTitleText != null) chapterTitleText.text = "DARKNESS & SANITY SURVIVAL";
                    if (chapterSubtitleText != null) chapterSubtitleText.text = "Maintain psychological fortitude in pitch darkness, locate Safe Lights, and evade The Shade.";
                    if (calloutBoxText != null) calloutBoxText.text = "<color=#F59E0B><b>TACTICAL ADVICE  —</b></color> <b>Flashlights do not heal sanity.</b> Only ceiling Safe Lights or staying close to teammates restores mental fortitude.";
                    break;
                case 4:
                    if (chapterTitleText != null) chapterTitleText.text = "MISSION TIMELINE & EVACUATION";
                    if (chapterSubtitleText != null) chapterSubtitleText.text = "Follow the 3 operational mission phases from Bedroom spawn to emergency Airlock extraction.";
                    if (calloutBoxText != null) calloutBoxText.text = "<color=#F59E0B><b>TACTICAL ADVICE  —</b></color> <b>Clear your escape route first!</b> Unlock blast doors before initiating the reactor override.";
                    break;
            }

            for (int i = 0; i < 5; i++)
            {
                if (chapterViews[i] != null)
                {
                    chapterViews[i].SetActive(i == currentTabIndex);
                }
            }

            int visitedCount = 0;
            for (int i = 0; i < 5; i++)
            {
                if (chaptersVisited[i]) visitedCount++;
            }

            if (headerProgressText != null)
            {
                if (isOrientationFullyCompleted)
                {
                    headerProgressText.text = "<color=#22C55E>ORIENTATION: COMPLETE</color>";
                }
                else if (visitedCount >= 5)
                {
                    headerProgressText.text = "<color=#38BDF8>ORIENTATION: 05 / 05 REVIEWED</color>";
                }
                else
                {
                    headerProgressText.text = $"<color=#38BDF8>ORIENTATION: 0{visitedCount} / 05 REVIEWED</color>";
                }
            }

            for (int i = 0; i < 5; i++)
            {
                bool isCurrent = (i == currentTabIndex);
                bool isViewed = chaptersVisited[i];

                if (tabButtonBgs[i] != null)
                {
                    tabButtonBgs[i].color = isCurrent ? TabActiveBg : TabNormalBg;
                }

                if (tabActiveIndicators[i] != null)
                {
                    tabActiveIndicators[i].SetActive(isCurrent);
                }

                if (tabButtonTexts[i] != null)
                {
                    string reviewTag = isViewed ? "  <color=#22C55E>DONE</color>" : "";
                    tabButtonTexts[i].color = isCurrent ? TabActiveText : (isViewed ? ColorTextLight : TabNormalText);
                    tabButtonTexts[i].text = GetTabShortTitle(i) + reviewTag;
                }
            }

            if (sidebarStatusText != null)
            {
                if (isOrientationFullyCompleted)
                {
                    sidebarStatusText.text = "<color=#22C55E><b>STATUS: READY FOR DUTY</b></color>";
                }
                else if (visitedCount >= 5)
                {
                    sidebarStatusText.text = "<color=#F59E0B><b>STATUS: READY TO COMPLETE</b></color>";
                }
                else
                {
                    sidebarStatusText.text = "<color=#38BDF8><b>STATUS: IN PROGRESS</b></color>";
                }
            }

            if (completionStatusText != null)
            {
                completionStatusText.text = string.Empty;
            }
        }

        private string GetTabShortTitle(int index)
        {
            return index switch
            {
                0 => "01  CONTROLS",
                1 => "02  CREW ROLES",
                2 => "03  SHIP SYSTEMS",
                3 => "04  SANITY & SHADE",
                _ => "05  EVACUATION"
            };
        }

        private void UpdateNavigationState()
        {
            if (btnPrevious != null)
            {
                btnPrevious.interactable = (currentTabIndex > 0);
            }
            if (prevButtonText != null)
            {
                prevButtonText.color = (currentTabIndex > 0) ? ColorTextLight : new Color(0.4f, 0.45f, 0.5f, 0.6f);
            }

            if (nextButtonText != null)
            {
                if (currentTabIndex == 4)
                {
                    nextButtonText.text = isOrientationFullyCompleted ? "ORIENTATION COMPLETED" : "COMPLETE ORIENTATION  (E)";
                }
                else
                {
                    nextButtonText.text = "NEXT CHAPTER  >";
                }
            }
        }

        public void RebuildUI()
        {
            if (modalPanel != null)
            {
                DestroyImmediate(modalPanel);
                modalPanel = null;
            }

            var allPanels = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var p in allPanels)
            {
                if (p != null && p.name == "Crew_Orientation_Panel")
                {
                    DestroyImmediate(p.gameObject);
                }
            }

            BuildUI();
        }

        public void EnsureUIHierarchy()
        {
            if (modalPanel != null)
            {
                if (modalPanel.transform.Find("Orientation_Card/Content_Container/Modular_Views_Container") == null)
                {
                    RebuildUI();
                    return;
                }
                return;
            }

            Transform existing = transform.Find("Crew_Orientation_Panel");
            if (existing == null)
            {
                var allPanels = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                foreach (var p in allPanels)
                {
                    if (p != null && p.name == "Crew_Orientation_Panel")
                    {
                        existing = p;
                        break;
                    }
                }
            }

            if (existing != null)
            {
                if (existing.Find("Orientation_Card/Content_Container/Modular_Views_Container") == null)
                {
                    RebuildUI();
                    return;
                }
                modalPanel = existing.gameObject;
                BindExistingElements();
                return;
            }

            BuildUI();
        }

        private void BindExistingElements()
        {
            if (modalPanel == null) return;

            var texts = modalPanel.GetComponentsInChildren<TextMeshProUGUI>(true);
            foreach (var t in texts)
            {
                if (t.name == "Header_Title") headerTitleText = t;
                if (t.name == "Header_Progress") headerProgressText = t;
                if (t.name == "Chapter_Number") chapterNumberText = t;
                if (t.name == "Chapter_Title") chapterTitleText = t;
                if (t.name == "Chapter_Subtitle") chapterSubtitleText = t;
                if (t.name == "Callout_Text") calloutBoxText = t;
                if (t.name == "Completion_Status") completionStatusText = t;
                if (t.name == "Sidebar_Status") sidebarStatusText = t;
                if (t.name == "Btn_Prev_Text") prevButtonText = t;
                if (t.name == "Btn_Next_Text") nextButtonText = t;
            }

            var buttons = modalPanel.GetComponentsInChildren<Button>(true);
            foreach (var b in buttons)
            {
                if (b.name == "Btn_Prev") btnPrevious = b;
                if (b.name == "Btn_Next") btnNext = b;
                if (b.name == "Btn_Close") btnClose = b;
            }

            var sidebar = modalPanel.transform.Find("Orientation_Card/Sidebar_Container");
            if (sidebar != null)
            {
                for (int i = 0; i < 5; i++)
                {
                    var tabBtnTr = sidebar.Find($"Btn_Tab_{i}");
                    if (tabBtnTr != null)
                    {
                        tabButtons[i] = tabBtnTr.GetComponent<Button>();
                        tabButtonBgs[i] = tabBtnTr.GetComponent<Image>();
                        tabButtonTexts[i] = tabBtnTr.GetComponentInChildren<TextMeshProUGUI>();
                        var ind = tabBtnTr.Find("Active_Indicator");
                        if (ind != null) tabActiveIndicators[i] = ind.gameObject;
                    }
                }
            }

            var viewsContainer = modalPanel.transform.Find("Orientation_Card/Content_Container/Modular_Views_Container");
            if (viewsContainer != null)
            {
                var v1 = viewsContainer.Find("View_Ch01_Controls");
                var v2 = viewsContainer.Find("View_Ch02_Roles");
                var v3 = viewsContainer.Find("View_Ch03_Systems");
                var v4 = viewsContainer.Find("View_Ch04_Sanity");
                var v5 = viewsContainer.Find("View_Ch05_Evacuation");

                if (v1 != null) chapterViews[0] = v1.gameObject;
                if (v2 != null) chapterViews[1] = v2.gameObject;
                if (v3 != null) chapterViews[2] = v3.gameObject;
                if (v4 != null) chapterViews[3] = v4.gameObject;
                if (v5 != null) chapterViews[4] = v5.gameObject;
            }
        }

        private static Sprite LoadTutorialSprite(string spriteName)
        {
#if UNITY_EDITOR
            string path = $"Assets/_Project/Sprites/UI/Tutorial/{spriteName}.png";
            var sp = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sp != null) return sp;
#endif
            return null;
        }

        private void BuildUI()
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

            if (parentCanvas == null)
            {
                GameObject canvasGo = new GameObject("CrewOrientationCanvas");
                parentCanvas = canvasGo.AddComponent<Canvas>();
                parentCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                var scaler = canvasGo.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.matchWidthOrHeight = 0.5f;
                canvasGo.AddComponent<GraphicRaycaster>();
            }

            // 1. Fullscreen Backdrop (Translucent dark frosted glass: 88% alpha)
            GameObject rootModal = new GameObject("Crew_Orientation_Panel");
            rootModal.layer = 5;
            rootModal.transform.SetParent(parentCanvas.transform, false);
            modalPanel = rootModal;

            RectTransform rtModal = rootModal.AddComponent<RectTransform>();
            rtModal.anchorMin = Vector2.zero;
            rtModal.anchorMax = Vector2.one;
            rtModal.sizeDelta = Vector2.zero;

            Image backdrop = rootModal.AddComponent<Image>();
            backdrop.color = ColorBackdrop;
            backdrop.raycastTarget = true;

            // 2. Central Station Card - 1360 x 820 (Sleek minimalist container)
            GameObject cardGo = new GameObject("Orientation_Card");
            cardGo.layer = 5;
            cardGo.transform.SetParent(rootModal.transform, false);
            mainCard = cardGo.AddComponent<RectTransform>();
            mainCard.anchorMin = new Vector2(0.5f, 0.5f);
            mainCard.anchorMax = new Vector2(0.5f, 0.5f);
            mainCard.pivot = new Vector2(0.5f, 0.5f);
            mainCard.sizeDelta = new Vector2(1360, 820);

            Image cardBg = cardGo.AddComponent<Image>();
            cardBg.color = ColorCardBg;

            Outline cardOutline = cardGo.AddComponent<Outline>();
            cardOutline.effectColor = ColorCardBorder;
            cardOutline.effectDistance = new Vector2(1, -1);

            // Top Header Bar (Height 64px)
            BuildHeader(cardGo.transform);

            // Left Navigation Sidebar (Width 280px)
            BuildSidebar(cardGo.transform);

            // Main Content Area (Width 1020px)
            BuildMainContent(cardGo.transform);

            // Bottom Navigation Bar (Height 56px)
            BuildBottomBar(cardGo.transform);

            modalPanel.SetActive(false);
        }

        private void BuildHeader(Transform parent)
        {
            GameObject header = new GameObject("Header_Bar");
            header.layer = 5;
            header.transform.SetParent(parent, false);
            RectTransform rt = header.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(0, 64);

            Image bg = header.AddComponent<Image>();
            bg.color = new Color(0.045f, 0.07f, 0.10f, 1f);

            // Header Title
            GameObject titleGo = CreateText(header.transform, "Header_Title",
                "CREW MUSTER & ORIENTATION // USS AETHELWULF",
                new Vector2(28, 0), new Vector2(560, 36), 17.5f, FontStyles.Bold, ColorCyan,
                TextAlignmentOptions.MidlineLeft, new Vector2(0, 0.5f),
                new Vector2(0, 0.5f), new Vector2(0, 0.5f));
            headerTitleText = titleGo.GetComponent<TextMeshProUGUI>();

            // Non-interactive Progress Readout
            GameObject progGo = CreateText(header.transform, "Header_Progress",
                "ORIENTATION: 01 / 05 REVIEWED",
                new Vector2(-180, 0), new Vector2(340, 32), 14f, FontStyles.Bold, ColorCyan,
                TextAlignmentOptions.MidlineRight, new Vector2(1, 0.5f),
                new Vector2(1, 0.5f), new Vector2(1, 0.5f));
            headerProgressText = progGo.GetComponent<TextMeshProUGUI>();

            // Close button ESC CLOSE
            GameObject btnCloseGo = CreateButton(header.transform, "Btn_Close", "ESC  CLOSE",
                new Vector2(-20, 0), new Vector2(140, 36), 12.5f, new Color(0.09f, 0.14f, 0.20f, 1f),
                ColorTextLight, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f));
            btnClose = btnCloseGo.GetComponent<Button>();
            btnClose.onClick.AddListener(CloseTutorial);

            // Subtle divider line
            CreateDivider(parent, new Vector2(0, -64), 1360, new Color(0.22f, 0.74f, 0.97f, 0.25f));
        }

        private void BuildSidebar(Transform parent)
        {
            GameObject sidebarGo = new GameObject("Sidebar_Container");
            sidebarGo.layer = 5;
            sidebarGo.transform.SetParent(parent, false);
            RectTransform rt = sidebarGo.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(20, -78);
            rt.sizeDelta = new Vector2(280, 675);

            for (int i = 0; i < 5; i++)
            {
                int index = i;
                float yPos = -(i * 80);
                string title = GetTabShortTitle(i);

                GameObject btnGo = CreateButton(sidebarGo.transform, $"Btn_Tab_{i}", title,
                    new Vector2(0, yPos), new Vector2(280, 68), 13.5f, TabNormalBg, TabNormalText,
                    new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), hasOutline: false);

                Button btn = btnGo.GetComponent<Button>();
                btn.onClick.AddListener(() => SelectChapter(index));

                // Left active indicator strip (Cyan 4px wide)
                GameObject indicator = new GameObject("Active_Indicator");
                indicator.layer = 5;
                indicator.transform.SetParent(btnGo.transform, false);
                RectTransform indRt = indicator.AddComponent<RectTransform>();
                indRt.anchorMin = new Vector2(0, 0);
                indRt.anchorMax = new Vector2(0, 1);
                indRt.pivot = new Vector2(0, 0.5f);
                indRt.anchoredPosition = Vector2.zero;
                indRt.sizeDelta = new Vector2(4, 0);
                Image indImg = indicator.AddComponent<Image>();
                indImg.color = ColorCyan;
                indicator.SetActive(false);

                tabButtons[i] = btn;
                tabButtonBgs[i] = btnGo.GetComponent<Image>();
                tabButtonTexts[i] = btnGo.GetComponentInChildren<TextMeshProUGUI>();
                tabActiveIndicators[i] = indicator;
            }

            // Sidebar Status Box at bottom
            GameObject statusBox = CreateCardPanel(sidebarGo.transform, "Sidebar_Status_Box",
                new Vector2(0, 16), new Vector2(0, 48), isFullWidth: true, hasOutline: false,
                customBg: new Color(0.045f, 0.07f, 0.10f, 0.90f));
            RectTransform sbRt = statusBox.GetComponent<RectTransform>();
            sbRt.anchorMin = new Vector2(0, 0);
            sbRt.anchorMax = new Vector2(1, 0);
            sbRt.pivot = new Vector2(0.5f, 0);
            sbRt.anchoredPosition = new Vector2(0, 16);

            GameObject statusTxtGo = CreateText(statusBox.transform, "Sidebar_Status",
                "<color=#38BDF8><b>STATUS: IN PROGRESS</b></color>",
                Vector2.zero, Vector2.zero, 13f, FontStyles.Bold, ColorTextLight,
                TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one);
            sidebarStatusText = statusTxtGo.GetComponent<TextMeshProUGUI>();

            // Vertical divider between sidebar and content
            GameObject vDiv = new GameObject("Vertical_Divider");
            vDiv.layer = 5;
            vDiv.transform.SetParent(parent, false);
            RectTransform rtDiv = vDiv.AddComponent<RectTransform>();
            rtDiv.anchorMin = new Vector2(0, 0.5f);
            rtDiv.anchorMax = new Vector2(0, 0.5f);
            rtDiv.pivot = new Vector2(0.5f, 0.5f);
            rtDiv.anchoredPosition = new Vector2(318, -32);
            rtDiv.sizeDelta = new Vector2(1, 690);
            Image divImg = vDiv.AddComponent<Image>();
            divImg.color = new Color(0.14f, 0.22f, 0.30f, 0.40f);
        }

        private void BuildMainContent(Transform parent)
        {
            GameObject contentGo = new GameObject("Content_Container");
            contentGo.layer = 5;
            contentGo.transform.SetParent(parent, false);
            RectTransform rt = contentGo.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(338, -74);
            rt.sizeDelta = new Vector2(-358, 680);

            // Chapter Number
            GameObject numGo = CreateText(contentGo.transform, "Chapter_Number", "CHAPTER 01 // 05",
                new Vector2(0, -12), new Vector2(350, 22), 13.5f, FontStyles.Bold, ColorCyan,
                TextAlignmentOptions.MidlineLeft, new Vector2(0, 0.5f));
            chapterNumberText = numGo.GetComponent<TextMeshProUGUI>();

            // Chapter Title (22pt bold)
            GameObject titleGo = CreateText(contentGo.transform, "Chapter_Title", "LOCOMOTION & ACTION CONTROLS",
                new Vector2(0, -40), new Vector2(980, 34), 22f, FontStyles.Bold, ColorTextLight,
                TextAlignmentOptions.MidlineLeft, new Vector2(0, 0.5f));
            chapterTitleText = titleGo.GetComponent<TextMeshProUGUI>();

            // Chapter Subtitle (Brief, 13.5pt)
            GameObject subGo = CreateText(contentGo.transform, "Chapter_Subtitle", "Interact with physical keys on your keyboard to verify onboard movement and tool response.",
                new Vector2(0, -70), new Vector2(980, 24), 13.5f, FontStyles.Normal, ColorSlate,
                TextAlignmentOptions.MidlineLeft, new Vector2(0, 0.5f));
            chapterSubtitleText = subGo.GetComponent<TextMeshProUGUI>();

            // Divider line below chapter header
            CreateDivider(contentGo.transform, new Vector2(0, -90), 980, new Color(0.18f, 0.28f, 0.38f, 0.45f));

            // View Container for 5 Chapters (Y: -100, Height: 480px)
            GameObject viewsContainer = new GameObject("Modular_Views_Container");
            viewsContainer.layer = 5;
            viewsContainer.transform.SetParent(contentGo.transform, false);
            RectTransform rtViews = viewsContainer.AddComponent<RectTransform>();
            rtViews.anchorMin = new Vector2(0, 1);
            rtViews.anchorMax = new Vector2(1, 1);
            rtViews.pivot = new Vector2(0, 1);
            rtViews.anchoredPosition = new Vector2(0, -100);
            rtViews.sizeDelta = new Vector2(0, 480);

            // Build Individual Modular Chapter Views (Hero Showcase Architecture)
            chapterViews[0] = BuildViewCh01_Controls(viewsContainer.transform);
            chapterViews[1] = BuildViewCh02_Roles(viewsContainer.transform);
            chapterViews[2] = BuildViewCh03_Systems(viewsContainer.transform);
            chapterViews[3] = BuildViewCh04_Sanity(viewsContainer.transform);
            chapterViews[4] = BuildViewCh05_Evacuation(viewsContainer.transform);

            // Tactical Advice Banner at bottom of content (Y: -590, Height: 68px)
            GameObject calloutGo = CreateCardPanel(contentGo.transform, "Callout_Box",
                new Vector2(0, -590), new Vector2(0, 68), isFullWidth: true, hasOutline: false,
                customBg: new Color(0.08f, 0.06f, 0.04f, 0.85f));
            calloutBoxContainer = calloutGo;

            AddLeftAccentStripe(calloutGo.transform, ColorAmber, 4f);

            GameObject calloutTxtGo = CreateText(calloutGo.transform, "Callout_Text",
                "<color=#F59E0B><b>TACTICAL ADVICE  —</b></color> <b>Save sprint stamina.</b> An exhausted crew member cannot outrun The Shade in the dark.",
                new Vector2(20, -12), new Vector2(940, 46), 13.5f, FontStyles.Normal, ColorTextLight,
                TextAlignmentOptions.TopLeft, new Vector2(0, 1f));
            calloutBoxText = calloutTxtGo.GetComponent<TextMeshProUGUI>();
        }

        // ====================================================================
        // CHAPTER 01 VIEW: HERO KEYBOARD SHOWCASE
        // 1 Giant Interactive Centerpiece + 3-Action Summary Bar Below
        // ====================================================================
        private GameObject BuildViewCh01_Controls(Transform parent)
        {
            GameObject view = new GameObject("View_Ch01_Controls");
            view.layer = 5;
            view.transform.SetParent(parent, false);
            RectTransform rt = view.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.sizeDelta = Vector2.zero;

            // 1. HERO KEYBOARD SHOWCASE (Central Interactive Panel: 980 x 375)
            GameObject hero = CreateCardPanel(view.transform, "Hero_Keyboard_Showcase",
                new Vector2(0, 0), new Vector2(0, 375), isFullWidth: true);
            AddLeftAccentStripe(hero.transform, ColorCyan, 4f);

            // --- LEFT WING: LOCOMOTION INPUTS ---
            CreateText(hero.transform, "Loco_Header", "LOCOMOTION INPUTS",
                new Vector2(36, -24), new Vector2(400, 24), 15f, FontStyles.Bold, ColorCyan,
                TextAlignmentOptions.MidlineLeft, new Vector2(0, 0.5f));

            // WASD Keycaps (Inverted-T, Large size: 56 x 50)
            var wGo = CreateKeycap(hero.transform, "Key_W", "W", new Vector2(128, -60), new Vector2(56, 50), 16f);
            var aGo = CreateKeycap(hero.transform, "Key_A", "A", new Vector2(64, -118), new Vector2(56, 50), 16f);
            var sGo = CreateKeycap(hero.transform, "Key_S", "S", new Vector2(128, -118), new Vector2(56, 50), 16f);
            var dGo = CreateKeycap(hero.transform, "Key_D", "D", new Vector2(192, -118), new Vector2(56, 50), 16f);

            keyW_Img = wGo.GetComponent<Image>(); keyW_Txt = wGo.GetComponentInChildren<TextMeshProUGUI>();
            keyA_Img = aGo.GetComponent<Image>(); keyA_Txt = aGo.GetComponentInChildren<TextMeshProUGUI>();
            keyS_Img = sGo.GetComponent<Image>(); keyS_Txt = sGo.GetComponentInChildren<TextMeshProUGUI>();
            keyD_Img = dGo.GetComponent<Image>(); keyD_Txt = dGo.GetComponentInChildren<TextMeshProUGUI>();

            // Sprint Keycap (Wide: 184 x 46)
            var shiftGo = CreateKeycap(hero.transform, "Key_Shift", "LEFT SHIFT", new Vector2(64, -182), new Vector2(184, 46), 13.5f);
            keyShift_Img = shiftGo.GetComponent<Image>();

            CreateText(hero.transform, "Shift_Label", "<color=#F59E0B>HOLD TO SPRINT</color>  (Uses Stamina)",
                new Vector2(264, -186), new Vector2(200, 38), 12f, FontStyles.Normal, ColorSlate,
                TextAlignmentOptions.MidlineLeft, new Vector2(0, 0.5f));

            CreateText(hero.transform, "Secondary_Arrows", "Secondary Controls:  [ ^ ][ < ][ v ][ > ] Arrow Keys",
                new Vector2(64, -244), new Vector2(380, 22), 12f, FontStyles.Normal, ColorSlate,
                TextAlignmentOptions.MidlineLeft, new Vector2(0, 0.5f));

            // Central Hairline Divider
            CreateVerticalDivider(hero.transform, new Vector2(470, -20), 260, new Color(0.18f, 0.28f, 0.38f, 0.40f));

            // --- RIGHT WING: ACTION & EQUIPMENT ---
            CreateText(hero.transform, "Action_Header", "ACTION & EQUIPMENT",
                new Vector2(510, -24), new Vector2(400, 24), 15f, FontStyles.Bold, ColorCyan,
                TextAlignmentOptions.MidlineLeft, new Vector2(0, 0.5f));

            // Mouse Aim Keycap
            var mouseGo = CreateKeycap(hero.transform, "Key_Mouse", "MOUSE AIM", new Vector2(510, -68), new Vector2(160, 48), 13.5f);
            keyMouse_Img = mouseGo.GetComponent<Image>();

            CreateText(hero.transform, "Mouse_Desc", "<b>Direct Flashlight Beam</b>\n<color=#94A3B8>Illuminates dark sectors. Cuts Sanity decay by 50%.</color>",
                new Vector2(685, -68), new Vector2(270, 48), 12.5f, FontStyles.Normal, ColorTextLight,
                TextAlignmentOptions.TopLeft, new Vector2(0, 1f));

            // E Interact Keycap
            var eGo = CreateKeycap(hero.transform, "Key_E", "E KEY", new Vector2(510, -140), new Vector2(160, 48), 13.5f);
            keyE_Img = eGo.GetComponent<Image>();

            CreateText(hero.transform, "E_Desc", "<b>Operate & Repair Grid</b>\n<color=#94A3B8>Access terminals, fix substations, and unseal blast doors.</color>",
                new Vector2(685, -140), new Vector2(270, 48), 12.5f, FontStyles.Normal, ColorTextLight,
                TextAlignmentOptions.TopLeft, new Vector2(0, 1f));

            // Live Input Test Banner (Centered at bottom of Hero Showcase)
            var testPromptGo = CreateText(hero.transform, "Test_Prompt",
                "<color=#38BDF8><b>LIVE TEST: PRESS WASD / SHIFT / E / MOUSE</b></color>",
                new Vector2(490, -315), new Vector2(980, 26), 13f, FontStyles.Bold, ColorCyan,
                TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f));
            testLocomotionPrompt_Txt = testPromptGo.GetComponent<TextMeshProUGUI>();

            // 2. BOTTOM 3-ACTION SUMMARY BAR (Height 85px, Y: -390)
            GameObject summaryBar = CreateCardPanel(view.transform, "Controls_Summary_Bar",
                new Vector2(0, -390), new Vector2(0, 85), isFullWidth: true);

            float colW = 310f;
            CreateText(summaryBar.transform, "Col1",
                "<color=#38BDF8><b>WASD / ARROWS</b></color>\n<size=92%><color=#94A3B8>Cardinal 8-directional locomotion across compartments.</color></size>",
                new Vector2(24, 0), new Vector2(colW, 60), 12.5f, FontStyles.Normal, ColorTextLight,
                TextAlignmentOptions.MidlineLeft, new Vector2(0, 0.5f));

            CreateText(summaryBar.transform, "Col2",
                "<color=#F59E0B><b>HOLD LEFT SHIFT</b></color>\n<size=92%><color=#94A3B8>Sprint acceleration. Preserve stamina to flee ambushes.</color></size>",
                new Vector2(348, 0), new Vector2(colW, 60), 12.5f, FontStyles.Normal, ColorTextLight,
                TextAlignmentOptions.MidlineLeft, new Vector2(0, 0.5f));

            CreateText(summaryBar.transform, "Col3",
                "<color=#22C55E><b>MOUSE + E KEY</b></color>\n<size=92%><color=#94A3B8>Aim directional beam and hold E to channel repairs.</color></size>",
                new Vector2(672, 0), new Vector2(colW, 60), 12.5f, FontStyles.Normal, ColorTextLight,
                TextAlignmentOptions.MidlineLeft, new Vector2(0, 0.5f));

            return view;
        }

        // ====================================================================
        // CHAPTER 02 VIEW: 3 HERO SPECIALIZATION CARDS
        // 3 Towering Character Columns + 1 Dedicated Hostile Alert Banner
        // ====================================================================
        private GameObject BuildViewCh02_Roles(Transform parent)
        {
            GameObject view = new GameObject("View_Ch02_Roles");
            view.layer = 5;
            view.transform.SetParent(parent, false);
            RectTransform rt = view.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.sizeDelta = Vector2.zero;

            // 3 Towering Hero Cards (Width: 310px each, Height: 375px, Spacing: 25px)
            float cardW = 310f;
            float spacing = 335f;

            // Card 1: ENGINEER
            BuildToweringRoleCard(view.transform, new Vector2(0, 0), cardW, 375f,
                "Icon_Role_Engineer", "ENGINEER", "ELECTRICAL OVERHAUL", ColorCyan,
                "<color=#38BDF8>Stamina: 140</color>  (Enhanced)\n<color=#22C55E>Repair: +40% Speed (1.4x)</color>",
                "Rapid electrical grid overhaul and substation bypass.\n\n<color=#F8FAFC><b>Specialty:</b> Solves blackout crises in record time.</color>");

            // Card 2: MECHANIC
            BuildToweringRoleCard(view.transform, new Vector2(spacing, 0), cardW, 375f,
                "Icon_Role_Mechanic", "MECHANIC", "HULL & LIFE SUPPORT", ColorCyan,
                "<color=#38BDF8>Stamina: 100</color>  (Standard)\n<color=#22C55E>Repair: Standard (1.0x)</color>",
                "Pneumatic conduits, hydraulic blast doors & airlock seals.\n\n<color=#F8FAFC><b>Specialty:</b> Rapid pathway unsealing and life support.</color>");

            // Card 3: LOGISTICS
            BuildToweringRoleCard(view.transform, new Vector2(spacing * 2, 0), cardW, 375f,
                "Icon_Role_Logistics", "LOGISTICS", "DEFENSIVE ESCORT", ColorAmber,
                "<color=#38BDF8>Stamina: 100</color>  (Standard)\n<color=#F59E0B>EMP Pulse: 4.0m Range</color>",
                "Defensive escort equipped with high-yield EMP shockwave.\n\n<color=#EF4444><b>Specialty: STUNS The Shade for 3 seconds!</b></color>");

            // Dedicated Hostile Alert Banner for The Shade (Height 85px, Y: -390)
            GameObject shadeAlert = CreateCardPanel(view.transform, "Shade_Threat_Banner",
                new Vector2(0, -390), new Vector2(0, 85), isFullWidth: true,
                customBg: new Color(0.08f, 0.04f, 0.06f, 0.90f));
            AddLeftAccentStripe(shadeAlert.transform, ColorDanger, 5f);

            CreateCardIcon(shadeAlert.transform, "Icon_Role_Shade", new Vector2(16, 0), new Vector2(50, 50), isCenterY: true);

            CreateText(shadeAlert.transform, "Shade_Header",
                "<color=#EF4444><b>BIOLOGICAL HAZARD // THE SHADE</b>  (Extraterrestrial Apex Predator — Non-Playable)</color>",
                new Vector2(80, -14), new Vector2(880, 24), 14.5f, FontStyles.Bold, ColorDanger,
                TextAlignmentOptions.MidlineLeft, new Vector2(0, 0.5f));

            CreateText(shadeAlert.transform, "Shade_Body",
                "Stalks darkness to ambush isolated crew. Invulnerable to weapons.\n<color=#EF4444><b>SURVIVAL MANDATE:</b> Never explore alone. Stick together under powered Safe Lights!</color>",
                new Vector2(80, -42), new Vector2(880, 36), 12.5f, FontStyles.Normal, ColorTextMuted,
                TextAlignmentOptions.TopLeft, new Vector2(0, 1f));

            return view;
        }

        private void BuildToweringRoleCard(Transform parent, Vector2 pos, float width, float height, string iconName, string roleTitle, string badge, Color accentColor, string stats, string duty)
        {
            GameObject card = CreateCardPanel(parent, $"Card_{roleTitle}", pos, new Vector2(width, height));
            AddLeftAccentStripe(card.transform, accentColor, 4f);

            // Large Hero Icon Centered at Top (68x68)
            GameObject iconGo = new GameObject("Role_Icon");
            iconGo.layer = 5;
            iconGo.transform.SetParent(card.transform, false);
            RectTransform iconRt = iconGo.AddComponent<RectTransform>();
            iconRt.anchorMin = new Vector2(0.5f, 1);
            iconRt.anchorMax = new Vector2(0.5f, 1);
            iconRt.pivot = new Vector2(0.5f, 1);
            iconRt.anchoredPosition = new Vector2(0, -22);
            iconRt.sizeDelta = new Vector2(68, 68);

            Image iconImg = iconGo.AddComponent<Image>();
            Sprite sp = LoadTutorialSprite(iconName);
            iconImg.sprite = sp;
            iconImg.color = sp != null ? Color.white : accentColor;

            // Role Title
            CreateText(card.transform, "Role_Title", roleTitle,
                new Vector2(width / 2f, -104), new Vector2(width - 24, 26), 18f, FontStyles.Bold, ColorTextLight,
                TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f));

            // Badge
            CreateText(card.transform, "Role_Badge", badge,
                new Vector2(width / 2f, -128), new Vector2(width - 24, 20), 12f, FontStyles.Bold, accentColor,
                TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f));

            // Divider
            CreateDivider(card.transform, new Vector2(0, -146), width - 36, new Color(0.18f, 0.28f, 0.38f, 0.45f));

            // Stats Box
            GameObject statsBox = CreateCardPanel(card.transform, "Stats_Box",
                new Vector2(14, -156), new Vector2(width - 28, 60), isFullWidth: false,
                customBg: new Color(0.04f, 0.06f, 0.09f, 0.90f));
            CreateText(statsBox.transform, "Stats_Text", stats,
                new Vector2(10, 0), new Vector2(width - 48, 50), 12.5f, FontStyles.Normal, ColorTextLight,
                TextAlignmentOptions.MidlineLeft, new Vector2(0, 0.5f));

            // Duty Description
            CreateText(card.transform, "Role_Duty", duty,
                new Vector2(16, -228), new Vector2(width - 32, 130), 12.5f, FontStyles.Normal, ColorTextMuted,
                TextAlignmentOptions.TopLeft, new Vector2(0, 1f));
        }

        // ====================================================================
        // CHAPTER 03 VIEW: LINEAR 3-STEP FLOWCHART
        // Connected Horizontal Workflow + High-Impact Teamwork Multiplier
        // ====================================================================
        private GameObject BuildViewCh03_Systems(Transform parent)
        {
            GameObject view = new GameObject("View_Ch03_Systems");
            view.layer = 5;
            view.transform.SetParent(parent, false);
            RectTransform rt = view.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.sizeDelta = Vector2.zero;

            // 3 Wide Step Pillars (Width: 295px each, Height: 250px)
            float cardW = 295f;
            float stepSpacing = 342f;

            // Step 1
            BuildSystemStepCard(view.transform, new Vector2(0, 0), cardW, 250f,
                "01. LOCATE CONSOLE", "Icon_Control_Interact",
                "Follow alarm klaxons to damaged consoles.\n\n<color=#38BDF8>Diagnoses offline power sectors across the ship.</color>");

            // Connector Arrow 1
            CreateText(view.transform, "Arrow_1", ">",
                new Vector2(cardW + 12, -125), new Vector2(40, 36), 24f, FontStyles.Bold, ColorCyan,
                TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f));

            // Step 2
            BuildSystemStepCard(view.transform, new Vector2(stepSpacing, 0), cardW, 250f,
                "02. REPAIR SUBSTATION", "Icon_System_Substation",
                "Approach damaged substation and <color=#22C55E><b>HOLD E</b></color>.\n\n<color=#F8FAFC>Channels electrical repairs into the ship grid.</color>");

            // Connector Arrow 2
            CreateText(view.transform, "Arrow_2", ">",
                new Vector2(stepSpacing + cardW + 12, -125), new Vector2(40, 36), 24f, FontStyles.Bold, ColorCyan,
                TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f));

            // Step 3
            BuildSystemStepCard(view.transform, new Vector2(stepSpacing * 2, 0), cardW, 250f,
                "03. POWER & LIGHTS ON", "Icon_Evacuation_Airlock",
                "Breakers engage.\n\n<color=#22C55E>Halts Sanity decay</color>, repels The Shade, and unseals heavy blast doors.");

            // PART B: Dedicated Teamwork Co-op Acceleration Component (Height 115px, Y: -265)
            BuildTeamworkBonusPanel(view.transform, new Vector2(0, -265));

            return view;
        }

        private void BuildSystemStepCard(Transform parent, Vector2 pos, float width, float height, string title, string iconName, string desc)
        {
            GameObject card = CreateCardPanel(parent, $"Step_{title.Substring(0, 2)}", pos, new Vector2(width, height));
            AddLeftAccentStripe(card.transform, ColorCyan, 4f);

            // Icon (60x60)
            GameObject iconGo = new GameObject("Step_Icon");
            iconGo.layer = 5;
            iconGo.transform.SetParent(card.transform, false);
            RectTransform iconRt = iconGo.AddComponent<RectTransform>();
            iconRt.anchorMin = new Vector2(0.5f, 1);
            iconRt.anchorMax = new Vector2(0.5f, 1);
            iconRt.pivot = new Vector2(0.5f, 1);
            iconRt.anchoredPosition = new Vector2(0, -20);
            iconRt.sizeDelta = new Vector2(60, 60);

            Image iconImg = iconGo.AddComponent<Image>();
            Sprite sp = LoadTutorialSprite(iconName);
            iconImg.sprite = sp;
            iconImg.color = sp != null ? Color.white : ColorCyan;

            // Title
            CreateText(card.transform, "Step_Title", title,
                new Vector2(width / 2f, -96), new Vector2(width - 24, 26), 15f, FontStyles.Bold, ColorTextLight,
                TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f));

            // Divider
            CreateDivider(card.transform, new Vector2(0, -118), width - 36, new Color(0.18f, 0.28f, 0.38f, 0.45f));

            // Description
            CreateText(card.transform, "Step_Desc", desc,
                new Vector2(16, -132), new Vector2(width - 32, 105), 13f, FontStyles.Normal, ColorTextMuted,
                TextAlignmentOptions.TopLeft, new Vector2(0, 1f));
        }

        private void BuildTeamworkBonusPanel(Transform parent, Vector2 pos)
        {
            GameObject card = CreateCardPanel(parent, "Teamwork_Bonus_Panel", pos, new Vector2(0, 115), isFullWidth: true);
            AddLeftAccentStripe(card.transform, ColorCyan, 4f);

            CreateCardIcon(card.transform, "Icon_Teamwork_Bonus", new Vector2(18, 0), new Vector2(56, 56), isCenterY: true);

            CreateText(card.transform, "Teamwork_Header",
                "<color=#38BDF8><b>TEAMWORK BONUS  —  COOPERATIVE REPAIR ACCELERATION</b></color>",
                new Vector2(88, -18), new Vector2(860, 24), 16f, FontStyles.Bold, ColorCyan,
                TextAlignmentOptions.MidlineLeft, new Vector2(0, 0.5f));

            CreateText(card.transform, "Teamwork_Formula",
                "<size=125%><color=#38BDF8><b>1 CREW = 1x SPEED</b></color>   •   <color=#22C55E><b>2 CREW = 2x SPEED</b></color>   •   <color=#22C55E><b>3 CREW = 3x SPEED!</b></color></size>",
                new Vector2(88, -44), new Vector2(860, 26), 14f, FontStyles.Bold, ColorTextLight,
                TextAlignmentOptions.MidlineLeft, new Vector2(0, 0.5f));

            CreateText(card.transform, "Teamwork_Desc",
                "<color=#94A3B8>Repair rates stack additively. Multiple crew members working together restore ship systems in seconds.</color>",
                new Vector2(88, -72), new Vector2(860, 32), 12.5f, FontStyles.Normal, ColorTextMuted,
                TextAlignmentOptions.TopLeft, new Vector2(0, 1f));
        }

        // ====================================================================
        // CHAPTER 04 VIEW: DUAL CONTRAST (DARKNESS VS SAFE LIGHT)
        // 4-Stage Cockpit Sanity Gauge + Dual Contrast Pillars
        // ====================================================================
        private GameObject BuildViewCh04_Sanity(Transform parent)
        {
            GameObject view = new GameObject("View_Ch04_Sanity");
            view.layer = 5;
            view.transform.SetParent(parent, false);
            RectTransform rt = view.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.sizeDelta = Vector2.zero;

            // PART A: Continuous 4-Segment Sanity Gauge Bar (Height 62px)
            BuildSanityStateVisualizer(view.transform, new Vector2(0, 0));

            // PART B: Dual Contrast Pillars (Width: 480px each, Height: 320px)
            float colW = 480f;

            // Left Pillar: DARKNESS HAZARDS (Crimson Accent)
            BuildSanityStructuredPillar(view.transform, new Vector2(0, -78), colW, 320f,
                "DARKNESS HAZARDS // THE THREAT", "Icon_Survival_Sanity", ColorDanger,
                new string[]
                {
                    "1. Pitch Darkness Decay", "Constant decay (-0.5 Sanity/sec) across all unlit ship corridors.",
                    "2. Critical Breakdown", "Camera shake, visual tunnel & hallucinations when Sanity drops below 15%.",
                    "3. The Shade Ambush", "Lethal predator stalks the shadows and executes isolated crew members."
                },
                "<color=#EF4444>Never explore dark bulkheads alone without light or a teammate!</color>");

            // Right Pillar: SAFE LIGHT & SURVIVAL (Emerald Accent)
            BuildSanityStructuredPillar(view.transform, new Vector2(500, -78), colW, 320f,
                "SAFE LIGHT DEFENSE // SURVIVAL", "Icon_Survival_SafeLight", ColorGreen,
                new string[]
                {
                    "1. Powered Safe Lights", "Ceiling luminaires fully halt decay & restore Sanity (+1.0/sec).",
                    "2. Handheld Flashlight", "Directional beam cuts darkness Sanity decay by 50% in front of you.",
                    "3. Crew Proximity (< 4m)", "Staying close to teammates cuts darkness Sanity drain by 50%."
                },
                "<color=#22C55E>Retreat to powered Safe Light zones whenever Sanity drops!</color>");

            return view;
        }

        private void BuildSanityStateVisualizer(Transform parent, Vector2 pos)
        {
            GameObject card = CreateCardPanel(parent, "Sanity_Visualizer_Panel", pos, new Vector2(0, 62), isFullWidth: true, hasOutline: false);

            float boxW = 236f;
            float spacing = 244f;

            BuildSanityStateSegment(card.transform, new Vector2(6, 0), boxW, "STABLE (71-100%)", "Clear vision & steady fortitude", ColorGreen);
            BuildSanityStateSegment(card.transform, new Vector2(6 + spacing, 0), boxW, "UNEASY (41-70%)", "Mild murmurs & tension", ColorCyan);
            BuildSanityStateSegment(card.transform, new Vector2(6 + spacing * 2, 0), boxW, "DISTRESSED (16-40%)", "Audio noise & dark vignette", ColorAmber);
            BuildSanityStateSegment(card.transform, new Vector2(6 + spacing * 3, 0), boxW, "BREAKING (0-15%)", "Camera shake & hallucinations", ColorDanger);
        }

        private void BuildSanityStateSegment(Transform parent, Vector2 pos, float width, string title, string sub, Color accentColor)
        {
            GameObject box = new GameObject($"Box_{title.Substring(0, 4)}");
            box.layer = 5;
            box.transform.SetParent(parent, false);
            RectTransform rt = box.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 0.5f);
            rt.anchorMax = new Vector2(0, 0.5f);
            rt.pivot = new Vector2(0, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(width, 48);

            Image bg = box.AddComponent<Image>();
            bg.color = new Color(0.045f, 0.07f, 0.10f, 0.90f);

            GameObject topStripe = new GameObject("Top_Stripe");
            topStripe.layer = 5;
            topStripe.transform.SetParent(box.transform, false);
            RectTransform tsRt = topStripe.AddComponent<RectTransform>();
            tsRt.anchorMin = new Vector2(0, 1);
            tsRt.anchorMax = new Vector2(1, 1);
            tsRt.pivot = new Vector2(0.5f, 1);
            tsRt.anchoredPosition = Vector2.zero;
            tsRt.sizeDelta = new Vector2(0, 2);
            Image tsImg = topStripe.AddComponent<Image>();
            tsImg.color = accentColor;

            CreateText(box.transform, "Title", title,
                new Vector2(6, -8), new Vector2(width - 12, 18), 12f, FontStyles.Bold, accentColor,
                TextAlignmentOptions.Center, new Vector2(0, 0.5f));

            CreateText(box.transform, "Sub", sub,
                new Vector2(6, -26), new Vector2(width - 12, 20), 11f, FontStyles.Normal, ColorTextMuted,
                TextAlignmentOptions.Center, new Vector2(0, 0.5f));
        }

        private void BuildSanityStructuredPillar(Transform parent, Vector2 pos, float width, float height, string header, string iconName, Color themeColor, string[] rulePairs, string mandate)
        {
            GameObject col = CreateCardPanel(parent, $"Pillar_{header.Substring(0, 6)}", pos, new Vector2(width, height));
            AddLeftAccentStripe(col.transform, themeColor, 4f);

            CreateCardIcon(col.transform, iconName, new Vector2(16, -12), new Vector2(46, 46));

            CreateText(col.transform, "Col_Header", header,
                new Vector2(72, -26), new Vector2(width - 85, 26), 14.5f, FontStyles.Bold, themeColor,
                TextAlignmentOptions.MidlineLeft, new Vector2(0, 0.5f));

            CreateDivider(col.transform, new Vector2(0, -60), width - 32, new Color(themeColor.r, themeColor.g, themeColor.b, 0.25f));

            float startY = -72f;
            float ruleGap = 62f;
            for (int r = 0; r < 3; r++)
            {
                int idx = r * 2;
                string rTitle = rulePairs[idx];
                string rDesc = rulePairs[idx + 1];
                float y = startY - (r * ruleGap);

                CreateText(col.transform, $"Rule_Title_{r}", $"<b>{rTitle}</b>",
                    new Vector2(18, y), new Vector2(width - 36, 20), 12.5f, FontStyles.Bold, ColorTextLight,
                    TextAlignmentOptions.MidlineLeft, new Vector2(0, 0.5f));

                CreateText(col.transform, $"Rule_Desc_{r}", rDesc,
                    new Vector2(18, y - 20), new Vector2(width - 36, 34), 12f, FontStyles.Normal, ColorTextMuted,
                    TextAlignmentOptions.TopLeft, new Vector2(0, 1f));
            }

            CreateText(col.transform, "Col_Mandate", $"<color=#94A3B8><b>TACTICAL MANDATE:</b></color> {mandate}",
                new Vector2(18, -264), new Vector2(width - 36, 42), 11.5f, FontStyles.Normal, ColorTextLight,
                TextAlignmentOptions.TopLeft, new Vector2(0, 1f));
        }

        // ====================================================================
        // CHAPTER 05 VIEW: 3-STAGE MISSION PIPELINE & VICTORY BANNER
        // Clean Pipeline Flow from Spawn to Emergency Airlock Extraction
        // ====================================================================
        private GameObject BuildViewCh05_Evacuation(Transform parent)
        {
            GameObject view = new GameObject("View_Ch05_Evacuation");
            view.layer = 5;
            view.transform.SetParent(parent, false);
            RectTransform rt = view.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.sizeDelta = Vector2.zero;

            // 3 Horizontal Phase Banners (Height: 90px each, Spacing: 104px)
            BuildPhaseTimelineCard(view.transform, new Vector2(0, 0),
                "PHASE 01  —  CREW MUSTER & CORRIDOR SWEEP", "Icon_Control_Interact",
                "<color=#94A3B8>ROUTE:</color> <color=#38BDF8>Bedroom Pods  >>  Orientation Terminal  >>  East Passage</color>",
                "Awaken in cryo-pods. Assign crew roles at Terminal, grab gear, and clear corridor sweep routes.",
                ColorCyan);

            BuildPhaseTimelineCard(view.transform, new Vector2(0, -104),
                "PHASE 02  —  POWER RELAYS & REACTOR OVERRIDE", "Icon_System_Substation",
                "<color=#94A3B8>ROUTE:</color> <color=#38BDF8>Substations  >>  Reactor Chamber  >>  Core Console</color>",
                "Restore power relays, advance to the Reactor Chamber, and press [E] to engage reactor core override.",
                ColorAmber);

            BuildPhaseTimelineCard(view.transform, new Vector2(0, -208),
                "PHASE 03  —  EMERGENCY AIRLOCK EXTRACTION", "Icon_Evacuation_Airlock",
                "<color=#94A3B8>ROUTE:</color> <color=#38BDF8>Reactor Core  >>  South Corridor  >>  Emergency Airlock</color>",
                "Run south to the Airlock together before the reactor meltdown timer expires!",
                ColorGreen);

            // Dedicated High-Impact Victory Banner (Height 90px, Y: -312)
            GameObject winBanner = CreateCardPanel(view.transform, "Victory_Banner",
                new Vector2(0, -312), new Vector2(0, 90), isFullWidth: true,
                customBg: new Color(0.04f, 0.08f, 0.05f, 0.90f));
            AddLeftAccentStripe(winBanner.transform, ColorGreen, 5f);

            CreateCardIcon(winBanner.transform, "Icon_Evacuation_Airlock", new Vector2(18, 0), new Vector2(52, 52), isCenterY: true);

            CreateText(winBanner.transform, "Win_Header",
                "<color=#22C55E><b>MISSION OBJECTIVE // SURVIVAL VICTORY CONDITION</b></color>",
                new Vector2(82, -14), new Vector2(880, 22), 14f, FontStyles.Bold, ColorGreen,
                TextAlignmentOptions.MidlineLeft, new Vector2(0, 0.5f));

            CreateText(winBanner.transform, "Win_BigText",
                "<size=125%><color=#22C55E><b>VICTORY CONDITION: AT LEAST 1 SURVIVOR EXTRACTS = WIN!</b></color></size>",
                new Vector2(82, -38), new Vector2(880, 26), 14.5f, FontStyles.Bold, ColorTextLight,
                TextAlignmentOptions.MidlineLeft, new Vector2(0, 0.5f));

            CreateText(winBanner.transform, "Win_Desc",
                "<color=#94A3B8>You do not need the full crew to extract alive — even a single survivor secures mission transmission!</color>",
                new Vector2(82, -62), new Vector2(880, 24), 12.5f, FontStyles.Normal, ColorTextMuted,
                TextAlignmentOptions.MidlineLeft, new Vector2(0, 0.5f));

            return view;
        }

        private void BuildPhaseTimelineCard(Transform parent, Vector2 pos, string phaseHeader, string iconName, string route, string description, Color accentColor)
        {
            GameObject card = CreateCardPanel(parent, $"Phase_{phaseHeader.Substring(0, 8)}", pos, new Vector2(0, 92), isFullWidth: true);
            AddLeftAccentStripe(card.transform, accentColor, 4f);

            CreateCardIcon(card.transform, iconName, new Vector2(16, 0), new Vector2(48, 48), isCenterY: true);

            CreateText(card.transform, "Phase_Title", phaseHeader,
                new Vector2(76, -16), new Vector2(880, 22), 15f, FontStyles.Bold, accentColor,
                TextAlignmentOptions.MidlineLeft, new Vector2(0, 0.5f));

            CreateText(card.transform, "Phase_Route", route,
                new Vector2(76, -38), new Vector2(880, 20), 12.5f, FontStyles.Normal, ColorSlate,
                TextAlignmentOptions.MidlineLeft, new Vector2(0, 0.5f));

            CreateText(card.transform, "Phase_Desc", description,
                new Vector2(76, -60), new Vector2(880, 26), 12.5f, FontStyles.Normal, ColorTextLight,
                TextAlignmentOptions.MidlineLeft, new Vector2(0, 0.5f));
        }

        // ====================================================================
        // BOTTOM NAVIGATION BAR
        // ====================================================================
        private void BuildBottomBar(Transform parent)
        {
            GameObject bottomBar = new GameObject("Bottom_Bar");
            bottomBar.layer = 5;
            bottomBar.transform.SetParent(parent, false);
            RectTransform rt = bottomBar.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 0);
            rt.anchorMax = new Vector2(1, 0);
            rt.pivot = new Vector2(0, 0);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(0, 56);

            Image bg = bottomBar.AddComponent<Image>();
            bg.color = new Color(0.045f, 0.07f, 0.10f, 1f);

            CreateDivider(bottomBar.transform, Vector2.zero, 1360, new Color(0.18f, 0.28f, 0.38f, 0.40f));

            // Previous Button (anchored left)
            GameObject btnPrevGo = CreateButton(bottomBar.transform, "Btn_Prev", "< PREV  (Q)",
                new Vector2(24, 0), new Vector2(170, 38), 13f, new Color(0.08f, 0.12f, 0.18f, 1f),
                ColorTextLight, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f));
            btnPrevious = btnPrevGo.GetComponent<Button>();
            btnPrevious.onClick.AddListener(PreviousChapter);
            prevButtonText = btnPrevGo.GetComponentInChildren<TextMeshProUGUI>();
            prevButtonText.name = "Btn_Prev_Text";

            // Status message in center
            GameObject statusGo = CreateText(bottomBar.transform, "Completion_Status",
                string.Empty,
                new Vector2(0, 0), new Vector2(480, 32), 13f, FontStyles.Bold, ColorCyan,
                TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            completionStatusText = statusGo.GetComponent<TextMeshProUGUI>();

            // Next Button (anchored right)
            GameObject btnNextGo = CreateButton(bottomBar.transform, "Btn_Next", "NEXT CHAPTER  >",
                new Vector2(-24, 0), new Vector2(230, 38), 13.5f, new Color(0.12f, 0.28f, 0.38f, 1f),
                ColorCyan, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f));
            btnNext = btnNextGo.GetComponent<Button>();
            btnNext.onClick.AddListener(NextChapter);
            nextButtonText = btnNextGo.GetComponentInChildren<TextMeshProUGUI>();
            nextButtonText.name = "Btn_Next_Text";
        }

        // ====================================================================
        // HELPER FACTORY METHODS (Flat Surface & Minimalist Architecture)
        // ====================================================================
        private GameObject CreateCardPanel(Transform parent, string name, Vector2 pos, Vector2 size, bool isFullWidth = false, bool hasOutline = false, Color? customBg = null, Color? accentBorder = null)
        {
            GameObject card = new GameObject(name);
            card.layer = 5;
            card.transform.SetParent(parent, false);
            RectTransform rt = card.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = isFullWidth ? new Vector2(1, 1) : new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;

            Image bg = card.AddComponent<Image>();
            bg.color = customBg ?? ColorSurfaceFlat;

            if (hasOutline || accentBorder.HasValue)
            {
                Outline outline = card.AddComponent<Outline>();
                outline.effectColor = accentBorder ?? new Color(0.18f, 0.28f, 0.38f, 0.35f);
                outline.effectDistance = new Vector2(1, -1);
            }
            return card;
        }

        private GameObject AddLeftAccentStripe(Transform card, Color stripeColor, float width = 4f)
        {
            GameObject stripe = new GameObject("Accent_Stripe");
            stripe.layer = 5;
            stripe.transform.SetParent(card, false);
            RectTransform rt = stripe.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 0);
            rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(width, 0);
            Image img = stripe.AddComponent<Image>();
            img.color = stripeColor;
            return stripe;
        }

        private void CreateCardIcon(Transform parent, string iconName, Vector2 pos, Vector2 size, bool isCenterY = false)
        {
            GameObject iconGo = new GameObject("Icon");
            iconGo.layer = 5;
            iconGo.transform.SetParent(parent, false);
            RectTransform iconRt = iconGo.AddComponent<RectTransform>();
            iconRt.anchorMin = isCenterY ? new Vector2(0, 0.5f) : new Vector2(0, 1);
            iconRt.anchorMax = isCenterY ? new Vector2(0, 0.5f) : new Vector2(0, 1);
            iconRt.pivot = isCenterY ? new Vector2(0, 0.5f) : new Vector2(0, 1);
            iconRt.anchoredPosition = pos;
            iconRt.sizeDelta = size;

            Image iconImg = iconGo.AddComponent<Image>();
            Sprite sp = LoadTutorialSprite(iconName);
            iconImg.sprite = sp;
            iconImg.color = sp != null ? Color.white : ColorCyan;
        }

        private GameObject CreateKeycap(Transform parent, string name, string label, Vector2 pos, Vector2 size, float fontSize = 12f)
        {
            GameObject keyGo = new GameObject(name);
            keyGo.layer = 5;
            keyGo.transform.SetParent(parent, false);
            RectTransform rt = keyGo.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;

            Image img = keyGo.AddComponent<Image>();
            img.color = new Color(0.09f, 0.14f, 0.20f, 1f);

            Outline outline = keyGo.AddComponent<Outline>();
            outline.effectColor = new Color(0.22f, 0.74f, 0.97f, 0.35f);
            outline.effectDistance = new Vector2(1, -1);

            GameObject txtGo = CreateText(keyGo.transform, "Key_Label", label,
                Vector2.zero, Vector2.zero, fontSize, FontStyles.Bold, ColorTextLight,
                TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one);
            txtGo.GetComponent<TextMeshProUGUI>().raycastTarget = false;
            return keyGo;
        }

        private GameObject CreateText(Transform parent, string name, string text, Vector2 pos, Vector2 size, float fontSize,
            FontStyles style, Color color, TextAlignmentOptions alignment = TextAlignmentOptions.MidlineLeft, Vector2? pivot = null,
            Vector2? anchorMin = null, Vector2? anchorMax = null)
        {
            GameObject go = new GameObject(name);
            go.layer = 5;
            go.transform.SetParent(parent, false);
            RectTransform rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin ?? new Vector2(0, 1);
            rt.anchorMax = anchorMax ?? new Vector2(0, 1);
            if (pivot.HasValue) rt.pivot = pivot.Value;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;

            TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.fontStyle = style;
            tmp.color = color;
            tmp.alignment = alignment;
            tmp.raycastTarget = false;
            tmp.textWrappingMode = TextWrappingModes.Normal;
            return go;
        }

        private GameObject CreateButton(Transform parent, string name, string text, Vector2 pos, Vector2 size, float fontSize,
            Color bgColor, Color textColor, Vector2? pivot = null, Vector2? anchorMin = null, Vector2? anchorMax = null, bool hasOutline = true)
        {
            GameObject go = new GameObject(name);
            go.layer = 5;
            go.transform.SetParent(parent, false);
            RectTransform rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin ?? new Vector2(0.5f, 0.5f);
            rt.anchorMax = anchorMax ?? new Vector2(0.5f, 0.5f);
            if (pivot.HasValue) rt.pivot = pivot.Value;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;

            Image img = go.AddComponent<Image>();
            img.color = bgColor;

            if (hasOutline)
            {
                Outline outline = go.AddComponent<Outline>();
                outline.effectColor = new Color(textColor.r, textColor.g, textColor.b, 0.35f);
                outline.effectDistance = new Vector2(1, -1);
            }

            Button btn = go.AddComponent<Button>();
            ColorBlock cb = btn.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(1.20f, 1.20f, 1.20f, 1f);
            cb.pressedColor = new Color(0.80f, 0.80f, 0.80f, 1f);
            cb.disabledColor = new Color(0.4f, 0.4f, 0.4f, 0.4f);
            btn.colors = cb;

            GameObject txtGo = CreateText(go.transform, "Btn_Text", text, Vector2.zero, size, fontSize, FontStyles.Bold, textColor, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one);
            RectTransform txtRt = txtGo.GetComponent<RectTransform>();
            txtRt.sizeDelta = Vector2.zero;
            txtRt.anchoredPosition = Vector2.zero;

            return go;
        }

        private void CreateDivider(Transform parent, Vector2 pos, float width, Color color)
        {
            GameObject div = new GameObject("Divider");
            div.layer = 5;
            div.transform.SetParent(parent, false);
            RectTransform rt = div.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 1);
            rt.anchorMax = new Vector2(0.5f, 1);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(width, 1);
            Image img = div.AddComponent<Image>();
            img.color = color;
        }

        private void CreateVerticalDivider(Transform parent, Vector2 pos, float height, Color color)
        {
            GameObject div = new GameObject("V_Divider");
            div.layer = 5;
            div.transform.SetParent(parent, false);
            RectTransform rt = div.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0.5f, 1);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(1, height);
            Image img = div.AddComponent<Image>();
            img.color = color;
        }
    }
}
