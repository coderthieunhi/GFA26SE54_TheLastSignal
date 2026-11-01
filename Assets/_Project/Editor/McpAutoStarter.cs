using UnityEditor;

namespace TheLastSignal.Editor
{
    [InitializeOnLoad]
    public static class McpAutoStarter
    {
        static McpAutoStarter()
        {
            EditorPrefs.SetBool("MCPForUnity.AutoStartOnLoad", true);
        }
    }
}
