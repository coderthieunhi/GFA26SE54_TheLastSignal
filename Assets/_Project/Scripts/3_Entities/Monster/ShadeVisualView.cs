using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using TheLastSignal.Entities.Player;

namespace TheLastSignal.Entities.Monster
{
    /// <summary>
    /// Visual and FX controller for The Shade (Xenomorph Chitin Apex Predator).
    /// Features:
    /// 1. Dynamic directional sprite swapping: Overrides body with authentic 8-directional
    ///    Xenomorph Chitin sprites (8-dir Idle and 8-frame Run cycles for all 8 directions).
    /// 2. Real-time predator crimson eye glow via 2D Point Light (Light2D) cutting through darkness.
    /// 3. Visceral 128x128 3-claw bloody laceration FX (FX_Shade_ClawSlash).
    /// 4. Dark matter shadow burst FX (FX_Shade_ShadowBurst) and afterimage trail on Shadow Dash.
    /// 5. Procedural spatial audio integration for slashes, flesh impacts, and shadow whooshes.
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    public class ShadeVisualView : MonoBehaviour
    {
        private PlayerController controller;
        private SpriteRenderer bodyRenderer;
        private AudioSource audioSource;

        private GameObject redEyesObject;
        private SpriteRenderer eyesRenderer;
        private Light2D predatorEyeLight;
        private Color originalBodyColor = Color.white;
        private bool isShadeActive = false;
        private float animTimer = 0f;

        // Cached Sprite Assets
        private readonly Dictionary<string, Sprite> idleSprites = new Dictionary<string, Sprite>();
        private readonly Dictionary<string, Sprite[]> runSprites = new Dictionary<string, Sprite[]>();
        private static Sprite cachedClawFx;
        private static Sprite cachedBurstFx;

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

            LoadShadeSprites();
            EnsureEyesHierarchy();
        }

        private void LoadShadeSprites()
        {
            // 1. Load 8-directional idle sprites
            Sprite[] idles = Resources.LoadAll<Sprite>("Monster/Shade_Idle_8Dir");
            if (idles != null)
            {
                for (int i = 0; i < idles.Length; i++)
                {
                    string name = idles[i].name;
                    if (name.Contains("SouthEast")) idleSprites["SouthEast"] = idles[i];
                    else if (name.Contains("SouthWest")) idleSprites["SouthWest"] = idles[i];
                    else if (name.Contains("South")) idleSprites["South"] = idles[i];
                    else if (name.Contains("NorthEast")) idleSprites["NorthEast"] = idles[i];
                    else if (name.Contains("NorthWest")) idleSprites["NorthWest"] = idles[i];
                    else if (name.Contains("North")) idleSprites["North"] = idles[i];
                    else if (name.Contains("East")) idleSprites["East"] = idles[i];
                    else if (name.Contains("West")) idleSprites["West"] = idles[i];
                }
            }

            // 2. Load 8-directional run animations (8 frames each)
            string[] directions = { "South", "SouthEast", "East", "NorthEast", "North", "NorthWest", "West", "SouthWest" };
            for (int i = 0; i < directions.Length; i++)
            {
                string dir = directions[i];
                Sprite[] frames = Resources.LoadAll<Sprite>($"Monster/Shade_Run_{dir}");
                if (frames != null && frames.Length > 0)
                {
                    // Sort frames numerically by suffix _0.._7
                    System.Array.Sort(frames, (a, b) => string.CompareOrdinal(a.name, b.name));
                    runSprites[dir] = frames;
                }
            }

            // 3. Load combat FX sprites
            if (cachedClawFx == null)
            {
                cachedClawFx = Resources.Load<Sprite>("Monster/FX_Shade_ClawSlash");
            }

            if (cachedBurstFx == null)
            {
                cachedBurstFx = Resources.Load<Sprite>("Monster/FX_Shade_ShadowBurst");
            }
        }

