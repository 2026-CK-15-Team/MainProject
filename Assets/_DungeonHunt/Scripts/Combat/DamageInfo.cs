using UnityEngine;

public enum DamageSource
{
    Player,
    Monster,
}

// 한 번의 명중 결과. 발사 시점 스냅샷(AttackSnapshot)에서 만들어짐.
public struct DamageInfo
{
    public int Amount;
    public bool IsCritical;
    public int AttackExecutionId;
    public WeaponType Weapon;
    public DamageSource Source;
    public Vector2 HitPoint;
    public Vector2 KnockbackDirection;
    public float KnockbackDistance;

    public static DamageInfo Simple(int amount, DamageSource source)
        => new DamageInfo { Amount = amount, Source = source, AttackExecutionId = -1 };
}
