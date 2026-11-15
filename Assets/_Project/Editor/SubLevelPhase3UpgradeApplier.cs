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

            int interactableLayer = LayerMask.NameToLayer("Interactable");

            // ========================================================
            // 1. NODE C PROPS & INTERACTIVITY
            // ========================================================
            // Bridge Nexus Control Terminal (At far tip of 2-tile dead-end)
            var bridgeConsole = SpawnPropObj("Prop_NodeC_Bridge_Console", new Vector3(-2.45f, -34.5f, 0f), $"{PropsSourceDir}/Prop_NodeC_Bridge_Console.png", true, new Vector2(0.8f, 0.8f));
            SpawnShadowObj("Shadow_NodeC_Bridge_Console", new Vector3(-2.45f, -34.85f, 0f), $"{PropsSourceDir}/Shadow_SubLevel_Medium.png", 0.50f);
            if (bridgeConsole != null)
            {
                if (interactableLayer != -1) bridgeConsole.layer = interactableLayer;
                var task = bridgeConsole.AddComponent<TaskLocation>();
                var so = new SerializedObject(task);
                so.FindProperty("stationId").stringValue = "TASK_NODEC_BRIDGE";
                so.FindProperty("stationDisplayName").stringValue = "Catwalk Nexus Bridge Console";
                so.FindProperty("stationType").enumValueIndex = (int)TaskStationType.ControlPanel;
                so.ApplyModifiedProperties();
            }

            // Guardrail Corner Stanchion Posts at Key Catwalk Junctions
            SpawnPropObj("Prop_NodeC_Stanchion_NW", new Vector3(-1.0f, -33.0f, 0f), $"{PropsSourceDir}/Prop_NodeC_Stanchion_Post.png", true, new Vector2(0.4f, 0.4f), "Obstacles", 7);
            SpawnPropObj("Prop_NodeC_Stanchion_NE", new Vector3(1.0f, -33.0f, 0f), $"{PropsSourceDir}/Prop_NodeC_Stanchion_Post.png", true, new Vector2(0.4f, 0.4f), "Obstacles", 7);
            SpawnPropObj("Prop_NodeC_Stanchion_SW", new Vector3(-1.0f, -35.0f, 0f), $"{PropsSourceDir}/Prop_NodeC_Stanchion_Post.png", true, new Vector2(0.4f, 0.4f), "Obstacles", 7);
            SpawnPropObj("Prop_NodeC_Stanchion_SE", new Vector3(1.0f, -35.0f, 0f), $"{PropsSourceDir}/Prop_NodeC_Stanchion_Post.png", true, new Vector2(0.4f, 0.4f), "Obstacles", 7);

            // ========================================================
            // 2. DEEP STORAGE PROPS & INTERACTIVITY
            // ========================================================
            // Configure interactable task stations on Specimen Incubation Tanks
            interactableLayer = LayerMask.NameToLayer("Interactable");

            // Configure Mega Containment Chamber (East Wall Centerpiece)
            var megaGo = GameObject.Find("Prop_Lab_Concept_Mega_Containment_Chamber") ?? GameObject.Find("Prop_Lab_Apex_Specimen_Zero_Tank");
            if (megaGo != null)
            {
                if (interactableLayer != -1) megaGo.layer = interactableLayer;
                var task = megaGo.GetComponent<TaskLocation>() ?? megaGo.AddComponent<TaskLocation>();
                var so = new SerializedObject(task);
                so.FindProperty("stationId").stringValue = "TASK_LAB_MEGA_CONTAINMENT";
                so.FindProperty("stationDisplayName").stringValue = "High-Risk Alien Mega-Containment Chamber // Level-5 Stasis Core";
                so.FindProperty("stationType").enumValueIndex = (int)TaskStationType.Computer;
                so.ApplyModifiedProperties();
            }

            // Configure West Dual Stasis Pods
            var westPodsGo = GameObject.Find("Prop_Lab_Concept_West_Dual_Pods") ?? GameObject.Find("Prop_Lab_Specimen_Pod_Battery_West");
            if (westPodsGo != null)
            {
                if (interactableLayer != -1) westPodsGo.layer = interactableLayer;
                var task = westPodsGo.GetComponent<TaskLocation>() ?? westPodsGo.AddComponent<TaskLocation>();
                var so = new SerializedObject(task);
                so.FindProperty("stationId").stringValue = "TASK_LAB_STASIS_PRESERVATION";
                so.FindProperty("stationDisplayName").stringValue = "Specimen Preservation Dual-Pods // Shared Coolant Manifold";
                so.FindProperty("stationType").enumValueIndex = (int)TaskStationType.Computer;
                so.ApplyModifiedProperties();
            }

            // Configure CRYO-VAULT Station (North-West Wall)
            var vaultGo = GameObject.Find("Prop_Lab_Concept_Cryo_Vault_Station");
            if (vaultGo != null)
            {
                if (interactableLayer != -1) vaultGo.layer = interactableLayer;
                var task = vaultGo.GetComponent<TaskLocation>() ?? vaultGo.AddComponent<TaskLocation>();
                var so = new SerializedObject(task);
                so.FindProperty("stationId").stringValue = "TASK_LAB_CRYO_VAULT";
                so.FindProperty("stationDisplayName").stringValue = "CRYO-VAULT 01 // Sample Vials Matrix & Telemetry Terminal";
                so.FindProperty("stationType").enumValueIndex = (int)TaskStationType.Computer;
                so.ApplyModifiedProperties();
            }

            // Configure CRYO-DECON Station (North-East Wall)
            var deconStationGo = GameObject.Find("Prop_Lab_Concept_Cryo_Decon_Station");
            if (deconStationGo != null)
            {
                if (interactableLayer != -1) deconStationGo.layer = interactableLayer;
                var task = deconStationGo.GetComponent<TaskLocation>() ?? deconStationGo.AddComponent<TaskLocation>();
                var so = new SerializedObject(task);
                so.FindProperty("stationId").stringValue = "TASK_LAB_CRYO_DECON";
                so.FindProperty("stationDisplayName").stringValue = "CRYO-DECON // High-Pressure Biohazard Decontamination Autoclave";
                so.FindProperty("stationType").enumValueIndex = (int)TaskStationType.ControlPanel;
                so.ApplyModifiedProperties();
            }

            // Configure Central Wet-Lab Island Workbench (DNA Helix & Microscope)
            var benchGo = GameObject.Find("Prop_Lab_Concept_Central_Workbench") ?? GameObject.Find("Prop_Lab_Central_Island_Workbench");
            if (benchGo != null)
            {
                if (interactableLayer != -1) benchGo.layer = interactableLayer;
                var task = benchGo.GetComponent<TaskLocation>() ?? benchGo.AddComponent<TaskLocation>();
                var so = new SerializedObject(task);
                so.FindProperty("stationId").stringValue = "TASK_LAB_CENTRAL_WORKBENCH";
                so.FindProperty("stationDisplayName").stringValue = "Xenobiology Central Research Station (DNA Helix Sequencer & Electron Microscope)";
                so.FindProperty("stationType").enumValueIndex = (int)TaskStationType.Computer;
                so.ApplyModifiedProperties();
            }

            // Configure Southeast Sub-Zero Archive Bank
            var seArchiveGo = GameObject.Find("Prop_Lab_Concept_Southeast_Archive_Bank");
            if (seArchiveGo != null)
            {
                if (interactableLayer != -1) seArchiveGo.layer = interactableLayer;
                var task = seArchiveGo.GetComponent<TaskLocation>() ?? seArchiveGo.AddComponent<TaskLocation>();
                var so = new SerializedObject(task);
                so.FindProperty("stationId").stringValue = "TASK_LAB_SUBO_ARCHIVE";
                so.FindProperty("stationDisplayName").stringValue = "Sub-Zero Biological Archive Bank // Deep-Cold Slide Rack & Mini-Pods";
                so.FindProperty("stationType").enumValueIndex = (int)TaskStationType.Computer;
                so.ApplyModifiedProperties();
            }

            // Configure interactable task station on Cryogenic Decontamination Column (East Wall)
            var deconGo = GameObject.Find("Prop_Cryo_Frozen_Corpse");
            if (deconGo != null)
            {
                if (interactableLayer != -1) deconGo.layer = interactableLayer;
                var task = deconGo.GetComponent<TaskLocation>() ?? deconGo.AddComponent<TaskLocation>();
                var so = new SerializedObject(task);
                so.FindProperty("stationId").stringValue = "TASK_LAB_CRYO_DECON_COLUMN";
                so.FindProperty("stationDisplayName").stringValue = "Specimen Cryo-Decontamination & Rapid Freezing Autoclave";
                so.FindProperty("stationType").enumValueIndex = (int)TaskStationType.Mechanical;
                so.ApplyModifiedProperties();
            }

            // Ensure prototype orange debug marker on TaskLoc_DeepStorage is hidden
            var taskLocGo = GameObject.Find("TaskLoc_DeepStorage");
            if (taskLocGo != null)
            {
                var sr = taskLocGo.GetComponent<SpriteRenderer>();
                if (sr != null) sr.enabled = false;
            }

            // Clean up old individual props superseded by unified stations
            string[] obsoleteProps = new string[] {
                "Prop_Cryo_Dewar_Storage_Rack", "Shadow_Cryo_Dewar_Storage_Rack",
                "Prop_Cryo_Compressor_Unit", "Shadow_Cryo_Compressor_Unit",
                "Prop_Lab_Specimen_Tank_Humanoid", "Shadow_Lab_Specimen_Tank_Humanoid",
                "Prop_Lab_Specimen_Tank_Tentacle", "Shadow_Lab_Specimen_Tank_Tentacle",
                "Prop_Cryo_Frozen_Corpse"
            };
            foreach (var propName in obsoleteProps)
            {
                var ob = GameObject.Find(propName);
                if (ob != null) Undo.DestroyObjectImmediate(ob);
            }

            // Clean up any obsolete crane or clutter props
            var oldCrane = GameObject.Find("Prop_DeepStorage_Crane_Trolley");
            if (oldCrane != null) Undo.DestroyObjectImmediate(oldCrane);
            var oldCraneShadow = GameObject.Find("Shadow_DeepStorage_Crane_Trolley");
            if (oldCraneShadow != null) Undo.DestroyObjectImmediate(oldCraneShadow);
            var oldFuelRack = GameObject.Find("Prop_DeepStorage_Fuel_Rack");
            if (oldFuelRack != null) Undo.DestroyObjectImmediate(oldFuelRack);
            var oldFuelShadow = GameObject.Find("Shadow_DeepStorage_Fuel_Rack");
            if (oldFuelShadow != null) Undo.DestroyObjectImmediate(oldFuelShadow);

            // Clean up obsolete generic equipment if still in scene
            var oldFE = GameObject.Find("Prop_DeepStorage_Fire_Extinguisher");
            if (oldFE != null) Undo.DestroyObjectImmediate(oldFE);
            var oldFEShadow = GameObject.Find("Shadow_DeepStorage_Fire_Extinguisher");
            if (oldFEShadow != null) Undo.DestroyObjectImmediate(oldFEShadow);
            var oldFA = GameObject.Find("Prop_DeepStorage_FirstAid");
            if (oldFA != null) Undo.DestroyObjectImmediate(oldFA);
            var oldFAShadow = GameObject.Find("Shadow_DeepStorage_FirstAid");
            if (oldFAShadow != null) Undo.DestroyObjectImmediate(oldFAShadow);

            // Configure interactable task station on Biohazard Decontamination Purge Station
            var purgeGo = GameObject.Find("Prop_Cryo_Thermal_Rescue_Kit");
            if (purgeGo != null)
            {
                if (interactableLayer != -1) purgeGo.layer = interactableLayer;
                var task = purgeGo.GetComponent<TaskLocation>() ?? purgeGo.AddComponent<TaskLocation>();
                var so = new SerializedObject(task);
                so.FindProperty("stationId").stringValue = "TASK_XENO_DECON_PURGE";
                so.FindProperty("stationDisplayName").stringValue = "Biohazard Decontamination Purge Station";
                so.FindProperty("stationType").enumValueIndex = (int)TaskStationType.ControlPanel;
                so.ApplyModifiedProperties();
            }

            // ========================================================
            // 3. POWER RELAY PROPS & INTERACTIVITY
            // ========================================================
            // Make Breaker Cabinet an Interactable Grid Control Point
            var breaker = GameObject.Find("Prop_PowerRelay_Breaker_Cabinet");
            if (breaker != null)
            {
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
            AddPointLight("Light_NodeC_Bridge_Console", new Vector3(-2.6f, -34.0f, 0f), new Color(1.0f, 0.75f, 0.30f, 1f), 0.85f, 2.8f);

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
