using System;
using UnityEngine;

public enum MonsterArchetype
{
    Charger,    // 돌진형 (원래 수호자)
    Brawler,    // 근접 유지형 (외부 침입자)
    Shooter,    // 조준 사격형 (외부 침입자)
    Barrage,    // 탄막 패턴형 (외부 침입자)
    Sniper,     // 예고 후 저격형 (원래 수호자)
}

public enum MonsterFaction
{
    Invader,    // 외부 침입자
    Guardian,   // 원래 수호자
}

[Serializable]
public struct EnemyProjectileSpec
{
    [Tooltip("탄속 (H/초)")]
    public float Speed;
    [Tooltip("탄 판정 반경 (H)")]
    public float Radius;
    [Tooltip("최대 비행 거리 (H). 넘으면 소멸")]
    public float MaxDistance;
    [Tooltip("명중 피해")]
    public int Damage;

    public EnemyProjectileSpec(float speed, float radius, float maxDistance, int damage)
    {
        Speed = speed;
        Radius = radius;
        MaxDistance = maxDistance;
        Damage = damage;
    }
}

// 몬스터 1종 데이터 (04 §9.6). 일반 몬스터는 아키타입당 1종이고 수치는 04 §8 그대로.
// 새로 만들면 아키타입 기본값이 들어감. 우클릭 메뉴 "Reset To Archetype Defaults"로 다시 채울 수 있음.
[CreateAssetMenu(fileName = "Monster", menuName = "DungeonHunt/Monster Definition")]
public class MonsterDefinition : ScriptableObject
{
    [Tooltip("행동 방식. 바꾼 뒤 우클릭 → Reset To Archetype Defaults로 기본 수치 채움")]
    public MonsterArchetype Archetype;
    [Tooltip("표시 이름")]
    public string DisplayName;

    [Header("외형 (비우면 기본 도형)")]
    [Tooltip("외형 프리팹. 비우면 Sprite 또는 기본 도형 사용")]
    public GameObject Prefab;
    [Tooltip("외형 스프라이트. 비우면 PlaceholderColor 도형")]
    public Sprite Sprite;
    [Tooltip("프리팹, 스프라이트가 없을 때 기본 도형 색")]
    public Color PlaceholderColor = Color.red;

    [Header("공통")]
    [Tooltip("층별 HP (1층, 2층, 3층). 04 §8 Monster_BaseHPByFloor")]
    public int[] HPByFloor = { 30, 42, 56 };
    [Tooltip("이동속도 (H/초)")]
    public float MoveSpeed = 1.2f;
    [Tooltip("몸체 반경 (H). 피격, 이동, 접촉 판정에 씀")]
    public float BodyRadius = 0.35f;
    [Tooltip("플레이어와 몸이 닿았을 때 피해")]
    public int ContactDamage = 8;
    [Tooltip("몬스터끼리 겹쳤을 때 밀어내는 속도 (H/초)")]
    public float SeparationSpeed = 0.6f;
    [Tooltip("경로 재탐색 주기 (초). 개발 착수값 (기준안 0.5초)")]
    public float RepathInterval = 0.25f;
    [Tooltip("공격 시작 간격 (초). 예고 시간 이상이어야 함")]
    public float AttackInterval = 1.5f;
    [Tooltip("공격 예고 시간 (초). 이 시간이 끝나면 공격 실행")]
    public float TelegraphDuration = 0.45f;
    [Tooltip("공격 실행 이 시간 전부터 조준 방향 고정 (초). 0이면 고정 안 함. 예고 시간 이하")]
    public float AimLockLeadTime = 0.25f;

    [Header("거리 유지 (근접 유지형, 원거리형)")]
    [Tooltip("플레이어와 유지할 거리 (최소, 최대) H. 멀면 접근, 가까우면 후퇴, 사이면 옆걸음")]
    public Vector2 KeepDistanceBand = new Vector2(0.55f, 0.8f);
    [Tooltip("공격을 시작할 수 있는 최대 거리 (H)")]
    public float AttackMaxDistance = 1f;

