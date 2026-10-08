using System.Collections.Generic;
using UnityEngine;

// 01-1 무기 전환/재장전 상태 + 01-2 발사 실행.
// 순환 순서 라이플 → 권총 → 샷건 → 라이플 (E 정방향, Q 역방향, 잠긴 무기 건너뜀). 런 시작은 라이플.
// 발사 가능 = 장착 중 AND SwapReadyDelay 끝 AND 회피 아님 AND 탄창 1 이상 (01-1 §5.2)
[RequireComponent(typeof(PlayerInputReader))]
public class WeaponController : MonoBehaviour
{
    private static readonly WeaponType[] CycleOrder = { WeaponType.Rifle, WeaponType.Pistol, WeaponType.Shotgun };

    [SerializeField] private WeaponScriptable[] definitions = new WeaponScriptable[3];
    [Tooltip("투사체 프리팹. 무기 정의에 ProjectilePrefab이 있으면 그걸 우선함. 둘 다 없으면 기본 도형")]
    [SerializeField] private GameObject projectilePrefab;
    [Tooltip("비우면 PlayerVisual의 무기 피벗 기준으로 계산")]
    [SerializeField] private Transform muzzle;

    private readonly List<WeaponRuntime> slots = new List<WeaponRuntime>();
    private int currentIndex;
    private PlayerInputReader input;
    private PlayerMovement movement;
    private PlayerStats stats;
    private PlayerVisual visual;
    private float lastRifleShotTime = -10f;
    private float shotgunKickUntil = -10f;
    private bool rifleFiringApplied;
    private bool pistolPassiveApplied;

    public IReadOnlyList<WeaponRuntime> Slots => slots;
    public WeaponRuntime CurrentWeapon => slots.Count > 0 ? slots[currentIndex] : null;
    public int CurrentIndex => currentIndex;

    private void Awake()
    {
        input = GetComponent<PlayerInputReader>();
        movement = GetComponent<PlayerMovement>();
        stats = GetComponent<PlayerStats>();
        if (stats == null) stats = gameObject.AddComponent<PlayerStats>();
        visual = GetComponent<PlayerVisual>();
        BuildSlots();
    }

    private void OnEnable()
    {
        input.WeaponChangeRequested += OnWeaponChangeRequested;
        input.ReloadPressed += OnReloadPressed;
    }

    private void OnDisable()
    {
        input.WeaponChangeRequested -= OnWeaponChangeRequested;
        input.ReloadPressed -= OnReloadPressed;
    }

    private void Start() => EquipInitial();

    // 코드로 만든 플레이어에 무기 정의 넣기 (GameBootstrap)
    public void SetDefinitions(WeaponScriptable[] defs, GameObject defaultProjectile = null)
    {
        if (defs != null && defs.Length > 0) definitions = defs;
        if (defaultProjectile != null) projectilePrefab = defaultProjectile;
        BuildSlots();
        EquipInitial();
    }

    public bool HasDefinitions => definitions != null && System.Array.Exists(definitions, d => d != null);

    // 인스펙터 순서와 상관없이 순환 순서로 정렬
    private void BuildSlots()
    {
        slots.Clear();
        WeaponScriptable[] source = HasDefinitions ? definitions
            : GameServices.Config != null && GameServices.Config.Weapons != null ? GameServices.Config.Weapons : new WeaponScriptable[0];
        foreach (WeaponType type in CycleOrder)
        {
            WeaponScriptable def = System.Array.Find(source, d => d != null && d.WeaponType == type);
            if (def == null)
            {
                Debug.LogWarning($"WeaponController: {type} 무기 정의가 없음. 기본값으로 만듦", this);
                def = WeaponDefaults.Create(type);
            }
            stats.RegisterWeaponBase(def);
            slots.Add(new WeaponRuntime(def, CreateFireMode(def.WeaponType), stats));
        }
    }

    private IWeaponFireMode CreateFireMode(WeaponType type) => type switch
    {
        WeaponType.Shotgun => new ConeFireMode(),
        _ => new ProjectileFireMode(),
    };

