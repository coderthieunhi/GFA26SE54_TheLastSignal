using System;
using System.Text;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using TheLastSignal.Core;
using TheLastSignal.Core.Bootstrap;

namespace TheLastSignal.UI
{
    /// <summary>
    /// TLS-E03-S02: lets a Client connect directly to a Dedicated Server by IP:port + join token,
    /// instead of Relay + room code. Entirely additive — builds its own small UI at runtime (same
    /// pattern LobbyUI already uses for its dynamic copy button/player rows) as a child of whatever
    /// GameObject it's attached to, so the existing Host/Join menu (LobbyUI) is never touched or
    /// replaced ("keep the existing menu intact").
    /// </summary>
    public class DirectConnectUI : MonoBehaviour
    {
        [SerializeField] private NetworkManagerExt networkManagerExt;

        private TMP_InputField ipField;
        private TMP_InputField portField;
        private TMP_InputField tokenField;
        private Button connectButton;
        private TextMeshProUGUI statusText;

        private bool isConnecting;

        private void Awake()
        {
            // TLS-E03-S01: a Dedicated Server never shows any connect UI (ARCHITECTURE_RULES.md Rule 2/4).
            if (GameBootstrap.CurrentMode == LaunchMode.DedicatedServer)
            {
                gameObject.SetActive(false);
                return;
            }

            if (networkManagerExt == null)
            {
                networkManagerExt = FindAnyObjectByType<NetworkManagerExt>();
            }

            BuildUi();
        }

        private void Start()
        {
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientConnectedCallback += HandleConnected;
                NetworkManager.Singleton.OnClientDisconnectCallback += HandleDisconnected;
            }
        }

        private void OnDestroy()
        {
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientConnectedCallback -= HandleConnected;
                NetworkManager.Singleton.OnClientDisconnectCallback -= HandleDisconnected;
            }

