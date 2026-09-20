using UnityEngine;

[RequireComponent(typeof(DummyEnemy))]
public class ShooterAI : MonoBehaviour
{
    private const string WallLayerName = "Wall";

    public float KeepDistanceMin = 2.5f;
    public float KeepDistanceMax = 3.5f;
    public float AttackMaxDistance = 4.5f;
    public float AttackInterval = 1.20f;
    public float MoveSpeed = 0.9f;

    public GameObject ProjectilePrefab;
    public float ProjectileSpeed = 3.5f;
    public float ProjectileMaxDistance = 6.0f;
    public int ProjectileDamage = 1;

    [SerializeField] private Transform muzzle;

    private Transform player;
    private MonsterKnockback knockback;
    private float nextFireTime;

    private void Start()
    {
        var playerHealth = FindObjectOfType<PlayerHealth>();
        if (playerHealth != null) player = playerHealth.transform;

        knockback = GetComponent<MonsterKnockback>();

        nextFireTime = Time.time + Random.Range(AttackInterval * 0.5f, AttackInterval);
    }

    private void Update()
    {
        if (player == null) return;
        if (HitStopState.IsActive) return;

        Vector2 toPlayer = (Vector2)player.position - (Vector2)transform.position;
        float distance = toPlayer.magnitude;

        if (knockback == null || !knockback.IsActive)
        {
            if (distance > KeepDistanceMax)
                transform.position += (Vector3)(toPlayer.normalized * MoveSpeed * Time.deltaTime);
            else if (distance < KeepDistanceMin)
                transform.position -= (Vector3)(toPlayer.normalized * MoveSpeed * Time.deltaTime);
        }

        if (Time.time >= nextFireTime && HasRangeAndLOS(distance))
        {
            Fire(toPlayer.normalized);
            nextFireTime = Time.time + AttackInterval;
        }
    }

    private void Fire(Vector2 direction)
    {
        if (ProjectilePrefab == null) return;

        Vector3 origin = muzzle != null ? muzzle.position : transform.position;
        GameObject go = ProjectilePool.Instance.Get(ProjectilePrefab, origin, Quaternion.identity);
        if (go.TryGetComponent<Projectile>(out var projectile))
            projectile.Launch(direction, ProjectileSpeed, ProjectileDamage, ProjectileMaxDistance, isEnemyAttack: true);
    }

    private bool HasRangeAndLOS(float distance)
    {
        if (distance > AttackMaxDistance) return false;

        int wallMask = LayerMask.GetMask(WallLayerName);
        RaycastHit2D hit = Physics2D.Linecast(transform.position, player.position, wallMask);
        return hit.collider == null;
    }
}