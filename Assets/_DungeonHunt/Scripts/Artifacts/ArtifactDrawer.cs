using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class ArtifactDrawer
{
    private const float SetBiasTriggerChance = 0.5f;

    public static ArtifactDefinition Draw(ArtifactCatalogData catalog, PlayerArtifacts currentEquipment,
        IEnumerable<ArtifactId> extraExclusions = null)
    {
        if (catalog == null || currentEquipment == null) return null;

        var excludedIds = BuildExclusionSet(currentEquipment, extraExclusions);

        ArtifactGrade grade = Random.value < 0.5f ? ArtifactGrade.Common : ArtifactGrade.Rare;
        List<ArtifactDefinition> pool = GetValidPool(catalog, grade, excludedIds);

        if (pool.Count == 0)
        {
            grade = grade == ArtifactGrade.Common ? ArtifactGrade.Rare : ArtifactGrade.Common;
            pool = GetValidPool(catalog, grade, excludedIds);
        }

        if (pool.Count == 0) return null;

        return TrySetBiasedPick(pool, catalog, currentEquipment) ?? pool[Random.Range(0, pool.Count)];
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
        IEnumerable<ArtifactId> extraExclusions = null)
    {
        if (catalog == null || currentEquipment == null) return null;

        ArtifactDefinition result = null;
        for (int attempt = 0; attempt < 20; attempt++)
        {
            var candidate = Draw(catalog, currentEquipment, extraExclusions);
            if (candidate == null) return null;

            result = candidate;
            if (LeavesValidSiblingInSet(candidate, catalog, currentEquipment)) return candidate;
        }

        Debug.LogWarning("[ArtifactDrawer] 세트고갈방지 조건을 만족하는 후보를 못 찾음, 마지막 결과 사용");
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
        IEnumerable<ArtifactId> extraExclusions = null)
    {
        if (catalog == null || currentEquipment == null) return null;

        var excludedIds = BuildExclusionSet(currentEquipment, extraExclusions);

        if (Random.value < 0.75f)
        {
            var targetPool = catalog.AllArtifacts.Where(a => a.SetType == targetSet && !excludedIds.Contains(a.Id)).ToList();
            if (targetPool.Count > 0)
            {
                var picked = targetPool[Random.Range(0, targetPool.Count)];
                PlaytestLogger.Log("Combat2Draw", $"branch=75,targetSet={targetSet},result={picked.Id}");
                return picked;
            }
        }

        var fallback = DrawExcludingSet(catalog, excludedIds, targetSet);
        PlaytestLogger.Log("Combat2Draw", $"branch=25,targetSet={targetSet},result={(fallback != null ? fallback.Id.ToString() : "none")}");
        return fallback;
    }

    private static ArtifactDefinition DrawExcludingSet(ArtifactCatalogData catalog, HashSet<ArtifactId> excludedIds, ArtifactSetType excludeSet)
    {
        ArtifactGrade grade = Random.value < 0.5f ? ArtifactGrade.Common : ArtifactGrade.Rare;
        var pool = catalog.AllArtifacts.Where(a => a.Grade == grade && a.SetType != excludeSet && !excludedIds.Contains(a.Id)).ToList();

        if (pool.Count == 0)
        {
            grade = grade == ArtifactGrade.Common ? ArtifactGrade.Rare : ArtifactGrade.Common;
            pool = catalog.AllArtifacts.Where(a => a.Grade == grade && a.SetType != excludeSet && !excludedIds.Contains(a.Id)).ToList();
        }

        return pool.Count > 0 ? pool[Random.Range(0, pool.Count)] : null;
    }
}