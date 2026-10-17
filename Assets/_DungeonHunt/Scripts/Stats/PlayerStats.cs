using System.Collections.Generic;
using UnityEngine;

// 01-4 플레이어 스탯. 최종값의 단일 소유자. 다른 시스템은 수정값을 원천(source) 단위로 등록/제거만 함.
// 합성 순서: (기본 + 고정값 합) x (1 + 비율 합) → [이동속도 하한] → x 상태 배율 곱 → 상하한 → 반올림
// 같은 원천 이름으로 다시 등록하면 덮어씀 → 장착/해제 순서가 결과를 바꾸지 않음.
public class PlayerStats : MonoBehaviour
{
    private readonly Dictionary<string, List<StatModifier>> sources = new Dictionary<string, List<StatModifier>>();
    private readonly Dictionary<WeaponType, WeaponScriptable> weaponBases = new Dictionary<WeaponType, WeaponScriptable>();
    private readonly Dictionary<StatId, float> lastValues = new Dictionary<StatId, float>();

    private PlayerBalance Balance => GameBalance.Current.Player;

    public void RegisterWeaponBase(WeaponScriptable definition)
    {
        if (definition != null)
            weaponBases[definition.WeaponType] = definition;
    }

    public void SetSource(string source, IEnumerable<StatModifier> modifiers)
    {
        var list = new List<StatModifier>(modifiers);
        if (list.Count == 0)
        {
            RemoveSource(source);
            return;
        }
        sources[source] = list;
        NotifyChanges();
    }

    public void SetSource(string source, StatModifier modifier) => SetSource(source, new[] { modifier });

    public void RemoveSource(string source)
    {
        if (sources.Remove(source))
            NotifyChanges();
    }

    public bool HasSource(string source) => sources.ContainsKey(source);

    // 런 한정 수정값 전부 제거 (RunStarted / RunCleared / RunOver). 영구 강화(Permanent:)만 남김.
    public void ClearRunSources()
    {
        var toRemove = new List<string>();
        foreach (string key in sources.Keys)
        {
            if (!key.StartsWith(StatSources.PermanentPrefix))
                toRemove.Add(key);
        }
        foreach (string key in toRemove)
            sources.Remove(key);
        NotifyChanges();
    }

    public float Get(StatId stat) => Compose(stat, BaseValue(stat), null);

    public int GetInt(StatId stat) => RoundHalfUp(Get(stat));

    public float GetWeapon(WeaponType weapon, WeaponStat kind)
    {
        (StatId specific, StatId? all) = StatNames.ForWeapon(weapon, kind);
        float baseValue = WeaponBase(weapon, kind);
        return Compose(specific, baseValue, all);
    }

    public int GetWeaponInt(WeaponType weapon, WeaponStat kind) => RoundHalfUp(GetWeapon(weapon, kind));

    // 피해량 계산용: 치명타 전 피해(반올림 전). 01-2 §8.3
    public float GetWeaponDamageRaw(WeaponType weapon) => GetWeapon(weapon, WeaponStat.Damage);

    public static int RoundHalfUp(float value) => Mathf.FloorToInt(value + 0.5f);

    private float Compose(StatId stat, float baseValue, StatId? alsoInclude)
    {
        float flat = 0f, percent = 0f, multiplier = 1f;
        foreach (List<StatModifier> list in sources.Values)
        {
            foreach (StatModifier modifier in list)
            {
                if (modifier.Stat != stat && (!alsoInclude.HasValue || modifier.Stat != alsoInclude.Value)) continue;
                switch (modifier.Kind)
                {
                    case ModifierKind.Flat: flat += modifier.Value; break;
                    case ModifierKind.Percent: percent += modifier.Value; break;
                    case ModifierKind.Multiplier: multiplier *= modifier.Value; break;
                }
            }
        }

        float value = (baseValue + flat) * (1f + percent);
        if (stat == StatId.MoveSpeed)
            value = Mathf.Max(value, Balance.MoveSpeedFloor);
        value *= multiplier;
        return Clamp(stat, value);
    }

