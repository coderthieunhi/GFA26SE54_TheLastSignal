using UnityEngine;
using UnityEditor;
using UnityEngine.Tilemaps;
using UnityEngine.Rendering.Universal;
using TheLastSignal.Core;
using TheLastSignal.Juice;
using TheLastSignal.Entities.Environment;
using TheLastSignal.Entities.Environment.Interactables;

namespace TheLastSignal.EditorTools
{
    public static class NodeCOverhaulApplier
    {
        private const string TileAssetsDir = "Assets/_Project/Tiles/SubLevel";
        private const string PropsSourceDir = "Assets/_Project/Sprites/Environments/SubLevel/Props";
        private const string SignageSourceDir = "Assets/_Project/Sprites/Environments/SubLevel/Signage";
        private const string TexturesDir = "Assets/_Project/Sprites/Environments/SubLevel/Tiles";

        [MenuItem("The Last Signal/Environment/Apply Node C Complete Overhaul", priority = 25)]
        public static void ApplyOverhaul()
        {
            var gridGo = GameObject.Find("Facility_Grid");
            if (gridGo == null)
            {
                Debug.LogError("[NodeCOverhaul] Facility_Grid not found in scene!");
                return;
            }

            var floorTm = gridGo.transform.Find("Floor_Tilemap")?.GetComponent<Tilemap>();
            var wallTm = gridGo.transform.Find("Wall_Tilemap")?.GetComponent<Tilemap>();
            if (floorTm == null || wallTm == null)
            {
                Debug.LogError("[NodeCOverhaul] Tilemaps not found!");
                return;
            }

            Undo.RecordObject(floorTm, "Node C Mega Catwalk Spine Floor");
            Undo.RecordObject(wallTm, "Node C Mega Catwalk Spine Wall");

            Debug.Log("[NodeCOverhaul] Executing Mega-Catwalk Spine on Node C (Connecting Cargo Hold to T-Junction)...");

            // 1. Tilemap: 21-tile continuous catwalk spine from Cargo Hold (Y = -25) to T-Junction (Y = -45)
            PaintMasterPolishTilemaps(floorTm, wallTm);

            // 2. Full-length Abyssal Depth Void Backdrop
            SetupAbyssVoidBackdrop();

            // 3. Rebuild Props: Continuous guardrails, balanced structural pylons, clean signs
            RebuildMasterPolishProps();

            // 4. Atmospheric 2D Lighting: Volumetric dual cyan abyss uplights & warm terminal light
            RebuildAbyssalLighting();

            // 5. Abyssal Mist Particle Systems
            RebuildAbyssalVFX();

            // 6. Full 21-Tile Void Physics Barriers
            SetupAbyssBarriers();

            EditorUtility.SetDirty(floorTm);
            EditorUtility.SetDirty(wallTm);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(floorTm.gameObject.scene);

            Debug.Log("[NodeCOverhaul] MEGA-CATWALK SPINE COMPLETE! Node C now connects directly from Cargo Hold to the T-Junction.");
        }

        private static void PaintMasterPolishTilemaps(Tilemap floorTm, Tilemap wallTm)
        {
            // Load Tiles
            Tile fTread = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/SubLevel_Floor_Heavy_Treadplate.asset");
            Tile fGrate = fTread; // Hybrid Catwalk: Crossroads center uses solid matte heavy treadplate
            Tile fEdgeN = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/SubLevel_Floor_Catwalk_Hybrid_Edge_N.asset") ?? fTread;
            Tile fEdgeS = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/SubLevel_Floor_Catwalk_Hybrid_Edge_S.asset") ?? fTread;
            Tile fEdgeW = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/SubLevel_Floor_Catwalk_Hybrid_Edge_W.asset") ?? fTread;
            Tile fEdgeE = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/SubLevel_Floor_Catwalk_Hybrid_Edge_E.asset") ?? fTread;
            Tile fCornNW = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/SubLevel_Floor_Catwalk_Hybrid_Corner_NW.asset") ?? fTread;
            Tile fCornSW = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/SubLevel_Floor_Catwalk_Hybrid_Corner_SW.asset") ?? fTread;
            Tile fThreshold = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/SubLevel_Floor_Threshold_Plate.asset") ?? fTread;

            Tile wWallH = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/SubLevel_Wall_Reinforced_H.asset");
            Tile wWallV = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/SubLevel_Wall_Reinforced_V.asset");
            Tile wJambTop = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/SubLevel_Wall_Door_Jamb_Top.asset") ?? wWallV;
            Tile wJambBot = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/SubLevel_Wall_Door_Jamb_Bottom.asset") ?? wWallV;

            // VentShaft transition tiles for seamless East L-Corridor junction
            Tile vsCornBL = AssetDatabase.LoadAssetAtPath<Tile>("Assets/_Project/Tiles/VentShaft/VS_Wall_Corner_BL.asset");
            Tile vsCornTL = AssetDatabase.LoadAssetAtPath<Tile>("Assets/_Project/Tiles/VentShaft/VS_Wall_Corner_TL.asset");

            // Step 1: Clear entirety of Node C mega-abyss void (X: [-6..5], Y: [-45..-25])
            for (int x = -6; x <= 5; x++)
            {
                for (int y = -45; y <= -25; y++)
                {
                    floorTm.SetTile(new Vector3Int(x, y, 0), null);
                    wallTm.SetTile(new Vector3Int(x, y, 0), null);
                }
            }

            // Step 2: BUILD REINFORCED 3-WAY ARCHITECTURAL PORTALS
            // A. North Portal (Y = -25, Cargo Hold South Blast Door)
            floorTm.SetTile(new Vector3Int(-1, -25, 0), fThreshold);
            floorTm.SetTile(new Vector3Int(0, -25, 0), fThreshold);
            wallTm.SetTile(new Vector3Int(-2, -25, 0), wJambTop);
            wallTm.SetTile(new Vector3Int(1, -25, 0), wJambTop);
            // Solid Cargo Hold South walls on left and right
            for (int x = -6; x <= -3; x++) wallTm.SetTile(new Vector3Int(x, -25, 0), wWallH);
            for (int x = 2; x <= 5; x++) wallTm.SetTile(new Vector3Int(x, -25, 0), wWallH);

            // B. South Portal (Y = -45, Sub-Level T-Junction Bedrock Landing)
            floorTm.SetTile(new Vector3Int(-1, -45, 0), fThreshold);
            floorTm.SetTile(new Vector3Int(0, -45, 0), fThreshold);
            wallTm.SetTile(new Vector3Int(-2, -45, 0), wJambBot);
            wallTm.SetTile(new Vector3Int(1, -45, 0), wJambBot);
            // Solid T-Junction North walls on left and right
            for (int x = -8; x <= -3; x++) wallTm.SetTile(new Vector3Int(x, -45, 0), wWallH);
            for (int x = 2; x <= 7; x++) wallTm.SetTile(new Vector3Int(x, -45, 0), wWallH);

            // C. East Portal (X = 5, opening at Y in [-35, -34] leading directly into East L-Corridor / Vent Shaft)
            floorTm.SetTile(new Vector3Int(5, -34, 0), fThreshold);
            floorTm.SetTile(new Vector3Int(5, -35, 0), fThreshold);
            wallTm.SetTile(new Vector3Int(5, -33, 0), vsCornBL ?? wWallH);
            wallTm.SetTile(new Vector3Int(5, -36, 0), vsCornTL ?? wWallH);

            // Step 3: PAINT 21-TILE MEGA-CATWALK SPINE
            // A. North Catwalk Spine: X in [-1, 0], Y in [-33..-26] (8 tiles long over void)
            for (int y = -33; y <= -26; y++)
            {
                floorTm.SetTile(new Vector3Int(-1, y, 0), fEdgeW);
                floorTm.SetTile(new Vector3Int(0, y, 0), fEdgeE);
            }

            // B. Central 2x2 Crossroads + West Dead-End Alcove + East Spur
            // Row Y = -34 (Upper lane):
            floorTm.SetTile(new Vector3Int(-3, -34, 0), fCornNW); // West dead-end cap NW
            floorTm.SetTile(new Vector3Int(-2, -34, 0), null);    // Cleared for Sub-Floor Pipe Trench Inspection Grate
            floorTm.SetTile(new Vector3Int(-1, -34, 0), fGrate);  // Crossroads NW
            floorTm.SetTile(new Vector3Int(0, -34, 0), fGrate);   // Crossroads NE
            floorTm.SetTile(new Vector3Int(1, -34, 0), fEdgeN);   // East spur North edge
            floorTm.SetTile(new Vector3Int(2, -34, 0), fEdgeN);   // East spur North edge
            floorTm.SetTile(new Vector3Int(3, -34, 0), fEdgeN);   // East spur North edge
            floorTm.SetTile(new Vector3Int(4, -34, 0), fEdgeN);   // East spur North edge (meets threshold plate at X=5)

            // Row Y = -35 (Lower lane):
            floorTm.SetTile(new Vector3Int(-3, -35, 0), fCornSW); // West dead-end cap SW
            floorTm.SetTile(new Vector3Int(-2, -35, 0), null);    // Cleared for Sub-Floor Pipe Trench Inspection Grate
            floorTm.SetTile(new Vector3Int(-1, -35, 0), fGrate);  // Crossroads SW
            floorTm.SetTile(new Vector3Int(0, -35, 0), fGrate);   // Crossroads SE
            floorTm.SetTile(new Vector3Int(1, -35, 0), fEdgeS);   // East spur South edge
            floorTm.SetTile(new Vector3Int(2, -35, 0), fEdgeS);   // East spur South edge
            floorTm.SetTile(new Vector3Int(3, -35, 0), fEdgeS);   // East spur South edge
            floorTm.SetTile(new Vector3Int(4, -35, 0), fEdgeS);   // East spur South edge (meets threshold plate at X=5)

            // C. South Catwalk Spine: X in [-1, 0], Y in [-44..-36] (9 tiles long over void)
            for (int y = -44; y <= -36; y++)
            {
                floorTm.SetTile(new Vector3Int(-1, y, 0), fEdgeW);
                floorTm.SetTile(new Vector3Int(0, y, 0), fEdgeE);
            }
        }

