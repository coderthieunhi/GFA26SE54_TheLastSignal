#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace TheLastSignal.EditorTools
{
    [InitializeOnLoad]
    public static class SpriteImportValidator
    {
        static SpriteImportValidator()
        {
            EditorApplication.delayCall += () =>
            {
                ValidateAndSliceSprites();
            };
        }

        [MenuItem("Tools/Characters/Setup Engineer 8Dir Sprites", false, 10)]
        public static void ValidateAndSliceSprites()
        {
            // Idle 8-dir
            SliceIdleFile("Assets/_Project/Sprites/Characters/Engineer_Idle_8Dir.png");
            SliceIdleFile("Assets/_Project/Resources/Engineer_Idle_8Dir.png");

            // All 8 Run sheets
            SliceRunSheet("Assets/_Project/Sprites/Characters/Engineer_Run_East.png", "Engineer_Run_East");
            SliceRunSheet("Assets/_Project/Resources/Engineer_Run_East.png", "Engineer_Run_East");

            SliceRunSheet("Assets/_Project/Sprites/Characters/Engineer_Run_NorthEast.png", "Engineer_Run_NorthEast");
            SliceRunSheet("Assets/_Project/Resources/Engineer_Run_NorthEast.png", "Engineer_Run_NorthEast");

            SliceRunSheet("Assets/_Project/Sprites/Characters/Engineer_Run_North.png", "Engineer_Run_North");
            SliceRunSheet("Assets/_Project/Resources/Engineer_Run_North.png", "Engineer_Run_North");

            SliceRunSheet("Assets/_Project/Sprites/Characters/Engineer_Run_NorthWest.png", "Engineer_Run_NorthWest");
            SliceRunSheet("Assets/_Project/Resources/Engineer_Run_NorthWest.png", "Engineer_Run_NorthWest");

            SliceRunSheet("Assets/_Project/Sprites/Characters/Engineer_Run_West.png", "Engineer_Run_West");
            SliceRunSheet("Assets/_Project/Resources/Engineer_Run_West.png", "Engineer_Run_West");

            SliceRunSheet("Assets/_Project/Sprites/Characters/Engineer_Run_SouthWest.png", "Engineer_Run_SouthWest");
            SliceRunSheet("Assets/_Project/Resources/Engineer_Run_SouthWest.png", "Engineer_Run_SouthWest");

            SliceRunSheet("Assets/_Project/Sprites/Characters/Engineer_Run_South.png", "Engineer_Run_South");
            SliceRunSheet("Assets/_Project/Resources/Engineer_Run_South.png", "Engineer_Run_South");

            SliceRunSheet("Assets/_Project/Sprites/Characters/Engineer_Run_SouthEast.png", "Engineer_Run_SouthEast");
            SliceRunSheet("Assets/_Project/Resources/Engineer_Run_SouthEast.png", "Engineer_Run_SouthEast");

            ApplyToPlayerPrefabAndScene();
        }

        private static void SliceIdleFile(string path)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = 64;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;

            SpriteMetaData[] sheet = new SpriteMetaData[8];
            sheet[0] = new SpriteMetaData { name = "Engineer_Idle_South", rect = new Rect(0, 92, 92, 92), alignment = (int)SpriteAlignment.Center, pivot = new Vector2(0.5f, 0.5f) };
            sheet[1] = new SpriteMetaData { name = "Engineer_Idle_SouthEast", rect = new Rect(92, 92, 92, 92), alignment = (int)SpriteAlignment.Center, pivot = new Vector2(0.5f, 0.5f) };
            sheet[2] = new SpriteMetaData { name = "Engineer_Idle_East", rect = new Rect(184, 92, 92, 92), alignment = (int)SpriteAlignment.Center, pivot = new Vector2(0.5f, 0.5f) };
            sheet[3] = new SpriteMetaData { name = "Engineer_Idle_NorthEast", rect = new Rect(276, 92, 92, 92), alignment = (int)SpriteAlignment.Center, pivot = new Vector2(0.5f, 0.5f) };
            sheet[4] = new SpriteMetaData { name = "Engineer_Idle_North", rect = new Rect(368, 92, 92, 92), alignment = (int)SpriteAlignment.Center, pivot = new Vector2(0.5f, 0.5f) };
            sheet[5] = new SpriteMetaData { name = "Engineer_Idle_NorthWest", rect = new Rect(0, 0, 92, 92), alignment = (int)SpriteAlignment.Center, pivot = new Vector2(0.5f, 0.5f) };
            sheet[6] = new SpriteMetaData { name = "Engineer_Idle_West", rect = new Rect(92, 0, 92, 92), alignment = (int)SpriteAlignment.Center, pivot = new Vector2(0.5f, 0.5f) };
            sheet[7] = new SpriteMetaData { name = "Engineer_Idle_SouthWest", rect = new Rect(184, 0, 92, 92), alignment = (int)SpriteAlignment.Center, pivot = new Vector2(0.5f, 0.5f) };

            importer.spritesheet = sheet;
            EditorUtility.SetDirty(importer);
            importer.SaveAndReimport();
        }

        private static void SliceRunSheet(string path, string prefix)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = 64;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;

            SpriteMetaData[] sheet = new SpriteMetaData[8];
            sheet[0] = new SpriteMetaData { name = $"{prefix}_0", rect = new Rect(0, 92, 92, 92), alignment = (int)SpriteAlignment.Center, pivot = new Vector2(0.5f, 0.5f) };
            sheet[1] = new SpriteMetaData { name = $"{prefix}_1", rect = new Rect(92, 92, 92, 92), alignment = (int)SpriteAlignment.Center, pivot = new Vector2(0.5f, 0.5f) };
            sheet[2] = new SpriteMetaData { name = $"{prefix}_2", rect = new Rect(184, 92, 92, 92), alignment = (int)SpriteAlignment.Center, pivot = new Vector2(0.5f, 0.5f) };
            sheet[3] = new SpriteMetaData { name = $"{prefix}_3", rect = new Rect(276, 92, 92, 92), alignment = (int)SpriteAlignment.Center, pivot = new Vector2(0.5f, 0.5f) };
            sheet[4] = new SpriteMetaData { name = $"{prefix}_4", rect = new Rect(368, 92, 92, 92), alignment = (int)SpriteAlignment.Center, pivot = new Vector2(0.5f, 0.5f) };

            sheet[5] = new SpriteMetaData { name = $"{prefix}_5", rect = new Rect(0, 0, 92, 92), alignment = (int)SpriteAlignment.Center, pivot = new Vector2(0.5f, 0.5f) };
            sheet[6] = new SpriteMetaData { name = $"{prefix}_6", rect = new Rect(92, 0, 92, 92), alignment = (int)SpriteAlignment.Center, pivot = new Vector2(0.5f, 0.5f) };
            sheet[7] = new SpriteMetaData { name = $"{prefix}_7", rect = new Rect(184, 0, 92, 92), alignment = (int)SpriteAlignment.Center, pivot = new Vector2(0.5f, 0.5f) };

            importer.spritesheet = sheet;
            EditorUtility.SetDirty(importer);
            importer.SaveAndReimport();
        }

        private static void ApplyToPlayerPrefabAndScene()
        {
            string spritePath = "Assets/_Project/Sprites/Characters/Engineer_Idle_8Dir.png";
            Object[] subAssets = AssetDatabase.LoadAllAssetsAtPath(spritePath);
            Sprite southSprite = null;
            Sprite sSouthEast = null, sEast = null, sNorthEast = null, sNorth = null, sNorthWest = null, sWest = null, sSouthWest = null;

            foreach (var asset in subAssets)
            {
                if (asset is Sprite s)
                {
                    switch (s.name)
                    {
                        case "Engineer_Idle_South": southSprite = s; break;
                        case "Engineer_Idle_SouthEast": sSouthEast = s; break;
                        case "Engineer_Idle_East": sEast = s; break;
                        case "Engineer_Idle_NorthEast": sNorthEast = s; break;
                        case "Engineer_Idle_North": sNorth = s; break;
                        case "Engineer_Idle_NorthWest": sNorthWest = s; break;
                        case "Engineer_Idle_West": sWest = s; break;
                        case "Engineer_Idle_SouthWest": sSouthWest = s; break;
                    }
                }
            }

            Shader unlitShader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default")
                              ?? Shader.Find("Sprites/Default");
            Material unlitMaterial = unlitShader != null ? new Material(unlitShader) : null;

            // Update Player Prefab
            string prefabPath = "Assets/_Project/Prefabs/Player.prefab";
            try
            {
                GameObject prefab = PrefabUtility.LoadPrefabContents(prefabPath);
                if (prefab != null)
                {
                    SpriteRenderer sr = prefab.GetComponent<SpriteRenderer>();
                    if (sr != null)
                    {
                        if (southSprite != null) sr.sprite = southSprite;
                        sr.color = Color.white;
                        if (unlitMaterial != null) sr.sharedMaterial = unlitMaterial;
                    }

                    var anim = prefab.GetComponent<TheLastSignal.Entities.Player.PlayerAnimator>();
                    if (anim != null)
                    {
                        var so = new SerializedObject(anim);
                        if (southSprite != null) so.FindProperty("idleSouth").objectReferenceValue = southSprite;
                        if (sSouthEast != null) so.FindProperty("idleSouthEast").objectReferenceValue = sSouthEast;
                        if (sEast != null) so.FindProperty("idleEast").objectReferenceValue = sEast;
                        if (sNorthEast != null) so.FindProperty("idleNorthEast").objectReferenceValue = sNorthEast;
                        if (sNorth != null) so.FindProperty("idleNorth").objectReferenceValue = sNorth;
                        if (sNorthWest != null) so.FindProperty("idleNorthWest").objectReferenceValue = sNorthWest;
                        if (sWest != null) so.FindProperty("idleWest").objectReferenceValue = sWest;
                        if (sSouthWest != null) so.FindProperty("idleSouthWest").objectReferenceValue = sSouthWest;

                        AssignRunProperty(so, "runEast", "Engineer_Run_East");
                        AssignRunProperty(so, "runNorthEast", "Engineer_Run_NorthEast");
                        AssignRunProperty(so, "runNorth", "Engineer_Run_North");
                        AssignRunProperty(so, "runNorthWest", "Engineer_Run_NorthWest");
                        AssignRunProperty(so, "runWest", "Engineer_Run_West");
                        AssignRunProperty(so, "runSouthWest", "Engineer_Run_SouthWest");
                        AssignRunProperty(so, "runSouth", "Engineer_Run_South");
                        AssignRunProperty(so, "runSouthEast", "Engineer_Run_SouthEast");

                        so.ApplyModifiedProperties();
                    }

                    PrefabUtility.SaveAsPrefabAsset(prefab, prefabPath);
                    PrefabUtility.UnloadPrefabContents(prefab);
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[SpriteImportValidator] Prefab update notice: {ex.Message}");
            }

            // Update open scene Player
            GameObject scenePlayer = GameObject.Find("Player");
            if (scenePlayer != null)
            {
                SpriteRenderer sr = scenePlayer.GetComponent<SpriteRenderer>();
                if (sr != null)
                {
                    if (southSprite != null) sr.sprite = southSprite;
                    sr.color = Color.white;
                    if (unlitMaterial != null) sr.sharedMaterial = unlitMaterial;
                    EditorUtility.SetDirty(sr);
                }

                var anim = scenePlayer.GetComponent<TheLastSignal.Entities.Player.PlayerAnimator>();
                if (anim != null)
                {
                    var so = new SerializedObject(anim);
                    if (southSprite != null) so.FindProperty("idleSouth").objectReferenceValue = southSprite;
                    if (sSouthEast != null) so.FindProperty("idleSouthEast").objectReferenceValue = sSouthEast;
                    if (sEast != null) so.FindProperty("idleEast").objectReferenceValue = sEast;
                    if (sNorthEast != null) so.FindProperty("idleNorthEast").objectReferenceValue = sNorthEast;
                    if (sNorth != null) so.FindProperty("idleNorth").objectReferenceValue = sNorth;
                    if (sNorthWest != null) so.FindProperty("idleNorthWest").objectReferenceValue = sNorthWest;
                    if (sWest != null) so.FindProperty("idleWest").objectReferenceValue = sWest;
                    if (sSouthWest != null) so.FindProperty("idleSouthWest").objectReferenceValue = sSouthWest;

                    AssignRunProperty(so, "runEast", "Engineer_Run_East");
                    AssignRunProperty(so, "runNorthEast", "Engineer_Run_NorthEast");
                    AssignRunProperty(so, "runNorth", "Engineer_Run_North");
                    AssignRunProperty(so, "runNorthWest", "Engineer_Run_NorthWest");
                    AssignRunProperty(so, "runWest", "Engineer_Run_West");
                    AssignRunProperty(so, "runSouthWest", "Engineer_Run_SouthWest");
                    AssignRunProperty(so, "runSouth", "Engineer_Run_South");
                    AssignRunProperty(so, "runSouthEast", "Engineer_Run_SouthEast");

                    so.ApplyModifiedProperties();
                    EditorUtility.SetDirty(anim);
                }
            }
        }

        private static void AssignRunProperty(SerializedObject so, string propertyName, string sheetName)
        {
            var prop = so.FindProperty(propertyName);
            if (prop == null) return;

            string spritePath = $"Assets/_Project/Sprites/Characters/{sheetName}.png";
            Object[] subAssets = AssetDatabase.LoadAllAssetsAtPath(spritePath);
            if (subAssets == null || subAssets.Length == 0) return;

            Sprite[] frames = new Sprite[8];
            foreach (var asset in subAssets)
            {
                if (asset is Sprite s)
                {
                    for (int i = 0; i < 8; i++)
                    {
                        if (s.name.EndsWith($"_{i}"))
                        {
                            frames[i] = s;
                            break;
                        }
                    }
                }
            }

            prop.arraySize = 8;
            for (int i = 0; i < 8; i++)
            {
                prop.GetArrayElementAtIndex(i).objectReferenceValue = frames[i];
            }
        }
    }
}
#endif
