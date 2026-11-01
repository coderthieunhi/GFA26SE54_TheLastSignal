#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Tilemaps;

namespace TheLastSignal.EditorTools
{
    public static class NodeBEnvironmentSetup
    {
        private const string TilesSourceDir = "Assets/_Project/Sprites/Environments/NodeB/Tiles";
        private const string PropsSourceDir = "Assets/_Project/Sprites/Environments/NodeB/Props";
        private const string DecalsSourceDir = "Assets/_Project/Sprites/Environments/NodeB/Decals";
        private const string SignageSourceDir = "Assets/_Project/Sprites/Environments/NodeB/Signage";
        private const string TileAssetsDir = "Assets/_Project/Tiles/NodeB";
        private const string PrefabDir = "Assets/_Project/Prefabs/Environments/NodeB";

        [MenuItem("Tools/Facility/Setup Node B From Reference", false, 55)]
        public static void SetupNodeBArt()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            // 1. Configure and import textures (PPU = 128)
            ConfigureTextures();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            // 2. Create / update Tile ScriptableObjects
            CreateTileAssets();
            AssetDatabase.SaveAssets();

            // 3. Create reusable Prefabs
            CreatePrefabs();
            AssetDatabase.SaveAssets();

            // 4. Decorate Node B and corridors matching the authoritative reference
            DecorateActiveScene();

            // 5. Setup Atmospheric Lighting matching reference
            SetupOperationalLighting();

            // 6. Setup Atmospheric Particle VFX
            var nodeBEnv = GameObject.Find("NodeB_Environment");
            if (nodeBEnv != null) SetupAtmosphericVFX(nodeBEnv);

            // 7. Configure and repair connecting corridor doors (eliminates red placeholder artifacts)
            ConfigureCorridorDoors();

            // 8. Synchronize peripheral rooms lighting with central breaker shunt matrix status
            SynchronizePeripheralLighting();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("<color=green>[NodeBEnvironmentSetup] Successfully built and decorated Node B with 100% Polish Pass!</color>");
        }

        [MenuItem("Tools/Facility/Turn ON Node B Lighting", false, 57)]
        public static void TurnOnNodeBLighting()
        {
            SetupOperationalLighting();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("<color=green>[NodeBEnvironmentSetup] Operational 2D Lighting for Node B is fully ON!</color>");
        }

        [MenuItem("Tools/Facility/Refine Complete Facility Visuals", false, 58)]
        public static void RefineCompleteFacility()
        {
            // 1. Run Node B Art setup
            SetupNodeBArt();

            // 1b. Setup Node B atmospheric particle systems
            var nodeBEnv = GameObject.Find("NodeB_Environment");
            if (nodeBEnv != null) SetupAtmosphericVFX(nodeBEnv);

            // 1c. Re-enable and balance Airlock lighting (fixes the pitch-black disabled lights)
            var airlockDecors = GameObject.Find("Airlock_Decorations");
            if (airlockDecors != null)
            {
                var light2DType = typeof(Light2D);
                var updateMeshMethod = light2DType.GetMethod("UpdateMesh", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var updateBoundsMethod = light2DType.GetMethod("UpdateBoundingSphere", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                void RefreshLight(Light2D l)
                {
                    if (l == null) return;
                    l.enabled = true;
                    if (updateMeshMethod != null) updateMeshMethod.Invoke(l, new object[] { true });
                    if (updateBoundsMethod != null) updateBoundsMethod.Invoke(l, null);
                    EditorUtility.SetDirty(l);
                }

                var ambient = airlockDecors.transform.Find("Light_Airlock_Ambient")?.GetComponent<Light2D>();
                if (ambient != null)
                {
                    ambient.intensity = 0.60f;
                    ambient.pointLightOuterRadius = 10.0f;
                    RefreshLight(ambient);
                }

                var nL = airlockDecors.transform.Find("Light_Neon_North_Left")?.GetComponent<Light2D>();
                if (nL != null) { nL.intensity = 0.85f; RefreshLight(nL); }

                var nR = airlockDecors.transform.Find("Light_Neon_North_Right")?.GetComponent<Light2D>();
                if (nR != null) { nR.intensity = 0.85f; RefreshLight(nR); }

                var sL = airlockDecors.transform.Find("Light_Neon_South_Left")?.GetComponent<Light2D>();
                if (sL != null) { sL.intensity = 0.85f; RefreshLight(sL); }

                var sR = airlockDecors.transform.Find("Light_Neon_South_Right")?.GetComponent<Light2D>();
                if (sR != null) { sR.intensity = 0.85f; RefreshLight(sR); }

                var radar = airlockDecors.transform.Find("Light_Console_Radar")?.GetComponent<Light2D>();
                if (radar != null) { radar.intensity = 0.95f; RefreshLight(radar); }
            }

            // 2. Refine Vent Shaft Lighting (Calm exaggerated fan beam & reduce room glare)
            var fanSpot = GameObject.Find("VP_Light_Fan_Spot")?.GetComponent<Light2D>();
            if (fanSpot != null)
            {
                fanSpot.intensity = 1.0f;
                fanSpot.pointLightOuterRadius = 4.2f;
                fanSpot.shadowIntensity = 0.45f;
                EditorUtility.SetDirty(fanSpot);
            }

            var consoleTeal = GameObject.Find("VP_Light_Console_Teal")?.GetComponent<Light2D>();
            if (consoleTeal != null)
            {
                consoleTeal.intensity = 0.70f;
                consoleTeal.pointLightOuterRadius = 2.4f;
                EditorUtility.SetDirty(consoleTeal);
            }

            var redEmergency = GameObject.Find("VP_Light_Red_Emergency")?.GetComponent<Light2D>();
            if (redEmergency != null)
            {
                redEmergency.intensity = 0.85f;
                redEmergency.pointLightOuterRadius = 2.2f;
                EditorUtility.SetDirty(redEmergency);
            }

            var flashBeam = GameObject.Find("VP_Light_Flashlight_Beam")?.GetComponent<Light2D>();
            if (flashBeam != null)
            {
                flashBeam.intensity = 0.75f;
                flashBeam.pointLightOuterRadius = 1.8f;
                EditorUtility.SetDirty(flashBeam);
            }

            var ventDoor = GameObject.Find("Indicator_Door_VentShaft")?.GetComponent<Light2D>();
            if (ventDoor != null)
            {
                ventDoor.intensity = 0.75f;
                ventDoor.pointLightOuterRadius = 1.4f;
                EditorUtility.SetDirty(ventDoor);
            }

            // 3. Refine Mess Hall Lighting (Balance kitchen glare & boost dining tables visibility)
            var kitchenTask = GameObject.Find("Light_MH_Kitchen_TaskLight")?.GetComponent<Light2D>();
            if (kitchenTask != null)
            {
                kitchenTask.intensity = 1.15f;
                kitchenTask.pointLightOuterRadius = 3.2f;
                EditorUtility.SetDirty(kitchenTask);
            }

            var kitchenAmber = GameObject.Find("Light_MH_Kitchen_Amber")?.GetComponent<Light2D>();
            if (kitchenAmber != null)
            {
                kitchenAmber.intensity = 0.85f;
                kitchenAmber.pointLightOuterRadius = 3.2f;
                EditorUtility.SetDirty(kitchenAmber);
            }

            var ceilWest = GameObject.Find("Light_MH_Ceiling_West")?.GetComponent<Light2D>();
            if (ceilWest != null)
            {
                ceilWest.intensity = 0.65f;
                ceilWest.pointLightOuterRadius = 3.0f;
                EditorUtility.SetDirty(ceilWest);
            }

            var ceilEast = GameObject.Find("Light_MH_Ceiling_East")?.GetComponent<Light2D>();
            if (ceilEast != null)
            {
                ceilEast.intensity = 0.65f;
                ceilEast.pointLightOuterRadius = 3.0f;
                EditorUtility.SetDirty(ceilEast);
            }

            var diningFill = GameObject.Find("Light_MH_Dining_Fill")?.GetComponent<Light2D>();
            if (diningFill != null)
            {
                diningFill.intensity = 0.85f;
                diningFill.pointLightOuterRadius = 6.0f;
                diningFill.pointLightInnerRadius = 1.0f;
                diningFill.color = new Color(1.0f, 0.86f, 0.65f, 1f);
                EditorUtility.SetDirty(diningFill);
            }

            var mhLightRoot = GameObject.Find("MessHall_Lighting");
            if (mhLightRoot != null)
            {
                Light2D EnsureLight(string name, Vector3 pos, Color col, float intensity, float outerRad, float innerRad)
                {
                    var t = mhLightRoot.transform.Find(name);
                    GameObject go;
                    if (t == null)
                    {
                        go = new GameObject(name);
                        go.transform.SetParent(mhLightRoot.transform);
                    }
                    else
                    {
                        go = t.gameObject;
                    }
                    go.transform.position = pos;
                    var l = go.GetComponent<Light2D>() ?? go.AddComponent<Light2D>();
                    l.lightType = Light2D.LightType.Point;
                    l.color = col;
                    l.intensity = intensity;
                    l.pointLightOuterRadius = outerRad;
                    l.pointLightInnerRadius = innerRad;
                    l.falloffIntensity = 0.5f;
                    EditorUtility.SetDirty(l);
                    return l;
                }

                EnsureLight("Light_MH_Tables_North", new Vector3(-15.35f, -2.50f, 0f), new Color(1.0f, 0.82f, 0.52f, 1f), 0.95f, 3.4f, 0.4f);
                EnsureLight("Light_MH_Tables_South", new Vector3(-15.35f, -5.60f, 0f), new Color(1.0f, 0.80f, 0.48f, 1f), 0.90f, 3.4f, 0.4f);
            }

            // 4. Refine Cargo Hold Lighting (Elevate bay lights from 0.22 to 0.55 for logistics lane readability)
            var cargoBayN = GameObject.Find("Light_CargoHold_Bay_North")?.GetComponent<Light2D>();
            if (cargoBayN != null)
            {
                cargoBayN.intensity = 0.55f;
                cargoBayN.pointLightOuterRadius = 6.0f;
                EditorUtility.SetDirty(cargoBayN);
            }

            var cargoBayS = GameObject.Find("Light_CargoHold_Bay_South")?.GetComponent<Light2D>();
            if (cargoBayS != null)
            {
                cargoBayS.intensity = 0.55f;
                cargoBayS.pointLightOuterRadius = 6.0f;
                EditorUtility.SetDirty(cargoBayS);
            }

            // 5. Elevate Central Hub Key Spotlight prominence
            var hubKeySpot = GameObject.Find("Light_NodeB_Junction_KeySpot")?.GetComponent<Light2D>();
            if (hubKeySpot != null)
            {
                hubKeySpot.intensity = 2.4f;
                hubKeySpot.pointLightOuterRadius = 4.2f;
                hubKeySpot.color = new Color(0.96f, 0.98f, 1.0f, 1f);
                EditorUtility.SetDirty(hubKeySpot);
            }

            EditorSceneManager.SaveOpenScenes();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("<color=green>[NodeBEnvironmentSetup] Complete 5-Space Facility Refinement successfully applied and saved!</color>");
        }

        public static void SetupAtmosphericVFX(GameObject nodeBEnv)
        {
            if (nodeBEnv == null) return;
            var vfxRoot = nodeBEnv.transform.Find("VFX");
            if (vfxRoot != null)
            {
                Object.DestroyImmediate(vfxRoot.gameObject);
            }
            var goRoot = new GameObject("VFX");
            goRoot.transform.SetParent(nodeBEnv.transform);

            Material dustMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Environment/Interactables/SafeLightJunction/Mat_VFX_CargoHold_Dust.mat");
            Material steamMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Environment/Interactables/SafeLightJunction/Mat_VFX_Steam.mat");
            Material sparksMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Environment/Interactables/SafeLightJunction/Mat_VFX_Sparks.mat");

            // 1. Dust Motes under central spotlight
            if (dustMat != null)
            {
                var dustGo = new GameObject("VFX_NodeB_DustMotes");
                dustGo.transform.SetParent(goRoot.transform);
                dustGo.transform.position = new Vector3(0.0f, -3.0f, 0f);
                var ps = dustGo.AddComponent<ParticleSystem>();
                var rend = dustGo.GetComponent<ParticleSystemRenderer>();
                rend.sharedMaterial = dustMat;
                rend.sortingLayerName = "Obstacles";
                rend.sortingOrder = 6;

                var main = ps.main;
                main.maxParticles = 24;
                main.startLifetime = 5.0f;
                main.startSpeed = 0.04f;
                main.startSize = 0.08f;
                main.startColor = new Color(0.85f, 0.92f, 1.0f, 0.35f);
                main.simulationSpace = ParticleSystemSimulationSpace.World;

                var emission = ps.emission;
                emission.rateOverTime = 4.0f;

                var shape = ps.shape;
                shape.shapeType = ParticleSystemShapeType.Box;
                shape.scale = new Vector3(3.2f, 3.2f, 0.5f);
            }

            // 2. Cryogenic / Coolant vapor leak at East Gas Manifold
            if (steamMat != null)
            {
                var steamGo = new GameObject("VFX_NodeB_Coolant_Mist");
                steamGo.transform.SetParent(goRoot.transform);
                steamGo.transform.position = new Vector3(4.3f, -1.2f, 0f);
                var ps = steamGo.AddComponent<ParticleSystem>();
                var rend = steamGo.GetComponent<ParticleSystemRenderer>();
                rend.sharedMaterial = steamMat;
                rend.sortingLayerName = "Floor";
                rend.sortingOrder = 4;

                var main = ps.main;
                main.maxParticles = 18;
                main.startLifetime = 2.8f;
                main.startSpeed = 0.12f;
                main.startSize = 0.28f;
                main.startColor = new Color(0.25f, 0.85f, 0.75f, 0.22f);

                var emission = ps.emission;
                emission.rateOverTime = 3.5f;

                var shape = ps.shape;
                shape.shapeType = ParticleSystemShapeType.Circle;
                shape.radius = 0.35f;
            }

            // 3. Electrical micro-sparks at SE damaged toolbox
            if (sparksMat != null)
            {
                var sparksGo = new GameObject("VFX_NodeB_Micro_Sparks");
                sparksGo.transform.SetParent(goRoot.transform);
                sparksGo.transform.position = new Vector3(4.2f, -7.0f, 0f);
                var ps = sparksGo.AddComponent<ParticleSystem>();
                var rend = sparksGo.GetComponent<ParticleSystemRenderer>();
                rend.sharedMaterial = sparksMat;
                rend.sortingLayerName = "Obstacles";
                rend.sortingOrder = 6;

                var main = ps.main;
                main.maxParticles = 15;
                main.startLifetime = 0.35f;
                main.startSpeed = 1.2f;
                main.startSize = 0.05f;
                main.gravityModifier = 0.8f;
                main.startColor = new Color(1.0f, 0.75f, 0.3f, 1.0f);

                var emission = ps.emission;
                emission.rateOverTime = 0f;
                emission.SetBursts(new ParticleSystem.Burst[] {
                    new ParticleSystem.Burst(0.5f, 2, 4, 1, 3.0f)
                });

                var shape = ps.shape;
                shape.shapeType = ParticleSystemShapeType.Cone;
                shape.angle = 45f;
                shape.radius = 0.1f;
            }
        }

        public static void ConfigureTextures()
        {
            string[] dirs = { TilesSourceDir, PropsSourceDir, DecalsSourceDir, SignageSourceDir };
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
                    bool texChanged = false;
                    if (texSettings.spriteMeshType != SpriteMeshType.FullRect) { texSettings.spriteMeshType = SpriteMeshType.FullRect; texChanged = true; }
                    if (texSettings.spriteExtrude != 1) { texSettings.spriteExtrude = 1; texChanged = true; }
                    if (texChanged)
                    {
                        importer.SetTextureSettings(texSettings);
                        changed = true;
                    }

                    if (changed)
                    {
                        EditorUtility.SetDirty(importer);
                        importer.SaveAndReimport();
                    }
                }
            }
        }