        private static void SetupAbyssVoidBackdrop()
        {
            // True Open Abyss Architecture:
            // Remove artificial hard-edged backdrop quads so the void blends seamlessly with the
            // global facility atmosphere and camera background, eliminating the artificial black square patch.
            var existing = GameObject.Find("NodeC_Abyss_Pitch_Backdrop");
            if (existing != null) Undo.DestroyObjectImmediate(existing);
            var existingTruss = GameObject.Find("NodeC_Abyss_Depth_Trusses");
            if (existingTruss != null) Undo.DestroyObjectImmediate(existingTruss);
        }

        private static void RebuildMasterPolishProps()
        {
            var envRoot = GameObject.Find("SubLevel_Environment");
            if (envRoot == null) return;

            var propsRoot = envRoot.transform.Find("Props")?.gameObject;
            if (propsRoot == null) return;

            // Remove existing guardrails, stanchions, struts, bollards, decals, and wall shadows
            string[] toRemove = new string[]
            {
                "Prop_NodeC_Guardrail",
                "Prop_NodeC_Stanchion",
                "Prop_NodeC_Abyssal_Strut",
                "Prop_NodeC_Bollard",
                "Shadow_NodeC_Bollard",
                "Decal_NodeC_Arrow",
                "Shadow_Wall_NodeC",
                "Prop_NodeC_Pipeline_Bank",
                "Prop_NodeC_Pipeline_Corner",
                "Prop_NodeC_Pipeline_Wall_Term",
                "Prop_NodeC_Pipeline_West_Manifold",
                "Prop_NodeC_Overhead_Cross_Gantry",
                "Decal_NodeC_",
                "Prop_NodeC_Expansion_Joint",
                "Prop_NodeC_Transition_Plate",
                "Prop_NodeC_East_Cable_Trunk",
                "Prop_NodeC_SubFloor_Trench",
                "Prop_NodeC_Gantry_Beam"
            };

            for (int i = propsRoot.transform.childCount - 1; i >= 0; i--)
            {
                var child = propsRoot.transform.GetChild(i).gameObject;
                foreach (var prefix in toRemove)
                {
                    if (child.name.StartsWith(prefix))
                    {
                        Undo.DestroyObjectImmediate(child);
                        break;
                    }
                }
            }

            for (int i = envRoot.transform.childCount - 1; i >= 0; i--)
            {
                var child = envRoot.transform.GetChild(i).gameObject;
                if (child.name.StartsWith("Shadow_Wall_NodeC"))
                {
                    Undo.DestroyObjectImmediate(child);
                }
            }

            GameObject SpawnProp(string name, Vector3 pos, string spritePath, bool addCol, Vector2 colSize, string sortingLayer = "Obstacles", int order = 6)
            {
                var existing = GameObject.Find(name);
                if (existing != null) Undo.DestroyObjectImmediate(existing);

                var go = new GameObject(name);
                Undo.RegisterCreatedObjectUndo(go, $"Spawn {name}");
                go.transform.SetParent(propsRoot.transform);
                go.transform.position = pos;

                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
                sr.sortingLayerName = sortingLayer;
                sr.sortingOrder = order;

                if (addCol)
                {
                    var col = go.AddComponent<BoxCollider2D>();
                    if (colSize != default) col.size = colSize;
                }
                return go;
            }

            // A. Continuous Guardrails along the entire 21-tile Catwalk Spine
            // 1. North Walkway: Y in [-33..-26] (Connecting from Crossroads up to Cargo Hold Door)
            for (int y = -33; y <= -26; y++)
            {
                SpawnProp($"Prop_NodeC_Guardrail_North_W_{y}", new Vector3(-1.0f, y + 0.5f, 0f), $"{PropsSourceDir}/Prop_NodeC_Guardrail_V.png", true, new Vector2(0.2f, 1.0f));
                SpawnProp($"Prop_NodeC_Guardrail_North_E_{y}", new Vector3(1.0f, y + 0.5f, 0f), $"{PropsSourceDir}/Prop_NodeC_Guardrail_V_Right.png", true, new Vector2(0.2f, 1.0f));
            }

            // 2. South Walkway: Y in [-44..-36] (Connecting from Crossroads down to T-Junction Landing)
            for (int y = -44; y <= -36; y++)
            {
                if (y == -40)
                {
                    // CLAUSTROPHOBIA: Sheared & broken railing section with severed sparking wire
                    SpawnProp($"Prop_NodeC_Guardrail_South_W_{y}", new Vector3(-1.0f, y + 0.5f, 0f), $"{PropsSourceDir}/Prop_NodeC_Guardrail_Broken_V.png", true, new Vector2(0.2f, 0.4f));
                }
                else
                {
                    SpawnProp($"Prop_NodeC_Guardrail_South_W_{y}", new Vector3(-1.0f, y + 0.5f, 0f), $"{PropsSourceDir}/Prop_NodeC_Guardrail_V.png", true, new Vector2(0.2f, 1.0f));
                }
                SpawnProp($"Prop_NodeC_Guardrail_South_E_{y}", new Vector3(1.0f, y + 0.5f, 0f), $"{PropsSourceDir}/Prop_NodeC_Guardrail_V_Right.png", true, new Vector2(0.2f, 1.0f));
            }

            // 3. West Dead-End Console Spur (Y = -33.0f on North, Y = -35.0f on South, X in [-3..-2])
            for (int x = -3; x <= -2; x++)
            {
                SpawnProp($"Prop_NodeC_Guardrail_West_N_{x}", new Vector3(x + 0.5f, -33.0f, 0f), $"{PropsSourceDir}/Prop_NodeC_Guardrail_H.png", true, new Vector2(1.0f, 0.2f));
                SpawnProp($"Prop_NodeC_Guardrail_West_S_{x}", new Vector3(x + 0.5f, -35.0f, 0f), $"{PropsSourceDir}/Prop_NodeC_Guardrail_H.png", true, new Vector2(1.0f, 0.2f));
            }
            // West End-Cap Railings (Flush at X = -3.0f)
            SpawnProp("Prop_NodeC_Guardrail_West_Cap_Top", new Vector3(-3.0f, -33.5f, 0f), $"{PropsSourceDir}/Prop_NodeC_Guardrail_V.png", true, new Vector2(0.2f, 1.0f));
            SpawnProp("Prop_NodeC_Guardrail_West_Cap_Bot", new Vector3(-3.0f, -34.5f, 0f), $"{PropsSourceDir}/Prop_NodeC_Guardrail_V.png", true, new Vector2(0.2f, 1.0f));

            // 4. East Spur (Y = -33.0f on North, Y = -35.0f on South, X in [1..4])
            for (int x = 1; x <= 4; x++)
            {
                SpawnProp($"Prop_NodeC_Guardrail_East_N_{x}", new Vector3(x + 0.5f, -33.0f, 0f), $"{PropsSourceDir}/Prop_NodeC_Guardrail_H.png", true, new Vector2(1.0f, 0.2f));
                SpawnProp($"Prop_NodeC_Guardrail_East_S_{x}", new Vector3(x + 0.5f, -35.0f, 0f), $"{PropsSourceDir}/Prop_NodeC_Guardrail_H.png", true, new Vector2(1.0f, 0.2f));
            }

            // B. ZERO-SNAG Stanchion Posts
            Vector3[] stanchionPositions = new Vector3[]
            {
                new Vector3(-1.0f, -33.0f, 0f), // Crossroads NW
                new Vector3(1.0f, -33.0f, 0f),  // Crossroads NE
                new Vector3(-1.0f, -35.0f, 0f), // Crossroads SW
                new Vector3(1.0f, -35.0f, 0f),  // Crossroads SE
                new Vector3(-3.0f, -33.0f, 0f), // West dead-end NW
                new Vector3(-3.0f, -35.0f, 0f), // West dead-end SW
                new Vector3(3.0f, -33.0f, 0f),  // East spur mid North
                new Vector3(3.0f, -35.0f, 0f),  // East spur mid South
                new Vector3(4.9f, -33.0f, 0f),  // East portal North jamb
                new Vector3(4.9f, -35.0f, 0f),  // East portal South jamb
                // Mid-span posts along the long spine
                new Vector3(-1.0f, -29.0f, 0f),
                new Vector3(1.0f, -29.0f, 0f),
                new Vector3(-1.0f, -40.0f, 0f),
                new Vector3(1.0f, -40.0f, 0f)
            };

            for (int i = 0; i < stanchionPositions.Length; i++)
            {
                SpawnProp($"Prop_NodeC_Stanchion_{i}", stanchionPositions[i], $"{PropsSourceDir}/Prop_NodeC_Stanchion_Post.png", false, default, "Obstacles", 7);
            }

            // C. 6 STRUCTURAL FOUNDATION PYLONS (Submerged in deep shadow under the 21-tile bridge)
            Vector3[] pylonPositions = new Vector3[]
            {
                new Vector3(0.0f, -28.0f, 0f),  // North Span Upper
                new Vector3(0.0f, -32.0f, 0f),  // North Span Lower
                new Vector3(0.0f, -38.0f, 0f),  // South Span Upper
                new Vector3(0.0f, -42.0f, 0f),  // South Span Lower
                new Vector3(-2.2f, -34.8f, 0f), // West Platform Column
                new Vector3(2.5f, -34.8f, 0f)   // East Spur Column
            };
            string[] pylonNames = new string[] { "North_Upper", "North_Lower", "South_Upper", "South_Lower", "West", "East" };

            for (int i = 0; i < 6; i++)
            {
                var strut = SpawnProp($"Prop_NodeC_Abyssal_Strut_{pylonNames[i]}", pylonPositions[i], $"{PropsSourceDir}/Prop_NodeC_Abyssal_Strut.png", false, default, "Floor", -5);
                var sr = strut.GetComponent<SpriteRenderer>();
                sr.color = new Color(0.10f, 0.14f, 0.18f, 0.70f);
                if (i < 4)
                    strut.transform.localScale = new Vector3(1.60f, 1.30f, 1.0f);
                else
                    strut.transform.localScale = new Vector3(1.20f, 1.40f, 1.0f);
            }

            // D. West Platform: Console & Emergency Safe-Light Junction
            SpawnProp("Prop_NodeC_Console_Maintenance_Plate", new Vector3(-2.45f, -34.5f, 0f), $"{PropsSourceDir}/Prop_NodeC_Console_Maintenance_Plate.png", false, default, "Floor", 1);

            var bridgeConsole = GameObject.Find("Prop_NodeC_Bridge_Console");
            if (bridgeConsole != null)
            {
                bridgeConsole.transform.position = new Vector3(-2.45f, -34.5f, 0f);
                var task = bridgeConsole.GetComponent<TaskLocation>();
                if (task != null) Object.DestroyImmediate(task);
            }

            var bridgeShadow = GameObject.Find("Shadow_NodeC_Bridge_Console");
            if (bridgeShadow != null)
            {
                bridgeShadow.transform.position = new Vector3(-2.45f, -34.85f, 0f);
            }

            var manifold = GameObject.Find("Prop_NodeC_Steam_Manifold");
            if (manifold != null)
            {
                manifold.transform.position = new Vector3(-2.65f, -33.30f, 0f);
            }

            var steamPurge = GameObject.Find("VFX_NodeC_Steam_Purge");
            if (steamPurge != null)
            {
                steamPurge.transform.position = new Vector3(-2.1f, -33.45f, 0f);
            }

            // E. Clean Overhead Gateway Signs
            // North Portal Sign: "Node C - Abyssal Catwalk Nexus" (Mounted at Cargo Hold South blast door)
            var signNorth = GameObject.Find("Sign_NodeC");
            if (signNorth == null)
            {
                signNorth = new GameObject("Sign_NodeC");
                Undo.RegisterCreatedObjectUndo(signNorth, "Spawn Sign_NodeC");
            }
            signNorth.transform.SetParent(propsRoot.transform);
            signNorth.transform.position = new Vector3(0.0f, -24.8f, 0f);
            var srSignN = signNorth.GetComponent<SpriteRenderer>();
            if (srSignN == null) srSignN = signNorth.AddComponent<SpriteRenderer>();
            srSignN.sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{SignageSourceDir}/Sign_SubLevel_NodeC.png");
            srSignN.sortingLayerName = "Obstacles";
            srSignN.sortingOrder = 6;

            // East Portal Sign: "Power Relay Sector"
            var signEast = GameObject.Find("Sign_NodeC_East_Relay");
            if (signEast == null)
            {
                signEast = new GameObject("Sign_NodeC_East_Relay");
                Undo.RegisterCreatedObjectUndo(signEast, "Spawn Sign_NodeC_East_Relay");
            }
            signEast.transform.SetParent(propsRoot.transform);
            signEast.transform.position = new Vector3(5.5f, -32.8f, 0f);
            var srSignE = signEast.GetComponent<SpriteRenderer>();
            if (srSignE == null) srSignE = signEast.AddComponent<SpriteRenderer>();
            srSignE.sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{SignageSourceDir}/Sign_SubLevel_PowerRelay.png");
            srSignE.sortingLayerName = "Obstacles";
            srSignE.sortingOrder = 6;

            // South Portal Sign: "Reactor Core Sector" (Mounted at T-Junction Landing)
            var signSouth = GameObject.Find("Sign_NodeC_South_Reactor");
            if (signSouth == null)
            {
                signSouth = new GameObject("Sign_NodeC_South_Reactor");
                Undo.RegisterCreatedObjectUndo(signSouth, "Spawn Sign_NodeC_South_Reactor");
            }
            signSouth.transform.SetParent(propsRoot.transform);
            signSouth.transform.position = new Vector3(0.0f, -45.3f, 0f);
            var srSignS = signSouth.GetComponent<SpriteRenderer>();
            if (srSignS == null) srSignS = signSouth.AddComponent<SpriteRenderer>();
            srSignS.sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{SignageSourceDir}/Sign_SubLevel_ReactorCore.png");
            srSignS.sortingLayerName = "Obstacles";
            srSignS.sortingOrder = 6;

            // F. OVERHEAD CLAUSTROPHOBIC GANTRY ARCHES (Spanning 2-tile catwalk above player head, centered at X = 0.0f)
            // 1. North Arch (Y = -29.0f): Recognizable industrial floodlight gantry facing South toward crossroads
            SpawnProp("Prop_NodeC_Gantry_Arch_North", new Vector3(0.0f, -29.0f, 0f), $"{PropsSourceDir}/Prop_NodeC_Floodlight_Gantry_North_READY.png", false, default, "Obstacles", 22);

            // 2. South Arch (Y = -40.5f): Recognizable industrial floodlight gantry facing North toward crossroads
            SpawnProp("Prop_NodeC_Gantry_Arch_South", new Vector3(0.0f, -40.5f, 0f), $"{PropsSourceDir}/Prop_NodeC_Floodlight_Gantry_South_READY.png", false, default, "Obstacles", 22);

            // Volumetric Overhead Spotlight Beams (7.0m reach meeting and overlapping at Crossroads)
            var beamN = SpawnProp("Prop_NodeC_Gantry_Beam_North", new Vector3(0.0f, -32.50f, 0f), $"{PropsSourceDir}/Prop_NodeC_Gantry_Beam_North.png", false, default, "Floor", 5);
            var srBeamN = beamN.GetComponent<SpriteRenderer>();
            if (srBeamN != null) srBeamN.enabled = false;

            var beamS = SpawnProp("Prop_NodeC_Gantry_Beam_South", new Vector3(0.0f, -37.00f, 0f), $"{PropsSourceDir}/Prop_NodeC_Gantry_Beam_South.png", false, default, "Floor", 5);
            var srBeamS = beamS.GetComponent<SpriteRenderer>();
            if (srBeamS != null) srBeamS.enabled = false;

            // G. 1-TILE SQUEEZE CHOKEPOINT (Damaged pipe obstruction on North Span)
            // Clamped to left rail (X = -1.0), blocks X in [-1.0..-0.5], leaving X in [0.0..1.0] free
            SpawnProp("Prop_NodeC_Chokepoint_Pipe", new Vector3(-0.75f, -29.0f, 0f), "Assets/_Project/Sprites/Environments/VentShaft/Props/VS_Prop_Damaged_Pipe_Joint.png", true, new Vector2(0.45f, 0.45f), "Obstacles", 8);

            // H. Clean Deck: East Spur Cable Snake removed for clear, uncluttered transit floor.
            // (Prop_NodeC_East_Cable_Trunk cleaned up via toRemove list)

            // I. Distant Sub-Level Abyss Beacons (Deep void depth cues)
            var distN = SpawnProp("Prop_NodeC_Distant_Beacon_N", new Vector3(-3.8f, -30.0f, 0f), $"{PropsSourceDir}/Prop_ReactorCore_Siren_Beacon.png", false, default, "Floor", -8);
            distN.transform.localScale = new Vector3(0.35f, 0.35f, 1.0f);
            var srDistN = distN.GetComponent<SpriteRenderer>();
            if (srDistN != null) srDistN.color = new Color(0.12f, 0.15f, 0.20f, 0.60f);

            var distS = SpawnProp("Prop_NodeC_Distant_Beacon_S", new Vector3(3.8f, -41.0f, 0f), $"{PropsSourceDir}/Prop_ReactorCore_Siren_Beacon.png", false, default, "Floor", -8);
            distS.transform.localScale = new Vector3(0.35f, 0.35f, 1.0f);
            var srDistS = distS.GetComponent<SpriteRenderer>();
            if (srDistS != null) srDistS.color = new Color(0.12f, 0.15f, 0.20f, 0.60f);

            // J. MASSIVE PIPELINE CONDUIT CHASM FLANKS (Dual mega-conduit banks flanking catwalk spine & East spur)
            // Left (West) Conduit Bank (Prop_NodeC_Mega_Pipeline_Bank_V.png) at X = -1.85f
            // Right (East) Conduit Bank (Prop_NodeC_Mega_Pipeline_Bank_V_Right.png) at X = +1.85f
            // 1. North Span (West side): Y = -31.0f, -29.0f, -27.0f, -25.5f (covers Y in [-32.0..-24.5])
            float[] northPipeY_W = new float[] { -31.0f, -29.0f, -27.0f, -25.5f };
            for (int i = 0; i < northPipeY_W.Length; i++)
            {
                string spriteName = (i == 1) ? "Prop_NodeC_Mega_Pipeline_Bank_V_Gauge.png" : "Prop_NodeC_Mega_Pipeline_Bank_V_Standard.png";
                SpawnProp($"Prop_NodeC_Pipeline_Bank_North_W_{i}", new Vector3(-1.85f, northPipeY_W[i], 0f), $"{PropsSourceDir}/{spriteName}", false, default, "Floor", -2);
            }

            // North Span (East side): Above the corner elbow at Y = -32.5f
            float[] northPipeY_E = new float[] { -31.0f, -29.0f, -27.0f, -25.5f };
            for (int i = 0; i < northPipeY_E.Length; i++)
            {
                string spriteName = (i == 1) ? "Prop_NodeC_Mega_Pipeline_Bank_V_Right_Box.png" : "Prop_NodeC_Mega_Pipeline_Bank_V_Right_Standard.png";
                SpawnProp($"Prop_NodeC_Pipeline_Bank_North_E_{i}", new Vector3(1.85f, northPipeY_E[i], 0f), $"{PropsSourceDir}/{spriteName}", false, default, "Floor", -2);
            }

            // 2. South Span (West side): Below the corner elbow at Y = -35.5f
            float[] southPipeY_W = new float[] { -37.0f, -39.0f, -41.0f, -43.0f, -44.5f };
            for (int i = 0; i < southPipeY_W.Length; i++)
            {
                string spriteName = (i == 1) ? "Prop_NodeC_Mega_Pipeline_Bank_V_Sensor.png" : "Prop_NodeC_Mega_Pipeline_Bank_V_Standard.png";
                SpawnProp($"Prop_NodeC_Pipeline_Bank_South_W_{i}", new Vector3(-1.85f, southPipeY_W[i], 0f), $"{PropsSourceDir}/{spriteName}", false, default, "Floor", -2);
            }

            // South Span (East side): Below the corner elbow at Y = -35.5f
            float[] southPipeY_E = new float[] { -37.0f, -39.0f, -41.0f, -43.0f, -44.5f };
            for (int i = 0; i < southPipeY_E.Length; i++)
            {
                string spriteName = (i == 1) ? "Prop_NodeC_Mega_Pipeline_Bank_V_Right_Valve.png" : "Prop_NodeC_Mega_Pipeline_Bank_V_Right_Standard.png";
                SpawnProp($"Prop_NodeC_Pipeline_Bank_South_E_{i}", new Vector3(1.85f, southPipeY_E[i], 0f), $"{PropsSourceDir}/{spriteName}", false, default, "Floor", -2);
            }

            // 3. CONTINUOUS EAST SPUR PIPELINE SYSTEM (Flanking both North and South sides of the East path)
            // A. North-East 90-degree Corner Elbow (Sweeping from North down into East)
            SpawnProp("Prop_NodeC_Pipeline_Corner_NE", new Vector3(1.85f, -32.50f, 0f), $"{PropsSourceDir}/Prop_NodeC_Mega_Pipeline_Corner_NE.png", false, default, "Floor", -2);
            // Horizontal pipeline bank running East
            SpawnProp("Prop_NodeC_Pipeline_Bank_H_North", new Vector3(3.35f, -32.50f, 0f), $"{PropsSourceDir}/Prop_NodeC_Mega_Pipeline_Bank_H_North.png", false, default, "Floor", -2);
            // Bulkhead wall collar penetration into Power Relay sector
            SpawnProp("Prop_NodeC_Pipeline_Wall_Term_North", new Vector3(4.85f, -32.50f, 0f), $"{PropsSourceDir}/Prop_NodeC_Mega_Pipeline_Wall_Term_North.png", false, default, "Floor", -2);

            // B. South-East 90-degree Corner Elbow (Sweeping from South up into East)
            SpawnProp("Prop_NodeC_Pipeline_Corner_SE", new Vector3(1.85f, -35.50f, 0f), $"{PropsSourceDir}/Prop_NodeC_Mega_Pipeline_Corner_SE.png", false, default, "Floor", -2);
            // Horizontal pipeline bank running East
            SpawnProp("Prop_NodeC_Pipeline_Bank_H_South", new Vector3(3.35f, -35.50f, 0f), $"{PropsSourceDir}/Prop_NodeC_Mega_Pipeline_Bank_H_South.png", false, default, "Floor", -2);
            // Bulkhead wall collar penetration into Power Relay sector
            SpawnProp("Prop_NodeC_Pipeline_Wall_Term_South", new Vector3(4.85f, -35.50f, 0f), $"{PropsSourceDir}/Prop_NodeC_Mega_Pipeline_Wall_Term_South.png", false, default, "Floor", -2);

            // 4. CONTINUOUS WEST FLANK STRAIGHT PIPELINE (Passing under West Platform via Sub-Floor Trench)
            // Mid-Span Pipeline Bank connecting North span to South span straight through Y in [-36.0 .. -32.0]
            SpawnProp("Prop_NodeC_Pipeline_Bank_Mid_W_0", new Vector3(-1.85f, -33.00f, 0f), $"{PropsSourceDir}/Prop_NodeC_Mega_Pipeline_Bank_V_Standard.png", false, default, "Floor", -2);
            SpawnProp("Prop_NodeC_Pipeline_Bank_Mid_W_1", new Vector3(-1.85f, -35.00f, 0f), $"{PropsSourceDir}/Prop_NodeC_Mega_Pipeline_Bank_V_Standard.png", false, default, "Floor", -2);

            // Sub-Floor Catwalk Inspection Trench Grate (Heavy see-through diamond grating revealing pipes beneath)
            SpawnProp("Prop_NodeC_SubFloor_Trench_Grate", new Vector3(-1.625f, -34.00f, 0f), $"{PropsSourceDir}/Prop_NodeC_SubFloor_Trench_Grate.png", false, default, "Floor", 1);

            // K. ENHANCED FLOOR STORYTELLING, MODULAR VARIANTS & TRANSITIONS
            // (Central Crossroads is kept clean, flat heavy treadplate deck without bulky obstructions or cross-gantry)

            // 2. Technical Sector Stencils (Directly stenciled onto deck plates)
            SpawnProp("Decal_NodeC_Sector_C1", new Vector3(0.0f, -27.5f, 0f), $"{PropsSourceDir}/Decal_NodeC_Sector_C1.png", false, default, "Floor", 2);
            SpawnProp("Decal_NodeC_Sector_C2", new Vector3(0.0f, -42.5f, 0f), $"{PropsSourceDir}/Decal_NodeC_Sector_C2.png", false, default, "Floor", 2);

            // 3. Heavy Transverse Structural Expansion Joints (Breaking the 21-tile monotony)
            SpawnProp("Prop_NodeC_Expansion_Joint_North", new Vector3(0.0f, -31.0f, 0f), $"{PropsSourceDir}/Prop_NodeC_Expansion_Joint.png", false, default, "Floor", 3);
            SpawnProp("Prop_NodeC_Expansion_Joint_South", new Vector3(0.0f, -38.0f, 0f), $"{PropsSourceDir}/Prop_NodeC_Expansion_Joint.png", false, default, "Floor", 3);

            // 4. Broken Rail Electrical Arc Scorch Decal (Carbon soot & molten spatter at sparking wire)
            SpawnProp("Decal_NodeC_Electrical_Scorch", new Vector3(-0.80f, -39.5f, 0f), $"{PropsSourceDir}/Decal_NodeC_Electrical_Scorch.png", false, default, "Floor", 3);

            // 5. West Alcove Steam / Machine Oil Leak Decal
            SpawnProp("Decal_NodeC_Oil_Leak", new Vector3(-2.65f, -33.6f, 0f), $"{PropsSourceDir}/Decal_NodeC_Oil_Leak.png", false, default, "Floor", 2);

            // 6. East Spur Heavy Transition Threshold Plate (Seamless coupling to East Loop hallway)
            SpawnProp("Prop_NodeC_Transition_Plate_East", new Vector3(4.9f, -34.5f, 0f), $"{PropsSourceDir}/Prop_NodeC_Transition_Plate_East.png", false, default, "Floor", 4);

            // 7. Setup Emergency Safe-Light Junction & Floodlight Visual Controller
            SetupNodeCSafeLightJunction(bridgeConsole);
        }

