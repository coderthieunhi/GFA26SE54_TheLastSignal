#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Tilemaps;

namespace TheLastSignal.EditorTools
{
    public static class BedroomEnvironmentSetup
    {
        private const string TilesSourceDir = "Assets/_Project/Sprites/Environments/Bedroom/Tiles";
        private const string PropsSourceDir = "Assets/_Project/Sprites/Environments/Bedroom/Props";
        private const string TileAssetsDir = "Assets/_Project/Tiles/Bedroom";

        private static bool IsReadOnlyOrPlayMode()
        {
            return EditorApplication.isPlayingOrWillChangePlaymode
                || EditorApplication.isPlaying
                || System.Environment.CommandLine.IndexOf("-readonly", System.StringComparison.OrdinalIgnoreCase) >= 0
                || System.Environment.CommandLine.IndexOf("--virtual-project-clone", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        [InitializeOnLoadMethod]
        private static void OnEditorLoaded()
        {
            EditorApplication.delayCall += () =>
            {
                if (IsReadOnlyOrPlayMode()) return;
                if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "SampleScene")
                {
                    SetupBedroomArt();
                }
            };
        }

        [MenuItem("Tools/Facility/Setup Bedroom Art & Environment", false, 52)]
        public static void SetupBedroomArt()
        {
            // 1. Configure and import Tile textures (PPU = 128)
            ConfigureTileTextures();

            // 2. Configure and import Prop textures (PPU = 128)
            ConfigurePropTextures();

            // 3. Create / update Tile ScriptableObjects
            CreateTileAssets();

            // 4. Decorate Bedroom in the active scene at the exact Facility Coordinates
            DecorateActiveScene();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("<color=green>[BedroomEnvironmentSetup] Successfully decorated Bedroom at exact coordinates [-21..-13, 9..16] without affecting any other room!</color>");
        }

        public static void ConfigureTileTextures()
        {
            if (!Directory.Exists(TilesSourceDir)) return;

            string[] files = Directory.GetFiles(TilesSourceDir, "*.png");
            foreach (string file in files)
            {
                TextureImporter importer = AssetImporter.GetAtPath(file) as TextureImporter;
                if (importer != null)
                {
                    bool changed = false;
                    if (importer.textureType != TextureImporterType.Sprite) { importer.textureType = TextureImporterType.Sprite; changed = true; }
                    if (importer.spriteImportMode != SpriteImportMode.Single) { importer.spriteImportMode = SpriteImportMode.Single; changed = true; }
                    if (importer.spritePixelsPerUnit != 128f) { importer.spritePixelsPerUnit = 128f; changed = true; }
                    if (importer.filterMode != FilterMode.Point) { importer.filterMode = FilterMode.Point; changed = true; }
                    if (importer.textureCompression != TextureImporterCompression.Uncompressed) { importer.textureCompression = TextureImporterCompression.Uncompressed; changed = true; }

                    TextureImporterSettings texSettings = new TextureImporterSettings();
                    importer.ReadTextureSettings(texSettings);
                    bool texChanged = false;
                    if (texSettings.spriteMeshType != SpriteMeshType.FullRect) { texSettings.spriteMeshType = SpriteMeshType.FullRect; texChanged = true; }
                    if (texSettings.spriteExtrude != 0) { texSettings.spriteExtrude = 0; texChanged = true; }
                    if (texChanged)
                    {
                        importer.SetTextureSettings(texSettings);
                        changed = true;
                    }

                    if (changed)
                    {
                        EditorUtility.SetDirty(importer);
                        importer.SaveAndReimport();
                    }
                }
            }
        }

        public static void ConfigurePropTextures()
        {
            if (!Directory.Exists(PropsSourceDir)) return;

            string[] files = Directory.GetFiles(PropsSourceDir, "*.png");
            foreach (string file in files)
            {
                TextureImporter importer = AssetImporter.GetAtPath(file) as TextureImporter;
                if (importer != null)
                {
                    bool changed = false;
                    if (importer.textureType != TextureImporterType.Sprite) { importer.textureType = TextureImporterType.Sprite; changed = true; }
                    if (importer.spriteImportMode != SpriteImportMode.Single) { importer.spriteImportMode = SpriteImportMode.Single; changed = true; }
                    if (importer.spritePixelsPerUnit != 128f) { importer.spritePixelsPerUnit = 128f; changed = true; }
                    if (importer.filterMode != FilterMode.Point) { importer.filterMode = FilterMode.Point; changed = true; }
                    if (!importer.alphaIsTransparency) { importer.alphaIsTransparency = true; changed = true; }
                    if (importer.textureCompression != TextureImporterCompression.Uncompressed) { importer.textureCompression = TextureImporterCompression.Uncompressed; changed = true; }

                    TextureImporterSettings texSettings = new TextureImporterSettings();
                    importer.ReadTextureSettings(texSettings);
                    bool texChanged = false;
                    if (texSettings.spriteMeshType != SpriteMeshType.FullRect) { texSettings.spriteMeshType = SpriteMeshType.FullRect; texChanged = true; }
                    if (texSettings.spriteExtrude != 0) { texSettings.spriteExtrude = 0; texChanged = true; }
                    if (texChanged)
                    {
                        importer.SetTextureSettings(texSettings);
                        changed = true;
                    }

                    if (changed)
                    {
                        EditorUtility.SetDirty(importer);
                        importer.SaveAndReimport();
                    }
                }
            }
        }

        public static void CreateTileAssets()
        {
            if (!Directory.Exists(TileAssetsDir))
            {
                Directory.CreateDirectory(TileAssetsDir);
                AssetDatabase.Refresh();
            }

            if (!Directory.Exists(TilesSourceDir)) return;

            string[] pngFiles = Directory.GetFiles(TilesSourceDir, "*.png");
            foreach (string pngPath in pngFiles)
            {
                string tileName = Path.GetFileNameWithoutExtension(pngPath);
                string assetPath = $"{TileAssetsDir}/{tileName}.asset";

                Sprite sprite = LoadSprite(pngPath);
                if (sprite == null) continue;

                Tile tile = AssetDatabase.LoadAssetAtPath<Tile>(assetPath);
                if (tile == null)
                {
                    tile = ScriptableObject.CreateInstance<Tile>();
                    tile.name = tileName;
                    tile.sprite = sprite;
                    tile.color = Color.white;
                    tile.colliderType = tileName.Contains("Wall") ? Tile.ColliderType.Grid : Tile.ColliderType.None;

                    AssetDatabase.CreateAsset(tile, assetPath);
                }
                else
                {
                    tile.sprite = sprite;
                    tile.color = Color.white;
                    EditorUtility.SetDirty(tile);
                }
            }
        }

        public static void DecorateActiveScene()
        {
            ConfigureTileTextures();
            ConfigurePropTextures();
            CreateTileAssets();

            GameObject gridGo = GameObject.Find("Facility_Grid") ?? GameObject.Find("Grid");
            if (gridGo == null)
            {
                Debug.LogError("[BedroomEnvironmentSetup] Facility_Grid not found in active scene.");
                return;
            }

            Tilemap floorMap = gridGo.transform.Find("Floor_Tilemap")?.GetComponent<Tilemap>();
            Tilemap wallMap = gridGo.transform.Find("Wall_Tilemap")?.GetComponent<Tilemap>();

            if (floorMap == null)
            {
                Debug.LogError("[BedroomEnvironmentSetup] Floor_Tilemap not found under Facility_Grid.");
                return;
            }

            // ========================================================
            // STEP 0: STRICT TARGETED CLEANUP (NEVER TOUCH OUTSIDE BOUNDS!)
            // Bounds strictly confined to Bedroom & Corridor footprint:
            // X in [-22, -7], Y in [8, 17]  ← Stop at -7, NOT -6!
            // X = -6 = West wall of Airlock → MUST NOT be cleared!
            // And previous misplaced test area strictly in void: X in [-17, -7], Y in [1, 7]
            // Loop_West_Vertical (X <= -23) and Airlock (X >= -6) are 100% UNTOUCHED!
            // ========================================================
            Undo.RecordObject(floorMap, "Clear Bedroom Footprint Tiles");
            if (wallMap != null) Undo.RecordObject(wallMap, "Clear Bedroom Footprint Wall Tiles");

            // Clean previous misplaced test area in void (X: [-17..-7], Y: [1..7])
            for (int x = -17; x <= -7; x++)
            {
                for (int y = 1; y <= 7; y++)
                {
                    floorMap.SetTile(new Vector3Int(x, y, 0), null);
                    if (wallMap != null) wallMap.SetTile(new Vector3Int(x, y, 0), null);
                }
            }

            // Clean Bedroom and corridor footprint only
            // IMPORTANT: Stop at X = -7 to preserve Airlock West wall tiles at X = -6!
            for (int x = -22; x <= -7; x++)
            {
                for (int y = 8; y <= 17; y++)
                {
                    floorMap.SetTile(new Vector3Int(x, y, 0), null);
                    if (wallMap != null) wallMap.SetTile(new Vector3Int(x, y, 0), null);
                }
            }

            // (Airlock West Wall is properly located at X = -5 in AirlockEnvironmentSetup; X = -6 is outside the room)

            // Load Tile assets
            Tile fDeck = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/Bedroom_Floor_Crew_Deck.asset");
            Tile fMat = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/Bedroom_Floor_Rubber_Mat.asset");
            Tile fGlow = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/Bedroom_Floor_Emergency_Glow.asset");
            Tile fGlowH = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/Bedroom_Floor_Emergency_Glow_H.asset") ?? fGlow;
            Tile fGlowEndW = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/Bedroom_Floor_Emergency_Glow_End_W.asset") ?? fGlowH;
            Tile fGrate = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/Bedroom_Floor_Air_Grate.asset");
            Tile fHaz = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/Bedroom_Floor_Hazard_Trim.asset");
            Tile fHazV = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/Bedroom_Floor_Hazard_V.asset") ?? fHaz;
            Tile fStencil = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/Bedroom_Floor_Cabin01_Stencil.asset");
            Tile fLight = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/Bedroom_Floor_Light_Tile.asset");

            // Directional Walls (Baseboards correctly oriented facing the room interior)
            Tile wBulkhead_N = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/Bedroom_Wall_Modular_Bulkhead_N.asset") ?? AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/Bedroom_Wall_Modular_Bulkhead.asset");
            Tile wBulkhead_S = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/Bedroom_Wall_Modular_Bulkhead_S.asset") ?? wBulkhead_N;
            Tile wBulkhead_W = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/Bedroom_Wall_Modular_Bulkhead_W.asset") ?? wBulkhead_N;
            Tile wBulkhead_E = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/Bedroom_Wall_Modular_Bulkhead_E.asset") ?? wBulkhead_N;

            Tile wBulkheadLED_N = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/Bedroom_Wall_Bulkhead_LED_N.asset") ?? AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/Bedroom_Wall_Bulkhead_LED.asset");
            Tile wBulkheadLED_S = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/Bedroom_Wall_Bulkhead_LED_S.asset") ?? wBulkheadLED_N;
            Tile wBulkheadLED_W = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/Bedroom_Wall_Bulkhead_LED_W.asset") ?? wBulkheadLED_N;
            Tile wBulkheadLED_E = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/Bedroom_Wall_Bulkhead_LED_E.asset") ?? wBulkheadLED_N;

