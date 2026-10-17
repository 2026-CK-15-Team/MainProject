using System.Collections.Generic;
using UnityEngine;

// 몬스터/보스 투사체 (04 §9.4). 생성 순간 값을 스냅샷으로 가짐.
// 벽, 플레이어(무적 중 포함), 최대 이동거리 중 하나에 닿으면 소멸. 다른 몬스터와는 충돌 안 함.
public class EnemyProjectile : MonoBehaviour
{
    private static readonly List<EnemyProjectile> active = new List<EnemyProjectile>();
    private static int spawnCounter;

    public Object Owner { get; private set; }
    public Monster OwnerMonster { get; private set; }
    public int SpawnSequence { get; private set; }

    private Vector2 direction;
    private EnemyProjectileSpec spec;
    private float travelled;

    public static IReadOnlyList<EnemyProjectile> Active => active;
    public float Radius => spec.Radius;

    public static EnemyProjectile Spawn(Object owner, Vector2 origin, Vector2 direction, EnemyProjectileSpec spec, Color color)
    {
        var go = new GameObject("EnemyProjectile");
        go.transform.position = origin;
        PlaceholderArt.CreateShape("Visual", go.transform, PlaceholderArt.Circle, color, Mathf.Max(0.12f, spec.Radius * 2.4f), 210);
        var projectile = go.AddComponent<EnemyProjectile>();
        projectile.Owner = owner;
        projectile.OwnerMonster = owner as Monster;
        projectile.direction = direction.normalized;
        projectile.spec = spec;
        projectile.SpawnSequence = spawnCounter++;
        return projectile;
    }

    // 공격 주체 사망 시 소유 투사체 즉시 제거 (04 §3.2). 공통 소멸 표현 출력 (09 §7)
    public static void RemoveOwnedBy(Object owner)
    {
        for (int i = active.Count - 1; i >= 0; i--)
        {
            EnemyProjectile p = active[i];
            if (p != null && p.Owner == owner) p.Vanish();
        }
    }

    public static void ClearAll()
    {
        for (int i = active.Count - 1; i >= 0; i--)
        {
            if (active[i] != null) Destroy(active[i].gameObject);
        }
        active.Clear();
    }

    private void OnEnable() => active.Add(this);
    private void OnDisable() => active.Remove(this);

    private void Update()
    {
        float dt = GameClock.CombatDelta;
        if (dt <= 0f || removed) return;

        float step = Mathf.Min(spec.Speed * dt, spec.MaxDistance - travelled);
        Vector2 from = transform.position;
        Vector2 to = from + direction * step;

        // 권총 도탄 상쇄는 같은 틱 플레이어 피격보다 먼저 (01-3 §7.2 1번)
        IReadOnlyList<Projectile> bullets = Projectile.Active;
        for (int i = bullets.Count - 1; i >= 0; i--)
        {
            Projectile bullet = bullets[i];
            if (bullet == null || bullet.IsRemoved || !bullet.CanCancelEnemyProjectiles) continue;
            if (Geometry2D.SweepCircle(from, to, spec.Radius, bullet.Position, bullet.HitRadius) < 0f) continue;
            bullet.CancelWith(this);
            return;
        }

        // 벽
        RaycastHit2D wall = Physics2D.CircleCast(from, spec.Radius, direction, step, GameLayers.WallMask);
        float wallT = wall.collider != null && step > 0f ? wall.distance / step : 2f;

        // 플레이어
        PlayerController player = GameServices.Player;
        float playerT = 2f;
        if (player != null && !player.Health.IsDead)
        {
            float t = Geometry2D.SweepCircle(from, to, spec.Radius, player.Position, player.HurtboxRadius);
            if (t >= 0f) playerT = t;
        }

        if (wallT <= 1f && wallT <= playerT)
        {
            GameServices.Feedback?.SpawnWallImpact(from + direction * wall.distance);
            Destroy(gameObject);
            return;
        }
        if (playerT <= 1f)
        {
            player.Health.ReceiveAttack(new IncomingAttack { Attacker = OwnerMonster, Owner = Owner, Damage = spec.Damage, IsContact = false });
            Vanish();
            return;
        }

        transform.position = to;
        travelled += step;
        if (travelled >= spec.MaxDistance - 1e-4f)
            Vanish();
    }

    // 상쇄 연출은 호출한 쪽이 냄
    public void RemoveSilently()
    {
        removed = true;
        Destroy(gameObject);
    }

    // Destroy는 프레임 끝에 처리되므로 같은 프레임 남은 Update에서 판정하지 않게
    private bool removed;
    public bool IsRemoved => removed;

    public void Vanish()
    {
        GameServices.Feedback?.SpawnVanish(transform.position);
        Destroy(gameObject);
    }
}
