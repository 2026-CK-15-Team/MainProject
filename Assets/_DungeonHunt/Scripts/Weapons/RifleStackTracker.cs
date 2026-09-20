using UnityEngine;

public class RifleStackTracker
{
    private const float Timeout = 0.6f;
    private const int MaxStack = 5;
    private const float PerStackBonus = 0.10f;

    private IDamageable activeTarget;
    private DummyEnemy activeTargetEnemy;
    private int currentStack;
    private float lastValidHitTime = float.NegativeInfinity;

    public bool Enabled { get; set; }
    public int CurrentStack => currentStack;
    public float TimeoutDuration => Timeout;
    public float TimeRemaining => Mathf.Max(0f, Timeout - (Time.time - lastValidHitTime));

    private bool IsExpired => Time.time - lastValidHitTime > Timeout;

    public (IDamageable target, int stack) CaptureSnapshot()
    {
        if (IsExpired) ClearTarget();
        return (activeTarget, currentStack);
    }

    public float ResolveHit(IDamageable snapshotTarget, int snapshotStack, IDamageable actualTarget, float rawDamage)
    {
        if (IsExpired)
        {
            //Debug.Log("[RifleStack] 타임아웃(0.6s) 경과 → 스택 리셋");
            ClearTarget();
        }

        bool snapshotMatches = snapshotTarget != null && snapshotTarget == actualTarget;
        float finalRaw = snapshotMatches
            ? rawDamage * (1f + snapshotStack * PerStackBonus)
            : rawDamage;
        float finalDamage = WeaponRuntime.RoundFinalDamage(finalRaw);

        bool sameAsCurrentActive = activeTarget != null && activeTarget == actualTarget;
        if (sameAsCurrentActive)
        {
            currentStack = Mathf.Min(MaxStack, currentStack + 1);
        }
        else
        {
            SetActiveTarget(actualTarget);
            currentStack = 1;
        }

        lastValidHitTime = Time.time;

        //Debug.Log($"[RifleStack] 대상={actualTarget}, 스냅샷일치={snapshotMatches}(스냅샷스택={snapshotStack}), " +
                  //$"적용피해={finalDamage:F1}, 갱신후스택={currentStack}");
        //PlaytestLogger.Log("RifleStackHit",
           // $"snapshotTarget={snapshotTarget},actualTarget={actualTarget},snapshotMatches={snapshotMatches},finalDamage={finalDamage},stackAfter={currentStack}");

        return finalDamage;
    }

    private void SetActiveTarget(IDamageable target)
    {
        if (activeTargetEnemy != null)
            activeTargetEnemy.Died -= OnActiveTargetDied;

        activeTarget = target;
        activeTargetEnemy = target as DummyEnemy;

        if (activeTargetEnemy != null)
            activeTargetEnemy.Died += OnActiveTargetDied;
    }

    private void OnActiveTargetDied(DummyEnemy enemy)
    {
        //Debug.Log("[RifleStack] 대상 사망 → 즉시 스택 제거");
        ClearTarget();
    }

    public void ClearTarget()
    {
        if (activeTargetEnemy != null)
            activeTargetEnemy.Died -= OnActiveTargetDied;

        activeTarget = null;
        activeTargetEnemy = null;
        currentStack = 0;
    }
}