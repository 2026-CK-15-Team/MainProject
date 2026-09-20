using UnityEngine;

[RequireComponent(typeof(DummyEnemy))]
public class BarrageAI : MonoBehaviour
{
    private const string WallLayerName = "Wall";

    public float KeepDistanceMin = 3.0f;
    public float KeepDistanceMax = 4.0f;
    public float AttackMaxDistance = 5.0f;
    public float TelegraphDuration = 0.6f;
    public float AttackInterval = 2.40f;
    public float MoveSpeed = 0.8f;

    public GameObject ProjectilePrefab;
    public float ProjectileSpeed = 2.6f;
    public float ProjectileMaxDistance = 5.5f;
    public int ProjectileDamage = 1;

    [SerializeField] private Transform muzzle;
    [SerializeField] private LineRenderer[] directionLines;
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Color pulseColor = new Color(1f, 0.4f, 1f);
    private const float PulseFrequency = 8f;

    private Color bodyOriginalColor;

    private Transform player;
    private MonsterKnockback knockback;
    private bool isTelegraphing;
    private float nextExecuteTime;
    private bool useOffsetPattern;

    private void Start()
    {
        var playerHealth = FindObjectOfType<PlayerHealth>();
        if (playerHealth != null) player = playerHealth.transform;

        knockback = GetComponent<MonsterKnockback>();
        if (spriteRenderer != null) bodyOriginalColor = spriteRenderer.color;

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
            nextExecuteTime = Time.time + TelegraphDuration;
        }
    }

    private void TickTelegraph()
    {
        DrawPatternPreview();
        TickBodyPulse();

        if (Time.time >= nextExecuteTime)
        {
            SetTelegraphVisuals(false);
            ExecuteOrCancel();
        }
    }

    private void TickBodyPulse()
    {
        if (spriteRenderer == null) return;

        float t = (Mathf.Sin(Time.time * PulseFrequency) + 1f) * 0.5f;
        spriteRenderer.color = Color.Lerp(bodyOriginalColor, pulseColor, t);
    }

    private void SetTelegraphVisuals(bool visible)
    {
        if (!visible && spriteRenderer != null)
            spriteRenderer.color = bodyOriginalColor;

        if (directionLines == null) return;
        foreach (var line in directionLines)
            if (line != null) line.enabled = visible;
    }

    private void ExecuteOrCancel()
    {
        float executeTime = nextExecuteTime;
        Vector2 toPlayer = (Vector2)player.position - (Vector2)transform.position;

        if (HasRangeAndLOS(toPlayer.magnitude))
        {
            FirePattern();
            useOffsetPattern = !useOffsetPattern;
        }

        isTelegraphing = false;
        nextExecuteTime = executeTime + AttackInterval;
    }

    private void FirePattern()
    {
        if (ProjectilePrefab == null) return;

        Vector3 origin = muzzle != null ? muzzle.position : transform.position;
        float baseAngle = useOffsetPattern ? 22.5f : 0f;

        for (int i = 0; i < 8; i++)
        {
            Vector2 direction = Quaternion.Euler(0, 0, baseAngle + 45f * i) * Vector2.right;

            GameObject go = ProjectilePool.Instance.Get(ProjectilePrefab, origin, Quaternion.identity);
            if (go.TryGetComponent<Projectile>(out var projectile))
                projectile.Launch(direction, ProjectileSpeed, ProjectileDamage, ProjectileMaxDistance, isEnemyAttack: true);
        }
    }

    private void DrawPatternPreview()
    {
        float baseAngle = useOffsetPattern ? 22.5f : 0f;
        bool hasLines = directionLines != null && directionLines.Length == 8;

        for (int i = 0; i < 8; i++)
        {
            Vector2 direction = Quaternion.Euler(0, 0, baseAngle + 45f * i) * Vector2.right;
            Debug.DrawRay(transform.position, direction * AttackMaxDistance, Color.magenta);

            if (!hasLines || directionLines[i] == null) continue;

            directionLines[i].enabled = true;
            directionLines[i].SetPosition(0, transform.position);
            directionLines[i].SetPosition(1, transform.position + (Vector3)(direction * AttackMaxDistance));
        }
    }

    private bool HasRangeAndLOS(float distance)
    {
        if (distance > AttackMaxDistance) return false;

        int wallMask = LayerMask.GetMask(WallLayerName);
        RaycastHit2D hit = Physics2D.Linecast(transform.position, player.position, wallMask);
        return hit.collider == null;
    }
}