using UnityEditor;
using UnityEngine;

public class TakeScreenshot
{
    [MenuItem("Tools/Take Scene Screenshot")]
    public static void Capture()
    {
        ScreenCapture.CaptureScreenshot("Assets/Screenshots/CrewQuarters_V2_Result.png");
        Debug.Log("Screenshot saved to Assets/Screenshots/CrewQuarters_V2_Result.png");
    }
}
