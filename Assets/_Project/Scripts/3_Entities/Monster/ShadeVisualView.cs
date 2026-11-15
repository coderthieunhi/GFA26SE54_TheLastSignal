using System.Collections;
using UnityEngine;
using TheLastSignal.Entities.Player;

namespace TheLastSignal.Entities.Monster
{
    /// <summary>
    /// Visual and FX controller for The Shade.
    /// Handles:
    /// - Predator shadow silhouette tinting and glowing red predator eyes.
    /// - 2D curved bloody/shadow claw swipe visualization.
    /// - Shadow dash afterimages and dark void particle dissipation.
    /// - Synchronized 3D audio playback for slashes, flesh impacts, and dashes.
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    public class ShadeVisualView : MonoBehaviour
    {
        private PlayerController controller;
        private SpriteRenderer bodyRenderer;
        private AudioSource audioSource;

        private GameObject redEyesObject;
        private SpriteRenderer eyesRenderer;
        private Color originalBodyColor = Color.white;
        private bool isShadeActive = false;

        private static Sprite cachedClawSprite;
        private static Sprite cachedEyeSprite;

        private void Awake()
        {
            controller = GetComponent<PlayerController>();
            bodyRenderer = GetComponentInChildren<SpriteRenderer>();
            if (bodyRenderer != null)
            {
                originalBodyColor = bodyRenderer.color;
            }

            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.spatialBlend = 0.5f;
                audioSource.playOnAwake = false;
            }

            EnsureEyesHierarchy();
        }

        private void EnsureEyesHierarchy()
        {
            Transform found = transform.Find("ShadeRedEyes");
            if (found != null)
            {
                redEyesObject = found.gameObject;
                eyesRenderer = redEyesObject.GetComponent<SpriteRenderer>();
            }
            else
            {
                redEyesObject = new GameObject("ShadeRedEyes");
                redEyesObject.transform.SetParent(transform, false);
                redEyesObject.transform.localPosition = new Vector3(0f, 0.42f, -0.05f); // Positioned at head level

                eyesRenderer = redEyesObject.AddComponent<SpriteRenderer>();
                eyesRenderer.sortingLayerName = "Characters";
                eyesRenderer.sortingOrder = 12;
                eyesRenderer.sprite = GetGlowingEyesSprite();
            }

            redEyesObject.SetActive(false);
        }

        private void Update()
        {
            if (controller == null) return;

            bool shouldBeShade = controller.IsMonster;
            if (shouldBeShade != isShadeActive)
            {
                SetShadeAppearance(shouldBeShade);
            }

            if (isShadeActive && redEyesObject != null && redEyesObject.activeSelf)
            {
                // Align eyes slightly based on facing direction
                Vector2 facing = controller.FacingDirection;
                redEyesObject.transform.localPosition = new Vector3(facing.x * 0.12f, 0.42f + facing.y * 0.06f, -0.05f);

                // Subtle sinister eye pulse
                float pulse = 0.85f + 0.15f * Mathf.Sin(Time.time * 6f);
                eyesRenderer.color = new Color(1.0f, 0.15f, 0.20f, pulse);
            }
        }

        public void SetShadeAppearance(bool active)
        {
            isShadeActive = active;

            if (bodyRenderer != null)
            {
                if (active)
                {
                    // Dark shadowy predator silhouette with subtle crimson edge tint
                    bodyRenderer.color = new Color(0.12f, 0.14f, 0.18f, 0.95f);
                }
                else
                {
                    bodyRenderer.color = originalBodyColor;
                }
            }

            if (redEyesObject != null)
            {
                redEyesObject.SetActive(active);
            }

            // Hide normal flashlight cone if acting as The Shade
            Transform flashlight = transform.Find("FlashlightCone");
            if (flashlight != null)
            {
                flashlight.gameObject.SetActive(!active);
            }
        }

        /// <summary>
        /// Instantiates a dynamic 2D claw slash swipe in the attack direction.
        /// </summary>
        public void PlaySlashEffect(Vector2 position, Vector2 aimDirection, bool hitSurvivor)
        {
            // 1. Play cutting whoosh sound
            if (audioSource != null)
            {
                audioSource.PlayOneShot(ShadeAudioSynthesizer.GetClawSlashClip(), 0.85f);
                if (hitSurvivor)
                {
                    audioSource.PlayOneShot(ShadeAudioSynthesizer.GetFleshImpactClip(), 1.0f);
                }
            }

            // 2. Spawn visual claw slash sprite
            GameObject slashGo = new GameObject("FX_ClawSlash");
            slashGo.transform.position = position;

            float angle = Mathf.Atan2(aimDirection.y, aimDirection.x) * Mathf.Rad2Deg;
            slashGo.transform.rotation = Quaternion.Euler(0f, 0f, angle);

            SpriteRenderer sr = slashGo.AddComponent<SpriteRenderer>();
            sr.sprite = GetClawSlashSprite();
            sr.sortingLayerName = "Characters";
            sr.sortingOrder = 16;
            sr.color = hitSurvivor ? new Color(1.0f, 0.10f, 0.15f, 1.0f) : new Color(0.85f, 0.20f, 0.25f, 0.9f);

            StartCoroutine(AnimateSlashCoroutine(slashGo, sr));
        }

