using System;
using UnityEngine;

public class DummyEnemy : MonoBehaviour, IDamageable
{
    [Header("더미 적 파라미터 움직임x")]
    public int MaxHP = 30;
    public int ContactDamage = 1;
    
    public int CurrentHP { get; private set; }

    public event Action<DummyEnemy> Died;

    private void Awake()
    {
        CurrentHP = MaxHP;
    }

    public void TakeDamage(float amount)
    {
        if (CurrentHP <= 0) return; 

        CurrentHP -= Mathf.RoundToInt(amount);
        Debug.Log(CurrentHP);
        if (CurrentHP <= 0)
        {
            Died?.Invoke(this);
            Destroy(gameObject);
        }
    }
    
    private void OnTriggerStay2D(Collider2D other)
    {
        if (other.TryGetComponent<IDamageable>(out var damageable))
            damageable.TakeDamage(ContactDamage);
    }
}