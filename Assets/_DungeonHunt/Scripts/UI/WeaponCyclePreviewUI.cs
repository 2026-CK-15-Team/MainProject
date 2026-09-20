using UnityEngine;
using UnityEngine.UI;

public class WeaponCyclePreviewUI : MonoBehaviour
{
    [System.Serializable]
    public struct SlotUI
    {
        public WeaponType Type;
        public Image Icon;
        public GameObject LockOverlay;
    }

    [SerializeField] private WeaponController weaponController;
    [SerializeField] private SlotUI[] slots; // 인스펙터에서 Rifle/Pistol/Shotgun 3개를 채운다

    private void Update()
    {
        foreach (var slot in slots)
        {
            bool locked = weaponController.IsLocked(slot.Type);

            if (slot.Icon != null)
                slot.Icon.color = locked ? new Color(1f, 1f, 1f, 0.4f) : Color.white;

            if (slot.LockOverlay != null)
                slot.LockOverlay.SetActive(locked);
        }
    }
}
