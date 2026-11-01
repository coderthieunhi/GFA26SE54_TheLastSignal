using UnityEngine;

namespace TheLastSignal.Juice
{
    /// <summary>
    /// Introduces micro-glitches and scanline flickers on damaged terminal screens.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class TerminalScreenGlitch : MonoBehaviour
    {
        [Range(0f, 1f)]
        public float glitchChance = 0.08f;

        [Range(0.2f, 1f)]
        public float minBrightness = 0.45f;

        private SpriteRenderer _sr;
        private Color _baseColor;
        private Color _targetColor;

        private void Awake()
        {
            _sr = GetComponent<SpriteRenderer>();
            _baseColor = _sr.color;
            _targetColor = _baseColor;
        }

        private void Update()
        {
            if (_sr == null) return;

            if (Random.value < glitchChance)
            {
                float dim = Random.Range(minBrightness, 1.0f);
                _targetColor = new Color(_baseColor.r * dim, _baseColor.g * dim, _baseColor.b * dim, _baseColor.a);
            }
            else
            {
                _targetColor = _baseColor;
            }

            _sr.color = Color.Lerp(_sr.color, _targetColor, Time.deltaTime * 20f);
        }
    }
}
