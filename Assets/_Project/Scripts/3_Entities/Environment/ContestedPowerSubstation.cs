using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using TMPro;
using TheLastSignal.Core.Interaction;
using TheLastSignal.Entities.Player;

namespace TheLastSignal.Entities.Environment
{
    public enum SubstationState
    {
        Unpowered, // Dark, broken/smashed. Survivors can channel [E] for 3s to restore power.
        Powered    // Active high-beam floodlights for 25s. Monster can smash it in 1s.
    }

    /// <summary>
    /// Contested Power Substation at Node A center for 3v1 asymmetrical gameplay.
    /// Survivors channel [E] for 3.0s to illuminate the junction for 25s, revealing and slowing the monster.
    /// The Monster player can smash the substation in 1.0s to plunge the junction back into darkness.
    /// </summary>
    [RequireComponent(typeof(Collider2D), typeof(AudioSource))]
    public class ContestedPowerSubstation : InteractableBase
    {
        [Header("State & Durations")]
        [SerializeField] private SubstationState state = SubstationState.Unpowered;
        [SerializeField] private float powerDuration = 25f;
        [SerializeField] private float survivorChannelDuration = 3.0f;
        [SerializeField] private float monsterChannelDuration = 0.8f;
        [SerializeField] private float monsterSlowMultiplier = 0.8f; // -20% speed debuff in light

        [Header("Lighting")]
        [SerializeField] private Light2D consoleLight;
        [SerializeField] private List<Light2D> floodlights = new List<Light2D>();

        [Header("Visual Sprites")]
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private Sprite unpoweredSprite;
        [SerializeField] private Sprite poweredSprite;

        [Header("Audio")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip clankClip;
        [SerializeField] private AudioClip humClip;
        [SerializeField] private AudioClip smashClip;
        [SerializeField] private AudioClip alarmClip;

        // Runtime Tracking
        private float currentPowerTimer = 0f;
        private PlayerController activeChanneler = null;
        private float channelProgress = 0f;
        private bool isChannelingAsMonster = false;

        // UI Progress Bar
        private GameObject progressCanvasGo;
        private Slider progressSlider;
        private Image progressFillImage;
        private TextMeshProUGUI progressText;

        // Boundary of Node A junction chamber for monster debuff
        private Bounds nodeABounds = new Bounds(new Vector3(-0.5f, 27.5f, 0f), new Vector3(8f, 8f, 10f));
        private List<PlayerController> slowedMonsters = new List<PlayerController>();

        public SubstationState State => state;
        public float RemainingPowerTime => currentPowerTimer;
        public bool IsChanneling => activeChanneler != null;

        private void Awake()
        {
            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
            }

            if (audioSource != null)
            {
                audioSource.playOnAwake = false;
                audioSource.spatialBlend = 0.5f;
            }

            if (consoleLight == null)
            {
                consoleLight = GetComponentInChildren<Light2D>();
            }

            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponent<SpriteRenderer>();
            }

            if (unpoweredSprite == null)
            {
#if UNITY_EDITOR
                unpoweredSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Sprites/Environments/Junction/Props/Prop_NodeA_BioReactor_Unpowered.png");
#endif
            }

            if (poweredSprite == null)
            {
#if UNITY_EDITOR
                poweredSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Sprites/Environments/Junction/Props/Prop_NodeA_BioReactor_Powered.png");
#endif
            }

            CreateProceduralAudioClips();
            EnsureProgressBarUI();
            InitializeFloodlights();
            UpdateVisualState();
        }

        private void Update()
        {
            HandleChannelingInput();
            HandlePowerTimer();
            HandleMonsterDebuffInLight();
        }

        public override string GetInteractionPrompt()
        {
            // Note: promptMessage is shown by InteractionPromptUI
            if (state == SubstationState.Unpowered)
            {
                return "[E] Khởi động Lưới điện Node A (3s)";
            }
            else
            {
                int sec = Mathf.CeilToInt(currentPowerTimer);
                return $"[E] Lưới điện đang bật ({sec}s) | Quái vật [E]: Đập phá";
            }
        }

        public override bool CanInteract(PlayerController player)
        {
            if (player == null) return false;
            if (activeChanneler != null && activeChanneler != player) return false;

            bool isMonster = player.IsMonster;

            if (state == SubstationState.Unpowered)
            {
                // Unpowered: Only survivors can channel to restore power
                if (isMonster) return false;
            }
            else
            {
                // Powered: Only monster can interact to smash the breaker
                if (!isMonster) return false;
            }

            // Generous proximity check to collider edge
            Collider2D col = GetComponent<Collider2D>();
            if (col != null)
            {
                Vector2 closest = col.ClosestPoint(player.transform.position);
                float distSqr = (closest - (Vector2)player.transform.position).sqrMagnitude;
                float maxAllowed = Mathf.Max(player.InteractionRange + 0.8f, 2.5f);
                if (distSqr > maxAllowed * maxAllowed) return false;
            }

            return true;
        }

