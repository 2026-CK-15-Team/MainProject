using UnityEngine;

public class WeaponRuntime
{
    public const float KnockbackDuration = 0.12f;

    public WeaponScriptable Definition { get; }
    public IWeaponFireMode FireMode { get; }
    public WeaponRuntime(WeaponScriptable definition, IWeaponFireMode fireMode)
    {
        Definition = definition;
        FireMode = fireMode;
        Ammo = definition.MaxAmmo;
    }

    public int Ammo { get; private set; }

    private bool isReloading;
    private float reloadEndTime;

    private float swapReadyEndTime;

    // ---- 아티팩트 배율 ----
    private float damageMultiplier = 1f;
    private float rangeMultiplier = 1f;
    private float reloadTimeMultiplier = 1f;
    private float fireIntervalMultiplier = 1f;
    public int MagazineBonus { get; private set; }
    public bool CanDeflectProjectiles { get; set; }
    public bool IsMagazineEnhanced { get; set; }
    public RifleStackTracker RifleStack { get; } = new RifleStackTracker();

    public void AddDamagePercent(float percent) => damageMultiplier += percent;
    public void AddRangePercent(float percent) => rangeMultiplier += percent;
    public void AddReloadTimePercent(float percent) => reloadTimeMultiplier += percent;
    public void AddFireIntervalPercent(float percent) => fireIntervalMultiplier += percent;
    public void AddMagazineBonus(int amount) => MagazineBonus += amount;

    private const float EnhancedDamageBonus = 0.20f;

    public float EffectiveDamage => Definition.Damage * (damageMultiplier + (IsMagazineEnhanced ? EnhancedDamageBonus : 0f));
    public float EffectiveRange => Definition.Range * rangeMultiplier;
    public float EffectiveReloadTime => Mathf.Max(0.04f, Definition.ReloadTime * reloadTimeMultiplier);
    public float EffectiveFireInterval => Mathf.Max(0.04f, Definition.FireInterval * fireIntervalMultiplier);
    public int EffectiveMaxAmmo => Definition.MaxAmmo + MagazineBonus;

    public bool IsReloading => isReloading;
    public bool IsSwapDelayed => Time.time < swapReadyEndTime;

    public float SwapDelayProgress01 => Definition.SwapReadyDelay > 0f
        ? Mathf.Clamp01(1f - (swapReadyEndTime - Time.time) / Definition.SwapReadyDelay)
        : 1f;

    public float ReloadProgress01 => isReloading
        ? Mathf.Clamp01(1f - (reloadEndTime - Time.time) / Mathf.Max(EffectiveReloadTime, 0.0001f))
        : 1f;

    public bool CanFire => !isReloading && !IsSwapDelayed && Ammo > 0 && Time.time >= nextFireTime;

    private float nextFireTime;

    public void OnEquipped()
    {
        swapReadyEndTime = Time.time + Definition.SwapReadyDelay;
    }

    public void TryStartReload()
    {
        if (isReloading) return;
        if (Ammo >= EffectiveMaxAmmo) return;
        isReloading = true;
        reloadEndTime = Time.time + EffectiveReloadTime;

        if (IsMagazineEnhanced)
        {
            Debug.Log("[SwapEnhance] 재장전 시작 → 강화 상태 즉시 제거");
            IsMagazineEnhanced = false;
        }
    }

    public void CancelReload()
    {
        if (!isReloading) return;
        isReloading = false;
    }

    public void TickAmmoAxis()
    {
        if (isReloading && Time.time >= reloadEndTime)
        {
            Ammo = EffectiveMaxAmmo;
            isReloading = false;
            IsMagazineEnhanced = false;
        }
    }

    public void ConsumeShot()
    {
        Ammo = Mathf.Max(0, Ammo - 1);
        nextFireTime = Time.time + EffectiveFireInterval;

        if (Ammo <= 0)
            TryStartReload();
    }

    public void RefundAmmo(int amount)
    {
        Ammo = Mathf.Min(EffectiveMaxAmmo, Ammo + amount);
    }

    public struct DamageRoll
    {
        public float RawDamage;
        public bool IsCritical;
    }

    public DamageRoll RollDamage(float baseDamage)
    {
        bool isCritical = Random.value < PlayerCombatStats.CritChance;
        float raw = isCritical ? baseDamage * PlayerCombatStats.CritDamageMultiplier : baseDamage;
        return new DamageRoll { RawDamage = raw, IsCritical = isCritical };
    }

    public static float RoundFinalDamage(float value) => Mathf.Floor(value + 0.5f);
}