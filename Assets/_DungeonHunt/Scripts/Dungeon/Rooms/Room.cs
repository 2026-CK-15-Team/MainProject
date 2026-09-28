using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

// 방 프리팹 루트 컴포넌트. 크기랑 문 정보는 인스펙터 Bake 버튼으로 타일맵에서 읽어 저장함.
public class Room : MonoBehaviour
{
    [SerializeField] private RoomTemplateData template = new RoomTemplateData();
    [SerializeField] private Tilemap floorTilemap;
    [SerializeField] private Tilemap wallTilemap;

    public RoomTemplateData Template => template;
    public Tilemap FloorTilemap => floorTilemap;
    public Tilemap WallTilemap => wallTilemap;

    // 층에서의 위치랑 연결 정보. 생성기가 배치한 방에만 있음.
    public RoomNode Node { get; private set; }

    // 배치 직후 호출. 막을 문 칸은 벽으로 채우고, 안 이어진 방향 장벽은 끔.
    public void Initialize(RoomNode node, IReadOnlyList<Vector2Int> sealedCells, TileBase wallTile)
    {
        Node = node;

        foreach (Vector2Int cell in sealedCells)
        {
            floorTilemap.SetTile((Vector3Int)cell, null);
            wallTilemap.SetTile((Vector3Int)cell, wallTile);
        }

        var connectedSides = new HashSet<RoomSide>();
        foreach (RoomNode neighbor in node.Neighbors)
            connectedSides.Add(node.SideTowards(neighbor));

        foreach (RoomDoor door in GetComponentsInChildren<RoomDoor>(true))
        {
            bool isConnected = connectedSides.Contains(door.Side);
            door.gameObject.SetActive(isConnected);
            if (isConnected)
                door.SetLocked(false);
        }
    }

    private void Reset()
    {
        foreach (Tilemap tilemap in GetComponentsInChildren<Tilemap>(true))
        {
            if (tilemap.name == "Floor")
                floorTilemap = tilemap;
            else if (tilemap.name == "Wall")
                wallTilemap = tilemap;
        }
    }
}
