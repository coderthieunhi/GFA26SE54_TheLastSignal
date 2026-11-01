#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

public static class CaptureSceneViewScreenshot
{
    [MenuItem("Tools/Capture Scene View Screenshot")]
    public static void Capture()
    {
        SceneView view = SceneView.lastActiveSceneView;
        if (view == null && SceneView.sceneViews.Count > 0)
        {
            view = (SceneView)SceneView.sceneViews[0];
        }

        if (view != null)
        {
            // Focus on Crew Quarters center
            view.LookAt(new Vector3(-0.5f, 42.5f, 0f), Quaternion.identity, 8.5f);
            view.Repaint();

            Camera cam = view.camera;
            if (cam != null)
            {
                int width = 1200;
                int height = 1200;
                RenderTexture rt = new RenderTexture(width, height, 24);
                RenderTexture prev = cam.targetTexture;
                cam.targetTexture = rt;
                cam.Render();

                RenderTexture.active = rt;
                Texture2D tex = new Texture2D(width, height, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                tex.Apply();

                cam.targetTexture = prev;
                RenderTexture.active = null;
                Object.DestroyImmediate(rt);

                byte[] bytes = tex.EncodeToPNG();
                Object.DestroyImmediate(tex);

                string outPath = "Assets/Screenshots/CrewQuarters_PixelArt_SceneView.png";
                File.WriteAllBytes(outPath, bytes);
                AssetDatabase.Refresh();
                Debug.Log($"Scene view screenshot captured to {outPath}");
            }
        }
    }
}
#endif
