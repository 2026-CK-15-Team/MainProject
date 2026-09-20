using UnityEngine;

public class MonsterSeparation : MonoBehaviour
{
    private const string EnemyLayerName = "Enemy";

    public float SeparationRadius = 0.5f;
    public float SeparationSpeed = 0.5f;

    private MonsterKnockback knockback;

    private void Awake()
    {
        knockback = GetComponent<MonsterKnockback>();
    }

    private void Update()
    {
        if (HitStopState.IsActive) return;
        if (knockback != null && knockback.IsActive) return;

        int enemyMask = LayerMask.GetMask(EnemyLayerName);
        Collider2D[] nearby = Physics2D.OverlapCircleAll(transform.position, SeparationRadius, enemyMask);

        Vector2 pushAway = Vector2.zero;
        int count = 0;

        foreach (var other in nearby)
        {
            if (other.gameObject == gameObject) continue;

            Vector2 diff = (Vector2)transform.position - (Vector2)other.transform.position;
            float dist = diff.magnitude;
            if (dist < 0.001f) continue; // 완전히 겹친 극단적 경우는 방향을 못 정하니 건너뜀

            pushAway += diff.normalized * (1f - dist / SeparationRadius); // 가까울수록 세게 민다
            count++;
        }

        if (count == 0) return;

        pushAway /= count;
        transform.position += (Vector3)(pushAway * SeparationSpeed * Time.deltaTime);
    }
}
