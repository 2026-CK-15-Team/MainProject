using UnityEngine;

public interface IWeaponFireMode
{
    void Fire(WeaponRuntime weapon, Vector2 origin, Vector2 direction, GameObject projectilePrefab);
}