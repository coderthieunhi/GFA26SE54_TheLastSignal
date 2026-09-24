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

        [InitializeOnLoadMethod]
        private static void OnEditorLoaded()
        {
            EditorApplication.delayCall += () =>
            {
                SetupMedBayArt();
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
            Grid grid = Object.FindAnyObjectByType<Grid>();
            if (grid == null)
            {
                GameObject gridGo = GameObject.Find("Facility_Grid") ?? GameObject.Find("Grid");
                if (gridGo != null)
                {
                    grid = gridGo.GetComponent<Grid>();
                }
            }

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
                if (tm.name.IndexOf("Floor", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    floorMap = tm;
                }
                else if (tm.name.IndexOf("Wall", System.StringComparison.OrdinalIgnoreCase) >= 0)
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
            Tile fBio = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/MedBay_Floor_Biohazard.asset");
            Tile fDiamond = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/MedBay_Floor_Diamond.asset");
            Tile fHatch = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/MedBay_Floor_Subfloor_Hatch.asset");
            Tile fHazTop = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/MedBay_Floor_Hazard_Top.asset");
            Tile fHazBot = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/MedBay_Floor_Hazard_Bottom.asset");
            Tile fHazLeft = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/MedBay_Floor_Hazard_Left.asset");
            Tile fHazRight = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/MedBay_Floor_Hazard_Right.asset");
            Tile fR2C5 = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/MedBay_Floor_R2C5.asset");

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
            // 1. PAINT INTERIOR FLOOR: X in [-20, -12], Y in [24, 31]
            // ==========================================
            Undo.RecordObject(floorMap, "Decorate MedBay Floor");

            for (int x = -20; x <= -12; x++)
            {
                for (int y = 24; y <= 31; y++)
                {
                    Vector3Int pos = new Vector3Int(x, y, 0);

                    // Operating Table sits directly over center drainage grates at (-17, 27) and (-16, 27) (shifted down 1 row, left 1 col)
                    if ((x == -17 || x == -16) && y == 27)
                    {
                        floorMap.SetTile(pos, fGrate ?? fDark ?? fClean);
                    }
                    // Hazard Quarantine Border directly surrounding the central grates (shifted down 1 row, left 1 col):
                    // Top border: y = 28, x in [-18, -15]
                    else if (y == 28 && x >= -18 && x <= -15)
                    {
                        floorMap.SetTile(pos, fHazTop ?? fClean);
                    }
                    // Bottom border: y = 26, x in [-18, -15]
                    else if (y == 26 && x >= -18 && x <= -15)
                    {
                        floorMap.SetTile(pos, fHazBot ?? fClean);
                    }
                    // Left border: x = -18, y = 27
                    else if (x == -18 && y == 27)
                    {
                        floorMap.SetTile(pos, fHazLeft ?? fClean);
                    }
                    // Right border: x = -15, y = 27
                    else if (x == -15 && y == 27)
                    {
                        floorMap.SetTile(pos, fHazRight ?? fClean);
                    }
                    // Biohazard dried fluids / stained markings:
                    else if ((x == -19 && y == 28) || (x == -13 && y == 25) || (x == -18 && y == 26))
                    {
                        floorMap.SetTile(pos, fBio ?? fDark ?? fClean);
                    }
                    // Sub-floor conduit hatch in NW area:
                    else if (x == -19 && y == 27)
                    {
                        floorMap.SetTile(pos, fHatch ?? fDiamond ?? fClean);
                    }
                    // Heavy diamond tread plate at East entrance doorway:
                    else if (x == -12 && (y == 27 || y == 28))
                    {
                        floorMap.SetTile(pos, fDiamond ?? fClean);
                    }
                    // Heavy diamond tread plate at West corridor doorway (clear walking path!):
                    else if (x == -20 && (y == 24 || y == 25))
                    {
                        floorMap.SetTile(pos, fDiamond ?? fClean);
                    }
                    // Top 2 rows: R2C5 diamond plate floor
                    else if (y == 31 || y == 30)
                    {
                        floorMap.SetTile(pos, fR2C5 ?? fWhite ?? fClean);
                    }
                    // Default dark weathered metal floor:
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

                // --- 4 CORNERS: R3C3 (Oxygen Manifold) as junction connectors ---
                wallMap.SetTile(new Vector3Int(-21, 32, 0), wO2 ?? wBulkhead); // Top-Left
                wallMap.SetTile(new Vector3Int(-11, 32, 0), wO2 ?? wBulkhead); // Top-Right
                wallMap.SetTile(new Vector3Int(-21, 23, 0), wO2 ?? wBulkhead); // Bottom-Left
                wallMap.SetTile(new Vector3Int(-11, 23, 0), wO2 ?? wBulkhead); // Bottom-Right
            }

            // ==========================================
            // 3. CONFIGURE BOTH DOORS (EAST & WEST)
            // ==========================================
            // Standard URP 2D Sprite Unlit Material (Preserves 100% original texture colors without black silhouette bugs)
            Material defaultSpriteMat = AssetDatabase.LoadAssetAtPath<Material>("Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat");
            if (defaultSpriteMat == null)
            {
                string matPath = AssetDatabase.GUIDToAssetPath("9dfc825aed78fcd4ba02077103263b40");
                if (!string.IsNullOrEmpty(matPath)) defaultSpriteMat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            }
            if (defaultSpriteMat == null)
            {
                Shader unlitShader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default") ?? Shader.Find("Sprites/Default");
                if (unlitShader != null) defaultSpriteMat = new Material(unlitShader);
            }

            Sprite doorVClosed = LoadSprite($"{PropsSourceDir}/Door_MedBay_Vertical_Closed.png");
            Sprite doorVOpen = LoadSprite($"{PropsSourceDir}/Door_MedBay_Vertical_Open.png");
            Sprite lightRed = LoadSprite($"{PropsSourceDir}/Door_MedBay_Light_Red.png");
            Sprite lightGreen = LoadSprite($"{PropsSourceDir}/Door_MedBay_Light_Green.png");

            // Clean up any old MedBay decorations or stray old props/indicators/crates
            GameObject oldCrate = GameObject.Find("Obstacle_MedBay_Crate");
            if (oldCrate != null) Undo.DestroyObjectImmediate(oldCrate);

            GameObject[] sceneObjects = Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
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
            DoorController[] allDoors = Object.FindObjectsByType<DoorController>(FindObjectsSortMode.None);
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

                SpriteRenderer dsr = medBayEastDoor.GetComponent<SpriteRenderer>() ?? medBayEastDoor.gameObject.AddComponent<SpriteRenderer>();
                Undo.RecordObject(dsr, "Update MedBay East Door Material");
                if (defaultSpriteMat != null) dsr.sharedMaterial = defaultSpriteMat;
                dsr.sprite = doorVClosed;
                dsr.color = Color.white;
                dsr.sortingLayerName = "Obstacles";
                dsr.sortingOrder = 5;

                BoxCollider2D box = medBayEastDoor.GetComponent<BoxCollider2D>() ?? medBayEastDoor.gameObject.AddComponent<BoxCollider2D>();
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

                SpriteRenderer dsr = medBayWestDoor.GetComponent<SpriteRenderer>() ?? medBayWestDoor.gameObject.AddComponent<SpriteRenderer>();
                Undo.RecordObject(dsr, "Update MedBay West Door Material");
                if (defaultSpriteMat != null) dsr.sharedMaterial = defaultSpriteMat;
                dsr.sprite = doorVClosed;
                dsr.color = Color.white;
                dsr.sortingLayerName = "Obstacles";
                dsr.sortingOrder = 5;

                BoxCollider2D box = medBayWestDoor.GetComponent<BoxCollider2D>() ?? medBayWestDoor.gameObject.AddComponent<BoxCollider2D>();
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
            // 1. Central Operating Table (Centered over shifted drainage grates at -16.5f, 27.0f)
            CreateProp(propsRoot, "Prop_MedBay_Operating_Table", new Vector3(-16.50f, 27.00f, 0f), Vector2.zero, Vector2.zero, "Obstacles", 2, defaultSpriteMat, true, new Vector3(0.55f, 0.55f, 1f));

            // 2. Hospital Bed (Gurney / Body Bag placed right beside Operating Table at -14.80f, 27.00f)
            CreateProp(propsRoot, "Prop_MedBay_Hospital_Bed", new Vector3(-14.80f, 27.00f, 0f), Vector2.zero, Vector2.zero, "Obstacles", 2, defaultSpriteMat, true, new Vector3(0.55f, 0.55f, 1f));

            // 3. Mobile Surgical Tool Cart (Placed beside Operating Table at -14.80f, 27.90f)
            CreateProp(propsRoot, "Prop_MedBay_Surgical_Cart", new Vector3(-14.80f, 27.90f, 0f), Vector2.zero, Vector2.zero, "Obstacles", 2, defaultSpriteMat, true, new Vector3(0.50f, 0.50f, 1f));

            // 4. Medicine Cabinet (Top-left corner: West wall is at X=-20.0, North wall is at Y=32.0)
            // Left edge = -19.95 (wall -20.0), Top edge = 31.96 (wall 32.0). 100% flush without wall overlap!
            CreateProp(propsRoot, "Prop_MedBay_Medicine_Cabinet", new Vector3(-19.20f, 31.15f, 0f), Vector2.zero, Vector2.zero, "Obstacles", 2, defaultSpriteMat, true, new Vector3(0.75f, 0.75f, 1f));

            // 5. Wash Station (Mounted flush against North wall Y=32.0, pipe connects directly to bulkhead)
            // Top of pipe = 31.99 (touches wall at 32.0)
            CreateProp(propsRoot, "Prop_MedBay_Wash_Station", new Vector3(-14.50f, 31.40f, 0f), Vector2.zero, Vector2.zero, "Obstacles", 2, defaultSpriteMat, true, new Vector3(0.70f, 0.70f, 1f));

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

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
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
            light.color = new Color(1.0f, 0.18f, 0.18f, 1.0f);
            light.intensity = 1.2f;
            light.pointLightOuterRadius = 2.0f;
            light.pointLightInnerRadius = 0.2f;

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