        protected override void ProcessInteraction(PlayerController player)
        {
            if (activeChanneler != null) return;

            bool isMonster = player.IsMonster;
            StartChanneling(player, isMonster);
        }

        private void StartChanneling(PlayerController player, bool asMonster)
        {
            activeChanneler = player;
            isChannelingAsMonster = asMonster;
            channelProgress = 0f;

            if (clankClip != null && audioSource != null) audioSource.PlayOneShot(clankClip, 0.7f);

            if (progressCanvasGo != null)
            {
                progressCanvasGo.SetActive(true);
                if (progressSlider != null) progressSlider.value = 0f;

                if (progressText != null)
                {
                    progressText.text = asMonster ? "ĐANG ĐẬP PHÁ CẦU DAO (0%)..." : "ĐANG ĐÓNG CẦU DAO (0%)...";
                    progressText.color = asMonster ? new Color(1f, 0.3f, 0.2f) : new Color(0.3f, 0.9f, 1f);
                }

                if (progressFillImage != null)
                {
                    progressFillImage.color = asMonster ? new Color(0.9f, 0.2f, 0.1f, 0.9f) : new Color(0.2f, 0.8f, 1f, 0.9f);
                }
            }

            Debug.Log($"[ContestedPowerSubstation] {(asMonster ? "Monster" : "Survivor")} started channeling.");
        }

        private void HandleChannelingInput()
        {
            if (activeChanneler == null) return;

            // Check distance to closest edge of console collider
            Collider2D col = GetComponent<Collider2D>();
            Vector2 closest = col != null ? col.ClosestPoint(activeChanneler.transform.position) : (Vector2)transform.position;
            float distSqr = (closest - (Vector2)activeChanneler.transform.position).sqrMagnitude;
            float maxAllowedDist = Mathf.Max(activeChanneler.InteractionRange + 0.8f, 2.5f);

            if (distSqr > maxAllowedDist * maxAllowedDist)
            {
                CancelChanneling();
                return;
            }

            float duration = isChannelingAsMonster ? monsterChannelDuration : survivorChannelDuration;
            channelProgress += Time.deltaTime;

            float pct = Mathf.Clamp01(channelProgress / duration);
            if (progressSlider != null)
            {
                progressSlider.value = pct;
            }

            if (progressText != null)
            {
                int p = Mathf.RoundToInt(pct * 100f);
                progressText.text = isChannelingAsMonster ? $"ĐANG ĐẬP PHÁ ({p}%)" : $"ĐANG ĐÓNG CẦU DAO ({p}%)";
            }

            if (channelProgress >= duration)
            {
                CompleteChanneling();
            }
        }

        private void CompleteChanneling()
        {
            if (progressCanvasGo != null) progressCanvasGo.SetActive(false);

            if (isChannelingAsMonster)
            {
                SmashBreaker();
            }
            else
            {
                PowerOn();
            }

            activeChanneler = null;
        }

        private void CancelChanneling()
        {
            if (progressCanvasGo != null) progressCanvasGo.SetActive(false);
            activeChanneler = null;
            channelProgress = 0f;
            Debug.Log("[ContestedPowerSubstation] Channeling cancelled.");
        }

        public void PowerOn()
        {
            state = SubstationState.Powered;
            currentPowerTimer = powerDuration;

            UpdateVisualState();
            if (clankClip != null && audioSource != null) audioSource.PlayOneShot(clankClip);
            PlaySound(humClip, loop: true);

            Debug.Log("<color=green>[ContestedPowerSubstation] POWER RESTORED! High-beam floodlights active for 25s.</color>");
        }

        public void SmashBreaker()
        {
            state = SubstationState.Unpowered;
            currentPowerTimer = 0f;

            UpdateVisualState();
            PlaySound(smashClip);
            StartCoroutine(PlayAlarmBurst());

            // Sparks / flicker effect
            StartCoroutine(FlashSparks());

            Debug.Log("<color=red>[ContestedPowerSubstation] BREAKER SMASHED BY MONSTER! Total blackout triggered.</color>");
        }

        private void HandlePowerTimer()
        {
            if (state != SubstationState.Powered) return;

            currentPowerTimer -= Time.deltaTime;
            if (currentPowerTimer <= 0f)
            {
                currentPowerTimer = 0f;
                state = SubstationState.Unpowered;
                UpdateVisualState();
                if (audioSource != null && audioSource.isPlaying && audioSource.clip == humClip)
                {
                    audioSource.Stop();
                }
                Debug.Log("[ContestedPowerSubstation] Power timer naturally expired. Returned to Unpowered.");
            }
        }

