using System;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class Projectile : MonoBehaviour
{
    [SerializeField] private float lifeTime = 3f;

    private Vector2 direction;
    private float speed;
    private float damage;
    private float maxDistance;
    private float traveledDistance;
    private bool canDeflectEnemyProjectiles;
    private bool isEnemyAttack;
    private bool isCritical;
    private float knockbackDistance;
    private float knockbackDuration;
    private float hitShakeAmplitude;
    private Func<IDamageable, float> damageResolver;
    private Rigidbody2D rb;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    public void Launch(Vector2 dir, float projectileSpeed, float dmg, float maxTravelDistance = Mathf.Infinity,
        bool canDeflectEnemyProjectiles = false, Func<IDamageable, float> damageResolver = null,
        bool isEnemyAttack = false, float knockbackDistance = 0f, float knockbackDuration = 0f,
        bool isCritical = false, float hitShakeAmplitude = 0f)
    {
        direction = dir;
        speed = projectileSpeed;
        damage = dmg;
        maxDistance = maxTravelDistance;
        traveledDistance = 0f;
        this.canDeflectEnemyProjectiles = canDeflectEnemyProjectiles;
        this.damageResolver = damageResolver;
        this.isEnemyAttack = isEnemyAttack;
        this.knockbackDistance = knockbackDistance;
        this.knockbackDuration = knockbackDuration;
        this.isCritical = isCritical;
        this.hitShakeAmplitude = hitShakeAmplitude;
        transform.right = direction;

        CancelInvoke(nameof(ReturnToPool));
        Invoke(nameof(ReturnToPool), lifeTime);
    }

    private void FixedUpdate()
    {
        if (HitStopState.IsActive) return;

        float step = speed * Time.fixedDeltaTime;
        rb.MovePosition(rb.position + direction * step);

        traveledDistance += step;
        if (traveledDistance >= maxDistance)
            ReturnToPool();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Wall"))
        {
            ReturnToPool();
            return;
        }

        if (canDeflectEnemyProjectiles
            && other.gameObject.layer == LayerMask.NameToLayer("EnemyProjectile")
            && other.TryGetComponent<Projectile>(out var enemyProjectile))
        {
            PlaytestLogger.Log("PistolDeflect", $"position={transform.position}");
            enemyProjectile.ReturnToPool();
            ReturnToPool();
            return;
        }

        if (other.TryGetComponent<IDamageable>(out var damageable))
        {
            float finalDamage = damageResolver != null ? damageResolver(damageable) : damage;
            damageable.TakeDamage(finalDamage, isEnemyAttack, isCritical);

            if (!isEnemyAttack)
                ScreenShake.Instance?.Shake(hitShakeAmplitude);

            if (knockbackDistance > 0f && other.TryGetComponent<MonsterKnockback>(out var knockback))
            {
                bool dashImmune = other.TryGetComponent<ChargerAI>(out var charger) && charger.IsDashing;
                if (!dashImmune)
                    knockback.ApplyKnockback(direction, knockbackDistance, knockbackDuration);
            }

            ReturnToPool();
        }
    }

    private void ReturnToPool()
    {
        CancelInvoke(nameof(ReturnToPool));
        ProjectilePool.Instance.Return(gameObject);
    }
}