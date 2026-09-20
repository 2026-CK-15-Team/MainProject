using System;
using UnityEngine;

public static class RunCurrency
{
    public static int Amount { get; private set; }

    public static event Action<int> Changed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ResetOnPlay()
    {
        Amount = 0;
        Changed = null;
    }

    public static void Gain(int amount)
    {
        Amount += amount;
        Changed?.Invoke(Amount);
        PlaytestLogger.Log("CurrencyGain", $"amount={amount},total={Amount}");
    }

    public static bool TrySpend(int amount)
    {
        if (Amount < amount) return false;

        Amount -= amount;
        Changed?.Invoke(Amount);
        PlaytestLogger.Log("CurrencySpend", $"amount={amount},total={Amount}");
        return true;
    }

    public static void Reset()
    {
        Debug.Log("[RunCurrency] 초기화됨 (0으로)");
        Amount = 0;
        Changed?.Invoke(Amount);
    }
}