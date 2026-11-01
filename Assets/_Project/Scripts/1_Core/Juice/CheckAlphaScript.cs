#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.IO;
using System.Collections.Generic;

public class CheckAlphaScript
{
    [MenuItem("Tools/Facility/Check Alpha Jagged Edges")]
    public static void CheckAlpha()
    {
        string[] searchPaths = new string[] {
            "Assets/_Project/Sprites/Environments/MedBay/Props",
            "Assets/_Project/Sprites/Environments/Airlock/Props"
        };
        
        List<string> jaggedFiles = new List<string>();

        foreach(string path in searchPaths)
        {
            if (!Directory.Exists(path)) continue;
            string[] files = Directory.GetFiles(path, "*.png", SearchOption.AllDirectories);
            foreach(string file in files)
            {
                Texture2D tex = new Texture2D(2, 2);
                byte[] data = File.ReadAllBytes(file);
                tex.LoadImage(data);
                
                Color32[] pixels = tex.GetPixels32();
                int totalAlpha = 0;
                int semiAlpha = 0;
                
                foreach(var p in pixels)
                {
                    if (p.a > 0)
                    {
                        totalAlpha++;
                        if (p.a < 250) semiAlpha++;
                    }
                }
                
                if (totalAlpha > 0)
                {
                    float ratio = (float)semiAlpha / totalAlpha;
                    if (ratio < 0.02f) // Less than 2% soft pixels -> hard cut
                    {
                        jaggedFiles.Add(file + " (Ratio: " + ratio + ")");
                    }
                }
                Object.DestroyImmediate(tex);
            }
        }
        
        File.WriteAllLines("jagged_edges_report.txt", jaggedFiles.ToArray());
        Debug.Log("Finished checking alpha! Found " + jaggedFiles.Count + " jagged files.");
    }
}
#endif
