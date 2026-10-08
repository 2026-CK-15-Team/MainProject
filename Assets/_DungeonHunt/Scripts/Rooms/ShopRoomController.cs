using System.Collections.Generic;
using UnityEngine;

public enum ShopItemKind
{
    Artifact,
    SmallHeal,
    LargeHeal,
    PermanentBundle,
}

public class ShopSlot
{
    public ShopItemKind Kind;
    public int Price;
    public int Amount;
    public ArtifactDefinition Artifact;
    public bool SoldOut;
}

// 03-3 상점. 최초 진입 때 재고/가격 1회 확정(Stocked), 재방문해도 그대로. 슬롯은 구매하면 매진.
// 구매 확정 = 검증 성공 뒤 가격 차감 + 매진 + 지급을 한 번에 (03-3 §5.3)
public class ShopRoomController : RoomContent
{
    public bool IsStocked { get; private set; }
    public List<ShopSlot> Slots { get; } = new List<ShopSlot>();

    private RoomInteractable npc;

    protected override void OnBound()
    {
        npc = RoomInteractable.Create("ShopNPC", Runtime.transform, Runtime.GetPoint(RoomPointKind.RoomObject), PlaceholderArt.Square, new Color(0.3f, 0.7f, 0.4f), 0.8f, "상점");
        npc.OnInteract = _ => Open();
    }

    public override void OnPlayerEntered(bool first)
    {
        if (!IsStocked) GenerateStock();
    }

    private void Open()
    {
        if (!IsStocked) GenerateStock();
        GameServices.UI?.OpenShop(this);
    }

    // 아티팩트 1 + HP 회복 1 고정, 나머지 1~2칸은 영구재화 묶음/HP 회복 50% 독립 (03-3 §8.1)
    private void GenerateStock()
    {
        IsStocked = true;
        ShopBalance b = GameBalance.Current.Shop;
        System.Random rng = RandomStreams.Loot;
        int total = rng.NextDouble() < b.FourSlotChance ? 4 : 3;

        ArtifactDefinition artifact = GameServices.Artifacts?.DrawAndReserve(AcquisitionOrigin.Shop, null, rng);
        if (artifact != null)
        {
            Vector2Int range = artifact.Grade switch
            {
                ArtifactGrade.Common => b.CommonArtifactPrice,
                ArtifactGrade.Rare => b.RareArtifactPrice,
                _ => b.LegendaryArtifactPrice,
            };
            Slots.Add(new ShopSlot { Kind = ShopItemKind.Artifact, Artifact = artifact, Price = RollPrice(range, rng) });
        }
        else
        {
            Debug.LogError("상점: 진열 아티팩트 예약 실패. 재고 생성 오류 (03-3 §5.2)");
        }

        Slots.Add(CreateHealSlot(rng));
        for (int i = 2; i < total; i++)
        {
            if (rng.NextDouble() < b.ExtraSlotBundleChance)
                Slots.Add(new ShopSlot { Kind = ShopItemKind.PermanentBundle, Amount = b.PermanentBundleAmount, Price = RollPrice(b.PermanentBundlePrice, rng) });
            else
                Slots.Add(CreateHealSlot(rng));
        }
        GameEvents.Raise(GameEvents.ShopStockGenerated, Runtime);
    }

    private ShopSlot CreateHealSlot(System.Random rng)
    {
        ShopBalance b = GameBalance.Current.Shop;
        bool large = rng.NextDouble() < b.LargeHealChance;
        return new ShopSlot
        {
            Kind = large ? ShopItemKind.LargeHeal : ShopItemKind.SmallHeal,
            Amount = large ? b.LargeHealAmount : b.SmallHealAmount,
            Price = RollPrice(large ? b.LargeHealPrice : b.SmallHealPrice, rng),
        };
    }

    // 범위 가운데일수록 자주: 가중치 1 + min(P - Min, Max - P) (03-3 §8.4)
    public static int RollPrice(Vector2Int range, System.Random rng)
    {
        int min = Mathf.Min(range.x, range.y), max = Mathf.Max(range.x, range.y);
        int total = 0;
        for (int p = min; p <= max; p++) total += 1 + Mathf.Min(p - min, max - p);
        int roll = rng.Next(total);
        for (int p = min; p <= max; p++)
        {
            roll -= 1 + Mathf.Min(p - min, max - p);
            if (roll < 0) return p;
        }
        return max;
    }

    public bool CanBuy(ShopSlot slot, out string reason)
    {
        reason = null;
        if (slot.SoldOut) { reason = "매진"; return false; }
        if (GameServices.Wallet == null || GameServices.Wallet.Balance < slot.Price) { reason = "재화 부족"; return false; }
        PlayerController player = GameServices.Player;
        if ((slot.Kind == ShopItemKind.SmallHeal || slot.Kind == ShopItemKind.LargeHeal) && (player == null || player.Health.IsFull))
        {
            reason = "HP 최대";
            return false;
        }
        return true;
    }

    public bool Buy(ShopSlot slot)
    {
        if (!CanBuy(slot, out string reason))
        {
            GameServices.UI?.ShowCenterToast(reason, 0.8f);
            return false;
        }
        if (slot.Kind == ShopItemKind.Artifact)
        {
            // 진열 ID 예약 재검증. 실패하면 재화/매진/장착 그대로, 대체 ID 없음 (03-3 §9)
            ArtifactSystem artifacts = GameServices.Artifacts;
            if (slot.Artifact == null || artifacts == null || !artifacts.IsReserved(slot.Artifact.Id))
            {
                Debug.LogError("상점: 진열 아티팩트 예약 검증 실패");
                GameServices.UI?.ShowCenterToast("구매할 수 없음", 0.8f);
                return false;
            }
        }

        GameServices.Wallet.TrySpend(slot.Price);
        slot.SoldOut = true;
        switch (slot.Kind)
        {
            case ShopItemKind.Artifact:
                // 리롤 불가 플래그: 즉시 지급형은 획득 UI를 거치지 않음 (07 §4.9)
                GameServices.Artifacts.GrantImmediate(slot.Artifact, AcquisitionOrigin.Shop);
                break;
            case ShopItemKind.SmallHeal:
            case ShopItemKind.LargeHeal:
                GameServices.Player.Health.Heal(slot.Amount);
                break;
            case ShopItemKind.PermanentBundle:
                GameServices.Run?.AddPendingPermanentCurrency(slot.Amount);
                break;
        }
        GameEvents.Raise(GameEvents.ShopItemPurchased, Runtime);
        return true;
    }
}
