using System;
using System.Collections.Generic;
using UnityEngine;

public enum BossPatternKind
{
    RadialBurst,   // 월드축 방사형
    AimedFan,      // 플레이어 조준 부채꼴
    Spiral,        // 회전 방사
}

[Serializable]
public class BossPatternData
{
    public BossPatternKind Kind;
    public float Interval = 2.5f;
    public float TelegraphDuration = 0.8f;
    public int ProjectileCount = 12;
    public float SpreadAngle = 40f;
    public EnemyProjectileSpec Projectile = new EnemyProjectileSpec(3f, 0.12f, 9f, 1);
}

// 05 최종보스 데이터. 패턴은 기획 미정 → 비워두면 접촉 피해만 있는 상태로 FSM과 페이즈 전환만 동작.
// UsePlaceholderPatterns는 테스트용 더미 패턴 (기획 확정 아님).
[CreateAssetMenu(fileName = "FinalBoss", menuName = "DungeonHunt/Boss Definition")]
public class BossDefinition : ScriptableObject
{
    public string DisplayName = "최종보스";
    public GameObject Prefab;
    public Sprite Sprite;
    public Color PlaceholderColor = new Color(0.55f, 0.1f, 0.15f);
    [Tooltip("0이면 GameBalance.Boss.HP (더미 1200)")]
    public int HP;
    public List<BossPatternData> Phase1Patterns = new List<BossPatternData>();
    public List<BossPatternData> Phase2Patterns = new List<BossPatternData>();
    [Tooltip("테스트 전용: 패턴 목록이 비었을 때 더미 패턴 사용")]
    public bool UsePlaceholderPatterns;
}
