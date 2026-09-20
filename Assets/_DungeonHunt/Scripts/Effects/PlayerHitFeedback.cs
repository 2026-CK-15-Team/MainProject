using System.Collections;
using UnityEngine;

public class PlayerHitFeedback : MonoBehaviour
{
    [SerializeField] private PlayerHealth health;
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private float tintDuration = 0.06f;
    [SerializeField] private float hitShakeAmplitude = 0.08f;

    private Color originalColor;
    private bool isTinting;

    private void Awake()
    {
        if (spriteRenderer != null) originalColor = spriteRenderer.color;
    }

    private void OnEnable() => health.Hurt += OnHurt;
    private void OnDisable() => health.Hurt -= OnHurt;

    private void OnHurt()
    {
        ScreenShake.Instance?.Shake(hitShakeAmplitude);

        if (isTinting) return;
        StartCoroutine(TintRoutine());
    }

    private IEnumerator TintRoutine()
    {
        isTinting = true;
        spriteRenderer.color = Color.red;
        yield return new WaitForSecondsRealtime(tintDuration);
        spriteRenderer.color = originalColor;
        isTinting = false;
    }
}
