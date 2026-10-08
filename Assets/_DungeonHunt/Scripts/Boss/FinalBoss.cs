using System.Collections.Generic;
using UnityEngine;

public enum BossState
{
    Idle,
    Phase1,
    Phase2,
    Defeated,
}

// 05 최종보스 FSM: Idle → Phase1 → (HP <= 임계치) Phase2 → (HP 0) Defeated
// - 페이즈 전환 컷신/무적 없음
// - HP 0 틱에 남은 탄과 예고 제거, 사망 모션 동안 비전투(피격/충돌/접촉 피해 없음), 시간 지나면 몸체 제거
public class FinalBoss : MonoBehaviour, ICombatTarget
{
    public static FinalBoss Current { get; private set; }

    public BossDefinition Definition { get; private set; }
    public BossState State { get; private set; } = BossState.Idle;
    public int CurrentHP { get; private set; }
    public int MaxHP { get; private set; }
    public RoomRuntime Room { get; private set; }

    public Vector2 Position => transform.position;
    public float HitRadius => GameBalance.Current.Boss.BodyRadius;
    public bool CanBeHit => State == BossState.Phase1 || State == BossState.Phase2;
    public int SpawnOrder => 0;

    private SpriteRenderer body;
    private Color baseColor;
    private float flashUntil;
    private float deathEndsAt;
    private float nextPatternAt;
    private int patternIndex;
    private BossPatternData telegraphing;
    private float telegraphEndsAt;
    private float spiralAngle;
    private TelegraphView telegraph;

    public static FinalBoss Spawn(BossDefinition def, Vector2 position, RoomRuntime room)
    {
        if (def == null)
        {
            Debug.LogWarning("FinalBoss: BossDefinition 없음. 더미 기본값 사용");
            def = ScriptableObject.CreateInstance<BossDefinition>();
        }
        GameObject go = def.Prefab != null ? Instantiate(def.Prefab, position, Quaternion.identity) : new GameObject(def.DisplayName);
        go.transform.position = position;
        if (room != null) go.transform.SetParent(room.MonsterRoot, true);
        FinalBoss boss = go.GetComponent<FinalBoss>();
        if (boss == null) boss = go.AddComponent<FinalBoss>();
        boss.Init(def, room);
        return boss;
    }

    private void Init(BossDefinition def, RoomRuntime room)
    {
        Definition = def;
        Room = room;
        MaxHP = def.HP > 0 ? def.HP : GameBalance.Current.Boss.HP;
        CurrentHP = MaxHP;
        body = GetComponentInChildren<SpriteRenderer>();
        if (body == null)
            body = PlaceholderArt.CreateShape("Body", transform, def.Sprite != null ? def.Sprite : PlaceholderArt.Circle, def.Sprite != null ? Color.white : def.PlaceholderColor, HitRadius * 2f, 90);
        baseColor = body.color;
        telegraph = new TelegraphView(transform);
        Current = this;
        State = BossState.Idle;
    }

    private void OnDestroy()
    {
        if (Current == this) Current = null;
        CombatTargets.Unregister(this);
    }

    // 등장 연출 종료 → Phase1, BossEncounterStarted (05 §5.1 4번)
    public void StartEncounter()
    {
        if (State != BossState.Idle) return;
        State = BossState.Phase1;
        CombatTargets.Register(this);
        nextPatternAt = GameClock.CombatTime + 1.5f;
        GameEvents.Raise(GameEvents.BossEncounterStarted);
    }

    public void TakeDamage(DamageInfo info)
    {
        if (!CanBeHit) return;
        CurrentHP = Mathf.Max(0, CurrentHP - info.Amount);
        flashUntil = Time.unscaledTime + GameBalance.Current.Feedback.MonsterWhiteFlashDuration;
        GameServices.Feedback?.OnMonsterHit(info, Position);

        if (CurrentHP <= 0)
        {
            Defeat();
            return;
        }
        if (State == BossState.Phase1 && CurrentHP <= MaxHP * GameBalance.Current.Boss.PhaseTransitionThreshold)
        {
            State = BossState.Phase2;
            patternIndex = 0;
            GameEvents.Raise(GameEvents.BossPhaseChanged);
        }
    }

    private void Defeat()
    {
        State = BossState.Defeated;
        CombatTargets.Unregister(this);
        EnemyProjectile.RemoveOwnedBy(this);
        telegraph.Hide();
        telegraphing = null;
        deathEndsAt = GameClock.CombatTime + GameBalance.Current.Boss.DeathMotionDuration;
        GameEvents.Raise(GameEvents.BossDefeated);
    }

    private void Update()
    {
        if (body != null)
        {
            Color c = Time.unscaledTime < flashUntil ? Color.white : baseColor;
            if (State == BossState.Defeated)
                c.a = Mathf.Clamp01((deathEndsAt - GameClock.CombatTime) / Mathf.Max(0.01f, GameBalance.Current.Boss.DeathMotionDuration));
            body.color = c;
        }

        if (GameClock.CombatDelta <= 0f) return;
        if (State == BossState.Defeated)
        {
            if (GameClock.CombatTime >= deathEndsAt) Destroy(gameObject);
            return;
        }
        if (State == BossState.Idle) return;

        TickContact();
        TickPatterns();
    }

