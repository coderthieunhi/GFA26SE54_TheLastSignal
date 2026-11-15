using System.Collections;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using TheLastSignal.Core.Interaction;
using TheLastSignal.Entities.Player;
using TheLastSignal.UI;

namespace TheLastSignal.Entities.Environment
{
    /// <summary>
    /// Interactive Crew Muster & Orientation Station at Bedroom center.
    /// Acts as the first interactive asset players encounter upon spawning.
    /// Provides initial gameplay briefing, crew roles, survival mechanics, and objective evacuation review.
    /// Implements unified IInteractable interface for seamless [E] player interaction.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class CrewOrientationStation : InteractableBase
    {
        public enum StationState
        {
            Idle,
            Active,
            Completed
        }

        [Header("Visual Components")]
        [SerializeField] private SpriteRenderer baseRenderer;
        [SerializeField] private SpriteRenderer hologramRenderer;
        [SerializeField] private Light2D projectorLight;

        [Header("Sprites")]
        [SerializeField] private Sprite idleSprite;
        [SerializeField] private Sprite activeSprite;
        [SerializeField] private Sprite completedSprite;
        [SerializeField] private Sprite hologramSprite;

        [Header("Hologram Settings")]
        [SerializeField] private Vector3 hologramOffset = new Vector3(0f, 0.16f, 0f);
        [SerializeField] private float bobbingAmplitude = 0.025f;
        [SerializeField] private float bobbingSpeed = 2.5f;

        [Header("Tutorial UI Reference")]
        [SerializeField] private CrewOrientationTutorialUI tutorialUI;

        // Runtime states
        private StationState currentState = StationState.Idle;
        private bool isCompleted = false;
        private bool isBusy = false;
        private PlayerController interactingPlayer;
        private Coroutine activeHoloCoroutine;
        private Vector3 initialHoloPos;

        public StationState CurrentState => currentState;
        public bool IsCompleted => isCompleted;
        public bool IsBusy => isBusy;

        /// <summary>
        /// Calculates a stable, non-overlapping world-space prompt anchor position opposite or clear of player approach.
        /// Guaranteed never to obscure player helmet, body, briefing tabletop, or active hologram.
        /// </summary>
        public Vector3 GetPromptPositionForPlayer(Vector3 playerPosition)
        {
            Vector2 delta = ((Vector2)playerPosition - (Vector2)transform.position);
            if (Mathf.Abs(delta.y) >= Mathf.Abs(delta.x))
            {
                // Player approaching from North or South
                return delta.y < 0 ? transform.position + new Vector3(0f, 1.50f, 0f) : transform.position + new Vector3(0f, -1.55f, 0f);
            }
            else
            {
                // Player approaching from East or West
                return delta.x < 0 ? transform.position + new Vector3(1.50f, 0f, 0f) : transform.position + new Vector3(-1.50f, 0f, 0f);
            }
        }

        private void Awake()
        {
            InitializeVisualComponents();
            promptMessage = "[ E ] ACCESS CREW ORIENTATION\n<size=75%><color=#7DD3FC>CREW MUSTER & ORIENTATION STATION</color></size>";
        }

        private void Start()
        {
            SetVisualState(isCompleted ? StationState.Completed : StationState.Idle);
        }

        public void InitializeVisualComponents()
        {
            // Auto-locate or build hierarchy if needed
            if (baseRenderer == null)
            {
                var tabletop = transform.Find("VisualRoot/Tabletop");
                if (tabletop != null) baseRenderer = tabletop.GetComponent<SpriteRenderer>();
                if (baseRenderer == null) baseRenderer = GetComponentInChildren<SpriteRenderer>();
            }

            if (hologramRenderer == null)
            {
                var holo = transform.Find("VisualRoot/Hologram");
                if (holo != null) hologramRenderer = holo.GetComponent<SpriteRenderer>();
            }

            if (projectorLight == null)
            {
                var lightTr = transform.Find("VisualRoot/ProjectorLight");
                if (lightTr != null) projectorLight = lightTr.GetComponent<Light2D>();
                if (projectorLight == null) projectorLight = GetComponentInChildren<Light2D>();
            }

            // Ensure sprites are loaded from AssetDatabase if running in Editor / standalone
#if UNITY_EDITOR
            const string spriteDir = "Assets/_Project/Sprites/Environments/Bedroom/Props";
            if (idleSprite == null) idleSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>($"{spriteDir}/Prop_Crew_Orientation_Station_Idle.png");
            if (activeSprite == null) activeSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>($"{spriteDir}/Prop_Crew_Orientation_Station_Active.png");
            if (completedSprite == null) completedSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>($"{spriteDir}/Prop_Crew_Orientation_Station_Completed.png");
            if (hologramSprite == null) hologramSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>($"{spriteDir}/Prop_Crew_Orientation_Station_Hologram.png");
#endif

            if (tutorialUI == null)
            {
                tutorialUI = FindAnyObjectByType<CrewOrientationTutorialUI>();
            }

            if (hologramRenderer != null)
            {
                hologramRenderer.transform.localPosition = hologramOffset;
                initialHoloPos = hologramOffset;
                if (hologramSprite != null && hologramRenderer.sprite == null)
                {
                    hologramRenderer.sprite = hologramSprite;
                }
            }
        }

        public override string GetInteractionPrompt()
        {
            if (isCompleted)
            {
                return "[ E ] REVIEW CREW ORIENTATION\n<size=75%><color=#86EFAC>CREW MUSTER & ORIENTATION STATION (COMPLETED)</color></size>";
            }
            return "[ E ] ACCESS CREW ORIENTATION\n<size=75%><color=#7DD3FC>CREW MUSTER & ORIENTATION STATION</color></size>";
        }

        public override bool CanInteract(PlayerController player)
        {
            if (isBusy) return false;
            return base.CanInteract(player);
        }

        protected override void ProcessInteraction(PlayerController player)
        {
            interactingPlayer = player;
            isBusy = true;

            // Activate visual state B
            SetVisualState(StationState.Active);

            // Open local UI
            if (tutorialUI == null)
            {
                tutorialUI = FindAnyObjectByType<CrewOrientationTutorialUI>();
                if (tutorialUI == null)
                {
                    GameObject uiHost = new GameObject("CrewOrientationTutorialUI_Host");
                    tutorialUI = uiHost.AddComponent<CrewOrientationTutorialUI>();
                }
            }

            if (tutorialUI != null)
            {
                tutorialUI.OpenTutorial(interactingPlayer, OnTutorialClosed);
            }
            else
            {
                Debug.LogWarning("[CrewOrientationStation] Tutorial UI could not be found or built!");
                isBusy = false;
                SetVisualState(isCompleted ? StationState.Completed : StationState.Idle);
            }
        }

        private void OnTutorialClosed()
        {
            isCompleted = true;
            isBusy = false;
            interactingPlayer = null;

            // Transition to visual state C (Completed / Review Available)
            SetVisualState(StationState.Completed);
            Debug.Log("[CrewOrientationStation] Crew Orientation session completed.");
        }

        public void SetVisualState(StationState state)
        {
            currentState = state;

            if (activeHoloCoroutine != null)
            {
                StopCoroutine(activeHoloCoroutine);
                activeHoloCoroutine = null;
            }

            switch (state)
            {
                case StationState.Idle:
                    if (baseRenderer != null && idleSprite != null) baseRenderer.sprite = idleSprite;
                    if (hologramRenderer != null) hologramRenderer.gameObject.SetActive(false);
                    if (projectorLight != null)
                    {
                        projectorLight.enabled = true;
                        projectorLight.intensity = 0.15f;
                        projectorLight.color = new Color(0.22f, 0.74f, 0.97f, 1f);
                    }
                    break;

                case StationState.Active:
                    if (baseRenderer != null && activeSprite != null) baseRenderer.sprite = activeSprite;
                    if (hologramRenderer != null)
                    {
                        hologramRenderer.gameObject.SetActive(true);
                        if (hologramSprite != null) hologramRenderer.sprite = hologramSprite;
                    }
                    if (projectorLight != null)
                    {
                        projectorLight.enabled = true;
                        projectorLight.intensity = 0.85f;
                        projectorLight.color = new Color(0.22f, 0.74f, 0.97f, 1f);
                    }
                    activeHoloCoroutine = StartCoroutine(AnimateHologram());
                    break;

                case StationState.Completed:
                    if (baseRenderer != null && completedSprite != null) baseRenderer.sprite = completedSprite;
                    if (hologramRenderer != null) hologramRenderer.gameObject.SetActive(false);
                    if (projectorLight != null)
                    {
                        projectorLight.enabled = true;
                        projectorLight.intensity = 0.20f;
                        projectorLight.color = new Color(0.13f, 0.77f, 0.37f, 1f); // Subtle green readiness LED
                    }
                    break;
            }
        }

        private IEnumerator AnimateHologram()
        {
            if (hologramRenderer == null) yield break;

            Color baseCol = new Color(1f, 1f, 1f, 0f);
            hologramRenderer.color = baseCol;

            // Fade in over 0.25s
            float elapsed = 0f;
            while (elapsed < 0.25f)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / 0.25f);
                hologramRenderer.color = new Color(1f, 1f, 1f, t * 0.95f);
                yield return null;
            }
            hologramRenderer.color = new Color(1f, 1f, 1f, 0.95f);

            // Subtle vertical bobbing & scanline shimmer while active
            float startTime = Time.time;
            while (currentState == StationState.Active)
            {
                float bob = Mathf.Sin((Time.time - startTime) * bobbingSpeed) * bobbingAmplitude;
                hologramRenderer.transform.localPosition = initialHoloPos + new Vector3(0f, bob, 0f);
                yield return null;
            }
        }
    }
}
