using UnityEngine;

// 아키타입별 판정과 이동 (04 §6, §9.2, §9.3). Monster FSM이 시점마다 호출함.
public abstract class MonsterBehaviour
{
    protected Monster monster;
    protected MonsterDefinition def;
    protected TelegraphView telegraph;

    public static MonsterBehaviour Create(MonsterArchetype archetype) => archetype switch
    {
        MonsterArchetype.Charger => new ChargerBehaviour(),
        MonsterArchetype.Brawler => new BrawlerBehaviour(),
        MonsterArchetype.Shooter => new ShooterBehaviour(),
        MonsterArchetype.Barrage => new BarrageBehaviour(),
        _ => new SniperBehaviour(),
    };

    public virtual void Init(Monster owner)
    {
        monster = owner;
        def = owner.Definition;
        telegraph = new TelegraphView(owner.transform);
    }

    // 목표 방향형만 방향 잠금 (돌진, 근접 유지, 저격). 탄막은 월드축 고정 (04 §9.2)
    public virtual bool UsesDirectionLock => true;
    public virtual bool StopsMovementWhileExecuting => true;
    public virtual float RecoveryDuration => 0f;

    public abstract bool CanStartAttack();
    public virtual void OnTelegraphStart() { }
    public virtual void TickTelegraph(float dt) { }
    public virtual void OnDirectionLocked() { }
    public virtual bool CanExecute() => true;
    public abstract void BeginExecute();
    // true를 돌려주면 실행 종료
    public virtual bool TickExecute(float dt) => true;
    public virtual void OnExecuteEnd() => telegraph.Hide();
    public virtual void OnCancel() => telegraph.Hide();
    public virtual void OnResumeMovement() { }
    public abstract Vector2 DesiredVelocity(float dt);

    // ---------- 공용 도우미 ----------

    protected Vector2 Pos => monster.Position;
    protected bool HasPlayer => monster.Player != null && !monster.Player.Health.IsDead;
    protected Vector2 PlayerPos => monster.Player != null ? monster.Player.Position : Pos;
    protected Vector2 ToPlayer => PlayerPos - Pos;
    protected Vector2 AimDirection => ToPlayer.sqrMagnitude > 0.0001f ? ToPlayer.normalized : Vector2.right;

    // 논리 발사점: 몬스터 방향을 따르는 전용 지점 (04 §9.4)
    protected Vector2 Muzzle(Vector2 dir) => Pos + dir.normalized * def.BodyRadius;

    protected float DistanceFromMuzzle => Vector2.Distance(Muzzle(AimDirection), PlayerPos);

    protected bool LineOfSightFrom(Vector2 from) => Geometry2D.HasLineOfSight(from, PlayerPos);

    protected Vector2 ApproachVelocity()
    {
        if (!HasPlayer) return Vector2.zero;
        return monster.DirectionTowards(PlayerPos) * def.MoveSpeed;
    }
}

// ---------------- 근접 ----------------

// 돌진형: 거리 조건 만족 시 예고 → 잠근 방향 직선 돌진 → 회복 (04 §6.1). LOS 검사 안 함.
public class ChargerBehaviour : MonsterBehaviour
{
    private float dashTravelled;
    private bool dashHit;

    public override float RecoveryDuration => def.PostDashRecovery;

    public override bool CanStartAttack() => HasPlayer && ToPlayer.magnitude <= def.ChargerTriggerDistance;

    public override void OnTelegraphStart()
    {
        monster.LockedDirection = AimDirection;
        telegraph.ShowLine(Pos, Pos + monster.LockedDirection * def.DashDistance, new Color(1f, 0.3f, 0.2f, 0.5f), 0.12f, true);
    }

    public override void TickTelegraph(float dt)
    {
        if (!monster.IsDirectionLocked) monster.LockedDirection = AimDirection;
        bool locked = monster.IsDirectionLocked;
        telegraph.ShowLine(Pos, Pos + monster.LockedDirection * def.DashDistance, new Color(1f, 0.3f, 0.2f, locked ? 0.9f : 0.45f), locked ? 0.2f : 0.12f, true);
    }

    public override void BeginExecute()
    {
        telegraph.Hide();
        dashTravelled = 0f;
        dashHit = false;
        monster.IsDashing = true;
    }

    public override bool TickExecute(float dt)
    {
        float step = Mathf.Min(def.DashSpeed * dt, def.DashDistance - dashTravelled);
        Vector2 moved = monster.MoveBy(monster.LockedDirection * step, out bool hitWall);
        dashTravelled += step;

        PlayerController player = monster.Player;
        if (!dashHit && player != null && Vector2.Distance(Pos, player.Position) <= def.BodyRadius + player.HurtboxRadius)
        {
            dashHit = true;
            monster.HitPlayerWithAttack(def.DashDamage);
            return true; // 플레이어에서 돌진 종료
        }
        return hitWall || moved.magnitude < step * 0.3f || dashTravelled >= def.DashDistance - 0.001f;
    }

    public override void OnExecuteEnd()
    {
        base.OnExecuteEnd();
        monster.IsDashing = false;
    }

    public override void OnCancel()
    {
        base.OnCancel();
        monster.IsDashing = false;
    }

    public override Vector2 DesiredVelocity(float dt) => ApproachVelocity();
}

// 근접 유지형: 유지 거리 안에서 짧은 예고 뒤 부채꼴 타격. 실행 시 꼭짓점→플레이어 LOS 막히면 빗나감 (04 §6.2)
public class BrawlerBehaviour : MonsterBehaviour
{
    public override bool CanStartAttack()
        => HasPlayer && ToPlayer.magnitude <= def.BrawlerAttackRange && LineOfSightFrom(Pos);

    public override void OnTelegraphStart()
    {
        monster.LockedDirection = AimDirection;
        DrawSector(false);
    }

    public override void TickTelegraph(float dt)
    {
        if (!monster.IsDirectionLocked) monster.LockedDirection = AimDirection;
        DrawSector(monster.IsDirectionLocked);
    }

    private void DrawSector(bool locked)
        => telegraph.ShowSector(Pos, monster.LockedDirection, def.BrawlerAttackRange, def.BrawlerAttackArc, new Color(1f, 0.4f, 0.2f, locked ? 0.9f : 0.45f));

    public override void BeginExecute()
    {
        telegraph.Hide();
        PlayerController player = monster.Player;
        if (player == null) return;
        bool inSector = Geometry2D.SectorIntersectsCircle(Pos, monster.LockedDirection, def.BrawlerAttackRange, def.BrawlerAttackArc, player.Position, player.HurtboxRadius);
        if (inSector && LineOfSightFrom(Pos))
            monster.HitPlayerWithAttack(def.BrawlerAttackDamage);
        GameServices.Feedback?.ShowSlash(Pos, monster.LockedDirection, def.BrawlerAttackRange, def.BrawlerAttackArc);
    }

    // 유지 거리 구간: 멀면 다가가고 가까우면 물러남
    public override Vector2 DesiredVelocity(float dt)
    {
        if (!HasPlayer) return Vector2.zero;
        float distance = ToPlayer.magnitude;
        if (distance > def.KeepDistanceBand.y) return monster.DirectionTowards(PlayerPos) * def.MoveSpeed;
        if (distance < def.KeepDistanceBand.x) return -AimDirection * def.MoveSpeed;
        return Vector2.zero;
    }
}
