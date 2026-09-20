using System;
using UnityEngine;

public class EndingChest : MonoBehaviour, IInteractable
{
    public event Action Opened;

    public void Interact()
    {
        Debug.Log("RunCleared");
        PlaytestLogger.FinalizeRun("RunCleared");
        RunCurrency.Reset();
        RunProgressState.Reset();
        FindObjectOfType<ResultScreenController>()?.ShowCleared();
        Opened?.Invoke();
        gameObject.SetActive(false);
    }
}