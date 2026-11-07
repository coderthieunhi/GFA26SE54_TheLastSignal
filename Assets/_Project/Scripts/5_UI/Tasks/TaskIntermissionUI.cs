using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using TheLastSignal.Entities.Player;
using TheLastSignal.Entities.Environment;
using TheLastSignal.Core;

namespace TheLastSignal.UI.Tasks
{
    /// <summary>
    /// Tactile Sci-Fi Brutalist Intermission Screen displayed between multi-stage minigame steps.
    /// Provides clear feedback on current station progress, operator role quotas (3 for Engineer, 5 for others),
    /// and allows players to choose between proceeding immediately [SPACE] or disengaging to look around and save progress [ESC].
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class TaskIntermissionUI : MonoBehaviour
    {
        private static TaskIntermissionUI _instance;
        public static TaskIntermissionUI Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = UnityEngine.Object.FindAnyObjectByType<TaskIntermissionUI>(FindObjectsInactive.Include);
                }
                return _instance;
            }
            private set => _instance = value;
        }

        [Header("UI Panels")]
        [SerializeField] private GameObject rootPanel;
        [SerializeField] private TextMeshProUGUI stationTitleText;
        [SerializeField] private TextMeshProUGUI operatorRoleText;
        [SerializeField] private TextMeshProUGUI progressSummaryText;
        [SerializeField] private TextMeshProUGUI nextSubTaskText;
        [SerializeField] private Image[] pipelineLeds = new Image[5];
        [SerializeField] private TextMeshProUGUI[] pipelineLedLabels = new TextMeshProUGUI[5];
        [SerializeField] private Button continueButton;
        [SerializeField] private Button exitButton;

        [Header("Audio")]
        [SerializeField] private AudioSource audioSource;

        private Action onContinueCallback;
        private Action onExitCallback;
        private PlayerController activePlayer;
        private TaskLocation activeStation;
        private bool isDisplaying = false;

        private AudioClip intermissionBeepClip;
        private AudioClip clickClip;

        // Visual Colors
        private readonly Color LedCompleted = new Color(0f, 1f, 0.50f, 1f); // Neon Green
        private readonly Color LedCurrent = new Color(0f, 0.90f, 1f, 1f);   // Neon Cyan
        private readonly Color LedPending = new Color(0.18f, 0.22f, 0.28f, 1f); // Slate Inactive

        public bool IsOpen => rootPanel != null && rootPanel.activeSelf;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;

            EnsureAudio();
            GenerateProceduralAudio();

            if (continueButton != null)
            {
                continueButton.onClick.RemoveAllListeners();
                continueButton.onClick.AddListener(TriggerContinue);
            }

            if (exitButton != null)
            {
                exitButton.onClick.RemoveAllListeners();
                exitButton.onClick.AddListener(TriggerExit);
            }

            if (rootPanel != null)
            {
                rootPanel.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }

        private void Update()
        {
            if (!IsOpen || !isDisplaying) return;

            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null)
            {
                // Space or E or Enter: Continue to next sub-task
                if (kb.spaceKey.wasPressedThisFrame || kb.eKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame)
                {
                    TriggerContinue();
                    return;
                }

                // Escape: Disengage and save progress
                if (kb.escapeKey.wasPressedThisFrame)
                {
                    TriggerExit();
                    return;
                }
            }
        }

        public void ShowIntermission(PlayerController player, TaskLocation taskLocation, int currentSteps, int targetSteps,
                                     string nextTaskName, Action onContinue, Action onExit)
        {
            activePlayer = player;
            activeStation = taskLocation;
            onContinueCallback = onContinue;
            onExitCallback = onExit;
            isDisplaying = true;

            if (activePlayer != null)
            {
                activePlayer.SetControlEnabled(false);
            }

            // Determine role name
            string roleName = "LOGISTICS";
            if (activePlayer != null && activePlayer.TryGetComponent(out PlayerRole r))
            {
                roleName = r.Role.ToString().ToUpper();
            }

            if (stationTitleText != null)
            {
                stationTitleText.text = $"STATION: <color=#00E5FF>{taskLocation.StationDisplayName.ToUpper()}</color>";
            }

            if (operatorRoleText != null)
            {
                operatorRoleText.text = $"OPERATOR ROLE: <color=#FFA026><b>{roleName}</b></color> (QUOTA: {targetSteps} SUB-TASKS)";
            }

            if (progressSummaryText != null)
            {
                int percent = Mathf.RoundToInt(((float)currentSteps / targetSteps) * 100f);
                progressSummaryText.text = $"PROGRESS: <color=#00FF7F><b>{currentSteps} / {targetSteps}</b></color> ({percent}%)";
            }

            if (nextSubTaskText != null)
            {
                nextSubTaskText.text = $"NEXT SUB-TASK: <color=#00E5FF><b>{nextTaskName}</b></color>";
            }

            // Update 5-stage pipeline indicator
            if (pipelineLeds != null)
            {
                for (int i = 0; i < pipelineLeds.Length; i++)
                {
                    if (pipelineLeds[i] == null) continue;

                    if (i < currentSteps)
                    {
                        pipelineLeds[i].color = LedCompleted;
                        if (pipelineLedLabels != null && i < pipelineLedLabels.Length && pipelineLedLabels[i] != null)
                            pipelineLedLabels[i].text = "DONE";
                    }
                    else if (i == currentSteps)
                    {
                        pipelineLeds[i].color = LedCurrent;
                        if (pipelineLedLabels != null && i < pipelineLedLabels.Length && pipelineLedLabels[i] != null)
                            pipelineLedLabels[i].text = "NEXT";
                    }
                    else
                    {
                        pipelineLeds[i].color = LedPending;
                        if (pipelineLedLabels != null && i < pipelineLedLabels.Length && pipelineLedLabels[i] != null)
                            pipelineLedLabels[i].text = $"[{i + 1}]";
                    }
                }
            }

            if (rootPanel != null)
            {
                rootPanel.SetActive(true);
            }

            PlaySfx(intermissionBeepClip);
        }

        public void TriggerContinue()
        {
            if (!isDisplaying) return;
            isDisplaying = false;

            PlaySfx(clickClip);

            if (rootPanel != null)
            {
                rootPanel.SetActive(false);
            }

            Action continueAction = onContinueCallback;
            onContinueCallback = null;
            onExitCallback = null;

            continueAction?.Invoke();
        }

        public void TriggerExit()
        {
            if (!isDisplaying) return;
            isDisplaying = false;

            PlaySfx(clickClip);

            if (rootPanel != null)
            {
                rootPanel.SetActive(false);
            }

            if (activePlayer != null)
            {
                activePlayer.SetControlEnabled(true);
            }

            Action exitAction = onExitCallback;
            onContinueCallback = null;
            onExitCallback = null;

            exitAction?.Invoke();
        }

        #region Audio & Hierarchy Builder

        private void EnsureAudio()
        {
            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
                if (audioSource == null)
                {
                    audioSource = gameObject.AddComponent<AudioSource>();
                }
            }
            if (audioSource != null)
            {
                audioSource.playOnAwake = false;
            }
        }

        private void GenerateProceduralAudio()
        {
            if (intermissionBeepClip == null)
            {
                intermissionBeepClip = CreateChordTone();
            }
            if (clickClip == null)
            {
                clickClip = CreateTickTone();
            }
        }

        private void PlaySfx(AudioClip clip)
        {
            if (audioSource != null && clip != null)
            {
                audioSource.PlayOneShot(clip, 0.75f);
            }
        }

        private static AudioClip CreateChordTone()
        {
            int sampleRate = 44100;
            float duration = 0.45f;
            int samples = (int)(sampleRate * duration);
            float[] data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Clamp01((duration - t) / duration);
                data[i] = (Mathf.Sin(2f * Mathf.PI * 523.25f * t) * 0.35f +
                           Mathf.Sin(2f * Mathf.PI * 659.25f * t) * 0.35f +
                           Mathf.Sin(2f * Mathf.PI * 783.99f * t) * 0.35f) * env;
            }
            AudioClip clip = AudioClip.Create("IntermissionChord", samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip CreateTickTone()
        {
            int sampleRate = 44100;
            float duration = 0.04f;
            int samples = (int)(sampleRate * duration);
            float[] data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / sampleRate;
                float env = 1f - (t / duration);
                data[i] = Mathf.Sin(2f * Mathf.PI * 1200f * t) * 0.35f * env;
            }
            AudioClip clip = AudioClip.Create("IntermissionTick", samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        public static TaskIntermissionUI GetOrCreateInstance()
        {
            if (Instance != null) return Instance;

            Canvas canvas = UnityEngine.Object.FindAnyObjectByType<Canvas>();
            if (canvas == null)
            {
                Debug.LogWarning("[TaskIntermissionUI] No Canvas found in scene.");
                return null;
            }

            _instance = CreateIntermissionUIHierarchy(canvas);
            return _instance;
        }

        public static TaskIntermissionUI CreateIntermissionUIHierarchy(Canvas canvas)
        {
            Sprite bezelSprite = Resources.Load<Sprite>("UI/Tasks/TerminalBezel");

            // Fullscreen dark overlay
            GameObject root = new GameObject("TaskIntermissionPanel", typeof(RectTransform), typeof(AudioSource));
            root.transform.SetParent(canvas.transform, false);
            RectTransform rootRect = root.GetComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;

            Image bgOverlay = root.AddComponent<Image>();
            bgOverlay.color = new Color(0.02f, 0.04f, 0.06f, 0.88f);

            // Center Chassis
            GameObject box = new GameObject("TerminalBox", typeof(RectTransform), typeof(Image));
            box.transform.SetParent(root.transform, false);
            RectTransform boxRect = box.GetComponent<RectTransform>();
            boxRect.sizeDelta = new Vector2(860, 520);
            boxRect.anchoredPosition = Vector2.zero;

            Image boxImg = box.GetComponent<Image>();
            if (bezelSprite != null)
            {
                boxImg.sprite = bezelSprite;
                boxImg.color = Color.white;
            }
            else
            {
                boxImg.color = new Color(0.10f, 0.13f, 0.17f, 1f);
                Outline outline = box.AddComponent<Outline>();
                outline.effectColor = new Color(0.2f, 0.35f, 0.5f, 1f);
                outline.effectDistance = new Vector2(3, -3);
            }

            // Header Title
            GameObject headerGo = new GameObject("HeaderTitle", typeof(RectTransform), typeof(TextMeshProUGUI));
            headerGo.transform.SetParent(box.transform, false);
            RectTransform headerRect = headerGo.GetComponent<RectTransform>();
            headerRect.anchoredPosition = new Vector2(0, 195);
            headerRect.sizeDelta = new Vector2(760, 36);
            TextMeshProUGUI headerTxt = headerGo.GetComponent<TextMeshProUGUI>();
            headerTxt.text = "STATION REPAIR PIPELINE // SUB-TASK COMPLETED";
            headerTxt.fontSize = 20;
            headerTxt.alignment = TextAlignmentOptions.Center;
            headerTxt.fontStyle = FontStyles.Bold;
            headerTxt.color = new Color(0.92f, 0.96f, 1f, 1f);

            // Subheader Tagline
            GameObject subHeadGo = new GameObject("SubHeader", typeof(RectTransform), typeof(TextMeshProUGUI));
            subHeadGo.transform.SetParent(box.transform, false);
            RectTransform shr = subHeadGo.GetComponent<RectTransform>();
            shr.anchoredPosition = new Vector2(0, 160);
            shr.sizeDelta = new Vector2(760, 24);
            TextMeshProUGUI sht = subHeadGo.GetComponent<TextMeshProUGUI>();
            sht.text = "SUB-SYSTEM LOGGED TO BUFFER — GRID INTEGRITY SYNCHRONIZING";
            sht.fontSize = 12;
            sht.alignment = TextAlignmentOptions.Center;
            sht.color = new Color(0.4f, 0.7f, 0.95f, 1f);

            // Central Status Housing
            GameObject centerHousing = new GameObject("StatusHousing", typeof(RectTransform), typeof(Image));
            centerHousing.transform.SetParent(box.transform, false);
            RectTransform chRect = centerHousing.GetComponent<RectTransform>();
            chRect.sizeDelta = new Vector2(760, 200);
            chRect.anchoredPosition = new Vector2(0, 30);
            Image chImg = centerHousing.GetComponent<Image>();
            chImg.color = new Color(0.04f, 0.07f, 0.10f, 1f);

            Outline cho = centerHousing.AddComponent<Outline>();
            cho.effectColor = new Color(0.18f, 0.28f, 0.38f, 1f);
            cho.effectDistance = new Vector2(2, -2);

            // Station Title Text
            GameObject stnGo = new GameObject("StationTitle", typeof(RectTransform), typeof(TextMeshProUGUI));
            stnGo.transform.SetParent(centerHousing.transform, false);
            RectTransform stnr = stnGo.GetComponent<RectTransform>();
            stnr.anchoredPosition = new Vector2(0, 68);
            stnr.sizeDelta = new Vector2(700, 26);
            TextMeshProUGUI stnt = stnGo.GetComponent<TextMeshProUGUI>();
            stnt.text = "STATION: <color=#00E5FF>GENERATOR A-01</color>";
            stnt.fontSize = 17;
            stnt.fontStyle = FontStyles.Bold;
            stnt.alignment = TextAlignmentOptions.Center;
            stnt.color = Color.white;

            // Operator Role Text
            GameObject roleGo = new GameObject("OperatorRole", typeof(RectTransform), typeof(TextMeshProUGUI));
            roleGo.transform.SetParent(centerHousing.transform, false);
            RectTransform roler = roleGo.GetComponent<RectTransform>();
            roler.anchoredPosition = new Vector2(0, 38);
            roler.sizeDelta = new Vector2(700, 22);
            TextMeshProUGUI rolet = roleGo.GetComponent<TextMeshProUGUI>();
            rolet.text = "OPERATOR: <color=#FFA026><b>LOGISTICS</b></color> (QUOTA: 5 SUB-TASKS)";
            rolet.fontSize = 13;
            rolet.alignment = TextAlignmentOptions.Center;
            rolet.color = new Color(0.85f, 0.90f, 0.95f, 1f);

            // 5-Stage Pipeline Indicator Box
            Image[] leds = new Image[5];
            TextMeshProUGUI[] ledLabels = new TextMeshProUGUI[5];
            float spacing = 120f;
            float startX = -240f;

            for (int i = 0; i < 5; i++)
            {
                GameObject ledGo = new GameObject($"PipelineLed_{i}", typeof(RectTransform), typeof(Image));
                ledGo.transform.SetParent(centerHousing.transform, false);
                RectTransform lr = ledGo.GetComponent<RectTransform>();
                lr.anchoredPosition = new Vector2(startX + i * spacing, -12);
                lr.sizeDelta = new Vector2(90, 34);
                Image li = ledGo.GetComponent<Image>();
                li.color = new Color(0.18f, 0.22f, 0.28f, 1f);

                Outline lOutline = ledGo.AddComponent<Outline>();
                lOutline.effectColor = new Color(0.25f, 0.35f, 0.45f, 1f);
                lOutline.effectDistance = new Vector2(1, -1);
                leds[i] = li;

                GameObject ltxtGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
                ltxtGo.transform.SetParent(ledGo.transform, false);
                RectTransform ltr = ltxtGo.GetComponent<RectTransform>();
                ltr.anchorMin = Vector2.zero;
                ltr.anchorMax = Vector2.one;
                ltr.offsetMin = Vector2.zero;
                ltr.offsetMax = Vector2.zero;
                TextMeshProUGUI ltt = ltxtGo.GetComponent<TextMeshProUGUI>();
                ltt.text = $"[{i + 1}]";
                ltt.fontSize = 12;
                ltt.fontStyle = FontStyles.Bold;
                ltt.alignment = TextAlignmentOptions.Center;
                ltt.color = Color.white;
                ledLabels[i] = ltt;
            }

            // Progress Summary Text
            GameObject progGo = new GameObject("ProgressSummary", typeof(RectTransform), typeof(TextMeshProUGUI));
            progGo.transform.SetParent(centerHousing.transform, false);
            RectTransform progr = progGo.GetComponent<RectTransform>();
            progr.anchoredPosition = new Vector2(0, -60);
            progr.sizeDelta = new Vector2(700, 22);
            TextMeshProUGUI progt = progGo.GetComponent<TextMeshProUGUI>();
            progt.text = "PROGRESS: <color=#00FF7F><b>1 / 5</b></color> (20%)";
            progt.fontSize = 14;
            progt.fontStyle = FontStyles.Bold;
            progt.alignment = TextAlignmentOptions.Center;
            progt.color = new Color(0.85f, 0.95f, 1f, 1f);

            // Next Subtask Hint
            GameObject nextGo = new GameObject("NextSubTask", typeof(RectTransform), typeof(TextMeshProUGUI));
            nextGo.transform.SetParent(box.transform, false);
            RectTransform nextr = nextGo.GetComponent<RectTransform>();
            nextr.anchoredPosition = new Vector2(0, -95);
            nextr.sizeDelta = new Vector2(760, 24);
            TextMeshProUGUI nextt = nextGo.GetComponent<TextMeshProUGUI>();
            nextt.text = "NEXT SUB-TASK: <color=#00E5FF><b>VALVE BALANCING</b></color>";
            nextt.fontSize = 13;
            nextt.alignment = TextAlignmentOptions.Center;
            nextt.color = new Color(0.70f, 0.85f, 0.95f, 1f);

            // Button 1: Continue [SPACE]
            GameObject contGo = new GameObject("ContinueButton", typeof(RectTransform), typeof(Image), typeof(Button));
            contGo.transform.SetParent(box.transform, false);
            RectTransform contRect = contGo.GetComponent<RectTransform>();
            contRect.anchoredPosition = new Vector2(180, -165);
            contRect.sizeDelta = new Vector2(340, 52);
            Image contImg = contGo.GetComponent<Image>();
            contImg.color = new Color(0.08f, 0.28f, 0.20f, 1f);

            Outline conto = contGo.AddComponent<Outline>();
            conto.effectColor = new Color(0f, 1f, 0.5f, 0.85f);
            conto.effectDistance = new Vector2(2, -2);
            Button contBtn = contGo.GetComponent<Button>();

            GameObject contTxtGo = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            contTxtGo.transform.SetParent(contGo.transform, false);
            RectTransform ctr = contTxtGo.GetComponent<RectTransform>();
            ctr.anchorMin = Vector2.zero;
            ctr.anchorMax = Vector2.one;
            ctr.offsetMin = Vector2.zero;
            ctr.offsetMax = Vector2.zero;
            TextMeshProUGUI ctt = contTxtGo.GetComponent<TextMeshProUGUI>();
            ctt.text = "[ CONTINUE // SPACE ]";
            ctt.fontSize = 15;
            ctt.fontStyle = FontStyles.Bold;
            ctt.alignment = TextAlignmentOptions.Center;
            ctt.color = Color.white;

            // Button 2: Exit & Save [ESC]
            GameObject exitGo = new GameObject("ExitButton", typeof(RectTransform), typeof(Image), typeof(Button));
            exitGo.transform.SetParent(box.transform, false);
            RectTransform exitRect = exitGo.GetComponent<RectTransform>();
            exitRect.anchoredPosition = new Vector2(-180, -165);
            exitRect.sizeDelta = new Vector2(340, 52);
            Image exitImg = exitGo.GetComponent<Image>();
            exitImg.color = new Color(0.18f, 0.12f, 0.12f, 1f);

            Outline exito = exitGo.AddComponent<Outline>();
            exito.effectColor = new Color(0.85f, 0.35f, 0.35f, 0.8f);
            exito.effectDistance = new Vector2(2, -2);
            Button exitBtn = exitGo.GetComponent<Button>();

            GameObject exitTxtGo = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            exitTxtGo.transform.SetParent(exitGo.transform, false);
            RectTransform etr = exitTxtGo.GetComponent<RectTransform>();
            etr.anchorMin = Vector2.zero;
            etr.anchorMax = Vector2.one;
            etr.offsetMin = Vector2.zero;
            etr.offsetMax = Vector2.zero;
            TextMeshProUGUI ett = exitTxtGo.GetComponent<TextMeshProUGUI>();
            ett.text = "[ DISENGAGE & SAVE // ESC ]";
            ett.fontSize = 15;
            ett.fontStyle = FontStyles.Bold;
            ett.alignment = TextAlignmentOptions.Center;
            ett.color = Color.white;

            TaskIntermissionUI ui = root.AddComponent<TaskIntermissionUI>();
            ui.rootPanel = root;
            ui.stationTitleText = stnt;
            ui.operatorRoleText = rolet;
            ui.progressSummaryText = progt;
            ui.nextSubTaskText = nextt;
            ui.pipelineLeds = leds;
            ui.pipelineLedLabels = ledLabels;
            ui.continueButton = contBtn;
            ui.exitButton = exitBtn;
            ui.audioSource = root.GetComponent<AudioSource>();

            return ui;
        }

        #endregion
    }
}
