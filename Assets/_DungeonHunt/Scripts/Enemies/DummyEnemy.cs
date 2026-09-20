using System;
using UnityEngine;

public class DummyEnemy : MonoBehaviour, IDamageable
{
    [Header("근접 유지형 기준값 (테스트용 스텁)")]
    public int MaxHP = 40;
    public int ContactDamage = 1;
    public float SpawnContactDamageGrace = 0.25f;
    public int CurrencyDrop = 1;

    [SerializeField] private GameObject damageNumberPrefab;

    [SerializeField] private int currentHP;
    public int CurrentHP => currentHP;

    public event Action<DummyEnemy> Died;
    public event Action Damaged;

    private float spawnGraceEndTime;

    private void Awake()
    {
        currentHP = MaxHP;
        spawnGraceEndTime = Time.time + SpawnContactDamageGrace;
    }

    public void TakeDamage(float amount, bool isJustDodgeEligible = false, bool isCritical = false)
    {
        if (currentHP <= 0) return;

        currentHP -= Mathf.RoundToInt(amount);
        Damaged?.Invoke();
        SpawnDamageNumber(amount, isCritical);

        if (currentHP <= 0)
        {
            HitStopState.Trigger(HitStopState.KillHitstopDuration);
            RunCurrency.Gain(CurrencyDrop);
            Died?.Invoke(this);
            Destroy(gameObject);
        }
    }

    private void SpawnDamageNumber(float amount, bool isCritical)
    {
        if (damageNumberPrefab == null) return;

        GameObject go = Instantiate(damageNumberPrefab, transform.position, Quaternion.identity);
        if (go.TryGetComponent<DamageNumber>(out var number))
            number.Show(amount, isCritical);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        if (Time.time < spawnGraceEndTime) return;

        if (other.TryGetComponent<IDamageable>(out var damageable))
            damageable.TakeDamage(ContactDamage);
    }
}