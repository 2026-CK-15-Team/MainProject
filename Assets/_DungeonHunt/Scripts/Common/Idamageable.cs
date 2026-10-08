// 피해를 받는 대상. 피해량과 치명타 같은 결과는 공격 쪽에서 확정해서 넘김 (01-2 §8.5 스냅샷).
public interface IDamageable
{
    void TakeDamage(DamageInfo info);
}
