using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(PlayerInputReader))]
public class WeaponController : MonoBehaviour
{
    [SerializeField] private WeaponScriptable[] definitions = new WeaponScriptable[3];
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private Transform muzzle;
    private WeaponRuntime[] weaponSlots;
    private int currentIndex;
    private PlayerInputReader input;

    public WeaponRuntime CurrentWeapon => weaponSlots[currentIndex];
    
    private void Awake()
    {
        input = GetComponent<PlayerInputReader>();

        weaponSlots = new WeaponRuntime[definitions.Length];
        for (int i = 0; i < definitions.Length; i++)
        {
            IWeaponFireMode fireMode = CreateFireMode(definitions[i].WeaponType);
            weaponSlots[i] = new WeaponRuntime(definitions[i], fireMode);
        }

        weaponSlots[currentIndex].OnEquipped(); 
    }

    private void OnEnable() => input.WeaponChangeRequested += OnWeaponChangeRequested;
    private void OnDisable() => input.WeaponChangeRequested -= OnWeaponChangeRequested;

    private void Update()
    {
        foreach (var weapon in weaponSlots)
            weapon.TickAmmoAxis();

        if (input.IsAttackHeld && CurrentWeapon.CanFire)
            Fire();
    }

    private void OnWeaponChangeRequested(int direction)
    {
        if (weaponSlots.Length <= 1) return; 

        currentIndex = (currentIndex + direction + weaponSlots.Length) % weaponSlots.Length;
        CurrentWeapon.OnEquipped(); 
    }

    private IWeaponFireMode CreateFireMode(WeaponType type) => type switch
    {
        WeaponType.Pistol => new ProjectileFireMode(),
        WeaponType.Rifle  => new ProjectileFireMode(),
        WeaponType.Shotgun => new ConeFireMode(),
        _ => throw new System.ArgumentOutOfRangeException()
    };
    private void Fire()
    {
        CurrentWeapon.ConsumeShot();

        Vector3 origin = muzzle != null ? muzzle.position : transform.position;
        Vector2 direction = AimUtility.ScreenPointToWorldDirection(input.AimScreenPosition, origin);

        CurrentWeapon.FireMode.Fire(CurrentWeapon, origin, direction, projectilePrefab);
    }
}