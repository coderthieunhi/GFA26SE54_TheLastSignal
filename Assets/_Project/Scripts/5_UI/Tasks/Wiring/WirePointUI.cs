using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

namespace TheLastSignal.UI.Tasks
{
    public class WirePointUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler, IPointerEnterHandler, IPointerExitHandler
    {
        [Header("Socket Configuration")]
        [SerializeField] private bool isLeft = true;
        [SerializeField] private WireColorType colorType;
        [SerializeField] private int socketIndex = 0;

        [Header("Visual Elements")]
        [SerializeField] private Image socketIndicator;
        [SerializeField] private Image socketLight;
        [SerializeField] private RectTransform wireLine; // Image UI with pivot (0, 0.5)
        [SerializeField] private TextMeshProUGUI terminalLabel;

        [Header("Line Settings")]
        [SerializeField] private float wireThickness = 14f;

        private WireTaskUI manager;
        private Canvas canvas;
        private Image wireImage;
        private bool isConnected = false;

        public bool IsLeft => isLeft;
        public WireColorType ColorType => colorType;
        public int SocketIndex => socketIndex;
        public bool IsConnected => isConnected;
        public Vector2 SocketPosition => ((RectTransform)transform).anchoredPosition;

        private void Awake()
        {
            EnsureWireParenting();
        }

        public void Configure(bool left, Image socket, Image light, RectTransform line, TextMeshProUGUI label = null)
        {
            isLeft = left;
            socketIndicator = socket;
            socketLight = light;
            wireLine = line;
            if (label != null) terminalLabel = label;
            EnsureWireParenting();
        }

        private void EnsureWireParenting()
        {
            if (wireLine != null && isLeft)
            {
                if (wireLine.parent != transform)
                {
                    wireLine.SetParent(transform, false);
                }
                wireImage = wireLine.GetComponent<Image>();
                wireLine.pivot = new Vector2(0f, 0.5f);
                wireLine.anchoredPosition = Vector2.zero;
            }
        }

        public void Initialize(WireTaskUI taskManager, Canvas parentCanvas, WireColorType type, int index = 0)
        {
            manager = taskManager;
            canvas = parentCanvas;
            colorType = type;
            socketIndex = index;
            isConnected = false;

            EnsureWireParenting();

            Color c = WireColorHelper.GetColor(colorType);
            string vText = WireColorHelper.GetVoltageText(colorType);

            if (socketIndicator != null)
            {
                socketIndicator.color = c;
            }

            if (socketLight != null)
            {
                socketLight.color = new Color(0.2f, 0.2f, 0.2f, 0.8f); // Off
            }

            if (terminalLabel == null)
            {
                Transform lblT = transform.Find("TerminalLabel");
                if (lblT != null) terminalLabel = lblT.GetComponent<TextMeshProUGUI>();
            }

            if (terminalLabel != null)
            {
                terminalLabel.text = isLeft ? $"[A-{index + 1}]  {vText}" : $"{vText}  [B-{index + 1}]";
                terminalLabel.color = isLeft ? new Color(0.85f, 0.92f, 1f, 1f) : new Color(0.75f, 0.85f, 0.95f, 1f);
            }

            if (wireLine != null)
            {
                if (wireImage != null) wireImage.color = c;
                wireLine.anchoredPosition = Vector2.zero;
                wireLine.sizeDelta = new Vector2(0, wireThickness);
                wireLine.localRotation = Quaternion.identity;
                wireLine.gameObject.SetActive(false);
            }
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (!isLeft || isConnected || (manager != null && manager.IsFaultLocked)) return;

            if (wireLine != null)
            {
                EnsureWireParenting();
                wireLine.gameObject.SetActive(true);
                UpdateWireToScreenPoint(eventData.position);
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!isLeft || isConnected || (manager != null && manager.IsFaultLocked) || wireLine == null) return;
            UpdateWireToScreenPoint(eventData.position);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!isLeft || isConnected) return;

            if (manager != null && manager.IsFaultLocked)
            {
                ResetWire();
                return;
            }

            // Check if dropped near matching target as a forgiving fallback
            WirePointUI fallbackTarget = manager != null ? manager.FindHoveredTarget(eventData.position) : null;
            if (fallbackTarget != null && !fallbackTarget.IsLeft && !fallbackTarget.IsConnected)
            {
                manager.TryConnectWire(this, fallbackTarget);
                return;
            }

            ResetWire();
        }

        public void OnDrop(PointerEventData eventData)
        {
            // Drop happens on right socket when a left socket is dragged over it
            if (isLeft || isConnected || (manager != null && manager.IsFaultLocked)) return;

            if (eventData.pointerDrag != null && eventData.pointerDrag.TryGetComponent(out WirePointUI draggedPoint))
            {
                if (draggedPoint.IsLeft && !draggedPoint.IsConnected)
                {
                    if (manager != null)
                    {
                        manager.TryConnectWire(draggedPoint, this);
                    }
                }
            }
        }

        public void ConnectTo(WirePointUI targetPoint)
        {
            isConnected = true;
            targetPoint.isConnected = true;

            if (socketLight != null) socketLight.color = Color.green;
            if (targetPoint.socketLight != null) targetPoint.socketLight.color = Color.green;

            if (wireLine != null)
            {
                EnsureWireParenting();
                wireLine.gameObject.SetActive(true);
                wireLine.anchoredPosition = Vector2.zero;

                Vector3 targetLocal = transform.InverseTransformPoint(targetPoint.transform.position);

                float angle = Mathf.Atan2(targetLocal.y, targetLocal.x) * Mathf.Rad2Deg;
                wireLine.localEulerAngles = new Vector3(0, 0, angle);
                wireLine.sizeDelta = new Vector2(((Vector2)targetLocal).magnitude, wireThickness);
            }
        }

        public void ResetWire()
        {
            isConnected = false;
            if (socketLight != null)
            {
                socketLight.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);
            }

            if (wireLine != null)
            {
                wireLine.anchoredPosition = Vector2.zero;
                wireLine.sizeDelta = new Vector2(0, wireThickness);
                wireLine.gameObject.SetActive(false);
            }
        }

        private void UpdateWireToScreenPoint(Vector2 screenPosition)
        {
            if (wireLine == null || canvas == null) return;

            EnsureWireParenting();
            Camera eventCam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            RectTransform socketRect = (RectTransform)transform;

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(socketRect, screenPosition, eventCam, out Vector2 localPoint))
            {
                float angle = Mathf.Atan2(localPoint.y, localPoint.x) * Mathf.Rad2Deg;
                wireLine.localEulerAngles = new Vector3(0, 0, angle);
                wireLine.sizeDelta = new Vector2(localPoint.magnitude, wireThickness);
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!isLeft && !isConnected && eventData.dragging && eventData.pointerDrag != null)
            {
                if (eventData.pointerDrag.TryGetComponent(out WirePointUI draggedPoint))
                {
                    if (draggedPoint.ColorType == colorType)
                    {
                        if (socketLight != null) socketLight.color = Color.yellow; // Hover highlight
                    }
                    else
                    {
                        if (socketLight != null) socketLight.color = new Color(0.9f, 0.2f, 0.2f, 1f); // Mismatch hover warning
                    }
                }
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (!isLeft && !isConnected && socketLight != null)
            {
                socketLight.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);
            }
        }
    }
}
