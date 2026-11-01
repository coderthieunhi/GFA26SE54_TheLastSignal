using UnityEngine;
using UnityEditor;
using System.IO;

public static class ImportPixelArtCrewQuarters
{
    [MenuItem("Tools/LastSignal/Import CrewQuarters Pixel Art Pack")]
    public static void ConfigureImporters()
    {
        string rootPath = "Assets/_Project/Sprites/Environments/CrewQuarters_PixelArt";
        if (!Directory.Exists(rootPath))
        {
            Debug.LogWarning("Directory not found: " + rootPath);
            return;
        }

        string[] files = Directory.GetFiles(rootPath, "*.png", SearchOption.AllDirectories);
        int count = 0;
        foreach (string file in files)
        {
            string assetPath = file.Replace("\\", "/");
            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spritePixelsPerUnit = 64;
                importer.filterMode = FilterMode.Point;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.SaveAndReimport();
                count++;
            }
        }
        Debug.Log($"[ImportPixelArtCrewQuarters] Configured and reimported {count} pixel art sprites with PPU=64, Point filter, Uncompressed.");
    }
}