    private void EquipInitial()
    {
        currentIndex = slots.FindIndex(w => w.Type == WeaponType.Rifle);
        if (currentIndex < 0) currentIndex = 0;
        ApplyEquipEffects(null);
        visual?.SetWeaponSprite(CurrentWeapon.Definition.WeaponSprite, CurrentWeapon.Definition.PlaceholderColor);
    }

    public void ResetForNewRun()
    {
        foreach (WeaponRuntime weapon in slots)
            weapon.ResetForNewRun();
        pistolPassiveApplied = false;
        stats.RemoveSource(StatSources.PistolPassive);
        stats.RemoveSource(StatSources.RifleFiring);
        stats.RemoveSource(StatSources.ShotgunKick);
        rifleFiringApplied = false;
        EquipInitial();
    }

    public void RefillAllForNewFloor()
    {
        foreach (WeaponRuntime weapon in slots)
            weapon.RefillForNewFloor();
    }

    private void Update()
    {
        float dt = GameClock.CombatDelta;
        if (dt <= 0f || slots.Count == 0) return;

        foreach (WeaponRuntime weapon in slots)
            weapon.Tick(dt);

        HandleAttackInput();
        UpdateMoveStateModifiers();
    }

    private void HandleAttackInput()
    {
        WeaponRuntime weapon = CurrentWeapon;
        bool pressedNew = input.AttackPressedThisFrame;
        bool held = input.IsAttackHeld;
        if (!held && !pressedNew) return;
        if (GameServices.Player != null && GameServices.Player.Health.IsDead) return;

        bool otherConditions = !weapon.IsSwapDelayed && !movement.IsDodging && weapon.Ammo >= 1 && !weapon.IsLocked;

        if (weapon.IsReloading)
        {
            // 재장전 취소 발사: 새로 누른 입력만. 홀드로는 취소 안 됨 (01-1 §7.2)
            if (!pressedNew || !otherConditions) return;
            if (weapon.IsFireCooldown) return;
            weapon.CancelReload();
            Fire(weapon);
            return;
        }

        if (otherConditions && !weapon.IsFireCooldown)
            Fire(weapon);
    }

    private void Fire(WeaponRuntime weapon)
    {
        Vector2 aim = movement.GetAimDirection();
        AttackSnapshot snapshot = CreateSnapshot(weapon);
        weapon.ConsumeShot();

        Vector2 origin = ResolveMuzzle(aim);
        GameObject prefab = weapon.Definition.ProjectilePrefab != null ? weapon.Definition.ProjectilePrefab : projectilePrefab;
        weapon.FireMode.Fire(snapshot, origin, aim, prefab);
        GameEvents.Raise(GameEvents.WeaponFired, weapon.Type);
        GameServices.Feedback?.ShowMuzzleFlash(origin, aim);

        if (weapon.Type == WeaponType.Rifle) lastRifleShotTime = GameClock.CombatTime;
        if (weapon.Type == WeaponType.Shotgun)
        {
            shotgunKickUntil = GameClock.CombatTime + GameBalance.Current.Weapon.ShotgunFireKickSlowDuration;
            stats.SetSource(StatSources.ShotgunKick, new StatModifier(StatId.MoveSpeed, ModifierKind.Multiplier, GameBalance.Current.Weapon.ShotgunFireMoveSpeedMultiplier));
        }
    }

