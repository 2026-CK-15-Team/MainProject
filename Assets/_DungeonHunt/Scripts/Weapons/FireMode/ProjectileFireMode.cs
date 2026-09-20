using UnityEngine;

public class ProjectileFireMode : IWeaponFireMode
{
    public void Fire(WeaponRuntime weapon, Vector2 origin, Vector2 direction, GameObject projectilePrefab)
    {
        if (projectilePrefab == null) return;

        GameObject go = ProjectilePool.Instance.Get(projectilePrefab, origin, Quaternion.identity);
        if (!go.TryGetComponent<Projectile>(out var projectile)) return;

        var roll = weapon.RollDamage(weapon.EffectiveDamage);
        //if (roll.IsCritical)
            //Debug.Log($"치명타! 확률={PlayerCombatStats.CritChance:P0}, 배율={PlayerCombatStats.CritDamageMultiplier}x, 기본데미지={weapon.EffectiveDamage}, 치명타데미지={roll.RawDamage}");

        System.Func<IDamageable, float> damageResolver = null;
        float flatDamage;

        if (weapon.RifleStack.Enabled)
        {
            var (snapshotTarget, snapshotStack) = weapon.RifleStack.CaptureSnapshot();
            damageResolver = actualTarget =>
                weapon.RifleStack.ResolveHit(snapshotTarget, snapshotStack, actualTarget, roll.RawDamage);
            flatDamage = 0f;
        }
        else
        {
            flatDamage = WeaponRuntime.RoundFinalDamage(roll.RawDamage);
        }

        projectile.Launch(direction, weapon.Definition.ProjectileSpeed, flatDamage, weapon.EffectiveRange,
            weapon.CanDeflectProjectiles, damageResolver, isEnemyAttack: false,
            knockbackDistance: weapon.Definition.KnockbackDistance, knockbackDuration: WeaponRuntime.KnockbackDuration,
            isCritical: roll.IsCritical, hitShakeAmplitude: weapon.Definition.HitShakeAmplitude);
    }
}