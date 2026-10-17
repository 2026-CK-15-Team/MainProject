using System.Collections.Generic;
using UnityEngine;

// 권총/라이플 플레이어 투사체 (01-2 §5.1, §8.5, §8.6).
// - 이전 위치→이번 위치를 반경 포함 연속 검사, 가장 먼저 닿는 것 하나만
// - 같은 거리(Collision_TieDistanceEpsilon 이내)면 벽 > 몬스터, 몬스터끼리는 CombatSpawnOrder 작은 쪽
// - 벽은 관통과 무관하게 항상 막음. 최대 사거리 경계에 닿은 적은 먼저 명중 처리
public class Projectile : MonoBehaviour
{
    private static readonly List<Projectile> active = new List<Projectile>();

    private AttackSnapshot snapshot;
    private Vector2 direction;
    private float travelled;

    // 권총 세트 도탄: 적 탄과 부딪히면 둘 다 사라짐 (투사체 상쇄). 발사 때 확률로 한 번 정함
    public bool CanCancelEnemyProjectiles { get; private set; }
    public Vector2 Position => transform.position;
    public float HitRadius => snapshot.HitRadius;
    public static IReadOnlyList<Projectile> Active => active;

    public static void Spawn(in AttackSnapshot snapshot, Vector2 origin, Vector2 direction, GameObject prefab)
    {
        GameObject go = prefab != null ? Instantiate(prefab, origin, Quaternion.identity) : CreatePlaceholder(origin, snapshot.Weapon);
        Projectile projectile = go.GetComponent<Projectile>();
        if (projectile == null) projectile = go.AddComponent<Projectile>();
        projectile.Launch(snapshot, direction);
    }

    // 순간이동, 층 전환 때 남은 플레이어 투사체 제거 (08 §5.1 6번, 03 §6.3 2번)
    public static void ClearAll()
    {
        for (int i = active.Count - 1; i >= 0; i--)
        {
            if (active[i] != null) Destroy(active[i].gameObject);
        }
        active.Clear();
    }

    private static GameObject CreatePlaceholder(Vector2 origin, WeaponType weapon)
    {
        var go = new GameObject("PlayerProjectile");
        go.transform.position = origin;
        Color color = weapon == WeaponType.Rifle ? new Color(1f, 0.95f, 0.5f) : new Color(1f, 0.8f, 0.3f);
        PlaceholderArt.CreateShape("Visual", go.transform, PlaceholderArt.Circle, color, 0.24f, 200);
        return go;
    }

    private void Awake()
    {
        // 예전 프리팹의 물리 컴포넌트는 판정에 안 씀
        if (TryGetComponent(out Rigidbody2D body)) body.bodyType = RigidbodyType2D.Kinematic;
        foreach (Collider2D col in GetComponents<Collider2D>()) col.enabled = false;
    }

    private void OnEnable() => active.Add(this);
    private void OnDisable() => active.Remove(this);

    // 예전 코드 호환용
    public void Launch(Vector2 dir, float projectileSpeed, float dmg)
    {
        Launch(new AttackSnapshot { Damage = Mathf.RoundToInt(dmg), ProjectileSpeed = projectileSpeed, Range = 10f, HitRadius = 0.12f, AttackExecutionId = AttackSnapshot.NextId() }, dir);
    }

    public void Launch(in AttackSnapshot attack, Vector2 dir)
    {
        snapshot = attack;
        direction = dir.normalized;
        travelled = 0f;
        transform.right = direction;
        CanCancelEnemyProjectiles = attack.Weapon == WeaponType.Pistol && ArtifactEffectRunner.Instance != null && ArtifactEffectRunner.Instance.RollRicochet();
    }

