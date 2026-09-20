using UnityEngine;

public class RoomRewardSpawner : MonoBehaviour
{
    [SerializeField] private RoomController room;
    [SerializeField] private Transform rewardSpawnPoint;
    [SerializeField] private GameObject fieldArtifactPrefab;
    [SerializeField] private GameObject fieldCurrencyPrefab;
    [SerializeField] private GameObject fieldHPPrefab;
    [SerializeField] private ArtifactCatalogData catalog;
    [SerializeField] private bool guaranteedArtifact;

    public int CurrencyAmount = 6;
    public int HPAmount = 1;

    private void OnEnable() => room.RoomCleared += OnRoomCleared;
    private void OnDisable() => room.RoomCleared -= OnRoomCleared;

    private void OnRoomCleared()
    {
        if (guaranteedArtifact)
        {
            SpawnArtifact();
            return;
        }

        float roll = Random.value;

        if (roll < 0.20f) SpawnArtifact();
        else if (roll < 0.75f) SpawnCurrency();
        else SpawnHP();
    }

    private void SpawnArtifact()
    {
        if (fieldArtifactPrefab == null) return;

        GameObject go = Instantiate(fieldArtifactPrefab, rewardSpawnPoint.position, Quaternion.identity);

        if (guaranteedArtifact && RunProgressState.TreasureSetType.HasValue
            && go.TryGetComponent<FieldArtifact>(out var field))
        {
            var artifacts = FindObjectOfType<PlayerArtifacts>();
            var targeted = ArtifactDrawer.DrawTargeted(catalog, artifacts, RunProgressState.TreasureSetType.Value);
            field.SetDefinition(targeted);
        }
    }

    private void SpawnCurrency()
    {
        if (fieldCurrencyPrefab == null) return;
        GameObject go = Instantiate(fieldCurrencyPrefab, rewardSpawnPoint.position, Quaternion.identity);
        if (go.TryGetComponent<FieldCurrency>(out var currency))
            currency.Amount = CurrencyAmount;
    }

    private void SpawnHP()
    {
        if (fieldHPPrefab == null) return;
        GameObject go = Instantiate(fieldHPPrefab, rewardSpawnPoint.position, Quaternion.identity);
        if (go.TryGetComponent<FieldHP>(out var hp))
            hp.Amount = HPAmount;
    }
}