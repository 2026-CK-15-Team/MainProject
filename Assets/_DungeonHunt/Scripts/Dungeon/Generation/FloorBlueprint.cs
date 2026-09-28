using System.Collections.Generic;
using UnityEngine;

// 층을 실제 타일 좌표로 펼친 결과. 이대로 방 만들고 복도 칠하면 끝.
public class FloorBlueprint
{
    private readonly List<PlacedRoom> rooms;
    private readonly List<Corridor> corridors = new List<Corridor>();

    public FloorBlueprint(DungeonLayout layout, List<PlacedRoom> rooms)
    {
        Layout = layout;
        this.rooms = rooms;
    }

    public DungeonLayout Layout { get; }

    // 방 Id 순서
    public IReadOnlyList<PlacedRoom> Rooms => rooms;
    public IReadOnlyList<Corridor> Corridors => corridors;
    public HashSet<Vector2Int> CorridorFloor { get; } = new HashSet<Vector2Int>();
    public HashSet<Vector2Int> CorridorWalls { get; } = new HashSet<Vector2Int>();

    public bool IsInsideRoom(Vector2Int cell) => rooms.Exists(room => room.Bounds.Contains(cell));

    public void AddCorridor(Corridor corridor)
    {
        corridors.Add(corridor);
        CorridorFloor.UnionWith(corridor.Cells);
    }
}

public class PlacedRoom
{
    // 복도랑 실제로 이어진 문 구간 (변 따라 시작 칸, 폭)
    private readonly Dictionary<RoomSide, (int start, int width)> openings = new Dictionary<RoomSide, (int start, int width)>();

    public PlacedRoom(RoomNode node, int templateIndex, RoomTemplateData template, Vector2Int position)
    {
        Node = node;
        TemplateIndex = templateIndex;
        Template = template;
        Position = position;
    }

    public RoomNode Node { get; }
    public int TemplateIndex { get; }
    public RoomTemplateData Template { get; }

    // 바운딩 사각형 왼쪽 아래의 층 좌표
    public Vector2Int Position { get; }
    public RectInt Bounds => new RectInt(Position, Template.Size);

    public void OpenDoorway(RoomSide side, int start, int width) => openings[side] = (start, width);

    // 막아야 할 문 칸(방 기준 좌표). 안 쓰는 문 전체 + 복도보다 넓은 문에서 남는 칸.
    public List<Vector2Int> GetSealedCells()
    {
        var cells = new List<Vector2Int>();
        foreach (Doorway doorway in Template.Doorways)
        {
            bool isOpen = openings.TryGetValue(doorway.Side, out (int start, int width) opening);
            for (int i = 0; i < doorway.Width; i++)
            {
                int along = doorway.Start + i;
                if (isOpen && along >= opening.start && along < opening.start + opening.width) continue;

                cells.Add(doorway.GetCell(Template.Size, i));
            }
        }
        return cells;
    }
}

public class Corridor
{
    public Corridor(RoomLink link, List<Vector2Int> cells)
    {
        Link = link;
        Cells = cells;
    }

    public RoomLink Link { get; }
    public IReadOnlyList<Vector2Int> Cells { get; }
}
