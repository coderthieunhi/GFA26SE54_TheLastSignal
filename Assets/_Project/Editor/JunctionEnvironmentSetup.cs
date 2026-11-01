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
    public static class JunctionEnvironmentSetup
    {
        private const string TilesSourceDir = "Assets/_Project/Sprites/Environments/Junction/Tiles";
        private const string PropsSourceDir = "Assets/_Project/Sprites/Environments/Junction/Props";
        private const string SignageSourceDir = "Assets/_Project/Sprites/Environments/Junction/Signage";
        private const string TileAssetsDir = "Assets/_Project/Tiles/Junction";

        private static bool IsReadOnlyOrPlayMode()
        {
            return EditorApplication.isPlayingOrWillChangePlaymode
                || EditorApplication.isPlaying
                || System.Environment.CommandLine.IndexOf("-readonly", System.StringComparison.OrdinalIgnoreCase) >= 0
                || System.Environment.CommandLine.IndexOf("--virtual-project-clone", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        [MenuItem("Tools/Facility/Setup Central Junction Art & Environment", false, 54)]
        public static void SetupJunctionArt()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            // 1. Configure and import Tile textures (PPU = 128)
            ConfigureTextures();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            // 2. Create / update Tile ScriptableObjects
            CreateTileAssets();
            AssetDatabase.SaveAssets();

            // 3. Decorate Central Junction in the active scene connecting all 4 rooms
            DecorateActiveScene();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("<color=green>[JunctionEnvironmentSetup] Successfully built and decorated Central Junction connecting North (Crew Quarters), West (Med Bay), East (Console Alpha), South (Air Lock)!</color>");
        }

        public static void ConfigureTextures()
        {
            string[] dirs = { TilesSourceDir, PropsSourceDir, SignageSourceDir };
            foreach (var dir in dirs)
            {
                if (!Directory.Exists(dir)) continue;
                string[] files = Directory.GetFiles(dir, "*.png");
                foreach (string file in files)
                {
                    TextureImporter importer = AssetImporter.GetAtPath(file) as TextureImporter;
                    if (importer == null) continue;

                    bool changed = false;
                    if (importer.textureType != TextureImporterType.Sprite) { importer.textureType = TextureImporterType.Sprite; changed = true; }
                    if (importer.spritePixelsPerUnit != 128f) { importer.spritePixelsPerUnit = 128f; changed = true; }
                    if (importer.filterMode != FilterMode.Point) { importer.filterMode = FilterMode.Point; changed = true; }
                    if (importer.textureCompression != TextureImporterCompression.Uncompressed) { importer.textureCompression = TextureImporterCompression.Uncompressed; changed = true; }
                    if (importer.wrapMode != TextureWrapMode.Clamp) { importer.wrapMode = TextureWrapMode.Clamp; changed = true; }

                    TextureImporterSettings texSettings = new TextureImporterSettings();
                    importer.ReadTextureSettings(texSettings);
                    bool texChanged = false;
                    if (texSettings.spriteMeshType != SpriteMeshType.FullRect) { texSettings.spriteMeshType = SpriteMeshType.FullRect; texChanged = true; }
                    if (texSettings.spriteExtrude != 1) { texSettings.spriteExtrude = 1; texChanged = true; }
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
                string tileName = Path.GetFileNameWithoutExtension(file);
                string assetPath = $"{TileAssetsDir}/{tileName}.asset";

                Tile tile = AssetDatabase.LoadAssetAtPath<Tile>(assetPath);
                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(file);
                if (sprite == null)
                {
                    Object[] allAssets = AssetDatabase.LoadAllAssetsAtPath(file);
                    foreach (Object a in allAssets)
                    {
                        if (a is Sprite sp) { sprite = sp; break; }
                    }
                }

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
            if (gridGo == null)
            {
                Debug.LogError("[JunctionEnvironmentSetup] Facility_Grid not found in active scene!");
                return;
            }

            Tilemap floorMap = gridGo.transform.Find("Floor_Tilemap")?.GetComponent<Tilemap>();
            Tilemap wallMap = gridGo.transform.Find("Wall_Tilemap")?.GetComponent<Tilemap>();

            if (floorMap == null || wallMap == null)
            {
                Debug.LogError("[JunctionEnvironmentSetup] Floor_Tilemap or Wall_Tilemap not found under Facility_Grid!");
                return;
            }

            Undo.RecordObject(floorMap, "Decorate Junction Floor");
            Undo.RecordObject(wallMap, "Decorate Junction Walls");

            // Configure TilemapRenderer materials to URP 2D Lit
            TilemapRenderer floorRenderer = floorMap.GetComponent<TilemapRenderer>();
            TilemapRenderer wallRenderer = wallMap.GetComponent<TilemapRenderer>();
            Material urp2DLitMat = AssetDatabase.LoadAssetAtPath<Material>("Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Lit-Default.mat");
            if (urp2DLitMat == null)
            {
                Shader urp2DLit = Shader.Find("Universal Render Pipeline/2D/Sprite-Lit-Default");
                if (urp2DLit != null) urp2DLitMat = new Material(urp2DLit);
            }
            if (urp2DLitMat != null)
            {
                if (floorRenderer != null && (floorRenderer.sharedMaterial == null || floorRenderer.sharedMaterial.shader != urp2DLitMat.shader))
                {
                    floorRenderer.sharedMaterial = urp2DLitMat;
                    EditorUtility.SetDirty(floorRenderer);
                }
                if (wallRenderer != null && (wallRenderer.sharedMaterial == null || wallRenderer.sharedMaterial.shader != urp2DLitMat.shader))
                {
                    wallRenderer.sharedMaterial = urp2DLitMat;
                    EditorUtility.SetDirty(wallRenderer);
                }
            }

            // --- LOAD FLOOR TILES ---
            Tile fMetal01 = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/Junction_Floor_Metal_01.asset");
            Tile fMetal02 = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/Junction_Floor_Metal_02.asset") ?? fMetal01;
            Tile fDark = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/Junction_Floor_Metal_Dark.asset") ?? fMetal01;
            Tile fWorn = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/Junction_Floor_Worn.asset") ?? fMetal01;
            Tile fDiscolored = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/Junction_Floor_Discolored.asset") ?? fMetal01;
            Tile fScratched = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/Junction_Floor_Scratched.asset") ?? fMetal01;
            Tile fOil = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/Junction_Floor_OilStain.asset") ?? fMetal01;
            Tile fCracked = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/Junction_Floor_Cracked.asset") ?? fMetal01;
            Tile fLifted = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/Junction_Floor_Lifted_Plate.asset") ?? fMetal01;
            Tile fElectricalBurn = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/Junction_Floor_Electrical_Burn.asset") ?? fMetal01;
            Tile fReinforcedBorder = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/Junction_Floor_Reinforced_Border.asset") ?? fDark;
            Tile fPressureTrack = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/Junction_Floor_Pressure_Track.asset") ?? fDark;
            Tile fGrateBroken = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/Junction_Floor_Grate_Broken.asset") ?? fMetal01;
            Tile fCoolant = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/Junction_Floor_Coolant_Stain.asset") ?? fOil;
            Tile fStencil = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/Junction_Floor_Faded_Stencil.asset") ?? fMetal01;
            Tile fCartWear = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/Junction_Floor_Cart_Wear.asset") ?? fMetal01;
            Tile fDoorScuff = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/Junction_Floor_Door_Scuff.asset") ?? fMetal01;
            Tile fCableH = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/Junction_Floor_Cable_Channel_H.asset") ?? fMetal01;
            Tile fCableV = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/Junction_Floor_Cable_Channel_V.asset") ?? fMetal01;
            Tile fGrate = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/Junction_Floor_Grate.asset") ?? fMetal01;
            Tile fHatch = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/Junction_Floor_Hatch.asset") ?? fMetal01;
            Tile fHazH = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/Junction_Floor_Hazard_Stripe_H.asset") ?? fMetal01;
            Tile fHazV = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/Junction_Floor_Hazard_Stripe_V.asset") ?? fMetal01;
            Tile fHazCorner = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/Junction_Floor_Hazard_Corner.asset") ?? fMetal01;
            Tile fTransition = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/Junction_Floor_Transition_Corridor.asset") ?? fMetal01;

            // --- LOAD CENTRAL HUB TILES ---
            Tile hubHatch = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/Junction_Center_Hatch.asset") ?? fHatch;
            Tile hubGrate = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/Junction_Center_Grate.asset") ?? fGrate;
            Tile hubHaz = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/Junction_Center_Hazard_Border.asset") ?? fHazH;
            Tile hubCables = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/Junction_Center_Cable_Hub.asset") ?? fCableH;

            // --- LOAD DOORWAY THRESHOLDS ---
            Tile threshN = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/Junction_Doorway_Threshold_North.asset") ?? fHazH;
            Tile threshS = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/Junction_Doorway_Threshold_South.asset") ?? fHazH;
            Tile threshW = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/Junction_Doorway_Threshold_West.asset") ?? fHazV;
            Tile threshE = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/Junction_Doorway_Threshold_East.asset") ?? fHazV;

            // --- LOAD WALL TILES ---
            Tile wStraightH = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/Junction_Wall_Straight_H.asset");
            Tile wStraightV = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/Junction_Wall_Straight_V.asset");
            Tile wCornerTL = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/Junction_Wall_Corner_TL.asset") ?? wStraightH;
            Tile wCornerTR = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/Junction_Wall_Corner_TR.asset") ?? wStraightH;
            Tile wCornerBL = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/Junction_Wall_Corner_BL.asset") ?? wStraightH;
            Tile wCornerBR = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/Junction_Wall_Corner_BR.asset") ?? wStraightH;
            Tile wInnerTL = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/Junction_Wall_InnerCorner_TL.asset") ?? wStraightH;
            Tile wInnerTR = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/Junction_Wall_InnerCorner_TR.asset") ?? wStraightH;
            Tile wInnerBL = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/Junction_Wall_InnerCorner_BL.asset") ?? wStraightH;
            Tile wInnerBR = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/Junction_Wall_InnerCorner_BR.asset") ?? wStraightH;
            Tile wEndL = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/Junction_Wall_EndCap_Left.asset") ?? wStraightH;
            Tile wEndR = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/Junction_Wall_EndCap_Right.asset") ?? wStraightH;
            Tile wBeam = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/Junction_Wall_Support_Beam.asset") ?? wStraightH;
            Tile wCables = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/Junction_Wall_Cables.asset") ?? wStraightH;
            Tile wPipes = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/Junction_Wall_Pipes.asset") ?? wStraightH;
            Tile wVent = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/Junction_Wall_Vent.asset") ?? wStraightH;
            Tile wDamaged = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/Junction_Wall_Damaged.asset") ?? wStraightH;
            Tile wLightUtil = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/Junction_Wall_Light_Utility.asset") ?? wStraightH;
            Tile wLightWarn = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/Junction_Wall_Light_Warning.asset") ?? wStraightH;
            Tile wBlowout = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/Junction_Wall_Electrical_Blowout.asset") ?? wDamaged;
            Tile wDetached = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/Junction_Wall_Detached_Panel.asset") ?? wDamaged;

            // ========================================================
            // 1. CLEAR EXISTING PROTOTYPE TILES IN JUNCTION REGION
            // X in [-10..10], Y in [18..37]
            // Note: Keep existing room doors and threshold boundaries intact!
            // ========================================================
            for (int x = -10; x <= 10; x++)
            {
                for (int y = 18; y <= 37; y++)
                {
                    // Do not clear MedBay room interior (X <= -11)
                    // Do not clear ConsoleAlpha room interior (X >= 11)
                    // Do not clear CrewQuarters room interior (Y >= 38)
                    // Do not clear Airlock room interior (Y <= 17)
                    var p = new Vector3Int(x, y, 0);
                    floorMap.SetTile(p, null);
                    wallMap.SetTile(p, null);
                }
            }

            // ========================================================
            // 2. PAINT NORTH CORRIDOR (Y in [32..36], X in [-1, 0])
            // Door threshold at Y = 37 connects to Crew Quarters!
            // Walls at X = -2 (West) and X = 1 (East)
            // ========================================================
            for (int y = 32; y <= 36; y++)
            {
                // Floor: Dominantly intact metal panels, single stenciled crew chevron, cable conduit
                Tile fn0 = fMetal01;
                Tile fn1 = fMetal02;

                if (y == 36) { fn0 = fDoorScuff; fn1 = fStencil; }
                else if (y == 35) { fn0 = fMetal02; fn1 = fMetal01; }
                else if (y == 34) { fn0 = fMetal01; fn1 = fWorn; }
                else if (y == 33) { fn0 = fMetal01; fn1 = fMetal02; }
                else if (y == 32) { fn0 = fMetal01; fn1 = fMetal02; }

                floorMap.SetTile(new Vector3Int(-1, y, 0), fn0);
                floorMap.SetTile(new Vector3Int(0, y, 0), fn1);

                // West Wall (X = -2)
                Tile wW = (y == 35) ? wCables : (y == 33) ? wLightUtil : (y == 32) ? wBeam : wStraightV;
                wallMap.SetTile(new Vector3Int(-2, y, 0), wW);

                // East Wall (X = 1)
                Tile wE = (y == 35) ? wPipes : (y == 33) ? wStraightV : (y == 34) ? wStraightV : wStraightV;
                wallMap.SetTile(new Vector3Int(1, y, 0), wE);
            }
            // North Doorway Threshold at Y = 37 (under Door_CrewQuarters)
            floorMap.SetTile(new Vector3Int(-1, 37, 0), threshN);
            floorMap.SetTile(new Vector3Int(0, 37, 0), threshN);
            wallMap.SetTile(new Vector3Int(-2, 37, 0), wStraightV);
            wallMap.SetTile(new Vector3Int(1, 37, 0), wStraightV);

            // ========================================================
            // 3. PAINT SOUTH CORRIDOR (Y in [19..23], X in [-1, 0])
            // Door threshold at Y = 18 connects to Airlock!
            // Walls at X = -2 (West) and X = 1 (East)
            // ========================================================
            for (int y = 18; y <= 23; y++)
            {
                // Floor: Clean, uniform industrial steel deck plates (harmonized across entire corridor)
                Tile fs0 = ((y + -1) % 2 == 0) ? fMetal01 : fMetal02;
                Tile fs1 = ((y + 0) % 2 == 0) ? fMetal01 : fMetal02;

                floorMap.SetTile(new Vector3Int(-1, y, 0), fs0);
                floorMap.SetTile(new Vector3Int(0, y, 0), fs1);

                // West Wall (X = -2)
                Tile wW = (y == 21) ? wPipes : (y == 22) ? wLightWarn : (y == 20) ? wBeam : wStraightV;
                wallMap.SetTile(new Vector3Int(-2, y, 0), wW);

                // East Wall (X = 1)
                Tile wE = (y == 20) ? wCables : (y == 22) ? wLightWarn : (y == 23) ? wBeam : wStraightV;
                wallMap.SetTile(new Vector3Int(1, y, 0), wE);
            }

            // ========================================================
            // 4. PAINT WEST CORRIDOR (X in [-10..-5], Y in [27, 28])
            // Door threshold at X = -10 connects to Med Bay!
            // Walls at Y = 29 (North) and Y = 26 (South)
            // ========================================================
            for (int x = -10; x <= -5; x++)
            {
                // Floor: Dominantly intact plates with localized coolant leak and stress crack
                Tile fw0 = fMetal01;
                Tile fw1 = fMetal02;

                if (x == -9) { fw0 = fCartWear; fw1 = fMetal02; }
                else if (x == -8) { fw0 = fCoolant; fw1 = fMetal01; }
                else if (x == -7) { fw0 = fWorn; fw1 = fMetal02; }
                else if (x == -6) { fw0 = fMetal01; fw1 = fCracked; }
                else if (x == -5) { fw0 = fMetal02; fw1 = fMetal01; }

                floorMap.SetTile(new Vector3Int(x, 28, 0), fw0);
                floorMap.SetTile(new Vector3Int(x, 27, 0), fw1);

                // North Wall (Y = 29) - Detached wall panel at X = -7
                Tile wN = (x == -8) ? wLightUtil : (x == -7) ? wDetached : (x == -6) ? wVent : (x == -9) ? wCables : (x == -5) ? wBeam : wStraightH;
                wallMap.SetTile(new Vector3Int(x, 29, 0), wN);

                // South Wall (Y = 26)
                Tile wS = (x == -8) ? wPipes : (x == -6) ? wStraightH : wStraightH;
                wallMap.SetTile(new Vector3Int(x, 26, 0), wS);
            }
            // West Doorway Threshold at X = -10 (in front of MedBay door at -10.5)
            floorMap.SetTile(new Vector3Int(-10, 28, 0), threshW);
            floorMap.SetTile(new Vector3Int(-10, 27, 0), threshW);

            // ========================================================
            // 5. PAINT EAST CORRIDOR (X in [4..9], Y in [27, 28])
            // Door threshold at X = 9 connects to Console Alpha!
            // Walls at Y = 29 (North) and Y = 26 (South)
            // ========================================================
            for (int x = 4; x <= 9; x++)
            {
                // Floor: Clean, uniform industrial steel deck plates, removed floor cables
                Tile fe0 = ((x + 28) % 2 == 0) ? fMetal01 : fMetal02;
                Tile fe1 = ((x + 27) % 2 == 0) ? fMetal01 : fMetal02;

                // Subtle electrical scorch near north wall blowout at x = 7, clean walkable floor
                if (x == 7) { fe0 = fElectricalBurn; }

                floorMap.SetTile(new Vector3Int(x, 28, 0), fe0);
                floorMap.SetTile(new Vector3Int(x, 27, 0), fe1);

                // North Wall (Y = 29) - Electrical blowout on wall at X = 7 aligned with floor burn
                Tile wN = (x == 7) ? wBlowout : (x == 8) ? wBeam : (x == 6) ? wCables : (x == 5) ? wLightUtil : wStraightH;
                wallMap.SetTile(new Vector3Int(x, 29, 0), wN);

                // South Wall (Y = 26)
                Tile wS = (x == 6) ? wCables : (x == 8) ? wVent : (x == 5) ? wPipes : wStraightH;
                wallMap.SetTile(new Vector3Int(x, 26, 0), wS);
            }
            // East Doorway Threshold at X = 9 (in front of Console Alpha door at 10.5)
            floorMap.SetTile(new Vector3Int(9, 28, 0), threshE);
            floorMap.SetTile(new Vector3Int(9, 27, 0), threshE);

            // ========================================================
            // 6. PAINT CENTRAL JUNCTION CHAMBER (X in [-4..3], Y in [24..31])
            // Size: 8x8 floor area with modular central feature and corner wall blocks
            // ========================================================
            for (int x = -4; x <= 3; x++)
            {
                for (int y = 24; y <= 31; y++)
                {
                    // Center 2x2 Feature at X in [-1, 0], Y in [27, 28] (Clean steel mounting deck under Power Console)
                    if ((x == -1 || x == 0) && (y == 27 || y == 28)) floorMap.SetTile(new Vector3Int(x, y, 0), fDark);
                    // Reinforced mounting platform rim immediately framing 2x2 cluster
                    else if ((x == -2 || x == 1) && (y == 27 || y == 28)) floorMap.SetTile(new Vector3Int(x, y, 0), fReinforcedBorder);
                    else if ((x == -1 || x == 0) && (y == 29 || y == 26)) floorMap.SetTile(new Vector3Int(x, y, 0), fReinforcedBorder);
                    // Corridor entrance transitions
                    else if ((x == -1 || x == 0) && y == 31) floorMap.SetTile(new Vector3Int(x, y, 0), fTransition);
                    else if (x == -4 && (y == 28 || y == 27)) floorMap.SetTile(new Vector3Int(x, y, 0), fTransition);
                    else if (x == 3 && (y == 28 || y == 27)) floorMap.SetTile(new Vector3Int(x, y, 0), fTransition);
                    // Chamber utility / vents
                    else if ((x == -3 && y == 25) || (x == 2 && y == 30)) floorMap.SetTile(new Vector3Int(x, y, 0), fGrate);
                    else if (x == 2 && y == 25) floorMap.SetTile(new Vector3Int(x, y, 0), fHatch);
                    // Controlled sparse wear: only single isolated tiles at opposite edges
                    else if (x == -3 && y == 29) floorMap.SetTile(new Vector3Int(x, y, 0), fWorn);
                    else if (x == 2 && y == 26) floorMap.SetTile(new Vector3Int(x, y, 0), fScratched);
                    else if (x == 0 && y == 25) floorMap.SetTile(new Vector3Int(x, y, 0), fOil);
                    else
                    {
                        // Dominant intact metal plates alternating rhythmically
                        Tile fIntact = ((x + y) % 2 == 0) ? fMetal01 : fMetal02;
                        floorMap.SetTile(new Vector3Int(x, y, 0), fIntact);
                    }
                }
            }

            // ========================================================
            // 7. CORNER WALL BLOCKS & INNER CORNER TURNS
            // NW Corner Block: X in [-4..-2], Y in [30..32]
            // NE Corner Block: X in [1..3], Y in [30..32]
            // SW Corner Block: X in [-4..-2], Y in [23..25]
            // SE Corner Block: X in [1..3], Y in [23..25]
            // ========================================================
            // NW Block inner corner at (-2, 31)
            wallMap.SetTile(new Vector3Int(-2, 31, 0), wInnerTL);
            wallMap.SetTile(new Vector3Int(-3, 31, 0), wStraightH);
            wallMap.SetTile(new Vector3Int(-4, 31, 0), wStraightH);
            wallMap.SetTile(new Vector3Int(-4, 30, 0), wStraightV);
            wallMap.SetTile(new Vector3Int(-4, 29, 0), wInnerBL);

            // NE Block inner corner at (1, 31)
            wallMap.SetTile(new Vector3Int(1, 31, 0), wInnerTR);
            wallMap.SetTile(new Vector3Int(2, 31, 0), wStraightH);
            wallMap.SetTile(new Vector3Int(3, 31, 0), wStraightH);
            wallMap.SetTile(new Vector3Int(3, 30, 0), wStraightV);
            wallMap.SetTile(new Vector3Int(3, 29, 0), wInnerBR);

            // SW Block inner corner at (-2, 24)
            wallMap.SetTile(new Vector3Int(-2, 24, 0), wInnerBL);
            wallMap.SetTile(new Vector3Int(-3, 24, 0), wStraightH);
            wallMap.SetTile(new Vector3Int(-4, 24, 0), wStraightH);
            wallMap.SetTile(new Vector3Int(-4, 25, 0), wStraightV);
            wallMap.SetTile(new Vector3Int(-4, 26, 0), wInnerTL);

            // SE Block inner corner at (1, 24)
            wallMap.SetTile(new Vector3Int(1, 24, 0), wInnerBR);
            wallMap.SetTile(new Vector3Int(2, 24, 0), wStraightH);
            wallMap.SetTile(new Vector3Int(3, 24, 0), wStraightH);
            wallMap.SetTile(new Vector3Int(3, 25, 0), wStraightV);
            wallMap.SetTile(new Vector3Int(3, 26, 0), wInnerTR);

            // Compress bounds for optimized rendering & culling
            floorMap.CompressBounds();
            wallMap.CompressBounds();

            // ========================================================
            // 8. SETUP DIRECTIONAL SIGNAGE & INDUSTRIAL PROPS
            // ========================================================
            SetupSignageAndProps();
        }

        private static void SetupSignageAndProps()
        {
            // Root GameObject for Junction Props
            GameObject junctionRoot = GameObject.Find("Junction_Environment");
            if (junctionRoot == null)
            {
                junctionRoot = new GameObject("Junction_Environment");
                Undo.RegisterCreatedObjectUndo(junctionRoot, "Create Junction_Environment Root");
            }

            // Clean previous children to make it safe to rerun
            while (junctionRoot.transform.childCount > 0)
            {
                Undo.DestroyObjectImmediate(junctionRoot.transform.GetChild(0).gameObject);
            }

            // Material (URP 2D Lit)
            Material litMat = AssetDatabase.LoadAssetAtPath<Material>("Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Lit-Default.mat");
            if (litMat == null)
            {
                Shader litShader = Shader.Find("Universal Render Pipeline/2D/Sprite-Lit-Default");
                if (litShader != null) litMat = new Material(litShader);
            }

            // --- 1. DIRECTIONAL SIGNS ---
            GameObject signsRoot = new GameObject("Signage");
            signsRoot.transform.SetParent(junctionRoot.transform, false);

            CreateSign(signsRoot, "Junction_Sign_CrewQuarters", new Vector3(-0.5f, 31.85f, 0f), "Sign_North_CrewQuarters", litMat);
            CreateSign(signsRoot, "Junction_Sign_AirLock", new Vector3(-0.5f, 23.85f, 0f), "Sign_South_AirLock", litMat);
            CreateSign(signsRoot, "Junction_Sign_MedBay", new Vector3(-4.6f, 29.85f, 0f), "Sign_West_MedBay", litMat);
            CreateSign(signsRoot, "Junction_Sign_ConsoleAlpha", new Vector3(3.6f, 29.85f, 0f), "Sign_East_ConsoleAlpha", litMat);

            // --- 2. DECORATION PROPS (Intentional, uncluttered, focused) ---
            GameObject propsRoot = new GameObject("Props");
            propsRoot.transform.SetParent(junctionRoot.transform, false);

            // Junction Terminal Box on North-West wall
            CreateProp(propsRoot, "Prop_Junction_Cable_Junction_Box", new Vector3(-2.0f, 33.5f, 0f), "Prop_NW_JunctionBox", litMat, "Obstacles", 6);
            // Pipe Section on East Corridor wall
            CreateProp(propsRoot, "Prop_Junction_Pipe_Section", new Vector3(5.5f, 29.2f, 0f), "Prop_PipeSection_East", litMat, "Obstacles", 6);
            // Wall Vent on West Corridor wall
            CreateProp(propsRoot, "Prop_Junction_Wall_Vent", new Vector3(-7.5f, 29.2f, 0f), "Prop_WallVent_West", litMat, "Obstacles", 6);
            // Warning Beacon on South Corridor corner
            CreateProp(propsRoot, "Prop_Junction_Warning_Beacon", new Vector3(1.0f, 22.8f, 0f), "Prop_WarningBeacon_South", litMat, "Obstacles", 7);
            // Loose Damaged Panel propped against SE corner wall
            CreateProp(propsRoot, "Prop_Junction_Damaged_Panel", new Vector3(2.5f, 24.8f, 0f), "Prop_DamagedPanel_SE", litMat, "Floor", 1);

            // --- 3. CENTRAL POWER & SURVEILLANCE CONSOLE AT NODE A CENTER (-0.5, 27.5) ---
            CreateCenterConsole(propsRoot, litMat);
        }

        private static void CreateSign(GameObject parent, string spriteName, Vector3 pos, string goName, Material mat)
        {
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{SignageSourceDir}/{spriteName}.png");
            if (sprite == null) return;

            GameObject go = new GameObject(goName);
            go.transform.SetParent(parent.transform, false);
            go.transform.position = pos;

            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingLayerName = "Obstacles";
            sr.sortingOrder = 10;
            if (mat != null) sr.sharedMaterial = mat;
        }

        private static void CreateProp(GameObject parent, string spriteName, Vector3 pos, string goName, Material mat, string sortingLayer, int order)
        {
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{PropsSourceDir}/{spriteName}.png");
            if (sprite == null) return;

            GameObject go = new GameObject(goName);
            go.transform.SetParent(parent.transform, false);
            go.transform.position = pos;

            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingLayerName = sortingLayer;
            sr.sortingOrder = order;
            if (mat != null) sr.sharedMaterial = mat;
        }

        private static void CreateCenterConsole(GameObject parent, Material mat)
        {
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{PropsSourceDir}/Prop_NodeA_Power_Console.png");
            if (sprite == null) return;

            GameObject consoleGo = new GameObject("Prop_NodeA_Power_Console");
            consoleGo.transform.SetParent(parent.transform, false);
            consoleGo.transform.position = new Vector3(0.0f, 28.0f, 0f);
            int interactableLayer = LayerMask.NameToLayer("Interactable");
            consoleGo.layer = interactableLayer != -1 ? interactableLayer : 10;

            SpriteRenderer sr = consoleGo.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingLayerName = "Obstacles";
            sr.sortingOrder = 3;
            if (mat != null) sr.sharedMaterial = mat;

            BoxCollider2D col = consoleGo.AddComponent<BoxCollider2D>();
            col.size = new Vector2(1.6f, 1.4f);
            col.offset = new Vector2(0f, 0f);

            CircleCollider2D triggerCol = consoleGo.AddComponent<CircleCollider2D>();
            triggerCol.radius = 1.8f;
            triggerCol.isTrigger = true;

            // Point Light 2D on Console
            GameObject lightGo = new GameObject("Light_Console_NodeA");
            lightGo.transform.SetParent(consoleGo.transform, false);
            lightGo.transform.localPosition = new Vector3(0f, 0f, 0f);

            Light2D light = lightGo.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Point;
            light.color = new Color(0.1f, 0.95f, 0.85f, 1f); // Cyan phosphor glow
            light.intensity = 1.2f;
            light.pointLightOuterRadius = 3.5f;
            light.pointLightInnerRadius = 0.3f;

            AudioSource audioSrc = consoleGo.GetComponent<AudioSource>();
            if (audioSrc == null) audioSrc = consoleGo.AddComponent<AudioSource>();
            audioSrc.playOnAwake = false;
            audioSrc.spatialBlend = 0.5f;

            // Add ContestedPowerSubstation for 3v1 gameplay
            var substationComp = consoleGo.AddComponent<TheLastSignal.Entities.Environment.ContestedPowerSubstation>();
            var unpowered = AssetDatabase.LoadAssetAtPath<Sprite>($"{PropsSourceDir}/Prop_NodeA_BioReactor_Unpowered.png") ?? sprite;
            var powered = AssetDatabase.LoadAssetAtPath<Sprite>($"{PropsSourceDir}/Prop_NodeA_BioReactor_Powered.png");

            var soSub = new SerializedObject(substationComp);
            soSub.FindProperty("spriteRenderer").objectReferenceValue = sr;
            soSub.FindProperty("unpoweredSprite").objectReferenceValue = unpowered;
            soSub.FindProperty("poweredSprite").objectReferenceValue = powered;
            soSub.ApplyModifiedProperties();

            // Clean up any deprecated surveillance objects
            GameObject oldCam = GameObject.Find("Surveillance_Camera_NodeA");
            if (oldCam != null) Undo.DestroyObjectImmediate(oldCam);
            GameObject hudCanvas = GameObject.Find("HUD_Canvas");
            if (hudCanvas != null)
            {
                Transform oldUI = hudCanvas.transform.Find("RoomInspectionTerminalUI");
                if (oldUI != null) Undo.DestroyObjectImmediate(oldUI.gameObject);
                Transform oldPanel = hudCanvas.transform.Find("Terminal_Panel");
                if (oldPanel != null) Undo.DestroyObjectImmediate(oldPanel.gameObject);
            }

            // Ensure MedBay has ceiling lights for inspection
            EnsureMedBayInspectionLights();
        }

        private static void EnsureMedBayInspectionLights()
        {
            GameObject medBayRoot = GameObject.Find("MedBay_Decorations");
            if (medBayRoot == null) return;

            Transform existingL1 = medBayRoot.transform.Find("Light_MedBay_Inspection_Oper");
            if (existingL1 == null)
            {
                GameObject l1 = new GameObject("Light_MedBay_Inspection_Oper");
                l1.transform.SetParent(medBayRoot.transform, false);
                l1.transform.position = new Vector3(-16.0f, 28.5f, 0f);
                Light2D light1 = l1.AddComponent<Light2D>();
                light1.lightType = Light2D.LightType.Point;
                light1.color = new Color(0.2f, 0.85f, 0.95f, 1f);
                light1.intensity = 0.8f;
                light1.pointLightOuterRadius = 6.0f;
                light1.pointLightInnerRadius = 0.5f;
            }

            Transform existingL2 = medBayRoot.transform.Find("Light_MedBay_Inspection_Surg");
            if (existingL2 == null)
            {
                GameObject l2 = new GameObject("Light_MedBay_Inspection_Surg");
                l2.transform.SetParent(medBayRoot.transform, false);
                l2.transform.position = new Vector3(-13.5f, 26.5f, 0f);
                Light2D light2 = l2.AddComponent<Light2D>();
                light2.lightType = Light2D.LightType.Point;
                light2.color = new Color(0.4f, 0.90f, 0.80f, 1f);
                light2.intensity = 0.8f;
                light2.pointLightOuterRadius = 5.0f;
                light2.pointLightInnerRadius = 0.5f;
            }
        }
    }
}
#endif