        private static void RebuildAbyssalLighting()
        {
            var envRoot = GameObject.Find("SubLevel_Environment");
            if (envRoot == null) return;

            var lightsRoot = envRoot.transform.Find("Lights")?.gameObject;
            if (lightsRoot == null)
            {
                lightsRoot = new GameObject("Lights");
                Undo.RegisterCreatedObjectUndo(lightsRoot, "Spawn Lights");
                lightsRoot.transform.SetParent(envRoot.transform);
            }

            // Clean up old lights if present
            string[] oldLights = new string[]
            {
                "Light_NodeC_Bollard_N_L", "Light_NodeC_Bollard_N_R",
                "Light_NodeC_Bollard_S_L", "Light_NodeC_Bollard_S_R",
                "Light_NodeC_Bollard_E_L", "Light_NodeC_Bollard_E_R",
                "Light_NodeC_Bollard_L", "Light_NodeC_Bollard_R",
                "Light_NodeC_Abyss_Uplight",
                "Light_NodeC_Crossroads_Gantry",
                "Light_NodeC_Nexus_Overhead"
            };
            foreach (var ol in oldLights)
            {
                var lGo = GameObject.Find(ol);
                if (lGo != null) Undo.DestroyObjectImmediate(lGo);
            }

            Light2D AddPointLight(string name, Vector3 pos, Color color, float intensity, float radius, float innerRadiusRatio = 0.25f)
            {
                var existing = GameObject.Find(name);
                if (existing != null) Undo.DestroyObjectImmediate(existing);

                var go = new GameObject(name);
                Undo.RegisterCreatedObjectUndo(go, $"Spawn {name}");
                go.transform.SetParent(lightsRoot.transform);
                go.transform.position = pos;

                var l = go.AddComponent<Light2D>();
                l.lightType = Light2D.LightType.Point;
                l.color = color;
                l.intensity = intensity;
                l.pointLightOuterRadius = radius;
                l.pointLightInnerRadius = radius * innerRadiusRatio;
                return l;
            }

            // 1. Dual Abyssal Cyan Uplights (Volumetric subterranean glow across the 21-tile spine)
            var abyssLightN = AddPointLight("Light_NodeC_Abyss_Uplight_North", new Vector3(0.0f, -29.5f, 0f), new Color(0.06f, 0.50f, 0.70f, 1f), 0.75f, 10.5f, 0.35f);
            var pulseN = abyssLightN.gameObject.AddComponent<PulsingLight2D>();
            var pSoN = new SerializedObject(pulseN);
            pSoN.FindProperty("minIntensity").floatValue = 0.55f;
            pSoN.FindProperty("maxIntensity").floatValue = 0.85f;
            pSoN.FindProperty("pulseSpeed").floatValue = 0.60f;
            pSoN.ApplyModifiedProperties();

            var abyssLightS = AddPointLight("Light_NodeC_Abyss_Uplight_South", new Vector3(0.0f, -40.5f, 0f), new Color(0.06f, 0.50f, 0.70f, 1f), 0.75f, 10.5f, 0.35f);
            var pulseS = abyssLightS.gameObject.AddComponent<PulsingLight2D>();
            var pSoS = new SerializedObject(pulseS);
            pSoS.FindProperty("minIntensity").floatValue = 0.55f;
            pSoS.FindProperty("maxIntensity").floatValue = 0.85f;
            pSoS.FindProperty("pulseSpeed").floatValue = 0.60f;
            pSoS.ApplyModifiedProperties();

            // 3. West Dead-End Console Task Light - THE HERO FOCAL POINT (Warm Amber Glow)
            AddPointLight("Light_NodeC_Bridge_Console", new Vector3(-2.40f, -34.2f, 0f), new Color(1.0f, 0.72f, 0.25f, 1f), 0.95f, 3.2f);

            // 4. Steam Hazard Warning & West Manifold Pressure Telemetry Glow
            AddPointLight("Light_NodeC_Manifold", new Vector3(-2.65f, -33.45f, 0f), new Color(1.0f, 0.40f, 0.10f, 1f), 0.55f, 2.2f);
            AddPointLight("Light_NodeC_West_Manifold", new Vector3(-2.85f, -34.0f, 0f), new Color(0.15f, 0.85f, 0.75f, 1f), 0.60f, 2.8f);

            // 5. Overhead Portal Sign Glows (Centered on catwalk X = 0.0f)
            AddPointLight("Light_NodeC_Sign_North", new Vector3(0.0f, -24.8f, 0f), new Color(1.0f, 0.70f, 0.20f, 1f), 0.50f, 2.0f);
            AddPointLight("Light_NodeC_Sign_East", new Vector3(5.5f, -32.8f, 0f), new Color(1.0f, 0.25f, 0.20f, 1f), 0.50f, 2.0f);
            AddPointLight("Light_NodeC_Sign_South", new Vector3(0.0f, -45.3f, 0f), new Color(0.20f, 0.75f, 1.0f, 1f), 0.50f, 2.0f);

            // 6. Overhead Gantry Arch Warning Strobes & Downlights (Centered on catwalk X = 0.0f)
            var strobeN = AddPointLight("Light_NodeC_Arch_North_Strobe", new Vector3(0.0f, -28.9f, 0f), new Color(1.0f, 0.55f, 0.10f, 1f), 0.85f, 4.0f);
            var pulseStrN = strobeN.gameObject.AddComponent<PulsingLight2D>();
            var pSoStrN = new SerializedObject(pulseStrN);
            pSoStrN.FindProperty("minIntensity").floatValue = 0.20f;
            pSoStrN.FindProperty("maxIntensity").floatValue = 0.90f;
            pSoStrN.FindProperty("pulseSpeed").floatValue = 2.0f;
            pSoStrN.ApplyModifiedProperties();

            AddPointLight("Light_NodeC_Arch_North_Down", new Vector3(0.0f, -29.3f, 0f), new Color(0.25f, 0.85f, 1.0f, 1f), 0.40f, 2.5f);

            var strobeS = AddPointLight("Light_NodeC_Arch_South_Strobe", new Vector3(0.0f, -40.4f, 0f), new Color(1.0f, 0.55f, 0.10f, 1f), 0.85f, 4.0f);
            var pulseStrS = strobeS.gameObject.AddComponent<PulsingLight2D>();
            var pSoStrS = new SerializedObject(pulseStrS);
            pSoStrS.FindProperty("minIntensity").floatValue = 0.15f;
            pSoStrS.FindProperty("maxIntensity").floatValue = 0.95f;
            pSoStrS.FindProperty("pulseSpeed").floatValue = 2.8f;
            pSoStrS.ApplyModifiedProperties();

            AddPointLight("Light_NodeC_Arch_South_Down", new Vector3(0.0f, -40.8f, 0f), new Color(0.25f, 0.85f, 1.0f, 1f), 0.40f, 2.5f);

            // 7. Sparking Broken Rail Warning Glow
            var sparkLight = AddPointLight("Light_NodeC_Spark_Glow", new Vector3(-1.0f, -39.5f, 0f), new Color(1.0f, 0.45f, 0.10f, 1f), 0.70f, 2.2f);
            var pulseSpark = sparkLight.gameObject.AddComponent<PulsingLight2D>();
            var pSoSpark = new SerializedObject(pulseSpark);
            pSoSpark.FindProperty("minIntensity").floatValue = 0.10f;
            pSoSpark.FindProperty("maxIntensity").floatValue = 0.80f;
            pSoSpark.FindProperty("pulseSpeed").floatValue = 4.5f;
            pSoSpark.ApplyModifiedProperties();

            // 8. East Spur Conduit Indicator Glow
            AddPointLight("Light_NodeC_East_Spur", new Vector3(2.8f, -34.5f, 0f), new Color(1.0f, 0.50f, 0.15f, 1f), 0.45f, 2.5f);

            // 9. Distant Deep Abyss Maintenance Beacons (Atmospheric depth cues)
            var lightDistN = AddPointLight("Light_NodeC_Distant_Beacon_N", new Vector3(-3.8f, -30.0f, 0f), new Color(1.0f, 0.15f, 0.10f, 1f), 0.40f, 2.5f);
            var pulseDistN = lightDistN.gameObject.AddComponent<PulsingLight2D>();
            var pSoDistN = new SerializedObject(pulseDistN);
            pSoDistN.FindProperty("minIntensity").floatValue = 0.05f;
            pSoDistN.FindProperty("maxIntensity").floatValue = 0.45f;
            pSoDistN.FindProperty("pulseSpeed").floatValue = 0.8f;
            pSoDistN.ApplyModifiedProperties();

            var lightDistS = AddPointLight("Light_NodeC_Distant_Beacon_S", new Vector3(3.8f, -41.0f, 0f), new Color(1.0f, 0.15f, 0.10f, 1f), 0.40f, 2.5f);
            var pulseDistS = lightDistS.gameObject.AddComponent<PulsingLight2D>();
            var pSoDistS = new SerializedObject(pulseDistS);
            pSoDistS.FindProperty("minIntensity").floatValue = 0.05f;
            pSoDistS.FindProperty("maxIntensity").floatValue = 0.45f;
            pSoDistS.FindProperty("pulseSpeed").floatValue = 1.1f;
            pSoDistS.ApplyModifiedProperties();
        }

