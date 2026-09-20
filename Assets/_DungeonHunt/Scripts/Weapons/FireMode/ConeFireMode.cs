using UnityEngine;

public class ConeFireMode : IWeaponFireMode
{
    private const string EnemyLayerName = "Enemy";
    private const string WallLayerName = "Wall";

    public void Fire(WeaponRuntime weapon, Vector2 origin, Vector2 direction, GameObject projectilePrefab)
    {
        float range = weapon.EffectiveRange;
        float halfAngle = weapon.Definition.ConeAngle * 0.5f;

        int enemyMask = LayerMask.GetMask(EnemyLayerName);
        int wallMask = LayerMask.GetMask(WallLayerName);

        // ---- 임시 디버그 시각화 ----
        // Scene 뷰에서 Play 중일 때만 보임. Game 뷰에선 안 보이니 Scene 탭 열어두고 확인할 것.
        Vector2 leftBoundary = Quaternion.Euler(0, 0, halfAngle) * direction;
        Vector2 rightBoundary = Quaternion.Euler(0, 0, -halfAngle) * direction;
        Debug.DrawRay(origin, leftBoundary * range, Color.yellow, 1f);
        Debug.DrawRay(origin, rightBoundary * range, Color.yellow, 1f);
        Debug.DrawRay(origin, direction * range, Color.red, 1f); // 중심 조준선

        if (weapon.Definition.ConeFlashPrefab != null)
        {
            GameObject flashGo = Object.Instantiate(weapon.Definition.ConeFlashPrefab, Vector3.zero, Quaternion.identity);
            if (flashGo.TryGetComponent<ConeFlash>(out var flash))
                flash.Show(origin, direction, range, weapon.Definition.ConeAngle);
        }

        PlaytestLogger.Log("Fire", $"weapon=Shotgun,enhanced={weapon.IsMagazineEnhanced},finalRange={range}");

        Collider2D[] hits = Physics2D.OverlapCircleAll(origin, range, enemyMask);

        var roll = weapon.RollDamage(weapon.EffectiveDamage);
        //if (roll.IsCritical)
            //Debug.Log($"치명타! 확률={PlayerCombatStats.CritChance:P0}, 배율={PlayerCombatStats.CritDamageMultiplier}x, 기본데미지={weapon.EffectiveDamage}, 치명타데미지={roll.RawDamage}");
        float finalDamage = WeaponRuntime.RoundFinalDamage(roll.RawDamage);

        foreach (var hit in hits)
        {
            Vector2 toTarget = (Vector2)hit.transform.position - origin;

            if (Vector2.Angle(direction, toTarget) > halfAngle) continue;

            RaycastHit2D wallCheck = Physics2D.Raycast(origin, toTarget.normalized, toTarget.magnitude, wallMask);

            Debug.DrawLine(origin, hit.transform.position, wallCheck.collider != null ? Color.gray : Color.green, 1f);

            if (wallCheck.collider != null) continue;

            if (hit.TryGetComponent<IDamageable>(out var damageable))
            {
                //Debug.Log($"샷건 명중: {hit.name}, 적용피해={finalDamage}, 치명타={roll.IsCritical}");
                damageable.TakeDamage(finalDamage, isCritical: roll.IsCritical);
                ScreenShake.Instance?.Shake(weapon.Definition.HitShakeAmplitude);

                if (weapon.Definition.KnockbackDistance > 0f && hit.TryGetComponent<MonsterKnockback>(out var knockback))
                {
                    bool dashImmune = hit.TryGetComponent<ChargerAI>(out var charger) && charger.IsDashing;
                    if (!dashImmune)
                        knockback.ApplyKnockback(toTarget.normalized, weapon.Definition.KnockbackDistance, WeaponRuntime.KnockbackDuration);
                }
            }
        }
    }
}