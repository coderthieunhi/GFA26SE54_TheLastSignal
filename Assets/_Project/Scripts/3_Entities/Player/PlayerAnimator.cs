using UnityEngine;

namespace TheLastSignal.Entities.Player
{
    /// <summary>
    /// Drives the player's visual appearance:
    /// 1. Feeds IsMoving/MoveX/MoveY into the Animator for blend trees.
    /// 2. Dynamically swaps the SpriteRenderer's sprite between 8 directional idle frames
    ///    and plays 8 authentic dedicated running animations (8 frames each) for all 8 directions.
    /// 3. Guarantees Unlit material and correct color at runtime to prevent black silhouettes.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class PlayerAnimator : MonoBehaviour
    {
        private static readonly int IsMovingParam = Animator.StringToHash("IsMoving");
        private static readonly int MoveXParam = Animator.StringToHash("MoveX");
        private static readonly int MoveYParam = Animator.StringToHash("MoveY");

        [SerializeField] private PlayerController playerController;
        [SerializeField] private SpriteRenderer spriteRenderer;

        [Header("8-Directional Idle Sprites")]
        [SerializeField] private Sprite idleSouth;
        [SerializeField] private Sprite idleSouthEast;
        [SerializeField] private Sprite idleEast;
        [SerializeField] private Sprite idleNorthEast;
        [SerializeField] private Sprite idleNorth;
        [SerializeField] private Sprite idleNorthWest;
        [SerializeField] private Sprite idleWest;
        [SerializeField] private Sprite idleSouthWest;

        [Header("8-Directional Run Animations (8 frames each)")]
        [SerializeField] private float animationFps = 10f;
        [SerializeField] private Sprite[] runEast = new Sprite[8];
        [SerializeField] private Sprite[] runNorthEast = new Sprite[8];
        [SerializeField] private Sprite[] runNorth = new Sprite[8];
        [SerializeField] private Sprite[] runNorthWest = new Sprite[8];
        [SerializeField] private Sprite[] runWest = new Sprite[8];
        [SerializeField] private Sprite[] runSouthWest = new Sprite[8];
        [SerializeField] private Sprite[] runSouth = new Sprite[8];
        [SerializeField] private Sprite[] runSouthEast = new Sprite[8];

        [Header("Idle Breathing Animation")]
        [SerializeField] private bool enableIdleBreathing = false;
        [SerializeField] private float breatheFrequency = 2.5f;
        [SerializeField] private float breatheAmplitude = 0.025f;

        private Animator animator;
        private float animTimer = 0f;
        private Vector3 initialScale;

        public Sprite IdleSouth => idleSouth;
        public Sprite IdleSouthEast => idleSouthEast;
        public Sprite IdleEast => idleEast;
        public Sprite IdleNorthEast => idleNorthEast;
        public Sprite IdleNorth => idleNorth;
        public Sprite IdleNorthWest => idleNorthWest;
        public Sprite IdleWest => idleWest;
        public Sprite IdleSouthWest => idleSouthWest;

        private void Awake()
        {
            animator = GetComponent<Animator>();
            if (playerController == null)
            {
                playerController = GetComponent<PlayerController>();
            }

            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponent<SpriteRenderer>();
            }

            initialScale = transform.localScale;

            // Cleanup any EquippedTool child if present
            Transform toolChild = transform.Find("EquippedTool");
            if (toolChild != null)
            {
                Destroy(toolChild.gameObject);
            }

            // Fallback 1: Load idle sprites from Resources if not assigned
            if (idleSouth == null)
            {
                LoadIdleSpritesFromResources();
            }

            // Fallback 2: Load all 8 run animations from Resources if not assigned
            if (runEast == null || runEast.Length == 0 || runEast[0] == null) runEast = LoadAnimationFrames("Engineer_Run_East");
            if (runNorthEast == null || runNorthEast.Length == 0 || runNorthEast[0] == null) runNorthEast = LoadAnimationFrames("Engineer_Run_NorthEast");
            if (runNorth == null || runNorth.Length == 0 || runNorth[0] == null) runNorth = LoadAnimationFrames("Engineer_Run_North");
            if (runNorthWest == null || runNorthWest.Length == 0 || runNorthWest[0] == null) runNorthWest = LoadAnimationFrames("Engineer_Run_NorthWest");
            if (runWest == null || runWest.Length == 0 || runWest[0] == null) runWest = LoadAnimationFrames("Engineer_Run_West");
            if (runSouthWest == null || runSouthWest.Length == 0 || runSouthWest[0] == null) runSouthWest = LoadAnimationFrames("Engineer_Run_SouthWest");
            if (runSouth == null || runSouth.Length == 0 || runSouth[0] == null) runSouth = LoadAnimationFrames("Engineer_Run_South");
            if (runSouthEast == null || runSouthEast.Length == 0 || runSouthEast[0] == null) runSouthEast = LoadAnimationFrames("Engineer_Run_SouthEast");

            // Fallback 3: Ensure sprite renderer is white and using unlit material
            if (spriteRenderer != null)
            {
                spriteRenderer.color = Color.white;

                if (spriteRenderer.sharedMaterial == null || (spriteRenderer.sharedMaterial.shader != null && spriteRenderer.sharedMaterial.shader.name.Contains("Lit")))
                {
                    Shader unlit = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default") ?? Shader.Find("Sprites/Default");
                    if (unlit != null)
                    {
                        spriteRenderer.material = new Material(unlit);
                    }
                }

                if (idleSouth != null && (spriteRenderer.sprite == null || spriteRenderer.sprite.name.Contains("Placeholder") || (spriteRenderer.sprite.texture != null && spriteRenderer.sprite.texture.width <= 16)))
                {
                    spriteRenderer.sprite = idleSouth;
                }
            }
        }

