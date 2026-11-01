using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

public class UpdateTilesV2
{
    [MenuItem("Tools/Apply V2 Tiles")]
    public static void Apply()
    {
        Sprite floorSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/CrewQuarters/V2/crew_floor_tile.jpg");
        Sprite wallHSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/CrewQuarters/V2/crew_wall_horizontal.jpg");
        Sprite wallVSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/CrewQuarters/V2/crew_wall_vertical.jpg");
        
        if (floorSprite == null || wallHSprite == null || wallVSprite == null)
        {
            Debug.LogError("Failed to load V2 sprites!");
            return;
        }

        Tile floorTile = AssetDatabase.LoadAssetAtPath<Tile>("Assets/Sprites/CrewQuarters/V2/FloorTileV2.asset");
        if (floorTile == null) {
            floorTile = ScriptableObject.CreateInstance<Tile>();
            floorTile.sprite = floorSprite;
            AssetDatabase.CreateAsset(floorTile, "Assets/Sprites/CrewQuarters/V2/FloorTileV2.asset");
        } else {
            floorTile.sprite = floorSprite;
            EditorUtility.SetDirty(floorTile);
        }
        
        Tile wallHTile = AssetDatabase.LoadAssetAtPath<Tile>("Assets/Sprites/CrewQuarters/V2/WallHTileV2.asset");
        if (wallHTile == null) {
            wallHTile = ScriptableObject.CreateInstance<Tile>();
            wallHTile.sprite = wallHSprite;
            AssetDatabase.CreateAsset(wallHTile, "Assets/Sprites/CrewQuarters/V2/WallHTileV2.asset");
        } else {
            wallHTile.sprite = wallHSprite;
            EditorUtility.SetDirty(wallHTile);
        }

        Tile wallVTile = AssetDatabase.LoadAssetAtPath<Tile>("Assets/Sprites/CrewQuarters/V2/WallVTileV2.asset");
        if (wallVTile == null) {
            wallVTile = ScriptableObject.CreateInstance<Tile>();
            wallVTile.sprite = wallVSprite;
            AssetDatabase.CreateAsset(wallVTile, "Assets/Sprites/CrewQuarters/V2/WallVTileV2.asset");
        } else {
            wallVTile.sprite = wallVSprite;
            EditorUtility.SetDirty(wallVTile);
        }
        
        AssetDatabase.SaveAssets();

        Grid grid = GameObject.FindAnyObjectByType<Grid>();
        if (grid == null) {
            GameObject gridGo = GameObject.Find("Facility_Grid") ?? GameObject.Find("Grid");
            if (gridGo != null) grid = gridGo.GetComponent<Grid>();
        }

        if (grid == null) {
            Debug.LogError("Grid not found");
            return;
        }

        Tilemap floorMap = null;
        Tilemap wallMap = null;
        Tilemap[] allTilemaps = grid.GetComponentsInChildren<Tilemap>(true);
        foreach (Tilemap tm in allTilemaps) {
            if (tm.name.IndexOf("Floor", System.StringComparison.OrdinalIgnoreCase) >= 0) floorMap = tm;
            else if (tm.name.IndexOf("Wall", System.StringComparison.OrdinalIgnoreCase) >= 0) wallMap = tm;
        }

        if (floorMap == null && allTilemaps.Length > 0) floorMap = allTilemaps[0];

        // Fill floor
        for (int x = -5; x <= 4; x++) {
            for (int y = 38; y <= 47; y++) {
                floorMap.SetTile(new Vector3Int(x, y, 0), floorTile);
            }
        }
        
        // Fill walls
        if (wallMap != null) {
            for (int x = -5; x <= 4; x++) wallMap.SetTile(new Vector3Int(x, 48, 0), wallHTile);
            for (int x = -5; x <= 4; x++) {
                if (x == -1 || x == 0) continue;
                wallMap.SetTile(new Vector3Int(x, 37, 0), wallHTile);
            }
            for (int y = 38; y <= 47; y++) wallMap.SetTile(new Vector3Int(-6, y, 0), wallVTile);
            for (int y = 38; y <= 47; y++) wallMap.SetTile(new Vector3Int(5, y, 0), wallVTile);
            
            // Corners
            wallMap.SetTile(new Vector3Int(-6, 48, 0), wallVTile);
            wallMap.SetTile(new Vector3Int(5, 48, 0), wallVTile);
            wallMap.SetTile(new Vector3Int(-6, 37, 0), wallVTile);
            wallMap.SetTile(new Vector3Int(5, 37, 0), wallVTile);
        }
        
        Debug.Log("Applied V2 tiles successfully!");
    }
}
