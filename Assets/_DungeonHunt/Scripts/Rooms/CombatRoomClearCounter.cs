using UnityEngine;

public class CombatRoomClearCounter : MonoBehaviour
{
    [SerializeField] private RoomController room;

    private void OnEnable() => room.RoomCleared += OnCleared;
    private void OnDisable() => room.RoomCleared -= OnCleared;

    private void OnCleared()
    {
        RunStats.RegisterCombatRoomCleared();
        FindObjectOfType<CenterToast>()?.Show("Cleared!", 1f);
    }
}