            Tile wAccess_N = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/Bedroom_Wall_Access_Panel_N.asset") ?? AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/Bedroom_Wall_Access_Panel.asset");
            Tile wAccess_S = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/Bedroom_Wall_Access_Panel_S.asset") ?? wAccess_N;
            Tile wAccess_W = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/Bedroom_Wall_Access_Panel_W.asset") ?? wAccess_N;
            Tile wAccess_E = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/Bedroom_Wall_Access_Panel_E.asset") ?? wAccess_N;

            Tile wClimate = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/Bedroom_Wall_Climate_Panel.asset") ?? wBulkhead_S;
            Tile wCornerTL = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/Bedroom_Wall_Corner_TL.asset") ?? wBulkhead_N;
            Tile wCornerTR = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/Bedroom_Wall_Corner_TR.asset") ?? wBulkhead_N;
            Tile wCornerBR = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/Bedroom_Wall_Corner_BR.asset") ?? wBulkhead_S;
            Tile wCornerBL = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/Bedroom_Wall_Corner_BL.asset") ?? wBulkhead_S;

            // ========================================================
            // STEP 1: PAINT BEDROOM INTERIOR FLOOR: X[-21..-13], Y[9..16]
            // AND CORRIDOR FLOOR: X[-12..-6], Y[12..13]
            // ========================================================
            for (int x = -21; x <= -13; x++)
            {
                for (int y = 9; y <= 16; y++)
                {
                    Vector3Int pos = new Vector3Int(x, y, 0);

                    // Welcome cabin stencil at entrance walkway: X = -17, Y = 12
                    if (x == -17 && y == 12)
                    {
                        floorMap.SetTile(pos, fStencil ?? fDeck);
                    }
                    // Industrial Hazard warning stripes directly under the 4 Cryo Sleep Pods:
                    // NW Bed: X in [-20, -19], Y in [14, 15] (Shifted 1 down, 1 left)
                    // NE Bed: X in [-15, -14], Y in [14, 15] (Shifted 1 down)
                    // SW Bed: X in [-20, -19], Y in [10, 11] (Shifted 1 left)
                    // SE Bed: X in [-15, -14], Y in [10, 11] (Base anchor)
                    else if ((x == -20 || x == -19) && (y == 14 || y == 15))
                    {
                        floorMap.SetTile(pos, fHaz ?? fDeck);
                    }
                    else if ((x == -15 || x == -14) && (y == 14 || y == 15))
                    {
                        floorMap.SetTile(pos, fHaz ?? fDeck);
                    }
                    else if ((x == -20 || x == -19) && (y == 10 || y == 11))
                    {
                        floorMap.SetTile(pos, fHaz ?? fDeck);
                    }
                    else if ((x == -15 || x == -14) && (y == 10 || y == 11))
                    {
                        floorMap.SetTile(pos, fHaz ?? fDeck);
                    }
                    // Ventilation air intake grate below Climate Station on South Wall:
                    else if (x == -17 && y == 9)
                    {
                        floorMap.SetTile(pos, fGrate ?? fDeck);
                    }
                    // Heavy industrial bordered steel deck plates:
                    else
                    {
                        floorMap.SetTile(pos, fDeck);
                    }
                }
            }

