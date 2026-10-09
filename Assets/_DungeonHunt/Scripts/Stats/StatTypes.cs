using System;
using UnityEngine;

// 01-4 스탯 ID. 무기 스탯은 무기별 ID + 전체 무기(All) ID가 같은 단계에서 합쳐짐.
public enum StatId
{
    MoveSpeed,
    DodgeChargeTime,
    DodgeMaxCharge,
    MaxHP,
    CritChance,
    CritDamage,
    SwapReadyDelay,

    WeaponDamageAll,
    ReloadTimeAll,
    MagazineSizeAll,
    FireIntervalAll,
    RangeAll,

    PistolDamage,
    PistolReloadTime,
    PistolMagazine,
    PistolFireInterval,
    PistolRange,
    PistolProjectileSpeed,

    ShotgunDamage,
    ShotgunReloadTime,
    ShotgunMagazine,
    ShotgunFireInterval,
    ShotgunRange,
    ShotgunConeAngle,

    RifleDamage,
    RifleReloadTime,
    RifleMagazine,
    RifleFireInterval,
    RifleRange,
    RifleProjectileSpeed,
}

public enum WeaponStat
{
    Damage,
    ReloadTime,
    Magazine,
    FireInterval,
    Range,
    ProjectileSpeed,
    ConeAngle,
}

// 고정값 / 비율 수정값 / 상태별 배율 (01-4 §5)
// 합성은 PlayerStats.Compose: (기본 + Flat 합) x (1 + Percent 합) x Multiplier 곱.
// Percent끼리는 더하고 Multiplier끼리는 곱함 → +10% 두 개 = +20%, 0.5배 두 개 = 0.25배.
public enum ModifierKind
{
    Flat,
    Percent,
    Multiplier,
}

[Serializable]
public struct StatModifier
{
    [Tooltip("대상 스탯. 무기 스탯은 무기별 ID 또는 전체 무기(All) ID")]
    public StatId Stat;
    [Tooltip("Flat: 기본값에 더함 / Percent: 비율끼리 합산 / Multiplier: 배율끼리 곱함")]
    public ModifierKind Kind;
    [Tooltip("Flat: 더할 값 / Percent: 0.1 = +10% / Multiplier: 0.75 = 0.75배")]
    public float Value;

    public StatModifier(StatId stat, ModifierKind kind, float value)
    {
        Stat = stat;
        Kind = kind;
        Value = value;
    }

    public override string ToString()
    {
        string name = StatNames.Display(Stat);
        return Kind switch
        {
            ModifierKind.Flat => $"{name} {Value:+0.##;-0.##}",
            ModifierKind.Percent => $"{name} {Value * 100f:+0.#;-0.#}%",
            _ => $"{name} x{Value:0.##}",
        };
    }
}

public static class StatNames
{
    public static string Display(StatId stat) => stat switch
    {
        StatId.MoveSpeed => "이동속도",
        StatId.DodgeChargeTime => "회피 충전 시간",
        StatId.DodgeMaxCharge => "회피 최대 충전",
        StatId.MaxHP => "최대 HP",
        StatId.CritChance => "치명타 확률",
        StatId.CritDamage => "치명타 피해",
        StatId.SwapReadyDelay => "무기 교체 지연",
        StatId.WeaponDamageAll => "무기 피해",
        StatId.ReloadTimeAll => "재장전 시간",
        StatId.MagazineSizeAll => "탄창",
        StatId.FireIntervalAll => "발사 간격",
        StatId.RangeAll => "사거리",
        StatId.PistolDamage => "권총 피해",
        StatId.PistolReloadTime => "권총 재장전 시간",
        StatId.PistolMagazine => "권총 탄창",
        StatId.PistolFireInterval => "권총 발사 간격",
        StatId.PistolRange => "권총 사거리",
        StatId.PistolProjectileSpeed => "권총 탄속",
        StatId.ShotgunDamage => "샷건 피해",
        StatId.ShotgunReloadTime => "샷건 재장전 시간",
        StatId.ShotgunMagazine => "샷건 탄창",
        StatId.ShotgunFireInterval => "샷건 발사 간격",
        StatId.ShotgunRange => "샷건 사거리",
        StatId.ShotgunConeAngle => "샷건 원추 각도",
        StatId.RifleDamage => "라이플 피해",
        StatId.RifleReloadTime => "라이플 재장전 시간",
        StatId.RifleMagazine => "라이플 탄창",
        StatId.RifleFireInterval => "라이플 발사 간격",
        StatId.RifleRange => "라이플 사거리",
        StatId.RifleProjectileSpeed => "라이플 탄속",
        _ => stat.ToString(),
    };

    // 무기 + 스탯 종류 → (무기별 ID, 전체 무기 ID 또는 null)
    public static (StatId specific, StatId? all) ForWeapon(WeaponType weapon, WeaponStat stat)
    {
        StatId? all = stat switch
        {
            WeaponStat.Damage => StatId.WeaponDamageAll,
            WeaponStat.ReloadTime => StatId.ReloadTimeAll,
            WeaponStat.Magazine => StatId.MagazineSizeAll,
            WeaponStat.FireInterval => StatId.FireIntervalAll,
            WeaponStat.Range => StatId.RangeAll,
            _ => null,
        };
        StatId specific = (weapon, stat) switch
        {
            (WeaponType.Pistol, WeaponStat.Damage) => StatId.PistolDamage,
            (WeaponType.Pistol, WeaponStat.ReloadTime) => StatId.PistolReloadTime,
            (WeaponType.Pistol, WeaponStat.Magazine) => StatId.PistolMagazine,
            (WeaponType.Pistol, WeaponStat.FireInterval) => StatId.PistolFireInterval,
            (WeaponType.Pistol, WeaponStat.Range) => StatId.PistolRange,
            (WeaponType.Pistol, _) => StatId.PistolProjectileSpeed,
            (WeaponType.Shotgun, WeaponStat.Damage) => StatId.ShotgunDamage,
            (WeaponType.Shotgun, WeaponStat.ReloadTime) => StatId.ShotgunReloadTime,
            (WeaponType.Shotgun, WeaponStat.Magazine) => StatId.ShotgunMagazine,
            (WeaponType.Shotgun, WeaponStat.FireInterval) => StatId.ShotgunFireInterval,
            (WeaponType.Shotgun, WeaponStat.Range) => StatId.ShotgunRange,
            (WeaponType.Shotgun, _) => StatId.ShotgunConeAngle,
            (WeaponType.Rifle, WeaponStat.Damage) => StatId.RifleDamage,
            (WeaponType.Rifle, WeaponStat.ReloadTime) => StatId.RifleReloadTime,
            (WeaponType.Rifle, WeaponStat.Magazine) => StatId.RifleMagazine,
            (WeaponType.Rifle, WeaponStat.FireInterval) => StatId.RifleFireInterval,
            (WeaponType.Rifle, WeaponStat.Range) => StatId.RifleRange,
            _ => StatId.RifleProjectileSpeed,
        };
        return (specific, all);
    }
}
