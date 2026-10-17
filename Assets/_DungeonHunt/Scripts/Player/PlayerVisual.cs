using UnityEngine;

// 01-1 §5.3 캐릭터/무기 방향 표현 + 09 플레이어 피드백 (피격 색, 무적 점멸, 저스트회피 외곽선).
// 스프라이트가 없으면 기본 도형으로 그림. 아트가 들어오면 bodySprites(좌상, 좌하, 우상, 우하)에 넣으면 됨.
public class PlayerVisual : MonoBehaviour
{
    [Tooltip("좌상, 좌하, 우상, 우하 순서 4방향 몸통 스프라이트. 비우면 기본 도형")]
    [SerializeField] private Sprite[] bodySprites = new Sprite[4];
    [SerializeField] private SpriteRenderer bodyRenderer;
    [SerializeField] private Transform weaponPivot;
    [SerializeField] private SpriteRenderer weaponRenderer;
    [SerializeField] private Color bodyColor = new Color(0.95f, 0.85f, 0.4f);
    [SerializeField] private Color hitTint = new Color(1f, 0.3f, 0.3f);

    private SpriteRenderer outline;
    private float ghostTimer;
    private float walkPhase;
    private bool dead;
    private float deadAt;
    private float hitTintUntil;
    private float outlineUntil;
    private PlayerController player;

    public Transform WeaponPivot => weaponPivot;

    private void Awake()
    {
        player = GetComponent<PlayerController>();
        if (bodyRenderer == null)
        {
            bodyRenderer = GetComponent<SpriteRenderer>();
            if (bodyRenderer == null || bodyRenderer.sprite == null)
                bodyRenderer = PlaceholderArt.CreateShape("Body", transform, PlaceholderArt.Circle, bodyColor, 0.56f, 100);
        }
        if (weaponPivot == null)
        {
            weaponPivot = new GameObject("WeaponPivot").transform;
            weaponPivot.SetParent(transform, false);
        }
        if (weaponRenderer == null)
        {
            weaponRenderer = PlaceholderArt.CreateShape("Weapon", weaponPivot, PlaceholderArt.Square, new Color(0.3f, 0.3f, 0.35f), 1f, 101);
            weaponRenderer.transform.localScale = new Vector3(0.45f, 0.1f, 1f);
            weaponRenderer.transform.localPosition = new Vector3(0.3f, 0f, 0f);
        }
        outline = PlaceholderArt.CreateShape("JustDodgeOutline", transform, PlaceholderArt.Ring, Color.white, 0.8f, 99);
        outline.enabled = false;
    }

    private void OnEnable()
    {
        GameEvents.PlayerHit += OnPlayerHit;
        GameEvents.JustDodgeTriggered += OnJustDodge;
        GameEvents.PlayerDied += OnPlayerDied;
        GameEvents.RunStarted += OnRunStarted;
    }

    private void OnDisable()
    {
        GameEvents.PlayerHit -= OnPlayerHit;
        GameEvents.JustDodgeTriggered -= OnJustDodge;
        GameEvents.PlayerDied -= OnPlayerDied;
        GameEvents.RunStarted -= OnRunStarted;
    }

    // 무기 모양 바꾸기 (무기 전환 시 WeaponController가 호출)
    public void SetWeaponSprite(Sprite sprite, Color fallbackColor)
    {
        if (weaponRenderer == null) return;
        if (sprite != null)
        {
            weaponRenderer.sprite = sprite;
            weaponRenderer.color = Color.white;
            weaponRenderer.transform.localScale = Vector3.one;
        }
        else
        {
            weaponRenderer.color = fallbackColor;
        }
    }

    // 논리 총구 = 무기 스프라이트에 붙은 지점 (01-2 §8.1)
    public Vector2 MuzzlePosition(Vector2 aimDirection)
    {
        Vector2 pivot = weaponPivot != null ? (Vector2)weaponPivot.position : (Vector2)transform.position;
        return pivot + aimDirection.normalized * 0.5f;
    }

    private void LateUpdate()
    {
        if (player == null) return;
        Vector2 aim = player.Movement.GetAimDirection();

        // 무기는 360도 회전, 몸통은 커서 분면에 스냅
        float angle = Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg;
        weaponPivot.rotation = Quaternion.Euler(0f, 0f, angle);
        bool facingLeft = aim.x < 0f;
        if (weaponRenderer != null) weaponRenderer.flipY = facingLeft;

        int quadrant = aim.x < 0f ? (aim.y >= 0f ? 0 : 1) : (aim.y >= 0f ? 2 : 3);
        if (bodySprites != null && bodySprites.Length == 4 && bodySprites[quadrant] != null)
            bodyRenderer.sprite = bodySprites[quadrant];

        UpdateTintAndBlink();
        UpdateMotion();
    }

