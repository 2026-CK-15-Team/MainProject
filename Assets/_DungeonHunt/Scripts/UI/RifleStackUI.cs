using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RifleStackUI : MonoBehaviour
{
    [SerializeField] private WeaponController weaponController;
    [SerializeField] private GameObject root; // 켜고 끌 대상. 이 스크립트가 붙은 오브젝트와 달라야 함
    [SerializeField] private TMP_Text stackText;
    [SerializeField] private Slider timeoutSlider;

    private void Update()
    {
        var rifle = weaponController.GetWeapon(WeaponType.Rifle);
        var stack = rifle?.RifleStack;

        bool shouldShow = stack != null && stack.Enabled && stack.CurrentStack > 0;
        root.SetActive(shouldShow);

        if (!shouldShow) return;

        stackText.text = $"{stack.CurrentStack}/5";
        timeoutSlider.value = stack.TimeRemaining / stack.TimeoutDuration;
    }
}
