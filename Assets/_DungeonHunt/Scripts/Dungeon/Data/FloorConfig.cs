using System;
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

    [Tooltip("방 사이 최소 간격(칸). 복도 꺾을 공간(문 폭 + 벽 두께)보다 작으면 알아서 늘어남.")]
    [Min(0)] public int MinRoomSpacing = 6;

    [Header("타일")]
    public TileBase CorridorFloorTile;

    [Tooltip("바닥 위쪽 벽 (벽면 + 천장 닿는 칸). 복도 벽이랑 안 쓰는 문 막을 때 씀")]
    public TileBase TopWallTile;

    [Tooltip("바닥 아래쪽 벽의 천장 닿는 칸 (충돌)")]
    public TileBase BottomWallTile;

    [Tooltip("바닥 아래쪽 벽이 바닥 위로 겹쳐 캐릭터 가리는 칸 (충돌 없음)")]
    public TileBase BottomWallFrontTile;

    [Tooltip("좌우 벽")]
    public TileBase SideWallTile;

    public TileBase GetWallTile(WallCell wall) => wall.Part switch
    {
        WallPart.Top => TopWallTile,
        WallPart.Bottom => wall.IsFront ? BottomWallFrontTile : BottomWallTile,
        WallPart.Side => SideWallTile,
        _ => throw new ArgumentOutOfRangeException(nameof(wall))
    };
}