        private void EnsureEyesHierarchy()
        {
            Transform found = transform.Find("ShadeRedEyes");
            if (found != null)
            {
                redEyesObject = found.gameObject;
                eyesRenderer = redEyesObject.GetComponent<SpriteRenderer>();
                if (eyesRenderer != null) eyesRenderer.enabled = false;

                predatorEyeLight = redEyesObject.GetComponent<Light2D>();
                if (predatorEyeLight == null)
                {
                    predatorEyeLight = redEyesObject.AddComponent<Light2D>();
                    predatorEyeLight.lightType = Light2D.LightType.Point;
                    predatorEyeLight.color = new Color(0.20f, 0.88f, 0.95f, 1.0f);
                    predatorEyeLight.intensity = 0.80f;
                    predatorEyeLight.pointLightInnerRadius = 0.2f;
                    predatorEyeLight.pointLightOuterRadius = 1.8f;
                    predatorEyeLight.falloffIntensity = 0.75f;
                }
                else
                {
                    predatorEyeLight.color = new Color(0.20f, 0.88f, 0.95f, 1.0f);
                }
            }
            else
            {
                redEyesObject = new GameObject("ShadeRedEyes");
                redEyesObject.transform.SetParent(transform, false);
                redEyesObject.transform.localPosition = new Vector3(0f, 0.38f, -0.05f);

                eyesRenderer = redEyesObject.AddComponent<SpriteRenderer>();
                eyesRenderer.sortingLayerName = "Characters";
                eyesRenderer.sortingOrder = 14;
                eyesRenderer.enabled = false; // Native face pixels already feature detailed eyes

                // Add 2D Point Light for predator eye glow cutting through darkness (Cyan bioluminescence)
                predatorEyeLight = redEyesObject.AddComponent<Light2D>();
                predatorEyeLight.lightType = Light2D.LightType.Point;
                predatorEyeLight.color = new Color(0.20f, 0.88f, 0.95f, 1.0f);
                predatorEyeLight.intensity = 0.80f;
                predatorEyeLight.pointLightInnerRadius = 0.2f;
                predatorEyeLight.pointLightOuterRadius = 1.8f;
                predatorEyeLight.falloffIntensity = 0.75f;
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
                // Align predator eye position slightly with facing direction
                Vector2 facing = controller.FacingDirection;
                redEyesObject.transform.localPosition = new Vector3(facing.x * 0.14f, 0.38f + (facing.y * 0.08f), -0.05f);

                // Menacing pulsing predator eye glow
                float pulse = 0.80f + (0.20f * Mathf.Sin(Time.time * 7f));
                if (predatorEyeLight != null)
                {
                    float dirFactor = facing.y > 0.5f ? 0.45f : 0.80f;
                    predatorEyeLight.intensity = dirFactor * pulse;
                }
            }
        }

        private void LateUpdate()
        {
            // Only override sprite animation when playing as The Shade
            if (!isShadeActive || bodyRenderer == null || controller == null) return;

            string dirKey = ResolveDirectionKey(controller.FacingDirection);

            if (controller.IsMoving)
            {
                // Advance running animation
                animTimer += Time.deltaTime * 10f;
                int frameIndex = Mathf.FloorToInt(animTimer) % 8;

                if (runSprites.TryGetValue(dirKey, out Sprite[] frames) && frames != null && frames.Length > 0)
                {
                    int safeIndex = Mathf.Clamp(frameIndex, 0, frames.Length - 1);
                    bodyRenderer.sprite = frames[safeIndex];
                }
                else if (idleSprites.TryGetValue(dirKey, out Sprite idleSprite))
                {
                    bodyRenderer.sprite = idleSprite;
                }
            }
            else
            {
                // Idle breathing posture
                if (idleSprites.TryGetValue(dirKey, out Sprite idleSprite))
                {
                    bodyRenderer.sprite = idleSprite;
                }
            }

            // Render crisp, high-contrast obsidian carapace without darkening tint
            bodyRenderer.color = Color.white;
        }

