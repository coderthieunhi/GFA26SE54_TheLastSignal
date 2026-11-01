#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Tilemaps;

namespace TheLastSignal.EditorTools
{
    /// <summary>
    /// Master builder and applier for Sub-Level wall and floor architectural refinements (Phases 1 & 2):
    /// - Node C (The Abyssal Catwalk Nexus)
    /// - Deep Storage (Classified Isotope Vault)
    /// - Power Relay Bay (High-Voltage Dynamo Substation)
    /// - Reactor Core (The Titan Lead Monolith Climax)
    /// - Lateral Corridor T-Junction Navigation
    /// </summary>
    public static class SubLevelRefinementApplier
    {
        private const string TilesSourceDir = "Assets/_Project/Sprites/Environments/SubLevel/Tiles";
        private const string TileAssetsDir = "Assets/_Project/Tiles/SubLevel";

        [MenuItem("Tools/Facility/Apply SubLevel Wall and Floor Refinements", false, 61)]
        public static void ApplyAllRefinements()
        {
            Debug.Log("<color=cyan>[SubLevelRefinementApplier] Starting Wall & Floor Refinements (Phase 2)...</color>");

            // 1. Configure and import newly generated sprites
            ConfigureNewSprites();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            // 2. Create ScriptableObject Tile assets
            CreateTileAssets();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            // 3. Paint upgraded tiles to active scene
            PaintUpgrades();

            // 4. Reposition Node C Bollards to clear walkway
            RepositionNodeCBollards();

            // 5. Reposition Deep Storage Vault Chests into painted staging bays
            RepositionDeepStorageCargo();

            // 6. Mark scene dirty & save
            var activeScene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveScene(activeScene);

            Debug.Log("<color=green>[SubLevelRefinementApplier] Successfully applied and saved Sub-Level Wall & Floor Refinements (Phase 2)!</color>");
        }

