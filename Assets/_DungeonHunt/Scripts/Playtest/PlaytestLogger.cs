using System;
using System.IO;
using UnityEngine;

public static class PlaytestLogger
{
    private static PlaytestLogData data;
    private static string filePath;
    private static string folderPath;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ResetOnPlay()
    {
        data = null;
        filePath = null;
        folderPath = null;
    }

    public static void StartRun()
    {
        folderPath = Path.Combine(Application.persistentDataPath, "PlaytestLogs");
        Directory.CreateDirectory(folderPath);

        MarkStaleRunsAsAborted();

        int seed = Environment.TickCount;
        UnityEngine.Random.InitState(seed);

        data = new PlaytestLogData
        {
            RunID = Guid.NewGuid().ToString(),
            BuildVersion = Application.version,
            RNGSeed = seed
        };

        filePath = Path.Combine(folderPath, $"{data.RunID}.json");
    }

    public static void Log(string type, string info)
    {
        if (data == null) return;
        data.Events.Add(new PlaytestLogEntry { timestamp = RunStats.GetRunTime(), type = type, data = info });
    }

    public static void SaveProgress()
    {
        if (!TryWrite())
            FindObjectOfTypeSafe<PlaytestLogFailureToast>()?.Show();
    }

    public static void FinalizeRun(string result)
    {
        if (data == null) return;

        data.Result = result;
        data.RunTime = RunStats.GetRunTime();

        if (!TryWrite())
            FindObjectOfTypeSafe<PlaytestLogFailureToast>()?.Show();
    }

    public static void RetryFinalSave()
    {
        if (!TryWrite())
            FindObjectOfTypeSafe<PlaytestLogFailureToast>()?.Show();
    }

    private static bool TryWrite()
    {
        if (data == null || filePath == null) return false;

        try
        {
            string json = JsonUtility.ToJson(data, true);
            File.WriteAllText(filePath, json);
            WriteEditorConvenienceCopy(json);
            return true;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[PlaytestLogger] 저장 실패: {e.Message}");
            return false;
        }
    }

    private static void WriteEditorConvenienceCopy(string json)
    {
#if UNITY_EDITOR
        string editorFolder = Path.Combine(Application.dataPath, "PlaytestLogs");
        Directory.CreateDirectory(editorFolder);
        string editorPath = Path.Combine(editorFolder, $"{data.RunID}.json");
        File.WriteAllText(editorPath, json);
        UnityEditor.AssetDatabase.Refresh();
#endif
    }

    private static void MarkStaleRunsAsAborted()
    {
        if (!Directory.Exists(folderPath)) return;

        foreach (var path in Directory.GetFiles(folderPath, "*.json"))
        {
            try
            {
                string json = File.ReadAllText(path);
                var stale = JsonUtility.FromJson<PlaytestLogData>(json);

                if (stale != null && stale.Result == "InProgress")
                {
                    stale.Result = "Aborted";
                    File.WriteAllText(path, JsonUtility.ToJson(stale, true));
                }
            }
            catch
            {
            }
        }
    }

    private static T FindObjectOfTypeSafe<T>() where T : UnityEngine.Object =>
        UnityEngine.Object.FindObjectOfType<T>();
}