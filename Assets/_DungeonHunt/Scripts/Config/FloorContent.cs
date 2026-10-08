using System.Collections.Generic;
using UnityEngine;

// 층 하나의 콘텐츠 데이터 (03 §8.1, 03-6 §3, 05)
[CreateAssetMenu(fileName = "FloorContent", menuName = "DungeonHunt/Floor Content")]
public class FloorContent : ScriptableObject
{
    [Tooltip("층 도착 안내 둘째 줄 (10 §5.4). 비우면 GameBalance의 임시 이름")]
    public string DisplayName;

    [Header("전투룸 몬스터 구성 (03 §8.1). 1층 4종 / 2층 6종 / 3층 8종")]
    public List<MonsterWavePreset> CombatPresets = new List<MonsterWavePreset>();

    [Header("층 종료 전투 EliteGateRoom 프리셋 3종 (03-6 §3). 3층은 비움")]
    public List<MonsterWavePreset> ElitePresets = new List<MonsterWavePreset>();

    [Header("3층 최종보스 (05)")]
    public BossDefinition Boss;

    [Header("구성 프리셋이 없을 때 개발용 자동 구성에 쓸 몬스터 (경고 출력)")]
    public List<MonsterDefinition> FallbackMonsters = new List<MonsterDefinition>();
    public int FallbackMonsterCount = 4;

    [Header("가림 덮개 색 (흙/돌, 03 §6.4)")]
    public Color FogColor = new Color(0.16f, 0.13f, 0.11f, 1f);
}
