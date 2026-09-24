#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;
using TheLastSignal.Entities.Environment;
using TheLastSignal.Entities.Player;
using TheLastSignal.Core.EventBus;

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
            // --- 11 Rooms (Kích thước đã chốt ở Đợt 1 & Đợt 2, vị trí dời giãn rộng) ---
            new MapBlock("Crew_Quarters", BlockType.Room, -5, 38, 4, 47),        // 10x10
            new MapBlock("Med_Bay", BlockType.Room, -20, 24, -12, 31),           // 9x8
            new MapBlock("Console_Alpha", BlockType.Room, 11, 23, 20, 32),       // 10x10
            new MapBlock("Airlock_Spawn", BlockType.Room, -5, 8, 4, 17),         // 10x10
            new MapBlock("Bedroom", BlockType.Room, -21, 9, -13, 16),            // 9x8
            new MapBlock("Mess_Hall", BlockType.Room, -19, -8, -12, -1),         // 8x8
            new MapBlock("Vent_Shaft", BlockType.Room, 12, -9, 20, 1),           // 9x11
            new MapBlock("Cargo_Hold", BlockType.Room, -6, -24, 5, -15),         // 12x10
            new MapBlock("Deep_Storage", BlockType.Room, -18, -52, -9, -43),     // 10x10
            new MapBlock("Power_Relay_Bay", BlockType.Room, 8, -52, 17, -43),    // 10x10
            new MapBlock("Reactor_Core", BlockType.Room, 7, -69, 18, -60),       // 12x10

            // --- 3 Nodes (Nâng cấp kích thước lớn) ---
            new MapBlock("Node_A", BlockType.Node, -4, 24, 3, 31),               // 8x8 (tăng từ 6x6)
            new MapBlock("Node_B", BlockType.Node, -5, -8, 4, 1),                // 10x10 (tăng từ 6x8)
            new MapBlock("Node_C", BlockType.Node, -4, -38, 3, -31),             // 8x8 (tăng từ 6x5)

            // --- Corridors on Main Spine (Dài 6 ô tiêu chuẩn, trục X: [-1, 0]) ---
            new MapBlock("Corridor_Crew_NodeA", BlockType.Corridor, -1, 32, 0, 37),       // 2x6
            new MapBlock("Corridor_NodeA_Airlock", BlockType.Corridor, -1, 18, 0, 23),    // 2x6
            new MapBlock("Corridor_Airlock_NodeB", BlockType.Corridor, -1, 2, 0, 7),      // 2x6
            new MapBlock("Corridor_NodeB_Cargo", BlockType.Corridor, -1, -14, 0, -9),     // 2x6
            new MapBlock("Corridor_Cargo_NodeC", BlockType.Corridor, -1, -30, 0, -25),    // 2x6
            new MapBlock("Corridor_C_Stem", BlockType.Corridor, -1, -47, 0, -39),         // 2x9

            // --- Lateral Corridors (Cánh Đông & Tây, dài 6-7 ô) ---
            new MapBlock("Corridor_MedBay_NodeA", BlockType.Corridor, -11, 27, -5, 28),     // 7x2
            new MapBlock("Corridor_NodeA_Console", BlockType.Corridor, 4, 27, 10, 28),      // 7x2
            new MapBlock("Corridor_Bedroom_Airlock", BlockType.Corridor, -12, 12, -6, 13),  // 7x2
            new MapBlock("Corridor_MessHall_NodeB", BlockType.Corridor, -11, -5, -6, -4),   // 6x2
            new MapBlock("Corridor_NodeB_VentShaft", BlockType.Corridor, 5, -5, 11, -4),    // 7x2
            new MapBlock("Corridor_C_Branch_West", BlockType.Corridor, -8, -47, -2, -46),   // 7x2
            new MapBlock("Corridor_C_Branch_East", BlockType.Corridor, 1, -47, 7, -46),     // 7x2
            new MapBlock("Corridor_Relay_Reactor", BlockType.Corridor, 12, -59, 13, -53),   // 2x7

            // --- Facility Loop Corridors (Vòng lặp bao ngoài mở rộng) ---
            new MapBlock("Loop_Vertical", BlockType.Corridor, 21, -35, 22, -9),             // 2x27
            new MapBlock("Loop_Horizontal_Bend", BlockType.Corridor, 4, -35, 20, -34),      // 17x2
            new MapBlock("Loop_West_Vertical", BlockType.Corridor, -25, -47, -24, 25),      // 2x73
            new MapBlock("Loop_West_Bend_North", BlockType.Corridor, -23, 24, -21, 25),     // 3x2
            new MapBlock("Loop_West_Bend_South", BlockType.Corridor, -23, -47, -19, -46)    // 5x2
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

            // Spawn 4 Bedroom Intro Beds (cosmetic placeholders)
            SpawnBedroomBeds();

            // Spawn Obstacles (Crates, Tables, Equipment, Structure)
            SpawnFacilityObstacles();

            // Spawn 1-4 Player Spawn Points
            SpawnFacilitySpawnPoints();

            // Spawn Gameplay Task Locations (Markers for 9 rooms)
            SpawnFacilityTaskLocations();

            // Move Player to exact P1 Spawn Point (0, 13, 0) in the middle of Airlock
            GameObject playerGo = GameObject.Find("Player");
            if (playerGo != null)
            {
                Undo.RecordObject(playerGo.transform, "Position Player at P1 Spawn");
                playerGo.transform.position = new Vector3(0.0f, 13.0f, 0f);

                SpriteRenderer playerSr = playerGo.GetComponent<SpriteRenderer>();
                if (playerSr != null)
                {
                    playerSr.sortingLayerName = "Characters";
                }
            }

            // Move Exit_Zone to Airlock_Spawn center if present
            GameObject exitGo = GameObject.Find("Exit_Zone");
            if (exitGo != null)
            {
                Undo.RecordObject(exitGo.transform, "Position Exit_Zone at Airlock Center");
                exitGo.transform.position = new Vector3(0.0f, 13.0f, 0f);
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
            VerifyConnectivity(floorPositions, new Vector3Int(0, 10, 0));
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

            // --- 13 Facility Entrance Doors (Precisely centered in wall openings between perimeter walls) ---
            // 1. Crew Quarters South Door: wall opening row Y=37, span X in [-1, 0]
            CreateDoorInstance("Door_CrewQuarters", doorsRoot.transform, new Vector3(0.0f, 37.5f, 0f), true, hSprite);

            // 2. Med Bay East Door: wall opening col X=-11, span Y in [27, 28]
            CreateDoorInstance("Door_MedBay", doorsRoot.transform, new Vector3(-10.5f, 28.0f, 0f), false, vSprite);

            // 3. Console Alpha West Door: wall opening col X=10, span Y in [27, 28]
            CreateDoorInstance("Door_ConsoleAlpha", doorsRoot.transform, new Vector3(10.5f, 28.0f, 0f), false, vSprite);

            // 4. Airlock North Door: wall opening row Y=18, span X in [-1, 0]
            CreateDoorInstance("Door_Airlock_North", doorsRoot.transform, new Vector3(0.0f, 18.5f, 0f), true, hSprite);

            // 5. Airlock South Door: wall opening row Y=7, span X in [-1, 0]
            CreateDoorInstance("Door_Airlock_South", doorsRoot.transform, new Vector3(0.0f, 7.5f, 0f), true, hSprite);

            // 6. Mess Hall East Door: wall opening col X=-11, span Y in [-5, -4]
            CreateDoorInstance("Door_MessHall", doorsRoot.transform, new Vector3(-10.5f, -4.0f, 0f), false, vSprite);

            // 7. Vent Shaft West Door: wall opening col X=11, span Y in [-5, -4]
            CreateDoorInstance("Door_VentShaft", doorsRoot.transform, new Vector3(11.5f, -4.0f, 0f), false, vSprite);

            // 8. Cargo Hold North Door: wall opening row Y=-14, span X in [-1, 0]
            CreateDoorInstance("Door_CargoHold_North", doorsRoot.transform, new Vector3(0.0f, -13.5f, 0f), true, hSprite);

            // 9. Cargo Hold South Door: wall opening row Y=-25, span X in [-1, 0]
            CreateDoorInstance("Door_CargoHold_South", doorsRoot.transform, new Vector3(0.0f, -24.5f, 0f), true, hSprite);

            // 10. Deep Storage East Door: wall opening col X=-8, span Y in [-47, -46]
            CreateDoorInstance("Door_DeepStorage", doorsRoot.transform, new Vector3(-7.5f, -46.0f, 0f), false, vSprite);

            // 11. Power Relay Bay West Door: wall opening col X=7, span Y in [-47, -46]
            CreateDoorInstance("Door_PowerRelayBay", doorsRoot.transform, new Vector3(7.5f, -46.0f, 0f), false, vSprite);

            // 12. Reactor Core North Door: wall opening row Y=-59, span X in [12, 13]
            CreateDoorInstance("Door_ReactorCore", doorsRoot.transform, new Vector3(13.0f, -58.5f, 0f), true, hSprite);

            // 13. Airlock West Door: wall opening col X=-6, span Y in [12, 13]
            CreateDoorInstance("Door_Airlock_West", doorsRoot.transform, new Vector3(-5.5f, 13.0f, 0f), false, vSprite);
        }

        public static void SpawnBedroomBeds()
        {
            GameObject oldBeds = GameObject.Find("Bedroom_Beds");
            if (oldBeds != null)
            {
                Undo.DestroyObjectImmediate(oldBeds);
            }

            GameObject bedsRoot = new GameObject("Bedroom_Beds");
            Undo.RegisterCreatedObjectUndo(bedsRoot, "Create Bedroom Beds");

            Sprite bedSprite = AssetDatabase.LoadAssetAtPath<Sprite>(TablePngPath);

            // 4 cosmetic intro beds (Transform + SpriteRenderer, NO collider, NO TaskLocation)
            CreateBedInstance("Bed_P1", bedsRoot.transform, new Vector3(-19.5f, 15.0f, 0f), bedSprite);
            CreateBedInstance("Bed_P2", bedsRoot.transform, new Vector3(-14.5f, 15.0f, 0f), bedSprite);
            CreateBedInstance("Bed_P3", bedsRoot.transform, new Vector3(-19.5f, 10.0f, 0f), bedSprite);
            CreateBedInstance("Bed_P4", bedsRoot.transform, new Vector3(-14.5f, 10.0f, 0f), bedSprite);
        }

        private static GameObject CreateBedInstance(string name, Transform parent, Vector3 position, Sprite sprite)
        {
            GameObject bedGo = new GameObject(name);
            bedGo.transform.SetParent(parent, false);
            bedGo.transform.position = position;

            SpriteRenderer sr = bedGo.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = new Color(0.85f, 0.92f, 1f, 1f); // Soft cosmetic tint for bed
            sr.sortingLayerName = "Obstacles";
            sr.sortingOrder = 1;

            return bedGo;
        }

        [MenuItem("Tools/Facility/Spawn Facility Obstacles", false, 3)]
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

            EnsureSpriteMeshTypeFullRect(CratePngPath);
            EnsureSpriteMeshTypeFullRect(CrateStackPngPath);
            EnsureSpriteMeshTypeFullRect(TablePngPath);
            EnsureSpriteMeshTypeFullRect(EquipmentPngPath);
            EnsureSpriteMeshTypeFullRect(StructurePngPath);

            Sprite crateSprite = AssetDatabase.LoadAssetAtPath<Sprite>(CratePngPath);
            Sprite tableSprite = AssetDatabase.LoadAssetAtPath<Sprite>(TablePngPath);
            Sprite equipSprite = AssetDatabase.LoadAssetAtPath<Sprite>(EquipmentPngPath);
            Sprite structSprite = AssetDatabase.LoadAssetAtPath<Sprite>(StructurePngPath);
            Sprite crateStackSprite = AssetDatabase.LoadAssetAtPath<Sprite>(CrateStackPngPath) ?? crateSprite;

            // --- 1. Cargo_Hold (12x10, X: [-6, 5], Y: [-24, -15]) ---
            // Z-shaped forklift aisles with heavy pallet containers & crates (breaks North-South straight sightline)
            CreateObstacleInstance("Obstacle_CargoHold_Pallet_West", obstaclesRoot.transform, new Vector3(-3.0f, -17.5f, 0f), new Vector2(2.0f, 3.0f), crateStackSprite);
            CreateObstacleInstance("Obstacle_CargoHold_Pallet_East", obstaclesRoot.transform, new Vector3(2.5f, -21.5f, 0f), new Vector2(2.0f, 2.0f), crateStackSprite);
            CreateObstacleInstance("Obstacle_CargoHold_Crate_NE", obstaclesRoot.transform, new Vector3(3.5f, -16.5f, 0f), new Vector2(1.0f, 1.0f), crateSprite);
            CreateObstacleInstance("Obstacle_CargoHold_Shelf_East", obstaclesRoot.transform, new Vector3(4.5f, -19.5f, 0f), new Vector2(1.0f, 2.0f), equipSprite);

            // --- 2. Mess_Hall (8x8, X: [-19, -12], Y: [-8, -1]) ---
            // Canteen counter bar in NW + dual parallel dining tables forming a U-loop
            CreateObstacleInstance("Obstacle_MessHall_Counter_North", obstaclesRoot.transform, new Vector3(-16.5f, -1.5f, 0f), new Vector2(3.0f, 1.0f), tableSprite);
            CreateObstacleInstance("Obstacle_MessHall_Table_West", obstaclesRoot.transform, new Vector3(-17.0f, -6.0f, 0f), new Vector2(2.0f, 1.0f), tableSprite);
            CreateObstacleInstance("Obstacle_MessHall_Table_East", obstaclesRoot.transform, new Vector3(-13.5f, -6.0f, 0f), new Vector2(2.0f, 1.0f), tableSprite);

            // --- 3. Crew_Quarters (10x10, X: [-5, 4], Y: [38, 47]) ---
            // Lockers on East wall + partition in NW bunk nook + center lounge table
            CreateObstacleInstance("Obstacle_CrewQuarters_Lockers", obstaclesRoot.transform, new Vector3(3.5f, 43.5f, 0f), new Vector2(1.0f, 3.0f), equipSprite);
            CreateObstacleInstance("Obstacle_CrewQuarters_Partition", obstaclesRoot.transform, new Vector3(-3.5f, 45.5f, 0f), new Vector2(2.0f, 1.0f), structSprite);
            CreateObstacleInstance("Obstacle_CrewQuarters_LoungeTable", obstaclesRoot.transform, new Vector3(-3.0f, 40.5f, 0f), new Vector2(2.0f, 1.0f), tableSprite);

            // --- 4. Deep_Storage (10x10, X: [-18, -9], Y: [-52, -43]) ---
            // Parallel vertical storage racks / archive shelves + SW pallet crate
            CreateObstacleInstance("Obstacle_DeepStorage_Rack_West", obstaclesRoot.transform, new Vector3(-15.5f, -46.5f, 0f), new Vector2(1.0f, 4.0f), equipSprite);
            CreateObstacleInstance("Obstacle_DeepStorage_Rack_East", obstaclesRoot.transform, new Vector3(-11.5f, -48.5f, 0f), new Vector2(1.0f, 4.0f), equipSprite);
            CreateObstacleInstance("Obstacle_DeepStorage_Crate_SW", obstaclesRoot.transform, new Vector3(-16.5f, -50.5f, 0f), new Vector2(1.0f, 1.0f), crateSprite);

            // --- 5. Console_Alpha (10x10, X: [11, 20], Y: [23, 32]) ---
            // 2 Server racks flanking console station + SE cable box + East auxiliary terminal
            CreateObstacleInstance("Obstacle_ConsoleAlpha_ServerRack_North", obstaclesRoot.transform, new Vector3(15.5f, 31.0f, 0f), new Vector2(3.0f, 1.0f), equipSprite);
            CreateObstacleInstance("Obstacle_ConsoleAlpha_ServerRack_South", obstaclesRoot.transform, new Vector3(15.5f, 24.5f, 0f), new Vector2(3.0f, 1.0f), equipSprite);
            CreateObstacleInstance("Obstacle_ConsoleAlpha_CableBox", obstaclesRoot.transform, new Vector3(19.0f, 24.5f, 0f), new Vector2(1.0f, 2.0f), equipSprite);
            CreateObstacleInstance("Obstacle_ConsoleAlpha_AuxRack_East", obstaclesRoot.transform, new Vector3(19.5f, 28.5f, 0f), new Vector2(1.0f, 2.0f), equipSprite);

            // --- 6. Vent_Shaft (9x11, X: [12, 20], Y: [-9, 1]) ---
            // Center industrial exhaust turbine + SE duct pipe + NW air filter box
            CreateObstacleInstance("Obstacle_VentShaft_Turbine_Center", obstaclesRoot.transform, new Vector3(16.5f, -1.0f, 0f), new Vector2(2.0f, 2.0f), structSprite);
            CreateObstacleInstance("Obstacle_VentShaft_Duct_SE", obstaclesRoot.transform, new Vector3(18.5f, -7.0f, 0f), new Vector2(2.0f, 1.0f), equipSprite);
            CreateObstacleInstance("Obstacle_VentShaft_Filter_NW", obstaclesRoot.transform, new Vector3(13.5f, 0.0f, 0f), new Vector2(1.0f, 1.0f), crateSprite);

            // --- 7. Power_Relay_Bay (10x10, X: [8, 17], Y: [-52, -43]) ---
            // East wall conduit racks + SW substation transformer
            CreateObstacleInstance("Obstacle_PowerRelay_Conduit_North", obstaclesRoot.transform, new Vector3(16.5f, -44.0f, 0f), new Vector2(1.0f, 2.0f), equipSprite);
            CreateObstacleInstance("Obstacle_PowerRelay_Conduit_South", obstaclesRoot.transform, new Vector3(16.5f, -50.5f, 0f), new Vector2(1.0f, 2.0f), equipSprite);
            CreateObstacleInstance("Obstacle_PowerRelay_Transformer_SW", obstaclesRoot.transform, new Vector3(10.0f, -50.0f, 0f), new Vector2(2.0f, 1.5f), structSprite);

            // --- 8. Reactor_Core (12x10, X: [7, 18], Y: [-69, -60]) ---
            // West/East coolant tanks + South base pipe + NW/NE heavy conduits
            CreateObstacleInstance("Obstacle_ReactorCore_CoolantTank_West", obstaclesRoot.transform, new Vector3(8.5f, -64.5f, 0f), new Vector2(1.0f, 2.0f), structSprite);
            CreateObstacleInstance("Obstacle_ReactorCore_CoolantTank_East", obstaclesRoot.transform, new Vector3(17.5f, -64.5f, 0f), new Vector2(1.0f, 2.0f), structSprite);
            CreateObstacleInstance("Obstacle_ReactorCore_BasePipe_South", obstaclesRoot.transform, new Vector3(12.5f, -68.5f, 0f), new Vector2(2.0f, 1.0f), tableSprite);
            CreateObstacleInstance("Obstacle_ReactorCore_Conduit_NW", obstaclesRoot.transform, new Vector3(9.0f, -61.5f, 0f), new Vector2(1.5f, 1.0f), equipSprite);
            CreateObstacleInstance("Obstacle_ReactorCore_Conduit_NE", obstaclesRoot.transform, new Vector3(17.0f, -61.5f, 0f), new Vector2(1.5f, 1.0f), equipSprite);

            // --- 9. Evacuation Route Friction Obstacles (MedBay crate removed per visual redesign) ---
            CreateObstacleInstance("Obstacle_Loop_Chicane", obstaclesRoot.transform, new Vector3(12.5f, -33.5f, 0f), new Vector2(1.0f, 1.0f), crateSprite);
            CreateObstacleInstance("Obstacle_NodeC_Center", obstaclesRoot.transform, new Vector3(-2.5f, -32.5f, 0f), new Vector2(1.0f, 1.0f), crateSprite);
            CreateObstacleInstance("Obstacle_NodeB_Center", obstaclesRoot.transform, new Vector3(-3.5f, -1.5f, 0f), new Vector2(1.0f, 1.0f), crateSprite);
            CreateObstacleInstance("Obstacle_NodeA_Center", obstaclesRoot.transform, new Vector3(-2.5f, 30.5f, 0f), new Vector2(1.0f, 1.0f), crateSprite);

            var activeScene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
            if (activeScene.IsValid())
            {
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(activeScene);
            }

            Debug.Log("<color=green>[FacilityObstacles] Đã bố trí thành công hệ thống vật cản chiến thuật cho toàn bộ cơ sở!</color>");
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

            if (colliderSize.x != 1f || colliderSize.y != 1f)
            {
                sr.drawMode = SpriteDrawMode.Tiled;
                sr.size = colliderSize;
            }

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

            // 1-4 Player Spawn Points inside Airlock Spawn room (X: [-5, 4], Y: [8, 17])
            // Center of Airlock is (0.0f, 13.0f)
            // P1 is positioned at the exact center (0.0, 13.0) for single-player / primary spawn
            // P2-P4 are positioned symmetrically around the center
            CreateSpawnPointInstance("SpawnPoint_P1", spawnRoot.transform, new Vector3(0.0f, 13.0f, 0f), 0);
            CreateSpawnPointInstance("SpawnPoint_P2", spawnRoot.transform, new Vector3(2.0f, 13.0f, 0f), 1);
            CreateSpawnPointInstance("SpawnPoint_P3", spawnRoot.transform, new Vector3(-2.0f, 13.0f, 0f), 2);
            CreateSpawnPointInstance("SpawnPoint_P4", spawnRoot.transform, new Vector3(0.0f, 11.0f, 0f), 3);

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
            CreateTaskLocationInstance("TaskLoc_CrewQuarters", taskLocsRoot.transform, new Vector3(-0.5f, 42.5f, 0f), cpSprite, new Vector2(1.0f, 1.0f));
            CreateTaskLocationInstance("TaskLoc_MedBay", taskLocsRoot.transform, new Vector3(-16.5f, 27.5f, 0f), compSprite, new Vector2(1.0f, 1.0f));
            CreateTaskLocationInstance("TaskLoc_ConsoleAlpha", taskLocsRoot.transform, new Vector3(15.5f, 27.5f, 0f), cpSprite, new Vector2(2.0f, 2.0f));
            CreateTaskLocationInstance("TaskLoc_MessHall", taskLocsRoot.transform, new Vector3(-15.5f, -4.5f, 0f), mechSprite, new Vector2(1.0f, 1.0f));
            CreateTaskLocationInstance("TaskLoc_VentShaft", taskLocsRoot.transform, new Vector3(16.5f, -4.5f, 0f), mechSprite, new Vector2(1.0f, 1.0f));
            CreateTaskLocationInstance("TaskLoc_CargoHold", taskLocsRoot.transform, new Vector3(-0.5f, -19.5f, 0f), exitSprite, new Vector2(1.0f, 1.0f));
            CreateTaskLocationInstance("TaskLoc_CargoHold_02", taskLocsRoot.transform, new Vector3(-4.0f, -22.0f, 0f), exitSprite, new Vector2(1.0f, 1.0f));
            CreateTaskLocationInstance("TaskLoc_DeepStorage", taskLocsRoot.transform, new Vector3(-13.5f, -47.5f, 0f), mechSprite, new Vector2(1.0f, 1.0f));
            CreateTaskLocationInstance("TaskLoc_PowerRelayBay", taskLocsRoot.transform, new Vector3(12.5f, -47.5f, 0f), genSprite, new Vector2(2.0f, 2.0f));
            GameObject reactorGo = CreateTaskLocationInstance("TaskLoc_ReactorCore", taskLocsRoot.transform, new Vector3(12.5f, -64.5f, 0f), genSprite, new Vector2(2.0f, 2.0f));
            ReactorCoreConsole reactorConsole = reactorGo.AddComponent<ReactorCoreConsole>();
            GameSessionStateEventChannelSO sessionChannel = AssetDatabase.LoadAssetAtPath<GameSessionStateEventChannelSO>("Assets/_Project/Scripts/4_Data/Events/OnGameSessionStateChanged.asset");
            if (sessionChannel != null)
            {
                SerializedObject consoleSo = new SerializedObject(reactorConsole);
                consoleSo.FindProperty("onGameSessionStateChanged").objectReferenceValue = sessionChannel;
                consoleSo.ApplyModifiedPropertiesWithoutUndo();
            }
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

            PlayerSpawnPoint psp = spGo.AddComponent<PlayerSpawnPoint>();
            psp.Configure(playerIndex);

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
                new Vector3Int(10, 27, 0), new Vector3Int(10, 28, 0),    // Door_ConsoleAlpha
                new Vector3Int(7, -47, 0), new Vector3Int(7, -46, 0),    // Door_PowerRelayBay
                new Vector3Int(12, -59, 0), new Vector3Int(13, -59, 0)   // Door_ReactorCore
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
            string[] nonDoorRooms = new[] { "Crew_Quarters", "Med_Bay", "Airlock_Spawn", "Mess_Hall", "Vent_Shaft", "Cargo_Hold", "Deep_Storage", "Bedroom" };
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

            Vector3Int medBayWestTile = new Vector3Int(-20, 24, 0);
            Vector3Int deepStorageWestTile = new Vector3Int(-18, -47, 0);

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

            if (!IsValidPng(FloorPngPath))
            {
                Texture2D floorTex = CreateBorderedTexture(32, 32, new Color(0.85f, 0.88f, 0.90f, 1f), new Color(0.70f, 0.73f, 0.76f, 1f));
                File.WriteAllBytes(FloorPngPath, floorTex.EncodeToPNG());
                AssetDatabase.ImportAsset(FloorPngPath);
                ConfigureTextureAsSprite(FloorPngPath);
            }

            if (!IsValidPng(WallPngPath))
            {
                Texture2D wallTex = CreateBorderedTexture(32, 32, new Color(0.18f, 0.20f, 0.23f, 1f), new Color(0.08f, 0.09f, 0.11f, 1f));
                File.WriteAllBytes(WallPngPath, wallTex.EncodeToPNG());
                AssetDatabase.ImportAsset(WallPngPath);
                ConfigureTextureAsSprite(WallPngPath);
            }

            if (!IsValidPng(NodePngPath))
            {
                Texture2D nodeTex = CreateBorderedTexture(32, 32, new Color(0.74f, 0.84f, 0.92f, 1f), new Color(0.58f, 0.70f, 0.80f, 1f));
                File.WriteAllBytes(NodePngPath, nodeTex.EncodeToPNG());
                AssetDatabase.ImportAsset(NodePngPath);
                ConfigureTextureAsSprite(NodePngPath);
            }

            // Ensure Obstacle Sprites
            EnsureObstacleSprites();

            AssetDatabase.Refresh();

            Tile floorTile = AssetDatabase.LoadAssetAtPath<Tile>(FloorTilePath);
            if (floorTile == null)
            {
                Sprite floorSprite = AssetDatabase.LoadAssetAtPath<Sprite>(FloorPngPath);
                floorTile = ScriptableObject.CreateInstance<Tile>();
                floorTile.sprite = floorSprite;
                floorTile.colliderType = Tile.ColliderType.None;
                AssetDatabase.CreateAsset(floorTile, FloorTilePath);
            }
            else if (floorTile.sprite == null)
            {
                floorTile.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(FloorPngPath);
                EditorUtility.SetDirty(floorTile);
            }

            Tile wallTile = AssetDatabase.LoadAssetAtPath<Tile>(WallTilePath);
            if (wallTile == null)
            {
                Sprite wallSprite = AssetDatabase.LoadAssetAtPath<Sprite>(WallPngPath);
                wallTile = ScriptableObject.CreateInstance<Tile>();
                wallTile.sprite = wallSprite;
                wallTile.colliderType = Tile.ColliderType.Grid;
                AssetDatabase.CreateAsset(wallTile, WallTilePath);
            }
            else if (wallTile.sprite == null)
            {
                wallTile.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(WallPngPath);
                EditorUtility.SetDirty(wallTile);
            }

            Tile nodeTile = AssetDatabase.LoadAssetAtPath<Tile>(NodeTilePath);
            if (nodeTile == null)
            {
                Sprite nodeSprite = AssetDatabase.LoadAssetAtPath<Sprite>(NodePngPath);
                nodeTile = ScriptableObject.CreateInstance<Tile>();
                nodeTile.sprite = nodeSprite;
                nodeTile.colliderType = Tile.ColliderType.None;
                AssetDatabase.CreateAsset(nodeTile, NodeTilePath);
            }
            else if (nodeTile.sprite == null)
            {
                nodeTile.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(NodePngPath);
                EditorUtility.SetDirty(nodeTile);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static bool IsValidPng(string path)
        {
            if (!File.Exists(path)) return false;
            try
            {
                using (FileStream fs = File.OpenRead(path))
                {
                    if (fs.Length < 8) return false;
                    byte[] header = new byte[8];
                    fs.Read(header, 0, 8);
                    return header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47 &&
                           header[4] == 0x0D && header[5] == 0x0A && header[6] == 0x1A && header[7] == 0x0A;
                }
            }
            catch
            {
                return false;
            }
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

        public static void EnsureSpriteMeshTypeFullRect(string path)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                TextureImporterSettings tis = new TextureImporterSettings();
                importer.ReadTextureSettings(tis);
                if (tis.spriteMeshType != SpriteMeshType.FullRect)
                {
                    tis.spriteMeshType = SpriteMeshType.FullRect;
                    importer.SetTextureSettings(tis);
                    importer.SaveAndReimport();
                }
            }
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

                TextureImporterSettings tis = new TextureImporterSettings();
                importer.ReadTextureSettings(tis);
                tis.spriteMeshType = SpriteMeshType.FullRect;
                importer.SetTextureSettings(tis);

                importer.SaveAndReimport();
            }
        }
    }
}
#endif