        private void HandleMonsterDebuffInLight()
        {
            if (state == SubstationState.Powered)
            {
                var allPlayers = FindObjectsByType<PlayerController>(FindObjectsInactive.Exclude);
                foreach (var p in allPlayers)
                {
                    if (p == null) continue;

                    if (p.IsMonster)
                    {
                        bool isInsideLight = nodeABounds.Contains(p.transform.position);
                        if (isInsideLight)
                        {
                            p.SpeedMultiplier = monsterSlowMultiplier;
                            if (!slowedMonsters.Contains(p)) slowedMonsters.Add(p);
                        }
                        else
                        {
                            p.SpeedMultiplier = 1.0f;
                            slowedMonsters.Remove(p);
                        }
                    }
                }
            }
            else
            {
                // Revert any slowed monsters when dark
                if (slowedMonsters.Count > 0)
                {
                    foreach (var m in slowedMonsters)
                    {
                        if (m != null) m.SpeedMultiplier = 1.0f;
                    }
                    slowedMonsters.Clear();
                }
            }
        }

        private void UpdateVisualState()
        {
            bool isPowered = (state == SubstationState.Powered);

            // 0. Dynamic Biomechanical Reactor Sprite (Unpowered vs Supercharged Powered)
            if (spriteRenderer != null)
            {
                if (isPowered && poweredSprite != null)
                {
                    spriteRenderer.sprite = poweredSprite;
                }
                else if (!isPowered && unpoweredSprite != null)
                {
                    spriteRenderer.sprite = unpoweredSprite;
                }
            }

            // 1. Console indicator light (Red standby when unpowered, vibrant green when powered)
            if (consoleLight != null)
            {
                consoleLight.color = isPowered ? new Color(0.15f, 0.9f, 1f, 1f) : new Color(1f, 0.2f, 0.15f, 1f);
                consoleLight.intensity = isPowered ? 2.0f : 0.5f;
                consoleLight.pointLightOuterRadius = isPowered ? 4.5f : 1.6f;
                consoleLight.enabled = true;
            }

            // 2. 4 Floodlights at corridor entrances
            foreach (var fl in floodlights)
            {
                if (fl != null)
                {
                    fl.enabled = isPowered;
                    fl.intensity = isPowered ? 1.4f : 0f;
                }
            }
        }

        private void InitializeFloodlights()
        {
            if (floodlights.Count == 4) return;
            floodlights.Clear();

            // Check if existing floodlights in scene
            string[] names = { "Floodlight_North", "Floodlight_South", "Floodlight_West", "Floodlight_East" };
            Vector3[] positions = {
                new Vector3(0.0f, 30.5f, 0f),
                new Vector3(0.0f, 25.5f, 0f),
                new Vector3(-3.0f, 28.0f, 0f),
                new Vector3(3.0f, 28.0f, 0f)
            };

            for (int i = 0; i < 4; i++)
            {
                Transform existing = transform.Find(names[i]);
                GameObject flGo = existing != null ? existing.gameObject : new GameObject(names[i]);
                flGo.transform.SetParent(transform, false);
                flGo.transform.position = positions[i];

                Light2D l2d = flGo.GetComponent<Light2D>();
                if (l2d == null) l2d = flGo.AddComponent<Light2D>();

                l2d.lightType = Light2D.LightType.Point;
                l2d.color = new Color(1.0f, 0.92f, 0.75f, 1f); // Warm high-beam industrial floodlight
                l2d.intensity = 1.4f;
                l2d.pointLightOuterRadius = 6.0f;
                l2d.pointLightInnerRadius = 0.5f;
                l2d.enabled = false;

                floodlights.Add(l2d);
            }
        }

