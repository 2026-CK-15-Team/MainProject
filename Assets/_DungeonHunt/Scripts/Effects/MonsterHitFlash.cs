using System.Collections;
using UnityEngine;

[RequireComponent(typeof(DummyEnemy))]
public class MonsterHitFlash : MonoBehaviour
{
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private float flashDuration = 0.06f;

    private Color originalColor;
    private bool isFlashing;
    private DummyEnemy enemy;

    private void Awake()
    {
        enemy = GetComponent<DummyEnemy>();
        if (spriteRenderer != null) originalColor = spriteRenderer.color;
    }

    private void OnEnable() => enemy.Damaged += OnDamaged;
    private void OnDisable() => enemy.Damaged -= OnDamaged;

    private void OnDamaged()
    {
        if (isFlashing) return; // 재생 중이면 새 요청 무시
        StartCoroutine(FlashRoutine());
    }

    private IEnumerator FlashRoutine()
    {
        isFlashing = true;
        spriteRenderer.color = Color.white;
        yield return new WaitForSecondsRealtime(flashDuration);
        spriteRenderer.color = originalColor;
        isFlashing = false;
    }
}
