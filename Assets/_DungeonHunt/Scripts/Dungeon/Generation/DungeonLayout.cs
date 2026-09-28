using System;
using System.Collections.Generic;
using UnityEngine;

// 한 층 방 연결 구조. 방 하나가 격자 한 칸 차지하고, 격자에서 맞닿은 방끼리만 문으로 이어짐.
// 실제 크기랑 위치는 여기 없음. 배치 단계에서 정함.
public class DungeonLayout
{
    private readonly List<RoomNode> rooms = new List<RoomNode>();
    private readonly List<RoomLink> links = new List<RoomLink>();
    private readonly Dictionary<Vector2Int, RoomNode> roomByCell = new Dictionary<Vector2Int, RoomNode>();

    public DungeonLayout(FloorPlan plan)
    {
        Plan = plan;
    }

    public FloorPlan Plan { get; }
    public IReadOnlyList<RoomNode> Rooms => rooms;
    public IReadOnlyList<RoomLink> Links => links;

    public bool IsOccupied(Vector2Int cell) => roomByCell.ContainsKey(cell);

    public int CountOccupiedNeighbors(Vector2Int cell)
    {
        int count = 0;
        foreach (RoomSide side in RoomSideUtility.All)
        {
            if (IsOccupied(cell + side.ToOffset()))
                count++;
        }
        return count;
    }

    public RoomNode AddRoom(RoomType type, Vector2Int cell)
    {
        var room = new RoomNode(rooms.Count, type, cell);
        rooms.Add(room);
        roomByCell.Add(cell, room);
        return room;
    }

    public void Link(RoomNode a, RoomNode b)
    {
        if (Mathf.Abs(a.Cell.x - b.Cell.x) + Mathf.Abs(a.Cell.y - b.Cell.y) != 1)
            throw new ArgumentException($"안 맞닿은 방은 연결 불가: {a.Cell} - {b.Cell}");

        links.Add(new RoomLink(a, b));
        a.AddNeighbor(b);
        b.AddNeighbor(a);
    }

    // 문 따라 이동한 거리. 못 가는 방은 결과에 없음.
    public Dictionary<RoomNode, int> GetDistancesFrom(RoomNode origin)
    {
        var distances = new Dictionary<RoomNode, int> { [origin] = 0 };
        var queue = new Queue<RoomNode>();
        queue.Enqueue(origin);

        while (queue.Count > 0)
        {
            RoomNode room = queue.Dequeue();
            foreach (RoomNode neighbor in room.Neighbors)
            {
                if (distances.ContainsKey(neighbor)) continue;

                distances[neighbor] = distances[room] + 1;
                queue.Enqueue(neighbor);
            }
        }
        return distances;
    }
}

public class RoomNode
{
    private readonly List<RoomNode> neighbors = new List<RoomNode>();

    public RoomNode(int id, RoomType type, Vector2Int cell)
    {
        Id = id;
        Type = type;
        Cell = cell;
    }

    public int Id { get; }
    public RoomType Type { get; }
    public Vector2Int Cell { get; }

    // 문으로 연결된 방
    public IReadOnlyList<RoomNode> Neighbors => neighbors;
    public int DoorCount => neighbors.Count;

    public RoomSide SideTowards(RoomNode other) => RoomSideUtility.FromOffset(other.Cell - Cell);

    internal void AddNeighbor(RoomNode room) => neighbors.Add(room);
}

public readonly struct RoomLink
{
    public RoomLink(RoomNode a, RoomNode b)
    {
        A = a;
        B = b;
    }

    public RoomNode A { get; }
    public RoomNode B { get; }
}
