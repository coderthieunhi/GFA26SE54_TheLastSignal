#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using TheLastSignal.Core;
using TheLastSignal.Core.EventBus;
using TheLastSignal.Entities;
using TheLastSignal.Entities.Player;
using TheLastSignal.UI;

namespace TheLastSignal.EditorTools
{
    /// <summary>
    /// Wires the prototype session loop into SampleScene: GameSession, 4 spawn slots, Exit zone, HUD.
    /// </summary>
    public static class GameSessionPrototypeSetup
    {
        private const string EventsDir = "Assets/_Project/Scripts/4_Data/Events";
        private const string PrefabDir = "Assets/_Project/Prefabs";
        private const string ScenePath = "Assets/Scenes/SampleScene.unity";
        private const string ObjectiveChannelPath = EventsDir + "/OnObjectiveChanged.asset";
        private const string SessionChannelPath = EventsDir + "/OnGameSessionStateChanged.asset";
        private const string PlayerPrefabPath = PrefabDir + "/Player.prefab";

        [MenuItem("Tools/Facility/Setup Game Session Prototype", false, 2)]
        public static void Setup()
        {
            if (!AssetDatabase.IsValidFolder("Assets/_Project/Scripts/4_Data"))
            {
                AssetDatabase.CreateFolder("Assets/_Project/Scripts", "4_Data");
            }
            if (!AssetDatabase.IsValidFolder(EventsDir))
            {
                AssetDatabase.CreateFolder("Assets/_Project/Scripts/4_Data", "Events");
            }
            if (!AssetDatabase.IsValidFolder(PrefabDir))
            {
                AssetDatabase.CreateFolder("Assets/_Project", "Prefabs");
            }

            ObjectiveDataEventChannelSO objectiveChannel = LoadOrCreate<ObjectiveDataEventChannelSO>(ObjectiveChannelPath);
            GameSessionStateEventChannelSO sessionChannel = LoadOrCreate<GameSessionStateEventChannelSO>(SessionChannelPath);

            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            GameObject player = GameObject.Find("Player");
            if (player == null)
            {
                Debug.LogError("[GameSessionPrototypeSetup] Player is missing from SampleScene.");
                return;
            }

            GameObject playerPrefab = PrefabUtility.SaveAsPrefabAsset(player, PlayerPrefabPath);

            GameObject sessionGo = GameObject.Find("GameSession");
            if (sessionGo == null)
            {
                sessionGo = new GameObject("GameSession");
                Undo.RegisterCreatedObjectUndo(sessionGo, "Create GameSession");
            }

            PlayerSpawner spawner = GetOrAddComponent<PlayerSpawner>(sessionGo);
            GameManager gameManager = GetOrAddComponent<GameManager>(sessionGo);

            SerializedObject spawnerSo = new SerializedObject(spawner);
            spawnerSo.FindProperty("playersToSpawn").intValue = 1;
            spawnerSo.FindProperty("playerPrefab").objectReferenceValue = playerPrefab.GetComponent<PlayerController>();
            spawnerSo.FindProperty("blockedLayers").intValue = LayerMask.GetMask("Obstacle");
            spawnerSo.FindProperty("spawnClearRadius").floatValue = 0.4f;
            spawnerSo.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject gmSo = new SerializedObject(gameManager);
            gmSo.FindProperty("startSessionOnPlay").boolValue = true;
            gmSo.FindProperty("playerSpawner").objectReferenceValue = spawner;
            gmSo.FindProperty("onGameSessionStateChanged").objectReferenceValue = sessionChannel;
            gmSo.FindProperty("onObjectiveChanged").objectReferenceValue = objectiveChannel;
            gmSo.ApplyModifiedPropertiesWithoutUndo();

            GameObject spawnArea = GameObject.Find("SpawnArea");
            if (spawnArea == null)
            {
                spawnArea = new GameObject("SpawnArea");
                Undo.RegisterCreatedObjectUndo(spawnArea, "Create SpawnArea");
            }

            spawnArea.transform.position = new Vector3(-0.5f, 3.5f, 0f);

            Vector2[] slots =
            {
                new Vector2(-1.5f, 4.5f),
                new Vector2(0.5f, 4.5f),
                new Vector2(-1.5f, 2.5f),
                new Vector2(0.5f, 2.5f)
            };

            for (int i = 0; i < slots.Length; i++)
            {
                string pointName = $"SpawnPoint_P{i + 1}";
                Transform child = spawnArea.transform.Find(pointName);
                GameObject pointGo = child != null ? child.gameObject : new GameObject(pointName);
                if (child == null)
                {
                    pointGo.transform.SetParent(spawnArea.transform, true);
                    Undo.RegisterCreatedObjectUndo(pointGo, "Create spawn point");
                }

                pointGo.transform.position = new Vector3(slots[i].x, slots[i].y, 0f);
                PlayerSpawnPoint point = GetOrAddComponent<PlayerSpawnPoint>(pointGo);
                point.Configure(i);
                EditorUtility.SetDirty(point);
            }

            GameObject exitGo = GameObject.Find("Exit_Zone");
            if (exitGo == null)
            {
                exitGo = new GameObject("Exit_Zone");
                Undo.RegisterCreatedObjectUndo(exitGo, "Create Exit_Zone");
            }

            exitGo.transform.position = new Vector3(-0.5f, 31.5f, 0f);
            BoxCollider2D box = GetOrAddComponent<BoxCollider2D>(exitGo);
            box.isTrigger = true;
            box.size = new Vector2(3f, 3f);
            GetOrAddComponent<ExitZone>(exitGo);

            SpriteRenderer sr = GetOrAddComponent<SpriteRenderer>(exitGo);
            SpriteRenderer playerSr = player.GetComponent<SpriteRenderer>();
            if (playerSr != null && playerSr.sprite != null)
            {
                sr.sprite = playerSr.sprite;
                sr.sortingLayerName = playerSr.sortingLayerName;
            }

            sr.color = new Color(0.25f, 1f, 0.4f, 0.85f);
            sr.sortingOrder = 5;

            GameObject hazardGo = GameObject.Find("Hazard_Zone");
            if (hazardGo == null)
            {
                hazardGo = new GameObject("Hazard_Zone");
                Undo.RegisterCreatedObjectUndo(hazardGo, "Create Hazard_Zone");
            }

            hazardGo.transform.position = new Vector3(-0.5f, -3f, 0f);
            BoxCollider2D hazardBox = GetOrAddComponent<BoxCollider2D>(hazardGo);
            hazardBox.isTrigger = true;
            hazardBox.size = new Vector2(2f, 2f);
            GetOrAddComponent<HazardZone>(hazardGo);

            SpriteRenderer hazardSr = GetOrAddComponent<SpriteRenderer>(hazardGo);
            if (playerSr != null && playerSr.sprite != null)
            {
                hazardSr.sprite = playerSr.sprite;
                hazardSr.sortingLayerName = playerSr.sortingLayerName;
            }

            hazardSr.color = new Color(1f, 0.25f, 0.25f, 0.85f);
            hazardSr.sortingOrder = 5;

            EnsureHud(objectiveChannel, sessionChannel);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[GameSessionPrototypeSetup] SampleScene wired: GameSession, SpawnArea P1-P4, Exit_Zone, Hazard_Zone, HUD.");
        }

