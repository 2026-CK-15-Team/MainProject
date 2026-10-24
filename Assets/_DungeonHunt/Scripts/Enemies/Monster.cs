using System.Collections.Generic;
using UnityEngine;

public enum MonsterState
{
    Inactive,
    ActiveMovement,
    Armed,
    AttackTelegraph,
    DirectionLocked,
    AttackExecute,
    PostDashRecovery,
    Dead,
}

// 04 몬스터 공통 FSM. 실제 공격 시각을 먼저 예약하고 예고 시작 시각을 거꾸로 계산함 (04 §9.2).
// 아키타입별 판정/이동은 MonsterBehaviour가 맡음. 넉백은 상태를 바꾸지 않는 독립 이동 억제 (04 §9.1).
public class Monster : MonoBehaviour, ICombatTarget
{
    private static readonly List<Monster> alive = new List<Monster>();
    public static IReadOnlyList<Monster> Alive => alive;

    public MonsterDefinition Definition { get; private set; }
    public RoomRuntime Room { get; private set; }
    public RoomNavGrid Nav { get; private set; }
    public MonsterState State { get; private set; } = MonsterState.Inactive;
    public int CurrentHP { get; private set; }
    public int MaxHP { get; private set; }
    public int SpawnOrder { get; private set; }
    public int CurrencyDrop { get; private set; }
    public bool IsDead => State == MonsterState.Dead;

    // ICombatTarget
    public Vector2 Position => transform.position;
    public float HitRadius => Definition.BodyRadius;
    public bool CanBeHit => State != MonsterState.Inactive && !IsDead;

    // 예약 시각
    public float NextExecuteTime { get; private set; }
    public float TelegraphStartTime { get; private set; }
    public float ExecuteTime { get; private set; }
    public Vector2 LockedDirection { get; set; }
    public bool IsDirectionLocked { get; private set; }
    public bool IsKnockedBack => knockbackRemaining > 0f;
    public bool IsDashing { get; set; }
    public bool IsStationaryAttack => State == MonsterState.AttackTelegraph || State == MonsterState.DirectionLocked
        || (State == MonsterState.AttackExecute && behaviour.StopsMovementWhileExecuting);

    public PlayerController Player => GameServices.Player;
    public float Now => GameClock.CombatTime;
    public SpriteRenderer BodyRenderer => bodyRenderer;

    private MonsterBehaviour behaviour;
    private SpriteRenderer bodyRenderer;
    private Color baseColor;
    private float flashUntil;
    private SpriteRenderer facing;
    private Vector3 bodyBaseScale = Vector3.one;
    private Vector2 lastVisualPosition;
    private float walkPhase;
    private float contactGraceUntil;
    private float recoveryUntil;

    // 넉백 (04 §9.1): v(t) = v0 (1 - t/Dk), v0 = 2S/Dk
    private Vector2 knockbackDirection;
    private float knockbackV0;
    private float knockbackDuration;
    private float knockbackRemaining;

    // 경로
    private readonly List<Vector2> path = new List<Vector2>();
    private float repathAt;
    private Vector2 pathGoal;

    // 체력바
    private Transform hpBarRoot;
    private Transform hpBarFill;
    private float hpBarHideAt;

    public static Monster Spawn(MonsterDefinition def, Vector2 position, RoomRuntime room, int floorIndex, int spawnOrder, int currencyDrop)
    {
        GameObject go = def.Prefab != null ? Instantiate(def.Prefab, position, Quaternion.identity) : new GameObject(def.DisplayName);
        go.transform.position = position;
        if (room != null) go.transform.SetParent(room.MonsterRoot, true);
        Monster monster = go.GetComponent<Monster>();
        if (monster == null) monster = go.AddComponent<Monster>();
        monster.Init(def, room, floorIndex, spawnOrder, currencyDrop);
        return monster;
    }

