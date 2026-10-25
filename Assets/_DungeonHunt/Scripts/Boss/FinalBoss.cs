using System.Collections.Generic;
using UnityEngine;

public enum BossState
{
    Idle,
    Combat,
    Defeated,
}

// 05 최종보스: 방 북쪽 고정, 순탄막형, 단일 상태 FSM Idle → Combat → (HP 0) Defeated
// - HP 50% 미만은 상태가 아니라 Combat 안의 규칙(쉬는 시간 단축, LowHPOnly 패턴 추가). 이벤트/무적/컷신 없음
// - 패턴은 한 번에 하나: 쉬는 시간(마지막 발사부터) → 선택 → 예고 → 방향 잠금 → 회차 발사 → 반복
// - 선택: 고정 순서 한 번 → (후반 첫 진입이면 LowHPOnly 한 번) → 직전 2개 제외 가중 추첨. 보스 전용 난수
// - HP 0 틱에 남은 탄과 예고 제거, 사망 모션 동안 비전투(피격/접촉 없음), 시간 지나면 몸체 제거
// - 넉백 면역, 몸체는 이동을 막지 않고 접촉 피해만. 받는 피해는 발사 순간 거리로 줄어듦 (§8.7)
public class FinalBoss : MonoBehaviour, ICombatTarget
{
    public static FinalBoss Current { get; private set; }

    public BossDefinition Definition { get; private set; }
    public BossState State { get; private set; } = BossState.Idle;
    public int CurrentHP { get; private set; }
    public int MaxHP { get; private set; }
    public RoomRuntime Room { get; private set; }
    public bool IsLowHP => CurrentHP < MaxHP * Balance.LowHPThreshold;

    public Vector2 Position => transform.position;
    public float HitRadius => Balance.BodyRadius;
    public bool CanBeHit => State == BossState.Combat;
    public int SpawnOrder => 0;

    private static BossBalance Balance => GameBalance.Current.Boss;
    private static readonly Color ShotColor = new Color(1f, 0.3f, 0.5f);
    private static readonly Color BigShotColor = new Color(1f, 0.55f, 0.1f);
    private static readonly Color TelegraphColor = new Color(1f, 0.3f, 0.4f, 0.6f);
    private static readonly Color LaserColor = new Color(1f, 0.9f, 0.2f, 1f);
    private static int nextAttackGroup = 1;

    private SpriteRenderer body;
    private Color baseColor;
    private float flashUntil;
    private float deathEndsAt;
    private TelegraphView telegraph;

    // 피해 기준 (일반 몬스터 값 참조)
    private int projectileDamage, laserDamage, contactDamage;
    private float laserWidth, aimLockLead, leadRatio;

    // 패턴 선택
    private float combatStartedAt;
    private float lastPatternEnd;
    private bool anyPatternDone;
    private int fixedIndex;
    private bool lowIntroDone;
    private readonly List<int> recent = new List<int>();

    // 진행 중 패턴
    private BossPatternData running;
    private float runStart;
    private int volleysFired;
    private int attackGroup;
    private bool aimLocked;
    private Vector2 lockedAim;
    private readonly List<float> gapAngles = new List<float>();

