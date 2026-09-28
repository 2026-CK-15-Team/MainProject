using UnityEngine;

[CreateAssetMenu(fileName = "RunConfig", menuName = "Dungeon/Run Config")]
public class RunConfig : ScriptableObject
{
    [Tooltip("1층부터 순서대로")]
    public FloorConfig[] Floors;

    [Tooltip("런 전체 도박방 1개, 이 확률이면 2개")]
    [Range(0f, 1f)] public float DoubleGambleChance = 0.35f;

    [Tooltip("런 전체 비밀방 1개, 이 확률이면 2개")]
    [Range(0f, 1f)] public float DoubleSecretChance = 0.3f;

    [Tooltip("벽 두께 규칙. 방 만들 때도 이 규칙대로 그려야 함")]
    public WallStyle WallStyle = new WallStyle();
}
