using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using TheLastSignal.Systems.Lighting;

namespace TheLastSignal.EditorTools
{
    /// <summary>
    /// Epic 5: warns when a Light2D has no matching GameplayLightSource (a client-visible light the
    /// headless server has no data model for — it would render on clients but never actually light
    /// anything for IsLit/Sanity/Shade exposure checks) or the reverse (a GameplayLightSource with no
    /// Light2D anywhere nearby — plausible for a Zone with level-art lighting instead, so this direction
    /// is a hint, not necessarily a mistake). Runs on scene save/open and via a menu item; does not
    /// modify anything.
    /// </summary>
    public static class GameplayLightValidator
    {
        [MenuItem("TheLastSignal/Validate Gameplay Lights")]
        public static void ValidateMenuItem()
        {
            int warnings = ValidateOpenScenes();
            Debug.Log($"[GameplayLightValidator] Validation complete — {warnings} warning(s).");
        }

        [InitializeOnLoadMethod]
        private static void RegisterSceneSavingHook()
        {
            EditorSceneManager.sceneSaving += (scene, path) => ValidateScene(scene);
        }

        private static int ValidateOpenScenes()
        {
            int warnings = 0;
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                warnings += ValidateScene(SceneManager.GetSceneAt(i));
            }

            return warnings;
        }

        private static int ValidateScene(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                return 0;
            }

            int warnings = 0;

            Light2D[] lights = Object.FindObjectsByType<Light2D>(FindObjectsInactive.Exclude);
            for (int i = 0; i < lights.Length; i++)
            {
                Light2D light = lights[i];
                if (light.gameObject.scene != scene)
                {
                    continue;
                }

                // A light that is purely decorative and never meant to represent a gameplay source
                // should sit outside this check; there is no "decorative" tag in the project today, so
                // every Light2D is expected to have a matching GameplayLightSource somewhere on itself
                // or a parent — the same object a flashlight/zone's cone/bounds live on.
                if (light.GetComponentInParent<GameplayLightSource>() == null)
                {
                    Debug.LogWarning(
                        $"[GameplayLightValidator] Light2D '{GetPath(light.transform)}' has no matching " +
                        "GameplayLightSource on itself or a parent — the headless server has no data model " +
                        "for it, so it will never affect IsLit/Sanity/Shade exposure.", light);
                    warnings++;
                }
            }

            GameplayLightSource[] sources = Object.FindObjectsByType<GameplayLightSource>(FindObjectsInactive.Exclude);
            for (int i = 0; i < sources.Length; i++)
            {
                GameplayLightSource source = sources[i];
                if (source.gameObject.scene != scene)
                {
                    continue;
                }

                if (source.GetComponentInChildren<Light2D>() == null)
                {
                    Debug.LogWarning(
                        $"[GameplayLightValidator] GameplayLightSource '{GetPath(source.transform)}' has no " +
                        "Light2D anywhere on itself or its children — clients will never see this source " +
                        "(this may be intentional, e.g. a Zone lit by level art instead).", source);
                    warnings++;
                }
            }

            return warnings;
        }

        private static string GetPath(Transform t)
        {
            string path = t.name;
            while (t.parent != null)
            {
                t = t.parent;
                path = $"{t.name}/{path}";
            }

            return path;
        }
    }
}
