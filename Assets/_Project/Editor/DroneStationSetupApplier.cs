using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.Rendering.Universal;
using TheLastSignal.Entities.Stations;
using TheLastSignal.Entities.Drone;
using TheLastSignal.UI;

namespace TheLastSignal.Editor
{
    [InitializeOnLoad]
    public static class DroneStationSetupApplier
    {
        private const string TargetScenePath = "Assets/Scenes/NetworkTestScene.unity";

        [InitializeOnLoadMethod]
        [MenuItem("TheLastSignal/Setup Drone Station & Operator", false, 100)]
        public static void ApplyDroneStationSetup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            var activeScene = EditorSceneManager.GetActiveScene();
            if (activeScene.path != TargetScenePath)
            {
                if (System.IO.File.Exists(TargetScenePath))
                {
                    activeScene = EditorSceneManager.OpenScene(TargetScenePath, OpenSceneMode.Single);
                }
                else
                {
                    return;
                }
            }

            bool dirty = false;

            // 1. Find Airlock_Prop_Radar_CRT
            GameObject radarCrt = GameObject.Find("Airlock_Prop_Radar_CRT");
            if (radarCrt != null)
            {
                DroneStation station = radarCrt.GetComponent<DroneStation>();
                if (station == null)
                {
                    station = radarCrt.AddComponent<DroneStation>();
                    dirty = true;
                }

                // Connect Light_Console_Radar
                GameObject lightRadar = GameObject.Find("Light_Console_Radar");
                if (lightRadar != null)
                {
                    Light2D l2d = lightRadar.GetComponent<Light2D>();
                    SerializedObject soStation = new SerializedObject(station);
                    SerializedProperty spLight = soStation.FindProperty("consoleLight");
                    if (spLight != null && spLight.objectReferenceValue != l2d)
                    {
                        spLight.objectReferenceValue = l2d;
                        soStation.ApplyModifiedProperties();
                        dirty = true;
                    }
                }
            }

            // 2. Ensure DroneHUDOverlay exists in scene
            DroneHUDOverlay hudOverlay = Object.FindAnyObjectByType<DroneHUDOverlay>();
            if (hudOverlay == null)
            {
                GameObject canvasObj = GameObject.Find("LobbyCanvas");
                GameObject overlayGo = new GameObject("DroneHUDOverlay", typeof(RectTransform));
                if (canvasObj != null)
                {
                    overlayGo.transform.SetParent(canvasObj.transform, false);
                    RectTransform rt = overlayGo.GetComponent<RectTransform>();
                    rt.anchorMin = Vector2.zero;
                    rt.anchorMax = Vector2.one;
                    rt.offsetMin = Vector2.zero;
                    rt.offsetMax = Vector2.zero;
                }
                hudOverlay = overlayGo.AddComponent<DroneHUDOverlay>();
                dirty = true;
            }

            // 3. Ensure DroneDockStation exists in scene
            DroneDockStation dock = Object.FindAnyObjectByType<DroneDockStation>();
            Vector3 dockPos = radarCrt != null ? radarCrt.transform.position + new Vector3(3.0f, -0.85f, 0f) : new Vector3(3.0f, 14.0f, 0f);
            if (dock == null)
            {
                GameObject dockGo = new GameObject("Prop_Drone_Dock_Station");
                dockGo.transform.position = dockPos;
                dockGo.layer = 9;
                if (radarCrt != null && radarCrt.transform.parent != null)
                {
                    dockGo.transform.SetParent(radarCrt.transform.parent, true);
                }
                dock = dockGo.AddComponent<DroneDockStation>();
                dirty = true;
            }
            else
            {
                if (dock.gameObject.layer != 9)
                {
                    dock.gameObject.layer = 9;
                    dirty = true;
                }

                if (Vector3.Distance(dock.transform.position, dockPos) > 0.01f)
                {
                    dock.transform.position = dockPos;
                    dirty = true;
                }
            }

            if (dock != null)
            {
                BoxCollider2D dockCol = dock.GetComponent<BoxCollider2D>();
                if (dockCol == null)
                {
                    dockCol = dock.gameObject.AddComponent<BoxCollider2D>();
                    dockCol.size = new Vector2(1.5f, 1.5f);
                    dockCol.isTrigger = false;
                    dirty = true;
                }
            }

            // 4. Ensure Recon Drone object exists in scene and is centered on dock
            DroneController drone = Object.FindAnyObjectByType<DroneController>();
            if (drone == null)
            {
                GameObject droneGo = new GameObject("ReconDrone_Spectre");
                droneGo.transform.position = dockPos;
                drone = droneGo.AddComponent<DroneController>();
                droneGo.AddComponent<DroneView>();
                dirty = true;
            }
            else
            {
                if (Vector3.Distance(drone.transform.position, dockPos) > 0.01f)
                {
                    drone.transform.position = dockPos;
                    dirty = true;
                }
            }

            if (radarCrt != null)
            {
                DroneStation station = radarCrt.GetComponent<DroneStation>();
                if (station != null)
                {
                    SerializedObject soStation = new SerializedObject(station);
                    SerializedProperty spDrone = soStation.FindProperty("droneController");
                    if (spDrone != null && spDrone.objectReferenceValue != drone)
                    {
                        spDrone.objectReferenceValue = drone;
                        dirty = true;
                    }

                    SerializedProperty spDock = soStation.FindProperty("dockStation");
                    if (spDock != null && spDock.objectReferenceValue != dock)
                    {
                        spDock.objectReferenceValue = dock;
                        dirty = true;
                    }

                    soStation.ApplyModifiedProperties();
                }
            }

            if (dirty)
            {
                EditorSceneManager.MarkSceneDirty(activeScene);
                EditorSceneManager.SaveScene(activeScene);
                Debug.Log("<color=green>[DroneStationSetupApplier]</color> Successfully configured Drone Station and Recon Drone in NetworkTestScene!");
            }
        }
    }
}
