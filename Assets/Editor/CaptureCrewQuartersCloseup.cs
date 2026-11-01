#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

public static class CaptureCrewQuartersCloseup
{
    [MenuItem("Tools/Capture Crew Quarters Closeup")]
    public static void Capture()
    {
        SceneView view = SceneView.lastActiveSceneView;
        if (view == null && SceneView.sceneViews.Count > 0)
        {
            view = (SceneView)SceneView.sceneViews[0];
        }

        int width = 1024;
        int height = 1024;

        GameObject camGo = new GameObject("TempScreenshotCam");
        Camera cam = camGo.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = 6.8f;
        cam.transform.position = new Vector3(0.0f, 43.0f, -10.0f);
        cam.transform.rotation = Quaternion.identity;
        cam.backgroundColor = new Color(0.12f, 0.13f, 0.15f, 1.0f);
        cam.clearFlags = CameraClearFlags.SolidColor;

        RenderTexture rt = new RenderTexture(width, height, 24);
        cam.targetTexture = rt;
        cam.Render();

        RenderTexture.active = rt;
        Texture2D tex = new Texture2D(width, height, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        tex.Apply();

        RenderTexture.active = null;
        cam.targetTexture = null;
        Object.DestroyImmediate(rt);
        Object.DestroyImmediate(camGo);

        byte[] bytes = tex.EncodeToPNG();
        Object.DestroyImmediate(tex);

        string outPath = "Assets/Screenshots/CrewQuarters_PixelArt_Closeup.png";
        File.WriteAllBytes(outPath, bytes);
        AssetDatabase.Refresh();
        Debug.Log($"Closeup captured to {outPath}");
    }
}
#endif
