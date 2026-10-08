using UnityEngine;

// 권총, 라이플: 논리 총구에서 투사체 생성 (01-2 §5.1)
public class ProjectileFireMode : IWeaponFireMode
{
    public void Fire(in AttackSnapshot snapshot, Vector2 origin, Vector2 direction, GameObject projectilePrefab)
    {
        Projectile.Spawn(snapshot, origin, direction, projectilePrefab);
    }
}