        private static void RebuildAbyssalVFX()
        {
            var envRoot = GameObject.Find("SubLevel_Environment");
            if (envRoot == null) return;

            var vfxRoot = envRoot.transform.Find("VFX")?.gameObject;
            if (vfxRoot == null)
            {
                vfxRoot = new GameObject("VFX");
                vfxRoot.transform.SetParent(envRoot.transform);
            }

            Material steamMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Materials/M_Steam_Particle.mat");
            if (steamMat == null) return;

            // 4 Abyssal Mist Emitters across the full 21-tile void
            Vector3[] pitCenters = new Vector3[]
            {
                new Vector3(-3.0f, -29.0f, 0f),
                new Vector3(3.0f, -29.0f, 0f),
                new Vector3(-3.0f, -40.0f, 0f),
                new Vector3(3.0f, -40.0f, 0f)
            };
            string[] pitNames = new string[] { "NW_Upper", "NE_Upper", "SW_Lower", "SE_Lower" };

            for (int i = 0; i < 4; i++)
            {
                string vfxName = $"VFX_NodeC_Abyss_Mist_{pitNames[i]}";
                var existing = GameObject.Find(vfxName);
                if (existing != null) Undo.DestroyObjectImmediate(existing);

                var go = new GameObject(vfxName);
                Undo.RegisterCreatedObjectUndo(go, $"Spawn {vfxName}");
                go.transform.SetParent(vfxRoot.transform);
                go.transform.position = pitCenters[i];

                var ps = go.AddComponent<ParticleSystem>();
                var rend = go.GetComponent<ParticleSystemRenderer>();
                rend.sharedMaterial = steamMat;
                rend.sortingLayerName = "Floor";
                rend.sortingOrder = 3;

                var main = ps.main;
                main.maxParticles = 18;
                main.startLifetime = 4.5f;
                main.startSpeed = 0.20f;
                main.startSize = 1.4f;
                main.startColor = new Color(0.18f, 0.60f, 0.80f, 0.20f);

                var emission = ps.emission;
                emission.rateOverTime = 3.2f;

                var shape = ps.shape;
                shape.shapeType = ParticleSystemShapeType.Circle;
                shape.radius = 1.2f;
            }

            // 5. Severed Wire Sparks (Dropping down into the abyss at broken railing)
            Material sparkMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Environment/Interactables/SafeLightJunction/Mat_VFX_Sparks.mat");
            if (sparkMat != null)
            {
                string sparkName = "VFX_NodeC_Severed_Sparks";
                var oldSpark = GameObject.Find(sparkName);
                if (oldSpark != null) Undo.DestroyObjectImmediate(oldSpark);

                var sparkGo = new GameObject(sparkName);
                Undo.RegisterCreatedObjectUndo(sparkGo, $"Spawn {sparkName}");
                sparkGo.transform.SetParent(vfxRoot.transform);
                sparkGo.transform.position = new Vector3(-1.0f, -39.5f, 0f);

                var ps = sparkGo.AddComponent<ParticleSystem>();
                var rend = sparkGo.GetComponent<ParticleSystemRenderer>();
                rend.sharedMaterial = sparkMat;
                rend.sortingLayerName = "Obstacles";
                rend.sortingOrder = 10;

                var main = ps.main;
                main.maxParticles = 30;
                main.startLifetime = 0.8f;
                main.startSpeed = 1.4f;
                main.gravityModifier = 3.0f; // Drop into abyss
                main.startSize = 0.08f;
                main.startColor = new Color(1.0f, 0.85f, 0.35f, 1f);

                var emission = ps.emission;
                emission.rateOverTime = 5.0f;

                var shape = ps.shape;
                shape.shapeType = ParticleSystemShapeType.Sphere;
                shape.radius = 0.15f;
            }

            // 6. Chokepoint Steam Leak (Hissing across left lane at North Span)
            string steamLeakName = "VFX_NodeC_Chokepoint_Steam";
            var oldLeak = GameObject.Find(steamLeakName);
            if (oldLeak != null) Undo.DestroyObjectImmediate(oldLeak);

            var leakGo = new GameObject(steamLeakName);
            Undo.RegisterCreatedObjectUndo(leakGo, $"Spawn {steamLeakName}");
            leakGo.transform.SetParent(vfxRoot.transform);
            leakGo.transform.position = new Vector3(-0.75f, -29.0f, 0f);
            leakGo.transform.rotation = Quaternion.Euler(0f, 0f, -45f);

            var psLeak = leakGo.AddComponent<ParticleSystem>();
            var rendLeak = leakGo.GetComponent<ParticleSystemRenderer>();
            rendLeak.sharedMaterial = steamMat;
            rendLeak.sortingLayerName = "Obstacles";
            rendLeak.sortingOrder = 9;

            var mainLeak = psLeak.main;
            mainLeak.maxParticles = 14;
            mainLeak.startLifetime = 1.0f;
            mainLeak.startSpeed = 0.7f;
            mainLeak.startSize = 0.40f;
            mainLeak.startColor = new Color(0.9f, 0.95f, 1.0f, 0.40f);

            var emLeak = psLeak.emission;
            emLeak.rateOverTime = 4.5f;

            var shapeLeak = psLeak.shape;
            shapeLeak.shapeType = ParticleSystemShapeType.Cone;
            shapeLeak.angle = 15f;
            shapeLeak.radius = 0.08f;
        }

