using System.Collections.Generic;
using UnityEngine;

// 원거리형 3종 공통 이동 (04 §9.3)
// - 유지 거리 밖: 다가가거나 물러남 / 안: 스트레이프 (공전 아님)
// - 스트레이프: 플레이어 수직 좌우, 무작위 간격으로 뒤집기, 막히면 즉시 뒤집기, 시야 예측
// - 시야가 막힌 조준 사격형/탄막 패턴형은 거리 대역 안 + 시야 열리는 지점으로 재배치
public abstract class RangedBehaviour : MonsterBehaviour
{
    private bool strafing;
    private int strafeSign = 1;
    private float strafeFlipAt;
    private Vector2? repositionTarget;
    private float repositionSearchAt;

    protected virtual bool NeedsLineOfSight => true;
    protected virtual bool UsesSightProbe => true;

    public override void OnResumeMovement() => strafing = false;

    public override Vector2 DesiredVelocity(float dt)
    {
        if (!HasPlayer) return Vector2.zero;
        Vector2 aim = AimDirection;
        float distance = DistanceFromMuzzle;

        if (NeedsLineOfSight && !LineOfSightFrom(Muzzle(aim)))
        {
            strafing = false;
            return RepositionVelocity();
        }
        repositionTarget = null;

        if (distance > def.KeepDistanceBand.y)
        {
            strafing = false;
            return monster.DirectionTowards(PlayerPos) * def.MoveSpeed;
        }
        if (distance < def.KeepDistanceBand.x)
        {
            strafing = false;
            return -aim * def.MoveSpeed;
        }
        return StrafeVelocity(aim, distance);
    }

    private Vector2 StrafeVelocity(Vector2 aim, float distance)
    {
        System.Random rng = RandomStreams.Combat;
        if (!strafing)
        {
            strafing = true;
            strafeSign = rng.NextDouble() < 0.5 ? 1 : -1;
            strafeFlipAt = monster.Now + rng.Range(def.StrafeFlipInterval.x, def.StrafeFlipInterval.y);
            if (IsBlocked(Perp(aim) * strafeSign)) strafeSign = -strafeSign;
        }
        if (monster.Now >= strafeFlipAt) Flip(rng);

        Vector2 side = Perp(aim) * strafeSign;
        if (IsBlocked(side) || (UsesSightProbe && !ProbeSight(side)))
        {
            Flip(rng);
            side = Perp(aim) * strafeSign;
            // 양쪽 다 막히면 게걸음 없이 거리 유지만
            if (IsBlocked(side)) return Vector2.zero;
        }

        // 거리 구간 보정 합성: 너무 가까우면 뒤로, 멀면 앞으로
        float mid = (def.KeepDistanceBand.x + def.KeepDistanceBand.y) * 0.5f;
        float half = Mathf.Max(0.01f, (def.KeepDistanceBand.y - def.KeepDistanceBand.x) * 0.5f);
        float correction = Mathf.Clamp((distance - mid) / half, -1f, 1f) * 0.5f;
        Vector2 dir = (side + aim * correction).normalized;
        return dir * def.MoveSpeed;
    }

    private void Flip(System.Random rng)
    {
        strafeSign = -strafeSign;
        strafeFlipAt = monster.Now + rng.Range(def.StrafeFlipInterval.x, def.StrafeFlipInterval.y);
    }

    private static Vector2 Perp(Vector2 v) => new Vector2(-v.y, v.x);

    private bool IsBlocked(Vector2 dir)
        => Physics2D.CircleCast(Pos, def.BodyRadius * 0.9f, dir, 0.2f, GameLayers.WallMask).collider != null;

    private bool ProbeSight(Vector2 side)
    {
        Vector2 probe = Pos + side * def.StrafeSightProbeDistance;
        return Geometry2D.HasLineOfSight(probe + AimDirection * def.BodyRadius, PlayerPos);
    }

