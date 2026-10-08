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
    public float Speed;
    public float Radius;
    public float MaxDistance;
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
    public MonsterArchetype Archetype;
    public string DisplayName;

    [Header("외형 (비우면 기본 도형)")]
    public GameObject Prefab;
    public Sprite Sprite;
    public Color PlaceholderColor = Color.red;

    [Header("공통")]
    [Tooltip("층별 HP (1층, 2층, 3층). 04 §8 Monster_BaseHPByFloor")]
    public int[] HPByFloor = { 30, 42, 56 };
    public float MoveSpeed = 1.2f;
    public float BodyRadius = 0.35f;
    public int ContactDamage = 1;
    public float SeparationSpeed = 0.6f;
    public float RepathInterval = 0.25f;     // 개발 착수값. 0.5초 경로 갱신 기준 안
    public float AttackInterval = 1.5f;
    public float TelegraphDuration = 0.45f;
    public float AimLockLeadTime = 0.25f;

    [Header("거리 유지 (근접 유지형, 원거리형)")]
    public Vector2 KeepDistanceBand = new Vector2(0.55f, 0.8f);
    public float AttackMaxDistance = 1f;

    [Header("돌진형")]
    public float ChargerTriggerDistance = 3f;
    public float DashSpeed = 6f;
    public float DashDistance = 3f;
    public float PostDashRecovery = 0.8f;
    public int DashDamage = 1;

    [Header("근접 유지형")]
    public float BrawlerAttackRange = 1f;
    public float BrawlerAttackArc = 120f;
    public int BrawlerAttackDamage = 1;

    [Header("조준 사격형")]
    public float BurstGap = 0.15f;
    public float BurstLeadRatio = 0.5f;
    public EnemyProjectileSpec SingleProjectile = new EnemyProjectileSpec(5f, 0.1f, 6f, 1);
    public EnemyProjectileSpec BurstProjectile = new EnemyProjectileSpec(3.5f, 0.1f, 6f, 1);

    [Header("탄막 패턴형")]
    public int BarrageProjectileCount = 8;
    public EnemyProjectileSpec BarrageProjectile = new EnemyProjectileSpec(2.6f, 0.08f, 5.5f, 1);

    [Header("예고 후 저격형")]
    public float SniperHitscanWidth = 0.36f;
    public int SniperDamage = 2;

    [Header("원거리 스트레이프 (04 §9.3)")]
    public Vector2 StrafeFlipInterval = new Vector2(1f, 2f);
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
        ContactDamage = 1;
        SeparationSpeed = 0.6f;
        StrafeFlipInterval = new Vector2(1f, 2f);
        StrafeSightProbeDistance = 0.5f;
        switch (Archetype)
        {
            case MonsterArchetype.Charger:
                DisplayName = "돌진형";
                MoveSpeed = 1.4f; BodyRadius = 0.35f;
                AttackInterval = 2.4f; TelegraphDuration = 0.65f; AimLockLeadTime = 0.25f;
                ChargerTriggerDistance = 3f; DashSpeed = 6f; DashDistance = 3f; PostDashRecovery = 0.8f; DashDamage = 1;
                PlaceholderColor = new Color(0.85f, 0.35f, 0.25f);
                break;
            case MonsterArchetype.Brawler:
                DisplayName = "근접 유지형";
                MoveSpeed = 1.2f; BodyRadius = 0.35f;
                AttackInterval = 1.5f; TelegraphDuration = 0.45f; AimLockLeadTime = 0.25f;
                KeepDistanceBand = new Vector2(0.55f, 0.8f);
                BrawlerAttackRange = 1f; BrawlerAttackArc = 120f; BrawlerAttackDamage = 1;
                AttackMaxDistance = 1f;
                PlaceholderColor = new Color(0.6f, 0.25f, 0.7f);
                break;
            case MonsterArchetype.Shooter:
                DisplayName = "조준 사격형";
                MoveSpeed = 0.9f; BodyRadius = 0.3f;
                AttackInterval = 1.2f; TelegraphDuration = 0f; AimLockLeadTime = 0f;
                KeepDistanceBand = new Vector2(2.5f, 3.5f); AttackMaxDistance = 4.5f;
                BurstGap = 0.15f; BurstLeadRatio = 0.5f;
                SingleProjectile = new EnemyProjectileSpec(5f, 0.1f, 6f, 1);
                BurstProjectile = new EnemyProjectileSpec(3.5f, 0.1f, 6f, 1);
                PlaceholderColor = new Color(0.3f, 0.55f, 0.85f);
                break;
            case MonsterArchetype.Barrage:
                DisplayName = "탄막 패턴형";
                MoveSpeed = 0.8f; BodyRadius = 0.3f;
                AttackInterval = 2.4f; TelegraphDuration = 0.6f; AimLockLeadTime = 0f;
                KeepDistanceBand = new Vector2(3f, 4f); AttackMaxDistance = 5f;
                BarrageProjectileCount = 8;
                BarrageProjectile = new EnemyProjectileSpec(2.6f, 0.08f, 5.5f, 1);
                PlaceholderColor = new Color(0.3f, 0.75f, 0.6f);
                break;
            case MonsterArchetype.Sniper:
                DisplayName = "예고 후 저격형";
                MoveSpeed = 0.75f; BodyRadius = 0.3f;
                AttackInterval = 2.0f; TelegraphDuration = 1.2f; AimLockLeadTime = 0.25f;
                KeepDistanceBand = new Vector2(4f, 5f); AttackMaxDistance = 6f;
                SniperHitscanWidth = 0.36f; SniperDamage = 2;
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
