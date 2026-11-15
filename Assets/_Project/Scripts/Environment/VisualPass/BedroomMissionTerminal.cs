using UnityEngine;
using UnityEngine.Rendering.Universal;
using TheLastSignal.Core.Interaction;
using TheLastSignal.Entities.Player;
using TheLastSignal.UI;

namespace TheLastSignal.VisualPass
{
    /// <summary>
    /// Interactive Apex-IV Command Terminal & Holo-Map located at the center of the Bedroom.
    /// Interacting opens the mission completion guidance, storyline objectives, and survival instructions.
    /// </summary>
    public class BedroomMissionTerminal : InteractableBase
    {
        [Header("Terminal References")]
        [SerializeField] private BedroomMissionTerminalUI terminalUI;
        [SerializeField] private Light2D holoCoreLight;

        [Header("Feedback Settings")]
        [SerializeField] private float baseLightIntensity = 1.4f;

        private void Awake()
        {
            promptMessage = "[E] Kích hoạt Trạm Chỉ Huy Apex-IV (Chỉ dẫn Nhiệm vụ)";
            if (terminalUI == null)
            {
                terminalUI = FindAnyObjectByType<BedroomMissionTerminalUI>();
            }

            if (holoCoreLight == null)
            {
                Transform tLight = transform.Find("Light_Holo_Core");
                if (tLight != null)
                {
                    holoCoreLight = tLight.GetComponent<Light2D>();
                }
            }
        }

        protected override void ProcessInteraction(PlayerController player)
        {
            if (terminalUI == null)
            {
                terminalUI = FindAnyObjectByType<BedroomMissionTerminalUI>();
                if (terminalUI == null)
                {
                    // Create UI component in scene dynamically if not yet attached to Canvas
                    Canvas canvas = FindAnyObjectByType<Canvas>();
                    if (canvas != null)
                    {
                        terminalUI = canvas.gameObject.AddComponent<BedroomMissionTerminalUI>();
                    }
                }
            }

            if (terminalUI != null)
            {
                if (terminalUI.IsOpen)
                {
                    terminalUI.CloseTerminal();
                }
                else
                {
                    terminalUI.OpenTerminal(player);
                }
            }
        }

        private void Update()
        {
            // Subtle, atmospheric pulsing glow for the holographic projection core
            if (holoCoreLight != null)
            {
                float pulse = 0.88f + 0.12f * Mathf.Sin(Time.time * 2.5f);
                holoCoreLight.intensity = baseLightIntensity * pulse;
            }
        }
    }
}
