using UnityEngine;

public interface IWeaponFireMode
{
    // projectilePrefab: 무기 정의의 프리팹, 없으면 WeaponController 기본 프리팹, 그것도 없으면 null(기본 도형)
    void Fire(in AttackSnapshot snapshot, Vector2 origin, Vector2 direction, GameObject projectilePrefab);
}
