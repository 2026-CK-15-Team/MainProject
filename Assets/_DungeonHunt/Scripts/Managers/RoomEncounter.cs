using System;
using System.Collections.Generic;
using UnityEngine;

public class RoomEncounter : MonoBehaviour
{
    [SerializeField] private List<DummyEnemy> enemies = new List<DummyEnemy>();

    public event Action RoomCleared;

    private void Start()
    {
        foreach (var enemy in enemies)
            enemy.Died += OnEnemyDied;
    }

    private void OnEnemyDied(DummyEnemy enemy)
    {
        enemies.Remove(enemy);
        enemy.Died -= OnEnemyDied;

        if (enemies.Count == 0)
        {
            Debug.Log("Room Cleared!");
            RoomCleared?.Invoke();
        }
    }
}