    private void Init(MonsterDefinition def, RoomRuntime room, int floorIndex, int spawnOrder, int currencyDrop)
    {
        Definition = def;
        Room = room;
        Nav = room != null ? room.Nav : null;
        SpawnOrder = spawnOrder;
        CurrencyDrop = Mathf.Max(1, currencyDrop);
        MaxHP = def.GetHP(floorIndex);
        CurrentHP = MaxHP;
        behaviour = MonsterBehaviour.Create(def.Archetype);
        behaviour.Init(this);

        bodyRenderer = GetComponentInChildren<SpriteRenderer>();
        if (bodyRenderer == null)
        {
            Sprite sprite = def.Sprite != null ? def.Sprite : PlaceholderArt.Circle;
            bodyRenderer = PlaceholderArt.CreateShape("Body", transform, sprite, def.Sprite != null ? Color.white : def.PlaceholderColor, def.BodyRadius * 2f, 90);
            if (def.Archetype == MonsterArchetype.Charger || def.Archetype == MonsterArchetype.Brawler)
                PlaceholderArt.CreateShape("Mark", transform, PlaceholderArt.Triangle, new Color(0, 0, 0, 0.4f), def.BodyRadius, 91);
        }
        baseColor = bodyRenderer.color;
        bodyBaseScale = bodyRenderer.transform.localScale;
        lastVisualPosition = transform.position;
        // 조준 방향 표시 (원거리형 스트레이프 자세: 몸은 플레이어를 보고 옆으로 이동, 09 §7)
        if (def.Sprite == null)
            facing = PlaceholderArt.CreateShape("Facing", transform, PlaceholderArt.Circle, new Color(1f, 1f, 1f, 0.85f), def.BodyRadius * 0.45f, 92);
        CreateHPBar();

        // 후속 웨이브는 생성 직후 접촉 피해 유예 (03-1 §8.3 7번)
        contactGraceUntil = Now + GameBalance.Current.CombatRoom.SpawnContactDamageGrace;
        Activate();
    }

    // 활성화 시각 t0 기준 첫 공격 예약: D ~ U[max(I/2, T), I] (04 §9.2)
    private void Activate()
    {
        float I = Definition.AttackInterval;
        float T = Definition.HasTelegraph ? Definition.TelegraphDuration : 0f;
        float min = Mathf.Max(I * 0.5f, T);
        float D = RandomStreams.Combat.Range(min, Mathf.Max(min, I));
        ScheduleExecute(Now + D);
        State = MonsterState.ActiveMovement;
        alive.Add(this);
        CombatTargets.Register(this);
        GameEvents.Raise(GameEvents.MonsterSpawned, this);
    }

    private void ScheduleExecute(float executeTime)
    {
        NextExecuteTime = executeTime;
        float T = Definition.HasTelegraph ? Definition.TelegraphDuration : 0f;
        TelegraphStartTime = executeTime - T;
    }

    private void OnDestroy()
    {
        alive.Remove(this);
        CombatTargets.Unregister(this);
    }

    private void Update()
    {
        float dt = GameClock.CombatDelta;
        UpdateVisuals();
        if (dt <= 0f || IsDead || State == MonsterState.Inactive) return;

        TickKnockback(dt);
        TickStateMachine(dt);
        if (IsDead) return;
        TickContactDamage();
        TickSeparation(dt);
    }