    // 피해 = (기본 + 고정) x (1 + 비율) x 상태 배율, 치명타면 x 치명타 배율, 마지막에 한 번 반올림 (01-2 §8.3)
    private AttackSnapshot CreateSnapshot(WeaponRuntime weapon)
    {
        WeaponType type = weapon.Type;
        float raw = stats.GetWeaponDamageRaw(type);
        bool crit = RandomStreams.Combat.NextDouble() < stats.Get(StatId.CritChance);
        float final = crit ? raw * stats.Get(StatId.CritDamage) : raw;

        return new AttackSnapshot
        {
            AttackExecutionId = AttackSnapshot.NextId(),
            Weapon = type,
            Damage = Mathf.Max(0, PlayerStats.RoundHalfUp(final)),
            IsCritical = crit,
            ProjectileSpeed = stats.GetWeapon(type, WeaponStat.ProjectileSpeed),
            Range = stats.GetWeapon(type, WeaponStat.Range),
            ConeAngle = stats.GetWeapon(type, WeaponStat.ConeAngle),
            HitRadius = weapon.Definition.ProjectileHitRadius,
            KnockbackDistance = weapon.Definition.KnockbackDistance,
        };
    }

    // 총구가 벽 안이나 너머면 캐릭터→총구 선분이 처음 만나는 벽 표면에서 시작 (01-2 §8.1)
    private Vector2 ResolveMuzzle(Vector2 aim)
    {
        Vector2 center = transform.position;
        Vector2 muzzlePoint = muzzle != null ? (Vector2)muzzle.position : (visual != null ? visual.MuzzlePosition(aim) : center + aim * 0.5f);
        RaycastHit2D hit = Physics2D.Linecast(center, muzzlePoint, GameLayers.WallMask);
        return hit.collider != null ? hit.point - aim * 0.01f : muzzlePoint;
    }

    private void UpdateMoveStateModifiers()
    {
        bool rifleFiring = CurrentWeapon.Type == WeaponType.Rifle && input.IsAttackHeld
            && GameClock.CombatTime - lastRifleShotTime <= GameBalance.Current.Weapon.RifleFiringGrace + CurrentWeapon.FireInterval;
        if (rifleFiring != rifleFiringApplied)
        {
            rifleFiringApplied = rifleFiring;
            if (rifleFiring) stats.SetSource(StatSources.RifleFiring, new StatModifier(StatId.MoveSpeed, ModifierKind.Multiplier, GameBalance.Current.Weapon.RifleFireMoveSpeedMultiplier));
            else stats.RemoveSource(StatSources.RifleFiring);
        }

        // 샷건 감속만 풀리고 다른 수정값은 그대로 (01-2 §10)
        if (GameClock.CombatTime >= shotgunKickUntil && stats.HasSource(StatSources.ShotgunKick))
            stats.RemoveSource(StatSources.ShotgunKick);
    }

    private void OnWeaponChangeRequested(int direction)
    {
        if (slots.Count <= 1) return;
        int next = FindNextUsable(direction);
        if (next < 0 || next == currentIndex) return; // 사용 가능 무기가 1개면 무시
        SwitchTo(next, manual: true);
    }

    private int FindNextUsable(int direction)
    {
        for (int step = 1; step < slots.Count; step++)
        {
            int index = ((currentIndex + direction * step) % slots.Count + slots.Count) % slots.Count;
            if (!slots[index].IsLocked) return index;
        }
        return -1;
    }

    private void SwitchTo(int index, bool manual)
    {
        WeaponRuntime previous = CurrentWeapon;
        currentIndex = index;
        CurrentWeapon.OnEquipped();
        ApplyEquipEffects(previous);
        visual?.SetWeaponSprite(CurrentWeapon.Definition.WeaponSprite, CurrentWeapon.Definition.PlaceholderColor);
        GameEvents.Raise(GameEvents.WeaponSwapped, CurrentWeapon.Type);
        if (manual) ManualSwapPerformed?.Invoke(CurrentWeapon.Type);
    }

    // 수동 전환에서만 발생 (스왑 세트효과 연결점, 01-1 §3 WeaponSwapped 비고)
    public event System.Action<WeaponType> ManualSwapPerformed;

