#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Tilemaps;
using TheLastSignal.Entities.Environment;

namespace TheLastSignal.EditorTools
{
    /// <summary>
    /// Master builder and applier for Phase 3 Sub-Level Enhancements:
    /// - Interactive Terminals & Vault Chests
    /// - Guardrail Stanchion Posts & Abyssal Bridge Console
    /// - Collider Optimization & Path Smoothing
    /// - Corridor Emergency Props (Fire Extinguishers, First Aid, Intercom)
    /// - Door LED Status Indicators
    /// - Wall Conduit Boxes & Floor Exclusion Lines
    /// - Geiger Station, Siren Beacon & Cryo Mist VFX
    /// </summary>
    public static class SubLevelPhase3UpgradeApplier
    {
        private const string SpritesRoot = "Assets/_Project/Sprites/Environments/SubLevel";
        private const string TilesSourceDir = SpritesRoot + "/Tiles";
        private const string PropsSourceDir = SpritesRoot + "/Props";
        private const string DecalsSourceDir = SpritesRoot + "/Decals";
        private const string TileAssetsDir = "Assets/_Project/Tiles/SubLevel";

        [MenuItem("Tools/Facility/Apply SubLevel Phase 3 Complete Upgrades", false, 62)]
        public static void ApplyPhase3()
        {
            Debug.Log("<color=cyan>[SubLevelPhase3UpgradeApplier] Starting Phase 3 Comprehensive Upgrade...</color>");

            // 1. Configure and import newly generated Phase 3 sprites
            ConfigureTextures();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            // 2. Create ScriptableObject Tile assets for new tiles
            CreateTileAssets();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            // 3. Paint wall conduit boxes and floor exclusion lines
            PaintTilemapEnhancements();

            // 4. Spawn & Configure Phase 3 Props, Interactive Terminals, and Decals
            ApplyPropsAndInteractivity();

            // 5. Setup Atmospheric Particle VFX
            SetupParticleVFX();

            // 6. Mark scene dirty & save
            var activeScene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveScene(activeScene);

            Debug.Log("<color=green>[SubLevelPhase3UpgradeApplier] Successfully applied and saved Phase 3 Complete Sub-Level Upgrades!</color>");
        }

        public static void ConfigureTextures()
        {
            string[] dirs = { TilesSourceDir, PropsSourceDir, DecalsSourceDir };
            foreach (var dir in dirs)
            {
                if (!Directory.Exists(dir)) continue;
                string[] files = Directory.GetFiles(dir, "*.png");
                foreach (string file in files)
                {
                    TextureImporter importer = AssetImporter.GetAtPath(file) as TextureImporter;
                    if (importer == null) continue;

                    bool changed = false;
                    if (importer.textureType != TextureImporterType.Sprite) { importer.textureType = TextureImporterType.Sprite; changed = true; }
                    if (importer.spriteImportMode != SpriteImportMode.Single) { importer.spriteImportMode = SpriteImportMode.Single; changed = true; }
                    if (importer.spritePixelsPerUnit != 128f) { importer.spritePixelsPerUnit = 128f; changed = true; }
                    if (importer.filterMode != FilterMode.Point) { importer.filterMode = FilterMode.Point; changed = true; }
                    if (importer.textureCompression != TextureImporterCompression.Uncompressed) { importer.textureCompression = TextureImporterCompression.Uncompressed; changed = true; }
                    if (importer.wrapMode != TextureWrapMode.Clamp) { importer.wrapMode = TextureWrapMode.Clamp; changed = true; }

                    TextureImporterSettings texSettings = new TextureImporterSettings();
                    importer.ReadTextureSettings(texSettings);
                    if (texSettings.spriteMeshType != SpriteMeshType.FullRect) { texSettings.spriteMeshType = SpriteMeshType.FullRect; changed = true; }
                    if (texSettings.spriteExtrude != 1) { texSettings.spriteExtrude = 1; changed = true; }

                    if (changed)
                    {
                        importer.SetTextureSettings(texSettings);
                        EditorUtility.SetDirty(importer);
                        importer.SaveAndReimport();
                    }
                }
            }
        }

