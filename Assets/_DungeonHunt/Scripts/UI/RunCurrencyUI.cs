using UnityEngine;
using TMPro;

public class RunCurrencyUI : MonoBehaviour
{
    [SerializeField] private TMP_Text text;

    private void OnEnable()
    {
        RunCurrency.Changed += OnChanged;
        OnChanged(RunCurrency.Amount);
    }

    private void OnDisable() => RunCurrency.Changed -= OnChanged;

    private void OnChanged(int amount) => text.text = amount.ToString();
}