    // 권총 고유 패시브: 이동속도 +, 회피 최대 충전 + (01-2 §7). 실제 장착 무기가 권총일 때만
    private void ApplyEquipEffects(WeaponRuntime previous)
    {
        bool pistol = CurrentWeapon.Type == WeaponType.Pistol;
        if (pistol == pistolPassiveApplied) return;
        pistolPassiveApplied = pistol;
        WeaponCommonBalance b = GameBalance.Current.Weapon;
        if (pistol)
        {
            stats.SetSource(StatSources.PistolPassive, new[]
            {
                new StatModifier(StatId.MoveSpeed, ModifierKind.Percent, b.PistolMoveSpeedBonus),
                new StatModifier(StatId.DodgeMaxCharge, ModifierKind.Flat, b.PistolDodgeChargeBonus),
            });
            movement.OnPistolPassiveChanged(true, b.PistolDodgeChargeBonus);
        }
        else
        {
            stats.RemoveSource(StatSources.PistolPassive);
            movement.OnPistolPassiveChanged(false, 0);
        }
    }

    private void OnReloadPressed()
    {
        // 회피 중에도 R 허용 (01-1 §4.3)
        CurrentWeapon?.TryStartReload();
    }

    // 저스트회피 성공 순간 장착 무기 탄창에 반환 (01-2 §5.6)
    public void OnJustDodge()
    {
        WeaponRuntime weapon = CurrentWeapon;
        if (weapon == null) return;
        int amount = Mathf.CeilToInt(weapon.MaxAmmo * GameBalance.Current.Weapon.JustDodgeAmmoRefundRatio);
        weapon.RefundAmmo(amount);
        GameEvents.Raise(GameEvents.JustDodgeAmmoRefund, amount);
    }

    // 무기 특화 세트 잠금 (07 §4.14). 잠기는 무기를 들고 있으면 특화 무기로 강제 전환(ForcedUnequip)
    public void SetWeaponLocks(ICollection<WeaponType> locked, WeaponType? specialization)
    {
        foreach (WeaponRuntime weapon in slots)
            weapon.IsLocked = locked != null && locked.Contains(weapon.Type);

        if (CurrentWeapon.IsLocked && specialization.HasValue)
        {
            int target = slots.FindIndex(w => w.Type == specialization.Value);
            if (target >= 0) SwitchTo(target, manual: false);
        }
    }

    public WeaponRuntime Get(WeaponType type) => slots.Find(w => w.Type == type);
}

// 무기 에셋이 비어 있을 때 쓰는 PrototypeDefault v0.2 값 (01-2 §7)
public static class WeaponDefaults
{
    public static WeaponScriptable Create(WeaponType type)
    {
        var def = ScriptableObject.CreateInstance<WeaponScriptable>();
        def.WeaponType = type;
        def.SwapReadyDelay = 0.12f;
        Apply(def);
        return def;
    }

    public static void Apply(WeaponScriptable def)
    {
        switch (def.WeaponType)
        {
            case WeaponType.Pistol:
                def.WeaponName = "권총";
                def.Damage = 10; def.FireInterval = 0.24f; def.Range = 5f; def.MaxAmmo = 12; def.ProjectileSpeed = 16f;
                def.ReloadTime = 1.05f; def.KnockbackDistance = 0.20f; def.ProjectileHitRadius = 0.12f;
                def.PlaceholderColor = new Color(0.4f, 0.6f, 0.9f);
                break;
            case WeaponType.Shotgun:
                def.WeaponName = "샷건";
                def.Damage = 28; def.FireInterval = 0.58f; def.Range = 2f; def.ConeAngle = 58f; def.MaxAmmo = 6;
                def.ReloadTime = 1.35f; def.KnockbackDistance = 0.55f;
                def.PlaceholderColor = new Color(0.9f, 0.5f, 0.3f);
                break;
            default:
                def.WeaponName = "라이플";
                def.Damage = 6; def.FireInterval = 0.08f; def.Range = 7f; def.MaxAmmo = 30; def.ProjectileSpeed = 22f;
                def.ReloadTime = 1.45f; def.KnockbackDistance = 0.05f; def.ProjectileHitRadius = 0.12f;
                def.PlaceholderColor = new Color(0.5f, 0.8f, 0.5f);
                break;
        }
    }
}