        private void LoadIdleSpritesFromResources()
        {
            Sprite[] loaded = Resources.LoadAll<Sprite>("Engineer_Idle_8Dir");
            if (loaded != null && loaded.Length > 0)
            {
                foreach (var s in loaded)
                {
                    switch (s.name)
                    {
                        case "Engineer_Idle_South": idleSouth = s; break;
                        case "Engineer_Idle_SouthEast": idleSouthEast = s; break;
                        case "Engineer_Idle_East": idleEast = s; break;
                        case "Engineer_Idle_NorthEast": idleNorthEast = s; break;
                        case "Engineer_Idle_North": idleNorth = s; break;
                        case "Engineer_Idle_NorthWest": idleNorthWest = s; break;
                        case "Engineer_Idle_West": idleWest = s; break;
                        case "Engineer_Idle_SouthWest": idleSouthWest = s; break;
                    }
                }
            }

            // Fallback: If sliced sprites could not be loaded via Resources.LoadAll, slice directly from Texture2D
            if (idleSouth == null)
            {
                Texture2D tex = Resources.Load<Texture2D>("Engineer_Idle_8Dir");
                if (tex != null)
                {
                    idleSouth = Sprite.Create(tex, new Rect(0, 92, 92, 92), new Vector2(0.5f, 0.5f), 64f);
                    idleSouth.name = "Engineer_Idle_South";
                    idleSouthEast = Sprite.Create(tex, new Rect(92, 92, 92, 92), new Vector2(0.5f, 0.5f), 64f);
                    idleSouthEast.name = "Engineer_Idle_SouthEast";
                    idleEast = Sprite.Create(tex, new Rect(184, 92, 92, 92), new Vector2(0.5f, 0.5f), 64f);
                    idleEast.name = "Engineer_Idle_East";
                    idleNorthEast = Sprite.Create(tex, new Rect(276, 92, 92, 92), new Vector2(0.5f, 0.5f), 64f);
                    idleNorthEast.name = "Engineer_Idle_NorthEast";
                    idleNorth = Sprite.Create(tex, new Rect(368, 92, 92, 92), new Vector2(0.5f, 0.5f), 64f);
                    idleNorth.name = "Engineer_Idle_North";
                    idleNorthWest = Sprite.Create(tex, new Rect(0, 0, 92, 92), new Vector2(0.5f, 0.5f), 64f);
                    idleNorthWest.name = "Engineer_Idle_NorthWest";
                    idleWest = Sprite.Create(tex, new Rect(92, 0, 92, 92), new Vector2(0.5f, 0.5f), 64f);
                    idleWest.name = "Engineer_Idle_West";
                    idleSouthWest = Sprite.Create(tex, new Rect(184, 0, 92, 92), new Vector2(0.5f, 0.5f), 64f);
                    idleSouthWest.name = "Engineer_Idle_SouthWest";
                }
            }
        }

