using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class RoomEntryTrigger : MonoBehaviour
{
    [SerializeField] private MonoBehaviour roomComponent;
    private IRoomEntryHandler room;
    private bool triggered;

    private void Awake()
    {
        room = roomComponent as IRoomEntryHandler;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (triggered) return;
        if (!other.TryGetComponent<PlayerHealth>(out _)) return;

        triggered = true;
        PlaytestLogger.Log("RoomEntered", gameObject.name);
        room?.EnterRoom();
    }
}