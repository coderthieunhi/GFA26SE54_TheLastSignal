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
    public static class CrewQuartersEnvironmentSetup
    {
        private const string TilesSourceDir = "Assets/_Project/Sprites/Environments/CrewQuarters_Industrial/Tiles";
        private const string PropsSourceDir = "Assets/_Project/Sprites/Environments/CrewQuarters_Industrial/Props";
        private const string DoorsSourceDir = "Assets/_Project/Sprites/Environments/CrewQuarters_Industrial/Doors";
        private const string TileAssetsDir = "Assets/_Project/Tiles/CrewQuarters_Industrial";

        [MenuItem("Tools/Facility/Setup Crew Quarters Art & Environment", false, 54)]
        public static void SetupCrewQuartersArt()
        {
            // 1. Configure and import Tile textures (PPU = 128)
            ConfigureTileTextures();

            // 2. Configure and import Prop textures (PPU = 128)
            ConfigurePropTextures();

            // 3. Create / update Tile ScriptableObjects
            CreateTileAssets();

            // 4. Decorate Crew Quarters in the active scene at coordinates [-5..4, 38..47]
            DecorateActiveScene();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("<color=green>[CrewQuartersEnvironmentSetup] Successfully decorated Crew Quarters Industrial at coordinates [-5..4, 38..47]!</color>");
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
                    if (importer.spritePixelsPerUnit != 128) { importer.spritePixelsPerUnit = 128; changed = true; }
                    if (importer.filterMode != FilterMode.Point) { importer.filterMode = FilterMode.Point; changed = true; }
                    if (importer.textureCompression != TextureImporterCompression.Uncompressed) { importer.textureCompression = TextureImporterCompression.Uncompressed; changed = true; }
                    if (importer.mipmapEnabled) { importer.mipmapEnabled = false; changed = true; }
                    if (!importer.alphaIsTransparency) { importer.alphaIsTransparency = true; changed = true; }

                    if (changed)
                    {
                        importer.SaveAndReimport();
                    }
                }
            }
        }

        public static void ConfigurePropTextures()
        {
            string[] propDirs = new string[] { PropsSourceDir, DoorsSourceDir };
            foreach (string dir in propDirs)
            {
                if (!Directory.Exists(dir)) continue;

                string[] files = Directory.GetFiles(dir, "*.png");
                foreach (string file in files)
                {
                    TextureImporter importer = AssetImporter.GetAtPath(file) as TextureImporter;
                    if (importer != null)
                    {
                        bool changed = false;
                        if (importer.textureType != TextureImporterType.Sprite) { importer.textureType = TextureImporterType.Sprite; changed = true; }
                        if (importer.spriteImportMode != SpriteImportMode.Single) { importer.spriteImportMode = SpriteImportMode.Single; changed = true; }
                        if (importer.spritePixelsPerUnit != 128) { importer.spritePixelsPerUnit = 128; changed = true; }
                        if (importer.filterMode != FilterMode.Point) { importer.filterMode = FilterMode.Point; changed = true; }
                        if (importer.textureCompression != TextureImporterCompression.Uncompressed) { importer.textureCompression = TextureImporterCompression.Uncompressed; changed = true; }
                        if (importer.mipmapEnabled) { importer.mipmapEnabled = false; changed = true; }
                        if (!importer.alphaIsTransparency) { importer.alphaIsTransparency = true; changed = true; }

                        if (changed)
                        {
                            importer.SaveAndReimport();
                        }
                    }
                }
            }
        }

        public static void CreateTileAssets()
        {
            if (!Directory.Exists(TileAssetsDir)) Directory.CreateDirectory(TileAssetsDir);
            if (!Directory.Exists(TilesSourceDir)) return;

            string[] pngFiles = Directory.GetFiles(TilesSourceDir, "*.png");
            foreach (string pngPath in pngFiles)
            {
                string tileName = Path.GetFileNameWithoutExtension(pngPath);
                string assetPath = $"{TileAssetsDir}/{tileName}.asset";

                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(pngPath);
                if (sprite == null) continue;

                Tile tile = AssetDatabase.LoadAssetAtPath<Tile>(assetPath);
                if (tile == null)
                {
                    tile = ScriptableObject.CreateInstance<Tile>();
                    tile.sprite = sprite;
                    tile.color = Color.white;
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
            GameObject gridGo = GameObject.Find("Facility_Grid") ?? GameObject.Find("Grid");
            Grid grid = gridGo != null ? gridGo.GetComponent<Grid>() : Object.FindAnyObjectByType<Grid>();

            if (grid == null)
            {
                Debug.LogError("[CrewQuartersEnvironmentSetup] Grid (Facility_Grid) not found in the active scene!");
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
                Debug.LogError("[CrewQuartersEnvironmentSetup] Floor Tilemap not found!");
                return;
            }

            // ========================================================
            // 1. CLEAR CREW QUARTERS FOOTPRINT & PERIMETER WALLS: X in [-6..5], Y in [37..48]
            // ========================================================
            Undo.RecordObject(floorMap, "Clear Crew Quarters Floor Tiles");
            if (wallMap != null) Undo.RecordObject(wallMap, "Clear Crew Quarters Wall Tiles");

            for (int x = -6; x <= 5; x++)
            {
                for (int y = 37; y <= 48; y++)
                {
                    Vector3Int p = new Vector3Int(x, y, 0);
                    if (y == 37 && (x == -1 || x == 0)) continue;
                    if (floorMap.HasTile(p)) floorMap.SetTile(p, null);
                    if (wallMap != null && wallMap.HasTile(p)) wallMap.SetTile(p, null);
                }
            }

            // --- LOAD FLOOR TILES ---
            Tile fPlate = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/CrewQuarters_Floor_Industrial_Plate.asset");
            Tile fDrain = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/CrewQuarters_Floor_Drain_Grille.asset");
            Tile fVentSmall = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/CrewQuarters_Floor_Vent_Small.asset");
            Tile fStripeTL = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/CrewQuarters_Floor_Yellow_Stripe_TL.asset");
            Tile fStripeTR = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/CrewQuarters_Floor_Yellow_Stripe_TR.asset");
            Tile fStripeBL = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/CrewQuarters_Floor_Yellow_Stripe_BL.asset");
            Tile fStripeBR = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/CrewQuarters_Floor_Yellow_Stripe_BR.asset");
            Tile fStripeH = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/CrewQuarters_Floor_Yellow_Stripe_H.asset");
            Tile fStripeV = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/CrewQuarters_Floor_Yellow_Stripe_V.asset");
            Tile fHazard = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/CrewQuarters_Floor_Entry_Hazard.asset");

            // --- LOAD WALL TILES ---
            Tile wPanel = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/CrewQuarters_Wall_Industrial_Panel.asset");
            Tile wStruts = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/CrewQuarters_Wall_Industrial_Struts.asset") ?? wPanel;
            Tile wBoard = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/CrewQuarters_Wall_Chalkboard_Screen.asset") ?? wPanel;
            Tile wCornerTL = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/CrewQuarters_Wall_Corner_Chamfer_TL.asset") ?? wPanel;
            Tile wCornerTR = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/CrewQuarters_Wall_Corner_Chamfer_TR.asset") ?? wPanel;
            Tile wCornerBL = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/CrewQuarters_Wall_Corner_Chamfer_BL.asset") ?? wPanel;
            Tile wCornerBR = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/CrewQuarters_Wall_Corner_Chamfer_BR.asset") ?? wPanel;

            // ========================================================
            // 2. PAINT INTERIOR FLOOR: 10x10 DARK INDUSTRIAL STEEL PLATES
            // ========================================================
            for (int x = -5; x <= 4; x++)
            {
                for (int y = 38; y <= 47; y++)
                {
                    floorMap.SetTile(new Vector3Int(x, y, 0), fPlate);
                }
            }

            // Floor Grilles / Drainage Vents (authentic positions from concept art)
            if (fVentSmall != null) floorMap.SetTile(new Vector3Int(-3, 44, 0), fVentSmall);
            if (fDrain != null) floorMap.SetTile(new Vector3Int(3, 43, 0), fDrain);

            // Doorway threshold connecting to Corridor_Crew_NodeA has hazard floor
            if (fHazard != null)
            {
                floorMap.SetTile(new Vector3Int(-1, 37, 0), fHazard);
                floorMap.SetTile(new Vector3Int(0, 37, 0), fHazard);
            }
            else
            {
                floorMap.SetTile(new Vector3Int(-1, 37, 0), fPlate);
                floorMap.SetTile(new Vector3Int(0, 37, 0), fPlate);
            }

            // ========================================================
            // 3. PAINT PERIMETER WALL TILES: EXACTLY 1 LAYER (Y=48, Y=37, X=-6, X=5)
            // ========================================================
            if (wallMap != null)
            {
                // North Wall (Y = 48)
                wallMap.SetTile(new Vector3Int(-6, 48, 0), wCornerTL);
                for (int x = -5; x <= 4; x++)
                {
                    Tile chosen = (x == -2 || x == 1) ? wStruts : wPanel;
                    wallMap.SetTile(new Vector3Int(x, 48, 0), chosen);
                }
                wallMap.SetTile(new Vector3Int(5, 48, 0), wCornerTR);

                // West Wall (X = -6, Y in [38..47])
                for (int y = 38; y <= 47; y++)
                {
                    wallMap.SetTile(new Vector3Int(-6, y, 0), (y == 43) ? wStruts : wPanel);
                }

                // East Wall (X = 5, Y in [38..47])
                for (int y = 38; y <= 47; y++)
                {
                    wallMap.SetTile(new Vector3Int(5, y, 0), (y == 43) ? wStruts : wPanel);
                }

                // South Wall (Y = 37)
                wallMap.SetTile(new Vector3Int(-6, 37, 0), wCornerBL);
                for (int x = -5; x <= -2; x++) wallMap.SetTile(new Vector3Int(x, 37, 0), wPanel);
                wallMap.SetTile(new Vector3Int(-1, 37, 0), null);
                wallMap.SetTile(new Vector3Int(0, 37, 0), null);
                for (int x = 1; x <= 4; x++) wallMap.SetTile(new Vector3Int(x, 37, 0), wPanel);
                wallMap.SetTile(new Vector3Int(5, 37, 0), wCornerBR);
            }

            // Material (URP 2D Lit)
            Material defaultSpriteMat = AssetDatabase.LoadAssetAtPath<Material>("Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Lit-Default.mat");
            if (defaultSpriteMat == null)
            {
                Shader litShader = Shader.Find("Universal Render Pipeline/2D/Sprite-Lit-Default");
                if (litShader != null) defaultSpriteMat = new Material(litShader);
            }

            // Clear old decorations
            string[] legacyNames = new string[] {
                "CrewQuarters_Decorations",
                "Obstacle_CrewQuarters_Partition",
                "Obstacle_CrewQuarters_Lockers",
                "Obstacle_CrewQuarters_LoungeTable"
            };

            foreach (string objName in legacyNames)
            {
                GameObject existing = GameObject.Find(objName);
                if (existing != null) Undo.DestroyObjectImmediate(existing);
            }

            GameObject crewQuartersRoot = new GameObject("CrewQuarters_Decorations");
            // ========================================================
            // 4. SPAWN AUTHENTIC PROPS (EXACTLY MATCHING CONCEPT IMAGE V4)
            // ========================================================
            // A. Northwest Workshop Drafting Bench & Blueprint Board
            CreateProp(crewQuartersRoot, "Prop_CrewQuarters_Workshop_Station",
                new Vector3(-3.50f, 45.40f, 0f), "Obstacles", 2, defaultSpriteMat,
                true, new Vector2(2.10f, 1.40f), new Vector2(0f, -0.20f), new Vector3(0.75f, 0.75f, 1f));
            CreateProp(crewQuartersRoot, "Prop_CrewQuarters_Potted_Plant",
                new Vector3(-4.40f, 46.30f, 0f), "Obstacles", 2, defaultSpriteMat,
                true, new Vector2(0.40f, 0.50f), Vector2.zero, new Vector3(0.70f, 0.70f, 1f));

            // B. Vending Machine "DRINKS" & Waste Bin
            CreateProp(crewQuartersRoot, "Prop_CrewQuarters_Vending_Machine",
                new Vector3(-2.10f, 46.40f, 0f), "Obstacles", 2, defaultSpriteMat,
                true, new Vector2(0.90f, 1.30f), new Vector2(0f, -0.15f), new Vector3(0.75f, 0.75f, 1f));
            CreateProp(crewQuartersRoot, "Prop_CrewQuarters_Trash_Can",
                new Vector3(-1.45f, 46.20f, 0f), "Obstacles", 2, defaultSpriteMat,
                true, new Vector2(0.35f, 0.50f), Vector2.zero, new Vector3(0.75f, 0.75f, 1f));

            // C. North Center Planning Sideboard Counter & Chalkboard (Centered at X = 0.0)
            CreateProp(crewQuartersRoot, "Prop_CrewQuarters_Planning_Counter",
                new Vector3(0.00f, 46.40f, 0f), "Obstacles", 2, defaultSpriteMat,
                true, new Vector2(2.40f, 1.00f), new Vector2(0f, -0.30f), new Vector3(0.85f, 0.85f, 1f));

            // D. Lockers 4-Bank
            CreateProp(crewQuartersRoot, "Prop_CrewQuarters_Lockers_4Bank",
                new Vector3(1.85f, 46.40f, 0f), "Obstacles", 2, defaultSpriteMat,
                true, new Vector2(1.50f, 1.20f), new Vector2(0f, -0.20f), new Vector3(0.72f, 0.72f, 1f));

            // E. Potted Plant & Footlocker Chest next to Lockers
            CreateProp(crewQuartersRoot, "Prop_CrewQuarters_Potted_Plant",
                new Vector3(1.10f, 46.20f, 0f), "Obstacles", 2, defaultSpriteMat,
                true, new Vector2(0.40f, 0.50f), Vector2.zero, new Vector3(0.70f, 0.70f, 1f));
            CreateProp(crewQuartersRoot, "Prop_CrewQuarters_Potted_Plant",
                new Vector3(2.55f, 46.20f, 0f), "Obstacles", 2, defaultSpriteMat,
                true, new Vector2(0.40f, 0.50f), Vector2.zero, new Vector3(0.70f, 0.70f, 1f));
            CreateProp(crewQuartersRoot, "Prop_CrewQuarters_Footlocker",
                new Vector3(3.05f, 46.20f, 0f), "Obstacles", 2, defaultSpriteMat,
                true, new Vector2(0.55f, 0.40f), Vector2.zero, new Vector3(0.75f, 0.75f, 1f));

            // F. Better Days Ahead Poster
            CreateProp(crewQuartersRoot, "Prop_CrewQuarters_Poster_BetterDays",
                new Vector3(3.05f, 47.10f, 0f), "Obstacles", 1, defaultSpriteMat,
                false, Vector2.zero, Vector2.zero, new Vector3(0.75f, 0.75f, 1f));

            // G. Northeast Tool Storage Rack with Yellow Toolbox
            CreateProp(crewQuartersRoot, "Prop_CrewQuarters_Tool_Storage_Rack",
                new Vector3(3.90f, 46.40f, 0f), "Obstacles", 2, defaultSpriteMat,
                true, new Vector2(1.30f, 1.40f), new Vector2(0f, -0.20f), new Vector3(0.72f, 0.72f, 1f));

            // H. East Wall Bookshelf & Potted Plant
            CreateProp(crewQuartersRoot, "Prop_CrewQuarters_Wall_Bookshelf",
                new Vector3(4.45f, 43.40f, 0f), "Obstacles", 2, defaultSpriteMat,
                true, new Vector2(0.60f, 1.50f), Vector2.zero, new Vector3(0.85f, 0.85f, 1f));
            CreateProp(crewQuartersRoot, "Prop_CrewQuarters_Potted_Plant",
                new Vector3(4.45f, 41.80f, 0f), "Obstacles", 2, defaultSpriteMat,
                true, new Vector2(0.40f, 0.50f), Vector2.zero, new Vector3(0.70f, 0.70f, 1f));

            // I. Southeast L-shaped Lounge Sofa Set, Coffee Table & Floor Lamp
            CreateProp(crewQuartersRoot, "Prop_CrewQuarters_Lounge_Sofa_Set",
                new Vector3(3.60f, 39.80f, 0f), "Obstacles", 2, defaultSpriteMat,
                true, new Vector2(2.20f, 2.20f), new Vector2(0.10f, 0.10f), new Vector3(0.75f, 0.75f, 1f));
            CreateProp(crewQuartersRoot, "Prop_CrewQuarters_Rug_Mat",
                new Vector3(2.55f, 39.80f, 0f), "Obstacles", 1, defaultSpriteMat,
                false, Vector2.zero, Vector2.zero, new Vector3(0.80f, 0.80f, 1f));
            CreateProp(crewQuartersRoot, "Prop_CrewQuarters_Footlocker",
                new Vector3(4.45f, 38.60f, 0f), "Obstacles", 2, defaultSpriteMat,
                true, new Vector2(0.55f, 0.40f), Vector2.zero, new Vector3(0.75f, 0.75f, 1f));

            // J. West Wall Storage (Locker Cabinet & Stacked Crates)
            CreateProp(crewQuartersRoot, "Prop_CrewQuarters_Locker_Single_Tall",
                new Vector3(-4.45f, 44.40f, 0f), "Obstacles", 2, defaultSpriteMat,
                true, new Vector2(0.50f, 0.80f), Vector2.zero, new Vector3(0.85f, 0.85f, 1f));
            CreateProp(crewQuartersRoot, "Prop_CrewQuarters_Potted_Plant",
                new Vector3(-4.45f, 43.60f, 0f), "Obstacles", 2, defaultSpriteMat,
                true, new Vector2(0.40f, 0.50f), Vector2.zero, new Vector3(0.70f, 0.70f, 1f));
            CreateProp(crewQuartersRoot, "Prop_CrewQuarters_Crate_Military",
                new Vector3(-4.45f, 42.60f, 0f), "Obstacles", 2, defaultSpriteMat,
                true, new Vector2(0.60f, 0.70f), Vector2.zero, new Vector3(0.75f, 0.75f, 1f));
            CreateProp(crewQuartersRoot, "Prop_CrewQuarters_Poster_West",
                new Vector3(-4.65f, 41.50f, 0f), "Obstacles", 1, defaultSpriteMat,
                false, Vector2.zero, Vector2.zero, new Vector3(0.75f, 0.75f, 1f));

            // K. Southwest Officer Workstation Desk & Chair
            CreateProp(crewQuartersRoot, "Prop_CrewQuarters_Workstation_Desk",
                new Vector3(-3.75f, 39.80f, 0f), "Obstacles", 2, defaultSpriteMat,
                true, new Vector2(1.50f, 1.60f), new Vector2(0f, 0f), new Vector3(0.75f, 0.75f, 1f));
            CreateProp(crewQuartersRoot, "Prop_CrewQuarters_Potted_Plant",
                new Vector3(-4.35f, 38.75f, 0f), "Obstacles", 2, defaultSpriteMat,
                true, new Vector2(0.40f, 0.50f), Vector2.zero, new Vector3(0.70f, 0.70f, 1f));
            CreateProp(crewQuartersRoot, "Prop_CrewQuarters_Footlocker",
                new Vector3(-3.85f, 38.75f, 0f), "Obstacles", 2, defaultSpriteMat,
                true, new Vector2(0.50f, 0.40f), Vector2.zero, new Vector3(0.65f, 0.65f, 1f));

            // L. Center Meeting Table with Yellow Hazard Frame, Chairs & Potted Plant
            CreateProp(crewQuartersRoot, "Prop_CrewQuarters_Center_Table_Set",
                new Vector3(0.00f, 42.85f, 0f), "Obstacles", 2, defaultSpriteMat,
                true, new Vector2(2.60f, 1.30f), new Vector2(0f, 0f), new Vector3(1.75f, 1.75f, 1f));

            // M. Rectangular Floor Access Hatch with Warning Triangle (Directly below table frame)
            CreateProp(crewQuartersRoot, "Prop_CrewQuarters_Floor_Access_Hatch",
                new Vector3(0.00f, 40.75f, 0f), "Obstacles", 1, defaultSpriteMat,
                false, Vector2.zero, Vector2.zero, new Vector3(1.75f, 1.75f, 1f));

            // N. South Crate beside Door
            CreateProp(crewQuartersRoot, "Prop_CrewQuarters_Footlocker",
                new Vector3(-1.80f, 37.00f, 0f), "Obstacles", 2, defaultSpriteMat,
                true, new Vector2(0.60f, 0.40f), Vector2.zero, new Vector3(0.75f, 0.75f, 1f));

            // ========================================================
            // 5. CONFIGURE HEAVY BULKHEAD SLIDING DOOR
            // ========================================================
            Sprite doorClosed = AssetDatabase.LoadAssetAtPath<Sprite>($"{DoorsSourceDir}/Door_CrewQuarters_Industrial_Closed.png");
            Sprite doorOpen = AssetDatabase.LoadAssetAtPath<Sprite>($"{DoorsSourceDir}/Door_CrewQuarters_Industrial_Open.png");

            DoorController cqDoor = null;
            DoorController[] allDoors = Object.FindObjectsByType<DoorController>();
            foreach (DoorController d in allDoors)
            {
                if (d.name == "Door_CrewQuarters")
                {
                    cqDoor = d;
                    break;
                }
            }

            if (cqDoor != null)
            {
                Undo.RecordObject(cqDoor.transform, "Position Crew Quarters Door");
                Undo.RecordObject(cqDoor, "Configure Crew Quarters Door");

                cqDoor.transform.position = new Vector3(0.0f, 37.5f, 0f);
                cqDoor.transform.localScale = Vector3.one;
                cqDoor.Initialize("Door_CrewQuarters");
                cqDoor.SetSprites(doorClosed, doorOpen);

                SpriteRenderer dsr = cqDoor.GetComponent<SpriteRenderer>();
                if (dsr == null) dsr = cqDoor.gameObject.AddComponent<SpriteRenderer>();
                Undo.RecordObject(dsr, "Update Crew Quarters Door Material");
                if (defaultSpriteMat != null) dsr.sharedMaterial = defaultSpriteMat;
                dsr.sprite = doorClosed;
                dsr.color = Color.white;
                dsr.sortingLayerName = "Obstacles";
                dsr.sortingOrder = 5;

                BoxCollider2D box = cqDoor.GetComponent<BoxCollider2D>();
                if (box == null) box = cqDoor.gameObject.AddComponent<BoxCollider2D>();
                Undo.RecordObject(box, "Update Crew Quarters Door Collider");
                box.size = new Vector2(2.0f, 0.5f);
                box.offset = new Vector2(0f, 0f);
                int interactableLayer = LayerMask.NameToLayer("Interactable");
                if (interactableLayer != -1) cqDoor.gameObject.layer = interactableLayer;

                EditorUtility.SetDirty(cqDoor);
            }

            // ========================================================
            // 6. SYNCHRONIZE TASKLOC_CREWQUARTERS
            // ========================================================
            GameObject taskLoc = GameObject.Find("TaskLoc_CrewQuarters");
            if (taskLoc != null)
            {
                Undo.RecordObject(taskLoc.transform, "Relocate TaskLoc_CrewQuarters");
                taskLoc.transform.position = new Vector3(0.00f, 43.00f, 0f); // Center meeting table
 
                SpriteRenderer taskSr = taskLoc.GetComponent<SpriteRenderer>();
                if (taskSr != null)
                {
                    Undo.RecordObject(taskSr, "Hide TaskLoc_CrewQuarters Marker Sprite");
                    taskSr.enabled = false;
                }
                EditorUtility.SetDirty(taskLoc);
            }

            // ========================================================
            // 7. MOODY ATMOSPHERIC 2D LIGHTING (MATCHING EXACT CONCEPT ART V4)
            // ========================================================
            // 1. North Planning Counter Warm Strip Light
            CreateLight(crewQuartersRoot, "Light_CrewQuarters_North_Strip",
                new Vector3(0.00f, 47.10f, 0f), new Color(1.00f, 0.88f, 0.65f, 1.0f), 1.60f, 4.0f, 0.3f);

            // 2. Center Table Warm Focus Spotlight
            CreateLight(crewQuartersRoot, "Light_CrewQuarters_Center_Table",
                new Vector3(0.00f, 43.00f, 0f), new Color(1.00f, 0.88f, 0.70f, 1.0f), 1.45f, 4.2f, 0.5f);

            // 3. Northwest Workshop Bench Warm Light
            CreateLight(crewQuartersRoot, "Light_CrewQuarters_Workshop_Light",
                new Vector3(-3.40f, 46.20f, 0f), new Color(1.00f, 0.90f, 0.70f, 1.0f), 1.20f, 3.0f, 0.2f);

            // 4. Northeast Tool Rack Accent Light
            CreateLight(crewQuartersRoot, "Light_CrewQuarters_Tool_Light",
                new Vector3(4.00f, 47.00f, 0f), new Color(1.00f, 0.88f, 0.65f, 1.0f), 1.20f, 2.8f, 0.2f);

            // 5. East Vertical Wall Tube Light
            CreateLight(crewQuartersRoot, "Light_CrewQuarters_East_Wall_Tube",
                new Vector3(4.80f, 44.50f, 0f), new Color(0.92f, 0.96f, 1.00f, 1.0f), 1.20f, 2.8f, 0.2f);

            // 6. Southeast Lounge Sofa Floor Lamp
            CreateLight(crewQuartersRoot, "Light_CrewQuarters_Sofa_Lamp",
                new Vector3(4.45f, 39.50f, 0f), new Color(1.00f, 0.85f, 0.65f, 1.0f), 1.30f, 3.0f, 0.2f);

            // 7. Southwest Workstation Desk Lamp
            CreateLight(crewQuartersRoot, "Light_CrewQuarters_Desk_Lamp",
                new Vector3(-4.10f, 40.20f, 0f), new Color(1.00f, 0.85f, 0.60f, 1.0f), 1.40f, 2.5f, 0.2f);
            CreateLight(crewQuartersRoot, "Light_CrewQuarters_Laptop_Cyan",
                new Vector3(-3.75f, 40.00f, 0f), new Color(0.60f, 0.85f, 1.00f, 1.0f), 0.80f, 1.5f, 0.1f);

            // 8. West Wall Vertical Strip Light
            CreateLight(crewQuartersRoot, "Light_CrewQuarters_West_Wall_Light",
                new Vector3(-4.80f, 44.50f, 0f), new Color(1.00f, 0.90f, 0.70f, 1.0f), 1.20f, 2.5f, 0.2f);

            // 9. Vending Machine Cool Cyan Glow
            CreateLight(crewQuartersRoot, "Light_CrewQuarters_Vending_Cyan",
                new Vector3(-2.10f, 46.50f, 0f), new Color(0.25f, 0.90f, 1.00f, 1.0f), 0.95f, 2.4f, 0.2f);

            // 10. South Corridor Wall Light
            CreateLight(crewQuartersRoot, "Light_CrewQuarters_South_Corridor",
                new Vector3(1.50f, 36.80f, 0f), new Color(1.00f, 0.90f, 0.70f, 1.0f), 1.50f, 3.2f, 0.3f);

            // 11. Keypad Security Terminal Green Glow (Door Right)
            CreateLight(crewQuartersRoot, "Light_CrewQuarters_Keypad_Green",
                new Vector3(0.65f, 37.50f, 0f), new Color(0.25f, 1.00f, 0.40f, 1.0f), 0.75f, 1.5f, 0.1f);

            // 12. Door Lock Indicator Red Glow (Door Left)
            CreateLight(crewQuartersRoot, "Light_CrewQuarters_Lock_Red",
                new Vector3(-0.65f, 37.50f, 0f), new Color(1.00f, 0.20f, 0.15f, 1.0f), 0.60f, 1.2f, 0.1f);

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }

        private static GameObject CreateProp(GameObject parent, string propSpriteName, Vector3 position, string sortingLayer, int sortingOrder, Material mat, bool addCollider, Vector2 colliderSize, Vector2 colliderOffset, Vector3? localScale = null)
        {
            string spritePath = $"{PropsSourceDir}/{propSpriteName}.png";
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
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
            sr.color = Color.white;
            sr.sortingLayerName = sortingLayer;
            sr.sortingOrder = sortingOrder;
            if (mat != null) sr.sharedMaterial = mat;

            if (addCollider)
            {
                BoxCollider2D collider = propGo.AddComponent<BoxCollider2D>();
                collider.size = colliderSize;
                collider.offset = colliderOffset;
            }

            int obstacleLayer = LayerMask.NameToLayer("Obstacles");
            if (obstacleLayer != -1) propGo.layer = obstacleLayer;

            return propGo;
        }

        private static void CreateLight(GameObject parent, string name, Vector3 pos, Color color, float intensity, float outerRadius, float innerRadius)
        {
            GameObject lightGo = new GameObject(name);
            lightGo.transform.SetParent(parent.transform);
            lightGo.transform.position = pos;
            Light2D light = lightGo.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.pointLightOuterRadius = outerRadius;
            light.pointLightInnerRadius = innerRadius;
            light.enabled = false;
        }
    }
}
#endif
