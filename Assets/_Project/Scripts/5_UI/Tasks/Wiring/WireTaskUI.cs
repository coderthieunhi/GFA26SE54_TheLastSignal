using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using TheLastSignal.Entities.Player;
using TheLastSignal.Entities.Environment;

namespace TheLastSignal.UI.Tasks
{
    /// <summary>
    /// Among Us style Wire Connecting Minigame.
    /// Handles wiring puzzle lifecycle, player control locking, and completion notifications.
    /// Can be spawned dynamically at runtime if not already placed in the scene.
    /// </summary>
    public class WireTaskUI : MonoBehaviour
    {
        private static WireTaskUI _instance;
        public static WireTaskUI Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = Object.FindAnyObjectByType<WireTaskUI>(FindObjectsInactive.Include);
                }
                return _instance;
            }
            private set => _instance = value;
        }

        [Header("UI Panels")]
        [SerializeField] private GameObject rootPanel;
        [SerializeField] private Image masterStatusLight;
        [SerializeField] private TextMeshProUGUI headerText;
        [SerializeField] private TextMeshProUGUI progressText;
        [SerializeField] private Button closeButton;

        [Header("Wiring Sockets")]
        [SerializeField] private List<WirePointUI> leftPoints = new List<WirePointUI>();
        [SerializeField] private List<WirePointUI> rightPoints = new List<WirePointUI>();

        [Header("Audio (Optional)")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip wireConnectedSfx;
        [SerializeField] private AudioClip taskCompletedSfx;

        private Canvas canvas;
        private PlayerController activePlayer;
        private TaskLocation activeTaskLocation;
        private int connectedCount = 0;
        private bool isFinished = false;

        public bool IsOpen => rootPanel != null && rootPanel.activeSelf;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;

            canvas = GetComponentInParent<Canvas>();

            if (closeButton != null)
            {
                closeButton.onClick.RemoveAllListeners();
                closeButton.onClick.AddListener(CloseMinigame);
            }

            if (rootPanel != null)
            {
                rootPanel.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }

        private void Update()
        {
            if (!IsOpen) return;

            // Emergency exit with ESC when in danger
            if (UnityEngine.InputSystem.Keyboard.current != null &&
                UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                CloseMinigame();
            }
        }

        public void Configure(GameObject root, Image light, TextMeshProUGUI header, TextMeshProUGUI progress, Button close, List<WirePointUI> left, List<WirePointUI> right)
        {
            rootPanel = root;
            masterStatusLight = light;
            headerText = header;
            progressText = progress;
            closeButton = close;

            if (closeButton != null)
            {
                closeButton.onClick.RemoveAllListeners();
                closeButton.onClick.AddListener(CloseMinigame);
            }

            leftPoints = left;
            rightPoints = right;
        }

        /// <summary>
        /// Retrieves the existing WireTaskUI in the scene or creates one automatically under a Canvas.
        /// </summary>
        public static WireTaskUI GetOrCreateInstance()
        {
            if (Instance != null) return Instance;

            Canvas canvas = Object.FindAnyObjectByType<Canvas>();
            if (canvas == null)
            {
                Debug.LogWarning("[WireTaskUI] No Canvas found in scene to create WireTaskUI.");
                return null;
            }

            _instance = CreateWiringUIHierarchy(canvas);
            return _instance;
        }

        public static WireTaskUI CreateWiringUIHierarchy(Canvas canvas)
        {
            // Root panel
            GameObject root = new GameObject("WiringMinigamePanel", typeof(RectTransform));
            root.transform.SetParent(canvas.transform, false);

            RectTransform rootRect = root.GetComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;

            // Dark semi-transparent background overlay
            Image bgOverlay = root.AddComponent<Image>();
            bgOverlay.color = new Color(0f, 0f, 0f, 0.75f);

            Sprite chassisSprite = Resources.Load<Sprite>("UI/Tasks/TerminalBezel");
            Sprite socketSprite = Resources.Load<Sprite>("UI/Tasks/WireSocket");

            // Center Panel (Metal Chassis)
            GameObject box = new GameObject("ElectricalBox", typeof(RectTransform), typeof(Image));
            box.transform.SetParent(root.transform, false);
            RectTransform boxRect = box.GetComponent<RectTransform>();
            boxRect.sizeDelta = new Vector2(960, 640);
            boxRect.anchoredPosition = Vector2.zero;
            Image boxImg = box.GetComponent<Image>();
            if (chassisSprite != null)
            {
                boxImg.sprite = chassisSprite;
                boxImg.color = Color.white;
            }
            else
            {
                boxImg.color = new Color(0.12f, 0.14f, 0.16f, 1f);
                Outline outline = box.AddComponent<Outline>();
                outline.effectColor = new Color(0.35f, 0.4f, 0.45f, 1f);
                outline.effectDistance = new Vector2(3, -3);
            }

            // Header Title
            GameObject headerGo = new GameObject("HeaderTitle", typeof(RectTransform), typeof(TextMeshProUGUI));
            headerGo.transform.SetParent(box.transform, false);
            RectTransform headerRect = headerGo.GetComponent<RectTransform>();
            headerRect.anchorMin = new Vector2(0f, 1f);
            headerRect.anchorMax = new Vector2(1f, 1f);
            headerRect.pivot = new Vector2(0.5f, 1f);
            headerRect.anchoredPosition = new Vector2(0, -20);
            headerRect.sizeDelta = new Vector2(-60, 45);
            TextMeshProUGUI headerText = headerGo.GetComponent<TextMeshProUGUI>();
            headerText.text = "ELECTRICAL JUNCTION PANEL";
            headerText.fontSize = 24;
            headerText.alignment = TextAlignmentOptions.Center;
            headerText.color = new Color(0.9f, 0.95f, 1f, 1f);

            // Progress Text
            GameObject progressGo = new GameObject("ProgressText", typeof(RectTransform), typeof(TextMeshProUGUI));
            progressGo.transform.SetParent(box.transform, false);
            RectTransform progressRect = progressGo.GetComponent<RectTransform>();
            progressRect.anchorMin = new Vector2(0f, 0f);
            progressRect.anchorMax = new Vector2(1f, 0f);
            progressRect.pivot = new Vector2(0.5f, 0f);
            progressRect.anchoredPosition = new Vector2(0, 20);
            progressRect.sizeDelta = new Vector2(-60, 35);
            TextMeshProUGUI progressText = progressGo.GetComponent<TextMeshProUGUI>();
            progressText.text = "WIRES: 0 / 4";
            progressText.fontSize = 20;
            progressText.alignment = TextAlignmentOptions.Center;
            progressText.color = new Color(0.7f, 0.8f, 0.9f, 1f);

            // Master Status Light
            GameObject lightGo = new GameObject("MasterLight", typeof(RectTransform), typeof(Image));
            lightGo.transform.SetParent(box.transform, false);
            RectTransform lightRect = lightGo.GetComponent<RectTransform>();
            lightRect.anchorMin = new Vector2(0f, 1f);
            lightRect.anchorMax = new Vector2(0f, 1f);
            lightRect.pivot = new Vector2(0f, 1f);
            lightRect.anchoredPosition = new Vector2(25, -20);
            lightRect.sizeDelta = new Vector2(24, 24);
            Image lightImg = lightGo.GetComponent<Image>();
            lightImg.color = Color.red;

            // Close Button [ESC / X]
            GameObject closeGo = new GameObject("CloseButton", typeof(RectTransform), typeof(Image), typeof(Button));
            closeGo.transform.SetParent(box.transform, false);
            RectTransform closeRect = closeGo.GetComponent<RectTransform>();
            closeRect.anchorMin = new Vector2(1f, 1f);
            closeRect.anchorMax = new Vector2(1f, 1f);
            closeRect.pivot = new Vector2(1f, 1f);
            closeRect.anchoredPosition = new Vector2(-20, -18);
            closeRect.sizeDelta = new Vector2(36, 36);
            Image closeImg = closeGo.GetComponent<Image>();
            closeImg.color = new Color(0.7f, 0.2f, 0.2f, 1f);
            Button closeBtn = closeGo.GetComponent<Button>();

            GameObject closeTextGo = new GameObject("CloseText", typeof(RectTransform), typeof(TextMeshProUGUI));
            closeTextGo.transform.SetParent(closeGo.transform, false);
            RectTransform ctRect = closeTextGo.GetComponent<RectTransform>();
            ctRect.anchorMin = Vector2.zero;
            ctRect.anchorMax = Vector2.one;
            ctRect.sizeDelta = Vector2.zero;
            TextMeshProUGUI closeTxt = closeTextGo.GetComponent<TextMeshProUGUI>();
            closeTxt.text = "X";
            closeTxt.fontSize = 22;
            closeTxt.alignment = TextAlignmentOptions.Center;
            closeTxt.color = Color.white;

            // Sockets
            float startY = 120f;
            float stepY = -90f;

            List<WirePointUI> leftPoints = new List<WirePointUI>();
            List<WirePointUI> rightPoints = new List<WirePointUI>();

            for (int i = 0; i < 4; i++)
            {
                WirePointUI leftPt = CreateSocket(box.transform, $"LeftSocket_{i}", new Vector2(-330, startY + i * stepY), true);
                leftPoints.Add(leftPt);

                WirePointUI rightPt = CreateSocket(box.transform, $"RightSocket_{i}", new Vector2(330, startY + i * stepY), false);
                rightPoints.Add(rightPt);
            }

            WireTaskUI wireTask = root.AddComponent<WireTaskUI>();
            wireTask.Configure(root, lightImg, headerText, progressText, closeBtn, leftPoints, rightPoints);

            root.SetActive(false);
            return wireTask;
        }

        private static WirePointUI CreateSocket(Transform parent, string name, Vector2 anchoredPos, bool isLeft)
        {
            Sprite socketSprite = Resources.Load<Sprite>("UI/Tasks/WireSocket");

            GameObject socketGo = new GameObject(name, typeof(RectTransform), typeof(Image));
            socketGo.transform.SetParent(parent, false);

            RectTransform sRect = socketGo.GetComponent<RectTransform>();
            sRect.sizeDelta = new Vector2(58, 58);
            sRect.anchoredPosition = anchoredPos;

            Image sImg = socketGo.GetComponent<Image>();
            if (socketSprite != null)
            {
                sImg.sprite = socketSprite;
                sImg.color = Color.white;
            }
            else
            {
                sImg.color = Color.white;
            }

            // Inner colored plug core
            GameObject coreGo = new GameObject("SocketCore", typeof(RectTransform), typeof(Image));
            coreGo.transform.SetParent(socketGo.transform, false);
            RectTransform coreRect = coreGo.GetComponent<RectTransform>();
            coreRect.sizeDelta = new Vector2(26, 26);
            coreRect.anchoredPosition = Vector2.zero;
            Image coreImg = coreGo.GetComponent<Image>();

            // Indicator light (LED)
            GameObject lightGo = new GameObject("LightIndicator", typeof(RectTransform), typeof(Image));
            lightGo.transform.SetParent(socketGo.transform, false);
            RectTransform lRect = lightGo.GetComponent<RectTransform>();
            lRect.sizeDelta = new Vector2(16, 16);
            lRect.anchoredPosition = isLeft ? new Vector2(-42, 0) : new Vector2(42, 0);
            Image lightImg = lightGo.GetComponent<Image>();
            lightImg.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);

            // Wire Line (only for left socket)
            RectTransform wireRect = null;
            if (isLeft)
            {
                GameObject wireGo = new GameObject("WireLine", typeof(RectTransform), typeof(Image));
                wireGo.transform.SetParent(socketGo.transform, false);
                wireGo.transform.SetAsFirstSibling();
                wireRect = wireGo.GetComponent<RectTransform>();
                wireRect.pivot = new Vector2(0f, 0.5f);
                wireRect.anchoredPosition = Vector2.zero;
                wireRect.sizeDelta = new Vector2(0, 14);

                Image wImg = wireGo.GetComponent<Image>();
                wImg.raycastTarget = false;
                wireGo.SetActive(false);
            }

            WirePointUI pointUI = socketGo.AddComponent<WirePointUI>();
            pointUI.Configure(isLeft, coreImg, lightImg, wireRect);

            return pointUI;
        }

        /// <summary>
        /// Opens the wiring minigame for the interacting player and task station.
        /// </summary>
        public void Open(PlayerController player, TaskLocation station)
        {
            activePlayer = player;
            activeTaskLocation = station;
            connectedCount = 0;
            isFinished = false;

            if (canvas == null)
            {
                canvas = GetComponentInParent<Canvas>();
            }

            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }

            if (masterStatusLight != null)
            {
                masterStatusLight.color = new Color(0.9f, 0.2f, 0.2f, 1f); // Red
            }

            if (headerText != null && station != null)
            {
                headerText.text = $"ELECTRICAL PANEL - {station.StationDisplayName.ToUpper()}";
            }

            UpdateProgressText();

            // Lock player controls while interacting with panel
            if (activePlayer != null)
            {
                activePlayer.SetControlEnabled(false);
            }

            SetupWires();

            if (rootPanel != null)
            {
                rootPanel.SetActive(true);
            }
        }

        private void SetupWires()
        {
            List<WireColorType> colors = new List<WireColorType>
            {
                WireColorType.Red,
                WireColorType.Blue,
                WireColorType.Yellow,
                WireColorType.Green
            };

            for (int i = 0; i < leftPoints.Count && i < colors.Count; i++)
            {
                leftPoints[i].Initialize(this, canvas, colors[i]);
            }

            List<WireColorType> shuffledColors = new List<WireColorType>(colors);
            for (int i = 0; i < shuffledColors.Count; i++)
            {
                int rnd = Random.Range(i, shuffledColors.Count);
                (shuffledColors[i], shuffledColors[rnd]) = (shuffledColors[rnd], shuffledColors[i]);
            }

            for (int i = 0; i < rightPoints.Count && i < shuffledColors.Count; i++)
            {
                rightPoints[i].Initialize(this, canvas, shuffledColors[i]);
            }
        }

        public WirePointUI CheckConnectTarget(Vector2 screenPos, WireColorType wireColor)
        {
            Camera eventCam = (canvas != null && canvas.renderMode == RenderMode.ScreenSpaceOverlay) ? null : canvas.worldCamera;

            foreach (var rightPoint in rightPoints)
            {
                if (rightPoint.IsConnected) continue;

                if (RectTransformUtility.RectangleContainsScreenPoint((RectTransform)rightPoint.transform, screenPos, eventCam))
                {
                    if (rightPoint.ColorType == wireColor)
                    {
                        return rightPoint;
                    }
                }
            }
            return null;
        }

        public void OnWireConnected(WirePointUI left, WirePointUI right)
        {
            connectedCount++;
            PlaySfx(wireConnectedSfx);
            UpdateProgressText();

            if (connectedCount >= leftPoints.Count && !isFinished)
            {
                StartCoroutine(CompleteRoutine());
            }
        }

        private void UpdateProgressText()
        {
            if (progressText != null)
            {
                progressText.text = $"WIRES: {connectedCount} / {leftPoints.Count}";
            }
        }

        private IEnumerator CompleteRoutine()
        {
            isFinished = true;
            if (masterStatusLight != null)
            {
                masterStatusLight.color = new Color(0.2f, 0.9f, 0.3f, 1f); // Green
            }

            PlaySfx(taskCompletedSfx);

            yield return new WaitForSeconds(0.45f);

            // Notify station
            if (activeTaskLocation != null)
            {
                activeTaskLocation.SetCompleted(true);
                if (activeTaskLocation.TryGetComponent(out StationRepair repair))
                {
                    repair.CompleteRepair();
                }
            }

            CloseMinigame();
        }

        public void CloseMinigame()
        {
            if (rootPanel != null)
            {
                rootPanel.SetActive(false);
            }

            // Restore player control
            if (activePlayer != null)
            {
                activePlayer.SetControlEnabled(true);
                activePlayer = null;
            }

            activeTaskLocation = null;
        }

        private void PlaySfx(AudioClip clip)
        {
            if (audioSource != null && clip != null)
            {
                audioSource.PlayOneShot(clip);
            }
        }
    }
}
