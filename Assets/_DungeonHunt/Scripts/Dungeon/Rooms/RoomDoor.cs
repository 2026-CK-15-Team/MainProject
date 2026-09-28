using UnityEngine;

// 문 자리에 두는 투명 장벽. 잠그면 못 지나감(전투 중 등).
[RequireComponent(typeof(Collider2D))]
public class RoomDoor : MonoBehaviour
{
    [SerializeField] private RoomSide side;
    [SerializeField] private Collider2D barrier;

    public RoomSide Side => side;
    public bool IsLocked => barrier.enabled;

    public void SetLocked(bool locked) => barrier.enabled = locked;

    private void Reset() => barrier = GetComponent<Collider2D>();
}