        public static void CreateTileAssets()
        {
            if (!Directory.Exists(TileAssetsDir)) Directory.CreateDirectory(TileAssetsDir);

            string[] files = Directory.GetFiles(TilesSourceDir, "*.png");
            foreach (string file in files)
            {
                string tileName = Path.GetFileNameWithoutExtension(file);
                string assetPath = $"{TileAssetsDir}/{tileName}.asset";

                Tile tile = AssetDatabase.LoadAssetAtPath<Tile>(assetPath);
                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(file);
                if (sprite == null)
                {
                    Object[] allAssets = AssetDatabase.LoadAllAssetsAtPath(file);
                    foreach (Object a in allAssets)
                    {
                        if (a is Sprite sp) { sprite = sp; break; }
                    }
                }

                if (sprite == null) continue;

                if (tile == null)
                {
                    tile = ScriptableObject.CreateInstance<Tile>();
                    tile.sprite = sprite;
                    tile.name = tileName;
                    AssetDatabase.CreateAsset(tile, assetPath);
                }
                else
                {
                    tile.sprite = sprite;
                    EditorUtility.SetDirty(tile);
                }
            }
        }

        public static void PaintTilemapEnhancements()
        {
            var gridGo = GameObject.Find("Facility_Grid");
            if (gridGo == null) return;

            var wallTm = gridGo.transform.Find("Wall_Tilemap")?.GetComponent<Tilemap>();
            var floorTm = gridGo.transform.Find("Floor_Tilemap")?.GetComponent<Tilemap>();
            if (wallTm == null || floorTm == null) return;

            Undo.RecordObject(wallTm, "Phase 3 Wall Enhancements");
            Undo.RecordObject(floorTm, "Phase 3 Floor Enhancements");

            Tile wConduit = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/SubLevel_Wall_Conduit_Box.asset");
            Tile fExclusion = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/SubLevel_Floor_Exclusion_Line_V.asset");

            // 1. Node C East Wall Conduit Boxes (at X = 4, Y = -32 and -37)
            if (wConduit != null)
            {
                wallTm.SetTile(new Vector3Int(4, -32, 0), wConduit);
                wallTm.SetTile(new Vector3Int(4, -37, 0), wConduit);
            }

            // 2. Reactor Core Exclusion Zone Boundary (columns X = 8 and X = 17 for Y in [-68..-61])
            if (fExclusion != null)
            {
                for (int y = -68; y <= -61; y++)
                {
                    floorTm.SetTile(new Vector3Int(8, y, 0), fExclusion);
                    floorTm.SetTile(new Vector3Int(17, y, 0), fExclusion);
                }
            }

            EditorUtility.SetDirty(wallTm);
            EditorUtility.SetDirty(floorTm);
        }

