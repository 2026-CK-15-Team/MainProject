using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// 06 §5.3 영구 진행 규칙 (해금, 기본 스탯 강화, 진행 조건). 저장 시점은 RunManager가 정함.
public static class MetaProgression
{
    private static PermanentData Data => GameServices.Save != null ? GameServices.Save.Data : null;
    private static ArtifactCatalog Catalog => GameServices.Config != null ? GameServices.Config.Artifacts : null;

    // 이번 런 후보 풀 = 초기 해금 + 재화 해금 + 이전 런까지의 해금 대기 (06 §5.3)
    public static List<string> UnlockedArtifactIds()
    {
        var ids = new List<string>();
        if (Catalog != null)
            foreach (ArtifactDefinition a in Catalog.InitialUnlocks()) ids.Add(a.Id);
        if (Data != null)
        {
            ids.AddRange(Data.CurrencyUnlockedArtifacts);
            ids.AddRange(Data.PendingUnlocks);
        }
        return ids.Distinct().ToList();
    }

    public static bool IsUnlocked(ArtifactDefinition def)
    {
        if (def == null) return false;
        if (def.UnlockSource == ArtifactUnlockSource.Initial) return true;
        return Data != null && (Data.CurrencyUnlockedArtifacts.Contains(def.Id) || Data.PendingUnlocks.Contains(def.Id));
    }

    public static bool IsDiscovered(ArtifactDefinition def) => def != null && Data != null && Data.DiscoveredArtifacts.Contains(def.Id);

    // 상세 정보가 처음 표시되는 순간 발견 등록. 새로 등록됐으면 true (호출 쪽이 즉시 저장)
    public static bool MarkDiscovered(ArtifactDefinition def)
    {
        if (def == null || Data == null || Data.DiscoveredArtifacts.Contains(def.Id)) return false;
        Data.DiscoveredArtifacts.Add(def.Id);
        return true;
    }

    // ---------- 로비 영구재화 상점 (상품, 가격은 기획 미정 → PrototypeDefault 후보값) ----------

    public static int UnlockCost => GameBalance.Current.Meta.ArtifactUnlockCost;

    public static bool CanBuyUnlock(ArtifactDefinition def)
        => def != null && def.UnlockSource == ArtifactUnlockSource.Currency && !IsUnlocked(def) && Data != null && Data.PermanentCurrency >= UnlockCost;

    public static bool BuyUnlock(ArtifactDefinition def)
    {
        if (!CanBuyUnlock(def)) return false;
        Data.PermanentCurrency -= UnlockCost;
        Data.CurrencyUnlockedArtifacts.Add(def.Id);
        GameEvents.Raise(GameEvents.ArtifactUnlockedViaCurrency, def.Id);
        return true;
    }

    public static int StatLevel(StatId stat) => Data == null ? 0 : stat == StatId.CritChance ? Data.CritChanceLevel : Data.CritDamageLevel;

    public static int MaxStatLevel => GameBalance.Current.Player.StatUpgradeMaxLevel;

    public static int NextStatCost(StatId stat)
    {
        int level = StatLevel(stat);
        int[] costs = GameBalance.Current.Meta.StatUpgradeCost;
        if (level >= MaxStatLevel || costs == null || costs.Length == 0) return -1;
        return costs[Mathf.Min(level, costs.Length - 1)];
    }

    public static bool CanUpgrade(StatId stat)
    {
        int cost = NextStatCost(stat);
        return cost >= 0 && Data != null && Data.PermanentCurrency >= cost;
    }

    public static bool Upgrade(StatId stat)
    {
        if (!CanUpgrade(stat)) return false;
        Data.PermanentCurrency -= NextStatCost(stat);
        if (stat == StatId.CritChance) Data.CritChanceLevel++;
        else Data.CritDamageLevel++;
        ApplyPermanentStats();
        GameEvents.Raise(GameEvents.BaseStatUpgraded, stat, StatLevel(stat));
        return true;
    }

    // 영구 강화 고정값 (01-4 §6)
    public static void ApplyPermanentStats()
    {
        PlayerController player = GameServices.Player;
        if (player == null || Data == null) return;
        PlayerBalance b = GameBalance.Current.Player;
        player.Stats.SetSource(StatSources.PermanentCrit, new[]
        {
            new StatModifier(StatId.CritChance, ModifierKind.Flat, b.CritChanceUpgradeStep * Data.CritChanceLevel),
            new StatModifier(StatId.CritDamage, ModifierKind.Flat, b.CritDamageUpgradeStep * Data.CritDamageLevel),
        });
    }

    // ---------- 진행 조건 (06 §5.3 표) ----------

    // 처음 달성하면 연결된 ID를 해금 대기로. 새로 생긴 해금 ID 목록을 돌려줌 (연결이 비면 해금 없음)
    public static List<string> AchieveCondition(ProgressCondition condition)
    {
        var created = new List<string>();
        if (Data == null || Data.AchievedConditions.Contains((int)condition)) return created;
        Data.AchievedConditions.Add((int)condition);
        ArtifactDefinition linked = Catalog != null ? Catalog.ProgressLink(condition) : null;
        if (linked != null && !Data.PendingUnlocks.Contains(linked.Id))
        {
            Data.PendingUnlocks.Add(linked.Id);
            created.Add(linked.Id);
            GameEvents.Raise(GameEvents.ArtifactProgressUnlockPending, linked.Id);
        }
        return created;
    }

    public static List<string> UnrevealedPendingUnlocks()
        => Data == null ? new List<string>() : Data.PendingUnlocks.Where(id => !Data.RevealedPendingUnlocks.Contains(id)).ToList();

    // 카드 표시 순간: 발견 + 공개 완료 (호출 쪽이 즉시 저장)
    public static void MarkRevealed(IEnumerable<string> ids)
    {
        if (Data == null) return;
        foreach (string id in ids)
        {
            if (!Data.RevealedPendingUnlocks.Contains(id)) Data.RevealedPendingUnlocks.Add(id);
            if (!Data.DiscoveredArtifacts.Contains(id)) Data.DiscoveredArtifacts.Add(id);
            GameEvents.Raise(GameEvents.ArtifactProgressUnlockRevealed, id);
        }
    }
}
