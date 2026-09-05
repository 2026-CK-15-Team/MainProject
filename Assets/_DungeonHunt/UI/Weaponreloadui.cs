using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class WeaponReloadUI : MonoBehaviour
{
    [SerializeField] private WeaponController weaponController;
    [SerializeField] private Slider reloadSlider;
    [SerializeField] private TMP_Text ammoText;

    private void Update()
    {
        WeaponRuntime current = weaponController.CurrentWeapon;

        reloadSlider.gameObject.SetActive(current.IsReloading);
        reloadSlider.value = current.ReloadProgress01;

        if (ammoText != null)
            ammoText.text = $"{current.Ammo} / {current.Definition.MaxAmmo}";
    }
}