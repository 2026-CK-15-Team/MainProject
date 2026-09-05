using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class Projectile : MonoBehaviour
{
    [SerializeField] private float lifeTime = 3f;

    private Vector2 direction;
    private float speed;
    private float damage;
    private Rigidbody2D rb;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    public void Launch(Vector2 dir, float projectileSpeed, float dmg)
    {
        direction = dir;
        speed = projectileSpeed;
        damage = dmg;
        transform.right = direction; 

        Destroy(gameObject, lifeTime);
    }

    private void FixedUpdate()
    {
        rb.MovePosition(rb.position + direction * speed * Time.fixedDeltaTime);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.TryGetComponent<IDamageable>(out var damageable))
        {
            damageable.TakeDamage(damage);
            Destroy(gameObject); 
            return;
        }

        if (other.CompareTag("Wall"))
        {
            Destroy(gameObject); 
        }
    }
}