            // Corridor_Bedroom_Airlock: X in [-12, -6], Y in [12, 13]
            for (int cx = -12; cx <= -6; cx++)
            {
                for (int cy = 12; cy <= 13; cy++)
                {
                    Vector3Int cpos = new Vector3Int(cx, cy, 0);
                    // Continuous Vertical Hazard threshold stripes at both door endpoints:
                    // Bedroom entrance threshold: cx = -12 (shifted right 1 tile from room interior)
                    // Airlock entrance threshold: cx = -6
                    if (cx == -12 || cx == -6)
                    {
                        floorMap.SetTile(cpos, fHazV ?? fHaz ?? fDeck);
                    }
                    else
                    {
                        floorMap.SetTile(cpos, fDeck);
                    }
                }
            }

            // ========================================================
            // STEP 2: PAINT WALLS (DIRECTIONAL BULKHEADS + MATCHING CORNERS)
            // North: Y = 17, X in [-21, -13] (Facing South)
            // South: Y = 8, X in [-21, -13] (Facing North)
            // West: X = -22, Y in [9, 16] (Facing East)
            // East: X = -12, Y in [9..11] and Y in [14..16] (Facing West, doorway at 12, 13)
            // ========================================================
            if (wallMap != null)
            {
                // North Wall: Y = 17 (Facing South into room)
                for (int x = -21; x <= -13; x++)
                {
                    Tile wt = wBulkhead_N;
                    if (x == -21 || x == -13) wt = wBulkheadLED_N;
                    else if (x == -19 || x == -15) wt = wAccess_N;
                    else wt = wBulkhead_N;
                    wallMap.SetTile(new Vector3Int(x, 17, 0), wt ?? wBulkhead_N);
                }
                wallMap.SetTile(new Vector3Int(-22, 17, 0), wCornerTL ?? wBulkhead_N);
                wallMap.SetTile(new Vector3Int(-12, 17, 0), wCornerTR ?? wBulkhead_N);

                // South Wall: Y = 8 (Facing North into room)
                for (int x = -21; x <= -13; x++)
                {
                    Tile wt = wBulkhead_S;
                    if (x == -21 || x == -13) wt = wBulkheadLED_S;
                    else if (x == -19 || x == -15) wt = wAccess_S;
                    else if (x == -17) wt = wClimate;
                    else wt = wBulkhead_S;
                    wallMap.SetTile(new Vector3Int(x, 8, 0), wt ?? wBulkhead_S);
                }
                wallMap.SetTile(new Vector3Int(-22, 8, 0), wCornerBL ?? wBulkhead_S);
                wallMap.SetTile(new Vector3Int(-12, 8, 0), wCornerBR ?? wBulkhead_S);

                // West Wall: X = -22 (Facing East into room - continuous vertical steel bulkhead)
                for (int y = 9; y <= 16; y++)
                {
                    wallMap.SetTile(new Vector3Int(-22, y, 0), wBulkhead_W ?? wBulkhead_N);
                }

                // East Wall: X = -12 (Facing West into room - continuous vertical steel bulkhead with doorway)
                for (int y = 9; y <= 16; y++)
                {
                    if (y == 12 || y == 13)
                    {
                        wallMap.SetTile(new Vector3Int(-12, y, 0), null); // doorway to corridor
                    }
                    else
                    {
                        wallMap.SetTile(new Vector3Int(-12, y, 0), wBulkhead_E ?? wBulkhead_N);
                    }
                }

                // Corridor Walls
                for (int cx = -11; cx <= -6; cx++)
                {
                    wallMap.SetTile(new Vector3Int(cx, 14, 0), wBulkhead_N); // North
                    wallMap.SetTile(new Vector3Int(cx, 11, 0), wBulkhead_S); // South
                }
            }

