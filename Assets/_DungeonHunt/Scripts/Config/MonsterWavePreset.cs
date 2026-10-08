using System;
using System.Collections.Generic;
using UnityEngine;

// 몬스터 구성 프리셋 (03-1 §8.2). 웨이브별 스폰 엔트리 목록.
// WaveIndex / SpawnEntryIndex는 목록 순서(1부터)로 정해짐 → 중복이 생길 수 없음.
// CombatSpawnOrder = (WaveIndex, SpawnEntryIndex) 오름차순.
[CreateAssetMenu(fileName = "MonsterPreset", menuName = "DungeonHunt/Monster Wave Preset")]
public class MonsterWavePreset : ScriptableObject
{
    [Serializable]
    public class SpawnEntry
    {
        [Tooltip("룸 레이아웃의 MonsterSpawn 지점 번호")]
        public int SpawnPoint = 1;
        public MonsterDefinition Monster;
        [Tooltip("처치 재화 값. 1 이상 (03-1 §8.2)")]
        [Min(1)] public int CurrencyDrop = 1;
    }

    [Serializable]
    public class Wave
    {
        public List<SpawnEntry> Entries = new List<SpawnEntry>();
    }

    public List<Wave> Waves = new List<Wave>();

    public int MonsterCount
    {
        get
        {
            int count = 0;
            foreach (Wave wave in Waves) count += wave.Entries.Count;
            return count;
        }
    }

    // 03 §8.1 허용 목록 검증 (스폰점 번호, 사용 불가 몬스터, 재화 값)
    public bool IsCompatible(ICollection<int> spawnIndices, out string error)
    {
        error = null;
        if (Waves.Count == 0) { error = "웨이브 없음"; return false; }
        foreach (Wave wave in Waves)
        {
            foreach (SpawnEntry entry in wave.Entries)
            {
                if (entry.Monster == null) { error = "몬스터 비어 있음"; return false; }
                if (!entry.Monster.Validate(out string monsterError)) { error = $"사용 불가 몬스터 {entry.Monster.name}: {monsterError}"; return false; }
                if (entry.CurrencyDrop < 1) { error = "재화 값 1 미만"; return false; }
                if (spawnIndices != null && !spawnIndices.Contains(entry.SpawnPoint)) { error = $"스폰점 {entry.SpawnPoint} 없음"; return false; }
            }
        }
        return true;
    }

    public bool ContainsGuardian()
    {
        foreach (Wave wave in Waves)
            foreach (SpawnEntry entry in wave.Entries)
                if (entry.Monster != null && entry.Monster.Faction == MonsterFaction.Guardian) return true;
        return false;
    }
}
