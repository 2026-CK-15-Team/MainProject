using UnityEngine;

public class ShopSlot : MonoBehaviour, IInteractable
{
    public enum SlotType { HPHeal, Artifact, PermanentCurrencyBundle }

    public SlotType Type;
    public int Price = 5;
    public bool SoldOut;

    [Header("HPHeal 전용")]
    public int HealAmount = 1;

    [Header("Artifact 전용")]
    public ArtifactDefinition Artifact;
    public ArtifactCatalogData Catalog;

    [Header("PermanentCurrencyBundle 전용")]
    public int BundleAmount = 2;

    private void Awake()
    {
        if (Type == SlotType.Artifact && Artifact == null && Catalog != null)
        {
            var artifacts = FindObjectOfType<PlayerArtifacts>();
            Artifact = ArtifactDrawer.Draw(Catalog, artifacts);
        }

        if (Type == SlotType.Artifact && Artifact != null)
            ArtifactReservation.Reserve(Artifact.Id);
    }

    public void Interact()
    {
        if (SoldOut)
        {
            Debug.Log("[Shop] 매진된 상품입니다");
            return;
        }

        var health = FindObjectOfType<PlayerHealth>();

        if (Type == SlotType.HPHeal && health != null && health.CurrentHP >= health.MaxHP)
        {
            Debug.Log("[Shop] 체력이 이미 가득 참");
            return;
        }

        if (!RunCurrency.TrySpend(Price))
        {
            Debug.Log("[Shop] 재화 부족");
            return;
        }

        SoldOut = true;
        PlaytestLogger.Log("ShopPurchase", $"{Type},price={Price}");

        switch (Type)
        {
            case SlotType.HPHeal:
                health?.Heal(HealAmount);
                Debug.Log($"[Shop] HP {HealAmount} 회복 구매");
                break;

            case SlotType.Artifact:
                var artifacts = FindObjectOfType<PlayerArtifacts>();
                artifacts?.TryEquip(Artifact);
                ArtifactReservation.Release(Artifact.Id);
                Debug.Log($"[Shop] 아티팩트 구매: {Artifact.DisplayName}");
                break;

            case SlotType.PermanentCurrencyBundle:
                Debug.Log($"[Shop] 영구재화 {BundleAmount} 획득 예정 (정산 시스템 미구현)");
                break;
        }
    }
}