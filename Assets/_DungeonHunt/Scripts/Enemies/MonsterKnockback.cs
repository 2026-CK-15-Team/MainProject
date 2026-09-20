using UnityEngine;

[RequireComponent(typeof(DummyEnemy))]
public class MonsterKnockback : MonoBehaviour
{
    private const string WallLayerName = "Wall";

    private Vector2 direction;
    private float initialSpeed;
    private float duration;
    private float elapsed;

    public bool IsActive { get; private set; }

    public void ApplyKnockback(Vector2 dir, float distance, float knockbackDuration)
    {
        if (IsActive) return;
        if (distance <= 0f || knockbackDuration <= 0f) return;

        direction = dir.normalized;
        duration = knockbackDuration;
        initialSpeed = 2f * distance / duration;
        elapsed = 0f;
        IsActive = true;
    }

    private void Update()
    {
        if (!IsActive) return;
        if (HitStopState.IsActive) return;

        elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(elapsed / duration);
        float currentSpeed = Mathf.Lerp(initialSpeed, 0f, t);
        float step = currentSpeed * Time.deltaTime;

        int wallMask = LayerMask.GetMask(WallLayerName);
        RaycastHit2D hit = Physics2D.Raycast(transform.position, direction, step, wallMask);
        if (hit.collider != null)
        {
            transform.position = hit.point;
            IsActive = false;
            return;
        }

        transform.position += (Vector3)(direction * step);

        if (elapsed >= duration)
            IsActive = false;
    }
}