    private void Update()
    {
        float dt = GameClock.CombatDelta;
        if (dt <= 0f || IsRemoved) return;

        float remaining = snapshot.Range - travelled;
        float step = Mathf.Min(snapshot.ProjectileSpeed * dt, remaining);
        Vector2 from = transform.position;
        Vector2 to = from + direction * step;

        if (CanCancelEnemyProjectiles && TryCancelEnemyProjectile(from, to)) return;
        if (ResolveCollision(from, to, step)) return;

        transform.position = to;
        travelled += step;
        if (travelled >= snapshot.Range - 1e-4f)
            Destroy(gameObject);
    }

    // 충돌 처리했으면 true (소멸)
    private bool ResolveCollision(Vector2 from, Vector2 to, float step)
    {
        float epsilon = GameBalance.Current.Weapon.CollisionTieEpsilon;
        float radius = snapshot.HitRadius;

        // 벽 후보
        float wallDistance = float.MaxValue;
        RaycastHit2D wallHit = default;
        if (step > 0f)
        {
            wallHit = Physics2D.CircleCast(from, radius, direction, step, GameLayers.WallMask);
            if (wallHit.collider != null) wallDistance = wallHit.distance;
        }

        // 몬스터 후보
        ICombatTarget bestTarget = null;
        float bestTargetDistance = float.MaxValue;
        foreach (ICombatTarget target in CombatTargets.All)
        {
            if (target == null || !target.CanBeHit) continue;
            float t = Geometry2D.SweepCircle(from, to, radius, target.Position, target.HitRadius);
            if (t < 0f) continue;
            float distance = t * step;
            bool better = distance < bestTargetDistance - epsilon
                || (Mathf.Abs(distance - bestTargetDistance) <= epsilon && bestTarget != null && target.SpawnOrder < bestTarget.SpawnOrder);
            if (better)
            {
                bestTarget = target;
                bestTargetDistance = distance;
            }
        }

        // 동률이면 벽 우선
        if (wallHit.collider != null && wallDistance <= bestTargetDistance + epsilon)
        {
            Vector2 point = from + direction * wallDistance;
            OnWallHit(wallHit, point);
            return true;
        }

        if (bestTarget != null)
        {
            Vector2 point = from + direction * bestTargetDistance;
            DamageInfo info = snapshot.ToDamage(point, direction);
            ArtifactEffectRunner.Instance?.ModifyPlayerHit(ref info, bestTarget);
            bestTarget.TakeDamage(info);
            GameEvents.Raise(GameEvents.ProjectileHit, info);
            if (info.IsCritical) GameEvents.Raise(GameEvents.CriticalHit, info);
            Destroy(gameObject);
            return true;
        }
        return false;
    }

    // 이번 이동 구간에 닿는 적 탄이 있으면 둘 다 제거
    private bool TryCancelEnemyProjectile(Vector2 from, Vector2 to)
    {
        IReadOnlyList<EnemyProjectile> enemies = EnemyProjectile.Active;
        for (int i = enemies.Count - 1; i >= 0; i--)
        {
            EnemyProjectile enemy = enemies[i];
            if (enemy == null || enemy.IsRemoved) continue;
            if (Geometry2D.SweepCircle(from, to, snapshot.HitRadius, enemy.transform.position, enemy.Radius) < 0f) continue;
            CancelWith(enemy);
            return true;
        }
        return false;
    }

    // 상쇄 확정: 권총 탄과 적 탄 각각 하나씩 제거 + 상쇄 연출 (09 §5 6번)
    public bool IsRemoved { get; private set; }

    public void CancelWith(EnemyProjectile enemy)
    {
        IsRemoved = true;
        Vector2 point = ((Vector2)transform.position + (Vector2)enemy.transform.position) * 0.5f;
        GameServices.Feedback?.SpawnProjectileCancel(point);
        enemy.RemoveSilently();
        Destroy(gameObject);
    }

    private void OnWallHit(RaycastHit2D hit, Vector2 point)
    {
        GameEvents.Raise(GameEvents.WallAttacked);
        SecretWall.TryBreakAt(hit.point, direction, hit.collider);
        GameServices.Feedback?.SpawnWallImpact(point);
        Destroy(gameObject);
    }
}
