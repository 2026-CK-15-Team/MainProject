using UnityEngine;

[RequireComponent(typeof(DummyEnemy))]
public class ChargerAI : MonoBehaviour
{
    private const string WallLayerName = "Wall";

    private enum Phase { Approaching, Telegraphing, Dashing, Recovering }

    public float TriggerDistance = 3.0f;
    public float TelegraphDuration = 0.65f;
    public float DashSpeed = 6.0f;
    public float DashDistance = 3.0f;
    public float PostDashRecovery = 0.80f;
    public float AttackInterval = 2.40f;
    public int DashDamage = 1;
    public float ApproachSpeed = 1.4f;
    public float DashHitRadius = 0.6f;

    [SerializeField] private LineRenderer telegraphLine;

    private Transform player;
    private MonsterKnockback knockback;
    private Phase phase = Phase.Approaching;

    public bool IsDashing => phase == Phase.Dashing;

    private bool directionLocked;
    private Vector2 lockedDirection;
    private float nextExecuteTime;
    private float dashStartTime;

    private float dashTraveled;
    private bool dashHitSomething;
    private float recoveryEndTime;

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

        switch (phase)
        {
            case Phase.Approaching: TickApproaching(); break;
            case Phase.Telegraphing: TickTelegraphing(); break;
            case Phase.Dashing: TickDashing(); break;
            case Phase.Recovering: TickRecovering(); break;
        }
    }

    private void TickApproaching()
    {
        Vector2 toPlayer = (Vector2)player.position - (Vector2)transform.position;

        if (knockback == null || !knockback.IsActive)
            transform.position += (Vector3)(toPlayer.normalized * ApproachSpeed * Time.deltaTime);

        float scheduledTelegraphStart = nextExecuteTime - TelegraphDuration;
        if (Time.time >= scheduledTelegraphStart && toPlayer.magnitude <= TriggerDistance)
        {
            phase = Phase.Telegraphing;
            directionLocked = false;
            nextExecuteTime = Time.time + TelegraphDuration;
        }
    }

    private void TickTelegraphing()
    {
        Vector2 toPlayer = (Vector2)player.position - (Vector2)transform.position;
        float timeToExecute = nextExecuteTime - Time.time;

        if (!directionLocked && timeToExecute <= 0.25f)
        {
            lockedDirection = toPlayer.normalized;
            directionLocked = true;
        }

        Vector2 debugDir = directionLocked ? lockedDirection : toPlayer.normalized;
        Debug.DrawRay(transform.position, debugDir * DashDistance, directionLocked ? Color.red : Color.yellow);

        if (telegraphLine != null)
        {
            telegraphLine.enabled = true;
            telegraphLine.startColor = telegraphLine.endColor = directionLocked ? Color.red : Color.yellow;
            telegraphLine.SetPosition(0, transform.position);
            telegraphLine.SetPosition(1, transform.position + (Vector3)(debugDir * DashDistance));
        }

        if (Time.time >= nextExecuteTime)
        {
            if (telegraphLine != null) telegraphLine.enabled = false;

            if (toPlayer.magnitude <= TriggerDistance)
            {
                phase = Phase.Dashing;
                dashStartTime = Time.time;
                dashTraveled = 0f;
                dashHitSomething = false;
            }
            else
            {
                phase = Phase.Approaching;
                nextExecuteTime = Time.time + AttackInterval;
            }
        }
    }

    private void TickDashing()
    {
        float remaining = DashDistance - dashTraveled;
        float moveDist = Mathf.Min(DashSpeed * Time.deltaTime, remaining);

        int wallMask = LayerMask.GetMask(WallLayerName);
        RaycastHit2D wallHit = Physics2D.Raycast(transform.position, lockedDirection, moveDist, wallMask);
        if (wallHit.collider != null)
        {
            transform.position = wallHit.point;
            EndDash();
            return;
        }

        Vector3 nextPos = transform.position + (Vector3)(lockedDirection * moveDist);

        if (!dashHitSomething && Vector2.Distance(nextPos, player.position) <= DashHitRadius)
        {
            if (player.TryGetComponent<IDamageable>(out var damageable))
                damageable.TakeDamage(DashDamage, true);
            dashHitSomething = true;
            transform.position = nextPos;
            EndDash();
            return;
        }

        transform.position = nextPos;
        dashTraveled += moveDist;

        if (dashTraveled >= DashDistance)
            EndDash();
    }

    private void EndDash()
    {
        phase = Phase.Recovering;
        recoveryEndTime = Time.time + PostDashRecovery;
        nextExecuteTime = dashStartTime + AttackInterval;
    }

    private void TickRecovering()
    {
        if (Time.time >= recoveryEndTime)
            phase = Phase.Approaching;
    }
}