        private string ResolveDirectionKey(Vector2 dir)
        {
            if (dir.sqrMagnitude < 0.001f) return "South";

            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            if (angle < 0) angle += 360f;

            if (angle >= 337.5f || angle < 22.5f) return "East";
            if (angle >= 22.5f && angle < 67.5f) return "NorthEast";
            if (angle >= 67.5f && angle < 112.5f) return "North";
            if (angle >= 112.5f && angle < 157.5f) return "NorthWest";
            if (angle >= 157.5f && angle < 202.5f) return "West";
            if (angle >= 202.5f && angle < 247.5f) return "SouthWest";
            if (angle >= 247.5f && angle < 292.5f) return "South";
            return "SouthEast"; // 292.5f to 337.5f
        }

        public void SetShadeAppearance(bool active)
        {
            isShadeActive = active;

            if (idleSprites.Count == 0)
            {
                LoadShadeSprites();
            }

            if (redEyesObject == null)
            {
                EnsureEyesHierarchy();
            }

            if (bodyRenderer == null)
            {
                bodyRenderer = GetComponentInChildren<SpriteRenderer>();
                if (bodyRenderer != null)
                {
                    originalBodyColor = bodyRenderer.color;
                }
            }

            if (bodyRenderer != null)
            {
                if (active)
                {
                    bodyRenderer.color = Color.white;
                    string dirKey = ResolveDirectionKey(controller != null ? controller.FacingDirection : Vector2.down);
                    if (idleSprites.TryGetValue(dirKey, out Sprite s) && s != null)
                    {
                        bodyRenderer.sprite = s;
                    }
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

            // Hide normal survivor flashlight cone if acting as The Shade
            Transform flashlight = transform.Find("FlashlightCone") ?? transform.Find("DroneFlashlight");
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
            // 1. Play cutting whoosh & flesh tearing impact sounds
            if (audioSource != null)
            {
                audioSource.PlayOneShot(ShadeAudioSynthesizer.GetClawSlashClip(), 0.90f);
                if (hitSurvivor)
                {
                    audioSource.PlayOneShot(ShadeAudioSynthesizer.GetFleshImpactClip(), 1.0f);
                }
            }

            // 2. Physical monster body lunge toward attack direction
            StartCoroutine(AnimateSlashLungeCoroutine(aimDirection));

            // 3. Spawn visual 128x128 claw slash sprite
            GameObject slashGo = new GameObject("FX_ClawSlash");
            slashGo.transform.position = position;

            float angle = Mathf.Atan2(aimDirection.y, aimDirection.x) * Mathf.Rad2Deg;
            slashGo.transform.rotation = Quaternion.Euler(0f, 0f, angle);

            SpriteRenderer sr = slashGo.AddComponent<SpriteRenderer>();
            sr.sprite = cachedClawFx != null ? cachedClawFx : GetClawSlashFallbackSprite();
            sr.sortingLayerName = "Characters";
            sr.sortingOrder = 16;
            sr.color = hitSurvivor ? new Color(1.0f, 0.10f, 0.15f, 1.0f) : new Color(0.95f, 0.20f, 0.30f, 0.95f);

            StartCoroutine(AnimateSlashCoroutine(slashGo, sr));
        }

        private IEnumerator AnimateSlashLungeCoroutine(Vector2 aimDirection)
        {
            if (bodyRenderer == null) yield break;

            Vector3 originalLocalPos = bodyRenderer.transform.localPosition;
            Vector3 lungeOffset = (Vector3)(aimDirection.normalized * 0.16f);

            // Turn face to aimDirection
            string dirKey = ResolveDirectionKey(aimDirection);
            if (idleSprites.TryGetValue(dirKey, out Sprite s) && s != null)
            {
                bodyRenderer.sprite = s;
            }

            // Quick snappy lunge forward (0.05s) and return (0.10s)
            float lungeTime = 0.05f;
            float returnTime = 0.10f;
            float elapsed = 0f;

            while (elapsed < lungeTime)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / lungeTime;
                bodyRenderer.transform.localPosition = Vector3.Lerp(originalLocalPos, originalLocalPos + lungeOffset, t);
                yield return null;
            }

            elapsed = 0f;
            while (elapsed < returnTime)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / returnTime;
                bodyRenderer.transform.localPosition = Vector3.Lerp(originalLocalPos + lungeOffset, originalLocalPos, t);
                yield return null;
            }

            bodyRenderer.transform.localPosition = originalLocalPos;
        }

