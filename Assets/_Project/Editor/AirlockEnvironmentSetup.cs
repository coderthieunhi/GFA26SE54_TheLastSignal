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
    public static class AirlockEnvironmentSetup
    {
        private const string TilesSourceDir = "Assets/_Project/Sprites/Environments/Airlock/Tiles";
        private const string PropsSourceDir = "Assets/_Project/Sprites/Environments/Airlock/Props";
        private const string TileAssetsDir = "Assets/_Project/Tiles/Airlock";

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
                    SetupAirlockArt();
                }
            };
        }

        [MenuItem("Tools/Facility/Setup Airlock Art & Environment", false, 50)]
        public static void SetupAirlockArt()
        {
            // 1. Configure and import Tile textures (PPU = 128)
            ConfigureTileTextures();

            // 2. Configure and import Prop textures (PPU = 128)
            ConfigurePropTextures();

            // 3. Create / update Tile ScriptableObjects
            CreateTileAssets();

            // 4. Decorate Airlock_Spawn in current scene
            DecorateActiveScene();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("<color=green>[AirlockEnvironmentSetup] Successfully configured tiles, props, and decorated Airlock_Spawn!</color>");
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
                    if (importer.wrapMode != TextureWrapMode.Clamp) { importer.wrapMode = TextureWrapMode.Clamp; changed = true; }
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
            Grid grid = gridGo != null ? gridGo.GetComponent<Grid>() : null;

            if (grid == null)
            {
                Debug.LogError("[AirlockEnvironmentSetup] Grid (Facility_Grid) not found in active scene!");
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
                Debug.LogError("[AirlockEnvironmentSetup] Floor Tilemap not found under Facility_Grid!");
                return;
            }

            // 1. Load Floor Tiles
            Tile tileClean = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/Airlock_Floor_Clean.asset");
            Tile tileWeathered = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/Airlock_Floor_Weathered.asset");
            Tile tileGrateV = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/Airlock_Grate_Vertical.asset");
            Tile tileGrateH = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/Airlock_Grate_Horizontal.asset");
            Tile tileHazTop = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/Airlock_Hazard_Top.asset");
            Tile tileHazBot = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/Airlock_Hazard_Bottom.asset");
            Tile tileHazLeft = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/Airlock_Hazard_Left.asset");
            Tile tileHazRight = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/Airlock_Hazard_Right.asset");
            Tile tileSpawnPad = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/Airlock_Floor_Spawn_Pad.asset");

            // 2. Load Wall Tiles
            Tile tileWallNorth = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/Airlock_Wall_Pipes_Top.asset");
            Tile tileWallSouth = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/Airlock_Wall_Pipes_Bottom.asset");
            Tile tileWallWest = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/Airlock_Wall_Conduit_V.asset");
            Tile tileWallEast = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/Airlock_Wall_Torn_Wires.asset")
                ?? AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/Airlock_Wall_Conduit_V.asset");
            Tile tileWallLightH = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/Airlock_Wall_Light_Horizontal.asset");
            Tile tileWallLightSouth = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/Airlock_Wall_Light_South.asset") ?? tileWallLightH;
            Tile tileWallLightV = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/Airlock_Wall_Light_Vertical.asset");
            Tile tileWallBeacon = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/Airlock_Wall_Warning_Beacon.asset");
            Tile tileWallBeaconEast = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/Airlock_Wall_Warning_Beacon_East.asset") ?? tileWallBeacon;
            Tile tileWallTL = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/Airlock_Wall_Corner_TL.asset");
            Tile tileWallTR = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/Airlock_Wall_Corner_TR.asset");
            Tile tileWallBL = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/Airlock_Wall_Corner_BL.asset");
            Tile tileWallBR = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/Airlock_Wall_Corner_BR.asset");

            // 1. Clear outer border tiles while safely preserving connecting corridors
            for (int x = -6; x <= 5; x++)
            {
                for (int y = 7; y <= 18; y++)
                {
                    // Preserve connecting corridors:
                    // West corridor: X in [-6, -5], Y in [11, 14]
                    if (x <= -5 && (y >= 11 && y <= 14)) continue;
                    // North corridor: X in [-2, 1], Y >= 17
                    if ((x >= -2 && x <= 1) && y >= 17) continue;
                    // South corridor: X in [-2, 1], Y <= 8
                    if ((x >= -2 && x <= 1) && y <= 8) continue;

                    if (x == -6 || x == 5 || y == 7 || y == 18)
                    {
                        Vector3Int p = new Vector3Int(x, y, 0);
                        if (floorMap.HasTile(p)) floorMap.SetTile(p, null);
                        if (wallMap != null && wallMap.HasTile(p)) wallMap.SetTile(p, null);
                    }
                }
            }

            // 2. Paint 10x10 Room Floor: X in [-4, 3], Y in [9, 16] (8x8 interior)
            Undo.RecordObject(floorMap, "Decorate Airlock Floor");

            for (int x = -4; x <= 3; x++)
            {
                for (int y = 9; y <= 16; y++)
                {
                    Vector3Int pos = new Vector3Int(x, y, 0);
                    if (wallMap != null && wallMap.HasTile(pos)) wallMap.SetTile(pos, null);

                    // Drainage grates: Col 2 (X=-3) and Col 7 (X=2), Y in [11, 14]
                    if ((x == -3 || x == 2) && (y >= 11 && y <= 14))
                    {
                        floorMap.SetTile(pos, tileGrateV ?? tileClean);
                    }
                    // 4 Player Spawn Nodes: X in [-2, 1], Y=10
                    else if ((x >= -2 && x <= 1) && y == 10)
                    {
                        floorMap.SetTile(pos, tileSpawnPad ?? tileClean);
                    }
                    else
                    {
                        floorMap.SetTile(pos, tileClean);
                    }
                }
            }

            // Doorway thresholds: connecting to outer corridors
            // Sàn vàng sọc đen đặt phía dưới cánh cửa, khi mở ra sẽ thấy:
            // 1. Dưới cửa Bắc (Y=17, X in [-1, 0])
            floorMap.SetTile(new Vector3Int(-1, 17, 0), tileHazTop ?? tileClean);
            floorMap.SetTile(new Vector3Int(0, 17, 0), tileHazTop ?? tileClean);

            // 2. Dưới cửa Nam (Y=8, X in [-1, 0])
            floorMap.SetTile(new Vector3Int(-1, 8, 0), tileHazBot ?? tileClean);
            floorMap.SetTile(new Vector3Int(0, 8, 0), tileHazBot ?? tileClean);
            floorMap.SetTile(new Vector3Int(-1, 7, 0), tileClean);
            floorMap.SetTile(new Vector3Int(0, 7, 0), tileClean);

            // 3. Dưới cửa Tây (X=-5, Y in [12, 13])
            floorMap.SetTile(new Vector3Int(-5, 12, 0), tileHazLeft ?? tileClean);
            floorMap.SetTile(new Vector3Int(-5, 13, 0), tileHazLeft ?? tileClean);
            // X=-6 belongs to Bedroom corridor (preserved)

            // 3. Paint Perimeter Bulkhead Walls on Wall_Tilemap (10x10)
            if (wallMap != null)
            {
                Undo.RecordObject(wallMap, "Decorate Airlock Walls");

                // North Wall (Y = 17, X in [-5, 4])
                for (int x = -5; x <= 4; x++)
                {
                    if (x == -1 || x == 0) {
                        wallMap.SetTile(new Vector3Int(x, 17, 0), null);
                        continue;
                    }
                    Tile wTile = (x == -3 || x == 2) ? (tileWallLightH ?? tileWallNorth)
                               : tileWallNorth;
                    wallMap.SetTile(new Vector3Int(x, 17, 0), wTile);
                    floorMap.SetTile(new Vector3Int(x, 17, 0), null);
                }

                // South Wall (Y = 8, X in [-5, 4])
                for (int x = -5; x <= 4; x++)
                {
                    if (x == -1 || x == 0) {
                        wallMap.SetTile(new Vector3Int(x, 8, 0), null);
                        continue;
                    }
                    Tile wTile = (x == -3 || x == 2) ? (tileWallLightSouth ?? tileWallLightH ?? tileWallSouth)
                               : tileWallSouth;
                    wallMap.SetTile(new Vector3Int(x, 8, 0), wTile);
                    floorMap.SetTile(new Vector3Int(x, 8, 0), null);
                }

                // West Wall (X = -5, Y in [8, 17])
                for (int y = 8; y <= 17; y++)
                {
                    if (y == 12 || y == 13) {
                        wallMap.SetTile(new Vector3Int(-5, y, 0), null);
                        continue;
                    }
                    Tile wTile = (y == 15 || y == 10) ? (tileWallBeacon ?? tileWallWest) : tileWallWest;
                    wallMap.SetTile(new Vector3Int(-5, y, 0), wTile);
                    floorMap.SetTile(new Vector3Int(-5, y, 0), null);
                }

                // East Wall (X = 4, Y in [8, 17])
                for (int y = 8; y <= 17; y++)
                {
                    Tile wTile = (y == 15 || y == 10) ? (tileWallBeaconEast ?? tileWallBeacon ?? tileWallEast)
                               : tileWallEast;
                    wallMap.SetTile(new Vector3Int(4, y, 0), wTile);
                    floorMap.SetTile(new Vector3Int(4, y, 0), null);
                }

                // 4 Corners
                wallMap.SetTile(new Vector3Int(-5, 17, 0), tileWallTL);
                wallMap.SetTile(new Vector3Int(4, 17, 0), tileWallTR);
                wallMap.SetTile(new Vector3Int(-5, 8, 0), tileWallBL);
                wallMap.SetTile(new Vector3Int(4, 8, 0), tileWallBR);

                // Reconnect corridor walls to prevent any holes into the void
                wallMap.SetTile(new Vector3Int(-2, 18, 0), tileWallWest);
                wallMap.SetTile(new Vector3Int(1, 18, 0), tileWallEast);
                wallMap.SetTile(new Vector3Int(-2, 7, 0), tileWallWest);
                wallMap.SetTile(new Vector3Int(1, 7, 0), tileWallEast);
                // X=-6 wall belongs to Bedroom corridor (preserved)
            }

            // Material (URP 2D Lit)
            Material litMat = AssetDatabase.LoadAssetAtPath<Material>("Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Lit-Default.mat");
            if (litMat == null)
            {
                Shader litShader = Shader.Find("Universal Render Pipeline/2D/Sprite-Lit-Default");
                if (litShader != null) litMat = new Material(litShader);
            }

            // Load Doors & Sprites
            Sprite doorHClosed = AssetDatabase.LoadAssetAtPath<Sprite>($"{PropsSourceDir}/Door_Horizontal_Closed.png") 
                ?? AssetDatabase.LoadAssetAtPath<Sprite>($"{PropsSourceDir}/Door_Horizontal_Art.png");
            Sprite doorHOpen = AssetDatabase.LoadAssetAtPath<Sprite>($"{PropsSourceDir}/Door_Horizontal_Open.png");

            Sprite doorVClosed = AssetDatabase.LoadAssetAtPath<Sprite>($"{PropsSourceDir}/Door_Airlock_Vertical_Closed.png") 
                ?? AssetDatabase.LoadAssetAtPath<Sprite>($"{PropsSourceDir}/Door_Horizontal_Closed.png");
            Sprite doorVOpen = AssetDatabase.LoadAssetAtPath<Sprite>($"{PropsSourceDir}/Door_Airlock_Vertical_Open.png") 
                ?? AssetDatabase.LoadAssetAtPath<Sprite>($"{PropsSourceDir}/Door_Horizontal_Open.png");

            Sprite lightRed = AssetDatabase.LoadAssetAtPath<Sprite>($"{PropsSourceDir}/Door_Light_Red.png");
            Sprite lightGreen = AssetDatabase.LoadAssetAtPath<Sprite>($"{PropsSourceDir}/Door_Light_Green.png");

            // Props Root Container
            Selection.objects = new Object[0];
            GameObject oldProps = GameObject.Find("Airlock_Decorations") ?? GameObject.Find("Airlock_Props");
            if (oldProps != null)
            {
                Undo.DestroyObjectImmediate(oldProps);
            }

            GameObject propsRoot = new GameObject("Airlock_Decorations");
            Undo.RegisterCreatedObjectUndo(propsRoot, "Create Airlock Decorations");

            // Configure DoorControllers
            DoorController[] allDoors = Object.FindObjectsByType<DoorController>(FindObjectsInactive.Exclude);
            foreach (DoorController d in allDoors)
            {
                if (d.name.Contains("Airlock"))
                {
                    Undo.RecordObject(d.transform, "Align Airlock Door");
                    Undo.RecordObject(d, "Update Airlock Door Colors");

                    SpriteRenderer dsr = d.GetComponent<SpriteRenderer>();
                    if (dsr != null) Undo.RecordObject(dsr, "Update Airlock Door Sprite");

                    BoxCollider2D box = d.GetComponent<BoxCollider2D>();
                    if (box != null) Undo.RecordObject(box, "Update Airlock Door Collider");

                    d.transform.localScale = Vector3.one;

                    if (d.name.Contains("North"))
                    {
                        // 2x1 Horizontal Door (256x128 = exactly 2 tiles) centered at (0.0, 17.5) in Row 17 wall gap
                        d.transform.position = new Vector3(0.0f, 17.5f, 0f);
                        d.transform.rotation = Quaternion.identity;
                        if (box != null) { box.size = new Vector2(2.0f, 1.0f); box.offset = Vector2.zero; }
                        d.SetSprites(doorHClosed, doorHOpen);
                        CreateIndicatorLight(propsRoot, d, new Vector3(1.35f, 17.5f, 0f), lightRed, lightGreen, litMat);
                    }
                    else if (d.name.Contains("South"))
                    {
                        // 2x1 Horizontal Door (256x128 = exactly 2 tiles) centered at (0.0, 8.5) in Row 8 wall gap
                        d.transform.position = new Vector3(0.0f, 8.5f, 0f);
                        d.transform.rotation = Quaternion.identity;
                        if (box != null) { box.size = new Vector2(2.0f, 1.0f); box.offset = Vector2.zero; }
                        d.SetSprites(doorHClosed, doorHOpen);
                        CreateIndicatorLight(propsRoot, d, new Vector3(1.35f, 8.5f, 0f), lightRed, lightGreen, litMat);
                    }
                    else if (d.name.Contains("West"))
                    {
                        // 1x2 Vertical Door (128x256 = exactly 2 tiles) centered at (-4.5, 13.0) in Col -5 wall gap
                        d.transform.position = new Vector3(-4.5f, 13.0f, 0f);
                        d.transform.rotation = Quaternion.identity;
                        if (box != null) { box.size = new Vector2(1.0f, 2.0f); box.offset = Vector2.zero; }
                        d.SetSprites(doorVClosed, doorVOpen);
                        CreateIndicatorLight(propsRoot, d, new Vector3(-5.0f, 11.25f, 0f), lightRed, lightGreen, litMat);
                    }

                    if (dsr != null)
                    {
                        if (litMat != null) dsr.material = litMat;
                        dsr.sortingLayerName = "Obstacles";
                        dsr.sortingOrder = 5;
                    }

                    EditorUtility.SetDirty(d);
                }
            }

            // Decorate Props Matching Mockup_Airlock_Room.png with pixel-perfect alignment
            // 1. Center Floor Emblem Decal (Chevron + "AIRLOCK" stencil) - Exactly at Room Center (0, 13)
            CreateProp(propsRoot, "Prop_Airlock_Center_Emblem", new Vector3(0.0f, 13.0f, 0f), Vector2.zero, Vector2.zero, "Floor", 1, litMat, false);

            // 2. NW Triple Spacesuit Station (3-Bay Locker): Flush against NW corner (Cols -4..-3, Rows 15..16)
            CreateProp(propsRoot, "Prop_Suit_Lockers", new Vector3(-3.0f, 16.0f, 0f), new Vector2(2.0f, 2.0f), Vector2.zero, "Obstacles", 2, litMat, true);

            // 3. Center Diagnostic Radar Console Desk (Above Center Emblem)
            CreateProp(propsRoot, "Airlock_Prop_Radar_CRT", new Vector3(0.0f, 14.85f, 0f), new Vector2(1.8f, 1.2f), new Vector2(0f, -0.1f), "Obstacles", 2, litMat, true);

            // 4. SW Stacked Cargo Crates: Flush in SW corner on floor (above South wall)
            CreateProp(propsRoot, "Prop_Supply_Crate_Stacked", new Vector3(-3.0f, 9.85f, 0f), new Vector2(1.8f, 1.2f), Vector2.zero, "Obstacles", 2, litMat, true, "Prop_Supply_Crate_SW");

            // 5. NE Yellow Crate: Flush in NE corner (Col 3, Row 16)
            CreateProp(propsRoot, "Prop_Supply_Crate", new Vector3(3.5f, 16.5f, 0f), new Vector2(1.0f, 1.0f), Vector2.zero, "Obstacles", 2, litMat, true, "Prop_Supply_Crate_NE");

            // 6. SE Equipment Locker Cage (mesh wireframe door): Flush against East wall (Col 3, Rows 11..12)
            CreateProp(propsRoot, "Prop_Airlock_Equipment_Locker", new Vector3(3.5f, 11.5f, 0f), new Vector2(1.0f, 2.0f), Vector2.zero, "Obstacles", 2, litMat, true);

            // Atmosphere & 2D Point Lights
            CreatePointLight(propsRoot, "Light_Airlock_Ambient", new Vector3(0.0f, 13.0f, 0f), new Color(0.40f, 0.50f, 0.60f, 1f), 0.75f, 10.0f);
            CreatePointLight(propsRoot, "Light_Neon_North_Left", new Vector3(-2.5f, 16.5f, 0f), new Color(0.1f, 0.85f, 0.95f, 1f), 1.0f, 3.5f);
            CreatePointLight(propsRoot, "Light_Neon_North_Right", new Vector3(1.5f, 16.5f, 0f), new Color(0.1f, 0.85f, 0.95f, 1f), 1.0f, 3.5f);
            CreatePointLight(propsRoot, "Light_Neon_South_Left", new Vector3(-2.5f, 8.5f, 0f), new Color(0.1f, 0.85f, 0.95f, 1f), 1.0f, 3.5f);
            CreatePointLight(propsRoot, "Light_Neon_South_Right", new Vector3(1.5f, 8.5f, 0f), new Color(0.1f, 0.85f, 0.95f, 1f), 1.0f, 3.5f);
            CreatePointLight(propsRoot, "Light_Console_Radar", new Vector3(0.0f, 15.2f, 0f), new Color(0.1f, 0.95f, 0.80f, 1f), 1.1f, 2.5f);

            // Update Spawn Points to the exact centers of the 4 nodes on the spawn pad
            GameObject sp1 = GameObject.Find("SpawnPoint_P1");
            if (sp1 != null) sp1.transform.position = new Vector3(-1.5f, 10.5f, 0f);
            GameObject sp2 = GameObject.Find("SpawnPoint_P2");
            if (sp2 != null) sp2.transform.position = new Vector3(-0.5f, 10.5f, 0f);
            GameObject sp3 = GameObject.Find("SpawnPoint_P3");
            if (sp3 != null) sp3.transform.position = new Vector3(0.5f, 10.5f, 0f);
            GameObject sp4 = GameObject.Find("SpawnPoint_P4");
            if (sp4 != null) sp4.transform.position = new Vector3(1.5f, 10.5f, 0f);

            // Update Player position to center of P2 spawn pad
            GameObject playerGo = GameObject.Find("Player");
            if (playerGo != null) playerGo.transform.position = new Vector3(-0.5f, 10.5f, 0f);

            // Update Exit_Zone position to room center
            GameObject exitGo = GameObject.Find("Exit_Zone");
            if (exitGo != null) exitGo.transform.position = new Vector3(0.0f, 13.0f, 0f);

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
        }

        private static GameObject CreatePointLight(GameObject parent, string name, Vector3 position, Color color, float intensity, float radius)
        {
            GameObject lightGo = new GameObject(name);
            lightGo.transform.SetParent(parent.transform);
            lightGo.transform.position = position;

            Light2D light = lightGo.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.pointLightOuterRadius = radius;
            light.pointLightInnerRadius = 0.2f;
            light.enabled = false;

            return lightGo;
        }

        private static GameObject CreateIndicatorLight(GameObject parent, DoorController door, Vector3 position, Sprite red, Sprite green, Material mat)
        {
            GameObject lightGo = new GameObject($"Indicator_{door.name}");
            lightGo.transform.SetParent(parent.transform);
            lightGo.transform.position = position;

            SpriteRenderer sr = lightGo.AddComponent<SpriteRenderer>();
            sr.sprite = red;
            sr.sortingLayerName = "Obstacles";
            sr.sortingOrder = 6;
            if (mat != null) sr.material = mat;

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

        private static GameObject CreateProp(GameObject parent, string propSpriteName, Vector3 position, Vector2 colliderSize, Vector2 colliderOffset, string sortingLayer, int sortingOrder, Material mat, bool addCollider, string gameObjectName = null)
        {
            string spritePath = $"{PropsSourceDir}/{propSpriteName}.png";
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
            if (sprite == null) return null;

            GameObject propGo = new GameObject(string.IsNullOrEmpty(gameObjectName) ? propSpriteName : gameObjectName);
            propGo.transform.SetParent(parent.transform);
            propGo.transform.position = position;

            SpriteRenderer sr = propGo.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingLayerName = sortingLayer;
            sr.sortingOrder = sortingOrder;
            if (mat != null) sr.material = mat;

            if (addCollider && colliderSize != Vector2.zero)
            {
                BoxCollider2D box = propGo.AddComponent<BoxCollider2D>();
                box.size = colliderSize;
                box.offset = colliderOffset;
                int obstacleLayer = LayerMask.NameToLayer("Obstacle");
                if (obstacleLayer != -1) propGo.layer = obstacleLayer;
            }

            return propGo;
        }
    }
}
#endif