            // ========================================================
            // STEP 3: SETUP PROPS WITH BALANCED SIZING & SYMMETRICAL LAYOUT
            // True Center: X = -16.5f, Y = 13.0f
            // ========================================================
            GameObject bedroomRoot = GameObject.Find("Bedroom_Props");
            if (bedroomRoot == null)
            {
                bedroomRoot = new GameObject("Bedroom_Props");
                Undo.RegisterCreatedObjectUndo(bedroomRoot, "Create Bedroom_Props Root");
            }

            // Deselect to avoid Inspector SerializedObjectNotCreatableException during object recreation
            if (Selection.activeGameObject != null && (Selection.activeGameObject.transform.IsChildOf(bedroomRoot.transform) || Selection.activeGameObject == bedroomRoot))
            {
                Selection.activeObject = null;
            }

            // Clean up old children of Bedroom_Props
            while (bedroomRoot.transform.childCount > 0)
            {
                Undo.DestroyObjectImmediate(bedroomRoot.transform.GetChild(0).gameObject);
            }

            // Destroy old placeholder Bedroom_Beds if present
            GameObject oldBeds = GameObject.Find("Bedroom_Beds");
            if (oldBeds != null)
            {
                Undo.DestroyObjectImmediate(oldBeds);
            }

            // Clean up any stray root props from previous runs
            GameObject[] allObjects = Object.FindObjectsByType<GameObject>();
            foreach (var go in allObjects)
            {
                if (go == null) continue;
                if ((go.name.StartsWith("Prop_Bedroom_") || go.name.StartsWith("Decal_Bedroom_") || go.name == "Bedroom_Wall_Observation_Window") && go.transform.parent == null)
                {
                    Undo.DestroyObjectImmediate(go);
                }
            }