        private void EnsureProgressBarUI()
        {
            if (progressCanvasGo != null) return;

            // World space mini canvas floating above substation
            progressCanvasGo = new GameObject("Substation_ProgressBar_Canvas");
            progressCanvasGo.transform.SetParent(transform, false);
            progressCanvasGo.transform.localPosition = new Vector3(0f, 1.35f, 0f);

            Canvas canvas = progressCanvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingLayerName = "Characters";
            canvas.sortingOrder = 100;
            progressCanvasGo.AddComponent<CanvasScaler>();

            RectTransform rtCanvas = progressCanvasGo.GetComponent<RectTransform>();
            rtCanvas.sizeDelta = new Vector2(200, 45);
            rtCanvas.localScale = new Vector3(0.012f, 0.012f, 1f);

            // Slider Background
            GameObject sliderGo = new GameObject("ProgressBar_Slider");
            sliderGo.transform.SetParent(progressCanvasGo.transform, false);
            RectTransform rtSlider = sliderGo.AddComponent<RectTransform>();
            rtSlider.sizeDelta = new Vector2(180, 24);
            rtSlider.anchoredPosition = new Vector2(0, -6);

            Image bgImg = sliderGo.AddComponent<Image>();
            bgImg.color = new Color(0.08f, 0.1f, 0.14f, 0.95f);

            progressSlider = sliderGo.AddComponent<Slider>();
            progressSlider.minValue = 0f;
            progressSlider.maxValue = 1f;

            // Fill Area
            GameObject fillArea = new GameObject("Fill Area");
            fillArea.transform.SetParent(sliderGo.transform, false);
            RectTransform rtFillArea = fillArea.AddComponent<RectTransform>();
            rtFillArea.sizeDelta = new Vector2(176, 20);

            GameObject fill = new GameObject("Fill");
            fill.transform.SetParent(fillArea.transform, false);
            RectTransform rtFill = fill.AddComponent<RectTransform>();
            rtFill.sizeDelta = new Vector2(0, 0);

            progressFillImage = fill.AddComponent<Image>();
            progressFillImage.color = new Color(0.2f, 0.8f, 1f, 0.95f);
            progressSlider.fillRect = rtFill;

            // Text Label
            GameObject textGo = new GameObject("Text_Action");
            textGo.transform.SetParent(progressCanvasGo.transform, false);
            RectTransform rtText = textGo.AddComponent<RectTransform>();
            rtText.sizeDelta = new Vector2(200, 20);
            rtText.anchoredPosition = new Vector2(0, 14);

            progressText = textGo.AddComponent<TextMeshProUGUI>();
            progressText.text = "ĐANG ĐÓNG CẦU DAO...";
            progressText.fontSize = 14;
            progressText.fontStyle = FontStyles.Bold;
            progressText.alignment = TextAlignmentOptions.Center;
            progressText.color = new Color(0.3f, 0.9f, 1f, 1f);

            progressCanvasGo.SetActive(false);
        }

        private IEnumerator FlashSparks()
        {
            if (consoleLight == null) yield break;
            for (int i = 0; i < 4; i++)
            {
                consoleLight.intensity = 2.5f;
                consoleLight.color = new Color(1f, 0.9f, 0.4f, 1f);
                yield return new WaitForSeconds(0.05f);
                consoleLight.intensity = 0.2f;
                consoleLight.color = new Color(1f, 0.1f, 0.05f, 1f);
                yield return new WaitForSeconds(0.06f);
            }
            consoleLight.intensity = 0.6f;
        }

        private IEnumerator PlayAlarmBurst()
        {
            PlaySound(alarmClip);
            yield return new WaitForSeconds(2.5f);
            if (audioSource != null && audioSource.clip == alarmClip) audioSource.Stop();
        }

        private void PlaySound(AudioClip clip, bool loop = false)
        {
            if (audioSource == null || clip == null) return;
            audioSource.loop = loop;
            audioSource.clip = clip;
            audioSource.Play();
        }

        private void CreateProceduralAudioClips()
        {
            if (clankClip == null) clankClip = GenerateToneClip("BreakerClank", 180, 0.25f, isPercussive: true);
            if (humClip == null) humClip = GenerateToneClip("PowerHum", 60, 1.0f, isPercussive: false);
            if (smashClip == null) smashClip = GenerateToneClip("BreakerSmash", 90, 0.45f, isPercussive: true);
            if (alarmClip == null) alarmClip = GenerateToneClip("AlarmBuzzer", 520, 0.8f, isPercussive: false);
        }

        private static AudioClip GenerateToneClip(string name, float freq, float length, bool isPercussive)
        {
            int sampleRate = 44100;
            int samples = (int)(sampleRate * length);
            float[] data = new float[samples];

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / sampleRate;
                float envelope = isPercussive ? Mathf.Exp(-t * 8f) : 1f;
                // Basic harmonic sine + noise
                float wave = Mathf.Sin(2f * Mathf.PI * freq * t) * 0.7f + Mathf.Sin(4f * Mathf.PI * freq * t) * 0.3f;
                if (isPercussive) wave += (Random.value * 2f - 1f) * 0.25f;
                data[i] = wave * envelope * 0.4f;
            }

            AudioClip clip = AudioClip.Create(name, samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.35f);
            Gizmos.DrawWireCube(nodeABounds.center, nodeABounds.size);
        }
    }
}