        private IEnumerator AnimateSlashCoroutine(GameObject slashGo, SpriteRenderer sr)
        {
            float duration = 0.22f;
            float elapsed = 0f;
            Vector3 startScale = new Vector3(0.6f, 0.6f, 1f);
            Vector3 targetScale = new Vector3(1.35f, 1.35f, 1f);

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
        /// Spawns dark matter bursts and shadow ghost trail afterimages during a shadow dash.
        /// </summary>
        public void PlayDashEffect(Vector2 startPos, Vector2 endPos)
        {
            if (audioSource != null)
            {
                audioSource.PlayOneShot(ShadeAudioSynthesizer.GetShadowDashClip(), 1.0f);
            }

            // Spawn launch and landing dark matter bursts
            SpawnBurstFx(startPos);
            SpawnBurstFx(endPos);

            StartCoroutine(SpawnShadowTrailCoroutine(startPos, endPos));
        }

        private void SpawnBurstFx(Vector2 pos)
        {
            GameObject burstGo = new GameObject("FX_ShadowBurst");
            burstGo.transform.position = pos;
            burstGo.transform.localScale = new Vector3(1.1f, 1.1f, 1f);

            SpriteRenderer sr = burstGo.AddComponent<SpriteRenderer>();
            sr.sprite = cachedBurstFx;
            sr.sortingLayerName = "Characters";
            sr.sortingOrder = 8;
            sr.color = new Color(0.85f, 0.15f, 0.25f, 0.85f);

            StartCoroutine(FadeBurstCoroutine(burstGo, sr));
        }

        private IEnumerator FadeBurstCoroutine(GameObject go, SpriteRenderer sr)
        {
            float duration = 0.28f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                Color c = sr.color;
                c.a = Mathf.Lerp(0.85f, 0f, t);
                sr.color = c;
                go.transform.localScale = Vector3.Lerp(Vector3.one * 0.8f, Vector3.one * 1.5f, t);
                yield return null;
            }
            Destroy(go);
        }

        private IEnumerator SpawnShadowTrailCoroutine(Vector2 startPos, Vector2 endPos)
        {
            int steps = 6;
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
                sr.color = new Color(0.12f, 0.08f, 0.18f, 0.70f);

                StartCoroutine(FadeGhostCoroutine(ghost, sr));
                yield return new WaitForSeconds(0.028f);
            }
        }

        private IEnumerator FadeGhostCoroutine(GameObject ghost, SpriteRenderer sr)
        {
            float duration = 0.32f;
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

        private static Sprite GetClawSlashFallbackSprite()
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

            float[] radii = new float[] { 22f, 26f, 30f };
            foreach (float r in radii)
            {
                for (float a = -0.7f; a <= 0.7f; a += 0.02f)
                {
                    int px = (int)(center.x + Mathf.Cos(a) * r);
                    int py = (int)(center.y + Mathf.Sin(a) * (r * 0.85f));
                    if (px >= 0 && px < size && py >= 0 && py < size)
                    {
                        tex.SetPixel(px, py, brightRed);
                        if (py + 1 < size) tex.SetPixel(px, py + 1, darkRed);
                    }
                }
            }

            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.2f, 0.5f), 32f);
        }
    }
}
