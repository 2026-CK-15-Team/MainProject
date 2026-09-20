using UnityEngine;

[RequireComponent(typeof(DummyEnemy))]
public class BrawlerAI : MonoBehaviour
{
    private const string WallLayerName = "Wall";

    public float KeepDistanceMin = 0.55f;
    public float KeepDistanceMax = 0.80f;
    public float AttackRange = 1.00f;
    public float AttackArcDegrees = 120f;
    public float TelegraphDuration = 0.45f;
    public float AttackInterval = 1.50f;
    public int AttackDamage = 1;
    public float MoveSpeed = 1.2f;

    [SerializeField] private LineRenderer directionLine;
    [SerializeField] private LineRenderer arcOutline;
    private const int ArcSegments = 10;

    private Transform player;
    private MonsterKnockback knockback;
    private bool isTelegraphing;
    private bool directionLocked;
    private Vector2 lockedDirection;
    private float nextExecuteTime;

    private void Start()
    {
        var playerHealth = FindObjectOfType<PlayerHealth>();
        if (playerHealth != null) player = playerHealth.transform;

        knockback = GetComponent<MonsterKnockback>();

        float minDelay = Mathf.Max(AttackInterval * 0.5f, TelegraphDuration);
        nextExecuteTime = Time.time + Random.Range(minDelay, AttackInterval);
    }

    private void Update()
    {
        if (player == null) return;
        if (HitStopState.IsActive) return;

        if (isTelegraphing)
            TickTelegraph();
        else
            TickMovementAndSchedule();
    }

    private void TickMovementAndSchedule()
    {
        Vector2 toPlayer = (Vector2)player.position - (Vector2)transform.position;
        float distance = toPlayer.magnitude;

        if (knockback == null || !knockback.IsActive)
        {
            if (distance > KeepDistanceMax)
                transform.position += (Vector3)(toPlayer.normalized * MoveSpeed * Time.deltaTime);
            else if (distance < KeepDistanceMin)
                transform.position -= (Vector3)(toPlayer.normalized * MoveSpeed * Time.deltaTime);
        }

        float scheduledTelegraphStart = nextExecuteTime - TelegraphDuration;
        if (Time.time >= scheduledTelegraphStart && HasRangeAndLOS(distance))
        {
            isTelegraphing = true;
            directionLocked = false;
            nextExecuteTime = Time.time + TelegraphDuration;
        }
    }

    private void TickTelegraph()
    {
        float timeToExecute = nextExecuteTime - Time.time;

        if (!directionLocked && timeToExecute <= 0.25f)
        {
            lockedDirection = ((Vector2)player.position - (Vector2)transform.position).normalized;
            directionLocked = true;
        }

        Vector2 debugDir = directionLocked ? lockedDirection : ((Vector2)player.position - (Vector2)transform.position).normalized;
        Debug.DrawRay(transform.position, debugDir * AttackRange, directionLocked ? Color.red : Color.yellow);

        if (directionLine != null)
        {
            directionLine.enabled = true;
            directionLine.startColor = directionLine.endColor = directionLocked ? Color.red : Color.yellow;
            directionLine.SetPosition(0, transform.position);
            directionLine.SetPosition(1, transform.position + (Vector3)(debugDir * AttackRange));
        }

        if (directionLocked)
        {
            float half = AttackArcDegrees * 0.5f;
            Vector2 leftBoundary = Quaternion.Euler(0, 0, half) * lockedDirection;
            Vector2 rightBoundary = Quaternion.Euler(0, 0, -half) * lockedDirection;
            Debug.DrawRay(transform.position, leftBoundary * AttackRange, Color.cyan);
            Debug.DrawRay(transform.position, rightBoundary * AttackRange, Color.cyan);

            DrawArcOutline(half);
        }

        if (Time.time >= nextExecuteTime)
        {
            if (directionLine != null) directionLine.enabled = false;
            if (arcOutline != null) arcOutline.enabled = false;
            ExecuteOrCancel();
        }
    }

    private void ExecuteOrCancel()
    {
        float executeTime = nextExecuteTime;
        Vector2 toPlayer = (Vector2)player.position - (Vector2)transform.position;
        float distance = toPlayer.magnitude;

        if (HasRangeAndLOS(distance))
        {
            float angle = Vector2.Angle(lockedDirection, toPlayer);
            if (angle <= AttackArcDegrees * 0.5f && player.TryGetComponent<IDamageable>(out var damageable))
                damageable.TakeDamage(AttackDamage, true);
        }

        isTelegraphing = false;
        nextExecuteTime = executeTime + AttackInterval;
    }

    private void DrawArcOutline(float halfAngleDegrees)
    {
        if (arcOutline == null) return;

        arcOutline.enabled = true;
        arcOutline.loop = true;
        arcOutline.positionCount = ArcSegments + 1;

        arcOutline.SetPosition(0, transform.position);
        for (int i = 0; i <= ArcSegments - 1; i++)
        {
            float t = (float)i / (ArcSegments - 1);
            float angle = Mathf.Lerp(halfAngleDegrees, -halfAngleDegrees, t);
            Vector2 dir = Quaternion.Euler(0, 0, angle) * lockedDirection;
            arcOutline.SetPosition(i + 1, transform.position + (Vector3)(dir * AttackRange));
        }
    }

    private bool HasRangeAndLOS(float distance)
    {
        if (distance > AttackRange) return false;

        int wallMask = LayerMask.GetMask(WallLayerName);
        RaycastHit2D hit = Physics2D.Linecast(transform.position, player.position, wallMask);
        return hit.collider == null;
    }
}