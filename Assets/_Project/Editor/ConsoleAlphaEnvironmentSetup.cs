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
    public static class ConsoleAlphaEnvironmentSetup
    {
        private const string TilesSourceDir = "Assets/_Project/Sprites/Environments/ConsoleAlpha/Tiles";
        private const string PropsSourceDir = "Assets/_Project/Sprites/Environments/ConsoleAlpha/Props";
        private const string TileAssetsDir = "Assets/_Project/Tiles/ConsoleAlpha";

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
                    SetupConsoleAlphaArt();
                }
            };
        }

        [MenuItem("Tools/Facility/Setup Console Alpha Art & Environment", false, 53)]
        public static void SetupConsoleAlphaArt()
        {
            // 1. Configure and import Tile textures (PPU = 128)
            ConfigureTileTextures();

            // 2. Configure and import Prop textures (PPU = 128)
            ConfigurePropTextures();

            // 3. Create / update Tile ScriptableObjects
            CreateTileAssets();

            // 4. Decorate Console Alpha in the active scene at exact Facility Coordinates [10..21, 22..33]
            DecorateActiveScene();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("<color=green>[ConsoleAlphaEnvironmentSetup] Successfully decorated Console Alpha (Option 1: Central Command Island) at coordinates [10..21, 22..33]!</color>");
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
            Grid grid = gridGo != null ? gridGo.GetComponent<Grid>() : Object.FindAnyObjectByType<Grid>();

            if (grid == null)
            {
                Debug.LogError("[ConsoleAlphaEnvironmentSetup] Grid (Facility_Grid) not found in the active scene!");
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
                Debug.LogError("[ConsoleAlphaEnvironmentSetup] Floor Tilemap not found!");
                return;
            }

            // ========================================================
            // CLEAR CONSOLE ALPHA FOOTPRINT TILES ONLY: X in [10..21], Y in [22..33]
            // Confined strictly to Console Alpha without altering Corridor or other rooms.
            // ========================================================
            Undo.RecordObject(floorMap, "Clear Console Alpha Floor Tiles");
            if (wallMap != null) Undo.RecordObject(wallMap, "Clear Console Alpha Wall Tiles");

            for (int x = 10; x <= 21; x++)
            {
                for (int y = 22; y <= 33; y++)
                {
                    Vector3Int p = new Vector3Int(x, y, 0);
                    if (floorMap.HasTile(p)) floorMap.SetTile(p, null);
                    if (wallMap != null && wallMap.HasTile(p)) wallMap.SetTile(p, null);
                }
            }

            // --- LOAD FLOOR TILES ---
            Tile fDeck = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/ConsoleAlpha_Floor_Server_Deck.asset");
            Tile fCable = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/ConsoleAlpha_Floor_Cable_Channel.asset");
            Tile fGrate = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/ConsoleAlpha_Floor_Coolant_Grate.asset");
            Tile fHazard = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/ConsoleAlpha_Floor_Hazard_ESD.asset");
            Tile fStencil = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/ConsoleAlpha_Floor_DataNode_Stencil.asset");
            Tile fHazV = AssetDatabase.LoadAssetAtPath<Tile>("Assets/_Project/Tiles/Bedroom/Bedroom_Floor_Hazard_V.asset") ?? fHazard;

            // --- LOAD WALL TILES ---
            Tile wBulkhead = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/ConsoleAlpha_Wall_Bulkhead.asset");
            Tile wServerInset = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/ConsoleAlpha_Wall_Server_Inset.asset");
            Tile wMainframePanel = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/ConsoleAlpha_Wall_Mainframe_Panel.asset");
            Tile wCoolantPipes = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/ConsoleAlpha_Wall_Coolant_Pipes.asset");
            Tile wCableTrunk = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/ConsoleAlpha_Wall_Cable_Trunk.asset");

            Tile wBulkhead_S = AssetDatabase.LoadAssetAtPath<Tile>("Assets/_Project/Tiles/Bedroom/Bedroom_Wall_Modular_Bulkhead_S.asset") ?? wBulkhead;
            Tile wBulkhead_W = AssetDatabase.LoadAssetAtPath<Tile>("Assets/_Project/Tiles/Bedroom/Bedroom_Wall_Modular_Bulkhead_W.asset") ?? wBulkhead;
            Tile wBulkhead_E = AssetDatabase.LoadAssetAtPath<Tile>("Assets/_Project/Tiles/Bedroom/Bedroom_Wall_Modular_Bulkhead_E.asset") ?? wBulkhead;
            Tile wCornerTL = AssetDatabase.LoadAssetAtPath<Tile>("Assets/_Project/Tiles/Bedroom/Bedroom_Wall_Corner_TL.asset") ?? wBulkhead;
            Tile wCornerTR = AssetDatabase.LoadAssetAtPath<Tile>("Assets/_Project/Tiles/Bedroom/Bedroom_Wall_Corner_TR.asset") ?? wBulkhead;
            Tile wCornerBL = AssetDatabase.LoadAssetAtPath<Tile>("Assets/_Project/Tiles/Bedroom/Bedroom_Wall_Corner_BL.asset") ?? wBulkhead_S;
            Tile wCornerBR = AssetDatabase.LoadAssetAtPath<Tile>("Assets/_Project/Tiles/Bedroom/Bedroom_Wall_Corner_BR.asset") ?? wBulkhead_S;

            // ========================================================
            // 1. PAINT FLOOR: OPTION 1 - CENTRAL COMMAND ISLAND
            // Interior: X in [11..20], Y in [23..32]
            // ========================================================
            for (int x = 11; x <= 20; x++)
            {
                for (int y = 23; y <= 32; y++)
                {
                    Vector3Int pos = new Vector3Int(x, y, 0);

                    // North Server Bank: Cable Channel (Y=32) and Hazard ESD Trim (Y=31)
                    if (y == 32 && x >= 13 && x <= 18)
                    {
                        floorMap.SetTile(pos, fCable ?? fDeck);
                    }
                    else if (y == 31 && x >= 13 && x <= 18)
                    {
                        floorMap.SetTile(pos, fHazard ?? fDeck);
                    }
                    // South Server Bank: Cable Channel (Y=23) and Hazard ESD Trim (Y=24)
                    else if (y == 23 && x >= 13 && x <= 17)
                    {
                        floorMap.SetTile(pos, fCable ?? fDeck);
                    }
                    else if (y == 24 && x >= 13 && x <= 17)
                    {
                        floorMap.SetTile(pos, fHazard ?? fDeck);
                    }
                    // Walkway entrance stencil DATA NODE α
                    else if (x == 12 && y == 28)
                    {
                        floorMap.SetTile(pos, fStencil ?? fDeck);
                    }
                    // Standard Heavy Industrial Server Deck
                    else
                    {
                        floorMap.SetTile(pos, fDeck);
                    }
                }
            }

            // Doorway threshold tiles at West entrance:
            floorMap.SetTile(new Vector3Int(10, 27, 0), fHazV ?? fDeck);
            floorMap.SetTile(new Vector3Int(10, 28, 0), fHazV ?? fDeck);

            // ========================================================
            // 2. PAINT WALLS & 4 CORNERS (OPTION 1)
            // ========================================================
            if (wallMap != null)
            {
                // --- NORTH WALL: Y = 33, X in [11..20] ---
                for (int x = 11; x <= 20; x++)
                {
                    Vector3Int pos = new Vector3Int(x, 33, 0);
                    if (x == 13 || x == 18) wallMap.SetTile(pos, wMainframePanel ?? wBulkhead);
                    else if (x == 14 || x == 17) wallMap.SetTile(pos, wServerInset ?? wBulkhead);
                    else if (x == 15 || x == 16) wallMap.SetTile(pos, wCoolantPipes ?? wBulkhead);
                    else wallMap.SetTile(pos, wBulkhead);
                }

                // --- SOUTH WALL: Y = 22, X in [11..20] ---
                for (int x = 11; x <= 20; x++)
                {
                    Vector3Int pos = new Vector3Int(x, 22, 0);
                    if (x == 18 || x == 19) wallMap.SetTile(pos, wCableTrunk ?? wBulkhead_S);
                    else wallMap.SetTile(pos, wBulkhead_S);
                }

                // --- WEST WALL: X = 10, Y in [23..32] (Door opening at Y in [27, 28]) ---
                for (int y = 23; y <= 32; y++)
                {
                    Vector3Int pos = new Vector3Int(10, y, 0);
                    if (y == 27 || y == 28) wallMap.SetTile(pos, null); // Doorway
                    else wallMap.SetTile(pos, wBulkhead_W);
                }

                // --- EAST WALL: X = 21, Y in [23..32] ---
                for (int y = 23; y <= 32; y++)
                {
                    Vector3Int pos = new Vector3Int(21, y, 0);
                    wallMap.SetTile(pos, wBulkhead_E);
                }

                // --- 4 CORNERS ---
                wallMap.SetTile(new Vector3Int(10, 33, 0), wCornerTL);
                wallMap.SetTile(new Vector3Int(21, 33, 0), wCornerTR);
                wallMap.SetTile(new Vector3Int(10, 22, 0), wCornerBL);
                wallMap.SetTile(new Vector3Int(21, 22, 0), wCornerBR);
            }

            // ========================================================
            // 3. CONFIGURE MATERIAL & CLEAN UP OLD OBJECTS
            // ========================================================
            Material defaultSpriteMat = AssetDatabase.LoadAssetAtPath<Material>("Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Lit-Default.mat");
            if (defaultSpriteMat == null)
            {
                Shader litShader = Shader.Find("Universal Render Pipeline/2D/Sprite-Lit-Default");
                if (litShader != null) defaultSpriteMat = new Material(litShader);
            }

            // Remove previous ConsoleAlpha decorations root or legacy placeholder obstacles
            string[] legacyNames = new string[] {
                "ConsoleAlpha_Decorations",
                "Obstacle_ConsoleAlpha_ServerRack_North",
                "Obstacle_ConsoleAlpha_ServerRack_South",
                "Obstacle_ConsoleAlpha_CableBox",
                "Obstacle_ConsoleAlpha_AuxRack_East"
            };

            foreach (string objName in legacyNames)
            {
                GameObject existing = GameObject.Find(objName);
                if (existing != null) Undo.DestroyObjectImmediate(existing);
            }

            GameObject[] sceneObjects = Object.FindObjectsByType<GameObject>();
            foreach (GameObject go in sceneObjects)
            {
                if (go == null) continue;
                if ((go.name.StartsWith("Prop_ConsoleAlpha_") && go.transform.parent == null) ||
                    (go.name.StartsWith("Indicator_Door_ConsoleAlpha") && go.transform.parent == null) ||
                    (go.name.StartsWith("Light_ConsoleAlpha_") && go.transform.parent == null))
                {
                    Undo.DestroyObjectImmediate(go);
                }
            }

            GameObject consoleAlphaRoot = new GameObject("ConsoleAlpha_Decorations");
            Undo.RegisterCreatedObjectUndo(consoleAlphaRoot, "Create ConsoleAlpha Decorations");

            // ========================================================
            // 4. SPAWN OPTION 1 RETRO-SCI-FI PROPS
            // ========================================================
            // 1. Central Mainframe Command Console at (15.50, 28.50)
            CreateProp(consoleAlphaRoot, "Prop_ConsoleAlpha_Mainframe_Console",
                new Vector3(15.50f, 28.50f, 0f), "Obstacles", 3, defaultSpriteMat,
                true, new Vector2(1.80f, 1.60f), new Vector2(0f, -0.15f), new Vector3(0.54f, 0.54f, 1f));

            // 2. Operator Swivel Chair at (15.50, 27.20)
            CreateProp(consoleAlphaRoot, "Prop_ConsoleAlpha_Operator_Chair",
                new Vector3(15.50f, 27.20f, 0f), "Obstacles", 4, defaultSpriteMat,
                true, new Vector2(0.50f, 0.40f), new Vector2(0f, -0.15f), new Vector3(0.50f, 0.50f, 1f));

            // 3. North Server Bank (3 Server Racks along North wall on Hazard Trim, Y = 31.95)
            Vector2 serverCol = new Vector2(1.10f, 1.40f);
            Vector2 serverOffset = new Vector2(0f, -0.20f);
            Vector3 serverScale = new Vector3(0.50f, 0.50f, 1f);

            CreateProp(consoleAlphaRoot, "Prop_ConsoleAlpha_Server_Rack",
                new Vector3(13.50f, 31.95f, 0f), "Obstacles", 2, defaultSpriteMat,
                true, serverCol, serverOffset, serverScale);
            CreateProp(consoleAlphaRoot, "Prop_ConsoleAlpha_Server_Rack",
                new Vector3(15.50f, 31.95f, 0f), "Obstacles", 2, defaultSpriteMat,
                true, serverCol, serverOffset, serverScale);
            CreateProp(consoleAlphaRoot, "Prop_ConsoleAlpha_Server_Rack",
                new Vector3(17.50f, 31.95f, 0f), "Obstacles", 2, defaultSpriteMat,
                true, serverCol, serverOffset, serverScale);

            // 4. South Server Bank (2 Server Racks along South wall on Hazard Trim, Y = 24.95)
            CreateProp(consoleAlphaRoot, "Prop_ConsoleAlpha_Server_Rack",
                new Vector3(13.50f, 24.95f, 0f), "Obstacles", 2, defaultSpriteMat,
                true, serverCol, serverOffset, serverScale);
            CreateProp(consoleAlphaRoot, "Prop_ConsoleAlpha_Server_Rack",
                new Vector3(15.50f, 24.95f, 0f), "Obstacles", 2, defaultSpriteMat,
                true, serverCol, serverOffset, serverScale);

            // 5. Auxiliary Diagnostic Terminal on East Wall at (20.30, 28.00)
            CreateProp(consoleAlphaRoot, "Prop_ConsoleAlpha_Aux_Terminal",
                new Vector3(20.30f, 28.00f, 0f), "Obstacles", 2, defaultSpriteMat,
                true, new Vector2(0.70f, 1.30f), new Vector2(0f, -0.10f), new Vector3(0.50f, 0.50f, 1f));

            // 6. Circuit Component Crate in SW Corner at (11.80f, 23.40f)
            CreateProp(consoleAlphaRoot, "Prop_ConsoleAlpha_Circuit_Crate",
                new Vector3(11.80f, 23.40f, 0f), "Obstacles", 2, defaultSpriteMat,
                true, new Vector2(0.85f, 0.85f), Vector2.zero, new Vector3(0.50f, 0.50f, 1f));

            // ========================================================
            // 5. CONFIGURE DOOR & INDICATOR LIGHT
            // ========================================================
            Sprite doorVClosed = AssetDatabase.LoadAssetAtPath<Sprite>($"{PropsSourceDir}/Door_ConsoleAlpha_Vertical_Closed.png");
            Sprite doorVOpen = AssetDatabase.LoadAssetAtPath<Sprite>($"{PropsSourceDir}/Door_ConsoleAlpha_Vertical_Open.png");
            Sprite lightRed = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Sprites/Environments/MedBay/Props/Door_MedBay_Light_Red.png");
            Sprite lightGreen = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Sprites/Environments/MedBay/Props/Door_MedBay_Light_Green.png");

            DoorController consoleDoor = null;
            DoorController[] allDoors = Object.FindObjectsByType<DoorController>();
            foreach (DoorController d in allDoors)
            {
                if (d.name == "Door_ConsoleAlpha")
                {
                    consoleDoor = d;
                    break;
                }
            }

            if (consoleDoor != null)
            {
                Undo.RecordObject(consoleDoor.transform, "Position Console Alpha Door");
                Undo.RecordObject(consoleDoor, "Configure Console Alpha Door");

                consoleDoor.transform.position = new Vector3(10.5f, 28.0f, 0f);
                consoleDoor.transform.localScale = Vector3.one;
                consoleDoor.Initialize("Door_ConsoleAlpha");
                consoleDoor.SetSprites(doorVClosed, doorVOpen);

                SpriteRenderer dsr = consoleDoor.GetComponent<SpriteRenderer>();
                if (dsr == null) dsr = consoleDoor.gameObject.AddComponent<SpriteRenderer>();
                Undo.RecordObject(dsr, "Update Console Alpha Door Material");
                if (defaultSpriteMat != null) dsr.sharedMaterial = defaultSpriteMat;
                dsr.sprite = doorVClosed;
                dsr.color = Color.white;
                dsr.sortingLayerName = "Obstacles";
                dsr.sortingOrder = 5;

                BoxCollider2D box = consoleDoor.GetComponent<BoxCollider2D>();
                if (box == null) box = consoleDoor.gameObject.AddComponent<BoxCollider2D>();
                Undo.RecordObject(box, "Update Console Alpha Door Collider");
                box.size = new Vector2(1.0f, 2.0f);
                box.offset = Vector2.zero;
                int interactableLayer = LayerMask.NameToLayer("Interactable");
                if (interactableLayer != -1) consoleDoor.gameObject.layer = interactableLayer;

                CreateIndicatorLight(consoleAlphaRoot, consoleDoor, new Vector3(10.5f, 29.15f, 0f), lightRed, lightGreen, defaultSpriteMat);
                EditorUtility.SetDirty(consoleDoor);
            }

            // ========================================================
            // 6. SYNCHRONIZE TASKLOC_CONSOLEALPHA
            // ========================================================
            GameObject taskLoc = GameObject.Find("TaskLoc_ConsoleAlpha");
            if (taskLoc != null)
            {
                Undo.RecordObject(taskLoc.transform, "Relocate TaskLoc_ConsoleAlpha");
                taskLoc.transform.position = new Vector3(15.50f, 28.50f, 0f);

                SpriteRenderer taskSr = taskLoc.GetComponent<SpriteRenderer>();
                if (taskSr != null)
                {
                    Undo.RecordObject(taskSr, "Hide TaskLoc_ConsoleAlpha Marker Sprite");
                    taskSr.enabled = false;
                }
                EditorUtility.SetDirty(taskLoc);
            }

            // ========================================================
            // 7. ATMOSPHERIC 2D LIGHTING
            // ========================================================
            // 4 Ceiling Ambient Illumination Points
            Vector3[] ambientPositions = new Vector3[] {
                new Vector3(13.5f, 29.5f, 0f),
                new Vector3(17.5f, 29.5f, 0f),
                new Vector3(13.5f, 25.5f, 0f),
                new Vector3(17.5f, 25.5f, 0f)
            };

            for (int i = 0; i < ambientPositions.Length; i++)
            {
                GameObject ambGo = new GameObject($"Light_ConsoleAlpha_Ceiling_{i}");
                ambGo.transform.SetParent(consoleAlphaRoot.transform);
                ambGo.transform.position = ambientPositions[i];
                Light2D l2d = ambGo.AddComponent<Light2D>();
                l2d.lightType = Light2D.LightType.Point;
                l2d.color = new Color(0.85f, 0.92f, 1.0f, 1.0f);
                l2d.intensity = 0.70f;
                l2d.pointLightOuterRadius = 5.5f;
                l2d.pointLightInnerRadius = 0.3f;
                l2d.enabled = false;
            }

            // Central Mainframe CRT Phosphor Green Glow
            GameObject crtGo = new GameObject("Light_ConsoleAlpha_CRT_Glow");
            crtGo.transform.SetParent(consoleAlphaRoot.transform);
            crtGo.transform.position = new Vector3(15.50f, 28.80f, 0f);
            Light2D crtLight = crtGo.AddComponent<Light2D>();
            crtLight.lightType = Light2D.LightType.Point;
            crtLight.color = new Color(0.20f, 1.00f, 0.35f, 1.0f);
            crtLight.intensity = 1.10f;
            crtLight.pointLightOuterRadius = 3.2f;
            crtLight.pointLightInnerRadius = 0.3f;
            crtLight.enabled = false;

            // Aux Terminal Vintage Phosphor Amber Glow
            GameObject auxGo = new GameObject("Light_ConsoleAlpha_Aux_Amber");
            auxGo.transform.SetParent(consoleAlphaRoot.transform);
            auxGo.transform.position = new Vector3(20.30f, 28.20f, 0f);
            Light2D auxLight = auxGo.AddComponent<Light2D>();
            auxLight.lightType = Light2D.LightType.Point;
            auxLight.color = new Color(1.00f, 0.65f, 0.15f, 1.0f);
            auxLight.intensity = 0.90f;
            auxLight.pointLightOuterRadius = 2.5f;
            auxLight.pointLightInnerRadius = 0.2f;
            auxLight.enabled = false;

            // North & South Server Status Cyan LED Accents
            GameObject srvNGo = new GameObject("Light_ConsoleAlpha_Server_LED_N");
            srvNGo.transform.SetParent(consoleAlphaRoot.transform);
            srvNGo.transform.position = new Vector3(15.50f, 32.25f, 0f);
            Light2D srvNLight = srvNGo.AddComponent<Light2D>();
            srvNLight.lightType = Light2D.LightType.Point;
            srvNLight.color = new Color(0.20f, 0.85f, 1.00f, 1.0f);
            srvNLight.intensity = 0.60f;
            srvNLight.pointLightOuterRadius = 2.5f;
            srvNLight.pointLightInnerRadius = 0.2f;
            srvNLight.enabled = false;

            GameObject srvSGo = new GameObject("Light_ConsoleAlpha_Server_LED_S");
            srvSGo.transform.SetParent(consoleAlphaRoot.transform);
            srvSGo.transform.position = new Vector3(15.50f, 25.25f, 0f);
            Light2D srvSLight = srvSGo.AddComponent<Light2D>();
            srvSLight.lightType = Light2D.LightType.Point;
            srvSLight.color = new Color(0.20f, 0.85f, 1.00f, 1.0f);
            srvSLight.intensity = 0.60f;
            srvSLight.pointLightOuterRadius = 2.5f;
            srvSLight.pointLightInnerRadius = 0.2f;
            srvSLight.enabled = false;

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
            light.enabled = false;

            DoorIndicatorLight indicator = lightGo.AddComponent<DoorIndicatorLight>();
            indicator.Initialize(door, red, green, light);

            return lightGo;
        }

        private static GameObject CreateProp(GameObject parent, string propSpriteName, Vector3 position, string sortingLayer, int sortingOrder, Material mat, bool addCollider, Vector2 colliderSize, Vector2 colliderOffset, Vector3? localScale = null)
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
