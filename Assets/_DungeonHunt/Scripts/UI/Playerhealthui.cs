using UnityEngine;
using UnityEngine.UI;

public class PlayerHealthUI : MonoBehaviour
{
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private Slider healthSlider;

    private void Start()
    {
        playerHealth.HPChanged += OnHPChanged;

        healthSlider.maxValue = playerHealth.MaxHP;
        healthSlider.value = playerHealth.CurrentHP;
    }
    private void OnEnable()
    {
        
    }

    private void OnDisable()
    {
        playerHealth.HPChanged -= OnHPChanged;
    }

    private void OnHPChanged(int currentHP)
    {
        healthSlider.value = currentHP;
    }
}