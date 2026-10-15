using UnityEngine;

public class SpriteFacingSwitcher : MonoBehaviour
{
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Sprite frontSprite;
    [SerializeField] private Sprite backSprite;
    [SerializeField] private bool frontFacesLeft = true;
    [SerializeField] private bool backFacesLeft = false;
    [SerializeField, Range(0f, 90f)] private float backEnterAngle = 40f;
    [SerializeField, Range(0f, 90f)] private float backExitAngle = 30f;
    [SerializeField] private PlayerMovement aimSource;
    [SerializeField] private Transform lookTarget;

    private const float HorizontalDeadZone = 0.05f;
    private const float MinDirectionSqr = 0.0001f;

    private bool showingBack;
    private bool lookingRight;

    private void Awake()
    {
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
    }

    private void Start()
    {
        if (aimSource == null && lookTarget == null)
        {
            var playerHealth = FindObjectOfType<PlayerHealth>();
            if (playerHealth != null) lookTarget = playerHealth.transform;
        }
        Apply();
    }

    private void OnValidate()
    {
        if (backExitAngle > backEnterAngle) backExitAngle = backEnterAngle;
    }

    private void Update()
    {
        if (!TryGetDirection(out Vector2 dir)) return;

        float enter = Mathf.Sin(backEnterAngle * Mathf.Deg2Rad);
        float exit = Mathf.Sin(backExitAngle * Mathf.Deg2Rad);

        if (!showingBack && dir.y > enter) showingBack = true;
        else if (showingBack && dir.y < exit) showingBack = false;

        if (Mathf.Abs(dir.x) > HorizontalDeadZone) lookingRight = dir.x > 0f;

        Apply();
    }

    private bool TryGetDirection(out Vector2 dir)
    {
        if (aimSource != null) dir = aimSource.GetAimDirection();
        else if (lookTarget != null) dir = lookTarget.position - transform.position;
        else dir = Vector2.zero;

        if (dir.sqrMagnitude < MinDirectionSqr) return false;

        dir.Normalize();
        return true;
    }

    private void Apply()
    {
        if (spriteRenderer == null) return;

        Sprite sprite = showingBack ? backSprite : frontSprite;
        if (sprite != null && spriteRenderer.sprite != sprite) spriteRenderer.sprite = sprite;

        bool spriteFacesLeft = showingBack ? backFacesLeft : frontFacesLeft;
        spriteRenderer.flipX = spriteFacesLeft ? lookingRight : !lookingRight;
    }
}
