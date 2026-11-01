#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace TheLastSignal.EditorTools
{
    public class SciFiHorrorPolishSetup
    {
        [MenuItem("Tools/Facility/Apply Sci-Fi Horror Polish", false, 100)]
        public static void ApplyPolish()
        {
            // 1. Setup Global Post-Processing
            GameObject ppVolume = GameObject.Find("Global_PostProcessing");
            if (ppVolume == null)
            {
                ppVolume = new GameObject("Global_PostProcessing");
            }
            
            Volume volume = ppVolume.GetComponent<Volume>();
            if (volume == null) volume = ppVolume.AddComponent<Volume>();
            volume.isGlobal = true;
            
            // Create profile
            VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/_Project/Settings/SciFiHorrorProfile.asset");
            if (profile == null)
            {
                System.IO.Directory.CreateDirectory("Assets/_Project/Settings");
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, "Assets/_Project/Settings/SciFiHorrorProfile.asset");
            }
            
            // Add overrides
            if (!profile.Has<Vignette>())
            {
                Vignette vignette = profile.Add<Vignette>();
                vignette.intensity.overrideState = true;
                vignette.intensity.value = 0.45f;
                vignette.smoothness.overrideState = true;
                vignette.smoothness.value = 0.8f;
                vignette.color.overrideState = true;
                vignette.color.value = new Color(0.02f, 0.05f, 0.05f); // Deep dark cyan-ish black
            }
            
            if (!profile.Has<Bloom>())
            {
                Bloom bloom = profile.Add<Bloom>();
                bloom.intensity.overrideState = true;
                bloom.intensity.value = 1.2f;
                bloom.threshold.overrideState = true;
                bloom.threshold.value = 0.8f;
                bloom.scatter.overrideState = true;
                bloom.scatter.value = 0.7f;
            }
            
            if (!profile.Has<FilmGrain>())
            {
                FilmGrain grain = profile.Add<FilmGrain>();
                grain.intensity.overrideState = true;
                grain.intensity.value = 0.3f;
                grain.type.overrideState = true;
                grain.type.value = FilmGrainLookup.Medium1;
            }
            
            if (!profile.Has<ChromaticAberration>())
            {
                ChromaticAberration ca = profile.Add<ChromaticAberration>();
                ca.intensity.overrideState = true;
                ca.intensity.value = 0.15f;
            }
            
            volume.profile = profile;
            
            // 2. Add Shadow Casters to all doors
            var doors = Object.FindObjectsByType<TheLastSignal.Entities.Environment.DoorController>(FindObjectsInactive.Exclude);
            foreach(var door in doors)
            {
                if (door.GetComponent<ShadowCaster2D>() == null)
                {
                    door.gameObject.AddComponent<ShadowCaster2D>();
                }
            }

            // 3. Flickering light on broken bed
            GameObject brokenBed = GameObject.Find("Prop_Bedroom_Sleep_Pod_Malfunction");
            if (brokenBed != null)
            {
                var light = brokenBed.GetComponentInChildren<Light2D>();
                if (light != null && light.GetComponent<TheLastSignal.Juice.FlickeringLight>() == null)
                {
                    var flicker = light.gameObject.AddComponent<TheLastSignal.Juice.FlickeringLight>();
                    flicker.minIntensity = 0.2f;
                    flicker.maxIntensity = 1.2f;
                    flicker.flickerChance = 0.15f;
                }
            }

            Debug.Log("<color=cyan>[Horror Polish]</color> Post-Processing, Shadow Casters, and Flickering Lights applied successfully!");
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
        }
    }
}
#endif
