#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;
using TheLastSignal.Entities.Environment;
using TheLastSignal.Entities.Player;

namespace TheLastSignal.EditorTools
{
    /// <summary>
    /// =========================================================================================
    /// BẢN GHI THAM CHIẾU KẾT CẤU BẢN ĐỒ CƠ SỞ (FACILITY MAP REFERENCE)
    /// ĐÃ CHỐT CHÍNH THỨC NGÀY: 14/09/2026
    /// 
    /// LƯU Ý QUAN TRỌNG:
    /// Kể từ ngày 14/09/2026, Scene 'SampleScene.unity' là NGUỒN SỰ THẬT DUY NHẤT (Single Source of Truth).
    /// Mọi thay đổi bản đồ sau này PHẢI được cập nhật trực tiếp trong Scene hoặc tài liệu tham chiếu 
    /// 'Assets/_Project/Documentation/Facility_Final_v1.md'.
    /// Menu 'Tools > Facility > Generate Prototype Tilemap' đã được trang bị hộp thoại cảnh báo xác nhận
    /// để ngăn chặn việc ghi đè ngoài ý muốn.
    /// =========================================================================================
    /// </summary>
    public static class PrototypeTilemapSetup
    {
        private const string TileDir = "Assets/_Project/Tiles";
        private const string FloorPngPath = TileDir + "/Tile_Floor.png";
        private const string WallPngPath = TileDir + "/Tile_Wall.png";
        private const string NodePngPath = TileDir + "/Tile_Node.png";
        private const string DoorHPngPath = TileDir + "/Door_Horizontal.png";
        private const string DoorVPngPath = TileDir + "/Door_Vertical.png";
        private const string CratePngPath = TileDir + "/Obstacle_Crate.png";
        private const string CrateStackPngPath = TileDir + "/Obstacle_CrateStack.png";
        private const string TablePngPath = TileDir + "/Obstacle_Table.png";
        private const string EquipmentPngPath = TileDir + "/Obstacle_Equipment.png";
        private const string StructurePngPath = TileDir + "/Obstacle_Structure.png";
        private const string MarkerGeneratorPngPath = TileDir + "/Marker_Generator.png";
        private const string MarkerComputerPngPath = TileDir + "/Marker_Computer.png";
        private const string MarkerMechanicalPngPath = TileDir + "/Marker_Mechanical.png";
        private const string MarkerControlPanelPngPath = TileDir + "/Marker_ControlPanel.png";
        private const string MarkerExitPngPath = TileDir + "/Marker_Exit.png";
        private const string FloorTilePath = TileDir + "/FloorTile.asset";
        private const string WallTilePath = TileDir + "/WallTile.asset";
        private const string NodeTilePath = TileDir + "/NodeTile.asset";

        public enum BlockType
        {
            Room,
            Node,
            Corridor
        }

        public struct MapBlock
        {
            public string Name;
            public BlockType Type;
            public int MinX;
            public int MinY;
            public int MaxX;
            public int MaxY;

            public MapBlock(string name, BlockType type, int minX, int minY, int maxX, int maxY)
            {
                Name = name;
                Type = type;
                MinX = minX;
                MinY = minY;
                MaxX = maxX;
                MaxY = maxY;
            }
        }

        public static readonly MapBlock[] FacilityBlocks = new[]
        {
            // --- 10 Rooms (Exact coordinates from Brief) ---
            new MapBlock("Crew_Quarters", BlockType.Room, -5, 23, 4, 30),
            new MapBlock("Med_Bay", BlockType.Room, -14, 14, -7, 19),
            new MapBlock("Console_Alpha", BlockType.Room, 6, 13, 15, 20),
            new MapBlock("Airlock_Spawn", BlockType.Room, -4, 1, 3, 8),
            new MapBlock("Mess_Hall", BlockType.Room, -14, -9, -7, -2),
            new MapBlock("Vent_Shaft", BlockType.Room, 6, -10, 13, -1),
            new MapBlock("Cargo_Hold", BlockType.Room, -6, -21, 5, -12),
            new MapBlock("Deep_Storage", BlockType.Room, -15, -39, -6, -32),
            new MapBlock("Power_Relay_Bay", BlockType.Room, 5, -39, 14, -32),
            new MapBlock("Reactor_Core", BlockType.Room, 5, -52, 14, -43),

            // --- 3 Nodes ---
            new MapBlock("Node_A", BlockType.Node, -3, 14, 2, 19),
            new MapBlock("Node_B", BlockType.Node, -3, -9, 2, -2),
            new MapBlock("Node_C", BlockType.Node, -3, -29, 2, -25),

            // --- Corridors on Main Spine (Standard 2-tile doorway width: X in [-1, 0]) ---
            new MapBlock("Corridor_Crew_NodeA", BlockType.Corridor, -1, 20, 0, 22),
            new MapBlock("Corridor_NodeA_Airlock", BlockType.Corridor, -1, 9, 0, 13),
            new MapBlock("Corridor_Airlock_NodeB", BlockType.Corridor, -1, -1, 0, 0),
            new MapBlock("Corridor_NodeB_Cargo", BlockType.Corridor, -1, -11, 0, -10),
            new MapBlock("Corridor_Cargo_NodeC", BlockType.Corridor, -1, -24, 0, -22),

            // --- Lateral Corridors (East-West) ---
            new MapBlock("Corridor_MedBay_NodeA", BlockType.Corridor, -6, 16, -4, 17),
            new MapBlock("Corridor_NodeA_Console", BlockType.Corridor, 3, 16, 5, 17),
            new MapBlock("Corridor_MessHall_NodeB", BlockType.Corridor, -6, -4, -4, -3),
            new MapBlock("Corridor_NodeB_VentShaft", BlockType.Corridor, 3, -4, 5, -3),

            // --- Node C Stem & T-Branch Corridors ---
            new MapBlock("Corridor_C_Stem", BlockType.Corridor, -1, -36, 0, -30),
            new MapBlock("Corridor_C_Branch_West", BlockType.Corridor, -5, -36, -2, -35),
            new MapBlock("Corridor_C_Branch_East", BlockType.Corridor, 1, -36, 4, -35),

            // --- Power Relay to Reactor Core Corridor ---
            new MapBlock("Corridor_Relay_Reactor", BlockType.Corridor, 9, -42, 10, -40),

            // --- Facility Loop Corridors (Vent Shaft South -> Node C East) ---
            new MapBlock("Loop_Vertical", BlockType.Corridor, 14, -28, 15, -10),
            new MapBlock("Loop_Horizontal_Bend", BlockType.Corridor, 3, -28, 14, -27),

            // --- Facility West Loop Corridors (Med Bay West -> Deep Storage West) ---
            new MapBlock("Loop_West_Vertical", BlockType.Corridor, -19, -34, -18, 15),
            new MapBlock("Loop_West_Bend_North", BlockType.Corridor, -17, 14, -15, 15),
            new MapBlock("Loop_West_Bend_South", BlockType.Corridor, -17, -34, -16, -33)
        };