    [Header("돌진형")]
    [Tooltip("플레이어가 이 거리 안에 오면 돌진 시작 (H)")]
    public float ChargerTriggerDistance = 3f;
    [Tooltip("돌진 속도 (H/초)")]
    public float DashSpeed = 6f;
    [Tooltip("돌진 거리 (H)")]
    public float DashDistance = 3f;
    [Tooltip("돌진 후 경직 시간 (초)")]
    public float PostDashRecovery = 0.8f;
    [Tooltip("돌진 명중 피해")]
    public int DashDamage = 8;

    [Header("근접 유지형")]
    [Tooltip("부채꼴 공격 반경 (H)")]
    public float BrawlerAttackRange = 1f;
    [Tooltip("부채꼴 공격 각도 (도)")]
    public float BrawlerAttackArc = 120f;
    [Tooltip("부채꼴 공격 피해")]
    public int BrawlerAttackDamage = 8;

    [Header("조준 사격형")]
    [Tooltip("연발 사이 간격 (초). 2 x 이 값 < 공격 간격이어야 함")]
    public float BurstGap = 0.15f;
    [Tooltip("연발탄 예측 사격 비율. 0 = 현재 위치, 1 = 이동 예측 위치 그대로")]
    public float BurstLeadRatio = 0.5f;
    [Tooltip("단발탄 수치")]
    public EnemyProjectileSpec SingleProjectile = new EnemyProjectileSpec(5f, 0.1f, 6f, 8);
    [Tooltip("연발탄 수치")]
    public EnemyProjectileSpec BurstProjectile = new EnemyProjectileSpec(3.5f, 0.1f, 6f, 8);

    [Header("탄막 패턴형")]
    [Tooltip("한 번에 원형으로 쏘는 탄 수")]
    public int BarrageProjectileCount = 8;
    [Tooltip("탄막 탄 수치")]
    public EnemyProjectileSpec BarrageProjectile = new EnemyProjectileSpec(2.6f, 0.08f, 5.5f, 8);

    [Header("예고 후 저격형")]
    [Tooltip("저격 판정 폭 (H)")]
    public float SniperHitscanWidth = 0.36f;
    [Tooltip("저격 피해")]
    public int SniperDamage = 16;

    [Header("원거리 스트레이프 (04 §9.3)")]
    [Tooltip("옆걸음 방향을 바꾸는 간격 범위 (최소, 최대) 초")]
    public Vector2 StrafeFlipInterval = new Vector2(1f, 2f);
    [Tooltip("옆걸음 전에 이 거리만큼 옆에서 시야가 확보되는지 확인 (H)")]
    public float StrafeSightProbeDistance = 0.5f;

    public MonsterFaction Faction => Archetype == MonsterArchetype.Charger || Archetype == MonsterArchetype.Sniper
        ? MonsterFaction.Guardian
        : MonsterFaction.Invader;

    public bool IsRanged => Archetype == MonsterArchetype.Shooter || Archetype == MonsterArchetype.Barrage || Archetype == MonsterArchetype.Sniper;
    public bool HasTelegraph => Archetype != MonsterArchetype.Shooter;

    public int GetHP(int floorIndex)
    {
        if (HPByFloor == null || HPByFloor.Length == 0) return 30;
        return HPByFloor[Mathf.Clamp(floorIndex, 0, HPByFloor.Length - 1)];
    }

    // 04 §10: 공격 데이터 조건을 어기면 사용 불가 (자동 보정 안 함)
    public bool Validate(out string error)
    {
        error = null;
        if (Archetype == MonsterArchetype.Shooter)
        {
            if (!(2f * BurstGap < AttackInterval)) error = "연발 데이터 조건 위반: 2 x BurstGap < AttackInterval";
        }
        else if (!(0f <= AimLockLeadTime && AimLockLeadTime <= TelegraphDuration && TelegraphDuration <= AttackInterval))
        {
            error = "공격 예고 데이터 조건 위반: 0 <= AimLockLeadTime <= TelegraphDuration <= AttackInterval";
        }
        if (Archetype == MonsterArchetype.Barrage && BarrageProjectileCount <= 0)
            error = "탄막 패턴 데이터 없음";
        return error == null;
    }