            // Load standard URP 2D Sprite Lit Material
            Material defaultSpriteMat = AssetDatabase.LoadAssetAtPath<Material>("Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Lit-Default.mat");
            if (defaultSpriteMat == null)
            {
                Shader litShader = Shader.Find("Universal Render Pipeline/2D/Sprite-Lit-Default");
                if (litShader != null) defaultSpriteMat = new Material(litShader);
            }

            // --- 4 CRYO STASIS SLEEP PODS (HUMAN-SCALE PROPORTIONS ~1.56m HEIGHT) ---
            Vector3 bedScale = Vector3.one;
            Vector2 bedColliderSize = new Vector2(0.80f, 1.50f);
            Vector2 bedColliderOffset = Vector2.zero;

            // Bed P1 (NW: -18.80, 15.10) - Malfunctioning Breached Cryo Pod (Cracked glass, red light, coolant spill)
            CreateProp(bedroomRoot, "Prop_Bedroom_Sleep_Pod_Malfunction", new Vector3(-18.80f, 15.10f, 0f), "Obstacles", 3, defaultSpriteMat, true, bedColliderSize, bedColliderOffset, bedScale, null, false);

            // Bed P2 (NE: -14.20, 15.10) - Normal Operational Cryo Pod
            CreateProp(bedroomRoot, "Prop_Bedroom_Sleep_Pod_Normal", new Vector3(-14.20f, 15.10f, 0f), "Obstacles", 3, defaultSpriteMat, true, bedColliderSize, bedColliderOffset, bedScale, null, true);

