using System;
using System.Collections.Generic;
using UnityEngine;

public enum BossPatternKind
{
    RadialBurst,   // 1 각인 방사: 월드축 방사를 A, B(반 칸 돌림) 번갈아 회차 발사
    AimedFan,      // 2 조준 부채꼴: 회차마다 방향 잠금, 2회차부터 선행 조준
    GapRing,       // 3 빈틈 탄벽: 링에서 연속 N발을 빼 빈틈 하나
    Splitter,      // 4 분열탄: 고정 방향 큰 탄(상쇄 불가) → 분열 거리에서 월드축 8방향
    Laser,         // 5 사선 레이저: 코어에서 즉발 줄 여러 개, 벽 무시
}

// 패턴 1개 (05 §7, §8.2). 종류마다 쓰는 칸만 의미 있음 (툴팁에 표시)
[Serializable]
public class BossPatternData
{
    public BossPatternKind Kind;
    [Tooltip("HP가 LowHPThreshold 미만일 때만 후보 (5번 사선 레이저)")]
    public bool LowHPOnly;
    [Tooltip("예고 시간 (초). 첫 회차 발사 전")]
    public float TelegraphDuration = 0.6f;
    [Tooltip("회차 수")]
    [Min(1)] public int Volleys = 1;
    [Tooltip("회차 간격 (초)")]
    public float VolleyInterval = 0.7f;
    [Tooltip("한 회차 발 수. 방사/링 = 한 바퀴 발 수, 부채꼴 = 부채꼴 발 수, 레이저 = 줄 수")]
    [Min(1)] public int Count = 16;
    [Tooltip("부채꼴 전체 각도 / 레이저 옆 줄 각도 (도)")]
    public float SpreadAngle = 28f;
    [Tooltip("탄속 (H/초)")]
    public float Speed = 2.8f;
    [Tooltip("투사체 반경 (H). 분열탄은 큰 탄 반경")]
    public float ProjectileRadius = 0.10f;

    [Header("조준 부채꼴")]
    [Tooltip("2회차부터 선행 조준 (비율은 조준 사격형 BurstLeadRatio)")]
    public bool LeadAfterFirstVolley = true;

    [Header("빈틈 탄벽")]
    [Tooltip("링에서 연속으로 빼는 발 수")]
    public int GapSize = 7;
    [Tooltip("두 번째 이후 빈틈이 앞 빈틈과 다른 각도 (최소, 최대). 한 발 간격 단위로 균등 추첨, 부호 균등")]
    public Vector2 NextGapOffset = new Vector2(64f, 96f);

    [Header("분열탄")]
    [Tooltip("큰 탄 발사 방향 (도, 남쪽 기준 좌우). 플레이어와 무관하게 고정")]
    public float[] FixedAngles = { 0f, -70f, 70f };
    [Tooltip("큰 탄이 분열하는 비행 거리 (H)")]
    public float SplitDistance = 3f;
    [Tooltip("분열 발 수 (월드축 균등)")]
    public int SplitCount = 8;
    [Tooltip("분열탄 탄속 (H/초)")]
    public float SplitSpeed = 2.6f;
    [Tooltip("분열탄 반경 (H)")]
    public float SplitRadius = 0.10f;

    [Header("사선 레이저")]
    [Tooltip("레이저 길이 (H)")]
    public float LaserLength = 22f;

    // 예고 포함 소요 시간 (마지막 발사까지)
    public float Duration => TelegraphDuration + VolleyInterval * Mathf.Max(0, Volleys - 1);

    public static List<BossPatternData> Defaults() => new List<BossPatternData>
    {
        new BossPatternData { Kind = BossPatternKind.RadialBurst, TelegraphDuration = 0.6f, Volleys = 3, VolleyInterval = 0.7f, Count = 16, Speed = 2.8f },
        new BossPatternData { Kind = BossPatternKind.AimedFan, TelegraphDuration = 0.5f, Volleys = 2, VolleyInterval = 0.6f, Count = 3, SpreadAngle = 28f, Speed = 4.5f },
        new BossPatternData { Kind = BossPatternKind.GapRing, TelegraphDuration = 0.9f, Volleys = 2, VolleyInterval = 1.2f, Count = 45, Speed = 3.2f },
        new BossPatternData { Kind = BossPatternKind.Splitter, TelegraphDuration = 0.6f, Volleys = 1, Count = 3, Speed = 3.0f, ProjectileRadius = 0.20f },
        new BossPatternData { Kind = BossPatternKind.Laser, LowHPOnly = true, TelegraphDuration = 1.4f, Volleys = 1, Count = 3, SpreadAngle = 25f },
    };
}

// 05 최종보스 데이터. 고정 위치 순탄막형, 단일 상태. 수치 공통값(HP, 반경, 쉬는 시간 등)은 GameBalance.Boss
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
    [Tooltip("0이면 GameBalance.Boss.HP")]
    public int HP;

    [Tooltip("패턴 목록. 번호는 목록 순서(1부터)")]
    public List<BossPatternData> Patterns = BossPatternData.Defaults();
    [Tooltip("처음 한 번 보여 줄 순서 (패턴 번호, 1부터). 끝나면 추첨")]
    public int[] FixedOrder = { 1, 2, 3, 4 };

    [Header("피해 기준 몬스터 (05 §7: 일반 몬스터 값과 같음). 비우면 아키타입 기본값")]
    [Tooltip("탄막 패턴형: 보스 투사체 피해, 몸체 접촉 피해")]
    public MonsterDefinition BarrageReference;
    [Tooltip("예고 후 저격형: 레이저 피해와 판정 폭, 방향 잠금 시간(AimLockLeadTime)")]
    public MonsterDefinition SniperReference;
    [Tooltip("조준 사격형: 선행 조준 비율(BurstLeadRatio)")]
    public MonsterDefinition ShooterReference;
}