        private IEnumerator AnimateSlashCoroutine(GameObject slashGo, SpriteRenderer sr)
        {
            float duration = 0.24f;
            float elapsed = 0f;
            Vector3 startScale = new Vector3(0.5f, 0.5f, 1f);
            Vector3 targetScale = new Vector3(1.3f, 1.3f, 1f);

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;

                slashGo.transform.localScale = Vector3.Lerp(startScale, targetScale, Mathf.Sin(t * Mathf.PI * 0.5f));
                Color c = sr.color;
                c.a = Mathf.Clamp01(1.0f - (t * t));
                sr.color = c;

                yield return null;
            }

            Destroy(slashGo);
        }

        /// <summary>
        /// Spawns shadow ghost trail afterimages during a shadow dash.
        /// </summary>
        public void PlayDashEffect(Vector2 startPos, Vector2 endPos)
        {
            if (audioSource != null)
            {
                audioSource.PlayOneShot(ShadeAudioSynthesizer.GetShadowDashClip(), 0.95f);
            }

            StartCoroutine(SpawnShadowTrailCoroutine(startPos, endPos));
        }

        private IEnumerator SpawnShadowTrailCoroutine(Vector2 startPos, Vector2 endPos)
        {
            int steps = 5;
            for (int i = 0; i <= steps; i++)
            {
                float frac = (float)i / steps;
                Vector2 ghostPos = Vector2.Lerp(startPos, endPos, frac);

                GameObject ghost = new GameObject("FX_ShadowAfterimage");
                ghost.transform.position = ghostPos;
                ghost.transform.localScale = transform.localScale;

                SpriteRenderer sr = ghost.AddComponent<SpriteRenderer>();
                if (bodyRenderer != null)
                {
                    sr.sprite = bodyRenderer.sprite;
                }
                sr.sortingLayerName = "Characters";
                sr.sortingOrder = 9;
                sr.color = new Color(0.08f, 0.05f, 0.12f, 0.65f);

                StartCoroutine(FadeGhostCoroutine(ghost, sr));
                yield return new WaitForSeconds(0.035f);
            }
        }

        private IEnumerator FadeGhostCoroutine(GameObject ghost, SpriteRenderer sr)
        {
            float duration = 0.35f;
            float elapsed = 0f;
            Color startColor = sr.color;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                sr.color = new Color(startColor.r, startColor.g, startColor.b, Mathf.Lerp(startColor.a, 0f, t));
                yield return null;
            }

            Destroy(ghost);
        }

        // =====================================================================
        // PROCEDURAL SPRITE GENERATORS
        // =====================================================================

        private static Sprite GetGlowingEyesSprite()
        {
            if (cachedEyeSprite == null)
            {
                int w = 12, h = 6;
                Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
                tex.filterMode = FilterMode.Point;
                Color clear = new Color(0, 0, 0, 0);

                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        tex.SetPixel(x, y, clear);
                    }
                }

                Color redCore = new Color(1.0f, 0.9f, 0.9f, 1.0f);
                Color redGlow = new Color(1.0f, 0.15f, 0.2f, 1.0f);

                // Left Eye (x: 2..4, y: 2..4)
                tex.SetPixel(2, 3, redGlow);
                tex.SetPixel(3, 3, redCore);
                tex.SetPixel(4, 3, redGlow);
                tex.SetPixel(3, 2, redGlow);
                tex.SetPixel(3, 4, redGlow);

                // Right Eye (x: 7..9, y: 2..4)
                tex.SetPixel(7, 3, redGlow);
                tex.SetPixel(8, 3, redCore);
                tex.SetPixel(9, 3, redGlow);
                tex.SetPixel(8, 2, redGlow);
                tex.SetPixel(8, 4, redGlow);

                tex.Apply();
                cachedEyeSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 32f);
            }
            return cachedEyeSprite;
        }

        private static Sprite GetClawSlashSprite()
        {
            if (cachedClawSprite == null)
            {
                int size = 48;
                Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
                tex.filterMode = FilterMode.Point;
                Color clear = new Color(0, 0, 0, 0);

                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        tex.SetPixel(x, y, clear);
                    }
                }

                Vector2 center = new Vector2(10, 24);
                Color brightRed = new Color(1.0f, 0.2f, 0.25f, 1.0f);
                Color darkRed = new Color(0.6f, 0.05f, 0.1f, 0.85f);
                Color whiteCore = new Color(1.0f, 0.9f, 0.95f, 1.0f);

                // 3 curved slash arcs simulating 3 lethal razor claws
                float[] radii = new float[] { 22f, 26f, 30f };
                foreach (float r in radii)
                {
                    for (float a = -0.7f; a <= 0.7f; a += 0.02f)
                    {
                        int px = (int)(center.x + Mathf.Cos(a) * r);
                        int py = (int)(center.y + Mathf.Sin(a) * (r * 0.85f));
                        if (px >= 0 && px < size && py >= 0 && py < size)
                        {
                            tex.SetPixel(px, py, (r == 26f && Mathf.Abs(a) < 0.3f) ? whiteCore : brightRed);
                            if (py + 1 < size) tex.SetPixel(px, py + 1, darkRed);
                            if (py - 1 >= 0) tex.SetPixel(px, py - 1, darkRed);
                        }
                    }
                }

                tex.Apply();
                cachedClawSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.2f, 0.5f), 32f);
            }
            return cachedClawSprite;
        }
    }
}
