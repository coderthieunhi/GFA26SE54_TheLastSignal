using UnityEngine;

namespace TheLastSignal.Environment.VisualPass
{
    /// <summary>
    /// Emits gentle, low-lying cryogenic vapor/mist drifting across the floor deck
    /// from ruptured sub-zero pressure cylinders.
    /// </summary>
    public class VP_CryoVaporEmitter : MonoBehaviour
    {
        [Header("Emission Settings")]
        [SerializeField] private float emitInterval = 0.35f;
        [SerializeField] private float particleLife = 2.4f;
        [SerializeField] private Vector2 driftVelocity = new Vector2(-0.25f, -0.08f);
        [SerializeField] private float startScale = 0.3f;
        [SerializeField] private float endScale = 0.95f;
        [SerializeField] private Color vaporColor = new Color(0.82f, 0.94f, 1.0f, 0.22f);

        private float _emitTimer = 0f;
        private static Sprite _cachedVaporSprite;

        private void Awake()
        {
            if (_cachedVaporSprite == null)
            {
                CreateCachedVaporSprite();
            }
        }

        private static void CreateCachedVaporSprite()
        {
            int size = 32;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color[] colors = new Color[size * size];
            Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
            float maxDist = size * 0.48f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), center);
                    float norm = Mathf.Clamp01(dist / maxDist);
                    // Smooth quadratic falloff
                    float alpha = Mathf.Pow(Mathf.Cos(norm * Mathf.PI * 0.5f), 2.0f);
                    colors[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            tex.SetPixels(colors);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.Apply();

            _cachedVaporSprite = Sprite.Create(
                tex,
                new Rect(0, 0, size, size),
                new Vector2(0.5f, 0.5f),
                32f
            );
        }

        private void Update()
        {
            _emitTimer += Time.deltaTime;
            if (_emitTimer >= emitInterval)
            {
                _emitTimer = 0f;
                SpawnParticle();
            }
        }

        private void SpawnParticle()
        {
            if (_cachedVaporSprite == null) return;

            GameObject p = new GameObject("CryoVapor_Particle");
            Vector3 offset = new Vector3(
                Random.Range(-0.15f, 0.15f),
                Random.Range(-0.1f, 0.1f),
                0f
            );
            p.transform.position = transform.position + offset;
            p.transform.localScale = Vector3.one * startScale;

            SpriteRenderer sr = p.AddComponent<SpriteRenderer>();
            sr.sprite = _cachedVaporSprite;
            sr.color = vaporColor;
            sr.sortingLayerName = "Obstacles";
            sr.sortingOrder = 3; // Renders just above floor/decals, beneath standing characters

            var mover = p.AddComponent<CryoVaporMover>();
            mover.Initialize(driftVelocity, particleLife, startScale, endScale, vaporColor);
        }

        private class CryoVaporMover : MonoBehaviour
        {
            private Vector2 _vel;
            private float _maxLife;
            private float _life;
            private float _sScale;
            private float _eScale;
            private Color _baseColor;
            private SpriteRenderer _sr;

            public void Initialize(Vector2 vel, float maxLife, float sScale, float eScale, Color baseColor)
            {
                // Add slight random variation
                _vel = vel + new Vector2(Random.Range(-0.06f, 0.06f), Random.Range(-0.04f, 0.04f));
                _maxLife = maxLife + Random.Range(-0.3f, 0.3f);
                _life = _maxLife;
                _sScale = sScale;
                _eScale = eScale;
                _baseColor = baseColor;
                _sr = GetComponent<SpriteRenderer>();
            }

            private void Update()
            {
                _life -= Time.deltaTime;
                if (_life <= 0f)
                {
                    Destroy(gameObject);
                    return;
                }

                float t = 1f - (_life / _maxLife);
                transform.position += (Vector3)_vel * Time.deltaTime;

                float curScale = Mathf.Lerp(_sScale, _eScale, Mathf.Sqrt(t));
                transform.localScale = Vector3.one * curScale;

                // Bell-curve fade: fade in rapidly, linger, then gently fade out
                float alphaMultiplier;
                if (t < 0.2f)
                {
                    alphaMultiplier = t / 0.2f;
                }
                else
                {
                    alphaMultiplier = (1f - t) / 0.8f;
                }

                Color c = _baseColor;
                c.a = _baseColor.a * alphaMultiplier;
                if (_sr != null) _sr.color = c;
            }
        }
    }
}
