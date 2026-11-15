using UnityEngine;
using UnityEngine.UI;

namespace TheLastSignal.UI.Tasks
{
    /// <summary>
    /// Custom UGUI graphic for rendering high-precision, real-time oscilloscope waveforms.
    /// Draws both the Target Signal (Amber with transmission noise) and Player Tuner Signal (Neon Cyan -> Green on Lock)
    /// as continuous, smooth quad-strip meshes using VertexHelper.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public class WaveformGraphic : MaskableGraphic
    {
        [Header("Wave Parameters")]
        [SerializeField, Range(0.5f, 6.0f)] private float targetFrequency = 2.5f;
        [SerializeField, Range(0.1f, 1.0f)] private float targetAmplitude = 0.6f;
        [SerializeField, Range(0.5f, 6.0f)] private float playerFrequency = 1.0f;
        [SerializeField, Range(0.1f, 1.0f)] private float playerAmplitude = 0.3f;

        [Header("Appearance")]
        [SerializeField] private float lineThickness = 3.5f;
        [SerializeField] private int resolution = 140;
        [SerializeField] private float waveSpeed = 2.5f;
        [SerializeField] private float noiseIntensity = 0.05f;

        [Header("Colors")]
        [SerializeField] private Color targetColor = new Color(1f, 0.62f, 0.15f, 0.85f);   // Amber / Orange
        [SerializeField] private Color playerColor = new Color(0f, 0.90f, 1f, 1f);          // Neon Cyan
        [SerializeField] private Color lockedColor = new Color(0f, 1f, 0.50f, 1f);          // Neon Green

        private float phase = 0f;
        private bool isLocked = false;
        private float matchFactor = 0f; // 0 = completely out of tune, 1 = perfectly locked

        public float TargetFrequency
        {
            get => targetFrequency;
            set { targetFrequency = value; SetVerticesDirty(); }
        }

        public float TargetAmplitude
        {
            get => targetAmplitude;
            set { targetAmplitude = value; SetVerticesDirty(); }
        }

        public float PlayerFrequency
        {
            get => playerFrequency;
            set { playerFrequency = value; SetVerticesDirty(); }
        }

        public float PlayerAmplitude
        {
            get => playerAmplitude;
            set { playerAmplitude = value; SetVerticesDirty(); }
        }

        public bool IsLocked
        {
            get => isLocked;
            set { isLocked = value; SetVerticesDirty(); }
        }

        public float MatchFactor
        {
            get => matchFactor;
            set { matchFactor = Mathf.Clamp01(value); SetVerticesDirty(); }
        }

        protected override void Awake()
        {
            base.Awake();
            useLegacyMeshGeneration = false;
        }

        private void Update()
        {
            if (!gameObject.activeInHierarchy) return;

            // Animate travelling sine wave motion
            phase += Time.deltaTime * waveSpeed;
            if (phase > Mathf.PI * 200f) phase -= Mathf.PI * 200f;

            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();

            Rect rect = rectTransform.rect;
            float width = rect.width;
            float height = rect.height;

            if (width <= 0 || height <= 0 || resolution < 2) return;

            float maxWaveHeight = (height * 0.5f) - lineThickness;

            // 1. Draw Target Wave (Amber with transmission jitter)
            Color currentTargetColor = targetColor;
            if (isLocked)
            {
                currentTargetColor = Color.Lerp(targetColor, lockedColor, 0.5f);
            }
            DrawSineWave(vh, width, maxWaveHeight, targetFrequency, targetAmplitude, phase * 0.8f, currentTargetColor, true);

            // 2. Draw Player Wave (Neon Cyan -> Neon Green as match increases)
            Color currentPlayerColor = Color.Lerp(playerColor, lockedColor, matchFactor);
            if (isLocked)
            {
                currentPlayerColor = lockedColor;
            }
            DrawSineWave(vh, width, maxWaveHeight, playerFrequency, playerAmplitude, phase, currentPlayerColor, false);
        }

        private void DrawSineWave(VertexHelper vh, float width, float maxAmplitude, float frequency, float amplitude,
                                  float wavePhase, Color waveColor, bool applyNoise)
        {
            int ptsCount = resolution;
            Vector2[] points = new Vector2[ptsCount];
            float startX = -width * 0.5f;
            float stepX = width / (ptsCount - 1);

            for (int i = 0; i < ptsCount; i++)
            {
                float x = startX + i * stepX;
                float normX = (float)i / (ptsCount - 1); // 0 to 1

                // Sine wave formula: y = A * sin(2 * pi * f * normX + phase)
                float angle = normX * frequency * Mathf.PI * 2f + wavePhase;
                float y = Mathf.Sin(angle) * (amplitude * maxAmplitude);

                if (applyNoise && !isLocked)
                {
                    // High-frequency pseudo-random static jitter along transmission
                    float noise = Mathf.Sin(normX * 47.3f + wavePhase * 3.1f) * (noiseIntensity * maxAmplitude);
                    y += noise;
                }

                points[i] = new Vector2(x, y);
            }

            // Build connected strip of quads with lineThickness
            float halfThick = lineThickness * 0.5f;
            int startIndex = vh.currentVertCount;

            for (int i = 0; i < ptsCount; i++)
            {
                Vector2 normal;
                if (i == 0)
                {
                    Vector2 dir = (points[1] - points[0]).normalized;
                    normal = new Vector2(-dir.y, dir.x);
                }
                else if (i == ptsCount - 1)
                {
                    Vector2 dir = (points[ptsCount - 1] - points[ptsCount - 2]).normalized;
                    normal = new Vector2(-dir.y, dir.x);
                }
                else
                {
                    Vector2 dir1 = (points[i] - points[i - 1]).normalized;
                    Vector2 dir2 = (points[i + 1] - points[i]).normalized;
                    Vector2 avgDir = (dir1 + dir2).normalized;
                    normal = new Vector2(-avgDir.y, avgDir.x);
                }

                Vector2 top = points[i] + normal * halfThick;
                Vector2 bottom = points[i] - normal * halfThick;

                vh.AddVert(new Vector3(top.x, top.y, 0f), waveColor, Vector2.zero);
                vh.AddVert(new Vector3(bottom.x, bottom.y, 0f), waveColor, Vector2.zero);
            }

            for (int i = 0; i < ptsCount - 1; i++)
            {
                int baseIdx = startIndex + i * 2;
                // Quad: (baseIdx, baseIdx + 1, baseIdx + 3, baseIdx + 2)
                vh.AddTriangle(baseIdx, baseIdx + 2, baseIdx + 1);
                vh.AddTriangle(baseIdx + 1, baseIdx + 2, baseIdx + 3);
            }
        }
    }
}