    private void TickStateMachine(float dt)
    {
        switch (State)
        {
            case MonsterState.ActiveMovement:
                MoveByAI(dt);
                if (Now >= TelegraphStartTime)
                {
                    if (behaviour.CanStartAttack()) StartAttack();
                    else State = MonsterState.Armed;
                }
                break;

            case MonsterState.Armed:
                MoveByAI(dt);
                // 조건을 처음 만족한 틱에 예고를 처음부터 재생
                if (behaviour.CanStartAttack()) StartAttack();
                break;

            case MonsterState.AttackTelegraph:
            case MonsterState.DirectionLocked:
                behaviour.TickTelegraph(dt);
                if (!IsDirectionLocked && Definition.AimLockLeadTime > 0f && Now >= ExecuteTime - Definition.AimLockLeadTime && behaviour.UsesDirectionLock)
                {
                    IsDirectionLocked = true;
                    State = MonsterState.DirectionLocked;
                    behaviour.OnDirectionLocked();
                }
                if (Now >= ExecuteTime)
                {
                    if (behaviour.CanExecute())
                    {
                        BeginExecute();
                    }
                    else
                    {
                        // 실행 직전 재검사 실패: 취소 시각부터 간격 재예약 (경로 3)
                        behaviour.OnCancel();
                        ScheduleExecute(Now + Definition.AttackInterval);
                        EndAttack();
                    }
                }
                break;

            case MonsterState.AttackExecute:
                if (!behaviour.StopsMovementWhileExecuting) MoveByAI(dt);
                if (behaviour.TickExecute(dt))
                {
                    behaviour.OnExecuteEnd();
                    float recovery = behaviour.RecoveryDuration;
                    if (recovery > 0f)
                    {
                        recoveryUntil = Now + recovery;
                        State = MonsterState.PostDashRecovery;
                    }
                    else
                    {
                        EndAttack();
                    }
                }
                break;

            case MonsterState.PostDashRecovery:
                if (Now >= recoveryUntil) EndAttack();
                break;
        }
    }

    private void StartAttack()
    {
        IsDirectionLocked = false;
        if (!Definition.HasTelegraph)
        {
            ExecuteTime = Now;
            BeginExecute();
            return;
        }
        ExecuteTime = Now + Definition.TelegraphDuration;
        State = MonsterState.AttackTelegraph;
        behaviour.OnTelegraphStart();
        GameEvents.Raise(GameEvents.MonsterAttackTelegraphed, this);
    }

    private void BeginExecute()
    {
        State = MonsterState.AttackExecute;
        float actual = Now;
        // 다음 간격은 실제 실행 시각부터 (경로 1, 2)
        ScheduleExecute(actual + Definition.AttackInterval);
        behaviour.BeginExecute();
        GameEvents.Raise(GameEvents.MonsterAttackExecuted, this);
    }

    private void EndAttack()
    {
        IsDirectionLocked = false;
        State = MonsterState.ActiveMovement;
        behaviour.OnResumeMovement();
        // 이미 예약 시각이 지났으면 같은 틱에 다시 검사
        if (Now >= TelegraphStartTime && State == MonsterState.ActiveMovement)
        {
            if (behaviour.CanStartAttack()) StartAttack();
            else State = MonsterState.Armed;
        }
    }

    // ---------------- 이동 ----------------

    private void MoveByAI(float dt)
    {
        if (IsKnockedBack) return;
        Vector2 velocity = behaviour.DesiredVelocity(dt);
        MoveBy(velocity * dt, out _);
    }

    public Vector2 MoveBy(Vector2 delta, out bool hitWall)
    {
        Vector2 before = transform.position;
        Vector2 moved = KinematicMover2D.Move(before, Definition.BodyRadius, delta, out hitWall);
        transform.position = before + moved;
        return moved;
    }

    // 경로를 따라 target으로 가는 방향 (경로 갱신 0.5초 기준 안, 04 §9.1)
    public Vector2 DirectionTowards(Vector2 target)
    {
        Vector2 pos = transform.position;
        Vector2 toTarget = target - pos;
        if (toTarget.sqrMagnitude < 0.0001f) return Vector2.zero;

        // 막힘 없으면 직선
        RaycastHit2D direct = Physics2D.CircleCast(pos, Definition.BodyRadius * 0.9f, toTarget.normalized, toTarget.magnitude, GameLayers.WallMask);
        if (direct.collider == null)
        {
            path.Clear();
            return toTarget.normalized;
        }

        if (Nav == null) return toTarget.normalized;
        if (Now >= repathAt || (pathGoal - target).sqrMagnitude > 1f || path.Count == 0)
        {
            repathAt = Now + Definition.RepathInterval;
            pathGoal = target;
            Nav.FindPath(pos, target, path);
        }
        while (path.Count > 0 && (path[0] - pos).sqrMagnitude < 0.04f)
            path.RemoveAt(0);
        if (path.Count == 0) return toTarget.normalized;
        return (path[0] - pos).normalized;
    }