    [ContextMenu("Reset To Archetype Defaults")]
    public void ResetToArchetypeDefaults()
    {
        HPByFloor = new[] { 30, 42, 56 };
        ContactDamage = 8;
        SeparationSpeed = 0.6f;
        StrafeFlipInterval = new Vector2(1f, 2f);
        StrafeSightProbeDistance = 0.5f;
        switch (Archetype)
        {
            case MonsterArchetype.Charger:
                DisplayName = "돌진형";
                MoveSpeed = 1.4f; BodyRadius = 0.35f;
                AttackInterval = 2.4f; TelegraphDuration = 0.65f; AimLockLeadTime = 0.25f;
                ChargerTriggerDistance = 3f; DashSpeed = 6f; DashDistance = 3f; PostDashRecovery = 0.8f; DashDamage = 8;
                PlaceholderColor = new Color(0.85f, 0.35f, 0.25f);
                break;
            case MonsterArchetype.Brawler:
                DisplayName = "근접 유지형";
                MoveSpeed = 1.2f; BodyRadius = 0.35f;
                AttackInterval = 1.5f; TelegraphDuration = 0.45f; AimLockLeadTime = 0.25f;
                KeepDistanceBand = new Vector2(0.55f, 0.8f);
                BrawlerAttackRange = 1f; BrawlerAttackArc = 120f; BrawlerAttackDamage = 8;
                AttackMaxDistance = 1f;
                PlaceholderColor = new Color(0.6f, 0.25f, 0.7f);
                break;
            case MonsterArchetype.Shooter:
                DisplayName = "조준 사격형";
                MoveSpeed = 0.9f; BodyRadius = 0.3f;
                AttackInterval = 1.2f; TelegraphDuration = 0f; AimLockLeadTime = 0f;
                KeepDistanceBand = new Vector2(2.5f, 3.5f); AttackMaxDistance = 4.5f;
                BurstGap = 0.15f; BurstLeadRatio = 0.5f;
                SingleProjectile = new EnemyProjectileSpec(5f, 0.1f, 6f, 8);
                BurstProjectile = new EnemyProjectileSpec(3.5f, 0.1f, 6f, 8);
                PlaceholderColor = new Color(0.3f, 0.55f, 0.85f);
                break;
            case MonsterArchetype.Barrage:
                DisplayName = "탄막 패턴형";
                MoveSpeed = 0.8f; BodyRadius = 0.3f;
                AttackInterval = 2.4f; TelegraphDuration = 0.6f; AimLockLeadTime = 0f;
                KeepDistanceBand = new Vector2(3f, 4f); AttackMaxDistance = 5f;
                BarrageProjectileCount = 8;
                BarrageProjectile = new EnemyProjectileSpec(2.6f, 0.08f, 5.5f, 8);
                PlaceholderColor = new Color(0.3f, 0.75f, 0.6f);
                break;
            case MonsterArchetype.Sniper:
                DisplayName = "예고 후 저격형";
                MoveSpeed = 0.75f; BodyRadius = 0.3f;
                AttackInterval = 2.0f; TelegraphDuration = 1.2f; AimLockLeadTime = 0.25f;
                KeepDistanceBand = new Vector2(4f, 5f); AttackMaxDistance = 6f;
                SniperHitscanWidth = 0.36f; SniperDamage = 16;
                PlaceholderColor = new Color(0.9f, 0.8f, 0.3f);
                break;
        }
    }

    private void Reset() => ResetToArchetypeDefaults();

    public static MonsterDefinition CreateDefault(MonsterArchetype archetype)
    {
        var def = CreateInstance<MonsterDefinition>();
        def.Archetype = archetype;
        def.ResetToArchetypeDefaults();
        def.name = archetype.ToString();
        return def;
    }
}
