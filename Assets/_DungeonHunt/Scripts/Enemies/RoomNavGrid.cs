using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

// 룸 하나의 이동 가능 칸 격자와 A* 경로 탐색 (04 §9.1).
// 걸을 수 있는 칸 = 바닥 타일 있음 + 벽 타일 없음 + 장애물(벽 레이어 콜라이더)과 안 겹침.
public class RoomNavGrid
{
    private readonly bool[,] walkable;
    private readonly Vector2Int size;
    private readonly Vector2 origin; // 칸 (0,0)의 왼쪽 아래 월드 좌표

    public RoomNavGrid(Room room)
    {
        size = room.Template.Size;
        origin = room.transform.position;
        walkable = new bool[Mathf.Max(1, size.x), Mathf.Max(1, size.y)];
        Tilemap floor = room.FloorTilemap;
        Tilemap wall = room.WallTilemap;
        Vector2 boxSize = new Vector2(0.6f, 0.6f);

        for (int x = 0; x < size.x; x++)
        {
            for (int y = 0; y < size.y; y++)
            {
                var cell = new Vector3Int(x, y, 0);
                bool isFloor = floor != null && floor.HasTile(cell);
                bool isWall = wall != null && wall.HasTile(cell);
                bool ok = isFloor && !isWall;
                if (ok && Physics2D.OverlapBox(CellCenter(x, y), boxSize, 0f, GameLayers.WallMask) != null)
                    ok = false;
                walkable[x, y] = ok;
            }
        }
    }

    public Vector2Int Size => size;

    public Vector2 CellCenter(int x, int y) => origin + new Vector2(x + 0.5f, y + 0.5f);
    public Vector2 CellCenter(Vector2Int cell) => CellCenter(cell.x, cell.y);

    public Vector2Int WorldToCell(Vector2 world)
    {
        Vector2 local = world - origin;
        return new Vector2Int(Mathf.FloorToInt(local.x), Mathf.FloorToInt(local.y));
    }

    public bool InBounds(Vector2Int c) => c.x >= 0 && c.y >= 0 && c.x < size.x && c.y < size.y;
    public bool IsWalkable(Vector2Int c) => InBounds(c) && walkable[c.x, c.y];

    public IEnumerable<Vector2Int> WalkableCells()
    {
        for (int x = 0; x < size.x; x++)
            for (int y = 0; y < size.y; y++)
                if (walkable[x, y]) yield return new Vector2Int(x, y);
    }

    // 가장 가까운 걸을 수 있는 칸 (BFS)
    public bool TryGetNearestWalkable(Vector2Int from, out Vector2Int result)
    {
        if (IsWalkable(from)) { result = from; return true; }
        var visited = new HashSet<Vector2Int>();
        var queue = new Queue<Vector2Int>();
        Vector2Int start = new Vector2Int(Mathf.Clamp(from.x, 0, size.x - 1), Mathf.Clamp(from.y, 0, size.y - 1));
        queue.Enqueue(start);
        visited.Add(start);
        while (queue.Count > 0)
        {
            Vector2Int c = queue.Dequeue();
            if (IsWalkable(c)) { result = c; return true; }
            foreach (Vector2Int d in Neighbors4)
            {
                Vector2Int n = c + d;
                if (InBounds(n) && visited.Add(n)) queue.Enqueue(n);
            }
        }
        result = from;
        return false;
    }

    private static readonly Vector2Int[] Neighbors4 = { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };
    private static readonly Vector2Int[] Neighbors8 =
    {
        Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left,
        new Vector2Int(1, 1), new Vector2Int(1, -1), new Vector2Int(-1, -1), new Vector2Int(-1, 1)
    };

    // 경로를 월드 좌표 웨이포인트로. 목표 칸이 못 가는 칸이면 가장 가까운 도달 가능 칸으로.
    public bool FindPath(Vector2 fromWorld, Vector2 toWorld, List<Vector2> result)
    {
        result.Clear();
        if (!TryGetNearestWalkable(WorldToCell(fromWorld), out Vector2Int start)) return false;
        if (!TryGetNearestWalkable(WorldToCell(toWorld), out Vector2Int goal)) return false;
        if (start == goal)
        {
            result.Add(toWorld);
            return true;
        }

        var open = new List<Vector2Int> { start };
        var cameFrom = new Dictionary<Vector2Int, Vector2Int>();
        var g = new Dictionary<Vector2Int, float> { [start] = 0f };
        var closed = new HashSet<Vector2Int>();
        Vector2Int bestReached = start;
        float bestH = Heuristic(start, goal);
        int guard = size.x * size.y * 4;

        while (open.Count > 0 && guard-- > 0)
        {
            int bestIndex = 0;
            float bestF = float.MaxValue;
            for (int i = 0; i < open.Count; i++)
            {
                float f = g[open[i]] + Heuristic(open[i], goal);
                if (f < bestF) { bestF = f; bestIndex = i; }
            }
            Vector2Int current = open[bestIndex];
            open.RemoveAt(bestIndex);
            if (current == goal) { bestReached = goal; break; }
            closed.Add(current);

            float h = Heuristic(current, goal);
            if (h < bestH) { bestH = h; bestReached = current; }

            foreach (Vector2Int d in Neighbors8)
            {
                Vector2Int next = current + d;
                if (!IsWalkable(next) || closed.Contains(next)) continue;
                // 대각선은 모서리 끼임 방지
                if (d.x != 0 && d.y != 0 && (!IsWalkable(current + new Vector2Int(d.x, 0)) || !IsWalkable(current + new Vector2Int(0, d.y)))) continue;
                float cost = g[current] + (d.x != 0 && d.y != 0 ? 1.4142f : 1f);
                if (g.TryGetValue(next, out float existing) && existing <= cost) continue;
                g[next] = cost;
                cameFrom[next] = current;
                if (!open.Contains(next)) open.Add(next);
            }
        }

        var cells = new List<Vector2Int>();
        Vector2Int step = bestReached;
        cells.Add(step);
        while (cameFrom.TryGetValue(step, out Vector2Int prev))
        {
            step = prev;
            cells.Add(step);
        }
        cells.Reverse();
        for (int i = 1; i < cells.Count; i++)
            result.Add(CellCenter(cells[i]));
        if (bestReached == goal)
            result.Add(toWorld);
        return result.Count > 0;
    }

    private static float Heuristic(Vector2Int a, Vector2Int b)
    {
        int dx = Mathf.Abs(a.x - b.x), dy = Mathf.Abs(a.y - b.y);
        return Mathf.Max(dx, dy) + 0.4142f * Mathf.Min(dx, dy);
    }
}
