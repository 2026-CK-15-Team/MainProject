using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// 06 §5.3 영구 진행 규칙 (해금, 진행 조건) + 06-1 성장 포인트. 저장 시점은 RunManager가 정함.
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

    // ---------- 성장 포인트 (06-1) ----------
    // 저장값은 구매 수와 트리별 배치 수뿐. 사용/남은 포인트는 매번 계산.

    public enum NodeState { Locked, Available, Allocated }

    private static MetaBalance Meta => GameBalance.Current.Meta;
    public static GrowthCategory[] GrowthTree => Meta.GrowthTree ?? new GrowthCategory[0];

    // 노드 k(0부터)의 필요 포인트 = k + 1
    public static int NodeCost(int node) => node + 1;

    public static int GrowthPointsMax => Meta.GrowthPointPrices != null ? Meta.GrowthPointPrices.Length : 0;
    public static int GrowthPointsPurchased => Data != null ? Data.GrowthPointsPurchased : 0;

    public static int Allocation(int category)
    {
        if (Data == null || category < 0 || category >= Data.GrowthAllocation.Count) return 0;
        return Data.GrowthAllocation[category];
    }

    // 배치 수 n이면 1 + 2 + ... + n
    public static int GrowthPointsUsed
    {
        get
        {
            int used = 0;
            for (int c = 0; c < GrowthTree.Length; c++)
            {
                int n = Allocation(c);
                used += n * (n + 1) / 2;
            }
            return used;
        }
    }

    public static int GrowthPointsRemaining => GrowthPointsPurchased - GrowthPointsUsed;

    // 다음 포인트 가격. 상한이면 -1
    public static int NextGrowthPointPrice
        => GrowthPointsPurchased >= GrowthPointsMax ? -1 : Meta.GrowthPointPrices[GrowthPointsPurchased];

    public static bool CanBuyGrowthPoint
    {
        get
        {
            int price = NextGrowthPointPrice;
            return price >= 0 && Data != null && Data.PermanentCurrency >= price;
        }
    }

    public static bool BuyGrowthPoint()
    {
        if (!CanBuyGrowthPoint) return false;
        int price = NextGrowthPointPrice;
        Data.PermanentCurrency -= price;
        Data.GrowthPointsPurchased++;
        GameEvents.Raise(GameEvents.GrowthPointPurchased, Data.GrowthPointsPurchased, price);
        return true;
    }

    public static NodeState GetNodeState(int category, int node)
    {
        int allocated = Allocation(category);
        if (node < allocated) return NodeState.Allocated;
        return node == allocated ? NodeState.Available : NodeState.Locked;
    }

    public static bool IsTopAllocated(int category, int node) => node == Allocation(category) - 1;

    public static bool CanAllocate(int category, int node)
        => GetNodeState(category, node) == NodeState.Available && GrowthPointsRemaining >= NodeCost(node);

    public static bool Allocate(int category, int node)
    {
        SanitizeGrowth();
        if (!CanAllocate(category, node)) return false;
        Data.GrowthAllocation[category]++;
        OnAllocationChanged();
        return true;
    }

    // 가장 위 배치 노드만 해제. 포인트는 남은 포인트로 돌아가고 영구 재화는 돌려주지 않음
    public static bool Release(int category, int node)
    {
        if (Data == null || !IsTopAllocated(category, node)) return false;
        Data.GrowthAllocation[category]--;
        OnAllocationChanged();
        return true;
    }

    // 배치만 0으로. 구매 수와 영구 재화는 그대로
    public static bool ResetGrowth()
    {
        if (Data == null || GrowthPointsUsed == 0) return false;
        for (int c = 0; c < Data.GrowthAllocation.Count; c++) Data.GrowthAllocation[c] = 0;
        OnAllocationChanged();
        return true;
    }

    private static void OnAllocationChanged()
    {
        ApplyPermanentStats();
        GameEvents.Raise(GameEvents.GrowthAllocationChanged);
    }

    // 배치된 노드 수정값 전부 (UI 미리보기는 extra로 노드 하나를 더 얹어 계산)
    public static List<StatModifier> GrowthModifiers(int extraCategory = -1)
    {
        var mods = new List<StatModifier>();
        GrowthCategory[] tree = GrowthTree;
        for (int c = 0; c < tree.Length; c++)
        {
            int count = Allocation(c) + (c == extraCategory ? 1 : 0);
            for (int n = 0; n < count && n < tree[c].Nodes.Length; n++)
                mods.AddRange(tree[c].Nodes[n].Modifiers);
        }
        return mods;
    }

    // 세이브 보정: 트리 수에 맞추고, 범위 밖이거나 사용 포인트가 구매 수를 넘으면 배치를 0으로 (06-1 §9)
    private static void SanitizeGrowth()
    {
        if (Data == null) return;
        GrowthCategory[] tree = GrowthTree;
        if (Data.GrowthAllocation == null) Data.GrowthAllocation = new List<int>();
        while (Data.GrowthAllocation.Count < tree.Length) Data.GrowthAllocation.Add(0);
        if (Data.GrowthAllocation.Count > tree.Length) Data.GrowthAllocation.RemoveRange(tree.Length, Data.GrowthAllocation.Count - tree.Length);
        Data.GrowthPointsPurchased = Mathf.Clamp(Data.GrowthPointsPurchased, 0, GrowthPointsMax);

        bool invalid = false;
        for (int c = 0; c < tree.Length; c++)
            if (Data.GrowthAllocation[c] < 0 || Data.GrowthAllocation[c] > tree[c].Nodes.Length) invalid = true;
        if (invalid || GrowthPointsUsed > Data.GrowthPointsPurchased)
            for (int c = 0; c < tree.Length; c++) Data.GrowthAllocation[c] = 0;
    }

    // 영구 강화 원천 (01-4 §5). 배치가 바뀌면 원천 하나를 통째로 교체
    public static void ApplyPermanentStats()
    {
        SanitizeGrowth();
        PlayerController player = GameServices.Player;
        if (player == null || Data == null) return;
        player.Stats.SetSource(StatSources.PermanentGrowth, GrowthModifiers());
    }

    // 상점 문구: 치명타는 %p, 회피 충전 수는 정수, 비율은 %
    public static string Describe(StatModifier m)
    {
        string name = StatNames.Display(m.Stat);
        if (m.Kind == ModifierKind.Percent) return $"{name} {m.Value * 100f:+0.#;-0.#}%";
        if (m.Stat == StatId.CritChance || m.Stat == StatId.CritDamage) return $"{name} {m.Value * 100f:+0.#;-0.#}%p";
        return m.ToString();
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
