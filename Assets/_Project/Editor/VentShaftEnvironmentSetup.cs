#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Tilemaps;
using TheLastSignal.Entities.Environment;

namespace TheLastSignal.EditorTools
{
    public static class VentShaftEnvironmentSetup
    {
        private const string TilesSourceDir = "Assets/_Project/Sprites/Environments/VentShaft/Tiles";
        private const string PropsSourceDir = "Assets/_Project/Sprites/Environments/VentShaft/Props";
        private const string TileAssetsDir = "Assets/_Project/Tiles/VentShaft";
        private const string PixelSmoothMatPath = "Assets/_Project/Materials/M_Sprite_Lit_PixelSmooth.mat";
        private const string SteamMatPath = "Assets/_Project/Art/Environment/Interactables/SafeLightJunction/Mat_VFX_Steam.mat";

        private static bool IsReadOnlyOrPlayMode()
        {
            return EditorApplication.isPlayingOrWillChangePlaymode
                || EditorApplication.isPlaying
                || System.Environment.CommandLine.IndexOf("-readonly", System.StringComparison.OrdinalIgnoreCase) >= 0
                || System.Environment.CommandLine.IndexOf("--virtual-project-clone", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        [MenuItem("Tools/Facility/Setup Vent Shaft & Corridor Art", false, 52)]
        public static void SetupVentShaftArt()
        {
            if (IsReadOnlyOrPlayMode()) return;

            Debug.Log("[VentShaftEnvironmentSetup] Starting Vent Shaft final polish pass...");

            // 1. Configure and import Tile textures
            ConfigureTileTextures();

            // 2. Configure and import Prop textures
            ConfigurePropTextures();

            // 3. Create Tile ScriptableObjects
            CreateTileAssets();

            // 4. Decorate Vent Shaft and Corridor in the active scene
            DecorateActiveScene();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("<color=green>[VentShaftEnvironmentSetup] Successfully built and integrated Vent Shaft final polish pass!</color>");
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

        public static void DecorateActiveScene()
        {
            GameObject gridGo = GameObject.Find("Facility_Grid") ?? GameObject.Find("Grid");
            Grid grid = gridGo != null ? gridGo.GetComponent<Grid>() : Object.FindAnyObjectByType<Grid>();

            if (grid == null)
            {
                Debug.LogError("[VentShaftEnvironmentSetup] Facility_Grid not found!");
                return;
            }

            Tilemap floorMap = null;
            Tilemap wallMap = null;
            Tilemap[] allTilemaps = grid.GetComponentsInChildren<Tilemap>(true);
            foreach (Tilemap tm in allTilemaps)
            {
                if (tm.name.IndexOf("Floor", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    floorMap = tm;
                else if (tm.name.IndexOf("Wall", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    wallMap = tm;
            }

            if (floorMap == null || wallMap == null)
            {
                Debug.LogError("[VentShaftEnvironmentSetup] Floor or Wall Tilemap not found!");
                return;
            }

            Material pixelSmoothMat = AssetDatabase.LoadAssetAtPath<Material>(PixelSmoothMatPath);
            Material steamMat = AssetDatabase.LoadAssetAtPath<Material>(SteamMatPath);

            // Ensure tilemap renderers use pixel-smooth material
            TilemapRenderer fRend = floorMap.GetComponent<TilemapRenderer>();
            if (fRend != null && pixelSmoothMat != null) fRend.sharedMaterial = pixelSmoothMat;
            TilemapRenderer wRend = wallMap.GetComponent<TilemapRenderer>();
            if (wRend != null && pixelSmoothMat != null) wRend.sharedMaterial = pixelSmoothMat;

            // Load Tiles
            Tile fSteel1 = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/VS_Floor_Steel_01.asset");
            Tile fSteel2 = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/VS_Floor_Steel_02.asset");
            Tile fReinf = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/VS_Floor_Reinforced.asset");
            Tile fHatch = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/VS_Floor_Access_Hatch.asset");

            Tile fCorrVWest = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/VS_Corridor_Floor_V_West.asset");
            Tile fCorrVEast = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/VS_Corridor_Floor_V_East.asset");
            Tile fCorrHNorth = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/VS_Corridor_Floor_H_North.asset");
            Tile fCorrHSouth = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/VS_Corridor_Floor_H_South.asset");
            Tile fCorrCTL = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/VS_Corridor_Floor_Corner_TL.asset");
            Tile fCorrCTR = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/VS_Corridor_Floor_Corner_TR.asset");
            Tile fCorrCBL = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/VS_Corridor_Floor_Corner_BL.asset");
            Tile fCorrCBR = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/VS_Corridor_Floor_Corner_BR.asset");
            Tile fCorrBuckled = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/VS_Corridor_Floor_V_Buckled.asset");

            // Unified 2-tile seamless horizontal hazard stripe threshold
            Tile fHazThreshLeft = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/VS_Floor_Threshold_Hazard_Left.asset");
            Tile fHazThreshRight = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/VS_Floor_Threshold_Hazard_Right.asset");
            Tile fHazThreshH = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/VS_Floor_Hazard_Threshold_H.asset");

            Tile wStrH = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/VS_Wall_Straight_H.asset");
            Tile wStrV = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/VS_Wall_Straight_V.asset");
            Tile wDoorTop = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/VS_Wall_Doorframe_Top.asset");
            Tile wDoorBot = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/VS_Wall_Doorframe_Bottom.asset");
            Tile wRibVWest = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/VS_Wall_Corridor_V_West.asset");
            Tile wRibVEast = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/VS_Wall_Corridor_V_East.asset");
            Tile wRibHNorth = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/VS_Wall_Corridor_H_North.asset");
            Tile wRibHSouth = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/VS_Wall_Corridor_H_South.asset");
            Tile wSouthSolid = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/VS_Wall_South_Solid.asset");
            Tile wBracketJunction = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/VS_Wall_Corner_Bracket_Junction.asset");
            Tile wBracketL = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/VS_Wall_Corner_Bracket_L.asset");
            Tile wCTL = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/VS_Wall_Corner_TL.asset");
            Tile wCTR = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/VS_Wall_Corner_TR.asset");
            Tile wCBL = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/VS_Wall_Corner_BL.asset");
            Tile wCBR = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/VS_Wall_Corner_BR.asset");
            Tile wJambLeft = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/VS_Wall_Doorway_Jamb_Left.asset") ?? wBracketJunction;
            Tile wJambRight = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/VS_Wall_Doorway_Jamb_Right.asset") ?? wBracketJunction;

            Undo.RecordObject(floorMap, "Paint Vent Shaft & Corridor Floors");
            Undo.RecordObject(wallMap, "Paint Vent Shaft & Corridor Walls");

            // ========================================================
            // 1. PAINT VENT SHAFT FLOOR & WALLS
            // Inner floor: X in [12, 20], Y in [-9, 1]
            // West doorway: X=11, Y in [-5, -4]
            // SE threshold: X in [21, 22], Y = -9
            // ========================================================

            // Room Floor: 3 Coherent Categories
            // Category C Machinery Foundation Bed (top 1/3 of room): X in [12, 20], Y in [-2, 1]
            // Category B Drainage / Cable Access Hatch: (13, -7)
            // Category A General Circulation Floor (bottom 2/3 of room): #2B3135 across remaining floor
            for (int x = 12; x <= 20; x++)
            {
                for (int y = -9; y <= 1; y++)
                {
                    if (y >= -2 && y <= 1 && fReinf != null)
                    {
                        floorMap.SetTile(new Vector3Int(x, y, 0), fReinf);
                    }
                    else if (x == 13 && y == -7)
                    {
                        floorMap.SetTile(new Vector3Int(x, y, 0), fHatch);
                    }
                    else
                    {
                        Tile tile = ((x + y) % 2 == 0) ? fSteel1 : fSteel2;
                        floorMap.SetTile(new Vector3Int(x, y, 0), tile);
                    }
                }
            }

            // West Entrance Doorway (X=11, Y in [-5, -4])
            floorMap.SetTile(new Vector3Int(11, -4, 0), fSteel1);
            floorMap.SetTile(new Vector3Int(11, -5, 0), fSteel2);

            // Clean outside floor tiles at Node B threshold to avoid bright white bleed
            floorMap.SetTile(new Vector3Int(10, -4, 0), null);
            floorMap.SetTile(new Vector3Int(10, -5, 0), null);

            // Entrance Floor connecting to corridor: X in [21, 22] at Y = -8
            floorMap.SetTile(new Vector3Int(21, -8, 0), fSteel2);
            floorMap.SetTile(new Vector3Int(22, -8, 0), fSteel1);

            // Hazard Threshold Location 2: Single continuous 256px horizontal hazard stripe band
            floorMap.SetTile(new Vector3Int(21, -9, 0), fHazThreshLeft);
            floorMap.SetTile(new Vector3Int(22, -9, 0), fHazThreshRight);

            // --- VENT SHAFT WALLS ---
            // North Wall: Y = 2, X in [11, 21]
            wallMap.SetTile(new Vector3Int(11, 2, 0), wCTL);
            for (int x = 12; x <= 20; x++)
            {
                wallMap.SetTile(new Vector3Int(x, 2, 0), wStrH);
            }
            wallMap.SetTile(new Vector3Int(21, 2, 0), wCTR);

            // West Wall: X = 11, Y in [-8, 1]
            // Framed door opening at Y in [-5, -4] with top and bottom door jambs
            wallMap.SetTile(new Vector3Int(11, 1, 0), wStrV);
            wallMap.SetTile(new Vector3Int(11, 0, 0), wStrV);
            wallMap.SetTile(new Vector3Int(11, -1, 0), wStrV);
            wallMap.SetTile(new Vector3Int(11, -2, 0), wStrV);
            wallMap.SetTile(new Vector3Int(11, -3, 0), wDoorTop);     // Lintel doorframe
            wallMap.SetTile(new Vector3Int(11, -4, 0), null);          // Door opening
            wallMap.SetTile(new Vector3Int(11, -5, 0), null);          // Door opening
            wallMap.SetTile(new Vector3Int(11, -6, 0), wDoorBot);     // Base doorframe
            wallMap.SetTile(new Vector3Int(11, -7, 0), wStrV);
            wallMap.SetTile(new Vector3Int(11, -8, 0), wStrV);
            wallMap.SetTile(new Vector3Int(11, -9, 0), wStrV);

            // East Wall: X = 21, Y in [-7, 1] (ends at Y=-7 so doorway at Y=-8 is open!)
            for (int y = -7; y <= 1; y++)
            {
                wallMap.SetTile(new Vector3Int(21, y, 0), wStrV);
            }

            // South Wall: Y = -10, X in [11, 20]
            // Clearly defined steel panels connecting BL corner to corridor junction
            wallMap.SetTile(new Vector3Int(11, -10, 0), wCBL);
            for (int x = 12; x <= 19; x++)
            {
                wallMap.SetTile(new Vector3Int(x, -10, 0), wSouthSolid);
            }
            // South Wall junction at (X=20, Y=-10) turning into corridor West wall
            wallMap.SetTile(new Vector3Int(20, -10, 0), wBracketJunction);

            // Corridor Entrance: (21, -8) and (22, -8) are OPEN (zero wall barrier above hazard line)
            wallMap.SetTile(new Vector3Int(21, -8, 0), null);
            wallMap.SetTile(new Vector3Int(22, -8, 0), null);
            wallMap.SetTile(new Vector3Int(23, -8, 0), wCTR);

            // ========================================================
            // 2. PAINT CORRIDOR FLOORS & WALLS (L-SHAPED NETWORK)
            // Vertical leg: X in [21, 22], Y in [-33, -10]
            // Corner bend: X in [21, 22], Y in [-35, -34]
            // Horizontal leg: X in [6, 20], Y in [-35, -34]
            // Threshold: X in [4, 5], Y in [-35, -34]
            // ========================================================

            // --- VERTICAL CORRIDOR FLOORS ---
            for (int y = -33; y <= -10; y++)
            {
                floorMap.SetTile(new Vector3Int(21, y, 0), fCorrVWest);
                floorMap.SetTile(new Vector3Int(22, y, 0), fCorrVEast);
            }

            // Dislodged/buckled grating slats exposing subfloor conduits at Y = -21
            if (fCorrBuckled != null)
            {
                floorMap.SetTile(new Vector3Int(21, -21, 0), fCorrBuckled);
            }

            // --- CORNER BEND FLOORS (90-DEGREE TURN) ---
            floorMap.SetTile(new Vector3Int(21, -34, 0), fCorrCTL);
            floorMap.SetTile(new Vector3Int(22, -34, 0), fCorrCTR);
            floorMap.SetTile(new Vector3Int(21, -35, 0), fCorrCBL);
            floorMap.SetTile(new Vector3Int(22, -35, 0), fCorrCBR);

            // --- HORIZONTAL CORRIDOR FLOORS ---
            for (int x = 6; x <= 20; x++)
            {
                floorMap.SetTile(new Vector3Int(x, -34, 0), fCorrHNorth);
                floorMap.SetTile(new Vector3Int(x, -35, 0), fCorrHSouth);
            }

            // Standard corridor floor at X = 5
            floorMap.SetTile(new Vector3Int(5, -34, 0), fCorrHNorth);
            floorMap.SetTile(new Vector3Int(5, -35, 0), fCorrHSouth);

            // Hazard Threshold Location 3: Exactly 1-tile wide (X=4) in front of Bulkhead Door
            floorMap.SetTile(new Vector3Int(4, -34, 0), fHazThreshH);
            floorMap.SetTile(new Vector3Int(4, -35, 0), fHazThreshH);

            // Clear sub-door and external floor tiles to guarantee zero light bleed / grey scrap pixels
            floorMap.SetTile(new Vector3Int(2, -34, 0), null);
            floorMap.SetTile(new Vector3Int(3, -34, 0), null);
            floorMap.SetTile(new Vector3Int(2, -35, 0), null);
            floorMap.SetTile(new Vector3Int(3, -35, 0), null);

            // --- VERTICAL CORRIDOR WALLS ---
            for (int y = -32; y <= -11; y++)
            {
                wallMap.SetTile(new Vector3Int(20, y, 0), wRibVWest);
            }
            for (int y = -35; y <= -9; y++)
            {
                wallMap.SetTile(new Vector3Int(23, y, 0), wRibVEast);
            }

            // --- CORNER BEND WALL BRACKETS ---
            wallMap.SetTile(new Vector3Int(20, -33, 0), wBracketL);
            wallMap.SetTile(new Vector3Int(23, -36, 0), wBracketL);

            // --- HORIZONTAL CORRIDOR WALLS ---
            for (int x = 4; x <= 19; x++)
            {
                wallMap.SetTile(new Vector3Int(x, -33, 0), wRibHNorth);
            }
            for (int x = 4; x <= 22; x++)
            {
                wallMap.SetTile(new Vector3Int(x, -36, 0), wRibHSouth);
            }

            floorMap.RefreshAllTiles();
            wallMap.RefreshAllTiles();

            // ========================================================
            // 3. REMOVE OLD PLACEHOLDERS & SETUP DECORATIONS ROOT
            // ========================================================
            string[] placeholders = new[]
            {
                "Obstacle_VentShaft_Turbine_Center",
                "Obstacle_VentShaft_Filter_NW",
                "Obstacle_VentShaft_Duct_SE",
                "Obstacle_Loop_Chicane"
            };

            foreach (string pName in placeholders)
            {
                GameObject oldGo = GameObject.Find(pName);
                if (oldGo != null)
                {
                    Undo.DestroyObjectImmediate(oldGo);
                }
            }

            GameObject oldRoot = GameObject.Find("VentShaft_Decorations");
            if (oldRoot != null)
            {
                Undo.DestroyObjectImmediate(oldRoot);
            }

            GameObject oldLighting = GameObject.Find("VentShaft_Lighting");
            if (oldLighting != null)
            {
                Undo.DestroyObjectImmediate(oldLighting);
            }

            GameObject propsRoot = new GameObject("VentShaft_Decorations");
            Undo.RegisterCreatedObjectUndo(propsRoot, "Create VentShaft Decorations");

            GameObject lightingRoot = new GameObject("VentShaft_Lighting");
            Undo.RegisterCreatedObjectUndo(lightingRoot, "Create VentShaft Lighting");

            // Re-configure West Entrance Door (Recessed mechanical door with red indicator)
            ConfigureVentShaftDoor(propsRoot, pixelSmoothMat);

            // ========================================================
            // 4. SPAWN VENT SHAFT PROPS
            // ========================================================

            // 1. Proportionate Integrated Fan Assembly (384x384, 3.0x3.0m)
            CreateProp(propsRoot, "VS_Prop_Ventilation_Fan_Assembly", new Vector3(16.5f, -0.8f, 0f), new Vector2(2.6f, 2.6f), new Vector2(0f, 0.1f), "Obstacles", 2, pixelSmoothMat, true);

            // 2. North Intake Duct connecting North Wall (Y=2.0) to Fan Assembly
            CreateProp(propsRoot, "VS_Prop_Duct_Intake_North", new Vector3(16.5f, 1.25f, 0f), new Vector2(1.8f, 0.9f), Vector2.zero, "Obstacles", 1, pixelSmoothMat, true);

            // 3. East Exhaust Duct connecting Fan Assembly to East Wall
            CreateProp(propsRoot, "VS_Prop_Duct_Exhaust_East", new Vector3(19.5f, -0.8f, 0f), new Vector2(1.0f, 1.4f), Vector2.zero, "Obstacles", 1, pixelSmoothMat, true);

            // 4. Freestanding Maintenance Terminal (256x192, 2.0x1.5m matching Cargo Hold) at TaskLoc_VentShaft (16.5, -4.5)
            CreateProp(propsRoot, "VS_Prop_Maintenance_Terminal", new Vector3(16.5f, -4.5f, 0f), new Vector2(1.6f, 1.0f), new Vector2(0f, -0.1f), "Obstacles", 2, pixelSmoothMat, true);

            // Relocate / Bind TaskLoc_VentShaft
            GameObject taskLoc = GameObject.Find("TaskLoc_VentShaft");
            if (taskLoc != null)
            {
                Undo.RecordObject(taskLoc.transform, "Position TaskLoc_VentShaft");
                taskLoc.transform.position = new Vector3(16.5f, -4.5f, 0f);
                SpriteRenderer taskSr = taskLoc.GetComponent<SpriteRenderer>();
                if (taskSr != null)
                {
                    Undo.RecordObject(taskSr, "Hide TaskLoc_VentShaft Marker Sprite");
                    taskSr.enabled = false;
                }
                EditorUtility.SetDirty(taskLoc);
            }

            // 6. Narrative Story Decal: Technician Incident organic blood pool & curved drag trail towards SE grille
            CreateProp(propsRoot, "VS_Decal_Story_Technician_Incident", new Vector3(18.0f, -5.2f, 0f), Vector2.zero, Vector2.zero, "Floor", 2, pixelSmoothMat, false);

            // 7. Primary Air Intake Grille on NW wall (soot & dust, distinct from SE)
            CreateProp(propsRoot, "VS_Prop_Grille_NW", new Vector3(13.0f, 0.5f, 0f), new Vector2(1.0f, 1.0f), Vector2.zero, "Obstacles", 2, pixelSmoothMat, true);

            // 8. Slim Motor Electrical Distribution Cabinet hugging West wall
            CreateProp(propsRoot, "VS_Prop_Electrical_Cabinet", new Vector3(11.25f, -1.5f, 0f), new Vector2(0.4f, 0.8f), Vector2.zero, "Obstacles", 2, pixelSmoothMat, true);

            // 9. Secondary Exhaust Grille on SE wall (rust streaks & oil seeps)
            CreateProp(propsRoot, "VS_Prop_Grille_SE", new Vector3(19.5f, -7.0f, 0f), new Vector2(1.0f, 1.0f), Vector2.zero, "Obstacles", 2, pixelSmoothMat, true);

            // 10. Sign Decal above West Entrance
            CreateProp(propsRoot, "VS_Decal_Sign_VentShaft", new Vector3(11.5f, -3.2f, 0f), Vector2.zero, Vector2.zero, "Obstacles", 5, pixelSmoothMat, false);

            // 11. Maintenance Utility Kit (Heavy Rectangular Industrial Toolbox & Coiled Cables) separated from hatch at (14.2, -7.5)
            CreateProp(propsRoot, "VS_Prop_Utility_Kit", new Vector3(14.2f, -7.5f, 0f), new Vector2(1.8f, 0.9f), Vector2.zero, "Obstacles", 2, pixelSmoothMat, true);

            // 12. Floor Transition Beam: Metal runner bar (#2E373D, 2-3px) along reinforced/plain floor seam at Y=-2.0
            CreateProp(propsRoot, "VS_Decal_Floor_Transition_Beam", new Vector3(16.0f, -2.0f, 0f), Vector2.zero, Vector2.zero, "Floor", 3, pixelSmoothMat, false);

            // 13. Jammed Blast Doorway Chokepoint (2 heavy steel pillars #323B42, 1-character opening, floor visible through slit)
            CreateProp(propsRoot, "VS_Prop_Doorway_Chokepoint", new Vector3(22.0f, -8.5f, 0f), new Vector2(2.0f, 1.0f), Vector2.zero, "Obstacles", 3, pixelSmoothMat, false);

            // ========================================================
            // 5. SPAWN CORRIDOR PROPS (ZERO WALKWAY OBSTRUCTION)
            // ========================================================

            // Leaking Pipe Joint at East wall (X=22.6, Y=-20.5) with steam VFX
            GameObject pipeJoint = CreateProp(propsRoot, "VS_Prop_Damaged_Pipe_Joint", new Vector3(22.6f, -20.5f, 0f), new Vector2(0.4f, 0.4f), Vector2.zero, "Obstacles", 3, pixelSmoothMat, true);
            if (pipeJoint != null)
            {
                CreateSteamParticleSystem(pipeJoint, steamMat);
            }

            // Bulkhead Door at bottom-left horizontal corridor exit (X=3.5, Y=-34.5)
            CreateProp(propsRoot, "VS_Prop_Bulkhead_Door", new Vector3(3.5f, -34.5f, 0f), new Vector2(1.0f, 2.0f), Vector2.zero, "Obstacles", 5, pixelSmoothMat, true);

            // ========================================================
            // 6. LIGHTING HIERARCHY (ZERO LIGHT BLEED OUTSIDE WALLS)
            // ========================================================

            // Vent Shaft Room Lights:
            // 1. Visceral Bio-Jam Alarm (Emergency Red #B83C31) at the 5 o'clock jammed blade
            CreateLight(lightingRoot, "Light_VS_Fan_BladeJam", new Vector3(17.4f, -1.6f, 0f), new Color(0.82f, 0.22f, 0.15f, 1f), 1.8f, 2.2f, 0.3f, 0.65f);

            // 2. Primary Fan Machinery Service Light (Cool steel industrial illumination #3A4550 for fan landmark readability)
            CreateLight(lightingRoot, "Light_VS_Fan_Service", new Vector3(16.5f, -0.4f, 0f), new Color(0.60f, 0.65f, 0.72f, 1f), 1.5f, 4.0f, 0.8f, 0.65f);

            // 3. Left Wall Emergency Indicator (Red-Orange #B83C31)
            CreateLight(lightingRoot, "Light_VS_LeftWall_Emergency", new Vector3(11.8f, -1.0f, 0f), new Color(0.82f, 0.22f, 0.15f, 1f), 1.4f, 2.2f, 0.3f, 0.65f);

            // 4. Maintenance Terminal Screen Glow (Authentic Phosphor Cyan #2B7A78)
            CreateLight(lightingRoot, "Light_VS_Terminal_Screen", new Vector3(16.5f, -4.6f, 0f), new Color(0.17f, 0.48f, 0.47f, 1f), 1.25f, 1.6f, 0.2f, 0.75f);

            // 5. Primary Room Cold Ambient Fill (Reduced by 40% to 0.80 to eliminate floor glare around terminal)
            CreateLight(lightingRoot, "Light_VS_Room_Ambient", new Vector3(16.5f, -2.8f, 0f), new Color(0.60f, 0.64f, 0.68f, 1f), 0.80f, 7.0f, 1.0f, 0.60f);

            // 6. South Wall & Walkway Illumination (Crisp readability of South wall panels & navigation)
            CreateLight(lightingRoot, "Light_VS_South_Wall", new Vector3(15.5f, -7.8f, 0f), new Color(0.65f, 0.70f, 0.75f, 1f), 1.4f, 5.0f, 0.8f, 0.60f);

            // 7. Doorway Chokepoint Red-Orange Indicator Beacon (#C0291B) casting soft light down into the slit
            CreateLight(lightingRoot, "Light_VS_Doorway_Indicator", new Vector3(22.0f, -8.3f, 0f), new Color(0.75f, 0.16f, 0.11f, 1f), 1.3f, 2.2f, 0.2f, 0.65f);

            // Corridor Lights: Spaced amber wall sconces (#D4882A, ~30% opacity) creating chiaroscuro
            Color amberCol = new Color(0.83f, 0.53f, 0.16f, 1f);

            // Vertical Corridor:
            CreateLight(lightingRoot, "Light_Corridor_Amber_V1", new Vector3(22.4f, -11.5f, 0f), amberCol, 1.4f, 2.4f, 0.3f, 0.65f);
            CreateLight(lightingRoot, "Light_Corridor_Amber_V2", new Vector3(20.6f, -17.5f, 0f), amberCol, 1.4f, 2.4f, 0.3f, 0.65f);
            CreateLight(lightingRoot, "Light_Corridor_Amber_V3", new Vector3(22.4f, -23.5f, 0f), amberCol, 1.4f, 2.4f, 0.3f, 0.65f);
            CreateLight(lightingRoot, "Light_Corridor_Amber_V4", new Vector3(20.6f, -29.5f, 0f), amberCol, 1.4f, 2.4f, 0.3f, 0.65f);

            // 90-Degree Corner Bend:
            CreateLight(lightingRoot, "Light_Corridor_Amber_Corner", new Vector3(22.0f, -34.5f, 0f), amberCol, 1.4f, 2.4f, 0.3f, 0.65f);

            // Horizontal Corridor:
            CreateLight(lightingRoot, "Light_Corridor_Amber_H1", new Vector3(17.5f, -33.6f, 0f), amberCol, 1.4f, 2.4f, 0.3f, 0.65f);
            CreateLight(lightingRoot, "Light_Corridor_Amber_H2", new Vector3(12.0f, -35.4f, 0f), amberCol, 1.4f, 2.4f, 0.3f, 0.65f);
            CreateLight(lightingRoot, "Light_Corridor_Amber_H3", new Vector3(7.0f, -33.6f, 0f), amberCol, 1.4f, 2.4f, 0.3f, 0.65f);

            // Bulkhead Door Lock Red Light at bottom-left:
            CreateLight(lightingRoot, "Light_Corridor_Door_Red", new Vector3(4.0f, -34.5f, 0f), new Color(0.75f, 0.16f, 0.11f, 1f), 1.2f, 2.0f, 0.2f, 0.65f);

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
        }

        private static GameObject CreateProp(GameObject parent, string propSpriteName, Vector3 position, Vector2 colliderSize, Vector2 colliderOffset, string sortingLayer, int sortingOrder, Material mat, bool addCollider)
        {
            string spritePath = $"{PropsSourceDir}/{propSpriteName}.png";
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
            if (sprite == null)
            {
                Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(spritePath);
                if (tex != null)
                {
                    sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 128f);
                    sprite.name = propSpriteName;
                }
            }

            if (sprite == null)
            {
                Debug.LogWarning($"[VentShaftEnvironmentSetup] Failed to load sprite: {spritePath}");
                return null;
            }

            GameObject propGo = new GameObject(propSpriteName);
            propGo.transform.SetParent(parent.transform);
            propGo.transform.position = position;

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

        private static Light2D CreateLight(GameObject parent, string name, Vector3 worldPos, Color color, float intensity, float outerRadius, float innerRadius, float falloff = 0.65f)
        {
            GameObject lightGo = new GameObject(name);
            lightGo.transform.SetParent(parent.transform);
            lightGo.transform.position = worldPos;

            Light2D light = lightGo.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.pointLightOuterRadius = outerRadius;
            light.pointLightInnerRadius = innerRadius;
            light.falloffIntensity = falloff;
            light.shadowsEnabled = false;

            return light;
        }

        private static void ConfigureVentShaftDoor(GameObject propsRoot, Material mat)
        {
            GameObject doorGo = GameObject.Find("Door_VentShaft");
            if (doorGo == null) return;

            DoorController dc = doorGo.GetComponent<DoorController>();
            if (dc == null) dc = doorGo.AddComponent<DoorController>();

            Sprite closedSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Sprites/Environments/VentShaft/Props/VS_Door_Mechanical_Recessed.png")
                ?? AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Sprites/Environments/Airlock/Props/Door_Vertical_Closed.png");
            Sprite openSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Sprites/Environments/Airlock/Props/Door_Vertical_Open.png");
            Sprite redLight = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Sprites/Environments/MedBay/Props/Door_MedBay_Light_Red.png");
            Sprite greenLight = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Sprites/Environments/MedBay/Props/Door_MedBay_Light_Green.png");

            dc.Initialize("Door_VentShaft");
            dc.SetSprites(closedSprite, openSprite);

            SpriteRenderer dsr = doorGo.GetComponent<SpriteRenderer>();
            if (dsr != null)
            {
                Undo.RecordObject(dsr, "Configure Door_VentShaft SpriteRenderer");
                dsr.sprite = closedSprite;
                dsr.color = Color.white;
                dsr.sortingLayerName = "Obstacles";
                dsr.sortingOrder = 5;
                if (mat != null) dsr.sharedMaterial = mat;
            }

            BoxCollider2D box = doorGo.GetComponent<BoxCollider2D>();
            if (box == null) box = doorGo.AddComponent<BoxCollider2D>();
            box.size = new Vector2(1.0f, 2.0f);
            box.offset = Vector2.zero;

            int interactableLayer = LayerMask.NameToLayer("Interactable");
            if (interactableLayer != -1) doorGo.layer = interactableLayer;

            GameObject oldInd = GameObject.Find("Indicator_Door_VentShaft");
            if (oldInd != null) Undo.DestroyObjectImmediate(oldInd);

            if (propsRoot != null && redLight != null && greenLight != null)
            {
                GameObject lightGo = new GameObject("Indicator_Door_VentShaft");
                lightGo.transform.SetParent(propsRoot.transform);
                lightGo.transform.position = new Vector3(11.5f, -2.85f, 0f);
                lightGo.transform.localScale = new Vector3(0.5f, 0.5f, 1f);

                SpriteRenderer lsr = lightGo.AddComponent<SpriteRenderer>();
                lsr.sprite = redLight;
                lsr.sortingLayerName = "Obstacles";
                lsr.sortingOrder = 6;
                if (mat != null) lsr.sharedMaterial = mat;

                Light2D l2d = lightGo.AddComponent<Light2D>();
                l2d.lightType = Light2D.LightType.Point;
                l2d.color = new Color(0.85f, 0.20f, 0.12f, 1.0f);
                l2d.intensity = 1.2f;
                l2d.pointLightOuterRadius = 1.8f;
                l2d.pointLightInnerRadius = 0.2f;
                l2d.shadowsEnabled = false;

                DoorIndicatorLight indicator = lightGo.AddComponent<DoorIndicatorLight>();
                indicator.Initialize(dc, redLight, greenLight, l2d);
            }
        }

        private static void CreateSteamParticleSystem(GameObject parent, Material steamMat)
        {
            GameObject psGo = new GameObject("Steam_VFX");
            psGo.transform.SetParent(parent.transform);
            psGo.transform.localPosition = new Vector3(-0.15f, 0f, 0f);

            ParticleSystem ps = psGo.AddComponent<ParticleSystem>();
            ParticleSystemRenderer psr = psGo.GetComponent<ParticleSystemRenderer>();
            if (steamMat != null) psr.sharedMaterial = steamMat;
            psr.sortingLayerName = "VFX";
            psr.sortingOrder = 10;

            var main = ps.main;
            main.startLifetime = 0.8f;
            main.startSpeed = 1.2f;
            main.startSize = 0.25f;
            main.startColor = new Color(0.85f, 0.90f, 0.95f, 0.35f);
            main.maxParticles = 30;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 15f;
            shape.radius = 0.05f;
            shape.rotation = new Vector3(0f, 0f, 180f);

            var emission = ps.emission;
            emission.rateOverTime = 12f;
        }
    }
}
#endif
