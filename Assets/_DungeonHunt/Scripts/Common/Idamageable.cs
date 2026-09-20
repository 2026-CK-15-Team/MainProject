public interface IDamageable
{
    void TakeDamage(float amount, bool isJustDodgeEligible = false, bool isCritical = false);
}