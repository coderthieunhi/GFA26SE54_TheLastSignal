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

        [MenuItem("The Last Signal/Environment/Setup Bedroom (Decorate + Save + Capture)", false, 50)]
        public static void SetupBedroomDecorateSaveCapture()
        {
            SetupBedroomArt();
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            CaptureBedroomScreenshots();
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

        public static void ConfigureTutorialTextures()
        {
            string tutDir = "Assets/_Project/Sprites/UI/Tutorial";
            if (!Directory.Exists(tutDir)) return;

            string[] files = Directory.GetFiles(tutDir, "*.png");
            foreach (string file in files)
            {
                TextureImporter importer = AssetImporter.GetAtPath(file) as TextureImporter;
                if (importer != null)
                {
                    bool changed = false;
                    if (importer.textureType != TextureImporterType.Sprite) { importer.textureType = TextureImporterType.Sprite; changed = true; }
                    if (importer.spriteImportMode != SpriteImportMode.Single) { importer.spriteImportMode = SpriteImportMode.Single; changed = true; }
                    if (importer.spritePixelsPerUnit != 64f) { importer.spritePixelsPerUnit = 64f; changed = true; }
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
            ConfigureTutorialTextures();
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
            Tile fAcousticDeck = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/Bedroom_Floor_Acoustic_Deck.asset") ?? fDeck;
            Tile fPodMount = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/Bedroom_Floor_Pod_Mount.asset") ?? fDeck;
            Tile fPodMountTop_W = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/Bedroom_Floor_Pod_Mount_Top.asset") ?? fPodMount;
            Tile fPodMountBottom_W = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/Bedroom_Floor_Pod_Mount_Bottom.asset") ?? fPodMount;
            Tile fPodMountTop_E = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/Bedroom_Floor_Pod_Mount_Top_E.asset") ?? fPodMountTop_W;
            Tile fPodMountBottom_E = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/Bedroom_Floor_Pod_Mount_Bottom_E.asset") ?? fPodMountBottom_W;
            Tile fThresholdSill = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/Bedroom_Floor_Threshold_Sill.asset");
            Tile fLoungeZone = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/Bedroom_Floor_Lounge_Zone.asset") ?? fDeck;
            Tile fCable = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/Bedroom_Floor_Cable_Channel.asset");
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

            // Engineered Architectural Bulkhead System (North & South)
            Tile wCol_N = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/Bedroom_Wall_Structural_Column_N.asset") ?? wBulkhead_N;
            Tile wCol_S = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/Bedroom_Wall_Structural_Column_S.asset") ?? wBulkhead_S;
            Tile wClad_N = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/Bedroom_Wall_Cladding_Smooth_N.asset") ?? wBulkhead_N;
            Tile wClad_S = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/Bedroom_Wall_Cladding_Smooth_S.asset") ?? wBulkhead_S;
            Tile wWindow_L = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/Bedroom_Wall_Window_Bay_L.asset") ?? wClad_N;
            Tile wWindow_M = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/Bedroom_Wall_Window_Bay_M.asset") ?? wClad_N;
            Tile wWindow_R = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/Bedroom_Wall_Window_Bay_R.asset") ?? wClad_N;
            Tile wHVAC_S = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/Bedroom_Wall_HVAC_Louver_S.asset") ?? wBulkhead_S;
            Tile wPower_S = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/Bedroom_Wall_Power_Service_S.asset") ?? wBulkhead_S;
            Tile wPadded_S = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/Bedroom_Wall_Padded_Acoustic_S.asset") ?? wBulkhead_S;

            // Engineered Architectural Bulkhead System (West & East)
            Tile wCol_W = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/Bedroom_Wall_Structural_Column_W.asset") ?? wBulkhead_W;
            Tile wCol_E = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/Bedroom_Wall_Structural_Column_E.asset") ?? wBulkhead_E;
            Tile wClad_W = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/Bedroom_Wall_Cladding_Smooth_W.asset") ?? wBulkhead_W;
            Tile wStencil_W = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/Bedroom_Wall_Cladding_Stencil_W.asset") ?? wClad_W;
            Tile wClad_E = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/Bedroom_Wall_Cladding_Smooth_E.asset") ?? wBulkhead_E;
            Tile wJambHeader_E = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/Bedroom_Wall_Door_Jamb_Header_E.asset") ?? wBulkhead_E;
            Tile wJambSill_E = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/Bedroom_Wall_Door_Jamb_Sill_E.asset") ?? wBulkhead_E;

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

                    // Central Living Lounge zone: X in [-17, -16], Y in [12, 13]
                    if ((x == -17 || x == -16) && (y == 12 || y == 13))
                    {
                        floorMap.SetTile(pos, fLoungeZone ?? fDeck);
                    }
                    // West Pod Mounting Chassis: X = -19 (Y = 15/14 for NW, Y = 11/10 for SW)
                    else if (x == -19 && (y == 15 || y == 11))
                    {
                        floorMap.SetTile(pos, fPodMountTop_W);
                    }
                    else if (x == -19 && (y == 14 || y == 10))
                    {
                        floorMap.SetTile(pos, fPodMountBottom_W);
                    }
                    // East Pod Mounting Chassis: X = -14 (Y = 15/14 for NE, Y = 11/10 for SE) - Mirrored for inward symmetry!
                    else if (x == -14 && (y == 15 || y == 11))
                    {
                        floorMap.SetTile(pos, fPodMountTop_E);
                    }
                    else if (x == -14 && (y == 14 || y == 10))
                    {
                        floorMap.SetTile(pos, fPodMountBottom_E);
                    }
                    // Balanced wayfinding guide lights flanking the central lounge approach:
                    else if ((x == -15 || x == -18) && y == 12)
                    {
                        floorMap.SetTile(pos, fLight ?? fDeck);
                    }
                    // Cabin 01 stencil on entrance aisle walkway: X = -13, Y = 12
                    else if (x == -13 && y == 12)
                    {
                        floorMap.SetTile(pos, fStencil ?? fDeck);
                    }
                    // Ventilation air intake grate below Climate Station on South Wall: X = -17, Y = 9
                    else if (x == -17 && y == 9)
                    {
                        floorMap.SetTile(pos, fGrate ?? fDeck);
                    }
                    // Standard Floor Panel: Continuous clean rolled steel floor plates
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
                    if (cx == -12)
                    {
                        floorMap.SetTile(cpos, fThresholdSill ?? fHazV ?? fDeck); // Architectural door threshold plate
                    }
                    else
                    {
                        floorMap.SetTile(cpos, fDeck);
                    }
                }
            }

            // ========================================================
            // STEP 2: PAINT WALLS (ENGINEERED BULKHEAD SYSTEM + MATCHING CORNERS)
            // North: Y = 17, X in [-21, -13] (Facing South)
            // South: Y = 8, X in [-21, -13] (Facing North)
            // West: X = -22, Y in [9, 16] (Facing East)
            // East: X = -12, Y in [9..11] and Y in [14..16] (Facing West, doorway at 12, 13)
            // ========================================================
            if (wallMap != null)
            {
                // North Wall: Y = 17 (Facing South into room)
                // Monolithic bulkhead with integrated 3-bay observation window:
                // X = -21: Column N (anchor to Corner TL)
                // X = -20: Smooth Cladding N
                // X = -19: Column N (flanking left jamb of observation window)
                // X = -18: Window Bay L (integrated deep space view)
                // X = -17: Window Bay M (integrated deep space view)
                // X = -16: Window Bay R (integrated deep space view)
                // X = -15: Column N (flanking right jamb of observation window)
                // X = -14: Smooth Cladding N (behind Pod 02)
                // X = -13: Column N (anchor to Corner TR)
                for (int x = -21; x <= -13; x++)
                {
                    Tile wt = wClad_N;
                    if (x == -21 || x == -19 || x == -15 || x == -13) wt = wCol_N;
                    else if (x == -18) wt = wWindow_L;
                    else if (x == -17) wt = wWindow_M;
                    else if (x == -16) wt = wWindow_R;
                    else wt = wClad_N;
                    wallMap.SetTile(new Vector3Int(x, 17, 0), wt ?? wBulkhead_N);
                }
                wallMap.SetTile(new Vector3Int(-22, 17, 0), wCornerTL ?? wBulkhead_N);
                wallMap.SetTile(new Vector3Int(-12, 17, 0), wCornerTR ?? wBulkhead_N);

                // South Wall: Y = 8 (Facing North into room - continuous lower structural foundation)
                for (int x = -21; x <= -13; x++)
                {
                    Tile wt = wClad_S;
                    if (x == -21 || x == -13) wt = wCol_S; // Structural Corner Anchors
                    else if (x == -17) wt = wClimate; // Functional climate station aligned with floor air grate
                    else wt = wClad_S; // Monolithic calm graphite cladding panels
                    wallMap.SetTile(new Vector3Int(x, 8, 0), wt ?? wBulkhead_S);
                }
                wallMap.SetTile(new Vector3Int(-22, 8, 0), wCornerBL ?? wBulkhead_S);
                wallMap.SetTile(new Vector3Int(-12, 8, 0), wCornerBR ?? wBulkhead_S);

                // West Wall: X = -22 (Facing East into room - 3 major structural columns + large calm cladding panels)
                for (int y = 9; y <= 16; y++)
                {
                    Tile wt = wClad_W;
                    if (y == 16 || y == 12 || y == 9) wt = wCol_W; // Top anchor, central column, bottom anchor
                    else if (y == 13) wt = wStencil_W; // Flush stenciled wayfinding signage
                    else wt = wClad_W; // Spacious calm graphite cladding
                    wallMap.SetTile(new Vector3Int(-22, y, 0), wt ?? wBulkhead_W);
                }

                // East Wall: X = -12 (Facing West into room - engineered bulkhead with reinforced doorway)
                for (int y = 9; y <= 16; y++)
                {
                    if (y == 12 || y == 13)
                    {
                        wallMap.SetTile(new Vector3Int(-12, y, 0), null); // doorway transit portal
                    }
                    else if (y == 14)
                    {
                        wallMap.SetTile(new Vector3Int(-12, y, 0), wJambHeader_E ?? wBulkhead_E);
                    }
                    else if (y == 11)
                    {
                        wallMap.SetTile(new Vector3Int(-12, y, 0), wJambSill_E ?? wBulkhead_E);
                    }
                    else if (y == 16 || y == 9)
                    {
                        wallMap.SetTile(new Vector3Int(-12, y, 0), wCol_E ?? wBulkhead_E);
                    }
                    else
                    {
                        wallMap.SetTile(new Vector3Int(-12, y, 0), wClad_E ?? wBulkhead_E);
                    }
                }

                // Corridor Walls
                for (int cx = -11; cx <= -6; cx++)
                {
                    Tile wtN = (cx == -9) ? (wCol_N ?? wBulkhead_N) : (wClad_N ?? wBulkhead_N);
                    Tile wtS = (cx == -9) ? (wCol_S ?? wBulkhead_S) : (wClad_S ?? wBulkhead_S);
                    wallMap.SetTile(new Vector3Int(cx, 14, 0), wtN); // North
                    wallMap.SetTile(new Vector3Int(cx, 11, 0), wtS); // South
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

            // --- 4 INDUSTRIAL CREW SLEEPING PODS (COHERENT EQUIPMENT FAMILY) ---
            Vector3 bedScale = Vector3.one;
            Vector2 bedColliderSize = new Vector2(0.72f, 1.58f);
            Vector2 bedColliderOffset = Vector2.zero;

            // Contact shadow helper
            Sprite shadowSp = LoadSprite($"{PropsSourceDir}/Prop_Bedroom_Pod_Contact_Shadow.png");
            System.Action<GameObject> addContactShadow = (podObj) =>
            {
                if (podObj == null || shadowSp == null) return;
                GameObject shGo = new GameObject("ContactShadow");
                shGo.transform.SetParent(podObj.transform, false);
                shGo.transform.localPosition = new Vector3(0f, -0.65f, 0f);
                SpriteRenderer shSr = shGo.AddComponent<SpriteRenderer>();
                shSr.sprite = shadowSp;
                shSr.sortingLayerName = "Obstacles";
                shSr.sortingOrder = 1; // Under pod at order 3
                if (defaultSpriteMat != null) shSr.sharedMaterial = defaultSpriteMat;
            };

            // Pod 01 (NW: -19.00, 14.85) - Exactly centered on mounting plate
            GameObject pod01 = CreateProp(bedroomRoot, "Prop_Bedroom_Pod_01_Damaged", new Vector3(-19.00f, 14.85f, 0f), "Obstacles", 3, defaultSpriteMat, true, bedColliderSize, bedColliderOffset, bedScale, null, false);
            addContactShadow(pod01);

            // Pod 02 (NE: -14.00, 14.85) - Exactly centered on mounting plate
            GameObject pod02 = CreateProp(bedroomRoot, "Prop_Bedroom_Pod_02_Ready", new Vector3(-14.00f, 14.85f, 0f), "Obstacles", 3, defaultSpriteMat, true, bedColliderSize, bedColliderOffset, bedScale, null, true);
            addContactShadow(pod02);

            // Pod 03 (SW: -19.00, 11.15) - Exactly centered on mounting plate
            GameObject pod03 = CreateProp(bedroomRoot, "Prop_Bedroom_Pod_03_Occupied", new Vector3(-19.00f, 11.15f, 0f), "Obstacles", 3, defaultSpriteMat, true, bedColliderSize, bedColliderOffset, bedScale, null, false);
            addContactShadow(pod03);

            // Pod 04 (SE: -14.00, 11.15) - Exactly centered on mounting plate
            GameObject pod04 = CreateProp(bedroomRoot, "Prop_Bedroom_Pod_04_Standby", new Vector3(-14.00f, 11.15f, 0f), "Obstacles", 3, defaultSpriteMat, true, bedColliderSize, bedColliderOffset, bedScale, null, true);
            addContactShadow(pod04);

            // --- CENTRAL CREW MUSTER & ORIENTATION STATION ---
            int interactableLayer = LayerMask.NameToLayer("Interactable");
            GameObject stationGo = new GameObject("Crew_Orientation_Station");
            stationGo.transform.SetParent(bedroomRoot.transform);
            stationGo.transform.position = new Vector3(-16.50f, 13.00f, 0f);

            if (interactableLayer >= 0) stationGo.layer = interactableLayer;

            // Physical collider (Central pedestal obstacle)
            CircleCollider2D stationCol = stationGo.AddComponent<CircleCollider2D>();
            stationCol.radius = 0.72f;
            stationCol.offset = Vector2.zero;

            // Visual Root
            GameObject visualRoot = new GameObject("VisualRoot");
            visualRoot.transform.SetParent(stationGo.transform, false);

            // 1. Pedestal (Structural central support column & centered radial floor mount)
            GameObject pedGo = new GameObject("Pedestal");
            pedGo.transform.SetParent(visualRoot.transform, false);
            SpriteRenderer pedSr = pedGo.AddComponent<SpriteRenderer>();
            Sprite pedSp = LoadSprite($"{PropsSourceDir}/Prop_Crew_Orientation_Pedestal.png");
            pedSr.sprite = pedSp;
            pedSr.sortingLayerName = "Obstacles";
            pedSr.sortingOrder = 2; // Under Tabletop at order 3
            if (defaultSpriteMat != null) pedSr.sharedMaterial = defaultSpriteMat;

            // 2. Tabletop (Beveled 184px diameter octagon with cardinal crew datapad terminals)
            GameObject tabletopGo = new GameObject("Tabletop");
            tabletopGo.transform.SetParent(visualRoot.transform, false);
            SpriteRenderer tableSr = tabletopGo.AddComponent<SpriteRenderer>();
            Sprite idleSp = LoadSprite($"{PropsSourceDir}/Prop_Crew_Orientation_Station_Idle.png");
            tableSr.sprite = idleSp;
            tableSr.sortingLayerName = "Obstacles";
            tableSr.sortingOrder = 3;
            if (defaultSpriteMat != null) tableSr.sharedMaterial = defaultSpriteMat;

            // 3. Hologram Projector
            GameObject holoGo = new GameObject("Hologram");
            holoGo.transform.SetParent(visualRoot.transform, false);
            holoGo.transform.localPosition = new Vector3(0f, 0.20f, 0f);
            holoGo.transform.localScale = new Vector3(1.20f, 1.20f, 1f);
            SpriteRenderer holoSs = holoGo.AddComponent<SpriteRenderer>();
            Sprite holoSp = LoadSprite($"{PropsSourceDir}/Prop_Crew_Orientation_Station_Hologram.png");
            holoSs.sprite = holoSp;
            holoSs.sortingLayerName = "Obstacles";
            holoSs.sortingOrder = 5;
            if (defaultSpriteMat != null) holoSs.sharedMaterial = defaultSpriteMat;
            holoGo.SetActive(false); // Initially hidden in Idle state

            // 4. Projector Light2D
            GameObject projLightGo = new GameObject("ProjectorLight");
            projLightGo.transform.SetParent(visualRoot.transform, false);
            projLightGo.transform.localPosition = new Vector3(0f, 0.05f, 0f);
            Light2D pLight = projLightGo.AddComponent<Light2D>();
            pLight.lightType = Light2D.LightType.Point;
            pLight.color = new Color(0.22f, 0.74f, 0.97f, 1f);
            pLight.intensity = 0.20f; // subtle standby pulse
            pLight.pointLightOuterRadius = 2.6f;
            pLight.pointLightInnerRadius = 0.25f;
            pLight.shadowsEnabled = false;

            // Attach CrewOrientationStation script
            TheLastSignal.Entities.Environment.CrewOrientationStation stationComp = stationGo.AddComponent<TheLastSignal.Entities.Environment.CrewOrientationStation>();
            stationComp.InitializeVisualComponents();

            // Save as Prefab
            const string prefabDir = "Assets/_Project/Prefabs/Environments/Bedroom";
            if (!Directory.Exists(prefabDir)) Directory.CreateDirectory(prefabDir);
            string prefabPath = $"{prefabDir}/Crew_Orientation_Station.prefab";
            PrefabUtility.SaveAsPrefabAssetAndConnect(stationGo, prefabPath, InteractionMode.AutomatedAction);

            // 4 Ergonomic Industrial Crew Stools (Positioned cardinally facing toward table)
            GameObject seatingRoot = new GameObject("SeatingRoot");
            seatingRoot.transform.SetParent(bedroomRoot.transform, false);

            string[] stoolNames = new string[] { "Stool_N", "Stool_S", "Stool_W", "Stool_E" };
            string[] stoolSprites = new string[] { "Prop_Crew_Orientation_Stool_N", "Prop_Crew_Orientation_Stool_S", "Prop_Crew_Orientation_Stool_W", "Prop_Crew_Orientation_Stool_E" };
            Vector3[] stoolPositions = new Vector3[]
            {
                new Vector3(-16.50f, 14.15f, 0f), // North Stool (Facing South toward table)
                new Vector3(-16.50f, 11.85f, 0f), // South Stool (Facing North toward table)
                new Vector3(-17.65f, 13.00f, 0f), // West Stool (Facing East toward table)
                new Vector3(-15.35f, 13.00f, 0f)  // East Stool (Facing West toward table)
            };
            for (int i = 0; i < stoolPositions.Length; i++)
            {
                GameObject stGo = CreateProp(seatingRoot, stoolSprites[i], stoolPositions[i], "Obstacles", 2, defaultSpriteMat, true, new Vector2(0.48f, 0.48f), Vector2.zero, Vector3.one, null, false);
                if (stGo != null) stGo.name = stoolNames[i];
            }

            // --- EAST ENTRANCE: REINFORCED HEAVY BULKHEAD SLIDING DOOR (ARCHITECTURAL PORTAL) ---
            GameObject doorGo = CreateProp(bedroomRoot, "Prop_Bedroom_Door_Bulkhead", new Vector3(-11.50f, 13.00f, 0f), "Obstacles", 4, defaultSpriteMat, true, new Vector2(1.00f, 2.00f), Vector2.zero, Vector3.one, null, false);
            if (doorGo != null)
            {
                doorGo.name = "Door_Bedroom";
                if (interactableLayer >= 0) doorGo.layer = interactableLayer;

                BoxCollider2D trig = doorGo.AddComponent<BoxCollider2D>();
                trig.isTrigger = true;
                trig.size = new Vector2(1.60f, 2.40f);

                TheLastSignal.Entities.Environment.Door doorComp = doorGo.AddComponent<TheLastSignal.Entities.Environment.Door>();
                Undo.RegisterCreatedObjectUndo(doorComp, "Add Door to Door_Bedroom");
            }

            // ========================================================
            // STEP 4: AMBIENT & PRACTICAL SCI-FI LIGHTING (COHESIVE PALETTE & DEPTH)
            // ========================================================
            // 0. Soft Ambient Room Fill Light (Deep eerie sci-fi indigo tone)
            GameObject ambLightGo = new GameObject("Light_Bedroom_Ambient_Fill");
            ambLightGo.transform.SetParent(bedroomRoot.transform);
            ambLightGo.transform.position = new Vector3(-16.50f, 13.00f, 0f);
            Light2D ambLight = ambLightGo.AddComponent<Light2D>();
            ambLight.lightType = Light2D.LightType.Point;
            ambLight.color = new Color(0.12f, 0.18f, 0.28f, 1.0f);
            ambLight.intensity = 0.58f;
            ambLight.pointLightOuterRadius = 11.0f;
            ambLight.pointLightInnerRadius = 3.5f;
            ambLight.shadowsEnabled = false;
            ambLight.enabled = true;

            // 1. Pod 01 Emergency Warning Red Light (NW - Dramatic, tuned to not overwhelm the room)
            GameObject redAlertGo = new GameObject("Light_Malfunction_Alarm");
            redAlertGo.transform.SetParent(bedroomRoot.transform);
            redAlertGo.transform.position = new Vector3(-19.00f, 15.10f, 0f);
            Light2D redLight = redAlertGo.AddComponent<Light2D>();
            redLight.lightType = Light2D.LightType.Point;
            redLight.color = new Color(1.0f, 0.10f, 0.10f, 1.0f);
            redLight.intensity = 1.35f; // Balanced from 2.40
            redLight.pointLightOuterRadius = 3.2f; // Contained to Pod 01
            redLight.pointLightInnerRadius = 0.35f;
            redLight.shadowsEnabled = false;
            redLight.enabled = true;

            // 2. Pod 02 Operational Ready Soft Cyan Light (NE)
            GameObject pod02LightGo = new GameObject("Light_Pod_02_Ready");
            pod02LightGo.transform.SetParent(bedroomRoot.transform);
            pod02LightGo.transform.position = new Vector3(-14.00f, 15.10f, 0f);
            Light2D pod02Light = pod02LightGo.AddComponent<Light2D>();
            pod02Light.lightType = Light2D.LightType.Point;
            pod02Light.color = new Color(0.25f, 0.75f, 0.90f, 1.0f);
            pod02Light.intensity = 0.85f;
            pod02Light.pointLightOuterRadius = 2.6f;
            pod02Light.pointLightInnerRadius = 0.25f;
            pod02Light.shadowsEnabled = false;
            pod02Light.enabled = true;

            // 3. Pod 03 Occupied Soft Cyan Lamp (SW)
            GameObject pod03LightGo = new GameObject("Light_Pod_03_Occupied");
            pod03LightGo.transform.SetParent(bedroomRoot.transform);
            pod03LightGo.transform.position = new Vector3(-19.00f, 11.20f, 0f);
            Light2D pod03Light = pod03LightGo.AddComponent<Light2D>();
            pod03Light.lightType = Light2D.LightType.Point;
            pod03Light.color = new Color(0.20f, 0.70f, 0.85f, 1.0f);
            pod03Light.intensity = 0.85f;
            pod03Light.pointLightOuterRadius = 2.6f;
            pod03Light.pointLightInnerRadius = 0.25f;
            pod03Light.shadowsEnabled = false;
            pod03Light.enabled = true;

            // 4. Pod 04 Standby Amber Glow (SE)
            GameObject pod04LightGo = new GameObject("Light_Pod_04_Standby");
            pod04LightGo.transform.SetParent(bedroomRoot.transform);
            pod04LightGo.transform.position = new Vector3(-14.00f, 11.20f, 0f);
            Light2D pod04Light = pod04LightGo.AddComponent<Light2D>();
            pod04Light.lightType = Light2D.LightType.Point;
            pod04Light.color = new Color(0.90f, 0.60f, 0.20f, 1.0f);
            pod04Light.intensity = 0.80f;
            pod04Light.pointLightOuterRadius = 2.5f;
            pod04Light.pointLightInnerRadius = 0.25f;
            pod04Light.shadowsEnabled = false;
            pod04Light.enabled = true;

            // 5. Central Crew Lounge Warm Light (Warm incandescent glow around briefing table)
            GameObject loungeLightGo = new GameObject("Light_Lounge_Warm");
            loungeLightGo.transform.SetParent(bedroomRoot.transform);
            loungeLightGo.transform.position = new Vector3(-16.50f, 13.00f, 0f);
            Light2D loungeLight = loungeLightGo.AddComponent<Light2D>();
            loungeLight.lightType = Light2D.LightType.Point;
            loungeLight.color = new Color(1.0f, 0.90f, 0.75f, 1.0f);
            loungeLight.intensity = 0.85f;
            loungeLight.pointLightOuterRadius = 3.4f;
            loungeLight.pointLightInnerRadius = 0.4f;
            loungeLight.shadowsEnabled = false;
            loungeLight.enabled = true;

            // 6. Observation Window Deep Cosmic Backlight (North Wall X = -17)
            GameObject obsLightGo = new GameObject("Light_Observation_Wall");
            obsLightGo.transform.SetParent(bedroomRoot.transform);
            obsLightGo.transform.position = new Vector3(-17.00f, 17.20f, 0f);
            Light2D obsLight = obsLightGo.AddComponent<Light2D>();
            obsLight.lightType = Light2D.LightType.Point;
            obsLight.color = new Color(0.18f, 0.45f, 0.85f, 1.0f);
            obsLight.intensity = 0.65f;
            obsLight.pointLightOuterRadius = 3.6f;
            obsLight.pointLightInnerRadius = 0.4f;
            obsLight.shadowsEnabled = false;
            obsLight.enabled = true;

            // 7. Climate Control Station Telemetry Glow (South Wall X = -17)
            GameObject climLightGo = new GameObject("Light_Climate_Station");
            climLightGo.transform.SetParent(bedroomRoot.transform);
            climLightGo.transform.position = new Vector3(-17.00f, 8.85f, 0f);
            Light2D climLight = climLightGo.AddComponent<Light2D>();
            climLight.lightType = Light2D.LightType.Point;
            climLight.color = new Color(0.18f, 0.85f, 0.75f, 1.0f);
            climLight.intensity = 0.35f; // Soft telemetry glow
            climLight.pointLightOuterRadius = 1.2f;
            climLight.pointLightInnerRadius = 0.15f;
            climLight.shadowsEnabled = false;

            // 8. Corridor Exit Wayfinding Light (Leading from Bedroom Door into Corridor X = -9.5, Y = 12.5)
            GameObject corrLightGo = new GameObject("Light_Bedroom_Corridor_Exit");
            corrLightGo.transform.SetParent(bedroomRoot.transform);
            corrLightGo.transform.position = new Vector3(-9.50f, 12.50f, 0f);
            Light2D corrLight = corrLightGo.AddComponent<Light2D>();
            corrLight.lightType = Light2D.LightType.Point;
            corrLight.color = new Color(0.35f, 0.55f, 0.80f, 1.0f);
            corrLight.intensity = 0.70f;
            corrLight.pointLightOuterRadius = 4.0f;
            corrLight.pointLightInnerRadius = 0.5f;
            corrLight.shadowsEnabled = false;

            foreach (var l in bedroomRoot.GetComponentsInChildren<Light2D>())
            {
                l.shadowsEnabled = false;
            }

            // Migrate player spawn points & active player to Bedroom
            MigratePlayerSpawnToBedroom();

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
        }

                [MenuItem("Tools/Facility/Migrate Player Spawn to Bedroom", false, 54)]
        public static void MigratePlayerSpawnToBedroom()
        {
            Vector3[] bedroomSpawns = new Vector3[]
            {
                new Vector3(-19.00f, 9.90f, 0f), // P1: Pod 03 (Clearance in front of pod)
                new Vector3(-14.00f, 13.60f, 0f), // P2: Pod 02 (Clearance in front of NE pod)
                new Vector3(-14.00f, 9.90f, 0f), // P3: Pod 04 (Clearance in front of SE pod)
                new Vector3(-16.50f, 11.20f, 0f)  // P4: Briefing table south front
            };

            // Update all PlayerSpawnPoint components
            TheLastSignal.Entities.Player.PlayerSpawnPoint[] spawnPoints = Object.FindObjectsByType<TheLastSignal.Entities.Player.PlayerSpawnPoint>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var sp in spawnPoints)
            {
                if (sp == null) continue;
                int slot = sp.SlotIndex;
                if (slot >= 0 && slot < bedroomSpawns.Length)
                {
                    sp.transform.position = bedroomSpawns[slot];
                    EditorUtility.SetDirty(sp.gameObject);
                }
            }

            // Update legacy SpawnPoint components if any
            TheLastSignal.Entities.Player.SpawnPoint[] legacySpawns = Object.FindObjectsByType<TheLastSignal.Entities.Player.SpawnPoint>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var lsp in legacySpawns)
            {
                if (lsp == null) continue;
                int pIdx = lsp.PlayerIndex;
                if (pIdx >= 0 && pIdx < bedroomSpawns.Length)
                {
                    lsp.transform.position = bedroomSpawns[pIdx];
                    EditorUtility.SetDirty(lsp.gameObject);
                }
            }

            // Move SpawnArea parent if present
            GameObject spawnArea = GameObject.Find("SpawnArea");
            if (spawnArea != null)
            {
                for (int i = 0; i < spawnArea.transform.childCount; i++)
                {
                    Transform child = spawnArea.transform.GetChild(i);
                    for (int s = 0; s < bedroomSpawns.Length; s++)
                    {
                        if (child.name.EndsWith((s + 1).ToString()))
                        {
                            child.position = bedroomSpawns[s];
                            EditorUtility.SetDirty(child.gameObject);
                            break;
                        }
                    }
                }
            }

            Debug.Log("<color=green>[BedroomEnvironmentSetup] Migrated Player Spawns to Bedroom!</color>");
        }

        [MenuItem("Tools/Facility/Capture Bedroom Screenshots", false, 53)]
        public static void CaptureBedroomScreenshots()
        {
            const string outputDir = "Assets/Screenshots";
            const string brainDir = "/Users/caodanh/.gemini/antigravity/brain/cc155396-9b60-4880-a38f-f7e01e5dde5f";
            if (!Directory.Exists(outputDir)) Directory.CreateDirectory(outputDir);

            var gLightObj = GameObject.Find("Global Light 2D");
            Light2D gLight = gLightObj != null ? gLightObj.GetComponent<Light2D>() : null;

            var camObj = new GameObject("TempBedroomCaptureCamera");
            var cam = camObj.AddComponent<Camera>();
            cam.orthographic = true;
            cam.transform.position = new Vector3(-17.00f, 13.00f, -10f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.04f, 0.05f, 0.07f, 1f);

            var urpData = camObj.AddComponent<UniversalAdditionalCameraData>();
            urpData.renderPostProcessing = false;

            // 1. Full Room Wide Shot - Illuminated
            cam.orthographicSize = 5.6f;
            cam.transform.position = new Vector3(-17.00f, 13.00f, -10f);
            var rt = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;

            if (gLight != null) { gLight.intensity = 0.95f; gLight.color = Color.white; }
            cam.Render();
            SaveRenderTexture(rt, Path.Combine(outputDir, "bedroom_v4_full_illuminated.png"), brainDir);
            SaveRenderTexture(rt, Path.Combine(outputDir, "bedroom_master_full_illuminated.png"), brainDir);

            // 2. Full Room Wide Shot - Atmospheric
            if (gLight != null) { gLight.intensity = 0.30f; gLight.color = new Color(0.60f, 0.75f, 0.95f, 1f); }
            cam.Render();
            SaveRenderTexture(rt, Path.Combine(outputDir, "bedroom_v4_full_atmospheric.png"), brainDir);
            SaveRenderTexture(rt, Path.Combine(outputDir, "bedroom_master_full_atmospheric.png"), brainDir);

            // Reset light for architectural clarity closeups
            if (gLight != null) { gLight.intensity = 0.95f; gLight.color = Color.white; }

            // 3. Standard Floor Panels Close-Up
            cam.transform.position = new Vector3(-17.00f, 11.00f, -10f);
            cam.orthographicSize = 1.8f;
            var rtStdFloor = new RenderTexture(1200, 1000, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rtStdFloor;
            cam.Render();
            SaveRenderTexture(rtStdFloor, Path.Combine(outputDir, "bedroom_close_floor_standard.png"), brainDir);

            // 4. Reinforced & Technical Floor Panels Close-Up
            cam.transform.position = new Vector3(-15.50f, 11.00f, -10f);
            cam.orthographicSize = 2.2f;
            var rtReinfFloor = new RenderTexture(1200, 1000, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rtReinfFloor;
            cam.Render();
            SaveRenderTexture(rtReinfFloor, Path.Combine(outputDir, "bedroom_close_floor_reinforced.png"), brainDir);

            // 5. Panoramic Observation Window & Upper Bulkhead Close-Up
            cam.transform.position = new Vector3(-16.50f, 16.80f, -10f);
            cam.orthographicSize = 2.2f;
            var rtWin = new RenderTexture(1400, 900, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rtWin;
            cam.Render();
            SaveRenderTexture(rtWin, Path.Combine(outputDir, "bedroom_v5_close_window.png"), brainDir);
            SaveRenderTexture(rtWin, Path.Combine(outputDir, "bedroom_close_wall_upper.png"), brainDir);

            // 6. Left Solid Bulkhead (West Wall, X = -22) Close-Up
            cam.transform.position = new Vector3(-20.50f, 13.00f, -10f);
            cam.orthographicSize = 2.8f;
            var rtLeftWall = new RenderTexture(1100, 1100, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rtLeftWall;
            cam.Render();
            SaveRenderTexture(rtLeftWall, Path.Combine(outputDir, "bedroom_close_wall_left.png"), brainDir);

            // 7. Right Bulkhead & Corridor Transition (East Wall, X = -12) Close-Up
            cam.transform.position = new Vector3(-12.50f, 13.00f, -10f);
            cam.orthographicSize = 2.6f;
            var rtRightWall = new RenderTexture(1100, 1100, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rtRightWall;
            cam.Render();
            SaveRenderTexture(rtRightWall, Path.Combine(outputDir, "bedroom_v5_close_door.png"), brainDir);
            SaveRenderTexture(rtRightWall, Path.Combine(outputDir, "bedroom_close_wall_right.png"), brainDir);

            // 8. Lower Service-Bulkhead (South Wall, Y = 8) Close-Up
            cam.transform.position = new Vector3(-17.00f, 9.80f, -10f);
            cam.orthographicSize = 2.2f;
            var rtLowerWall = new RenderTexture(1300, 900, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rtLowerWall;
            cam.Render();
            SaveRenderTexture(rtLowerWall, Path.Combine(outputDir, "bedroom_close_wall_lower.png"), brainDir);

            // 9. Wall-to-Floor Transition Close-Up
            cam.transform.position = new Vector3(-17.00f, 10.20f, -10f);
            cam.orthographicSize = 1.6f;
            var rtWallFloor = new RenderTexture(1100, 900, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rtWallFloor;
            cam.Render();
            SaveRenderTexture(rtWallFloor, Path.Combine(outputDir, "bedroom_close_wall_floor_transition.png"), brainDir);

            // 10. Sleeping Pod Platforms Close-Up
            cam.transform.position = new Vector3(-16.50f, 14.50f, -10f);
            cam.orthographicSize = 2.8f;
            var rtPods = new RenderTexture(1200, 1000, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rtPods;
            cam.Render();
            SaveRenderTexture(rtPods, Path.Combine(outputDir, "bedroom_v5_close_pods.png"), brainDir);
            SaveRenderTexture(rtPods, Path.Combine(outputDir, "bedroom_close_pod_platforms.png"), brainDir);

            // 11. Central Crew Orientation Station (Idle State)
            cam.transform.position = new Vector3(-16.50f, 13.00f, -10f);
            cam.orthographicSize = 1.8f;
            var rtLounge = new RenderTexture(1100, 1000, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rtLounge;

            var stationComp = Object.FindAnyObjectByType<TheLastSignal.Entities.Environment.CrewOrientationStation>();
            if (stationComp != null)
            {
                stationComp.SetVisualState(TheLastSignal.Entities.Environment.CrewOrientationStation.StationState.Idle);
            }
            cam.Render();
            SaveRenderTexture(rtLounge, Path.Combine(outputDir, "bedroom_close_lounge.png"), brainDir);
            SaveRenderTexture(rtLounge, Path.Combine(outputDir, "bedroom_close_lounge_master.png"), brainDir);
            SaveRenderTexture(rtLounge, Path.Combine(outputDir, "bedroom_close_lounge_refined.png"), brainDir);
            SaveRenderTexture(rtLounge, Path.Combine(outputDir, "bedroom_orientation_close_idle.png"), brainDir);

            // Detailed Close-Up of Tabletop (Idle state with 4 crew-position indicators)
            cam.transform.position = new Vector3(-16.50f, 13.00f, -10f);
            cam.orthographicSize = 1.15f;
            cam.Render();
            SaveRenderTexture(rtLounge, Path.Combine(outputDir, "bedroom_orientation_table_closeup.png"), brainDir);

            // Detailed Close-Up of Compact Pedestal & Floor Mounting
            cam.transform.position = new Vector3(-16.50f, 12.45f, -10f);
            cam.orthographicSize = 0.75f;
            cam.Render();
            SaveRenderTexture(rtLounge, Path.Combine(outputDir, "bedroom_orientation_pedestal_closeup.png"), brainDir);

            // Detailed Close-Up of a Single Industrial Stool (Seat cushion, rim, 3 legs, footrest & rubber caps)
            cam.transform.position = new Vector3(-16.50f, 12.12f, -10f);
            cam.orthographicSize = 0.55f;
            cam.Render();
            SaveRenderTexture(rtLounge, Path.Combine(outputDir, "bedroom_orientation_stool_single_closeup.png"), brainDir);

            // Detailed Close-Up showing all 4 Surrounding Stools
            cam.transform.position = new Vector3(-16.50f, 13.00f, -10f);
            cam.orthographicSize = 1.95f;
            cam.Render();
            SaveRenderTexture(rtLounge, Path.Combine(outputDir, "bedroom_orientation_stools_closeup.png"), brainDir);

            // Detailed Close-Up of Optical Projector Lens & Aperture
            cam.orthographicSize = 0.65f;
            cam.Render();
            SaveRenderTexture(rtLounge, Path.Combine(outputDir, "bedroom_orientation_projector_closeup.png"), brainDir);
            cam.orthographicSize = 1.8f;

            // Contextual [E] Interaction Prompt: Station in IDLE state
            // Test all 4 approach directions (South, North, West, East) to prove zero player/table overlap
            var playerGo = GameObject.FindWithTag("Player") ?? GameObject.Find("Player");
            Vector3 origPlayerPos = Vector3.zero;
            if (playerGo != null)
            {
                origPlayerPos = playerGo.transform.position;
            }

            var promptApproaches = new (string filename, Vector3 playerPos, Vector3 promptPos)[]
            {
                ("bedroom_orientation_prompt.png", new Vector3(-16.50f, 11.40f, 0f), new Vector3(-16.50f, 14.55f, 0f)),
                ("bedroom_orientation_prompt_south.png", new Vector3(-16.50f, 11.40f, 0f), new Vector3(-16.50f, 14.55f, 0f)),
                ("bedroom_orientation_prompt_north.png", new Vector3(-16.50f, 14.60f, 0f), new Vector3(-16.50f, 11.40f, 0f)),
                ("bedroom_orientation_prompt_west.png", new Vector3(-17.95f, 13.00f, 0f), new Vector3(-15.00f, 13.00f, 0f)),
                ("bedroom_orientation_prompt_east.png", new Vector3(-15.05f, 13.00f, 0f), new Vector3(-17.95f, 13.00f, 0f))
            };

            foreach (var app in promptApproaches)
            {
                if (playerGo != null) playerGo.transform.position = app.playerPos;

                GameObject worldPromptGo = new GameObject("TempWorldPrompt");
                worldPromptGo.transform.position = app.promptPos;
                Canvas worldPromptCanvas = worldPromptGo.AddComponent<Canvas>();
                worldPromptCanvas.renderMode = RenderMode.WorldSpace;
                worldPromptCanvas.worldCamera = cam;
                worldPromptCanvas.sortingLayerName = "Obstacles";
                worldPromptCanvas.sortingOrder = 50;

                RectTransform wpRt = worldPromptGo.GetComponent<RectTransform>();
                wpRt.sizeDelta = new Vector2(340, 48); // Wide enough for single-line primary prompt
                worldPromptGo.transform.localScale = new Vector3(0.0038f, 0.0038f, 1f);

                GameObject wpBg = new GameObject("Bg");
                wpBg.transform.SetParent(worldPromptGo.transform, false);
                UnityEngine.UI.Image wpImg = wpBg.AddComponent<UnityEngine.UI.Image>();
                wpImg.color = new Color(0.06f, 0.09f, 0.13f, 0.94f);
                RectTransform bgRt = wpBg.GetComponent<RectTransform>();
                bgRt.anchorMin = Vector2.zero;
                bgRt.anchorMax = Vector2.one;
                bgRt.sizeDelta = Vector2.zero;
                UnityEngine.UI.Outline wpOutline = wpBg.AddComponent<UnityEngine.UI.Outline>();
                wpOutline.effectColor = new Color(0.22f, 0.74f, 0.97f, 0.90f);
                wpOutline.effectDistance = new Vector2(2, -2);

                GameObject wpTxtGo = new GameObject("Text");
                wpTxtGo.transform.SetParent(wpBg.transform, false);
                TMPro.TextMeshProUGUI wpTxt = wpTxtGo.AddComponent<TMPro.TextMeshProUGUI>();
                wpTxt.text = "<b>[ E ] ACCESS CREW ORIENTATION</b>\n<size=75%><color=#7DD3FC>CREW MUSTER & ORIENTATION STATION</color></size>";
                wpTxt.alignment = TMPro.TextAlignmentOptions.Center;
                wpTxt.fontSize = 14.5f;
                wpTxt.color = Color.white;
                RectTransform txtRt = wpTxtGo.GetComponent<RectTransform>();
                txtRt.anchorMin = Vector2.zero;
                txtRt.anchorMax = Vector2.one;
                txtRt.sizeDelta = Vector2.zero;

                cam.transform.position = new Vector3(-16.50f, 13.00f, -10f);
                cam.orthographicSize = 2.45f;
                cam.Render();
                SaveRenderTexture(rtLounge, Path.Combine(outputDir, app.filename), brainDir);

                Object.DestroyImmediate(worldPromptGo);
            }

            if (playerGo != null) playerGo.transform.position = origPlayerPos;

            // Active State Close-Up with Holographic Projection
            if (stationComp != null)
            {
                stationComp.SetVisualState(TheLastSignal.Entities.Environment.CrewOrientationStation.StationState.Active);
            }
            cam.transform.position = new Vector3(-16.50f, 13.00f, -10f);
            cam.orthographicSize = 1.95f;
            cam.Render();
            SaveRenderTexture(rtLounge, Path.Combine(outputDir, "bedroom_orientation_close_active.png"), brainDir);

            // Detailed Close-Up of Active Hologram
            cam.orthographicSize = 1.15f;
            cam.Render();
            SaveRenderTexture(rtLounge, Path.Combine(outputDir, "bedroom_orientation_hologram_closeup.png"), brainDir);
            cam.orthographicSize = 1.8f;

            // Completed State Close-Up
            if (stationComp != null)
            {
                stationComp.SetVisualState(TheLastSignal.Entities.Environment.CrewOrientationStation.StationState.Completed);
            }
            cam.Render();
            SaveRenderTexture(rtLounge, Path.Combine(outputDir, "bedroom_orientation_close_completed.png"), brainDir);

            // Reset back to Idle for gameplay
            if (stationComp != null)
            {
                stationComp.SetVisualState(TheLastSignal.Entities.Environment.CrewOrientationStation.StationState.Idle);
            }

            // Tutorial UI Full Modal Interface Capture (All 5 Chapters & Multi-resolution)
            var tutUI = Object.FindAnyObjectByType<TheLastSignal.UI.CrewOrientationTutorialUI>();
            if (tutUI == null)
            {
                GameObject tutHost = new GameObject("CrewOrientationTutorialUI_Host");
                tutUI = tutHost.AddComponent<TheLastSignal.UI.CrewOrientationTutorialUI>();
            }
            if (tutUI != null)
            {
                // Hide any sibling gameplay HUD elements so they don't bleed through
                var disabledSiblings = new System.Collections.Generic.List<GameObject>();
                string[] hideNames = { "ObjectiveText", "SessionStatusText", "CountdownText", "InteractionPrompt_Container", "SimpleObjectiveView", "Apex_Terminal_Panel" };
                foreach (var hName in hideNames)
                {
                    var sObj = GameObject.Find(hName);
                    if (sObj != null && sObj.activeSelf)
                    {
                        sObj.SetActive(false);
                        disabledSiblings.Add(sObj);
                    }
                }

                // Rebuild UI to ensure latest layout, icons, and dividers are generated
                tutUI.RebuildUI();

                Transform panelTr = null;
                var allPanels = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                foreach (var p in allPanels)
                {
                    if (p.name == "Crew_Orientation_Panel") { panelTr = p; break; }
                }

                if (panelTr != null)
                {
                    panelTr.gameObject.SetActive(true);
                    Canvas canvas = panelTr.GetComponentInParent<Canvas>();
                    RenderMode origMode = canvas != null ? canvas.renderMode : RenderMode.ScreenSpaceOverlay;
                    UnityEngine.Camera origWorldCam = canvas != null ? canvas.worldCamera : null;

                    var uiCamGo = new GameObject("TempUICaptureCamera");
                    var uiCam = uiCamGo.AddComponent<Camera>();
                    uiCam.clearFlags = CameraClearFlags.SolidColor;
                    uiCam.backgroundColor = new Color(0.02f, 0.04f, 0.06f, 1f);

                    if (canvas != null)
                    {
                        canvas.renderMode = RenderMode.ScreenSpaceCamera;
                        canvas.worldCamera = uiCam;
                        canvas.planeDistance = 10f;
                    }

                    // 1. Capture All 5 Individual Chapters (1920x1080)
                    for (int ch = 0; ch < 5; ch++)
                    {
                        tutUI.SetProgressStateExplicit(ch, false);
                        Canvas.ForceUpdateCanvases();

                        var rtCh = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32);
                        uiCam.targetTexture = rtCh;
                        uiCam.Render();

                        string chFileName = $"bedroom_orientation_tutorial_ch0{ch + 1}.png";
                        SaveRenderTexture(rtCh, Path.Combine(outputDir, chFileName), brainDir);

                        if (ch == 0)
                        {
                            SaveRenderTexture(rtCh, Path.Combine(outputDir, "bedroom_orientation_tutorial_ui.png"), brainDir);
                        }

                        uiCam.targetTexture = null;
                        rtCh.Release();
                        Object.DestroyImmediate(rtCh);
                    }

                    // 2. Capture the 3 Explicit States: IN PROGRESS, READY TO COMPLETE, COMPLETED
                    // State 01: IN PROGRESS (Chapters 1-3 reviewed, 4-5 pending)
                    tutUI.SetProgressStateExplicit(2, false);
                    Canvas.ForceUpdateCanvases();
                    var rtState1 = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32);
                    uiCam.targetTexture = rtState1;
                    uiCam.Render();
                    SaveRenderTexture(rtState1, Path.Combine(outputDir, "bedroom_tutorial_state_inprogress.png"), brainDir);
                    uiCam.targetTexture = null;
                    rtState1.Release();
                    Object.DestroyImmediate(rtState1);

                    // State 02: READY TO COMPLETE (All 5 reviewed, awaiting explicit confirmation)
                    tutUI.SetProgressStateExplicit(4, false);
                    Canvas.ForceUpdateCanvases();
                    var rtState2 = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32);
                    uiCam.targetTexture = rtState2;
                    uiCam.Render();
                    SaveRenderTexture(rtState2, Path.Combine(outputDir, "bedroom_tutorial_state_ready.png"), brainDir);
                    uiCam.targetTexture = null;
                    rtState2.Release();
                    Object.DestroyImmediate(rtState2);

                    // State 03: COMPLETED (Explicitly completed, READY FOR DUTY)
                    tutUI.SetProgressStateExplicit(4, true);
                    Canvas.ForceUpdateCanvases();
                    var rtState3 = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32);
                    uiCam.targetTexture = rtState3;
                    uiCam.Render();
                    SaveRenderTexture(rtState3, Path.Combine(outputDir, "bedroom_tutorial_state_completed.png"), brainDir);
                    uiCam.targetTexture = null;
                    rtState3.Release();
                    Object.DestroyImmediate(rtState3);

                    // 3. Multi-Resolution Tests (1920x1080, 1600x900, 1366x768)
                    tutUI.SetProgressStateExplicit(0, false);
                    Canvas.ForceUpdateCanvases();

                    int[][] testResolutions = new int[][]
                    {
                        new int[] { 1920, 1080 },
                        new int[] { 1600, 900 },
                        new int[] { 1366, 768 }
                    };

                    foreach (var res in testResolutions)
                    {
                        var rtRes = new RenderTexture(res[0], res[1], 24, RenderTextureFormat.ARGB32);
                        uiCam.targetTexture = rtRes;
                        uiCam.Render();
                        string resFileName = $"bedroom_tutorial_res_{res[0]}x{res[1]}.png";
                        SaveRenderTexture(rtRes, Path.Combine(outputDir, resFileName), brainDir);
                        uiCam.targetTexture = null;
                        rtRes.Release();
                        Object.DestroyImmediate(rtRes);
                    }

                    if (canvas != null)
                    {
                        canvas.renderMode = origMode;
                        canvas.worldCamera = origWorldCam;
                    }
                    panelTr.gameObject.SetActive(false);
                    uiCam.targetTexture = null;
                    Object.DestroyImmediate(uiCamGo);

                    // Restore disabled siblings
                    foreach (var sObj in disabledSiblings)
                    {
                        if (sObj != null) sObj.SetActive(true);
                    }
                }
            }

            // Reset station back to Idle
            if (stationComp != null)
            {
                stationComp.SetVisualState(TheLastSignal.Entities.Environment.CrewOrientationStation.StationState.Idle);
            }

            // 12. Structural Corners Close-Up (All 4 Corners: TL, TR, BL, BR)
            cam.orthographicSize = 1.6f;
            var rtCorners = new RenderTexture(1000, 1000, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rtCorners;

            // TL (Top-Left)
            cam.transform.position = new Vector3(-21.20f, 16.30f, -10f);
            cam.Render();
            SaveRenderTexture(rtCorners, Path.Combine(outputDir, "bedroom_close_corner_tl.png"), brainDir);
            SaveRenderTexture(rtCorners, Path.Combine(outputDir, "bedroom_close_corners.png"), brainDir);

            // TR (Top-Right)
            cam.transform.position = new Vector3(-12.80f, 16.30f, -10f);
            cam.Render();
            SaveRenderTexture(rtCorners, Path.Combine(outputDir, "bedroom_close_corner_tr.png"), brainDir);

            // BL (Bottom-Left)
            cam.transform.position = new Vector3(-21.20f, 8.70f, -10f);
            cam.Render();
            SaveRenderTexture(rtCorners, Path.Combine(outputDir, "bedroom_close_corner_bl.png"), brainDir);

            // BR (Bottom-Right)
            cam.transform.position = new Vector3(-12.80f, 8.70f, -10f);
            cam.Render();
            SaveRenderTexture(rtCorners, Path.Combine(outputDir, "bedroom_close_corner_br.png"), brainDir);

            // Additional Pod close-ups:
            // Pod 03 SW Crew Belongings & Personal Mat
            cam.transform.position = new Vector3(-19.30f, 11.30f, -10f);
            cam.orthographicSize = 1.9f;
            var rtPod03 = new RenderTexture(1000, 1000, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rtPod03;
            cam.Render();
            SaveRenderTexture(rtPod03, Path.Combine(outputDir, "bedroom_v5_close_pod03_mat.png"), brainDir);
            SaveRenderTexture(rtPod03, Path.Combine(outputDir, "bedroom_close_pod_sw.png"), brainDir);

            // Pod 01 NW (Cryo breach)
            cam.transform.position = new Vector3(-19.00f, 14.80f, -10f);
            cam.orthographicSize = 1.9f;
            var rtPod01 = new RenderTexture(1000, 1000, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rtPod01;
            cam.Render();
            SaveRenderTexture(rtPod01, Path.Combine(outputDir, "bedroom_close_pod_nw.png"), brainDir);

            // Pod 02 NE (Upper right)
            cam.transform.position = new Vector3(-14.00f, 14.80f, -10f);
            cam.orthographicSize = 1.9f;
            var rtPod02 = new RenderTexture(1000, 1000, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rtPod02;
            cam.Render();
            SaveRenderTexture(rtPod02, Path.Combine(outputDir, "bedroom_close_pod_ne.png"), brainDir);

            // Pod 04 SE (Mechanic locker & tools)
            cam.transform.position = new Vector3(-14.00f, 11.30f, -10f);
            cam.orthographicSize = 1.9f;
            var rtPod04 = new RenderTexture(1000, 1000, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rtPod04;
            cam.Render();
            SaveRenderTexture(rtPod04, Path.Combine(outputDir, "bedroom_close_pod_se.png"), brainDir);

            // Clean up
            cam.targetTexture = null;
            RenderTexture.active = null;
            rt.Release();
            rtStdFloor.Release();
            rtReinfFloor.Release();
            rtPods.Release();
            rtWin.Release();
            rtLeftWall.Release();
            rtRightWall.Release();
            rtLowerWall.Release();
            rtWallFloor.Release();
            rtPod03.Release();
            rtPod01.Release();
            rtPod02.Release();
            rtPod04.Release();
            rtLounge.Release();
            rtCorners.Release();
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(rtStdFloor);
            Object.DestroyImmediate(rtReinfFloor);
            Object.DestroyImmediate(rtPods);
            Object.DestroyImmediate(rtWin);
            Object.DestroyImmediate(rtLeftWall);
            Object.DestroyImmediate(rtRightWall);
            Object.DestroyImmediate(rtLowerWall);
            Object.DestroyImmediate(rtWallFloor);
            Object.DestroyImmediate(rtPod03);
            Object.DestroyImmediate(rtPod01);
            Object.DestroyImmediate(rtPod02);
            Object.DestroyImmediate(rtPod04);
            Object.DestroyImmediate(rtLounge);
            Object.DestroyImmediate(rtCorners);
            Object.DestroyImmediate(camObj);

            Debug.Log("<color=green>[BedroomEnvironmentSetup] Captured all 12 Bedroom Architectural & Visual Master Screenshots!</color>");
        }

        private static void SaveRenderTexture(RenderTexture rt, string filePath, string secondaryDir = null)
        {
            RenderTexture.active = rt;
            Texture2D tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            byte[] bytes = tex.EncodeToPNG();
            File.WriteAllBytes(filePath, bytes);

            string projectScreenshotsDir = "Assets/_Project/Screenshots/Bedroom";
            if (!Directory.Exists(projectScreenshotsDir)) Directory.CreateDirectory(projectScreenshotsDir);
            string projectPath = Path.Combine(projectScreenshotsDir, Path.GetFileName(filePath));
            File.WriteAllBytes(projectPath, bytes);

            if (!string.IsNullOrEmpty(secondaryDir) && Directory.Exists(secondaryDir))
            {
                string secondaryPath = Path.Combine(secondaryDir, Path.GetFileName(filePath));
                File.WriteAllBytes(secondaryPath, bytes);
            }
            Object.DestroyImmediate(tex);
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
