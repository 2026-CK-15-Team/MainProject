using System.Collections.Generic;
using UnityEngine;

public enum CombatRoomState
{
    Unvisited,
    Locked,        // LockedWave1 / LockedWave2 포함
    WaveTransition,
    Cleared,
}

// 03-1 전투룸 + 03-6 EliteGateRoom (2웨이브, 희귀 이상 아티팩트, FloorExitAvailable).
// - 최초 RoomEntered에 문 잠금 + 첫 웨이브도 이후 웨이브처럼 예고 1초 → 소환 이펙트 + 접촉 피해 유예
// - 웨이브 전멸 → (히트스톱 끝난 뒤) 예고 1초 → 다음 웨이브 + 접촉 피해 유예
// - 마지막 웨이브 전멸 + 플레이어 생존 → 처치 피드백, 잠금 해제, 클리어 표시, 보상 생성 순서
[DefaultExecutionOrder(100)]
public class CombatRoomController : RoomContent
{
    public CombatRoomState State { get; private set; } = CombatRoomState.Unvisited;
    public MonsterWavePreset Preset { get; set; }
    public bool IsEliteGate { get; set; }
    public bool IsLarge { get; set; }

    public override bool IsLocked => State == CombatRoomState.Locked || State == CombatRoomState.WaveTransition;

    private readonly List<Monster> aliveMonsters = new List<Monster>();
    private int waveIndex;
    private bool waveRegistered;
    private float waveStartAt;
    private readonly List<GameObject> warningMarkers = new List<GameObject>();

    private void OnEnable() => GameEvents.MonsterDied += OnMonsterDied;
    private void OnDisable() => GameEvents.MonsterDied -= OnMonsterDied;

    public override void OnPlayerEntered(bool first)
    {
        if (State != CombatRoomState.Unvisited) return; // Cleared는 재방문해도 유지
        if (Preset == null || Preset.Waves.Count == 0)
        {
            Debug.LogError($"{name}: 몬스터 구성 프리셋 없음. 전투 없이 클리어 처리", this);
            State = CombatRoomState.Cleared;
            return;
        }

        Runtime.SetDoorsLocked(true);
        if (IsEliteGate) GameEvents.Raise(GameEvents.FloorGateRoomLocked, Runtime);
        else GameEvents.Raise(GameEvents.CombatRoomLocked, Runtime);

        waveIndex = -1;
        BeginWaveTransition();
    }

    private void SpawnWave(int index)
    {
        aliveMonsters.Clear();
        waveRegistered = false;
        Dictionary<int, Vector2> points = Runtime.SpawnPoints();
        MonsterWavePreset.Wave wave = Preset.Waves[index];
        for (int entryIndex = 0; entryIndex < wave.Entries.Count; entryIndex++)
        {
            MonsterWavePreset.SpawnEntry entry = wave.Entries[entryIndex];
            if (entry.Monster == null) continue;
            Vector2 position = points.TryGetValue(entry.SpawnPoint, out Vector2 p) ? p : FallbackSpawn(entryIndex);
            int spawnOrder = (index + 1) * 1000 + (entryIndex + 1);
            Monster monster = Monster.Spawn(entry.Monster, position, Runtime, Runtime.FloorIndex, spawnOrder, entry.CurrencyDrop);
            aliveMonsters.Add(monster);
            GameServices.Feedback?.SpawnSummonEffect(position);
        }
        // 등록을 클리어 판정보다 먼저 끝냄 (03-1 §9 첫 적 등록 전 0마리)
        waveRegistered = true;
    }

    // 레이아웃 스폰점이 없을 때 개발용: 플레이어 안전 반경 밖, 룸 바닥 경계에서 2칸 이상 안쪽 칸
    private Vector2 FallbackSpawn(int entryIndex)
    {
        RoomNavGrid nav = Runtime.Nav;
        Rect interior = Runtime.InteriorRect;
        PlayerController player = GameServices.Player;
        float safe = GameBalance.Current.CombatRoom.FirstEntrySafeRadius;
        var candidates = new List<Vector2>();
        foreach (Vector2Int cell in nav.WalkableCells())
        {
            Vector2 p = nav.CellCenter(cell);
            if (p.x < interior.xMin + 2f || p.x > interior.xMax - 2f || p.y < interior.yMin + 2f || p.y > interior.yMax - 2f) continue;
            if (player != null && Vector2.Distance(p, player.Position) < safe) continue;
            candidates.Add(p);
        }
        if (candidates.Count == 0) return Runtime.SafeCenter();
        return candidates[(entryIndex * 7 + 3) % candidates.Count];
    }

    private void OnMonsterDied(Monster monster)
    {
        aliveMonsters.Remove(monster);
    }

