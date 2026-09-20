using UnityEngine;

public class FieldCurrency : MonoBehaviour
{
    public int Amount = 6;

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
        if (!other.TryGetComponent<PlayerHealth>(out _)) return;

        RunCurrency.Gain(Amount);
        gameObject.SetActive(false);
    }
}