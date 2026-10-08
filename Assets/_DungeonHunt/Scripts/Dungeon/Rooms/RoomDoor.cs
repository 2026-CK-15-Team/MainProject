using UnityEngine;

// 문 자리에 두는 투명 장벽. 잠그면 못 지나감(전투 중 등).
// 잠금 이펙트(빨강, 03-1 §8.5): 벽과 똑같이 몸체(회피 포함)와 양쪽 투사체를 막음.
// lockVisual을 비워두면 장벽 콜라이더 크기대로 빨간 사각형을 만들어 씀.
[RequireComponent(typeof(Collider2D))]
public class RoomDoor : MonoBehaviour
{
    public static readonly Color LockColor = new Color(0.95f, 0.15f, 0.15f, 0.85f);

    [SerializeField] private RoomSide side;
    [SerializeField] private Collider2D barrier;
    [Tooltip("잠금 이펙트 오브젝트. 비우면 기본 빨간 사각형")]
    [SerializeField] private GameObject lockVisual;

    public RoomSide Side => side;
    public bool IsLocked => barrier != null && barrier.enabled;

    private void Awake()
    {
        if (barrier == null) barrier = GetComponent<Collider2D>();
        // 투사체/이동 판정이 벽 레이어를 보므로 장벽도 벽 레이어·태그로 맞춤
        int wallLayer = GameLayers.Wall;
        if (wallLayer >= 0) gameObject.layer = wallLayer;
        try { gameObject.tag = GameLayers.WallTag; } catch (UnityException) { }
    }

    public void SetLocked(bool locked)
    {
        if (barrier == null) barrier = GetComponent<Collider2D>();
        // 보이던 잠금 이펙트 기준 (초기화 때 SetLocked(false)로는 연출 안 나옴)
        bool wasVisible = lockVisual != null && lockVisual.activeSelf;
        barrier.enabled = locked;
        EnsureVisual();
        if (lockVisual != null) lockVisual.SetActive(locked);

        // 잠금 이펙트 등장/소멸 연출 (09 §3)
        if (wasVisible == locked || !Application.isPlaying || GameServices.Feedback == null) return;
        if (GameServices.Run != null && GameServices.Run.IsTransitioning) return; // 층 생성 중에는 연출 없음
        Bounds bounds = VisualBounds();
        if (locked) GameServices.Feedback.SpawnLockAppear(bounds);
        else GameServices.Feedback.SpawnLockRelease(bounds);
    }

    private Bounds VisualBounds()
    {
        if (lockVisual != null && lockVisual.TryGetComponent(out SpriteRenderer renderer)) return renderer.bounds;
        bool enabled = barrier.enabled;
        barrier.enabled = true;
        Bounds bounds = barrier.bounds;
        barrier.enabled = enabled;
        return bounds;
    }

    private void EnsureVisual()
    {
        if (lockVisual != null || barrier == null) return;
        Bounds bounds = barrier.bounds;
        if (!barrier.enabled)
        {
            barrier.enabled = true;
            bounds = barrier.bounds;
            barrier.enabled = false;
        }
        SpriteRenderer renderer = PlaceholderArt.CreateShape("LockEffect", transform, PlaceholderArt.Square, LockColor, 1f, 150);
        renderer.transform.position = bounds.center;
        Vector3 lossy = transform.lossyScale;
        renderer.transform.localScale = new Vector3(bounds.size.x / Mathf.Max(0.0001f, lossy.x), bounds.size.y / Mathf.Max(0.0001f, lossy.y), 1f);
        lockVisual = renderer.gameObject;
    }

    private void Reset() => barrier = GetComponent<Collider2D>();
}