        [InitializeOnLoadMethod]
        private static void OnEditorLoaded()
        {
            EnsureTileAssets();
        }

        [MenuItem("Tools/Facility/Generate Prototype Tilemap", false, 1)]
        public static void GeneratePrototypeTilemap()
        {
            bool proceed = EditorUtility.DisplayDialog(
                "Cảnh báo: Bản đồ đã được chốt chính thức",
                "Map đã được chốt làm bản chính thức. Chạy lại sẽ ghi đè toàn bộ thay đổi thủ công trong Scene. Bạn có chắc chắn muốn tiếp tục?",
                "Tiếp tục",
                "Hủy bỏ"
            );

            if (!proceed)
            {
                Debug.Log("<color=yellow>[FacilityTilemap] Thao tác tạo lại bản đồ đã bị hủy để bảo vệ dữ liệu Scene chính thức.</color>");
                return;
            }

            EnsureTileAssets();

            AssetDatabase.Refresh();
            TileBase floorTile = AssetDatabase.LoadAssetAtPath<TileBase>(FloorTilePath) ?? AssetDatabase.LoadAssetAtPath<Tile>(FloorTilePath);
            TileBase wallTile = AssetDatabase.LoadAssetAtPath<TileBase>(WallTilePath) ?? AssetDatabase.LoadAssetAtPath<Tile>(WallTilePath);
            TileBase nodeTile = AssetDatabase.LoadAssetAtPath<TileBase>(NodeTilePath) ?? AssetDatabase.LoadAssetAtPath<Tile>(NodeTilePath);

            if (floorTile == null || wallTile == null || nodeTile == null)
            {
                Debug.LogError($"[FacilityTilemap] Failed to load Tile assets! (Floor: {floorTile != null}, Wall: {wallTile != null}, Node: {nodeTile != null}) - Re-generating tiles...");
                RecreateTileAssets();
                floorTile = AssetDatabase.LoadAssetAtPath<TileBase>(FloorTilePath);
                wallTile = AssetDatabase.LoadAssetAtPath<TileBase>(WallTilePath);
                nodeTile = AssetDatabase.LoadAssetAtPath<TileBase>(NodeTilePath);

                if (floorTile == null || wallTile == null || nodeTile == null)
                {
                    Debug.LogError($"[FacilityTilemap] Still failed to load Tile assets after recreation! (Floor: {floorTile != null}, Wall: {wallTile != null}, Node: {nodeTile != null})");
                    return;
                }
            }

            GameObject gridGo = GameObject.Find("Facility_Grid");
            if (gridGo == null)
            {
                gridGo = new GameObject("Facility_Grid", typeof(Grid));
                Undo.RegisterCreatedObjectUndo(gridGo, "Create Facility Grid");
            }

            Grid grid = gridGo.GetComponent<Grid>();
            grid.cellSize = new Vector3(1f, 1f, 0f);

            // Floor Tilemap
            Transform floorTrans = gridGo.transform.Find("Floor_Tilemap");
            GameObject floorGo = floorTrans != null ? floorTrans.gameObject : new GameObject("Floor_Tilemap", typeof(Tilemap), typeof(TilemapRenderer));
            if (floorTrans == null)
            {
                floorGo.transform.SetParent(gridGo.transform, false);
                Undo.RegisterCreatedObjectUndo(floorGo, "Create Floor Tilemap");
            }

            Tilemap floorTilemap = floorGo.GetComponent<Tilemap>();
            TilemapRenderer floorRenderer = floorGo.GetComponent<TilemapRenderer>();
            floorRenderer.sortingLayerName = "Floor";
            floorRenderer.sortingOrder = 0;

            // Wall Tilemap
            Transform wallTrans = gridGo.transform.Find("Wall_Tilemap");
            GameObject wallGo = wallTrans != null ? wallTrans.gameObject : new GameObject("Wall_Tilemap", typeof(Tilemap), typeof(TilemapRenderer));
            if (wallTrans == null)
            {
                wallGo.transform.SetParent(gridGo.transform, false);
                Undo.RegisterCreatedObjectUndo(wallGo, "Create Wall Tilemap");
            }

            int obstacleLayer = LayerMask.NameToLayer("Obstacle");
            if (obstacleLayer != -1)
            {
                wallGo.layer = obstacleLayer;
            }

            Tilemap wallTilemap = wallGo.GetComponent<Tilemap>();
            TilemapRenderer wallRenderer = wallGo.GetComponent<TilemapRenderer>();
            wallRenderer.sortingLayerName = "Obstacles";
            wallRenderer.sortingOrder = 0;

            TilemapCollider2D tilemapCollider = wallGo.GetComponent<TilemapCollider2D>();
            if (tilemapCollider == null)
            {
                tilemapCollider = Undo.AddComponent<TilemapCollider2D>(wallGo);
            }
            tilemapCollider.compositeOperation = Collider2D.CompositeOperation.Merge;

            CompositeCollider2D compositeCollider = wallGo.GetComponent<CompositeCollider2D>();
            if (compositeCollider == null)
            {
                compositeCollider = Undo.AddComponent<CompositeCollider2D>(wallGo);
            }
            compositeCollider.geometryType = CompositeCollider2D.GeometryType.Polygons;

            Rigidbody2D rb = wallGo.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.bodyType = RigidbodyType2D.Static;
                rb.simulated = true;
            }

            // Build full facility map
            BuildFullFacility(floorTilemap, wallTilemap, floorTile, wallTile, nodeTile);

            // Spawn Doors precisely bridging wall frames
            SpawnFacilityDoors();

            // Spawn Obstacles (Crates, Tables, Equipment, Structure)
            SpawnFacilityObstacles();

            // Spawn 1-4 Player Spawn Points
            SpawnFacilitySpawnPoints();

