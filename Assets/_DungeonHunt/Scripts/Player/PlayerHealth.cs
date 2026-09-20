using System;
using UnityEngine;

[RequireComponent(typeof(PlayerMovement))]
public class PlayerHealth : MonoBehaviour, IDamageable
{
    [Header("체력 파라미터")]
    public int MaxHP = 6;
    public float HitInvincibleDuration = 0.55f;

    [SerializeField] private int currentHP;
    public int CurrentHP => currentHP;
    public bool IsDead { get; private set; }

    public event Action<int> HPChanged;
    public event Action Died;
    public event Action Hurt;

    private float hitInvincibleUntil;
    private PlayerMovement movement;

    private bool IsHitInvincible => Time.time < hitInvincibleUntil;

    private void Awake()
    {
        movement = GetComponent<PlayerMovement>();
        currentHP = MaxHP;
        RunStats.StartRun();
        PlaytestLogger.StartRun();
    }

    public void TakeDamage(float amount, bool isJustDodgeEligible = false, bool isCritical = false)
    {
        if (IsDead) return;

        if (movement.IsDodgeInvincible)
        {
            if (isJustDodgeEligible) movement.TryTriggerJustDodge();
            return;
        }
        if (IsHitInvincible) return;

        currentHP = Mathf.Max(0, currentHP - Mathf.RoundToInt(amount));
        HPChanged?.Invoke(currentHP);
        Hurt?.Invoke();
        PlaytestLogger.Log("PlayerHit", $"amount={amount},hpAfter={currentHP},crit={isCritical}");

        if (currentHP <= 0)
        {
            IsDead = true;
            Died?.Invoke();
            HandleDeath();
            return;
        }

        hitInvincibleUntil = Time.time + HitInvincibleDuration;
    }

    public void Heal(int amount)
    {
        if (IsDead) return;
        currentHP = Mathf.Min(MaxHP, currentHP + amount);
        HPChanged?.Invoke(currentHP);
    }

    private void HandleDeath()
    {
        Debug.Log("Player Died");
        PlaytestLogger.Log("Death", $"position={transform.position}");
        PlaytestLogger.FinalizeRun("RunOver");
        RunCurrency.Reset();
        RunProgressState.Reset();
        FindObjectOfType<ResultScreenController>()?.ShowOver();
        if (TryGetComponent<PlayerMovement>(out var movementComponent)) movementComponent.enabled = false;
        if (TryGetComponent<WeaponController>(out var weaponComponent)) weaponComponent.enabled = false;
    }
}