        public static void ApplyPropsAndInteractivity()
        {
            var envRoot = GameObject.Find("SubLevel_Environment");
            if (envRoot == null)
            {
                envRoot = new GameObject("SubLevel_Environment");
                Undo.RegisterCreatedObjectUndo(envRoot, "Create SubLevel_Environment");
            }

            var propsRoot = envRoot.transform.Find("Props")?.gameObject;
            if (propsRoot == null)
            {
                propsRoot = new GameObject("Props");
                propsRoot.transform.SetParent(envRoot.transform);
            }

            var decalsRoot = envRoot.transform.Find("Decals")?.gameObject;
            if (decalsRoot == null)
            {
                decalsRoot = new GameObject("Decals");
                decalsRoot.transform.SetParent(envRoot.transform);
            }

            // Helper Spawn Method
            GameObject SpawnPropObj(string name, Vector3 pos, string spritePath, bool addCol = true, Vector2 colSize = default, string layer = "Obstacles", int order = 5)
            {
                var existing = GameObject.Find(name);
                if (existing != null) Undo.DestroyObjectImmediate(existing);

                GameObject go = new GameObject(name);
                Undo.RegisterCreatedObjectUndo(go, $"Spawn {name}");
                go.transform.SetParent(propsRoot.transform);
                go.transform.position = pos;

                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
                sr.sortingLayerName = layer;
                sr.sortingOrder = order;

                if (addCol)
                {
                    var col = go.AddComponent<BoxCollider2D>();
                    if (colSize != default) col.size = colSize;
                }
                return go;
            }

            GameObject SpawnShadowObj(string name, Vector3 pos, string spritePath, float alpha = 0.50f)
            {
                var existing = GameObject.Find(name);
                if (existing != null) Undo.DestroyObjectImmediate(existing);

                GameObject go = new GameObject(name);
                Undo.RegisterCreatedObjectUndo(go, $"Spawn {name}");
                go.transform.SetParent(propsRoot.transform);
                go.transform.position = pos;

                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
                sr.sortingLayerName = "Floor";
                sr.sortingOrder = 3;
                sr.color = new Color(0f, 0f, 0f, alpha);
                return go;
            }

            GameObject SpawnDecalObj(string name, Vector3 pos, string spritePath)
            {
                var existing = GameObject.Find(name);
                if (existing != null) Undo.DestroyObjectImmediate(existing);

                GameObject go = new GameObject(name);
                Undo.RegisterCreatedObjectUndo(go, $"Spawn {name}");
                go.transform.SetParent(decalsRoot.transform);
                go.transform.position = pos;

                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
                sr.sortingLayerName = "Floor";
                sr.sortingOrder = 2;
                return go;
            }

            // ========================================================
            // 1. NODE C PROPS & INTERACTIVITY
            // ========================================================
            // Bridge Nexus Control Terminal
            var bridgeConsole = SpawnPropObj("Prop_NodeC_Bridge_Console", new Vector3(-2.8f, -32.5f, 0f), $"{PropsSourceDir}/Prop_NodeC_Bridge_Console.png", true, new Vector2(0.8f, 0.8f));
            SpawnShadowObj("Shadow_NodeC_Bridge_Console", new Vector3(-2.8f, -32.85f, 0f), $"{PropsSourceDir}/Shadow_SubLevel_Medium.png", 0.50f);
            if (bridgeConsole != null)
            {
                int interactableLayer = LayerMask.NameToLayer("Interactable");
                if (interactableLayer != -1) bridgeConsole.layer = interactableLayer;
                var task = bridgeConsole.AddComponent<TaskLocation>();
                var so = new SerializedObject(task);
                so.FindProperty("stationId").stringValue = "TASK_NODEC_BRIDGE";
                so.FindProperty("stationDisplayName").stringValue = "Catwalk Nexus Bridge Console";
                so.FindProperty("stationType").enumValueIndex = (int)TaskStationType.ControlPanel;
                so.ApplyModifiedProperties();
            }

            // Guardrail Corner Stanchion Posts
            SpawnPropObj("Prop_NodeC_Stanchion_NW", new Vector3(-4.3f, -30.7f, 0f), $"{PropsSourceDir}/Prop_NodeC_Stanchion_Post.png", true, new Vector2(0.4f, 0.4f), "Obstacles", 7);
            SpawnPropObj("Prop_NodeC_Stanchion_NE", new Vector3(3.3f, -30.7f, 0f), $"{PropsSourceDir}/Prop_NodeC_Stanchion_Post.png", true, new Vector2(0.4f, 0.4f), "Obstacles", 7);
            SpawnPropObj("Prop_NodeC_Stanchion_SW", new Vector3(-4.3f, -38.3f, 0f), $"{PropsSourceDir}/Prop_NodeC_Stanchion_Post.png", true, new Vector2(0.4f, 0.4f), "Obstacles", 7);
            SpawnPropObj("Prop_NodeC_Stanchion_SE", new Vector3(3.3f, -38.3f, 0f), $"{PropsSourceDir}/Prop_NodeC_Stanchion_Post.png", true, new Vector2(0.4f, 0.4f), "Obstacles", 7);

            // ========================================================
            // 2. DEEP STORAGE PROPS & INTERACTIVITY
            // ========================================================
            // Update Vault Chest Sprites & Make Interactable
            var c1 = GameObject.Find("Prop_DeepStorage_Vault_Chest_1");
            if (c1 != null)
            {
                var sr = c1.GetComponent<SpriteRenderer>();
                if (sr != null) sr.sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{PropsSourceDir}/Prop_DeepStorage_Lead_Vault_Chest.png");
                int interactableLayer = LayerMask.NameToLayer("Interactable");
                if (interactableLayer != -1) c1.layer = interactableLayer;
                var task = c1.GetComponent<TaskLocation>() ?? c1.AddComponent<TaskLocation>();
                var so = new SerializedObject(task);
                so.FindProperty("stationId").stringValue = "TASK_DEEPSTORAGE_ISOTOPE_1";
                so.FindProperty("stationDisplayName").stringValue = "Isotope Vault Chest Alpha";
                so.FindProperty("stationType").enumValueIndex = (int)TaskStationType.Mechanical;
                so.ApplyModifiedProperties();
            }

            var c2 = GameObject.Find("Prop_DeepStorage_Vault_Chest_2");
            if (c2 != null)
            {
                var sr = c2.GetComponent<SpriteRenderer>();
                if (sr != null) sr.sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{PropsSourceDir}/Prop_DeepStorage_Lead_Vault_Chest.png");
                int interactableLayer = LayerMask.NameToLayer("Interactable");
                if (interactableLayer != -1) c2.layer = interactableLayer;
                var task = c2.GetComponent<TaskLocation>() ?? c2.AddComponent<TaskLocation>();
                var so = new SerializedObject(task);
                so.FindProperty("stationId").stringValue = "TASK_DEEPSTORAGE_ISOTOPE_2";
                so.FindProperty("stationDisplayName").stringValue = "Isotope Vault Chest Beta";
                so.FindProperty("stationType").enumValueIndex = (int)TaskStationType.Mechanical;
                so.ApplyModifiedProperties();
            }

            // Optimize Crane Trolley Collider for Smooth Passage
            var crane = GameObject.Find("Prop_DeepStorage_Crane_Trolley");
            if (crane != null)
            {
                var col = crane.GetComponent<BoxCollider2D>();
                if (col != null) col.size = new Vector2(1.2f, 0.4f);
            }

            // Crane Track Wear Decals
            SpawnDecalObj("Decal_DeepStorage_Track_Wear_1", new Vector3(-14.0f, -47.5f, 0f), $"{DecalsSourceDir}/Decal_SubLevel_Track_Scratches.png");
            SpawnDecalObj("Decal_DeepStorage_Track_Wear_2", new Vector3(-13.0f, -47.5f, 0f), $"{DecalsSourceDir}/Decal_SubLevel_Track_Scratches.png");

            // South Wall Equipment
            SpawnPropObj("Prop_DeepStorage_Fire_Extinguisher", new Vector3(-15.5f, -52.6f, 0f), $"{PropsSourceDir}/Prop_SubLevel_Fire_Extinguisher.png", true, new Vector2(0.4f, 0.8f));
            SpawnPropObj("Prop_DeepStorage_FirstAid", new Vector3(-11.5f, -52.6f, 0f), $"{PropsSourceDir}/Prop_SubLevel_FirstAid_Locker.png", true, new Vector2(0.4f, 0.8f));

            // ========================================================
            // 3. POWER RELAY PROPS & INTERACTIVITY
            // ========================================================
            // Make Breaker Cabinet an Interactable Grid Control Point
            var breaker = GameObject.Find("Prop_PowerRelay_Breaker_Cabinet");
            if (breaker != null)
            {
                int interactableLayer = LayerMask.NameToLayer("Interactable");
                if (interactableLayer != -1) breaker.layer = interactableLayer;
                var task = breaker.GetComponent<TaskLocation>() ?? breaker.AddComponent<TaskLocation>();
                var so = new SerializedObject(task);
                so.FindProperty("stationId").stringValue = "TASK_POWERRELAY_BREAKER";
                so.FindProperty("stationDisplayName").stringValue = "Substation Main Grid Breaker";
                so.FindProperty("stationType").enumValueIndex = (int)TaskStationType.Generator;
                so.ApplyModifiedProperties();
            }

            // Optimize Central Machinery Colliders (Widening walking pathways)
            var switchboard = GameObject.Find("Prop_PowerRelay_Switchboard_Island");
            if (switchboard != null)
            {
                var col = switchboard.GetComponent<BoxCollider2D>();
                if (col != null) col.size = new Vector2(1.4f, 0.6f);
            }
            var transformer = GameObject.Find("Prop_PowerRelay_Stepdown_Transformer");
            if (transformer != null)
            {
                var col = transformer.GetComponent<BoxCollider2D>();
                if (col != null) col.size = new Vector2(1.4f, 0.6f);
            }

            // Transformer Oil Decals
            SpawnDecalObj("Decal_PowerRelay_Oil_1", new Vector3(9.5f, -45.0f, 0f), $"{DecalsSourceDir}/Decal_SubLevel_Oil_Scorch.png");
            SpawnDecalObj("Decal_PowerRelay_Oil_2", new Vector3(10.0f, -49.0f, 0f), $"{DecalsSourceDir}/Decal_SubLevel_Oil_Scorch.png");

            // Warning Strobe Lamp at West Entrance
            SpawnPropObj("Prop_PowerRelay_Warning_Strobe", new Vector3(7.2f, -45.5f, 0f), $"{PropsSourceDir}/Prop_ReactorCore_Siren_Beacon.png", false, default, "Obstacles", 6);

            // ========================================================
            // 4. REACTOR CORE PROPS & INTERACTIVITY
            // ========================================================
            // Update Master Console Sprite to prominent CRT Monitors
            var rConsole = GameObject.Find("Prop_ReactorCore_Master_Console");
            if (rConsole != null)
            {
                var sr = rConsole.GetComponent<SpriteRenderer>();
                if (sr != null) sr.sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{PropsSourceDir}/Prop_ReactorCore_Control_Console.png");
            }

            // Update Radiation Shield Sprite to heavy casters version
            var rShield = GameObject.Find("Prop_ReactorCore_Radiation_Shield");
            if (rShield != null)
            {
                var sr = rShield.GetComponent<SpriteRenderer>();
                if (sr != null) sr.sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{PropsSourceDir}/Prop_ReactorCore_Radiation_Shield.png");
            }

            // Wall Geiger Radiation Station & Siren Beacon
            SpawnPropObj("Prop_ReactorCore_Geiger_Station", new Vector3(11.0f, -58.5f, 0f), $"{PropsSourceDir}/Prop_ReactorCore_Geiger_Station.png", true, new Vector2(0.8f, 0.8f));
            SpawnPropObj("Prop_ReactorCore_Siren_Beacon", new Vector3(14.0f, -58.5f, 0f), $"{PropsSourceDir}/Prop_ReactorCore_Siren_Beacon.png", true, new Vector2(0.5f, 0.5f));

            // ========================================================
            // 5. T-JUNCTION & LATERAL CORRIDOR PROPS
            // ========================================================
            // Emergency Corridor Equipment
            SpawnPropObj("Prop_TJunction_FirstAid", new Vector3(-4.0f, -44.8f, 0f), $"{PropsSourceDir}/Prop_SubLevel_FirstAid_Locker.png", true, new Vector2(0.4f, 0.6f));
            SpawnPropObj("Prop_TJunction_Fire_Extinguisher", new Vector3(4.0f, -44.8f, 0f), $"{PropsSourceDir}/Prop_SubLevel_Fire_Extinguisher.png", true, new Vector2(0.4f, 0.8f));
            SpawnPropObj("Prop_TJunction_Intercom", new Vector3(-0.5f, -44.8f, 0f), $"{PropsSourceDir}/Prop_SubLevel_Intercom.png", true, new Vector2(0.4f, 0.4f));

            // Door Status Indicators
            SpawnPropObj("Prop_Door_DeepStorage_LED", new Vector3(-7.5f, -45.4f, 0f), $"{PropsSourceDir}/Prop_SubLevel_Door_Status_LED.png", false, default, "Obstacles", 7);
            SpawnPropObj("Prop_Door_PowerRelay_LED", new Vector3(7.5f, -45.4f, 0f), $"{PropsSourceDir}/Prop_SubLevel_Door_Status_LED.png", false, default, "Obstacles", 7);

            // Configure Lighting Enhancements
            SetupLightingEnhancements(envRoot);
        }

