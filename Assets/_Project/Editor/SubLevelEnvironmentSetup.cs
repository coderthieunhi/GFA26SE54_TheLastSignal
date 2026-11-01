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
    /// <summary>
    /// Master builder and decorator for the Heavy Brutalist Sub-Level:
    /// - Node C (The Abyssal Catwalk Nexus)
    /// - Deep Storage (Classified Isotope Vault)
    /// - Power Relay Bay (High-Voltage Dynamo Substation)
    /// - Reactor Core (The Titan Lead Monolith Climax)
    /// 100% brand-new custom pixel art assets and 2D atmospheric lighting.
    /// </summary>
    public static class SubLevelEnvironmentSetup
    {
        private const string SpritesRoot = "Assets/_Project/Sprites/Environments/SubLevel";
        private const string TilesSourceDir = SpritesRoot + "/Tiles";
        private const string PropsSourceDir = SpritesRoot + "/Props";
        private const string DecalsSourceDir = SpritesRoot + "/Decals";
        private const string SignageSourceDir = SpritesRoot + "/Signage";
        private const string DoorsSourceDir = SpritesRoot + "/Doors";
        private const string TileAssetsDir = "Assets/_Project/Tiles/SubLevel";

        [MenuItem("Tools/Facility/Build Complete Heavy Brutalist Sub-Level", false, 60)]
        public static void BuildCompleteSubLevel()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            // 1. Configure and import all SubLevel textures with 128 PPU
            ConfigureTextures();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            // 2. Create ScriptableObject Tiles
            CreateTileAssets();
            AssetDatabase.SaveAssets();

            // 3. Paint Tilemaps for Node C, Corridors, Deep Storage, Power Relay, Reactor Core
            PaintSubLevelTilemaps();

            // 4. Instantiate Props, Decals, and Terminals
            InstantiateSubLevelProps();

            // 5. Configure SubLevel Doors
            ConfigureSubLevelDoors();

            // 6. Setup 2D Atmospheric Lighting
            SetupSubLevelLighting();

            // 7. Setup Particle VFX
            SetupSubLevelVFX();

            // 8. Apply Phase 2 Refinements (Expanded floor transitions, junction boxes, warning signage)
            SubLevelRefinementApplier.ApplyAllRefinements();

            // 9. Apply Phase 3 Upgrades (Interactive terminals, stanchions, emergency props, path smoothing)
            SubLevelPhase3UpgradeApplier.ApplyPhase3();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("<color=green>[SubLevelEnvironmentSetup] Successfully built and decorated Complete Heavy Brutalist Sub-Level with all Phase 2 & 3 enhancements!</color>");
        }

        public static void ConfigureTextures()
        {
            string[] dirs = { TilesSourceDir, PropsSourceDir, DecalsSourceDir, SignageSourceDir, DoorsSourceDir };
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
                    if (importer.spriteImportMode != SpriteImportMode.Single) { importer.spriteImportMode = SpriteImportMode.Single; changed = true; }
                    if (importer.spritePixelsPerUnit != 128f) { importer.spritePixelsPerUnit = 128f; changed = true; }
                    if (importer.filterMode != FilterMode.Point) { importer.filterMode = FilterMode.Point; changed = true; }
                    if (importer.textureCompression != TextureImporterCompression.Uncompressed) { importer.textureCompression = TextureImporterCompression.Uncompressed; changed = true; }
                    if (importer.wrapMode != TextureWrapMode.Clamp) { importer.wrapMode = TextureWrapMode.Clamp; changed = true; }

                    TextureImporterSettings texSettings = new TextureImporterSettings();
                    importer.ReadTextureSettings(texSettings);
                    if (texSettings.spriteMeshType != SpriteMeshType.FullRect) { texSettings.spriteMeshType = SpriteMeshType.FullRect; changed = true; }
                    if (texSettings.spriteExtrude != 1) { texSettings.spriteExtrude = 1; changed = true; }

                    if (changed)
                    {
                        importer.SetTextureSettings(texSettings);
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

        public static void PaintSubLevelTilemaps()
        {
            var gridGo = GameObject.Find("Facility_Grid");
            if (gridGo == null) return;

            var wallTm = gridGo.transform.Find("Wall_Tilemap")?.GetComponent<Tilemap>();
            var floorTm = gridGo.transform.Find("Floor_Tilemap")?.GetComponent<Tilemap>();
            if (wallTm == null || floorTm == null) return;

            Undo.RecordObject(wallTm, "Paint SubLevel Walls");
            Undo.RecordObject(floorTm, "Paint SubLevel Floor");

            // Load Base Tiles
            Tile fTread = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/SubLevel_Floor_Heavy_Treadplate.asset");
            Tile fGrate = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/SubLevel_Floor_Catwalk_Grate.asset") != null ? AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/SubLevel_Floor_Catwalk_Grate.asset") : fTread;
            Tile fGrateHaz = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/SubLevel_Floor_Catwalk_Grate_Hazard.asset") != null ? AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/SubLevel_Floor_Catwalk_Grate_Hazard.asset") : fGrate;
            Tile fEdgeN = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/SubLevel_Floor_Catwalk_Edge_N.asset") != null ? AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/SubLevel_Floor_Catwalk_Edge_N.asset") : fGrate;
            Tile fEdgeS = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/SubLevel_Floor_Catwalk_Edge_S.asset") != null ? AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/SubLevel_Floor_Catwalk_Edge_S.asset") : fGrate;
            Tile fEdgeW = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/SubLevel_Floor_Catwalk_Edge_W.asset") != null ? AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/SubLevel_Floor_Catwalk_Edge_W.asset") : fGrate;
            Tile fEdgeE = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/SubLevel_Floor_Catwalk_Edge_E.asset") != null ? AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/SubLevel_Floor_Catwalk_Edge_E.asset") : fGrate;
            Tile fCornNW = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/SubLevel_Floor_Catwalk_Corner_NW.asset") != null ? AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/SubLevel_Floor_Catwalk_Corner_NW.asset") : fGrate;
            Tile fCornNE = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/SubLevel_Floor_Catwalk_Corner_NE.asset") != null ? AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/SubLevel_Floor_Catwalk_Corner_NE.asset") : fGrate;
            Tile fCornSW = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/SubLevel_Floor_Catwalk_Corner_SW.asset") != null ? AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/SubLevel_Floor_Catwalk_Corner_SW.asset") : fGrate;
            Tile fCornSE = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/SubLevel_Floor_Catwalk_Corner_SE.asset") != null ? AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/SubLevel_Floor_Catwalk_Corner_SE.asset") : fGrate;
            Tile fRubber = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/SubLevel_Floor_Dielectric_Rubber.asset") != null ? AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/SubLevel_Floor_Dielectric_Rubber.asset") : fTread;
            Tile fTrack = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/SubLevel_Floor_Crane_Track.asset") != null ? AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/SubLevel_Floor_Crane_Track.asset") : fTread;
            Tile fRad = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/SubLevel_Floor_Radiation_Plate.asset") != null ? AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/SubLevel_Floor_Radiation_Plate.asset") : fTread;
            Tile fTransStripe = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/SubLevel_Floor_Transition_Stripe.asset") != null ? AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/SubLevel_Floor_Transition_Stripe.asset") : fTread;

            Tile wWallH = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/SubLevel_Wall_Reinforced_H.asset");
            Tile wWallV = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/SubLevel_Wall_Reinforced_V.asset");
            Tile wCornerTL = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/SubLevel_Wall_Corner_TL.asset") != null ? AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/SubLevel_Wall_Corner_TL.asset") : wWallH;
            Tile wCornerTR = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/SubLevel_Wall_Corner_TR.asset") != null ? AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/SubLevel_Wall_Corner_TR.asset") : wWallH;
            Tile wCornerBL = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/SubLevel_Wall_Corner_BL.asset") != null ? AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/SubLevel_Wall_Corner_BL.asset") : wWallH;
            Tile wCornerBR = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/SubLevel_Wall_Corner_BR.asset") != null ? AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/SubLevel_Wall_Corner_BR.asset") : wWallH;
            Tile wVent = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/SubLevel_Wall_Hazard_Vent.asset") != null ? AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/SubLevel_Wall_Hazard_Vent.asset") : wWallH;

            // Load Refinement Tiles
            Tile fCapNW = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/SubLevel_Floor_Catwalk_Cap_NW.asset") != null ? AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/SubLevel_Floor_Catwalk_Cap_NW.asset") : fEdgeN;
            Tile fCapNE = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/SubLevel_Floor_Catwalk_Cap_NE.asset") != null ? AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/SubLevel_Floor_Catwalk_Cap_NE.asset") : fEdgeN;
            Tile fCapSW = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/SubLevel_Floor_Catwalk_Cap_SW.asset") != null ? AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/SubLevel_Floor_Catwalk_Cap_SW.asset") : fEdgeS;
            Tile fCapSE = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/SubLevel_Floor_Catwalk_Cap_SE.asset") != null ? AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/SubLevel_Floor_Catwalk_Cap_SE.asset") : fEdgeS;
            Tile fThreshold = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/SubLevel_Floor_Threshold_Plate.asset") != null ? AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/SubLevel_Floor_Threshold_Plate.asset") : fTransStripe;
            Tile wColumn = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/SubLevel_Wall_Reinforced_Column.asset") != null ? AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/SubLevel_Wall_Reinforced_Column.asset") : wWallV;

            Tile fCraneBufN = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/SubLevel_Floor_Crane_Buffer_North.asset") != null ? AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/SubLevel_Floor_Crane_Buffer_North.asset") : fTrack;
            Tile fCraneBufS = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/SubLevel_Floor_Crane_Buffer_South.asset") != null ? AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/SubLevel_Floor_Crane_Buffer_South.asset") : fTrack;
            Tile fCargoTL = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/SubLevel_Floor_Cargo_Bay_TL.asset") != null ? AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/SubLevel_Floor_Cargo_Bay_TL.asset") : fTread;
            Tile fCargoTR = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/SubLevel_Floor_Cargo_Bay_TR.asset") != null ? AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/SubLevel_Floor_Cargo_Bay_TR.asset") : fTread;
            Tile fCargoBL = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/SubLevel_Floor_Cargo_Bay_BL.asset") != null ? AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/SubLevel_Floor_Cargo_Bay_BL.asset") : fTread;
            Tile fCargoBR = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/SubLevel_Floor_Cargo_Bay_BR.asset") != null ? AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/SubLevel_Floor_Cargo_Bay_BR.asset") : fTread;
            Tile wCraneAnchor = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/SubLevel_Wall_Crane_Gantry_Anchor.asset") != null ? AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/SubLevel_Wall_Crane_Gantry_Anchor.asset") : wWallH;

            Tile fBusbarH = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/SubLevel_Floor_Busbar_Trench_H.asset") != null ? AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/SubLevel_Floor_Busbar_Trench_H.asset") : fRubber;
            Tile fBusbarV = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/SubLevel_Floor_Busbar_Trench_V.asset") != null ? AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/SubLevel_Floor_Busbar_Trench_V.asset") : fRubber;
            Tile fHvBorderN = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/SubLevel_Floor_HV_Border_N.asset") != null ? AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/SubLevel_Floor_HV_Border_N.asset") : fRubber;
            Tile fHvBorderS = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/SubLevel_Floor_HV_Border_S.asset") != null ? AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/SubLevel_Floor_HV_Border_S.asset") : fRubber;
            Tile wHvTrunk = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/SubLevel_Wall_HV_Busbar_Trunk.asset") != null ? AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/SubLevel_Wall_HV_Busbar_Trunk.asset") : wWallV;

            Tile fLeadPlain = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/SubLevel_Floor_Lead_Plates_Plain.asset") != null ? AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/SubLevel_Floor_Lead_Plates_Plain.asset") : fTread;
            Tile fCoolTrench = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/SubLevel_Floor_Coolant_Trench.asset") != null ? AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/SubLevel_Floor_Coolant_Trench.asset") : fTread;
            Tile fPipeCradle = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/SubLevel_Floor_Pipe_Cradle.asset") != null ? AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/SubLevel_Floor_Pipe_Cradle.asset") : fTread;
            Tile wCoolFlange = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/SubLevel_Wall_Coolant_Penetration_Flange.asset") != null ? AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/SubLevel_Wall_Coolant_Penetration_Flange.asset") : wWallV;
            Tile wCoolFlangeE = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/SubLevel_Wall_Coolant_Penetration_Flange_E.asset") != null ? AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/SubLevel_Wall_Coolant_Penetration_Flange_E.asset") : wWallV;
            Tile wCtrlRods = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/SubLevel_Wall_Control_Rod_Actuators.asset") != null ? AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/SubLevel_Wall_Control_Rod_Actuators.asset") : wWallH;

            Tile fSignStorage = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/SubLevel_Floor_Sign_West_Storage.asset") != null ? AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/SubLevel_Floor_Sign_West_Storage.asset") : fTread;
            Tile fSignRelay = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/SubLevel_Floor_Sign_East_Relay.asset") != null ? AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/SubLevel_Floor_Sign_East_Relay.asset") : fTread;
            Tile fSignReactor = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/SubLevel_Floor_Sign_South_Reactor.asset") != null ? AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/SubLevel_Floor_Sign_South_Reactor.asset") : fTread;

            Tile fCoolBridge = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/SubLevel_Floor_Coolant_Trench_Bridge.asset") != null ? AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/SubLevel_Floor_Coolant_Trench_Bridge.asset") : fTread;
            Tile fTJunctionL = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/SubLevel_Floor_Sign_TJunction_L.asset") != null ? AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/SubLevel_Floor_Sign_TJunction_L.asset") : fTread;
            Tile fTJunctionR = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/SubLevel_Floor_Sign_TJunction_R.asset") != null ? AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/SubLevel_Floor_Sign_TJunction_R.asset") : fTread;
            Tile wDirectoryL = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/SubLevel_Wall_Directory_L.asset") != null ? AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/SubLevel_Wall_Directory_L.asset") : wWallH;
            Tile wDirectoryR = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/SubLevel_Wall_Directory_R.asset") != null ? AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/SubLevel_Wall_Directory_R.asset") : wWallH;
            Tile wJambTop = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/SubLevel_Wall_Door_Jamb_Top.asset") != null ? AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/SubLevel_Wall_Door_Jamb_Top.asset") : wWallV;
            Tile wJambBot = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/SubLevel_Wall_Door_Jamb_Bottom.asset") != null ? AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/SubLevel_Wall_Door_Jamb_Bottom.asset") : wWallV;
            Tile wFan = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/SubLevel_Wall_Exhaust_Fan.asset") != null ? AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/SubLevel_Wall_Exhaust_Fan.asset") : wVent;

            // ========================================================
            // 1. CORRIDOR: CARGO HOLD -> NODE C (X: [-1, 0], Y: [-30..-25])
            // ========================================================
            for (int y = -30; y <= -25; y++)
            {
                floorTm.SetTile(new Vector3Int(-1, y, 0), fTread);
                floorTm.SetTile(new Vector3Int(0, y, 0), fTread);
                wallTm.SetTile(new Vector3Int(-2, y, 0), wWallV);
                wallTm.SetTile(new Vector3Int(1, y, 0), wWallV);
            }

            // ========================================================
            // 2. NODE C: THE ABYSSAL CATWALK NEXUS (X: [-4..3], Y: [-38..-31])
            // Catwalk grid floor over void chasm, reinforced perimeter walls
            // Openings: North (X in [-1, 0]), South (X in [-1, 0]), East (Y in [-35, -34])
            // ========================================================
            // Clear old prototype tiles in Node C area
            for (int x = -5; x <= 4; x++)
            {
                for (int y = -39; y <= -30; y++)
                {
                    wallTm.SetTile(new Vector3Int(x, y, 0), null);
                    floorTm.SetTile(new Vector3Int(x, y, 0), null);
                }
            }

            // Floor: Catwalk Grate interior with continuous directional hazard borders & clean doorway caps
            for (int x = -4; x <= 3; x++)
            {
                for (int y = -38; y <= -31; y++)
                {
                    Tile t = fGrate;
                    if (x == -4 && y == -31) t = fCornNW;
                    else if (x == 3 && y == -31) t = fCornNE;
                    else if (x == -4 && y == -38) t = fCornSW;
                    else if (x == 3 && y == -38) t = fCornSE;
                    else if (y == -31)
                    {
                        if (x == -2) t = fCapNW;
                        else if (x == 1) t = fCapNE;
                        else if (x == -1 || x == 0) t = fGrate; // Open passage at doorway
                        else t = fEdgeN;
                    }
                    else if (y == -38)
                    {
                        if (x == -2) t = fCapSW;
                        else if (x == 1) t = fCapSE;
                        else if (x == -1 || x == 0) t = fGrate; // Open passage at doorway
                        else t = fEdgeS;
                    }
                    else if (x == -4) t = fEdgeW;
                    else if (x == 3) t = (y == -34 || y == -35) ? fGrate : fEdgeE; // Open passage to East corridor
                    floorTm.SetTile(new Vector3Int(x, y, 0), t);
                }
            }

            // North Walls (Opening at X in [-1, 0], Y = -30)
            for (int x = -4; x <= -2; x++) wallTm.SetTile(new Vector3Int(x, -30, 0), wWallH);
            for (int x = 1; x <= 3; x++) wallTm.SetTile(new Vector3Int(x, -30, 0), wWallH);
            floorTm.SetTile(new Vector3Int(-1, -30, 0), fThreshold);
            floorTm.SetTile(new Vector3Int(0, -30, 0), fThreshold);

            // South Walls (Opening at X in [-1, 0], Y = -39)
            for (int x = -4; x <= -2; x++) wallTm.SetTile(new Vector3Int(x, -39, 0), wWallH);
            for (int x = 1; x <= 3; x++) wallTm.SetTile(new Vector3Int(x, -39, 0), wWallH);
            floorTm.SetTile(new Vector3Int(-1, -39, 0), fThreshold);
            floorTm.SetTile(new Vector3Int(0, -39, 0), fThreshold);

            // West Walls (Continuous solid blast wall at X = -5, Y in [-38..-31] with structural columns)
            for (int y = -38; y <= -31; y++)
            {
                Tile wt = wWallV;
                if (y == -34 || y == -35) wt = wVent;
                else if (y == -33 || y == -36) wt = wColumn;
                wallTm.SetTile(new Vector3Int(-5, y, 0), wt);
            }

            // East Walls (Opening at Y in [-35, -34] for Loop East bend)
            for (int y = -38; y <= -36; y++) wallTm.SetTile(new Vector3Int(4, y, 0), wWallV);
            for (int y = -33; y <= -31; y++) wallTm.SetTile(new Vector3Int(4, y, 0), wWallV);
            // East opening floor (Threshold plate transition)
            floorTm.SetTile(new Vector3Int(4, -34, 0), fThreshold);
            floorTm.SetTile(new Vector3Int(4, -35, 0), fThreshold);

            // 4 Corner bulkheads
            wallTm.SetTile(new Vector3Int(-5, -30, 0), wCornerTL);
            wallTm.SetTile(new Vector3Int(4, -30, 0), wCornerTR);
            wallTm.SetTile(new Vector3Int(-5, -39, 0), wCornerBL);
            wallTm.SetTile(new Vector3Int(4, -39, 0), wCornerBR);

            // ========================================================
            // 3. CORRIDOR C STEM (X: [-1, 0], Y: [-45..-40])
            // ========================================================
            for (int y = -45; y <= -40; y++)
            {
                floorTm.SetTile(new Vector3Int(-1, y, 0), fTread);
                floorTm.SetTile(new Vector3Int(0, y, 0), fTread);
                wallTm.SetTile(new Vector3Int(-2, y, 0), wWallV);
                wallTm.SetTile(new Vector3Int(1, y, 0), wWallV);
            }

            // Stem corridor directional navigation stencil
            floorTm.SetTile(new Vector3Int(0, -44, 0), fSignReactor);

            // ========================================================
            // 4. T-JUNCTION & LATERAL BRANCHES (Y: [-47, -46])
            // - West branch: X in [-8..-2] -> leads to Deep Storage Door at X = -7.5
            // - East branch: X in [1..7] -> leads to Power Relay Door at X = 7.5
            // ========================================================
            for (int x = -8; x <= 7; x++)
            {
                for (int y = -47; y <= -46; y++)
                {
                    floorTm.SetTile(new Vector3Int(x, y, 0), fTread);
                }
                // North wall of lateral corridor
                if (x < -1 || x > 0) wallTm.SetTile(new Vector3Int(x, -45, 0), wWallH);
                // South wall of lateral corridor
                wallTm.SetTile(new Vector3Int(x, -48, 0), wWallH);
            }

            // Lateral corridor directional navigation stencils & intersection chevrons
            floorTm.SetTile(new Vector3Int(-4, -46, 0), fSignStorage);
            floorTm.SetTile(new Vector3Int(3, -46, 0), fSignRelay);
            floorTm.SetTile(new Vector3Int(-1, -46, 0), fTJunctionL);
            floorTm.SetTile(new Vector3Int(0, -46, 0), fTJunctionR);

            // South wall master wayfinding directory sign (facing player)
            wallTm.SetTile(new Vector3Int(-1, -48, 0), wDirectoryL);
            wallTm.SetTile(new Vector3Int(0, -48, 0), wDirectoryR);

            // ========================================================
            // 5. DEEP STORAGE ROOM (X: [-18..-9], Y: [-52..-43]) - 10x10
            // Classified Vault with Heavy Crane Rails, Staging Bays & End Buffers
            // Openings:
            // - East: Door_DeepStorage at X = -8, Y in [-47, -46]
            // - West: Loop West bend connection at X = -19, Y in [-47, -46]
            // ========================================================
            // Clear area first
            for (int x = -19; x <= -8; x++)
            {
                for (int y = -53; y <= -42; y++)
                {
                    wallTm.SetTile(new Vector3Int(x, y, 0), null);
                    floorTm.SetTile(new Vector3Int(x, y, 0), null);
                }
            }

            // Interior Floor (X in [-18..-9], Y in [-52..-43])
            for (int x = -18; x <= -9; x++)
            {
                for (int y = -52; y <= -43; y++)
                {
                    // Crane tracks run down rows X = -14 and X = -13
                    if (x == -14 || x == -13)
                    {
                        if (y == -43) floorTm.SetTile(new Vector3Int(x, y, 0), fCraneBufN);
                        else if (y == -52) floorTm.SetTile(new Vector3Int(x, y, 0), fCraneBufS);
                        else floorTm.SetTile(new Vector3Int(x, y, 0), fTrack);
                    }
                    else
                    {
                        floorTm.SetTile(new Vector3Int(x, y, 0), fTread);
                    }
                }
            }

            // Painted Cargo Staging Bays (West & East wings)
            floorTm.SetTile(new Vector3Int(-18, -49, 0), fCargoTL);
            floorTm.SetTile(new Vector3Int(-16, -49, 0), fCargoTR);
            floorTm.SetTile(new Vector3Int(-18, -51, 0), fCargoBL);
            floorTm.SetTile(new Vector3Int(-16, -51, 0), fCargoBR);

            floorTm.SetTile(new Vector3Int(-11, -49, 0), fCargoTL);
            floorTm.SetTile(new Vector3Int(-9, -49, 0), fCargoTR);
            floorTm.SetTile(new Vector3Int(-11, -51, 0), fCargoBL);
            floorTm.SetTile(new Vector3Int(-9, -51, 0), fCargoBR);

            // North Wall (Y = -42, X in [-18..-9]) with Crane Gantry Anchors
            for (int x = -18; x <= -9; x++)
            {
                Tile wt = wWallH;
                if (x == -14 || x == -13) wt = wCraneAnchor;
                wallTm.SetTile(new Vector3Int(x, -42, 0), wt);
            }

            // South Wall (Y = -53, X in [-18..-9]) with Crane Gantry Anchors
            for (int x = -18; x <= -9; x++)
            {
                Tile wt = wWallH;
                if (x == -14 || x == -13) wt = wCraneAnchor;
                wallTm.SetTile(new Vector3Int(x, -53, 0), wt);
            }

            // West Wall (X = -19, Opening at Y in [-47, -46] for West Loop with Door Jambs)
            for (int y = -52; y <= -48; y++) wallTm.SetTile(new Vector3Int(-19, y, 0), (y == -48) ? wJambBot : wWallV);
            for (int y = -45; y <= -43; y++) wallTm.SetTile(new Vector3Int(-19, y, 0), (y == -45) ? wJambTop : wWallV);
            floorTm.SetTile(new Vector3Int(-19, -46, 0), fTransStripe);
            floorTm.SetTile(new Vector3Int(-19, -47, 0), fTransStripe);

            // East Wall (X = -8, Opening at Y in [-47, -46] for Door_DeepStorage)
            for (int y = -52; y <= -48; y++) wallTm.SetTile(new Vector3Int(-8, y, 0), wWallV);
            for (int y = -45; y <= -43; y++) wallTm.SetTile(new Vector3Int(-8, y, 0), wWallV);
            floorTm.SetTile(new Vector3Int(-8, -46, 0), fTransStripe);
            floorTm.SetTile(new Vector3Int(-8, -47, 0), fTransStripe);
            floorTm.SetTile(new Vector3Int(-7, -46, 0), fThreshold);
            floorTm.SetTile(new Vector3Int(-7, -47, 0), fThreshold);

            // Corners
            wallTm.SetTile(new Vector3Int(-19, -42, 0), wCornerTL);
            wallTm.SetTile(new Vector3Int(-8, -42, 0), wCornerTR);
            wallTm.SetTile(new Vector3Int(-19, -53, 0), wCornerBL);
            wallTm.SetTile(new Vector3Int(-8, -53, 0), wCornerBR);

            // ========================================================
            // 6. POWER RELAY BAY ROOM (X: [8..17], Y: [-52..-43]) - 10x10
            // High Voltage Substation with Terracotta Dielectric Rubber Floors & Busbar Channels
            // Openings:
            // - West: Door_PowerRelayBay at X = 7, Y in [-47, -46]
            // - South: Corridor_Relay_Reactor at X in [12, 13], Y = -53
            // ========================================================
            // Clear area first
            for (int x = 7; x <= 18; x++)
            {
                for (int y = -53; y <= -42; y++)
                {
                    wallTm.SetTile(new Vector3Int(x, y, 0), null);
                    floorTm.SetTile(new Vector3Int(x, y, 0), null);
                }
            }

            // Interior Floor (X in [8..17], Y in [-52..-43])
            for (int x = 8; x <= 17; x++)
            {
                for (int y = -52; y <= -43; y++)
                {
                    floorTm.SetTile(new Vector3Int(x, y, 0), fRubber);
                }
            }

            // Protective Grated Busbar Trenches
            floorTm.SetTile(new Vector3Int(9, -47, 0), fBusbarH);
            floorTm.SetTile(new Vector3Int(10, -47, 0), fBusbarH);
            floorTm.SetTile(new Vector3Int(15, -47, 0), fBusbarH);
            floorTm.SetTile(new Vector3Int(16, -47, 0), fBusbarH);

            floorTm.SetTile(new Vector3Int(12, -45, 0), fBusbarV);
            floorTm.SetTile(new Vector3Int(12, -49, 0), fBusbarV);
            floorTm.SetTile(new Vector3Int(12, -50, 0), fBusbarV);

            // High Voltage Caution Borders
            for (int x = 10; x <= 15; x++)
            {
                floorTm.SetTile(new Vector3Int(x, -44, 0), fHvBorderN);
                floorTm.SetTile(new Vector3Int(x, -51, 0), fHvBorderS);
            }

            // North Wall (Y = -42, X in [8..17]) with Industrial Exhaust Fans
            for (int x = 8; x <= 17; x++)
            {
                Tile wt = (x == 12 || x == 13) ? wVent : wWallH;
                if (x == 10 || x == 11 || x == 14 || x == 15) wt = wFan;
                wallTm.SetTile(new Vector3Int(x, -42, 0), wt);
            }

            // East Wall (X = 18, solid with HV Busbar Trunking)
            for (int y = -52; y <= -43; y++)
            {
                Tile wt = wWallV;
                if (y == -45 || y == -49) wt = wHvTrunk;
                wallTm.SetTile(new Vector3Int(18, y, 0), wt);
            }

            // West Wall (X = 7, Opening at Y in [-47, -46] for Door_PowerRelayBay)
            for (int y = -52; y <= -48; y++) wallTm.SetTile(new Vector3Int(7, y, 0), wWallV);
            for (int y = -45; y <= -43; y++) wallTm.SetTile(new Vector3Int(7, y, 0), wWallV);
            floorTm.SetTile(new Vector3Int(7, -46, 0), fTransStripe);
            floorTm.SetTile(new Vector3Int(7, -47, 0), fTransStripe);
            floorTm.SetTile(new Vector3Int(6, -46, 0), fThreshold);
            floorTm.SetTile(new Vector3Int(6, -47, 0), fThreshold);

            // South Wall (Y = -53, Opening at X in [12, 13] for Corridor_Relay_Reactor)
            for (int x = 8; x <= 11; x++) wallTm.SetTile(new Vector3Int(x, -53, 0), wWallH);
            for (int x = 14; x <= 17; x++) wallTm.SetTile(new Vector3Int(x, -53, 0), wWallH);
            floorTm.SetTile(new Vector3Int(12, -53, 0), fThreshold);
            floorTm.SetTile(new Vector3Int(13, -53, 0), fThreshold);

            // Corners
            wallTm.SetTile(new Vector3Int(7, -42, 0), wCornerTL);
            wallTm.SetTile(new Vector3Int(18, -42, 0), wCornerTR);
            wallTm.SetTile(new Vector3Int(7, -53, 0), wCornerBL);
            wallTm.SetTile(new Vector3Int(18, -53, 0), wCornerBR);

            // ========================================================
            // 7. CORRIDOR: POWER RELAY -> REACTOR CORE (X in [12, 13], Y in [-59..-54])
            // ========================================================
            for (int y = -59; y <= -54; y++)
            {
                floorTm.SetTile(new Vector3Int(12, y, 0), fTread);
                floorTm.SetTile(new Vector3Int(13, y, 0), fTread);
                wallTm.SetTile(new Vector3Int(11, y, 0), wWallV);
                wallTm.SetTile(new Vector3Int(14, y, 0), wWallV);
            }

            // ========================================================
            // 8. REACTOR CORE ROOM (X: [7..18], Y: [-69..-60]) - 12x10
            // The Titan Lead Monolith Room - Heavy Lead Plates, Drainage Trench & Control Rods
            // Opening: North (X in [12, 13], Y = -59)
            // ========================================================
            // Clear area first
            for (int x = 6; x <= 19; x++)
            {
                for (int y = -70; y <= -59; y++)
                {
                    wallTm.SetTile(new Vector3Int(x, y, 0), null);
                    floorTm.SetTile(new Vector3Int(x, y, 0), null);
                }
            }

            // Interior Floor (X in [7..18], Y in [-69..-60])
            for (int x = 7; x <= 18; x++)
            {
                for (int y = -69; y <= -60; y++)
                {
                    // Radiation containment basin deck
                    if ((x >= 10 && x <= 15) && (y >= -67 && y <= -62))
                        floorTm.SetTile(new Vector3Int(x, y, 0), fLeadPlain);
                    else
                        floorTm.SetTile(new Vector3Int(x, y, 0), fTread);
                }
            }

            // Strictly 4 cardinal hazard perimeter markers
            floorTm.SetTile(new Vector3Int(11, -63, 0), fRad);
            floorTm.SetTile(new Vector3Int(14, -63, 0), fRad);
            floorTm.SetTile(new Vector3Int(11, -66, 0), fRad);
            floorTm.SetTile(new Vector3Int(14, -66, 0), fRad);

            // Coolant Drainage Perimeter Trench & North Footbridge
            for (int x = 10; x <= 15; x++)
            {
                if (x == 12 || x == 13)
                {
                    floorTm.SetTile(new Vector3Int(x, -61, 0), fCoolBridge); // Walkway bridge over coolant
                }
                else
                {
                    floorTm.SetTile(new Vector3Int(x, -61, 0), fCoolTrench);
                }
                floorTm.SetTile(new Vector3Int(x, -68, 0), fCoolTrench);
            }
            for (int y = -67; y <= -62; y++)
            {
                floorTm.SetTile(new Vector3Int(9, y, 0), fCoolTrench);
                floorTm.SetTile(new Vector3Int(16, y, 0), fCoolTrench);
            }

            // Horizontal Coolant Pipe Floor Saddles
            floorTm.SetTile(new Vector3Int(7, -65, 0), fPipeCradle);
            floorTm.SetTile(new Vector3Int(8, -65, 0), fPipeCradle);
            floorTm.SetTile(new Vector3Int(17, -65, 0), fPipeCradle);
            floorTm.SetTile(new Vector3Int(18, -65, 0), fPipeCradle);

            // North Wall (Y = -59, Opening at X in [12, 13])
            for (int x = 7; x <= 11; x++) wallTm.SetTile(new Vector3Int(x, -59, 0), wWallH);
            for (int x = 14; x <= 18; x++) wallTm.SetTile(new Vector3Int(x, -59, 0), wWallH);
            floorTm.SetTile(new Vector3Int(12, -59, 0), fTread);
            floorTm.SetTile(new Vector3Int(13, -59, 0), fTread);

            // South Wall (Y = -70, solid with Hydraulic Control Rod Actuators)
            for (int x = 7; x <= 18; x++)
            {
                Tile wt = (x == 12 || x == 13) ? wVent : wWallH;
                if (x >= 11 && x <= 14) wt = wCtrlRods;
                wallTm.SetTile(new Vector3Int(x, -70, 0), wt);
            }

            // West Wall (X = 6, solid with Coolant Pipe Flange penetration at Y = -65)
            for (int y = -69; y <= -60; y++)
            {
                Tile wt = (y == -65) ? wCoolFlange : wWallV;
                wallTm.SetTile(new Vector3Int(6, y, 0), wt);
            }

            // East Wall (X = 19, solid with Mirrored Coolant Pipe Flange penetration at Y = -65)
            for (int y = -69; y <= -60; y++)
            {
                Tile wt = (y == -65) ? wCoolFlangeE : wWallV;
                wallTm.SetTile(new Vector3Int(19, y, 0), wt);
            }

            // Corners
            wallTm.SetTile(new Vector3Int(6, -59, 0), wCornerTL);
            wallTm.SetTile(new Vector3Int(19, -59, 0), wCornerTR);
            wallTm.SetTile(new Vector3Int(6, -70, 0), wCornerBL);
            wallTm.SetTile(new Vector3Int(19, -70, 0), wCornerBR);

            EditorUtility.SetDirty(wallTm);
            EditorUtility.SetDirty(floorTm);
        }

        public static void InstantiateSubLevelProps()
        {
            var oldRoot = GameObject.Find("SubLevel_Environment");
            if (oldRoot != null) Undo.DestroyObjectImmediate(oldRoot);

            GameObject envRoot = new GameObject("SubLevel_Environment");
            Undo.RegisterCreatedObjectUndo(envRoot, "Create SubLevel_Environment");

            GameObject propsRoot = new GameObject("Props");
            propsRoot.transform.SetParent(envRoot.transform);

            Material litMat = AssetDatabase.LoadAssetAtPath<Material>("Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Lit-Default.mat");
            if (litMat == null)
            {
                Shader s = Shader.Find("Universal Render Pipeline/2D/Sprite-Lit-Default");
                if (s != null) litMat = new Material(s);
            }

            GameObject SpawnProp(string name, Vector3 pos, string spritePath, bool addCol = true, Vector2 colSize = default, string sortingLayer = "Obstacles", int order = 5)
            {
                var sp = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
                if (sp == null) return null;
                var go = new GameObject(name);
                go.transform.SetParent(propsRoot.transform);
                go.transform.position = pos;
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = sp;
                sr.sortingLayerName = sortingLayer;
                sr.sortingOrder = order;
                if (litMat != null) sr.sharedMaterial = litMat;

                if (addCol)
                {
                    var col = go.AddComponent<BoxCollider2D>();
                    col.size = colSize == default ? sp.rect.size / 128f : colSize;
                }
                return go;
            }

            GameObject SpawnShadow(string name, Vector3 pos, string spritePath, float alpha = 0.50f)
            {
                var sp = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
                if (sp == null) return null;
                var go = new GameObject(name);
                go.transform.SetParent(propsRoot.transform);
                go.transform.position = pos;
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = sp;
                sr.sortingLayerName = "Floor";
                sr.sortingOrder = 3;
                sr.color = new Color(0f, 0f, 0f, alpha);
                if (litMat != null) sr.sharedMaterial = litMat;
                return go;
            }

            GameObject SpawnWallDropShadow(string name, Vector3 pos)
            {
                var sp = AssetDatabase.LoadAssetAtPath<Sprite>($"{TilesSourceDir}/SubLevel_Wall_DropShadow_H.png");
                if (sp == null) return null;
                var go = new GameObject(name);
                go.transform.SetParent(propsRoot.transform);
                go.transform.position = pos;
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = sp;
                sr.sortingLayerName = "Floor";
                sr.sortingOrder = 1;
                sr.color = new Color(1f, 1f, 1f, 0.75f);
                if (litMat != null) sr.sharedMaterial = litMat;
                return go;
            }

            // ==================== NORTH WALL DROP SHADOWS ====================
            // Shadows are 0.5 units high (span Y in [WallBottom - 0.5, WallBottom]).
            // Bottom of wall is at integer Y; center of shadow must be at Y = WallBottom - 0.25f to sit 100% flush!
            // Deep Storage North Wall (X: [-18..-9], wall bottom = -42.0f -> Y = -42.25f)
            for (int x = -18; x <= -9; x++) SpawnWallDropShadow($"Shadow_Wall_DeepStorage_{x}", new Vector3(x + 0.5f, -42.25f, 0f));
            // Power Relay Bay North Wall (X: [8..17], wall bottom = -42.0f -> Y = -42.25f)
            for (int x = 8; x <= 17; x++) SpawnWallDropShadow($"Shadow_Wall_PowerRelay_{x}", new Vector3(x + 0.5f, -42.25f, 0f));
            // Reactor Core North Wall (X: [7..11] & [14..18], wall bottom = -59.0f -> Y = -59.25f)
            for (int x = 7; x <= 11; x++) SpawnWallDropShadow($"Shadow_Wall_ReactorCore_{x}", new Vector3(x + 0.5f, -59.25f, 0f));
            for (int x = 14; x <= 18; x++) SpawnWallDropShadow($"Shadow_Wall_ReactorCore_{x}", new Vector3(x + 0.5f, -59.25f, 0f));
            // Node C North Wall (X: [-4..-2] & [1..3], wall bottom = -30.0f -> Y = -30.25f)
            for (int x = -4; x <= -2; x++) SpawnWallDropShadow($"Shadow_Wall_NodeC_{x}", new Vector3(x + 0.5f, -30.25f, 0f));
            for (int x = 1; x <= 3; x++) SpawnWallDropShadow($"Shadow_Wall_NodeC_{x}", new Vector3(x + 0.5f, -30.25f, 0f));
            // T-Junction North Wall (X: [-8..-2] & [1..7], wall bottom = -45.0f -> Y = -45.25f)
            for (int x = -8; x <= -2; x++) SpawnWallDropShadow($"Shadow_Wall_TJunction_{x}", new Vector3(x + 0.5f, -45.25f, 0f));
            for (int x = 1; x <= 7; x++) SpawnWallDropShadow($"Shadow_Wall_TJunction_{x}", new Vector3(x + 0.5f, -45.25f, 0f));

            // ==================== NODE C PROPS & SHADOWS ====================
            // 4 Massive Abyssal Struts supporting the central catwalk
            SpawnProp("Prop_NodeC_Abyssal_Strut_NW", new Vector3(-3.5f, -32.5f, 0f), $"{PropsSourceDir}/Prop_NodeC_Abyssal_Strut.png", true, new Vector2(0.8f, 1.8f));
            SpawnProp("Prop_NodeC_Abyssal_Strut_NE", new Vector3(2.5f, -32.5f, 0f), $"{PropsSourceDir}/Prop_NodeC_Abyssal_Strut.png", true, new Vector2(0.8f, 1.8f));
            SpawnProp("Prop_NodeC_Abyssal_Strut_SW", new Vector3(-3.5f, -36.5f, 0f), $"{PropsSourceDir}/Prop_NodeC_Abyssal_Strut.png", true, new Vector2(0.8f, 1.8f));
            SpawnProp("Prop_NodeC_Abyssal_Strut_SE", new Vector3(2.5f, -36.5f, 0f), $"{PropsSourceDir}/Prop_NodeC_Abyssal_Strut.png", true, new Vector2(0.8f, 1.8f));

            // Industrial Safety Guardrails along Catwalk perimeter
            // West Edge (X = -4.3, Y in [-38..-31] except manifold area at [-35, -34])
            for (int y = -38; y <= -31; y++)
            {
                if (y == -34 || y == -35) continue;
                SpawnProp($"Prop_NodeC_Guardrail_W_{y}", new Vector3(-4.3f, y + 0.5f, 0f), $"{PropsSourceDir}/Prop_NodeC_Guardrail_V.png", true, new Vector2(0.2f, 1.0f), "Obstacles", 6);
            }
            // East Edge (X = 3.3, Y in [-38..-31] except East passage at [-35, -34])
            for (int y = -38; y <= -31; y++)
            {
                if (y == -34 || y == -35) continue;
                SpawnProp($"Prop_NodeC_Guardrail_E_{y}", new Vector3(3.3f, y + 0.5f, 0f), $"{PropsSourceDir}/Prop_NodeC_Guardrail_V.png", true, new Vector2(0.2f, 1.0f), "Obstacles", 6);
            }
            // North Edge (Y = -30.7, X in [-4..-2] & [1..3])
            for (int x = -4; x <= -2; x++) SpawnProp($"Prop_NodeC_Guardrail_N_{x}", new Vector3(x + 0.5f, -30.7f, 0f), $"{PropsSourceDir}/Prop_NodeC_Guardrail_H.png", true, new Vector2(1.0f, 0.2f), "Obstacles", 6);
            for (int x = 1; x <= 3; x++) SpawnProp($"Prop_NodeC_Guardrail_N_{x}", new Vector3(x + 0.5f, -30.7f, 0f), $"{PropsSourceDir}/Prop_NodeC_Guardrail_H.png", true, new Vector2(1.0f, 0.2f), "Obstacles", 6);
            // South Edge (Y = -38.3, X in [-4..-2] & [1..3])
            for (int x = -4; x <= -2; x++) SpawnProp($"Prop_NodeC_Guardrail_S_{x}", new Vector3(x + 0.5f, -38.3f, 0f), $"{PropsSourceDir}/Prop_NodeC_Guardrail_H.png", true, new Vector2(1.0f, 0.2f), "Obstacles", 6);
            for (int x = 1; x <= 3; x++) SpawnProp($"Prop_NodeC_Guardrail_S_{x}", new Vector3(x + 0.5f, -38.3f, 0f), $"{PropsSourceDir}/Prop_NodeC_Guardrail_H.png", true, new Vector2(1.0f, 0.2f), "Obstacles", 6);

            // Steam Manifold on West Wall
            SpawnProp("Prop_NodeC_Steam_Manifold", new Vector3(-4.4f, -34.5f, 0f), $"{PropsSourceDir}/Prop_NodeC_HighPressure_Manifold.png", true, new Vector2(1.0f, 0.8f));

            // Warning Bollards flanking the catwalk edges (clearing center walkway) + contact shadows
            SpawnProp("Prop_NodeC_Bollard_L", new Vector3(-3.2f, -34.5f, 0f), $"{PropsSourceDir}/Prop_NodeC_Warning_Bollard.png", true, new Vector2(0.4f, 0.8f));
            SpawnShadow("Shadow_NodeC_Bollard_L", new Vector3(-3.2f, -34.85f, 0f), $"{PropsSourceDir}/Shadow_SubLevel_Small.png", 0.50f);
            SpawnProp("Prop_NodeC_Bollard_R", new Vector3(2.2f, -34.5f, 0f), $"{PropsSourceDir}/Prop_NodeC_Warning_Bollard.png", true, new Vector2(0.4f, 0.8f));
            SpawnShadow("Shadow_NodeC_Bollard_R", new Vector3(2.2f, -34.85f, 0f), $"{PropsSourceDir}/Shadow_SubLevel_Small.png", 0.50f);

            // Node C Sign
            SpawnProp("Sign_NodeC", new Vector3(-0.5f, -30.6f, 0f), $"{SignageSourceDir}/Sign_SubLevel_NodeC.png", false, default, "Obstacles", 4);

            // ==================== DEEP STORAGE PROPS & SHADOWS ====================
            // Isotope Vault Chests (Placed in painted staging bays) + shadows
            SpawnProp("Prop_DeepStorage_Vault_Chest_1", new Vector3(-17.0f, -50.0f, 0f), $"{PropsSourceDir}/Prop_DeepStorage_Lead_Vault_Chest.png", true, new Vector2(1.0f, 0.9f));
            SpawnShadow("Shadow_DeepStorage_Vault_Chest_1", new Vector3(-17.0f, -50.35f, 0f), $"{PropsSourceDir}/Shadow_SubLevel_Medium.png", 0.55f);
            SpawnProp("Prop_DeepStorage_Vault_Chest_2", new Vector3(-10.0f, -50.0f, 0f), $"{PropsSourceDir}/Prop_DeepStorage_Lead_Vault_Chest.png", true, new Vector2(1.0f, 0.9f));
            SpawnShadow("Shadow_DeepStorage_Vault_Chest_2", new Vector3(-10.0f, -50.35f, 0f), $"{PropsSourceDir}/Shadow_SubLevel_Medium.png", 0.55f);

            // Rad-Suit Lockers at North Wall staging area + shadow
            SpawnProp("Prop_DeepStorage_RadSuit_Locker", new Vector3(-16.5f, -44.5f, 0f), $"{PropsSourceDir}/Prop_DeepStorage_RadSuit_Locker.png", true, new Vector2(0.9f, 0.9f));
            SpawnShadow("Shadow_DeepStorage_RadSuit_Locker", new Vector3(-16.5f, -44.85f, 0f), $"{PropsSourceDir}/Shadow_SubLevel_Small.png", 0.55f);

            // Stacks of Military Crates at North Wall + shadows
            SpawnProp("Prop_DeepStorage_Crate_Stack_1", new Vector3(-10.5f, -44.5f, 0f), $"{PropsSourceDir}/Prop_DeepStorage_Military_Crate_Stack.png", true, new Vector2(1.0f, 0.8f));
            SpawnShadow("Shadow_DeepStorage_Crate_Stack_1", new Vector3(-10.5f, -44.80f, 0f), $"{PropsSourceDir}/Shadow_SubLevel_Medium.png", 0.50f);
            SpawnProp("Prop_DeepStorage_Crate_Stack_2", new Vector3(-13.5f, -51.5f, 0f), $"{PropsSourceDir}/Prop_DeepStorage_Military_Crate_Stack.png", true, new Vector2(1.0f, 0.8f));
            SpawnShadow("Shadow_DeepStorage_Crate_Stack_2", new Vector3(-13.5f, -51.80f, 0f), $"{PropsSourceDir}/Shadow_SubLevel_Medium.png", 0.50f);

            // Motorized Crane Hoist Trolley (Wide 2-tile bridge spanning across both rail tracks) + shadow
            SpawnProp("Prop_DeepStorage_Crane_Trolley", new Vector3(-13.5f, -46.5f, 0f), $"{PropsSourceDir}/Prop_DeepStorage_Crane_Trolley.png", true, new Vector2(2.0f, 0.8f));
            SpawnShadow("Shadow_DeepStorage_Crane_Trolley", new Vector3(-13.5f, -46.90f, 0f), $"{PropsSourceDir}/Shadow_SubLevel_Large.png", 0.55f);

            // Isotope Fuel Rod Storage Rack + shadow
            SpawnProp("Prop_DeepStorage_Fuel_Rack", new Vector3(-10.5f, -48.0f, 0f), $"{PropsSourceDir}/Prop_DeepStorage_Fuel_Rack.png", true, new Vector2(0.9f, 0.9f));
            SpawnShadow("Shadow_DeepStorage_Fuel_Rack", new Vector3(-10.5f, -48.40f, 0f), $"{PropsSourceDir}/Shadow_SubLevel_Medium.png", 0.55f);

            // Heavy Pallet Stack + shadow
            SpawnProp("Prop_DeepStorage_Pallet_Stack", new Vector3(-17.5f, -48.0f, 0f), $"{PropsSourceDir}/Prop_DeepStorage_Pallet_Stack.png", true, new Vector2(0.9f, 0.9f));
            SpawnShadow("Shadow_DeepStorage_Pallet_Stack", new Vector3(-17.5f, -48.40f, 0f), $"{PropsSourceDir}/Shadow_SubLevel_Medium.png", 0.50f);

            // Industrial Floor Cables decal
            SpawnProp("Decal_DeepStorage_Cables", new Vector3(-13.5f, -44.5f, 0f), $"{DecalsSourceDir}/Decal_SubLevel_Floor_Cables.png", false, default, "Floor", 2);

            // Large Radiation Decals
            SpawnProp("Decal_DeepStorage_Radiation", new Vector3(-13.5f, -49.0f, 0f), $"{DecalsSourceDir}/Decal_SubLevel_Radiation_Large.png", false, default, "Floor", 2);

            // Heavy Transition Bulkhead Arch at entrance from West Corridor (Perfect center of corridor opening at Y = -46.0)
            SpawnProp("Prop_Transition_Bulkhead_Arch", new Vector3(-19.0f, -46.0f, 0f), $"{PropsSourceDir}/Prop_Transition_Bulkhead_Arch.png", false, default, "Obstacles", 6);

            // Sign
            SpawnProp("Sign_DeepStorage", new Vector3(-8.8f, -44.5f, 0f), $"{SignageSourceDir}/Sign_SubLevel_DeepStorage.png", false, default, "Obstacles", 4);

            // ==================== POWER RELAY BAY PROPS & SHADOWS ====================
            // 2 Rotary Dynamos + shadows
            SpawnProp("Prop_PowerRelay_Dynamo_1", new Vector3(9.5f, -44.5f, 0f), $"{PropsSourceDir}/Prop_PowerRelay_Rotary_Dynamo.png", true, new Vector2(1.0f, 0.9f));
            SpawnShadow("Shadow_PowerRelay_Dynamo_1", new Vector3(9.5f, -44.85f, 0f), $"{PropsSourceDir}/Shadow_SubLevel_Medium.png", 0.55f);
            SpawnProp("Prop_PowerRelay_Dynamo_2", new Vector3(15.5f, -44.5f, 0f), $"{PropsSourceDir}/Prop_PowerRelay_Rotary_Dynamo.png", true, new Vector2(1.0f, 0.9f));
            SpawnShadow("Shadow_PowerRelay_Dynamo_2", new Vector3(15.5f, -44.85f, 0f), $"{PropsSourceDir}/Shadow_SubLevel_Medium.png", 0.55f);

            // Insulator Banks + shadows
            SpawnProp("Prop_PowerRelay_Insulator_Bank_1", new Vector3(9.5f, -51.5f, 0f), $"{PropsSourceDir}/Prop_PowerRelay_Insulator_Bank.png", true, new Vector2(1.0f, 0.8f));
            SpawnShadow("Shadow_PowerRelay_Insulator_Bank_1", new Vector3(9.5f, -51.80f, 0f), $"{PropsSourceDir}/Shadow_SubLevel_Medium.png", 0.50f);
            SpawnProp("Prop_PowerRelay_Insulator_Bank_2", new Vector3(15.5f, -51.5f, 0f), $"{PropsSourceDir}/Prop_PowerRelay_Insulator_Bank.png", true, new Vector2(1.0f, 0.8f));
            SpawnShadow("Shadow_PowerRelay_Insulator_Bank_2", new Vector3(15.5f, -51.80f, 0f), $"{PropsSourceDir}/Shadow_SubLevel_Medium.png", 0.50f);

            // High Voltage Breaker Cabinet + shadow
            SpawnProp("Prop_PowerRelay_Breaker_Cabinet", new Vector3(12.5f, -44.2f, 0f), $"{PropsSourceDir}/Prop_PowerRelay_Breaker_Cabinet.png", true, new Vector2(1.0f, 0.9f));
            SpawnShadow("Shadow_PowerRelay_Breaker_Cabinet", new Vector3(12.5f, -44.60f, 0f), $"{PropsSourceDir}/Shadow_SubLevel_Medium.png", 0.55f);

            // Central High-Voltage Distribution Switchboard Island + shadow
            SpawnProp("Prop_PowerRelay_Switchboard_Island", new Vector3(12.5f, -48.0f, 0f), $"{PropsSourceDir}/Prop_PowerRelay_Switchboard_Island.png", true, new Vector2(1.8f, 0.8f));
            SpawnShadow("Shadow_PowerRelay_Switchboard_Island", new Vector3(12.5f, -48.45f, 0f), $"{PropsSourceDir}/Shadow_SubLevel_Large.png", 0.55f);

            // Step-Down Oil Transformer (repositioned to West flank) + shadow
            SpawnProp("Prop_PowerRelay_Stepdown_Transformer", new Vector3(10.0f, -48.5f, 0f), $"{PropsSourceDir}/Prop_PowerRelay_Stepdown_Transformer.png", true, new Vector2(1.8f, 0.9f));
            SpawnShadow("Shadow_PowerRelay_Stepdown_Transformer", new Vector3(10.0f, -48.95f, 0f), $"{PropsSourceDir}/Shadow_SubLevel_Large.png", 0.55f);

            // Surge Capacitor Station (replacing old prototype obstacle) + shadow
            SpawnProp("Prop_PowerRelay_Capacitor_Station", new Vector3(16.5f, -44.0f, 0f), $"{PropsSourceDir}/Prop_PowerRelay_Capacitor_Station.png", true, new Vector2(0.9f, 1.8f));
            SpawnShadow("Shadow_PowerRelay_Capacitor_Station", new Vector3(16.5f, -44.80f, 0f), $"{PropsSourceDir}/Shadow_SubLevel_Medium.png", 0.55f);

            // Emergency Battery Bank (replacing old prototype obstacle) + shadow
            SpawnProp("Prop_PowerRelay_Battery_Bank", new Vector3(16.5f, -50.5f, 0f), $"{PropsSourceDir}/Prop_PowerRelay_Battery_Bank.png", true, new Vector2(0.9f, 1.8f));
            SpawnShadow("Shadow_PowerRelay_Battery_Bank", new Vector3(16.5f, -51.30f, 0f), $"{PropsSourceDir}/Shadow_SubLevel_Medium.png", 0.55f);

            // Floor cables & Voltage Decals
            SpawnProp("Decal_PowerRelay_Cables", new Vector3(12.5f, -49.5f, 0f), $"{DecalsSourceDir}/Decal_SubLevel_Floor_Cables.png", false, default, "Floor", 2);
            SpawnProp("Decal_PowerRelay_Voltage", new Vector3(12.5f, -46.5f, 0f), $"{DecalsSourceDir}/Decal_SubLevel_HighVoltage_Lightning.png", false, default, "Floor", 2);

            // Sign
            SpawnProp("Sign_PowerRelay", new Vector3(8.8f, -44.5f, 0f), $"{SignageSourceDir}/Sign_SubLevel_PowerRelay.png", false, default, "Obstacles", 4);

            // ==================== REACTOR CORE PROPS & SHADOWS ====================
            // The 3x3 Titan Lead Monolith Core in center (X: 12.5, Y: -64.5) + Titan base shadow
            var monolithGo = SpawnProp("Prop_ReactorCore_Titan_Monolith", new Vector3(12.5f, -64.5f, 0f), $"{PropsSourceDir}/Prop_ReactorCore_Titan_Monolith.png", true, new Vector2(2.6f, 2.6f), "Obstacles", 5);
            SpawnShadow("Shadow_ReactorCore_Titan_Monolith", new Vector3(12.5f, -65.40f, 0f), $"{PropsSourceDir}/Shadow_SubLevel_Titan.png", 0.65f);

            // Twin Coolant Towers + large shadows
            SpawnProp("Prop_ReactorCore_Coolant_Tower_Left", new Vector3(8.5f, -64.5f, 0f), $"{PropsSourceDir}/Prop_ReactorCore_Coolant_Tower_Left.png", true, new Vector2(0.9f, 1.8f));
            SpawnShadow("Shadow_ReactorCore_Coolant_Tower_Left", new Vector3(8.5f, -65.20f, 0f), $"{PropsSourceDir}/Shadow_SubLevel_Large.png", 0.60f);

            SpawnProp("Prop_ReactorCore_Coolant_Tower_Right", new Vector3(16.5f, -64.5f, 0f), $"{PropsSourceDir}/Prop_ReactorCore_Coolant_Tower_Right.png", true, new Vector2(0.9f, 1.8f));
            SpawnShadow("Shadow_ReactorCore_Coolant_Tower_Right", new Vector3(16.5f, -65.20f, 0f), $"{PropsSourceDir}/Shadow_SubLevel_Large.png", 0.60f);

            // Insulated Cryogenic Coolant Pipes bridging coolant towers directly to the Monolith Core
            SpawnProp("Prop_ReactorCore_Coolant_Pipe_Left", new Vector3(9.85f, -64.5f, 0f), $"{PropsSourceDir}/Prop_ReactorCore_Coolant_Pipe.png", true, new Vector2(1.8f, 0.4f), "Obstacles", 4);
            SpawnProp("Prop_ReactorCore_Coolant_Pipe_Right", new Vector3(15.15f, -64.5f, 0f), $"{PropsSourceDir}/Prop_ReactorCore_Coolant_Pipe.png", true, new Vector2(1.8f, 0.4f), "Obstacles", 4);

            // Sagging Hazard Warning Barrier Chains between Perimeter Stanchions
            SpawnProp("Prop_ReactorCore_Chain_North_L", new Vector3(11.0f, -62.0f, 0f), $"{PropsSourceDir}/Prop_ReactorCore_Hazard_Chain_H.png", false, default, "Obstacles", 5);
            SpawnProp("Prop_ReactorCore_Chain_North_R", new Vector3(14.0f, -62.0f, 0f), $"{PropsSourceDir}/Prop_ReactorCore_Hazard_Chain_H.png", false, default, "Obstacles", 5);
            SpawnProp("Prop_ReactorCore_Chain_South_L", new Vector3(11.0f, -66.5f, 0f), $"{PropsSourceDir}/Prop_ReactorCore_Hazard_Chain_H.png", false, default, "Obstacles", 5);
            SpawnProp("Prop_ReactorCore_Chain_South_R", new Vector3(14.0f, -66.5f, 0f), $"{PropsSourceDir}/Prop_ReactorCore_Hazard_Chain_H.png", false, default, "Obstacles", 5);

            // Master Activation Console in front of Reactor + shadow
            var consoleGo = SpawnProp("Prop_ReactorCore_Master_Console", new Vector3(12.5f, -67.5f, 0f), $"{PropsSourceDir}/Prop_ReactorCore_Control_Console.png", true, new Vector2(1.0f, 0.9f));
            SpawnShadow("Shadow_ReactorCore_Master_Console", new Vector3(12.5f, -67.85f, 0f), $"{PropsSourceDir}/Shadow_SubLevel_Medium.png", 0.55f);
            if (consoleGo != null)
            {
                int interactableLayer = LayerMask.NameToLayer("Interactable");
                if (interactableLayer != -1) consoleGo.layer = interactableLayer;

                var rcc = consoleGo.AddComponent<ReactorCoreConsole>();
                var eventChannel = AssetDatabase.LoadAssetAtPath<ScriptableObject>("Assets/_Project/Scripts/4_Data/Events/OnGameSessionStateChanged.asset");
                if (eventChannel != null)
                {
                    var serializedRcc = new SerializedObject(rcc);
                    var prop = serializedRcc.FindProperty("onGameSessionStateChanged");
                    if (prop != null)
                    {
                        prop.objectReferenceValue = eventChannel;
                        serializedRcc.ApplyModifiedProperties();
                    }
                }
                var oldRcc = GameObject.Find("TaskLoc_ReactorCore")?.GetComponent<ReactorCoreConsole>();
                if (oldRcc != null) Undo.DestroyObjectImmediate(oldRcc);
            }

            // Mobile Radiation Lead Glass Shield + shadow
            SpawnProp("Prop_ReactorCore_Radiation_Shield", new Vector3(10.5f, -67.5f, 0f), $"{PropsSourceDir}/Prop_ReactorCore_Radiation_Shield.png", true, new Vector2(0.9f, 0.9f));
            SpawnShadow("Shadow_ReactorCore_Radiation_Shield", new Vector3(10.5f, -67.85f, 0f), $"{PropsSourceDir}/Shadow_SubLevel_Medium.png", 0.50f);

            // Wall-mounted Emergency Bypass Valve
            SpawnProp("Prop_ReactorCore_Bypass_Valve", new Vector3(6.6f, -64.5f, 0f), $"{PropsSourceDir}/Prop_ReactorCore_Bypass_Valve.png", true, new Vector2(0.8f, 0.8f));

            // Radiation Hazard Perimeter Stanchions
            SpawnProp("Prop_ReactorCore_Stanchion_1", new Vector3(9.5f, -62.0f, 0f), $"{PropsSourceDir}/Prop_ReactorCore_Perimeter_Stanchion.png", true, new Vector2(0.4f, 0.6f));
            SpawnProp("Prop_ReactorCore_Stanchion_2", new Vector3(15.5f, -62.0f, 0f), $"{PropsSourceDir}/Prop_ReactorCore_Perimeter_Stanchion.png", true, new Vector2(0.4f, 0.6f));
            SpawnProp("Prop_ReactorCore_Stanchion_3", new Vector3(9.5f, -66.5f, 0f), $"{PropsSourceDir}/Prop_ReactorCore_Perimeter_Stanchion.png", true, new Vector2(0.4f, 0.6f));
            SpawnProp("Prop_ReactorCore_Stanchion_4", new Vector3(15.5f, -66.5f, 0f), $"{PropsSourceDir}/Prop_ReactorCore_Perimeter_Stanchion.png", true, new Vector2(0.4f, 0.6f));

            // Coolant leak decals under towers
            SpawnProp("Decal_ReactorCore_Leak_L", new Vector3(8.5f, -65.5f, 0f), $"{DecalsSourceDir}/Decal_SubLevel_Coolant_Leak.png", false, default, "Floor", 2);
            SpawnProp("Decal_ReactorCore_Leak_R", new Vector3(16.5f, -65.5f, 0f), $"{DecalsSourceDir}/Decal_SubLevel_Coolant_Leak.png", false, default, "Floor", 2);

            // Sign
            SpawnProp("Sign_ReactorCore", new Vector3(12.5f, -60.5f, 0f), $"{SignageSourceDir}/Sign_SubLevel_ReactorCore.png", false, default, "Obstacles", 4);

            // ==================== TASK LOCATIONS CLEANUP ====================
            void ConfigureTaskLoc(string locName, Vector3 pos)
            {
                var go = GameObject.Find(locName);
                if (go != null)
                {
                    Undo.RecordObject(go.transform, "Align " + locName);
                    go.transform.position = pos;
                    var col = go.GetComponent<BoxCollider2D>();
                    if (col != null)
                    {
                        Undo.RecordObject(col, "Trigger " + locName);
                        col.isTrigger = true;
                        col.size = new Vector2(1.0f, 1.0f);
                        EditorUtility.SetDirty(col);
                    }
                    EditorUtility.SetDirty(go);
                }
            }

            ConfigureTaskLoc("TaskLoc_DeepStorage", new Vector3(-16.5f, -44.5f, 0f));
            ConfigureTaskLoc("TaskLoc_PowerRelayBay", new Vector3(12.5f, -44.2f, 0f));
            ConfigureTaskLoc("TaskLoc_ReactorCore", new Vector3(12.5f, -67.5f, 0f));

            // ==================== DEACTIVATE ALL OBSOLETE PROTOTYPE OBSTACLES ====================
            string[] obsolete = {
                "Obstacle_NodeC_Center",
                "Obstacle_DeepStorage_Rack_East", "Obstacle_DeepStorage_Crate_SW", "Obstacle_DeepStorage_Rack_West",
                "Obstacle_PowerRelay_Conduit_North", "Obstacle_PowerRelay_Conduit_South", "Obstacle_PowerRelay_Transformer_SW",
                "Obstacle_ReactorCore_Conduit_NW", "Obstacle_ReactorCore_CoolantTank_East",
                "Obstacle_ReactorCore_Conduit_NE", "Obstacle_ReactorCore_BasePipe_South", "Obstacle_ReactorCore_CoolantTank_West"
            };
            foreach (var obsName in obsolete)
            {
                var o = GameObject.Find(obsName);
                if (o != null) o.SetActive(false);
            }
        }

        public static void ConfigureSubLevelDoors()
        {
            var doorVClosed = AssetDatabase.LoadAssetAtPath<Sprite>($"{DoorsSourceDir}/Door_SubLevel_Vertical_Closed.png");
            var doorVOpen = AssetDatabase.LoadAssetAtPath<Sprite>($"{DoorsSourceDir}/Door_SubLevel_Vertical_Open.png");
            var doorHClosed = AssetDatabase.LoadAssetAtPath<Sprite>($"{DoorsSourceDir}/Door_SubLevel_Horizontal_Closed.png");
            var doorHOpen = AssetDatabase.LoadAssetAtPath<Sprite>($"{DoorsSourceDir}/Door_SubLevel_Horizontal_Open.png");

            void ConfigureDoor(string doorName, Vector3 pos, bool isHorizontal)
            {
                var dGo = GameObject.Find(doorName);
                if (dGo == null) return;
                Undo.RecordObject(dGo.transform, "Align " + doorName);
                dGo.transform.position = pos;
                dGo.transform.rotation = Quaternion.identity;
                dGo.transform.localScale = Vector3.one;

                var dc = dGo.GetComponent<DoorController>();
                if (dc != null)
                {
                    Undo.RecordObject(dc, "Configure " + doorName);
                    dc.SetSprites(isHorizontal ? doorHClosed : doorVClosed, isHorizontal ? doorHOpen : doorVOpen);
                    EditorUtility.SetDirty(dc);
                }

                var sr = dGo.GetComponent<SpriteRenderer>();
                if (sr != null)
                {
                    Undo.RecordObject(sr, "Configure SR " + doorName);
                    sr.sprite = isHorizontal ? doorHClosed : doorVClosed;
                    sr.sortingLayerName = "Obstacles";
                    sr.sortingOrder = 5;
                    EditorUtility.SetDirty(sr);
                }

                var col = dGo.GetComponent<BoxCollider2D>();
                if (col != null)
                {
                    Undo.RecordObject(col, "Configure Col " + doorName);
                    col.size = isHorizontal ? new Vector2(2.0f, 1.0f) : new Vector2(1.0f, 2.0f);
                    col.offset = Vector2.zero;
                    EditorUtility.SetDirty(col);
                }
            }

            // Door_DeepStorage: Vertical 1x2 at X = -7.5, Y = -46.0 (opening between Y = -46 and -47)
            ConfigureDoor("Door_DeepStorage", new Vector3(-7.5f, -46.0f, 0f), false);

            // Door_PowerRelayBay: Vertical 1x2 at X = 7.5, Y = -46.0 (opening between Y = -46 and -47)
            ConfigureDoor("Door_PowerRelayBay", new Vector3(7.5f, -46.0f, 0f), false);

            // Door_ReactorCore: Horizontal 2x1 at X = 13.0, Y = -58.5 (opening at X in [12, 13])
            ConfigureDoor("Door_ReactorCore", new Vector3(13.0f, -58.5f, 0f), true);
        }

        public static void SetupSubLevelLighting()
        {
            var oldLighting = GameObject.Find("SubLevel_Lighting");
            if (oldLighting != null) Undo.DestroyObjectImmediate(oldLighting);

            GameObject lightingRoot = new GameObject("SubLevel_Lighting");
            Undo.RegisterCreatedObjectUndo(lightingRoot, "Create SubLevel_Lighting");

            var light2DType = typeof(Light2D);
            var updateMeshMethod = light2DType.GetMethod("UpdateMesh", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var updateBoundsMethod = light2DType.GetMethod("UpdateBoundingSphere", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            Light2D AddLight(string name, Vector3 pos, Color color, float intensity, float outerRadius, float innerRadius = 0.1f)
            {
                var go = new GameObject(name);
                go.transform.SetParent(lightingRoot.transform);
                go.transform.position = pos;
                var l = go.AddComponent<Light2D>();
                l.lightType = Light2D.LightType.Point;
                l.color = color;
                l.intensity = intensity;
                l.pointLightOuterRadius = outerRadius;
                l.pointLightInnerRadius = innerRadius;
                l.shadowsEnabled = false;

                updateMeshMethod?.Invoke(l, new object[] { true });
                updateBoundsMethod?.Invoke(l, null);
                EditorUtility.SetDirty(l);
                return l;
            }

            // ==================== NODE C LIGHTING (Amber Warning & Catwalk Grazing) ====================
            AddLight("Light_NodeC_Ambient", new Vector3(-0.5f, -34.5f, 0f), new Color(0.20f, 0.22f, 0.26f, 1f), 0.45f, 12.0f);
            var bL = AddLight("Light_NodeC_Bollard_L", new Vector3(-3.2f, -34.3f, 0f), new Color(1.0f, 0.70f, 0.20f, 1f), 0.75f, 2.8f);
            var bR = AddLight("Light_NodeC_Bollard_R", new Vector3(2.2f, -34.3f, 0f), new Color(1.0f, 0.70f, 0.20f, 1f), 0.75f, 2.8f);
            AddLight("Light_NodeC_Manifold", new Vector3(-4.4f, -34.5f, 0f), new Color(1.0f, 0.55f, 0.20f, 1f), 0.50f, 2.2f);

            // ==================== DEEP STORAGE LIGHTING (Low-key Cyan & Emergency Lamp) ====================
            AddLight("Light_DeepStorage_Ambient", new Vector3(-13.5f, -47.5f, 0f), new Color(0.18f, 0.25f, 0.32f, 1f), 0.55f, 14.0f);
            AddLight("Light_DeepStorage_Vault_1", new Vector3(-17.0f, -50.0f, 0f), new Color(0.20f, 0.85f, 0.95f, 1f), 0.75f, 3.5f);
            AddLight("Light_DeepStorage_Vault_2", new Vector3(-10.0f, -50.0f, 0f), new Color(0.20f, 0.85f, 0.95f, 1f), 0.75f, 3.5f);
            AddLight("Light_DeepStorage_RadSuit", new Vector3(-16.5f, -44.5f, 0f), new Color(1.0f, 0.80f, 0.25f, 1f), 0.65f, 3.0f);
            AddLight("Light_DeepStorage_Pallet_Area", new Vector3(-16.5f, -48.0f, 0f), new Color(1.0f, 0.85f, 0.45f, 1f), 0.65f, 3.2f);

            // ==================== POWER RELAY LIGHTING (Flickering Arc & Warm Orange) ====================
            AddLight("Light_PowerRelay_Ambient", new Vector3(12.5f, -47.5f, 0f), new Color(0.22f, 0.20f, 0.18f, 1f), 0.35f, 14.0f);
            var dynamo1 = AddLight("Light_PowerRelay_Dynamo_1", new Vector3(9.5f, -44.5f, 0f), new Color(1.0f, 0.65f, 0.25f, 1f), 0.85f, 3.8f);
            var dynamo2 = AddLight("Light_PowerRelay_Dynamo_2", new Vector3(15.5f, -44.5f, 0f), new Color(1.0f, 0.65f, 0.25f, 1f), 0.85f, 3.8f);
            AddLight("Light_PowerRelay_Breaker", new Vector3(12.5f, -44.0f, 0f), new Color(1.0f, 0.30f, 0.20f, 1f), 0.70f, 2.8f);
            AddLight("Light_PowerRelay_Insulator_1", new Vector3(9.5f, -51.5f, 0f), new Color(0.25f, 0.90f, 0.75f, 1f), 0.50f, 2.6f);
            AddLight("Light_PowerRelay_Insulator_2", new Vector3(15.5f, -51.5f, 0f), new Color(0.25f, 0.90f, 0.75f, 1f), 0.50f, 2.6f);
            var arcLight = AddLight("Light_PowerRelay_Arc_Flash", new Vector3(10.0f, -48.5f, 0f), new Color(0.40f, 0.85f, 1.0f, 1f), 0.90f, 3.5f);

            // Attach flicker juice to dynamos and transformer arc flash
            dynamo1.gameObject.AddComponent<TheLastSignal.Juice.FlickeringLight>();
            dynamo2.gameObject.AddComponent<TheLastSignal.Juice.FlickeringLight>();
            arcLight.gameObject.AddComponent<TheLastSignal.Juice.FlickeringLight>();

            // ==================== REACTOR CORE LIGHTING (CLIMAX - Ethereal Cerenkov Blue) ====================
            AddLight("Light_ReactorCore_Ambient", new Vector3(12.5f, -64.5f, 0f), new Color(0.08f, 0.15f, 0.28f, 1f), 0.45f, 18.0f);
            
            // The Cerenkov Core Luminary (Blinding Cobalt Cyan at Reactor Center)
            var cerenkovCore = AddLight("Light_ReactorCore_Cerenkov_Spot", new Vector3(12.5f, -64.5f, 0f), new Color(0.0f, 0.85f, 1.0f, 1f), 1.80f, 6.8f, 0.5f);
            cerenkovCore.gameObject.AddComponent<TheLastSignal.Juice.PulsingLight2D>();

            // Coolant Tower Frost Lights
            AddLight("Light_ReactorCore_Tower_L", new Vector3(8.5f, -64.5f, 0f), new Color(0.30f, 0.90f, 1.0f, 1f), 0.60f, 3.5f);
            AddLight("Light_ReactorCore_Tower_R", new Vector3(16.5f, -64.5f, 0f), new Color(0.30f, 0.90f, 1.0f, 1f), 0.60f, 3.5f);

            // Control Console Task Light (Amber Glow on Controls)
            AddLight("Light_ReactorCore_Console_Amber", new Vector3(12.5f, -67.2f, 0f), new Color(1.0f, 0.80f, 0.35f, 1f), 0.75f, 2.2f);
        }

        public static void SetupSubLevelVFX()
        {
            var envRoot = GameObject.Find("SubLevel_Environment");
            if (envRoot == null) return;

            var vfxRoot = new GameObject("VFX");
            vfxRoot.transform.SetParent(envRoot.transform);

            Material steamMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Materials/M_Steam_Particle.mat");
            Material sparksMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Materials/M_Spark_Particle.mat");

            // 1. Steam Purge on Node C Manifold
            if (steamMat != null)
            {
                var steamGo = new GameObject("VFX_NodeC_Steam_Purge");
                steamGo.transform.SetParent(vfxRoot.transform);
                steamGo.transform.position = new Vector3(-4.4f, -34.5f, 0f);
                var ps = steamGo.AddComponent<ParticleSystem>();
                var rend = steamGo.GetComponent<ParticleSystemRenderer>();
                rend.sharedMaterial = steamMat;
                rend.sortingLayerName = "Obstacles";
                rend.sortingOrder = 6;

                var main = ps.main;
                main.maxParticles = 20;
                main.startLifetime = 1.8f;
                main.startSpeed = 0.8f;
                main.startSize = 0.35f;
                main.startColor = new Color(0.85f, 0.92f, 1.0f, 0.35f);

                var emission = ps.emission;
                emission.rateOverTime = 5.0f;

                var shape = ps.shape;
                shape.shapeType = ParticleSystemShapeType.Cone;
                shape.angle = 25f;
                shape.radius = 0.15f;
            }

            // 2. Electrical Sparks on Power Relay Dynamo
            if (sparksMat != null)
            {
                var sparksGo = new GameObject("VFX_PowerRelay_Sparks");
                sparksGo.transform.SetParent(vfxRoot.transform);
                sparksGo.transform.position = new Vector3(9.5f, -44.0f, 0f);
                var ps = sparksGo.AddComponent<ParticleSystem>();
                var rend = sparksGo.GetComponent<ParticleSystemRenderer>();
                rend.sharedMaterial = sparksMat;
                rend.sortingLayerName = "Obstacles";
                rend.sortingOrder = 6;

                var main = ps.main;
                main.maxParticles = 12;
                main.startLifetime = 0.30f;
                main.startSpeed = 1.5f;
                main.startSize = 0.06f;
                main.gravityModifier = 1.0f;
                main.startColor = new Color(1.0f, 0.75f, 0.25f, 1.0f);

                var emission = ps.emission;
                emission.rateOverTime = 0f;
                emission.SetBursts(new ParticleSystem.Burst[] {
                    new ParticleSystem.Burst(0.4f, 2, 5, 1, 2.5f)
                });
            }
        }
    }
}
#endif
