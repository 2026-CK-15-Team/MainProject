using UnityEngine;

public class ProjectileFireMode : IWeaponFireMode
{
    public void Fire(WeaponRuntime weapon, Vector2 origin, Vector2 direction, GameObject projectilePrefab)
    {
        if (projectilePrefab == null) return;

        GameObject go = Object.Instantiate(projectilePrefab, origin, Quaternion.identity);
        if (go.TryGetComponent<Projectile>(out var projectile))
            projectile.Launch(direction, weapon.Definition.ProjectileSpeed, weapon.Definition.Damage);
    }
}
