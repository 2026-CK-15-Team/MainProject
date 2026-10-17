using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// 아티팩트 카드 문구 (와이어프레임 공용 카드 부품): 옵션 행(실효값 병기), 경고 행, 세트 진행, 교체 시 세트 변화
public static class ArtifactUIText
{
    public static readonly Color Good = new Color(0.45f, 0.9f, 0.5f);
    public static readonly Color Bad = new Color(1f, 0.45f, 0.4f);
    public static readonly Color Muted = new Color(0.62f, 0.62f, 0.68f);

    public static string Hex(Color c) => ColorUtility.ToHtmlStringRGB(c);

    public static string WeaponName(WeaponType type) => type switch
    {
        WeaponType.Pistol => "권총",
        WeaponType.Shotgun => "샷건",
        _ => "라이플",
    };

    // 옵션 행: 스탯 수정값마다 "권총 재장전 시간 -20% (1.05 → 0.84초)", 특수 효과는 문구만.
    // equipped = 이미 장착한 것 (빼면 얼마 → 지금), 아니면 지금 → 장착하면 얼마
    public static List<string> OptionRows(ArtifactDefinition def, bool equipped)
    {
        var rows = new List<string>();
        if (def == null) return rows;
        foreach (StatModifier m in def.Modifiers)
            rows.Add(m + EffectiveSuffix(m, def, equipped));
        foreach (ArtifactEffect e in def.Effects)
            rows.Add(e.ToString());
        return rows;
    }

    // 실효값 없이 짧은 옵션 문구 (상점 목록, 도박 결과처럼 자리가 좁은 곳)
    public static List<string> PlainRows(ArtifactDefinition def)
    {
        var rows = new List<string>();
        if (def == null) return rows;
        foreach (StatModifier m in def.Modifiers) rows.Add(m.ToString());
        foreach (ArtifactEffect e in def.Effects) rows.Add(e.ToString());
        return rows;
    }

    public static string EffectiveSuffix(StatModifier m, ArtifactDefinition def, bool equipped)
    {
        PlayerStats stats = GameServices.Player != null ? GameServices.Player.Stats : null;
        if (stats == null || !TryValue(stats, m.Stat, out Func<float> get, out Func<float, string> format)) return "";
        float now = get();
        float other = stats.EvaluateWith(StatSources.Artifact(def.Id), equipped ? null : def.Modifiers, get);
        float before = equipped ? other : now;
        float after = equipped ? now : other;
        string a = format(before), b = format(after);
        return a == b ? "" : $" <color=#{Hex(Muted)}>({a} → {b})</color>";
    }

    // 실효값을 보여줄 스탯과 단위. 전체 무기 스탯은 무기마다 달라서 생략
    private static bool TryValue(PlayerStats stats, StatId stat, out Func<float> get, out Func<float, string> format)
    {
        get = null;
        format = null;
        switch (stat)
        {
            case StatId.MoveSpeed: get = () => stats.Get(stat); format = v => v.ToString("0.#"); return true;
            case StatId.DodgeChargeTime:
            case StatId.JustDodgeWindow:
            case StatId.HitInvincibleDuration: get = () => stats.Get(stat); format = Seconds; return true;
            case StatId.DodgeMaxCharge:
            case StatId.MaxHP: get = () => stats.GetInt(stat); format = v => v.ToString("0"); return true;
            case StatId.CritChance:
            case StatId.JustDodgeAmmoRefund: get = () => stats.Get(stat); format = v => (v * 100f).ToString("0") + "%"; return true;
            case StatId.CritDamage: get = () => stats.Get(stat); format = v => "x" + v.ToString("0.0#"); return true;
        }

        foreach (WeaponType weapon in Enum.GetValues(typeof(WeaponType)))
        {
            foreach (WeaponStat kind in Enum.GetValues(typeof(WeaponStat)))
            {
                if (StatNames.ForWeapon(weapon, kind).specific != stat) continue;
                WeaponType w = weapon;
                WeaponStat k = kind;
                switch (kind)
                {
                    case WeaponStat.Magazine: get = () => stats.GetWeaponInt(w, k); format = v => v.ToString("0"); break;
                    case WeaponStat.ReloadTime:
                    case WeaponStat.FireInterval: get = () => stats.GetWeapon(w, k); format = Seconds; break;
                    case WeaponStat.ConeAngle: get = () => stats.GetWeapon(w, k); format = v => v.ToString("0") + "°"; break;
                    default: get = () => stats.GetWeapon(w, k); format = v => v.ToString("0.#"); break;
                }
                return true;
            }
        }
        return false;
    }

