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
        [SerializeField] private float powerDuration = 8f; // 8-second burst window for high tension
        [SerializeField] private float survivorChannelDuration = 3.0f;
        [SerializeField] private float monsterChannelDuration = 0.8f;
        [SerializeField] private float monsterSlowMultiplier = 0.8f; // -20% speed debuff in light

        [Header("Internal & Outward Lighting")]
        [SerializeField] private Light2D consoleLight;
        [SerializeField] private Light2D coreLight;
        [SerializeField] private Light2D chamberDeckLight;
        [SerializeField] private List<Light2D> outwardBeams = new List<Light2D>();
        [SerializeField] private ParticleSystem beamParticles;

        [Header("Node A Network Environmental Lighting")]
        [SerializeField] private List<Light2D> nodeAEnvironmentalLights = new List<Light2D>();

        [Header("Calibrated Base Lighting Intensities")]
        [SerializeField] private float baseCoreIntensity = 1.6f;
        [SerializeField] private float baseDeckIntensity = 0.35f;
        [SerializeField] private float baseConnectorIntensity = 0.5f;
        [SerializeField] private float baseConsoleIntensity = 0.35f;
        [SerializeField] private float baseEnvironmentalLightIntensity = 0.50f;

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
                unpoweredSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Sprites/Environments/Junction/Props/Prop_NodeA_Substation_Unpowered.png");
