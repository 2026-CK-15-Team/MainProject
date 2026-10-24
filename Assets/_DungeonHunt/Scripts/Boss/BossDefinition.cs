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
    [Tooltip("패턴 종류. 방사형(월드축 고정) / 조준 부채꼴 / 회전 방사(쏠 때마다 반 칸씩 회전)")]
    public BossPatternKind Kind;
    [Tooltip("이 패턴 예고 시작부터 다음 패턴 예고 시작까지 (초). 예고 시간보다 길게")]
    public float Interval = 2.5f;
    [Tooltip("예고 시간 (초). 끝나면 발사")]
    public float TelegraphDuration = 0.8f;
    [Tooltip("한 번에 쏘는 탄 수. 예고선은 최대 24개까지만 그림")]
    public int ProjectileCount = 12;
    [Tooltip("조준 부채꼴 전체 각도 (도). AimedFan에서만 씀")]
    public float SpreadAngle = 40f;
    [Tooltip("탄 수치")]
    public EnemyProjectileSpec Projectile = new EnemyProjectileSpec(3f, 0.12f, 9f, 8);
}

// 05 최종보스 데이터. 패턴은 기획 미정 → 비워두면 접촉 피해만 있는 상태로 FSM과 페이즈 전환만 동작.
// UsePlaceholderPatterns는 테스트용 더미 패턴 (기획 확정 아님).
[CreateAssetMenu(fileName = "FinalBoss", menuName = "DungeonHunt/Boss Definition")]
public class BossDefinition : ScriptableObject
{
    [Tooltip("표시 이름")]
    public string DisplayName = "최종보스";
    [Tooltip("외형 프리팹. 비우면 Sprite 또는 기본 도형 사용")]
    public GameObject Prefab;
    [Tooltip("외형 스프라이트. 비우면 PlaceholderColor 도형")]
    public Sprite Sprite;
    [Tooltip("프리팹, 스프라이트가 없을 때 기본 도형 색")]
    public Color PlaceholderColor = new Color(0.55f, 0.1f, 0.15f);
    [Tooltip("0이면 GameBalance.Boss.HP (더미 1200)")]
    public int HP;
    [Tooltip("1페이즈 패턴. 목록 순서대로 반복")]
    public List<BossPatternData> Phase1Patterns = new List<BossPatternData>();
    [Tooltip("2페이즈 패턴. 전환 시 처음부터 목록 순서대로 반복")]
    public List<BossPatternData> Phase2Patterns = new List<BossPatternData>();
    [Tooltip("테스트 전용: 패턴 목록이 비었을 때 더미 패턴 사용")]
    public bool UsePlaceholderPatterns;
}
