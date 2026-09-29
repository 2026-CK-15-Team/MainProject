using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class FinalCombatController : MonoBehaviour, IRoomEntryHandler
{
    [SerializeField] private List<DummyEnemy> wave1 = new();
    [SerializeField] private List<DummyEnemy> wave2 = new();
    [SerializeField] private List<Door> doorsToLock = new();
    [SerializeField] private GameObject endingChest;
    [SerializeField] private float wave2TelegraphDuration = 1.0f;
    [SerializeField] private EntrySafeZoneValidator safeZoneValidator; // 1웨이브 최초 배치 검증용, 없으면 생략

    private List<DummyEnemy> remaining = new();
    private bool wave1Cleared;
    private bool entered;

    private void Awake()
    {
        foreach (var enemy in wave1)
        {
            enemy.gameObject.SetActive(false);
            enemy.Died += OnWave1Died;
        }
        foreach (var enemy in wave2)
        {
            enemy.gameObject.SetActive(false);
            enemy.Died += OnWave2Died;
        }

        if (endingChest != null) endingChest.SetActive(false);
    }

    public void EnterRoom()
    {
        if (entered) return;
        entered = true;

        foreach (var door in doorsToLock) door.Lock();

        if (safeZoneValidator != null)
            safeZoneValidator.Validate(wave1.Select(e => e.transform), $"{gameObject.name}-wave1");

        remaining = new List<DummyEnemy>(wave1);
        foreach (var enemy in wave1) enemy.gameObject.SetActive(true);
    }

    private void OnWave1Died(DummyEnemy enemy)
    {
        remaining.Remove(enemy);
        if (remaining.Count == 0 && !wave1Cleared)
        {
            wave1Cleared = true;
            PlaytestLogger.Log("Wave1Cleared", gameObject.name);
            StartCoroutine(SpawnWave2AfterTelegraph());
        }
    }

    private IEnumerator SpawnWave2AfterTelegraph()
    {
        FindObjectOfType<CenterToast>()?.Show("Wave 2 Incoming", wave2TelegraphDuration);

        float telegraphStart = Time.time;
        PlaytestLogger.Log("Wave2TelegraphStart", $"configured={wave2TelegraphDuration}");
        yield return new WaitForSeconds(wave2TelegraphDuration);

        remaining = new List<DummyEnemy>(wave2);
        foreach (var enemy in wave2) enemy.gameObject.SetActive(true);

        PlaytestLogger.Log("Wave2Spawned", $"elapsed={Time.time - telegraphStart:F3},configured={wave2TelegraphDuration}");
    }

    private void OnWave2Died(DummyEnemy enemy)
    {
        remaining.Remove(enemy);
        if (remaining.Count == 0)
        {
            foreach (var door in doorsToLock) door.Unlock();
            if (endingChest != null) endingChest.SetActive(true);

            PlaytestLogger.Log("RoomCleared", gameObject.name);
            PlaytestLogger.SaveProgress();
        }
    }
}