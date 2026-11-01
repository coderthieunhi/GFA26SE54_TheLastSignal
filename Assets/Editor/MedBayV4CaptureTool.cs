#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

public static class MedBayV4CaptureTool
{
    [MenuItem("Tools/Capture MedBay v4 GameView and Room")]
    public static void CaptureAll()
    {
        string outDir = "Docs/VisualPass_MedBay_v4";
        if (!Directory.Exists(outDir)) Directory.CreateDirectory(outDir);

        Camera mainCam = Camera.main;
        if (mainCam == null) mainCam = Object.FindAnyObjectByType<Camera>();

        if (mainCam != null)
        {
            // 1. Capture Full GameView (1920x1080)
            RenderTexture rtGame = new RenderTexture(1920, 1080, 24);
            RenderTexture prev = mainCam.targetTexture;
            mainCam.targetTexture = rtGame;
            mainCam.Render();

            RenderTexture.active = rtGame;
            Texture2D texGame = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
            texGame.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
            texGame.Apply();

            mainCam.targetTexture = prev;
            RenderTexture.active = null;
            Object.DestroyImmediate(rtGame);

            string pathGame = Path.Combine(outDir, "02_After_AllPhases_GameView.png");
            File.WriteAllBytes(pathGame, texGame.EncodeToPNG());
            Object.DestroyImmediate(texGame);
            Debug.Log($"[MedBayV4CaptureTool] Saved full GameView to {pathGame}");

            // 2. Capture Standalone 2048px Lit Room
            Vector3 origPos = mainCam.transform.position;
            float origOrtho = mainCam.orthographicSize;

            mainCam.transform.position = new Vector3(-16.0f, 27.5f, origPos.z);
            mainCam.orthographicSize = 5.2f;

            int roomW = 2048;
            int roomH = 1862;
            RenderTexture rtRoom = new RenderTexture(roomW, roomH, 24);
            mainCam.targetTexture = rtRoom;
            mainCam.Render();

            RenderTexture.active = rtRoom;
            Texture2D texRoom = new Texture2D(roomW, roomH, TextureFormat.RGB24, false);
            texRoom.ReadPixels(new Rect(0, 0, roomW, roomH), 0, 0);
            texRoom.Apply();

            mainCam.targetTexture = prev;
            RenderTexture.active = null;
            Object.DestroyImmediate(rtRoom);

            // Restore camera
            mainCam.transform.position = origPos;
            mainCam.orthographicSize = origOrtho;

            string pathRoom = Path.Combine(outDir, "05_MedBay_Room_Lit_HD.png");
            File.WriteAllBytes(pathRoom, texRoom.EncodeToPNG());
            Object.DestroyImmediate(texRoom);
            Debug.Log($"[MedBayV4CaptureTool] Saved HD room capture to {pathRoom}");
        }
    }
}
#endif