        private static void SetupAbyssBarriers()
        {
            var envRoot = GameObject.Find("SubLevel_Environment");
            if (envRoot == null) return;

            var barriersGo = GameObject.Find("NodeC_Abyss_Barriers");
            if (barriersGo != null) Undo.DestroyObjectImmediate(barriersGo);

            barriersGo = new GameObject("NodeC_Abyss_Barriers");
            Undo.RegisterCreatedObjectUndo(barriersGo, "Spawn NodeC_Abyss_Barriers");
            barriersGo.transform.SetParent(envRoot.transform);
            int obsLayer = LayerMask.NameToLayer("Obstacles");
            if (obsLayer != -1) barriersGo.layer = obsLayer;

            // Full-length Void Colliders protecting both sides of the 21-tile catwalk spine
            // NW Abyss: X in [-6.0..-1.0], Y in [-33.0..-24.0]
            var colNW = barriersGo.AddComponent<BoxCollider2D>();
            colNW.offset = new Vector2(-3.5f, -28.5f);
            colNW.size = new Vector2(5.0f, 9.0f);

            // NE Abyss: X in [1.0..6.0], Y in [-33.0..-24.0]
            var colNE = barriersGo.AddComponent<BoxCollider2D>();
            colNE.offset = new Vector2(3.5f, -28.5f);
            colNE.size = new Vector2(5.0f, 9.0f);

            // SW Abyss: X in [-6.0..-1.0], Y in [-46.0..-35.0]
            var colSW = barriersGo.AddComponent<BoxCollider2D>();
            colSW.offset = new Vector2(-3.5f, -40.5f);
            colSW.size = new Vector2(5.0f, 11.0f);

            // SE Abyss: X in [1.0..6.0], Y in [-46.0..-35.0]
            var colSE = barriersGo.AddComponent<BoxCollider2D>();
            colSE.offset = new Vector2(3.5f, -40.5f);
            colSE.size = new Vector2(5.0f, 11.0f);

            // West Void Tip: beyond X = -3.0f
            var colW = barriersGo.AddComponent<BoxCollider2D>();
            colW.offset = new Vector2(-4.5f, -34.5f);
            colW.size = new Vector2(3.0f, 3.0f);
        }