    private static string Seconds(float v) => v.ToString("0.00") + "초";

    // 경고 행: 충돌, 무기 잠김, 스왑 발동 불가, HP 변화 (장착 전 후보만)
    public static List<string> WarningRows(ArtifactDefinition def)
    {
        var rows = new List<string>();
        ArtifactSystem artifacts = GameServices.Artifacts;
        if (def == null || artifacts == null) return rows;

        if (artifacts.WouldConflict(def))
            rows.Add("다른 무기 특화 세트와 함께 장착 불가 (정리 필요)");

        List<ArtifactDefinition> after = artifacts.LoadoutWith(-1, def);
        HashSet<WeaponType> lockedNow = artifacts.LockedWeaponsFor(artifacts.Slots.ToList());
        HashSet<WeaponType> lockedAfter = artifacts.LockedWeaponsFor(after);
        var newLocks = lockedAfter.Except(lockedNow).ToList();
        if (newLocks.Count > 0)
            rows.Add($"장착 시 {string.Join("·", newLocks.Select(WeaponName))} 잠김");

        bool swapEffect = def.Effects.Any(e => e.Kind == ArtifactEffectKind.SwapDamageWindow || e.Kind == ArtifactEffectKind.SwapEnhancedRounds || e.Kind == ArtifactEffectKind.SwapRefill);
        if (swapEffect && lockedAfter.Count >= 2)
            rows.Add("무기가 잠겨 교체 효과 발동 불가");

        PlayerStats stats = GameServices.Player != null ? GameServices.Player.Stats : null;
        if (stats != null && def.Modifiers.Any(m => m.Stat == StatId.MaxHP))
        {
            int now = stats.GetInt(StatId.MaxHP);
            int next = PlayerStats.RoundHalfUp(stats.EvaluateWith(StatSources.Artifact(def.Id), def.Modifiers, () => stats.Get(StatId.MaxHP)));
            if (now != next) rows.Add($"최대 HP {now} → {next}");
        }
        return rows;
    }

    // 장착 수 count인 세트의 진행: (채운 점, 전체 점)
    public static (int filled, int total) SetDots(int count)
    {
        ArtifactSystem artifacts = GameServices.Artifacts;
        int total = artifacts != null ? artifacts.NextThreshold(count) : 2;
        return (Mathf.Min(count, total), total);
    }

    public static string TierName(SetTier tier) => tier == SetTier.Tier2 ? "Tier2" : "Tier1";

    // 교체하면: 세트 Tier 켜짐/꺼짐 + 무기 잠금/해제 요약 줄
    public static List<(string text, bool good)> ChangeLines(IList<ArtifactDefinition> before, IList<ArtifactDefinition> after)
    {
        var lines = new List<(string, bool)>();
        ArtifactSystem artifacts = GameServices.Artifacts;
        if (artifacts == null) return lines;

        var sets = before.Concat(after).Where(a => a != null && a.Set != null).Select(a => a.Set).Distinct();
        foreach (ArtifactSetDefinition set in sets)
        {
            SetTier from = artifacts.TierForCount(before.Count(a => a != null && a.Set == set));
            SetTier to = artifacts.TierForCount(after.Count(a => a != null && a.Set == set));
            if (from == to) continue;
            if (to > from) lines.Add(($"{set.DisplayName} {TierName(to)} 켜짐", true));
            else lines.Add(($"{set.DisplayName} {TierName(from)} 꺼짐", false));
        }

        HashSet<WeaponType> lockedBefore = artifacts.LockedWeaponsFor(before);
        HashSet<WeaponType> lockedAfter = artifacts.LockedWeaponsFor(after);
        var unlocked = lockedBefore.Except(lockedAfter).ToList();
        var locked = lockedAfter.Except(lockedBefore).ToList();
        if (unlocked.Count > 0) lines.Add(($"{string.Join("·", unlocked.Select(WeaponName))} 잠금 해제", true));
        if (locked.Count > 0) lines.Add(($"{string.Join("·", locked.Select(WeaponName))} 잠김", false));
        return lines;
    }
}
