using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// 07 아티팩트와 빌드. 6슬롯, 추첨(등급 → ID 2단계), 제외 목록 3종, 세트 Tier, 무기 특화 잠금을 소유함.
// - 현재 장착 ID 제외 (§4.4) / 활성 예약 (§4.5) / 세션 확인 ID (§4.13)
// - 빈 등급은 빼고 남은 등급 비율 유지 재정규화. 전체 소진이면 생성 취소 + 오류 기록 (§4.2)
public class ArtifactSystem : MonoBehaviour
{
    private ArtifactDefinition[] slots;
    private readonly HashSet<string> reserved = new HashSet<string>();
    private readonly HashSet<string> unlockedForRun = new HashSet<string>();
    private readonly Dictionary<ArtifactSetDefinition, SetTier> setTiers = new Dictionary<ArtifactSetDefinition, SetTier>();

    public IReadOnlyList<ArtifactDefinition> Slots => slots;
    public int SlotCount => slots.Length;
    public ArtifactCatalog Catalog => GameServices.Config != null ? GameServices.Config.Artifacts : null;
    private ArtifactBalance Balance => GameBalance.Current.Artifact;

    public event Action SlotsChanged;

    private void Awake()
    {
        GameServices.Artifacts = this;
        slots = new ArtifactDefinition[Mathf.Max(1, Balance.SlotCount)];
    }

    // 런 시작: 슬롯 비움, 예약 초기화, 이번 런 후보 풀 = 해금 목록 (해금 대기는 다음 런부터 → 런 시작 시점 해금 목록 고정)
    public void BeginRun(IEnumerable<string> unlockedIds)
    {
        slots = new ArtifactDefinition[Mathf.Max(1, Balance.SlotCount)];
        reserved.Clear();
        setTiers.Clear();
        unlockedForRun.Clear();
        foreach (string id in unlockedIds) unlockedForRun.Add(id);
        RecalculateSets();
        SlotsChanged?.Invoke();
    }

    // 층 이탈: 회수하지 않은 필드 복사본 소실 → 예약 해제 (§4.5)
    public void OnFloorLeft() => reserved.Clear();

    public bool IsEquipped(string id) => slots.Any(s => s != null && s.Id == id);
    public bool IsReserved(string id) => reserved.Contains(id);
    public int EquippedCount => slots.Count(s => s != null);
    public bool HasEmptySlot => slots.Any(s => s == null);

    public void Reserve(ArtifactDefinition def) { if (def != null) reserved.Add(def.Id); }
    public void Release(ArtifactDefinition def) { if (def != null) reserved.Remove(def.Id); }

    // ---------------- 추첨 ----------------

    public float[] GradeWeights(AcquisitionOrigin origin)
    {
        // EliteGateRoom: 일반 제외, 희귀:전설 비율 유지 (§4.2 1번)
        if (origin == AcquisitionOrigin.EliteGate)
            return new[] { 0f, Balance.RareWeight, Balance.LegendaryWeight };
        return new[] { Balance.CommonWeight, Balance.RareWeight, Balance.LegendaryWeight };
    }

    public bool IsValidCandidate(ArtifactDefinition def, ICollection<string> sessionSeen)
    {
        if (def == null || string.IsNullOrEmpty(def.Id)) return false;
        if (!unlockedForRun.Contains(def.Id)) return false;
        if (IsEquipped(def.Id) || reserved.Contains(def.Id)) return false;
        if (sessionSeen != null && sessionSeen.Contains(def.Id)) return false;
        return true;
    }

    // 성공하면 ID를 즉시 예약까지 함 (같은 묶음 중복 방지)
    public ArtifactDefinition DrawAndReserve(AcquisitionOrigin origin, ICollection<string> sessionSeen = null, System.Random rng = null)
    {
        ArtifactDefinition result = Draw(origin, sessionSeen, rng);
        if (result != null) Reserve(result);
        return result;
    }

