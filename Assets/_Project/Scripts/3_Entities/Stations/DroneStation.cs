using System.Collections;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using TheLastSignal.Core;
using TheLastSignal.Core.Interaction;
using TheLastSignal.Entities.Drone;
using TheLastSignal.Entities.Player;
using TheLastSignal.UI;

namespace TheLastSignal.Entities.Stations
{
    /// <summary>
    /// Airlock Recon Drone Terminal (FR-SUR-06, BR-17).
    /// Located at Airlock_Prop_Radar_CRT in the Airlock room.
    /// Exclusively accessible to the Drone Operator role.
    /// Manages drone deployment, docked battery charging, console CRT visuals, and clearance validation.
    /// </summary>
    public class DroneStation : InteractableBase
    {
        [Header("Clearance & Prompt Settings")]
        [SerializeField] private string authorizedPrompt = "<color=#00FFCC>[E]</color> Pilot Recon Drone";
        [SerializeField] private string restrictedPrompt = "<color=#FF4444>[RESTRICTED]</color> Drone Operator Clearance Required";
        [SerializeField] private bool bypassRoleCheckInEditor = false;

        [Header("Drone Binding")]
        [SerializeField] private DroneController droneController;
        [SerializeField] private DroneDockStation dockStation;
        [SerializeField] private Vector2 dockPositionOffset = new Vector2(3.0f, -0.85f);

        [Header("Console Visuals & Lighting")]
        [SerializeField] private Light2D consoleLight;
        [SerializeField] private Color activeConsoleColor = new Color(0f, 0.95f, 1f, 1f);
        [SerializeField] private Color idleConsoleColor = new Color(0.2f, 0.7f, 0.5f, 0.8f);
        [SerializeField] private Color deniedConsoleColor = new Color(1f, 0.2f, 0.2f, 1f);

        [Header("Audio")]
        [SerializeField] private AudioSource audioSource;

        private Coroutine alertFlashRoutine;
        private Coroutine terminalPulseRoutine;
        private bool hasInitialDockAligned = false;

        private void Awake()
        {
            promptMessage = authorizedPrompt;

            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
                if (audioSource == null)
                {
                    audioSource = gameObject.AddComponent<AudioSource>();
                    audioSource.playOnAwake = false;
                    audioSource.spatialBlend = 0.5f;
                }
            }

            FindOrCreateConsoleLight();
            FindOrCreateDockStation();
            EnsureDroneController();
            AlignDroneToDockOnStartup();
        }

        private void Start()
        {
            EnsureDroneController();
            if (droneController != null)
            {
                droneController.OnFlightStateChanged += HandleDroneFlightStateChanged;
            }

            if (consoleLight != null)
            {
                consoleLight.color = idleConsoleColor;
            }
        }

        private void OnDestroy()
        {
            if (droneController != null)
            {
                droneController.OnFlightStateChanged -= HandleDroneFlightStateChanged;
            }
        }

        private void AlignDroneToDockOnStartup()
        {
            if (hasInitialDockAligned) return;
            hasInitialDockAligned = true;

            if (droneController != null && dockStation != null && !droneController.IsFlightActive)
            {
                Vector3 dockPos = dockStation.transform.position;
                droneController.transform.position = dockPos;
                Rigidbody2D rb = droneController.GetComponent<Rigidbody2D>();
                if (rb != null)
                {
                    rb.position = dockPos;
                    rb.linearVelocity = Vector2.zero;
                    rb.constraints = RigidbodyConstraints2D.FreezeAll;
                }
                droneController.SetDockPosition(dockPos);
            }
        }

        private void FindOrCreateConsoleLight()
        {
            if (consoleLight == null)
            {
                // Check if Light_Console_Radar is nearby
                Transform parent = transform.parent;
                if (parent != null)
                {
                    Transform lightObj = parent.Find("Light_Console_Radar");
                    if (lightObj != null)
                    {
                        consoleLight = lightObj.GetComponent<Light2D>();
                    }
                }

                if (consoleLight == null)
                {
                    consoleLight = GetComponentInChildren<Light2D>();
                }
            }
        }

