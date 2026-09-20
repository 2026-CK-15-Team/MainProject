using System;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(PlayerInputReader))]
[RequireComponent(typeof(PlayerMovement))]
public class WeaponController : MonoBehaviour
{
    private static readonly WeaponType[] CycleOrder = { WeaponType.Rifle, WeaponType.Pistol, WeaponType.Shotgun };

    [SerializeField] private WeaponScriptable[] definitions = new WeaponScriptable[3];
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private Transform muzzle;

    private WeaponRuntime[] weaponSlots;
    private Dictionary<WeaponType, int> indexByType;
    private readonly HashSet<WeaponType> lockedTypes = new();
    private bool swapTier1Active;
    private int currentIndex;
    private PlayerInputReader input;
    private PlayerMovement movement;

    public WeaponRuntime CurrentWeapon => weaponSlots[currentIndex];

    private void Awake()
    {
        input = GetComponent<PlayerInputReader>();
        movement = GetComponent<PlayerMovement>();

        weaponSlots = new WeaponRuntime[definitions.Length];
        indexByType = new Dictionary<WeaponType, int>();

        for (int i = 0; i < definitions.Length; i++)
        {
            IWeaponFireMode fireMode = CreateFireMode(definitions[i].WeaponType);
            weaponSlots[i] = new WeaponRuntime(definitions[i], fireMode);
            indexByType[definitions[i].WeaponType] = i;
        }

        currentIndex = indexByType.TryGetValue(WeaponType.Rifle, out int rifleIndex) ? rifleIndex : 0;
        weaponSlots[currentIndex].OnEquipped();
    }

    private void OnEnable()
    {
        input.WeaponChangeRequested += OnWeaponChangeRequested;
        input.AttackPressed += OnAttackPressed;
        input.ReloadPressed += OnReloadPressed;
        movement.JustDodgeTriggered += OnJustDodgeTriggered;
    }

    private void OnDisable()
    {
        input.WeaponChangeRequested -= OnWeaponChangeRequested;
        input.AttackPressed -= OnAttackPressed;
        input.ReloadPressed -= OnReloadPressed;
        movement.JustDodgeTriggered -= OnJustDodgeTriggered;
    }

    private void OnReloadPressed()
    {
        if (ModalGate.AnyModalOpen) return;
        if (HitStopState.IsActive) return;
        CurrentWeapon.TryStartReload();
        PlaytestLogger.Log("ReloadStart", CurrentWeapon.Definition.WeaponType.ToString());
    }

    private void OnAttackPressed()
    {
        if (ModalGate.AnyModalOpen) return;
        if (HitStopState.IsActive) return;
        if (IsDodging) return;

        var weapon = CurrentWeapon;
        if (weapon.IsReloading && weapon.Ammo > 0 && !weapon.IsSwapDelayed)
        {
            weapon.CancelReload();
            PlaytestLogger.Log("ReloadCancelled", weapon.Definition.WeaponType.ToString());
            Fire();
        }
    }

    private bool IsDodging => movement.CurrentState == movement.DodgeState;

    private void OnJustDodgeTriggered()
    {
        int refund = Mathf.CeilToInt(CurrentWeapon.EffectiveMaxAmmo * 0.25f);
        CurrentWeapon.RefundAmmo(refund);
        Debug.Log($"[JustDodge] {CurrentWeapon.Definition.WeaponType} 탄환 {refund}발 반환 (현재 잔탄 {CurrentWeapon.Ammo})");
    }

    private void Update()
    {
        if (HitStopState.IsActive) return;

        foreach (var weapon in weaponSlots)
            weapon.TickAmmoAxis();

        if (input.IsAttackHeld && !IsDodging && !ModalGate.AnyModalOpen && CurrentWeapon.CanFire)
            Fire();
    }

    private void OnWeaponChangeRequested(int direction)
    {
        if (ModalGate.AnyModalOpen) return;
        if (HitStopState.IsActive) return;

        WeaponType currentType = definitions[currentIndex].WeaponType;
        int cyclePos = Array.IndexOf(CycleOrder, currentType);

        for (int step = 1; step <= CycleOrder.Length; step++)
        {
            int nextPos = (cyclePos + direction * step + CycleOrder.Length) % CycleOrder.Length;
            WeaponType candidate = CycleOrder[nextPos];

            if (lockedTypes.Contains(candidate)) continue;
            if (!indexByType.TryGetValue(candidate, out int nextIndex)) continue;

            currentIndex = nextIndex;
            CurrentWeapon.OnEquipped();
            PlaytestLogger.Log("WeaponSwap", candidate.ToString());

            if (swapTier1Active)
            {
                CurrentWeapon.IsMagazineEnhanced = true;
                Debug.Log($"[SwapEnhance] {candidate} 교체 → 강화탄 상태 ON (잔탄 {CurrentWeapon.Ammo})");
            }

            return;
        }
    }

    public void SetLocked(WeaponType type, bool locked)
    {
        if (locked) lockedTypes.Add(type);
        else lockedTypes.Remove(type);
    }

    public void ForceEquip(WeaponType type)
    {
        if (!indexByType.TryGetValue(type, out int index)) return;

        currentIndex = index;
        CurrentWeapon.OnEquipped();
    }

    public bool IsLocked(WeaponType type) => lockedTypes.Contains(type);

    public WeaponRuntime GetWeapon(WeaponType type) =>
        indexByType.TryGetValue(type, out int index) ? weaponSlots[index] : null;

    public void SetSwapTier1Active(bool active) => swapTier1Active = active;

    public void ClearAllEnhancedAmmo()
    {
        //Debug.Log("[SwapEnhance] 모든 무기 강화탄 제거 (무기 특화 Tier1 활성화)");
        foreach (var weapon in weaponSlots)
            weapon.IsMagazineEnhanced = false;
    }

    private IWeaponFireMode CreateFireMode(WeaponType type) => type switch
    {
        WeaponType.Pistol => new ProjectileFireMode(),
        WeaponType.Rifle => new ProjectileFireMode(),
        WeaponType.Shotgun => new ConeFireMode(),
        _ => throw new ArgumentOutOfRangeException()
    };

    private void Fire()
    {
        WeaponType type = CurrentWeapon.Definition.WeaponType;
        //Debug.Log($"[SwapEnhance] {type} 발사 | 강화탄={CurrentWeapon.IsMagazineEnhanced} | 최종피해={CurrentWeapon.EffectiveDamage:F1}");
        //PlaytestLogger.Log("Fire", $"weapon={type},enhanced={CurrentWeapon.IsMagazineEnhanced},ammoLeft={CurrentWeapon.Ammo - 1}");

        CurrentWeapon.ConsumeShot();

        Vector3 origin = muzzle != null ? muzzle.position : transform.position;
        Vector2 direction = AimUtility.ScreenPointToWorldDirection(input.AimScreenPosition, origin);

        CurrentWeapon.FireMode.Fire(CurrentWeapon, origin, direction, projectilePrefab);
    }
}