    public ArtifactDefinition Draw(AcquisitionOrigin origin, ICollection<string> sessionSeen = null, System.Random rng = null)
    {
        rng ??= RandomStreams.Loot;
        ArtifactCatalog catalog = Catalog;
        if (catalog == null)
        {
            Debug.LogError("ArtifactSystem: 카탈로그 없음 (GameConfig.Artifacts)");
            return null;
        }

        float[] weights = GradeWeights(origin);
        var candidatesByGrade = new List<ArtifactDefinition>[3];
        for (int g = 0; g < 3; g++)
            candidatesByGrade[g] = catalog.Artifacts.Where(a => a != null && (int)a.Grade == g && IsValidCandidate(a, sessionSeen)).ToList();

        // 유효 ID 0개 등급 제외 후 재정규화
        float total = 0f;
        for (int g = 0; g < 3; g++)
        {
            if (candidatesByGrade[g].Count == 0) weights[g] = 0f;
            total += weights[g];
        }
        if (total <= 0f)
        {
            Debug.LogError($"ArtifactSystem: 후보 풀 소진, 생성 취소 ({origin}). 콘텐츠 검증 조건 위반 (07 §4.2)");
            return null;
        }

        double roll = rng.NextDouble() * total;
        int grade = 0;
        for (int g = 0; g < 3; g++)
        {
            if (weights[g] <= 0f) continue;
            grade = g;
            if (roll < weights[g]) break;
            roll -= weights[g];
        }
        List<ArtifactDefinition> pool = candidatesByGrade[grade];

        // 세트 쏠림: 장착 중인 미완성 세트마다 추첨권 1장, 판정 성공하면 세트 하나 균등 → 그 세트 ID 균등 (§4.3)
        List<ArtifactSetDefinition> biasSets = IncompleteEquippedSets()
            .Where(set => pool.Any(a => a.Set == set))
            .ToList();
        if (biasSets.Count > 0 && rng.NextDouble() < Balance.SetBiasTriggerChance)
        {
            ArtifactSetDefinition set = biasSets[rng.Next(biasSets.Count)];
            List<ArtifactDefinition> setPool = pool.Where(a => a.Set == set).ToList();
            return setPool[rng.Next(setPool.Count)];
        }
        return pool[rng.Next(pool.Count)];
    }

    // 리롤 가능 여부: 모든 허용 등급에 미확인 유효 ID가 있는지 (§4.13 2번)
    public bool HasAnyCandidate(AcquisitionOrigin origin, ICollection<string> sessionSeen)
    {
        ArtifactCatalog catalog = Catalog;
        if (catalog == null) return false;
        float[] weights = GradeWeights(origin);
        return catalog.Artifacts.Any(a => a != null && weights[(int)a.Grade] > 0f && IsValidCandidate(a, sessionSeen));
    }

    private IEnumerable<ArtifactSetDefinition> IncompleteEquippedSets()
    {
        foreach (var group in slots.Where(s => s != null && s.Set != null).GroupBy(s => s.Set))
        {
            if (group.Count() < Balance.Tier2Threshold) yield return group.Key;
        }
    }

    // ---------------- 장착 ----------------

    // 특화 제한: 최종 6슬롯에서 Tier1 이상 무기 특화 세트는 1개뿐 (§4.14)
    public bool IsValidLoadout(IList<ArtifactDefinition> loadout)
    {
        if (!Balance.WeaponSpecializationRules) return true;
        int activeSpecializations = loadout
            .Where(a => a != null && a.Set != null && a.Set.IsWeaponSpecialization)
            .GroupBy(a => a.Set)
            .Count(g => g.Count() >= Balance.Tier1Threshold);
        return activeSpecializations <= 1;
    }

    // 새 아티팩트를 빈 슬롯에 넣었을 때 특화 충돌이 생기는지
    public bool WouldConflict(ArtifactDefinition incoming)
    {
        var loadout = slots.ToList();
        int empty = loadout.IndexOf(null);
        if (empty < 0) loadout.Add(incoming);
        else loadout[empty] = incoming;
        return !IsValidLoadout(loadout);
    }

    // 빈 슬롯 있고 특화 충돌 없으면 즉시 장착 (§4.10 2번)
    public bool TryAutoEquip(ArtifactDefinition def)
    {
        int empty = Array.IndexOf(slots, null);
        if (empty < 0 || WouldConflict(def)) return false;
        EquipToSlot(def, empty);
        return true;
    }

    // 즉시 지급형 (상점, 도박 §4.9). 장착 못 하면 획득 대기 모드, 취소하면 현재 룸 바닥에 남김(리롤 불가, 환불 없음)
    public void GrantImmediate(ArtifactDefinition def, AcquisitionOrigin origin)
    {
        GameEvents.Raise(GameEvents.ArtifactAcquired, def);
        if (TryAutoEquip(def)) return;
        GameServices.UI?.OpenInventoryPending(def, origin, onEquipped: null, onCancelled: () => ArtifactPickup.SpawnDropped(def, origin));
    }

    public void EquipToSlot(ArtifactDefinition def, int slotIndex)
    {
        Release(def);
        slots[slotIndex] = def;
        GameServices.Player?.Stats.SetSource(StatSources.Artifact(def.Id), def.Modifiers);
        GameEvents.Raise(GameEvents.ArtifactEquipped, def);
        RecalculateSets();
        SlotsChanged?.Invoke();
    }