    public static FinalBoss Spawn(BossDefinition def, Vector2 position, RoomRuntime room)
    {
        if (def == null)
        {
            Debug.LogWarning("FinalBoss: BossDefinition 없음. 기본값 사용");
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
        MaxHP = def.HP > 0 ? def.HP : Balance.HP;
        CurrentHP = MaxHP;
        body = GetComponentInChildren<SpriteRenderer>();
        if (body == null)
            body = PlaceholderArt.CreateShape("Body", transform, def.Sprite != null ? def.Sprite : PlaceholderArt.Circle, def.Sprite != null ? Color.white : def.PlaceholderColor, HitRadius * 2f, 90);
        baseColor = body.color;
        telegraph = new TelegraphView(transform, 5);

        MonsterDefinition barrage = def.BarrageReference != null ? def.BarrageReference : MonsterDefinition.CreateDefault(MonsterArchetype.Barrage);
        MonsterDefinition sniper = def.SniperReference != null ? def.SniperReference : MonsterDefinition.CreateDefault(MonsterArchetype.Sniper);
        MonsterDefinition shooter = def.ShooterReference != null ? def.ShooterReference : MonsterDefinition.CreateDefault(MonsterArchetype.Shooter);
        projectileDamage = barrage.BarrageProjectile.Damage;
        contactDamage = barrage.ContactDamage;
        laserDamage = sniper.SniperDamage;
        laserWidth = sniper.SniperHitscanWidth;
        aimLockLead = sniper.AimLockLeadTime;
        leadRatio = shooter.BurstLeadRatio;

        Current = this;
        State = BossState.Idle;
    }

    private void OnDestroy()
    {
        if (Current == this) Current = null;
        CombatTargets.Unregister(this);
    }

    // 등장 연출 종료 → Combat, BossEncounterStarted (05 §5.1 4번)
    public void StartEncounter()
    {
        if (State != BossState.Idle) return;
        State = BossState.Combat;
        CombatTargets.Register(this);
        RandomStreams.BeginBoss();
        combatStartedAt = GameClock.CombatTime;
        GameEvents.Raise(GameEvents.BossEncounterStarted);
    }

    // 발사 순간 거리로 배율: Near 이하 1.0 → Far 이상 Min, 사이는 선형. 정수 반올림, 최소 1 (§8.7)
    public void TakeDamage(DamageInfo info)
    {
        if (!CanBeHit) return;
        float multiplier = Mathf.Lerp(1f, Balance.DamageFalloffMin, Mathf.InverseLerp(Balance.DamageFalloffNear, Balance.DamageFalloffFar, info.BossDistance));
        if (multiplier < 1f)
        {
            int reduced = Mathf.Max(1, PlayerStats.RoundHalfUp(info.Amount * multiplier));
            info.Reduced = reduced < info.Amount;
            info.Amount = reduced;
        }
        CurrentHP = Mathf.Max(0, CurrentHP - info.Amount);
        flashUntil = Time.unscaledTime + GameBalance.Current.Feedback.MonsterWhiteFlashDuration;
        GameServices.Feedback?.OnMonsterHit(info, Position);
        if (CurrentHP <= 0) Defeat();
    }

    private void Defeat()
    {
        State = BossState.Defeated;
        CombatTargets.Unregister(this);
        EnemyProjectile.RemoveOwnedBy(this);
        running = null;
        telegraph.Hide();
        deathEndsAt = GameClock.CombatTime + Balance.DeathMotionDuration;
        GameEvents.Raise(GameEvents.BossDefeated);
    }

    private void Update()
    {
        if (body != null)
        {
            Color c = Time.unscaledTime < flashUntil ? Color.white : baseColor;
            if (State == BossState.Defeated)
                c.a = Mathf.Clamp01((deathEndsAt - GameClock.CombatTime) / Mathf.Max(0.01f, Balance.DeathMotionDuration));
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
        if (running != null) TickPattern();
        else TickRest();
    }

    private void TickContact()
    {
        PlayerController player = GameServices.Player;
        if (player == null || player.Health.IsDead) return;
        if (Vector2.Distance(Position, player.Position) <= HitRadius + player.HurtboxRadius)
            player.Health.ReceiveAttack(new IncomingAttack { Owner = this, Damage = contactDamage, IsContact = true });
    }

    // ---------- 쉬는 시간과 선택 ----------

    // 쉬는 시간은 지금 HP 기준이라 쉬는 중 50% 미만이 되면 종료 시각 + Low로 다시 계산되는 것과 같음
    private void TickRest()
    {
        float now = GameClock.CombatTime;
        float readyAt = anyPatternDone
            ? lastPatternEnd + (IsLowHP ? Balance.RestIntervalLow : Balance.RestIntervalHigh)
            : combatStartedAt + Balance.PostIntroWait;
        if (now < readyAt) return;
        int number = ChooseNext();
        if (number > 0) Begin(number);
    }

    // 목록이 비어 있으면 기획 기본 5종 (에셋은 건드리지 않음)
    private List<BossPatternData> defaultPatterns;
    private List<BossPatternData> Patterns => Definition.Patterns != null && Definition.Patterns.Count > 0 ? Definition.Patterns : (defaultPatterns ??= BossPatternData.Defaults());

    // 패턴 번호(1부터). 고정 순서 → 후반 첫 LowHPOnly → 가중 추첨 (05 §5.3)
    private int ChooseNext()
    {
        List<BossPatternData> patterns = Patterns;
        int[] order = Definition.FixedOrder ?? new int[0];
        while (fixedIndex < order.Length)
        {
            int n = order[fixedIndex++];
            if (n >= 1 && n <= patterns.Count) return Remember(n);
        }

        if (IsLowHP && !lowIntroDone)
        {
            lowIntroDone = true;
            int low = patterns.FindIndex(p => p.LowHPOnly) + 1;
            if (low > 0) return Remember(low);
        }

        var pool = new List<int>();
        for (int i = 0; i < patterns.Count; i++)
            if (!patterns[i].LowHPOnly || IsLowHP) pool.Add(i + 1);
        List<int> candidates = pool.FindAll(n => !recent.Contains(n));
        if (candidates.Count == 0) candidates = pool;
        if (candidates.Count == 0) return 0;

        float total = 0f;
        foreach (int n in candidates) total += Weight(n);
        double roll = RandomStreams.Boss.NextDouble() * total;
        foreach (int n in candidates)
        {
            roll -= Weight(n);
            if (roll < 0) return Remember(n);
        }
        return Remember(candidates[candidates.Count - 1]);
    }

    private float Weight(int number) => Patterns[number - 1].LowHPOnly ? Balance.LowOnlyWeight : 1f;

    private int Remember(int number)
    {
        recent.Add(number);
        while (recent.Count > Mathf.Max(0, Balance.RecentExclude)) recent.RemoveAt(0);
        return number;
    }

    // ---------- 패턴 진행 ----------

    private void Begin(int number)
    {
        running = Patterns[number - 1];
        runStart = GameClock.CombatTime;
        volleysFired = 0;
        aimLocked = false;
        attackGroup = nextAttackGroup++;
        gapAngles.Clear();
        // 내부 난수는 패턴 추첨 다음 순서: 첫 빈틈 칸 → 다음 빈틈 각도 칸 → 부호 (§8.5)
        if (running.Kind == BossPatternKind.GapRing)
        {
            float step = 360f / Mathf.Max(1, running.Count);
            float angle = RandomStreams.Boss.Next(Mathf.Max(1, running.Count)) * step;
            gapAngles.Add(angle);
            int minSteps = Mathf.RoundToInt(running.NextGapOffset.x / step);
            int maxSteps = Mathf.Max(minSteps, Mathf.RoundToInt(running.NextGapOffset.y / step));
            for (int v = 1; v < running.Volleys; v++)
            {
                int steps = minSteps + RandomStreams.Boss.Next(maxSteps - minSteps + 1);
                int sign = RandomStreams.Boss.Next(2) == 0 ? -1 : 1;
                angle = Mathf.Repeat(angle + sign * steps * step, 360f);
                gapAngles.Add(angle);
            }
        }
    }

    private float FireTime(int volley) => runStart + running.TelegraphDuration + running.VolleyInterval * volley;
    private bool Tracks => running.Kind == BossPatternKind.AimedFan || running.Kind == BossPatternKind.Laser;

    private void TickPattern()
    {
        float now = GameClock.CombatTime;
        while (running != null && volleysFired < running.Volleys)
        {
            float fireAt = FireTime(volleysFired);
            // 방향을 따라가는 발은 발마다 발사 AimLockLeadTime 전에 잠금 (§8.1)
            if (Tracks && !aimLocked && now >= fireAt - aimLockLead)
            {
                lockedAim = AimFor(volleysFired);
                aimLocked = true;
            }
            if (now < fireAt) break;
            if (Tracks && !aimLocked) lockedAim = AimFor(volleysFired);
            Fire(volleysFired);
            volleysFired++;
            aimLocked = false;
            if (volleysFired >= running.Volleys)
            {
                lastPatternEnd = fireAt;
                anyPatternDone = true;
                running = null;
                telegraph.Hide();
                return;
            }
        }
        DrawTelegraph(now);
    }

    // 1회차는 지금 위치, 2회차부터는 선행 조준 (조준 사격형 셋째 발과 같은 식, 거리는 발사 원점부터)
    private Vector2 AimFor(int volley)
    {
        PlayerController player = GameServices.Player;
        if (player == null) return Vector2.down;
        Vector2 target = player.Position;
        Vector2 dir = Direction(target);
        if (running.Kind == BossPatternKind.AimedFan && volley > 0 && running.LeadAfterFirstVolley)
        {
            float dist = Vector2.Distance(Position + dir * HitRadius, target);
            target += player.Movement.ActualVelocity * (dist / Mathf.Max(0.01f, running.Speed)) * leadRatio;
            dir = Direction(target);
        }
        return dir;
    }

    private Vector2 Direction(Vector2 target)
    {
        Vector2 d = target - Position;
        return d.sqrMagnitude > 0.0001f ? d.normalized : Vector2.down;
    }

    private Vector2 CurrentAim => aimLocked ? lockedAim : AimFor(volleysFired);

    // 1, 2, 4번 발사 방향선 / 3번 빈틈 화살 / 5번 레이저 예고선. 모두 바닥에 그림 (§8.6)
    private void DrawTelegraph(float now)
    {
        bool beforeFirst = volleysFired == 0;
        var segments = new List<(Vector2, Vector2)>();
        float width = 0.06f;
        Color color = TelegraphColor;
        switch (running.Kind)
        {
            case BossPatternKind.RadialBurst:
                if (beforeFirst)
                    foreach (Vector2 dir in RadialDirections(0)) segments.Add(Ray(dir, HitRadius, 1.5f));
                break;
            case BossPatternKind.AimedFan:
                // 1회차는 예고 동안 따라가며, 2회차부터는 잠긴 뒤에만 표시
                if (beforeFirst || aimLocked)
                    foreach (Vector2 dir in FanDirections(CurrentAim)) segments.Add(Ray(dir, HitRadius, 2f));
                break;
            case BossPatternKind.GapRing:
                // 다음 회차 빈틈 화살: 첫 링은 예고 동안, 다음 링은 앞 링 발사부터
                Arrow(segments, Geometry2D.FromAngle(gapAngles[volleysFired]));
                width = 0.1f;
                break;
            case BossPatternKind.Splitter:
                if (beforeFirst)
                    foreach (Vector2 dir in SplitterDirections()) segments.Add(Ray(dir, HitRadius, running.SplitDistance));
                break;
            case BossPatternKind.Laser:
                foreach (Vector2 dir in LaserDirections(CurrentAim)) segments.Add((Position, Position + dir * running.LaserLength));
                color = new Color(LaserColor.r, LaserColor.g, LaserColor.b, aimLocked ? 0.85f : 0.3f);
                width = aimLocked ? laserWidth : laserWidth * 0.3f;
                break;
        }
        if (segments.Count > 0) telegraph.ShowSegments(segments, color, width);
        else telegraph.Hide();
    }

    private (Vector2, Vector2) Ray(Vector2 dir, float from, float length) => (Position + dir * from, Position + dir * (from + length));

    private void Arrow(List<(Vector2, Vector2)> segments, Vector2 dir)
    {
        Vector2 tail = Position + dir * (HitRadius + 0.3f);
        Vector2 head = Position + dir * (HitRadius + 2.2f);
        segments.Add((tail, head));
        segments.Add((head, head + Geometry2D.Rotate(-dir, 30f) * 0.5f));
        segments.Add((head, head + Geometry2D.Rotate(-dir, -30f) * 0.5f));
    }

    private void Fire(int volley)
    {
        EnemyProjectileSpec spec = new EnemyProjectileSpec(running.Speed, running.ProjectileRadius, Balance.ProjectileMaxDistance, projectileDamage);
        switch (running.Kind)
        {
            case BossPatternKind.RadialBurst:
                foreach (Vector2 dir in RadialDirections(volley)) Shoot(dir, spec, ShotColor);
                break;
            case BossPatternKind.AimedFan:
                foreach (Vector2 dir in FanDirections(lockedAim)) Shoot(dir, spec, ShotColor);
                break;
            case BossPatternKind.GapRing:
                foreach (Vector2 dir in RingDirections(gapAngles[volley])) Shoot(dir, spec, ShotColor);
                break;
            case BossPatternKind.Splitter:
                var child = new EnemyProjectileSpec(running.SplitSpeed, running.SplitRadius, Balance.ProjectileMaxDistance, projectileDamage);
                foreach (Vector2 dir in SplitterDirections())
                {
                    EnemyProjectile big = Shoot(dir, spec, BigShotColor);
                    big.Uncancelable = true;
                    big.Split = new ProjectileSplit { Distance = running.SplitDistance, Count = running.SplitCount, Child = child, ChildColor = ShotColor };
                }
                break;
            case BossPatternKind.Laser:
                FireLasers(lockedAim);
                break;
        }
    }

    private EnemyProjectile Shoot(Vector2 dir, EnemyProjectileSpec spec, Color color)
    {
        EnemyProjectile p = EnemyProjectile.Spawn(this, Position + dir * HitRadius, dir, spec, color);
        p.AttackGroupId = attackGroup;
        return p;
    }

    // 코어에서 즉발, 벽 무시. 중심선과 피격원 교차로 판정. 여러 줄에 걸려도 피해는 한 번 (§8.2 5번)
    private void FireLasers(Vector2 aim)
    {
        PlayerController player = GameServices.Player;
        bool hit = false;
        foreach (Vector2 dir in LaserDirections(aim))
        {
            Vector2 to = Position + dir * running.LaserLength;
            if (player != null && !player.Health.IsDead && Geometry2D.SegmentIntersectsCircle(Position, to, player.Position, player.HurtboxRadius)) hit = true;
            GameServices.Feedback?.ShowHitscan(Position, to);
        }
        if (hit) player.Health.ReceiveAttack(new IncomingAttack { Owner = this, Damage = laserDamage, IsContact = false, AttackGroupId = attackGroup });
    }

    // A = 월드축 0도부터, B = 간격의 절반 돌림. A, B, A... (§8.2 1번)
    private IEnumerable<Vector2> RadialDirections(int volley)
    {
        int count = Mathf.Max(1, running.Count);
        float step = 360f / count;
        float offset = volley % 2 == 1 ? step * 0.5f : 0f;
        for (int i = 0; i < count; i++) yield return Geometry2D.FromAngle(offset + step * i);
    }

    private IEnumerable<Vector2> FanDirections(Vector2 aim)
    {
        int count = Mathf.Max(1, running.Count);
        for (int i = 0; i < count; i++)
        {
            float t = count == 1 ? 0.5f : i / (float)(count - 1);
            yield return Geometry2D.Rotate(aim, Mathf.Lerp(-running.SpreadAngle * 0.5f, running.SpreadAngle * 0.5f, t));
        }
    }

    // 빈틈 중심에서 GapSize발을 뺀 링
    private IEnumerable<Vector2> RingDirections(float gapAngle)
    {
        int count = Mathf.Max(1, running.Count);
        float step = 360f / count;
        float half = (running.GapSize - 1) * 0.5f * step + step * 0.01f;
        for (int i = 0; i < count; i++)
        {
            float angle = step * i;
            if (Mathf.Abs(Mathf.DeltaAngle(angle, gapAngle)) <= half) continue;
            yield return Geometry2D.FromAngle(angle);
        }
    }

    // 남쪽(270도) 기준 고정 각도
    private IEnumerable<Vector2> SplitterDirections()
    {
        float[] angles = running.FixedAngles != null && running.FixedAngles.Length > 0 ? running.FixedAngles : new[] { 0f };
        foreach (float a in angles) yield return Geometry2D.FromAngle(270f + a);
    }

    // 가운데 줄이 조준 방향, 나머지는 SpreadAngle 간격으로 양옆
    private IEnumerable<Vector2> LaserDirections(Vector2 aim)
    {
        int count = Mathf.Max(1, running.Count);
        for (int i = 0; i < count; i++)
            yield return Geometry2D.Rotate(aim, (i - (count - 1) * 0.5f) * running.SpreadAngle);
    }
}
