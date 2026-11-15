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

            // Xenobiology Research Laboratory Custom Tiles
            Tile fLabEpoxy = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/SubLevel_Floor_Lab_Epoxy.asset") ?? fTread;
            Tile fLabEpoxyVar = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/SubLevel_Floor_Lab_Epoxy_Variant.asset") ?? fLabEpoxy;
            Tile fDeckSplitV = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/SubLevel_Floor_Lab_Deck_Split_V.asset") ?? fLabEpoxy;
            Tile fDeckSplitH = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/SubLevel_Floor_Lab_Deck_Split_H.asset") ?? fLabEpoxy;
            Tile fCryoPanel = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/SubLevel_Floor_Cryo_Panel.asset") ?? fLabEpoxy;
            Tile fXenoGrate = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/SubLevel_Floor_Xeno_DeconGrate.asset") ?? fLabEpoxy;
            Tile fLabZone = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/SubLevel_Floor_Lab_Zone_Stripe.asset") ?? fLabEpoxy;
            Tile fXenoClean = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/SubLevel_Floor_Xeno_Cleanroom.asset") ?? fLabEpoxy;
            Tile fXenoHaz = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/SubLevel_Floor_Xeno_BioHazard.asset") ?? fTransStripe;
            Tile fLabSticky = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/SubLevel_Floor_Lab_Sticky_Mat.asset") ?? fLabZone;
            Tile fLabMetric = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/SubLevel_Floor_Lab_Metric_Grid.asset") ?? fLabEpoxy;
            Tile fLabHatch = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/SubLevel_Floor_Lab_Utility_Hatch.asset") ?? fLabEpoxy;
            Tile wXenoH = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/SubLevel_Wall_Xeno_Bulkhead_H.asset") ?? wWallH;
            Tile wXenoV = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/SubLevel_Wall_Xeno_Bulkhead_V.asset") ?? wWallV;
            Tile wXenoEast = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/SubLevel_Wall_East_Bulkhead.asset") ?? wXenoV;
            Tile wXenoSouth = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/SubLevel_Wall_South_Bulkhead.asset") ?? wXenoH;
            Tile wXenoGelPipe = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/SubLevel_Wall_Xeno_GelPipe.asset") ?? wXenoH;
            Tile wXenoWinL = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/SubLevel_Wall_Xeno_Observation_Window_L.asset") ?? wXenoH;
            Tile wXenoWinM = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/SubLevel_Wall_Xeno_Observation_Window_M.asset") ?? wXenoH;
            Tile wXenoWinR = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/SubLevel_Wall_Xeno_Observation_Window_R.asset") ?? wXenoH;
            Tile wXenoHepa = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/SubLevel_Wall_Xeno_HEPA_Vent.asset") ?? wXenoH;
            Tile wXenoBay = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/SubLevel_Wall_Xeno_Recessed_Bay.asset") ?? wXenoH;
            Tile wXenoHvac = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/SubLevel_Wall_Xeno_Vent_HVAC.asset") ?? wXenoHepa;
            Tile wXenoTL = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/SubLevel_Wall_Xeno_Corner_TL.asset") ?? wCornerTL;
            Tile wXenoTR = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/SubLevel_Wall_Xeno_Corner_TR.asset") ?? wCornerTR;
            Tile wXenoBL = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/SubLevel_Wall_Xeno_Corner_BL.asset") ?? wCornerBL;
            Tile wXenoBR = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/SubLevel_Wall_Xeno_Corner_BR.asset") ?? wCornerBR;

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
            // 2. NODE C: THE ABYSSAL CATWALK NEXUS (21-TILE MEGA SPINE: Y in [-45..-25])
            // Delegated to NodeCOverhaulApplier to guarantee the authoritative 21-tile continuous catwalk spine
            // ========================================================
            NodeCOverhaulApplier.ApplyOverhaul();

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
            // 5. DEEP STORAGE: CRYOGENIC COLD STORAGE VAULT (-185°C)
            // (X: [-18..-9], Y: [-52..-43]) - 10x10
            // Complete Sub-Zero Freezer: Insulated floor panels, heavy permafrost ice rime,
            // liquid nitrogen drainage grating trenches, insulated bulkhead walls with icicles.
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

            // ========================================================
            // 5. DEEP STORAGE: XENOBIOLOGY RESEARCH LABORATORY (X: [-18..-9], Y: [-52..-43]) - 10x10
            // Non-Repetitive Modular Spacecraft Deck Plates with Natural Engineering & Layered Bulkheads
            // ========================================================
            // 1. Base Layer: Large Monolithic Steel Deck Plates (visually calm, spacious)
            for (int x = -18; x <= -9; x++)
            {
                for (int y = -52; y <= -43; y++)
                {
                    floorTm.SetTile(new Vector3Int(x, y, 0), fLabEpoxy);
                }
            }

            // 2. Controlled Panel Variation (Breaks up rectangular repetition with believable interlocking plates)
            // Vertical split runner plates (X-varying structural joins)
            floorTm.SetTile(new Vector3Int(-18, -43, 0), fDeckSplitV);
            floorTm.SetTile(new Vector3Int(-10, -43, 0), fDeckSplitV);
            floorTm.SetTile(new Vector3Int(-14, -44, 0), fDeckSplitV);
            floorTm.SetTile(new Vector3Int(-16, -48, 0), fDeckSplitV);
            floorTm.SetTile(new Vector3Int(-18, -51, 0), fDeckSplitV);
            floorTm.SetTile(new Vector3Int(-11, -52, 0), fDeckSplitV);

            // Horizontal split plates (Y-varying reinforcement joins)
            floorTm.SetTile(new Vector3Int(-15, -43, 0), fDeckSplitH);
            floorTm.SetTile(new Vector3Int(-9, -44, 0), fDeckSplitH);
            floorTm.SetTile(new Vector3Int(-12, -45, 0), fDeckSplitH);
            floorTm.SetTile(new Vector3Int(-13, -49, 0), fDeckSplitH);
            floorTm.SetTile(new Vector3Int(-11, -51, 0), fDeckSplitH);
            floorTm.SetTile(new Vector3Int(-17, -52, 0), fDeckSplitH);

            // Worn / Patina plates (high-traffic footpaths and machine perimeters)
            floorTm.SetTile(new Vector3Int(-12, -43, 0), fLabEpoxyVar);
            floorTm.SetTile(new Vector3Int(-16, -44, 0), fLabEpoxyVar);
            floorTm.SetTile(new Vector3Int(-14, -45, 0), fLabEpoxyVar);
            floorTm.SetTile(new Vector3Int(-15, -49, 0), fLabEpoxyVar);
            floorTm.SetTile(new Vector3Int(-12, -50, 0), fLabEpoxyVar);
            floorTm.SetTile(new Vector3Int(-14, -52, 0), fLabEpoxyVar);

            // Maintenance Access Hatches
            floorTm.SetTile(new Vector3Int(-11, -44, 0), fLabHatch);
            floorTm.SetTile(new Vector3Int(-16, -51, 0), fLabHatch);

            // 3. Functional Zone: Workstation Area (X in [-15..-12], Y in [-48..-46])
            // Reinforced subfloor plates with precision metric coordinate grid & heavy anchor bolts
            for (int x = -15; x <= -12; x++)
            {
                for (int y = -48; y <= -46; y++)
                {
                    floorTm.SetTile(new Vector3Int(x, y, 0), fLabMetric);
                }
            }

            // 4. Functional Zone: West Specimen Containment Area (X in [-18..-17], Y in [-50..-44] excluding doorway)
            // Cryogenic insulated deck slabs with thermal expansion rubber gaskets
            for (int x = -18; x <= -17; x++)
            {
                for (int y = -50; y <= -44; y++)
                {
                    if (y == -46 || y == -47) continue; // Doorway transit lane remains clear
                    floorTm.SetTile(new Vector3Int(x, y, 0), fCryoPanel);
                }
            }

            // 5. Functional Zone: East Mega Containment Area (X in [-10..-9], Y in [-51..-48])
            // Cryo apron bounded by restrained dark amber hazard demarcation line
            for (int x = -10; x <= -9; x++)
            {
                for (int y = -51; y <= -48; y++)
                {
                    floorTm.SetTile(new Vector3Int(x, y, 0), fCryoPanel);
                }
            }
            floorTm.SetTile(new Vector3Int(-11, -50, 0), fXenoHaz);
            floorTm.SetTile(new Vector3Int(-11, -49, 0), fXenoHaz);

            // 6. Functional Zone: Machinery & Decontamination Drainage
            floorTm.SetTile(new Vector3Int(-14, -51, 0), fXenoGrate);
            floorTm.SetTile(new Vector3Int(-13, -51, 0), fXenoGrate);

            // 7. North Wall Bulkheads (Y = -42, X in [-18..-9]) - Upper Wall Architecture:
            // Large reinforced armor panels with upper utility tray & recessed equipment alcoves
            for (int x = -18; x <= -9; x++)
            {
                Tile wt = wXenoH;
                if (x == -17 || x == -16) wt = wXenoBay;
                else if (x == -15 || x == -14 || x == -13) wt = wXenoH;
                else if (x == -12) wt = wXenoHvac;
                else if (x == -11) wt = wXenoH;
                else if (x == -10 || x == -9) wt = wXenoBay;
                wallTm.SetTile(new Vector3Int(x, -42, 0), wt);
            }

            // 8. South Wall Bulkheads (Y = -53, X in [-18..-9]) - Lower Wall Architecture:
            // Heavy base structures with integrated mechanical service panels
            for (int x = -18; x <= -9; x++)
            {
                Tile wt = wXenoSouth;
                if (x == -15) wt = wXenoHvac;
                else if (x == -14 || x == -13) wt = wXenoBay;
                else if (x == -11) wt = wXenoHvac;
                wallTm.SetTile(new Vector3Int(x, -53, 0), wt);
            }

            // 9. West Wall Bulkheads (X = -19, Opening at Y in [-47, -46] with Sticky Decon Mat) - Left Wall Architecture:
            // Stronger vertical structural columns & power infeed conduit runs
            for (int y = -52; y <= -43; y++)
            {
                if (y == -46 || y == -47)
                {
                    wallTm.SetTile(new Vector3Int(-19, y, 0), null);
                    floorTm.SetTile(new Vector3Int(-19, y, 0), fLabSticky);
                }
                else
                {
                    wallTm.SetTile(new Vector3Int(-19, y, 0), wXenoV);
                }
            }

            // 10. East Wall Bulkheads (X = -8, Opening at Y in [-47, -46] with Sticky Decon Mat) - Right Wall Architecture:
            // Deep reinforced bulkhead construction backing Mega Containment Chamber
            for (int y = -52; y <= -43; y++)
            {
                if (y == -46 || y == -47)
                {
                    wallTm.SetTile(new Vector3Int(-8, y, 0), null);
                    floorTm.SetTile(new Vector3Int(-8, y, 0), fLabSticky);
                }
                else
                {
                    wallTm.SetTile(new Vector3Int(-8, y, 0), wXenoEast);
                }
            }
            // Seamless insulated wall transition at X = -7
            wallTm.SetTile(new Vector3Int(-7, -45, 0), wXenoH);
            wallTm.SetTile(new Vector3Int(-7, -48, 0), wXenoH);
            floorTm.SetTile(new Vector3Int(-7, -46, 0), fLabSticky);
            floorTm.SetTile(new Vector3Int(-7, -47, 0), fLabSticky);

            // Bio-Containment Vault Corners
            wallTm.SetTile(new Vector3Int(-19, -42, 0), wXenoTL);
            wallTm.SetTile(new Vector3Int(-8, -42, 0), wXenoTR);
            wallTm.SetTile(new Vector3Int(-19, -53, 0), wXenoBL);
            wallTm.SetTile(new Vector3Int(-8, -53, 0), wXenoBR);

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

            Material litMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/M_Sprite_Lit_PixelSmooth.mat")
                ?? AssetDatabase.LoadAssetAtPath<Material>("Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Lit-Default.mat");
            if (litMat == null)
            {
                Shader s = Shader.Find("Universal Render Pipeline/2D/Sprite-Lit-Default");
                if (s != null) litMat = new Material(s);
            }

            GameObject SpawnProp(string name, Vector3 pos, string spritePath, bool addCol = true, Vector2 colSize = default, string sortingLayer = "Obstacles", int order = 5, Vector2 colOffset = default)
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
                    if (colOffset != default) col.offset = colOffset;
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

            GameObject SpawnWallDropShadowV(string name, Vector3 pos, bool flipX = false)
            {
                var sp = AssetDatabase.LoadAssetAtPath<Sprite>($"{TilesSourceDir}/SubLevel_Wall_DropShadow_V.png");
                if (sp == null) return null;
                var go = new GameObject(name);
                go.transform.SetParent(propsRoot.transform);
                go.transform.position = pos;
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = sp;
                sr.flipX = flipX;
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
            // Deep Storage South Wall (X: [-18..-9], wall top = -53.0f -> Y = -52.75f)
            for (int x = -18; x <= -9; x++)
            {
                var s = SpawnWallDropShadow($"Shadow_Wall_DeepStorage_S_{x}", new Vector3(x + 0.5f, -52.75f, 0f));
                if (s != null) s.GetComponent<SpriteRenderer>().flipY = true;
            }
            // Deep Storage West Wall (X = -18.75f, Y in [-52..-43] excluding doorway at -47, -46)
            for (int y = -52; y <= -43; y++)
            {
                if (y == -46 || y == -47) continue;
                SpawnWallDropShadowV($"Shadow_Wall_DeepStorage_W_{y}", new Vector3(-18.75f, y + 0.5f, 0f));
            }
            // Deep Storage East Wall (X = -8.25f, Y in [-52..-43] excluding doorway at -47, -46)
            for (int y = -52; y <= -43; y++)
            {
                if (y == -46 || y == -47) continue;
                SpawnWallDropShadowV($"Shadow_Wall_DeepStorage_E_{y}", new Vector3(-8.25f, y + 0.5f, 0f), flipX: true);
            }
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
            // 4 Massive Abyssal Support Girders anchored directly under the crossroads junction
            var sNW = SpawnProp("Prop_NodeC_Abyssal_Strut_NW", new Vector3(-1.2f, -32.5f, 0f), $"{PropsSourceDir}/Prop_NodeC_Abyssal_Strut.png", false, default, "Floor", 1);
            var sNE = SpawnProp("Prop_NodeC_Abyssal_Strut_NE", new Vector3(1.2f, -32.5f, 0f), $"{PropsSourceDir}/Prop_NodeC_Abyssal_Strut.png", false, default, "Floor", 1);
            var sSW = SpawnProp("Prop_NodeC_Abyssal_Strut_SW", new Vector3(-1.2f, -35.5f, 0f), $"{PropsSourceDir}/Prop_NodeC_Abyssal_Strut.png", false, default, "Floor", 1);
            var sSE = SpawnProp("Prop_NodeC_Abyssal_Strut_SE", new Vector3(1.2f, -35.5f, 0f), $"{PropsSourceDir}/Prop_NodeC_Abyssal_Strut.png", false, default, "Floor", 1);
            Color strutTint = new Color(0.40f, 0.48f, 0.58f, 0.90f);
            if (sNW != null) { sNW.GetComponent<SpriteRenderer>().color = strutTint; sNW.transform.localScale = new Vector3(1.15f, 1.35f, 1.0f); }
            if (sNE != null) { sNE.GetComponent<SpriteRenderer>().color = strutTint; sNE.transform.localScale = new Vector3(1.15f, 1.35f, 1.0f); }
            if (sSW != null) { sSW.GetComponent<SpriteRenderer>().color = strutTint; sSW.transform.localScale = new Vector3(1.15f, 1.35f, 1.0f); }
            if (sSE != null) { sSE.GetComponent<SpriteRenderer>().color = strutTint; sSE.transform.localScale = new Vector3(1.15f, 1.35f, 1.0f); }

            // Industrial Safety Guardrails strictly at Catwalk perimeters bordering the Abyss
            // North Spur: Left rail at X = -1.0, Right rail at X = 1.0
            for (int y = -33; y <= -31; y++)
            {
                SpawnProp($"Prop_NodeC_Guardrail_North_W_{y}", new Vector3(-1.0f, y + 0.5f, 0f), $"{PropsSourceDir}/Prop_NodeC_Guardrail_V.png", true, new Vector2(0.2f, 1.0f));
                SpawnProp($"Prop_NodeC_Guardrail_North_E_{y}", new Vector3(1.0f, y + 0.5f, 0f), $"{PropsSourceDir}/Prop_NodeC_Guardrail_V.png", true, new Vector2(0.2f, 1.0f));
            }
            // South Spur: Left rail at X = -1.0, Right rail at X = 1.0
            for (int y = -38; y <= -36; y++)
            {
                SpawnProp($"Prop_NodeC_Guardrail_South_W_{y}", new Vector3(-1.0f, y + 0.5f, 0f), $"{PropsSourceDir}/Prop_NodeC_Guardrail_V.png", true, new Vector2(0.2f, 1.0f));
                SpawnProp($"Prop_NodeC_Guardrail_South_E_{y}", new Vector3(1.0f, y + 0.5f, 0f), $"{PropsSourceDir}/Prop_NodeC_Guardrail_V.png", true, new Vector2(0.2f, 1.0f));
            }
            // West Dead-End Console Spur: North rail at Y = -33.0, South rail at Y = -35.0, End cap at X = -3.0
            for (int x = -3; x <= -2; x++)
            {
                SpawnProp($"Prop_NodeC_Guardrail_West_N_{x}", new Vector3(x + 0.5f, -33.0f, 0f), $"{PropsSourceDir}/Prop_NodeC_Guardrail_H.png", true, new Vector2(1.0f, 0.2f));
                SpawnProp($"Prop_NodeC_Guardrail_West_S_{x}", new Vector3(x + 0.5f, -35.0f, 0f), $"{PropsSourceDir}/Prop_NodeC_Guardrail_H.png", true, new Vector2(1.0f, 0.2f));
            }
            SpawnProp("Prop_NodeC_Guardrail_West_Cap", new Vector3(-3.0f, -34.0f, 0f), $"{PropsSourceDir}/Prop_NodeC_Guardrail_V.png", true, new Vector2(0.2f, 1.0f));
            SpawnProp("Prop_NodeC_Guardrail_West_Cap_2", new Vector3(-3.0f, -34.8f, 0f), $"{PropsSourceDir}/Prop_NodeC_Guardrail_V.png", true, new Vector2(0.2f, 1.0f));

            // East Spur: North rail at Y = -33.0, South rail at Y = -35.0
            for (int x = 1; x <= 3; x++)
            {
                SpawnProp($"Prop_NodeC_Guardrail_East_N_{x}", new Vector3(x + 0.5f, -33.0f, 0f), $"{PropsSourceDir}/Prop_NodeC_Guardrail_H.png", true, new Vector2(1.0f, 0.2f));
                SpawnProp($"Prop_NodeC_Guardrail_East_S_{x}", new Vector3(x + 0.5f, -35.0f, 0f), $"{PropsSourceDir}/Prop_NodeC_Guardrail_H.png", true, new Vector2(1.0f, 0.2f));
            }

            // Stanchion Posts at the 8 key geometric vertices
            SpawnProp("Prop_NodeC_Stanchion_0", new Vector3(-1.0f, -33.0f, 0f), $"{PropsSourceDir}/Prop_NodeC_Stanchion_Post.png", true, new Vector2(0.4f, 0.4f), "Obstacles", 7);
            SpawnProp("Prop_NodeC_Stanchion_1", new Vector3(1.0f, -33.0f, 0f), $"{PropsSourceDir}/Prop_NodeC_Stanchion_Post.png", true, new Vector2(0.4f, 0.4f), "Obstacles", 7);
            SpawnProp("Prop_NodeC_Stanchion_2", new Vector3(-1.0f, -35.0f, 0f), $"{PropsSourceDir}/Prop_NodeC_Stanchion_Post.png", true, new Vector2(0.4f, 0.4f), "Obstacles", 7);
            SpawnProp("Prop_NodeC_Stanchion_3", new Vector3(1.0f, -35.0f, 0f), $"{PropsSourceDir}/Prop_NodeC_Stanchion_Post.png", true, new Vector2(0.4f, 0.4f), "Obstacles", 7);
            SpawnProp("Prop_NodeC_Stanchion_4", new Vector3(-3.0f, -33.0f, 0f), $"{PropsSourceDir}/Prop_NodeC_Stanchion_Post.png", true, new Vector2(0.4f, 0.4f), "Obstacles", 7);
            SpawnProp("Prop_NodeC_Stanchion_5", new Vector3(-3.0f, -35.0f, 0f), $"{PropsSourceDir}/Prop_NodeC_Stanchion_Post.png", true, new Vector2(0.4f, 0.4f), "Obstacles", 7);
            SpawnProp("Prop_NodeC_Stanchion_6", new Vector3(3.9f, -33.0f, 0f), $"{PropsSourceDir}/Prop_NodeC_Stanchion_Post.png", true, new Vector2(0.4f, 0.4f), "Obstacles", 7);
            SpawnProp("Prop_NodeC_Stanchion_7", new Vector3(3.9f, -35.0f, 0f), $"{PropsSourceDir}/Prop_NodeC_Stanchion_Post.png", true, new Vector2(0.4f, 0.4f), "Obstacles", 7);

            // Steam Manifold on West Railing
            SpawnProp("Prop_NodeC_Steam_Manifold", new Vector3(-2.65f, -33.45f, 0f), $"{PropsSourceDir}/Prop_NodeC_HighPressure_Manifold.png", true, new Vector2(1.0f, 0.8f));

            // Note: Node C props, guardrails, and floodlights are managed authority-wide by NodeCOverhaulApplier.

            // ==================== DEEP STORAGE: OFFICIAL CONCEPT RECONSTRUCTION ====================
            // --- 1. NORTH WALL: SKIRTING, CRYO-VAULT, OBSERVATION WINDOW, AIR-SCRUBBER, CRYO-DECON ---
            // Continuous Heavy Steel Skirting & Perforated Cable Conduit Trays along North Wall
            SpawnProp("Decal_Lab_Concept_Wall_Cable_Ducts_North", new Vector3(-13.5f, -42.45f, 0f), $"{DecalsSourceDir}/Decal_Lab_Concept_Wall_Cable_Ducts_North.png", false, default, "Floor", 3);

            // North-West: CRYO-VAULT Station (Standing on floor flush against North Wall, top at -42.0)
            SpawnProp("Prop_Lab_Concept_Cryo_Vault_Station", new Vector3(-16.5f, -42.95f, 0f), $"{PropsSourceDir}/Prop_Lab_Concept_Cryo_Vault_Station.png", true, new Vector2(1.5f, 0.7f), "Obstacles", 5, new Vector2(0f, 0.1f));
            SpawnShadow("Shadow_Lab_Concept_Cryo_Vault", new Vector3(-16.5f, -43.75f, 0f), $"{PropsSourceDir}/Shadow_SubLevel_Medium.png", 0.55f);

            // North-Center: Observation Window Frame (Mounted seamlessly inside North Wall row Y = -42)
            SpawnProp("Prop_Lab_Concept_Observation_Window", new Vector3(-13.5f, -41.5f, 0f), $"{PropsSourceDir}/Prop_Lab_Concept_Observation_Window.png", false, default, "Obstacles", 4);

            // North Wall Technical Markings & Hazard Chevrons (Bulkhead Decal)
            SpawnProp("Decal_Lab_Concept_Wall_Stencils_North", new Vector3(-14.5f, -41.90f, 0f), $"{DecalsSourceDir}/Decal_Lab_Concept_Wall_Stencils.png", false, default, "Obstacles", 6);

            // North Air-Scrubber / Inset HEPA Louver (Flush-mounted between Window and Decon Station)
            SpawnProp("Prop_Lab_Concept_Wall_Air_Scrubber", new Vector3(-11.10f, -42.15f, 0f), $"{PropsSourceDir}/Prop_Lab_Concept_Wall_Air_Scrubber.png", false, default, "Obstacles", 5);

            // North-East: CRYO-DECON Station (Standing on floor flush against North Wall, top at -42.0)
            SpawnProp("Prop_Lab_Concept_Cryo_Decon_Station", new Vector3(-9.5f, -42.95f, 0f), $"{PropsSourceDir}/Prop_Lab_Concept_Cryo_Decon_Station.png", true, new Vector2(1.0f, 0.7f), "Obstacles", 5, new Vector2(0f, 0.1f));
            SpawnShadow("Shadow_Lab_Concept_Cryo_Decon", new Vector3(-9.5f, -43.75f, 0f), $"{PropsSourceDir}/Shadow_SubLevel_Small.png", 0.55f);

            // --- 2. WEST BAY: SKIRTING, BREAKER SWITCHBOARD, AIRLOCK PIPES & DUAL STASIS PODS ---
            // Continuous Heavy Steel Skirting & Vertical Cable Ducts along West Wall
            SpawnProp("Decal_Lab_Concept_Wall_Cable_Ducts_West", new Vector3(-18.45f, -47.50f, 0f), $"{DecalsSourceDir}/Decal_Lab_Concept_Wall_Cable_Ducts_West.png", false, default, "Floor", 3);

            // High-Voltage Emergency Breaker Switchboard (Replacing disconnected boxes, flush on West Wall above doorway)
            SpawnProp("Prop_Lab_Concept_Wall_Breaker_Board", new Vector3(-18.30f, -45.60f, 0f), $"{PropsSourceDir}/Prop_Lab_Concept_Wall_Breaker_Board.png", false, default, "Obstacles", 5);
            SpawnShadow("Shadow_Lab_Concept_Wall_Breaker", new Vector3(-18.30f, -46.25f, 0f), $"{PropsSourceDir}/Shadow_SubLevel_Small.png", 0.45f);

            // West Airlock Hydraulic Orange Pipes connecting Dual Pods to Southwest Compressor Plant (Clean on Floor)
            SpawnProp("Prop_Lab_Concept_West_Airlock_Pipes", new Vector3(-17.6f, -50.25f, 0f), $"{PropsSourceDir}/Prop_Lab_Concept_West_Airlock_Pipes.png", false, default, "Floor", 2);

            // West Dual Stasis Pods (Cyan & Teal Stasis Cylinders flush against West Wall at X = -18.0)
            SpawnProp("Prop_Lab_Concept_West_Dual_Pods", new Vector3(-16.75f, -48.5f, 0f), $"{PropsSourceDir}/Prop_Lab_Concept_West_Dual_Pods.png", true, new Vector2(2.0f, 1.0f), "Obstacles", 5, new Vector2(0f, -0.7f));
            SpawnShadow("Shadow_Lab_Concept_West_Dual_Pods", new Vector3(-16.75f, -49.5f, 0f), $"{PropsSourceDir}/Shadow_SubLevel_Large.png", 0.60f);

            // Southwest Refrigeration & Compressor Plant (Standing on floor flush against South Wall at Y = -52.0)
            SpawnProp("Prop_Lab_Concept_Southwest_Compressor_Plant", new Vector3(-16.8f, -51.1f, 0f), $"{PropsSourceDir}/Prop_Lab_Concept_Southwest_Compressor_Plant.png", true, new Vector2(1.6f, 0.8f), "Obstacles", 5, new Vector2(0f, -0.1f));
            SpawnShadow("Shadow_Lab_Concept_SW_Compressor", new Vector3(-16.8f, -51.85f, 0f), $"{PropsSourceDir}/Shadow_SubLevel_Medium.png", 0.55f);

            // --- 3. EAST WALL: SKIRTING, NORTHEAST CHEMICAL STATION & MEGA-CONTAINMENT CHAMBER ---
            // Continuous Heavy Steel Skirting & Vertical Cable Ducts along East Wall
            SpawnProp("Decal_Lab_Concept_Wall_Cable_Ducts_East", new Vector3(-8.55f, -47.50f, 0f), $"{DecalsSourceDir}/Decal_Lab_Concept_Wall_Cable_Ducts_East.png", false, default, "Floor", 3);

            // Northeast Wall Emergency Bio-Neutralizer & First Aid Station (Lấp vùng chết góc Đông Bắc)
            SpawnProp("Prop_Lab_Concept_Wall_Chemical_Cabinet", new Vector3(-8.75f, -43.60f, 0f), $"{PropsSourceDir}/Prop_Lab_Concept_Wall_Chemical_Cabinet.png", false, default, "Obstacles", 5);
            SpawnShadow("Shadow_Lab_Concept_Chemical_Cabinet", new Vector3(-8.75f, -44.25f, 0f), $"{PropsSourceDir}/Shadow_SubLevel_Small.png", 0.45f);

            // High-Risk Alien Containment Chamber (Welded directly into East Wall at X = -9.15, Y = -49.0, clearing Doorway buffer)
            SpawnProp("Prop_Lab_Concept_Mega_Containment_Chamber", new Vector3(-9.15f, -49.0f, 0f), $"{PropsSourceDir}/Prop_Lab_Concept_Mega_Containment_Chamber.png", true, new Vector2(1.8f, 1.1f), "Obstacles", 5, new Vector2(0f, -0.9f));
            SpawnShadow("Shadow_Lab_Concept_Mega_Chamber", new Vector3(-9.15f, -50.2f, 0f), $"{PropsSourceDir}/Shadow_SubLevel_Large.png", 0.65f);

            // Southeast Sub-Zero Archive Bank (Standing on floor flush against South Wall at Y = -52.0)
            SpawnProp("Prop_Lab_Concept_Southeast_Archive_Bank", new Vector3(-10.2f, -51.2f, 0f), $"{PropsSourceDir}/Prop_Lab_Concept_Southeast_Archive_Bank.png", true, new Vector2(1.8f, 0.7f), "Obstacles", 5, new Vector2(0f, -0.15f));
            SpawnShadow("Shadow_Lab_Concept_SE_Archive", new Vector3(-10.2f, -51.85f, 0f), $"{PropsSourceDir}/Shadow_SubLevel_Medium.png", 0.55f);

            // --- 4. SOUTH WALL: SKIRTING, BULKHEAD MONITORING & EYEWASH ---
            // Continuous Heavy Steel Skirting & Coolant Lines along South Wall
            SpawnProp("Decal_Lab_Concept_Wall_Cable_Ducts_South", new Vector3(-13.5f, -52.45f, 0f), $"{DecalsSourceDir}/Decal_Lab_Concept_Wall_Cable_Ducts_South.png", false, default, "Floor", 3);

            // Eyewash Safety Station (Standing on floor flush against South Wall at Y = -52.0)
            SpawnProp("Prop_Lab_Concept_South_Monitor_Eyewash", new Vector3(-13.5f, -51.45f, 0f), $"{PropsSourceDir}/Prop_Lab_Concept_South_Monitor_Eyewash.png", true, new Vector2(1.8f, 0.55f));

            // --- 5. CENTRAL WORKBENCH & ENVIRONMENTAL STORYTELLING ---
            // Diamond-Plate Sub-Floor Platform under Workbench (Seamless beveled isolator deck)
            SpawnProp("Decal_Lab_Concept_Workbench_Border", new Vector3(-13.50f, -47.65f, 0f), $"{DecalsSourceDir}/Decal_Lab_Concept_Workbench_Border.png", false, default, "Floor", 2);

            // Integrated wet-lab workstation with biological microscope, specimen tray, secondary CRT, and grounded operator seating
            SpawnProp("Prop_Lab_Concept_Central_Workbench", new Vector3(-13.5f, -47.5f, 0f), $"{PropsSourceDir}/Prop_Lab_Concept_Central_Workbench.png", true, new Vector2(2.2f, 1.0f), "Obstacles", 5, new Vector2(0f, 0.2f));
            SpawnShadow("Shadow_Lab_Concept_Workbench", new Vector3(-13.5f, -48.35f, 0f), $"{PropsSourceDir}/Shadow_SubLevel_Large.png", 0.60f);

            // Heavy Cast-Iron Recessed Floor Drainage Trench Network (Centered at X = -13.5, Y = -50.6)
            SpawnProp("Decal_Lab_Concept_Floor_Drain_Grate", new Vector3(-13.5f, -50.6f, 0f), $"{DecalsSourceDir}/Decal_Lab_Concept_Floor_Drain_Grate.png", false, default, "Floor", 2);

            // Mega Containment Chamber Biohazard Level 5 Demarcation Zone
            SpawnProp("Decal_Lab_Concept_Mega_Chamber_Hazard", new Vector3(-9.15f, -49.60f, 0f), $"{DecalsSourceDir}/Decal_Lab_Concept_Mega_Chamber_Hazard.png", false, default, "Floor", 2);

            // High-voltage biological conduit connection from wall into Mega Chamber hood
            SpawnProp("Decal_Lab_Apex_BioCables", new Vector3(-9.15f, -47.70f, 0f), $"{DecalsSourceDir}/Decal_Lab_Apex_BioCables.png", false, default, "Floor", 3);

            // Biological residue / corrosive seepage at base drainage lip beneath Mega Chamber
            SpawnProp("Decal_Xeno_Acid_Burn_Spatter", new Vector3(-9.20f, -50.20f, 0f), $"{DecalsSourceDir}/Decal_Xeno_Acid_Burn_Spatter.png", false, default, "Floor", 2);

            // Emergency Egress Walkway Guide & Scattered Research Documents
            SpawnProp("Decal_Lab_Concept_Walkway_Guide", new Vector3(-10.80f, -46.50f, 0f), $"{DecalsSourceDir}/Decal_Lab_Concept_Walkway_Guide.png", false, default, "Floor", 2);

            // Illuminated Biohazard Level 5 Cryo-Vault Warning Sign above entrance doorway threshold
            SpawnProp("Sign_DeepStorage", new Vector3(-7.4f, -45.2f, 0f), $"{SignageSourceDir}/Sign_SubLevel_XenoContainment.png", false, default, "Obstacles", 7);

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
                    var sr = go.GetComponent<SpriteRenderer>();
                    if (sr != null)
                    {
                        Undo.RecordObject(sr, "Hide " + locName + " debug marker");
                        sr.enabled = false;
                        EditorUtility.SetDirty(sr);
                    }
                    EditorUtility.SetDirty(go);
                }
            }

            ConfigureTaskLoc("TaskLoc_DeepStorage", new Vector3(-13.5f, -48.2f, 0f));
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

            // Door_DeepStorage: Vertical 1x2 at X = -7.5, Y = -46.0 (Pneumatic Cryo-Vault Door)
            ConfigureDoor("Door_DeepStorage", new Vector3(-7.5f, -46.0f, 0f), false);
            var dDeep = GameObject.Find("Door_DeepStorage");
            if (dDeep != null)
            {
                var cryoClosed = AssetDatabase.LoadAssetAtPath<Sprite>($"{DoorsSourceDir}/Door_Cryo_Vault_Vertical_Closed.png");
                var cryoOpen = AssetDatabase.LoadAssetAtPath<Sprite>($"{DoorsSourceDir}/Door_Cryo_Vault_Vertical_Open.png");
                var dc = dDeep.GetComponent<DoorController>();
                if (dc != null && cryoClosed != null && cryoOpen != null)
                {
                    dc.SetSprites(cryoClosed, cryoOpen);
                    EditorUtility.SetDirty(dc);
                }
                var sr = dDeep.GetComponent<SpriteRenderer>();
                if (sr != null && cryoClosed != null)
                {
                    sr.sprite = cryoClosed;
                    EditorUtility.SetDirty(sr);
                }
            }

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

            Light2D AddLight(string name, Vector3 pos, Color color, float intensity, float outerRadius, float innerRadius = 0.1f, float falloff = 0.65f)
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
                l.falloffIntensity = falloff;
                l.shadowsEnabled = false;

                updateMeshMethod?.Invoke(l, new object[] { true });
                updateBoundsMethod?.Invoke(l, null);
                EditorUtility.SetDirty(l);
                return l;
            }

            // ==================== NODE C LIGHTING (Abyssal Uplight & Amber Warning) ====================
            AddLight("Light_NodeC_Ambient", new Vector3(0.0f, -34.5f, 0f), new Color(0.18f, 0.20f, 0.24f, 1f), 0.35f, 12.0f);
            var abyssL = AddLight("Light_NodeC_Abyss_Uplight", new Vector3(0.0f, -34.5f, 0f), new Color(0.06f, 0.52f, 0.72f, 1f), 0.70f, 9.5f, 3.0f);
            AddLight("Light_NodeC_Nexus_Overhead", new Vector3(0.0f, -34.5f, 0f), new Color(1.0f, 0.85f, 0.60f, 1f), 0.50f, 4.8f, 1.2f);
            AddLight("Light_NodeC_Bollard_L", new Vector3(-1.4f, -30.3f, 0f), new Color(1.0f, 0.65f, 0.20f, 1f), 0.75f, 2.5f);
            AddLight("Light_NodeC_Bollard_R", new Vector3(1.4f, -38.3f, 0f), new Color(1.0f, 0.65f, 0.20f, 1f), 0.75f, 2.5f);
            AddLight("Light_NodeC_Manifold", new Vector3(-4.4f, -34.5f, 0f), new Color(1.0f, 0.50f, 0.20f, 1f), 0.60f, 2.2f);

            // ==================== XENOBIOLOGY RESEARCH LAB LIGHTING (FINAL POLISHED AMBIENT & HORROR CHIAROSCURO) ====================
            // --- 1. PRIORITY ONE: UPPER AND LOWER AMBIENT ILLUMINATION (Subtle Dark Blue-Gray Field) ---
            // North Ambient Node: Subtly illuminates floor panels above workstation (Y = -44.5 to -43.0), upper wall panels & ventilation
            AddLight("Light_DeepStorage_Ambient_North", new Vector3(-13.50f, -43.80f, 0f), new Color(0.13f, 0.19f, 0.28f, 1f), 0.30f, 9.2f, 2.2f, 0.58f);
            // South Ambient Node: Subtly illuminates lower floor near eyewash (Y = -50.5 to -52.0), lower walls & kickplate transitions
            AddLight("Light_DeepStorage_Ambient_South", new Vector3(-13.50f, -50.50f, 0f), new Color(0.13f, 0.19f, 0.28f, 1f), 0.30f, 9.2f, 2.2f, 0.58f);
            // Central Chase Floor Fill: Calibrated fill for 4-player silhouette separation without a dominant bright spot
            AddLight("Light_DeepStorage_Central_Floor_Fill", new Vector3(-13.50f, -47.25f, 0f), new Color(0.10f, 0.15f, 0.22f, 1f), 0.15f, 5.8f, 1.2f, 0.65f);
            // Overhead Failing Cathode Tube: Clinical flicker over upper corridor
            var cathodeLight = AddLight("Light_DeepStorage_Cathode_Ceiling", new Vector3(-13.50f, -45.50f, 0f), new Color(0.35f, 0.48f, 0.65f, 1f), 0.24f, 4.2f, 0.4f, 0.65f);
            cathodeLight.gameObject.AddComponent<TheLastSignal.Juice.FlickeringLight>();

            // --- 2. PRIORITY THREE: CENTRAL WORKSTATION LIGHT SPILL (Integrated Non-Circular Envelope) ---
            // --- 2. BIOLOGICAL RESEARCH WORKSTATION LIGHTING (Focused Task Illumination) ---
            // Microscope Substage Illuminator (Subtle pale-cyan optical beam focused on stage specimen)
            AddLight("Light_Lab_Workbench_Microscope_Spot", new Vector3(-13.90f, -47.35f, 0f), new Color(0.25f, 0.88f, 0.95f, 1f), 0.40f, 1.4f, 0.15f, 0.70f);
            // Desk Surface Task Light (Refined & focused directly on examination surface, preserving dark surrounding deck)
            AddLight("Light_Lab_Workbench_Spot", new Vector3(-13.50f, -47.35f, 0f), new Color(0.85f, 0.92f, 0.98f, 1f), 0.75f, 1.8f, 0.3f, 0.68f);
            // Secondary Telemetry Screen Bloom (Supporting cyan glow on right diagnostic CRT)
            AddLight("Light_Lab_Workbench_Monitor_Cyan", new Vector3(-12.95f, -47.35f, 0f), new Color(0.20f, 0.75f, 0.90f, 1f), 0.45f, 1.6f, 0.2f, 0.65f);
            // Subfloor Bracket & Operator Stool Illumination (Gentle grounded definition without large circular pool)
            AddLight("Light_Lab_Workbench_Subfloor_Spill", new Vector3(-13.50f, -47.85f, 0f), new Color(0.12f, 0.22f, 0.32f, 1f), 0.20f, 2.2f, 0.3f, 0.70f);
            AddLight("Light_Lab_Workbench_Front_Spill", new Vector3(-13.50f, -46.65f, 0f), new Color(0.10f, 0.20f, 0.30f, 1f), 0.18f, 2.0f, 0.3f, 0.70f);

            // --- 3. CYAN & GREEN REFLECTIONS BEHIND STABLE SPECIMEN TANKS ---
            // Internal Stasis Fluid Glow (Vibrant core inside cylinders)
            AddLight("Light_Lab_West_Pods_Teal", new Vector3(-17.00f, -48.20f, 0f), new Color(0.12f, 0.82f, 0.78f, 1f), 1.05f, 2.6f, 0.4f, 0.60f);
            // Metal Wall Panels Immediately Behind the Two Tanks (Localized cyan specular back-reflection)
            AddLight("Light_Lab_West_Pods_Wall_Reflection", new Vector3(-18.00f, -48.50f, 0f), new Color(0.08f, 0.52f, 0.60f, 1f), 0.36f, 2.0f, 0.2f, 0.65f);
            // Metallic Support Bases Beneath the Tanks & Hydraulic Pipe Mounts
            AddLight("Light_Lab_West_Pods_Base_Reflection", new Vector3(-16.90f, -49.30f, 0f), new Color(0.08f, 0.48f, 0.55f, 1f), 0.32f, 1.8f, 0.2f, 0.68f);
            // Natural Cyan Reflection Gradient on Floor in Front of Tanks (Gradually blends to ambient without flooding west side)
            AddLight("Light_Lab_West_Pods_Floor_Spill", new Vector3(-15.70f, -48.30f, 0f), new Color(0.06f, 0.38f, 0.48f, 1f), 0.28f, 2.8f, 0.3f, 0.72f);
            // Mutating Pod Fault Indicator (Amber alert, unchanged)
            AddLight("Light_Lab_West_Pod_Fault_Amber", new Vector3(-16.80f, -49.20f, 0f), new Color(0.95f, 0.55f, 0.10f, 1f), 0.30f, 1.2f, 0.1f, 0.60f);

            // --- 4. CONTROLLED PURPLE CONTAINMENT LIGHTING (CONTAINED WITHIN MEGA-CHAMBER) ---
            // Internal Biological Core Glow (Apex landmark energy inside chamber)
            AddLight("Light_Lab_Mega_Chamber_Violet", new Vector3(-9.15f, -49.20f, 0f), new Color(0.65f, 0.18f, 0.88f, 1f), 1.25f, 2.4f, 0.5f, 0.60f);
            // Contained Purple Reflection on Base Rim & Immediate Apron (Tightened falloff to prevent room flood)
            AddLight("Light_Lab_Mega_Chamber_Apron_Spill", new Vector3(-9.50f, -49.20f, 0f), new Color(0.35f, 0.12f, 0.52f, 1f), 0.26f, 2.2f, 0.3f, 0.72f);
            // Subtle Plum-Navy Transition Blend
            AddLight("Light_Lab_Mega_Chamber_Transition_Blend", new Vector3(-10.10f, -49.00f, 0f), new Color(0.14f, 0.08f, 0.22f, 1f), 0.14f, 2.6f, 0.2f, 0.75f);
            // Diagnostic Strobe Beacon
            var megaBeacon = AddLight("Light_Lab_Mega_Beacon_Red", new Vector3(-9.15f, -47.10f, 0f), new Color(0.85f, 0.12f, 0.12f, 1f), 0.65f, 2.2f, 0.3f, 0.65f);
            megaBeacon.gameObject.AddComponent<TheLastSignal.Juice.PulsingLight2D>();

            // --- 5. SECONDARY PERIMETER UTILITY STATIONS (Preserved with Refined Soft Falloff) ---
            AddLight("Light_Lab_Cryo_Vault_Cyan", new Vector3(-16.50f, -42.85f, 0f), new Color(0.12f, 0.68f, 0.84f, 1f), 0.45f, 2.2f, 0.2f, 0.70f);
            AddLight("Light_Lab_Observation_Window_Void", new Vector3(-13.50f, -41.60f, 0f), new Color(0.10f, 0.30f, 0.68f, 1f), 0.42f, 2.8f, 0.2f, 0.70f);
            AddLight("Light_Lab_Cryo_Decon_Cyan", new Vector3(-9.50f, -42.85f, 0f), new Color(0.14f, 0.72f, 0.86f, 1f), 0.45f, 2.2f, 0.2f, 0.70f);
            AddLight("Light_Lab_Eyewash_Amber", new Vector3(-13.50f, -51.20f, 0f), new Color(0.88f, 0.60f, 0.12f, 1f), 0.38f, 2.0f, 0.2f, 0.70f);
            AddLight("Light_Lab_SE_Archive_Glow", new Vector3(-10.20f, -51.20f, 0f), new Color(0.48f, 0.20f, 0.78f, 1f), 0.38f, 2.0f, 0.2f, 0.70f);
            AddLight("Light_Lab_SW_Compressor_Cyan", new Vector3(-16.80f, -51.10f, 0f), new Color(0.12f, 0.50f, 0.82f, 1f), 0.38f, 2.0f, 0.2f, 0.70f);

            // --- 6. DOORWAY THRESHOLDS & PINPOINT ACCENTS ---
            AddLight("Light_DeepStorage_Door_East", new Vector3(-7.50f, -46.50f, 0f), new Color(0.45f, 0.65f, 0.82f, 1f), 0.36f, 2.2f, 0.3f, 0.70f);
            var wCorridorLight = GameObject.Find("Light_CorridorW_SouthEast_Run");
            if (wCorridorLight != null)
            {
                var l2d = wCorridorLight.GetComponent<Light2D>();
                if (l2d != null)
                {
                    wCorridorLight.transform.position = new Vector3(-19.8f, -46.5f, 0f);
                    l2d.color = new Color(0.50f, 0.75f, 0.95f, 1f);
                    l2d.intensity = 0.50f;
                    l2d.pointLightOuterRadius = 3.5f;
                    l2d.pointLightInnerRadius = 0.4f;
                    l2d.falloffIntensity = 0.70f;
                    updateMeshMethod?.Invoke(l2d, new object[] { true });
                    updateBoundsMethod?.Invoke(l2d, null);
                    EditorUtility.SetDirty(l2d);
                }
            }

            AddLight("Light_Lab_Air_Scrubber_Green", new Vector3(-11.10f, -42.15f, 0f), new Color(0.10f, 0.85f, 0.35f, 1f), 0.10f, 0.8f, 0.05f, 0.70f);
            AddLight("Light_Lab_Chemical_Cabinet_Amber", new Vector3(-8.75f, -43.60f, 0f), new Color(0.95f, 0.70f, 0.12f, 1f), 0.10f, 0.6f, 0.05f, 0.70f);
            AddLight("Light_Lab_Breaker_Board_Amber", new Vector3(-18.30f, -45.60f, 0f), new Color(0.95f, 0.50f, 0.10f, 1f), 0.08f, 0.5f, 0.05f, 0.70f);

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

            Material steamMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Environment/Interactables/SafeLightJunction/Mat_VFX_Steam.mat")
                ?? AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Materials/M_Steam_Particle.mat");
            Material sparksMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Environment/Interactables/SafeLightJunction/Mat_VFX_Sparks.mat")
                ?? AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Materials/M_Spark_Particle.mat");
            Material dustMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Environment/Interactables/SafeLightJunction/Mat_VFX_CargoHold_Dust.mat");

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

            // 3. Deep Storage Cryogenic Vapor & Ground Mist (Option A: Cold Breach Horror)
            if (steamMat != null)
            {
                // A. Heavy Cryogenic Vapor plume erupting continuously from Ruptured Vault Chest
                var breachCryoGo = new GameObject("VFX_DeepStorage_Breach_Vapor");
                breachCryoGo.transform.SetParent(vfxRoot.transform);
                breachCryoGo.transform.position = new Vector3(-10.0f, -50.1f, 0f);
                var psBreach = breachCryoGo.AddComponent<ParticleSystem>();
                var rendBreach = breachCryoGo.GetComponent<ParticleSystemRenderer>();
                rendBreach.sharedMaterial = steamMat;
                rendBreach.sortingLayerName = "Floor";
                rendBreach.sortingOrder = 5;

                var mainBreach = psBreach.main;
                mainBreach.maxParticles = 8;
                mainBreach.startLifetime = 2.0f;
                mainBreach.startSpeed = 0.20f;
                mainBreach.startSize = 0.40f;
                mainBreach.startColor = new Color(0.45f, 0.90f, 1.0f, 0.22f); // Restrained breach vapor

                var emBreach = psBreach.emission;
                emBreach.rateOverTime = 3.0f;

                var shapeBreach = psBreach.shape;
                shapeBreach.shapeType = ParticleSystemShapeType.Cone;
                shapeBreach.angle = 35f;
                shapeBreach.radius = 0.40f;

                // B. Sparing cold seal condensation from Intact Stasis Pod 1
                var sealedCryoGo = new GameObject("VFX_DeepStorage_Sealed_Vapor");
                sealedCryoGo.transform.SetParent(vfxRoot.transform);
                sealedCryoGo.transform.position = new Vector3(-17.0f, -50.3f, 0f);
                var psSealed = sealedCryoGo.AddComponent<ParticleSystem>();
                var rendSealed = sealedCryoGo.GetComponent<ParticleSystemRenderer>();
                rendSealed.sharedMaterial = steamMat;
                rendSealed.sortingLayerName = "Floor";
                rendSealed.sortingOrder = 4;

                var mainSealed = psSealed.main;
                mainSealed.maxParticles = 8;
                mainSealed.startLifetime = 2.0f;
                mainSealed.startSpeed = 0.15f;
                mainSealed.startSize = 0.35f;
                mainSealed.startColor = new Color(0.35f, 0.75f, 0.95f, 0.16f);

                var emSealed = psSealed.emission;
                emSealed.rateOverTime = 2.0f;

                var shapeSealed = psSealed.shape;
                shapeSealed.shapeType = ParticleSystemShapeType.Circle;
                shapeSealed.radius = 0.30f;

                // C. High-Pressure Liquid Nitrogen Jet from Specimen Preservation Units
                var dewarVfxGo = new GameObject("VFX_DeepStorage_Dewar_Vent");
                dewarVfxGo.transform.SetParent(vfxRoot.transform);
                dewarVfxGo.transform.position = new Vector3(-17.8f, -44.0f, 0f);
                var psDewar = dewarVfxGo.AddComponent<ParticleSystem>();
                var rendDewar = dewarVfxGo.GetComponent<ParticleSystemRenderer>();
                rendDewar.sharedMaterial = steamMat;
                rendDewar.sortingLayerName = "Obstacles";
                rendDewar.sortingOrder = 6;

                var mainDewar = psDewar.main;
                mainDewar.maxParticles = 16;
                mainDewar.startLifetime = 1.2f;
                mainDewar.startSpeed = 0.85f;
                mainDewar.startSize = 0.28f;
                mainDewar.startColor = new Color(0.80f, 0.94f, 1.0f, 0.35f);

                var emDewar = psDewar.emission;
                emDewar.rateOverTime = 6.0f;

                var shapeDewar = psDewar.shape;
                shapeDewar.shapeType = ParticleSystemShapeType.Cone;
                shapeDewar.angle = 15f;
                shapeDewar.radius = 0.12f;

                // D. Cold vapor rolling out of open Cryo-Morgue Drawer (downward cascade along floor)
                var morgueVfxGo = new GameObject("VFX_DeepStorage_Morgue_Cold");
                morgueVfxGo.transform.SetParent(vfxRoot.transform);
                morgueVfxGo.transform.position = new Vector3(-13.5f, -51.8f, 0f);
                var psMorgue = morgueVfxGo.AddComponent<ParticleSystem>();
                var rendMorgue = morgueVfxGo.GetComponent<ParticleSystemRenderer>();
                rendMorgue.sharedMaterial = steamMat;
                rendMorgue.sortingLayerName = "Floor";
                rendMorgue.sortingOrder = 4;

                var mainMorgue = psMorgue.main;
                mainMorgue.maxParticles = 18;
                mainMorgue.startLifetime = 3.0f;
                mainMorgue.startSpeed = 0.15f;
                mainMorgue.startSize = 0.65f;
                mainMorgue.gravityModifier = 0.04f;
                mainMorgue.startColor = new Color(0.40f, 0.80f, 1.0f, 0.25f);

                var emMorgue = psMorgue.emission;
                emMorgue.rateOverTime = 5.5f;

                var shapeMorgue = psMorgue.shape;
                shapeMorgue.shapeType = ParticleSystemShapeType.Box;
                shapeMorgue.scale = new Vector3(1.4f, 0.3f, 0.1f);
                shapeMorgue.scale = new Vector3(1.2f, 0.3f, 0.1f);

                // E. Creeping Low-Lying Ground Cryo-Fog across the entire floor
                var groundFogGo = new GameObject("VFX_DeepStorage_Ground_Fog");
                groundFogGo.transform.SetParent(vfxRoot.transform);
                groundFogGo.transform.position = new Vector3(-13.5f, -47.5f, 0f);
                var psFog = groundFogGo.AddComponent<ParticleSystem>();
                var rendFog = groundFogGo.GetComponent<ParticleSystemRenderer>();
                rendFog.sharedMaterial = steamMat;
                rendFog.sortingLayerName = "Floor";
                rendFog.sortingOrder = 4;

                var mainFog = psFog.main;
                mainFog.maxParticles = 40;
                mainFog.startLifetime = 4.5f;
                mainFog.startSpeed = 0.06f;
                mainFog.startSize = 1.10f;
                mainFog.startColor = new Color(0.35f, 0.70f, 0.90f, 0.15f); // Soft eerie blanket of floor mist

                var emFog = psFog.emission;
                emFog.rateOverTime = 8.5f;

                var shapeFog = psFog.shape;
                shapeFog.shapeType = ParticleSystemShapeType.Box;
                shapeFog.scale = new Vector3(8.5f, 7.0f, 0.1f);
            }

            // 4. Deep Storage Ambient Vault Dust Motes
            if (dustMat != null)
            {
                var dustGo = new GameObject("VFX_DeepStorage_Atmosphere_Dust");
                dustGo.transform.SetParent(vfxRoot.transform);
                dustGo.transform.position = new Vector3(-13.5f, -47.5f, 0f);
                var ps = dustGo.AddComponent<ParticleSystem>();
                var rend = dustGo.GetComponent<ParticleSystemRenderer>();
                rend.sharedMaterial = dustMat;
                rend.sortingLayerName = "Obstacles";
                rend.sortingOrder = 7;

                var main = ps.main;
                main.maxParticles = 25;
                main.startLifetime = 4.5f;
                main.startSpeed = 0.05f;
                main.startSize = 0.06f;
                main.startColor = new Color(0.60f, 0.70f, 0.80f, 0.16f);

                var emission = ps.emission;
                emission.rateOverTime = 5f;

                var shape = ps.shape;
                shape.shapeType = ParticleSystemShapeType.Box;
                shape.scale = new Vector3(8.5f, 7.0f, 0.1f);
            }
        }
    }
}
#endif
