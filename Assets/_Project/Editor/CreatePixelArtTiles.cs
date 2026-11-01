using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEditor;
using System.IO;

public static class CreatePixelArtTiles
{
    [MenuItem("Tools/LastSignal/Create CrewQuarters Pixel Art Tiles")]
    public static void GenerateTiles()
    {
        string spriteFolder = "Assets/_Project/Sprites/Environments/CrewQuarters_PixelArt/Tiles";
        string tileFolder = "Assets/_Project/Tiles/CrewQuarters_PixelArt";

        if (!Directory.Exists(tileFolder))
        {
            Directory.CreateDirectory(tileFolder);
            AssetDatabase.Refresh();
        }

        string[] spriteFiles = Directory.GetFiles(spriteFolder, "*.png");
        int count = 0;

        foreach (string file in spriteFiles)
        {
            string assetPath = file.Replace("\\", "/");
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
            if (sprite != null)
            {
                string tileName = Path.GetFileNameWithoutExtension(assetPath);
                string tileAssetPath = $"{tileFolder}/{tileName}.asset";

                Tile tile = AssetDatabase.LoadAssetAtPath<Tile>(tileAssetPath);
                if (tile == null)
                {
                    tile = ScriptableObject.CreateInstance<Tile>();
                    AssetDatabase.CreateAsset(tile, tileAssetPath);
                }

                tile.sprite = sprite;
                tile.color = Color.white;
                tile.colliderType = tileName.Contains("Wall") ? Tile.ColliderType.Grid : Tile.ColliderType.None;

                EditorUtility.SetDirty(tile);
                count++;
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[CreatePixelArtTiles] Successfully created/updated {count} pixel art tile assets at {tileFolder}");
    }
}
