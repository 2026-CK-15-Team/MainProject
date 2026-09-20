using System.Collections.Generic;
using UnityEngine;

public class FieldArtifact : MonoBehaviour, IInteractable
{
    [SerializeField] private ArtifactDefinition definition;
    [SerializeField] private ArtifactCatalogData catalog;
    [SerializeField] private bool isTreasureReward;

    private const int RerollCost = 6;

    private int rerollCount;
    private bool rerollDisabledPermanently;
    private readonly HashSet<ArtifactId> sessionSeenIds = new();

    public ArtifactDefinition CurrentDefinition => definition;
    public int RerollCount => rerollCount;
    public bool CanReroll => !rerollDisabledPermanently && rerollCount < 2;

    private void Awake()
    {
        if (definition == null && catalog != null)
        {
            var artifacts = FindObjectOfType<PlayerArtifacts>();
            definition = isTreasureReward
                ? ArtifactDrawer.DrawForTreasure(catalog, artifacts)
                : ArtifactDrawer.Draw(catalog, artifacts);
        }

        if (definition != null)
        {
            ArtifactReservation.Reserve(definition.Id);
            sessionSeenIds.Add(definition.Id);
        }
    }

    public void SetDefinition(ArtifactDefinition newDefinition)
    {
        if (newDefinition == null) return;

        if (definition != null) ArtifactReservation.Release(definition.Id);
        definition = newDefinition;
        ArtifactReservation.Reserve(definition.Id);
        sessionSeenIds.Add(definition.Id);
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
            Debug.Log("[FieldArtifact] 리롤 실패: CanReroll=false (버림 이력 또는 2회 소진)");
            return false;
        }

        int cost = rerollCount == 0 ? 0 : RerollCost;
        if (cost > 0 && !RunCurrency.TrySpend(cost))
        {
            Debug.Log($"[FieldArtifact] 리롤 실패: 재화 부족 (필요 {cost}, 보유 {RunCurrency.Amount})");
            return false;
        }

        if (catalog == null)
        {
            Debug.LogWarning("[FieldArtifact] 리롤 실패: Catalog가 비어있음. 이 오브젝트의 Catalog 필드를 연결하세요.");
            return false;
        }

        var artifacts = FindObjectOfType<PlayerArtifacts>();
        var redrawn = isTreasureReward
            ? ArtifactDrawer.DrawForTreasure(catalog, artifacts, sessionSeenIds)
            : ArtifactDrawer.Draw(catalog, artifacts, sessionSeenIds);

        if (redrawn == null)
        {
            Debug.LogWarning("[FieldArtifact] 리롤 실패: 뽑을 수 있는 유효 후보가 없음");
            return false;
        }

        if (definition != null) ArtifactReservation.Release(definition.Id);
        definition = redrawn;
        ArtifactReservation.Reserve(definition.Id);
        sessionSeenIds.Add(definition.Id);

        rerollCount++;
        newDefinition = definition;
        Debug.Log($"[FieldArtifact] 리롤 성공: {definition.DisplayName}");
        PlaytestLogger.Log("ArtifactReroll", $"{definition.Id},rerollCount={rerollCount},cost={cost}");
        return true;
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