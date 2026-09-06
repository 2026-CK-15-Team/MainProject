using System;
using UnityEngine;

[RequireComponent(typeof(PlayerMovement))]
public class PlayerHealth : MonoBehaviour, IDamageable
{
    [Header("체력 파라미터")]
    public int MaxHP = 5;
    public float HitInvincibleDuration = 0.5f;

    public int CurrentHP { get; private set; }
    public bool IsDead { get; private set; }

    public event Action<int> HPChanged;
    public event Action Died;

    private float hitInvincibleUntil;
    private PlayerMovement movement;

    private bool IsHitInvincible => Time.time < hitInvincibleUntil;

    private void Awake()
    {
        movement = GetComponent<PlayerMovement>();
        CurrentHP = MaxHP;
    }

    public void TakeDamage(float amount)
    {
        if (IsDead) return;

        if (movement.IsDodgeInvincible) return; 
        if (IsHitInvincible) return;

        CurrentHP = Mathf.Max(0, CurrentHP - Mathf.RoundToInt(amount));
        HPChanged?.Invoke(CurrentHP);
        Debug.Log("Player Damged :" + CurrentHP);
        if (CurrentHP <= 0)
        {
            IsDead = true;
            Died?.Invoke();
            HandleDeath();
            return;
        }

        hitInvincibleUntil = Time.time + HitInvincibleDuration;
    }
    
    private void HandleDeath()
    {
        Debug.Log("Player Died");
        if (TryGetComponent<PlayerMovement>(out var movementComponent)) movementComponent.enabled = false;
        if (TryGetComponent<WeaponController>(out var weaponComponent)) weaponComponent.enabled = false;
    }
}