using System;
using UnityEngine;

// 03-3 런전용 재화. 몬스터 처치와 룸 보상으로 얻고 런이 끝나면 전부 사라짐.
public class RunWallet : MonoBehaviour
{
    public int Balance { get; private set; }
    public event Action<int> Changed;

    private void Awake() => GameServices.Wallet = this;

    public void ResetForNewRun()
    {
        if (Balance > 0) GameEvents.Raise(GameEvents.RunCurrencyLost, Balance);
        Balance = 0;
        Changed?.Invoke(Balance);
    }

    public void Add(int amount)
    {
        if (amount <= 0) return;
        Balance += amount;
        GameEvents.Raise(GameEvents.RunCurrencyGained, amount);
        Changed?.Invoke(Balance);
    }

    public bool CanAfford(int amount) => Balance >= amount;

    public bool TrySpend(int amount)
    {
        if (amount < 0 || Balance < amount) return false;
        Balance -= amount;
        Changed?.Invoke(Balance);
        return true;
    }

    // 런 종료: 잔여 재화 소멸 (03-3 §5.1 2번)
    public void LoseAll()
    {
        if (Balance > 0) GameEvents.Raise(GameEvents.RunCurrencyLost, Balance);
        Balance = 0;
        Changed?.Invoke(Balance);
    }
}
