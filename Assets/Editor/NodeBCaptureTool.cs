#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace TheLastSignal.EditorTools
{
    public static class NodeBCaptureTool
    {
        private const string OutDir = "Docs/NodeB_Production";

        [MenuItem("Tools/Capture Node B GameView Suite", false, 56)]
        public static void CaptureGameViewSuite()
        {
            if (!Directory.Exists(OutDir)) Directory.CreateDirectory(OutDir);

            Camera cam = Camera.main ?? Object.FindAnyObjectByType<Camera>();
            if (cam == null)
            {
                Debug.LogError("No camera found for capture!");
                return;
            }

            Vector3 origPos = cam.transform.position;
            float origOrtho = cam.orthographicSize;
            RenderTexture origRT = cam.targetTexture;

            try
            {
                // Pre-simulate particle systems so ambient dust motes and cryogenic mist are fully rendered
                var allPs = Object.FindObjectsByType<ParticleSystem>(FindObjectsInactive.Exclude);
                foreach (var ps in allPs)
                {
                    ps.Simulate(3.0f, true, true);
                }

                var chaser = Object.FindAnyObjectByType<TheLastSignal.Juice.NodeBRedConduitChaser>();
                if (chaser != null)
                {
                    chaser.ClearWave();
                }

                // 1. Central Hub Focused Close-Up (Steady Floor Grazing Sheen)
                CaptureView(cam, new Vector3(0.0f, -3.0f, origPos.z), 5.2f, 1920, 1080, Path.Combine(OutDir, "node_b_gameview_hub.png"));

                // 1b. Node B Full Room Panorama (Complete Hub walls, chamfers and 4 entrances)
                CaptureView(cam, new Vector3(0.0f, -3.0f, origPos.z), 6.8f, 1920, 1080, Path.Combine(OutDir, "node_b_gameview_panorama.png"));

                // 1c. Central Hub & Panorama with Active High-Voltage Surge Wave Pulse (Chaser Surge Wave in action)
                if (chaser != null)
                {
                    chaser.SetWavePosition(4.0f);
                }
                CaptureView(cam, new Vector3(0.0f, -3.0f, origPos.z), 5.2f, 1920, 1080, Path.Combine(OutDir, "node_b_gameview_hub_surge.png"));
                CaptureView(cam, new Vector3(0.0f, -3.0f, origPos.z), 6.8f, 1920, 1080, Path.Combine(OutDir, "node_b_gameview_panorama_surge.png"));

                if (chaser != null)
                {
                    chaser.ClearWave();
                }

                // 2. 4-Way Cross Junction (Unobstructed Routes, Game View)
                CaptureView(cam, new Vector3(0.0f, -3.0f, origPos.z), 8.5f, 1920, 1080, Path.Combine(OutDir, "node_b_gameview_cross_junction.png"));

                // 3. Full 5-Space Overview (Node B + All 4 Connected Rooms 100% Uncropped, Game View)
                CaptureView(cam, new Vector3(0.0f, -3.0f, origPos.z), 23.5f, 2560, 1440, Path.Combine(OutDir, "node_b_gameview_grand_overview.png"));

                // 4. North Room: Airlock / Spawn (Full Room & Bulkheads, Game View)
                CaptureView(cam, new Vector3(0.0f, 13.0f, origPos.z), 6.2f, 1920, 1080, Path.Combine(OutDir, "node_b_gameview_room_airlock.png"));

                // 5. South Room: Cargo Hold (Full Logistics Lane, Pallets & Crates, Game View)
                CaptureView(cam, new Vector3(0.0f, -19.0f, origPos.z), 7.2f, 1920, 1080, Path.Combine(OutDir, "node_b_gameview_room_cargohold.png"));

                // 6. West Room: Mess Hall (Centered on 4 Dining Tables & Kitchen, Game View)
                CaptureView(cam, new Vector3(-14.5f, -4.0f, origPos.z), 5.8f, 1920, 1080, Path.Combine(OutDir, "node_b_gameview_room_messhall.png"));

                // 7. East Room: Vent Shaft (Centered on Fan Assembly & Maintenance Pit, Game View)
                CaptureView(cam, new Vector3(16.0f, -4.0f, origPos.z), 6.2f, 1920, 1080, Path.Combine(OutDir, "node_b_gameview_room_ventshaft.png"));

                Debug.Log("<color=green>[NodeBCaptureTool] All GameView screenshots captured successfully without Editor gizmos!</color>");
            }
            finally
            {
                cam.transform.position = origPos;
                cam.orthographicSize = origOrtho;
                cam.targetTexture = origRT;
                RenderTexture.active = null;
            }
        }

        private static void CaptureView(Camera cam, Vector3 pos, float ortho, int w, int h, string outFile)
        {
            cam.transform.position = pos;
            cam.orthographicSize = ortho;

            RenderTexture rt = new RenderTexture(w, h, 24);
            cam.targetTexture = rt;
            cam.Render();

            RenderTexture.active = rt;
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();

            cam.targetTexture = null;
            RenderTexture.active = null;
            Object.DestroyImmediate(rt);

            byte[] bytes = tex.EncodeToPNG();
            File.WriteAllBytes(outFile, bytes);
            Object.DestroyImmediate(tex);

            Debug.Log($"Saved GameView screenshot to {outFile}");
        }
    }
}
#endif