        private void FindOrCreateDockStation()
        {
            if (dockStation == null)
            {
                dockStation = FindAnyObjectByType<DroneDockStation>();
            }

            if (dockStation == null)
            {
                GameObject dockGo = new GameObject("Prop_Drone_Dock_Station");
                dockGo.transform.position = (Vector2)transform.position + dockPositionOffset;
                if (transform.parent != null) dockGo.transform.SetParent(transform.parent, true);

                dockStation = dockGo.AddComponent<DroneDockStation>();
            }
        }

        private void EnsureDroneController()
        {
            if (droneController == null)
            {
                droneController = FindAnyObjectByType<DroneController>();
            }

            if (droneController == null)
            {
                FindOrCreateDockStation();
                Vector2 initialSpawnPos = dockStation != null 
                    ? (Vector2)dockStation.transform.position 
                    : (Vector2)transform.position + dockPositionOffset;

                GameObject droneGo = new GameObject("ReconDrone_Spectre");
                droneGo.transform.position = initialSpawnPos;
                droneController = droneGo.AddComponent<DroneController>();
                droneGo.AddComponent<DroneView>();
            }
        }

        public override string GetInteractionPrompt()
        {
            PlayerController localPlayer = PlayerController.Local;
            if (localPlayer == null) localPlayer = FindAnyObjectByType<PlayerController>();

            if (localPlayer != null && localPlayer.TryGetComponent(out PlayerRole role))
            {
                bool canUse = RoleAbilityValidator.CanUseDroneAbility(role.Role);
#if UNITY_EDITOR || DEBUG
                if (bypassRoleCheckInEditor) canUse = true;
#endif
                if (canUse)
                {
                    return authorizedPrompt;
                }
                else
                {
                    string roleName = role.Data != null ? role.Data.displayName : role.Role.ToString();
#if UNITY_EDITOR || DEBUG
                    return $"{restrictedPrompt} [{roleName}] - Press <color=#FFDD44>[F2]</color> to switch";
#else
                    return restrictedPrompt;
#endif
                }
            }

            return promptMessage;
        }

        public override bool CanInteract(PlayerController player)
        {
            if (player == null) return false;

            // Player can interact as long as they are close enough and not downed
            if (player.TryGetComponent(out PlayerHealth health) && health.IsDowned)
            {
                return false;
            }

            Collider2D col = GetComponent<Collider2D>();
            float sqrDist;
            if (col != null)
            {
                Vector2 closest = col.ClosestPoint(player.transform.position);
                sqrDist = (closest - (Vector2)player.transform.position).sqrMagnitude;
            }
            else
            {
                sqrDist = (transform.position - player.transform.position).sqrMagnitude;
            }

            return sqrDist <= (player.InteractionRange * player.InteractionRange);
        }

