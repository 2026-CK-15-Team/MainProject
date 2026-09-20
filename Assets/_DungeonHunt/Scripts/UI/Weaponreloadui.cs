using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class WeaponReloadUI : MonoBehaviour
{
    [SerializeField] private WeaponController weaponController;
    [SerializeField] private Image weaponIcon;
    [SerializeField] private Outline enhancedOutline;
    [SerializeField] private Image swapDelayFill;
    [SerializeField] private Slider reloadSlider;
    [SerializeField] private TMP_Text ammoText;

    private void Update()
    {
        WeaponRuntime current = weaponController.CurrentWeapon;

        if (weaponIcon != null)
            weaponIcon.sprite = current.Definition.Icon;

        if (enhancedOutline != null)
            enhancedOutline.enabled = current.IsMagazineEnhanced;

        if (swapDelayFill != null)
            swapDelayFill.fillAmount = current.SwapDelayProgress01;

        reloadSlider.gameObject.SetActive(current.IsReloading);
        reloadSlider.value = current.ReloadProgress01;

        if (ammoText != null)
            ammoText.text = $"{current.Ammo} / {current.EffectiveMaxAmmo}";
    }
}