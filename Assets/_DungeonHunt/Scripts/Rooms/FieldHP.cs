using UnityEngine;

public class FieldHP : MonoBehaviour
{
    public int Amount = 1;

    [SerializeField] private Collider2D pickupCollider;
    [SerializeField] private float enableDelay = 0.25f;

    private void Awake()
    {
        if (pickupCollider != null)
        {
            pickupCollider.enabled = false;
            Invoke(nameof(EnablePickup), enableDelay);
        }
    }

    private void EnablePickup()
    {
        if (pickupCollider != null) pickupCollider.enabled = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.TryGetComponent<PlayerHealth>(out var health)) return;
        if (health.CurrentHP >= health.MaxHP) return; // 만피면 필드에 그대로 남는다

        health.Heal(Amount);
        gameObject.SetActive(false);
    }
}