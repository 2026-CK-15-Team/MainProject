using System.Collections.Generic;
using UnityEngine;

public enum RewardKind
{
    Currency,
    Heal,
}

// 필드 보상 (런전용 재화, HP 회복). 접촉하면 자동 획득 (01-1 §3 F 비고).
// 생성 연출 시작 뒤 RewardPickup_EnableDelay가 지나야 획득 판정 켜짐 (03-1 §8.4).
// HP: 만피면 획득하지 않고 필드에 남음, 초과 회복은 버림 (03-1 §9).
public class RewardPickup : MonoBehaviour
{
    private static readonly List<RewardPickup> active = new List<RewardPickup>();
    public static IReadOnlyList<RewardPickup> Active => active;

    public RewardKind Kind { get; private set; }
    public int Amount { get; private set; }
    public RoomRuntime Room { get; private set; }

    private float enableAt;
    private const float PickupRadius = 0.25f;

    public static RewardPickup Spawn(RewardKind kind, int amount, Vector2 position, RoomRuntime room)
    {
        var go = new GameObject(kind == RewardKind.Currency ? "CurrencyPickup" : "HealPickup");
        go.transform.position = position;
        if (room != null) go.transform.SetParent(room.PickupRoot, true);
        var pickup = go.AddComponent<RewardPickup>();
        pickup.Kind = kind;
        pickup.Amount = amount;
        pickup.Room = room;
        pickup.enableAt = GameClock.CombatTime + GameBalance.Current.CombatRoom.RewardPickupEnableDelay;
        Color color = kind == RewardKind.Currency ? new Color(1f, 0.85f, 0.2f) : new Color(1f, 0.35f, 0.45f);
        float size = kind == RewardKind.Heal && amount >= GameBalance.Current.CombatRoom.LargeHPAmount ? 0.45f : 0.32f; // 소량/대량 구분
        PlaceholderArt.CreateShape("Visual", go.transform, kind == RewardKind.Currency ? PlaceholderArt.Coin : PlaceholderArt.Heart, color, size, 82);
        GameServices.Feedback?.SpawnRewardBurst(position, color);
        return pickup;
    }

    private void OnEnable() => active.Add(this);
    private void OnDisable() => active.Remove(this);

    private void Update()
    {
        if (GameClock.CombatTime < enableAt || GameClock.CombatDelta <= 0f) return;
        PlayerController player = GameServices.Player;
        if (player == null || player.Health.IsDead) return;
        if (Vector2.Distance(player.Position, transform.position) > player.BodyRadius + PickupRadius) return;

        if (Kind == RewardKind.Currency)
        {
            GameServices.Wallet?.Add(Amount);
        }
        else
        {
            if (player.Health.IsFull) return;
            player.Health.Heal(Amount);
        }
        GameServices.Feedback?.SpawnRewardBurst(transform.position, Color.white);
        Destroy(gameObject);
    }
}
