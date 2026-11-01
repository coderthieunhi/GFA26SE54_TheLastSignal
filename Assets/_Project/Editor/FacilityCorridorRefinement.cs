using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEditor;
using UnityEditor.SceneManagement;
using TheLastSignal.Entities.Environment;

namespace TheLastSignal.EditorTools
{
    public static class FacilityCorridorRefinement
    {
        private const string TileDirMedBay = "Assets/_Project/Tiles/MedBay";
        private const string TileDirNodeB = "Assets/_Project/Tiles/NodeB";
        private const string TileDirConsoleAlpha = "Assets/_Project/Tiles/ConsoleAlpha";

        [MenuItem("Tools/Facility/Refine Corridors and Walls Master Pass", false, 60)]
        public static void RefineAllFacilityCorridorsAndWalls()
        {
            var activeScene = EditorSceneManager.GetActiveScene();
            Debug.Log($"<color=cyan>[FacilityCorridorRefinement] Starting comprehensive pass on scene '{activeScene.name}'...</color>");

            Tilemap floorMap = GameObject.Find("Floor_Tilemap")?.GetComponent<Tilemap>();
            Tilemap wallMap = GameObject.Find("Wall_Tilemap")?.GetComponent<Tilemap>();

            if (floorMap == null || wallMap == null)
            {
                Debug.LogError("[FacilityCorridorRefinement] Could not locate Floor_Tilemap or Wall_Tilemap!");
                return;
            }

            Undo.RecordObject(floorMap, "Refine Corridor Floors");
            Undo.RecordObject(wallMap, "Refine Corridor Walls");

            // 1. Refine Node B Doors and Wall Corners
            RefineNodeBDoors(wallMap);

            // 2. Standardize Lateral Corridors (Node B <-> Mess Hall, Node B <-> Vent Shaft)
            StandardizeLateralCorridors(floorMap, wallMap);

            // 3. Standardize South Horizontal Corridor Leg & East Vertical Corridor
            StandardizeEastCorridorNetwork(floorMap, wallMap);

            // 4. Fill 20-Tile East Corridor Void Gap & Connect to Console Alpha
            FillEastCorridorVoidGap(floorMap, wallMap);

            // 5. Clean up and Standardize Console Alpha Walls
            RefineConsoleAlphaWalls(floorMap, wallMap);

            // Save Scene & Assets
            EditorUtility.SetDirty(floorMap);
            EditorUtility.SetDirty(wallMap);
            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveScene(activeScene);
            AssetDatabase.SaveAssets();

            Debug.Log("<color=green>[FacilityCorridorRefinement] Completed corridor & wall refinement pass successfully!</color>");
        }

