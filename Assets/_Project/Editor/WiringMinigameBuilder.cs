#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TheLastSignal.UI.Tasks;

namespace TheLastSignal.Editor
{
    public static class WiringMinigameBuilder
    {
        [MenuItem("TheLastSignal/Tasks/Create Wiring Minigame UI (Recreate)", false, 100)]
        [MenuItem("GameObject/UI/The Last Signal - Wiring Minigame", false, 20)]
        public static void CreateWiringMinigameUI()
        {
            Canvas canvas = EnsureCanvas();

            WireTaskUI existing = Object.FindAnyObjectByType<WireTaskUI>(FindObjectsInactive.Include);
            if (existing != null)
            {
                Undo.DestroyObjectImmediate(existing.gameObject);
                Debug.Log("<color=yellow>[WiringMinigameBuilder]</color> Removed old Wiring Minigame UI to upgrade.");
            }

            WireTaskUI wireUI = WireTaskUI.CreateWiringUIHierarchy(canvas);

            Selection.activeGameObject = wireUI.gameObject;
            Undo.RegisterCreatedObjectUndo(wireUI.gameObject, "Create Upgraded Wiring Minigame UI");
            Debug.Log("<color=green>[WiringMinigameBuilder]</color> Upgraded Wiring Minigame UI successfully created under Canvas!");
        }

        [MenuItem("TheLastSignal/Tasks/Create Valve Balancing Minigame UI (Recreate)", false, 101)]
        [MenuItem("GameObject/UI/The Last Signal - Valve Minigame", false, 21)]
        public static void CreateValveMinigameUI()
        {
            Canvas canvas = EnsureCanvas();

            ValveTaskUI existing = Object.FindAnyObjectByType<ValveTaskUI>(FindObjectsInactive.Include);
            if (existing != null)
            {
                Undo.DestroyObjectImmediate(existing.gameObject);
                Debug.Log("<color=yellow>[WiringMinigameBuilder]</color> Removed old Valve Minigame UI to upgrade.");
            }

            ValveTaskUI valveUI = ValveTaskUI.CreateValveUIHierarchy(canvas);

            Selection.activeGameObject = valveUI.gameObject;
            Undo.RegisterCreatedObjectUndo(valveUI.gameObject, "Create Upgraded Valve Minigame UI");
            Debug.Log("<color=green>[WiringMinigameBuilder]</color> Upgraded Valve Minigame UI successfully created under Canvas!");
        }

        private static Canvas EnsureCanvas()
        {
            Canvas canvas = Object.FindAnyObjectByType<Canvas>();
            if (canvas == null)
            {
                GameObject canvasGo = new GameObject("HUD_Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                canvas = canvasGo.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;

                CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.matchWidthOrHeight = 0.5f;

                if (Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
                {
                    new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem), typeof(UnityEngine.EventSystems.StandaloneInputModule));
                }
            }
            return canvas;
        }
    }
}
#endif