        private static void SetupNodeCSafeLightJunction(GameObject consoleAnchor)
        {
            var junctionGo = GameObject.Find("SafeLightJunction_NodeC");
            if (junctionGo == null)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Environment/Interactables/SafeLightJunction.prefab");
                if (prefab != null)
                {
                    junctionGo = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                    junctionGo.name = "SafeLightJunction_NodeC";
                }
            }

            if (junctionGo != null)
            {
                junctionGo.transform.position = new Vector3(0f, -34.5f, 0f);
                var slj = junctionGo.GetComponent<SafeLightJunction>();
                if (slj != null)
                {
                    var so = new SerializedObject(slj);
                    so.FindProperty("junctionName").stringValue = "Node C Emergency Floodlight";
                    so.FindProperty("interactionPromptText").stringValue = "[E] Bật Đèn Khẩn Cấp (5s)";
                    so.FindProperty("activeDuration").floatValue = 5.0f;
                    so.FindProperty("brownoutDuration").floatValue = 1.5f;
                    so.FindProperty("cooldownDuration").floatValue = 15.0f;
                    so.FindProperty("monsterSpeedDebuff").floatValue = 0.8f;
                    so.FindProperty("safeRadius").floatValue = 6.0f;
                    so.FindProperty("interactionRange").floatValue = 1.6f;
                    so.FindProperty("emitterOuterRadius").floatValue = 12.5f;
                    so.FindProperty("emitterInnerRadius").floatValue = 1.0f;
                    so.FindProperty("emitterInnerAngle").floatValue = 32.0f;
                    so.FindProperty("emitterOuterAngle").floatValue = 68.0f;
                    so.FindProperty("emitterLightIntensity").floatValue = 2.2f;
                    so.FindProperty("visualLightRadius").floatValue = 6.0f;
                    so.FindProperty("localFloorLightIntensity").floatValue = 0.45f;
                    if (consoleAnchor != null)
                    {
                        so.FindProperty("interactionAnchor").objectReferenceValue = consoleAnchor.transform;
                    }
                    so.ApplyModifiedProperties();
                }

                var sr = junctionGo.GetComponent<SpriteRenderer>();
                if (sr != null) sr.enabled = false;

                var colliders = junctionGo.GetComponents<CircleCollider2D>();
                foreach (var c in colliders)
                {
                    if (!c.isTrigger)
                    {
                        c.enabled = false;
                    }
                    else
                    {
                        c.radius = 1.6f;
                        c.offset = new Vector2(-2.45f, 0f);
                    }
                }

                // Attach and wire NodeCFloodlightVisuals
                var visuals = junctionGo.GetComponent<NodeCFloodlightVisuals>();
                if (visuals == null) visuals = junctionGo.AddComponent<NodeCFloodlightVisuals>();

                var soVis = new SerializedObject(visuals);
                soVis.FindProperty("junction").objectReferenceValue = slj;
                if (consoleAnchor != null)
                    soVis.FindProperty("consoleRenderer").objectReferenceValue = consoleAnchor.GetComponent<SpriteRenderer>();

                var northGo = GameObject.Find("Prop_NodeC_Gantry_Arch_North");
                if (northGo != null)
                    soVis.FindProperty("northGantryRenderer").objectReferenceValue = northGo.GetComponent<SpriteRenderer>();

                var southGo = GameObject.Find("Prop_NodeC_Gantry_Arch_South");
                if (southGo != null)
                    soVis.FindProperty("southGantryRenderer").objectReferenceValue = southGo.GetComponent<SpriteRenderer>();

                var northBeamGo = GameObject.Find("Prop_NodeC_Gantry_Beam_North");
                if (northBeamGo != null)
                    soVis.FindProperty("northBeamRenderer").objectReferenceValue = northBeamGo.GetComponent<SpriteRenderer>();

                var southBeamGo = GameObject.Find("Prop_NodeC_Gantry_Beam_South");
                if (southBeamGo != null)
                    soVis.FindProperty("southBeamRenderer").objectReferenceValue = southBeamGo.GetComponent<SpriteRenderer>();

                // Configure Twin Overhead Gantry Spotlight Rig
                var centerTr = junctionGo.transform.Find("Light_Center_Amber");
                if (centerTr != null)
                {
                    var l = centerTr.GetComponent<UnityEngine.Rendering.Universal.Light2D>();
                    if (l != null) { l.enabled = false; l.intensity = 0f; }
                }

                var floorTr = junctionGo.transform.Find("Light_LocalFloor");
                if (floorTr != null)
                {
                    var l = floorTr.GetComponent<UnityEngine.Rendering.Universal.Light2D>();
                    if (l != null) { l.intensity = 0.45f; l.pointLightOuterRadius = 6.0f; l.color = new Color(1.0f, 0.90f, 0.70f); }
                }

                var northLightTr = junctionGo.transform.Find("CorridorLight_North");
                if (northLightTr != null)
                {
                    northLightTr.position = new Vector3(0.0f, -29.0f, 0f);
                    northLightTr.rotation = Quaternion.Euler(0f, 0f, 180f); // Angled down towards Crossroads
                    var l = northLightTr.GetComponent<UnityEngine.Rendering.Universal.Light2D>();
                    if (l != null)
                    {
                        l.lightType = UnityEngine.Rendering.Universal.Light2D.LightType.Point;
                        l.pointLightInnerAngle = 32f;
                        l.pointLightOuterAngle = 68f;
                        l.pointLightInnerRadius = 1.0f;
                        l.pointLightOuterRadius = 12.5f;
                        l.color = new Color(1.0f, 0.94f, 0.82f, 1.0f);
                    }
                }

                var southLightTr = junctionGo.transform.Find("CorridorLight_South");
                if (southLightTr != null)
                {
                    southLightTr.position = new Vector3(0.0f, -40.5f, 0f);
                    southLightTr.rotation = Quaternion.Euler(0f, 0f, 0f); // Angled up towards Crossroads
                    var l = southLightTr.GetComponent<UnityEngine.Rendering.Universal.Light2D>();
                    if (l != null)
                    {
                        l.lightType = UnityEngine.Rendering.Universal.Light2D.LightType.Point;
                        l.pointLightInnerAngle = 32f;
                        l.pointLightOuterAngle = 68f;
                        l.pointLightInnerRadius = 1.0f;
                        l.pointLightOuterRadius = 12.5f;
                        l.color = new Color(1.0f, 0.94f, 0.82f, 1.0f);
                    }
                }

                var northLensTr = junctionGo.transform.Find("LensGlow_North");
                if (northLensTr != null) northLensTr.position = new Vector3(0.0f, -29.0f, 0f);

                var southLensTr = junctionGo.transform.Find("LensGlow_South");
                if (southLensTr != null) southLensTr.position = new Vector3(0.0f, -40.5f, 0f);

                soVis.FindProperty("consoleReadySprite").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>($"{PropsSourceDir}/Prop_NodeC_Bridge_Console_Ready.png");
                soVis.FindProperty("consoleActiveSprite").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>($"{PropsSourceDir}/Prop_NodeC_Bridge_Console_Active.png");
                soVis.FindProperty("consoleWarningSprite").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>($"{PropsSourceDir}/Prop_NodeC_Bridge_Console_Warning.png");
                soVis.FindProperty("consoleRechargeSprite").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>($"{PropsSourceDir}/Prop_NodeC_Bridge_Console_Recharge.png");

                soVis.FindProperty("northReadySprite").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>($"{PropsSourceDir}/Prop_NodeC_Floodlight_Gantry_North_READY.png");
                soVis.FindProperty("northActiveSprite").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>($"{PropsSourceDir}/Prop_NodeC_Floodlight_Gantry_North_ACTIVE.png");
                soVis.FindProperty("northWarningSprite").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>($"{PropsSourceDir}/Prop_NodeC_Floodlight_Gantry_North_WARNING.png");
                soVis.FindProperty("northRechargeSprite").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>($"{PropsSourceDir}/Prop_NodeC_Floodlight_Gantry_North_RECHARGE.png");

                soVis.FindProperty("southReadySprite").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>($"{PropsSourceDir}/Prop_NodeC_Floodlight_Gantry_South_READY.png");
                soVis.FindProperty("southActiveSprite").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>($"{PropsSourceDir}/Prop_NodeC_Floodlight_Gantry_South_ACTIVE.png");
                soVis.FindProperty("southWarningSprite").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>($"{PropsSourceDir}/Prop_NodeC_Floodlight_Gantry_South_WARNING.png");
                soVis.FindProperty("southRechargeSprite").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>($"{PropsSourceDir}/Prop_NodeC_Floodlight_Gantry_South_RECHARGE.png");
                soVis.ApplyModifiedProperties();
            }
        }
    }
}
