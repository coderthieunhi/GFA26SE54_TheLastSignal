#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class MedBayPlayModeCapture
{
    private const string PrefKey = "MedBay_Capture_Requested";

    [MenuItem("Tools/Capture MedBay in Play Mode")]
    public static void StartCaptureInPlayMode()
    {
        string scenePath = "Assets/Scenes/SampleScene_VisualPass_MedBay_v4.unity";
        if (EditorSceneManager.GetActiveScene().path != scenePath)
        {
            EditorSceneManager.OpenScene(scenePath);
        }

        EditorPrefs.SetBool(PrefKey, true);
        EditorApplication.isPlaying = true;
    }

    static MedBayPlayModeCapture()
    {
        EditorApplication.update += OnEditorUpdate;
    }

    private static int _frameWait = 0;

    private static void OnEditorUpdate()
    {
        if (!EditorPrefs.GetBool(PrefKey, false)) return;

        if (EditorApplication.isPlaying && !EditorApplication.isPaused)
        {
            _frameWait++;
            if (_frameWait > 40) // Wait ~40 frames for all lights and post-processing to settle
            {
                DoCapture();
                EditorPrefs.SetBool(PrefKey, false);
                _frameWait = 0;
                EditorApplication.isPlaying = false;
            }
        }
    }

    private static void DoCapture()
    {
        string outDir = "Docs/VisualPass_MedBay_v4";
        if (!Directory.Exists(outDir)) Directory.CreateDirectory(outDir);

        Camera cam = Camera.main;
        if (cam == null) cam = Object.FindAnyObjectByType<Camera>();

        if (cam != null)
        {
            Vector3 origPos = cam.transform.position;
            float origOrtho = cam.orthographicSize;

            // 1. Full GameView (1920x1080)
            int gW = 1920;
            int gH = 1080;
            RenderTexture rtGame = RenderTexture.GetTemporary(gW, gH, 24, RenderTextureFormat.ARGB32);
            RenderTexture prev = cam.targetTexture;
            cam.targetTexture = rtGame;
            cam.Render();

            RenderTexture.active = rtGame;
            Texture2D texGame = new Texture2D(gW, gH, TextureFormat.RGB24, false);
            texGame.ReadPixels(new Rect(0, 0, gW, gH), 0, 0);
            texGame.Apply();

            string pathGame = Path.Combine(outDir, "02_After_AllPhases_GameView.png");
            File.WriteAllBytes(pathGame, texGame.EncodeToPNG());
            Object.DestroyImmediate(texGame);
            RenderTexture.ReleaseTemporary(rtGame);

            // 2. Focused MedBay Room Lit HD (2048x1862)
            cam.transform.position = new Vector3(-16.0f, 27.5f, origPos.z);
            cam.orthographicSize = 5.2f;

            int rW = 2048;
            int rH = 1862;
            RenderTexture rtRoom = RenderTexture.GetTemporary(rW, rH, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rtRoom;
            cam.Render();

            RenderTexture.active = rtRoom;
            Texture2D texRoom = new Texture2D(rW, rH, TextureFormat.RGB24, false);
            texRoom.ReadPixels(new Rect(0, 0, rW, rH), 0, 0);
            texRoom.Apply();

            string pathRoom = Path.Combine(outDir, "05_MedBay_Room_Lit_HD.png");
            File.WriteAllBytes(pathRoom, texRoom.EncodeToPNG());
            Object.DestroyImmediate(texRoom);
            RenderTexture.ReleaseTemporary(rtRoom);

            // Restore camera
            cam.targetTexture = prev;
            RenderTexture.active = null;
            cam.transform.position = origPos;
            cam.orthographicSize = origOrtho;

            Debug.Log("[MedBayPlayModeCapture] Successfully captured PlayMode GameView & Room Lit HD!");
        }
    }
}
#endif
