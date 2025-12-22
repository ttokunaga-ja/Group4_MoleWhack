using System.IO;
using UnityEngine;

public class LogToFile : MonoBehaviour
{
    string logPath;

    void OnEnable()
    {
        // プロジェクト直下に PlaymodeLog.txt を作成
        logPath = Path.Combine(Application.dataPath, "../PlaymodeLog.txt");
        File.WriteAllText(logPath, "=== Playmode Log Start ===\n");
        Application.logMessageReceived += HandleLog;
    }

    void OnDisable()
    {
        Application.logMessageReceived -= HandleLog;
    }

    void HandleLog(string condition, string stackTrace, LogType type)
    {
        File.AppendAllText(logPath, $"{type}: {condition}\n");
    }
}
