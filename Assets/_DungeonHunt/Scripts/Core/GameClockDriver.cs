using UnityEngine;

// 씬에 하나. 실행 순서 맨 앞에서 시계를 돌림.
// 씬에 없으면 자동으로 하나 만듦 (GameBootstrap 없는 테스트 씬에서도 게임플레이 시간이 흐르게)
[DefaultExecutionOrder(-1000)]
public class GameClockDriver : MonoBehaviour
{
    private static GameClockDriver instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureExists()
    {
        if (instance != null || FindFirstObjectByType<GameClockDriver>() != null) return;
        var go = new GameObject("GameClockDriver");
        go.AddComponent<GameClockDriver>();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(this);
            return;
        }
        instance = this;
        GameClock.ResetAll();
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    private void Update() => GameClock.Tick(Time.unscaledDeltaTime);
}
