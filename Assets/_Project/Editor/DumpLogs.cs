using UnityEditor;
using UnityEngine;
using System.IO;
using System.Reflection;
using System;

public class DumpLogs : EditorWindow
{
    [MenuItem("Tools/Dump Console Logs")]
    public static void Dump()
    {
        string logFile = "console_dump.txt";
        var assembly = Assembly.GetAssembly(typeof(EditorWindow));
        var type = assembly.GetType("UnityEditor.LogEntries");
        var startMethod = type.GetMethod("StartGettingEntries");
        var endMethod = type.GetMethod("EndGettingEntries");
        var getCountMethod = type.GetMethod("GetCount");
        var getEntryInternalMethod = type.GetMethod("GetEntryInternal");

        var logEntryType = assembly.GetType("UnityEditor.LogEntry");
        var logEntry = Activator.CreateInstance(logEntryType);
        var messageField = logEntryType.GetField("message", BindingFlags.Instance | BindingFlags.Public);
        var conditionField = logEntryType.GetField("condition", BindingFlags.Instance | BindingFlags.Public);

        try
        {
            startMethod.Invoke(null, null);
            int count = (int)getCountMethod.Invoke(null, null);
            
            using (StreamWriter writer = new StreamWriter(logFile))
            {
                for (int i = 0; i < count; i++)
                {
                    getEntryInternalMethod.Invoke(null, new object[] { i, logEntry });
                    string msg = (string)messageField.GetValue(logEntry);
                    string cond = (string)conditionField.GetValue(logEntry);
                    writer.WriteLine(cond ?? msg ?? "");
                }
            }
            Debug.Log("Dumped " + count + " logs to " + logFile);
        }
        finally
        {
            endMethod.Invoke(null, null);
        }
    }
}