    private void LateUpdate()
    {
        if (State == CombatRoomState.Locked && waveRegistered)
        {
            aliveMonsters.RemoveAll(m => m == null || m.IsDead);
            if (aliveMonsters.Count > 0) return;
            // 같은 처리 틱의 플레이어 사망이 우선 (01-3 §7.2)
            PlayerController player = GameServices.Player;
            if (player == null || player.Health.IsDead) return;
            // 처치 히트스톱이 끝난 뒤 진행 (03-1 §8.3 2번)
            if (GameClock.IsHitstop) return;

            if (waveIndex + 1 < Preset.Waves.Count) BeginWaveTransition();
            else Clear();
        }
        else if (State == CombatRoomState.WaveTransition)
        {
            if (GameClock.CombatTime >= waveStartAt)
            {
                foreach (GameObject marker in warningMarkers) if (marker != null) Destroy(marker);
                warningMarkers.Clear();
                waveIndex++;
                State = CombatRoomState.Locked;
                SpawnWave(waveIndex);
                if (IsEliteGate && waveIndex > 0) GameEvents.Raise(GameEvents.EliteGateWaveAdvanced, Runtime);
            }
        }
    }

    // 문은 잠긴 채 WaveTransitionWarningDuration 동안 등장 예고. 예고 중 적 없음, 플레이어 조작 자유
    private void BeginWaveTransition()
    {
        State = CombatRoomState.WaveTransition;
        waveStartAt = GameClock.CombatTime + GameBalance.Current.CombatRoom.WaveTransitionWarningDuration;
        Dictionary<int, Vector2> points = Runtime.SpawnPoints();
        MonsterWavePreset.Wave next = Preset.Waves[waveIndex + 1];
        for (int i = 0; i < next.Entries.Count; i++)
        {
            Vector2 at = points.TryGetValue(next.Entries[i].SpawnPoint, out Vector2 p) ? p : FallbackSpawn(i);
            warningMarkers.Add(GameServices.Feedback != null ? GameServices.Feedback.SpawnWaveWarning(at) : null);
        }
        if (waveIndex >= 0) GameServices.UI?.ShowCenterToast("다음 웨이브", 0.8f);
    }

    private void Clear()
    {
        State = CombatRoomState.Cleared;
        Runtime.SetDoorsLocked(false);
        GameServices.UI?.ShowCenterToast("클리어", 1.0f);

        if (IsEliteGate)
        {
            GameEvents.Raise(GameEvents.FloorGateRoomCleared, Runtime);
            GameEvents.Raise(GameEvents.FloorExitAvailable);
            SpawnEliteReward();
        }
        else
        {
            GameEvents.Raise(GameEvents.CombatRoomCleared, Runtime);
            SpawnClearReward();
        }
    }

    // 보상 풀 1회 추첨: 아티팩트 / 런전용 재화 / HP 회복 (03-1 §7). 대형룸은 전용 강화 풀
    private void SpawnClearReward()
    {
        CombatRoomBalance b = GameBalance.Current.CombatRoom;
        float artifact = IsLarge ? b.LargeArtifactChance : b.RewardArtifactChance;
        float currency = IsLarge ? b.LargeCurrencyChance : b.RewardCurrencyChance;
        float hp = IsLarge ? b.LargeHPChance : b.RewardHPChance;
        Vector2 at = Runtime.GetPoint(RoomPointKind.RewardSpawn);
        double roll = RandomStreams.Loot.NextDouble() * (artifact + currency + hp);

        if (roll < artifact)
        {
            ArtifactDefinition def = GameServices.Artifacts?.DrawAndReserve(AcquisitionOrigin.CombatRoom);
            if (def != null) ArtifactPickup.Spawn(def, at, AcquisitionOrigin.CombatRoom, true, Runtime);
        }
        else if (roll < artifact + currency)
        {
            RewardPickup.Spawn(RewardKind.Currency, IsLarge ? b.LargeCurrencyAmount : b.RewardCurrencyAmount, at, Runtime);
        }
        else
        {
            RewardPickup.Spawn(RewardKind.Heal, IsLarge ? b.LargeHPAmount : b.RewardHPAmount, at, Runtime);
        }
    }

    // 희귀 이상 아티팩트 1개 확정 (03-6 §7 EliteGate_RewardCount / MinGrade)
    private void SpawnEliteReward()
    {
        Vector2 at = Runtime.GetPoint(RoomPointKind.RewardSpawn);
        ArtifactDefinition def = GameServices.Artifacts?.DrawAndReserve(AcquisitionOrigin.EliteGate);
        if (def != null)
        {
            ArtifactPickup.Spawn(def, at, AcquisitionOrigin.EliteGate, true, Runtime);
            GameServices.Feedback?.SpawnRewardBurst(at, ArtifactGradeUtility.Color(def.Grade));
        }
        GameEvents.Raise(GameEvents.EliteGateRewardSpawned, Runtime);
    }
}
