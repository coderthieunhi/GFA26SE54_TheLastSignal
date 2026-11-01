using UnityEditor;
using UnityEngine;

public static class SetupNewSprites
{
    [MenuItem("Tools/Setup V2 Sprites")]
    public static void SetupSprites()
    {
        string[] paths = {
            "Assets/Sprites/CrewQuarters/V2/crew_floor_tile.jpg",
            "Assets/Sprites/CrewQuarters/V2/crew_wall_horizontal.jpg",
            "Assets/Sprites/CrewQuarters/V2/crew_wall_vertical.jpg"
        };
        
        foreach(var path in paths) {
            AssetDatabase.ImportAsset(path);
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null) {
                importer.textureType = TextureImporterType.Sprite;
                importer.spritePixelsPerUnit = 64;
                importer.filterMode = FilterMode.Point;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
                Debug.Log("Configured sprite: " + path);
            }
        }
    }
}