        private static T GetOrAddComponent<T>(GameObject go) where T : Component
        {
            T comp = go.GetComponent<T>();
            if (comp == null)
            {
                comp = go.AddComponent<T>();
            }
            return comp;
        }

        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            T existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null)
            {
                return existing;
            }

            if (System.IO.File.Exists(path))
            {
                AssetDatabase.DeleteAsset(path);
            }

            T asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            return asset;
        }

        private static void EnsureHud(
            ObjectiveDataEventChannelSO objectiveChannel,
            GameSessionStateEventChannelSO sessionChannel)
        {
            GameObject canvasGo = GameObject.Find("HUD_Canvas");
            if (canvasGo == null)
            {
                canvasGo = new GameObject("HUD_Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                Undo.RegisterCreatedObjectUndo(canvasGo, "Create HUD_Canvas");
            }

            Canvas canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            TextMeshProUGUI objectiveText = FindOrCreateText(
                canvasGo.transform, "ObjectiveText", new Vector2(20f, -20f), TextAlignmentOptions.TopLeft);
            TextMeshProUGUI statusText = FindOrCreateText(
                canvasGo.transform, "SessionStatusText", new Vector2(0f, -80f), TextAlignmentOptions.Top);

            HUDManager hud = GetOrAddComponent<HUDManager>(canvasGo);
            SerializedObject so = new SerializedObject(hud);
            so.FindProperty("onObjectiveChanged").objectReferenceValue = objectiveChannel;
            so.FindProperty("onGameSessionStateChanged").objectReferenceValue = sessionChannel;
            so.FindProperty("objectiveText").objectReferenceValue = objectiveText;
            so.FindProperty("sessionStatusText").objectReferenceValue = statusText;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static TextMeshProUGUI FindOrCreateText(
            Transform parent, string name, Vector2 anchoredPos, TextAlignmentOptions align)
        {
            Transform existing = parent.Find(name);
            GameObject go = existing != null
                ? existing.gameObject
                : new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));

            if (existing == null)
            {
                go.transform.SetParent(parent, false);
            }

            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = new Vector2(-40f, 80f);

            TextMeshProUGUI tmp = go.GetComponent<TextMeshProUGUI>();
            tmp.fontSize = 36;
            tmp.alignment = align;
            tmp.color = Color.white;
            tmp.text = name == "ObjectiveText" ? "Objective: Reach the Exit" : string.Empty;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.raycastTarget = false;
            return tmp;
        }
    }
}
#endif