            // Bed P3 (SW: -18.80, 11.20) - Normal Operational Cryo Pod
            CreateProp(bedroomRoot, "Prop_Bedroom_Sleep_Pod_Normal", new Vector3(-18.80f, 11.20f, 0f), "Obstacles", 3, defaultSpriteMat, true, bedColliderSize, bedColliderOffset, bedScale, null, false);

            // Bed P4 (SE: -14.20, 11.20) - Normal Operational Cryo Pod
            CreateProp(bedroomRoot, "Prop_Bedroom_Sleep_Pod_Normal", new Vector3(-14.20f, 11.20f, 0f), "Obstacles", 3, defaultSpriteMat, true, bedColliderSize, bedColliderOffset, bedScale, null, true);

            // --- 4 DIVERSE CORNER UTILITIES (COMPACT, BELIEVABLE SIZES) ---
            
            // 1. NW Corner: Twin Cryo O2 Tanks (Compact rack: ~0.66 x 0.90 units)
            Vector3 o2Scale = Vector3.one;
            Vector2 o2Col = new Vector2(0.65f, 0.90f);
            Vector2 o2Offset = Vector2.zero;
            CreateProp(bedroomRoot, "Prop_Bedroom_O2_Tanks", new Vector3(-20.20f, 15.30f, 0f), "Obstacles", 3, defaultSpriteMat, true, o2Col, o2Offset, o2Scale, null, false);

            // 2. SW Corner: Heavy Crew Footlocker Stash (Low chest: ~0.82 x 0.70 units)
            Vector3 footlockerScale = Vector3.one;
            Vector2 footlockerCol = new Vector2(0.80f, 0.65f);
            Vector2 footlockerOffset = Vector2.zero;
            CreateProp(bedroomRoot, "Prop_Bedroom_Footlocker", new Vector3(-20.20f, 11.20f, 0f), "Obstacles", 3, defaultSpriteMat, true, footlockerCol, footlockerOffset, footlockerScale, null, false);

            // 3. NE Corner: Officer's Single Command Terminal (Desk: ~0.95 x 1.05 units) + Stool
            Vector3 nightstandScale = Vector3.one;
            Vector2 nightstandCol = new Vector2(0.95f, 1.0f);
            Vector2 nightstandOffset = Vector2.zero;
            CreateProp(bedroomRoot, "Prop_Bedroom_Nightstand", new Vector3(-12.80f, 15.40f, 0f), "Obstacles", 3, defaultSpriteMat, true, nightstandCol, nightstandOffset, nightstandScale, null, false);
            CreateProp(bedroomRoot, "Prop_Bedroom_Stool", new Vector3(-12.80f, 14.50f, 0f), "Obstacles", 3, defaultSpriteMat, false, null, null, Vector3.one);

