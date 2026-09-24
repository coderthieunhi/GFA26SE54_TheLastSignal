using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace TheLastSignal.Entities.Environment
{
    /// <summary>
    /// Visual status indicator light mounted on the wall adjacent to a door.
    /// Changes sprite and 2D light color between Red (Closed) and Green (Open) dynamically.
    /// </summary>
    [ExecuteAlways]
    public class DoorIndicatorLight : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private DoorController targetDoor;
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private Light2D statusLight;

        [Header("Sprites")]
        [SerializeField] private Sprite redSprite;
        [SerializeField] private Sprite greenSprite;

        [Header("Light Colors")]
        [SerializeField] private Color redColor = new Color(1.0f, 0.18f, 0.18f, 1.0f);
        [SerializeField] private Color greenColor = new Color(0.2f, 1.0f, 0.4f, 1.0f);

        private DoorState lastState = (DoorState)(-1);

        private void Awake()
        {
            if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
            if (statusLight == null) statusLight = GetComponent<Light2D>();
        }

        private void OnEnable()
        {
            if (targetDoor != null)
            {
                targetDoor.OnStateChanged += HandleStateChanged;
                UpdateVisuals(targetDoor.CurrentState);
            }
        }

        private void OnDisable()
        {
            if (targetDoor != null)
            {
                targetDoor.OnStateChanged -= HandleStateChanged;
            }
        }

        private void Update()
        {
            if (targetDoor != null && targetDoor.CurrentState != lastState)
            {
                UpdateVisuals(targetDoor.CurrentState);
            }
        }

        public void Initialize(DoorController door, Sprite red, Sprite green, Light2D light = null)
        {
            targetDoor = door;
            redSprite = red;
            greenSprite = green;
            if (light != null) statusLight = light;
            if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();

            if (targetDoor != null)
            {
                targetDoor.OnStateChanged += HandleStateChanged;
                UpdateVisuals(targetDoor.CurrentState);
            }
        }

        private void HandleStateChanged(DoorState state)
        {
            UpdateVisuals(state);
        }

        public void UpdateVisuals(DoorState state)
        {
            lastState = state;
            bool isOpen = (state == DoorState.Open);

            if (spriteRenderer != null)
            {
                Sprite targetSprite = isOpen ? greenSprite : redSprite;
                if (targetSprite != null)
                {
                    spriteRenderer.sprite = targetSprite;
                }
            }

            if (statusLight != null)
            {
                statusLight.color = isOpen ? greenColor : redColor;
            }
        }
    }
}
