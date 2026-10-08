using System.Collections.Generic;
using UnityEngine;

// 플레이어 공격이 맞힐 수 있는 대상 (몬스터, 보스, 더미).
// 판정은 물리 콜백이 아니라 이 목록을 수학으로 검사함 → 순회 순서와 무관하게 결과 고정 (01-3 §7.2).
public interface ICombatTarget : IDamageable
{
    Vector2 Position { get; }
    float HitRadius { get; }
    bool CanBeHit { get; }
    // 같은 거리 동률일 때 작은 쪽 우선 (01-2 §8.6 CombatSpawnOrder)
    int SpawnOrder { get; }
}

public static class CombatTargets
{
    private static readonly List<ICombatTarget> targets = new List<ICombatTarget>();

    public static IReadOnlyList<ICombatTarget> All => targets;

    public static void Register(ICombatTarget target)
    {
        if (!targets.Contains(target))
            targets.Add(target);
    }

    public static void Unregister(ICombatTarget target) => targets.Remove(target);
}