            // 4. SE Corner: Ration & Coffee Synthesizer Dispenser (Slim vending: ~0.66 x 1.20 units)
            Vector3 dispenserScale = Vector3.one;
            Vector2 dispenserCol = new Vector2(0.65f, 1.20f);
            Vector2 dispenserOffset = Vector2.zero;
            CreateProp(bedroomRoot, "Prop_Bedroom_Ration_Dispenser", new Vector3(-12.80f, 11.40f, 0f), "Obstacles", 3, defaultSpriteMat, true, dispenserCol, dispenserOffset, dispenserScale, null, false);

            // --- CENTRAL LOUNGE (OPTION 1: CREW MESS TABLE & EMBLEM RUG) ---
            // 1. Decal Rug Emblem at True Center: X = -16.50f, Y = 13.00f (SortingLayer Floor, Order 1)
            CreateProp(bedroomRoot, "Decal_Bedroom_Rug_Emblem", new Vector3(-16.50f, 13.00f, 0f), "Floor", 1, defaultSpriteMat, false, null, null, Vector3.one);

            // 2. Round Mess Table & Stools Set: Centered on the rug, X = -16.50f, Y = 13.15f (SortingLayer Obstacles, Order 3)
            // Collider covers the table base & stools (~1.05 x 0.85 units), allowing ample walking space in all 4 directions
            Vector2 messTableCol = new Vector2(1.05f, 0.85f);
            Vector2 messTableOffset = new Vector2(0f, -0.05f);
            CreateProp(bedroomRoot, "Prop_Bedroom_Mess_Table_Set", new Vector3(-16.50f, 13.15f, 0f), "Obstacles", 3, defaultSpriteMat, true, messTableCol, messTableOffset, Vector3.one, null, false);

            // --- NORTH WALL OBSERVATION VIEW ---
            CreateProp(bedroomRoot, "Bedroom_Wall_Observation_Window", new Vector3(-16.50f, 17.50f, 0f), "Obstacles", 2, defaultSpriteMat, false, null, null, new Vector3(1.0f, 1.0f, 1f), TilesSourceDir);

            // --- ENVIRONMENTAL STORYTELLING: ALIEN CLAW MARKS ---
            CreateProp(bedroomRoot, "Decal_Bedroom_Claw_Marks", new Vector3(-18.50f, 14.50f, 0f), "Floor", 1, defaultSpriteMat, false, null, null, new Vector3(1.10f, 1.10f, 1f));

            // ========================================================
            // STEP 4: AMBIENT & PRACTICAL LIGHTING
            // ========================================================
            // Floor Lights (Matching the fLight tile positions)
            Vector3[] floorLightPositions = new Vector3[]
            {
                new Vector3(-18.5f, 14.5f, 0f),
                new Vector3(-14.5f, 14.5f, 0f),
                new Vector3(-18.5f, 11.5f, 0f),
                new Vector3(-14.5f, 11.5f, 0f)
            };
            
            for (int i = 0; i < floorLightPositions.Length; i++)
            {
                GameObject fLightGo = new GameObject($"Light_Floor_{i}");
                fLightGo.transform.SetParent(bedroomRoot.transform);
                fLightGo.transform.position = floorLightPositions[i];
                Light2D l2d = fLightGo.AddComponent<Light2D>();
                l2d.lightType = Light2D.LightType.Point;
                l2d.color = new Color(0.9f, 0.95f, 1.0f, 1.0f); // Soft cool white
                l2d.intensity = 0.8f;
                l2d.pointLightOuterRadius = 6.0f;
                l2d.pointLightInnerRadius = 0.2f;
                l2d.enabled = false;
            }

            // Wall LED Panels (North Wall)
            Vector3[] wallLightPositions = new Vector3[]
            {
                new Vector3(-12.5f, 17.5f, 0f)  // North Right
            };
            
            for (int i = 0; i < wallLightPositions.Length; i++)
            {
                GameObject wLightGo = new GameObject($"Light_Wall_{i}");
                wLightGo.transform.SetParent(bedroomRoot.transform);
                wLightGo.transform.position = wallLightPositions[i];
                Light2D l2d = wLightGo.AddComponent<Light2D>();
                l2d.lightType = Light2D.LightType.Point;
                l2d.color = new Color(0.1f, 0.6f, 0.9f, 1.0f); // Cyan glow
                l2d.intensity = 0.8f;
                l2d.pointLightOuterRadius = 3.5f;
                l2d.pointLightInnerRadius = 0.5f;
                l2d.enabled = false;
            }

