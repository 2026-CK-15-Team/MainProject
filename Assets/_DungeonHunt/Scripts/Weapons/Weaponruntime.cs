using UnityEngine;

public class WeaponRuntime
{
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

    public bool IsReloading => isReloading;
    public bool IsSwapDelayed => Time.time < swapReadyEndTime;

    public float ReloadProgress01 => isReloading
        ? Mathf.Clamp01(1f - (reloadEndTime - Time.time) / Mathf.Max(Definition.ReloadTime, 0.0001f))
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
        isReloading = true;
        reloadEndTime = Time.time + Definition.ReloadTime;
    }

    public void TickAmmoAxis()
    {
        if (isReloading && Time.time >= reloadEndTime)
        {
            Ammo = Definition.MaxAmmo;
            isReloading = false;
        }
    }

    public void ConsumeShot()
    {
        Ammo = Mathf.Max(0, Ammo - 1);
        nextFireTime = Time.time + Definition.FireInterval;

        if (Ammo <= 0)
            TryStartReload();
    }

    public void RefundAmmo(int amount)
    {
        Ammo = Mathf.Min(Definition.MaxAmmo, Ammo + amount);
    }
}