        public static void SetupLightingEnhancements(GameObject envRoot)
        {
            var lightsRoot = envRoot.transform.Find("Lights")?.gameObject;
            if (lightsRoot == null) return;

            Light2D AddPointLight(string name, Vector3 pos, Color color, float intensity, float radius)
            {
                var existing = GameObject.Find(name);
                if (existing != null) Undo.DestroyObjectImmediate(existing);

                GameObject go = new GameObject(name);
                Undo.RegisterCreatedObjectUndo(go, $"Add {name}");
                go.transform.SetParent(lightsRoot.transform);
                go.transform.position = pos;

                var l = go.AddComponent<Light2D>();
                l.lightType = Light2D.LightType.Point;
                l.color = color;
                l.intensity = intensity;
                l.pointLightOuterRadius = radius;
                l.pointLightInnerRadius = radius * 0.25f;
                return l;
            }

            // Node C Bridge Console Amber Point Light
            AddPointLight("Light_NodeC_Bridge_Console", new Vector3(-2.8f, -32.3f, 0f), new Color(1.0f, 0.70f, 0.20f, 1f), 0.75f, 2.8f);

            // Power Relay Warning Strobe Red Lamp
            AddPointLight("Light_PowerRelay_Strobe", new Vector3(7.2f, -45.5f, 0f), new Color(1.0f, 0.25f, 0.15f, 1f), 0.85f, 3.2f);

            // Reactor Core Meltdown Warning Beacon Red Lamp
            AddPointLight("Light_ReactorCore_Alarm_Beacon", new Vector3(14.0f, -58.5f, 0f), new Color(1.0f, 0.20f, 0.15f, 1f), 0.90f, 3.5f);

            // Geiger Station Dial Ambient Glow
            AddPointLight("Light_ReactorCore_Geiger", new Vector3(11.0f, -58.5f, 0f), new Color(0.20f, 0.90f, 1.0f, 1f), 0.50f, 2.0f);
        }