    // 거리 대역 안이면서 시야가 열리는 도달 가능 지점으로 이동. 없으면 가장 가까운 도달 지점(플레이어 쪽)
    private Vector2 RepositionVelocity()
    {
        if (monster.Now >= repositionSearchAt)
        {
            repositionSearchAt = monster.Now + def.RepathInterval;
            repositionTarget = FindRepositionPoint();
        }
        Vector2 target = repositionTarget ?? PlayerPos;
        if ((target - Pos).sqrMagnitude < 0.01f) return Vector2.zero;
        return monster.DirectionTowards(target) * def.MoveSpeed;
    }

    private Vector2? FindRepositionPoint()
    {
        RoomNavGrid nav = monster.Nav;
        if (nav == null) return null;
        Vector2 player = PlayerPos;
        Vector2? best = null;
        float bestScore = float.MaxValue;
        foreach (Vector2Int cell in nav.WalkableCells())
        {
            Vector2 center = nav.CellCenter(cell);
            float d = Vector2.Distance(center, player);
            if (d < def.KeepDistanceBand.x || d > def.KeepDistanceBand.y) continue;
            float score = (center - Pos).sqrMagnitude;
            if (score >= bestScore) continue;
            if (!Geometry2D.HasLineOfSight(center, player)) continue;
            best = center;
            bestScore = score;
        }
        return best;
    }
}

// 조준 사격형: 예고 없음. 첫 발 시각에 사거리+시야 검사 → 단발/연발 50:50 (04 §6.3, §9.2)
public class ShooterBehaviour : RangedBehaviour
{
    private bool isBurst;
    private int shotsFired;
    private float nextShotAt;

    public override bool UsesDirectionLock => false;
    public override bool StopsMovementWhileExecuting => isBurst;

    public override bool CanStartAttack()
        => HasPlayer && DistanceFromMuzzle <= def.AttackMaxDistance && LineOfSightFrom(Muzzle(AimDirection));

    public override void BeginExecute()
    {
        isBurst = RandomStreams.Combat.NextDouble() < 0.5;
        shotsFired = 0;
        FireShot();
        nextShotAt = monster.Now + def.BurstGap;
    }

    public override bool TickExecute(float dt)
    {
        if (!isBurst) return true;
        // 둘째 발부터는 사거리, 시야 검사 없이 끝까지 쏨
        if (monster.Now >= nextShotAt)
        {
            FireShot();
            nextShotAt = monster.Now + def.BurstGap;
        }
        return shotsFired >= 3;
    }

    public override void OnResumeMovement()
    {
        // 단발은 이동을 멈추지 않으므로 스트레이프 방향/타이머 유지
        if (isBurst) base.OnResumeMovement();
    }

    private void FireShot()
    {
        PlayerController player = monster.Player;
        if (player == null) return;
        shotsFired++;
        Vector2 target = player.Position;
        EnemyProjectileSpec spec = isBurst ? def.BurstProjectile : def.SingleProjectile;

        // 연발 셋째 발만 부분 선행 조준
        if (isBurst && shotsFired == 3)
        {
            Vector2 muzzle = Muzzle(AimDirection);
            float dist = Vector2.Distance(muzzle, target);
            target += player.Movement.ActualVelocity * (dist / Mathf.Max(0.01f, spec.Speed)) * def.BurstLeadRatio;
        }
        Vector2 dir = (target - Pos).normalized;
        if (dir == Vector2.zero) dir = Vector2.right;
        EnemyProjectile.Spawn(monster, Muzzle(dir), dir, spec, new Color(1f, 0.35f, 0.35f));
        GameServices.Feedback?.ShowMuzzleFlash(Muzzle(dir), dir);
    }
}

// 탄막 패턴형: 월드축 0도 시작 45도 간격 8발 / 다음 차례는 +22.5도. 취소된 공격은 차례를 넘기지 않음 (04 §9.4)
public class BarrageBehaviour : RangedBehaviour
{
    private bool secondPattern;
    private float pulse;