    // 겹친 몬스터 약한 분리. 멈춰 선(예고/연발) 쪽은 비키지 않고 움직이는 쪽이 비킴 (04 §9.1)
    private void TickSeparation(float dt)
    {
        if (IsDashing || IsKnockedBack) return;
        Vector2 pos = transform.position;
        Vector2 push = Vector2.zero;
        bool selfStationary = IsStationaryAttack;
        foreach (Monster other in alive)
        {
            if (other == this || other.IsDead || other.Room != Room) continue;
            Vector2 offset = pos - (Vector2)other.transform.position;
            float minDistance = Definition.BodyRadius + other.Definition.BodyRadius;
            float distance = offset.magnitude;
            if (distance >= minDistance) continue;
            bool otherStationary = other.IsStationaryAttack;
            if (selfStationary && !otherStationary) continue;
            Vector2 dir = distance > 0.0001f ? offset / distance : Geometry2D.FromAngle(SpawnOrder * 47f);
            push += dir;
        }
        if (push == Vector2.zero) return;
        MoveBy(push.normalized * Definition.SeparationSpeed * dt, out _);
    }

    // ---------------- 피격 ----------------

    public void TakeDamage(DamageInfo info)
    {
        if (!CanBeHit) return;
        CurrentHP -= info.Amount;
        flashUntil = Time.unscaledTime + GameBalance.Current.Feedback.MonsterWhiteFlashDuration;
        hpBarHideAt = Time.unscaledTime + GameBalance.Current.UI.MonsterHPBarFadeDelay;
        GameServices.Feedback?.OnMonsterHit(info, Position);

        if (CurrentHP <= 0)
        {
            Die(info);
            return;
        }

        if (info.KnockbackDistance > 0f)
            ApplyKnockback(info.KnockbackDirection, info.KnockbackDistance);
    }

    // 넉백 중 추가 넉백 무시, 돌진 중 면역 (04 §9.1)
    public void ApplyKnockback(Vector2 direction, float distance)
    {
        if (IsKnockedBack || IsDashing || direction.sqrMagnitude < 0.0001f) return;
        knockbackDuration = Mathf.Max(0.01f, GameBalance.Current.Weapon.KnockbackDuration);
        knockbackRemaining = knockbackDuration;
        knockbackDirection = direction.normalized;
        knockbackV0 = 2f * distance / knockbackDuration;
    }

    private void TickKnockback(float dt)
    {
        if (!IsKnockedBack) return;
        float t0 = knockbackDuration - knockbackRemaining;
        float t1 = Mathf.Min(knockbackDuration, t0 + dt);
        // 선형 감속 구간 적분
        float d = knockbackV0 * ((t1 - t0) - (t1 * t1 - t0 * t0) / (2f * knockbackDuration));
        knockbackRemaining -= dt;
        Vector2 before = transform.position;
        Vector2 moved = KinematicMover2D.MoveUntilWall(before, Definition.BodyRadius, knockbackDirection * d, out bool hitWall);
        transform.position = before + moved;
        if (hitWall) knockbackRemaining = 0f; // 벽에 닿으면 남은 거리 버림
    }

    private void TickContactDamage()
    {
        PlayerController player = Player;
        if (player == null || player.Health.IsDead || Now < contactGraceUntil) return;
        float distance = Vector2.Distance(Position, player.Position);
        if (distance <= Definition.BodyRadius + player.HurtboxRadius)
        {
            bool accepted = !player.Health.ReceiveAttack(new IncomingAttack { Attacker = this, Owner = this, Damage = Definition.ContactDamage, IsContact = true });
            if (accepted) GameEvents.Raise(GameEvents.MonsterContactDamage, this);
        }
    }