    private float Clamp(StatId stat, float value)
    {
        switch (stat)
        {
            case StatId.MoveSpeed: return Mathf.Max(0f, value);
            case StatId.CritChance: return Mathf.Clamp01(value);
            case StatId.CritDamage: return Mathf.Max(1f, value);
            case StatId.MaxHP: return Mathf.Max(1f, value);
            case StatId.DodgeMaxCharge: return Mathf.Max(0f, value);
            case StatId.DodgeChargeTime: return Mathf.Max(0.05f, value);
            case StatId.SwapReadyDelay: return Mathf.Max(0f, value);
            case StatId.JustDodgeAmmoRefund: return Mathf.Clamp01(value);
            case StatId.PistolFireInterval:
            case StatId.ShotgunFireInterval:
            case StatId.RifleFireInterval:
                return Mathf.Max(GameBalance.Current.Weapon.FireIntervalFloor, value);
            case StatId.PistolMagazine:
            case StatId.ShotgunMagazine:
            case StatId.RifleMagazine:
                return Mathf.Max(1f, value);
            case StatId.PistolReloadTime:
            case StatId.ShotgunReloadTime:
            case StatId.RifleReloadTime:
                return Mathf.Max(0.05f, value);
            default: return Mathf.Max(0f, value);
        }
    }

    private float BaseValue(StatId stat) => stat switch
    {
        StatId.MoveSpeed => Balance.MoveMaxSpeed,
        StatId.DodgeChargeTime => Balance.DodgeChargeTime,
        StatId.DodgeMaxCharge => Balance.DodgeMaxCharge,
        StatId.MaxHP => Balance.MaxHP,
        StatId.CritChance => Balance.CritChance,
        StatId.CritDamage => Balance.CritDamageMultiplier,
        StatId.SwapReadyDelay => 0f, // 무기 정의값을 쓰므로 여기선 가산치만 다룸
        StatId.JustDodgeWindow => Balance.JustDodgeWindow,
        StatId.JustDodgeAmmoRefund => GameBalance.Current.Weapon.JustDodgeAmmoRefundRatio,
        StatId.HitInvincibleDuration => Balance.HitInvincibilityDuration,
        _ => 0f,
    };

    private float WeaponBase(WeaponType weapon, WeaponStat kind)
    {
        if (!weaponBases.TryGetValue(weapon, out WeaponScriptable def)) return 0f;
        return kind switch
        {
            WeaponStat.Damage => def.Damage,
            WeaponStat.ReloadTime => def.ReloadTime,
            WeaponStat.Magazine => def.MaxAmmo,
            WeaponStat.FireInterval => def.FireInterval,
            WeaponStat.Range => def.Range,
            WeaponStat.ProjectileSpeed => def.ProjectileSpeed,
            WeaponStat.ConeAngle => def.ConeAngle,
            _ => 0f,
        };
    }

    // 실제로 바뀐 스탯만 EffectiveStatChanged로 알림
    private void NotifyChanges()
    {
        foreach (StatId stat in System.Enum.GetValues(typeof(StatId)))
        {
            float value = Get(stat);
            if (lastValues.TryGetValue(stat, out float previous) && Mathf.Approximately(previous, value)) continue;
            lastValues[stat] = value;
            GameEvents.EffectiveStatChanged?.Invoke(stat, previous, value);
        }
    }
}

public static class StatSources
{
    public const string PermanentPrefix = "Permanent:";
    public const string PermanentCrit = PermanentPrefix + "Crit";
    public const string PistolPassive = "Passive:Pistol";
    public const string RifleFiring = "State:RifleFiring";
    public const string ShotgunKick = "State:ShotgunKick";
    public static string Artifact(string id) => "Artifact:" + id;
    public static string Set(string id) => "Set:" + id;
}
