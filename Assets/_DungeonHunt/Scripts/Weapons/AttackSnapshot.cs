// 발사 1회(탄약 1회 소비)의 결과 그릇 (01-2 §4 AttackExecutionID, §8.5 스냅샷).
// 발사 뒤 무기 전환이나 스탯 변화가 있어도 이 값은 안 바뀜.
public struct AttackSnapshot
{
    public int AttackExecutionId;
    public WeaponType Weapon;
    public int Damage;
    public bool IsCritical;
    public float ProjectileSpeed;
    public float Range;
    public float ConeAngle;
    public float HitRadius;
    public float KnockbackDistance;
    public float BossDistance;

    private static int nextId = 1;
    public static int NextId() => nextId++;

    public DamageInfo ToDamage(UnityEngine.Vector2 hitPoint, UnityEngine.Vector2 knockbackDirection) => new DamageInfo
    {
        Amount = Damage,
        IsCritical = IsCritical,
        AttackExecutionId = AttackExecutionId,
        Weapon = Weapon,
        Source = DamageSource.Player,
        HitPoint = hitPoint,
        KnockbackDirection = knockbackDirection,
        KnockbackDistance = KnockbackDistance,
        BossDistance = BossDistance,
    };
}
