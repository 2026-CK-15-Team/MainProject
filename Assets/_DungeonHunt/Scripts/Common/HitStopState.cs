using UnityEngine;

public static class HitStopState
{
    public static float KillHitstopDuration = 0.04f;

    private static float endRealTime = float.NegativeInfinity;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ResetOnPlay() => endRealTime = float.NegativeInfinity;

    public static bool IsActive => Time.unscaledTime < endRealTime;

    public static void Trigger(float duration)
    {
        if (IsActive)
        {
            //Debug.Log($"[HitStopState] 재발동 무시됨 (이미 진행 중, 남은시간={endRealTime - Time.unscaledTime:F3})");
            return;
        }

        endRealTime = Time.unscaledTime + duration;
        //Debug.Log($"[HitStopState] 발동: duration={duration}, now={Time.unscaledTime:F3}, endRealTime={endRealTime:F3}");
    }
}