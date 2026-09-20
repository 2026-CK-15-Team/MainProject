using UnityEngine;

public static class RunStats
{
    public static float RunStartTime { get; private set; }
    public static int ClearedCombatRoomCount { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ResetOnPlay()
    {
        RunStartTime = 0f;
        ClearedCombatRoomCount = 0;
    }

    public static void StartRun()
    {
        RunStartTime = Time.time;
        ClearedCombatRoomCount = 0;
    }

    public static void RegisterCombatRoomCleared() => ClearedCombatRoomCount++;

    public static float GetRunTime() => Time.time - RunStartTime;
}