    private void TickContact()
    {
        PlayerController player = GameServices.Player;
        if (player == null || player.Health.IsDead) return;
        if (Vector2.Distance(Position, player.Position) <= HitRadius + player.HurtboxRadius)
            player.Health.ReceiveAttack(new IncomingAttack { Owner = this, Damage = GameBalance.Current.Boss.ContactDamage, IsContact = true });
    }

    private List<BossPatternData> CurrentPatterns()
    {
        List<BossPatternData> list = State == BossState.Phase2 ? Definition.Phase2Patterns : Definition.Phase1Patterns;
        if ((list == null || list.Count == 0) && Definition.UsePlaceholderPatterns)
            list = State == BossState.Phase2 ? PlaceholderPhase2 : PlaceholderPhase1;
        return list;
    }

    private void TickPatterns()
    {
        float now = GameClock.CombatTime;
        if (telegraphing != null)
        {
            if (now >= telegraphEndsAt)
            {
                Execute(telegraphing);
                telegraphing = null;
                telegraph.Hide();
            }
            return;
        }

        List<BossPatternData> patterns = CurrentPatterns();
        if (patterns == null || patterns.Count == 0 || now < nextPatternAt) return;
        BossPatternData pattern = patterns[patternIndex % patterns.Count];
        patternIndex++;
        telegraphing = pattern;
        telegraphEndsAt = now + pattern.TelegraphDuration;
        nextPatternAt = now + pattern.Interval;
        ShowTelegraph(pattern);
    }

    private void ShowTelegraph(BossPatternData pattern)
    {
        var segments = new List<(Vector2, Vector2)>();
        Vector2 aim = AimDir();
        int count = Mathf.Clamp(pattern.ProjectileCount, 1, 24);
        for (int i = 0; i < count; i++)
        {
            Vector2 dir = DirectionFor(pattern, i, count, aim);
            segments.Add((Position + dir * HitRadius, Position + dir * (HitRadius + 1.5f)));
        }
        telegraph.ShowSegments(segments, new Color(1f, 0.3f, 0.4f, 0.6f), 0.06f);
    }

    private void Execute(BossPatternData pattern)
    {
        Vector2 aim = AimDir();
        int count = Mathf.Max(1, pattern.ProjectileCount);
        for (int i = 0; i < count; i++)
        {
            Vector2 dir = DirectionFor(pattern, i, count, aim);
            EnemyProjectile.Spawn(this, Position + dir * HitRadius, dir, pattern.Projectile, new Color(1f, 0.3f, 0.5f));
        }
        if (pattern.Kind == BossPatternKind.Spiral) spiralAngle += 360f / count * 0.5f;
    }

    private Vector2 DirectionFor(BossPatternData pattern, int i, int count, Vector2 aim)
    {
        switch (pattern.Kind)
        {
            case BossPatternKind.AimedFan:
                float t = count == 1 ? 0.5f : i / (float)(count - 1);
                return Geometry2D.Rotate(aim, Mathf.Lerp(-pattern.SpreadAngle * 0.5f, pattern.SpreadAngle * 0.5f, t));
            case BossPatternKind.Spiral:
                return Geometry2D.FromAngle(spiralAngle + 360f / count * i);
            default:
                return Geometry2D.FromAngle(360f / count * i);
        }
    }

    private Vector2 AimDir()
    {
        PlayerController player = GameServices.Player;
        if (player == null) return Vector2.down;
        Vector2 d = player.Position - Position;
        return d.sqrMagnitude > 0.0001f ? d.normalized : Vector2.down;
    }

    // ---- 테스트용 더미 패턴 (기획 확정 아님) ----
    private static readonly List<BossPatternData> PlaceholderPhase1 = new List<BossPatternData>
    {
        new BossPatternData { Kind = BossPatternKind.RadialBurst, Interval = 2.6f, TelegraphDuration = 0.8f, ProjectileCount = 12, Projectile = new EnemyProjectileSpec(2.6f, 0.12f, 9f, 1) },
        new BossPatternData { Kind = BossPatternKind.AimedFan, Interval = 2.2f, TelegraphDuration = 0.6f, ProjectileCount = 5, SpreadAngle = 50f, Projectile = new EnemyProjectileSpec(3.5f, 0.1f, 9f, 1) },
    };
    private static readonly List<BossPatternData> PlaceholderPhase2 = new List<BossPatternData>
    {
        new BossPatternData { Kind = BossPatternKind.Spiral, Interval = 1.6f, TelegraphDuration = 0.5f, ProjectileCount = 16, Projectile = new EnemyProjectileSpec(2.8f, 0.12f, 9f, 1) },
        new BossPatternData { Kind = BossPatternKind.AimedFan, Interval = 1.8f, TelegraphDuration = 1.0f, ProjectileCount = 7, SpreadAngle = 70f, Projectile = new EnemyProjectileSpec(4f, 0.12f, 9f, 2) },
    };
}