        private Sprite[] LoadAnimationFrames(string sheetName)
        {
            Sprite[] loaded = Resources.LoadAll<Sprite>(sheetName);
            if (loaded != null && loaded.Length > 0)
            {
                int maxIndex = -1;
                foreach (var s in loaded)
                {
                    int underscore = s.name.LastIndexOf('_');
                    if (underscore >= 0 && int.TryParse(s.name.Substring(underscore + 1), out int parsedIdx))
                    {
                        if (parsedIdx > maxIndex) maxIndex = parsedIdx;
                    }
                }

                int total = (maxIndex >= 0) ? (maxIndex + 1) : 8;
                Sprite[] frames = new Sprite[total];
                foreach (var s in loaded)
                {
                    for (int i = 0; i < total; i++)
                    {
                        if (s.name.EndsWith($"_{i}"))
                        {
                            frames[i] = s;
                            break;
                        }
                    }
                }
                if (frames[0] != null) return frames;
            }

            // Fallback: Slice directly from Texture2D
            Texture2D tex = Resources.Load<Texture2D>(sheetName);
            if (tex != null)
            {
                Sprite[] frames = new Sprite[8];
                frames[0] = Sprite.Create(tex, new Rect(0, 92, 92, 92), new Vector2(0.5f, 0.5f), 64f);
                frames[1] = Sprite.Create(tex, new Rect(92, 92, 92, 92), new Vector2(0.5f, 0.5f), 64f);
                frames[2] = Sprite.Create(tex, new Rect(184, 92, 92, 92), new Vector2(0.5f, 0.5f), 64f);
                frames[3] = Sprite.Create(tex, new Rect(276, 92, 92, 92), new Vector2(0.5f, 0.5f), 64f);
                frames[4] = Sprite.Create(tex, new Rect(368, 92, 92, 92), new Vector2(0.5f, 0.5f), 64f);
                frames[5] = Sprite.Create(tex, new Rect(0, 0, 92, 92), new Vector2(0.5f, 0.5f), 64f);
                frames[6] = Sprite.Create(tex, new Rect(92, 0, 92, 92), new Vector2(0.5f, 0.5f), 64f);
                frames[7] = Sprite.Create(tex, new Rect(184, 0, 92, 92), new Vector2(0.5f, 0.5f), 64f);
                return frames;
            }

            return new Sprite[8];
        }

        private void Update()
        {
            if (playerController == null)
            {
                return;
            }

            Vector2 facing = playerController.FacingDirection;
            bool isMoving = playerController.IsMoving;

            // Feed Animator parameters
            if (animator != null)
            {
                animator.SetBool(IsMovingParam, isMoving);
                animator.SetFloat(MoveXParam, facing.x);
                animator.SetFloat(MoveYParam, facing.y);
            }

            // Update SpriteRenderer
            if (spriteRenderer != null)
            {
                if (isMoving)
                {
                    if (enableIdleBreathing && transform.localScale != initialScale)
                    {
                        transform.localScale = initialScale;
                    }

                    float speedRatio = 1f;
                    if (playerController != null && playerController.MoveSpeed > 0.001f)
                    {
                        speedRatio = Mathf.Clamp(playerController.CurrentVelocity.magnitude / playerController.MoveSpeed, 0.25f, 1.25f);
                    }

                    animTimer += Time.deltaTime * speedRatio;
                    int frameIndex = (int)(animTimer * animationFps);
                    UpdateMovingSprite(facing, frameIndex);
                }
                else
                {
                    animTimer = 0f;
                    spriteRenderer.flipX = false;
                    Sprite targetSprite = GetDirectionalSprite(facing);
                    if (targetSprite != null && (spriteRenderer.sprite != targetSprite || spriteRenderer.sprite.name.Contains("Placeholder") || (spriteRenderer.sprite.texture != null && spriteRenderer.sprite.texture.width <= 16)))
                    {
                        spriteRenderer.sprite = targetSprite;
                    }

                    if (enableIdleBreathing)
                    {
                        float breathe = Mathf.Sin(Time.time * breatheFrequency) * breatheAmplitude;
                        transform.localScale = new Vector3(
                            initialScale.x * (1f - breathe * 0.35f),
                            initialScale.y * (1f + breathe),
                            initialScale.z
                        );
                    }
                    else if (initialScale != Vector3.zero && transform.localScale != initialScale)
                    {
                        transform.localScale = initialScale;
                    }
                }
            }
        }

        private void OnDisable()
        {
            if (initialScale != Vector3.zero)
            {
                transform.localScale = initialScale;
            }
        }