    // 슬롯에서 빼고 필드에 안전 배치 (교체, 버리기 §4.11, §4.12). 다시 주우면 재획득(리롤 없음)
    public ArtifactDefinition RemoveFromSlot(int slotIndex, bool dropToField)
    {
        ArtifactDefinition def = slots[slotIndex];
        if (def == null) return null;
        slots[slotIndex] = null;
        GameServices.Player?.Stats.RemoveSource(StatSources.Artifact(def.Id));
        GameEvents.Raise(GameEvents.ArtifactUnequipped, def);
        if (dropToField)
        {
            Reserve(def);
            ArtifactPickup.SpawnDropped(def, AcquisitionOrigin.TreasureRoom);
            GameEvents.Raise(GameEvents.ArtifactDroppedToField, def);
        }
        RecalculateSets();
        SlotsChanged?.Invoke();
        return def;
    }

    // 획득 대기 모드에서 버리기/교체 시뮬레이션 검사
    public bool CanEquipAfter(int removeSlot, ArtifactDefinition incoming, int targetSlot)
    {
        var loadout = slots.ToList();
        if (removeSlot >= 0) loadout[removeSlot] = null;
        if (targetSlot < 0) targetSlot = loadout.IndexOf(null);
        if (targetSlot < 0) return false;
        if (loadout[targetSlot] != null && targetSlot != removeSlot) return false;
        loadout[targetSlot] = incoming;
        return IsValidLoadout(loadout);
    }

    // ---------------- 세트 ----------------

    public int SetCount(ArtifactSetDefinition set) => slots.Count(s => s != null && s.Set == set);

    // 장착 아티팩트 특수 효과 + 활성 세트 Tier 특수 효과 (Tier2면 Tier1 것도 포함)
    public void CollectEffects(List<ArtifactEffect> result)
    {
        result.Clear();
        foreach (ArtifactDefinition def in slots)
        {
            if (def == null || def.Effects == null) continue;
            result.AddRange(def.Effects);
        }
        foreach (var pair in setTiers)
        {
            if (pair.Key == null) continue;
            if (pair.Value >= SetTier.Tier1 && pair.Key.Tier1Effects != null) result.AddRange(pair.Key.Tier1Effects);
            if (pair.Value >= SetTier.Tier2 && pair.Key.Tier2Effects != null) result.AddRange(pair.Key.Tier2Effects);
        }
    }

    public SetTier TierOf(ArtifactSetDefinition set)
    {
        int count = SetCount(set);
        if (count >= Balance.Tier2Threshold) return SetTier.Tier2;
        if (count >= Balance.Tier1Threshold) return SetTier.Tier1;
        return SetTier.Inactive;
    }

    public IEnumerable<(ArtifactSetDefinition set, SetTier tier)> ActiveSets()
    {
        foreach (var pair in setTiers)
            if (pair.Value != SetTier.Inactive) yield return (pair.Key, pair.Value);
    }

    private void RecalculateSets()
    {
        PlayerController player = GameServices.Player;
        var sets = new HashSet<ArtifactSetDefinition>(slots.Where(s => s != null && s.Set != null).Select(s => s.Set));
        foreach (ArtifactSetDefinition known in setTiers.Keys.ToList()) sets.Add(known);

        var lockedWeapons = new HashSet<WeaponType>();
        WeaponType? specialization = null;

        foreach (ArtifactSetDefinition set in sets)
        {
            SetTier tier = TierOf(set);
            setTiers.TryGetValue(set, out SetTier previous);
            if (tier != previous)
            {
                setTiers[set] = tier;
                GameEvents.Raise(GameEvents.SetTierChanged, set, tier);
            }

            if (player != null)
            {
                var mods = new List<StatModifier>();
                if (tier >= SetTier.Tier1) mods.AddRange(set.Tier1Modifiers);
                if (tier >= SetTier.Tier2) mods.AddRange(set.Tier2Modifiers);
                player.Stats.SetSource(StatSources.Set(set.Id), mods);
            }

            if (Balance.WeaponSpecializationRules && set.IsWeaponSpecialization && tier >= SetTier.Tier1)
            {
                specialization = set.SpecializedWeapon;
                foreach (WeaponType w in Enum.GetValues(typeof(WeaponType)))
                    if (w != set.SpecializedWeapon) lockedWeapons.Add(w);
            }
        }

        player?.Weapons.SetWeaponLocks(lockedWeapons, specialization);
    }
}
