using UnityEngine;

public class ConeFireMode : IWeaponFireMode
{
    private const string EnemyLayerName = "Enemy";
    private const string WallLayerName = "Wall";

    public void Fire(WeaponRuntime weapon, Vector2 origin, Vector2 direction, GameObject projectilePrefab)
    {
        float range = weapon.Definition.Range;
        float halfAngle = weapon.Definition.ConeAngle * 0.5f;

        int enemyMask = LayerMask.GetMask(EnemyLayerName);
        int wallMask = LayerMask.GetMask(WallLayerName);

        Collider2D[] hits = Physics2D.OverlapCircleAll(origin, range, enemyMask);

        foreach (var hit in hits)
        {
            Vector2 toTarget = (Vector2)hit.transform.position - origin;
            
            if (Vector2.Angle(direction, toTarget) > halfAngle) continue;
            
            RaycastHit2D wallCheck = Physics2D.Raycast(origin, toTarget.normalized, toTarget.magnitude, wallMask);
            if (wallCheck.collider != null) continue;

            if (hit.TryGetComponent<IDamageable>(out var damageable))
                damageable.TakeDamage(weapon.Definition.Damage);
        }
    }
}