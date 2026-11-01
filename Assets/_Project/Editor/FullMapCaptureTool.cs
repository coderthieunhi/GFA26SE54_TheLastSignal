#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace TheLastSignal.EditorTools
{
    public static class FullMapCaptureTool
    {
        [MenuItem("Tools/Facility/Capture Full Map (White Background)", false, 70)]
        public static void CaptureFullMapWhite()
        {
            const int width = 3072;
            const int height = 6144;
            const string outputDir = "Assets/Screenshots";

            if (!Directory.Exists(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }

            var gLightObj = GameObject.Find("Global Light 2D");
            Light2D gLight = gLightObj != null ? gLightObj.GetComponent<Light2D>() : null;
            float origIntensity = gLight != null ? gLight.intensity : 0.17f;
            Color origColor = gLight != null ? gLight.color : Color.white;

            var camObj = new GameObject("TempMapCaptureCamera");
            var cam = camObj.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 64f;
            cam.transform.position = new Vector3(-1.06f, -10.5f, -10f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.white;

            var urpData = camObj.AddComponent<UniversalAdditionalCameraData>();
            urpData.renderPostProcessing = false;

            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;

            // 1. Capture Illuminated / Blueprint View
            if (gLight != null)
            {
                gLight.intensity = 0.95f;
                gLight.color = Color.white;
            }
            cam.Render();

            RenderTexture.active = rt;
            var texIlluminated = new Texture2D(width, height, TextureFormat.RGB24, false);
            texIlluminated.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            texIlluminated.Apply();
            string pathIlluminated = Path.Combine(outputDir, "full_facility_map_white_illuminated.png");
            File.WriteAllBytes(pathIlluminated, texIlluminated.EncodeToPNG());
            Object.DestroyImmediate(texIlluminated);

            // 2. Capture Atmospheric / Horror Lit View
            if (gLight != null)
            {
                gLight.intensity = origIntensity;
                gLight.color = origColor;
            }
            cam.Render();

            var texAtmospheric = new Texture2D(width, height, TextureFormat.RGB24, false);
            texAtmospheric.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            texAtmospheric.Apply();
            string pathAtmospheric = Path.Combine(outputDir, "full_facility_map_white_atmospheric.png");
            File.WriteAllBytes(pathAtmospheric, texAtmospheric.EncodeToPNG());
            Object.DestroyImmediate(texAtmospheric);

            // Cleanup
            cam.targetTexture = null;
            RenderTexture.active = null;
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(camObj);

            AssetDatabase.Refresh();
            Debug.Log($"<color=green>[FullMapCaptureTool] Captured full map to '{pathIlluminated}' and '{pathAtmospheric}'</color>");
        }
    }
}
#endif