        public static void CreateTileAssets()
        {
            if (!Directory.Exists(TileAssetsDir))
            {
                Directory.CreateDirectory(TileAssetsDir);
            }

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

        public static void CreatePrefabs()
        {
            if (!Directory.Exists(PrefabDir))
            {
                Directory.CreateDirectory(PrefabDir);
            }

            Material urp2DLitMat = AssetDatabase.LoadAssetAtPath<Material>("Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Lit-Default.mat");
            if (urp2DLitMat == null)
            {
                Shader urp2DLit = Shader.Find("Universal Render Pipeline/2D/Sprite-Lit-Default");
                if (urp2DLit != null) urp2DLitMat = new Material(urp2DLit);
            }

            Sprite LoadSpriteSafe(string spritePath)
            {
                var s = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
                if (s != null) return s;
                var all = AssetDatabase.LoadAllAssetsAtPath(spritePath);
                foreach (var a in all)
                {
                    if (a is Sprite sub) return sub;
                }
                return null;
            }

            void BuildPrefab(string prefabName, string spritePath, bool addCollider = false, Vector2 colSize = default, Vector2 colOffset = default)
            {
                string fullPrefabPath = $"{PrefabDir}/{prefabName}.prefab";
                Sprite sp = LoadSpriteSafe(spritePath);
                if (sp == null) return;

                GameObject go = new GameObject(prefabName);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = sp;
                sr.sortingLayerName = "Obstacles";
                sr.sortingOrder = 5;
                if (urp2DLitMat != null) sr.sharedMaterial = urp2DLitMat;

                if (addCollider)
                {
                    var col = go.AddComponent<BoxCollider2D>();
                    col.size = colSize == default ? sp.rect.size / 128f : colSize;
                    col.offset = colOffset;
                }

                PrefabUtility.SaveAsPrefabAsset(go, fullPrefabPath);
                Object.DestroyImmediate(go);
            }

            // Create prefabs matching authoritative reference
            BuildPrefab("NodeB_Center_Pedestal", $"{PropsSourceDir}/Prop_NodeB_Center_Pedestal.png", false);
            BuildPrefab("NodeB_Command_Desk", $"{PropsSourceDir}/Prop_NodeB_Command_Desk.png", true, new Vector2(1.5f, 0.9f), new Vector2(0f, -0.1f));
            BuildPrefab("NodeB_West_Crates_Stack", $"{PropsSourceDir}/Prop_NodeB_West_Crates_Stack.png", true, new Vector2(0.9f, 1.4f), new Vector2(0f, 0.2f));
            BuildPrefab("NodeB_East_Cylinders_Rack", $"{PropsSourceDir}/Prop_NodeB_East_Cylinders_Rack.png", true, new Vector2(0.9f, 1.4f), new Vector2(0f, 0.2f));
            BuildPrefab("NodeB_NE_Equipment_Shelving", $"{PropsSourceDir}/Prop_NodeB_NE_Equipment_Shelving.png", true, new Vector2(1.1f, 0.9f));
            BuildPrefab("NodeB_South_Storage_Lockers", $"{PropsSourceDir}/Prop_NodeB_South_Storage_Lockers.png", true, new Vector2(2.0f, 0.6f));
            BuildPrefab("NodeB_SW_Sub_Crates", $"{PropsSourceDir}/Prop_NodeB_SW_Sub_Crates.png", true, new Vector2(0.9f, 0.7f));
            BuildPrefab("NodeB_SE_Maintenance_Tools", $"{PropsSourceDir}/Prop_NodeB_SE_Maintenance_Tools.png", true, new Vector2(0.9f, 0.7f));
            BuildPrefab("NodeB_Corridor_Wall_Pipes_L", $"{PropsSourceDir}/Prop_NodeB_Corridor_Wall_Pipes_L.png", false);
            BuildPrefab("NodeB_Corridor_Wall_Pipes_R", $"{PropsSourceDir}/Prop_NodeB_Corridor_Wall_Pipes_R.png", false);
            BuildPrefab("NodeB_Floor_Cables_Snake", $"{PropsSourceDir}/Prop_NodeB_Floor_Cables_Snake.png", false);
        }

        public static void DecorateActiveScene()
        {
            GameObject gridGo = GameObject.Find("Facility_Grid") ?? GameObject.Find("Grid");
            if (gridGo == null)
            {
                Debug.LogError("[NodeBEnvironmentSetup] Facility_Grid not found!");
                return;
            }

            Tilemap floorMap = gridGo.transform.Find("Floor_Tilemap")?.GetComponent<Tilemap>();
            Tilemap wallMap = gridGo.transform.Find("Wall_Tilemap")?.GetComponent<Tilemap>();
            if (floorMap == null || wallMap == null)
            {
                Debug.LogError("[NodeBEnvironmentSetup] Tilemaps not found!");
                return;
            }

            Undo.RecordObject(floorMap, "Decorate Node B Floor");
            Undo.RecordObject(wallMap, "Decorate Node B Walls");

            // --- LOAD TILES ---
            Tile fStd = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/NodeB_Floor_Standard.asset");
            Tile fCorridor = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/NodeB_Floor_Alt.asset") ?? fStd;
            Tile fReinforced = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/NodeB_Floor_Reinforced.asset") ?? fStd;
            Tile fScuffed = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/NodeB_Floor_Scuffed.asset") ?? fStd;
            Tile fStained = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/NodeB_Floor_Stained.asset") ?? fStd;
            Tile fRusted = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/NodeB_Floor_Rusted.asset") ?? fStd;
            Tile fDrain = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/NodeB_Floor_DrainageGrate.asset") ?? fStd;
            Tile fCableV = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/NodeB_Floor_CableChannel_V.asset") ?? fStd;
            Tile fCableH = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/NodeB_Floor_CableChannel_H.asset") ?? fStd;

            Tile thAirlock = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/NodeB_Threshold_Airlock.asset") ?? fStd;
            Tile thCargo = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/NodeB_Threshold_CargoHold.asset") ?? fStd;
            Tile thMess = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/NodeB_Threshold_MessHall.asset") ?? fStd;
            Tile thVent = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/NodeB_Threshold_VentShaft.asset") ?? fStd;

            Tile cBorderH = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/NodeB_Floor_Caution_H.asset") ?? fStd;
            Tile cBorderV = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/NodeB_Floor_Caution_V.asset") ?? fStd;
            Tile cCornerTL = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/NodeB_Floor_Caution_Corner_TL.asset") ?? fStd;
            Tile cCornerTR = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/NodeB_Floor_Caution_Corner_TR.asset") ?? fStd;
            Tile cCornerBL = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/NodeB_Floor_Caution_Corner_BL.asset") ?? fStd;
            Tile cCornerBR = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/NodeB_Floor_Caution_Corner_BR.asset") ?? fStd;
            Tile fGrate = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/NodeB_Floor_Platform_Grate.asset") ?? fStd;
            Tile thHaz = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/NodeB_Threshold_Hazard.asset") ?? fStd;

            // Walls
            Tile wStraightH = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/NodeB_Wall_Straight_H.asset");
            Tile wStraightV = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/NodeB_Wall_Straight_V.asset");
            Tile wCornerTL = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/NodeB_Wall_Corner_TL.asset") ?? wStraightH;
            Tile wCornerTR = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/NodeB_Wall_Corner_TR.asset") ?? wStraightH;
            Tile wCornerBL = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/NodeB_Wall_Corner_BL.asset") ?? wStraightH;
            Tile wCornerBR = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/NodeB_Wall_Corner_BR.asset") ?? wStraightH;
            Tile wElbowTL = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/NodeB_Wall_Corner_Elbow_TL.asset") ?? wCornerTL;
            Tile wElbowTR = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/NodeB_Wall_Corner_Elbow_TR.asset") ?? wCornerTR;
            Tile wElbowBL = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/NodeB_Wall_Corner_Elbow_BL.asset") ?? wCornerBL;
            Tile wElbowBR = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/NodeB_Wall_Corner_Elbow_BR.asset") ?? wCornerBR;

            // ========================================================
            // 1. PAINT NODE B FLOOR HIERARCHY (X in [-5..4], Y in [-8..1])
            // Cohesive, uniform industrial steel deck concourse across the full 10x10 Hub
            // (Completely eliminates the wall-to-wall dark cross!)
            // ========================================================
            for (int x = -5; x <= 4; x++)
            {
                for (int y = -8; y <= 1; y++)
                {
                    floorMap.SetTile(new Vector3Int(x, y, 0), fStd);
                }
            }

            // ========================================================
            // COMPACT OCTAGONAL TECHNICAL MOUNTING APRON
            // Confined strictly beneath the 3.0m x 3.0m central junction console
            // Grounds the console without slicing the concourse into quadrants
            // ========================================================
            // Core mounting floor (under the console chassis):
            floorMap.SetTile(new Vector3Int(-1, -3, 0), fReinforced);
            floorMap.SetTile(new Vector3Int(0, -3, 0), fReinforced);
            floorMap.SetTile(new Vector3Int(-1, -4, 0), fReinforced);
            floorMap.SetTile(new Vector3Int(0, -4, 0), fReinforced);

            // Technical collar tiles:
            floorMap.SetTile(new Vector3Int(-1, -2, 0), fReinforced);
            floorMap.SetTile(new Vector3Int(0, -2, 0), fReinforced);
            floorMap.SetTile(new Vector3Int(-1, -5, 0), fReinforced);
            floorMap.SetTile(new Vector3Int(0, -5, 0), fReinforced);
            floorMap.SetTile(new Vector3Int(-2, -3, 0), fReinforced);
            floorMap.SetTile(new Vector3Int(-2, -4, 0), fReinforced);
            floorMap.SetTile(new Vector3Int(1, -3, 0), fReinforced);
            floorMap.SetTile(new Vector3Int(1, -4, 0), fReinforced);

            // ========================================================
            // INDUSTRIAL FLOOR ZONING & CIRCULATION PATINA
            // ========================================================
            // Reinforced structural decking under heavy equipment footings:
            // NW Command Station decking:
            floorMap.SetTile(new Vector3Int(-4, 0, 0), fReinforced);
            floorMap.SetTile(new Vector3Int(-3, 0, 0), fReinforced);
            floorMap.SetTile(new Vector3Int(-4, 1, 0), fReinforced);
            floorMap.SetTile(new Vector3Int(-3, 1, 0), fReinforced);

            // NE Equipment Shelving decking:
            floorMap.SetTile(new Vector3Int(2, 0, 0), fReinforced);
            floorMap.SetTile(new Vector3Int(3, 0, 0), fReinforced);
            floorMap.SetTile(new Vector3Int(2, 1, 0), fReinforced);
            floorMap.SetTile(new Vector3Int(3, 1, 0), fReinforced);

            // South Storage Locker bank decking:
            floorMap.SetTile(new Vector3Int(-4, -7, 0), fReinforced);
            floorMap.SetTile(new Vector3Int(-3, -7, 0), fReinforced);
            floorMap.SetTile(new Vector3Int(-2, -7, 0), fReinforced);
            floorMap.SetTile(new Vector3Int(1, -7, 0), fReinforced);
            floorMap.SetTile(new Vector3Int(2, -7, 0), fReinforced);
            floorMap.SetTile(new Vector3Int(3, -7, 0), fReinforced);

            // North-South direct circulation wear path (between Airlock and Cargo Hold)
            for (int y = -8; y <= 1; y++)
            {
                if (y >= -1 || y <= -6)
                {
                    floorMap.SetTile(new Vector3Int(-1, y, 0), fScuffed);
                    floorMap.SetTile(new Vector3Int(0, y, 0), fScuffed);
                }
            }

            // West-East direct circulation wear path (between Mess Hall and Vent Shaft)
            for (int x = -5; x <= 4; x++)
            {
                if (x <= -3 || x >= 2)
                {
                    floorMap.SetTile(new Vector3Int(x, -4, 0), fScuffed);
                    floorMap.SetTile(new Vector3Int(x, -3, 0), fScuffed);
                }
            }

            // Technical service drainage grates in perimeter maintenance corners and fluid runoff zones (Visibly placed)
            floorMap.SetTile(new Vector3Int(-3, -1, 0), fDrain); // In front of NW Command Desk
            floorMap.SetTile(new Vector3Int(3, -2, 0), fDrain);  // In front of NE Gas Rack & Coolant Spill
            floorMap.SetTile(new Vector3Int(-4, -5, 0), fDrain); // Open maintenance floor in SW
            floorMap.SetTile(new Vector3Int(3, -5, 0), fDrain);  // Open maintenance floor in SE
            floorMap.SetTile(new Vector3Int(-1, -6, 0), fDrain); // Central south gutter
            floorMap.SetTile(new Vector3Int(0, -6, 0), fDrain);  // Central south gutter

            // Heavy subfloor access hatches (flush with floor plates)
            Tile fHatch = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetsDir}/NodeB_Floor_ServiceHatch.asset") ?? fStd;
            floorMap.SetTile(new Vector3Int(-2, 0, 0), fHatch);  // NW technical hatch
            floorMap.SetTile(new Vector3Int(2, 0, 0), fHatch);   // NE technical hatch
            floorMap.SetTile(new Vector3Int(-2, -7, 0), fHatch); // SW technical hatch
            floorMap.SetTile(new Vector3Int(1, -7, 0), fHatch);  // SE technical hatch

            // Subtle equipment base patina (strictly under wall equipment footings):
            floorMap.SetTile(new Vector3Int(-5, -1, 0), fScuffed);
            floorMap.SetTile(new Vector3Int(4, -1, 0), fScuffed);
            floorMap.SetTile(new Vector3Int(-5, -7, 0), fScuffed);
            floorMap.SetTile(new Vector3Int(4, -7, 0), fScuffed);

            // ========================================================
            // CONTINUOUS SUBFLOOR CABLE TRENCHES
            // Seamless conduit raceways extending from central terminal to 4 doorways
            // ========================================================
            // North Raceway (exiting pedestal at Y=-2 up to Doorway threshold at Y=0)
            for (int y = -2; y <= 0; y++)
            {
                floorMap.SetTile(new Vector3Int(-1, y, 0), fCableV);
                floorMap.SetTile(new Vector3Int(0, y, 0), fCableV);
            }
            // South Raceway (exiting pedestal at Y=-5 down to Doorway threshold at Y=-7)
            for (int y = -7; y <= -5; y++)
            {
                floorMap.SetTile(new Vector3Int(-1, y, 0), fCableV);
                floorMap.SetTile(new Vector3Int(0, y, 0), fCableV);
            }
            // West Raceway (exiting pedestal at X=-2 over to Doorway threshold at X=-4)
            for (int x = -4; x <= -2; x++)
            {
                floorMap.SetTile(new Vector3Int(x, -4, 0), fCableH);
                floorMap.SetTile(new Vector3Int(x, -3, 0), fCableH);
            }
            // East Raceway (exiting pedestal at X=1 over to Doorway threshold at X=3)
            for (int x = 1; x <= 3; x++)
            {
                floorMap.SetTile(new Vector3Int(x, -4, 0), fCableH);
                floorMap.SetTile(new Vector3Int(x, -3, 0), fCableH);
            }

            // ========================================================
            // MECHANICAL RECESSED BLAST DOOR SILLS (PORTAL THRESHOLDS)
            // ========================================================
            floorMap.SetTile(new Vector3Int(-1, 1, 0), thAirlock);
            floorMap.SetTile(new Vector3Int(0, 1, 0), thAirlock);

            floorMap.SetTile(new Vector3Int(-1, -8, 0), thCargo);
            floorMap.SetTile(new Vector3Int(0, -8, 0), thCargo);

            floorMap.SetTile(new Vector3Int(-5, -4, 0), thMess);
            floorMap.SetTile(new Vector3Int(-5, -5, 0), thMess);

            floorMap.SetTile(new Vector3Int(4, -4, 0), thVent);
            floorMap.SetTile(new Vector3Int(4, -5, 0), thVent);

            // ========================================================
            // 2. PAINT 4 CONNECTING CORRIDORS
            // ========================================================
            // North Corridor (X in [-1, 0], Y in [2..6])
            for (int y = 2; y <= 6; y++)
            {
                floorMap.SetTile(new Vector3Int(-1, y, 0), fCorridor);
                floorMap.SetTile(new Vector3Int(0, y, 0), fCorridor);
                wallMap.SetTile(new Vector3Int(-2, y, 0), wStraightV);
                wallMap.SetTile(new Vector3Int(1, y, 0), wStraightV);
            }
            // North Entrance Threshold (Clean industrial floor without painted hazard lines)
            floorMap.SetTile(new Vector3Int(-1, 7, 0), fCorridor);
            floorMap.SetTile(new Vector3Int(0, 7, 0), fCorridor);
            wallMap.SetTile(new Vector3Int(-2, 7, 0), wStraightV);
            wallMap.SetTile(new Vector3Int(1, 7, 0), wStraightV);

            // South Corridor (X in [-1, 0], Y in [-12..-9])
            for (int y = -12; y <= -9; y++)
            {
                floorMap.SetTile(new Vector3Int(-1, y, 0), fCorridor);
                floorMap.SetTile(new Vector3Int(0, y, 0), fCorridor);
                wallMap.SetTile(new Vector3Int(-2, y, 0), wStraightV);
                wallMap.SetTile(new Vector3Int(1, y, 0), wStraightV);
            }
            // South Entrance Threshold (Clean industrial floor without painted hazard lines)
            floorMap.SetTile(new Vector3Int(-1, -13, 0), fCorridor);
            floorMap.SetTile(new Vector3Int(0, -13, 0), fCorridor);
            wallMap.SetTile(new Vector3Int(-2, -13, 0), wStraightV);
            wallMap.SetTile(new Vector3Int(1, -13, 0), wStraightV);

            // West Corridor (X in [-10..-7], Y in [-5, -4])
            for (int x = -10; x <= -7; x++)
            {
                floorMap.SetTile(new Vector3Int(x, -4, 0), fCorridor);
                floorMap.SetTile(new Vector3Int(x, -5, 0), fCorridor);
                wallMap.SetTile(new Vector3Int(x, -3, 0), wStraightH);
                wallMap.SetTile(new Vector3Int(x, -6, 0), wStraightH);
            }
            // West Entrance Threshold (Clean industrial floor without painted hazard lines)
            floorMap.SetTile(new Vector3Int(-6, -4, 0), fCorridor);
            floorMap.SetTile(new Vector3Int(-6, -5, 0), fCorridor);
            wallMap.SetTile(new Vector3Int(-6, -3, 0), wStraightV);
            wallMap.SetTile(new Vector3Int(-6, -6, 0), wStraightV);

            // Restore Mess Hall West portal connection (X = -11)
            Tile mhFloorCold01 = AssetDatabase.LoadAssetAtPath<Tile>("Assets/_Project/Tiles/MessHall/MH_Floor_Cold_01.asset");
            Tile mhFloorColdPlates = AssetDatabase.LoadAssetAtPath<Tile>("Assets/_Project/Tiles/MessHall/MH_Floor_Cold_Plates.asset");
            Tile mhWallDoorTop = AssetDatabase.LoadAssetAtPath<Tile>("Assets/_Project/Tiles/MessHall/MH_Wall_Doorframe_Top.asset");
            Tile mhWallDoorBot = AssetDatabase.LoadAssetAtPath<Tile>("Assets/_Project/Tiles/MessHall/MH_Wall_Doorframe_Bottom.asset");
            if (mhFloorColdPlates != null) floorMap.SetTile(new Vector3Int(-11, -4, 0), mhFloorColdPlates);
            if (mhFloorCold01 != null) floorMap.SetTile(new Vector3Int(-11, -5, 0), mhFloorCold01);
            if (mhWallDoorTop != null) wallMap.SetTile(new Vector3Int(-11, -3, 0), mhWallDoorTop);
            if (mhWallDoorBot != null) wallMap.SetTile(new Vector3Int(-11, -6, 0), mhWallDoorBot);

            // East Corridor (X in [5..9], Y in [-5, -4])
            for (int x = 5; x <= 9; x++)
            {
                floorMap.SetTile(new Vector3Int(x, -4, 0), fCorridor);
                floorMap.SetTile(new Vector3Int(x, -5, 0), fCorridor);
                wallMap.SetTile(new Vector3Int(x, -3, 0), wStraightH);
                wallMap.SetTile(new Vector3Int(x, -6, 0), wStraightH);
            }
            // East Entrance Threshold (Clean industrial floor without painted hazard lines)
            floorMap.SetTile(new Vector3Int(10, -4, 0), fCorridor);
            floorMap.SetTile(new Vector3Int(10, -5, 0), fCorridor);
            wallMap.SetTile(new Vector3Int(10, -3, 0), wStraightH);
            wallMap.SetTile(new Vector3Int(10, -6, 0), wStraightH);

            // ========================================================
            // 3. PAINT NODE B BULKHEAD WALLS & 45-DEGREE INNER CHAMFERS
            // ========================================================
            // North Straight Walls
            for (int x = -4; x <= -2; x++) wallMap.SetTile(new Vector3Int(x, 2, 0), wStraightH);
            for (int x = 1; x <= 3; x++) wallMap.SetTile(new Vector3Int(x, 2, 0), wStraightH);

            // South Straight Walls
            for (int x = -4; x <= -2; x++) wallMap.SetTile(new Vector3Int(x, -9, 0), wStraightH);
            for (int x = 1; x <= 3; x++) wallMap.SetTile(new Vector3Int(x, -9, 0), wStraightH);

            // West Straight Walls (All Y in [-3..0] and [-7..-6] are wStraightV Image 2 asset)
            for (int y = -3; y <= 0; y++) wallMap.SetTile(new Vector3Int(-6, y, 0), wStraightV);
            for (int y = -7; y <= -6; y++) wallMap.SetTile(new Vector3Int(-6, y, 0), wStraightV);

            // East Straight Walls (All Y in [-3..0] and [-7..-6] are wStraightV Image 2 asset)
            for (int y = -3; y <= 0; y++) wallMap.SetTile(new Vector3Int(5, y, 0), wStraightV);
            for (int y = -7; y <= -6; y++) wallMap.SetTile(new Vector3Int(5, y, 0), wStraightV);

            // Unified Industrial Perimeter Wall & Die-Cast Corner Elbow Loop (90° Junction Enclosures)
            // Completely clears inner chamfer obstructions, letting the corner junction boxes and LEDs shine!
            wallMap.SetTile(new Vector3Int(-5, 2, 0), wStraightH);
            wallMap.SetTile(new Vector3Int(-6, 1, 0), wStraightV);
            wallMap.SetTile(new Vector3Int(-6, 2, 0), wElbowTL);

            wallMap.SetTile(new Vector3Int(4, 2, 0), wStraightH);
            wallMap.SetTile(new Vector3Int(5, 1, 0), wStraightV);
            wallMap.SetTile(new Vector3Int(5, 2, 0), wElbowTR);

            wallMap.SetTile(new Vector3Int(-5, -9, 0), wStraightH);
            wallMap.SetTile(new Vector3Int(-6, -8, 0), wStraightV);
            wallMap.SetTile(new Vector3Int(-6, -9, 0), wElbowBL);

            wallMap.SetTile(new Vector3Int(4, -9, 0), wStraightH);
            wallMap.SetTile(new Vector3Int(5, -8, 0), wStraightV);
            wallMap.SetTile(new Vector3Int(5, -9, 0), wElbowBR);

            // Clear any obsolete inner chamfer tiles from wallMap so 90° corners are pristine
            wallMap.SetTile(new Vector3Int(-5, 1, 0), null);
            wallMap.SetTile(new Vector3Int(4, 1, 0), null);
            wallMap.SetTile(new Vector3Int(-5, -8, 0), null);
            wallMap.SetTile(new Vector3Int(4, -8, 0), null);

            // Ensure the 4 corner floor tiles are clean, unoccluded industrial steel deck
            floorMap.SetTile(new Vector3Int(-5, 1, 0), fStd);
            floorMap.SetTile(new Vector3Int(4, 1, 0), fStd);
            floorMap.SetTile(new Vector3Int(-5, -8, 0), fStd);
            floorMap.SetTile(new Vector3Int(4, -8, 0), fStd);

            // ========================================================
            // 4. INSTANTIATE REUSABLE PREFABS FROM REFERENCE
            // ========================================================
            var existingEnv = GameObject.Find("NodeB_Environment");
            if (existingEnv != null) Undo.DestroyObjectImmediate(existingEnv);

            GameObject nodeBEnv = new GameObject("NodeB_Environment");
            Undo.RegisterCreatedObjectUndo(nodeBEnv, "Create NodeB_Environment");

            GameObject propsRoot = new GameObject("Props");
            propsRoot.transform.SetParent(nodeBEnv.transform);

            Material urp2DLitMat = AssetDatabase.LoadAssetAtPath<Material>("Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Lit-Default.mat");
            if (urp2DLitMat == null)
            {
                Shader litShader = Shader.Find("Universal Render Pipeline/2D/Sprite-Lit-Default");
                if (litShader != null) urp2DLitMat = new Material(litShader);
            }

            Material urp2DUnlitMat = AssetDatabase.LoadAssetAtPath<Material>("Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat");
            if (urp2DUnlitMat == null)
            {
                Shader unlitShader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
                if (unlitShader != null) urp2DUnlitMat = new Material(unlitShader);
            }

            Sprite LoadSpriteSafe(string spritePath)
            {
                var s = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
                if (s != null) return s;
                var all = AssetDatabase.LoadAllAssetsAtPath(spritePath);
                foreach (var a in all)
                {
                    if (a is Sprite sub) return sub;
                }
                return null;
            }

            void AttachShadowCaster(GameObject go, Vector2 size, Vector2 offset = default)
            {
                var sc = go.AddComponent<ShadowCaster2D>();
                sc.selfShadows = false;
                sc.castsShadows = true;
                float hw = size.x * 0.5f;
                float hh = size.y * 0.5f;
                Vector3[] shapePath = new Vector3[] {
                    new Vector3(-hw + offset.x, -hh + offset.y, 0),
                    new Vector3(-hw + offset.x,  hh + offset.y, 0),
                    new Vector3( hw + offset.x,  hh + offset.y, 0),
                    new Vector3( hw + offset.x, -hh + offset.y, 0)
                };
                var field = typeof(ShadowCaster2D).GetField("m_ShapePath", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (field != null) field.SetValue(sc, shapePath);
                var hashField = typeof(ShadowCaster2D).GetField("m_ShapePathHash", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (hashField != null) hashField.SetValue(sc, (int)System.DateTime.Now.Ticks);
            }

            GameObject SpawnProp(string name, Vector3 pos, string spritePath, bool addCol = true, Vector2 colSize = default, Vector2 colOffset = default, string sortingLayer = "Obstacles", int sortingOrder = 5, bool addShadow = false, Material customMat = null, Color tint = default)
            {
                var sp = LoadSpriteSafe(spritePath);
                if (sp == null) return null;
                var go = new GameObject(name);
                go.transform.SetParent(propsRoot.transform);
                go.transform.position = pos;
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = sp;
                sr.sortingLayerName = sortingLayer;
                sr.sortingOrder = sortingOrder;
                sr.sharedMaterial = customMat != null ? customMat : urp2DLitMat;
                if (tint != default) sr.color = tint;

                if (addCol)
                {
                    var col = go.AddComponent<BoxCollider2D>();
                    col.size = colSize == default ? sp.rect.size / 128f : colSize;
                    col.offset = colOffset;
                }

                if (addShadow)
                {
                    Vector2 shadowSize = colSize == default ? sp.rect.size / 128f : colSize;
                    AttachShadowCaster(go, shadowSize, colOffset);
                }

                return go;
            }

            // 0. Central Junction Pedestal Contact AO Decal (Grounds the octagonal installation with soft gaussian falloff)
            SpawnProp("Decal_NodeB_Center_Pedestal_AO", new Vector3(0.0f, -3.0f, 0f), $"{DecalsSourceDir}/NodeB_Decal_Center_Pedestal_AO.png", false, default, default, "Floor", 2);

            // 1. Central Junction Routing Console (384x384 Master 3.0m x 3.0m 3D installation with physical BoxCollider2D & Interactive Terminal)
            // Centered at room center (0.0, -3.0). Overhead luminaire illuminates floor radially without artificial planar horizontal shadows.
            var pedestalGo = SpawnProp("Prop_NodeB_Center_Pedestal", new Vector3(0.0f, -3.0f, 0f), $"{PropsSourceDir}/Prop_NodeB_Center_Pedestal.png", true, new Vector2(1.2f, 0.9f), new Vector2(0f, 0.05f), "Obstacles", 3, false);
            if (pedestalGo != null)
            {
                pedestalGo.AddComponent<TheLastSignal.Entities.Environment.NodeBJunctionTerminal>();
            }

            // 2. NW Tactical Command Desk (with radar monitors, 2.5D contact drop shadow & Interactive Lore Terminal)
            var commandDeskGo = SpawnProp("Prop_NodeB_Command_Desk", new Vector3(-3.5f, 0.6f, 0f), $"{PropsSourceDir}/Prop_NodeB_Command_Desk.png", true, new Vector2(1.5f, 0.8f), Vector2.zero, "Obstacles", 5, true);
            if (commandDeskGo != null)
            {
                commandDeskGo.AddComponent<TheLastSignal.Entities.Environment.NodeBCommandLoreTerminal>();
            }

            // 3. NE Equipment Shelving (with rubber footing pads & 2.5D contact drop shadow, resting cleanly on floor)
            SpawnProp("Prop_NodeB_NE_Equipment_Shelving", new Vector3(2.8f, 0.6f, 0f), $"{PropsSourceDir}/Prop_NodeB_NE_Equipment_Shelving.png", true, new Vector2(1.1f, 0.8f), Vector2.zero, "Obstacles", 5, true);

            // 4. West Cargo Crates Stack (On industrial wooden shipping pallet, resting cleanly ON FLOOR flush against west bulkhead)
            SpawnProp("Prop_NodeB_West_Crates_Stack", new Vector3(-4.40f, -1.20f, 0f), $"{PropsSourceDir}/Prop_NodeB_West_Crates_Stack.png", true, new Vector2(0.9f, 1.2f), new Vector2(0f, -0.2f), "Obstacles", 5, true);

            // 5. East Oxygen / Gas Cylinders Rack (Bolted to wall via mounting brackets, skid base resting cleanly ON FLOOR)
            SpawnProp("Prop_NodeB_East_Cylinders_Rack", new Vector3(4.35f, -0.70f, 0f), $"{PropsSourceDir}/Prop_NodeB_East_Cylinders_Rack.png", true, new Vector2(0.9f, 1.2f), new Vector2(0f, -0.2f), "Obstacles", 5, true);

            // 6. Coolant Leak Puddle Decal under gas manifold base
            SpawnProp("Decal_NodeB_Coolant_Leak", new Vector3(4.25f, -1.25f, 0f), $"{DecalsSourceDir}/NodeB_Decal_Coolant_Leak.png", false, default, default, "Floor", 1);

            // 7. South Storage Lockers LEFT & RIGHT (Symmetric 1.0m clearance to South Doorway, resting completely ON FLOOR)
            var leftLockerGo = SpawnProp("Prop_NodeB_South_Storage_Lockers_Left", new Vector3(-2.5f, -7.0f, 0f), $"{PropsSourceDir}/Prop_NodeB_South_Storage_Lockers.png", true, new Vector2(2.0f, 0.8f), new Vector2(0f, -0.4f), "Obstacles", 5, true);
            if (leftLockerGo != null)
            {
                var lComp = leftLockerGo.AddComponent<TheLastSignal.Entities.Environment.NodeBLockerContainer>();
                lComp.ConfigureLocker("Tủ Vật Tư Kỹ Thuật A", "Pin Năng Lượng Dự Phòng & Cờ Lê Chịu Lực.");
            }

            var rightLockerGo = SpawnProp("Prop_NodeB_South_Storage_Lockers_Right", new Vector3(2.5f, -7.0f, 0f), $"{PropsSourceDir}/Prop_NodeB_South_Storage_Lockers.png", true, new Vector2(2.0f, 0.8f), new Vector2(0f, -0.4f), "Obstacles", 5, true);
            if (rightLockerGo != null)
            {
                var rComp = rightLockerGo.AddComponent<TheLastSignal.Entities.Environment.NodeBLockerContainer>();
                rComp.ConfigureLocker("Tủ Cứu Thương B", "Bộ Băng Gạc Kháng Khuẩn & Thuốc Trợ Tim.");
            }

            // 8. Secondary Clutter Props in safe corner zones (SW Crates tucked into corner, completely freeing Locker 01)
            SpawnProp("Prop_NodeB_SW_Sub_Crates", new Vector3(-4.3f, -7.0f, 0f), $"{PropsSourceDir}/Prop_NodeB_SW_Sub_Crates.png", true, new Vector2(0.9f, 0.7f), Vector2.zero, "Obstacles", 5, true);
            SpawnProp("Prop_NodeB_SE_Maintenance_Tools", new Vector3(3.8f, -7.0f, 0f), $"{PropsSourceDir}/Prop_NodeB_SE_Maintenance_Tools.png", true, new Vector2(0.9f, 0.7f), Vector2.zero, "Obstacles", 5, true);

            // 9. Doorway Vertical Nameplate Plaques (Cleanly mounted on West/East wall jambs, North/South use pristine floor thresholds)
            SpawnProp("Sign_NodeB_MessHall", new Vector3(-5.95f, -2.60f, 0f), $"{SignageSourceDir}/NodeB_Sign_MessHall_V.png", false, default, default, "Obstacles", 4, false, urp2DUnlitMat);
            SpawnProp("Sign_NodeB_VentShaft", new Vector3(4.95f, -2.60f, 0f), $"{SignageSourceDir}/NodeB_Sign_VentShaft_V.png", false, default, default, "Obstacles", 4, false, urp2DUnlitMat);

            // 10. North Wall Ambient Occlusion & Cast Shadow (Eliminates flat paper-cutout appearance)
            SpawnProp("Decal_NodeB_Wall_AO_North", new Vector3(0.0f, 1.15f, 0f), "Assets/_Project/Sprites/Environments/VentShaft/Props/VP_Decal_AO_North_Wall.png", false, default, default, "Floor", 3);

            // 11. Dropped Technician Datapad (Glowing cracked screen, red alert indicator, interactive lore)
            var datapadGo = SpawnProp("Prop_NodeB_Cracked_Datapad", new Vector3(-2.8f, -0.6f, 0f), $"{PropsSourceDir}/Prop_NodeB_Cracked_Datapad.png", true, new Vector2(0.5f, 0.5f), Vector2.zero, "Floor", 4);
            if (datapadGo != null)
            {
                datapadGo.AddComponent<TheLastSignal.Entities.Environment.NodeBTechDatapad>();
            }

            // 12. Industrial Weathering & Narrative Storytelling
            // Primary North-South Transit Corridor Hazard Clearance Decals (Airlock <-> Cargo Hold)
            SpawnProp("Decal_NodeB_Clearance_N", new Vector3(0.0f, -1.30f, 0f), $"{DecalsSourceDir}/NodeB_Decal_Clearance_Stripe.png", false, default, default, "Floor", 1);
            SpawnProp("Decal_NodeB_Clearance_S", new Vector3(0.0f, -4.70f, 0f), $"{DecalsSourceDir}/NodeB_Decal_Clearance_Stripe.png", false, default, default, "Floor", 1);

            // Electrical arc scorch mark perfectly aligned beneath the blown console fuse tube!
            SpawnProp("Decal_NodeB_Electrical_Scorch", new Vector3(-0.65f, -2.85f, 0f), $"{DecalsSourceDir}/NodeB_Decal_Electrical_Scorch.png", false, default, default, "Floor", 2);

            SpawnProp("Decal_NodeB_Blood_Handprint_Door", new Vector3(4.85f, -4.50f, 0f), "Assets/_Project/Art/Environment/MessHall/BloodDecals/MH_Blood_06_Handprint_Doorframe.png", false, default, default, "Obstacles", 4);
            SpawnProp("Decal_NodeB_Blood_Drips_East", new Vector3(3.20f, -4.30f, 0f), "Assets/_Project/Art/Environment/MessHall/BloodDecals/MH_Blood_03_Small_Drips.png", false, default, default, "Floor", 2);
            SpawnProp("Decal_NodeB_Blood_Seep", new Vector3(0.0f, -4.30f, 0f), "Assets/_Project/Art/Environment/MessHall/BloodDecals/MH_Blood_07_SeamSeep.png", false, default, default, "Floor", 2);
            SpawnProp("Decal_NodeB_Blood_Smear_South", new Vector3(0.0f, -6.80f, 0f), "Assets/_Project/Sprites/Environments/CargoHold/Props/Decal_Dried_Blood_Smear.png", false, default, default, "Floor", 2);
            SpawnProp("Decal_NodeB_Grime_SW", new Vector3(-4.40f, -6.80f, 0f), "Assets/_Project/Sprites/Environments/CargoHold/Props/Decal_Floor_Industrial_Grime_Large.png", false, default, default, "Floor", 2);

            // Clean up legacy test primitives / obstructions in the corridor approaches
            string[] legacyToDeactivate = { "Obstacle_Wall_A", "Obstacle_Wall_B", "Interactable_Closer", "Interactable_Facing", "Prop_MH_Door_Lamp_CorridorEnd", "Light_MH_Door_CorridorEnd", "Prop_NodeB_Floor_Cables_Snake" };
            foreach (var leg in legacyToDeactivate)
            {
                var legGo = GameObject.Find(leg);
                if (legGo != null) legGo.SetActive(false);
            }

            EditorSceneManager.MarkSceneDirty(floorMap.gameObject.scene);
        }

        public static void SetupOperationalLighting()
        {
            var nodeBEnv = GameObject.Find("NodeB_Environment");
            if (nodeBEnv == null)
            {
                Debug.LogWarning("[NodeBEnvironmentSetup] NodeB_Environment not found in scene!");
                return;
            }

            var lightingRoot = nodeBEnv.transform.Find("Lighting");
            if (lightingRoot == null)
            {
                var go = new GameObject("Lighting");
                go.transform.SetParent(nodeBEnv.transform);
                lightingRoot = go.transform;
            }

            while (lightingRoot.childCount > 0)
            {
                Object.DestroyImmediate(lightingRoot.GetChild(0).gameObject);
            }

            var light2DType = typeof(Light2D);
            var updateMeshMethod = light2DType.GetMethod("UpdateMesh", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var updateBoundsMethod = light2DType.GetMethod("UpdateBoundingSphere", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            Light2D AddLight(string name, Vector3 pos, Color col, float intensity, float outerRad, float innerRad = 0.2f, bool enableShadows = true, float falloff = 0.5f)
            {
                var go = new GameObject(name);
                go.transform.SetParent(lightingRoot);
                go.transform.position = pos;
                var l = go.AddComponent<Light2D>();
                l.lightType = Light2D.LightType.Point;
                l.color = col;
                l.intensity = intensity;
                l.pointLightOuterRadius = outerRad;
                l.pointLightInnerRadius = innerRad;
                l.falloffIntensity = falloff;
                l.shadowsEnabled = enableShadows;
                l.shadowIntensity = 0.75f;

                updateMeshMethod?.Invoke(l, new object[] { true });
                updateBoundsMethod?.Invoke(l, null);
                EditorUtility.SetDirty(l);
                return l;
            }

            // ========================================================
            // ZONE 1: SUBTLE INDUSTRIAL BASE AMBIENCE & PERIMETER WALL DEFINITION
            // Deep, shadowy low-key ambient (survival horror mood, rich shadow contrast, no void bleed)
            // ========================================================
            AddLight("Light_NodeB_Ambient_Core", new Vector3(0.0f, -3.0f, 0f), new Color(0.22f, 0.27f, 0.35f, 1f), 0.36f, 16.0f, 0.8f, false, 0.35f);

            // 4 Subtle Perimeter Corner Bulkhead Fills (Eliminates void bleed, keeps bulkhead silhouette visible)
            AddLight("Light_NodeB_Ambient_Corner_NW", new Vector3(-4.8f, 1.2f, 0f), new Color(0.12f, 0.18f, 0.26f, 1f), 0.22f, 4.5f, 0.4f, false, 0.30f);
            AddLight("Light_NodeB_Ambient_Corner_NE", new Vector3(4.2f, 1.2f, 0f), new Color(0.12f, 0.18f, 0.26f, 1f), 0.22f, 4.5f, 0.4f, false, 0.30f);
            AddLight("Light_NodeB_Ambient_Corner_SW", new Vector3(-4.8f, -7.8f, 0f), new Color(0.12f, 0.18f, 0.26f, 1f), 0.22f, 4.5f, 0.4f, false, 0.30f);
            AddLight("Light_NodeB_Ambient_Corner_SE", new Vector3(4.2f, -7.8f, 0f), new Color(0.12f, 0.18f, 0.26f, 1f), 0.22f, 4.5f, 0.4f, false, 0.30f);

            // ========================================================
            // ZONE 2: THE LANDMARK - JUNCTION ROUTING CONTROL NEXUS
            // Physically-grounded overhead industrial downlight with smooth radial falloff
            // (Zero 2D planar shadow occlusion; simulates overhead ceiling luminaire spilling smoothly onto steel concourse)
            // ========================================================
            AddLight("Light_NodeB_Junction_KeySpot", new Vector3(0.0f, -3.0f, 0f), new Color(0.96f, 0.98f, 1.0f, 1f), 1.50f, 5.2f, 0.35f, false, 0.65f);

            // Soft secondary ambient bounce (diffuse reflection scattering off metallic console chassis onto surrounding deck)
            AddLight("Light_NodeB_Junction_AmbientBounce", new Vector3(0.0f, -3.0f, 0f), new Color(0.30f, 0.38f, 0.50f, 1f), 0.38f, 7.2f, 0.5f, false, 0.50f);

            // Localized cyan core LED inside the nexus display (No self-shadow occlusion)
            AddLight("Light_NodeB_Junction_CoreLED", new Vector3(0.0f, -3.0f, 0f), new Color(0.12f, 0.88f, 1.0f, 1f), 0.85f, 1.8f, 0.2f, false);

            // Dedicated warm amber front console task fill (Illuminates hazard-striped breaker panel without self-shadowing)
            AddLight("Light_NodeB_Junction_ConsoleFill", new Vector3(0.0f, -3.45f, 0f), new Color(1.0f, 0.95f, 0.88f, 1f), 0.55f, 1.8f, 0.15f, false);

            // ========================================================
            // ZONE 3: NW TACTICAL COMMAND STATION & NE SHELVING
            // Balanced, subdued task lighting (does not compete with centerpiece)
            // ========================================================
            // Localized CRT Phosphor Green Display Glow (No shadow occlusion against own desk)
            AddLight("Light_NodeB_Command_CRT_Glow", new Vector3(-3.5f, 0.55f, 0f), new Color(0.15f, 0.96f, 0.38f, 1f), 0.55f, 1.2f, 0.2f, false);
            // Incandescent Work Tasklamp mounted above console
            AddLight("Light_NodeB_Command_TaskLight", new Vector3(-3.5f, 1.05f, 0f), new Color(1.0f, 0.94f, 0.82f, 1f), 0.35f, 1.4f, 0.3f, true);
            // NE Equipment Shelving Maintenance Luminaire
            AddLight("Light_NodeB_NE_Shelf_Luminaire", new Vector3(2.8f, 1.0f, 0f), new Color(1.0f, 0.78f, 0.45f, 1f), 0.40f, 1.5f, 0.3f, true);

            // Dropped Technician Datapad Red Alert Blinking LED
            var datapadLight = AddLight("Light_NodeB_Datapad_LED", new Vector3(-2.8f, -0.6f, 0f), new Color(1.0f, 0.15f, 0.10f, 1f), 0.55f, 0.85f, 0.1f, false);
            var datapadPulse = datapadLight.gameObject.AddComponent<TheLastSignal.Juice.PulsingLight2D>();
            datapadPulse.minIntensity = 0.2f;
            datapadPulse.maxIntensity = 0.7f;
            datapadPulse.pulseSpeed = 2.8f;

            // ========================================================
            // ZONE 4: WEST CARGO STAGING (ON FLOOR)
            // ========================================================
            AddLight("Light_NodeB_West_Cargo_Bulkhead", new Vector3(-4.40f, -0.6f, 0f), new Color(1.0f, 0.80f, 0.45f, 1f), 0.35f, 1.5f, 0.3f, true);
            AddLight("Light_NodeB_SW_Sub_Crates", new Vector3(-4.30f, -6.8f, 0f), new Color(1.0f, 0.70f, 0.30f, 1f), 0.30f, 1.4f, 0.2f, true);

            // ========================================================
            // ZONE 5: EAST LOGISTICS, GAS MANIFOLD & COOLANT SPILL
            // Subdued bulkhead lighting on gas rack
            // ========================================================
            AddLight("Light_NodeB_East_Cylinder_Bulkhead", new Vector3(4.35f, -0.5f, 0f), new Color(1.0f, 0.78f, 0.40f, 1f), 0.35f, 1.5f, 0.3f, true);
            // Coolant leak puddle glow (Emissive accent, no self-shadow)
            AddLight("Light_NodeB_East_Coolant_Leak", new Vector3(4.25f, -1.2f, 0f), new Color(0.10f, 0.85f, 0.72f, 1f), 0.20f, 0.7f, 0.1f, false);
            // SE Toolbox status LED (Emissive indicator, no self-shadow)
            var seToolLight = AddLight("Light_NodeB_SE_Toolbox_LED", new Vector3(3.80f, -6.6f, 0f), new Color(1.0f, 0.55f, 0.20f, 1f), 0.30f, 1.3f, 0.2f, false);
            var seFlicker = seToolLight.gameObject.AddComponent<TheLastSignal.Juice.FlickeringLight>();
            seFlicker.minIntensity = 0.05f;
            seFlicker.maxIntensity = 0.35f;
            seFlicker.flickerChance = 0.15f;

            // ========================================================
            // ZONE 6: SOUTH STORAGE LOCKER BANKS (SYMMETRIC ON FLOOR)
            // Warm amber lighting + Glowing interactive loot keycard indicators
            // ========================================================
            AddLight("Light_NodeB_South_Lockers_L", new Vector3(-2.5f, -6.9f, 0f), new Color(1.0f, 0.84f, 0.55f, 1f), 0.60f, 2.2f, 0.3f, false);
            AddLight("Light_NodeB_South_Lockers_R", new Vector3(2.5f, -6.9f, 0f), new Color(1.0f, 0.84f, 0.55f, 1f), 0.60f, 2.2f, 0.3f, false);
            // Electronic Keycard Status LEDs (Signals searchable container to player)
            AddLight("Light_NodeB_Locker_Keypad_L", new Vector3(-2.5f, -6.6f, 0f), new Color(0.20f, 0.95f, 0.70f, 1f), 0.45f, 0.85f, 0.1f, false);
            AddLight("Light_NodeB_Locker_Keypad_R", new Vector3(2.5f, -6.6f, 0f), new Color(0.25f, 1.0f, 0.45f, 1f), 0.45f, 0.85f, 0.1f, false);

            // ========================================================
            // ZONE 6B: 4-WALL RED CONDUIT WITH SPECULAR FLOOR GRAZING & CHASER SURGE
            // Positions grazing inward/downward onto concourse floor (0.8m - 1.2m specular wash)
            // Driven by NodeBRedConduitChaser for sequential high-voltage traveling surge waves!
            // ========================================================
            Vector3[] redStripPositions = new Vector3[]
            {
                // North Wall (TL -> NE, grazing downward onto concourse floor 1.2m)
                new Vector3(-5.2f, 1.65f, 0f),
                new Vector3(-3.8f, 1.65f, 0f),
                new Vector3(-2.2f, 1.65f, 0f),
                new Vector3(1.2f, 1.65f, 0f),
                new Vector3(2.8f, 1.65f, 0f),
                new Vector3(4.5f, 1.65f, 0f),

                // East Wall (NE -> SE, grazing westward onto floor)
                new Vector3(4.35f, 1.2f, 0f),
                new Vector3(4.35f, -0.8f, 0f),
                new Vector3(4.35f, -2.5f, 0f),
                new Vector3(4.35f, -6.5f, 0f),
                new Vector3(4.35f, -8.0f, 0f),

                // South Wall (SE -> SW, grazing northward onto floor)
                new Vector3(3.5f, -7.70f, 0f),
                new Vector3(1.3f, -7.70f, 0f),
                new Vector3(-2.3f, -7.70f, 0f),
                new Vector3(-3.8f, -7.70f, 0f),
                new Vector3(-5.2f, -7.70f, 0f),

                // West Wall (SW -> NW, grazing eastward onto floor)
                new Vector3(-4.85f, -8.0f, 0f),
                new Vector3(-4.85f, -6.5f, 0f),
                new Vector3(-4.85f, -2.5f, 0f),
                new Vector3(-4.85f, -0.8f, 0f),
                new Vector3(-4.85f, 1.2f, 0f)
            };

            var chaserList = new System.Collections.Generic.List<Light2D>();
            for (int i = 0; i < redStripPositions.Length; i++)
            {
                // Floor-grazing specular emergency light (reaches out onto concourse floor smoothly)
                var sLight = AddLight($"Light_NodeB_RedNeonStrip_{i:D2}", redStripPositions[i], new Color(0.92f, 0.12f, 0.10f, 1f), 0.38f, 2.40f, 0.15f, true, 0.35f);
                chaserList.Add(sLight);
            }

            // Attach the Sequential Circuit Surge Chaser Controller
            var chaserGo = new GameObject("NodeB_RedConduit_SurgeController");
            chaserGo.transform.SetParent(lightingRoot);
            var chaserComp = chaserGo.AddComponent<TheLastSignal.Juice.NodeBRedConduitChaser>();
            chaserComp.conduitLights = chaserList;
            chaserComp.surgeInterval = 4.0f;
            chaserComp.surgeSpeed = 14.0f;
            chaserComp.waveWidth = 3.6f;
            chaserComp.ambientIntensity = 0.38f;
            chaserComp.peakIntensity = 1.60f;
            chaserComp.ambientColor = new Color(0.92f, 0.12f, 0.10f, 1f);
            chaserComp.surgeColor = new Color(1.0f, 0.48f, 0.20f, 1f);

            // ========================================================
            // ZONE 7: 4 DOORWAY BULKHEADS, SIGNS & STATUS LEDS
            // Clean portal door status LEDs (North/South pristine thresholds, West/East wall jamb plaques)
            // ========================================================
            // North Doorway (Airlock Portal Status LEDs)
            AddLight("Light_Door_N_L", new Vector3(-0.8f, 1.8f, 0f), new Color(0.95f, 0.22f, 0.12f, 1f), 0.70f, 0.9f, 0.1f, false);
            AddLight("Light_Door_N_R", new Vector3(0.8f, 1.8f, 0f), new Color(0.95f, 0.22f, 0.12f, 1f), 0.70f, 0.9f, 0.1f, false);

            // South Doorway (Cargo Hold Portal Status LEDs)
            AddLight("Light_Door_S_L", new Vector3(-0.8f, -8.8f, 0f), new Color(0.95f, 0.22f, 0.12f, 1f), 0.70f, 0.9f, 0.1f, false);
            AddLight("Light_Door_S_R", new Vector3(0.8f, -8.8f, 0f), new Color(0.95f, 0.22f, 0.12f, 1f), 0.70f, 0.9f, 0.1f, false);

            // West Doorway (Mess Hall Portal Plaque & Door LEDs)
            AddLight("Light_Sign_MessHall", new Vector3(-5.95f, -2.6f, 0f), new Color(1.0f, 0.85f, 0.35f, 1f), 0.65f, 1.2f, 0.2f, false);
            AddLight("Light_Door_W_T", new Vector3(-5.85f, -3.2f, 0f), new Color(0.95f, 0.22f, 0.12f, 1f), 0.70f, 0.9f, 0.1f, false);
            AddLight("Light_Door_W_B", new Vector3(-5.85f, -5.8f, 0f), new Color(0.95f, 0.22f, 0.12f, 1f), 0.70f, 0.9f, 0.1f, false);

            // East Doorway (Vent Shaft Portal Plaque & Door LEDs)
            AddLight("Light_Sign_VentShaft", new Vector3(4.95f, -2.6f, 0f), new Color(0.3f, 0.85f, 1.0f, 1f), 0.65f, 1.2f, 0.2f, false);
            AddLight("Light_Door_E_T", new Vector3(4.85f, -3.2f, 0f), new Color(0.95f, 0.22f, 0.12f, 1f), 0.70f, 0.9f, 0.1f, false);
            AddLight("Light_Door_E_B", new Vector3(4.85f, -5.8f, 0f), new Color(0.95f, 0.22f, 0.12f, 1f), 0.70f, 0.9f, 0.1f, false);

            // ========================================================
            // ZONE 8: 4 CONNECTING CORRIDORS
            // Atmospheric, claustrophobic corridor lighting (subdued to prioritize Central Hub)
            // ========================================================
            // North Corridor (Sterile Cool Transition)
            AddLight("Light_Corridor_N_1", new Vector3(0.0f, 3.8f, 0f), new Color(0.85f, 0.95f, 1.0f, 1f), 0.70f, 2.6f, 0.2f);
            AddLight("Light_Corridor_N_2", new Vector3(0.0f, 5.8f, 0f), new Color(0.85f, 0.95f, 1.0f, 1f), 0.70f, 2.6f, 0.2f);

            // South Corridor (Logistics Amber)
            AddLight("Light_Corridor_S_1", new Vector3(0.0f, -10.4f, 0f), new Color(1.0f, 0.76f, 0.35f, 1f), 0.70f, 2.6f, 0.2f);
            AddLight("Light_Corridor_S_2", new Vector3(0.0f, -12.4f, 0f), new Color(1.0f, 0.76f, 0.35f, 1f), 0.70f, 2.6f, 0.2f);

            // West Corridor (Mess Hall Transition Amber)
            AddLight("Light_Corridor_W_1", new Vector3(-7.4f, -4.0f, 0f), new Color(1.0f, 0.78f, 0.40f, 1f), 0.70f, 2.6f, 0.2f);
            AddLight("Light_Corridor_W_2", new Vector3(-9.2f, -4.0f, 0f), new Color(1.0f, 0.78f, 0.40f, 1f), 0.70f, 2.6f, 0.2f);

            // East Corridor (Vent Shaft Warning Amber + Survival Horror Filament Flicker)
            AddLight("Light_Corridor_E_1", new Vector3(6.8f, -4.0f, 0f), new Color(1.0f, 0.72f, 0.30f, 1f), 0.70f, 2.6f, 0.2f);
            var flickLight = AddLight("Light_Corridor_E_2", new Vector3(8.8f, -4.0f, 0f), new Color(1.0f, 0.72f, 0.30f, 1f), 0.70f, 2.6f, 0.2f);
            flickLight.gameObject.AddComponent<TheLastSignal.Juice.FlickeringLight>();
            flickLight.gameObject.AddComponent<TheLastSignal.Juice.FlickeringLight>();

            // Attach Subtle Breathing Pulse to Doorway Warning Status LEDs
            string[] doorLEDs = { "Light_Door_N_L", "Light_Door_N_R", "Light_Door_S_L", "Light_Door_S_R", "Light_Door_W_T", "Light_Door_W_B", "Light_Door_E_T", "Light_Door_E_B" };
            for (int i = 0; i < doorLEDs.Length; i++)
            {
                var dGo = lightingRoot.Find(doorLEDs[i]);
                if (dGo != null)
                {
                    var pulse = dGo.gameObject.AddComponent<TheLastSignal.Juice.PulsingLight2D>();
                    pulse.minIntensity = 0.6f;
                    pulse.maxIntensity = 1.1f;
                    pulse.pulseSpeed = 2.0f;
                    pulse.phaseOffset = (i % 2 == 0) ? 0f : 0.5f;
                }
            }

            // Configure connecting corridor doors (fixes West Door placeholder artifact)
            ConfigureCorridorDoors();

            // Synchronize peripheral room lighting
            SynchronizePeripheralLighting();

            EditorSceneManager.MarkSceneDirty(nodeBEnv.scene);
        }

        public static void ConfigureCorridorDoors()
        {
            var doorVClosed = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Sprites/Environments/Airlock/Props/Door_Vertical_Closed.png");
            var doorVOpen = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Sprites/Environments/Airlock/Props/Door_Vertical_Open.png");
            var doorHClosed = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Sprites/Environments/Airlock/Props/Door_Horizontal_Closed.png");
            var doorHOpen = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Sprites/Environments/Airlock/Props/Door_Horizontal_Open.png");

            // 1. West Door (Door_MessHall) - Vertical 1x2 Door centered at (-10.5, -4.0)
            var doorMess = GameObject.Find("Door_MessHall");
            if (doorMess != null)
            {
                Undo.RecordObject(doorMess.transform, "Align Door_MessHall");
                doorMess.transform.position = new Vector3(-10.5f, -4.0f, 0f);
                doorMess.transform.rotation = Quaternion.identity;
                doorMess.transform.localScale = Vector3.one;

                var dc = doorMess.GetComponent<TheLastSignal.Entities.Environment.DoorController>();
                if (dc != null)
                {
                    Undo.RecordObject(dc, "Configure Door_MessHall Sprites");
                    dc.SetSprites(doorVClosed, doorVOpen);
                    dc.SetColors(Color.white, new Color(0.2f, 0.8f, 0.4f, 0.35f));
                    EditorUtility.SetDirty(dc);
                }

                var sr = doorMess.GetComponent<SpriteRenderer>();
                if (sr != null)
                {
                    Undo.RecordObject(sr, "Configure Door_MessHall SpriteRenderer");
                    sr.color = Color.white;
                    if (doorVClosed != null) sr.sprite = doorVClosed;
                    sr.sortingLayerName = "Obstacles";
                    sr.sortingOrder = 5;
                    EditorUtility.SetDirty(sr);
                }

                var col = doorMess.GetComponent<BoxCollider2D>();
                if (col != null)
                {
                    Undo.RecordObject(col, "Configure Door_MessHall Collider");
                    col.size = new Vector2(1.0f, 2.0f);
                    col.offset = Vector2.zero;
                    EditorUtility.SetDirty(col);
                }
            }

            // 2. East Door (Door_VentShaft) - Vertical 1x2 Door centered at (11.5, -4.0)
            var doorVent = GameObject.Find("Door_VentShaft");
            if (doorVent != null)
            {
                Undo.RecordObject(doorVent.transform, "Align Door_VentShaft");
                doorVent.transform.position = new Vector3(11.5f, -4.0f, 0f);
                doorVent.transform.rotation = Quaternion.identity;
                doorVent.transform.localScale = Vector3.one;

                var dc = doorVent.GetComponent<TheLastSignal.Entities.Environment.DoorController>();
                if (dc != null)
                {
                    Undo.RecordObject(dc, "Configure Door_VentShaft Sprites");
                    dc.SetSprites(doorVClosed, doorVOpen);
                    dc.SetColors(Color.white, new Color(0.2f, 0.8f, 0.4f, 0.35f));
                    EditorUtility.SetDirty(dc);
                }

                var sr = doorVent.GetComponent<SpriteRenderer>();
                if (sr != null)
                {
                    Undo.RecordObject(sr, "Configure Door_VentShaft SpriteRenderer");
                    sr.color = Color.white;
                    if (doorVClosed != null) sr.sprite = doorVClosed;
                    sr.sortingLayerName = "Obstacles";
                    sr.sortingOrder = 5;
                    EditorUtility.SetDirty(sr);
                }

                var col = doorVent.GetComponent<BoxCollider2D>();
                if (col != null)
                {
                    Undo.RecordObject(col, "Configure Door_VentShaft Collider");
                    col.size = new Vector2(1.0f, 2.0f);
                    col.offset = Vector2.zero;
                    EditorUtility.SetDirty(col);
                }
            }

            // 3. South Door (Door_CargoHold_North) - Horizontal 2x1 Door centered at (-0.5, -13.0)
            var doorCargo = GameObject.Find("Door_CargoHold_North");
            if (doorCargo != null)
            {
                Undo.RecordObject(doorCargo.transform, "Align Door_CargoHold_North");
                doorCargo.transform.position = new Vector3(0.0f, -13.0f, 0f);
                doorCargo.transform.rotation = Quaternion.identity;
                doorCargo.transform.localScale = Vector3.one;

                var dc = doorCargo.GetComponent<TheLastSignal.Entities.Environment.DoorController>();
                if (dc != null)
                {
                    Undo.RecordObject(dc, "Configure Door_CargoHold_North Sprites");
                    dc.SetSprites(doorHClosed, doorHOpen);
                    dc.SetColors(Color.white, new Color(0.2f, 0.8f, 0.4f, 0.35f));
                    EditorUtility.SetDirty(dc);
                }

                var sr = doorCargo.GetComponent<SpriteRenderer>();
                if (sr != null)
                {
                    Undo.RecordObject(sr, "Configure Door_CargoHold_North SpriteRenderer");
                    sr.color = Color.white;
                    if (doorHClosed != null) sr.sprite = doorHClosed;
                    sr.sortingLayerName = "Obstacles";
                    sr.sortingOrder = 5;
                    EditorUtility.SetDirty(sr);
                }

                var col = doorCargo.GetComponent<BoxCollider2D>();
                if (col != null)
                {
                    Undo.RecordObject(col, "Configure Door_CargoHold_North Collider");
                    col.size = new Vector2(2.0f, 1.0f);
                    col.offset = Vector2.zero;
                    EditorUtility.SetDirty(col);
                }
            }
        }

        public static void SynchronizePeripheralLighting()
        {
            // 1. Mess Hall (West Route - Tripped/Open Circuit on CRT!)
            // Fluorescents powered down to emergency low-voltage standby
            var ceilWest = GameObject.Find("Light_MH_Ceiling_West")?.GetComponent<Light2D>();
            if (ceilWest != null) { ceilWest.intensity = 0.05f; ceilWest.pointLightOuterRadius = 2.0f; EditorUtility.SetDirty(ceilWest); }
            var ceilEast = GameObject.Find("Light_MH_Ceiling_East")?.GetComponent<Light2D>();
            if (ceilEast != null) { ceilEast.intensity = 0.05f; ceilEast.pointLightOuterRadius = 2.0f; EditorUtility.SetDirty(ceilEast); }
            var kitchenTask = GameObject.Find("Light_MH_Kitchen_TaskLight")?.GetComponent<Light2D>();
            if (kitchenTask != null) { kitchenTask.intensity = 0.30f; kitchenTask.pointLightOuterRadius = 2.2f; EditorUtility.SetDirty(kitchenTask); }
            var kitchenAmber = GameObject.Find("Light_MH_Kitchen_Amber")?.GetComponent<Light2D>();
            if (kitchenAmber != null) { kitchenAmber.intensity = 0.40f; kitchenAmber.pointLightOuterRadius = 2.5f; EditorUtility.SetDirty(kitchenAmber); }
            var diningFill = GameObject.Find("Light_MH_Dining_Fill")?.GetComponent<Light2D>();
            if (diningFill != null) { diningFill.intensity = 0.22f; diningFill.pointLightOuterRadius = 4.5f; diningFill.color = new Color(0.9f, 0.55f, 0.25f, 1f); EditorUtility.SetDirty(diningFill); }

            // 2. Cargo Hold (South Route - Breaker 3 Catastrophic Fault!)
            var cargoBayN = GameObject.Find("Light_CargoHold_Bay_North")?.GetComponent<Light2D>();
            if (cargoBayN != null) { cargoBayN.intensity = 0.25f; cargoBayN.color = new Color(0.95f, 0.50f, 0.15f, 1f); EditorUtility.SetDirty(cargoBayN); }
            var cargoBayS = GameObject.Find("Light_CargoHold_Bay_South")?.GetComponent<Light2D>();
            if (cargoBayS != null) { cargoBayS.intensity = 0.25f; cargoBayS.color = new Color(0.95f, 0.50f, 0.15f, 1f); EditorUtility.SetDirty(cargoBayS); }

            // 3. Vent Shaft (East Route - Auxiliary Disconnected)
            var fanSpot = GameObject.Find("VP_Light_Fan_Spot")?.GetComponent<Light2D>();
            if (fanSpot != null) { fanSpot.intensity = 0.55f; EditorUtility.SetDirty(fanSpot); }
            var consoleTeal = GameObject.Find("VP_Light_Console_Teal")?.GetComponent<Light2D>();
            if (consoleTeal != null) { consoleTeal.intensity = 0.45f; EditorUtility.SetDirty(consoleTeal); }
        }
    }
}
#endif
