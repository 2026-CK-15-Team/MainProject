using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class ArtifactDrawer
{
    private const float SetBiasTriggerChance = 0.5f;
    private const float Combat2TargetChance = 0.75f;

    private struct DrawTrace
    {
        public ArtifactGrade RolledGrade;
        public ArtifactGrade UsedGrade;
        public bool Flipped;
        public int PoolSize;
        public bool BiasUsed;
    }

    public static ArtifactDefinition Draw(ArtifactCatalogData catalog, PlayerArtifacts currentEquipment,
        IEnumerable<ArtifactId> extraExclusions = null, string source = "general",
        string branch = "general", string info = null)
    {
        if (catalog == null || currentEquipment == null) return null;

        var picked = DrawCore(catalog, currentEquipment, extraExclusions, out var trace);
        LogDraw(source, branch, info, picked, trace);
        return picked;
    }

    private static ArtifactDefinition DrawCore(ArtifactCatalogData catalog, PlayerArtifacts currentEquipment,
        IEnumerable<ArtifactId> extraExclusions, out DrawTrace trace)
    {
        trace = default;
        var excludedIds = BuildExclusionSet(currentEquipment, extraExclusions);

        ArtifactGrade rolled = Random.value < 0.5f ? ArtifactGrade.Common : ArtifactGrade.Rare;
        ArtifactGrade grade = rolled;
        List<ArtifactDefinition> pool = GetValidPool(catalog, grade, excludedIds);
        bool flipped = false;

        if (pool.Count == 0)
        {
            grade = grade == ArtifactGrade.Common ? ArtifactGrade.Rare : ArtifactGrade.Common;
            pool = GetValidPool(catalog, grade, excludedIds);
            flipped = true;
        }

        trace.RolledGrade = rolled;
        trace.UsedGrade = grade;
        trace.Flipped = flipped;
        trace.PoolSize = pool.Count;

        if (pool.Count == 0) return null;

        var biased = TrySetBiasedPick(pool, catalog, currentEquipment);
        trace.BiasUsed = biased != null;
        return biased ?? pool[Random.Range(0, pool.Count)];
    }

    private static void LogDraw(string source, string branch, string info, ArtifactDefinition picked, DrawTrace t)
    {
        string eventName = source.StartsWith("combat2") ? "Combat2Draw" : "ArtifactDraw";
        string infoPart = string.IsNullOrEmpty(info) ? "" : $",{info}";
        string result = picked != null ? picked.Id.ToString() : "none";

        PlaytestLogger.Log(eventName,
            $"source={source},branch={branch}{infoPart},gradeRoll={t.RolledGrade},gradeUsed={t.UsedGrade}," +
            $"flipped={t.Flipped},poolSize={t.PoolSize},setBias={t.BiasUsed},result={result}");
    }

    private static HashSet<ArtifactId> BuildExclusionSet(PlayerArtifacts currentEquipment, IEnumerable<ArtifactId> extraExclusions)
    {
        var excludedIds = new HashSet<ArtifactId>(currentEquipment.Equipped.Select(a => a.Id));

        foreach (ArtifactId id in System.Enum.GetValues(typeof(ArtifactId)))
            if (ArtifactReservation.IsReserved(id))
                excludedIds.Add(id);

        if (extraExclusions != null)
            foreach (var id in extraExclusions)
                excludedIds.Add(id);

        return excludedIds;
    }

    private static List<ArtifactDefinition> GetValidPool(ArtifactCatalogData catalog, ArtifactGrade grade, HashSet<ArtifactId> excludedIds)
    {
        return catalog.AllArtifacts.Where(a => a.Grade == grade && !excludedIds.Contains(a.Id)).ToList();
    }

    private static ArtifactDefinition TrySetBiasedPick(List<ArtifactDefinition> pool, ArtifactCatalogData catalog, PlayerArtifacts currentEquipment)
    {
        if (Random.value >= SetBiasTriggerChance) return null;

        var incompleteSets = pool
            .Select(a => a.SetType)
            .Distinct()
            .Where(setType => IsIncomplete(setType, catalog, currentEquipment))
            .ToList();

        if (incompleteSets.Count == 0) return null;

        ArtifactSetType chosenSet = incompleteSets[Random.Range(0, incompleteSets.Count)];
        var candidates = pool.Where(a => a.SetType == chosenSet).ToList();
        return candidates[Random.Range(0, candidates.Count)];
    }

    private static bool IsIncomplete(ArtifactSetType setType, ArtifactCatalogData catalog, PlayerArtifacts currentEquipment)
    {
        int equippedCount = currentEquipment.Equipped.Count(a => a.SetType == setType);
        int totalInSet = catalog.AllArtifacts.Count(a => a.SetType == setType);
        return equippedCount >= 1 && equippedCount < totalInSet;
    }

    public static ArtifactDefinition DrawForTreasure(ArtifactCatalogData catalog, PlayerArtifacts currentEquipment,
        IEnumerable<ArtifactId> extraExclusions = null, string source = "treasure")
    {
        if (catalog == null || currentEquipment == null) return null;

        ArtifactDefinition result = null;
        DrawTrace trace = default;
        int attempts = 0;
        bool siblingRuleMet = false;

        for (int attempt = 0; attempt < 20; attempt++)
        {
            attempts++;
            var candidate = DrawCore(catalog, currentEquipment, extraExclusions, out trace);
            if (candidate == null)
            {
                result = null;
                break;
            }

            result = candidate;
            if (LeavesValidSiblingInSet(candidate, catalog, currentEquipment))
            {
                siblingRuleMet = true;
                break;
            }
        }

        if (result != null && !siblingRuleMet)
            Debug.LogWarning("[ArtifactDrawer] 세트고갈방지 조건을 만족하는 후보를 못 찾음, 마지막 결과 사용");

        LogDraw(source, "general", $"attempts={attempts},siblingRuleMet={siblingRuleMet}", result, trace);
        return result;
    }

    private static bool LeavesValidSiblingInSet(ArtifactDefinition candidate, ArtifactCatalogData catalog, PlayerArtifacts currentEquipment)
    {
        var equippedIds = new HashSet<ArtifactId>(currentEquipment.Equipped.Select(a => a.Id));

        int remainingSiblings = catalog.AllArtifacts.Count(a =>
            a.SetType == candidate.SetType && a.Id != candidate.Id && !equippedIds.Contains(a.Id));

        return remainingSiblings >= 1;
    }

    public static ArtifactDefinition DrawTargeted(ArtifactCatalogData catalog, PlayerArtifacts currentEquipment, ArtifactSetType targetSet,
        IEnumerable<ArtifactId> extraExclusions = null, string source = "combat2")
    {
        if (catalog == null || currentEquipment == null) return null;

        var excludedIds = BuildExclusionSet(currentEquipment, extraExclusions);
        var targetPool = catalog.AllArtifacts.Where(a => a.SetType == targetSet && !excludedIds.Contains(a.Id)).ToList();
        string info = $"targetSet={targetSet}";

        bool targetUsable = targetPool.Count > 0 && IsIncomplete(targetSet, catalog, currentEquipment);
        if (!targetUsable)
            return Draw(catalog, currentEquipment, extraExclusions, source, "full-fallback", info);

        if (Random.value < Combat2TargetChance)
        {
            var picked = targetPool[Random.Range(0, targetPool.Count)];
            PlaytestLogger.Log("Combat2Draw",
                $"source={source},branch=target-direct,{info},gradeRoll=none,poolSize={targetPool.Count},result={picked.Id}");
            return picked;
        }

        var other = DrawExcludingSet(catalog, excludedIds, targetSet, out var trace);
        LogDraw(source, "target-exclude", info, other, trace);
        return other;
    }

    private static ArtifactDefinition DrawExcludingSet(ArtifactCatalogData catalog, HashSet<ArtifactId> excludedIds,
        ArtifactSetType excludeSet, out DrawTrace trace)
    {
        trace = default;

        ArtifactGrade rolled = Random.value < 0.5f ? ArtifactGrade.Common : ArtifactGrade.Rare;
        ArtifactGrade grade = rolled;
        var pool = catalog.AllArtifacts.Where(a => a.Grade == grade && a.SetType != excludeSet && !excludedIds.Contains(a.Id)).ToList();
        bool flipped = false;

        if (pool.Count == 0)
        {
            grade = grade == ArtifactGrade.Common ? ArtifactGrade.Rare : ArtifactGrade.Common;
            pool = catalog.AllArtifacts.Where(a => a.Grade == grade && a.SetType != excludeSet && !excludedIds.Contains(a.Id)).ToList();
            flipped = true;
        }

        trace.RolledGrade = rolled;
        trace.UsedGrade = grade;
        trace.Flipped = flipped;
        trace.PoolSize = pool.Count;

        return pool.Count > 0 ? pool[Random.Range(0, pool.Count)] : null;
    }
}