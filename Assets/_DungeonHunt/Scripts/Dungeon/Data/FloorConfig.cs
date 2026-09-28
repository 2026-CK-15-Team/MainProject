using UnityEngine;
using UnityEngine.Tilemaps;

[CreateAssetMenu(fileName = "FloorConfig", menuName = "Dungeon/Floor Config")]
public class FloorConfig : ScriptableObject
{
    [Header("구조")]
    [Tooltip("이 층 전투방 수. 딱 이만큼만 생성됨.")]
    [Min(1)] public int CombatRoomCount = 8;

    [Tooltip("전투방끼리 순환 1개 만듦. 전투방 4개 이상 필요.")]
    public bool HasLoop;

    [Tooltip("보스방 뒤에 다음 층 계단방 붙임.")]
    public bool HasStairs = true;

    [Header("방")]
    [Tooltip("이 층에서 쓸 방 프리팹. 종류별로 최소 1개씩 있어야 함.")]
    public Room[] RoomPrefabs;

    [Tooltip("방 사이 최소 간격(칸). 복도 꺾을 공간(제일 넓은 문 폭 + 4)보다 작으면 알아서 늘어남.")]
    [Min(0)] public int MinRoomSpacing = 6;

    [Header("타일")]
    public TileBase CorridorFloorTile;

    [Tooltip("복도 벽이랑 안 쓰는 문 막을 때 씀.")]
    public TileBase WallTile;
}