            // Spawn Gameplay Task Locations (Markers for 9 rooms)
            SpawnFacilityTaskLocations();

            // Move Player to exact P1 Spawn Point (-1.5, 4.5, 0)
            GameObject playerGo = GameObject.Find("Player");
            if (playerGo != null)
            {
                Undo.RecordObject(playerGo.transform, "Position Player at P1 Spawn");
                playerGo.transform.position = new Vector3(-1.5f, 4.5f, 0f);

                SpriteRenderer playerSr = playerGo.GetComponent<SpriteRenderer>();
                if (playerSr != null)
                {
                    playerSr.sortingLayerName = "Characters";
                }
            }

            // Deactivate old primitives & test interactables if present
            GameObject oldWallA = GameObject.Find("Obstacle_Wall_A");
            if (oldWallA != null) oldWallA.SetActive(false);
            GameObject oldWallB = GameObject.Find("Obstacle_Wall_B");
            if (oldWallB != null) oldWallB.SetActive(false);
            GameObject oldInteractableFacing = GameObject.Find("Interactable_Facing");
            if (oldInteractableFacing != null) oldInteractableFacing.SetActive(false);
            GameObject oldInteractableCloser = GameObject.Find("Interactable_Closer");
            if (oldInteractableCloser != null) oldInteractableCloser.SetActive(false);

            EditorUtility.SetDirty(gridGo);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gridGo.scene);

