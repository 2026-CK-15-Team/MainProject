using System.Collections.Generic;
using UnityEngine;

// 분열 설정 (05 §8.2 4번 분열탄). 분열 거리까지 날거나 그 전에 벽/플레이어에 닿으면 그 자리에서 분열
public struct ProjectileSplit
{
    public float Distance;
    public int Count;            // 월드축 균등 (0도부터)
    public EnemyProjectileSpec Child;
    public Color ChildColor;
}

// 몬스터/보스 투사체 (04 §9.4). 생성 순간 값을 스냅샷으로 가짐.
// 벽, 플레이어(무적 중 포함), 최대 이동거리 중 하나에 닿으면 소멸. 다른 몬스터와는 충돌 안 함.
public class EnemyProjectile : MonoBehaviour
{
    // 권총 상쇄 대상 아님 (05 §8.3 분열탄의 큰 탄). 권총 탄은 통과
    public bool Uncancelable { get; set; }
    // 같은 값의 공격은 저스트회피가 1회만 발동 (05 §8.3 보스 패턴 회차 ID). 0이면 제한 없음
    public int AttackGroupId { get; set; }
    public ProjectileSplit? Split { get; set; }

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
        if (Split.HasValue) step = Mathf.Min(step, Split.Value.Distance - travelled);
        Vector2 from = transform.position;
        Vector2 to = from + direction * step;

        // 권총 도탄 상쇄는 같은 틱 플레이어 피격보다 먼저 (01-3 §7.2 1번)
        IReadOnlyList<Projectile> bullets = Projectile.Active;
        for (int i = Uncancelable ? -1 : bullets.Count - 1; i >= 0; i--)
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
            Vector2 at = from + direction * wall.distance;
            GameServices.Feedback?.SpawnWallImpact(at);
            if (Split.HasValue) SplitAt(at);
            Destroy(gameObject);
            return;
        }
        if (playerT <= 1f)
        {
            player.Health.ReceiveAttack(new IncomingAttack { Attacker = OwnerMonster, Owner = Owner, Damage = spec.Damage, IsContact = false, AttackGroupId = AttackGroupId });
            if (Split.HasValue) SplitAt(from + (to - from) * playerT);
            Vanish();
            return;
        }

        transform.position = to;
        travelled += step;
        if (Split.HasValue && travelled >= Split.Value.Distance - 1e-4f)
        {
            SplitAt(to);
            RemoveSilently();
            return;
        }
        if (travelled >= spec.MaxDistance - 1e-4f)
            Vanish();
    }

    // 자식은 소유자와 회차 ID를 승계 → 보스 처치 때 RemoveOwnedBy로 함께 제거. 자식은 상쇄 규칙을 따름
    private void SplitAt(Vector2 at)
    {
        ProjectileSplit split = Split.Value;
        Split = null;
        int count = Mathf.Max(1, split.Count);
        for (int i = 0; i < count; i++)
        {
            EnemyProjectile child = Spawn(Owner, at, Geometry2D.FromAngle(360f / count * i), split.Child, split.ChildColor);
            child.AttackGroupId = AttackGroupId;
        }
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
