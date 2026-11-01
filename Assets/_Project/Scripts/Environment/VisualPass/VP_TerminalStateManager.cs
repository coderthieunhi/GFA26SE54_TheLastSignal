using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace TheLastSignal.Environment.VisualPass
{
    public class VP_TerminalStateManager : MonoBehaviour
    {
        [SerializeField] private Light2D screenLight;
        [SerializeField] private SpriteRenderer terminalRenderer;
        [SerializeField] private Sprite faultSprite;
        [SerializeField] private Sprite onlineSprite;
        [SerializeField] private Color faultColor = new Color(0.247f, 0.816f, 0.784f, 1f);
        [SerializeField] private Color onlineColor = new Color(0.25f, 0.85f, 0.45f, 1f);

        [SerializeField] private bool isOnline = false;

        private void Start()
        {
            ApplyState();
        }

        private void Update()
        {
#if UNITY_EDITOR
            // Press T to toggle terminal state for testing (compatible with new Input System)
#if ENABLE_INPUT_SYSTEM
            if (UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.tKey.wasPressedThisFrame)
            {
                SetTerminalOnline(!isOnline);
            }
#elif ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.T))
            {
                SetTerminalOnline(!isOnline);
            }
#endif
#endif
        }

        public void SetTerminalOnline(bool online)
        {
            isOnline = online;
            ApplyState();
        }

        private void ApplyState()
        {
            if (screenLight != null)
            {
                screenLight.color = isOnline ? onlineColor : faultColor;
            }

            if (terminalRenderer != null)
            {
                if (isOnline && onlineSprite != null)
                {
                    terminalRenderer.sprite = onlineSprite;
                }
                else if (!isOnline && faultSprite != null)
                {
                    terminalRenderer.sprite = faultSprite;
                }
            }
        }
    }
}
