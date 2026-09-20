using System;
using System.Collections.Generic;

[Serializable]
public class PlaytestLogEntry
{
    public float timestamp;
    public string type;
    public string data;
}

[Serializable]
public class PlaytestLogData
{
    public string RunID;
    public string BuildVersion;
    public int RNGSeed;
    public string Result = "InProgress";
    public float RunTime;
    public List<PlaytestLogEntry> Events = new();
}