        public static void ConfigureNewSprites()
        {
            if (!Directory.Exists(TilesSourceDir)) return;

            string[] files = Directory.GetFiles(TilesSourceDir, "*.png");
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

        public static void PaintUpgrades()
        {
            var gridGo = GameObject.Find("Facility_Grid");
            if (gridGo == null)
            {
                Debug.LogError("[SubLevelRefinementApplier] Facility_Grid not found!");
                return;
            }

            var wallTm = gridGo.transform.Find("Wall_Tilemap")?.GetComponent<Tilemap>();
            var floorTm = gridGo.transform.Find("Floor_Tilemap")?.GetComponent<Tilemap>();
            if (wallTm == null || floorTm == null)
            {
                Debug.LogError("[SubLevelRefinementApplier] Tilemaps not found!");
                return;
            }

            Undo.RecordObject(wallTm, "Upgrade SubLevel Walls");
            Undo.RecordObject(floorTm, "Upgrade SubLevel Floor");

            // Load Existing Base Tiles
            Tile fTread = LoadTile("SubLevel_Floor_Heavy_Treadplate");
            Tile fGrate = LoadTile("SubLevel_Floor_Catwalk_Grate") != null ? LoadTile("SubLevel_Floor_Catwalk_Grate") : fTread;
            Tile fEdgeN = LoadTile("SubLevel_Floor_Catwalk_Edge_N") != null ? LoadTile("SubLevel_Floor_Catwalk_Edge_N") : fGrate;
            Tile fEdgeS = LoadTile("SubLevel_Floor_Catwalk_Edge_S") != null ? LoadTile("SubLevel_Floor_Catwalk_Edge_S") : fGrate;
            Tile fEdgeW = LoadTile("SubLevel_Floor_Catwalk_Edge_W") != null ? LoadTile("SubLevel_Floor_Catwalk_Edge_W") : fGrate;
            Tile fEdgeE = LoadTile("SubLevel_Floor_Catwalk_Edge_E") != null ? LoadTile("SubLevel_Floor_Catwalk_Edge_E") : fGrate;
            Tile fCornNW = LoadTile("SubLevel_Floor_Catwalk_Corner_NW") != null ? LoadTile("SubLevel_Floor_Catwalk_Corner_NW") : fGrate;
            Tile fCornNE = LoadTile("SubLevel_Floor_Catwalk_Corner_NE") != null ? LoadTile("SubLevel_Floor_Catwalk_Corner_NE") : fGrate;
            Tile fCornSW = LoadTile("SubLevel_Floor_Catwalk_Corner_SW") != null ? LoadTile("SubLevel_Floor_Catwalk_Corner_SW") : fGrate;
            Tile fCornSE = LoadTile("SubLevel_Floor_Catwalk_Corner_SE") != null ? LoadTile("SubLevel_Floor_Catwalk_Corner_SE") : fGrate;
            Tile fRubber = LoadTile("SubLevel_Floor_Dielectric_Rubber") != null ? LoadTile("SubLevel_Floor_Dielectric_Rubber") : fTread;
            Tile fTrack = LoadTile("SubLevel_Floor_Crane_Track") != null ? LoadTile("SubLevel_Floor_Crane_Track") : fTread;
            Tile fRad = LoadTile("SubLevel_Floor_Radiation_Plate") != null ? LoadTile("SubLevel_Floor_Radiation_Plate") : fTread;
            Tile fTransStripe = LoadTile("SubLevel_Floor_Transition_Stripe") != null ? LoadTile("SubLevel_Floor_Transition_Stripe") : fTread;

            Tile wWallH = LoadTile("SubLevel_Wall_Reinforced_H");
            Tile wWallV = LoadTile("SubLevel_Wall_Reinforced_V");
            Tile wVent = LoadTile("SubLevel_Wall_Hazard_Vent") != null ? LoadTile("SubLevel_Wall_Hazard_Vent") : wWallH;

            // Load Phase 1 Tiles
            Tile fCapNW = LoadTile("SubLevel_Floor_Catwalk_Cap_NW") != null ? LoadTile("SubLevel_Floor_Catwalk_Cap_NW") : fEdgeN;
            Tile fCapNE = LoadTile("SubLevel_Floor_Catwalk_Cap_NE") != null ? LoadTile("SubLevel_Floor_Catwalk_Cap_NE") : fEdgeN;
            Tile fCapSW = LoadTile("SubLevel_Floor_Catwalk_Cap_SW") != null ? LoadTile("SubLevel_Floor_Catwalk_Cap_SW") : fEdgeS;
            Tile fCapSE = LoadTile("SubLevel_Floor_Catwalk_Cap_SE") != null ? LoadTile("SubLevel_Floor_Catwalk_Cap_SE") : fEdgeS;
            Tile fThreshold = LoadTile("SubLevel_Floor_Threshold_Plate") != null ? LoadTile("SubLevel_Floor_Threshold_Plate") : fTransStripe;
            Tile wColumn = LoadTile("SubLevel_Wall_Reinforced_Column") != null ? LoadTile("SubLevel_Wall_Reinforced_Column") : wWallV;

            Tile fCraneBufN = LoadTile("SubLevel_Floor_Crane_Buffer_North") != null ? LoadTile("SubLevel_Floor_Crane_Buffer_North") : fTrack;
            Tile fCraneBufS = LoadTile("SubLevel_Floor_Crane_Buffer_South") != null ? LoadTile("SubLevel_Floor_Crane_Buffer_South") : fTrack;
            Tile fCargoTL = LoadTile("SubLevel_Floor_Cargo_Bay_TL") != null ? LoadTile("SubLevel_Floor_Cargo_Bay_TL") : fTread;
            Tile fCargoTR = LoadTile("SubLevel_Floor_Cargo_Bay_TR") != null ? LoadTile("SubLevel_Floor_Cargo_Bay_TR") : fTread;
            Tile fCargoBL = LoadTile("SubLevel_Floor_Cargo_Bay_BL") != null ? LoadTile("SubLevel_Floor_Cargo_Bay_BL") : fTread;
            Tile fCargoBR = LoadTile("SubLevel_Floor_Cargo_Bay_BR") != null ? LoadTile("SubLevel_Floor_Cargo_Bay_BR") : fTread;
            Tile wCraneAnchor = LoadTile("SubLevel_Wall_Crane_Gantry_Anchor") != null ? LoadTile("SubLevel_Wall_Crane_Gantry_Anchor") : wWallH;

            Tile fBusbarH = LoadTile("SubLevel_Floor_Busbar_Trench_H") != null ? LoadTile("SubLevel_Floor_Busbar_Trench_H") : fRubber;
            Tile fBusbarV = LoadTile("SubLevel_Floor_Busbar_Trench_V") != null ? LoadTile("SubLevel_Floor_Busbar_Trench_V") : fRubber;
            Tile fHvBorderN = LoadTile("SubLevel_Floor_HV_Border_N") != null ? LoadTile("SubLevel_Floor_HV_Border_N") : fRubber;
            Tile fHvBorderS = LoadTile("SubLevel_Floor_HV_Border_S") != null ? LoadTile("SubLevel_Floor_HV_Border_S") : fRubber;
            Tile wHvTrunk = LoadTile("SubLevel_Wall_HV_Busbar_Trunk") != null ? LoadTile("SubLevel_Wall_HV_Busbar_Trunk") : wWallV;

            Tile fLeadPlain = LoadTile("SubLevel_Floor_Lead_Plates_Plain") != null ? LoadTile("SubLevel_Floor_Lead_Plates_Plain") : fTread;
            Tile fCoolTrench = LoadTile("SubLevel_Floor_Coolant_Trench") != null ? LoadTile("SubLevel_Floor_Coolant_Trench") : fTread;
            Tile fPipeCradle = LoadTile("SubLevel_Floor_Pipe_Cradle") != null ? LoadTile("SubLevel_Floor_Pipe_Cradle") : fTread;
            Tile wCoolFlangeW = LoadTile("SubLevel_Wall_Coolant_Penetration_Flange") != null ? LoadTile("SubLevel_Wall_Coolant_Penetration_Flange") : wWallV;
            Tile wCtrlRods = LoadTile("SubLevel_Wall_Control_Rod_Actuators") != null ? LoadTile("SubLevel_Wall_Control_Rod_Actuators") : wWallH;

            Tile fSignStorage = LoadTile("SubLevel_Floor_Sign_West_Storage") != null ? LoadTile("SubLevel_Floor_Sign_West_Storage") : fTread;
            Tile fSignRelay = LoadTile("SubLevel_Floor_Sign_East_Relay") != null ? LoadTile("SubLevel_Floor_Sign_East_Relay") : fTread;
            Tile fSignReactor = LoadTile("SubLevel_Floor_Sign_South_Reactor") != null ? LoadTile("SubLevel_Floor_Sign_South_Reactor") : fTread;

            // Load Phase 2 Refinement Tiles
            Tile fCoolBridge = LoadTile("SubLevel_Floor_Coolant_Trench_Bridge") != null ? LoadTile("SubLevel_Floor_Coolant_Trench_Bridge") : fTread;
            Tile wCoolFlangeE = LoadTile("SubLevel_Wall_Coolant_Penetration_Flange_E") != null ? LoadTile("SubLevel_Wall_Coolant_Penetration_Flange_E") : wWallV;
            Tile fTJunctionL = LoadTile("SubLevel_Floor_Sign_TJunction_L") != null ? LoadTile("SubLevel_Floor_Sign_TJunction_L") : fTread;
            Tile fTJunctionR = LoadTile("SubLevel_Floor_Sign_TJunction_R") != null ? LoadTile("SubLevel_Floor_Sign_TJunction_R") : fTread;
            Tile wDirectoryL = LoadTile("SubLevel_Wall_Directory_L") != null ? LoadTile("SubLevel_Wall_Directory_L") : wWallH;
            Tile wDirectoryR = LoadTile("SubLevel_Wall_Directory_R") != null ? LoadTile("SubLevel_Wall_Directory_R") : wWallH;
            Tile wJambTop = LoadTile("SubLevel_Wall_Door_Jamb_Top") != null ? LoadTile("SubLevel_Wall_Door_Jamb_Top") : wWallV;
            Tile wJambBot = LoadTile("SubLevel_Wall_Door_Jamb_Bottom") != null ? LoadTile("SubLevel_Wall_Door_Jamb_Bottom") : wWallV;
            Tile wFan = LoadTile("SubLevel_Wall_Exhaust_Fan") != null ? LoadTile("SubLevel_Wall_Exhaust_Fan") : wVent;

            // ========================================================
            // 1. NODE C REFINEMENTS
            // ========================================================
            // North Doorway Hazard Caps (Y = -31):
            floorTm.SetTile(new Vector3Int(-2, -31, 0), fCapNW);
            floorTm.SetTile(new Vector3Int(1, -31, 0), fCapNE);
            floorTm.SetTile(new Vector3Int(-1, -30, 0), fThreshold);
            floorTm.SetTile(new Vector3Int(0, -30, 0), fThreshold);

            // South Doorway Hazard Caps (Y = -38):
            floorTm.SetTile(new Vector3Int(-2, -38, 0), fCapSW);
            floorTm.SetTile(new Vector3Int(1, -38, 0), fCapSE);
            floorTm.SetTile(new Vector3Int(-1, -39, 0), fThreshold);
            floorTm.SetTile(new Vector3Int(0, -39, 0), fThreshold);

            // East Threshold (X = 4, Y in [-35, -34]): Threshold plate transition
            floorTm.SetTile(new Vector3Int(4, -34, 0), fThreshold);
            floorTm.SetTile(new Vector3Int(4, -35, 0), fThreshold);

            // West Wall Structural Columns (X = -5)
            wallTm.SetTile(new Vector3Int(-5, -33, 0), wColumn);
            wallTm.SetTile(new Vector3Int(-5, -36, 0), wColumn);

            // Refresh catwalk center with updated abyss-depth grates
            for (int x = -3; x <= 2; x++)
            {
                for (int y = -37; y <= -32; y++)
                {
                    floorTm.SetTile(new Vector3Int(x, y, 0), fGrate);
                }
            }

            // ========================================================
            // 2. DEEP STORAGE REFINEMENTS
            // ========================================================
            // Crane Track Hydraulic End Buffers
            floorTm.SetTile(new Vector3Int(-14, -43, 0), fCraneBufN);
            floorTm.SetTile(new Vector3Int(-13, -43, 0), fCraneBufN);
            floorTm.SetTile(new Vector3Int(-14, -52, 0), fCraneBufS);
            floorTm.SetTile(new Vector3Int(-13, -52, 0), fCraneBufS);

            // Crane Gantry Wall Anchors
            wallTm.SetTile(new Vector3Int(-14, -42, 0), wCraneAnchor);
            wallTm.SetTile(new Vector3Int(-13, -42, 0), wCraneAnchor);
            wallTm.SetTile(new Vector3Int(-14, -53, 0), wCraneAnchor);
            wallTm.SetTile(new Vector3Int(-13, -53, 0), wCraneAnchor);

            // Painted Cargo Staging Bays (West & East)
            floorTm.SetTile(new Vector3Int(-18, -49, 0), fCargoTL);
            floorTm.SetTile(new Vector3Int(-17, -49, 0), fTread);
            floorTm.SetTile(new Vector3Int(-16, -49, 0), fCargoTR);
            floorTm.SetTile(new Vector3Int(-18, -50, 0), fTread);
            floorTm.SetTile(new Vector3Int(-17, -50, 0), fTread);
            floorTm.SetTile(new Vector3Int(-16, -50, 0), fTread);
            floorTm.SetTile(new Vector3Int(-18, -51, 0), fCargoBL);
            floorTm.SetTile(new Vector3Int(-17, -51, 0), fTread);
            floorTm.SetTile(new Vector3Int(-16, -51, 0), fCargoBR);

            floorTm.SetTile(new Vector3Int(-11, -49, 0), fCargoTL);
            floorTm.SetTile(new Vector3Int(-10, -49, 0), fTread);
            floorTm.SetTile(new Vector3Int(-9, -49, 0), fCargoTR);
            floorTm.SetTile(new Vector3Int(-11, -50, 0), fTread);
            floorTm.SetTile(new Vector3Int(-10, -50, 0), fTread);
            floorTm.SetTile(new Vector3Int(-9, -50, 0), fTread);
            floorTm.SetTile(new Vector3Int(-11, -51, 0), fCargoBL);
            floorTm.SetTile(new Vector3Int(-10, -51, 0), fTread);
            floorTm.SetTile(new Vector3Int(-9, -51, 0), fCargoBR);

            // East Doorway Transition
            floorTm.SetTile(new Vector3Int(-8, -46, 0), fTransStripe);
            floorTm.SetTile(new Vector3Int(-8, -47, 0), fTransStripe);
            floorTm.SetTile(new Vector3Int(-7, -46, 0), fThreshold);
            floorTm.SetTile(new Vector3Int(-7, -47, 0), fThreshold);

            // West Doorway Wall Jambs (X = -19)
            wallTm.SetTile(new Vector3Int(-19, -45, 0), wJambTop);
            wallTm.SetTile(new Vector3Int(-19, -48, 0), wJambBot);

            // ========================================================
            // 3. POWER RELAY BAY REFINEMENTS
            // ========================================================
            // Doorway Transitions
            floorTm.SetTile(new Vector3Int(7, -46, 0), fTransStripe);
            floorTm.SetTile(new Vector3Int(7, -47, 0), fTransStripe);
            floorTm.SetTile(new Vector3Int(6, -46, 0), fThreshold);
            floorTm.SetTile(new Vector3Int(6, -47, 0), fThreshold);

            floorTm.SetTile(new Vector3Int(12, -53, 0), fThreshold);
            floorTm.SetTile(new Vector3Int(13, -53, 0), fThreshold);

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

            // North Wall Industrial Heat Exhaust Fans & Vents (Y = -42)
            wallTm.SetTile(new Vector3Int(10, -42, 0), wFan);
            wallTm.SetTile(new Vector3Int(11, -42, 0), wFan);
            wallTm.SetTile(new Vector3Int(14, -42, 0), wFan);
            wallTm.SetTile(new Vector3Int(15, -42, 0), wFan);

            // East Wall Heavy Busbar Trunking (X = 18)
            wallTm.SetTile(new Vector3Int(18, -45, 0), wHvTrunk);
            wallTm.SetTile(new Vector3Int(18, -49, 0), wHvTrunk);

            // ========================================================
            // 4. REACTOR CORE REFINEMENTS
            // ========================================================
            // Central containment basin plain lead shielding
            for (int x = 10; x <= 15; x++)
            {
                for (int y = -67; y <= -62; y++)
                {
                    floorTm.SetTile(new Vector3Int(x, y, 0), fLeadPlain);
                }
            }

            // 4 Cardinal radiation markers
            floorTm.SetTile(new Vector3Int(11, -63, 0), fRad);
            floorTm.SetTile(new Vector3Int(14, -63, 0), fRad);
            floorTm.SetTile(new Vector3Int(11, -66, 0), fRad);
            floorTm.SetTile(new Vector3Int(14, -66, 0), fRad);

            // Coolant Drainage Perimeter Trench
            for (int x = 10; x <= 15; x++)
            {
                floorTm.SetTile(new Vector3Int(x, -61, 0), fCoolTrench);
                floorTm.SetTile(new Vector3Int(x, -68, 0), fCoolTrench);
            }
            for (int y = -67; y <= -62; y++)
            {
                floorTm.SetTile(new Vector3Int(9, y, 0), fCoolTrench);
                floorTm.SetTile(new Vector3Int(16, y, 0), fCoolTrench);
            }

            // North Entry Coolant Trench Footbridge (spanning over the glowing trench at X in [12, 13], Y = -61)
            floorTm.SetTile(new Vector3Int(12, -61, 0), fCoolBridge);
            floorTm.SetTile(new Vector3Int(13, -61, 0), fCoolBridge);

            // Coolant Pipe Saddles
            floorTm.SetTile(new Vector3Int(7, -65, 0), fPipeCradle);
            floorTm.SetTile(new Vector3Int(8, -65, 0), fPipeCradle);
            floorTm.SetTile(new Vector3Int(17, -65, 0), fPipeCradle);
            floorTm.SetTile(new Vector3Int(18, -65, 0), fPipeCradle);

            // West Wall Flange (X = 6, Y = -65)
            wallTm.SetTile(new Vector3Int(6, -65, 0), wCoolFlangeW);

            // East Wall Mirrored Coolant Flange (X = 19, Y = -65)
            wallTm.SetTile(new Vector3Int(19, -65, 0), wCoolFlangeE);

            // South Wall Control Rod Actuators (Y = -70)
            wallTm.SetTile(new Vector3Int(11, -70, 0), wCtrlRods);
            wallTm.SetTile(new Vector3Int(12, -70, 0), wCtrlRods);
            wallTm.SetTile(new Vector3Int(13, -70, 0), wCtrlRods);
            wallTm.SetTile(new Vector3Int(14, -70, 0), wCtrlRods);

            // ========================================================
            // 5. T-JUNCTION WAYFINDING & DIRECTIONAL STENCILS
            // ========================================================
            // Lateral Corridor: West sign pointing to Deep Storage
            floorTm.SetTile(new Vector3Int(-4, -46, 0), fSignStorage);

            // Lateral Corridor: East sign pointing to Power Relay
            floorTm.SetTile(new Vector3Int(3, -46, 0), fSignRelay);

            // Stem Corridor: South sign pointing to Reactor Core
            floorTm.SetTile(new Vector3Int(0, -44, 0), fSignReactor);

            // Center Intersection Chevron Split Markers
            floorTm.SetTile(new Vector3Int(-1, -46, 0), fTJunctionL);
            floorTm.SetTile(new Vector3Int(0, -46, 0), fTJunctionR);

            // South Wall Master Facility Wayfinding Directory Sign (Y = -48, directly facing player)
            wallTm.SetTile(new Vector3Int(-1, -48, 0), wDirectoryL);
            wallTm.SetTile(new Vector3Int(0, -48, 0), wDirectoryR);

            EditorUtility.SetDirty(wallTm);
            EditorUtility.SetDirty(floorTm);
        }

        public static void RepositionNodeCBollards()
        {
            var bL = GameObject.Find("Prop_NodeC_Bollard_L");
            if (bL != null)
            {
                Undo.RecordObject(bL.transform, "Reposition Node C Bollard L");
                bL.transform.position = new Vector3(-3.2f, -34.5f, 0f);
                EditorUtility.SetDirty(bL);
            }

            var sL = GameObject.Find("Shadow_NodeC_Bollard_L");
            if (sL != null)
            {
                Undo.RecordObject(sL.transform, "Reposition Node C Shadow L");
                sL.transform.position = new Vector3(-3.2f, -34.85f, 0f);
                EditorUtility.SetDirty(sL);
            }

            var lL = GameObject.Find("Light_NodeC_Bollard_L");
            if (lL != null)
            {
                Undo.RecordObject(lL.transform, "Reposition Node C Light L");
                lL.transform.position = new Vector3(-3.2f, -34.3f, 0f);
                EditorUtility.SetDirty(lL);
            }

            var bR = GameObject.Find("Prop_NodeC_Bollard_R");
            if (bR != null)
            {
                Undo.RecordObject(bR.transform, "Reposition Node C Bollard R");
                bR.transform.position = new Vector3(2.2f, -34.5f, 0f);
                EditorUtility.SetDirty(bR);
            }

            var sR = GameObject.Find("Shadow_NodeC_Bollard_R");
            if (sR != null)
            {
                Undo.RecordObject(sR.transform, "Reposition Node C Shadow R");
                sR.transform.position = new Vector3(2.2f, -34.85f, 0f);
                EditorUtility.SetDirty(sR);
            }

            var lR = GameObject.Find("Light_NodeC_Bollard_R");
            if (lR != null)
            {
                Undo.RecordObject(lR.transform, "Reposition Node C Light R");
                lR.transform.position = new Vector3(2.2f, -34.3f, 0f);
                EditorUtility.SetDirty(lR);
            }
        }

        public static void RepositionDeepStorageCargo()
        {
            // Move Chest 1 and its shadow into the West Staging Bay center (-17.0, -50.0)
            var c1 = GameObject.Find("Prop_DeepStorage_Vault_Chest_1");
            if (c1 != null)
            {
                Undo.RecordObject(c1.transform, "Move Vault Chest 1");
                c1.transform.position = new Vector3(-17.0f, -50.0f, 0f);
                EditorUtility.SetDirty(c1);
            }
            var s1 = GameObject.Find("Shadow_DeepStorage_Vault_Chest_1");
            if (s1 != null)
            {
                Undo.RecordObject(s1.transform, "Move Vault Shadow 1");
                s1.transform.position = new Vector3(-17.0f, -50.35f, 0f);
                EditorUtility.SetDirty(s1);
            }
            var l1 = GameObject.Find("Light_DeepStorage_Vault_1");
            if (l1 != null)
            {
                Undo.RecordObject(l1.transform, "Move Vault Light 1");
                l1.transform.position = new Vector3(-17.0f, -50.0f, 0f);
                EditorUtility.SetDirty(l1);
            }

            // Move Chest 2 and its shadow into the East Staging Bay center (-10.0, -50.0)
            var c2 = GameObject.Find("Prop_DeepStorage_Vault_Chest_2");
            if (c2 != null)
            {
                Undo.RecordObject(c2.transform, "Move Vault Chest 2");
                c2.transform.position = new Vector3(-10.0f, -50.0f, 0f);
                EditorUtility.SetDirty(c2);
            }
            var s2 = GameObject.Find("Shadow_DeepStorage_Vault_Chest_2");
            if (s2 != null)
            {
                Undo.RecordObject(s2.transform, "Move Vault Shadow 2");
                s2.transform.position = new Vector3(-10.0f, -50.35f, 0f);
                EditorUtility.SetDirty(s2);
            }
            var l2 = GameObject.Find("Light_DeepStorage_Vault_2");
            if (l2 != null)
            {
                Undo.RecordObject(l2.transform, "Move Vault Light 2");
                l2.transform.position = new Vector3(-10.0f, -50.0f, 0f);
                EditorUtility.SetDirty(l2);
            }

            // Move RadSuit Locker to North Wall (-16.5, -44.5)
            var rLocker = GameObject.Find("Prop_DeepStorage_RadSuit_Locker");
            if (rLocker != null)
            {
                Undo.RecordObject(rLocker.transform, "Move RadSuit Locker");
                rLocker.transform.position = new Vector3(-16.5f, -44.5f, 0f);
                EditorUtility.SetDirty(rLocker);
            }
            var rShadow = GameObject.Find("Shadow_DeepStorage_RadSuit_Locker");
            if (rShadow != null)
            {
                Undo.RecordObject(rShadow.transform, "Move RadSuit Shadow");
                rShadow.transform.position = new Vector3(-16.5f, -44.85f, 0f);
                EditorUtility.SetDirty(rShadow);
            }
            var rLight = GameObject.Find("Light_DeepStorage_RadSuit");
            if (rLight != null)
            {
                Undo.RecordObject(rLight.transform, "Move RadSuit Light");
                rLight.transform.position = new Vector3(-16.5f, -44.5f, 0f);
                EditorUtility.SetDirty(rLight);
            }

            // Move Crate Stack 1 to North Wall (-10.5, -44.5)
            var crate1 = GameObject.Find("Prop_DeepStorage_Crate_Stack_1");
            if (crate1 != null)
            {
                Undo.RecordObject(crate1.transform, "Move Crate Stack 1");
                crate1.transform.position = new Vector3(-10.5f, -44.5f, 0f);
                EditorUtility.SetDirty(crate1);
            }
            var crate1Shadow = GameObject.Find("Shadow_DeepStorage_Crate_Stack_1");
            if (crate1Shadow != null)
            {
                Undo.RecordObject(crate1Shadow.transform, "Move Crate 1 Shadow");
                crate1Shadow.transform.position = new Vector3(-10.5f, -44.80f, 0f);
                EditorUtility.SetDirty(crate1Shadow);
            }
        }

        private static Tile LoadTile(string tileName)
        {
            return AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/{tileName}.asset");
        }
    }
}
#endif
