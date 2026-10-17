using UnityEngine;

// 샷건: 즉시 원추 판정 (01-2 §5.2, §8.5)
// - 원추와 몬스터 피격 판정이 경계에서 닿기만 해도 명중
// - 논리 총구 → 대상 몸체 중심 선분이 벽에 막히면 제외
// - 한 발사는 여러 명을 맞혀도 같은 AttackExecutionID, 같은 피해/치명타
public class ConeFireMode : IWeaponFireMode
{
    public void Fire(in AttackSnapshot snapshot, Vector2 origin, Vector2 direction, GameObject projectilePrefab)
    {
        float range = snapshot.Range;
        float arc = snapshot.ConeAngle;

        foreach (ICombatTarget target in CombatTargets.All.ToArraySafe())
        {
            if (target == null || !target.CanBeHit) continue;
            if (!Geometry2D.SectorIntersectsCircle(origin, direction, range, arc, target.Position, target.HitRadius)) continue;
            if (!Geometry2D.HasLineOfSight(origin, target.Position)) continue;

            Vector2 knockDir = (target.Position - origin).normalized;
            DamageInfo info = snapshot.ToDamage(target.Position, knockDir);
            ArtifactEffectRunner.Instance?.ModifyPlayerHit(ref info, target);
            target.TakeDamage(info);
            GameEvents.Raise(GameEvents.ShotgunConeHit, info);
            if (info.IsCritical) GameEvents.Raise(GameEvents.CriticalHit, info);
        }

        // 원추 중심선이 벽에 닿으면 벽 피격으로 처리 (비밀 벽 발견 경로, 03-5 §4)
        RaycastHit2D wall = Physics2D.Raycast(origin, direction, range, GameLayers.WallMask);
        if (wall.collider != null)
        {
            GameEvents.Raise(GameEvents.WallAttacked);
            SecretWall.TryBreakAt(wall.point, direction, wall.collider);
        }

        GameServices.Feedback?.ShowShotgunCone(origin, direction, range, arc);
    }
}

public static class ListExtensions
{
    // 순회 중 목록이 바뀌어도 안전하게 복사본으로 순회
    public static T[] ToArraySafe<T>(this System.Collections.Generic.IReadOnlyList<T> list)
    {
        var array = new T[list.Count];
        for (int i = 0; i < list.Count; i++) array[i] = list[i];
        return array;
    }
}