    public override bool UsesDirectionLock => false;

    private bool InRangeAndSight => HasPlayer && DistanceFromMuzzle <= def.AttackMaxDistance && LineOfSightFrom(Muzzle(AimDirection));

    public override bool CanStartAttack() => InRangeAndSight;
    public override bool CanExecute() => InRangeAndSight;

    private float PatternOffset => secondPattern ? 22.5f : 0f;

    public override void OnTelegraphStart()
    {
        pulse = 0f;
        DrawPattern();
    }

    public override void TickTelegraph(float dt)
    {
        pulse += dt;
        DrawPattern();
    }

    private void DrawPattern()
    {
        int count = Mathf.Max(1, def.BarrageProjectileCount);
        float step = 360f / count;
        var segments = new List<(Vector2, Vector2)>();
        for (int i = 0; i < count; i++)
        {
            Vector2 dir = Geometry2D.FromAngle(PatternOffset + step * i);
            segments.Add((Pos + dir * def.BodyRadius, Pos + dir * (def.BodyRadius + 1.2f)));
        }
        float alpha = 0.35f + 0.35f * Mathf.Abs(Mathf.Sin(pulse * 12f));
        telegraph.ShowSegments(segments, new Color(0.4f, 1f, 0.8f, alpha), 0.06f);
        telegraph.Pulse(monster.BodyRenderer, pulse);
    }

    public override void BeginExecute()
    {
        telegraph.Hide();
        int count = Mathf.Max(1, def.BarrageProjectileCount);
        float step = 360f / count;
        for (int i = 0; i < count; i++)
        {
            Vector2 dir = Geometry2D.FromAngle(PatternOffset + step * i);
            EnemyProjectile.Spawn(monster, Pos + dir * def.BodyRadius, dir, def.BarrageProjectile, new Color(0.5f, 1f, 0.85f));
        }
        secondPattern = !secondPattern;
    }
}

// 예고 후 저격형: 긴 조준 예고 → 잠금 → 벽 무시 즉발 선분 1회 판정 (04 §6.3, §9.4)
public class SniperBehaviour : RangedBehaviour
{
    protected override bool NeedsLineOfSight => false;
    protected override bool UsesSightProbe => false;

    private bool InRange => HasPlayer && DistanceFromMuzzle <= def.AttackMaxDistance;

    public override bool CanStartAttack() => InRange;
    public override bool CanExecute() => InRange;

    public override void OnTelegraphStart()
    {
        monster.LockedDirection = AimDirection;
        Draw();
    }

    public override void TickTelegraph(float dt)
    {
        if (!monster.IsDirectionLocked) monster.LockedDirection = AimDirection;
        Draw();
    }

    // 위험 표시는 실제 선분과 같은 길이, 폭은 표시 폭. 판정을 넓히지 않음
    private void Draw()
    {
        bool locked = monster.IsDirectionLocked;
        Vector2 from = Muzzle(monster.LockedDirection);
        Vector2 to = from + monster.LockedDirection * def.AttackMaxDistance;
        telegraph.ShowLine(from, to, new Color(1f, 0.9f, 0.2f, locked ? 0.85f : 0.3f), locked ? def.SniperHitscanWidth : def.SniperHitscanWidth * 0.3f, false);
    }

    public override void BeginExecute()
    {
        telegraph.Hide();
        Vector2 from = Muzzle(monster.LockedDirection);
        Vector2 to = from + monster.LockedDirection * def.AttackMaxDistance;
        PlayerController player = monster.Player;
        if (player != null && Geometry2D.SegmentIntersectsCircle(from, to, player.Position, player.HurtboxRadius))
            monster.HitPlayerWithAttack(def.SniperDamage);
        GameServices.Feedback?.ShowHitscan(from, to);
    }
}
