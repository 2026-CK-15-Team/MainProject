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
    public float BossDistance;   // 발사 순간 보스 중심~플레이어 거리 (05 §8.7). 보스가 없으면 0
    public bool Reduced;         // 거리 배율로 줄어든 피해 (피해 숫자 색 구분)

    public static DamageInfo Simple(int amount, DamageSource source)
        => new DamageInfo { Amount = amount, Source = source, AttackExecutionId = -1 };
}
