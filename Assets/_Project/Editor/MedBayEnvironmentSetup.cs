#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Tilemaps;
using TheLastSignal.Entities.Environment;

namespace TheLastSignal.EditorTools
{
    public static class MedBayEnvironmentSetup
    {
        private const string TilesSourceDir = "Assets/_Project/Sprites/Environments/MedBay/Tiles";
        private const string PropsSourceDir = "Assets/_Project/Sprites/Environments/MedBay/Props";
        private const string TileAssetsDir = "Assets/_Project/Tiles/MedBay";

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
                    SetupMedBayArt();
                }
            };
        }

        [MenuItem("Tools/Facility/Setup MedBay Art & Environment", false, 51)]
        public static void SetupMedBayArt()
        {
            // 1. Configure and import Tile textures
            ConfigureTileTextures();

            // 2. Configure and import Prop textures (PPU = 128)
            ConfigurePropTextures();

            // 3. Create Tile ScriptableObjects
            CreateTileAssets();

            // 4. Decorate Med_Bay in the active scene
            DecorateActiveScene();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("<color=green>[MedBayEnvironmentSetup] Successfully configured Sci-Fi Horror MedBay tiles, props, atmospheric lighting, and clean pathways!</color>");
        }

        public static void ConfigureTileTextures()
        {
            if (!Directory.Exists(TilesSourceDir)) return;

            string[] files = Directory.GetFiles(TilesSourceDir, "*.png");
            foreach (string file in files)
            {
                if (file.Contains("Showcase_")) continue;
                TextureImporter importer = AssetImporter.GetAtPath(file) as TextureImporter;
                if (importer != null)
                {
                    bool changed = false;
                    if (importer.textureType != TextureImporterType.Sprite) { importer.textureType = TextureImporterType.Sprite; changed = true; }
                    if (importer.spriteImportMode != SpriteImportMode.Single) { importer.spriteImportMode = SpriteImportMode.Single; changed = true; }
                    if (importer.spritePixelsPerUnit != 128f) { importer.spritePixelsPerUnit = 128f; changed = true; }
                    if (importer.filterMode != FilterMode.Point) { importer.filterMode = FilterMode.Point; changed = true; }
                    if (importer.textureCompression != TextureImporterCompression.Uncompressed) { importer.textureCompression = TextureImporterCompression.Uncompressed; changed = true; }

                    // Fix black/white borders between tiles
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
                if (file.Contains("Showcase_")) continue;
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

                    // Fix white borders on props
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
            }

            string[] files = Directory.GetFiles(TilesSourceDir, "*.png");
            foreach (string file in files)
            {
                if (file.Contains("Showcase_")) continue;
                string tileName = Path.GetFileNameWithoutExtension(file);
                string assetPath = $"{TileAssetsDir}/{tileName}.asset";

                Tile tile = AssetDatabase.LoadAssetAtPath<Tile>(assetPath);
                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(file);

                if (sprite == null) continue;

                if (tile == null)
                {
                    tile = ScriptableObject.CreateInstance<Tile>();
                    tile.sprite = sprite;
                    tile.name = tileName;
                    AssetDatabase.CreateAsset(tile, assetPath);
                }
                else
                {
                    tile.sprite = sprite;
                    EditorUtility.SetDirty(tile);
                }
            }
        }

        public static void DecorateActiveScene()
        {
            GameObject gridGo = GameObject.Find("Facility_Grid") ?? GameObject.Find("Grid");
            Grid grid = gridGo != null ? gridGo.GetComponent<Grid>() : Object.FindAnyObjectByType<Grid>();

            if (grid == null)
            {
                Debug.LogError("[MedBayEnvironmentSetup] Grid (Facility_Grid) not found in the active scene!");
                return;
            }

            Tilemap floorMap = null;
            Tilemap wallMap = null;
            Tilemap[] allTilemaps = grid.GetComponentsInChildren<Tilemap>(true);
            foreach (Tilemap tm in allTilemaps)
            {
                if (tm.name == "Floor_Tilemap" || tm.name == "Floor")
                {
                    floorMap = tm;
                }
                else if (tm.name == "Wall_Tilemap" || tm.name == "Walls")
                {
                    wallMap = tm;
                }
            }

            if (floorMap == null && allTilemaps.Length > 0)
            {
                floorMap = allTilemaps[0];
            }

            if (floorMap == null)
            {
                Debug.LogError("[MedBayEnvironmentSetup] Floor Tilemap not found!");
                return;
            }

            // --- LOAD FLOOR TILES ---
            Tile fClean = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/MedBay_Floor_Clean.asset");
            Tile fWhite = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/MedBay_Floor_White.asset");
            Tile fDark = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/MedBay_Floor_Dark.asset");
            Tile fGrate = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/MedBay_Floor_Grate.asset");
            Tile fDiamond = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/MedBay_Floor_Diamond.asset");
            Tile fHatch = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/MedBay_Floor_Subfloor_Hatch.asset");
            Tile fR2C5 = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/MedBay_Floor_R2C5.asset");
            Tile fTrans = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/MedBay_Floor_Transition_Strip.asset");

            Tile cTL = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/MedBay_Floor_Containment_TL.asset");
            Tile cT = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/MedBay_Floor_Containment_T.asset");
            Tile cTLabel = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/MedBay_Floor_Containment_T_Label.asset");
            Tile cTR = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/MedBay_Floor_Containment_TR.asset");
            Tile cL = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/MedBay_Floor_Containment_L.asset");
            Tile cR = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/MedBay_Floor_Containment_R.asset");
            Tile cBL = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/MedBay_Floor_Containment_BL.asset");
            Tile cB = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/MedBay_Floor_Containment_B.asset");
            Tile cBR = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/MedBay_Floor_Containment_BR.asset");

            Tile sNW = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/MedBay_Floor_Spill_NW.asset");
            Tile sSE = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/MedBay_Floor_Spill_SE.asset");

            // --- LOAD WALL TILES ---
            Tile wBulkhead = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/MedBay_Wall_Bulkhead.asset");
            Tile wPipeTop = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/MedBay_Wall_Pipes_Top.asset");
            Tile wPipeBot = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/MedBay_Wall_Pipes_Bottom.asset");
            Tile wPipeV = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/MedBay_Wall_Pipes_V.asset");
            Tile wPipeVEast = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/MedBay_Wall_Pipes_V_East.asset") ?? wPipeV;
            // Original vertical pipe tiles (for West/East walls)
            Tile wO2 = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/MedBay_Wall_Oxygen_Manifold.asset");
            Tile wPipesLower = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/MedBay_Wall_Pipes_Lower.asset");
            // Rotated horizontal pipe tiles (for North/South walls) — R3C3 and R4C3 rotated 90° CW
            Tile wO2H = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/MedBay_Wall_Oxygen_Manifold_H.asset");
            Tile wPipesLowerH = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/MedBay_Wall_Pipes_Lower_H.asset");
            Tile wCross = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/MedBay_Wall_RedCross.asset");
            Tile wCRT = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/MedBay_Wall_CRT_Monitor.asset");
            Tile wTerminal = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/MedBay_Wall_Terminal.asset");
            Tile wClaw = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/MedBay_Wall_Breached_Claw.asset");
            Tile wAutopsy = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/MedBay_Wall_Autopsy.asset");
            Tile wWindow = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/MedBay_Wall_Window.asset");
            Tile wLocker = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/MedBay_Wall_Locker.asset");

            // --- 4 SQUARE CORNER TILES ---
            Tile wCornerTL = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/MedBay_Wall_Corner_TL.asset");
            Tile wCornerTR = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/MedBay_Wall_Corner_TR.asset");
            Tile wCornerBL = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/MedBay_Wall_Corner_BL.asset");
            Tile wCornerBR = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/MedBay_Wall_Corner_BR.asset");

            // ==========================================
            // 0. CLEAR OUTSIDE BORDER TILES (Void perimeter only, preserving corridors)
            // ==========================================
            Undo.RecordObjects(new UnityEngine.Object[] { floorMap, wallMap }, "Clear MedBay Outside Border Tiles");
            // Clear Northwest void above West corridor bend (X in [-30, -21], Y in [27, 35])
            for (int ox = -30; ox <= -21; ox++)
            {
                for (int oy = 27; oy <= 35; oy++)
                {
                    // Do not touch MedBay West wall at X = -21
                    if (ox == -21) continue;
                    Vector3Int pos = new Vector3Int(ox, oy, 0);
                    floorMap.SetTile(pos, null);
                    if (wallMap != null) wallMap.SetTile(pos, null);
                }
            }
            // Clear Far West void (X in [-35, -27], Y in [18, 26])
            for (int ox = -35; ox <= -27; ox++)
            {
                for (int oy = 18; oy <= 26; oy++)
                {
                    Vector3Int pos = new Vector3Int(ox, oy, 0);
                    floorMap.SetTile(pos, null);
                    if (wallMap != null) wallMap.SetTile(pos, null);
                }
            }

            // ==========================================
            // 1. PAINT INTERIOR FLOOR: X in [-20, -12], Y in [24, 31]
            // ==========================================
            Undo.RecordObject(floorMap, "Decorate MedBay Floor");

            for (int x = -20; x <= -12; x++)
            {
                for (int y = 24; y <= 31; y++)
                {
                    Vector3Int pos = new Vector3Int(x, y, 0);

                    // Row 31: Diamond-plate medical service floor
                    if (y == 31)
                    {
                        floorMap.SetTile(pos, fR2C5 ?? fWhite ?? fClean);
                    }
                    // Row 30: Physical expansion joint threshold transition
                    else if (y == 30)
                    {
                        floorMap.SetTile(pos, fTrans ?? fClean);
                    }
                    // Clean Restrained Containment Perimeter around Autopsy Table (X in [-18, -15], Y in [26, 28])
                    // Top row:
                    else if (y == 28 && x == -18) floorMap.SetTile(pos, cTL ?? fDark);
                    else if (y == 28 && x == -17) floorMap.SetTile(pos, cT ?? fDark);
                    else if (y == 28 && x == -16) floorMap.SetTile(pos, cTLabel ?? fDark); // Single SPECIMEN CONTAINMENT label
                    else if (y == 28 && x == -15) floorMap.SetTile(pos, cTR ?? fDark);
                    // Mid row:
                    else if (y == 27 && x == -18) floorMap.SetTile(pos, cL ?? fDark);
                    else if (y == 27 && (x == -17 || x == -16)) floorMap.SetTile(pos, fGrate ?? fDark); // Drainage grates under table
                    else if (y == 27 && x == -15) floorMap.SetTile(pos, cR ?? fDark);
                    // Bottom row:
                    else if (y == 26 && x == -18) floorMap.SetTile(pos, cBL ?? fDark);
                    else if (y == 26 && (x == -17 || x == -16)) floorMap.SetTile(pos, cB ?? fDark);
                    else if (y == 26 && x == -15) floorMap.SetTile(pos, cBR ?? fDark);
                    // Unique Organic Stains:
                    else if (x == -19 && y == 28) floorMap.SetTile(pos, sNW ?? fDark); // NW chemical spill
                    else if (x == -13 && y == 25) floorMap.SetTile(pos, sSE ?? fDark); // SE drag mark
                    // Sub-floor conduit hatch in NW area:
                    else if (x == -19 && y == 27) floorMap.SetTile(pos, fHatch ?? fDiamond ?? fClean);
                    // Heavy diamond tread plate at East entrance doorway:
                    else if (x == -12 && (y == 27 || y == 28)) floorMap.SetTile(pos, fDiamond ?? fClean);
                    // Heavy diamond tread plate at West corridor doorway:
                    else if (x == -20 && (y == 24 || y == 25)) floorMap.SetTile(pos, fDiamond ?? fClean);
                    // Default modular steel floor:
                    else
                    {
                        floorMap.SetTile(pos, fDark ?? fClean);
                    }
                }
            }

            // Doorway threshold tiles:
            // East entrance to Corridor_MedBay_NodeA (X = -11, Y in [27, 28])
            floorMap.SetTile(new Vector3Int(-11, 27, 0), fDiamond ?? fClean);
            floorMap.SetTile(new Vector3Int(-11, 28, 0), fDiamond ?? fClean);

            // West entrance to Loop_West (X = -21, Y in [24, 25])
            floorMap.SetTile(new Vector3Int(-21, 24, 0), fDiamond ?? fClean);
            floorMap.SetTile(new Vector3Int(-21, 25, 0), fDiamond ?? fClean);

            // West Corridor Bend (Loop_West_Bend_North) connection guarantee
            for (int bx = -23; bx <= -22; bx++)
            {
                floorMap.SetTile(new Vector3Int(bx, 24, 0), fDiamond ?? fClean);
                floorMap.SetTile(new Vector3Int(bx, 25, 0), fDiamond ?? fClean);
            }

            // ==========================================
            // 2. PAINT PERIMETER WALLS & 4 SQUARE CORNERS
            // R4C3 (Pipes Lower) = straight pipe runs for all walls
            //   - Horizontal walls (N/S): R4C3 rotated 90° CW → pipes nằm ngang
            //   - Vertical walls (W/E):   R4C3 original → pipes chạy dọc
            // R3C3 (Oxygen Manifold) = junction piece at 4 corners to connect wall rows
            // ==========================================
            if (wallMap != null)
            {
                Undo.RecordObject(wallMap, "Decorate MedBay Walls");

                // Horizontal pipe tile (R4C3 rotated), fallback to original if not yet created
                Tile hPipe = wPipesLowerH ?? wPipesLower;

                // --- NORTH WALL: Y = 32, X in [-20, -12] ---
                for (int x = -20; x <= -12; x++)
                {
                    wallMap.SetTile(new Vector3Int(x, 32, 0), hPipe ?? wBulkhead);
                }

                // --- SOUTH WALL: Y = 23, X in [-20, -12] ---
                for (int x = -20; x <= -12; x++)
                {
                    wallMap.SetTile(new Vector3Int(x, 23, 0), hPipe ?? wBulkhead);
                }

                // --- WEST WALL: X = -21, Y in [26, 31] ---
                // All R4C3 original (vertical pipes), preserving decal Window at Y = 28
                wallMap.SetTile(new Vector3Int(-21, 31, 0), wPipesLower ?? wBulkhead);
                wallMap.SetTile(new Vector3Int(-21, 30, 0), wPipesLower ?? wBulkhead);
                wallMap.SetTile(new Vector3Int(-21, 29, 0), wPipesLower ?? wBulkhead);
                wallMap.SetTile(new Vector3Int(-21, 28, 0), wWindow ?? wPipesLower ?? wBulkhead); // Window decal
                wallMap.SetTile(new Vector3Int(-21, 27, 0), wPipesLower ?? wBulkhead);
                wallMap.SetTile(new Vector3Int(-21, 26, 0), wPipesLower ?? wBulkhead);
                // Door opening (Y in [24, 25])
                wallMap.SetTile(new Vector3Int(-21, 24, 0), null);
                wallMap.SetTile(new Vector3Int(-21, 25, 0), null);

                // --- EAST WALL: X = -11, Y in [24, 31] except doorway [27, 28] ---
                // All R4C3 original (vertical pipes), preserving decal Autopsy at Y = 31
                wallMap.SetTile(new Vector3Int(-11, 31, 0), wAutopsy ?? wPipesLower ?? wBulkhead); // Autopsy decal
                wallMap.SetTile(new Vector3Int(-11, 30, 0), wPipesLower ?? wBulkhead);
                wallMap.SetTile(new Vector3Int(-11, 29, 0), wPipesLower ?? wBulkhead);
                // Door opening (Y in [27, 28])
                wallMap.SetTile(new Vector3Int(-11, 27, 0), null);
                wallMap.SetTile(new Vector3Int(-11, 28, 0), null);
                wallMap.SetTile(new Vector3Int(-11, 26, 0), wPipesLower ?? wBulkhead);
                wallMap.SetTile(new Vector3Int(-11, 25, 0), wPipesLower ?? wBulkhead);
                wallMap.SetTile(new Vector3Int(-11, 24, 0), wPipesLower ?? wBulkhead);

                // --- 4 CORNERS: Proper 90-degree pipe corner turns connecting wall pipe runs ---
                wallMap.SetTile(new Vector3Int(-21, 32, 0), wCornerTL ?? wBulkhead); // Top-Left
                wallMap.SetTile(new Vector3Int(-11, 32, 0), wCornerTR ?? wBulkhead); // Top-Right
                wallMap.SetTile(new Vector3Int(-21, 23, 0), wCornerBL ?? wBulkhead); // Bottom-Left
                wallMap.SetTile(new Vector3Int(-11, 23, 0), wCornerBR ?? wBulkhead); // Bottom-Right

                // --- WEST CORRIDOR BEND WALLS GUARANTEE ---
                for (int bx = -23; bx <= -22; bx++)
                {
                    wallMap.SetTile(new Vector3Int(bx, 26, 0), hPipe ?? wBulkhead);
                    wallMap.SetTile(new Vector3Int(bx, 23, 0), hPipe ?? wBulkhead);
                }
            }

            // ==========================================
            // 3. CONFIGURE BOTH DOORS (EAST & WEST)
            // ==========================================
            Material defaultSpriteMat = AssetDatabase.LoadAssetAtPath<Material>("Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Lit-Default.mat");
            if (defaultSpriteMat == null)
            {
                Shader litShader = Shader.Find("Universal Render Pipeline/2D/Sprite-Lit-Default");
                if (litShader != null) defaultSpriteMat = new Material(litShader);
            }

            Sprite doorVClosed = LoadSprite($"{PropsSourceDir}/Door_MedBay_Vertical_Closed.png");
            Sprite doorVOpen = LoadSprite($"{PropsSourceDir}/Door_MedBay_Vertical_Open.png");
            Sprite lightRed = LoadSprite($"{PropsSourceDir}/Door_MedBay_Light_Red.png");
            Sprite lightGreen = LoadSprite($"{PropsSourceDir}/Door_MedBay_Light_Green.png");

            // Clean up any old MedBay decorations or stray old props/indicators/crates
            GameObject oldCrate = GameObject.Find("Obstacle_MedBay_Crate");
            if (oldCrate != null) Undo.DestroyObjectImmediate(oldCrate);

            GameObject[] sceneObjects = Object.FindObjectsByType<GameObject>();
            foreach (GameObject go in sceneObjects)
            {
                if (go == null) continue;
                if (go.name == "MedBay_Decorations" ||
                    go.name == "Obstacle_MedBay_Crate" ||
                    (go.name.StartsWith("Prop_MedBay_") && go.transform.parent == null) ||
                    (go.name.StartsWith("Indicator_Door_MedBay") && go.transform.parent == null))
                {
                    Undo.DestroyObjectImmediate(go);
                }
            }

            GameObject propsRoot = new GameObject("MedBay_Decorations");
            Undo.RegisterCreatedObjectUndo(propsRoot, "Create MedBay Decorations");

            GameObject doorsRoot = GameObject.Find("Doors");

            // --- DOOR 1: EAST DOOR (Door_MedBay) at (-10.5f, 28.0f) ---
            DoorController medBayEastDoor = null;
            DoorController[] allDoors = Object.FindObjectsByType<DoorController>();
            foreach (DoorController d in allDoors)
            {
                if (d.name == "Door_MedBay" || d.name == "Door_MedBay_East")
                {
                    medBayEastDoor = d;
                    break;
                }
            }

            if (medBayEastDoor == null && doorsRoot != null)
            {
                GameObject doorGo = new GameObject("Door_MedBay");
                doorGo.transform.SetParent(doorsRoot.transform);
                medBayEastDoor = doorGo.AddComponent<DoorController>();
            }

            if (medBayEastDoor != null)
            {
                Undo.RecordObject(medBayEastDoor.transform, "Position MedBay East Door");
                Undo.RecordObject(medBayEastDoor, "Configure MedBay East Door");

                medBayEastDoor.transform.position = new Vector3(-10.5f, 28.0f, 0f);
                medBayEastDoor.transform.localScale = new Vector3(0.7485f, 0.7485f, 1f);
                medBayEastDoor.Initialize("Door_MedBay");
                medBayEastDoor.SetSprites(doorVClosed, doorVOpen);

                SpriteRenderer dsr = medBayEastDoor.GetComponent<SpriteRenderer>();
                if (dsr == null) dsr = medBayEastDoor.gameObject.AddComponent<SpriteRenderer>();
                Undo.RecordObject(dsr, "Update MedBay East Door Material");
                if (defaultSpriteMat != null) dsr.sharedMaterial = defaultSpriteMat;
                dsr.sprite = doorVClosed;
                dsr.color = Color.white;
                dsr.sortingLayerName = "Obstacles";
                dsr.sortingOrder = 5;

                BoxCollider2D box = medBayEastDoor.GetComponent<BoxCollider2D>();
                if (box == null) box = medBayEastDoor.gameObject.AddComponent<BoxCollider2D>();
                Undo.RecordObject(box, "Update MedBay East Door Collider");
                box.size = new Vector2(1.0f, 2.0f);
                box.offset = Vector2.zero;
                int interactableLayer = LayerMask.NameToLayer("Interactable");
                if (interactableLayer != -1) medBayEastDoor.gameObject.layer = interactableLayer;

                CreateIndicatorLight(propsRoot, medBayEastDoor, new Vector3(-10.5f, 29.15f, 0f), lightRed, lightGreen, defaultSpriteMat);
                EditorUtility.SetDirty(medBayEastDoor);
            }

            // --- DOOR 2: WEST DOOR (Door_MedBay_West) at (-20.5f, 25.0f) leading to long corridor ---
            DoorController medBayWestDoor = null;
            foreach (DoorController d in allDoors)
            {
                if (d.name == "Door_MedBay_West")
                {
                    medBayWestDoor = d;
                    break;
                }
            }

            if (medBayWestDoor == null)
            {
                GameObject doorWestGo = new GameObject("Door_MedBay_West");
                if (doorsRoot != null) doorWestGo.transform.SetParent(doorsRoot.transform);
                medBayWestDoor = doorWestGo.AddComponent<DoorController>();
            }

            if (medBayWestDoor != null)
            {
                Undo.RecordObject(medBayWestDoor.transform, "Position MedBay West Door");
                Undo.RecordObject(medBayWestDoor, "Configure MedBay West Door");

                medBayWestDoor.transform.position = new Vector3(-20.5f, 25.0f, 0f);
                medBayWestDoor.transform.localScale = new Vector3(0.7485f, 0.7485f, 1f);
                medBayWestDoor.Initialize("Door_MedBay_West");
                medBayWestDoor.SetSprites(doorVClosed, doorVOpen);

                SpriteRenderer dsr = medBayWestDoor.GetComponent<SpriteRenderer>();
                if (dsr == null) dsr = medBayWestDoor.gameObject.AddComponent<SpriteRenderer>();
                Undo.RecordObject(dsr, "Update MedBay West Door Material");
                if (defaultSpriteMat != null) dsr.sharedMaterial = defaultSpriteMat;
                dsr.sprite = doorVClosed;
                dsr.color = Color.white;
                dsr.sortingLayerName = "Obstacles";
                dsr.sortingOrder = 5;

                BoxCollider2D box = medBayWestDoor.GetComponent<BoxCollider2D>();
                if (box == null) box = medBayWestDoor.gameObject.AddComponent<BoxCollider2D>();
                Undo.RecordObject(box, "Update MedBay West Door Collider");
                box.size = new Vector2(1.0f, 2.0f);
                box.offset = Vector2.zero;
                int interactableLayer = LayerMask.NameToLayer("Interactable");
                if (interactableLayer != -1) medBayWestDoor.gameObject.layer = interactableLayer;

                CreateIndicatorLight(propsRoot, medBayWestDoor, new Vector3(-20.5f, 26.15f, 0f), lightRed, lightGreen, defaultSpriteMat);
                EditorUtility.SetDirty(medBayWestDoor);
            }

            // ==========================================
            // 4. SCI-FI HORROR PROPS & PRECISE PLACEMENT
            // (Using 100% original unlit horror pixel-art colors without light tinting)
            // ==========================================
            // 1. Central Operating Table (Centered squarely over the 2 drainage grates at -16.0f, 27.35f, inside hazard boundary)
            CreateProp(propsRoot, "Prop_MedBay_Operating_Table", new Vector3(-16.00f, 27.35f, 0f), Vector2.zero, Vector2.zero, "Obstacles", 2, defaultSpriteMat, true, new Vector3(0.55f, 0.55f, 1f));

            // 2. Mobile Surgical Tool Cart (Placed cleanly beside Operating Table at -14.45f, 27.35f, cleanly inside boundary with no stripe intersection)
            CreateProp(propsRoot, "Prop_MedBay_Surgical_Cart", new Vector3(-14.45f, 27.35f, 0f), Vector2.zero, Vector2.zero, "Obstacles", 2, defaultSpriteMat, true, new Vector3(0.50f, 0.50f, 1f));

            // 3. Body Bag (Casualty zipped in body bag placed on the floor beside the biohazard blood puddle at -14.50f, 25.20f)
            CreateProp(propsRoot, "Prop_MedBay_Body_Bag", new Vector3(-14.50f, 25.20f, 0f), Vector2.zero, Vector2.zero, "Obstacles", 2, defaultSpriteMat, true, new Vector3(0.55f, 0.55f, 1f));

            // 4. Medicine Cabinet (Top-left corner: West wall is at X=-20.0, North wall is at Y=32.0)
            // Left edge = -19.95 (wall -20.0), Top edge = 31.96 (wall 32.0). 100% flush without wall overlap!
            CreateProp(propsRoot, "Prop_MedBay_Medicine_Cabinet", new Vector3(-19.20f, 31.15f, 0f), Vector2.zero, Vector2.zero, "Obstacles", 2, defaultSpriteMat, true, new Vector3(0.75f, 0.75f, 1f));

            // 5. Wash Station (Mounted onto North wall at Y=31.60 so the intake pipe visually plugs directly into the bulkhead)
            CreateProp(propsRoot, "Prop_MedBay_Wash_Station", new Vector3(-15.00f, 31.60f, 0f), Vector2.zero, Vector2.zero, "Obstacles", 2, defaultSpriteMat, true, new Vector3(0.70f, 0.70f, 1f));

            // 6. EKG Console (Top-right corner: East wall is at X=-11.0, North wall is at Y=32.0)
            // Right edge = -11.03 (wall -11.0), Top edge = 31.96 (wall 32.0). 100% flush without wall overlap!
            CreateProp(propsRoot, "Prop_MedBay_EKG_Console", new Vector3(-11.55f, 31.15f, 0f), Vector2.zero, Vector2.zero, "Obstacles", 2, defaultSpriteMat, true, new Vector3(0.75f, 0.75f, 1f));

            // 7. Oxygen Tank Manifold Rack (Bottom-right corner: East wall is at X=-11.0, South wall is at Y=24.0)
            // Right edge = -11.04 (wall -11.0), Bottom edge = 24.02 (wall 24.0). 100% flush without wall overlap! Flipped X.
            CreateProp(propsRoot, "Prop_MedBay_Oxygen_Rack", new Vector3(-11.75f, 24.62f, 0f), Vector2.zero, Vector2.zero, "Obstacles", 2, defaultSpriteMat, true, new Vector3(-0.75f, 0.75f, 1f));

            // ==========================================
            // 5. DECALS & WALL WARNING MARKINGS
            // (Mounted directly on non-pipe bulkhead wall tiles from new Doors sheet)
            // ==========================================
            // Metal Plaque 'RESTRICTED - MEDICAL PERSONNEL ONLY' on East bulkhead wall above doorway
            CreateProp(propsRoot, "MedBay_Decal_Restricted", new Vector3(-10.5f, 29.8f, 0f), Vector2.zero, Vector2.zero, "Obstacles", 10, defaultSpriteMat, false, new Vector3(0.85f, 0.85f, 1f));

            // Metal Plaque 'RESTRICTED - MEDICAL PERSONNEL ONLY' on West bulkhead wall north of corridor door
            CreateProp(propsRoot, "MedBay_Decal_Restricted", new Vector3(-20.5f, 27.0f, 0f), Vector2.zero, Vector2.zero, "Obstacles", 10, defaultSpriteMat, false, new Vector3(0.85f, 0.85f, 1f));

            // ==========================================
            // 6. RELOCATE TASKLOC_MEDBAY & HIDE PLACEHOLDER
            // ==========================================
            GameObject taskLoc = GameObject.Find("TaskLoc_MedBay");
            if (taskLoc != null)
            {
                Undo.RecordObject(taskLoc.transform, "Relocate TaskLoc_MedBay");
                // Place at EKG diagnostic console against North wall
                taskLoc.transform.position = new Vector3(-11.55f, 31.15f, 0f);

                SpriteRenderer taskSr = taskLoc.GetComponent<SpriteRenderer>();
                if (taskSr != null)
                {
                    Undo.RecordObject(taskSr, "Hide TaskLoc_MedBay Marker Sprite");
                    taskSr.enabled = false;
                }
                EditorUtility.SetDirty(taskLoc);
            }

            // ==========================================
            // 7. MEDBAY ATMOSPHERIC & TASK LIGHTING
            // ==========================================
            SetupMedBayLighting();

            // ==========================================
            // 8. MEDBAY WEST CORRIDOR DECORATION (DIRECTION A)
            // ==========================================
            DecorateWestCorridor();

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
        }

        public static void SetupMedBayLighting()
        {
            GameObject oldLighting = GameObject.Find("MedBay_Lighting");
            if (oldLighting != null) Undo.DestroyObjectImmediate(oldLighting);

            GameObject lightingRoot = new GameObject("MedBay_Lighting");
            Undo.RegisterCreatedObjectUndo(lightingRoot, "Create MedBay_Lighting");

            System.Action<string, Vector3, Color, float, float, float, bool> addLight = (name, pos, color, intensity, innerR, outerR, addFlicker) =>
            {
                GameObject go = new GameObject(name);
                go.transform.SetParent(lightingRoot.transform);
                go.transform.position = pos;

                Light2D light = go.AddComponent<Light2D>();
                light.lightType = Light2D.LightType.Point;
                light.color = color;
                light.intensity = intensity;
                light.pointLightInnerRadius = innerR;
                light.pointLightOuterRadius = outerR;
                light.falloffIntensity = 0.65f;

                if (addFlicker)
                {
                    var flicker = go.AddComponent<TheLastSignal.Entities.Environment.VisualPass.VP_FlickerLight>();
                    var so = new SerializedObject(flicker);
                    so.FindProperty("mode").enumValueIndex = 1; // IrregularGlitches
                    so.FindProperty("baseIntensity").floatValue = intensity;
                    so.FindProperty("glitchChance").floatValue = 0.04f;
                    so.ApplyModifiedProperties();
                }
            };

            // Layer 1: Base Visibility Ambient Fill - lifts surrounding floor and seam readability
            addLight("Light_MedBay_Room_Ambient", new Vector3(-15.50f, 28.00f, 0f), new Color(0.38f, 0.48f, 0.55f, 1f), 0.95f, 1.5f, 9.0f, false);

            // Layer 2: Functional Task & Focal Lights
            // Surgical Lamp: Centered directly at lamp head (-16.15, 27.80) with smooth falloff extending across boundary
            addLight("Light_MedBay_Surgical_Lamp", new Vector3(-16.15f, 27.80f, 0f), new Color(0.85f, 0.94f, 1.0f, 1f), 1.55f, 0.8f, 5.0f, false);
            // Diagnostic Monitor: Localized phosphor green accent
            addLight("Light_MedBay_EKG_Screen", new Vector3(-11.55f, 31.15f, 0f), new Color(0.20f, 0.88f, 0.42f, 1f), 0.80f, 0.15f, 1.4f, false);
            // Ceiling West: Balanced overhead ambient fill
            addLight("Light_MedBay_Ceiling_West", new Vector3(-18.50f, 29.50f, 0f), new Color(0.70f, 0.82f, 0.90f, 1f), 0.90f, 0.5f, 3.5f, true);
            // Hand Wash Station: Warm tungsten task lamp
            addLight("Light_MedBay_WashStation", new Vector3(-15.00f, 31.60f, 0f), new Color(0.95f, 0.80f, 0.52f, 1f), 0.75f, 0.2f, 1.6f, false);
            // South Casualty Fill: Restrained low-key fill
            addLight("Light_MedBay_South_Ambient", new Vector3(-14.50f, 25.20f, 0f), new Color(0.30f, 0.42f, 0.50f, 1f), 0.65f, 0.4f, 3.0f, false);

            // Contact drop shadows
            Sprite shadowSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Sprites/Environments/VentShaft/Props/VP_Prop_Contact_Shadow.png");
            Material mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/M_Sprite_Lit_PixelSmooth.mat")
                ?? AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Environment/Materials/M_Sprite_Lit_PixelSmooth.mat");

            string[] sNames = new string[] { "Shadow_MedBay_Table", "Shadow_MedBay_Cart", "Shadow_MedBay_BodyBag", "Shadow_MedBay_OxygenRack" };
            Vector3[] sPositions = new Vector3[] { new Vector3(-16.00f, 26.65f, 0f), new Vector3(-14.45f, 26.80f, 0f), new Vector3(-14.50f, 25.05f, 0f), new Vector3(-11.75f, 24.25f, 0f) };
            Vector3[] sScales = new Vector3[] { new Vector3(2.2f, 0.6f, 1f), new Vector3(0.85f, 0.35f, 1f), new Vector3(1.2f, 0.4f, 1f), new Vector3(1.1f, 0.5f, 1f) };
            float[] sAlphas = new float[] { 0.50f, 0.55f, 0.50f, 0.50f };

            for (int i = 0; i < sNames.Length; i++)
            {
                GameObject sGo = new GameObject(sNames[i]);
                sGo.transform.SetParent(lightingRoot.transform);
                sGo.transform.position = sPositions[i];
                sGo.transform.localScale = sScales[i];

                SpriteRenderer sr = sGo.AddComponent<SpriteRenderer>();
                sr.sprite = shadowSprite;
                if (mat != null) sr.sharedMaterial = mat;
                sr.color = new Color(0f, 0f, 0f, sAlphas[i]);
                sr.sortingLayerName = "Floor";
                sr.sortingOrder = 3;
            }
        }

        [MenuItem("Tools/Facility/Decorate MedBay West Corridor (Direction A)", false, 52)]
        public static void DecorateWestCorridor()
        {
            GameObject gridGo = GameObject.Find("Facility_Grid") ?? GameObject.Find("Grid");
            if (gridGo == null) return;

            Tilemap floorMap = null;
            Tilemap wallMap = null;
            foreach (var tm in gridGo.GetComponentsInChildren<Tilemap>())
            {
                if (tm.name.ToLower().Contains("floor")) floorMap = tm;
                else if (tm.name.ToLower().Contains("wall")) wallMap = tm;
            }
            if (floorMap == null || wallMap == null) return;

            Undo.RecordObjects(new UnityEngine.Object[] { floorMap, wallMap }, "Decorate MedBay West Corridor");

            // Load Tiles
            // Load Tiles - MedBay Active Dark Steel Suite & Bulkheads
            Tile fDark = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/MedBay_Floor_Dark.asset");
            Tile fDarkStained = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/MedBay_Floor_Dark_Stained.asset") ?? fDark;
            Tile fDarkScratched = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/MedBay_Floor_Dark_Scratched.asset") ?? fDark;
            Tile fDarkRusted = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/MedBay_Floor_Dark_Rusted.asset") ?? fDark;
            Tile fDiamond = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/MedBay_Floor_Diamond.asset") ?? fDark;
            Tile fGrate = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/MedBay_Floor_Grate.asset") ?? fDiamond;
            Tile fHatch = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/MedBay_Floor_Subfloor_Hatch.asset") ?? fDiamond;
            Tile fTrans = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/MedBay_Floor_Transition_Strip.asset") ?? fDiamond;

            Tile wBulkhead = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/MedBay_Wall_Bulkhead.asset");
            Tile wPipesCleanH = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/MedBay_Wall_Pipes_Clean_H.asset") ?? wBulkhead;
            Tile wPipesCleanNorth = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/MedBay_Wall_Pipes_Clean_North.asset") ?? wBulkhead;
            Tile wPipesCleanV = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/MedBay_Wall_Pipes_Clean_V.asset") ?? wBulkhead;
            Tile wPipesCleanVEast = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/MedBay_Wall_Pipes_Clean_V_East.asset") ?? wPipesCleanV;
            Tile wCornerTL = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/MedBay_Wall_Corner_TL.asset") ?? wBulkhead;
            Tile wCornerBL = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/MedBay_Wall_Corner_BL.asset") ?? wBulkhead;
            Tile wInnerCornerBR = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/MedBay_Wall_Inner_Corner_BR.asset") ?? wBulkhead;
            Tile wWarningNotice = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/MedBay_Wall_Warning_Notice.asset") ?? wBulkhead;

            // ==========================================
            // 1. HORIZONTAL DECONTAMINATION STRETCH (X in [-23, -21], Y in [24, 25])
            // 100% authentic uniform MedBay Floor (MedBay_Floor_Dark with 4 corner rivets)
            // ==========================================
            floorMap.SetTile(new Vector3Int(-21, 25, 0), fDark);
            floorMap.SetTile(new Vector3Int(-21, 24, 0), fDark);
            floorMap.SetTile(new Vector3Int(-22, 25, 0), fDark);
            floorMap.SetTile(new Vector3Int(-22, 24, 0), fDark);
            floorMap.SetTile(new Vector3Int(-23, 25, 0), fDark);
            floorMap.SetTile(new Vector3Int(-23, 24, 0), fDark);

            // North Wall (Y = 26) - Clean horizontal pipes running uninterrupted across the entire north wall into corner
            wallMap.SetTile(new Vector3Int(-21, 26, 0), wPipesCleanNorth);
            wallMap.SetTile(new Vector3Int(-22, 26, 0), wPipesCleanNorth);
            wallMap.SetTile(new Vector3Int(-23, 26, 0), wPipesCleanNorth);
            wallMap.SetTile(new Vector3Int(-24, 26, 0), wPipesCleanNorth);
            wallMap.SetTile(new Vector3Int(-25, 26, 0), wPipesCleanNorth);
            wallMap.SetTile(new Vector3Int(-26, 26, 0), wCornerTL);

            // South Wall (Y = 23) - Exact copper + blue pipes matching user reference (VP8 South Trunk)
            wallMap.SetTile(new Vector3Int(-21, 23, 0), wPipesCleanH);
            wallMap.SetTile(new Vector3Int(-22, 23, 0), wPipesCleanH);
            wallMap.SetTile(new Vector3Int(-23, 23, 0), wInnerCornerBR); // Seamless mathematical inner corner connecting Clean_H to Clean_V_East!
            wallMap.SetTile(new Vector3Int(-24, 23, 0), null); // Clear misplaced wall to free corridor lane!

            // ==========================================
            // 2. 90-DEGREE CORRIDOR BEND (X in [-25, -24], Y in [24, 25])
            // ==========================================
            floorMap.SetTile(new Vector3Int(-25, 25, 0), fDark);
            floorMap.SetTile(new Vector3Int(-24, 25, 0), fDark);
            floorMap.SetTile(new Vector3Int(-25, 24, 0), fDark);
            floorMap.SetTile(new Vector3Int(-24, 24, 0), fDark);

            wallMap.SetTile(new Vector3Int(-26, 25, 0), wPipesCleanV);
            wallMap.SetTile(new Vector3Int(-26, 24, 0), wPipesCleanV);

            // ==========================================
            // 3. ENTIRE VERTICAL TRANSIT SPINE (X in [-25, -24], Y from 23 down to -45)
            // 100% continuous, uniform MedBay Floor & symmetrical pipe walls without broken fragments!
            // ==========================================
            for (int y = 23; y >= -45; y--)
            {
                // Walkway floor: 100% MedBay_Floor_Dark
                floorMap.SetTile(new Vector3Int(-25, y, 0), fDark);
                floorMap.SetTile(new Vector3Int(-24, y, 0), fDark);

                // West Wall (X = -26): 100% continuous vertical conduits
                wallMap.SetTile(new Vector3Int(-26, y, 0), wPipesCleanV);

                // East Wall (X = -23): 100% continuous symmetrical vertical conduits (from Y=22 down to -45)
                if (y <= 22)
                {
                    wallMap.SetTile(new Vector3Int(-23, y, 0), wPipesCleanVEast);
                }
            }

            // ==========================================
            // 4. SOUTHERN SECTOR JUNCTION & BOTTOM TURN (Y in [-45, -48], X in [-26, -19])
            // ==========================================
            // Y = -46 and -47: Corridor turns East
            for (int y = -46; y >= -47; y--)
            {
                // West Wall continues to the bottom-left corner
                wallMap.SetTile(new Vector3Int(-26, y, 0), wPipesCleanV);

                // Walkway floor turns East from X=-25 to X=-19
                for (int x = -25; x <= -19; x++)
                {
                    floorMap.SetTile(new Vector3Int(x, y, 0), fDark);
                }
            }

            // Outer Bottom-Left Corner (-26, -48)
            wallMap.SetTile(new Vector3Int(-26, -48, 0), wCornerBL);

            // South Wall of bottom corridor (Y = -48, X in [-25, -19])
            for (int x = -25; x <= -19; x++)
            {
                wallMap.SetTile(new Vector3Int(x, -48, 0), wPipesCleanH);
            }

            // North Wall of bottom corridor (Y = -45, X in [-22, -19])
            for (int x = -22; x <= -19; x++)
            {
                wallMap.SetTile(new Vector3Int(x, -45, 0), wPipesCleanNorth);
            }

            // Inner Corner at (-23, -45) - Structural Bulkhead Pillar
            wallMap.SetTile(new Vector3Int(-23, -45, 0), wBulkhead);

            // Flanking boundary wall at (-19, -42..-44)
            wallMap.SetTile(new Vector3Int(-19, -42, 0), wBulkhead);
            wallMap.SetTile(new Vector3Int(-19, -43, 0), wBulkhead);
            wallMap.SetTile(new Vector3Int(-19, -44, 0), wBulkhead);

            // ==========================================
            // 5. PROPS & ENVIRONMENTAL STORYTELLING
            // ==========================================
            SetupWestCorridorProps();

            // ==========================================
            // 6. ATMOSPHERIC LIGHTING ALONG FULL CORRIDOR
            // ==========================================
            SetupWestCorridorLighting();
        }

        public static void SetupWestCorridorProps()
        {
            GameObject oldRoot = GameObject.Find("MedBay_WestCorridor_Decorations");
            if (oldRoot != null) Undo.DestroyObjectImmediate(oldRoot);

            GameObject decorRoot = new GameObject("MedBay_WestCorridor_Decorations");
            Undo.RegisterCreatedObjectUndo(decorRoot, "Create MedBay_WestCorridor_Decorations");

            Material litMat = AssetDatabase.LoadAssetAtPath<Material>("Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Lit-Default.mat");

            // 1. Overturned Surgical Cart at Corner Bend (-24.15f, 24.15f)
            CreateProp(decorRoot, "VP5_Prop_Overturned_Cart", new Vector3(-24.15f, 24.15f, 0f), new Vector2(0.8f, 0.6f), Vector2.zero, "Obstacles", 2, litMat, true, new Vector3(0.52f, 0.52f, 1f));

            // 2. Stainless Instrument Supply Cart tucked against West wall at (-25.55f, 22.20f)
            CreateProp(decorRoot, "Prop_MedBay_Biohazard_Bin", new Vector3(-25.55f, 22.20f, 0f), new Vector2(0.5f, 0.5f), Vector2.zero, "Obstacles", 3, litMat, true, new Vector3(0.40f, 0.40f, 1f));

            // 3. Blood Drag Mark trailing along the floor (-24.50f, 21.00f)
            var bloodDrag = CreateProp(decorRoot, "VP5_Decal_BloodDrag_Long", new Vector3(-24.50f, 21.00f, 0f), Vector2.zero, Vector2.zero, "Floor", 2, litMat, false, new Vector3(0.75f, 0.75f, 1f));
            if (bloodDrag != null)
            {
                bloodDrag.transform.rotation = Quaternion.Euler(0f, 0f, 270f);
            }

            // 4. Blood Drag Turn Decal at Corner Bend (-24.50f, 24.40f)
            CreateProp(decorRoot, "VP5_Decal_BloodDrag_SE_Pixel", new Vector3(-24.50f, 24.40f, 0f), Vector2.zero, Vector2.zero, "Floor", 2, litMat, false, new Vector3(0.65f, 0.65f, 1f));

            // 5. Claw Scrapes Decal on metal wall (-25.90f, 23.50f)
            CreateProp(decorRoot, "VP9_Decal_Claw_Scrapes", new Vector3(-25.90f, 23.50f, 0f), Vector2.zero, Vector2.zero, "Obstacles", 4, litMat, false, new Vector3(0.60f, 0.60f, 1f));

            // 6. Bloody Handprint Slide on metal wall (-25.92f, 21.80f)
            CreateProp(decorRoot, "VP9_Decal_BloodHand_Slide", new Vector3(-25.92f, 21.80f, 0f), Vector2.zero, Vector2.zero, "Obstacles", 4, litMat, false, new Vector3(0.60f, 0.60f, 1f));

            // 7. Wall Emergency First Aid Board (-23.08f, 21.50f)
            CreateProp(decorRoot, "VP5_Prop_Wall_Emergency_Board", new Vector3(-23.08f, 21.50f, 0f), Vector2.zero, Vector2.zero, "Obstacles", 4, litMat, false, new Vector3(0.60f, 0.60f, 1f));

            // 8. Pipe Valve Wheel (-23.08f, 17.50f)
            CreateProp(decorRoot, "VP6_Prop_Valve_Wheel", new Vector3(-23.08f, 17.50f, 0f), Vector2.zero, Vector2.zero, "Obstacles", 4, litMat, false, new Vector3(0.65f, 0.65f, 1f));

            // 10. Quarantine Notice Stencil on Decon bulkhead wall (-21.90f, 25.80f)
            CreateProp(decorRoot, "MedBay_Decal_Quarantine", new Vector3(-21.90f, 25.80f, 0f), Vector2.zero, Vector2.zero, "Obstacles", 4, litMat, false, new Vector3(0.65f, 0.65f, 1f));

            // 11. Dropped Medical Clipboard on the ground near corner turn (-23.85f, 24.30f)
            var clip = CreateProp(decorRoot, "VP5_Prop_Medical_Clipboard", new Vector3(-23.85f, 24.30f, 0f), Vector2.zero, Vector2.zero, "Floor", 2, litMat, false, new Vector3(0.55f, 0.55f, 1f));
            if (clip != null)
            {
                clip.transform.rotation = Quaternion.Euler(0f, 0f, 35f);
            }
        }

        public static void SetupWestCorridorLighting()
        {
            var oldLight = GameObject.Find("Light_WestCorridor_Amber");
            if (oldLight != null) Undo.DestroyObjectImmediate(oldLight);

            GameObject oldLightRoot = GameObject.Find("MedBay_WestCorridor_Lighting");
            if (oldLightRoot != null) Undo.DestroyObjectImmediate(oldLightRoot);

            GameObject lightRoot = new GameObject("MedBay_WestCorridor_Lighting");
            Undo.RegisterCreatedObjectUndo(lightRoot, "Create MedBay_WestCorridor_Lighting");

            Light2D AddPointLight(string name, Vector3 pos, Color col, float intensity, float outerRad, float innerRad, bool flicker)
            {
                GameObject go = new GameObject(name);
                go.transform.SetParent(lightRoot.transform);
                go.transform.position = pos;

                var l = go.AddComponent<Light2D>();
                l.lightType = Light2D.LightType.Point;
                l.color = col;
                l.intensity = intensity;
                l.pointLightInnerRadius = innerRad;
                l.pointLightOuterRadius = outerRad;
                l.falloffIntensity = 0.60f;
                l.shadowsEnabled = false;

                if (flicker)
                {
                    var fl = go.AddComponent<TheLastSignal.Juice.FlickeringLight>();
                    fl.minIntensity = 0.45f;
                    fl.maxIntensity = 1.45f;
                    fl.flickerChance = 0.15f;
                }
                return l;
            }

            // 1. Amber Warning Light at Corner Bend (Flickering alarm light)
            AddPointLight("Light_CorridorW_Alarm_Amber", new Vector3(-24.50f, 24.50f, 0f), new Color(1.0f, 0.60f, 0.15f, 1f), 1.45f, 5.0f, 0.5f, true);

            // 2. Cyan Decontamination Overhead Tube (Airlock transition right outside MedBay West door)
            AddPointLight("Light_CorridorW_Decon_Tube", new Vector3(-22.00f, 24.80f, 0f), new Color(0.72f, 0.90f, 1.0f, 1f), 1.35f, 5.2f, 0.5f, false);

            // 3. Sterile Cyan Point Light (Sterile clinical glow on upper spine)
            AddPointLight("Light_CorridorW_Sterile_Cyan", new Vector3(-24.50f, 20.50f, 0f), new Color(0.65f, 0.88f, 1.0f, 1f), 1.35f, 5.5f, 0.5f, false);

            // 4. Cool Steel Transit Light (Flanking Crew Quarters)
            AddPointLight("Light_CorridorW_Spine_12", new Vector3(-24.50f, 12.50f, 0f), new Color(0.40f, 0.65f, 0.88f, 1f), 1.25f, 6.0f, 0.5f, false);

            // 5. Amber Service Light (Between Crew Quarters & Mess Hall)
            AddPointLight("Light_CorridorW_Spine_04", new Vector3(-24.50f, 4.00f, 0f), new Color(1.0f, 0.68f, 0.25f, 1f), 1.35f, 6.0f, 0.5f, true);

            // 6. Cool Cyan Light (Flanking Mess Hall exterior)
            AddPointLight("Light_CorridorW_Spine_M04", new Vector3(-24.50f, -4.00f, 0f), new Color(0.65f, 0.88f, 1.0f, 1f), 1.25f, 6.0f, 0.5f, false);

            // 7. Steel Maintenance Light (Mid-corridor)
            AddPointLight("Light_CorridorW_Spine_M12", new Vector3(-24.50f, -12.00f, 0f), new Color(0.40f, 0.65f, 0.88f, 1f), 1.25f, 6.0f, 0.5f, false);

            // 8. Amber Alarm Light (Mid-south transit)
            AddPointLight("Light_CorridorW_Spine_M20", new Vector3(-24.50f, -20.00f, 0f), new Color(1.0f, 0.60f, 0.15f, 1f), 1.35f, 6.0f, 0.5f, true);

            // 9. Cool Cyan Light (Lower spine)
            AddPointLight("Light_CorridorW_Spine_M28", new Vector3(-24.50f, -28.00f, 0f), new Color(0.65f, 0.88f, 1.0f, 1f), 1.25f, 6.0f, 0.5f, false);

            // 10. Steel Maintenance Light (Approaching Cargo/Power sector)
            AddPointLight("Light_CorridorW_Spine_M36", new Vector3(-24.50f, -36.00f, 0f), new Color(0.40f, 0.65f, 0.88f, 1f), 1.25f, 6.0f, 0.5f, false);

            // 11. Cyan Southern Light (Approaching southern junction)
            AddPointLight("Light_CorridorW_Spine_M42", new Vector3(-24.50f, -42.00f, 0f), new Color(0.65f, 0.88f, 1.0f, 1f), 1.35f, 6.0f, 0.5f, false);

            // 12. Amber Southern Junction Warning Light (At bottom corner turn)
            AddPointLight("Light_CorridorW_Junction_Amber", new Vector3(-24.50f, -46.50f, 0f), new Color(1.0f, 0.60f, 0.15f, 1f), 1.45f, 6.5f, 0.5f, true);

            // 13. Deep Storage / Power Relay Eastbound Run Light
            AddPointLight("Light_CorridorW_SouthEast_Run", new Vector3(-18.00f, -46.50f, 0f), new Color(0.60f, 0.85f, 1.0f, 1f), 1.25f, 6.5f, 0.5f, false);
        }

        private static GameObject CreateIndicatorLight(GameObject parent, DoorController door, Vector3 position, Sprite red, Sprite green, Material mat)
        {
            GameObject lightGo = new GameObject($"Indicator_{door.name}");
            lightGo.transform.SetParent(parent.transform);
            lightGo.transform.position = position;
            lightGo.transform.localScale = new Vector3(0.5f, 0.5f, 1f);

            SpriteRenderer sr = lightGo.AddComponent<SpriteRenderer>();
            sr.sprite = red;
            sr.sortingLayerName = "Obstacles";
            sr.sortingOrder = 6;
            if (mat != null) sr.sharedMaterial = mat;

            Light2D light = lightGo.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Point;
            light.color = new Color(0.88f, 0.18f, 0.18f, 1.0f);
            light.intensity = 0.85f;
            light.pointLightOuterRadius = 1.5f;
            light.pointLightInnerRadius = 0.15f;
            light.enabled = true;

            DoorIndicatorLight indicator = lightGo.AddComponent<DoorIndicatorLight>();
            indicator.Initialize(door, red, green, light);

            return lightGo;
        }

        private static GameObject CreateProp(GameObject parent, string propSpriteName, Vector3 position, Vector2 colliderSize, Vector2 colliderOffset, string sortingLayer, int sortingOrder, Material mat, bool addCollider, Vector3? localScale = null)
        {
            string spritePath = $"{PropsSourceDir}/{propSpriteName}.png";
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
            sr.sortingLayerName = sortingLayer;
            sr.sortingOrder = sortingOrder;
            if (mat != null) sr.sharedMaterial = mat;

            if (addCollider)
            {
                BoxCollider2D box = propGo.AddComponent<BoxCollider2D>();
                if (colliderSize == Vector2.zero)
                {
                    // By default, match EXACT visual dimensions of the sprite.
                    // In local space: size = sprite.rect.size / sprite.pixelsPerUnit.
                    // When Unity scales the GameObject by localScale, the world collider precisely matches the visual sprite footprint,
                    // preventing the player from touching or penetrating any part of the prop image.
                    box.size = new Vector2(sprite.rect.width / sprite.pixelsPerUnit, sprite.rect.height / sprite.pixelsPerUnit);
                    box.offset = Vector2.zero;
                }
                else
                {
                    box.size = colliderSize;
                    box.offset = colliderOffset;
                }

                int obstacleLayer = LayerMask.NameToLayer("Obstacle");
                if (obstacleLayer != -1) propGo.layer = obstacleLayer;
            }

            return propGo;
        }

        private static Sprite LoadSprite(string path)
        {
            Sprite s = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (s == null)
            {
                Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (tex != null)
                {
                    s = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 128f);
                    s.name = Path.GetFileNameWithoutExtension(path);
                }
            }
            return s;
        }
    }
}
#endif
