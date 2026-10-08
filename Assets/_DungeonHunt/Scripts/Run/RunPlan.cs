using System.Collections.Generic;
using UnityEngine;

// 런 계획 (03 §6.1): 런 시작 시 콘텐츠 시드로 한 번 확정하는 층별 선택.
// 맵 구조는 생성기(RunPlanner)가, 콘텐츠(EliteGate 프리셋, 비밀방 콘텐츠, 전투룸 구성)는 여기서 정함.
public class RunPlan
{
    public int RunSeed { get; }
    public int[] ElitePresetIndex { get; }

    public RunPlan(int runSeed, GameConfig config)
    {
        RunSeed = runSeed;
        int floors = Mathf.Max(1, config != null ? config.FloorCount : 1);
        ElitePresetIndex = new int[floors];
        System.Random rng = RandomStreams.CreateContentRandom(0xE1);
        for (int f = 0; f < floors; f++)
        {
            FloorContent content = config != null ? config.GetFloor(f) : null;
            int count = content != null ? content.ElitePresets.Count : 0;
            // 같은 층 3종 각 1/3, 이전 런 결과 제외 없음 (03-6 §7)
            ElitePresetIndex[f] = count > 0 ? rng.Next(count) : -1;
        }
    }

    public MonsterWavePreset ElitePreset(GameConfig config, int floor)
    {
        FloorContent content = config.GetFloor(floor);
        int index = floor < ElitePresetIndex.Length ? ElitePresetIndex[floor] : -1;
        if (content == null || index < 0 || index >= content.ElitePresets.Count) return null;
        return content.ElitePresets[index];
    }

    // 비밀방 콘텐츠: 런 시드 + 층 + 방 순서로 결정 → 비밀 벽을 부술 때 다시 뽑지 않음 (03-5 §4 1번)
    public SecretContent SecretContentFor(int floor, int nodeId)
    {
        SecretRoomBalance b = GameBalance.Current.SecretRoom;
        System.Random rng = RandomStreams.CreateContentRandom(0x5EC0000 + floor * 1000 + nodeId);
        float gamble = b.SecondUIFallback ? 0f : b.GambleWeight;
        float total = b.CurrencyWeight + b.HPWeight + b.ArtifactWeight + gamble;
        double roll = rng.NextDouble() * total;
        if (roll < b.CurrencyWeight) return SecretContent.Currency;
        roll -= b.CurrencyWeight;
        if (roll < b.HPWeight) return SecretContent.Heal;
        roll -= b.HPWeight;
        if (roll < b.ArtifactWeight) return SecretContent.Artifact;
        return SecretContent.Gamble;
    }

    // 전투룸 구성 배정 (03 §8.1)
    // - 그 룸의 스폰점으로 성립하는 프리셋만 (허용 목록 검증)
    // - 아직 안 쓴 구성 먼저, 바로 연결된 전투룸과 같은 구성 금지(다른 후보가 있으면)
    public static void AssignCombatPresets(int floor, IReadOnlyList<RoomRuntime> rooms, FloorContent content)
    {
        System.Random rng = RandomStreams.CreateContentRandom(0xC0B0 + floor);
        var used = new Dictionary<MonsterWavePreset, int>();
        var assigned = new Dictionary<RoomRuntime, MonsterWavePreset>();
        List<MonsterWavePreset> presets = content != null ? content.CombatPresets : new List<MonsterWavePreset>();

        foreach (RoomRuntime room in rooms)
        {
            if (room.Role != RoomRole.Combat || !(room.Content is CombatRoomController combat)) continue;
            HashSet<int> spawnIndices = new HashSet<int>(room.SpawnPoints().Keys);
            var candidates = new List<MonsterWavePreset>();
            foreach (MonsterWavePreset preset in presets)
            {
                if (preset == null) continue;
                bool ok = preset.IsCompatible(spawnIndices.Count > 0 ? spawnIndices : null, out string error);
                if (!ok)
                {
                    Debug.LogWarning($"{room.name}: 프리셋 {preset.name} 제외 - {error}");
                    continue;
                }
                // 1층 구성에 원래 수호자가 있으면 허용 목록에서 제외 (03 §8.1)
                if (floor == 0 && preset.ContainsGuardian())
                {
                    Debug.LogWarning($"1층 프리셋 {preset.name}에 원래 수호자 포함 → 제외");
                    continue;
                }
                candidates.Add(preset);
            }

            var neighborPresets = new HashSet<MonsterWavePreset>();
            foreach (RoomRuntime n in room.Neighbors)
                if (assigned.TryGetValue(n, out MonsterWavePreset p)) neighborPresets.Add(p);

            List<MonsterWavePreset> pool = Filter(candidates, c => !neighborPresets.Contains(c));
            if (pool.Count == 0) pool = candidates;
            int minUse = int.MaxValue;
            foreach (MonsterWavePreset c in pool) minUse = Mathf.Min(minUse, used.TryGetValue(c, out int u) ? u : 0);
            List<MonsterWavePreset> leastUsed = Filter(pool, c => (used.TryGetValue(c, out int u) ? u : 0) == minUse);

            MonsterWavePreset chosen = leastUsed.Count > 0 ? leastUsed[rng.Next(leastUsed.Count)] : null;
            if (chosen == null) chosen = CreateFallbackPreset(floor, content, room);
            combat.Preset = chosen;
            assigned[room] = chosen;
            used[chosen] = (used.TryGetValue(chosen, out int count) ? count : 0) + 1;
        }
    }

    private static List<T> Filter<T>(List<T> list, System.Predicate<T> predicate)
    {
        var result = new List<T>();
        foreach (T item in list) if (predicate(item)) result.Add(item);
        return result;
    }

    // 프리셋이 없을 때 개발용 자동 구성. 기획 데이터 아님 (경고 출력)
    public static MonsterWavePreset CreateFallbackPreset(int floor, FloorContent content, RoomRuntime room)
    {
        Debug.LogWarning($"{room.name}: 쓸 수 있는 몬스터 구성 프리셋 없음 → 개발용 자동 구성 사용 (03 §8.1 콘텐츠 데이터 미정)");
        var preset = ScriptableObject.CreateInstance<MonsterWavePreset>();
        preset.name = "AutoPreset";
        var wave = new MonsterWavePreset.Wave();
        var pool = new List<MonsterDefinition>();
        if (content != null) pool.AddRange(content.FallbackMonsters);
        if (pool.Count == 0)
        {
            pool.Add(MonsterDefinition.CreateDefault(MonsterArchetype.Brawler));
            pool.Add(MonsterDefinition.CreateDefault(MonsterArchetype.Shooter));
            pool.Add(MonsterDefinition.CreateDefault(MonsterArchetype.Barrage));
            if (floor > 0)
            {
                pool.Add(MonsterDefinition.CreateDefault(MonsterArchetype.Charger));
                pool.Add(MonsterDefinition.CreateDefault(MonsterArchetype.Sniper));
            }
        }
        int count = content != null ? Mathf.Max(1, content.FallbackMonsterCount) : 4 + floor;
        System.Random rng = RandomStreams.CreateContentRandom(0xFA11 + floor * 100 + room.Node.Id);
        for (int i = 0; i < count; i++)
            wave.Entries.Add(new MonsterWavePreset.SpawnEntry { SpawnPoint = 1000 + i, Monster = pool[rng.Next(pool.Count)], CurrencyDrop = GameBalance.Current.CombatRoom.MonsterCurrencyDropDefault });
        preset.Waves.Add(wave);
        return preset;
    }
}
