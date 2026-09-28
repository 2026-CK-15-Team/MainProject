using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

// 방 프리팹 루트 컴포넌트. 크기랑 문 정보는 인스펙터 Bake 버튼으로 타일맵에서 읽어 저장함.
public class Room : MonoBehaviour
{
    [SerializeField] private RoomTemplateData template = new RoomTemplateData();
    [SerializeField] private Tilemap floorTilemap;

    [Tooltip("충돌 있는 벽")]
    [SerializeField] private Tilemap wallTilemap;

    [Tooltip("아래 벽 중 캐릭터를 가리는 칸. 충돌 없음, 캐릭터보다 위에 그림. 비워두면 Wall에 칠함(충돌 생김)")]
    [SerializeField] private Tilemap wallFrontTilemap;

    public RoomTemplateData Template => template;
    public Tilemap FloorTilemap => floorTilemap;
    public Tilemap WallTilemap => wallTilemap;
    public Tilemap WallFrontTilemap => wallFrontTilemap;

    // 층에서의 위치랑 연결 정보. 생성기가 배치한 방에만 있음.
    public RoomNode Node { get; private set; }

    // 배치 직후 호출. 안 이어진 방향 장벽은 끄고, 이어진 문은 잠금 풀어둠.
    public void Initialize(RoomNode node)
    {
        Node = node;

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
            switch (tilemap.name)
            {
                case "Floor": floorTilemap = tilemap; break;
                case "Wall": wallTilemap = tilemap; break;
                case "WallFront": wallFrontTilemap = tilemap; break;
            }
        }
    }
}
