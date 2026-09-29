using System.Collections.Generic;
using UnityEngine;

public class FieldArtifact : MonoBehaviour, IInteractable
{
    [SerializeField] private ArtifactDefinition definition; // 직접 지정하려면 채우고, 비워두면 카탈로그에서 무작위로 뽑음
    [SerializeField] private ArtifactCatalogData catalog;
    [SerializeField] private bool isTreasureReward;

    private const int RerollCost = 6;
    private const int MaxRerollCount = 2;

    private int rerollCount;
    private bool rerollDisabledPermanently; // 버리기 이후 재획득 시 true
    private bool rerollUnavailable;         // 새 후보를 뽑을 수 없을 때 true
    private readonly HashSet<ArtifactId> sessionSeenIds = new(); // 이번 획득 세션에서 이미 본 ID들, 리롤로 재등장 방지

    private bool isCombat2Reward;
    private ArtifactSetType? combat2TargetSet;

    public ArtifactDefinition CurrentDefinition => definition;
    public int RerollCount => rerollCount;
    public bool CanReroll => !rerollDisabledPermanently && !rerollUnavailable && rerollCount < MaxRerollCount;

    private void Awake()
    {
        RegisterCurrent();
    }

    private void Start()
    {
        if (definition != null || catalog == null) return;

        var artifacts = FindObjectOfType<PlayerArtifacts>();
        definition = isTreasureReward
            ? ArtifactDrawer.DrawForTreasure(catalog, artifacts, null, "treasure")
            : ArtifactDrawer.Draw(catalog, artifacts, null, "field");

        RegisterCurrent();
    }

    public void ConfigureCombat2(ArtifactCatalogData spawnerCatalog, ArtifactSetType? targetSet)
    {
        if (catalog == null) catalog = spawnerCatalog;

        isCombat2Reward = true;
        combat2TargetSet = targetSet;

        if (definition != null || catalog == null) return;

        var artifacts = FindObjectOfType<PlayerArtifacts>();
        definition = DrawCombat2(artifacts, null, "combat2");
        RegisterCurrent();
    }

    private ArtifactDefinition DrawCombat2(PlayerArtifacts artifacts, IEnumerable<ArtifactId> exclusions, string source)
    {
        return combat2TargetSet.HasValue
            ? ArtifactDrawer.DrawTargeted(catalog, artifacts, combat2TargetSet.Value, exclusions, source)
            : ArtifactDrawer.Draw(catalog, artifacts, exclusions, source, "full-fallback", "targetSet=none");
    }

    private void RegisterCurrent()
    {
        if (definition == null) return;

        ArtifactReservation.Reserve(definition.Id);
        sessionSeenIds.Add(definition.Id);
    }

    public void SetDefinition(ArtifactDefinition newDefinition)
    {
        if (newDefinition == null) return;

        if (definition != null) ArtifactReservation.Release(definition.Id);
        definition = newDefinition;
        RegisterCurrent();
    }

    public void Interact()
    {
        if (definition == null) return;

        var ui = FindObjectOfType<ArtifactAcquireUI>();
        if (ui == null)
        {
            var fallbackArtifacts = FindObjectOfType<PlayerArtifacts>();
            if (fallbackArtifacts != null && fallbackArtifacts.TryEquip(definition))
            {
                ArtifactReservation.Release(definition.Id);
                gameObject.SetActive(false);
            }
            return;
        }

        ui.Open(this);
    }

    public bool TryReroll(out ArtifactDefinition newDefinition)
    {
        newDefinition = null;

        if (!CanReroll)
        {
            Debug.Log("[FieldArtifact] 리롤 실패: CanReroll=false (버림 이력, 2회 소진 또는 후보 없음)");
            return false;
        }

        if (catalog == null)
        {
            Debug.LogWarning("[FieldArtifact] 리롤 실패: Catalog가 비어있음. 이 오브젝트의 Catalog 필드를 연결하세요.");
            return false;
        }

        int cost = rerollCount == 0 ? 0 : RerollCost;
        if (cost > 0 && RunCurrency.Amount < cost)
        {
            Debug.Log($"[FieldArtifact] 리롤 실패: 재화 부족 (필요 {cost}, 보유 {RunCurrency.Amount})");
            return false;
        }

        var redrawn = DrawForReroll();
        if (redrawn == null)
        {
            rerollUnavailable = true;
            Debug.LogWarning("[FieldArtifact] 리롤 불가: 뽑을 수 있는 유효 후보가 없음, 현재 후보 유지");
            PlaytestLogger.Log("ArtifactRerollFailed",
                $"{(definition != null ? definition.Id.ToString() : "none")},reason=no-candidate,rerollCount={rerollCount}");
            return false;
        }

        if (cost > 0 && !RunCurrency.TrySpend(cost)) return false;

        if (definition != null) ArtifactReservation.Release(definition.Id);
        definition = redrawn;
        RegisterCurrent();

        rerollCount++;
        newDefinition = definition;
        Debug.Log($"[FieldArtifact] 리롤 성공: {definition.DisplayName}");
        PlaytestLogger.Log("ArtifactReroll", $"{definition.Id},rerollCount={rerollCount},cost={cost}");
        return true;
    }

    private ArtifactDefinition DrawForReroll()
    {
        var artifacts = FindObjectOfType<PlayerArtifacts>();

        if (isCombat2Reward) return DrawCombat2(artifacts, sessionSeenIds, "combat2-reroll");
        if (isTreasureReward) return ArtifactDrawer.DrawForTreasure(catalog, artifacts, sessionSeenIds, "treasure-reroll");
        return ArtifactDrawer.Draw(catalog, artifacts, sessionSeenIds, "field-reroll");
    }

    public void ConfirmKeep()
    {
        var artifacts = FindObjectOfType<PlayerArtifacts>();
        if (artifacts != null && artifacts.TryEquip(definition))
        {
            if (isTreasureReward)
                RunProgressState.TreasureSetType = definition.SetType;

            ArtifactReservation.Release(definition.Id);
            gameObject.SetActive(false);
        }
    }

    public void ConfirmDiscard()
    {
        rerollDisabledPermanently = true;
    }
}