        [MenuItem("Tools/Facility/Revert Right L-Corridor", false, 61)]
        public static void RevertRightLCorridor()
        {
            var activeScene = EditorSceneManager.GetActiveScene();
            Debug.Log($"<color=cyan>[FacilityCorridorRefinement] Reverting Right L-Corridor to original Vent Shaft art in scene '{activeScene.name}'...</color>");

            Tilemap floorMap = GameObject.Find("Floor_Tilemap")?.GetComponent<Tilemap>();
            Tilemap wallMap = GameObject.Find("Wall_Tilemap")?.GetComponent<Tilemap>();

            if (floorMap == null || wallMap == null)
            {
                Debug.LogError("[FacilityCorridorRefinement] Floor or Wall Tilemap not found!");
                return;
            }

            Undo.RecordObject(floorMap, "Revert Right L-Corridor Floors");
            Undo.RecordObject(wallMap, "Revert Right L-Corridor Walls");

            string vsTileDir = "Assets/_Project/Tiles/VentShaft";

            // 1. Load Original Vent Shaft Tiles
            Tile fSteel1 = AssetDatabase.LoadAssetAtPath<Tile>($"{vsTileDir}/VS_Floor_Steel_01.asset");
            Tile fSteel2 = AssetDatabase.LoadAssetAtPath<Tile>($"{vsTileDir}/VS_Floor_Steel_02.asset");
            Tile fReinf = AssetDatabase.LoadAssetAtPath<Tile>($"{vsTileDir}/VS_Floor_Reinforced.asset");
            Tile fCorrVWest = AssetDatabase.LoadAssetAtPath<Tile>($"{vsTileDir}/VS_Corridor_Floor_V_West.asset");
            Tile fCorrVEast = AssetDatabase.LoadAssetAtPath<Tile>($"{vsTileDir}/VS_Corridor_Floor_V_East.asset");
            Tile fCorrHNorth = AssetDatabase.LoadAssetAtPath<Tile>($"{vsTileDir}/VS_Corridor_Floor_H_North.asset");
            Tile fCorrHSouth = AssetDatabase.LoadAssetAtPath<Tile>($"{vsTileDir}/VS_Corridor_Floor_H_South.asset");
            Tile fCorrCTL = AssetDatabase.LoadAssetAtPath<Tile>($"{vsTileDir}/VS_Corridor_Floor_Corner_TL.asset");
            Tile fCorrCTR = AssetDatabase.LoadAssetAtPath<Tile>($"{vsTileDir}/VS_Corridor_Floor_Corner_TR.asset");
            Tile fCorrCBL = AssetDatabase.LoadAssetAtPath<Tile>($"{vsTileDir}/VS_Corridor_Floor_Corner_BL.asset");
            Tile fCorrCBR = AssetDatabase.LoadAssetAtPath<Tile>($"{vsTileDir}/VS_Corridor_Floor_Corner_BR.asset");
            Tile fCorrBuckled = AssetDatabase.LoadAssetAtPath<Tile>($"{vsTileDir}/VS_Corridor_Floor_V_Buckled.asset");

            Tile fHazThreshLeft = AssetDatabase.LoadAssetAtPath<Tile>($"{vsTileDir}/VS_Floor_Threshold_Hazard_Left.asset");
            Tile fHazThreshRight = AssetDatabase.LoadAssetAtPath<Tile>($"{vsTileDir}/VS_Floor_Threshold_Hazard_Right.asset");
            Tile fHazThreshH = AssetDatabase.LoadAssetAtPath<Tile>($"{vsTileDir}/VS_Floor_Hazard_Threshold_H.asset");

            Tile wStrH = AssetDatabase.LoadAssetAtPath<Tile>($"{vsTileDir}/VS_Wall_Straight_H.asset");
            Tile wStrV = AssetDatabase.LoadAssetAtPath<Tile>($"{vsTileDir}/VS_Wall_Straight_V.asset");
            Tile wRibVWest = AssetDatabase.LoadAssetAtPath<Tile>($"{vsTileDir}/VS_Wall_Corridor_V_West.asset");
            Tile wRibVEast = AssetDatabase.LoadAssetAtPath<Tile>($"{vsTileDir}/VS_Wall_Corridor_V_East.asset");
            Tile wRibHNorth = AssetDatabase.LoadAssetAtPath<Tile>($"{vsTileDir}/VS_Wall_Corridor_H_North.asset");
            Tile wRibHSouth = AssetDatabase.LoadAssetAtPath<Tile>($"{vsTileDir}/VS_Wall_Corridor_H_South.asset");
            Tile wBracketJunction = AssetDatabase.LoadAssetAtPath<Tile>($"{vsTileDir}/VS_Wall_Corner_Bracket_Junction.asset");
            Tile wBracketL = AssetDatabase.LoadAssetAtPath<Tile>($"{vsTileDir}/VS_Wall_Corner_Bracket_L.asset");
            Tile wCTR = AssetDatabase.LoadAssetAtPath<Tile>($"{vsTileDir}/VS_Wall_Corner_TR.asset");
            Tile wCTL = AssetDatabase.LoadAssetAtPath<Tile>($"{vsTileDir}/VS_Wall_Corner_TL.asset");

            // 2. Clear the Void Extension above Vent Shaft (Y = 3 to 26, X in [20..23])
            for (int y = 3; y <= 26; y++)
            {
                for (int x = 20; x <= 23; x++)
                {
                    // Preserve Console Alpha wall at X = 20 for Y in [22..26]
                    if (x == 20 && y >= 22 && y <= 26) continue;
                    floorMap.SetTile(new Vector3Int(x, y, 0), null);
                    wallMap.SetTile(new Vector3Int(x, y, 0), null);
                }
            }
            floorMap.SetTile(new Vector3Int(21, 2, 0), null);
            floorMap.SetTile(new Vector3Int(22, 2, 0), null);
            wallMap.SetTile(new Vector3Int(22, 2, 0), null);
            wallMap.SetTile(new Vector3Int(23, 2, 0), null);

            // 3. Restore Vent Shaft East Wall at X = 21, Y in [-7..2]
            wallMap.SetTile(new Vector3Int(21, 2, 0), wCTR);
            for (int y = -7; y <= 1; y++)
            {
                wallMap.SetTile(new Vector3Int(21, y, 0), wStrV);
                floorMap.SetTile(new Vector3Int(21, y, 0), null);
                floorMap.SetTile(new Vector3Int(22, y, 0), null);
                wallMap.SetTile(new Vector3Int(22, y, 0), null);
                wallMap.SetTile(new Vector3Int(23, y, 0), null);
            }

            // Restore Vent Shaft interior floor at X = 20
            for (int y = -9; y <= 1; y++)
            {
                wallMap.SetTile(new Vector3Int(20, y, 0), null);
                if (y >= -2 && y <= 1)
                    floorMap.SetTile(new Vector3Int(20, y, 0), fReinf);
                else
                    floorMap.SetTile(new Vector3Int(20, y, 0), ((20 + y) % 2 == 0) ? fSteel1 : fSteel2);
            }
            // South Wall junction at (20, -10)
            wallMap.SetTile(new Vector3Int(20, -10, 0), wBracketJunction);

            // Entrance Floor connecting to corridor: X in [21, 22] at Y = -8
            floorMap.SetTile(new Vector3Int(21, -8, 0), fSteel2);
            floorMap.SetTile(new Vector3Int(22, -8, 0), fSteel1);
            wallMap.SetTile(new Vector3Int(21, -8, 0), null);
            wallMap.SetTile(new Vector3Int(22, -8, 0), null);
            wallMap.SetTile(new Vector3Int(23, -8, 0), wCTR);

            // Hazard Threshold at Y = -9
            floorMap.SetTile(new Vector3Int(21, -9, 0), fHazThreshLeft);
            floorMap.SetTile(new Vector3Int(22, -9, 0), fHazThreshRight);
            wallMap.SetTile(new Vector3Int(23, -9, 0), wRibVEast);

            // 4. Restore Vertical Leg Floors (Y in [-33..-10], X in [21, 22])
            for (int y = -33; y <= -10; y++)
            {
                floorMap.SetTile(new Vector3Int(21, y, 0), fCorrVWest);
                floorMap.SetTile(new Vector3Int(22, y, 0), fCorrVEast);
            }
            if (fCorrBuckled != null)
            {
                floorMap.SetTile(new Vector3Int(21, -21, 0), fCorrBuckled);
            }

            // Vertical Walls
            for (int y = -32; y <= -11; y++)
            {
                wallMap.SetTile(new Vector3Int(20, y, 0), wRibVWest);
            }
            for (int y = -35; y <= -10; y++)
            {
                wallMap.SetTile(new Vector3Int(23, y, 0), wRibVEast);
            }

            // 5. Restore Corner Bend Floors & Wall Brackets (Y in [-35, -34], X in [21, 22])
            floorMap.SetTile(new Vector3Int(21, -34, 0), fCorrCTL);
            floorMap.SetTile(new Vector3Int(22, -34, 0), fCorrCTR);
            floorMap.SetTile(new Vector3Int(21, -35, 0), fCorrCBL);
            floorMap.SetTile(new Vector3Int(22, -35, 0), fCorrCBR);

            wallMap.SetTile(new Vector3Int(20, -33, 0), wBracketL);
            wallMap.SetTile(new Vector3Int(23, -36, 0), wBracketL);

            // 6. Restore Horizontal Leg Floors & Walls (Y in [-35, -34], X in [4..20])
            for (int x = 5; x <= 20; x++)
            {
                floorMap.SetTile(new Vector3Int(x, -34, 0), fCorrHNorth);
                floorMap.SetTile(new Vector3Int(x, -35, 0), fCorrHSouth);
            }
            floorMap.SetTile(new Vector3Int(4, -34, 0), fHazThreshH);
            floorMap.SetTile(new Vector3Int(4, -35, 0), fHazThreshH);

            for (int x = 4; x <= 19; x++)
            {
                wallMap.SetTile(new Vector3Int(x, -33, 0), wRibHNorth);
            }
            for (int x = 4; x <= 22; x++)
            {
                wallMap.SetTile(new Vector3Int(x, -36, 0), wRibHSouth);
            }

            // 7. Enclose Console Alpha at Y in [24, 25], X = 20
            Tile caBulkhead = AssetDatabase.LoadAssetAtPath<Tile>("Assets/_Project/Tiles/ConsoleAlpha/ConsoleAlpha_Wall_Bulkhead.asset");
            if (caBulkhead != null)
            {
                wallMap.SetTile(new Vector3Int(20, 24, 0), caBulkhead);
                wallMap.SetTile(new Vector3Int(20, 25, 0), caBulkhead);
                wallMap.SetTile(new Vector3Int(20, 26, 0), caBulkhead);
            }

            // 8. Remove the 4 lights in the extension corridor
            string[] extensionLights = new string[] {
                "Light_CorridorE_Spine_04",
                "Light_CorridorE_Spine_12",
                "Light_CorridorE_Console_Cyan",
                "Light_CorridorE_Threshold_Amber"
            };
            foreach (var lName in extensionLights)
            {
                var lGo = GameObject.Find(lName);
                if (lGo != null) Undo.DestroyObjectImmediate(lGo);
            }

            floorMap.RefreshAllTiles();
            wallMap.RefreshAllTiles();

            EditorUtility.SetDirty(floorMap);
            EditorUtility.SetDirty(wallMap);
            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveScene(activeScene);
            AssetDatabase.SaveAssets();

            Debug.Log("<color=green>[FacilityCorridorRefinement] Successfully reverted Right L-Corridor to original Vent Shaft art!</color>");
        }