        protected override void ProcessInteraction(PlayerController player)
        {
            if (player == null) return;

            // Clearance check: Only Drone Operator may interface with terminal
            bool hasClearance = false;
            if (player.TryGetComponent(out PlayerRole playerRole))
            {
                hasClearance = RoleAbilityValidator.CanUseDroneAbility(playerRole.Role);
#if UNITY_EDITOR || DEBUG
                if (bypassRoleCheckInEditor) hasClearance = true;
#endif
                if (hasClearance && playerRole.Data != null)
                {
                    EnsureDroneController();
                    droneController.ApplyRoleConfig(playerRole.Data);
                }
            }

            if (hasClearance)
            {
                // Authorized Drone Operator!
                EnsureDroneController();

                if (droneController.IsFlightActive)
                {
                    // Already in flight; disengage
                    droneController.DisengageFlight("Recalled via Station Terminal");
                }
                else
                {
                    // Activate persistent drone at its current map location & open Sci-Fi CRT Overlay
                    droneController.EngageFlight(player);

                    // Open Sci-Fi CRT overlay
                    DroneHUDOverlay overlay = DroneHUDOverlay.Instance;
                    if (overlay == null)
                    {
                        overlay = FindAnyObjectByType<DroneHUDOverlay>();
                    }

                    if (overlay == null)
                    {
                        GameObject canvasObj = GameObject.Find("LobbyCanvas");
                        if (canvasObj == null)
                        {
                            Canvas c = FindAnyObjectByType<Canvas>();
                            if (c != null) canvasObj = c.gameObject;
                        }

                        GameObject overlayGo = new GameObject("DroneHUDOverlay", typeof(RectTransform));
                        if (canvasObj != null)
                        {
                            overlayGo.transform.SetParent(canvasObj.transform, false);
                            RectTransform rt = overlayGo.GetComponent<RectTransform>();
                            rt.anchorMin = Vector2.zero;
                            rt.anchorMax = Vector2.one;
                            rt.offsetMin = Vector2.zero;
                            rt.offsetMax = Vector2.zero;
                        }
                        overlay = overlayGo.AddComponent<DroneHUDOverlay>();
                    }

                    if (overlay != null)
                    {
                        overlay.Open(droneController);
                    }
                }
            }
            else
            {
                // Access Denied! Show tactile feedback and buzzer
                PlayAccessDeniedFeedback(player);
            }
        }

        private void PlayAccessDeniedFeedback(PlayerController player)
        {
            if (audioSource != null)
            {
                audioSource.PlayOneShot(DroneAudioSynthesizer.GetAccessDeniedClip(), 0.7f);
            }

            string roleName = "Unknown";
            if (player.TryGetComponent(out PlayerRole r))
            {
                roleName = r.Data != null ? r.Data.displayName : r.Role.ToString();
            }

            Debug.LogWarning($"<color=#FF4444>[DroneStation]</color> ACCESS DENIED for {player.name} (Role: {roleName}). Requires Drone Operator clearance! Press [F2] in Editor to switch role.");

            if (alertFlashRoutine != null) StopCoroutine(alertFlashRoutine);
            alertFlashRoutine = StartCoroutine(FlashDeniedColorRoutine());
        }

        private IEnumerator FlashDeniedColorRoutine()
        {
            if (consoleLight == null) yield break;

            Color original = consoleLight.color;
            for (int i = 0; i < 3; i++)
            {
                consoleLight.color = deniedConsoleColor;
                consoleLight.intensity = 2.0f;
                yield return new WaitForSeconds(0.08f);
                consoleLight.color = Color.black;
                yield return new WaitForSeconds(0.06f);
            }

            consoleLight.color = original;
            consoleLight.intensity = 1.0f;
        }

        private void HandleDroneFlightStateChanged(bool inFlight)
        {
            if (consoleLight != null)
            {
                if (inFlight)
                {
                    consoleLight.color = activeConsoleColor;
                    consoleLight.intensity = 1.8f;
                    if (terminalPulseRoutine != null) StopCoroutine(terminalPulseRoutine);
                    terminalPulseRoutine = StartCoroutine(PulseActiveTerminalLightRoutine());
                }
                else
                {
                    if (terminalPulseRoutine != null)
                    {
                        StopCoroutine(terminalPulseRoutine);
                        terminalPulseRoutine = null;
                    }
                    consoleLight.color = idleConsoleColor;
                    consoleLight.intensity = 1.0f;
                }
            }
        }

        private IEnumerator PulseActiveTerminalLightRoutine()
        {
            while (droneController != null && droneController.IsFlightActive)
            {
                float t = (Mathf.Sin(Time.time * 4f) * 0.35f) + 1.45f;
                if (consoleLight != null)
                {
                    consoleLight.intensity = t;
                }
                yield return null;
            }
        }
    }
}