        private void UpdateMovingSprite(Vector2 direction, int frameIndex)
        {
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            if (angle < 0f) angle += 360f;

            int octant = (int)((angle + 22.5f) / 45f) % 8;
            Sprite frame = null;

            // All 8 directions have dedicated drawn animations - no flip needed!
            switch (octant)
            {
                case 0: // East (0 deg)
                    frame = GetFrame(runEast, frameIndex);
                    break;
                case 1: // NorthEast (45 deg)
                    frame = GetFrame(runNorthEast, frameIndex);
                    break;
                case 2: // North (90 deg)
                    frame = GetFrame(runNorth, frameIndex);
                    break;
                case 3: // NorthWest (135 deg)
                    frame = GetFrame(runNorthWest, frameIndex);
                    break;
                case 4: // West (180 deg)
                    frame = GetFrame(runWest, frameIndex);
                    break;
                case 5: // SouthWest (225 deg)
                    frame = GetFrame(runSouthWest, frameIndex);
                    break;
                case 6: // South (270 deg)
                    frame = GetFrame(runSouth, frameIndex);
                    break;
                case 7: // SouthEast (315 deg)
                    frame = GetFrame(runSouthEast, frameIndex);
                    break;
            }

            if (frame != null)
            {
                spriteRenderer.sprite = frame;
            }
            spriteRenderer.flipX = false;
        }

        private Sprite GetFrame(Sprite[] frames, int index)
        {
            if (frames != null && frames.Length > 0)
            {
                int validCount = 0;
                while (validCount < frames.Length && frames[validCount] != null)
                {
                    validCount++;
                }

                if (validCount > 0)
                {
                    int safeIndex = (index >= 0 ? index : 0) % validCount;
                    return frames[safeIndex];
                }
            }
            return idleSouth;
        }

        public Sprite GetDirectionalSprite(Vector2 direction)
        {
            if (direction.sqrMagnitude < 0.0001f)
            {
                return idleSouth;
            }

            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            if (angle < 0f)
            {
                angle += 360f;
            }

            int octant = (int)((angle + 22.5f) / 45f) % 8;
            return octant switch
            {
                0 => idleEast ?? idleSouth,
                1 => idleNorthEast ?? idleSouth,
                2 => idleNorth ?? idleSouth,
                3 => idleNorthWest ?? idleSouth,
                4 => idleWest ?? idleSouth,
                5 => idleSouthWest ?? idleSouth,
                6 => idleSouth,
                7 => idleSouthEast ?? idleSouth,
                _ => idleSouth
            };
        }

#if UNITY_EDITOR
        private void Reset()
        {
            if (playerController == null) playerController = GetComponent<PlayerController>();
            if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
            AutoPopulateSprites();
        }

        private void OnValidate()
        {
            if (idleSouth == null || runSouth == null || runSouth.Length == 0 || runSouth[0] == null)
            {
                AutoPopulateSprites();
            }
        }

        private void AutoPopulateSprites()
        {
            var assets = UnityEditor.AssetDatabase.LoadAllAssetsAtPath("Assets/_Project/Sprites/Characters/Engineer_Idle_8Dir.png");
            if (assets != null && assets.Length > 0)
            {
                foreach (var asset in assets)
                {
                    if (asset is Sprite s)
                    {
                        switch (s.name)
                        {
                            case "Engineer_Idle_South": idleSouth = s; break;
                            case "Engineer_Idle_SouthEast": idleSouthEast = s; break;
                            case "Engineer_Idle_East": idleEast = s; break;
                            case "Engineer_Idle_NorthEast": idleNorthEast = s; break;
                            case "Engineer_Idle_North": idleNorth = s; break;
                            case "Engineer_Idle_NorthWest": idleNorthWest = s; break;
                            case "Engineer_Idle_West": idleWest = s; break;
                            case "Engineer_Idle_SouthWest": idleSouthWest = s; break;
                        }
                    }
                }
            }

            PopulateRunArray("Engineer_Run_East", ref runEast);
            PopulateRunArray("Engineer_Run_NorthEast", ref runNorthEast);
            PopulateRunArray("Engineer_Run_North", ref runNorth);
            PopulateRunArray("Engineer_Run_NorthWest", ref runNorthWest);
            PopulateRunArray("Engineer_Run_West", ref runWest);
            PopulateRunArray("Engineer_Run_SouthWest", ref runSouthWest);
            PopulateRunArray("Engineer_Run_South", ref runSouth);
            PopulateRunArray("Engineer_Run_SouthEast", ref runSouthEast);
        }

        private void PopulateRunArray(string sheetName, ref Sprite[] target)
        {
            var assets = UnityEditor.AssetDatabase.LoadAllAssetsAtPath($"Assets/_Project/Sprites/Characters/{sheetName}.png");
            if (assets == null || assets.Length == 0) return;

            Sprite[] frames = new Sprite[8];
            foreach (var asset in assets)
            {
                if (asset is Sprite s)
                {
                    for (int i = 0; i < 8; i++)
                    {
                        if (s.name.EndsWith($"_{i}"))
                        {
                            frames[i] = s;
                            break;
                        }
                    }
                }
            }
            if (frames[0] != null)
            {
                target = frames;
            }
        }
#endif
    }
}