#endif
            }

            if (poweredSprite == null)
            {
#if UNITY_EDITOR
                poweredSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Sprites/Environments/Junction/Props/Prop_NodeA_Substation_Powered.png");
#endif
            }

            CreateProceduralAudioClips();
            EnsureProgressBarUI();
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
            StartCoroutine(StartupSpoolUpSequence());
            if (clankClip != null && audioSource != null) audioSource.PlayOneShot(clankClip);
            PlaySound(humClip, loop: true);

            Debug.Log("<color=green>[ContestedPowerSubstation] POWER RESTORED! 8s emergency floodlight burst active.</color>");
        }

        public void SmashBreaker()
        {
            state = SubstationState.Unpowered;
            currentPowerTimer = 0f;

            StopAllCoroutines();
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

            if (currentPowerTimer > 2.5f)
            {
                // Full power: subtle AC energy pulse (+/- 4%)
                float pulse = 1.0f + Mathf.Sin(Time.time * 18f) * 0.04f;
                ApplyDynamicPowerModulation(pulse, isBrownout: false);
            }
            else if (currentPowerTimer > 0f)
            {
                // Brownout warning (last 2.5s): rapid voltage fluctuation
                float flicker = (Mathf.Sin(Time.time * 28f) > 0.05f) ? 0.85f : 0.35f;
                ApplyDynamicPowerModulation(flicker, isBrownout: true);
            }
            else
            {
                currentPowerTimer = 0f;
                state = SubstationState.Unpowered;
                UpdateVisualState();
                if (audioSource != null && audioSource.isPlaying && audioSource.clip == humClip)
                {
                    audioSource.Stop();
                }
                Debug.Log("[ContestedPowerSubstation] Power timer naturally expired (8s). Returned to Unpowered.");
            }
        }

        private void ApplyDynamicPowerModulation(float multiplier, bool isBrownout)
        {
            Color coreCol = isBrownout ? new Color(1.0f, 0.75f, 0.4f, 1f) : new Color(0.0f, 0.85f, 1.0f, 1f);
            Color beamCol = isBrownout ? new Color(1.0f, 0.70f, 0.35f, 1f) : new Color(0.1f, 0.85f, 1.0f, 1f);

            if (coreLight != null && coreLight.enabled)
            {
                coreLight.intensity = baseCoreIntensity * multiplier;
                coreLight.color = coreCol;
            }

            if (chamberDeckLight != null && chamberDeckLight.enabled)
            {
                chamberDeckLight.intensity = baseDeckIntensity * multiplier;
            }

            foreach (var b in outwardBeams)
            {
                if (b != null && b.enabled)
                {
                    b.intensity = baseConnectorIntensity * multiplier;
                    b.color = beamCol;
                }
            }

            // Also modulate environmental corridor lights during brownout flicker
            foreach (var envLight in nodeAEnvironmentalLights)
            {
                if (envLight != null && envLight.enabled)
                {
                    envLight.intensity = isBrownout ? (multiplier * 0.45f) : 0.85f;
                }
            }
        }

        private IEnumerator StartupSpoolUpSequence()
        {
            // Initial electrical ignition surge (0.08s) - controlled proportional surge without blowout
            if (coreLight != null) coreLight.intensity = baseCoreIntensity * 1.35f;
            foreach (var b in outwardBeams) { if (b != null) b.intensity = baseConnectorIntensity * 1.3f; }
            yield return new WaitForSeconds(0.08f);

            // Voltage dip
            if (coreLight != null) coreLight.intensity = baseCoreIntensity * 0.5f;
            foreach (var b in outwardBeams) { if (b != null) b.intensity = baseConnectorIntensity * 0.4f; }
            yield return new WaitForSeconds(0.06f);

            // Second surge & settle
            if (coreLight != null) coreLight.intensity = baseCoreIntensity * 1.15f;
            foreach (var b in outwardBeams) { if (b != null) b.intensity = baseConnectorIntensity * 1.1f; }
            yield return new WaitForSeconds(0.10f);

            UpdateVisualState();
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

        public void UpdateVisualState()
        {
            bool isPowered = (state == SubstationState.Powered);

            // 0. Dynamic Heavy Power Nexus Substation Sprite (Unpowered vs Supercharged Powered)
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

            // 1. South Diagnostic Display Status Light (Subtle screen glow)
            if (consoleLight != null)
            {
                consoleLight.color = isPowered ? new Color(0.1f, 0.85f, 1f, 1f) : new Color(1f, 0.2f, 0.1f, 1f);
                consoleLight.intensity = isPowered ? baseConsoleIntensity : 0.15f;
                consoleLight.pointLightOuterRadius = 1.0f;
                consoleLight.pointLightInnerRadius = 0.2f;
                consoleLight.enabled = true;
            }

            // 2. Central Mechanically Enclosed Energy Core (Controlled cyan glow, not magical)
            if (coreLight != null)
            {
                coreLight.enabled = isPowered;
                if (isPowered)
                {
                    coreLight.color = new Color(0.0f, 0.85f, 1.0f, 1f);
                    coreLight.intensity = baseCoreIntensity;
                    coreLight.pointLightOuterRadius = 1.8f;
                    coreLight.pointLightInnerRadius = 0.2f;
                }
            }

            // 3. Subtle Localized Floor Reflection (Restrained local light spill around hub rim)
            if (chamberDeckLight != null)
            {
                chamberDeckLight.enabled = isPowered;
                if (isPowered)
                {
                    chamberDeckLight.color = new Color(0.0f, 0.65f, 0.95f, 1f);
                    chamberDeckLight.intensity = baseDeckIntensity;
                    chamberDeckLight.pointLightOuterRadius = 2.6f;
                    chamberDeckLight.pointLightInnerRadius = 0.8f;
                }
            }

            // 4. 4 Restrained Directional Connection Status Spotlights (Point outward into floor conduits, NO spill onto top deck)
            for (int i = 0; i < outwardBeams.Count; i++)
            {
                var b = outwardBeams[i];
                if (b != null)
                {
                    b.enabled = isPowered;
                    b.intensity = isPowered ? baseConnectorIntensity : 0f;
                    b.color = new Color(0.1f, 0.85f, 1.0f, 1f);
                    b.pointLightInnerRadius = 0.15f;
                    b.pointLightOuterRadius = 1.25f;
                    b.pointLightInnerAngle = 35f;
                    b.pointLightOuterAngle = 65f;
                }
            }

            // 5. Node A Environmental Lighting Network (Restores actual room illumination across junction corridors)
            foreach (var envLight in nodeAEnvironmentalLights)
            {
                if (envLight != null)
                {
                    envLight.enabled = isPowered;
                    if (isPowered)
                    {
                        envLight.intensity = baseEnvironmentalLightIntensity;
                    }
                }
            }

            // 6. Volumetric Corridor Dust Motes / Beam Particles
            if (beamParticles != null)
            {
                if (isPowered)
                {
                    if (!beamParticles.isPlaying) beamParticles.Play();
                }
                else
                {
                    if (beamParticles.isPlaying) beamParticles.Stop();
                }
            }
        }

        [ContextMenu("Preview: Power ON")]
        public void PreviewPowerOn()
        {
            state = SubstationState.Powered;
            UpdateVisualState();
        }

        [ContextMenu("Preview: Power OFF")]
        public void PreviewPowerOff()
        {
            state = SubstationState.Unpowered;
            UpdateVisualState();
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
