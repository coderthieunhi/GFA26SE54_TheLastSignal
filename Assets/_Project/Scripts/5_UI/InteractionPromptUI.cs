using UnityEngine;
using TMPro;
using TheLastSignal.Entities.Player;

namespace TheLastSignal.UI
{
    /// <summary>
    /// Displays the interaction prompt for the local player's currently targeted interactable object.
    /// Designed to be decoupled from game logic, per ARCHITECTURE_RULES.md Rule 2.
    /// </summary>
    public class InteractionPromptUI : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private TextMeshProUGUI promptText;
        [SerializeField] private GameObject promptContainer;

        [Header("Player Reference")]
        [Tooltip("Optional: If empty, will find PlayerController in scene (single-player prototype).")]
        [SerializeField] private PlayerController localPlayer;

        private void Start()
        {
            if (localPlayer == null)
            {
                localPlayer = FindAnyObjectByType<PlayerController>();
            }

            if (promptContainer != null)
            {
                promptContainer.SetActive(false);
            }
        }

        private void Update()
        {
            if (localPlayer == null || promptText == null) return;

            var hit = localPlayer.CurrentInteractable;
            if (hit != null)
            {
                string prompt = null;

                if (hit.TryGetComponent(out TheLastSignal.Core.Interaction.IInteractable interactable) ||
                    hit.GetComponentInParent<TheLastSignal.Core.Interaction.IInteractable>() is { } parent && (interactable = parent) != null)
                {
                    if (interactable.CanInteract(localPlayer))
                    {
                        prompt = interactable.InteractionPrompt;
                    }
                }

                if (!string.IsNullOrEmpty(prompt))
                {
                    if (promptContainer != null && !promptContainer.activeSelf)
                    {
                        promptContainer.SetActive(true);
                    }

                    promptText.text = prompt;
                }
                else
                {
                    if (promptContainer != null && promptContainer.activeSelf)
                    {
                        promptContainer.SetActive(false);
                    }
                }
            }
            else
            {
                if (promptContainer != null && promptContainer.activeSelf)
                {
                    promptContainer.SetActive(false);
                }
            }
        }
    }
}