        // =========================================================================
        // 1. NODE B DOORS & CORNER WALL REPOSITIONING
        // =========================================================================
        private static void RefineNodeBDoors(Tilemap wallMap)
        {
            Debug.Log("[FacilityCorridorRefinement] Step 1: Repositioning Node B Doors & Corner Walls...");

            // Door Sprites
            Sprite doorVClosed = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Sprites/Environments/Airlock/Props/Door_Vertical_Closed.png");
            Sprite doorVOpen = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Sprites/Environments/Airlock/Props/Door_Vertical_Open.png");

            // A. Move Door_MessHall to Node B West Threshold: (-5.50, -4.00, 0)
            GameObject doorMess = GameObject.Find("Door_MessHall");
            if (doorMess != null)
            {
                Undo.RecordObject(doorMess.transform, "Reposition Door_MessHall to Node B West");
                doorMess.transform.position = new Vector3(-5.50f, -4.00f, 0f);
                doorMess.transform.rotation = Quaternion.identity;
                doorMess.transform.localScale = Vector3.one;

                var dc = doorMess.GetComponent<DoorController>();
                if (dc != null)
                {
                    Undo.RecordObject(dc, "Configure Door_MessHall Sprites");
                    dc.SetSprites(doorVClosed, doorVOpen);
                    dc.SetColors(Color.white, new Color(0.2f, 0.8f, 0.4f, 0.35f));
                    EditorUtility.SetDirty(dc);
                }

                var sr = doorMess.GetComponent<SpriteRenderer>();
                if (sr != null)
                {
                    Undo.RecordObject(sr, "Configure Door_MessHall SpriteRenderer");
                    sr.sortingLayerName = "Obstacles";
                    sr.sortingOrder = 5;
                    if (doorVClosed != null) sr.sprite = doorVClosed;
                    EditorUtility.SetDirty(sr);
                }

                var col = doorMess.GetComponent<BoxCollider2D>();
                if (col != null)
                {
                    Undo.RecordObject(col, "Configure Door_MessHall Collider");
                    col.size = new Vector2(1.0f, 2.0f);
                    col.offset = Vector2.zero;
                    EditorUtility.SetDirty(col);
                }
                EditorUtility.SetDirty(doorMess);
            }

            // Create or Reposition Indicator_Door_MessHall
            GameObject indMess = GameObject.Find("Indicator_Door_MessHall");
            if (indMess == null)
            {
                indMess = new GameObject("Indicator_Door_MessHall");
                GameObject messDecor = GameObject.Find("MessHall_Decorations");
                if (messDecor == null) messDecor = GameObject.Find("Doors");
                if (messDecor != null) indMess.transform.SetParent(messDecor.transform, false);
                Undo.RegisterCreatedObjectUndo(indMess, "Create Indicator_Door_MessHall");
            }
            indMess.transform.position = new Vector3(-5.50f, -2.85f, 0f);
            indMess.transform.localScale = new Vector3(0.5f, 0.5f, 1.0f);

            Sprite redInd = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Sprites/Environments/MedBay/Props/Door_MedBay_Light_Red.png");
            Sprite greenInd = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Sprites/Environments/MedBay/Props/Door_MedBay_Light_Green.png");

            var srInd = indMess.GetComponent<SpriteRenderer>();
            if (srInd == null) srInd = indMess.AddComponent<SpriteRenderer>();
            srInd.sprite = redInd;
            srInd.sortingLayerName = "Obstacles";
            srInd.sortingOrder = 6;

            var lightInd = indMess.GetComponent<UnityEngine.Rendering.Universal.Light2D>();
            if (lightInd == null) lightInd = indMess.AddComponent<UnityEngine.Rendering.Universal.Light2D>();
            lightInd.lightType = UnityEngine.Rendering.Universal.Light2D.LightType.Point;
            lightInd.color = new Color(1.0f, 0.18f, 0.18f, 1f);
            lightInd.intensity = 0.75f;
            lightInd.pointLightOuterRadius = 1.4f;

            var dilMess = indMess.GetComponent<DoorIndicatorLight>();
            if (dilMess == null) dilMess = indMess.AddComponent<DoorIndicatorLight>();
            if (doorMess != null)
            {
                var dcMess = doorMess.GetComponent<DoorController>();
                var fieldDoor = typeof(DoorIndicatorLight).GetField("targetDoor", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (fieldDoor != null) fieldDoor.SetValue(dilMess, dcMess);
            }
            EditorUtility.SetDirty(indMess);

            // B. Move Door_VentShaft to Node B East Threshold: (4.50, -4.00, 0)
            GameObject doorVent = GameObject.Find("Door_VentShaft");
            if (doorVent != null)
            {
                Undo.RecordObject(doorVent.transform, "Reposition Door_VentShaft to Node B East");
                doorVent.transform.position = new Vector3(4.50f, -4.00f, 0f);
                doorVent.transform.rotation = Quaternion.identity;
                doorVent.transform.localScale = Vector3.one;

                var dc = doorVent.GetComponent<DoorController>();
                if (dc != null)
                {
                    Undo.RecordObject(dc, "Configure Door_VentShaft Sprites");
                    dc.SetSprites(doorVClosed, doorVOpen);
                    dc.SetColors(Color.white, new Color(0.2f, 0.8f, 0.4f, 0.35f));
                    EditorUtility.SetDirty(dc);
                }

                var sr = doorVent.GetComponent<SpriteRenderer>();
                if (sr != null)
                {
                    Undo.RecordObject(sr, "Configure Door_VentShaft SpriteRenderer");
                    sr.sortingLayerName = "Obstacles";
                    sr.sortingOrder = 5;
                    if (doorVClosed != null) sr.sprite = doorVClosed;
                    EditorUtility.SetDirty(sr);
                }

                var col = doorVent.GetComponent<BoxCollider2D>();
                if (col != null)
                {
                    Undo.RecordObject(col, "Configure Door_VentShaft Collider");
                    col.size = new Vector2(1.0f, 2.0f);
                    col.offset = Vector2.zero;
                    EditorUtility.SetDirty(col);
                }
                EditorUtility.SetDirty(doorVent);
            }

            // Move Indicator_Door_VentShaft to (4.50, -2.85, 0)
            GameObject indVent = GameObject.Find("Indicator_Door_VentShaft");
            if (indVent != null)
            {
                Undo.RecordObject(indVent.transform, "Reposition Indicator_Door_VentShaft");
                indVent.transform.position = new Vector3(4.50f, -2.85f, 0f);
                EditorUtility.SetDirty(indVent);
            }

            // C. Fix Node B 4 doorway corner tiles
            Tile wCornerTL = AssetDatabase.LoadAssetAtPath<Tile>($"{TileDirNodeB}/NodeB_Wall_Corner_TL.asset");
            Tile wCornerBL = AssetDatabase.LoadAssetAtPath<Tile>($"{TileDirNodeB}/NodeB_Wall_Corner_BL.asset");
            Tile wCornerTR = AssetDatabase.LoadAssetAtPath<Tile>($"{TileDirNodeB}/NodeB_Wall_Corner_TR.asset");
            Tile wCornerBR = AssetDatabase.LoadAssetAtPath<Tile>($"{TileDirNodeB}/NodeB_Wall_Corner_BR.asset");

            if (wCornerTL != null) wallMap.SetTile(new Vector3Int(-6, -3, 0), wCornerTL);
            if (wCornerBL != null) wallMap.SetTile(new Vector3Int(-6, -6, 0), wCornerBL);
            if (wCornerTR != null) wallMap.SetTile(new Vector3Int(5, -3, 0), wCornerTR);
            if (wCornerBR != null) wallMap.SetTile(new Vector3Int(5, -6, 0), wCornerBR);
        }

        // =========================================================================
        // 2. STANDARDIZE LATERAL CORRIDORS TO MEDBAY DARK STEEL & PIPES
        // =========================================================================
        private static void StandardizeLateralCorridors(Tilemap floorMap, Tilemap wallMap)
        {
            Debug.Log("[FacilityCorridorRefinement] Step 2: Standardizing Lateral Corridors...");

            Tile fDark = AssetDatabase.LoadAssetAtPath<Tile>($"{TileDirMedBay}/MedBay_Floor_Dark.asset");
            Tile wPipesH = AssetDatabase.LoadAssetAtPath<Tile>($"{TileDirMedBay}/MedBay_Wall_Pipes_Clean_H.asset");
            Tile wPipesNorth = AssetDatabase.LoadAssetAtPath<Tile>($"{TileDirMedBay}/MedBay_Wall_Pipes_Clean_North.asset");

            // West Lateral Corridor: X in [-10..-6], Y in [-5, -4]
            for (int x = -10; x <= -6; x++)
            {
                floorMap.SetTile(new Vector3Int(x, -4, 0), fDark);
                floorMap.SetTile(new Vector3Int(x, -5, 0), fDark);
            }
            for (int x = -10; x <= -7; x++)
            {
                wallMap.SetTile(new Vector3Int(x, -3, 0), wPipesNorth);
                wallMap.SetTile(new Vector3Int(x, -6, 0), wPipesH);
            }

            // East Lateral Corridor: X in [5..10], Y in [-5, -4]
            for (int x = 5; x <= 10; x++)
            {
                floorMap.SetTile(new Vector3Int(x, -4, 0), fDark);
                floorMap.SetTile(new Vector3Int(x, -5, 0), fDark);
            }
            for (int x = 6; x <= 10; x++)
            {
                wallMap.SetTile(new Vector3Int(x, -3, 0), wPipesNorth);
                wallMap.SetTile(new Vector3Int(x, -6, 0), wPipesH);
            }
        }

        // =========================================================================
        // 3. STANDARDIZE EAST CORRIDOR NETWORK & SOUTH BOTTOM LEG
        // =========================================================================
        private static void StandardizeEastCorridorNetwork(Tilemap floorMap, Tilemap wallMap)
        {
            Debug.Log("[FacilityCorridorRefinement] Step 3: Standardizing East Corridor Loop & South Horizontal Leg...");

            Tile fDark = AssetDatabase.LoadAssetAtPath<Tile>($"{TileDirMedBay}/MedBay_Floor_Dark.asset");
            Tile wPipesH = AssetDatabase.LoadAssetAtPath<Tile>($"{TileDirMedBay}/MedBay_Wall_Pipes_Clean_H.asset");
            Tile wPipesNorth = AssetDatabase.LoadAssetAtPath<Tile>($"{TileDirMedBay}/MedBay_Wall_Pipes_Clean_North.asset");
            Tile wPipesV = AssetDatabase.LoadAssetAtPath<Tile>($"{TileDirMedBay}/MedBay_Wall_Pipes_Clean_V.asset");
            Tile wPipesVEast = AssetDatabase.LoadAssetAtPath<Tile>($"{TileDirMedBay}/MedBay_Wall_Pipes_Clean_V_East.asset");
            Tile wCornerBR = AssetDatabase.LoadAssetAtPath<Tile>($"{TileDirMedBay}/MedBay_Wall_Corner_BR.asset");
            Tile wInnerCornerBR = AssetDatabase.LoadAssetAtPath<Tile>($"{TileDirMedBay}/MedBay_Wall_Inner_Corner_BR.asset");

            // Horizontal Bottom Leg: X in [5..20], Y in [-35, -34]
            for (int x = 5; x <= 20; x++)
            {
                floorMap.SetTile(new Vector3Int(x, -34, 0), fDark);
                floorMap.SetTile(new Vector3Int(x, -35, 0), fDark);
                wallMap.SetTile(new Vector3Int(x, -33, 0), wPipesNorth);
                wallMap.SetTile(new Vector3Int(x, -36, 0), wPipesH);
            }

            // Corner Bend at X in [21, 22], Y in [-35, -34]
            floorMap.SetTile(new Vector3Int(21, -34, 0), fDark);
            floorMap.SetTile(new Vector3Int(22, -34, 0), fDark);
            floorMap.SetTile(new Vector3Int(21, -35, 0), fDark);
            floorMap.SetTile(new Vector3Int(22, -35, 0), fDark);

            wallMap.SetTile(new Vector3Int(21, -36, 0), wPipesH);
            wallMap.SetTile(new Vector3Int(22, -36, 0), wPipesH);
            wallMap.SetTile(new Vector3Int(23, -36, 0), wCornerBR);
            wallMap.SetTile(new Vector3Int(23, -35, 0), wPipesVEast);
            wallMap.SetTile(new Vector3Int(23, -34, 0), wPipesVEast);
            wallMap.SetTile(new Vector3Int(20, -33, 0), wInnerCornerBR);

            // Vertical Leg from Y = -33 up to Y = -9
            for (int y = -33; y <= -9; y++)
            {
                floorMap.SetTile(new Vector3Int(21, y, 0), fDark);
                floorMap.SetTile(new Vector3Int(22, y, 0), fDark);

                wallMap.SetTile(new Vector3Int(23, y, 0), wPipesVEast);
                wallMap.SetTile(new Vector3Int(20, y, 0), wPipesV);
            }
        }

        // =========================================================================
        // 4. FILL 20-TILE EAST CORRIDOR VOID GAP & CONNECT TO CONSOLE ALPHA
        // =========================================================================
        private static void FillEastCorridorVoidGap(Tilemap floorMap, Tilemap wallMap)
        {
            Debug.Log("[FacilityCorridorRefinement] Step 4: Completing Missing East Corridor Gap (Y = -8 to Y = 25)...");

            Tile fDark = AssetDatabase.LoadAssetAtPath<Tile>($"{TileDirMedBay}/MedBay_Floor_Dark.asset");
            Tile wPipesV = AssetDatabase.LoadAssetAtPath<Tile>($"{TileDirMedBay}/MedBay_Wall_Pipes_Clean_V.asset");
            Tile wPipesVEast = AssetDatabase.LoadAssetAtPath<Tile>($"{TileDirMedBay}/MedBay_Wall_Pipes_Clean_V_East.asset");
            Tile wPipesNorth = AssetDatabase.LoadAssetAtPath<Tile>($"{TileDirMedBay}/MedBay_Wall_Pipes_Clean_North.asset");
            Tile wCornerTR = AssetDatabase.LoadAssetAtPath<Tile>($"{TileDirMedBay}/MedBay_Wall_Corner_TR.asset");
            Tile wCornerTL = AssetDatabase.LoadAssetAtPath<Tile>($"{TileDirMedBay}/MedBay_Wall_Corner_TL.asset");

            // Fill Continuous Floor across X = 21, 22 from Y = -8 all the way up to Y = 25
            for (int y = -8; y <= 25; y++)
            {
                floorMap.SetTile(new Vector3Int(21, y, 0), fDark);
                floorMap.SetTile(new Vector3Int(22, y, 0), fDark);

                // Ensure corridor floor has NO wall obstructing it
                wallMap.SetTile(new Vector3Int(21, y, 0), null);
                wallMap.SetTile(new Vector3Int(22, y, 0), null);

                // East Wall at X = 23 continuously
                wallMap.SetTile(new Vector3Int(23, y, 0), wPipesVEast);
            }

            // West Wall at X = 20 from Y = -7 to Y = 23
            for (int y = -7; y <= 23; y++)
            {
                // Clear any subfloor under the wall at X = 20 (from previous room bounds)
                floorMap.SetTile(new Vector3Int(20, y, 0), null);

                // Y = -8 is the chokepoint doorway from Vent Shaft, keep open
                wallMap.SetTile(new Vector3Int(20, y, 0), wPipesV);
            }

            // Doorway opening into Console Alpha at X = 20, Y in [24, 25]
            wallMap.SetTile(new Vector3Int(20, 24, 0), null);
            wallMap.SetTile(new Vector3Int(20, 25, 0), null);

            // Top North Wall Cap at Y = 26 (matching West Corridor symmetry)
            wallMap.SetTile(new Vector3Int(20, 26, 0), wCornerTL);
            wallMap.SetTile(new Vector3Int(21, 26, 0), wPipesNorth);
            wallMap.SetTile(new Vector3Int(22, 26, 0), wPipesNorth);
            wallMap.SetTile(new Vector3Int(23, 26, 0), wCornerTR);
        }

        // =========================================================================
        // 5. REFINE & CLEAN UP CONSOLE ALPHA WALLS
        // =========================================================================
        private static void RefineConsoleAlphaWalls(Tilemap floorMap, Tilemap wallMap)
        {
            Debug.Log("[FacilityCorridorRefinement] Step 5: Replacing Bedroom walls in Console Alpha with Authentic High-Tech Assets...");

            Tile wBulkhead = AssetDatabase.LoadAssetAtPath<Tile>($"{TileDirConsoleAlpha}/ConsoleAlpha_Wall_Bulkhead.asset");
            Tile wCableTrunk = AssetDatabase.LoadAssetAtPath<Tile>($"{TileDirConsoleAlpha}/ConsoleAlpha_Wall_Cable_Trunk.asset");
            Tile wCorner = AssetDatabase.LoadAssetAtPath<Tile>($"{TileDirConsoleAlpha}/ConsoleAlpha_Wall_Corner.asset");
            Tile wMainframe = AssetDatabase.LoadAssetAtPath<Tile>($"{TileDirConsoleAlpha}/ConsoleAlpha_Wall_Mainframe_Panel.asset");

            // South Wall of Console Alpha: Y = 22, X in [11..20]
            for (int x = 11; x <= 20; x++)
            {
                Vector3Int pos = new Vector3Int(x, 22, 0);
                if (x == 18 || x == 19)
                {
                    wallMap.SetTile(pos, wCableTrunk != null ? wCableTrunk : wBulkhead);
                }
                else
                {
                    wallMap.SetTile(pos, wBulkhead);
                }
            }

            // West Wall of Console Alpha: X = 10, Y in [23..32]
            for (int y = 23; y <= 32; y++)
            {
                Vector3Int pos = new Vector3Int(10, y, 0);
                if (y == 27 || y == 28)
                {
                    wallMap.SetTile(pos, null); // West Doorway
                }
                else
                {
                    wallMap.SetTile(pos, wBulkhead);
                }
            }

            // Corners:
            wallMap.SetTile(new Vector3Int(10, 22, 0), wCorner != null ? wCorner : wBulkhead);
            wallMap.SetTile(new Vector3Int(10, 33, 0), wCorner != null ? wCorner : wBulkhead);

            // Clean up any stray Bedroom walls at X = 21, Y in [22..33]
            // At Y = 22..25, X = 21 is now Corridor floor, wallMap is null
            for (int y = 22; y <= 25; y++)
            {
                wallMap.SetTile(new Vector3Int(21, y, 0), null);
            }
            // For Y in [26..32], X = 20 is the solid bulkhead wall of Console Alpha
            for (int y = 26; y <= 32; y++)
            {
                wallMap.SetTile(new Vector3Int(20, y, 0), wBulkhead);
                // X = 21 is outside Console Alpha
                wallMap.SetTile(new Vector3Int(21, y, 0), null);
            }
            // Top corner at (20, 33)
            wallMap.SetTile(new Vector3Int(20, 33, 0), wCorner != null ? wCorner : wBulkhead);
            wallMap.SetTile(new Vector3Int(21, 33, 0), null);

            // Reposition ConsoleAlpha Aux Terminal inside Console Alpha against West of wall 20
            GameObject auxTerm = GameObject.Find("Prop_ConsoleAlpha_Aux_Terminal");
            if (auxTerm != null)
            {
                Undo.RecordObject(auxTerm.transform, "Align Console Alpha Aux Terminal");
                auxTerm.transform.position = new Vector3(19.35f, 28.00f, 0f);
                EditorUtility.SetDirty(auxTerm);
            }
            GameObject auxLight = GameObject.Find("Light_ConsoleAlpha_Aux_Amber");
            if (auxLight != null)
            {
                Undo.RecordObject(auxLight.transform, "Align Console Alpha Aux Amber Light");
                auxLight.transform.position = new Vector3(19.35f, 28.20f, 0f);
                EditorUtility.SetDirty(auxLight);
            }
        }
    }
}