            Selection.activeGameObject = gridGo;
            Debug.Log("<color=green>[FacilityTilemap] Perfectly aligned Doors & Facility generated successfully!</color>");
        }

        public static void BuildFullFacility(Tilemap floorMap, Tilemap wallMap, TileBase floorTile, TileBase wallTile, TileBase nodeTile)
        {
            floorMap.ClearAllTiles();
            wallMap.ClearAllTiles();

            HashSet<Vector3Int> floorPositions = new HashSet<Vector3Int>();
            Dictionary<Vector3Int, TileBase> floorTiles = new Dictionary<Vector3Int, TileBase>();

            // 1. Carve Floors
            foreach (var block in FacilityBlocks)
            {
                TileBase tileToUse = (block.Type == BlockType.Node) ? nodeTile : floorTile;
                for (int x = block.MinX; x <= block.MaxX; x++)
                {
                    for (int y = block.MinY; y <= block.MaxY; y++)
                    {
                        Vector3Int pos = new Vector3Int(x, y, 0);
                        floorPositions.Add(pos);
                        if (!floorTiles.ContainsKey(pos) || block.Type == BlockType.Node)
                        {
                            floorTiles[pos] = tileToUse;
                        }
                    }
                }
            }

            foreach (var kvp in floorTiles)
            {
                floorMap.SetTile(kvp.Key, kvp.Value);
            }

            // 2. Build Perimeter Walls
            HashSet<Vector3Int> wallPositions = new HashSet<Vector3Int>();
            Vector3Int[] directions = new[]
            {
                new Vector3Int(-1, 0, 0), new Vector3Int(1, 0, 0),
                new Vector3Int(0, -1, 0), new Vector3Int(0, 1, 0),
                new Vector3Int(-1, -1, 0), new Vector3Int(-1, 1, 0),
                new Vector3Int(1, -1, 0), new Vector3Int(1, 1, 0)
            };

            foreach (var fPos in floorPositions)
            {
                foreach (var dir in directions)
                {
                    Vector3Int nPos = fPos + dir;
                    if (!floorPositions.Contains(nPos))
                    {
                        wallPositions.Add(nPos);
                    }
                }
            }

            foreach (var wPos in wallPositions)
            {
                wallMap.SetTile(wPos, wallTile);
            }

            floorMap.RefreshAllTiles();
            wallMap.RefreshAllTiles();

            // 3. Verify Connectivity
            VerifyConnectivity(floorPositions, new Vector3Int(0, 3, 0));
        }

        public static void SpawnFacilityDoors()
        {
            GameObject oldDoors = GameObject.Find("Facility_Doors");
            if (oldDoors != null)
            {
                Undo.DestroyObjectImmediate(oldDoors);
            }

            GameObject doorsRoot = GameObject.Find("Doors");
            if (doorsRoot != null)
            {
                Undo.DestroyObjectImmediate(doorsRoot);
            }

            doorsRoot = new GameObject("Doors");
            Undo.RegisterCreatedObjectUndo(doorsRoot, "Create Doors");

            Sprite hSprite = AssetDatabase.LoadAssetAtPath<Sprite>(DoorHPngPath);
            Sprite vSprite = AssetDatabase.LoadAssetAtPath<Sprite>(DoorVPngPath);

            // --- 12 Facility Entrance Doors (Precisely centered in wall openings between perimeter walls) ---
            // 1. Crew Quarters South Door: wall opening row Y=22, span X in [-1, 0] (bounds [-1.0, 1.0]), wall Left: (-2, 22), Right: (1, 22)
            CreateDoorInstance("Door_CrewQuarters", doorsRoot.transform, new Vector3(0.0f, 22.5f, 0f), true, hSprite);

            // 2. Med Bay East Door: wall opening col X=-6, span Y in [16, 17] (bounds [16.0, 18.0]), wall Bottom: (-6, 15), Top: (-6, 18)
            CreateDoorInstance("Door_MedBay", doorsRoot.transform, new Vector3(-5.5f, 17.0f, 0f), false, vSprite);

            // 3. Console Alpha West Door: wall opening col X=5, span Y in [16, 17] (bounds [16.0, 18.0]), wall Bottom: (5, 15), Top: (5, 18)
            CreateDoorInstance("Door_ConsoleAlpha", doorsRoot.transform, new Vector3(5.5f, 17.0f, 0f), false, vSprite);

            // 4. Airlock North Door: wall opening row Y=9, span X in [-1, 0] (bounds [-1.0, 1.0]), wall Left: (-2, 9), Right: (1, 9)
            CreateDoorInstance("Door_Airlock_North", doorsRoot.transform, new Vector3(0.0f, 9.5f, 0f), true, hSprite);

            // 5. Airlock South Door: wall opening row Y=0, span X in [-1, 0] (bounds [-1.0, 1.0]), wall Left: (-2, 0), Right: (1, 0)
            CreateDoorInstance("Door_Airlock_South", doorsRoot.transform, new Vector3(0.0f, 0.5f, 0f), true, hSprite);

            // 6. Mess Hall East Door: wall opening col X=-6, span Y in [-4, -3] (bounds [-4.0, -2.0]), wall Bottom: (-6, -5), Top: (-6, -2)
            CreateDoorInstance("Door_MessHall", doorsRoot.transform, new Vector3(-5.5f, -3.0f, 0f), false, vSprite);

            // 7. Vent Shaft West Door: wall opening col X=5, span Y in [-4, -3] (bounds [-4.0, -2.0]), wall Bottom: (5, -5), Top: (5, -2)
            CreateDoorInstance("Door_VentShaft", doorsRoot.transform, new Vector3(5.5f, -3.0f, 0f), false, vSprite);

            // 8. Cargo Hold North Door: wall opening row Y=-11, span X in [-1, 0] (bounds [-1.0, 1.0]), wall Left: (-2, -11), Right: (1, -11)
            CreateDoorInstance("Door_CargoHold_North", doorsRoot.transform, new Vector3(0.0f, -10.5f, 0f), true, hSprite);

            // 9. Cargo Hold South Door: wall opening row Y=-22, span X in [-1, 0] (bounds [-1.0, 1.0]), wall Left: (-2, -22), Right: (1, -22)
            CreateDoorInstance("Door_CargoHold_South", doorsRoot.transform, new Vector3(0.0f, -21.5f, 0f), true, hSprite);

            // 10. Deep Storage East Door: wall opening col X=-5, span Y in [-36, -35] (bounds [-36.0, -34.0]), wall Bottom: (-5, -37), Top: (-5, -34)
            CreateDoorInstance("Door_DeepStorage", doorsRoot.transform, new Vector3(-4.5f, -35.0f, 0f), false, vSprite);

            // 11. Power Relay Bay West Door: wall opening col X=4, span Y in [-36, -35] (bounds [-36.0, -34.0]), wall Bottom: (4, -37), Top: (4, -34)
            CreateDoorInstance("Door_PowerRelayBay", doorsRoot.transform, new Vector3(4.5f, -35.0f, 0f), false, vSprite);

            // 12. Reactor Core North Door: wall opening row Y=-42, span X in [9, 10] (bounds [9.0, 11.0]), wall Left: (8, -42), Right: (11, -42)
            CreateDoorInstance("Door_ReactorCore", doorsRoot.transform, new Vector3(10.0f, -41.5f, 0f), true, hSprite);
        }

        public static void SpawnFacilityObstacles()
        {
            GameObject oldObstacles = GameObject.Find("Facility_Obstacles");
            if (oldObstacles != null)
            {
                Undo.DestroyObjectImmediate(oldObstacles);
            }

            GameObject obstaclesRoot = GameObject.Find("Obstacles");
            if (obstaclesRoot != null)
            {
                Undo.DestroyObjectImmediate(obstaclesRoot);
            }

            obstaclesRoot = new GameObject("Obstacles");
            Undo.RegisterCreatedObjectUndo(obstaclesRoot, "Create Obstacles");

            Sprite crateSprite = AssetDatabase.LoadAssetAtPath<Sprite>(CratePngPath);
            Sprite tableSprite = AssetDatabase.LoadAssetAtPath<Sprite>(TablePngPath);
            Sprite equipSprite = AssetDatabase.LoadAssetAtPath<Sprite>(EquipmentPngPath);

            Sprite structSprite = AssetDatabase.LoadAssetAtPath<Sprite>(StructurePngPath);

            // 1. Obstacle_MedBay_Crate: room [-14,14,-7,19], pos (-9.0, 16.5), box (2, 2) - doubled from 1x1, sits along route to TaskLoc_MedBay, >= 2 tiles from East Door (-6, 16..17)
            CreateObstacleInstance("Obstacle_MedBay_Crate", obstaclesRoot.transform, new Vector3(-9.0f, 16.5f, 0f), new Vector2(2.0f, 2.0f), crateSprite);

            // 2. Obstacle_DeepStorage_Equipment: room [-15,-39,-6,-32], pos (-8.0, -35.5), box (2, 3) - doubled from 1x2, sits along route to TaskLoc_DeepStorage, >= 2 tiles from East Door (-5, -36..-35)
            CreateObstacleInstance("Obstacle_DeepStorage_Equipment", obstaclesRoot.transform, new Vector3(-8.0f, -35.5f, 0f), new Vector2(2.0f, 3.0f), equipSprite);

            // 3-5. Cargo Hold Obstacle Cluster: 3 objects forming a horizontal bar spanning 4 tiles East-West at Y=-15.5 across the central spine
            // Forces player entering from North (-1..0, -11) or South (-1..0, -22) to navigate around left or right.
            CreateObstacleInstance("Obstacle_CargoHold_Crate_01", obstaclesRoot.transform, new Vector3(-2.0f, -15.5f, 0f), new Vector2(1.0f, 1.0f), crateSprite);
            CreateObstacleInstance("Obstacle_CargoHold_Table_02", obstaclesRoot.transform, new Vector3(-0.5f, -15.5f, 0f), new Vector2(2.0f, 1.0f), tableSprite);
            CreateObstacleInstance("Obstacle_CargoHold_Crate_03", obstaclesRoot.transform, new Vector3(1.0f, -15.5f, 0f), new Vector2(1.0f, 1.0f), crateSprite);

            // --- 3 Evacuation Friction Obstacles (Adds gentle chicane/turning without blocking any doorway or corridor) ---
            // 6. Obstacle_Loop_Chicane: Loop_Horizontal_Bend [3..14, -28..-27], placed on North row (Y=-26.5), leaves South row fully open (100% passable chicane)
            CreateObstacleInstance("Obstacle_Loop_Chicane", obstaclesRoot.transform, new Vector3(8.5f, -26.5f, 0f), new Vector2(1.0f, 1.0f), crateSprite);

            // 7. Obstacle_NodeC_Center: Node_C [-3..2, -29..-25], placed in NW corner at (-2.5, -25.5), clear of North spine (-1..0), South spine (-1..0), and East bend (2)
            CreateObstacleInstance("Obstacle_NodeC_Center", obstaclesRoot.transform, new Vector3(-2.5f, -25.5f, 0f), new Vector2(1.0f, 1.0f), crateSprite);

            // 8. Obstacle_NodeB_Center: Node_B [-3..2, -8..-3], placed in SW corner at (-2.5, -6.5), clear of North spine (-1..0), South spine (-1..0), West corridor (-4..-3), and East corridor (-4..-3)
            CreateObstacleInstance("Obstacle_NodeB_Center", obstaclesRoot.transform, new Vector3(-2.5f, -6.5f, 0f), new Vector2(1.0f, 1.0f), crateSprite);

            // --- 4 Balancing Obstacles for Empty Rooms & Node A ---
            // 9. Obstacle_CrewQuarters_Table: Crew_Quarters [-5..4, 23..30], placed at West (-3.0, 26.5) with size (2x1), off-axis from South door (-1..0) to TaskLoc (-0.5)
            CreateObstacleInstance("Obstacle_CrewQuarters_Table", obstaclesRoot.transform, new Vector3(-3.0f, 26.5f, 0f), new Vector2(2.0f, 1.0f), tableSprite);

            // 10. Obstacle_MessHall_Table: Mess_Hall [-14..-7, -9..-2], placed at North (-10.5, -3.5) with size (2x1), off-axis from East door (-6, -6..-5) to TaskLoc (-10.5, -5.5)
            CreateObstacleInstance("Obstacle_MessHall_Table", obstaclesRoot.transform, new Vector3(-10.5f, -3.5f, 0f), new Vector2(2.0f, 1.0f), tableSprite);

            // 11. Obstacle_VentShaft_Equipment: Vent_Shaft [6..13, -10..-1], placed at North (9.5, -3.0) with size (2x1), off-axis from West door (5, -6..-5) to TaskLoc (9.5, -5.5), clear of South Loop entrance (Y=-10)
            CreateObstacleInstance("Obstacle_VentShaft_Equipment", obstaclesRoot.transform, new Vector3(9.5f, -3.0f, 0f), new Vector2(2.0f, 1.0f), equipSprite);

            // 12. Obstacle_NodeA_Center: Node_A [-3..2, 14..19], placed in NW corner at (-2.5, 18.5) with size (1x1), matching Node B & C pattern, clear of all 4 doorway axes
            CreateObstacleInstance("Obstacle_NodeA_Center", obstaclesRoot.transform, new Vector3(-2.5f, 18.5f, 0f), new Vector2(1.0f, 1.0f), crateSprite);

            // --- 6 Obstacles for 3 Dual Console Rooms (Difficulty Tiered) ---
            // 13. Console Alpha (Easy): 1 technical cable box (1x2) in SE corner, far from Door and TaskLoc
            CreateObstacleInstance("Obstacle_ConsoleAlpha_CableBox", obstaclesRoot.transform, new Vector3(14.0f, 14.0f, 0f), new Vector2(1.0f, 2.0f), equipSprite);

            // 14-15. Power Relay Bay (Medium): 2 conduit/equipment racks (1x2) placed symmetrically on North and South flanks at East wall
            CreateObstacleInstance("Obstacle_PowerRelay_Conduit_North", obstaclesRoot.transform, new Vector3(13.5f, -33.0f, 0f), new Vector2(1.0f, 2.0f), equipSprite);
            CreateObstacleInstance("Obstacle_PowerRelay_Conduit_South", obstaclesRoot.transform, new Vector3(13.5f, -38.0f, 0f), new Vector2(1.0f, 2.0f), equipSprite);

            // 16-18. Reactor Core (Finale): 3 coolant tanks / structural supports filling out 10x10 space without blocking doorway or TaskLoc
            CreateObstacleInstance("Obstacle_ReactorCore_CoolantTank_West", obstaclesRoot.transform, new Vector3(6.0f, -47.5f, 0f), new Vector2(1.0f, 2.0f), structSprite);
            CreateObstacleInstance("Obstacle_ReactorCore_CoolantTank_East", obstaclesRoot.transform, new Vector3(13.5f, -47.5f, 0f), new Vector2(1.0f, 2.0f), structSprite);
            CreateObstacleInstance("Obstacle_ReactorCore_BasePipe_South", obstaclesRoot.transform, new Vector3(9.5f, -51.5f, 0f), new Vector2(2.0f, 1.0f), tableSprite);
        }

        private static GameObject CreateObstacleInstance(string name, Transform parent, Vector3 position, Vector2 colliderSize, Sprite sprite)
        {
            GameObject obstacleGo = new GameObject(name);
            obstacleGo.transform.SetParent(parent, false);
            obstacleGo.transform.position = position;

            int obstacleLayer = LayerMask.NameToLayer("Obstacle");
            if (obstacleLayer != -1)
            {
                obstacleGo.layer = obstacleLayer;
            }

            SpriteRenderer sr = obstacleGo.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingLayerName = "Obstacles";
            sr.sortingOrder = 2;

            BoxCollider2D boxCol = obstacleGo.AddComponent<BoxCollider2D>();
            boxCol.size = colliderSize;
            boxCol.isTrigger = false;

            return obstacleGo;
        }

        public static void SpawnFacilitySpawnPoints()
        {
            GameObject spawnRoot = GameObject.Find("Spawn_Points");
            if (spawnRoot != null)
            {
                Undo.DestroyObjectImmediate(spawnRoot);
            }

            spawnRoot = new GameObject("Spawn_Points");
            Undo.RegisterCreatedObjectUndo(spawnRoot, "Create Spawn Points");
            SpawnManager spawnManager = spawnRoot.AddComponent<SpawnManager>();

            // 1-4 Player Spawn Points arranged in a 2x2 grid inside Airlock Spawn room (X: [-4, 3], Y: [1, 8])
            // P1: Top-Left (-1.5, 4.5), P2: Top-Right (1.5, 4.5)
            // P3: Bottom-Left (-1.5, 2.5), P4: Bottom-Right (1.5, 2.5)
            CreateSpawnPointInstance("SpawnPoint_P1", spawnRoot.transform, new Vector3(-1.5f, 4.5f, 0f), 0);
            CreateSpawnPointInstance("SpawnPoint_P2", spawnRoot.transform, new Vector3(1.5f, 4.5f, 0f), 1);
            CreateSpawnPointInstance("SpawnPoint_P3", spawnRoot.transform, new Vector3(-1.5f, 2.5f, 0f), 2);
            CreateSpawnPointInstance("SpawnPoint_P4", spawnRoot.transform, new Vector3(1.5f, 2.5f, 0f), 3);

            spawnManager.RefreshSpawnPoints();
        }

        public static void SpawnFacilityTaskLocations()
        {
            GameObject oldObjectives = GameObject.Find("Facility_Objectives");
            if (oldObjectives != null)
            {
                Undo.DestroyObjectImmediate(oldObjectives);
            }

            GameObject taskLocsRoot = GameObject.Find("TaskLocations");
            if (taskLocsRoot != null)
            {
                Undo.DestroyObjectImmediate(taskLocsRoot);
            }

            taskLocsRoot = new GameObject("TaskLocations");
            Undo.RegisterCreatedObjectUndo(taskLocsRoot, "Create Task Locations");

            Sprite genSprite = AssetDatabase.LoadAssetAtPath<Sprite>(MarkerGeneratorPngPath);
            Sprite compSprite = AssetDatabase.LoadAssetAtPath<Sprite>(MarkerComputerPngPath);
            Sprite mechSprite = AssetDatabase.LoadAssetAtPath<Sprite>(MarkerMechanicalPngPath);
            Sprite cpSprite = AssetDatabase.LoadAssetAtPath<Sprite>(MarkerControlPanelPngPath);
            Sprite exitSprite = AssetDatabase.LoadAssetAtPath<Sprite>(MarkerExitPngPath);

            // 10 Task Locations across the facility (excluding Airlock Spawn per brief)
            // Group 2: Dual Console rooms (ConsoleAlpha, PowerRelayBay, ReactorCore) enlarged to 2x2 physical colliders
            CreateTaskLocationInstance("TaskLoc_CrewQuarters", taskLocsRoot.transform, new Vector3(-0.5f, 26.5f, 0f), cpSprite, new Vector2(1.0f, 1.0f));
            CreateTaskLocationInstance("TaskLoc_MedBay", taskLocsRoot.transform, new Vector3(-10.5f, 16.5f, 0f), compSprite, new Vector2(1.0f, 1.0f));
            CreateTaskLocationInstance("TaskLoc_ConsoleAlpha", taskLocsRoot.transform, new Vector3(10.5f, 16.5f, 0f), cpSprite, new Vector2(2.0f, 2.0f));
            CreateTaskLocationInstance("TaskLoc_MessHall", taskLocsRoot.transform, new Vector3(-10.5f, -5.5f, 0f), mechSprite, new Vector2(1.0f, 1.0f));
            CreateTaskLocationInstance("TaskLoc_VentShaft", taskLocsRoot.transform, new Vector3(9.5f, -5.5f, 0f), mechSprite, new Vector2(1.0f, 1.0f));
            CreateTaskLocationInstance("TaskLoc_CargoHold", taskLocsRoot.transform, new Vector3(-0.5f, -16.5f, 0f), exitSprite, new Vector2(1.0f, 1.0f));
            CreateTaskLocationInstance("TaskLoc_CargoHold_02", taskLocsRoot.transform, new Vector3(-4.0f, -19.0f, 0f), exitSprite, new Vector2(1.0f, 1.0f));
            CreateTaskLocationInstance("TaskLoc_DeepStorage", taskLocsRoot.transform, new Vector3(-10.5f, -35.5f, 0f), mechSprite, new Vector2(1.0f, 1.0f));
            CreateTaskLocationInstance("TaskLoc_PowerRelayBay", taskLocsRoot.transform, new Vector3(9.5f, -35.5f, 0f), genSprite, new Vector2(2.0f, 2.0f));
            CreateTaskLocationInstance("TaskLoc_ReactorCore", taskLocsRoot.transform, new Vector3(9.5f, -47.5f, 0f), genSprite, new Vector2(2.0f, 2.0f));
        }

        private static GameObject CreateTaskLocationInstance(string name, Transform parent, Vector3 position, Sprite markerSprite = null, Vector2? colliderSize = null)
        {
            GameObject locGo = new GameObject(name);
            locGo.transform.SetParent(parent, false);
            locGo.transform.position = position;

            // Set Layer 10 (Interactable) so PlayerController's Physics2D.OverlapCircleAll detects it
            int interactableLayer = LayerMask.NameToLayer("Interactable");
            if (interactableLayer != -1)
            {
                locGo.layer = interactableLayer;
            }

            if (markerSprite != null)
            {
                SpriteRenderer sr = locGo.AddComponent<SpriteRenderer>();
                sr.sprite = markerSprite;
                sr.sortingLayerName = "Obstacles";
                sr.sortingOrder = 3;
            }

            // Solid Physical Collider for realistic physical station presence
            BoxCollider2D boxCol = locGo.AddComponent<BoxCollider2D>();
            boxCol.size = colliderSize ?? new Vector2(1.0f, 1.0f);
            boxCol.isTrigger = false;

            TaskLocationMarker marker = locGo.AddComponent<TaskLocationMarker>();
            marker.Initialize(name);

            return locGo;
        }

        private static GameObject CreateSpawnPointInstance(string name, Transform parent, Vector3 position, int playerIndex)
        {
            GameObject spGo = new GameObject(name);
            spGo.transform.SetParent(parent, false);
            spGo.transform.position = position;

            SpawnPoint sp = spGo.AddComponent<SpawnPoint>();
            sp.Initialize(playerIndex);

            return spGo;
        }

        private static GameObject CreateDoorInstance(string name, Transform parent, Vector3 position, bool isHorizontal, Sprite sprite)
        {
            GameObject doorGo = new GameObject(name);
            doorGo.transform.SetParent(parent, false);
            doorGo.transform.position = position;

            int interactableLayer = LayerMask.NameToLayer("Interactable");
            if (interactableLayer != -1)
            {
                doorGo.layer = interactableLayer;
            }

            // Visual (Sorting order higher than WallTilemap so it layers nicely)
            SpriteRenderer sr = doorGo.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingLayerName = "Obstacles";
            sr.sortingOrder = 5;

            // Solid Physical Blocker Collider (Managed by DoorController)
            BoxCollider2D boxCol = doorGo.AddComponent<BoxCollider2D>();
            boxCol.size = isHorizontal ? new Vector2(2.0f, 1.0f) : new Vector2(1.0f, 2.0f);
            boxCol.isTrigger = false;

            // Door Controller (implements IInteractable)
            DoorController controller = doorGo.AddComponent<DoorController>();
            controller.Initialize(name, DoorState.Closed);

            return doorGo;
        }

        private static void VerifyConnectivity(HashSet<Vector3Int> allFloorTiles, Vector3Int startPos)
        {
            if (!allFloorTiles.Contains(startPos))
            {
                Debug.LogError($"[FacilityTilemap] Start position {startPos} is not a valid floor tile!");
                return;
            }

            Vector3Int[] cardinal = new[]
            {
                new Vector3Int(-1, 0, 0), new Vector3Int(1, 0, 0),
                new Vector3Int(0, -1, 0), new Vector3Int(0, 1, 0)
            };

            // 1. Flood-Fill with all doors OPEN
            Queue<Vector3Int> queue = new Queue<Vector3Int>();
            HashSet<Vector3Int> visitedOpen = new HashSet<Vector3Int>();

            queue.Enqueue(startPos);
            visitedOpen.Add(startPos);

            while (queue.Count > 0)
            {
                Vector3Int current = queue.Dequeue();
                foreach (var dir in cardinal)
                {
                    Vector3Int neighbor = current + dir;
                    if (allFloorTiles.Contains(neighbor) && !visitedOpen.Contains(neighbor))
                    {
                        visitedOpen.Add(neighbor);
                        queue.Enqueue(neighbor);
                    }
                }
            }

            if (visitedOpen.Count == allFloorTiles.Count)
            {
                Debug.Log($"<color=cyan>[FacilityTilemap] OPEN CONNECTIVITY VERIFIED 100%: All {visitedOpen.Count} floor tiles are fully reachable!</color>");
            }
            else
            {
                Debug.LogError($"[FacilityTilemap] OPEN CONNECTIVITY FAILED! Reachable: {visitedOpen.Count}, Total: {allFloorTiles.Count}.");
            }

            // 2. Flood-Fill with all 3 doors CLOSED (Verify no soft-locks on main spine and loop)
            HashSet<Vector3Int> doorTiles = new HashSet<Vector3Int>
            {
                new Vector3Int(6, 16, 0), new Vector3Int(6, 17, 0),       // Door_ConsoleAlpha
                new Vector3Int(5, -36, 0), new Vector3Int(5, -35, 0),    // Door_PowerRelayBay
                new Vector3Int(9, -43, 0), new Vector3Int(10, -43, 0)     // Door_ReactorCore
            };

            HashSet<Vector3Int> openTiles = new HashSet<Vector3Int>(allFloorTiles);
            openTiles.ExceptWith(doorTiles);

            HashSet<Vector3Int> visitedClosed = new HashSet<Vector3Int>();
            queue.Clear();
            queue.Enqueue(startPos);
            visitedClosed.Add(startPos);

            while (queue.Count > 0)
            {
                Vector3Int current = queue.Dequeue();
                foreach (var dir in cardinal)
                {
                    Vector3Int neighbor = current + dir;
                    if (openTiles.Contains(neighbor) && !visitedClosed.Contains(neighbor))
                    {
                        visitedClosed.Add(neighbor);
                        queue.Enqueue(neighbor);
                    }
                }
            }

            // Verify all non-door rooms remain 100% interconnected
            string[] nonDoorRooms = new[] { "Crew_Quarters", "Med_Bay", "Airlock_Spawn", "Mess_Hall", "Vent_Shaft", "Cargo_Hold", "Deep_Storage" };
            bool allNonDoorReachable = true;
            foreach (var block in FacilityBlocks)
            {
                if (System.Array.IndexOf(nonDoorRooms, block.Name) != -1)
                {
                    for (int x = block.MinX; x <= block.MaxX; x++)
                    {
                        for (int y = block.MinY; y <= block.MaxY; y++)
                        {
                            Vector3Int tilePos = new Vector3Int(x, y, 0);
                            if (!visitedClosed.Contains(tilePos))
                            {
                                allNonDoorReachable = false;
                                Debug.LogError($"[FacilityTilemap] Soft-lock detected! Tile {tilePos} in {block.Name} is unreachable when doors are closed.");
                            }
                        }
                    }
                }
            }

            if (allNonDoorReachable)
            {
                Debug.Log($"<color=green>[FacilityTilemap] CLOSED DOORS CONNECTIVITY VERIFIED 100%: All non-door rooms & spine remain connected without soft-lock ({visitedClosed.Count} tiles reached)!</color>");
            }

            // 3. Isolated 2-Way BFS for West Loop (Med_Bay <-> Deep_Storage independent of main spine)
            HashSet<Vector3Int> westLoopFloors = new HashSet<Vector3Int>();
            string[] westLoopBlockNames = new[] { "Med_Bay", "Loop_West_Vertical", "Loop_West_Bend_North", "Loop_West_Bend_South", "Deep_Storage" };
            foreach (var block in FacilityBlocks)
            {
                if (System.Array.IndexOf(westLoopBlockNames, block.Name) != -1)
                {
                    for (int x = block.MinX; x <= block.MaxX; x++)
                    {
                        for (int y = block.MinY; y <= block.MaxY; y++)
                        {
                            westLoopFloors.Add(new Vector3Int(x, y, 0));
                        }
                    }
                }
            }

            Vector3Int medBayWestTile = new Vector3Int(-14, 14, 0);
            Vector3Int deepStorageWestTile = new Vector3Int(-15, -34, 0);

            // Test Direction 1: Med_Bay -> Deep_Storage
            queue.Clear();
            HashSet<Vector3Int> visitedWestA = new HashSet<Vector3Int>();
            queue.Enqueue(medBayWestTile);
            visitedWestA.Add(medBayWestTile);
            bool reachedDeepStorage = false;
            while (queue.Count > 0)
            {
                Vector3Int curr = queue.Dequeue();
                if (curr == deepStorageWestTile)
                {
                    reachedDeepStorage = true;
                    break;
                }
                foreach (var dir in cardinal)
                {
                    Vector3Int nbr = curr + dir;
                    if (westLoopFloors.Contains(nbr) && !visitedWestA.Contains(nbr))
                    {
                        visitedWestA.Add(nbr);
                        queue.Enqueue(nbr);
                    }
                }
            }

            // Test Direction 2: Deep_Storage -> Med_Bay
            queue.Clear();
            HashSet<Vector3Int> visitedWestB = new HashSet<Vector3Int>();
            queue.Enqueue(deepStorageWestTile);
            visitedWestB.Add(deepStorageWestTile);
            bool reachedMedBay = false;
            while (queue.Count > 0)
            {
                Vector3Int curr = queue.Dequeue();
                if (curr == medBayWestTile)
                {
                    reachedMedBay = true;
                    break;
                }
                foreach (var dir in cardinal)
                {
                    Vector3Int nbr = curr + dir;
                    if (westLoopFloors.Contains(nbr) && !visitedWestB.Contains(nbr))
                    {
                        visitedWestB.Add(nbr);
                        queue.Enqueue(nbr);
                    }
                }
            }

            if (reachedDeepStorage && reachedMedBay)
            {
                Debug.Log("<color=green>[FacilityTilemap] WEST LOOP ISOLATED 2-WAY BFS VERIFIED 100%: Direct bypass Med_Bay <-> Deep_Storage confirmed!</color>");
            }
            else
            {
                Debug.LogError($"[FacilityTilemap] WEST LOOP ISOLATED BFS FAILED! Forward: {reachedDeepStorage}, Backward: {reachedMedBay}");
            }
        }

        public static void EnsureTileAssets()
        {
            if (!Directory.Exists(TileDir))
            {
                Directory.CreateDirectory(TileDir);
            }

            if (!File.Exists(FloorPngPath))
            {
                Texture2D floorTex = CreateBorderedTexture(32, 32, new Color(0.85f, 0.88f, 0.90f, 1f), new Color(0.70f, 0.73f, 0.76f, 1f));
                File.WriteAllBytes(FloorPngPath, floorTex.EncodeToPNG());
                AssetDatabase.ImportAsset(FloorPngPath);
                ConfigureTextureAsSprite(FloorPngPath);
            }

            if (!File.Exists(WallPngPath))
            {
                Texture2D wallTex = CreateBorderedTexture(32, 32, new Color(0.18f, 0.20f, 0.23f, 1f), new Color(0.08f, 0.09f, 0.11f, 1f));
                File.WriteAllBytes(WallPngPath, wallTex.EncodeToPNG());
                AssetDatabase.ImportAsset(WallPngPath);
                ConfigureTextureAsSprite(WallPngPath);
            }

            if (!File.Exists(NodePngPath))
            {
                Texture2D nodeTex = CreateBorderedTexture(32, 32, new Color(0.74f, 0.84f, 0.92f, 1f), new Color(0.58f, 0.70f, 0.80f, 1f));
                File.WriteAllBytes(NodePngPath, nodeTex.EncodeToPNG());
                AssetDatabase.ImportAsset(NodePngPath);
                ConfigureTextureAsSprite(NodePngPath);
            }

            // Ensure Obstacle Sprites
            EnsureObstacleSprites();

            AssetDatabase.Refresh();

            if (!File.Exists(FloorTilePath))
            {
                Sprite floorSprite = AssetDatabase.LoadAssetAtPath<Sprite>(FloorPngPath);
                Tile floorTile = ScriptableObject.CreateInstance<Tile>();
                floorTile.sprite = floorSprite;
                floorTile.colliderType = Tile.ColliderType.None;
                AssetDatabase.CreateAsset(floorTile, FloorTilePath);
            }

            if (!File.Exists(WallTilePath))
            {
                Sprite wallSprite = AssetDatabase.LoadAssetAtPath<Sprite>(WallPngPath);
                Tile wallTile = ScriptableObject.CreateInstance<Tile>();
                wallTile.sprite = wallSprite;
                wallTile.colliderType = Tile.ColliderType.Grid;
                AssetDatabase.CreateAsset(wallTile, WallTilePath);
            }

            if (!File.Exists(NodeTilePath))
            {
                Sprite nodeSprite = AssetDatabase.LoadAssetAtPath<Sprite>(NodePngPath);
                Tile nodeTile = ScriptableObject.CreateInstance<Tile>();
                nodeTile.sprite = nodeSprite;
                nodeTile.colliderType = Tile.ColliderType.None;
                AssetDatabase.CreateAsset(nodeTile, NodeTilePath);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void RecreateTileAssets()
        {
            if (File.Exists(FloorTilePath)) AssetDatabase.DeleteAsset(FloorTilePath);
            if (File.Exists(WallTilePath)) AssetDatabase.DeleteAsset(WallTilePath);
            if (File.Exists(NodeTilePath)) AssetDatabase.DeleteAsset(NodeTilePath);

            AssetDatabase.Refresh();

            Sprite floorSprite = AssetDatabase.LoadAssetAtPath<Sprite>(FloorPngPath);
            Tile floorTile = ScriptableObject.CreateInstance<Tile>();
            floorTile.sprite = floorSprite;
            floorTile.colliderType = Tile.ColliderType.None;
            AssetDatabase.CreateAsset(floorTile, FloorTilePath);

            Sprite wallSprite = AssetDatabase.LoadAssetAtPath<Sprite>(WallPngPath);
            Tile wallTile = ScriptableObject.CreateInstance<Tile>();
            wallTile.sprite = wallSprite;
            wallTile.colliderType = Tile.ColliderType.Grid;
            AssetDatabase.CreateAsset(wallTile, WallTilePath);

            Sprite nodeSprite = AssetDatabase.LoadAssetAtPath<Sprite>(NodePngPath);
            Tile nodeTile = ScriptableObject.CreateInstance<Tile>();
            nodeTile.sprite = nodeSprite;
            nodeTile.colliderType = Tile.ColliderType.None;
            AssetDatabase.CreateAsset(nodeTile, NodeTilePath);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void EnsureObstacleSprites()
        {
            string[] obstaclePaths = new[]
            {
                CratePngPath, CrateStackPngPath, TablePngPath, EquipmentPngPath, StructurePngPath,
                MarkerGeneratorPngPath, MarkerComputerPngPath, MarkerMechanicalPngPath, MarkerControlPanelPngPath, MarkerExitPngPath
            };
            foreach (var path in obstaclePaths)
            {
                if (File.Exists(path))
                {
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                    ConfigureTextureAsSprite(path);
                }
            }
        }

        private static Texture2D CreateBorderedTexture(int width, int height, Color fill, Color border)
        {
            Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    bool isBorder = (x == 0 || x == width - 1 || y == 0 || y == height - 1);
                    tex.SetPixel(x, y, isBorder ? border : fill);
                }
            }
            tex.Apply();
            return tex;
        }

        private static void ConfigureTextureAsSprite(string path)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 32;
                importer.filterMode = FilterMode.Point;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }
        }
    }
}
#endif
