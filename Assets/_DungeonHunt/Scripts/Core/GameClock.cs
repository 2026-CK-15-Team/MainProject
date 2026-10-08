using System.Collections.Generic;
using UnityEngine;

// 전투 시뮬레이션 시계. 게임플레이 타이머는 Time.time 대신 이걸 씀.
// - 일시정지/전환/저장 실패 같은 정지 사유가 하나라도 있으면 멈춤 (Time.timeScale도 0)
// - 처치 히트스톱 중에는 전투 시간만 멈춤 (카메라 흔들림, UI, RunTime은 계속)
public static class GameClock
{
    private static readonly HashSet<string> pauseReasons = new HashSet<string>();
    private static float hitstopRemaining;

    // 전투 시뮬레이션 누적 시간 / 이번 프레임 진행량
    public static float CombatTime { get; private set; }
    public static float CombatDelta { get; private set; }

    public static bool IsPaused => pauseReasons.Count > 0;
    public static bool IsHitstop => hitstopRemaining > 0f;
    public static bool IsCombatRunning => !IsPaused && !IsHitstop;

    public static void Pause(string reason)
    {
        pauseReasons.Add(reason);
        Time.timeScale = 0f;
    }

    public static void Resume(string reason)
    {
        pauseReasons.Remove(reason);
        if (pauseReasons.Count == 0)
            Time.timeScale = 1f;
    }

    public static bool IsPausedBy(string reason) => pauseReasons.Contains(reason);

    // 진행 중인 히트스톱은 연장하지 않음 (09 §6)
    public static bool RequestHitstop(float duration)
    {
        if (IsHitstop || duration <= 0f) return false;
        hitstopRemaining = duration;
        return true;
    }

    public static void ResetAll()
    {
        pauseReasons.Clear();
        hitstopRemaining = 0f;
        CombatDelta = 0f;
        Time.timeScale = 1f;
    }

    // GameClockDriver가 매 프레임 가장 먼저 호출
    public static void Tick(float unscaledDelta)
    {
        unscaledDelta = Mathf.Min(unscaledDelta, 0.1f);
        if (IsPaused)
        {
            CombatDelta = 0f;
            return;
        }
        if (IsHitstop)
        {
            hitstopRemaining -= unscaledDelta;
            CombatDelta = 0f;
            return;
        }
        CombatDelta = unscaledDelta;
        CombatTime += unscaledDelta;
    }
}

// 정지 사유 이름 모음
public static class PauseReason
{
    public const string PauseMenu = "PauseMenu";
    public const string Transition = "Transition";
    public const string SaveFailure = "SaveFailure";
    public const string Focus = "Focus";
    public const string Ending = "Ending";
    public const string Death = "Death";
    public const string OutOfRun = "OutOfRun";
    public const string BossIntro = "BossIntro";
}
