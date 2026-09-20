using System;
using System.Collections.Generic;
using UnityEngine;

public class RoomController : MonoBehaviour, IRoomEntryHandler
{
    [SerializeField] private List<DummyEnemy> enemies = new();
    [SerializeField] private List<Door> doorsToLock = new();

    private List<DummyEnemy> remaining = new();
    private bool entered;

    public event Action RoomCleared;

    private void Awake()
    {
        foreach (var enemy in enemies)
        {
            enemy.gameObject.SetActive(false);
            enemy.Died += OnEnemyDied;
        }
    }

    public void EnterRoom()
    {
        if (entered) return;
        entered = true;

        if (enemies.Count == 0)
        {
            foreach (var door in doorsToLock) door.Unlock();
            return;
        }

        foreach (var door in doorsToLock) door.Lock();

        remaining = new List<DummyEnemy>(enemies);
        foreach (var enemy in enemies) enemy.gameObject.SetActive(true);
    }

    private void OnEnemyDied(DummyEnemy enemy)
    {
        remaining.Remove(enemy);
        if (remaining.Count == 0)
        {
            foreach (var door in doorsToLock) door.Unlock();
            PlaytestLogger.Log("RoomCleared", gameObject.name);
            PlaytestLogger.SaveProgress();
            RoomCleared?.Invoke();
        }
    }
}