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

        [MenuItem("Tools/Facility/Setup Airlock Art & Environment", false, 50)]
        public static void SetupAirlockArt()
        {
            // 1. Configure and import Tile textures
            ConfigureTileTextures();

            // 2. Configure and import Prop textures with realistic character proportions (PPU = 240)
            ConfigurePropTextures();

            // 3. Create Tile ScriptableObjects
            CreateTileAssets();

            // 4. Decorate Airlock_Spawn in current scene
            DecorateActiveScene();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("<color=green>[AirlockEnvironmentSetup] Successfully configured tiles, props, and decorated Airlock_Spawn!</color>");
        }

        private static void ConfigureTileTextures()
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

                    if (changed)
                    {
                        EditorUtility.SetDirty(importer);
                        importer.SaveAndReimport();
                    }
                }
            }
        }

        private static void ConfigurePropTextures()
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
                    
                    // All props and door sprites are mapped to 1 unit = 128 px (1 tile = 128 px = 1 unit)
                    if (importer.spritePixelsPerUnit != 128f) { importer.spritePixelsPerUnit = 128f; changed = true; }

                    if (importer.filterMode != FilterMode.Point) { importer.filterMode = FilterMode.Point; changed = true; }
                    if (!importer.alphaIsTransparency) { importer.alphaIsTransparency = true; changed = true; }
                    if (importer.textureCompression != TextureImporterCompression.Uncompressed) { importer.textureCompression = TextureImporterCompression.Uncompressed; changed = true; }

                    if (changed)
                    {
                        EditorUtility.SetDirty(importer);
                        importer.SaveAndReimport();
                    }
                }
            }
        }

        private static void CreateTileAssets()
        {
            if (!Directory.Exists(TileAssetsDir))
            {
                Directory.CreateDirectory(TileAssetsDir);
            }

            string[] files = Directory.GetFiles(TilesSourceDir, "*.png");
            foreach (string file in files)
            {
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

        private static void DecorateActiveScene()
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
                Debug.LogError("[AirlockEnvironmentSetup] Grid (Facility_Grid) not found in the active scene! Please ensure SampleScene is open.");
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
                Debug.LogError("[AirlockEnvironmentSetup] Floor Tilemap (Floor_Tilemap) not found under Facility_Grid!");
                return;
            }

            // Load floor tiles
            Tile tileClean = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/Airlock_Floor_Clean.asset");
            Tile tileScratched = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/Airlock_Floor_Scratched.asset");
            Tile tileRusty1 = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/Airlock_Floor_Rusty1.asset");
            Tile tileRusty2 = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/Airlock_Floor_Rusty2.asset");
            Tile tileWeathered = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/Airlock_Floor_Weathered.asset");
            Tile tileRusted = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/Airlock_Floor_Rusted.asset");
            Tile tileOil = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/Airlock_Floor_Oil.asset");
            Tile tileDark = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/Airlock_Floor_Dark.asset");

            Tile tileGrateH = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/Airlock_Grate_Horizontal.asset");
            Tile tileGrateV = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/Airlock_Grate_Vertical.asset");

            Tile tileHazTop = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/Airlock_Hazard_Top.asset");
            Tile tileHazBot = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/Airlock_Hazard_Bottom.asset");
            Tile tileHazLeft = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/Airlock_Hazard_Left.asset");
            Tile tileHazRight = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/Airlock_Hazard_Right.asset");

            // Load wall tiles
            Tile tileWallTop = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/Airlock_Wall_Pipes_Top.asset");
            Tile tileWallBot = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/Airlock_Wall_Pipes_Bottom.asset");
            Tile tileWallV = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/Airlock_Wall_Conduit_V.asset");
            Tile tileWallSteel = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/Airlock_Wall_Steel.asset");
            Tile tileWallTL = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/Airlock_Wall_Corner_TL.asset");
            Tile tileWallTL_Pipes = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/Airlock_Wall_Pipes_Corner_TL.asset") ?? tileWallTL;
            Tile tileWallTR = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/Airlock_Wall_Pipes_Corner_TR.asset");
            Tile tileWallBR = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/Airlock_Wall_Corner_BR.asset");
            Tile tileWallBL = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/Airlock_Wall_Pipes_Corner_BL.asset")
                ?? AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/Airlock_Wall_Corner_BL.asset");
            Tile tileWallClaw = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/Airlock_Wall_Torn_Claw.asset");
            Tile tileWallWires = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/Airlock_Wall_Torn_Wires.asset");
            Tile tileFloorDent = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/Airlock_Floor_Bullet_Dent.asset");

            Tile[] floorPool = new Tile[] { tileClean, tileScratched, tileRusty1, tileRusty2, tileWeathered, tileRusted, tileOil, tileDark };

            // 1. Paint Entire 10x10 Interior Floor: X in [-5, 4], Y in [8, 17]
            Undo.RecordObject(floorMap, "Decorate Airlock Floor");

            Tile baseDeck = tileScratched ?? tileClean;

            for (int x = -5; x <= 4; x++)
            {
                for (int y = 8; y <= 17; y++)
                {
                    Vector3Int pos = new Vector3Int(x, y, 0);

                    // Central Drainage grate channel across room at Y = 11
                    if (y == 11 && x >= -4 && x <= 3)
                    {
                        floorMap.SetTile(pos, tileGrateH ?? tileClean);
                    }
                    // Heavy-duty foundation under EVA suit lockers (North wall: x in [-5, -2], y = 17)
                    else if (y == 17 && x >= -5 && x <= -2)
                    {
                        floorMap.SetTile(pos, tileDark ?? baseDeck);
                    }
                    // Subtle cargo storage wear in SE corner under supply crate (x = 4, y in [8, 9])
                    else if (x == 4 && (y == 8 || y == 9))
                    {
                        floorMap.SetTile(pos, tileRusty1 ?? baseDeck);
                    }
                    // Maintenance corner near toolbox (x = -5, y = 8)
                    else if (x == -5 && y == 8)
                    {
                        floorMap.SetTile(pos, tileRusty2 ?? baseDeck);
                    }
                    // Bullet impact / crater in SW maintenance zone near toolbox (x = -4, y = 9)
                    else if (x == -4 && y == 9)
                    {
                        floorMap.SetTile(pos, tileFloorDent ?? tileRusty2 ?? baseDeck);
                    }
                    // Perimeter border steel plates along walls for clean framing
                    else if (x == -5 || x == 4 || y == 8 || y == 17)
                    {
                        floorMap.SetTile(pos, tileClean ?? baseDeck);
                    }
                    // Main cohesive interior diamond plate deck
                    else
                    {
                        floorMap.SetTile(pos, baseDeck);
                    }
                }
            }

            // Doorway thresholds: Paint hazard stripes right at the doorway openings bridging room to corridors
            // North Doorway Opening (x in [-1, 0], y = 18)
            floorMap.SetTile(new Vector3Int(-1, 18, 0), tileHazTop ?? tileClean);
            floorMap.SetTile(new Vector3Int(0, 18, 0), tileHazTop ?? tileClean);

            // South Doorway Opening (x in [-1, 0], y = 7)
            floorMap.SetTile(new Vector3Int(-1, 7, 0), tileHazBot ?? tileClean);
            floorMap.SetTile(new Vector3Int(0, 7, 0), tileHazBot ?? tileClean);

            // West Doorway Opening (x = -6, y in [12, 13])
            floorMap.SetTile(new Vector3Int(-6, 12, 0), tileHazLeft ?? tileClean);
            floorMap.SetTile(new Vector3Int(-6, 13, 0), tileHazLeft ?? tileClean);

            // 2. Paint Perimeter Bulkhead Walls on Wall_Tilemap
            if (wallMap != null)
            {
                Undo.RecordObject(wallMap, "Decorate Airlock Walls");

                // North Wall (y = 18, x in [-5..4] except doorway [-1..0])
                for (int x = -5; x <= 4; x++)
                {
                    if (x == -1 || x == 0) continue;
                    wallMap.SetTile(new Vector3Int(x, 18, 0), tileWallTop ?? tileWallSteel);
                }

                // South Wall (y = 7, x in [-5..4] except doorway [-1..0])
                for (int x = -5; x <= 4; x++)
                {
                    if (x == -1 || x == 0) continue;
                    wallMap.SetTile(new Vector3Int(x, 7, 0), tileWallBot ?? tileWallSteel);
                }

                // West Wall (x = -6, y in [8..17] except doorway [12..13])
                for (int y = 8; y <= 17; y++)
                {
                    if (y == 12 || y == 13) continue;
                    wallMap.SetTile(new Vector3Int(-6, y, 0), tileWallV ?? tileWallSteel);
                }

                // East Wall (x = 5, y in [8..17]) - Torn bulkhead breach sections
                for (int y = 8; y <= 17; y++)
                {
                    if (y == 10)
                    {
                        wallMap.SetTile(new Vector3Int(5, y, 0), tileWallClaw ?? tileWallV ?? tileWallSteel);
                    }
                    else if (y == 15)
                    {
                        wallMap.SetTile(new Vector3Int(5, y, 0), tileWallWires ?? tileWallV ?? tileWallSteel);
                    }
                    else
                    {
                        wallMap.SetTile(new Vector3Int(5, y, 0), tileWallV ?? tileWallSteel);
                    }
                }

                // 4 Corners
                wallMap.SetTile(new Vector3Int(-6, 18, 0), tileWallTL_Pipes ?? tileWallSteel);
                wallMap.SetTile(new Vector3Int(5, 18, 0), tileWallTR ?? tileWallSteel);
                wallMap.SetTile(new Vector3Int(5, 7, 0), tileWallBR ?? tileWallSteel);
                wallMap.SetTile(new Vector3Int(-6, 7, 0), tileWallBL ?? tileWallSteel);
            }

            // 3. Align and Configure Doors in Airlock
            Shader unlitShader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default") ?? Shader.Find("Sprites/Default");
            Material unlitMat = unlitShader != null ? new Material(unlitShader) : null;

            Sprite doorHClosed = AssetDatabase.LoadAssetAtPath<Sprite>($"{PropsSourceDir}/Door_Horizontal_Closed.png") 
                ?? AssetDatabase.LoadAssetAtPath<Sprite>($"{PropsSourceDir}/Door_Horizontal_Art.png");
            Sprite doorHOpen = AssetDatabase.LoadAssetAtPath<Sprite>($"{PropsSourceDir}/Door_Horizontal_Open.png");

            Sprite doorVClosed = AssetDatabase.LoadAssetAtPath<Sprite>($"{PropsSourceDir}/Door_Vertical_Closed.png") 
                ?? AssetDatabase.LoadAssetAtPath<Sprite>($"{PropsSourceDir}/Door_Vertical_Art.png");
            Sprite doorVOpen = AssetDatabase.LoadAssetAtPath<Sprite>($"{PropsSourceDir}/Door_Vertical_Open.png");

            Sprite lightRed = AssetDatabase.LoadAssetAtPath<Sprite>($"{PropsSourceDir}/Door_Light_Red.png");
            Sprite lightGreen = AssetDatabase.LoadAssetAtPath<Sprite>($"{PropsSourceDir}/Door_Light_Green.png");

            // 4. Props & Indicator Lights Root Container
            GameObject oldProps = GameObject.Find("Airlock_Decorations");
            if (oldProps != null)
            {
                Undo.DestroyObjectImmediate(oldProps);
            }

            GameObject propsRoot = new GameObject("Airlock_Decorations");
            Undo.RegisterCreatedObjectUndo(propsRoot, "Create Airlock Decorations");

            DoorController[] allDoors = Object.FindObjectsByType<DoorController>();
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

                    if (d.name.Contains("North"))
                    {
                        d.transform.position = new Vector3(0.0f, 18.5f, 0f);
                        if (box != null) { box.size = new Vector2(2.0f, 1.0f); box.offset = Vector2.zero; }
                        d.SetSprites(doorHClosed, doorHOpen);
                        CreateIndicatorLight(propsRoot, d, new Vector3(1.35f, 18.5f, 0f), lightRed, lightGreen, unlitMat);
                    }
                    else if (d.name.Contains("South"))
                    {
                        d.transform.position = new Vector3(0.0f, 7.5f, 0f);
                        if (box != null) { box.size = new Vector2(2.0f, 1.0f); box.offset = Vector2.zero; }
                        d.SetSprites(doorHClosed, doorHOpen);
                        CreateIndicatorLight(propsRoot, d, new Vector3(1.35f, 7.5f, 0f), lightRed, lightGreen, unlitMat);
                    }
                    else if (d.name.Contains("West"))
                    {
                        d.transform.position = new Vector3(-5.5f, 13.0f, 0f);
                        if (box != null) { box.size = new Vector2(1.0f, 2.0f); box.offset = Vector2.zero; }
                        d.SetSprites(doorVClosed, doorVOpen);
                        CreateIndicatorLight(propsRoot, d, new Vector3(-5.5f, 14.35f, 0f), lightRed, lightGreen, unlitMat);
                    }

                    if (dsr != null)
                    {
                        if (unlitMat != null) dsr.material = unlitMat;
                        dsr.sortingLayerName = "Obstacles";
                        dsr.sortingOrder = 5;
                    }

                    EditorUtility.SetDirty(d);
                }
            }

            // 5. Decorate Props with Exact Integer Grid Units & Flush Wall Placement:
            // 1. EVA Suit Lockers: Flush against North wall, exactly 3 tiles wide (X in [-5, -2], Y in [16.75, 18.0])
            CreateProp(propsRoot, "Prop_Suit_Lockers", new Vector3(-3.5f, 17.375f, 0f), new Vector2(3.0f, 1.0f), new Vector2(0f, 0.125f), "Obstacles", 2, unlitMat, true);

            // 2. Supply Crate: Flush in SE corner, exactly 1x1 tile (X in [4, 5], Y in [8, 9])
            CreateProp(propsRoot, "Prop_Supply_Crate", new Vector3(4.5f, 8.5f, 0f), new Vector2(1.0f, 1.0f), Vector2.zero, "Obstacles", 2, unlitMat, true);

            // 4. Toolbox: Flush in SW corner against South wall, exactly 1 tile wide (X in [-5, -4], Y in [8, 8.75])
            CreateProp(propsRoot, "Prop_Toolbox", new Vector3(-4.5f, 8.375f, 0f), new Vector2(1.0f, 0.75f), Vector2.zero, "Obstacles", 2, unlitMat, true);

            // 5. Horror Environmental Storytelling Props & Decals:
            // - Blood-splattered Pressure Gauge near North airlock door control
            CreateProp(propsRoot, "Airlock_Decal_Blood_Gauge", new Vector3(1.85f, 18.0f, 0f), Vector2.zero, Vector2.zero, "Obstacles", 6, unlitMat, false);

            // - Yellow DANGER Warning Sign next to North airlock door
            CreateProp(propsRoot, "Airlock_Decal_Danger_Sign", new Vector3(-1.85f, 18.0f, 0f), Vector2.zero, Vector2.zero, "Obstacles", 6, unlitMat, false);


            // - Emergency Power Cutoff Switch with hazard stripes on West Wall
            CreateProp(propsRoot, "Airlock_Prop_Emergency_Switch", new Vector3(-5.35f, 11.2f, 0f), Vector2.zero, Vector2.zero, "Obstacles", 6, unlitMat, false);

            // - High-Voltage Breaker Box with warning triangle on West Wall
            CreateProp(propsRoot, "Airlock_Prop_Breaker_Box", new Vector3(-5.35f, 9.8f, 0f), Vector2.zero, Vector2.zero, "Obstacles", 6, unlitMat, false);

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
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

        private static GameObject CreateProp(GameObject parent, string propSpriteName, Vector3 position, Vector2 colliderSize, Vector2 colliderOffset, string sortingLayer, int sortingOrder, Material mat, bool addCollider)
        {
            string spritePath = $"{PropsSourceDir}/{propSpriteName}.png";
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
            if (sprite == null) return null;

            GameObject propGo = new GameObject(propSpriteName);
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
