#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace TheLastSignal.EditorTools
{
    public static class PrototypeTilemapSetup
    {
        private const string TileDir = "Assets/_Project/Tiles";
        private const string FloorPngPath = TileDir + "/Tile_Floor.png";
        private const string WallPngPath = TileDir + "/Tile_Wall.png";
        private const string NodePngPath = TileDir + "/Tile_Node.png";
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
            // --- 10 Rooms (Expanded distance) ---
            new MapBlock("Crew_Quarters", BlockType.Room, -5, 28, 4, 35),
            new MapBlock("Med_Bay", BlockType.Room, -21, 14, -14, 19),
            new MapBlock("Console_Alpha", BlockType.Room, 13, 13, 22, 20),
            new MapBlock("Airlock_Spawn", BlockType.Room, -4, 0, 3, 7),
            new MapBlock("Mess_Hall", BlockType.Room, -21, -17, -14, -10),
            new MapBlock("Vent_Shaft", BlockType.Room, 13, -18, 20, -9),
            new MapBlock("Cargo_Hold", BlockType.Room, -6, -36, 5, -27),
            new MapBlock("Deep_Storage", BlockType.Room, -23, -65, -14, -58),
            new MapBlock("Power_Relay_Bay", BlockType.Room, 13, -65, 22, -58),
            new MapBlock("Reactor_Core", BlockType.Room, 13, -84, 22, -75),

            // --- 3 Nodes ---
            new MapBlock("Node_A", BlockType.Node, -3, 14, 2, 19),
            new MapBlock("Node_B", BlockType.Node, -3, -16, 2, -11),
            new MapBlock("Node_C", BlockType.Node, -3, -52, 2, -47),

            // --- Elongated Corridors (North-South Main Spine) ---
            new MapBlock("Corridor_Crew_NodeA", BlockType.Corridor, -2, 20, 1, 27),
            new MapBlock("Corridor_NodeA_Airlock", BlockType.Corridor, -2, 8, 1, 13),
            new MapBlock("Corridor_Airlock_NodeB", BlockType.Corridor, -2, -10, 1, -1),
            new MapBlock("Corridor_NodeB_Cargo", BlockType.Corridor, -2, -26, 1, -17),
            new MapBlock("Corridor_Cargo_NodeC", BlockType.Corridor, -2, -46, 1, -37),

            // --- Elongated Lateral Corridors (East-West) ---
            new MapBlock("Corridor_MedBay_NodeA", BlockType.Corridor, -13, 16, -4, 17),
            new MapBlock("Corridor_NodeA_Console", BlockType.Corridor, 3, 16, 12, 17),
            new MapBlock("Corridor_MessHall_NodeB", BlockType.Corridor, -13, -14, -4, -13),
            new MapBlock("Corridor_NodeB_VentShaft", BlockType.Corridor, 3, -14, 12, -13),

            // --- Node C T-Junction Corridors ---
            new MapBlock("Corridor_C_Stem", BlockType.Corridor, -2, -62, 1, -53),
            new MapBlock("Corridor_C_Branch", BlockType.Corridor, -13, -62, 12, -61),

            // --- Power Relay to Reactor Core Corridor ---
            new MapBlock("Corridor_Relay_Reactor", BlockType.Corridor, 16, -74, 19, -66),

            // --- Elongated Loop Corridor (Vent Shaft South -> Node C East) ---
            new MapBlock("Loop_Vertical", BlockType.Corridor, 15, -50, 17, -19),
            new MapBlock("Loop_Horizontal_Bend", BlockType.Corridor, 2, -50, 15, -49)
        };

        [InitializeOnLoadMethod]
        private static void OnEditorLoaded()
        {
            EnsureTileAssets();
        }

        [MenuItem("Tools/Facility/Generate Prototype Tilemap", false, 1)]
        public static void GeneratePrototypeTilemap()
        {
            EnsureTileAssets();

            TileBase floorTile = AssetDatabase.LoadAssetAtPath<TileBase>(FloorTilePath);
            TileBase wallTile = AssetDatabase.LoadAssetAtPath<TileBase>(WallTilePath);
            TileBase nodeTile = AssetDatabase.LoadAssetAtPath<TileBase>(NodeTilePath);

            if (floorTile == null || wallTile == null || nodeTile == null)
            {
                Debug.LogError("[FacilityTilemap] Failed to load Tile assets!");
                return;
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

            // Move Player to Airlock Spawn center (-0.5, 3.5, 0)
            GameObject playerGo = GameObject.Find("Player");
            if (playerGo != null)
            {
                Undo.RecordObject(playerGo.transform, "Position Player at Airlock Spawn");
                playerGo.transform.position = new Vector3(-0.5f, 3.5f, 0f);

                SpriteRenderer playerSr = playerGo.GetComponent<SpriteRenderer>();
                if (playerSr != null)
                {
                    playerSr.sortingLayerName = "Characters";
                }
            }

            // Deactivate old primitives if present
            GameObject oldWallA = GameObject.Find("Obstacle_Wall_A");
            if (oldWallA != null) oldWallA.SetActive(false);
            GameObject oldWallB = GameObject.Find("Obstacle_Wall_B");
            if (oldWallB != null) oldWallB.SetActive(false);

            EditorUtility.SetDirty(gridGo);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gridGo.scene);

            Selection.activeGameObject = gridGo;
            Debug.Log("<color=green>[FacilityTilemap] Expanded Research Facility generated successfully!</color>");
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

        private static void VerifyConnectivity(HashSet<Vector3Int> allFloorTiles, Vector3Int startPos)
        {
            if (!allFloorTiles.Contains(startPos))
            {
                Debug.LogError($"[FacilityTilemap] Start position {startPos} is not a valid floor tile!");
                return;
            }

            Queue<Vector3Int> queue = new Queue<Vector3Int>();
            HashSet<Vector3Int> visited = new HashSet<Vector3Int>();

            queue.Enqueue(startPos);
            visited.Add(startPos);

            Vector3Int[] cardinal = new[]
            {
                new Vector3Int(-1, 0, 0), new Vector3Int(1, 0, 0),
                new Vector3Int(0, -1, 0), new Vector3Int(0, 1, 0)
            };

            while (queue.Count > 0)
            {
                Vector3Int current = queue.Dequeue();
                foreach (var dir in cardinal)
                {
                    Vector3Int neighbor = current + dir;
                    if (allFloorTiles.Contains(neighbor) && !visited.Contains(neighbor))
                    {
                        visited.Add(neighbor);
                        queue.Enqueue(neighbor);
                    }
                }
            }

            if (visited.Count == allFloorTiles.Count)
            {
                Debug.Log($"<color=cyan>[FacilityTilemap] CONNECTIVITY VERIFIED 100%: All {visited.Count} floor tiles across expanded facility are fully connected!</color>");
            }
            else
            {
                Debug.LogError($"[FacilityTilemap] CONNECTIVITY FAILED! Reachable: {visited.Count}, Total: {allFloorTiles.Count}.");
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
