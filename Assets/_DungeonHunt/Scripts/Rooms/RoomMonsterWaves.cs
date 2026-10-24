using System.Collections.Generic;
using UnityEngine;

// 방 프리팹에서 직접 정하는 몬스터 구성. 방 루트에 붙이고 웨이브가 하나라도 있으면
// 그 방은 층 프리셋(FloorContent) 추첨 대신 이 구성을 씀. 전투룸, EliteGate 공통 (엘리트는 2웨이브)
public class RoomMonsterWaves : MonoBehaviour
{
    [Tooltip("웨이브 순서대로 등장. 엔트리마다 스폰점 번호(RoomPoint MonsterSpawn의 S번호), 몬스터, 처치 재화")]
    public List<MonsterWavePreset.Wave> Waves = new List<MonsterWavePreset.Wave>();

    public bool HasWaves => Waves.Exists(w => w != null && w.Entries.Count > 0);

    // 런타임용 프리셋 (방 이름으로 로그에 나옴)
    public MonsterWavePreset CreatePreset()
    {
        var preset = ScriptableObject.CreateInstance<MonsterWavePreset>();
        preset.name = name + " (방 지정)";
        foreach (MonsterWavePreset.Wave wave in Waves)
            if (wave != null && wave.Entries.Count > 0) preset.Waves.Add(wave);
        return preset;
    }

    // 스폰점 라벨용: 이 스폰점에 나오는 몬스터를 웨이브마다 한 줄 ("1W Test_Brawler")
    public string LabelFor(int spawnIndex)
    {
        var parts = new List<string>();
        for (int w = 0; w < Waves.Count; w++)
        {
            if (Waves[w] == null) continue;
            foreach (MonsterWavePreset.SpawnEntry entry in Waves[w].Entries)
                if (entry.SpawnPoint == spawnIndex)
                    parts.Add($"{w + 1}W {(entry.Monster != null ? entry.Monster.name : "(비어 있음)")}");
        }
        return string.Join("\n", parts);
    }
}