    // 플레이어에게 능동 공격 판정 (돌진, 부채꼴, 저격). 저스트회피 대상
    public void HitPlayerWithAttack(int damage)
    {
        Player?.Health.ReceiveAttack(new IncomingAttack { Attacker = this, Owner = this, Damage = damage, IsContact = false });
    }

    private void Die(DamageInfo killingBlow)
    {
        State = MonsterState.Dead;
        behaviour.OnCancel();
        alive.Remove(this);
        CombatTargets.Unregister(this);
        EnemyProjectile.RemoveOwnedBy(this);

        // 처치 재화 즉시 지급 (03-1 §8.2)
        GameServices.Wallet?.Add(CurrencyDrop);
        GameServices.Feedback?.OnMonsterKilled(killingBlow, Position, Definition.PlaceholderColor);
        GameEvents.Raise(GameEvents.MonsterDied, this);
        Destroy(gameObject);
    }

    // 룸 정리 등으로 조용히 제거
    public void Despawn()
    {
        State = MonsterState.Dead;
        behaviour.OnCancel();
        EnemyProjectile.RemoveOwnedBy(this);
        Destroy(gameObject);
    }

    // ---------------- 표시 ----------------

    private void UpdateVisuals()
    {
        if (bodyRenderer != null)
            bodyRenderer.color = Time.unscaledTime < flashUntil ? Color.white : baseColor;

        // 이동 중 통통 튐, 멈추면(예고, 연발 정지 자세) 정지. 히트스톱 중에는 전투 시간 기준이라 멈춤
        Vector2 position = transform.position;
        float dt = GameClock.CombatDelta;
        bool moved = dt > 0f && (position - lastVisualPosition).sqrMagnitude > 0.000004f;
        lastVisualPosition = position;
        if (bodyRenderer != null && bodyRenderer.transform != transform)
        {
            if (moved) walkPhase += dt * 12f;
            float bob = moved ? Mathf.Abs(Mathf.Sin(walkPhase)) : 0f;
            bodyRenderer.transform.localScale = Vector3.Scale(bodyBaseScale, new Vector3(1f - bob * 0.06f, 1f + bob * 0.08f, 1f));
        }

        if (facing != null)
        {
            PlayerController player = GameServices.Player;
            bool locked = State == MonsterState.DirectionLocked || State == MonsterState.AttackExecute;
            Vector2 dir = locked ? LockedDirection : player != null ? (Vector2)player.transform.position - position : Vector2.right;
            if (dir.sqrMagnitude > 0.0001f) facing.transform.localPosition = dir.normalized * Definition.BodyRadius * 0.6f;
            facing.enabled = !IsDead;
        }

        if (hpBarRoot != null)
        {
            bool show = Time.unscaledTime < hpBarHideAt && !IsDead;
            hpBarRoot.gameObject.SetActive(show);
            if (show)
            {
                float ratio = Mathf.Clamp01(CurrentHP / (float)MaxHP);
                hpBarFill.localScale = new Vector3(ratio, 1f, 1f);
                hpBarFill.localPosition = new Vector3((ratio - 1f) * 0.5f, 0f, 0f);
            }
        }
    }

    private void CreateHPBar()
    {
        hpBarRoot = new GameObject("HPBar").transform;
        hpBarRoot.SetParent(transform, false);
        hpBarRoot.localPosition = new Vector3(0f, Definition.BodyRadius + 0.18f, 0f);
        hpBarRoot.localScale = new Vector3(0.7f, 0.08f, 1f);
        PlaceholderArt.CreateShape("Back", hpBarRoot, PlaceholderArt.Square, new Color(0, 0, 0, 0.7f), 1f, 300);
        hpBarFill = PlaceholderArt.CreateShape("Fill", hpBarRoot, PlaceholderArt.Square, new Color(0.9f, 0.2f, 0.2f), 1f, 301).transform;
        hpBarRoot.gameObject.SetActive(false);
    }
}
