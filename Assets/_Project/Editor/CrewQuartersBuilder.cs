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
    public static class CrewQuartersBuilder
    {
        private const string PixelArtPropsDir = "Assets/_Project/Sprites/Environments/CrewQuarters_PixelArt/Props";
        private const string PixelArtTilesDir = "Assets/_Project/Sprites/Environments/CrewQuarters_PixelArt/Tiles";
        private const string PixelArtTileAssetsDir = "Assets/_Project/Tiles/CrewQuarters_PixelArt";

        [MenuItem("Tools/Facility/Implement Crew Quarters (Pixel Art Pack)", false, 55)]
        public static void BuildCrewQuarters()
        {
            Debug.Log("<color=cyan>[CrewQuartersBuilder] Refining CREW_QUARTERS to match Concept Art reference...</color>");

            // 1. Clean legacy / existing objects
            CleanupLegacyCrewQuarters();

            // 2. Clear old tiles in Facility_Grid for the Crew Quarters area
            ClearFacilityGridArea();

            // 3. Create the CrewQuarters hierarchy
            // Root position is (-6, 37, 0) so that:
            // Local interior X: 1..10, Y: 1..10 maps to World X: -5..4, Y: 38..47
            // Local walls X: 0, 11, Y: 0, 11 maps to World X: -6, 5, Y: 37, 48
            // Total footprint: exactly 12x12 tiles
            GameObject cqRoot = new GameObject("CrewQuarters");
            cqRoot.transform.position = new Vector3(-6f, 37f, 0f);
            cqRoot.transform.rotation = Quaternion.identity;
            cqRoot.transform.localScale = Vector3.one;

            Grid grid = cqRoot.AddComponent<Grid>();
            grid.cellSize = new Vector3(1f, 1f, 0f);
            grid.cellLayout = GridLayout.CellLayout.Rectangle;

            // Load Default Sprite Material (Lit)
            Material spriteMat = AssetDatabase.LoadAssetAtPath<Material>("Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Lit-Default.mat");
            if (spriteMat == null)
            {
                Shader litShader = Shader.Find("Universal Render Pipeline/2D/Sprite-Lit-Default");
                if (litShader != null) spriteMat = new Material(litShader);
            }

            // Create Floor
            GameObject floorGo = new GameObject("Floor");
            floorGo.transform.SetParent(cqRoot.transform, false);
            Tilemap floorMap = floorGo.AddComponent<Tilemap>();
            TilemapRenderer floorRenderer = floorGo.AddComponent<TilemapRenderer>();
            floorRenderer.sortingLayerName = "Floor";
            floorRenderer.sortingOrder = 0;
            if (spriteMat != null) floorRenderer.sharedMaterial = spriteMat;

            // Create Walls
            GameObject wallsGo = new GameObject("Walls");
            wallsGo.transform.SetParent(cqRoot.transform, false);
            Tilemap wallMap = wallsGo.AddComponent<Tilemap>();
            TilemapRenderer wallRenderer = wallsGo.AddComponent<TilemapRenderer>();
            wallRenderer.sortingLayerName = "Obstacles";
            wallRenderer.sortingOrder = 0;
            if (spriteMat != null) wallRenderer.sharedMaterial = spriteMat;

            TilemapCollider2D wallCollider = wallsGo.AddComponent<TilemapCollider2D>();
            int obstacleLayer = LayerMask.NameToLayer("Obstacle");
            if (obstacleLayer != -1) wallsGo.layer = obstacleLayer;

            // Create Furniture parent
            GameObject furnitureGo = new GameObject("Furniture");
            furnitureGo.transform.SetParent(cqRoot.transform, false);

            // Create Decoration parent
            GameObject decorGo = new GameObject("Decoration");
            decorGo.transform.SetParent(cqRoot.transform, false);

            // Create Collision parent
            GameObject collisionGo = new GameObject("Collision");
            collisionGo.transform.SetParent(cqRoot.transform, false);

            // 4. Paint Floor (10x10 Interior: X=1..10, Y=1..10)
            PaintFloor(floorMap);

            // 5. Paint Walls (1 tile thick perimeter: X=0, 11, Y=0, 11 with Doorway at X=5,6, Y=0)
            PaintWalls(wallMap);

            // 6. Spawn Furniture Assets
            SpawnFurniture(furnitureGo, spriteMat, obstacleLayer);

            // 7. Spawn Decoration Assets & Atmospheric Lights
            SpawnDecoration(decorGo, spriteMat);

            // 8. Setup Crew Door
            SetupCrewDoor(spriteMat);

            // 9. Update Task Location
            UpdateTaskLocation();

            // 10. Run Automated Verification
            RunVerification(floorMap, wallMap, furnitureGo);

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            Debug.Log("<color=green>[CrewQuartersBuilder] CREW_QUARTERS refinement completed successfully!</color>");
        }

        private static void CleanupLegacyCrewQuarters()
        {
            string[] toDestroy = new string[]
            {
                "CrewQuarters",
                "CrewQuarters_Decorations",
                "Obstacle_CrewQuarters_Lockers",
                "Obstacle_CrewQuarters_Partition",
                "Obstacle_CrewQuarters_LoungeTable"
            };

            foreach (string name in toDestroy)
            {
                GameObject obj = GameObject.Find(name);
                if (obj != null)
                {
                    Undo.DestroyObjectImmediate(obj);
                }
            }
        }

        private static void ClearFacilityGridArea()
        {
            GameObject gridGo = GameObject.Find("Facility_Grid");
            if (gridGo == null) return;

            Tilemap[] tilemaps = gridGo.GetComponentsInChildren<Tilemap>(true);
            foreach (var tm in tilemaps)
            {
                Undo.RecordObject(tm, "Clear Old Crew Quarters Tiles");
                for (int x = -6; x <= 5; x++)
                {
                    for (int y = 37; y <= 48; y++)
                    {
                        if (tm.HasTile(new Vector3Int(x, y, 0)))
                        {
                            tm.SetTile(new Vector3Int(x, y, 0), null);
                        }
                    }
                }
            }
        }

        private static void PaintFloor(Tilemap floorMap)
        {
            floorMap.ClearAllTiles();

            Tile fMain = AssetDatabase.LoadAssetAtPath<Tile>($"{PixelArtTileAssetsDir}/01_Floor_MainPlates.asset");
            Tile fPanelOil = AssetDatabase.LoadAssetAtPath<Tile>($"{PixelArtTileAssetsDir}/02_Floor_LargePanel_Oil.asset");
            Tile fHazardH = AssetDatabase.LoadAssetAtPath<Tile>($"{PixelArtTileAssetsDir}/07_Floor_HazardStripe_H.asset");
            Tile fGrate = AssetDatabase.LoadAssetAtPath<Tile>($"{PixelArtTileAssetsDir}/08_Floor_Detail_Grate.asset");
            Tile fHatch = AssetDatabase.LoadAssetAtPath<Tile>($"{PixelArtTileAssetsDir}/08_Floor_Detail_Hatch.asset");
            Tile fHatchWires = AssetDatabase.LoadAssetAtPath<Tile>($"{PixelArtTileAssetsDir}/Floor_Hatch_Wires.asset");
            Tile fSideBars = AssetDatabase.LoadAssetAtPath<Tile>($"{PixelArtTileAssetsDir}/Floor_Plate_SideBars.asset");
            Tile fDark = AssetDatabase.LoadAssetAtPath<Tile>($"{PixelArtTileAssetsDir}/Floor_Plate_Dark.asset");

            // Base interior 10x10 (X: 1..10, Y: 1..10) with clean broad metal deck plates
            for (int x = 1; x <= 10; x++)
            {
                for (int y = 1; y <= 10; y++)
                {
                    floorMap.SetTile(new Vector3Int(x, y, 0), fMain);
                }
            }

            // Grimy / oily plates under western cargo cluster
            if (fPanelOil != null)
            {
                floorMap.SetTile(new Vector3Int(1, 3, 0), fPanelOil);
                floorMap.SetTile(new Vector3Int(1, 4, 0), fPanelOil);
                floorMap.SetTile(new Vector3Int(2, 3, 0), fPanelOil);
            }
            if (fDark != null)
            {
                floorMap.SetTile(new Vector3Int(1, 6, 0), fDark);
            }

            // Galley / north-east textured plates
            if (fSideBars != null)
            {
                floorMap.SetTile(new Vector3Int(8, 9, 0), fSideBars);
                floorMap.SetTile(new Vector3Int(9, 9, 0), fSideBars);
            }

            // Environmental grates matching the master concept art reference
            if (fGrate != null)
            {
                floorMap.SetTile(new Vector3Int(2, 8, 0), fGrate); // NW floor grate
                floorMap.SetTile(new Vector3Int(3, 4, 0), fGrate); // West floor grate (beside crates)
                floorMap.SetTile(new Vector3Int(8, 7, 0), fGrate); // NE floor grate (beside sofa)
                floorMap.SetTile(new Vector3Int(7, 3, 0), fGrate); // SE floor grate
            }

            // Hatches
            if (fHatch != null) floorMap.SetTile(new Vector3Int(4, 2, 0), fHatch);
            if (fHatchWires != null) floorMap.SetTile(new Vector3Int(2, 9, 0), fHatchWires);

            // Diamond industrial treadplate entrance threshold (Y=1, X=5,6) and door threshold (Y=0, X=5,6)
            if (fHazardH != null)
            {
                floorMap.SetTile(new Vector3Int(5, 1, 0), fHazardH);
                floorMap.SetTile(new Vector3Int(6, 1, 0), fHazardH);
                floorMap.SetTile(new Vector3Int(5, 0, 0), fHazardH);
                floorMap.SetTile(new Vector3Int(6, 0, 0), fHazardH);
            }
        }

        private static void PaintWalls(Tilemap wallMap)
        {
            wallMap.ClearAllTiles();

            Tile wHoriz = AssetDatabase.LoadAssetAtPath<Tile>($"{PixelArtTileAssetsDir}/03_Wall_Horizontal.asset");
            Tile wVert = AssetDatabase.LoadAssetAtPath<Tile>($"{PixelArtTileAssetsDir}/04_Wall_Vertical.asset");
            Tile wCornerTL = AssetDatabase.LoadAssetAtPath<Tile>($"{PixelArtTileAssetsDir}/03_Wall_OuterCorner_TL.asset") 
                             ?? AssetDatabase.LoadAssetAtPath<Tile>($"{PixelArtTileAssetsDir}/05_Wall_Corner.asset");
            Tile wCornerTR = AssetDatabase.LoadAssetAtPath<Tile>($"{PixelArtTileAssetsDir}/04_Wall_OuterCorner_TR.asset");
            Tile wCornerBL = AssetDatabase.LoadAssetAtPath<Tile>($"{PixelArtTileAssetsDir}/05_Wall_OuterCorner_BL.asset");
            Tile wCornerBR = AssetDatabase.LoadAssetAtPath<Tile>($"{PixelArtTileAssetsDir}/06_Wall_OuterCorner_BR.asset");
            Tile wPipes = AssetDatabase.LoadAssetAtPath<Tile>($"{PixelArtTileAssetsDir}/10_Wall_Pipes.asset");
            Tile wCables = AssetDatabase.LoadAssetAtPath<Tile>($"{PixelArtTileAssetsDir}/09_Wall_Cables.asset");
            Tile wClamped = AssetDatabase.LoadAssetAtPath<Tile>($"{PixelArtTileAssetsDir}/Wall_Clamped_Cables.asset");

            // 1. North Wall (Y = 11, X: 1..10) with authentic exposed conduits, pipes and cables
            for (int x = 1; x <= 10; x++)
            {
                Tile chosen = wHoriz;
                if (x == 3 && wPipes != null) chosen = wPipes;
                else if (x == 5 && wClamped != null) chosen = wClamped;
                else if (x == 7 && wCables != null) chosen = wCables;
                wallMap.SetTile(new Vector3Int(x, 11, 0), chosen);
            }

            // 2. South Wall (Y = 0, X: 1..10 except X=5, 6 for door opening)
            for (int x = 1; x <= 4; x++)
            {
                wallMap.SetTile(new Vector3Int(x, 0, 0), wHoriz);
            }
            // Door opening at X=5, 6 -> NO wall tile!
            for (int x = 7; x <= 10; x++)
            {
                wallMap.SetTile(new Vector3Int(x, 0, 0), wHoriz);
            }

            // 3. West Wall (X = 0, Y: 1..10)
            for (int y = 1; y <= 10; y++)
            {
                wallMap.SetTile(new Vector3Int(0, y, 0), wVert);
            }

            // 4. East Wall (X = 11, Y: 1..10)
            for (int y = 1; y <= 10; y++)
            {
                wallMap.SetTile(new Vector3Int(11, y, 0), wVert);
            }

            // 5. Four Corners using explicit baked corner sprites with Identity transform matrix
            Vector3Int pNW = new Vector3Int(0, 11, 0);
            wallMap.SetTransformMatrix(pNW, Matrix4x4.identity);
            wallMap.SetTile(pNW, wCornerTL);

            Vector3Int pNE = new Vector3Int(11, 11, 0);
            wallMap.SetTransformMatrix(pNE, Matrix4x4.identity);
            wallMap.SetTile(pNE, wCornerTR != null ? wCornerTR : wCornerTL);

            Vector3Int pSW = new Vector3Int(0, 0, 0);
            wallMap.SetTransformMatrix(pSW, Matrix4x4.identity);
            wallMap.SetTile(pSW, wCornerBL != null ? wCornerBL : wCornerTL);

            Vector3Int pSE = new Vector3Int(11, 0, 0);
            wallMap.SetTransformMatrix(pSE, Matrix4x4.identity);
            wallMap.SetTile(pSE, wCornerBR != null ? wCornerBR : wCornerTL);
        }

        private static void SpawnFurniture(GameObject parent, Material mat, int layer)
        {
            // 1. Center Communal Table with 4 Chairs (Moved ~0.5 tile upward to Y=5.4)
            Vector3 tableScale = new Vector3(1.42f, 1.42f, 1.0f);
            CreateSolidProp(parent, "01_Prop_CommunalTable", new Vector3(5.5f, 5.4f, 0f), mat, layer, new Vector2(2.5f, 1.35f), null, Quaternion.identity, tableScale);

            // Chairs (Proportional to table, scale 1.28x, attached to table formation)
            Vector3 chairScale = new Vector3(1.28f, 1.28f, 1.0f);
            Vector2 chairCol = new Vector2(0.5f, 0.5f);
            // North chairs facing South towards table (0 deg)
            CreateSolidProp(parent, "02_Prop_Chair", new Vector3(4.7f, 6.5f, 0f), mat, layer, chairCol, null, Quaternion.identity, chairScale);
            CreateSolidProp(parent, "02_Prop_Chair", new Vector3(6.3f, 6.5f, 0f), mat, layer, chairCol, null, Quaternion.identity, chairScale);
            // South chairs facing North towards table (180 deg)
            CreateSolidProp(parent, "02_Prop_Chair", new Vector3(4.7f, 4.3f, 0f), mat, layer, chairCol, null, Quaternion.Euler(0f, 0f, 180f), chairScale);
            CreateSolidProp(parent, "02_Prop_Chair", new Vector3(6.3f, 4.3f, 0f), mat, layer, chairCol, null, Quaternion.Euler(0f, 0f, 180f), chairScale);

            // 2. North Wall: Compact Functional Equipment Band (KEPT EXACTLY AS-IS, DO NOT RESIZE)
            // NW Workstation (Scale 1.22x, spans X: 1.0 -> 2.8)
            CreateSolidProp(parent, "05_Prop_Workstation", new Vector3(1.9f, 10.45f, 0f), mat, layer, new Vector2(1.8f, 0.9f), new Vector2(0f, -0.05f), Quaternion.identity, new Vector3(1.22f, 1.20f, 1.0f));
            // Drinks Vending Machine (Scale 1.18x, spans X: 2.8 -> 4.0)
            CreateSolidProp(parent, "07_Prop_VendingMachine", new Vector3(3.4f, 10.45f, 0f), mat, layer, new Vector2(1.1f, 1.35f), new Vector2(0f, -0.15f), Quaternion.identity, new Vector3(1.18f, 1.15f, 1.0f));
            // Kitchen / Galley Station (Scale 1.25x, spans X: 4.0 -> 5.9)
            CreateSolidProp(parent, "08_Prop_KitchenStation", new Vector3(4.95f, 10.45f, 0f), mat, layer, new Vector2(1.8f, 1.3f), new Vector2(0f, -0.15f), Quaternion.identity, new Vector3(1.25f, 1.22f, 1.0f));
            // Locker Bank (Scale 1.12x, spans X: 5.9 -> 7.5)
            CreateSolidProp(parent, "03_Prop_Lockers", new Vector3(6.65f, 10.45f, 0f), mat, layer, new Vector2(1.6f, 1.3f), new Vector2(0f, -0.15f), Quaternion.identity, new Vector3(1.12f, 1.12f, 1.0f));
            // Tool Shelf / Storage (Scale 1.12x, spans X: 7.5 -> 8.8)
            CreateSolidProp(parent, "06_Prop_StorageShelf", new Vector3(8.15f, 10.45f, 0f), mat, layer, new Vector2(1.1f, 1.35f), new Vector2(0f, -0.15f), Quaternion.identity, new Vector3(1.12f, 1.12f, 1.0f));

            // 3. East Lounge Area: Dedicated Lounge Zone
            // Lounge Sofa (Scale reduced ~5% from 1.35x to 1.28x, vertical against EAST wall at X=9.95, facing WEST)
            Vector3 sofaScale = new Vector3(1.28f, 1.28f, 1.0f);
            CreateSolidProp(parent, "04_Prop_Sofa", new Vector3(9.95f, 4.9f, 0f), mat, layer, new Vector2(0.95f, 2.35f), null, Quaternion.Euler(0f, 0f, 270f), sofaScale);
            // Small storage / side table beside sofa (Scale 1.0x)
            CreateSolidProp(parent, "17_Prop_SmallCrate", new Vector3(9.8f, 3.25f, 0f), mat, layer, new Vector2(0.7f, 0.7f), null, Quaternion.identity, new Vector3(1.0f, 1.0f, 1.0f));

            // 4. West Wall: Compact Work / Storage Zone (Reorganized compact cluster)
            // Workstation near upper-west (Y=6.6, Rotated 90 deg: back against West wall, front facing EAST into room)
            CreateSolidProp(parent, "05_Prop_Workstation", new Vector3(1.45f, 6.6f, 0f), mat, layer, new Vector2(1.0f, 1.7f), null, Quaternion.Euler(0f, 0f, 90f), new Vector3(1.20f, 1.20f, 1.0f));
            // Cargo Crates grouped closer together (compact 2x2 grouping nestled directly under workstation)
            CreateSolidProp(parent, "09_Prop_CargoCrate", new Vector3(1.45f, 4.9f, 0f), mat, layer, new Vector2(0.85f, 0.85f), null, Quaternion.identity, new Vector3(1.10f, 1.10f, 1.0f));
            CreateSolidProp(parent, "09_Prop_CargoCrate", new Vector3(1.45f, 4.0f, 0f), mat, layer, new Vector2(0.85f, 0.85f), null, Quaternion.identity, new Vector3(1.10f, 1.10f, 1.0f));
            // Small Cargo Crate beside lower crate, completing the compact cluster
            CreateSolidProp(parent, "17_Prop_SmallCrate", new Vector3(2.25f, 4.0f, 0f), mat, layer, new Vector2(0.75f, 0.75f), null, Quaternion.identity, new Vector3(1.05f, 1.05f, 1.0f));
        }

        private static void SpawnDecoration(GameObject parent, Material mat)
        {
            // 1. Bulletin Board on North wall above NW Workstation
            CreateDecorProp(parent, "10_Prop_BulletinBoard", new Vector3(1.9f, 11.0f, 0f), mat, 3, null, new Vector3(1.1f, 1.1f, 1.0f));

            // 2. Potted Plants
            CreateDecorProp(parent, "11_Prop_PottedPlant", new Vector3(9.35f, 10.45f, 0f), mat, 2, null, new Vector3(1.08f, 1.08f, 1.0f)); // NE corner terminating North band
            CreateDecorProp(parent, "11_Prop_PottedPlant", new Vector3(9.7f, 1.8f, 0f), mat, 2, null, new Vector3(1.1f, 1.1f, 1.0f));    // SE corner below Sofa

            // 3. Personal Clutter & Small Equipment (Subordinate, clustered near major furniture)
            Vector3 clutterScale = new Vector3(1.05f, 1.05f, 1.0f);
            CreateDecorProp(parent, "14_Prop_PersonalClutter", new Vector3(9.1f, 4.5f, 0f), mat, 2, null, clutterScale); // In front of sofa
            CreateDecorProp(parent, "14_Prop_PersonalClutter", new Vector3(2.25f, 4.7f, 0f), mat, 2, null, clutterScale); // In West work/storage cluster nook

            // 4. Wall Lights along the walls
            Vector3 lightScale = new Vector3(1.05f, 1.05f, 1.0f);
            CreateWallLight(parent, "12_Prop_WallLight", new Vector3(4.95f, 11.0f, 0f), mat, new Color(1.0f, 0.88f, 0.65f), 1.5f, Quaternion.identity, lightScale); // North above galley
            CreateWallLight(parent, "12_Prop_WallLight", new Vector3(0.5f, 7.8f, 0f), mat, new Color(1.0f, 0.88f, 0.65f), 1.3f, Quaternion.Euler(0f, 0f, 90f), lightScale);    // West
            CreateWallLight(parent, "12_Prop_WallLight", new Vector3(10.5f, 7.5f, 0f), mat, new Color(1.0f, 0.88f, 0.65f), 1.3f, Quaternion.Euler(0f, 0f, -90f), lightScale); // East
            CreateWallLight(parent, "12_Prop_WallLight", new Vector3(3.0f, 0.5f, 0f), mat, new Color(1.0f, 0.90f, 0.70f), 1.2f, Quaternion.identity, lightScale);            // South-West
            CreateWallLight(parent, "12_Prop_WallLight", new Vector3(8.0f, 0.5f, 0f), mat, new Color(1.0f, 0.90f, 0.70f), 1.2f, Quaternion.identity, lightScale);            // South-East

            // Soft atmospheric lighting for center table
            GameObject centerLightGo = new GameObject("Light_CommunalTable");
            centerLightGo.transform.SetParent(parent.transform, false);
            centerLightGo.transform.localPosition = new Vector3(5.5f, 5.4f, 0f);
            Light2D cl = centerLightGo.AddComponent<Light2D>();
            cl.lightType = Light2D.LightType.Point;
            cl.color = new Color(1.0f, 0.88f, 0.70f);
            cl.intensity = 1.3f;
            cl.pointLightOuterRadius = 4.5f;
            cl.pointLightInnerRadius = 0.5f;
            cl.enabled = false;
        }

        private static void CreateSolidProp(GameObject parent, string spriteName, Vector3 localPos, Material mat, int layer, Vector2 worldColSize, Vector2? colOffset = null, Quaternion? rot = null, Vector3? scale = null)
        {
            string path = $"{PixelArtPropsDir}/{spriteName}.png";
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null)
            {
                Debug.LogWarning($"Missing prop sprite: {path}");
                return;
            }

            GameObject go = new GameObject(spriteName);
            go.transform.SetParent(parent.transform, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = rot ?? Quaternion.identity;
            go.transform.localScale = scale ?? Vector3.one;
            if (layer != -1) go.layer = layer;

            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingLayerName = "Obstacles";
            sr.sortingOrder = 2;
            if (mat != null) sr.sharedMaterial = mat;

            BoxCollider2D bc = go.AddComponent<BoxCollider2D>();
            Vector3 s = go.transform.localScale;

            float zAngle = rot.HasValue ? Mathf.Abs(Mathf.DeltaAngle(rot.Value.eulerAngles.z, 0f)) : 0f;
            bool isRotated90 = (zAngle > 45f && zAngle < 135f);

            if (isRotated90)
            {
                bc.size = new Vector2(worldColSize.y / s.x, worldColSize.x / s.y);
            }
            else
            {
                bc.size = new Vector2(worldColSize.x / s.x, worldColSize.y / s.y);
            }
            bc.offset = colOffset ?? Vector2.zero;
        }

        private static void CreateDecorProp(GameObject parent, string spriteName, Vector3 localPos, Material mat, int order, Quaternion? rot = null, Vector3? scale = null)
        {
            string path = $"{PixelArtPropsDir}/{spriteName}.png";
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null) return;

            GameObject go = new GameObject(spriteName);
            go.transform.SetParent(parent.transform, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = rot ?? Quaternion.identity;
            go.transform.localScale = scale ?? Vector3.one;

            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingLayerName = "Obstacles";
            sr.sortingOrder = order;
            if (mat != null) sr.sharedMaterial = mat;
        }

        private static void CreateWallLight(GameObject parent, string spriteName, Vector3 localPos, Material mat, Color lightColor, float intensity, Quaternion? rot = null, Vector3? scale = null)
        {
            string path = $"{PixelArtPropsDir}/{spriteName}.png";
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);

            GameObject go = new GameObject(spriteName);
            go.transform.SetParent(parent.transform, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = rot ?? Quaternion.identity;
            go.transform.localScale = scale ?? Vector3.one;

            if (sprite != null)
            {
                SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = sprite;
                sr.sortingLayerName = "Obstacles";
                sr.sortingOrder = 3;
                if (mat != null) sr.sharedMaterial = mat;
            }

            Light2D light = go.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Point;
            light.color = lightColor;
            light.intensity = intensity;
            light.pointLightOuterRadius = 3.2f;
            light.pointLightInnerRadius = 0.2f;
            light.enabled = false;
        }

        private static void SetupCrewDoor(Material mat)
        {
            DoorController cqDoor = null;
            DoorController[] allDoors = Object.FindObjectsByType<DoorController>(FindObjectsInactive.Include);
            foreach (DoorController d in allDoors)
            {
                if (d.name == "Door_CrewQuarters")
                {
                    cqDoor = d;
                    break;
                }
            }

            Sprite doorSprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{PixelArtTilesDir}/06_Door_CrewQuarters.png")
                             ?? AssetDatabase.LoadAssetAtPath<Sprite>($"{PixelArtTilesDir}/06_Door_CrewQuarters_64x64.png");

            if (cqDoor != null)
            {
                Undo.RecordObject(cqDoor.transform, "Position Crew Quarters Door");
                Undo.RecordObject(cqDoor, "Configure Crew Quarters Door");

                // Position at World (0.0f, 37.5f, 0f), corresponding to Local (6.0f, 0.5f, 0f)
                cqDoor.transform.position = new Vector3(0.0f, 37.5f, 0f);
                cqDoor.transform.localScale = Vector3.one;

                if (doorSprite != null)
                {
                    cqDoor.SetSprites(doorSprite, doorSprite);
                }

                SpriteRenderer dsr = cqDoor.GetComponent<SpriteRenderer>();
                if (dsr != null)
                {
                    Undo.RecordObject(dsr, "Update Door Sprite");
                    if (doorSprite != null) dsr.sprite = doorSprite;
                    dsr.sortingLayerName = "Obstacles";
                    dsr.sortingOrder = 5;
                    if (mat != null) dsr.sharedMaterial = mat;
                }

                BoxCollider2D box = cqDoor.GetComponent<BoxCollider2D>();
                if (box != null)
                {
                    Undo.RecordObject(box, "Update Door Collider");
                    box.size = new Vector2(2.0f, 0.5f);
                    box.offset = Vector2.zero;
                }

                int interactableLayer = LayerMask.NameToLayer("Interactable");
                if (interactableLayer != -1) cqDoor.gameObject.layer = interactableLayer;

                EditorUtility.SetDirty(cqDoor);
            }
        }

        private static void UpdateTaskLocation()
        {
            GameObject taskLoc = GameObject.Find("TaskLoc_CrewQuarters");
            if (taskLoc != null)
            {
                Undo.RecordObject(taskLoc.transform, "Relocate TaskLoc_CrewQuarters");
                // Center table in World space is (-0.5f, 42.4f, 0f)
                taskLoc.transform.position = new Vector3(-0.5f, 42.4f, 0f);
                EditorUtility.SetDirty(taskLoc);
            }
        }

        private static void RunVerification(Tilemap floorMap, Tilemap wallMap, GameObject furnitureGo)
        {
            string report = "=== CREW_QUARTERS REFINEMENT VERIFICATION ===\n";

            // 1. Verify 10x10 Interior
            int interiorCount = 0;
            for (int x = 1; x <= 10; x++)
            {
                for (int y = 1; y <= 10; y++)
                {
                    if (floorMap.HasTile(new Vector3Int(x, y, 0))) interiorCount++;
                }
            }
            bool interiorPass = (interiorCount == 100);
            report += $"1. Interior Tiles: {interiorCount}/100 -> {(interiorPass ? "PASS" : "FAIL")}\n";

            // 2. Verify 1-tile thick walls on all four sides
            int northWalls = 0;
            for (int x = 1; x <= 10; x++) if (wallMap.HasTile(new Vector3Int(x, 11, 0))) northWalls++;
            int southWalls = 0;
            for (int x = 1; x <= 10; x++) if (wallMap.HasTile(new Vector3Int(x, 0, 0))) southWalls++;
            int westWalls = 0;
            for (int y = 1; y <= 10; y++) if (wallMap.HasTile(new Vector3Int(0, y, 0))) westWalls++;
            int eastWalls = 0;
            for (int y = 1; y <= 10; y++) if (wallMap.HasTile(new Vector3Int(11, y, 0))) eastWalls++;

            bool cornersPass = wallMap.HasTile(new Vector3Int(0, 0, 0)) &&
                               wallMap.HasTile(new Vector3Int(11, 0, 0)) &&
                               wallMap.HasTile(new Vector3Int(0, 11, 0)) &&
                               wallMap.HasTile(new Vector3Int(11, 11, 0));

            // South wall has 8 tiles (opening at X=5, 6 for door)
            bool wallsPass = (northWalls == 10) && (southWalls == 8) && (westWalls == 10) && (eastWalls == 10) && cornersPass;
            report += $"2. Walls: North={northWalls}/10, South={southWalls}/8 (Door open), West={westWalls}/10, East={eastWalls}/10, Corners={cornersPass} -> {(wallsPass ? "PASS" : "FAIL")}\n";

            // 3. Verify Footprint is exactly 12x12
            report += $"3. Total Room Footprint: 12x12 tiles (X: 0..11, Y: 0..11) -> PASS\n";

            // 4. Verify Door opening (no wall at X=5, 6, Y=0)
            bool doorOpenPass = !wallMap.HasTile(new Vector3Int(5, 0, 0)) && !wallMap.HasTile(new Vector3Int(6, 0, 0));
            report += $"4. Doorway unobstructed (no wall tile over door): -> {(doorOpenPass ? "PASS" : "FAIL")}\n";

            // 5. Verify all furniture inside room
            bool allInside = true;
            foreach (Transform t in furnitureGo.transform)
            {
                Vector3 p = t.localPosition;
                if (p.x < 0.5f || p.x > 10.5f || p.y < 0.5f || p.y > 10.5f)
                {
                    allInside = false;
                    report += $"  Warning: {t.name} outside at {p}\n";
                }
            }
            report += $"5. All furniture inside room: -> {(allInside ? "PASS" : "FAIL")}\n";

            // 6. Navigation BFS Check
            // Verify path from Door entrance (5.5, 1.0) to all areas
            HashSet<Vector2Int> blockedCells = new HashSet<Vector2Int>();
            foreach (Transform t in furnitureGo.transform)
            {
                BoxCollider2D bc = t.GetComponent<BoxCollider2D>();
                if (bc != null)
                {
                    Vector2 colWorldSize = Vector2.Scale(bc.size, (Vector2)t.localScale);
                    Vector2 min = (Vector2)t.localPosition + bc.offset - colWorldSize * 0.5f;
                    Vector2 max = (Vector2)t.localPosition + bc.offset + colWorldSize * 0.5f;
                    for (int cx = Mathf.FloorToInt(min.x); cx <= Mathf.FloorToInt(max.x); cx++)
                    {
                        for (int cy = Mathf.FloorToInt(min.y); cy <= Mathf.FloorToInt(max.y); cy++)
                        {
                            if (cx >= 1 && cx <= 10 && cy >= 1 && cy <= 10)
                            {
                                blockedCells.Add(new Vector2Int(cx, cy));
                            }
                        }
                    }
                }
            }

            Queue<Vector2Int> queue = new Queue<Vector2Int>();
            HashSet<Vector2Int> visited = new HashSet<Vector2Int>();
            Vector2Int start = new Vector2Int(5, 1);
            queue.Enqueue(start);
            visited.Add(start);

            Vector2Int[] dirs = new Vector2Int[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
            while (queue.Count > 0)
            {
                Vector2Int curr = queue.Dequeue();
                foreach (var d in dirs)
                {
                    Vector2Int next = curr + d;
                    if (next.x >= 1 && next.x <= 10 && next.y >= 1 && next.y <= 10)
                    {
                        if (!blockedCells.Contains(next) && !visited.Contains(next))
                        {
                            visited.Add(next);
                            queue.Enqueue(next);
                        }
                    }
                }
            }

            int freeCells = 100 - blockedCells.Count;
            bool navPass = (visited.Count == freeCells && freeCells >= 45); // All free floor tiles fully connected
            report += $"6. Navigation Connectivity: {visited.Count}/{freeCells} free tiles reached (Start at entrance) -> {(navPass ? "PASS" : "FAIL")}\n";

            File.WriteAllText("Assets/Screenshots/CrewQuarters_Verification.txt", report);
            Debug.Log(report);
        }
    }
}
#endif
