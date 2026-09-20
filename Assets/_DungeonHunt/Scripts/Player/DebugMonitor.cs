using UnityEngine;

public class DebugMonitor : MonoBehaviour
{
    [SerializeField] private PlayerHealth health;
    [SerializeField] private WeaponController weaponController;

    [Header("실시간 표시용 (플레이 중에만 값이 채워짐)")]
    [SerializeField] private int currentHP;
    [SerializeField] private int maxHP;
    [SerializeField] private int runCurrency;
    [SerializeField] private WeaponType currentWeaponType;
    [SerializeField] private int currentAmmo;
    [SerializeField] private int maxAmmo;
    [SerializeField] private bool isReloading;

    private void Update()
    {
        if (health != null)
        {
            currentHP = health.CurrentHP;
            maxHP = health.MaxHP;
        }

        runCurrency = RunCurrency.Amount;

        if (weaponController != null)
        {
            var weapon = weaponController.CurrentWeapon;
            currentWeaponType = weapon.Definition.WeaponType;
            currentAmmo = weapon.Ammo;
            maxAmmo = weapon.EffectiveMaxAmmo;
            isReloading = weapon.IsReloading;
        }
    }
}