            if (connectButton != null)
            {
                connectButton.onClick.RemoveListener(OnConnectButtonPressed);
            }
        }

        private void OnConnectButtonPressed()
        {
            if (networkManagerExt == null || isConnecting || (NetworkManager.Singleton?.IsListening ?? false))
            {
                return;
            }

            string ip = ipField != null ? ipField.text.Trim() : string.Empty;
            string portText = portField != null ? portField.text.Trim() : string.Empty;
            string token = tokenField != null ? tokenField.text : string.Empty;

            if (string.IsNullOrEmpty(ip))
            {
                SetStatus("Please enter a server IP.", isError: true);
                return;
            }

            if (!ushort.TryParse(portText, out ushort port))
            {
                SetStatus($"\"{portText}\" is not a valid port (0-65535).", isError: true);
                return;
            }

            byte[] tokenBytes = Encoding.UTF8.GetBytes(token ?? string.Empty);

            isConnecting = true;
            SetControlsInteractable(false);
            SetStatus($"Connecting to {ip}:{port}...", isError: false);

            if (!networkManagerExt.ConnectDirect(ip, port, tokenBytes))
            {
                isConnecting = false;
                SetControlsInteractable(true);
                SetStatus("Could not start Client (see console).", isError: true);
            }

            // Success/failure beyond this point is reported by NGO's own callbacks — see
            // HandleConnected/HandleDisconnected — since connection + server-side token approval both
            // happen asynchronously after StartClient() returns.
        }

        private void HandleConnected(ulong clientId)
        {
            if (!isConnecting)
            {
                return;
            }

            isConnecting = false;
            SetStatus("Connected.", isError: false);
        }

        private void HandleDisconnected(ulong clientId)
        {
            if (!isConnecting)
            {
                return;
            }

            isConnecting = false;
            SetControlsInteractable(true);

            string reason = NetworkManager.Singleton != null ? NetworkManager.Singleton.DisconnectReason : string.Empty;
            SetStatus(string.IsNullOrEmpty(reason) ? "Connection failed or was refused." : $"Rejected: {reason}", isError: true);
        }

        private void SetControlsInteractable(bool interactable)
        {
            if (connectButton != null)
            {
                connectButton.interactable = interactable;
            }
        }

        private void SetStatus(string message, bool isError)
        {
            if (statusText == null)
            {
                return;
            }

            statusText.text = isError ? $"<color=#FF5555>{message}</color>" : message;
        }

        // ---------------------------------------------------------------- Self-built UI

        private void BuildUi()
        {
            GameObject root = new GameObject("DirectConnectPanel", typeof(RectTransform));
            root.transform.SetParent(transform, false);
            RectTransform rootRect = root.GetComponent<RectTransform>();
            rootRect.sizeDelta = new Vector2(360f, 190f);

            VerticalLayoutGroup layout = root.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 6f;
            layout.childForceExpandWidth = false;
            layout.childControlWidth = false;
            layout.childAlignment = TextAnchor.UpperLeft;

            CreateLabel(root.transform, "Direct Connect (Dedicated Server)");
            ipField = CreateInputField(root.transform, "Server IP (e.g. 127.0.0.1)");
            portField = CreateInputField(root.transform, "Port (e.g. 7777)");
            portField.characterLimit = 5;
            portField.contentType = TMP_InputField.ContentType.IntegerNumber;
            tokenField = CreateInputField(root.transform, "Join Token");

            connectButton = CreateButton(root.transform, "Connect");
            connectButton.onClick.AddListener(OnConnectButtonPressed);

            statusText = CreateLabel(root.transform, string.Empty);
        }

        private TextMeshProUGUI CreateLabel(Transform parent, string text)
        {
            GameObject go = new GameObject("Label", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            go.GetComponent<RectTransform>().sizeDelta = new Vector2(340f, 24f);

            TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = 18;
            tmp.color = Color.white;
            tmp.raycastTarget = false;
            return tmp;
        }

        private TMP_InputField CreateInputField(Transform parent, string placeholder)
        {
            GameObject go = new GameObject("InputField", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            go.GetComponent<RectTransform>().sizeDelta = new Vector2(340f, 28f);
            go.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.1f);

            TMP_InputField field = go.AddComponent<TMP_InputField>();

            GameObject textArea = new GameObject("Text Area", typeof(RectTransform));
            textArea.transform.SetParent(go.transform, false);
            RectTransform textAreaRect = textArea.GetComponent<RectTransform>();
            textAreaRect.anchorMin = Vector2.zero;
            textAreaRect.anchorMax = Vector2.one;
            textAreaRect.offsetMin = new Vector2(8f, 2f);
            textAreaRect.offsetMax = new Vector2(-8f, -2f);

            GameObject textGo = new GameObject("Text", typeof(RectTransform));
            textGo.transform.SetParent(textArea.transform, false);
            RectTransform textRect = textGo.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            TextMeshProUGUI text = textGo.AddComponent<TextMeshProUGUI>();
            text.fontSize = 16;
            text.color = Color.white;
            text.raycastTarget = false;

            GameObject placeholderGo = new GameObject("Placeholder", typeof(RectTransform));
            placeholderGo.transform.SetParent(textArea.transform, false);
            RectTransform placeholderRect = placeholderGo.GetComponent<RectTransform>();
            placeholderRect.anchorMin = Vector2.zero;
            placeholderRect.anchorMax = Vector2.one;
            placeholderRect.offsetMin = Vector2.zero;
            placeholderRect.offsetMax = Vector2.zero;

            TextMeshProUGUI placeholderText = placeholderGo.AddComponent<TextMeshProUGUI>();
            placeholderText.text = placeholder;
            placeholderText.fontSize = 16;
            placeholderText.fontStyle = FontStyles.Italic;
            placeholderText.color = new Color(1f, 1f, 1f, 0.5f);
            placeholderText.raycastTarget = false;

            field.textViewport = textAreaRect;
            field.textComponent = text;
            field.placeholder = placeholderText;

            return field;
        }

        private Button CreateButton(Transform parent, string label)
        {
            GameObject go = new GameObject("Button", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            go.GetComponent<RectTransform>().sizeDelta = new Vector2(160f, 32f);
            go.GetComponent<Image>().color = new Color(0.2f, 0.4f, 0.8f, 1f);

            Button button = go.GetComponent<Button>();
            button.targetGraphic = go.GetComponent<Image>();

            GameObject textGo = new GameObject("Text", typeof(RectTransform));
            textGo.transform.SetParent(go.transform, false);
            RectTransform textRect = textGo.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            TextMeshProUGUI text = textGo.AddComponent<TextMeshProUGUI>();
            text.text = label;
            text.fontSize = 18;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
            text.raycastTarget = false;

            return button;
        }
    }
}