    // 기본 도형용 모션: 이동 중 통통 튐 / 회피 잔상 / 사망 시 쓰러짐 (09 §3 플레이어 대기, 이동, 회피, 사망)
    private void UpdateMotion()
    {
        Transform body = bodyRenderer.transform;
        // 몸통이 루트 자신이면(아트 프리팹) 크기/회전 모션 생략. 잔상만
        if (body == transform) body = null;
        if (dead && body != null)
        {
            float t = Mathf.Clamp01((Time.unscaledTime - deadAt) / 0.4f);
            body.localScale = new Vector3(1f + t * 0.3f, 1f - t * 0.6f, 1f) * BaseBodyScale;
            body.localRotation = Quaternion.Euler(0f, 0f, t * 80f);
            Color c = bodyRenderer.color;
            bodyRenderer.color = Color.Lerp(c, new Color(0.35f, 0.35f, 0.38f, 1f), t);
            if (weaponRenderer != null) weaponRenderer.enabled = false;
            return;
        }

        float dt = GameClock.CombatDelta;
        bool moving = player.Input != null && player.Input.MoveInput.sqrMagnitude > 0.01f;
        if (player.Movement.IsDodging)
        {
            if (body != null) body.localScale = new Vector3(1.15f, 0.85f, 1f) * BaseBodyScale;
            ghostTimer -= dt;
            if (ghostTimer <= 0f)
            {
                ghostTimer = 0.03f;
                Color ghost = bodyRenderer.color;
                ghost.a = 0.35f;
                GameServices.Feedback?.SpawnGhost(bodyRenderer.sprite, transform.position, bodyRenderer.bounds.size.x, ghost);
            }
        }
        else if (moving)
        {
            walkPhase += dt * 14f;
            float bob = Mathf.Abs(Mathf.Sin(walkPhase));
            if (body != null) body.localScale = new Vector3(1f - bob * 0.06f, 1f + bob * 0.08f, 1f) * BaseBodyScale;
        }
        else
        {
            walkPhase = 0f;
            // 대기: 아주 느린 숨쉬기
            float breathe = Mathf.Sin(Time.unscaledTime * 2.5f) * 0.02f;
            if (body != null) body.localScale = new Vector3(1f, 1f + breathe, 1f) * BaseBodyScale;
        }
    }

    private float baseBodyScale = -1f;
    private float BaseBodyScale => baseBodyScale > 0f ? baseBodyScale : baseBodyScale = bodyRenderer.transform.localScale.x;

    private void OnPlayerDied()
    {
        dead = true;
        deadAt = Time.unscaledTime;
    }

    private void OnRunStarted()
    {
        dead = false;
        if (bodyRenderer != null && bodyRenderer.transform != transform)
        {
            bodyRenderer.transform.localRotation = Quaternion.identity;
            bodyRenderer.transform.localScale = Vector3.one * BaseBodyScale;
        }
        if (weaponRenderer != null) weaponRenderer.enabled = true;
    }

    private void UpdateTintAndBlink()
    {
        float now = Time.unscaledTime;
        FeedbackBalance fb = GameBalance.Current.Feedback;
        Color color = bodySprites != null && bodySprites.Length == 4 && bodySprites[0] != null ? Color.white : bodyColor;
        if (now < hitTintUntil)
            color = hitTint;

        // 피격 무적 점멸: 40% ~ 100% 불투명 (09 §8)
        if (player.Health.IsHitInvincible)
        {
            float period = Mathf.Max(0.01f, fb.PlayerInvincibleBlinkPeriod);
            bool dim = Mathf.FloorToInt(GameClock.CombatTime / period) % 2 == 0;
            color.a = dim ? 0.4f : 1f;
        }
        // 효과 무적 (저스트회피 무적 등): 금색으로 깜빡임
        else if (player.Health.IsEffectInvincible)
        {
            bool on = Mathf.FloorToInt(GameClock.CombatTime / 0.1f) % 2 == 0;
            color = Color.Lerp(color, new Color(1f, 0.9f, 0.4f, color.a), on ? 0.7f : 0.3f);
        }
        // 회피 중 살짝 투명
        else if (player.Movement.IsDodgeInvincible)
        {
            color.a = 0.6f;
        }
        bodyRenderer.color = color;

        outline.enabled = now < outlineUntil;
    }

    private void OnPlayerHit(int damage) => hitTintUntil = Time.unscaledTime + GameBalance.Current.Feedback.PlayerHitTintDuration;
    private void OnJustDodge() => outlineUntil = Time.unscaledTime + 0.25f;
}
