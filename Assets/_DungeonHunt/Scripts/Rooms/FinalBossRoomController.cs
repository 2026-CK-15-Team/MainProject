using UnityEngine;

public enum BossRoomState
{
    Unvisited,
    Locked,
    Cleared,
}

// 03-6 FinalBossRoom: 첫 진입에 보스 조우 (05 §5.1). 처치 + 플레이어 생존 → Cleared, 문 열림. 보상 없음.
[DefaultExecutionOrder(100)]
public class FinalBossRoomController : RoomContent
{
    public BossRoomState State { get; private set; } = BossRoomState.Unvisited;
    public FinalBoss Boss { get; private set; }
    public override bool IsLocked => State == BossRoomState.Locked;

    private bool defeatPending;

    private void OnEnable() => GameEvents.BossDefeated += OnBossDefeated;
    private void OnDisable() => GameEvents.BossDefeated -= OnBossDefeated;

    public override void OnPlayerEntered(bool first)
    {
        if (State != BossRoomState.Unvisited) return;
        State = BossRoomState.Locked;
        GameServices.Run?.BeginBossEncounter(this);
    }

    // 페이드 아웃 중(검은 화면) 호출: 잠금 + 보스 Idle 배치 (05 §5.1 2번)
    public void LockAndPlaceBoss()
    {
        Runtime.SetDoorsLocked(true);
        GameEvents.Raise(GameEvents.FloorGateRoomLocked, Runtime);
        FloorContent content = GameServices.Config != null ? GameServices.Config.GetFloor(Runtime.FloorIndex) : null;
        BossDefinition def = content != null ? content.Boss : null;
        Boss = FinalBoss.Spawn(def, Runtime.SafeCenter(), Runtime);
    }

    private void OnBossDefeated() => defeatPending = true;

    // 플레이어 사망이 같은 틱이면 Cleared 안 함 (05 §8)
    private void LateUpdate()
    {
        if (!defeatPending || State != BossRoomState.Locked) return;
        PlayerController player = GameServices.Player;
        if (player == null || player.Health.IsDead) return;
        defeatPending = false;
        State = BossRoomState.Cleared;
        Runtime.SetDoorsLocked(false);
        GameEvents.Raise(GameEvents.FloorGateRoomCleared, Runtime);
    }
}