        public static void SetupParticleVFX()
        {
            var envRoot = GameObject.Find("SubLevel_Environment");
            if (envRoot == null) return;

            var vfxRoot = envRoot.transform.Find("VFX")?.gameObject;
            if (vfxRoot == null)
            {
                vfxRoot = new GameObject("VFX");
                vfxRoot.transform.SetParent(envRoot.transform);
            }

            Material steamMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Materials/M_Steam_Particle.mat");
            if (steamMat == null) return;

            // 1. Cryo Frost Mist on Coolant Towers (Left & Right)
            void CreateCryoMist(string name, Vector3 pos)
            {
                var existing = GameObject.Find(name);
                if (existing != null) Undo.DestroyObjectImmediate(existing);

                var go = new GameObject(name);
                Undo.RegisterCreatedObjectUndo(go, $"Spawn {name}");
                go.transform.SetParent(vfxRoot.transform);
                go.transform.position = pos;

                var ps = go.AddComponent<ParticleSystem>();
                var rend = go.GetComponent<ParticleSystemRenderer>();
                rend.sharedMaterial = steamMat;
                rend.sortingLayerName = "Obstacles";
                rend.sortingOrder = 6;

                var main = ps.main;
                main.maxParticles = 18;
                main.startLifetime = 2.2f;
                main.startSpeed = 0.5f;
                main.startSize = 0.40f;
                main.startColor = new Color(0.40f, 0.90f, 1.0f, 0.30f); // Cold Cyan Nitrogen Mist

                var emission = ps.emission;
                emission.rateOverTime = 4.0f;

                var shape = ps.shape;
                shape.shapeType = ParticleSystemShapeType.Circle;
                shape.radius = 0.25f;
            }

            CreateCryoMist("VFX_ReactorCore_Cryo_Mist_L", new Vector3(8.5f, -64.0f, 0f));
            CreateCryoMist("VFX_ReactorCore_Cryo_Mist_R", new Vector3(16.5f, -64.0f, 0f));

            // 2. Abyssal Steam Vapour in Node C
            var existingAbyss = GameObject.Find("VFX_NodeC_Abyssal_Steam");
            if (existingAbyss != null) Undo.DestroyObjectImmediate(existingAbyss);

            var abyssGo = new GameObject("VFX_NodeC_Abyssal_Steam");
            Undo.RegisterCreatedObjectUndo(abyssGo, "Spawn VFX_NodeC_Abyssal_Steam");
            abyssGo.transform.SetParent(vfxRoot.transform);
            abyssGo.transform.position = new Vector3(-0.5f, -34.5f, 0f);

            var psAbyss = abyssGo.AddComponent<ParticleSystem>();
            var rendAbyss = abyssGo.GetComponent<ParticleSystemRenderer>();
            rendAbyss.sharedMaterial = steamMat;
            rendAbyss.sortingLayerName = "Floor";
            rendAbyss.sortingOrder = 4;

            var mainA = psAbyss.main;
            mainA.maxParticles = 30;
            mainA.startLifetime = 3.5f;
            mainA.startSpeed = 0.3f;
            mainA.startSize = 0.8f;
            mainA.startColor = new Color(0.70f, 0.80f, 0.95f, 0.15f); // Subtle soft abyss fog

            var emA = psAbyss.emission;
            emA.rateOverTime = 6.0f;

            var shapeA = psAbyss.shape;
            shapeA.shapeType = ParticleSystemShapeType.Box;
            shapeA.scale = new Vector3(6.0f, 6.0f, 0f);
        }
    }
}
#endif