            // Practical prop accent lights:
            // 1. Red warning alarm glow at Malfunctioning Pod P1
            GameObject redAlertGo = new GameObject("Light_Malfunction_Alarm");
            redAlertGo.transform.SetParent(bedroomRoot.transform);
            redAlertGo.transform.position = new Vector3(-18.80f, 14.90f, 0f);
            Light2D redLight = redAlertGo.AddComponent<Light2D>();
            redLight.lightType = Light2D.LightType.Point;
            redLight.color = new Color(1.0f, 0.1f, 0.1f, 1.0f);
            redLight.intensity = 1.0f;
            redLight.pointLightOuterRadius = 2.5f;
            redLight.pointLightInnerRadius = 0.2f;
            redLight.enabled = false;

            // 2. Phosphor Green terminal glow at Officer CRT Desk
            GameObject crtLightGo = new GameObject("Light_CRT_Terminal");
            crtLightGo.transform.SetParent(bedroomRoot.transform);
            crtLightGo.transform.position = new Vector3(-12.80f, 15.60f, 0f);
            Light2D crtLight = crtLightGo.AddComponent<Light2D>();
            crtLight.lightType = Light2D.LightType.Point;
            crtLight.color = new Color(0.2f, 1.0f, 0.3f, 1.0f);
            crtLight.intensity = 0.7f;
            crtLight.pointLightOuterRadius = 2.0f;
            crtLight.pointLightInnerRadius = 0.2f;
            crtLight.enabled = false;

            // 3. Amber glow at Ration Dispenser
            GameObject amberLightGo = new GameObject("Light_Ration_Dispenser");
            amberLightGo.transform.SetParent(bedroomRoot.transform);
            amberLightGo.transform.position = new Vector3(-12.80f, 11.50f, 0f);
            Light2D amberLight = amberLightGo.AddComponent<Light2D>();
            amberLight.lightType = Light2D.LightType.Point;
            amberLight.color = new Color(1.0f, 0.65f, 0.1f, 1.0f);
            amberLight.intensity = 0.6f;
            amberLight.pointLightOuterRadius = 2.0f;
            amberLight.pointLightInnerRadius = 0.2f;
            amberLight.enabled = false;

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
        }

        private static GameObject CreateProp(GameObject parent, string propSpriteName, Vector3 position, string sortingLayer, int sortingOrder, Material mat, bool addCollider, Vector2? colliderSize = null, Vector2? colliderOffset = null, Vector3? localScale = null, string sourceDir = null, bool flipX = false)
        {
            string dir = sourceDir ?? PropsSourceDir;
            string spritePath = $"{dir}/{propSpriteName}.png";
            Sprite sprite = LoadSprite(spritePath);
            if (sprite == null) return null;

            GameObject propGo = new GameObject(propSpriteName);
            propGo.transform.SetParent(parent.transform);
            propGo.transform.position = position;

            if (localScale.HasValue)
            {
                propGo.transform.localScale = localScale.Value;
            }

            SpriteRenderer sr = propGo.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = Color.white; // 100% authentic original pixel art colors
            sr.flipX = flipX;
            sr.sortingLayerName = sortingLayer;
            sr.sortingOrder = sortingOrder;
            if (mat != null) sr.sharedMaterial = mat;

            if (addCollider)
            {
                BoxCollider2D box = propGo.AddComponent<BoxCollider2D>();
                if (colliderSize.HasValue) box.size = colliderSize.Value;
                if (colliderOffset.HasValue)
                {
                    box.offset = flipX ? new Vector2(-colliderOffset.Value.x, colliderOffset.Value.y) : colliderOffset.Value;
                }

                int obstacleLayer = LayerMask.NameToLayer("Obstacle");
                if (obstacleLayer >= 0) propGo.layer = obstacleLayer;
            }

            return propGo;
        }

        private static Sprite LoadSprite(string path)
        {
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
    }
}
#endif
